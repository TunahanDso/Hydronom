import { useEffect, useRef } from "react";
import type { Vec2 } from "../../../shared/types/common.types";
import {
  computeBounds,
  estimateCellSizeWorld,
  getPixelsPerWorldUnit,
  isFinitePoint,
  worldToPanel
} from "../../../features/vehicle-state/lib/map.utils";

const CANVAS_WIDTH = 920;
const CANVAS_HEIGHT = 520;
const MIN_HALF_SPAN = 6;
const PANEL_PADDING = 24;

interface MissionMapCanvasProps {
  vehiclePosition: Vec2;
  headingDeg: number;
  trail: Vec2[];
  route: Vec2[];
  goal: Vec2 | null;
  obstacles: Array<{
    id: string;
    position: Vec2;
    radius: number;
  }>;
  lidarPoints: Vec2[];
  occupancyPreview?: Vec2[];
  occupancyCells?: Vec2[];
  ekfTrail?: Vec2[];
  ekfPose?: Vec2 | null;
  odomHint?: Vec2 | null;
}

interface MapBounds {
  minX: number;
  maxX: number;
  minY: number;
  maxY: number;
}

// Araç merkezli ama veri alanını da kaçırmayan daha stabil sınırlar üretir
function buildStableBounds(
  vehiclePosition: Vec2,
  trail: Vec2[],
  route: Vec2[],
  goal: Vec2 | null,
  ekfTrail: Vec2[],
  occupancyPreview: Vec2[],
  occupancyCells: Vec2[],
  lidarPoints: Vec2[],
  obstacles: Array<{
    id: string;
    position: Vec2;
    radius: number;
  }>
): MapBounds {
  const anchorPoints: Vec2[] = [];

  if (isFinitePoint(vehiclePosition)) {
    anchorPoints.push(vehiclePosition);
  }

  for (const point of trail) {
    if (isFinitePoint(point)) anchorPoints.push(point);
  }

  for (const point of route) {
    if (isFinitePoint(point)) anchorPoints.push(point);
  }

  for (const point of ekfTrail) {
    if (isFinitePoint(point)) anchorPoints.push(point);
  }

  for (const point of occupancyPreview) {
    if (isFinitePoint(point)) anchorPoints.push(point);
  }

  for (const point of occupancyCells) {
    if (isFinitePoint(point)) anchorPoints.push(point);
  }

  for (const point of lidarPoints) {
    if (isFinitePoint(point)) anchorPoints.push(point);
  }

  for (const obstacle of obstacles) {
    if (!isFinitePoint(obstacle.position)) {
      continue;
    }

    anchorPoints.push(obstacle.position);

    const r = Number.isFinite(obstacle.radius) ? Math.max(0, obstacle.radius) : 0;
    if (r > 0) {
      anchorPoints.push(
        { x: obstacle.position.x - r, y: obstacle.position.y },
        { x: obstacle.position.x + r, y: obstacle.position.y },
        { x: obstacle.position.x, y: obstacle.position.y - r },
        { x: obstacle.position.x, y: obstacle.position.y + r }
      );
    }
  }

  if (isFinitePoint(goal)) {
    anchorPoints.push(goal);
  }

  const safePoints = anchorPoints.length > 0 ? anchorPoints : [vehiclePosition];
  const raw = computeBounds(safePoints);

  const centerX =
    Number.isFinite(vehiclePosition.x) ? vehiclePosition.x : (raw.minX + raw.maxX) * 0.5;
  const centerY =
    Number.isFinite(vehiclePosition.y) ? vehiclePosition.y : (raw.minY + raw.maxY) * 0.5;

  const rawSpanX = Math.max(raw.maxX - raw.minX, MIN_HALF_SPAN * 2);
  const rawSpanY = Math.max(raw.maxY - raw.minY, MIN_HALF_SPAN * 2);

  const paddedSpanX = Math.max(rawSpanX * 1.18, MIN_HALF_SPAN * 2);
  const paddedSpanY = Math.max(rawSpanY * 1.18, MIN_HALF_SPAN * 2);

  const halfX = paddedSpanX * 0.5;
  const halfY = paddedSpanY * 0.5;

  return {
    minX: centerX - halfX,
    maxX: centerX + halfX,
    minY: centerY - halfY,
    maxY: centerY + halfY
  };
}

