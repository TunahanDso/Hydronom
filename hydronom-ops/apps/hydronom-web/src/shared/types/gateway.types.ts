import type { DiagnosticsState } from "../../entities/diagnostics/model/diagnostics.types";
import type { MissionState } from "../../entities/mission/model/mission.types";
import type { SensorState } from "../../entities/sensor/model/sensor.types";
import type { ActuatorState } from "../../entities/actuator/model/actuator.types";
import type { VehicleTelemetry } from "../../entities/vehicle/model/vehicle.types";
import type { WorldState } from "../../entities/world/model/world.types";
import type {
  ConnectionState,
  HealthState,
  TimestampIso,
  VehicleId
} from "./common.types";

// Gateway üzerinden taşınan temel event zarfı.
export interface GatewayEnvelope<TType extends string, TPayload> {
  type: TType;
  vehicleId: VehicleId;
  timestampUtc: TimestampIso;
  payload: TPayload;
  source?: string;
  sequence?: number;
}

// Araç telemetrisi eventi.
export type VehicleTelemetryMessage = GatewayEnvelope<
  "vehicle.telemetry",
  VehicleTelemetry
>;

// Görev durumu eventi.
export type MissionStateMessage = GatewayEnvelope<
  "mission.state",
  MissionState
>;

// Dünya/senaryo durumu eventi.
export type WorldStateMessage = GatewayEnvelope<
  "world.state",
  WorldState
>;

// Aktüatör durumu eventi.
export type ActuatorStateMessage = GatewayEnvelope<
  "actuator.state",
  ActuatorState
>;

// Sensör durumu eventi.
export type SensorStateMessage = GatewayEnvelope<
  "sensor.state",
  SensorState
>;

// Diagnostics durumu eventi.
export type DiagnosticsStateMessage = GatewayEnvelope<
  "diagnostics.state",
  DiagnosticsState
>;

// Gateway runtime telemetry-summary endpointinden gelen özet payload.
// Bu payload tam mission/world/actuator datası değildir; sadece runtime'ın son özet durumudur.
export interface GatewayRuntimeTelemetrySummaryPayload {
  runtimeId?: string;
  timestampUtc: TimestampIso;
  overallHealth?: string;
  hasCriticalIssue?: boolean;
  hasWarnings?: boolean;

  runtimeConnected?: boolean;
  pythonConnected?: boolean;
  webSocketClientCount?: number;
  totalMessagesReceived?: number;
  totalMessagesBroadcast?: number;

  vehicleId?: VehicleId;
  hasVehicleTelemetry?: boolean;

  x?: number;
  y?: number;
  z?: number;

  rollDeg?: number;
  pitchDeg?: number;
  yawDeg?: number;
  headingDeg?: number;

  vx?: number;
  vy?: number;
  vz?: number;

  rollRateDeg?: number;
  pitchRateDeg?: number;
  yawRateDeg?: number;

  obstacleCount?: number;
  obstacleAhead?: boolean;

  hasSensorState?: boolean;
  sensorHealthy?: boolean;
  sensorName?: string;
  sensorType?: string;

  hasDiagnosticsState?: boolean;
  gatewayStatus?: string;
  lastRuntimeIngressUtc?: TimestampIso | null;
  lastGatewayBroadcastUtc?: TimestampIso | null;
  lastError?: string | null;
  summary?: string;
}

// Runtime summary mesajı.
// Gateway client bu endpointi okuduğunda bu message tipine sarıp dispatcher'a verebilir.
export type GatewayRuntimeTelemetrySummaryMessage = GatewayEnvelope<
  "runtime.telemetry-summary",
  GatewayRuntimeTelemetrySummaryPayload
>;

// Sistem log eventi.
export interface GatewayLogPayload {
  level?: "trace" | "debug" | "info" | "warn" | "error" | "critical";
  source?: string;
  category?: string;
  message: string;
  detail?: string | null;
}

export type GatewayLogMessage =
  | GatewayEnvelope<"system.log", GatewayLogPayload>
  | GatewayEnvelope<"gateway.log", GatewayLogPayload>
  | GatewayEnvelope<"log", GatewayLogPayload>;

// Bağlantı / heartbeat eventi.
export interface GatewayHeartbeatPayload {
  health?: HealthState;
  connection?: ConnectionState;
  runtimeConnected?: boolean;
  connectedClientCount?: number;
  isAlive?: boolean;
}

export type GatewayHeartbeatMessage =
  | GatewayEnvelope<"system.heartbeat", GatewayHeartbeatPayload>
  | GatewayEnvelope<"heartbeat", GatewayHeartbeatPayload>;

// Gateway'den gelebilecek tüm mesajlar.
export type GatewayMessage =
  | VehicleTelemetryMessage
  | MissionStateMessage
  | WorldStateMessage
  | ActuatorStateMessage
  | SensorStateMessage
  | DiagnosticsStateMessage
  | GatewayRuntimeTelemetrySummaryMessage
  | GatewayLogMessage
  | GatewayHeartbeatMessage;