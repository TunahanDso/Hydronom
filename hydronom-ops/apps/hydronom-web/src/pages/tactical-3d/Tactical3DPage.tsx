import { useEffect, useMemo, useRef } from "react";
import type { ReactNode } from "react";
import * as THREE from "three";
import { Canvas, useFrame } from "@react-three/fiber";
import { Grid, Line, OrbitControls, Points, PointMaterial } from "@react-three/drei";

import { useFleetStore } from "../../features/fleet-management/store/fleet.store";
import { useVehicleStore } from "../../features/vehicle-state/store/vehicle.store";
import { useActuatorStore } from "../../features/actuator-monitoring/store/actuator.store";
import { useMissionStore } from "../../features/mission-state/store/mission.store";
import { useSensorStore } from "../../features/sensor-visualization/store/sensor.store";

type Vec3Like = { x: number; y: number; z: number };
type Vec2Like = { x: number; y: number };
type ObstacleLike = { x: number; y: number; r: number };

type LooseRecord = Record<string, unknown>;

function asRecord(value: unknown): LooseRecord | undefined {
  return value && typeof value === "object" ? (value as LooseRecord) : undefined;
}

function getNumber(obj: unknown, key: string): number | undefined {
  const rec = asRecord(obj);
  const value = rec?.[key];
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function getString(obj: unknown, key: string): string | undefined {
  const rec = asRecord(obj);
  const value = rec?.[key];
  return typeof value === "string" ? value : undefined;
}

function getArray<T = unknown>(obj: unknown, key: string): T[] {
  const rec = asRecord(obj);
  const value = rec?.[key];
  return Array.isArray(value) ? (value as T[]) : [];
}

function getNested(obj: unknown, ...path: string[]): unknown {
  let current: unknown = obj;
  for (const key of path) {
    const rec = asRecord(current);
    if (!rec || !(key in rec)) return undefined;
    current = rec[key];
  }
  return current;
}

function getTelemetryWorldPosition(telemetry: unknown): Vec3Like | undefined {
  const worldPosition = getNested(telemetry, "map", "worldPosition");
  const wx = getNumber(worldPosition, "x");
  const wy = getNumber(worldPosition, "y");
  const wz = getNumber(worldPosition, "z") ?? 0;

  if (wx !== undefined && wy !== undefined) {
    return { x: wx, y: wy, z: wz };
  }

  const x = getNumber(telemetry, "x");
  const y = getNumber(telemetry, "y");
  const z = getNumber(telemetry, "z") ?? 0;

  if (x !== undefined && y !== undefined) {
    return { x, y, z };
  }

  return undefined;
}

function getTelemetryYawDeg(telemetry: unknown): number {
  return (
    getNumber(getNested(telemetry, "pose", "orientation"), "yaw") ??
    getNumber(telemetry, "yawDeg") ??
    getNumber(telemetry, "headingDeg") ??
    0
  );
}

function getTelemetryRollDeg(telemetry: unknown): number {
  return (
    getNumber(getNested(telemetry, "pose", "orientation"), "roll") ??
    getNumber(telemetry, "rollDeg") ??
    0
  );
}

function getTelemetryPitchDeg(telemetry: unknown): number {
  return (
    getNumber(getNested(telemetry, "pose", "orientation"), "pitch") ??
    getNumber(telemetry, "pitchDeg") ??
    0
  );
}

function getTelemetrySpeed(telemetry: unknown): number {
  const motionSpeed = getNumber(getNested(telemetry, "motion"), "speed");
  if (motionSpeed !== undefined) return motionSpeed;

  const vx = getNumber(telemetry, "vx") ?? 0;
  const vy = getNumber(telemetry, "vy") ?? 0;
  const vz = getNumber(telemetry, "vz") ?? 0;

  return Math.sqrt(vx * vx + vy * vy + vz * vz);
}

function getTelemetryObstacleCount(telemetry: unknown): number {
  return getNumber(telemetry, "obstacleCount") ?? getTelemetryObstacles(telemetry).length;
}

function getTelemetryObstacles(telemetry: unknown): ObstacleLike[] {
  return getArray<unknown>(telemetry, "obstacles")
    .map((item) => {
      const x = getNumber(item, "x");
      const y = getNumber(item, "y");
      const r = getNumber(item, "r");
      if (x === undefined || y === undefined || r === undefined) return null;
      return { x, y, r };
    })
    .filter((item): item is ObstacleLike => item !== null);
}

function getTelemetryTargetX(telemetry: unknown): number | undefined {
  return getNumber(telemetry, "targetX");
}

function getTelemetryTargetY(telemetry: unknown): number | undefined {
  return getNumber(telemetry, "targetY");
}

function getTelemetryDisplayName(telemetry: unknown): string | undefined {
  return getString(telemetry, "displayName");
}

function getTelemetryArmState(telemetry: unknown): string {
  return getString(telemetry, "armState") ?? "disarmed";
}

function getTelemetryPosePosition(telemetry: unknown): Vec3Like {
  const posePosition = getNested(telemetry, "pose", "position");
  const mapWorld = getNested(telemetry, "map", "worldPosition");

  return {
    x:
      getNumber(posePosition, "x") ??
      getNumber(mapWorld, "x") ??
      getNumber(telemetry, "x") ??
      0,
    y:
      getNumber(posePosition, "y") ??
      getNumber(mapWorld, "y") ??
      getNumber(telemetry, "y") ??
      0,
    z:
      getNumber(posePosition, "z") ??
      getNumber(mapWorld, "z") ??
      getNumber(telemetry, "z") ??
      0
  };
}

export function Tactical3DPage() {
  const selectedVehicleId = useFleetStore((state) => state.selectedVehicleId);

  const telemetry = useVehicleStore((state) =>
    selectedVehicleId ? state.telemetryByVehicleId[selectedVehicleId] : undefined
  );

  const actuator = useActuatorStore((state) =>
    selectedVehicleId ? state.actuatorByVehicleId[selectedVehicleId] : undefined
  );

  const mission = useMissionStore((state) =>
    selectedVehicleId ? state.missionByVehicleId[selectedVehicleId] : undefined
  );

  const sensor = useSensorStore((state) =>
    selectedVehicleId ? state.sensorByVehicleId[selectedVehicleId] : undefined
  );

  const worldPosition = getTelemetryWorldPosition(telemetry);
  const yawDeg = getTelemetryYawDeg(telemetry);
  const rollDeg = getTelemetryRollDeg(telemetry);
  const pitchDeg = getTelemetryPitchDeg(telemetry);
  const speed = getTelemetrySpeed(telemetry);

  const forceBody = actuator?.wrench?.forceBody ?? { x: 0, y: 0, z: 0 };
  const torqueBody = actuator?.wrench?.torqueBody ?? { x: 0, y: 0, z: 0 };

  const hasSceneData = Boolean(worldPosition);

  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6 shadow-panel">
      <h1 className="text-3xl font-bold tracking-tight">3D Tactical View</h1>
      <p className="mt-2 max-w-3xl text-sm text-slate-400">
        Gerçek 3D taktik görünüm. Araç gövdesi, yönelim, thruster yerleşimi,
        takip kamerası, trail, hedef, LiDAR noktaları ve runtime obstacle
        marker’ları burada gösterilir.
      </p>

      <div className="mt-6 grid grid-cols-1 gap-6 xl:grid-cols-12">
        <div className="xl:col-span-9">
          <div className="h-[780px] overflow-hidden rounded-3xl border border-slate-800 bg-slate-950">
            {hasSceneData ? (
              <Canvas camera={{ position: [0, 20, -30], fov: 60, near: 0.1, far: 2000 }}>
                <color attach="background" args={["#020617"]} />
                <fog attach="fog" args={["#020617", 80, 260]} />

                <ambientLight intensity={0.7} />
                <directionalLight position={[10, 20, 10]} intensity={1.1} />
                <directionalLight position={[-8, 14, -12]} intensity={0.35} />

                <Grid
                  args={[400, 400]}
                  cellSize={5}
                  cellThickness={0.5}
                  cellColor="#334155"
                  sectionSize={25}
                  sectionThickness={1.2}
                  sectionColor="#475569"
                  fadeDistance={260}
                  fadeStrength={1}
                  infiniteGrid
                />

                <axesHelper args={[5]} />

                <BoatSceneContent
                  telemetry={telemetry}
                  actuator={actuator}
                  mission={mission}
                  sensor={sensor}
                />

                <OrbitControls
                  enableDamping
                  dampingFactor={0.08}
                  maxPolarAngle={Math.PI / 2.1}
                  minDistance={8}
                  maxDistance={140}
                  target={[0, 0, 0]}
                />
              </Canvas>
            ) : (
              <Placeholder heightClass="h-full">
                3D sahne için henüz araç telemetrisi yok
              </Placeholder>
            )}
          </div>
        </div>

        <div className="space-y-6 xl:col-span-3">
          <PanelCard title="Vehicle State" subtitle="Pozisyon, yönelim ve temel durum">
            <InfoRow
              label="Vehicle"
              value={getTelemetryDisplayName(telemetry) ?? selectedVehicleId ?? "N/A"}
            />
            <InfoRow
              label="World XY"
              value={
                worldPosition
                  ? `(${formatNumber(worldPosition.x, 2)}, ${formatNumber(worldPosition.y, 2)})`
                  : "N/A"
              }
            />
            <InfoRow
              label="Roll / Pitch / Yaw"
              value={`${formatNumber(rollDeg, 1)} / ${formatNumber(
                pitchDeg,
                1
              )} / ${formatNumber(yawDeg, 1)}°`}
            />
            <InfoRow label="Speed" value={`${formatNumber(speed, 2)} m/s`} />
            <InfoRow label="Obstacles" value={`${getTelemetryObstacleCount(telemetry)}`} />
          </PanelCard>

          <PanelCard title="Force / Torque" subtitle="Body frame vektör özeti">
            <InfoRow
              label="Force"
              value={`(${formatNumber(forceBody.x, 2)}, ${formatNumber(
                forceBody.y,
                2
              )}, ${formatNumber(forceBody.z, 2)})`}
            />
            <InfoRow
              label="Torque"
              value={`(${formatNumber(torqueBody.x, 2)}, ${formatNumber(
                torqueBody.y,
                2
              )}, ${formatNumber(torqueBody.z, 2)})`}
            />
          </PanelCard>

          <PanelCard title="Scene Notes" subtitle="Bu sürümde çalışan katmanlar">
            <ul className="space-y-2 text-sm text-slate-300">
              <li>• Gerçek 3D tekne gövdesi</li>
              <li>• Takip kamerası</li>
              <li>• Trail line</li>
              <li>• Goal marker</li>
              <li>• LiDAR point cloud</li>
              <li>• Runtime obstacle spheres</li>
              <li>• Thruster görselleştirmesi</li>
              <li>• Force vector</li>
            </ul>
          </PanelCard>
        </div>
      </div>
    </section>
  );
}

