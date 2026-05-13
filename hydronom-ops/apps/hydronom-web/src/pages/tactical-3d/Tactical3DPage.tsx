import { useEffect, useRef, useState } from "react";
import type { MutableRefObject, PointerEvent, ReactNode } from "react";
import { Canvas, useFrame, useThree } from "@react-three/fiber";
import { Grid, OrbitControls } from "@react-three/drei";
import type { OrbitControls as OrbitControlsImpl } from "three-stdlib";
import * as THREE from "three";

import { useFleetStore } from "../../features/fleet-management/store/fleet.store";
import { useVehicleStore } from "../../features/vehicle-state/store/vehicle.store";
import { useActuatorStore } from "../../features/actuator-monitoring/store/actuator.store";
import { useMissionStore } from "../../features/mission-state/store/mission.store";
import { useWorldStore } from "../../features/world-state/store/world.store";
import { useSensorStore } from "../../features/sensor-visualization/store/sensor.store";

import { TacticalScene } from "./components/TacticalScene";
import {
  InfoRow,
  LimiterGrid,
  MetricTile,
  PanelCard,
  SensorHealthGrid,
  StatusBadge,
  ThrusterList
} from "./components/TacticalHud";

import {
  formatFreshness,
  formatNumber,
  getTelemetryArmState,
  getTelemetryDisplayName,
  getTelemetryDistanceToGoal,
  getTelemetryHeadingErrorDeg,
  getTelemetryObstacleAhead,
  getTelemetryObstacleCount,
  getTelemetryPitchDeg,
  getTelemetryRollDeg,
  getTelemetrySpeed,
  getTelemetryWorldPosition,
  getTelemetryYawDeg,
  getTelemetryYawRateDeg,
  getVec3Magnitude
} from "./lib/tactical3d-utils";

function pickFirstKey<T>(record: Record<string, T>) {
  return Object.keys(record)[0];
}

type TacticalPanelKey = "mission" | "vehicle" | "risk" | "actuator";

type FloatingPanelPosition = {
  x: number;
  y: number;
};

const initialPanelPositions: Record<TacticalPanelKey, FloatingPanelPosition> = {
  mission: { x: 20, y: 560 },
  vehicle: { x: 330, y: 560 },
  risk: { x: 960, y: 560 },
  actuator: { x: 1270, y: 560 }
};

