import { useEffect, useMemo, useRef } from "react";
import * as THREE from "three";
import { useFrame } from "@react-three/fiber";
import { Line, Points, PointMaterial } from "@react-three/drei";

import type { ActuatorState } from "../../../entities/actuator/model/actuator.types";
import type { MissionState } from "../../../entities/mission/model/mission.types";
import type { SensorState } from "../../../entities/sensor/model/sensor.types";
import type { VehicleTelemetry } from "../../../entities/vehicle/model/vehicle.types";
import type { WorldObject, WorldState } from "../../../entities/world/model/world.types";

import type {
  SceneRoute,
  SceneThruster,
  SceneWaypoint,
  Vec2Like,
  Vec3Like
} from "../types";

import {
  buildSceneRoute,
  deg2rad,
  getTelemetryArmState,
  getTelemetryObstacles,
  getTelemetryPitchDeg,
  getTelemetryPosePosition,
  getTelemetryRollDeg,
  getTelemetrySpeed,
  getTelemetryYawDeg,
  getThrusterPosition
} from "../lib/tactical3d-utils";

const VEHICLE_LENGTH_M = 1.0;
const VEHICLE_WIDTH_M = 1.0;
const VEHICLE_HEIGHT_M = 0.4;

const PONTOON_LENGTH_M = 1.0;
const PONTOON_RADIUS_M = 0.105;
const PONTOON_CENTER_OFFSET_Z_M = 0.34;

const DECK_LENGTH_M = 0.76;
const DECK_WIDTH_M = 0.86;
const DECK_HEIGHT_M = 0.06;

const CABIN_LENGTH_M = 0.26;
const CABIN_WIDTH_M = 0.30;
const CABIN_HEIGHT_M = 0.14;

const REAL_BUOY_RADIUS_M = 0.15;
const REAL_BUOY_HEIGHT_M = 0.50;

const WAYPOINT_POST_HEIGHT_M = 0.34;
const WAYPOINT_POST_RADIUS_M = 0.055;

export function TacticalScene(props: {
  telemetry: VehicleTelemetry | undefined;
  actuator: ActuatorState | undefined;
  mission: MissionState | undefined;
  world: WorldState | undefined;
  sensor: SensorState | undefined;
}) {
  return (
    <BoatSceneContent
      telemetry={props.telemetry}
      actuator={props.actuator}
      mission={props.mission}
      world={props.world}
      sensor={props.sensor}
    />
  );
}

