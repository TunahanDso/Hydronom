import type {
  ConnectionState,
  FreshnessInfo,
  HealthState,
  VehicleId
} from "../../../shared/types/common.types";

// Stream metrikleri
export interface StreamMetric {
  key: string;
  label: string;
  rateHz: number;
  ageMs: number;
  state: ConnectionState;
}

// Sistem log satırı
export interface DiagnosticLogItem {
  id: string;
  timestamp: string;
  level: "info" | "warn" | "error";
  source: string;
  message: string;
}

// Kaynak inceleme modeli
export interface SourceInspectorItem {
  key: string;
  label: string;
  source: string;
  freshnessMs: number;
  state: HealthState;
}

// Diagnostics ana modeli
export interface DiagnosticsState {
  vehicleId: VehicleId;
  overallConnection: ConnectionState;
  overallHealth: HealthState;
  streamMetrics: StreamMetric[];
  sourceInspector: SourceInspectorItem[];
  logs: DiagnosticLogItem[];
  freshness: FreshnessInfo;
}