// roundRect desteklenmeyen ortamlarda fallback kullanır
function drawRoundedRect(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number
) {
  const r = Math.min(radius, width * 0.5, height * 0.5);

  ctx.beginPath();

  if (typeof ctx.roundRect === "function") {
    ctx.roundRect(x, y, width, height, r);
    return;
  }

  ctx.moveTo(x + r, y);
  ctx.lineTo(x + width - r, y);
  ctx.quadraticCurveTo(x + width, y, x + width, y + r);
  ctx.lineTo(x + width, y + height - r);
  ctx.quadraticCurveTo(x + width, y + height, x + width - r, y + height);
  ctx.lineTo(x + r, y + height);
  ctx.quadraticCurveTo(x, y + height, x, y + height - r);
  ctx.lineTo(x, y + r);
  ctx.quadraticCurveTo(x, y, x + r, y);
  ctx.closePath();
}

// Arka planı daha derin operasyon ekranı hissiyle çizer
function drawBackground(ctx: CanvasRenderingContext2D, width: number, height: number) {
  const bg = ctx.createLinearGradient(0, 0, 0, height);
  bg.addColorStop(0, "#020617");
  bg.addColorStop(0.55, "#06101d");
  bg.addColorStop(1, "#020617");

  ctx.save();
  ctx.fillStyle = bg;
  ctx.fillRect(0, 0, width, height);

  const glow = ctx.createRadialGradient(
    width * 0.5,
    height * 0.48,
    20,
    width * 0.5,
    height * 0.48,
    Math.max(width, height) * 0.65
  );
  glow.addColorStop(0, "rgba(56, 189, 248, 0.06)");
  glow.addColorStop(0.45, "rgba(34, 197, 94, 0.03)");
  glow.addColorStop(1, "rgba(2, 6, 23, 0)");

  ctx.fillStyle = glow;
  ctx.fillRect(0, 0, width, height);
  ctx.restore();
}

// Daha kaliteli grid görünümü
function drawGrid(ctx: CanvasRenderingContext2D, width: number, height: number) {
  ctx.save();

  const minorStep = 32;
  const majorStep = 128;

  for (let x = 0; x <= width; x += minorStep) {
    ctx.beginPath();
    ctx.moveTo(x + 0.5, 0);
    ctx.lineTo(x + 0.5, height);
    ctx.strokeStyle = "rgba(51, 65, 85, 0.16)";
    ctx.lineWidth = 1;
    ctx.stroke();
  }

  for (let y = 0; y <= height; y += minorStep) {
    ctx.beginPath();
    ctx.moveTo(0, y + 0.5);
    ctx.lineTo(width, y + 0.5);
    ctx.strokeStyle = "rgba(51, 65, 85, 0.16)";
    ctx.lineWidth = 1;
    ctx.stroke();
  }

  for (let x = 0; x <= width; x += majorStep) {
    ctx.beginPath();
    ctx.moveTo(x + 0.5, 0);
    ctx.lineTo(x + 0.5, height);
    ctx.strokeStyle = "rgba(100, 116, 139, 0.22)";
    ctx.lineWidth = 1;
    ctx.stroke();
  }

  for (let y = 0; y <= height; y += majorStep) {
    ctx.beginPath();
    ctx.moveTo(0, y + 0.5);
    ctx.lineTo(width, y + 0.5);
    ctx.strokeStyle = "rgba(100, 116, 139, 0.22)";
    ctx.lineWidth = 1;
    ctx.stroke();
  }

  ctx.restore();
}

// Panel merkezine hafif crosshair çizer
function drawCrosshair(ctx: CanvasRenderingContext2D, width: number, height: number) {
  const cx = width * 0.5;
  const cy = height * 0.5;

  ctx.save();
  ctx.strokeStyle = "rgba(148, 163, 184, 0.18)";
  ctx.lineWidth = 1;

  ctx.beginPath();
  ctx.moveTo(cx - 14, cy);
  ctx.lineTo(cx + 14, cy);
  ctx.moveTo(cx, cy - 14);
  ctx.lineTo(cx, cy + 14);
  ctx.stroke();

  ctx.restore();
}

