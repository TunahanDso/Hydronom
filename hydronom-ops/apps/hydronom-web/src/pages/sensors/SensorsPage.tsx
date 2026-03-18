import { useEffect, useMemo, useRef } from "react";
import type { ReactNode } from "react";
import { useFleetStore } from "../../features/fleet-management/store/fleet.store";
import { useVehicleStore } from "../../features/vehicle-state/store/vehicle.store";
import { useSensorStore } from "../../features/sensor-visualization/store/sensor.store";
import type { SensorState } from "../../entities/sensor/model/sensor.types";

type MapPoint = {
  x: number;
  y: number;
};

type LandmarkPoint = {
  x?: number;
  y?: number;
};

type LandmarkLike = {
  id?: string;
  type?: string;
  shape?: string;
  points?: LandmarkPoint[];
};

export function SensorsPage() {
  const selectedVehicleId = useFleetStore((state) => state.selectedVehicleId);

  const telemetry = useVehicleStore((state) =>
    selectedVehicleId ? state.telemetryByVehicleId[selectedVehicleId] : undefined
  );

  const sensor = useSensorStore((state) =>
    selectedVehicleId ? state.sensorByVehicleId[selectedVehicleId] : undefined
  ) as SensorState | undefined;

  const rawLandmarks = (telemetry?.landmarks ?? []) as LandmarkLike[];

  const toVec2Array = (points?: LandmarkPoint[]): MapPoint[] => {
    return (points ?? [])
      .filter(
        (point) =>
          typeof point?.x === "number" &&
          Number.isFinite(point.x) &&
          typeof point?.y === "number" &&
          Number.isFinite(point.y)
      )
      .map((point) => ({
        x: point.x as number,
        y: point.y as number
      }));
  };

  const occupancyPreviewLandmark = useMemo(
    () =>
      rawLandmarks.find(
        (landmark) =>
          landmark.type === "occupancy_preview" ||
          landmark.id === "ogm_preview" ||
          landmark.id === "occ_poly"
      ),
    [rawLandmarks]
  );

  const occupancyCellsLandmark = useMemo(
    () =>
      rawLandmarks.find(
        (landmark) =>
          landmark.type === "occupancy_cells" ||
          landmark.id === "ogm_preview_cells" ||
          landmark.id === "occ_poly_cells"
      ),
    [rawLandmarks]
  );

  const lidarPoints = useMemo(() => {
    return (sensor?.lidarPoints ?? [])
      .filter(
        (point) =>
          typeof point?.x === "number" &&
          Number.isFinite(point.x) &&
          typeof point?.y === "number" &&
          Number.isFinite(point.y)
      )
      .map((point) => ({
        x: point.x,
        y: point.y
      }));
  }, [sensor?.lidarPoints]);

  const occupancyPreviewPoints = useMemo(
    () => toVec2Array(occupancyPreviewLandmark?.points),
    [occupancyPreviewLandmark]
  );

  const occupancyCellPoints = useMemo(
    () => toVec2Array(occupancyCellsLandmark?.points),
    [occupancyCellsLandmark]
  );

  const cameraImageUrl =
    sensor?.cameraFrameUrl ??
    sensor?.cameraUrl ??
    sensor?.camera?.imageUrl ??
    null;

  const lidarCount = lidarPoints.length;
  const occupancyWidth = sensor?.occupancy?.width ?? 0;
  const occupancyHeight = sensor?.occupancy?.height ?? 0;
  const occupiedCellCount =
    sensor?.occupancy?.occupiedCellCount ?? occupancyCellPoints.length ?? 0;

  const lastCameraTs =
    sensor?.camera?.timestampUtc ??
    sensor?.cameraTimestampUtc ??
    null;

  const lastLidarTs =
    sensor?.lidar?.timestampUtc ??
    sensor?.lidarTimestampUtc ??
    sensor?.lastLidarTimestampUtc ??
    null;

  const lastOccupancyTs =
    sensor?.occupancy?.timestampUtc ??
    sensor?.occupancyTimestampUtc ??
    sensor?.lastOccupancyTimestampUtc ??
    null;

  const freshnessCamera =
    sensor?.freshness?.cameraMs ??
    sensor?.camera?.ageMs ??
    null;

  const freshnessLidar =
    sensor?.freshness?.lidarMs ??
    sensor?.lidar?.ageMs ??
    null;

  const freshnessOccupancy =
    sensor?.freshness?.occupancyMs ??
    sensor?.occupancy?.ageMs ??
    null;

  const sensorHealthText =
    sensor?.status ??
    (lidarCount > 0 || occupiedCellCount > 0 || cameraImageUrl ? "active" : "waiting");

  const healthTone: "neutral" | "info" | "ok" | "warn" =
    sensorHealthText === "active" || sensorHealthText === "ok"
      ? "ok"
      : sensorHealthText === "warning" || sensorHealthText === "degraded"
        ? "warn"
        : sensorHealthText === "disabled" || sensorHealthText === "waiting"
          ? "info"
          : "neutral";

  return (
    <section className="space-y-6">
      <PageHeader
        title="Sensors & Camera"
        description="Kamera akışı, LiDAR, occupancy grid, sensör sağlığı ve veri tazeliği bu sayfada izlenir."
      />

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-12">
        <div className="xl:col-span-12">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5">
            <MiniStat
              label="Araç"
              value={telemetry?.displayName ?? selectedVehicleId ?? "N/A"}
              tone="neutral"
            />
            <MiniStat
              label="Health"
              value={String(sensorHealthText).toUpperCase()}
              tone={healthTone}
            />
            <MiniStat
              label="LiDAR"
              value={`${lidarCount} nokta`}
              tone={lidarCount > 0 ? "ok" : "warn"}
            />
            <MiniStat
              label="Occupancy"
              value={`${occupiedCellCount} dolu hücre`}
              tone={occupiedCellCount > 0 ? "ok" : "warn"}
            />
            <MiniStat
              label="Kamera"
              value={cameraImageUrl ? "AKIŞ VAR" : "BEKLİYOR"}
              tone={cameraImageUrl ? "ok" : "warn"}
            />
          </div>
        </div>

        <div className="xl:col-span-7">
          <PanelCard
            title="Camera Stream"
            subtitle="Operatör görüntüsü veya algı kamerası için ayrılmış yüzey"
          >
            <CameraPanel imageUrl={cameraImageUrl} />
            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2">
              <InfoRow label="Last Camera Timestamp" value={formatText(lastCameraTs)} />
              <InfoRow label="Camera Freshness" value={formatMs(freshnessCamera)} />
            </div>
          </PanelCard>
        </div>

        <div className="xl:col-span-5">
          <PanelCard
            title="LiDAR Point Cloud"
            subtitle="2D mini önizleme ve yoğunluk özeti"
          >
            <MiniCanvasFrame heightClass="h-[320px]">
              <PointCloudPreview points={lidarPoints} />
            </MiniCanvasFrame>

            <div className="mt-4 space-y-1">
              <InfoRow label="Point Count" value={String(lidarCount)} />
              <InfoRow label="Last LiDAR Timestamp" value={formatText(lastLidarTs)} />
              <InfoRow label="LiDAR Freshness" value={formatMs(freshnessLidar)} />
            </div>
          </PanelCard>
        </div>

        <div className="xl:col-span-8">
          <PanelCard
            title="Occupancy Preview"
            subtitle="OGM preview ve occupancy cells katmanlarının kompakt görünümü"
          >
            <MiniCanvasFrame heightClass="h-[320px]">
              <OccupancyPreviewCanvas
                previewPoints={occupancyPreviewPoints}
                cellPoints={occupancyCellPoints}
              />
            </MiniCanvasFrame>

            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2">
              <InfoRow
                label="Grid Size"
                value={`${occupancyWidth} x ${occupancyHeight}`}
              />
              <InfoRow
                label="Occupied Cells"
                value={String(occupiedCellCount)}
              />
              <InfoRow
                label="Preview Polyline Points"
                value={String(occupancyPreviewPoints.length)}
              />
              <InfoRow
                label="Cell Landmark Points"
                value={String(occupancyCellPoints.length)}
              />
              <InfoRow
                label="Last Occupancy Timestamp"
                value={formatText(lastOccupancyTs)}
              />
              <InfoRow
                label="Occupancy Freshness"
                value={formatMs(freshnessOccupancy)}
              />
            </div>
          </PanelCard>
        </div>

        <div className="xl:col-span-4">
          <PanelCard
            title="Sensor Health & Notes"
            subtitle="Veri tazeliği, landmark fallback ve sayfa amacı"
          >
            <InfoRow label="Selected Vehicle" value={selectedVehicleId ?? "N/A"} />
            <InfoRow
              label="Landmark Count"
              value={String(rawLandmarks.length)}
            />
            <InfoRow
              label="Occupancy Preview Landmark"
              value={occupancyPreviewLandmark?.id ?? occupancyPreviewLandmark?.type ?? "N/A"}
            />
            <InfoRow
              label="Occupancy Cells Landmark"
              value={occupancyCellsLandmark?.id ?? occupancyCellsLandmark?.type ?? "N/A"}
            />

            <div className="mt-4 rounded-2xl border border-slate-800 bg-slate-950/60 p-4 text-sm leading-6 text-slate-400">
              Bu sayfa, sensör verisini yalnızca ham sayı olarak değil; operatörün
              okuyabileceği görsel bloklar halinde sunmak için hazırlandı. Kamera,
              LiDAR ve occupancy tarafı aynı ekranda takip edilebilir.
            </div>
          </PanelCard>
        </div>
      </div>
    </section>
  );
}

