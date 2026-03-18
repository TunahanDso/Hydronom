import { useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { useFleetStore } from "../../features/fleet-management/store/fleet.store";
import { useVehicleStore } from "../../features/vehicle-state/store/vehicle.store";
import { useMissionStore } from "../../features/mission-state/store/mission.store";
import { useActuatorStore } from "../../features/actuator-monitoring/store/actuator.store";
import { useSensorStore } from "../../features/sensor-visualization/store/sensor.store";
import { MissionMapCanvas } from "./components/MissionMapCanvas";

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

// Yeni canvas tabanlı mission map sayfası
export function MissionMapCanvasPage() {
  const [zoom, setZoom] = useState(1);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const fullscreenRef = useRef<HTMLDivElement | null>(null);

  const selectedVehicleId = useFleetStore((state) => state.selectedVehicleId);

  const telemetry = useVehicleStore((state) =>
    selectedVehicleId ? state.telemetryByVehicleId[selectedVehicleId] : undefined
  );

  const mission = useMissionStore((state) =>
    selectedVehicleId ? state.missionByVehicleId[selectedVehicleId] : undefined
  );

  const actuator = useActuatorStore((state) =>
    selectedVehicleId ? state.actuatorByVehicleId[selectedVehicleId] : undefined
  );

  const sensor = useSensorStore((state) =>
    selectedVehicleId ? state.sensorByVehicleId[selectedVehicleId] : undefined
  );

  useEffect(() => {
    const onFullscreenChange = () => {
      setIsFullscreen(document.fullscreenElement === fullscreenRef.current);
    };

    document.addEventListener("fullscreenchange", onFullscreenChange);
    return () => {
      document.removeEventListener("fullscreenchange", onFullscreenChange);
    };
  }, []);

  const handleZoomIn = () => {
    setZoom((prev) => Math.min(prev + 0.15, 2.6));
  };

  const handleZoomOut = () => {
    setZoom((prev) => Math.max(prev - 0.15, 0.5));
  };

  const handleZoomReset = () => {
    setZoom(1);
  };

  const handleToggleFullscreen = async () => {
    const target = fullscreenRef.current;
    if (!target) return;

    try {
      if (document.fullscreenElement === target) {
        await document.exitFullscreen();
      } else {
        await target.requestFullscreen();
      }
    } catch (error) {
      console.error("Fullscreen geçişi başarısız:", error);
    }
  };

  const rawLandmarks = (telemetry?.landmarks ?? []) as LandmarkLike[];

  const findLandmark = (
    landmarks: LandmarkLike[],
    matcher: (landmark: LandmarkLike) => boolean
  ) => landmarks.find(matcher);

  const occupancyPreviewLandmark = useMemo(
    () =>
      findLandmark(
        rawLandmarks,
        (landmark) =>
          landmark.type === "occupancy_preview" ||
          landmark.id === "ogm_preview" ||
          landmark.id === "occ_poly"
      ),
    [rawLandmarks]
  );

  const occupancyCellsLandmark = useMemo(
    () =>
      findLandmark(
        rawLandmarks,
        (landmark) =>
          landmark.type === "occupancy_cells" ||
          landmark.id === "ogm_preview_cells" ||
          landmark.id === "occ_poly_cells"
      ),
    [rawLandmarks]
  );

  const ekfTrailLandmark = useMemo(
    () =>
      findLandmark(
        rawLandmarks,
        (landmark) => landmark.type === "trail_ekf" || landmark.id === "ekf_trail"
      ),
    [rawLandmarks]
  );

  const ekfPoseLandmark = useMemo(
    () =>
      findLandmark(
        rawLandmarks,
        (landmark) => landmark.type === "ekf_pose" || landmark.id === "ekf_pose"
      ),
    [rawLandmarks]
  );

  const odomHintLandmark = useMemo(
    () =>
      findLandmark(
        rawLandmarks,
        (landmark) => landmark.type === "odometry" || landmark.id === "odom_hint"
      ),
    [rawLandmarks]
  );

  const headingDeg = telemetry?.map?.headingDeg ?? telemetry?.headingDeg ?? 0;
  const speed = telemetry?.motion?.speed;

  const worldX = telemetry?.map?.worldPosition?.x ?? telemetry?.x;
  const worldY = telemetry?.map?.worldPosition?.y ?? telemetry?.y;

  const obsAheadFlag =
    telemetry?.flags?.find((flag) => flag.key === "obsAhead")?.value ??
    telemetry?.obstacleAhead ??
    false;

  const forceX = actuator?.wrench?.forceBody?.x;
  const forceY = actuator?.wrench?.forceBody?.y;
  const forceZ = actuator?.wrench?.forceBody?.z;

  const torqueX = actuator?.wrench?.torqueBody?.x;
  const torqueY = actuator?.wrench?.torqueBody?.y;
  const torqueZ = actuator?.wrench?.torqueBody?.z;

  const vehiclePosition =
    telemetry?.map?.worldPosition ??
    (typeof telemetry?.x === "number" && typeof telemetry?.y === "number"
      ? { x: telemetry.x, y: telemetry.y }
      : undefined);

  const hasMapData =
    typeof vehiclePosition?.x === "number" &&
    Number.isFinite(vehiclePosition.x) &&
    typeof vehiclePosition?.y === "number" &&
    Number.isFinite(vehiclePosition.y);

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

  const mapTrail = useMemo(() => {
    if (!vehiclePosition) return [];
    return telemetry?.map?.trail?.length ? telemetry.map.trail : [vehiclePosition];
  }, [telemetry?.map?.trail, vehiclePosition]);

  const mapRoute = useMemo(() => mission?.route ?? [], [mission?.route]);

  const mapGoal = useMemo(() => mission?.goalPosition ?? null, [mission?.goalPosition]);

  const mapObstacles = useMemo(() => {
    return (telemetry?.obstacles ?? []).map((obstacle, index) => ({
      id: `runtime-obstacle-${index}`,
      position: {
        x: obstacle.x,
        y: obstacle.y
      },
      radius: obstacle.r
    }));
  }, [telemetry?.obstacles]);

  const mapLidarPoints = useMemo(() => {
    return (sensor?.lidarPoints ?? []).map((point) => ({
      x: point.x,
      y: point.y
    }));
  }, [sensor?.lidarPoints]);

  const mapOccupancyPreview = useMemo(
    () => toVec2Array(occupancyPreviewLandmark?.points),
    [occupancyPreviewLandmark]
  );

  const mapOccupancyCells = useMemo(
    () => toVec2Array(occupancyCellsLandmark?.points),
    [occupancyCellsLandmark]
  );

  const mapEkfTrail = useMemo(
    () => toVec2Array(ekfTrailLandmark?.points),
    [ekfTrailLandmark]
  );

  const mapEkfPose = useMemo(() => {
    const first = toVec2Array(ekfPoseLandmark?.points)[0];
    return first ?? null;
  }, [ekfPoseLandmark]);

  const mapOdomHint = useMemo(() => {
    const first = toVec2Array(odomHintLandmark?.points)[0];
    return first ?? null;
  }, [odomHintLandmark]);

  const trailCount = telemetry?.map?.trail?.length ?? 0;
  const routeCount = mission?.route?.length ?? 0;
  const waypointCount = mission?.waypoints?.length ?? 0;
  const obstacleCount = telemetry?.obstacles?.length ?? telemetry?.obstacleCount ?? 0;

  const lidarCount = sensor?.lidarPoints?.length ?? 0;
  const occupancyWidth = sensor?.occupancy?.width ?? 0;
  const occupancyHeight = sensor?.occupancy?.height ?? 0;
  const occupiedCellCount =
    sensor?.occupancy?.occupiedCellCount ?? occupancyCellsLandmark?.points?.length ?? 0;

  const renderHealthTone =
    lidarCount > 0 || mapOccupancyCells.length > 0 || mapObstacles.length > 0
      ? "ok"
      : hasMapData
        ? "info"
        : "warn";

  const zoomPercent = Math.round(zoom * 100);

  return (
    <section className="space-y-6">
      <PageTitle
        title="Mission Map Canvas"
        description="Canvas tabanlı yeni nesil 2D görev haritası. Araç, rota, hedef, trail, LiDAR, runtime obstacle, occupancy ve EKF/odometry katmanlarını aynı canlı store verisiyle daha güçlü bir render yüzeyinde gösterir."
      />

      <div
        ref={fullscreenRef}
        className={[
          "grid grid-cols-1 gap-6 2xl:grid-cols-12",
          isFullscreen ? "min-h-screen bg-slate-950 p-6" : ""
        ].join(" ")}
      >
        <div
          className={[
            "space-y-6",
            isFullscreen ? "2xl:col-span-9" : "2xl:col-span-8"
          ].join(" ")}
        >
          <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6 shadow-panel">
            <PanelTitle
              title="Canvas World View"
              subtitle="Harita katmanları tek canvas yüzeyinde işlenir. Görsel kalite ve yoğun veri render davranışı burada test edilir."
            />

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5">
              <MiniStat
                label="Araç"
                value={telemetry?.displayName ?? selectedVehicleId ?? "N/A"}
                tone="neutral"
              />
              <MiniStat
                label="Heading"
                value={`${formatNumber(headingDeg, 1, "0.0")}°`}
                tone="info"
              />
              <MiniStat
                label="Hız"
                value={`${formatNumber(speed, 2, "0.00")} m/s`}
                tone="info"
              />
              <MiniStat
                label="obsAhead"
                value={String(obsAheadFlag).toUpperCase()}
                tone={obsAheadFlag ? "warn" : "ok"}
              />
              <MiniStat
                label="Render"
                value={hasMapData ? "AKTİF" : "BEKLİYOR"}
                tone={renderHealthTone}
              />
            </div>

            <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
              <div className="flex flex-wrap items-center gap-2">
                <ControlButton onClick={handleZoomOut}>-</ControlButton>
                <ControlButton onClick={handleZoomReset}>%{zoomPercent}</ControlButton>
                <ControlButton onClick={handleZoomIn}>+</ControlButton>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <Tag text={`LiDAR ${lidarCount}`} />
                <Tag text={`Obstacle ${obstacleCount}`} />
                <Tag text={`Occ ${mapOccupancyCells.length}`} />
                <ControlButton onClick={handleToggleFullscreen}>
                  {isFullscreen ? "Tam ekrandan çık" : "Tam ekran"}
                </ControlButton>
              </div>
            </div>

            <div className="mt-5">
              {hasMapData && vehiclePosition ? (
                <div
                  className={[
                    "overflow-auto rounded-2xl border border-slate-800 bg-slate-950/60 p-4",
                    isFullscreen ? "max-h-[calc(100vh-220px)]" : ""
                  ].join(" ")}
                >
                  <div
                    className="origin-top-left transition-transform duration-200 ease-out"
                    style={{ transform: `scale(${zoom})`, width: "fit-content" }}
                  >
                    <MissionMapCanvas
                      vehiclePosition={vehiclePosition}
                      headingDeg={headingDeg}
                      trail={mapTrail}
                      route={mapRoute}
                      goal={mapGoal}
                      obstacles={mapObstacles}
                      lidarPoints={mapLidarPoints}
                      occupancyPreview={mapOccupancyPreview}
                      occupancyCells={mapOccupancyCells}
                      ekfTrail={mapEkfTrail}
                      ekfPose={mapEkfPose}
                      odomHint={mapOdomHint}
                    />
                  </div>
                </div>
              ) : (
                <Placeholder heightClass="h-[520px]">
                  Harita verisi henüz hazır değil
                </Placeholder>
              )}
            </div>

            <div className="mt-5 flex flex-wrap items-center gap-2 text-xs text-slate-400">
              <Tag text={`x: ${formatNumber(worldX, 2, "0.00")}`} />
              <Tag text={`y: ${formatNumber(worldY, 2, "0.00")}`} />
              <Tag text={`Trail: ${trailCount}`} />
              <Tag text={`EKF Trail: ${mapEkfTrail.length}`} />
              <Tag text={`Route: ${routeCount}`} />
              <Tag text={`Waypoint: ${waypointCount}`} />
              <Tag text={`Obstacle: ${obstacleCount}`} />
              <Tag text={`LiDAR: ${lidarCount}`} />
              <Tag text={`Grid: ${occupancyWidth}x${occupancyHeight}`} />
              <Tag text={`Occ Preview: ${mapOccupancyPreview.length}`} />
              <Tag text={`Occ Cells: ${mapOccupancyCells.length}`} />
              <Tag text={`Occupied: ${occupiedCellCount}`} />
            </div>
          </div>
        </div>

        <div
          className={[
            "space-y-6",
            isFullscreen ? "2xl:col-span-3" : "2xl:col-span-4"
          ].join(" ")}
        >
          <PanelCard
            title="Render Summary"
            subtitle="Canvas yüzeyine giden canlı katman özeti"
          >
            <InfoRow label="Vehicle" value={telemetry?.displayName ?? "N/A"} />
            <InfoRow label="Selected Vehicle" value={selectedVehicleId ?? "N/A"} />
            <InfoRow
              label="Heading"
              value={`${formatNumber(headingDeg, 1, "0.0")}°`}
            />
            <InfoRow
              label="Speed"
              value={`${formatNumber(speed, 2, "0.00")} m/s`}
            />
            <InfoRow label="Trail" value={String(mapTrail.length)} />
            <InfoRow label="Route" value={String(mapRoute.length)} />
            <InfoRow label="Obstacles" value={String(mapObstacles.length)} />
            <InfoRow label="LiDAR Points" value={String(mapLidarPoints.length)} />
            <InfoRow label="Occ Preview" value={String(mapOccupancyPreview.length)} />
            <InfoRow label="Occ Cells" value={String(mapOccupancyCells.length)} />
            <InfoRow label="EKF Trail" value={String(mapEkfTrail.length)} />
            <InfoRow
              label="EKF Pose"
              value={
                mapEkfPose
                  ? `(${formatNumber(mapEkfPose.x, 2, "0.00")}, ${formatNumber(
                      mapEkfPose.y,
                      2,
                      "0.00"
                    )})`
                  : "N/A"
              }
            />
            <InfoRow
              label="Odom Hint"
              value={
                mapOdomHint
                  ? `(${formatNumber(mapOdomHint.x, 2, "0.00")}, ${formatNumber(
                      mapOdomHint.y,
                      2,
                      "0.00"
                    )})`
                  : "N/A"
              }
            />
          </PanelCard>

          <PanelCard
            title="Force / Torque"
            subtitle="Actuator katmanından gelen wrench özeti"
          >
            <InfoRow
              label="Force Body"
              value={`(${formatNumber(forceX, 2, "0.00")}, ${formatNumber(
                forceY,
                2,
                "0.00"
              )}, ${formatNumber(forceZ, 2, "0.00")})`}
            />
            <InfoRow
              label="Torque Body"
              value={`(${formatNumber(torqueX, 2, "0.00")}, ${formatNumber(
                torqueY,
                2,
                "0.00"
              )}, ${formatNumber(torqueZ, 2, "0.00")})`}
            />
            <InfoRow
              label="Goal"
              value={
                mapGoal
                  ? `(${formatNumber(mapGoal.x, 2, "0.00")}, ${formatNumber(
                      mapGoal.y,
                      2,
                      "0.00"
                    )})`
                  : "N/A"
              }
            />
            <InfoRow label="obsAhead" value={String(obsAheadFlag).toUpperCase()} />
          </PanelCard>

          <PanelCard
            title="Map Quality Notes"
            subtitle="Bu ekranın amacı"
          >
            <div className="space-y-3 text-sm leading-6 text-slate-400">
              <p>
                Bu sayfa mevcut SVG mission paneline dokunmadan, aynı store verisiyle çalışan
                yeni canvas render yaklaşımını test etmek için ayrılmıştır.
              </p>
              <p>
                Buradaki amaç yalnızca veri göstermek değil; occupancy, lidar, obstacle ve
                rota katmanlarını daha yoğun ve daha okunabilir bir operasyon görünümüne
                taşımaktır.
              </p>
            </div>
          </PanelCard>
        </div>
      </div>
    </section>
  );
}

function formatNumber(
  value: number | null | undefined,
  fractionDigits: number,
  fallback: string
) {
  return typeof value === "number" && Number.isFinite(value)
    ? value.toFixed(fractionDigits)
    : fallback;
}

function PageTitle(props: { title: string; description: string }) {
  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight">{props.title}</h1>
      <p className="mt-2 max-w-4xl text-sm leading-6 text-slate-400">{props.description}</p>
    </div>
  );
}