function drawPolyline(
  ctx: CanvasRenderingContext2D,
  points: Vec2[],
  bounds: MapBounds,
  width: number,
  height: number,
  strokeStyle: string,
  lineWidth: number,
  alpha = 1,
  glow = 0
) {
  const validPoints = points.filter(isFinitePoint);
  if (validPoints.length < 2) {
    return;
  }

  ctx.save();
  ctx.strokeStyle = strokeStyle;
  ctx.lineWidth = lineWidth;
  ctx.globalAlpha = alpha;
  ctx.lineJoin = "round";
  ctx.lineCap = "round";

  if (glow > 0) {
    ctx.shadowColor = strokeStyle;
    ctx.shadowBlur = glow;
  }

  const first = worldToPanel(validPoints[0], bounds, width, height, PANEL_PADDING);
  ctx.beginPath();
  ctx.moveTo(first.x, first.y);

  for (let i = 1; i < validPoints.length; i += 1) {
    const projected = worldToPanel(validPoints[i], bounds, width, height, PANEL_PADDING);
    ctx.lineTo(projected.x, projected.y);
  }

  ctx.stroke();
  ctx.restore();
}

// Occupancy hücrelerini nokta gibi değil gerçek hücre gibi çizer
function drawOccupancyCells(
  ctx: CanvasRenderingContext2D,
  points: Vec2[],
  bounds: MapBounds,
  width: number,
  height: number
) {
  const validPoints = points.filter(isFinitePoint);
  if (validPoints.length === 0) {
    return;
  }

  const pixelsPerUnit = getPixelsPerWorldUnit(bounds, width, height, PANEL_PADDING);
  const estimatedWorldCell = estimateCellSizeWorld(validPoints);
  const cellPx = Math.max(2, Math.min(18, estimatedWorldCell * pixelsPerUnit));

  ctx.save();

  for (const point of validPoints) {
    const projected = worldToPanel(point, bounds, width, height, PANEL_PADDING);

    const x = projected.x - cellPx * 0.5;
    const y = projected.y - cellPx * 0.5;

    const grad = ctx.createRadialGradient(
      projected.x,
      projected.y,
      0,
      projected.x,
      projected.y,
      cellPx
    );
    grad.addColorStop(0, "rgba(250, 204, 21, 0.34)");
    grad.addColorStop(0.65, "rgba(245, 158, 11, 0.22)");
    grad.addColorStop(1, "rgba(245, 158, 11, 0.08)");

    ctx.fillStyle = grad;
    ctx.fillRect(x, y, cellPx, cellPx);

    ctx.strokeStyle = "rgba(251, 191, 36, 0.12)";
    ctx.lineWidth = 1;
    ctx.strokeRect(x, y, cellPx, cellPx);
  }

  ctx.restore();
}

// Preview eğrisini daha sıcak tonlu gösterir
function drawOccupancyPreview(
  ctx: CanvasRenderingContext2D,
  points: Vec2[],
  bounds: MapBounds,
  width: number,
  height: number
) {
  drawPolyline(ctx, points, bounds, width, height, "#f59e0b", 1.8, 0.95, 8);
}

// Lidar noktalarını glow + çekirdek nokta olarak çizer
function drawLidarCloud(
  ctx: CanvasRenderingContext2D,
  points: Vec2[],
  bounds: MapBounds,
  width: number,
  height: number
) {
  const validPoints = points.filter(isFinitePoint);
  if (validPoints.length === 0) {
    return;
  }

  ctx.save();

  for (const point of validPoints) {
    const projected = worldToPanel(point, bounds, width, height, PANEL_PADDING);

    ctx.beginPath();
    ctx.arc(projected.x, projected.y, 5.5, 0, Math.PI * 2);
    ctx.fillStyle = "rgba(139, 92, 246, 0.08)";
    ctx.fill();

    ctx.beginPath();
    ctx.arc(projected.x, projected.y, 2.4, 0, Math.PI * 2);
    ctx.fillStyle = "rgba(167, 139, 250, 0.32)";
    ctx.fill();

    ctx.beginPath();
    ctx.arc(projected.x, projected.y, 1.15, 0, Math.PI * 2);
    ctx.fillStyle = "rgba(233, 213, 255, 0.95)";
    ctx.fill();
  }

  ctx.restore();
}

