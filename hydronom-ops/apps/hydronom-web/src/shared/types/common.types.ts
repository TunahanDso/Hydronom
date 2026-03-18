// Ortak kullanılacak temel tipler burada tutulur

export type TimestampIso = string;
export type VehicleId = string;
export type SourceKind = "runtime" | "external" | "python" | "twin" | "sim" | "unknown";
export type ConnectionState = "connected" | "degraded" | "disconnected";
export type HealthState = "ok" | "warn" | "error" | "unknown";

export interface Vec2 {
  x: number;
  y: number;
}

export interface Vec3 {
  x: number;
  y: number;
  z: number;
}

export interface Rpy {
  roll: number;
  pitch: number;
  yaw: number;
}

export interface FreshnessInfo {
  timestamp: TimestampIso;
  ageMs: number;
  isStale: boolean;
  source: SourceKind;
}

export interface StatusFlag {
  key: string;
  label: string;
  value: boolean | string | number;
}