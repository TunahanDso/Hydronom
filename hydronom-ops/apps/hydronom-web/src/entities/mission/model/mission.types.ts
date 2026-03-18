import type { FreshnessInfo, Vec2, VehicleId } from "../../../shared/types/common.types";

// Görev üst durumu
export type MissionStatus =
  | "idle"
  | "planning"
  | "running"
  | "paused"
  | "completed"
  | "aborted"
  | "failed";

// Görev adımı tipi
export interface MissionStep {
  id: string;
  title: string;
  description: string;
  status: "pending" | "active" | "completed" | "failed";
  order: number;
}

// Waypoint tipi
export interface MissionWaypoint {
  id: string;
  label: string;
  position: Vec2;
  reached: boolean;
}

// Görev event kaydı
export interface MissionEventItem {
  id: string;
  timestamp: string;
  level: "info" | "warn" | "error";
  message: string;
}

// Görev özeti
export interface MissionState {
  vehicleId: VehicleId;
  missionId: string;
  missionName: string;
  status: MissionStatus;
  activeStepId: string | null;
  progressPercent: number;
  goalPosition: Vec2 | null;
  route: Vec2[];
  waypoints: MissionWaypoint[];
  steps: MissionStep[];
  recentEvents: MissionEventItem[];
  freshness: FreshnessInfo;
}