import type { GatewayMessage } from "../../shared/types/gateway.types";
import {
  type ConnectionState,
  type HealthState
} from "../../shared/types/common.types";
import { useVehicleStore } from "../../features/vehicle-state/store/vehicle.store";
import { useMissionStore } from "../../features/mission-state/store/mission.store";
import { useActuatorStore } from "../../features/actuator-monitoring/store/actuator.store";
import { useSensorStore } from "../../features/sensor-visualization/store/sensor.store";
import { useDiagnosticsStore } from "../../features/diagnostics-monitoring/store/diagnostics.store";

// Gateway'den gelen mesajı ilgili store'a dağıtır
export function dispatchGatewayMessage(message: GatewayMessage) {
  const timestamp =
    (message as { timestampUtc?: string }).timestampUtc ??
    (message as { timestamp?: string }).timestamp ??
    new Date().toISOString();

  switch (message.type) {
    case "vehicle.telemetry": {
      useVehicleStore.getState().upsertTelemetry(message.payload);
      return;
    }

    case "mission.state": {
      useMissionStore.getState().upsertMissionState(message.payload);
      return;
    }

    case "actuator.state": {
      useActuatorStore.getState().upsertActuatorState(message.payload);
      return;
    }

    case "sensor.state": {
      useSensorStore.getState().upsertSensorState(message.payload);
      return;
    }

    case "diagnostics.state": {
      useDiagnosticsStore.getState().upsertDiagnosticsState(message.payload);
      return;
    }

    case "system.log":
    case "gateway.log":
    case "log": {
      const diagnosticsStore = useDiagnosticsStore.getState();
      const current = diagnosticsStore.diagnosticsByVehicleId[message.vehicleId];

      if (!current) {
        return;
      }

      const payload = message.payload as {
        source?: string;
        category?: string;
        level?: string;
        message?: string;
      };

      diagnosticsStore.upsertDiagnosticsState({
        ...current,
        logs: [
          {
            id: `${message.vehicleId}-${timestamp}-${payload.source ?? payload.category ?? "gateway"}-${payload.level ?? "info"}`,
            timestamp,
            level: normalizeLogLevel(payload.level),
            source: payload.source ?? payload.category ?? "gateway",
            message: payload.message ?? ""
          },
          ...current.logs
        ].slice(0, 100),
        freshness: {
          ...current.freshness,
          timestamp,
          ageMs: 0
        }
      });

      return;
    }

    case "system.heartbeat":
    case "heartbeat": {
      const diagnosticsStore = useDiagnosticsStore.getState();
      const current = diagnosticsStore.diagnosticsByVehicleId[message.vehicleId];

      if (!current) {
        return;
      }

      const payload = message.payload as {
        connection?: string;
        health?: string;
        runtimeConnected?: boolean;
        connectedClientCount?: number;
        isAlive?: boolean;
      };

      diagnosticsStore.upsertDiagnosticsState({
        ...current,
        overallConnection: normalizeConnectionState(
          payload.connection,
          payload.runtimeConnected
        ),
        overallHealth: normalizeHealthState(payload.health, payload.isAlive),
        freshness: {
          ...current.freshness,
          timestamp,
          ageMs: 0,
          isStale:
            payload.connection === "disconnected" ||
            payload.runtimeConnected === false
        }
      });

      return;
    }

    default: {
      console.debug("Bilinmeyen gateway mesajı:", message);
      return;
    }
  }
}

function normalizeConnectionState(
  value?: string,
  runtimeConnected?: boolean
): ConnectionState {
  const normalized = (value ?? "").toLowerCase();

  if (normalized === "connected") {
    return "connected";
  }

  if (normalized === "degraded") {
    return "degraded";
  }

  if (normalized === "disconnected") {
    return "disconnected";
  }

  if (runtimeConnected === true) {
    return "connected";
  }

  return "disconnected";
}

function normalizeHealthState(
  value?: string,
  isAlive?: boolean
): HealthState {
  const normalized = (value ?? "").toLowerCase();

  if (normalized === "ok" || normalized === "healthy") {
    return "ok";
  }

  if (normalized === "warn" || normalized === "warning") {
    return "warn";
  }

  if (normalized === "error" || normalized === "critical") {
    return "error";
  }

  if (isAlive === false) {
    return "error";
  }

  return "ok";
}

function normalizeLogLevel(value?: string): "info" | "warn" | "error" {
  const normalized = (value ?? "").toLowerCase();

  if (normalized === "warn" || normalized === "warning") {
    return "warn";
  }

  if (normalized === "error" || normalized === "critical") {
    return "error";
  }

  return "info";
}