function BoatSceneContent(props: {
  telemetry: VehicleTelemetry | undefined;
  actuator: ActuatorState | undefined;
  mission: MissionState | undefined;
  world: WorldState | undefined;
  sensor: SensorState | undefined;
}) {
  const { telemetry, actuator, mission, world, sensor } = props;

  const groupRef = useRef<THREE.Group | null>(null);
  const originRef = useRef<Vec3Like | null>(null);
  const trailRef = useRef<THREE.Vector3[]>([]);

  const pose = getTelemetryPosePosition(telemetry);

  const rawX = pose.x;
  const rawY = pose.y;
  const rawZ = pose.z;

  const yawDeg = getTelemetryYawDeg(telemetry);
  const rollDeg = getTelemetryRollDeg(telemetry);
  const pitchDeg = getTelemetryPitchDeg(telemetry);
  const speed = getTelemetrySpeed(telemetry);

  const isArmed = getTelemetryArmState(telemetry).toLowerCase() === "armed";
  const forceBody = actuator?.wrench?.forceBody ?? { x: 0, y: 0, z: 0 };
  const torqueBody = actuator?.wrench?.torqueBody ?? { x: 0, y: 0, z: 0 };

  if (!originRef.current) {
    originRef.current = { x: rawX, y: rawY, z: rawZ };
  }

  const origin = originRef.current;

  const local = {
    x: rawX - origin.x,
    y: rawY - origin.y,
    z: rawZ - origin.z
  };

  const boatPosition = useMemo<[number, number, number]>(() => {
    return [local.x, local.z, -local.y];
  }, [local.x, local.y, local.z]);

  const missionSceneRoute = useMemo<SceneRoute>(() => {
    return buildSceneRoute(mission, telemetry, origin);
  }, [mission, telemetry, origin]);

  const worldRoutePoints = useMemo<THREE.Vector3[]>(() => {
    const route = world?.route ?? [];

    return route
      .slice()
      .sort((a, b) => a.index - b.index)
      .map((point) => worldToSceneVector(point.x, point.y, point.z, origin, 0.08));
  }, [world?.route, origin]);

  const worldWaypointObjects = useMemo(() => {
    const objects = world?.objects ?? [];

    return objects
      .filter((object) =>
        object.type === "start" ||
        object.type === "checkpoint" ||
        object.type === "finish" ||
        object.type === "gate"
      )
      .map((object) => ({
        object,
        waypoint: worldObjectToWaypoint(object, origin)
      }));
  }, [world?.objects, origin]);

  const worldBuoys = useMemo(() => {
    const objects = world?.objects ?? [];

    return objects
      .filter((object) => object.type === "buoy")
      .map((object) => ({
        id: object.id,
        side: normalizeBuoySide(object.side),
        position: worldToSceneTuple(object.x, object.y, object.z, origin),
        radius: getWorldObjectNumber(object, "radius", REAL_BUOY_RADIUS_M),
        height: getWorldObjectNumber(object, "height", REAL_BUOY_HEIGHT_M),
        color: object.color ?? undefined,
        label: object.label ?? object.id
      }));
  }, [world?.objects, origin]);

  const worldObjectObstacles = useMemo(() => {
    const objects = world?.objects ?? [];

    return objects
      .filter((object) => object.type === "obstacle")
      .map((object) => ({
        x: object.x - origin.x,
        y: object.y - origin.y,
        z: object.z - origin.z,
        r: Math.max(0.05, object.radius),
        height: getWorldObjectNumber(object, "height", REAL_BUOY_HEIGHT_M),
        color: object.color ?? "#ef4444",
        source: "world"
      }));
  }, [world?.objects, origin]);

  const activeObjectivePosition = useMemo<[number, number, number] | null>(() => {
    const target = world?.activeObjectiveTarget;

    if (target) {
      return worldToSceneTuple(target.x, target.y, target.z, origin);
    }

    const activeRoute = world?.route?.find((point) => point.active);
    if (activeRoute) {
      return worldToSceneTuple(activeRoute.x, activeRoute.y, activeRoute.z, origin);
    }

    const activeObject = world?.objects?.find((object) => object.active);
    if (activeObject) {
      return worldToSceneTuple(activeObject.x, activeObject.y, activeObject.z, origin);
    }

    return missionSceneRoute.goalPosition;
  }, [world?.activeObjectiveTarget, world?.route, world?.objects, missionSceneRoute.goalPosition, origin]);

  const activeObjectiveRadius = useMemo(() => {
    const targetTolerance = world?.activeObjectiveTarget?.toleranceMeters;
    if (typeof targetTolerance === "number" && Number.isFinite(targetTolerance)) {
      return Math.max(0.35, targetTolerance);
    }

    const activeRouteTolerance = world?.route?.find((point) => point.active)?.toleranceMeters;
    if (typeof activeRouteTolerance === "number" && Number.isFinite(activeRouteTolerance)) {
      return Math.max(0.35, activeRouteTolerance);
    }

    const activeObjectRadius = world?.objects?.find((object) => object.active)?.radius;
    if (typeof activeObjectRadius === "number" && Number.isFinite(activeObjectRadius)) {
      return Math.max(0.35, activeObjectRadius);
    }

    return 1.25;
  }, [world?.activeObjectiveTarget?.toleranceMeters, world?.route, world?.objects]);

  const hasWorldLayer =
    worldRoutePoints.length > 1 ||
    worldWaypointObjects.length > 0 ||
    worldBuoys.length > 0 ||
    Boolean(activeObjectivePosition);

  const hasMissionRoute =
    missionSceneRoute.routePoints.length > 1 ||
    missionSceneRoute.waypoints.length > 0 ||
    Boolean(missionSceneRoute.goalPosition);

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
        array.push(p.x - origin.x, 0.12, -(p.y - origin.y));
      }
    }

    return new Float32Array(array);
  }, [sensor?.lidarPoints, origin]);

  const obstacles = useMemo(() => {
    const telemetryObstacles = getTelemetryObstacles(telemetry).map((o) => ({
      x: o.x - origin.x,
      y: o.y - origin.y,
      z: 0,
      r: Math.max(0.05, o.r),
      height: REAL_BUOY_HEIGHT_M,
      color: "#fb923c",
      source: "runtime"
    }));

    const sensorObstacles = (sensor?.obstacles ?? []).map((o) => ({
      x: o.position.x - origin.x,
      y: o.position.y - origin.y,
      z: 0,
      r: Math.max(0.05, o.radius),
      height: REAL_BUOY_HEIGHT_M,
      color: "#f97316",
      source: o.source
    }));

    return [...telemetryObstacles, ...sensorObstacles, ...worldObjectObstacles];
  }, [telemetry, sensor?.obstacles, worldObjectObstacles, origin]);

  const thrusters = useMemo<SceneThruster[]>(() => {
    const source = actuator?.thrusters ?? [];

    return source.map((thruster, index) => ({
      id: thruster.id ?? `T${index + 1}`,
      active: Boolean(thruster.active),
      rpm: thruster.rpm ?? 0,
      normalizedCommand: thruster.normalizedCommand ?? 0,
      position: normalizeThrusterPosition(getThrusterPosition(index))
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
    vec.multiplyScalar(Math.min(5, Math.max(1.2, magnitude * 0.4)));

    return [vec.x, vec.y, vec.z];
  }, [forceBody?.x, forceBody?.y, forceBody?.z]);

  const torqueVector = useMemo<[number, number, number]>(() => {
    const tx = torqueBody?.x ?? 0;
    const ty = torqueBody?.y ?? 0;
    const tz = torqueBody?.z ?? 0;

    const original = new THREE.Vector3(tx, -tz, ty);
    const magnitude = original.length();

    if (magnitude < 0.02) return [0, 0, 0];

    const vec = original.clone().normalize();
    vec.multiplyScalar(Math.min(3.5, Math.max(0.9, magnitude * 2.0)));

    return [vec.x, vec.y, vec.z];
  }, [torqueBody?.x, torqueBody?.y, torqueBody?.z]);

  const headingVector = useMemo<[number, number, number]>(() => {
    const yawRad = deg2rad(yawDeg);
    return [Math.cos(yawRad) * 1.65, 0, -Math.sin(yawRad) * 1.65];
  }, [yawDeg]);

  const velocityVector = useMemo<[number, number, number]>(() => {
    const vx = telemetry?.motion?.linearVelocity?.x ?? telemetry?.vx ?? 0;
    const vy = telemetry?.motion?.linearVelocity?.y ?? telemetry?.vy ?? 0;
    const vector = new THREE.Vector3(vx, 0, -vy);
    const magnitude = vector.length();

    if (magnitude < 0.02 || speed < 0.02) return [0, 0, 0];

    vector.normalize().multiplyScalar(Math.min(3.5, Math.max(0.9, speed * 2.0)));

    return [vector.x, vector.y, vector.z];
  }, [telemetry, speed]);

  useEffect(() => {
    const next = new THREE.Vector3(boatPosition[0], boatPosition[1] + 0.06, boatPosition[2]);
    const trail = trailRef.current;
    const last = trail[trail.length - 1];

    if (!last || last.distanceTo(next) > 0.05) {
      trail.push(next);
      if (trail.length > 2000) trail.shift();
    }
  }, [boatPosition]);

  return (
    <>
      {hasWorldLayer ? (
        <>
          {worldRoutePoints.length > 1 ? (
            <Line points={worldRoutePoints} color="#60a5fa" lineWidth={2} />
          ) : null}

          {worldWaypointObjects.map(({ object, waypoint }) => (
            <WaypointMarker
              key={object.id}
              waypoint={waypoint}
              colorOverride={object.color ?? undefined}
              radiusOverride={object.radius}
              type={object.type}
            />
          ))}

          {activeObjectivePosition ? (
            <>
              <GoalMarker position={activeObjectivePosition} />
              <ZoneRing
                position={activeObjectivePosition}
                radius={activeObjectiveRadius}
                color="#f97316"
                opacity={0.92}
              />
              <ZoneRing
                position={activeObjectivePosition}
                radius={activeObjectiveRadius * 1.6}
                color="#22c55e"
                opacity={0.32}
              />
            </>
          ) : null}

          {worldBuoys.map((buoy) => (
            <BuoyMarker
              key={buoy.id}
              position={buoy.position}
              side={buoy.side}
              radius={buoy.radius}
              height={buoy.height}
              colorOverride={buoy.color}
            />
          ))}
        </>
      ) : hasMissionRoute ? (
        <>
          {missionSceneRoute.routePoints.length > 1 ? (
            <Line points={missionSceneRoute.routePoints} color="#60a5fa" lineWidth={2} />
          ) : null}

          {missionSceneRoute.waypoints.map((waypoint) => (
            <WaypointMarker key={waypoint.id} waypoint={waypoint} />
          ))}

          {missionSceneRoute.goalPosition ? (
            <>
              <GoalMarker position={missionSceneRoute.goalPosition} />
              <ZoneRing position={missionSceneRoute.goalPosition} radius={1.25} color="#f97316" opacity={0.92} />
              <ZoneRing position={missionSceneRoute.goalPosition} radius={2.25} color="#22c55e" opacity={0.32} />
            </>
          ) : null}
        </>
      ) : null}

      <group ref={groupRef} position={boatPosition}>
        <BoatModel
          yawDeg={yawDeg}
          rollDeg={rollDeg}
          pitchDeg={pitchDeg}
          isArmed={isArmed}
          thrusters={thrusters}
        />

        <VectorArrow vector={headingVector} color="#38bdf8" shaftRadius={0.016} headRadius={0.075} headLength={0.18} />
        <VectorArrow vector={velocityVector} color="#22c55e" shaftRadius={0.022} headRadius={0.095} headLength={0.22} />
        <VectorArrow vector={forceVector} color="#ef4444" shaftRadius={0.032} headRadius={0.11} headLength={0.26} />
        <VectorArrow vector={torqueVector} color="#a855f7" shaftRadius={0.022} headRadius={0.09} headLength={0.22} />
      </group>

      {trailRef.current.length > 1 ? (
        <Line points={trailRef.current} color="#22d3ee" lineWidth={2.2} />
      ) : null}

      {lidarPoints.length > 0 ? (
        <Points positions={lidarPoints} stride={3}>
          <PointMaterial size={0.12} color="#4ade80" sizeAttenuation />
        </Points>
      ) : null}

      {obstacles.map((obstacle, index) => (
        <ObstacleMarker
          key={`obstacle-${obstacle.source}-${index}`}
          position={[obstacle.x, obstacle.z, -obstacle.y]}
          radius={obstacle.r}
          height={obstacle.height}
          color={obstacle.color}
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
  thrusters: SceneThruster[];
}) {
  const deckRef = useRef<THREE.Mesh | null>(null);
  const statusLightRef = useRef<THREE.Mesh | null>(null);

  useFrame(({ clock }) => {
    if (deckRef.current) {
      const material = deckRef.current.material as THREE.MeshStandardMaterial;
      material.emissive.setHex(props.isArmed ? 0x0ea5e9 : 0x334155);
      material.emissiveIntensity = props.isArmed ? 0.10 : 0.04;
    }

    if (statusLightRef.current) {
      const material = statusLightRef.current.material as THREE.MeshStandardMaterial;
      material.emissive.setHex(props.isArmed ? 0x22c55e : 0xef4444);
      material.emissiveIntensity = props.isArmed
        ? 0.55 + Math.sin(clock.getElapsedTime() * 4) * 0.18
        : 0.45;
    }
  });

  return (
    <group rotation={[deg2rad(props.rollDeg), deg2rad(props.yawDeg), deg2rad(props.pitchDeg)]}>
      {/* Sol ponton */}
      <group position={[0, PONTOON_RADIUS_M, PONTOON_CENTER_OFFSET_Z_M]}>
        <mesh rotation={[0, 0, Math.PI / 2]} castShadow receiveShadow>
          <cylinderGeometry args={[PONTOON_RADIUS_M, PONTOON_RADIUS_M, PONTOON_LENGTH_M * 0.82, 18]} />
          <meshStandardMaterial color="#64748b" metalness={0.55} roughness={0.34} />

        </mesh>

        <mesh position={[PONTOON_LENGTH_M * 0.45, 0, 0]} rotation={[0, 0, -Math.PI / 2]} castShadow>
          <coneGeometry args={[PONTOON_RADIUS_M, PONTOON_LENGTH_M * 0.18, 18]} />
          <meshStandardMaterial color="#94a3b8" metalness={0.48} roughness={0.30} />
        </mesh>

        <mesh position={[-PONTOON_LENGTH_M * 0.45, 0, 0]} rotation={[0, 0, Math.PI / 2]} castShadow>
          <coneGeometry args={[PONTOON_RADIUS_M * 0.92, PONTOON_LENGTH_M * 0.10, 18]} />
          <meshStandardMaterial color="#475569" metalness={0.45} roughness={0.38} />
        </mesh>
      </group>

      {/* Sağ ponton */}
      <group position={[0, PONTOON_RADIUS_M, -PONTOON_CENTER_OFFSET_Z_M]}>
        <mesh rotation={[0, 0, Math.PI / 2]} castShadow receiveShadow>
          <cylinderGeometry args={[PONTOON_RADIUS_M, PONTOON_RADIUS_M, PONTOON_LENGTH_M * 0.82, 18]} />
          <meshStandardMaterial color="#64748b" metalness={0.55} roughness={0.34} />
        </mesh>

        <mesh position={[PONTOON_LENGTH_M * 0.45, 0, 0]} rotation={[0, 0, -Math.PI / 2]} castShadow>
          <coneGeometry args={[PONTOON_RADIUS_M, PONTOON_LENGTH_M * 0.18, 18]} />
          <meshStandardMaterial color="#94a3b8" metalness={0.48} roughness={0.30} />
        </mesh>

        <mesh position={[-PONTOON_LENGTH_M * 0.45, 0, 0]} rotation={[0, 0, Math.PI / 2]} castShadow>
          <coneGeometry args={[PONTOON_RADIUS_M * 0.92, PONTOON_LENGTH_M * 0.10, 18]} />
          <meshStandardMaterial color="#475569" metalness={0.45} roughness={0.38} />
        </mesh>
      </group>

      {/* Ana güverte */}
      <mesh ref={deckRef} position={[0.02, 0.245, 0]} castShadow receiveShadow>
        <boxGeometry args={[DECK_LENGTH_M, DECK_HEIGHT_M, DECK_WIDTH_M]} />
        <meshStandardMaterial color="#38bdf8" metalness={0.45} roughness={0.22} />
      </mesh>

      {/* Güverte üst koyu panel */}
      <mesh position={[0.02, 0.285, 0]} castShadow>
        <boxGeometry args={[0.54, 0.018, 0.62]} />
        <meshStandardMaterial color="#0f172a" metalness={0.30} roughness={0.38} />
      </mesh>

      {/* Ön ve arka traversler */}
      <mesh position={[0.30, 0.20, 0]} castShadow>
        <boxGeometry args={[0.06, 0.055, VEHICLE_WIDTH_M * 0.82]} />
        <meshStandardMaterial color="#64748b" metalness={0.42} roughness={0.42} />
      </mesh>

      <mesh position={[-0.30, 0.20, 0]} castShadow>
        <boxGeometry args={[0.06, 0.055, VEHICLE_WIDTH_M * 0.82]} />
        <meshStandardMaterial color="#64748b" metalness={0.42} roughness={0.42} />
      </mesh>

      {/* Elektronik kutusu */}
      <mesh position={[0.02, 0.38, 0]} castShadow>
        <boxGeometry args={[CABIN_LENGTH_M, CABIN_HEIGHT_M, CABIN_WIDTH_M]} />
        <meshStandardMaterial color="#cbd5e1" metalness={0.30} roughness={0.50} />
      </mesh>

      {/* Ön kamera / sensör kafası */}
      <mesh position={[0.20, 0.39, 0]} rotation={[0, Math.PI / 2, 0]} castShadow>
        <cylinderGeometry args={[0.045, 0.045, 0.06, 16]} />
        <meshStandardMaterial color="#020617" metalness={0.50} roughness={0.28} />
      </mesh>

      <mesh position={[0.235, 0.39, 0]} rotation={[0, Math.PI / 2, 0]} castShadow>
        <cylinderGeometry args={[0.026, 0.026, 0.014, 16]} />
        <meshStandardMaterial color="#38bdf8" emissive="#38bdf8" emissiveIntensity={0.20} />
      </mesh>

      {/* Anten / GNSS direği */}
      <mesh position={[-0.05, 0.50, 0]} castShadow>
        <cylinderGeometry args={[0.012, 0.012, 0.18, 10]} />
        <meshStandardMaterial color="#e2e8f0" metalness={0.18} roughness={0.65} />
      </mesh>

      <mesh position={[-0.05, 0.61, 0]} castShadow>
        <sphereGeometry args={[0.034, 16, 16]} />
        <meshStandardMaterial color="#e2e8f0" emissive="#bae6fd" emissiveIntensity={0.16} />
      </mesh>

      {/* Durum lambası */}
      <mesh ref={statusLightRef} position={[0.12, 0.475, 0.13]} castShadow>
        <sphereGeometry args={[0.026, 16, 16]} />
        <meshStandardMaterial color={props.isArmed ? "#22c55e" : "#ef4444"} />
      </mesh>

      {/* Ön yön işareti */}
      <mesh position={[0.56, 0.29, 0]} rotation={[0, 0, -Math.PI / 2]} castShadow>
        <coneGeometry args={[0.075, 0.18, 14]} />
        <meshStandardMaterial color="#0ea5e9" metalness={0.38} roughness={0.24} />
      </mesh>

      {/* Boyut referans çerçevesi */}
      <mesh position={[0, 0.255, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <ringGeometry args={[0.69, 0.705, 64]} />
        <meshBasicMaterial color="#7dd3fc" transparent opacity={0.15} side={THREE.DoubleSide} />
      </mesh>

      {props.thrusters.map((thruster) => (
        <ThrusterMesh
          key={thruster.id}
          position={thruster.position}
          active={thruster.active}
          rpm={thruster.rpm}
          normalizedCommand={thruster.normalizedCommand}
        />
      ))}
    </group>
  );
}

function ThrusterMesh(props: {
  position: [number, number, number];
  active: boolean;
  rpm: number;
  normalizedCommand: number;
}) {
  const ref = useRef<THREE.Mesh | null>(null);
  const commandScale = Math.min(1.45, Math.max(0.75, Math.abs(props.normalizedCommand) * 1.6));

  useFrame(() => {
    if (!ref.current) return;
    ref.current.rotation.z += (props.rpm ?? 0) / 65000;
  });

  return (
    <group position={props.position}>
      <mesh ref={ref} rotation={[Math.PI / 2, 0, 0]} scale={[commandScale, commandScale, 1]} castShadow>
        <cylinderGeometry args={[0.05, 0.05, 0.06, 12]} />
        <meshStandardMaterial color={props.active ? "#facc15" : "#64748b"} metalness={0.22} roughness={0.38} />
      </mesh>

      <mesh position={[0.045, 0, 0]} rotation={[0, Math.PI / 2, 0]} castShadow>
        <cylinderGeometry args={[0.025, 0.025, 0.08, 10]} />
        <meshStandardMaterial color="#334155" metalness={0.35} roughness={0.4} />
      </mesh>

      {props.active ? (
        <mesh position={[-0.08, 0, 0]} rotation={[0, 0, Math.PI / 2]}>
          <coneGeometry args={[0.035, 0.10, 12]} />
          <meshStandardMaterial color="#fde047" emissive="#facc15" emissiveIntensity={0.28} />
        </mesh>
      ) : null}
    </group>
  );
}

function VectorArrow(props: {
  vector: [number, number, number];
  color: string;
  shaftRadius: number;
  headRadius: number;
  headLength: number;
}) {
  const vec = new THREE.Vector3(...props.vector);
  const length = vec.length();

  if (length < 0.001) return null;

  const dir = vec.clone().normalize();
  const midpoint = dir.clone().multiplyScalar(length * 0.5);
  const quat = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);

  return (
    <group position={[0, 0.34, 0]}>
      <mesh position={[midpoint.x, midpoint.y, midpoint.z]} quaternion={quat}>
        <cylinderGeometry args={[props.shaftRadius, props.shaftRadius, length, 8]} />
        <meshStandardMaterial color={props.color} emissive={props.color} emissiveIntensity={0.18} />
      </mesh>

      <mesh position={[dir.x * length, dir.y * length, dir.z * length]} quaternion={quat}>
        <coneGeometry args={[props.headRadius, props.headLength, 12]} />
        <meshStandardMaterial color={props.color} emissive={props.color} emissiveIntensity={0.24} />
      </mesh>
    </group>
  );
}

function GoalMarker(props: { position: [number, number, number] }) {
  return (
    <group position={props.position}>
      <mesh position={[0, 0.45, 0]} castShadow>
        <cylinderGeometry args={[0.025, 0.025, 0.9, 10]} />
        <meshStandardMaterial color="#f8fafc" metalness={0.2} roughness={0.45} />
      </mesh>

      <mesh position={[0.18, 0.78, 0]} castShadow>
        <boxGeometry args={[0.34, 0.18, 0.025]} />
        <meshStandardMaterial color="#f97316" emissive="#f97316" emissiveIntensity={0.18} />
      </mesh>

      <mesh position={[0, 0.08, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <ringGeometry args={[0.18, 0.22, 32]} />
        <meshBasicMaterial color="#f97316" transparent opacity={0.75} side={THREE.DoubleSide} />
      </mesh>
    </group>
  );
}

function WaypointMarker(props: {
  waypoint: SceneWaypoint;
  colorOverride?: string;
  radiusOverride?: number;
  type?: string;
}) {
  const { waypoint } = props;
  const zoneRadius = props.radiusOverride
    ? Math.max(0.35, props.radiusOverride)
    : waypoint.active
      ? 1.25
      : 0.75;

  const color =
    props.colorOverride ??
    (waypoint.reached ? "#22c55e" : waypoint.active ? "#facc15" : "#60a5fa");

  const isStart = props.type === "start";
  const isFinish = props.type === "finish";

  return (
    <group position={waypoint.position}>
      <mesh position={[0, WAYPOINT_POST_HEIGHT_M * 0.5, 0]} castShadow>
        {isFinish ? (
          <coneGeometry args={[0.13, WAYPOINT_POST_HEIGHT_M, 14]} />
        ) : isStart ? (
          <boxGeometry args={[0.16, WAYPOINT_POST_HEIGHT_M, 0.16]} />
        ) : (
          <cylinderGeometry args={[WAYPOINT_POST_RADIUS_M, WAYPOINT_POST_RADIUS_M, WAYPOINT_POST_HEIGHT_M, 14]} />
        )}
        <meshStandardMaterial color={color} emissive={color} emissiveIntensity={waypoint.active ? 0.22 : 0.08} />
      </mesh>

      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.025, 0]}>
        <ringGeometry args={[Math.max(0.12, zoneRadius * 0.96), Math.max(0.14, zoneRadius), 48]} />
        <meshBasicMaterial color={color} transparent opacity={waypoint.active ? 0.55 : 0.28} side={THREE.DoubleSide} />
      </mesh>

      {waypoint.active ? (
        <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.035, 0]}>
          <ringGeometry args={[Math.max(0.18, zoneRadius * 0.58), Math.max(0.2, zoneRadius * 0.62), 48]} />
          <meshBasicMaterial color="#ffffff" transparent opacity={0.32} side={THREE.DoubleSide} />
        </mesh>
      ) : null}
    </group>
  );
}

function ZoneRing(props: {
  position: [number, number, number];
  radius: number;
  color: string;
  opacity: number;
}) {
  return (
    <mesh position={[props.position[0], props.position[1] + 0.04, props.position[2]]} rotation={[-Math.PI / 2, 0, 0]}>
      <ringGeometry args={[props.radius * 0.985, props.radius, 96]} />
      <meshBasicMaterial color={props.color} transparent opacity={props.opacity} side={THREE.DoubleSide} />
    </mesh>
  );
}

function ObstacleMarker(props: {
  position: [number, number, number];
  radius: number;
  height: number;
  color: string;
}) {
  const pulseRef = useRef<THREE.Mesh | null>(null);
  const physicalRadius = Math.max(0.08, Math.min(0.35, props.radius));
  const physicalHeight = Math.max(0.20, Math.min(0.90, props.height));
  const safetyRadius = Math.max(physicalRadius * 1.35, props.radius);

  useFrame(({ clock }) => {
    if (!pulseRef.current) return;

    const t = clock.getElapsedTime();
    const s = 1 + Math.sin(t * 2.2) * 0.05;
    pulseRef.current.scale.setScalar(s);
  });

  return (
    <group position={props.position}>
      <mesh position={[0, physicalHeight * 0.5, 0]} castShadow>
        <cylinderGeometry args={[physicalRadius * 0.85, physicalRadius, physicalHeight, 18]} />
        <meshStandardMaterial color={props.color} emissive={props.color} emissiveIntensity={0.18} roughness={0.45} />
      </mesh>

      <mesh position={[0, physicalHeight + physicalRadius * 0.35, 0]} castShadow>
        <sphereGeometry args={[physicalRadius * 0.62, 18, 18]} />
        <meshStandardMaterial color={props.color} emissive={props.color} emissiveIntensity={0.24} roughness={0.4} />
      </mesh>

      <mesh ref={pulseRef} rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.035, 0]}>
        <ringGeometry args={[Math.max(0.08, safetyRadius * 0.96), Math.max(0.1, safetyRadius), 48]} />
        <meshBasicMaterial color="#f59e0b" transparent opacity={0.42} side={THREE.DoubleSide} />
      </mesh>

      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.045, 0]}>
        <ringGeometry args={[Math.max(0.08, physicalRadius * 1.05), Math.max(0.1, physicalRadius * 1.22), 32]} />
        <meshBasicMaterial color="#fed7aa" transparent opacity={0.58} side={THREE.DoubleSide} />
      </mesh>
    </group>
  );
}