function drawObstacles(
  ctx: CanvasRenderingContext2D,
  obstacles: Array<{
    id: string;
    position: Vec2;
    radius: number;
  }>,
  bounds: MapBounds,
  width: number,
  height: number,
  pixelsPerUnit: number
) {
  ctx.save();

  for (const obstacle of obstacles) {
    if (!isFinitePoint(obstacle.position)) {
      continue;
    }

    const projected = worldToPanel(obstacle.position, bounds, width, height, PANEL_PADDING);
    const worldRadius = Number.isFinite(obstacle.radius) ? obstacle.radius : 0.4;
    const pixelRadius = Math.max(9, Math.min(42, worldRadius * pixelsPerUnit));

    const fillGrad = ctx.createRadialGradient(
      projected.x,
      projected.y,
      pixelRadius * 0.15,
      projected.x,
      projected.y,
      pixelRadius
    );
    fillGrad.addColorStop(0, "rgba(251, 113, 133, 0.28)");
    fillGrad.addColorStop(1, "rgba(244, 63, 94, 0.08)");

    ctx.beginPath();
    ctx.arc(projected.x, projected.y, pixelRadius, 0, Math.PI * 2);
    ctx.fillStyle = fillGrad;
    ctx.fill();

    ctx.beginPath();
    ctx.arc(projected.x, projected.y, pixelRadius, 0, Math.PI * 2);
    ctx.strokeStyle = "rgba(251, 113, 133, 0.92)";
    ctx.lineWidth = 2;
    ctx.stroke();

    ctx.beginPath();
    ctx.arc(projected.x, projected.y, Math.max(3, pixelRadius * 0.18), 0, Math.PI * 2);
    ctx.fillStyle = "rgba(255, 241, 242, 0.98)";
    ctx.fill();

    ctx.beginPath();
    ctx.arc(projected.x, projected.y, pixelRadius + 5, 0, Math.PI * 2);
    ctx.strokeStyle = "rgba(251, 113, 133, 0.18)";
    ctx.lineWidth = 1;
    ctx.stroke();
  }

  ctx.restore();
}

function drawGoal(
  ctx: CanvasRenderingContext2D,
  goal: Vec2 | null,
  bounds: MapBounds,
  width: number,
  height: number
) {
  if (!isFinitePoint(goal)) {
    return;
  }

  const projected = worldToPanel(goal, bounds, width, height, PANEL_PADDING);

  ctx.save();

  ctx.beginPath();
  ctx.arc(projected.x, projected.y, 14, 0, Math.PI * 2);
  ctx.fillStyle = "rgba(34, 197, 94, 0.12)";
  ctx.fill();

  ctx.beginPath();
  ctx.arc(projected.x, projected.y, 8, 0, Math.PI * 2);
  ctx.strokeStyle = "rgba(74, 222, 128, 0.98)";
  ctx.lineWidth = 2;
  ctx.stroke();

  ctx.beginPath();
  ctx.moveTo(projected.x - 18, projected.y);
  ctx.lineTo(projected.x + 18, projected.y);
  ctx.moveTo(projected.x, projected.y - 18);
  ctx.lineTo(projected.x, projected.y + 18);
  ctx.strokeStyle = "rgba(134, 239, 172, 0.88)";
  ctx.lineWidth = 1.5;
  ctx.stroke();

  ctx.restore();
}

function drawPoseMarker(
  ctx: CanvasRenderingContext2D,
  pose: Vec2 | null,
  bounds: MapBounds,
  width: number,
  height: number,
  fill: string,
  stroke: string
) {
  if (!isFinitePoint(pose)) {
    return;
  }

  const projected = worldToPanel(pose, bounds, width, height, PANEL_PADDING);

  ctx.save();

  ctx.beginPath();
  ctx.arc(projected.x, projected.y, 6, 0, Math.PI * 2);
  ctx.fillStyle = fill;
  ctx.fill();

  ctx.beginPath();
  ctx.arc(projected.x, projected.y, 11, 0, Math.PI * 2);
  ctx.strokeStyle = stroke;
  ctx.lineWidth = 1.5;
  ctx.stroke();

  ctx.restore();
}

// Araç işaretini daha güçlü gövde/yön görünümüyle çizer
function drawVehicle(
  ctx: CanvasRenderingContext2D,
  vehiclePosition: Vec2,
  headingDeg: number,
  bounds: MapBounds,
  width: number,
  height: number
) {
  if (!isFinitePoint(vehiclePosition)) {
    return;
  }

  const projected = worldToPanel(vehiclePosition, bounds, width, height, PANEL_PADDING);
  const headingRad = ((headingDeg - 90) * Math.PI) / 180;

  ctx.save();
  ctx.translate(projected.x, projected.y);
  ctx.rotate(headingRad);

  ctx.beginPath();
  ctx.arc(0, 0, 18, 0, Math.PI * 2);
  ctx.fillStyle = "rgba(56, 189, 248, 0.08)";
  ctx.fill();

  ctx.beginPath();
  ctx.moveTo(0, -34);
  ctx.lineTo(0, -7);
  ctx.strokeStyle = "rgba(125, 211, 252, 0.95)";
  ctx.lineWidth = 2.5;
  ctx.stroke();

  ctx.beginPath();
  ctx.moveTo(0, -18);
  ctx.lineTo(11, 8);
  ctx.lineTo(0, 3);
  ctx.lineTo(-11, 8);
  ctx.closePath();
  ctx.fillStyle = "#38bdf8";
  ctx.fill();

  ctx.beginPath();
  ctx.moveTo(0, -18);
  ctx.lineTo(11, 8);
  ctx.lineTo(0, 3);
  ctx.lineTo(-11, 8);
  ctx.closePath();
  ctx.strokeStyle = "rgba(224, 242, 254, 0.85)";
  ctx.lineWidth = 1.2;
  ctx.stroke();

  ctx.beginPath();
  ctx.arc(0, 0, 3.2, 0, Math.PI * 2);
  ctx.fillStyle = "rgba(224, 242, 254, 0.95)";
  ctx.fill();

  ctx.restore();
}

