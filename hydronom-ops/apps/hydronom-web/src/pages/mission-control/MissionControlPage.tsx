import { useMemo } from "react";
import type { ReactNode } from "react";
import { useFleetStore } from "../../features/fleet-management/store/fleet.store";
import { useVehicleStore } from "../../features/vehicle-state/store/vehicle.store";
import { useMissionStore } from "../../features/mission-state/store/mission.store";
import { useActuatorStore } from "../../features/actuator-monitoring/store/actuator.store";
import { useSensorStore } from "../../features/sensor-visualization/store/sensor.store";
import { MissionMapPanel } from "../../widgets/mission-map/MissionMapPanel";
import { SensorSummaryCard } from "../../widgets/sensor-summary/SensorSummaryCard";
import { OccupancySummaryCard } from "../../widgets/occupancy-summary/OccupancySummaryCard";
import { DiagnosticsSummaryCard } from "../../widgets/diagnostics-summary/DiagnosticsSummaryCard";

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

// Ana operasyon ekranı burada şekillenecek
export function MissionControlPage() {
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

  const activeStep = mission?.steps?.find(
    (step) => step.id === mission.activeStepId
  );

  const activeThrusterCount =
    actuator?.thrusters?.filter((thruster) => thruster.active).length ?? 0;

  const obsAheadFlag =
    telemetry?.flags?.find((flag) => flag.key === "obsAhead")?.value ??
    telemetry?.obstacleAhead ??
    false;

  const headingDeg = telemetry?.map?.headingDeg ?? telemetry?.headingDeg;
  const speed = telemetry?.motion?.speed;

  const worldX = telemetry?.map?.worldPosition?.x ?? telemetry?.x;
  const worldY = telemetry?.map?.worldPosition?.y ?? telemetry?.y;

  const poseX = telemetry?.pose?.position?.x ?? telemetry?.x;
  const poseY = telemetry?.pose?.position?.y ?? telemetry?.y;
  const poseZ = telemetry?.pose?.position?.z ?? telemetry?.z;

  const roll = telemetry?.pose?.orientation?.roll ?? telemetry?.rollDeg;
  const pitch = telemetry?.pose?.orientation?.pitch ?? telemetry?.pitchDeg;
  const yaw = telemetry?.pose?.orientation?.yaw ?? telemetry?.yawDeg;

  const forceX = actuator?.wrench?.forceBody?.x;
  const forceY = actuator?.wrench?.forceBody?.y;
  const forceZ = actuator?.wrench?.forceBody?.z;

  const torqueX = actuator?.wrench?.torqueBody?.x;
  const torqueY = actuator?.wrench?.torqueBody?.y;
  const torqueZ = actuator?.wrench?.torqueBody?.z;

  const rawLandmarks = (telemetry?.landmarks ?? []) as LandmarkLike[];

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

  const ekfTrailLandmark = useMemo(
    () =>
      rawLandmarks.find(
        (landmark) =>
          landmark.type === "trail_ekf" ||
          landmark.id === "ekf_trail"
      ),
    [rawLandmarks]
  );

  const ekfPoseLandmark = useMemo(
    () =>
      rawLandmarks.find(
        (landmark) =>
          landmark.type === "ekf_pose" ||
          landmark.id === "ekf_pose"
      ),
    [rawLandmarks]
  );

  const odomHintLandmark = useMemo(
    () =>
      rawLandmarks.find(
        (landmark) =>
          landmark.type === "odometry" ||
          landmark.id === "odom_hint"
      ),
    [rawLandmarks]
  );

  const trailCount = telemetry?.map?.trail?.length ?? 0;
  const routeCount = mission?.route?.length ?? 0;
  const waypointCount = mission?.waypoints?.length ?? 0;
  const obstacleCount =
    telemetry?.obstacles?.length ?? telemetry?.obstacleCount ?? 0;

  const lidarCount = sensor?.lidarPoints?.length ?? 0;
  const occupancyWidth = sensor?.occupancy?.width ?? 0;
  const occupancyHeight = sensor?.occupancy?.height ?? 0;
  const occupiedCellCount =
    sensor?.occupancy?.occupiedCellCount ??
    occupancyCellsLandmark?.points?.length ??
    0;

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

  const mapTrail = useMemo(() => {
    if (!vehiclePosition) return [];
    return telemetry?.map?.trail?.length
      ? telemetry.map.trail
      : [vehiclePosition];
  }, [telemetry?.map?.trail, vehiclePosition]);

  const mapRoute = useMemo(() => {
    return mission?.route ?? [];
  }, [mission?.route]);

  const mapGoal = useMemo(() => {
    return mission?.goalPosition ?? null;
  }, [mission?.goalPosition]);

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

  return (
    <section className="space-y-6">
      <PageTitle
        title="Mission Control"
        description="Ana operasyon ekranı. Harita, telemetri, görev özeti, sensörler, occupancy-grid ve actuator durumu burada birleşecek."
      />

      <div className="grid grid-cols-1 gap-6 2xl:grid-cols-12">
        <div className="space-y-6 2xl:col-span-8">
          <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6 shadow-panel">
            <PanelTitle
              title="World View"
              subtitle="2D operasyon haritası. Araç, rota, hedef, trail, lidar, runtime obstacle, occupancy-grid ve EKF/odometry katmanları burada gösteriliyor."
            />

            <div className="grid grid-cols-1 gap-4 lg:grid-cols-4">
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
            </div>

            <div className="mt-4">
              {hasMapData && vehiclePosition ? (
                <MissionMapPanel
                  vehiclePosition={vehiclePosition}
                  headingDeg={telemetry?.map?.headingDeg ?? telemetry?.headingDeg ?? 0}
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
              ) : (
                <Placeholder heightClass="h-[440px]">
                  Harita verisi henüz hazır değil
                </Placeholder>
              )}
            </div>

            <div className="mt-4 flex flex-wrap items-center gap-2 text-xs text-slate-400">
              <Tag text={`x: ${formatNumber(worldX, 2, "0.00")}`} />
              <Tag text={`y: ${formatNumber(worldY, 2, "0.00")}`} />
              <Tag text={`Trail: ${trailCount} nokta`} />
              <Tag text={`EKF Trail: ${mapEkfTrail.length}`} />
              <Tag text={`Route: ${routeCount} nokta`} />
              <Tag text={`Waypoint: ${waypointCount}`} />
              <Tag text={`Obstacle: ${obstacleCount}`} />
              <Tag text={`Lidar: ${lidarCount}`} />
              <Tag text={`Grid: ${occupancyWidth}x${occupancyHeight}`} />
              <Tag text={`Occ Preview: ${mapOccupancyPreview.length}`} />
              <Tag text={`Occ Cells: ${mapOccupancyCells.length}`} />
              <Tag text={`Occupied: ${occupiedCellCount}`} />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
            <OccupancySummaryCard sensor={sensor} />
            <SensorSummaryCard sensor={sensor} />
          </div>
        </div>

        <div className="space-y-6 2xl:col-span-4">
          <PanelCard
            title="Telemetry Summary"
            subtitle="Heading, hız, pose, mode, arm"
          >
            <InfoRow label="Vehicle" value={telemetry?.displayName ?? "N/A"} />
            <InfoRow
              label="Mode"
              value={(telemetry?.mode ?? "unknown").toUpperCase()}
            />
            <InfoRow
              label="Arm"
              value={(telemetry?.armState ?? "disarmed").toUpperCase()}
            />
            <InfoRow
              label="Pose"
              value={`(${formatNumber(poseX, 2, "0.00")}, ${formatNumber(
                poseY,
                2,
                "0.00"
              )}, ${formatNumber(poseZ, 2, "0.00")})`}
            />
            <InfoRow
              label="RPY"
              value={`${formatNumber(roll, 1, "0.0")} / ${formatNumber(
                pitch,
                1,
                "0.0"
              )} / ${formatNumber(yaw, 1, "0.0")}°`}
            />
            <InfoRow
              label="Speed"
              value={`${formatNumber(speed, 2, "0.00")} m/s`}
            />
            <InfoRow label="Obstacles" value={String(obstacleCount)} />
            <InfoRow
              label="Source"
              value={(telemetry?.freshness?.source ?? "unknown").toUpperCase()}
            />
            <InfoRow
              label="Freshness"
              value={`${telemetry?.freshness?.ageMs ?? 0} ms`}
            />
          </PanelCard>

          <PanelCard
            title="Mission Summary"
            subtitle="Görev adımı, hedef, aktif durum"
          >
            <InfoRow label="Mission" value={mission?.missionName ?? "N/A"} />
            <InfoRow
              label="Status"
              value={(mission?.status ?? "idle").toUpperCase()}
            />
            <InfoRow
              label="Progress"
              value={`%${mission?.progressPercent ?? 0}`}
            />
            <InfoRow label="Active Step" value={activeStep?.title ?? "N/A"} />
            <InfoRow
              label="Goal"
              value={
                mission?.goalPosition
                  ? `(${formatNumber(mission.goalPosition.x, 2, "0.00")}, ${formatNumber(
                      mission.goalPosition.y,
                      2,
                      "0.00"
                    )})`
                  : "N/A"
              }
            />
            <InfoRow label="Route Points" value={String(routeCount)} />
            <InfoRow label="Waypoints" value={String(waypointCount)} />

            <div className="mt-4 rounded-2xl border border-slate-800 bg-slate-950/60 p-3">
              <div className="text-xs uppercase tracking-[0.25em] text-slate-500">
                Recent Events
              </div>

              <div className="mt-3 space-y-2">
                {(mission?.recentEvents ?? []).slice(0, 3).map((event) => (
                  <div
                    key={event.id}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2"
                  >
                    <div className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-400">
                      {event.level}
                    </div>
                    <div className="mt-1 text-sm text-slate-200">
                      {event.message}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </PanelCard>

          <PanelCard
            title="Actuator Summary"
            subtitle="Motorlar, thrust, limiter ve wrench özeti"
          >
            <InfoRow
              label="Active Thrusters"
              value={`${activeThrusterCount}/${actuator?.thrusters?.length ?? 0}`}
            />
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

            <div className="mt-4 grid grid-cols-2 gap-3">
              {(actuator?.thrusters ?? []).map((thruster) => (
                <div
                  key={thruster.id}
                  className="rounded-2xl border border-slate-800 bg-slate-950/60 p-3"
                >
                  <div className="flex items-center justify-between gap-3">
                    <div className="text-sm font-semibold text-slate-200">
                      {thruster.id}
                    </div>
                    <span
                      className={[
                        "inline-flex rounded-full px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.2em]",
                        thruster.active
                          ? "bg-emerald-500/15 text-emerald-300"
                          : "bg-slate-800 text-slate-400"
                      ].join(" ")}
                    >
                      {thruster.active ? "active" : "idle"}
                    </span>
                  </div>

                  <div className="mt-3 space-y-1 text-xs text-slate-400">
                    <div>
                      Cmd: {formatNumber(thruster.normalizedCommand, 2, "0.00")}
                    </div>
                    <div>
                      Applied: {formatNumber(thruster.appliedCommand, 2, "0.00")}
                    </div>
                    <div>RPM: {thruster.rpm ?? 0}</div>
                    <div>Dir: {(thruster.direction ?? "unknown").toUpperCase()}</div>
                  </div>
                </div>
              ))}
            </div>

            <div className="mt-4 flex flex-wrap gap-2">
              <LimiterTag label="satT" active={actuator?.limiter?.satT ?? false} />
              <LimiterTag label="satR" active={actuator?.limiter?.satR ?? false} />
              <LimiterTag label="rlT" active={actuator?.limiter?.rlT ?? false} />
              <LimiterTag label="rlR" active={actuator?.limiter?.rlR ?? false} />
              <LimiterTag label="dbT" active={actuator?.limiter?.dbT ?? false} />
              <LimiterTag label="dbR" active={actuator?.limiter?.dbR ?? false} />
              <LimiterTag
                label="assist"
                active={actuator?.limiter?.assist ?? false}
              />
              <LimiterTag label="dt" active={actuator?.limiter?.dt ?? false} />
            </div>
          </PanelCard>

          <DiagnosticsSummaryCard />
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
      <p className="mt-2 max-w-3xl text-sm text-slate-400">{props.description}</p>
    </div>
  );
}

function PanelTitle(props: { title: string; subtitle: string }) {
  return (
    <div className="mb-4">
      <h2 className="text-xl font-semibold">{props.title}</h2>
      <p className="mt-1 text-sm text-slate-400">{props.subtitle}</p>
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
      <p className="mt-1 text-sm text-slate-400">{props.subtitle}</p>
      <div className="mt-4">{props.children}</div>
    </div>
  );
}

function InfoRow(props: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-slate-800 py-2 last:border-b-0">
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

function LimiterTag(props: { label: string; active: boolean }) {
  return (
    <span
      className={[
        "rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-[0.2em]",
        props.active
          ? "bg-amber-500/15 text-amber-300"
          : "bg-slate-800 text-slate-400"
      ].join(" ")}
    >
      {props.label}
    </span>
  );
}