function BoatSceneContent(props: {
  telemetry: unknown;
  actuator: any;
  mission: any;
  sensor: any;
}) {
  const { telemetry, actuator, mission, sensor } = props;

  const groupRef = useRef<THREE.Group | null>(null);
  const cameraTargetRef = useRef(new THREE.Vector3());
  const originRef = useRef<Vec3Like | null>(null);
  const trailRef = useRef<THREE.Vector3[]>([]);

  const pose = getTelemetryPosePosition(telemetry);

  const rawX = pose.x;
  const rawY = pose.y;
  const rawZ = pose.z;

  const yawDeg = getTelemetryYawDeg(telemetry);
  const rollDeg = getTelemetryRollDeg(telemetry);
  const pitchDeg = getTelemetryPitchDeg(telemetry);

  const isArmed = getTelemetryArmState(telemetry).toLowerCase() === "armed";
  const forceBody = actuator?.wrench?.forceBody ?? { x: 0, y: 0, z: 0 };

  if (!originRef.current) {
    originRef.current = { x: rawX, y: rawY, z: rawZ };
  }

  const local = {
    x: rawX - (originRef.current?.x ?? 0),
    y: rawY - (originRef.current?.y ?? 0),
    z: rawZ - (originRef.current?.z ?? 0)
  };

  const boatPosition = useMemo<[number, number, number]>(() => {
    return [local.x, 0, -local.y];
  }, [local.x, local.y]);

  const targetPosition = useMemo<[number, number, number] | null>(() => {
    const goalX = mission?.goalPosition?.x ?? getTelemetryTargetX(telemetry);
    const goalY = mission?.goalPosition?.y ?? getTelemetryTargetY(telemetry);

    if (typeof goalX === "number" && typeof goalY === "number") {
      return [
        goalX - (originRef.current?.x ?? 0),
        1.5,
        -(goalY - (originRef.current?.y ?? 0))
      ];
    }

    return null;
  }, [mission?.goalPosition?.x, mission?.goalPosition?.y, telemetry]);

  const lidarPoints = useMemo<Float32Array>(() => {
    const pts = (sensor?.lidarPoints ?? []) as Vec2Like[];
    const array: number[] = [];

    for (const p of pts) {
      if (
        typeof p?.x === "number" &&
        Number.isFinite(p.x) &&
        typeof p?.y === "number" &&
        Number.isFinite(p.y)
      ) {
        array.push(p.x, 0.05, -p.y);
      }
    }

    return new Float32Array(array);
  }, [sensor?.lidarPoints]);

  const obstacles = useMemo(() => {
    const raw = getTelemetryObstacles(telemetry);

    return raw.map((o) => ({
      x: o.x - (originRef.current?.x ?? 0),
      y: o.y - (originRef.current?.y ?? 0),
      r: Math.max(0.05, o.r)
    }));
  }, [telemetry]);

  const thrusters = useMemo(() => {
    const source = actuator?.thrusters ?? [];
    return source.map((thruster: any, index: number) => ({
      id: thruster.id ?? `T${index + 1}`,
      active: Boolean(thruster.active),
      rpm: thruster.rpm ?? 0,
      normalizedCommand: thruster.normalizedCommand ?? 0,
      position: getThrusterPosition(index)
    }));
  }, [actuator?.thrusters]);

  const forceVector = useMemo<[number, number, number]>(() => {
    const fx = forceBody?.x ?? 0;
    const fy = forceBody?.y ?? 0;
    const fz = forceBody?.z ?? 0;

    const original = new THREE.Vector3(fx, -fz, fy);
    const magnitude = original.length();

    if (magnitude < 0.05) return [0, 0, 0];

    const vec = original.clone().normalize();
    vec.multiplyScalar(Math.min(8, Math.max(2, magnitude * 0.5)));
    return [vec.x, vec.y, vec.z];
  }, [forceBody?.x, forceBody?.y, forceBody?.z]);

  useEffect(() => {
    const next = new THREE.Vector3(boatPosition[0], boatPosition[1] + 0.01, boatPosition[2]);
    const trail = trailRef.current;
    const last = trail[trail.length - 1];

    if (!last || last.distanceTo(next) > 0.05) {
      trail.push(next);
      if (trail.length > 2000) trail.shift();
    }
  }, [boatPosition]);

  useFrame(({ camera }) => {
    if (!groupRef.current) return;

    const desiredCamera = new THREE.Vector3(boatPosition[0], 12, boatPosition[2] + 22);

    camera.position.lerp(desiredCamera, 0.08);

    cameraTargetRef.current.lerp(new THREE.Vector3(boatPosition[0], 0, boatPosition[2]), 0.1);

    camera.lookAt(cameraTargetRef.current);
  });

  return (
    <>
      <group ref={groupRef} position={boatPosition}>
        <BoatModel
          yawDeg={yawDeg}
          rollDeg={rollDeg}
          pitchDeg={pitchDeg}
          isArmed={isArmed}
          thrusters={thrusters}
        />
        <ForceArrow vector={forceVector} />
      </group>

      {trailRef.current.length > 1 ? (
        <Line points={trailRef.current} color="#38bdf8" lineWidth={2} />
      ) : null}

      {targetPosition ? <GoalMarker position={targetPosition} /> : null}

      {lidarPoints.length > 0 ? (
        <Points positions={lidarPoints} stride={3}>
          <PointMaterial size={0.22} color="#4ade80" sizeAttenuation />
        </Points>
      ) : null}

      {obstacles.map((obstacle, index) => (
        <ObstacleMarker
          key={`obstacle-${index}`}
          position={[obstacle.x, Math.max(0.12, obstacle.r * 0.35), -obstacle.y]}
          radius={obstacle.r}
        />
      ))}
    </>
  );
}

