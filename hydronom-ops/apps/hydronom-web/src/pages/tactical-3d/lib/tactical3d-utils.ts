import * as THREE from "three";

import type { MissionState } from "../../../entities/mission/model/mission.types";
import type { VehicleTelemetry } from "../../../entities/vehicle/model/vehicle.types";
import type { Vec2, Vec3 } from "../../../shared/types/common.types";
import type {
  LooseRecord,
  ObstacleLike,
  SceneRoute,
  Vec3Like
} from "../types";

export function asRecord(value: unknown): LooseRecord | undefined {
  return value && typeof value === "object" ? (value as LooseRecord) : undefined;
}

export function getNumber(obj: unknown, key: string): number | undefined {
  const rec = asRecord(obj);
  const value = rec?.[key];
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

export function getString(obj: unknown, key: string): string | undefined {
  const rec = asRecord(obj);
  const value = rec?.[key];
  return typeof value === "string" ? value : undefined;
}

export function getArray<T = unknown>(obj: unknown, key: string): T[] {
  const rec = asRecord(obj);
  const value = rec?.[key];
  return Array.isArray(value) ? (value as T[]) : [];
}

export function getNested(obj: unknown, ...path: string[]): unknown {
  let current: unknown = obj;

  for (const key of path) {
    const rec = asRecord(current);

    if (!rec || !(key in rec)) {
      return undefined;
    }

    current = rec[key];
  }

  return current;
}

export function getTelemetryWorldPosition(
  telemetry: VehicleTelemetry | undefined
): Vec3Like | undefined {
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

export function getTelemetryYawDeg(telemetry: VehicleTelemetry | undefined): number {
  return (
    getNumber(getNested(telemetry, "pose", "orientation"), "yaw") ??
    getNumber(telemetry, "yawDeg") ??
    getNumber(telemetry, "headingDeg") ??
    0
  );
}

export function getTelemetryRollDeg(telemetry: VehicleTelemetry | undefined): number {
  return (
    getNumber(getNested(telemetry, "pose", "orientation"), "roll") ??
    getNumber(telemetry, "rollDeg") ??
    0
  );
}

export function getTelemetryPitchDeg(telemetry: VehicleTelemetry | undefined): number {
  return (
    getNumber(getNested(telemetry, "pose", "orientation"), "pitch") ??
    getNumber(telemetry, "pitchDeg") ??
    0
  );
}

export function getTelemetryYawRateDeg(
  telemetry: VehicleTelemetry | undefined
): number {
  return (
    getNumber(getNested(telemetry, "motion", "angularVelocity"), "z") ??
    getNumber(telemetry, "yawRateDeg") ??
    0
  );
}

export function getTelemetrySpeed(telemetry: VehicleTelemetry | undefined): number {
  const motionSpeed = getNumber(getNested(telemetry, "motion"), "speed");
  if (motionSpeed !== undefined) return motionSpeed;

  const vx = getNumber(telemetry, "vx") ?? 0;
  const vy = getNumber(telemetry, "vy") ?? 0;
  const vz = getNumber(telemetry, "vz") ?? 0;

  return Math.sqrt(vx * vx + vy * vy + vz * vz);
}

export function getTelemetryObstacleCount(
  telemetry: VehicleTelemetry | undefined
): number {
  return getNumber(telemetry, "obstacleCount") ?? getTelemetryObstacles(telemetry).length;
}

export function getTelemetryObstacles(
  telemetry: VehicleTelemetry | undefined
): ObstacleLike[] {
  return getArray<unknown>(telemetry, "obstacles")
    .map((item) => {
      const x = getNumber(item, "x");
      const y = getNumber(item, "y");
      const r = getNumber(item, "r");

      if (x === undefined || y === undefined || r === undefined) {
        return null;
      }

      return { x, y, r };
    })
    .filter((item): item is ObstacleLike => item !== null);
}

export function getTelemetryTargetX(
  telemetry: VehicleTelemetry | undefined
): number | undefined {
  return getNumber(telemetry, "targetX");
}

export function getTelemetryTargetY(
  telemetry: VehicleTelemetry | undefined
): number | undefined {
  return getNumber(telemetry, "targetY");
}

export function getTelemetryDistanceToGoal(
  telemetry: VehicleTelemetry | undefined
): number | undefined {
  return getNumber(telemetry, "distanceToGoalM");
}

export function getTelemetryHeadingErrorDeg(
  telemetry: VehicleTelemetry | undefined
): number | undefined {
  return getNumber(telemetry, "headingErrorDeg");
}

export function getTelemetryObstacleAhead(
  telemetry: VehicleTelemetry | undefined
): boolean {
  return Boolean(asRecord(telemetry)?.obstacleAhead);
}

export function getTelemetryDisplayName(
  telemetry: VehicleTelemetry | undefined
): string | undefined {
  return getString(telemetry, "displayName");
}

export function getTelemetryArmState(telemetry: VehicleTelemetry | undefined): string {
  return getString(telemetry, "armState") ?? "disarmed";
}

export function getTelemetryPosePosition(
  telemetry: VehicleTelemetry | undefined
): Vec3Like {
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

export function getVec3Magnitude(vec: Vec3 | Vec3Like | undefined): number {
  if (!vec) return 0;
  return Math.sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
}

export function buildSceneRoute(
  mission: MissionState | undefined,
  telemetry: VehicleTelemetry | undefined,
  origin: Vec3Like
): SceneRoute {
  const route = mission?.route ?? [];

  const routePoints = route.map(
    (point) => new THREE.Vector3(point.x - origin.x, 0.08, -(point.y - origin.y))
  );

  const firstUnreachedWaypointId = mission?.waypoints.find(
    (waypoint) => !waypoint.reached
  )?.id;

  const waypoints = (mission?.waypoints ?? []).map((waypoint) => ({
    id: waypoint.id,
    label: waypoint.label,
    reached: waypoint.reached,
    active: waypoint.id === firstUnreachedWaypointId,
    position: toScenePosition(waypoint.position, origin, 0.18)
  }));

  const goalX = mission?.goalPosition?.x ?? getTelemetryTargetX(telemetry);
  const goalY = mission?.goalPosition?.y ?? getTelemetryTargetY(telemetry);

  const goalPosition =
    typeof goalX === "number" && typeof goalY === "number"
      ? toScenePosition({ x: goalX, y: goalY }, origin, 1.5)
      : null;

  return {
    routePoints,
    waypoints,
    goalPosition
  };
}

export function toScenePosition(
  point: Vec2,
  origin: Vec3Like,
  height = 0
): [number, number, number] {
  return [point.x - origin.x, height, -(point.y - origin.y)];
}

export function formatFreshness(ageMs: number | undefined) {
  if (ageMs === undefined || !Number.isFinite(ageMs)) return "N/A";
  if (ageMs < 1000) return `${formatNumber(ageMs, 0)} ms`;
  return `${formatNumber(ageMs / 1000, 1)} s`;
}

export function formatNumber(value: number, fractionDigits: number) {
  return Number.isFinite(value) ? value.toFixed(fractionDigits) : "0.00";
}

export function deg2rad(deg: number) {
  return (deg * Math.PI) / 180;
}

export function getThrusterPosition(index: number): [number, number, number] {
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