function PageHeader(props: { title: string; description: string }) {
  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight">{props.title}</h1>
      <p className="mt-2 max-w-4xl text-sm leading-6 text-slate-400">
        {props.description}
      </p>
    </div>
  );
}

function PanelCard(props: {
  title: string;
  subtitle: string;
  children: ReactNode;
}) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6 shadow-panel">
      <h2 className="text-xl font-semibold">{props.title}</h2>
      <p className="mt-1 text-sm leading-6 text-slate-400">{props.subtitle}</p>
      <div className="mt-5">{props.children}</div>
    </section>
  );
}

function MiniCanvasFrame(props: {
  children: ReactNode;
  heightClass: string;
}) {
  return (
    <div
      className={[
        "overflow-hidden rounded-2xl border border-slate-800 bg-slate-950/70",
        props.heightClass
      ].join(" ")}
    >
      {props.children}
    </div>
  );
}

function CameraPanel(props: { imageUrl: string | null }) {
  if (!props.imageUrl) {
    return (
      <div className="flex h-[320px] items-center justify-center rounded-2xl border border-dashed border-slate-700 bg-slate-950/50 text-sm text-slate-500">
        Kamera akışı henüz yok
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-2xl border border-slate-800 bg-slate-950/60">
      <img
        src={props.imageUrl}
        alt="Camera stream"
        className="block h-[320px] w-full object-cover"
      />
    </div>
  );
}

function PointCloudPreview(props: { points: MapPoint[] }) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }

    const width = 640;
    const height = 320;
    const dpr = window.devicePixelRatio || 1;

    canvas.width = width * dpr;
    canvas.height = height * dpr;
    canvas.style.width = `${width}px`;
    canvas.style.height = `${height}px`;

    const ctx = canvas.getContext("2d");
    if (!ctx) {
      return;
    }

    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, width, height);

    drawPanelBackground(ctx, width, height);
    drawMiniGrid(ctx, width, height);

    if (props.points.length === 0) {
      drawCenteredText(ctx, width, height, "LiDAR verisi bekleniyor");
      return;
    }

    const bounds = computeBoundsFromPoints(props.points, 0.15);

    for (const point of props.points) {
      const projected = worldToPanelMini(point, bounds, width, height, 18);

      ctx.beginPath();
      ctx.arc(projected.x, projected.y, 4.5, 0, Math.PI * 2);
      ctx.fillStyle = "rgba(139, 92, 246, 0.10)";
      ctx.fill();

      ctx.beginPath();
      ctx.arc(projected.x, projected.y, 2.1, 0, Math.PI * 2);
      ctx.fillStyle = "rgba(167, 139, 250, 0.35)";
      ctx.fill();

      ctx.beginPath();
      ctx.arc(projected.x, projected.y, 1, 0, Math.PI * 2);
      ctx.fillStyle = "rgba(243, 232, 255, 0.95)";
      ctx.fill();
    }
  }, [props.points]);

  return <canvas ref={canvasRef} className="block h-full w-full" />;
}

