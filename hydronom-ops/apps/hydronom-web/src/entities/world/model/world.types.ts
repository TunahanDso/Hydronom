import type { FreshnessInfo, TimestampIso, VehicleId } from "../../../shared/types/common.types";

export interface WorldTarget {
  x: number;
  y: number;
  z: number;
  toleranceMeters?: number | null;
}

export interface WorldRoutePoint {
  id: string;
  label?: string | null;
  objectiveId?: string | null;
  index: number;
  type: string;
  x: number;
  y: number;
  z: number;
  toleranceMeters?: number | null;
  active: boolean;
  completed: boolean;
}

export type WorldObjectType =
  | "start"
  | "checkpoint"
  | "finish"
  | "buoy"
  | "obstacle"
  | "gate"
  | "zone"
  | "object"
  | string;

export interface WorldObject {
  id: string;
  type: WorldObjectType;
  label?: string | null;
  objectiveId?: string | null;
  side?: "left" | "right" | string | null;
  x: number;
  y: number;
  z: number;
  radius: number;
  color?: string | null;
  active: boolean;
  completed: boolean;
  metrics: Record<string, number>;
  fields: Record<string, string>;
}

export interface WorldState {
  timestampUtc: TimestampIso;
  vehicleId: VehicleId;
  source: string;
  scenarioId?: string | null;
  scenarioName?: string | null;
  runId?: string | null;
  currentObjectiveId?: string | null;
  activeObjectiveTarget?: WorldTarget | null;
  route: WorldRoutePoint[];
  objects: WorldObject[];
  metrics: Record<string, number>;
  fields: Record<string, string>;
  freshness?: FreshnessInfo | null;
}