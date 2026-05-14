import * as THREE from "three";

import type { WorldObject } from "../../../entities/world/model/world.types";
import type { SceneWaypoint, Vec3Like } from "../types";

export function worldToSceneVector(
  x: number,
  y: number,
  z: number,
  origin: Vec3Like,
  yOffset = 0
): THREE.Vector3 {
  return new THREE.Vector3(
    x - origin.x,
    z - origin.z + yOffset,
    -(y - origin.y)
  );
}

export function worldToSceneTuple(
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

export function worldObjectToWaypoint(
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

export function getWorldObjectNumber(
  object: WorldObject,
  key: string,
  fallback: number
): number {
  const record = object as unknown as Record<string, unknown>;
  const value = record[key];

  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  const metricValue = object.metrics?.[key];
  if (typeof metricValue === "number" && Number.isFinite(metricValue)) {
    return metricValue;
  }

  const tagMetricValue = object.metrics?.[`tag.${key}`];
  if (typeof tagMetricValue === "number" && Number.isFinite(tagMetricValue)) {
    return tagMetricValue;
  }

  const fieldValue = object.fields?.[key] ?? object.fields?.[`tag.${key}`];
  if (typeof fieldValue === "string") {
    const parsed = Number(fieldValue);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }

  return fallback;
}

export function isWorldWaypointLike(type: string): boolean {
  return (
    type === "start" ||
    type === "start_zone" ||
    type === "checkpoint" ||
    type === "finish" ||
    type === "gate" ||
    type === "waypoint" ||
    type === "release_zone" ||
    type === "pipe_gate" ||
    type === "hint_marker" ||
    type === "pipe_checkpoint"
  );
}

export function normalizeBuoySide(side: string | null | undefined): "left" | "right" {
  return side === "right" ? "right" : "left";
}