function OccupancyPreviewCanvas(props: {
  previewPoints: MapPoint[];
  cellPoints: MapPoint[];
}) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }

    const width = 640;
    const height = 320;
    const dpr = window.devicePixelRatio || 1;

    canvas.width = width * dpr;
    canvas.height = height * dpr;
    canvas.style.width = `${width}px`;
    canvas.style.height = `${height}px`;

    const ctx = canvas.getContext("2d");
    if (!ctx) {
      return;
    }

    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, width, height);

    drawPanelBackground(ctx, width, height);
    drawMiniGrid(ctx, width, height);

    const allPoints = [...props.previewPoints, ...props.cellPoints];

    if (allPoints.length === 0) {
      drawCenteredText(ctx, width, height, "Occupancy verisi bekleniyor");
      return;
    }

    const bounds = computeBoundsFromPoints(allPoints, 0.18);

    for (const point of props.cellPoints) {
      const projected = worldToPanelMini(point, bounds, width, height, 18);
      const size = 4;

      ctx.fillStyle = "rgba(245, 158, 11, 0.25)";
      ctx.fillRect(projected.x - size * 0.5, projected.y - size * 0.5, size, size);

      ctx.strokeStyle = "rgba(251, 191, 36, 0.14)";
      ctx.lineWidth = 1;
      ctx.strokeRect(projected.x - size * 0.5, projected.y - size * 0.5, size, size);
    }

    if (props.previewPoints.length >= 2) {
      const first = worldToPanelMini(props.previewPoints[0], bounds, width, height, 18);

      ctx.beginPath();
      ctx.moveTo(first.x, first.y);

      for (let i = 1; i < props.previewPoints.length; i += 1) {
        const projected = worldToPanelMini(props.previewPoints[i], bounds, width, height, 18);
        ctx.lineTo(projected.x, projected.y);
      }

      ctx.strokeStyle = "#f59e0b";
      ctx.lineWidth = 1.8;
      ctx.shadowColor = "#f59e0b";
      ctx.shadowBlur = 8;
      ctx.stroke();
      ctx.shadowBlur = 0;
    }
  }, [props.previewPoints, props.cellPoints]);

  return <canvas ref={canvasRef} className="block h-full w-full" />;
}