// Sağ üstte operasyon HUD bilgisi
function drawHud(
  ctx: CanvasRenderingContext2D,
  width: number,
  headingDeg: number,
  lidarCount: number,
  occupancyCount: number,
  obstacleCount: number
) {
  const boxWidth = 210;
  const boxHeight = 106;
  const x = width - boxWidth - 14;
  const y = 14;

  ctx.save();

  ctx.fillStyle = "rgba(15, 23, 42, 0.78)";
  ctx.strokeStyle = "rgba(71, 85, 105, 0.65)";
  ctx.lineWidth = 1;

  drawRoundedRect(ctx, x, y, boxWidth, boxHeight, 14);
  ctx.fill();
  ctx.stroke();

  ctx.font = "600 12px Inter, system-ui, sans-serif";
  ctx.fillStyle = "rgba(226, 232, 240, 0.95)";
  ctx.fillText("MISSION MAP", x + 14, y + 20);

  ctx.font = "12px Inter, system-ui, sans-serif";
  ctx.fillStyle = "rgba(148, 163, 184, 0.95)";
  ctx.fillText(
    `Heading: ${Number.isFinite(headingDeg) ? headingDeg.toFixed(1) : "0.0"}°`,
    x + 14,
    y + 44
  );
  ctx.fillText(`LiDAR points: ${lidarCount}`, x + 14, y + 64);
  ctx.fillText(`Occupancy cells: ${occupancyCount}`, x + 14, y + 84);
  ctx.fillText(`Obstacles: ${obstacleCount}`, x + 14, y + 104);

  ctx.restore();
}

// Sol altta küçük ölçek göstergesi
function drawScaleBar(
  ctx: CanvasRenderingContext2D,
  bounds: MapBounds,
  width: number,
  height: number
) {
  const pixelsPerUnit = getPixelsPerWorldUnit(bounds, width, height, PANEL_PADDING);

  if (!Number.isFinite(pixelsPerUnit) || pixelsPerUnit <= 0) {
    return;
  }

  const targetPx = 120;
  const roughMeters = targetPx / pixelsPerUnit;

  const niceSteps = [0.25, 0.5, 1, 2, 5, 10, 20, 50, 100];
  let chosen = niceSteps[niceSteps.length - 1];

  for (const step of niceSteps) {
    if (step >= roughMeters) {
      chosen = step;
      break;
    }
  }

  const barPx = chosen * pixelsPerUnit;
  const x = 18;
  const y = height - 24;

  ctx.save();
  ctx.strokeStyle = "rgba(226, 232, 240, 0.92)";
  ctx.lineWidth = 2;

  ctx.beginPath();
  ctx.moveTo(x, y);
  ctx.lineTo(x + barPx, y);
  ctx.moveTo(x, y - 5);
  ctx.lineTo(x, y + 5);
  ctx.moveTo(x + barPx, y - 5);
  ctx.lineTo(x + barPx, y + 5);
  ctx.stroke();

  ctx.font = "12px Inter, system-ui, sans-serif";
  ctx.fillStyle = "rgba(226, 232, 240, 0.92)";
  ctx.fillText(`${chosen} m`, x, y - 10);

  ctx.restore();
}

