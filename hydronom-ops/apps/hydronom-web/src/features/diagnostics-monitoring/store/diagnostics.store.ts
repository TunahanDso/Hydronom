import { create } from "zustand";
import type { DiagnosticsState } from "../../../entities/diagnostics/model/diagnostics.types";
import type { SourceKind, VehicleId } from "../../../shared/types/common.types";

interface GatewayDiagnosticsStateDto {
  timestampUtc: string;
  vehicleId?: VehicleId;
  gatewayStatus?: string;
  runtimeConnected?: boolean;
  hasWebSocketClients?: boolean;
  connectedWebSocketClients?: number;
  lastRuntimeMessageUtc?: string | null;
  runtimeFreshness?: {
    timestamp: string;
    ageMs: number;
    isStale?: boolean;
    isFresh?: boolean;
    source?: string;
  } | null;
  lastError?: string | null;
  lastErrorUtc?: string | null;
  ingressMessageCount?: number;
  broadcastMessageCount?: number;
}

interface DiagnosticsStore {
  diagnosticsByVehicleId: Record<VehicleId, DiagnosticsState>;
  upsertDiagnosticsState: (
    state: DiagnosticsState | GatewayDiagnosticsStateDto
  ) => void;
}

// Başlangıç için mock diagnostics verisi
const initialDiagnostics: DiagnosticsState = {
  vehicleId: "HYD-01",
  overallConnection: "connected",
  overallHealth: "ok",
  streamMetrics: [
    {
      key: "runtime",
      label: "Runtime Stream",
      rateHz: 20,
      ageMs: 35,
      state: "connected"
    },
    {
      key: "lidar",
      label: "Lidar Stream",
      rateHz: 10,
      ageMs: 92,
      state: "connected"
    },
    {
      key: "twin",
      label: "Twin Sim Stream",
      rateHz: 15,
      ageMs: 44,
      state: "connected"
    }
  ],
  sourceInspector: [
    {
      key: "pose",
      label: "Vehicle Pose",
      source: "runtime",
      freshnessMs: 35,
      state: "ok"
    },
    {
      key: "externalState",
      label: "External State",
      source: "python/twin",
      freshnessMs: 48,
      state: "ok"
    },
    {
      key: "occupancy",
      label: "Occupancy Grid",
      source: "python plugin",
      freshnessMs: 140,
      state: "warn"
    }
  ],
  logs: [
    {
      id: "log-1",
      timestamp: new Date().toISOString(),
      level: "info",
      source: "gateway",
      message: "Hydronom Ops mock diagnostics initialized."
    },
    {
      id: "log-2",
      timestamp: new Date().toISOString(),
      level: "warn",
      source: "runtime",
      message:
        "Navigation health is degraded due to obstacle-aware path adjustment."
    }
  ],
  freshness: {
    timestamp: new Date().toISOString(),
    ageMs: 80,
    isStale: false,
    source: "runtime"
  }
};

function isGatewayDiagnosticsStateDto(
  diagnosticsState: DiagnosticsState | GatewayDiagnosticsStateDto
): diagnosticsState is GatewayDiagnosticsStateDto {
  return "timestampUtc" in diagnosticsState;
}

// Gateway tarafındaki serbest metin source değerlerini frontend'in güvenli SourceKind tipine indirger.
function normalizeSourceKind(value?: string | null): SourceKind {
  switch ((value ?? "").toLowerCase()) {
    case "runtime":
      return "runtime";
    case "external":
      return "external";
    case "python":
      return "python";
    case "twin":
      return "twin";
    case "sim":
      return "sim";
    default:
      return "unknown";
  }
}

function mapGatewayDiagnosticsStateToDiagnosticsState(
  dto: GatewayDiagnosticsStateDto,
  previous?: DiagnosticsState
): DiagnosticsState {
  const vehicleId = dto.vehicleId ?? previous?.vehicleId ?? "hydronom-main";

  const runtimeConnected = dto.runtimeConnected ?? false;
  const wsCount = dto.connectedWebSocketClients ?? 0;
  const ageMs = dto.runtimeFreshness?.ageMs ?? 0;

  return {
    vehicleId,
    overallConnection: runtimeConnected ? "connected" : "disconnected",
    overallHealth:
      dto.lastError || dto.gatewayStatus === "error"
        ? "error"
        : dto.runtimeFreshness?.isStale === true
          ? "warn"
          : "ok",
    streamMetrics: [
      {
        key: "runtime",
        label: "Runtime Stream",
        rateHz: 0,
        ageMs,
        state: runtimeConnected ? "connected" : "disconnected"
      },
      {
        key: "websocket",
        label: "WebSocket Clients",
        rateHz: 0,
        ageMs: 0,
        state: wsCount > 0 ? "connected" : "disconnected"
      }
    ],
    sourceInspector: [
      {
        key: "runtime",
        label: "Runtime Feed",
        source: "gateway",
        freshnessMs: ageMs,
        state: runtimeConnected ? "ok" : "warn"
      }
    ],
    logs: previous?.logs ?? [],
    freshness: dto.runtimeFreshness
      ? {
          timestamp: dto.runtimeFreshness.timestamp,
          ageMs: dto.runtimeFreshness.ageMs,
          isStale:
            dto.runtimeFreshness.isStale ??
            (dto.runtimeFreshness.isFresh !== undefined
              ? !dto.runtimeFreshness.isFresh
              : false),
          source: normalizeSourceKind(dto.runtimeFreshness.source ?? "runtime")
        }
      : {
          timestamp: dto.timestampUtc,
          ageMs: 0,
          isStale: false,
          source: "runtime"
        }
  };
}

export const useDiagnosticsStore = create<DiagnosticsStore>((set) => ({
  diagnosticsByVehicleId: {
    [initialDiagnostics.vehicleId]: initialDiagnostics
  },

  upsertDiagnosticsState: (diagnosticsState) =>
    set((state) => {
      if (isGatewayDiagnosticsStateDto(diagnosticsState)) {
        const vehicleId =
          diagnosticsState.vehicleId ?? ("hydronom-main" as VehicleId);
        const previous = state.diagnosticsByVehicleId[vehicleId];

        return {
          diagnosticsByVehicleId: {
            ...state.diagnosticsByVehicleId,
            [vehicleId]: mapGatewayDiagnosticsStateToDiagnosticsState(
              diagnosticsState,
              previous
            )
          }
        };
      }

      return {
        diagnosticsByVehicleId: {
          ...state.diagnosticsByVehicleId,
          [diagnosticsState.vehicleId]: diagnosticsState
        }
      };
    })
}));