function BoatModel(props: {
  yawDeg: number;
  rollDeg: number;
  pitchDeg: number;
  isArmed: boolean;
  thrusters: Array<{
    id: string;
    active: boolean;
    rpm: number;
    normalizedCommand: number;
    position: [number, number, number];
  }>;
}) {
  const hullRef = useRef<THREE.Mesh | null>(null);

  useFrame(() => {
    if (!hullRef.current) return;
    const material = hullRef.current.material as THREE.MeshStandardMaterial;
    material.emissive.setHex(props.isArmed ? 0x22c55e : 0xef4444);
    material.emissiveIntensity = 0.28;
  });

  return (
    <group rotation={[deg2rad(props.rollDeg), deg2rad(props.yawDeg), deg2rad(props.pitchDeg)]}>
      <mesh ref={hullRef} castShadow receiveShadow>
        <boxGeometry args={[4, 0.6, 1.6]} />
        <meshStandardMaterial color="#38bdf8" metalness={0.5} roughness={0.2} />
      </mesh>

      <mesh position={[0, 0.45, 0]} castShadow>
        <boxGeometry args={[1.1, 0.35, 0.8]} />
        <meshStandardMaterial color="#94a3b8" metalness={0.35} roughness={0.45} />
      </mesh>

      {props.thrusters.map((thruster) => (
        <ThrusterMesh
          key={thruster.id}
          position={thruster.position}
          active={thruster.active}
          rpm={thruster.rpm}
        />
      ))}
    </group>
  );
}