export function Tactical3DPage() {
  const selectedVehicleId = useFleetStore((state) => state.selectedVehicleId);

  const telemetryByVehicleId = useVehicleStore((state) => state.telemetryByVehicleId);
  const actuatorByVehicleId = useActuatorStore((state) => state.actuatorByVehicleId);
  const missionByVehicleId = useMissionStore((state) => state.missionByVehicleId);
  const worldByVehicleId = useWorldStore((state) => state.worldByVehicleId);
  const sensorByVehicleId = useSensorStore((state) => state.sensorByVehicleId);

  const controlsRef = useRef<OrbitControlsImpl | null>(null);

  const [collapsedPanels, setCollapsedPanels] = useState<Record<TacticalPanelKey, boolean>>({
    mission: false,
    vehicle: false,
    risk: false,
    actuator: false
  });

  const [panelPositions, setPanelPositions] =
    useState<Record<TacticalPanelKey, FloatingPanelPosition>>(initialPanelPositions);

  const togglePanel = (panelKey: TacticalPanelKey) => {
    setCollapsedPanels((current) => ({
      ...current,
      [panelKey]: !current[panelKey]
    }));
  };

  const movePanel = (panelKey: TacticalPanelKey, position: FloatingPanelPosition) => {
    setPanelPositions((current) => ({
      ...current,
      [panelKey]: position
    }));
  };

  const visualVehicleId =
    selectedVehicleId in telemetryByVehicleId
      ? selectedVehicleId
      : selectedVehicleId in worldByVehicleId
        ? selectedVehicleId
        : selectedVehicleId in missionByVehicleId
          ? selectedVehicleId
          : selectedVehicleId in actuatorByVehicleId
            ? selectedVehicleId
            : selectedVehicleId in sensorByVehicleId
              ? selectedVehicleId
              : pickFirstKey(telemetryByVehicleId) ??
                pickFirstKey(worldByVehicleId) ??
                pickFirstKey(missionByVehicleId) ??
                pickFirstKey(actuatorByVehicleId) ??
                pickFirstKey(sensorByVehicleId) ??
                selectedVehicleId;

  const telemetry = telemetryByVehicleId[visualVehicleId];
  const actuator = actuatorByVehicleId[visualVehicleId];
  const mission = missionByVehicleId[visualVehicleId];
  const world = worldByVehicleId[visualVehicleId];
  const sensor = sensorByVehicleId[visualVehicleId];

  const worldPosition = getTelemetryWorldPosition(telemetry);
  const yawDeg = getTelemetryYawDeg(telemetry);
  const rollDeg = getTelemetryRollDeg(telemetry);
  const pitchDeg = getTelemetryPitchDeg(telemetry);
  const yawRateDeg = getTelemetryYawRateDeg(telemetry);
  const speed = getTelemetrySpeed(telemetry);
  const distanceToGoal = getTelemetryDistanceToGoal(telemetry);
  const headingErrorDeg = getTelemetryHeadingErrorDeg(telemetry);
  const obstacleAhead = getTelemetryObstacleAhead(telemetry);

  const forceBody = actuator?.wrench?.forceBody ?? { x: 0, y: 0, z: 0 };
  const torqueBody = actuator?.wrench?.torqueBody ?? { x: 0, y: 0, z: 0 };

  const forceMagnitude = getVec3Magnitude(forceBody);
  const torqueMagnitude = getVec3Magnitude(torqueBody);

  const activeStep = mission?.steps.find((step) => step.id === mission.activeStepId);
  const activeWaypoint = mission?.waypoints.find((waypoint) => !waypoint.reached);
  const reachedWaypointCount =
    mission?.waypoints.filter((waypoint) => waypoint.reached).length ?? 0;
  const waypointCount = mission?.waypoints.length ?? 0;

  const worldRouteCount = world?.route?.length ?? 0;
  const worldObjectCount = world?.objects?.length ?? 0;
  const worldCheckpointCount =
    world?.objects?.filter((object) => object.type === "checkpoint" || object.type === "finish").length ?? 0;
  const worldCompletedCheckpointCount =
    world?.objects?.filter(
      (object) =>
        (object.type === "checkpoint" || object.type === "finish") &&
        object.completed
    ).length ?? 0;
  const activeWorldPoint =
    world?.route?.find((point) => point.active) ??
    world?.route?.find((point) => !point.completed);

  const lidarCount = sensor?.lidarPoints?.length ?? 0;
  const sensorObstacleCount = sensor?.obstacles?.length ?? 0;
  const thrusterCount = actuator?.thrusters?.length ?? 0;
  const activeThrusterCount =
    actuator?.thrusters?.filter((thruster) => thruster.active).length ?? 0;

  const armState = getTelemetryArmState(telemetry);
  const totalObstacleCount = getTelemetryObstacleCount(telemetry) + sensorObstacleCount;

  return (
    <section className="h-[calc(100vh-4.5rem)] min-h-[760px] overflow-hidden rounded-3xl border border-slate-800 bg-slate-950 shadow-panel">
      <div className="relative h-full w-full overflow-hidden">
        <div className="absolute inset-0">
          <Canvas
            dpr={[1, 1.35]}
            gl={{
              antialias: true,
              powerPreference: "high-performance"
            }}
            camera={{
              position: [0, 24, -40],
              fov: 58,
              near: 0.8,
              far: 650
            }}
          >
            <color attach="background" args={["#020617"]} />
            <fog attach="fog" args={["#020617", 85, 230]} />

            <ambientLight intensity={0.72} />
            <directionalLight position={[10, 22, 10]} intensity={1.1} />
            <directionalLight position={[-8, 14, -12]} intensity={0.35} />

            <Grid
              args={[320, 320]}
              cellSize={5}
              cellThickness={0.32}
              cellColor="#263445"
              sectionSize={25}
              sectionThickness={0.85}
              sectionColor="#405064"
              fadeDistance={150}
              fadeStrength={1.6}
            />

            <TacticalScene
              telemetry={telemetry}
              actuator={actuator}
              mission={mission}
              world={world}
              sensor={sensor}
            />

            <KeyboardCameraController controlsRef={controlsRef} />

            <OrbitControls
              ref={controlsRef}
              makeDefault
              enableDamping
              dampingFactor={0.14}
              enableZoom
              enablePan
              screenSpacePanning={false}
              zoomSpeed={0.72}
              panSpeed={0.5}
              rotateSpeed={0.52}
              maxPolarAngle={Math.PI / 2.08}
              minDistance={5}
              maxDistance={150}
              target={[0, 0, 0]}
            />
          </Canvas>
        </div>

        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_center,transparent_0%,rgba(2,6,23,0.04)_54%,rgba(2,6,23,0.62)_100%)]" />
        <div className="pointer-events-none absolute inset-x-0 top-0 h-36 bg-gradient-to-b from-slate-950/78 to-transparent" />
        <div className="pointer-events-none absolute inset-x-0 bottom-0 h-40 bg-gradient-to-t from-slate-950/78 to-transparent" />

        <div className="pointer-events-auto absolute left-5 top-5 z-10 w-[min(680px,calc(100%-405px))] rounded-2xl border border-slate-700/60 bg-slate-950/32 p-4 shadow-[0_12px_40px_rgba(0,0,0,0.30)] backdrop-blur-md">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-bold tracking-tight text-slate-100">
              3D Tactical Mission View
            </h1>
            <StatusBadge
              label={telemetry?.health?.overall ?? "unknown"}
              state={telemetry?.health?.overall ?? "unknown"}
            />
            <StatusBadge
              label={armState.toUpperCase()}
              state={armState === "armed" ? "ok" : "warn"}
            />
          </div>

          <p className="mt-1.5 text-xs text-slate-400">
            Vehicle: {getTelemetryDisplayName(telemetry) ?? visualVehicleId}. WASD + Q/E ile serbest kamera, fare ile orbit/pan, scroll ile zoom. Paneller sürüklenebilir.
          </p>
        </div>

        <div className="pointer-events-auto absolute right-5 top-5 z-10 grid w-[360px] grid-cols-2 gap-2.5">
          <MetricTile label="Speed" value={`${formatNumber(speed, 2)} m/s`} />
          <MetricTile
            label="Distance"
            value={distanceToGoal !== undefined ? `${formatNumber(distanceToGoal, 2)} m` : "N/A"}
          />
          <MetricTile
            label="Heading Err"
            value={headingErrorDeg !== undefined ? `${formatNumber(headingErrorDeg, 1)}°` : "N/A"}
          />
          <MetricTile label="Yaw Rate" value={`${formatNumber(yawRateDeg, 2)} °/s`} />
        </div>

        <div className="pointer-events-auto absolute left-5 top-[132px] z-10 w-[270px] rounded-2xl border border-slate-700/60 bg-slate-950/32 px-3.5 py-3 shadow-[0_12px_36px_rgba(0,0,0,0.28)] backdrop-blur-md">
          <div className="text-[10px] font-semibold uppercase tracking-[0.25em] text-cyan-300">
            Tactical Layer
          </div>
          <div className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1 text-[11px] text-slate-300">
            <span>Route: {worldRouteCount || mission?.route?.length || 0}</span>
            <span>Objects: {worldObjectCount}</span>
            <span>LiDAR: {lidarCount}</span>
            <span>Obs: {totalObstacleCount}</span>
          </div>
        </div>

        {obstacleAhead ? (
          <div className="pointer-events-none absolute left-1/2 top-5 z-20 -translate-x-1/2 rounded-2xl border border-amber-500/40 bg-amber-950/55 px-5 py-3 text-sm font-semibold text-amber-200 shadow-[0_12px_40px_rgba(0,0,0,0.35)] backdrop-blur-md">
            OBSTACLE-AWARE ADJUSTMENT ACTIVE
          </div>
        ) : null}

        <FloatingPanel
          position={panelPositions.mission}
          width={300}
          onMove={(position) => movePanel("mission", position)}
        >
          <PanelCard
            title="Mission"
            subtitle="Hedef ve waypoint"
            collapsed={collapsedPanels.mission}
            onToggleCollapsed={() => togglePanel("mission")}
          >
            <InfoRow label="Mission" value={world?.scenarioName ?? mission?.missionName ?? "N/A"} />
            <InfoRow label="Status" value={mission?.status ?? "idle"} />
            <InfoRow
              label="Step"
              value={activeWorldPoint?.label ?? activeStep?.title ?? mission?.activeStepId ?? "N/A"}
            />
            <InfoRow
              label="Progress"
              value={mission ? `${formatNumber(mission.progressPercent, 0)}%` : "N/A"}
            />
            <InfoRow
              label="Waypoint"
              value={
                worldCheckpointCount > 0
                  ? `${worldCompletedCheckpointCount}/${worldCheckpointCount}`
                  : waypointCount > 0
                    ? `${reachedWaypointCount}/${waypointCount}`
                    : "N/A"
              }
            />
            <InfoRow
              label="Active WP"
              value={activeWorldPoint?.label ?? activeWaypoint?.label ?? "N/A"}
            />
          </PanelCard>
        </FloatingPanel>

        <FloatingPanel
          position={panelPositions.vehicle}
          width={300}
          onMove={(position) => movePanel("vehicle", position)}
        >
          <PanelCard
            title="Vehicle"
            subtitle="Pozisyon ve yönelim"
            collapsed={collapsedPanels.vehicle}
            onToggleCollapsed={() => togglePanel("vehicle")}
          >
            <InfoRow
              label="Vehicle"
              value={getTelemetryDisplayName(telemetry) ?? visualVehicleId ?? "N/A"}
            />
            <InfoRow label="Mode" value={telemetry?.mode ?? "unknown"} />
            <InfoRow
              label="World XY"
              value={
                worldPosition
                  ? `(${formatNumber(worldPosition.x, 2)}, ${formatNumber(worldPosition.y, 2)})`
                  : "N/A"
              }
            />
            <InfoRow
              label="RPY"
              value={`${formatNumber(rollDeg, 1)} / ${formatNumber(pitchDeg, 1)} / ${formatNumber(yawDeg, 1)}°`}
            />
            <InfoRow label="Freshness" value={formatFreshness(telemetry?.freshness?.ageMs)} />
          </PanelCard>
        </FloatingPanel>

        <FloatingPanel
          position={panelPositions.risk}
          width={300}
          onMove={(position) => movePanel("risk", position)}
        >
          <PanelCard
            title="Risk"
            subtitle="Algı ve obstacle"
            collapsed={collapsedPanels.risk}
            onToggleCollapsed={() => togglePanel("risk")}
          >
            <InfoRow label="Obstacle Ahead" value={obstacleAhead ? "YES" : "NO"} />
            <InfoRow label="Runtime Obs" value={`${getTelemetryObstacleCount(telemetry)}`} />
            <InfoRow label="Sensor Obs" value={`${sensorObstacleCount}`} />
            <InfoRow label="LiDAR" value={`${lidarCount} pts`} />
            <InfoRow label="Occupancy" value={`${sensor?.occupancy?.occupiedCellCount ?? 0}`} />
            <SensorHealthGrid sensor={sensor} />
          </PanelCard>
        </FloatingPanel>

        <FloatingPanel
          position={panelPositions.actuator}
          width={320}
          onMove={(position) => movePanel("actuator", position)}
        >
          <PanelCard
            title="Actuator"
            subtitle="Thruster ve limiter"
            collapsed={collapsedPanels.actuator}
            onToggleCollapsed={() => togglePanel("actuator")}
          >
            <InfoRow label="Thrusters" value={`${activeThrusterCount}/${thrusterCount} active`} />
            <InfoRow label="Force |F|" value={`${formatNumber(forceMagnitude, 2)} N`} />
            <InfoRow label="Torque |T|" value={`${formatNumber(torqueMagnitude, 2)} Nm`} />
            <LimiterGrid limiter={actuator?.limiter} />
            <ThrusterList thrusters={actuator?.thrusters ?? []} />
          </PanelCard>
        </FloatingPanel>
      </div>
    </section>
  );
}