function BuoyMarker(props: {
  position: [number, number, number];
  side: "left" | "right";
  radius: number;
  height: number;
  colorOverride?: string;
}) {
  const color = props.colorOverride ?? (props.side === "left" ? "#22c55e" : "#ef4444");
  const radius = Math.max(0.08, Math.min(0.30, props.radius || REAL_BUOY_RADIUS_M));
  const height = Math.max(0.25, Math.min(0.90, props.height || REAL_BUOY_HEIGHT_M));

  return (
    <group position={props.position}>
      <mesh position={[0, height * 0.5, 0]} castShadow>
        <cylinderGeometry args={[radius * 0.82, radius, height, 18]} />
        <meshStandardMaterial color={color} emissive={color} emissiveIntensity={0.12} roughness={0.42} />
      </mesh>

      <mesh position={[0, height + radius * 0.28, 0]} castShadow>
        <sphereGeometry args={[radius * 0.58, 18, 18]} />
        <meshStandardMaterial color={color} emissive={color} emissiveIntensity={0.20} roughness={0.38} />
      </mesh>

      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.025, 0]}>
        <ringGeometry args={[radius * 1.20, radius * 1.42, 32]} />
        <meshBasicMaterial color={color} transparent opacity={0.42} side={THREE.DoubleSide} />
      </mesh>
    </group>
  );
}

