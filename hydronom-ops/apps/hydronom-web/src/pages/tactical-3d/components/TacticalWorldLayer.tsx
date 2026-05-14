import { useMemo, useRef } from "react";
import * as THREE from "three";
import { useFrame } from "@react-three/fiber";
import { Line } from "@react-three/drei";

import type { WorldState } from "../../../entities/world/model/world.types";
import type { SceneWaypoint, Vec3Like } from "../types";

import {
  getWorldObjectNumber,
  isWorldWaypointLike,
  normalizeBuoySide,
  worldObjectToWaypoint,
  worldToSceneTuple,
  worldToSceneVector
} from "../lib/tactical-world-utils";

import { deg2rad } from "../lib/tactical3d-utils";

const REAL_BUOY_RADIUS_M = 0.15;
const REAL_BUOY_HEIGHT_M = 0.5;

const WAYPOINT_POST_HEIGHT_M = 0.34;
const WAYPOINT_POST_RADIUS_M = 0.055;

export function TacticalWorldLayer(props: {
  world: WorldState | undefined;
  origin: Vec3Like;
  fallbackGoalPosition: [number, number, number] | null;
}) {
  const { world, origin, fallbackGoalPosition } = props;

  const routePoints = useMemo<THREE.Vector3[]>(() => {
    const route = world?.route ?? [];

    return route
      .slice()
      .sort((a, b) => a.index - b.index)
      .map((point) => worldToSceneVector(point.x, point.y, point.z, origin, 0.08));
  }, [world?.route, origin]);

  const waypointObjects = useMemo(() => {
    const objects = world?.objects ?? [];

    return objects
      .filter((object) => isWorldWaypointLike(object.type))
      .map((object) => ({
        object,
        waypoint: worldObjectToWaypoint(object, origin)
      }));
  }, [world?.objects, origin]);

  const buoys = useMemo(() => {
    const objects = world?.objects ?? [];

    return objects
      .filter((object) => object.type === "buoy")
      .map((object) => ({
        id: object.id,
        side: normalizeBuoySide(object.side),
        position: worldToSceneTuple(object.x, object.y, object.z, origin),
        radius: getWorldObjectNumber(object, "radius", REAL_BUOY_RADIUS_M),
        height: getWorldObjectNumber(object, "height", REAL_BUOY_HEIGHT_M),
        color: object.color ?? undefined
      }));
  }, [world?.objects, origin]);

  const pathSegments = useMemo(() => {
    const objects = world?.objects ?? [];

    return objects
      .filter((object) => object.type === "guidance_path_segment")
      .map((object) => ({
        id: object.id,
        position: worldToSceneTuple(object.x, object.y, object.z, origin),
        length: getWorldObjectNumber(object, "length", 1.0),
        width: getWorldObjectNumber(object, "width", 0.15),
        height: getWorldObjectNumber(object, "height", 0.03),
        yawDeg: getWorldObjectNumber(object, "yawDeg", 0),
        color: object.color ?? "#ef4444"
      }));
  }, [world?.objects, origin]);

  const trackingStripes = useMemo(() => {
    const objects = world?.objects ?? [];

    return objects
      .filter((object) => object.type === "tracking_stripe")
      .map((object) => ({
        id: object.id,
        position: worldToSceneTuple(object.x, object.y, object.z, origin),
        length: getWorldObjectNumber(object, "length", 1.0),
        width: getWorldObjectNumber(object, "width", 0.03),
        height: getWorldObjectNumber(object, "height", 0.012),
        yawDeg: getWorldObjectNumber(object, "yawDeg", 0),
        color: object.color ?? "#020617"
      }));
  }, [world?.objects, origin]);

  const pipes = useMemo(() => {
    const objects = world?.objects ?? [];

    return objects
      .filter((object) => object.type === "pipe")
      .map((object) => ({
        id: object.id,
        position: worldToSceneTuple(object.x, object.y, object.z, origin),
        length: getWorldObjectNumber(object, "length", 3.0),
        diameter: getWorldObjectNumber(object, "diameter", Math.max(0.1, object.radius * 2)),
        yawDeg: getWorldObjectNumber(object, "yawDeg", 0),
        color: object.color ?? "#475569"
      }));
  }, [world?.objects, origin]);

  const releaseZones = useMemo(() => {
    const objects = world?.objects ?? [];

    return objects
      .filter((object) => object.type === "release_zone")
      .map((object) => ({
        id: object.id,
        position: worldToSceneTuple(object.x, object.y, object.z, origin),
        radius: Math.max(0.25, object.radius),
        height: getWorldObjectNumber(object, "height", 0.7),
        color: object.color ?? "#22c55e"
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

    return fallbackGoalPosition;
  }, [world?.activeObjectiveTarget, world?.route, world?.objects, fallbackGoalPosition, origin]);

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

  const hasWorld =
    routePoints.length > 1 ||
    waypointObjects.length > 0 ||
    buoys.length > 0 ||
    pathSegments.length > 0 ||
    trackingStripes.length > 0 ||
    pipes.length > 0 ||
    releaseZones.length > 0 ||
    Boolean(activeObjectivePosition);

  if (!hasWorld) {
    return null;
  }

  return (
    <>
      {routePoints.length > 1 ? (
        <Line points={routePoints} color="#60a5fa" lineWidth={2} />
      ) : null}

      {pathSegments.map((segment) => (
        <PathSlabMarker
          key={segment.id}
          position={segment.position}
          length={segment.length}
          width={segment.width}
          height={segment.height}
          yawDeg={segment.yawDeg}
          color={segment.color}
          opacity={0.82}
        />
      ))}

      {trackingStripes.map((stripe) => (
        <PathSlabMarker
          key={stripe.id}
          position={stripe.position}
          length={stripe.length}
          width={stripe.width}
          height={stripe.height}
          yawDeg={stripe.yawDeg}
          color={stripe.color}
          opacity={1.0}
        />
      ))}

      {pipes.map((pipe) => (
        <PipeMarker
          key={pipe.id}
          position={pipe.position}
          length={pipe.length}
          diameter={pipe.diameter}
          yawDeg={pipe.yawDeg}
          color={pipe.color}
        />
      ))}

      {releaseZones.map((zone) => (
        <CylinderZoneMarker
          key={zone.id}
          position={zone.position}
          radius={zone.radius}
          height={zone.height}
          color={zone.color}
          opacity={0.22}
        />
      ))}

      {waypointObjects.map(({ object, waypoint }) => (
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

      {buoys.map((buoy) => (
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
  );
}

function PathSlabMarker(props: {
  position: [number, number, number];
  length: number;
  width: number;
  height: number;
  yawDeg: number;
  color: string;
  opacity: number;
}) {
  return (
    <group position={props.position} rotation={[0, -deg2rad(props.yawDeg), 0]}>
      <mesh receiveShadow>
        <boxGeometry
          args={[
            Math.max(0.05, props.length),
            Math.max(0.008, props.height),
            Math.max(0.01, props.width)
          ]}
        />
        <meshStandardMaterial
          color={props.color}
          transparent={props.opacity < 1}
          opacity={props.opacity}
          roughness={0.72}
          metalness={0.08}
        />
      </mesh>
    </group>
  );
}

function PipeMarker(props: {
  position: [number, number, number];
  length: number;
  diameter: number;
  yawDeg: number;
  color: string;
}) {
  const radius = Math.max(0.08, props.diameter * 0.5);
  const length = Math.max(0.2, props.length);

  return (
    <group position={props.position} rotation={[0, -deg2rad(props.yawDeg), Math.PI / 2]}>
      <mesh castShadow receiveShadow>
        <cylinderGeometry args={[radius, radius, length, 32, 1, true]} />
        <meshStandardMaterial
          color={props.color}
          transparent
          opacity={0.42}
          roughness={0.38}
          metalness={0.18}
          side={THREE.DoubleSide}
        />
      </mesh>

      <mesh>
        <torusGeometry args={[radius, 0.018, 10, 48]} />
        <meshStandardMaterial color="#38bdf8" emissive="#38bdf8" emissiveIntensity={0.16} />
      </mesh>

      <mesh position={[0, length * 0.5, 0]}>
        <torusGeometry args={[radius, 0.018, 10, 48]} />
        <meshStandardMaterial color="#38bdf8" emissive="#38bdf8" emissiveIntensity={0.16} />
      </mesh>

      <mesh position={[0, -length * 0.5, 0]}>
        <torusGeometry args={[radius, 0.018, 10, 48]} />
        <meshStandardMaterial color="#38bdf8" emissive="#38bdf8" emissiveIntensity={0.16} />
      </mesh>
    </group>
  );
}

function CylinderZoneMarker(props: {
  position: [number, number, number];
  radius: number;
  height: number;
  color: string;
  opacity: number;
}) {
  return (
    <group position={props.position}>
      <mesh position={[0, props.height * 0.5, 0]}>
        <cylinderGeometry args={[props.radius, props.radius, props.height, 48, 1, true]} />
        <meshBasicMaterial color={props.color} transparent opacity={props.opacity} side={THREE.DoubleSide} />
      </mesh>

      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.035, 0]}>
        <ringGeometry args={[props.radius * 0.96, props.radius, 64]} />
        <meshBasicMaterial color={props.color} transparent opacity={0.58} side={THREE.DoubleSide} />
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

  const isStart = props.type === "start" || props.type === "start_zone";
  const isFinish = props.type === "finish";
  const isHint = props.type === "hint_marker";

  return (
    <group position={waypoint.position}>
      <mesh position={[0, WAYPOINT_POST_HEIGHT_M * 0.5, 0]} castShadow>
        {isFinish ? (
          <coneGeometry args={[0.13, WAYPOINT_POST_HEIGHT_M, 14]} />
        ) : isStart ? (
          <boxGeometry args={[0.16, WAYPOINT_POST_HEIGHT_M, 0.16]} />
        ) : isHint ? (
          <sphereGeometry args={[0.12, 18, 18]} />
        ) : (
          <cylinderGeometry args={[WAYPOINT_POST_RADIUS_M, WAYPOINT_POST_RADIUS_M, WAYPOINT_POST_HEIGHT_M, 14]} />
        )}
        <meshStandardMaterial color={color} emissive={color} emissiveIntensity={waypoint.active ? 0.22 : 0.08} />
      </mesh>

      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.025, 0]}>
        <ringGeometry args={[Math.max(0.12, zoneRadius * 0.96), Math.max(0.14, zoneRadius), 48]} />
        <meshBasicMaterial color={color} transparent opacity={waypoint.active ? 0.55 : 0.28} side={THREE.DoubleSide} />
      </mesh>
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
        <ringGeometry args={[radius * 1.2, radius * 1.42, 32]} />
        <meshBasicMaterial color={color} transparent opacity={0.42} side={THREE.DoubleSide} />
      </mesh>
    </group>
  );
}