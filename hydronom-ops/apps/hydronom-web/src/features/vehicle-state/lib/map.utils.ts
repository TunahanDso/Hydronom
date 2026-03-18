import type { Vec2 } from "../../../shared/types/common.types";

export interface MapBounds {
  minX: number;
  maxX: number;
  minY: number;
  maxY: number;
}

const DEFAULT_HALF_SPAN = 10;

// Noktanın sayısal olarak geçerli olup olmadığını kontrol eder
export function isFinitePoint(point: Vec2 | null | undefined): point is Vec2 {
  return !!point && Number.isFinite(point.x) && Number.isFinite(point.y);
}

// Geçersiz noktaları ayıklar
export function sanitizePoints(points: Vec2[]): Vec2[] {
  return points.filter(isFinitePoint);
}

// Noktaları panel koordinat sistemine taşımak için sınır kutusu üretir
export function computeBounds(points: Vec2[]): MapBounds {
  const validPoints = sanitizePoints(points);

  if (validPoints.length === 0) {
    return {
      minX: -DEFAULT_HALF_SPAN,
      maxX: DEFAULT_HALF_SPAN,
      minY: -DEFAULT_HALF_SPAN,
      maxY: DEFAULT_HALF_SPAN
    };
  }

  const xs = validPoints.map((point) => point.x);
  const ys = validPoints.map((point) => point.y);

  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);

  const spanX = Math.max(maxX - minX, DEFAULT_HALF_SPAN * 0.4);
  const spanY = Math.max(maxY - minY, DEFAULT_HALF_SPAN * 0.4);

  const padX = Math.max(spanX * 0.18, 1.5);
  const padY = Math.max(spanY * 0.18, 1.5);

  return {
    minX: minX - padX,
    maxX: maxX + padX,
    minY: minY - padY,
    maxY: maxY + padY
  };
}

// Bounds merkezini verir
export function getBoundsCenter(bounds: MapBounds): Vec2 {
  return {
    x: (bounds.minX + bounds.maxX) * 0.5,
    y: (bounds.minY + bounds.maxY) * 0.5
  };
}

// Bounds genişlik/yüksekliğini verir
export function getBoundsSpan(bounds: MapBounds) {
  return {
    spanX: Math.max(bounds.maxX - bounds.minX, 0.0001),
    spanY: Math.max(bounds.maxY - bounds.minY, 0.0001)
  };
}

// Haritada aspect ratio korunacak şekilde bounds düzeltir
export function fitBoundsToViewport(
  bounds: MapBounds,
  width: number,
  height: number,
  padding = 24
): MapBounds {
  const usableWidth = Math.max(1, width - padding * 2);
  const usableHeight = Math.max(1, height - padding * 2);

  const viewportAspect = usableWidth / usableHeight;
  const center = getBoundsCenter(bounds);
  const { spanX, spanY } = getBoundsSpan(bounds);

  let finalSpanX = spanX;
  let finalSpanY = spanY;

  const boundsAspect = spanX / spanY;

  if (boundsAspect > viewportAspect) {
    finalSpanY = spanX / viewportAspect;
  } else {
    finalSpanX = spanY * viewportAspect;
  }

  return {
    minX: center.x - finalSpanX * 0.5,
    maxX: center.x + finalSpanX * 0.5,
    minY: center.y - finalSpanY * 0.5,
    maxY: center.y + finalSpanY * 0.5
  };
}

// Dünya birimini piksele çevirir
export function getPixelsPerWorldUnit(
  bounds: MapBounds,
  width: number,
  height: number,
  padding = 24
) {
  const usableWidth = Math.max(1, width - padding * 2);
  const usableHeight = Math.max(1, height - padding * 2);
  const { spanX, spanY } = getBoundsSpan(bounds);

  return Math.min(usableWidth / spanX, usableHeight / spanY);
}

// Dünya koordinatını panel koordinatına dönüştürür
export function worldToPanel(
  point: Vec2,
  bounds: MapBounds,
  width: number,
  height: number,
  padding = 24
) {
  const fittedBounds = fitBoundsToViewport(bounds, width, height, padding);

  const usableWidth = width - padding * 2;
  const usableHeight = height - padding * 2;

  const xRatio =
    fittedBounds.maxX === fittedBounds.minX
      ? 0.5
      : (point.x - fittedBounds.minX) / (fittedBounds.maxX - fittedBounds.minX);

  const yRatio =
    fittedBounds.maxY === fittedBounds.minY
      ? 0.5
      : (point.y - fittedBounds.minY) / (fittedBounds.maxY - fittedBounds.minY);

  return {
    x: padding + xRatio * usableWidth,
    y: height - (padding + yRatio * usableHeight)
  };
}

// Panel koordinatını dünya koordinatına geri dönüştürür
export function panelToWorld(
  point: Vec2,
  bounds: MapBounds,
  width: number,
  height: number,
  padding = 24
): Vec2 {
  const fittedBounds = fitBoundsToViewport(bounds, width, height, padding);

  const usableWidth = width - padding * 2;
  const usableHeight = height - padding * 2;

  const xRatio = usableWidth <= 0 ? 0.5 : (point.x - padding) / usableWidth;
  const yRatio = usableHeight <= 0 ? 0.5 : 1 - (point.y - padding) / usableHeight;

  return {
    x: fittedBounds.minX + xRatio * (fittedBounds.maxX - fittedBounds.minX),
    y: fittedBounds.minY + yRatio * (fittedBounds.maxY - fittedBounds.minY)
  };
}

// Çoklu noktayı panel koordinatına çevirir
export function projectPoints(
  points: Vec2[],
  bounds: MapBounds,
  width: number,
  height: number,
  padding = 24
): Vec2[] {
  return sanitizePoints(points).map((point) => worldToPanel(point, bounds, width, height, padding));
}

// Çoklu noktayı SVG polyline formatına çevirir
export function buildPolyline(
  points: Vec2[],
  bounds: MapBounds,
  width: number,
  height: number,
  padding = 24
) {
  return sanitizePoints(points)
    .map((point) => {
      const projected = worldToPanel(point, bounds, width, height, padding);
      return `${projected.x},${projected.y}`;
    })
    .join(" ");
}

// Noktalardan yaklaşık hücre boyu tahmini çıkarır
export function estimateCellSizeWorld(points: Vec2[]): number {
  const validPoints = sanitizePoints(points);

  if (validPoints.length < 2) {
    return 0.3;
  }

  const xs = new Set<number>();
  const ys = new Set<number>();

  for (const point of validPoints) {
    xs.add(Number(point.x.toFixed(3)));
    ys.add(Number(point.y.toFixed(3)));
  }

  const sortedX = Array.from(xs).sort((a, b) => a - b);
  const sortedY = Array.from(ys).sort((a, b) => a - b);

  let best = Number.POSITIVE_INFINITY;

  for (let i = 1; i < sortedX.length; i += 1) {
    const diff = sortedX[i] - sortedX[i - 1];
    if (diff > 0.0001 && diff < best) {
      best = diff;
    }
  }

  for (let i = 1; i < sortedY.length; i += 1) {
    const diff = sortedY[i] - sortedY[i - 1];
    if (diff > 0.0001 && diff < best) {
      best = diff;
    }
  }

  if (!Number.isFinite(best)) {
    return 0.3;
  }

  return Math.max(0.08, Math.min(best, 1.25));
}