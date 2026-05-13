import * as THREE from "three";

import type { HealthState } from "../../shared/types/common.types";

export type Vec3Like = { x: number; y: number; z: number };
export type Vec2Like = { x: number; y: number };
export type ObstacleLike = { x: number; y: number; r: number };

export type LooseRecord = Record<string, unknown>;

export interface SceneThruster {
  id: string;
  active: boolean;
  rpm: number;
  normalizedCommand: number;
  position: [number, number, number];
}

export interface SceneWaypoint {
  id: string;
  label: string;
  reached: boolean;
  active: boolean;
  position: [number, number, number];
}

export interface SceneRoute {
  routePoints: THREE.Vector3[];
  waypoints: SceneWaypoint[];
  goalPosition: [number, number, number] | null;
}

export interface MetricTileProps {
  label: string;
  value: string;
}

export interface StatusBadgeProps {
  label: string;
  state: HealthState;
}