function FloatingPanel(props: {
  position: FloatingPanelPosition;
  width: number;
  children: ReactNode;
  onMove: (position: FloatingPanelPosition) => void;
}) {
  const dragStateRef = useRef<{
    startX: number;
    startY: number;
    startPanelX: number;
    startPanelY: number;
  } | null>(null);

  const handlePointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const target = event.target as HTMLElement;

    if (target.closest("button")) {
      return;
    }

    event.currentTarget.setPointerCapture(event.pointerId);
    dragStateRef.current = {
      startX: event.clientX,
      startY: event.clientY,
      startPanelX: props.position.x,
      startPanelY: props.position.y
    };
  };

  const handlePointerMove = (event: PointerEvent<HTMLDivElement>) => {
    if (!dragStateRef.current) return;

    const deltaX = event.clientX - dragStateRef.current.startX;
    const deltaY = event.clientY - dragStateRef.current.startY;

    props.onMove({
      x: Math.max(8, dragStateRef.current.startPanelX + deltaX),
      y: Math.max(8, dragStateRef.current.startPanelY + deltaY)
    });
  };

  const handlePointerUp = (event: PointerEvent<HTMLDivElement>) => {
    dragStateRef.current = null;

    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
  };

  return (
    <div
      className="pointer-events-auto absolute z-10 cursor-move select-none"
      style={{
        left: props.position.x,
        top: props.position.y,
        width: props.width
      }}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerCancel={handlePointerUp}
    >
      {props.children}
    </div>
  );
}