// Kenarlarda hafif vignette ile odak artırılır
function drawVignette(ctx: CanvasRenderingContext2D, width: number, height: number) {
  const vignette = ctx.createRadialGradient(
    width * 0.5,
    height * 0.5,
    Math.min(width, height) * 0.25,
    width * 0.5,
    height * 0.5,
    Math.max(width, height) * 0.72
  );
  vignette.addColorStop(0, "rgba(2, 6, 23, 0)");
  vignette.addColorStop(1, "rgba(2, 6, 23, 0.34)");

  ctx.save();
  ctx.fillStyle = vignette;
  ctx.fillRect(0, 0, width, height);
  ctx.restore();
}

// Canvas tabanlı gelişmiş 2D mission map yüzeyi
export function MissionMapCanvas({
  vehiclePosition,
  headingDeg,
  trail,
  route,
  goal,
  obstacles,
  lidarPoints,
  occupancyPreview = [],
  occupancyCells = [],
  ekfTrail = [],
  ekfPose = null,
  odomHint = null
}: MissionMapCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }

    const dpr = window.devicePixelRatio || 1;
    canvas.width = CANVAS_WIDTH * dpr;
    canvas.height = CANVAS_HEIGHT * dpr;
    canvas.style.width = `${CANVAS_WIDTH}px`;
    canvas.style.height = `${CANVAS_HEIGHT}px`;

    const ctx = canvas.getContext("2d");
    if (!ctx) {
      return;
    }

    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);

    const bounds = buildStableBounds(
      vehiclePosition,
      trail,
      route,
      goal,
      ekfTrail,
      occupancyPreview,
      occupancyCells,
      lidarPoints,
      obstacles
    );

    const pixelsPerUnit = getPixelsPerWorldUnit(bounds, CANVAS_WIDTH, CANVAS_HEIGHT, PANEL_PADDING);

    drawBackground(ctx, CANVAS_WIDTH, CANVAS_HEIGHT);
    drawGrid(ctx, CANVAS_WIDTH, CANVAS_HEIGHT);
    drawCrosshair(ctx, CANVAS_WIDTH, CANVAS_HEIGHT);

    drawOccupancyCells(ctx, occupancyCells, bounds, CANVAS_WIDTH, CANVAS_HEIGHT);
    drawOccupancyPreview(ctx, occupancyPreview, bounds, CANVAS_WIDTH, CANVAS_HEIGHT);
    drawLidarCloud(ctx, lidarPoints, bounds, CANVAS_WIDTH, CANVAS_HEIGHT);

    drawPolyline(ctx, trail, bounds, CANVAS_WIDTH, CANVAS_HEIGHT, "#38bdf8", 2.6, 0.98, 10);
    drawPolyline(ctx, route, bounds, CANVAS_WIDTH, CANVAS_HEIGHT, "#e879f9", 2.3, 0.92, 8);
    drawPolyline(ctx, ekfTrail, bounds, CANVAS_WIDTH, CANVAS_HEIGHT, "#22c55e", 2.1, 0.95, 8);

    drawObstacles(ctx, obstacles, bounds, CANVAS_WIDTH, CANVAS_HEIGHT, pixelsPerUnit);
    drawGoal(ctx, goal, bounds, CANVAS_WIDTH, CANVAS_HEIGHT);

    drawPoseMarker(
      ctx,
      ekfPose,
      bounds,
      CANVAS_WIDTH,
      CANVAS_HEIGHT,
      "rgba(34, 197, 94, 0.95)",
      "rgba(34, 197, 94, 0.5)"
    );

    drawPoseMarker(
      ctx,
      odomHint,
      bounds,
      CANVAS_WIDTH,
      CANVAS_HEIGHT,
      "rgba(56, 189, 248, 0.92)",
      "rgba(56, 189, 248, 0.42)"
    );

    drawVehicle(ctx, vehiclePosition, headingDeg, bounds, CANVAS_WIDTH, CANVAS_HEIGHT);

    drawScaleBar(ctx, bounds, CANVAS_WIDTH, CANVAS_HEIGHT);
    drawHud(
      ctx,
      CANVAS_WIDTH,
      headingDeg,
      lidarPoints.length,
      occupancyCells.length,
      obstacles.length
    );
    drawVignette(ctx, CANVAS_WIDTH, CANVAS_HEIGHT);
  }, [
    vehiclePosition,
    headingDeg,
    trail,
    route,
    goal,
    obstacles,
    lidarPoints,
    occupancyPreview,
    occupancyCells,
    ekfTrail,
    ekfPose,
    odomHint
  ]);

  return (
    <div className="overflow-hidden rounded-2xl border border-slate-800 bg-slate-950/70">
      <canvas
        ref={canvasRef}
        className="block h-[520px] w-full"
      />
    </div>
  );
}