function drawPanelBackground(ctx: CanvasRenderingContext2D, width: number, height: number) {
  const bg = ctx.createLinearGradient(0, 0, 0, height);
  bg.addColorStop(0, "#020617");
  bg.addColorStop(0.55, "#07111f");
  bg.addColorStop(1, "#020617");

  ctx.fillStyle = bg;
  ctx.fillRect(0, 0, width, height);
}

function drawMiniGrid(ctx: CanvasRenderingContext2D, width: number, height: number) {
  ctx.save();

  for (let x = 0; x <= width; x += 32) {
    ctx.beginPath();
    ctx.moveTo(x + 0.5, 0);
    ctx.lineTo(x + 0.5, height);
    ctx.strokeStyle = "rgba(51, 65, 85, 0.16)";
    ctx.lineWidth = 1;
    ctx.stroke();
  }

  for (let y = 0; y <= height; y += 32) {
    ctx.beginPath();
    ctx.moveTo(0, y + 0.5);
    ctx.lineTo(width, y + 0.5);
    ctx.strokeStyle = "rgba(51, 65, 85, 0.16)";
    ctx.lineWidth = 1;
    ctx.stroke();
  }

  ctx.restore();
}

function drawCenteredText(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
  text: string
) {
  ctx.save();
  ctx.fillStyle = "rgba(148, 163, 184, 0.85)";
  ctx.font = "14px Inter, system-ui, sans-serif";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(text, width * 0.5, height * 0.5);
  ctx.restore();
}

function computeBoundsFromPoints(points: MapPoint[], padRatio: number) {
  const xs = points.map((point) => point.x);
  const ys = points.map((point) => point.y);

  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);

  const spanX = Math.max(maxX - minX, 1);
  const spanY = Math.max(maxY - minY, 1);

  const padX = Math.max(spanX * padRatio, 1);
  const padY = Math.max(spanY * padRatio, 1);

  return {
    minX: minX - padX,
    maxX: maxX + padX,
    minY: minY - padY,
    maxY: maxY + padY
  };
}

function worldToPanelMini(
  point: MapPoint,
  bounds: { minX: number; maxX: number; minY: number; maxY: number },
  width: number,
  height: number,
  padding: number
) {
  const usableWidth = width - padding * 2;
  const usableHeight = height - padding * 2;

  const spanX = Math.max(bounds.maxX - bounds.minX, 0.0001);
  const spanY = Math.max(bounds.maxY - bounds.minY, 0.0001);

  const xRatio = (point.x - bounds.minX) / spanX;
  const yRatio = (point.y - bounds.minY) / spanY;

  return {
    x: padding + xRatio * usableWidth,
    y: height - (padding + yRatio * usableHeight)
  };
}

function formatText(value: string | null | undefined) {
  return value && String(value).trim().length > 0 ? value : "N/A";
}

function formatMs(value: number | null | undefined) {
  return typeof value === "number" && Number.isFinite(value) ? `${value.toFixed(0)} ms` : "N/A";
}

function InfoRow(props: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-slate-800 py-2.5 last:border-b-0">
      <div className="text-sm text-slate-400">{props.label}</div>
      <div className="text-right text-sm font-medium text-slate-100">
        {props.value}
      </div>
    </div>
  );
}

function MiniStat(props: {
  label: string;
  value: string;
  tone: "neutral" | "info" | "ok" | "warn";
}) {
  const toneClass =
    props.tone === "warn"
      ? "border-amber-500/30 bg-amber-500/10 text-amber-200"
      : props.tone === "ok"
        ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-200"
        : props.tone === "info"
          ? "border-sky-500/30 bg-sky-500/10 text-sky-200"
          : "border-slate-800 bg-slate-950/60 text-slate-200";

  return (
    <div className={`rounded-2xl border p-4 ${toneClass}`}>
      <div className="text-[10px] uppercase tracking-[0.25em] text-slate-400">
        {props.label}
      </div>
      <div className="mt-2 text-sm font-semibold">{props.value}</div>
    </div>
  );
}