function worldObjectToWaypoint(
  object: WorldObject,
  origin: Vec3Like
): SceneWaypoint {
  return {
    id: object.id,
    label: object.label ?? object.id,
    reached: object.completed,
    active: object.active,
    position: worldToSceneTuple(object.x, object.y, object.z, origin)
  };
}

function worldToSceneVector(
  x: number,
  y: number,
  z: number,
  origin: Vec3Like,
  yOffset = 0
) {
  return new THREE.Vector3(
    x - origin.x,
    z - origin.z + yOffset,
    -(y - origin.y)
  );
}

function worldToSceneTuple(
  x: number,
  y: number,
  z: number,
  origin: Vec3Like
): [number, number, number] {
  return [
    x - origin.x,
    z - origin.z,
    -(y - origin.y)
  ];
}

function normalizeBuoySide(side: string | null | undefined): "left" | "right" {
  return side === "right" ? "right" : "left";
}

function normalizeThrusterPosition(
  position: [number, number, number]
): [number, number, number] {
  const scaledX = clamp(position[0] * 0.24, -0.44, 0.44);
  const scaledZ = clamp(position[2] * 0.26, -0.36, 0.36);

  return [scaledX, 0.11, scaledZ];
}

function getWorldObjectNumber(
  object: WorldObject,
  key: string,
  fallback: number
): number {
  const record = object as unknown as Record<string, unknown>;
  const value = record[key];

  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  return fallback;
}

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(max, value));
}