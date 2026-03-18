import type { FreshnessInfo, Vec3, VehicleId } from "../../../shared/types/common.types";

// Thruster yön bilgisi
export type ThrusterDirection = "forward" | "reverse" | "neutral";

// Tek thruster durumu
export interface ThrusterState {
  id: string;
  label: string;
  normalizedCommand: number;
  appliedCommand: number;
  direction: ThrusterDirection;
  rpm: number;
  active: boolean;
}

// Limiter ve debug bayrakları
export interface ActuatorLimiterState {
  satT: boolean;
  satR: boolean;
  rlT: boolean;
  rlR: boolean;
  dbT: boolean;
  dbR: boolean;
  assist: boolean;
  dt: boolean;
}

// Toplam kuvvet / tork görünümü
export interface WrenchState {
  forceBody: Vec3;
  torqueBody: Vec3;
}

// Aktüatör genel durumu
export interface ActuatorState {
  vehicleId: VehicleId;
  thrusters: ThrusterState[];
  wrench: WrenchState;
  limiter: ActuatorLimiterState;
  freshness: FreshnessInfo;
}