function KeyboardCameraController(props: {
  controlsRef: MutableRefObject<OrbitControlsImpl | null>;
}) {
  const { camera } = useThree();
  const pressedKeysRef = useRef<Set<string>>(new Set());
  const velocityRef = useRef(new THREE.Vector3());

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      const key = event.key.toLowerCase();

      if (["w", "a", "s", "d", "q", "e"].includes(key)) {
        event.preventDefault();
        pressedKeysRef.current.add(key);
      }
    };

    const handleKeyUp = (event: KeyboardEvent) => {
      pressedKeysRef.current.delete(event.key.toLowerCase());
    };

    window.addEventListener("keydown", handleKeyDown);
    window.addEventListener("keyup", handleKeyUp);

    return () => {
      window.removeEventListener("keydown", handleKeyDown);
      window.removeEventListener("keyup", handleKeyUp);
    };
  }, []);

  useFrame((_, delta) => {
    const keys = pressedKeysRef.current;
    const desiredMove = new THREE.Vector3();

    if (keys.size > 0) {
      const forward = new THREE.Vector3();
      camera.getWorldDirection(forward);
      forward.y = 0;

      if (forward.lengthSq() <= 0.0001) {
        forward.set(0, 0, -1);
      }

      forward.normalize();

      const right = new THREE.Vector3()
        .crossVectors(forward, new THREE.Vector3(0, 1, 0))
        .normalize();

      const up = new THREE.Vector3(0, 1, 0);

      if (keys.has("w")) desiredMove.add(forward);
      if (keys.has("s")) desiredMove.sub(forward);
      if (keys.has("d")) desiredMove.add(right);
      if (keys.has("a")) desiredMove.sub(right);
      if (keys.has("e")) desiredMove.add(up);
      if (keys.has("q")) desiredMove.sub(up);

      if (desiredMove.lengthSq() > 0) {
        desiredMove.normalize().multiplyScalar(16);
      }
    }

    const smoothing = 1 - Math.exp(-10 * delta);
    velocityRef.current.lerp(desiredMove, smoothing);

    if (velocityRef.current.lengthSq() < 0.00001) {
      velocityRef.current.set(0, 0, 0);
      return;
    }

    const frameMove = velocityRef.current.clone().multiplyScalar(delta);

    camera.position.add(frameMove);

    if (props.controlsRef.current) {
      props.controlsRef.current.target.add(frameMove);
      props.controlsRef.current.update();
    }
  });

  return null;
}