function ThrusterMesh(props: {
  position: [number, number, number];
  active: boolean;
  rpm: number;
}) {
  const ref = useRef<THREE.Mesh | null>(null);

  useFrame(() => {
    if (!ref.current) return;
    ref.current.rotation.z += (props.rpm ?? 0) / 60000;
  });

  return (
    <mesh ref={ref} position={props.position} rotation={[Math.PI / 2, 0, 0]} castShadow>
      <cylinderGeometry args={[0.3, 0.3, 0.1, 10]} />
      <meshStandardMaterial color={props.active ? "#facc15" : "#64748b"} />
    </mesh>
  );
}

function ForceArrow(props: { vector: [number, number, number] }) {
  const vec = new THREE.Vector3(...props.vector);
  const length = vec.length();

  if (length < 0.001) return null;

  const dir = vec.clone().normalize();
  const midpoint = dir.clone().multiplyScalar(length * 0.5);

  const quat = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);

  return (
    <group position={[0, 0, 0]}>
      <mesh position={[midpoint.x, midpoint.y, midpoint.z]} quaternion={quat}>
        <cylinderGeometry args={[0.05, 0.05, length, 8]} />
        <meshStandardMaterial color="#ef4444" />
      </mesh>

      <mesh position={[dir.x * length, dir.y * length, dir.z * length]} quaternion={quat}>
        <coneGeometry args={[0.16, 0.42, 12]} />
        <meshStandardMaterial color="#ef4444" />
      </mesh>
    </group>
  );
}