function PanelTitle(props: { title: string; subtitle: string }) {
  return (
    <div className="mb-4">
      <h2 className="text-xl font-semibold">{props.title}</h2>
      <p className="mt-1 text-sm leading-6 text-slate-400">{props.subtitle}</p>
    </div>
  );
}

function Placeholder(props: {
  heightClass: string;
  children?: ReactNode;
}) {
  return (
    <div
      className={[
        "flex w-full items-center justify-center rounded-2xl border border-dashed border-slate-700",
        "bg-slate-950/50 text-sm text-slate-500",
        props.heightClass
      ].join(" ")}
    >
      {props.children ?? "İçerik bu alana gelecek"}
    </div>
  );
}

function PanelCard(props: {
  title: string;
  subtitle: string;
  children: ReactNode;
}) {
  return (
    <div className="rounded-3xl border border-slate-800 bg-slate-900 p-5 shadow-panel">
      <h3 className="text-lg font-semibold">{props.title}</h3>
      <p className="mt-1 text-sm leading-6 text-slate-400">{props.subtitle}</p>
      <div className="mt-4">{props.children}</div>
    </div>
  );
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

function Tag(props: { text: string }) {
  return (
    <span className="rounded-full border border-slate-700 bg-slate-900 px-3 py-1">
      {props.text}
    </span>
  );
}

function ControlButton(props: {
  children: ReactNode;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={props.onClick}
      className="rounded-xl border border-slate-700 bg-slate-950/80 px-3 py-2 text-sm font-medium text-slate-200 transition hover:border-cyan-500/40 hover:text-cyan-300"
    >
      {props.children}
    </button>
  );
}