function GoalMarker(props: { position: [number, number, number] }) {
  return (
    <group position={props.position}>
      <mesh>
        <coneGeometry args={[0.8, 3, 16]} />
        <meshStandardMaterial color="#f97316" />
      </mesh>
    </group>
  );
}

function ObstacleMarker(props: {
  position: [number, number, number];
  radius: number;
}) {
  const pulseRef = useRef<THREE.Mesh | null>(null);

  useFrame(({ clock }) => {
    if (!pulseRef.current) return;
    const t = clock.getElapsedTime();
    const s = 1 + Math.sin(t * 2.2) * 0.08;
    pulseRef.current.scale.setScalar(s);
  });

  return (
    <group position={props.position}>
      <mesh rotation={[-Math.PI / 2, 0, 0]}>
        <ringGeometry args={[Math.max(0.05, props.radius * 0.92), Math.max(0.06, props.radius), 32]} />
        <meshBasicMaterial color="#f59e0b" transparent opacity={0.9} side={THREE.DoubleSide} />
      </mesh>

      <mesh ref={pulseRef} position={[0, Math.max(0.08, props.radius * 0.18), 0]}>
        <sphereGeometry args={[Math.max(0.08, props.radius * 0.18), 20, 20]} />
        <meshStandardMaterial
          color="#fb923c"
          emissive="#f97316"
          emissiveIntensity={0.45}
          transparent
          opacity={0.9}
        />
      </mesh>
    </group>
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
      <div className="text-right text-sm font-medium text-slate-100">{props.value}</div>
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

function formatNumber(value: number, fractionDigits: number) {
  return Number.isFinite(value) ? value.toFixed(fractionDigits) : "0.00";
}

function deg2rad(deg: number) {
  return (deg * Math.PI) / 180;
}

function getThrusterPosition(index: number): [number, number, number] {
  const positions: [number, number, number][] = [
    [1.8, -0.3, 0.8],
    [1.8, -0.3, -0.8],
    [-1.8, -0.3, 0.8],
    [-1.8, -0.3, -0.8],
    [0, -0.3, 0.9],
    [0, -0.3, -0.9]
  ];

  return positions[index] ?? [0, -0.3, 0];
}