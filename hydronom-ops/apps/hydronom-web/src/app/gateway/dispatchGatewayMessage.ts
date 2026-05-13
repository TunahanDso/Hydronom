import type {
  GatewayMessage,
  GatewayRuntimeTelemetrySummaryPayload
} from "../../shared/types/gateway.types";
import {
  type ConnectionState,
  type HealthState,
  type SourceKind,
  type VehicleId
} from "../../shared/types/common.types";
import type { VehicleTelemetry } from "../../entities/vehicle/model/vehicle.types";
import type { SensorState } from "../../entities/sensor/model/sensor.types";
import type {
  MissionState,
  MissionStatus,
  MissionStep
} from "../../entities/mission/model/mission.types";
import type {
  ActuatorLimiterState,
  ActuatorState,
  ThrusterDirection,
  ThrusterState
} from "../../entities/actuator/model/actuator.types";
import type { DiagnosticsState } from "../../entities/diagnostics/model/diagnostics.types";
import type { WorldState } from "../../entities/world/model/world.types";

import { useVehicleStore } from "../../features/vehicle-state/store/vehicle.store";
import { useMissionStore } from "../../features/mission-state/store/mission.store";
import { useWorldStore } from "../../features/world-state/store/world.store";
import { useActuatorStore } from "../../features/actuator-monitoring/store/actuator.store";
import { useSensorStore } from "../../features/sensor-visualization/store/sensor.store";
import { useDiagnosticsStore } from "../../features/diagnostics-monitoring/store/diagnostics.store";

type BackendFreshness = {
  timestamp?: string;
  timestampUtc?: string;
  ageMs?: number;
  isStale?: boolean;
  isFresh?: boolean;
  source?: string;
};

type BackendMissionState = {
  timestampUtc?: string;
  vehicleId?: VehicleId;
  missionId?: string | null;
  missionName?: string | null;
  status?: string | null;
  currentStepIndex?: number;
  totalStepCount?: number;
  currentStepTitle?: string | null;
  nextObjective?: string | null;
  remainingDistanceMeters?: number | null;
  warnings?: string[];
  freshness?: BackendFreshness | null;
};

type BackendActuatorState = {
  timestampUtc?: string;
  vehicleId?: VehicleId;
  actuatorName?: string;
  actuatorType?: string;
  isEnabled?: boolean;
  isHealthy?: boolean;
  command?: number;
  rawCommand?: number;
  rpm?: number;
  currentMa?: number;
  voltage?: number;
  temperatureC?: number;
  lastError?: string | null;
  metrics?: Record<string, number>;
  fields?: Record<string, string>;
  freshness?: BackendFreshness | null;
};

type GatewaySnapshotPayload = GatewayRuntimeTelemetrySummaryPayload & {
  vehicleTelemetry?: VehicleTelemetry | null;
  missionState?: BackendMissionState | MissionState | null;
  worldState?: WorldState | null;
  sensorState?: SensorState | null;
  runtimeSensorState?: SensorState | null;
  debugSensorState?: SensorState | null;
  actuatorState?: BackendActuatorState | ActuatorState | null;
  diagnosticsState?: DiagnosticsState | null;
};

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

    case "runtime.telemetry-summary": {
      dispatchRuntimeTelemetrySummary(message.payload, timestamp);
      return;
    }

    case "mission.state": {
      useMissionStore.getState().upsertMissionState(message.payload);
      return;
    }

    case "world.state": {
      useWorldStore.getState().upsertWorldState(message.payload);
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

      if (!current) return;

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

      if (!current) return;

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

function dispatchRuntimeTelemetrySummary(
  payload: GatewayRuntimeTelemetrySummaryPayload,
  fallbackTimestamp: string
) {
  const snapshot = payload as GatewaySnapshotPayload;
  const timestamp = payload.timestampUtc ?? fallbackTimestamp;
  const vehicleId = payload.vehicleId ?? ("hydronom-main" as VehicleId);

  const worldState = snapshot.worldState ?? null;
  const missionState = snapshot.missionState ?? null;
  const actuatorState = snapshot.actuatorState ?? null;

  if (worldState) {
    useWorldStore.getState().upsertWorldState(worldState);
  }

  if (snapshot.vehicleTelemetry) {
    useVehicleStore.getState().upsertTelemetry(
      normalizeSnapshotVehicleTelemetry(
        snapshot.vehicleTelemetry,
        payload,
        missionState,
        worldState,
        vehicleId,
        timestamp
      )
    );
  } else if (payload.hasVehicleTelemetry !== false) {
    useVehicleStore.getState().upsertTelemetry(
      mapRuntimeSummaryToVehicleTelemetry(
        payload,
        missionState,
        worldState,
        vehicleId,
        timestamp
      )
    );
  }

  if (missionState) {
    useMissionStore.getState().upsertMissionState(
      normalizeSnapshotMissionState(missionState, worldState, vehicleId, timestamp)
    );
  }

  if (actuatorState) {
    useActuatorStore.getState().upsertActuatorState(
      normalizeSnapshotActuatorState(actuatorState, vehicleId, timestamp)
    );
  }

  if (snapshot.sensorState) {
    useSensorStore.getState().upsertSensorState(snapshot.sensorState);
  } else if (snapshot.runtimeSensorState) {
    useSensorStore.getState().upsertSensorState(snapshot.runtimeSensorState);
  } else if (payload.hasSensorState !== false) {
    useSensorStore.getState().upsertSensorState(
      mapRuntimeSummaryToSensorState(payload, vehicleId, timestamp)
    );
  }

  if (snapshot.diagnosticsState) {
    useDiagnosticsStore.getState().upsertDiagnosticsState(snapshot.diagnosticsState);
    return;
  }

  useDiagnosticsStore.getState().upsertDiagnosticsState({
    timestampUtc: timestamp,
    vehicleId,
    gatewayStatus: payload.gatewayStatus ?? payload.overallHealth ?? "Healthy",
    runtimeConnected: payload.runtimeConnected ?? false,
    hasWebSocketClients: (payload.webSocketClientCount ?? 0) > 0,
    connectedWebSocketClients: payload.webSocketClientCount ?? 0,
    lastRuntimeMessageUtc: payload.lastRuntimeIngressUtc ?? timestamp,
    runtimeFreshness: {
      timestamp: payload.lastRuntimeIngressUtc ?? timestamp,
      ageMs: 0,
      isStale: payload.runtimeConnected === false,
      isFresh: payload.runtimeConnected !== false,
      source: "runtime"
    },
    lastError: payload.lastError ?? null,
    lastErrorUtc: null,
    ingressMessageCount: payload.totalMessagesReceived ?? 0,
    broadcastMessageCount: payload.totalMessagesBroadcast ?? 0
  });
}

function normalizeSnapshotVehicleTelemetry(
  telemetry: VehicleTelemetry,
  payload: GatewayRuntimeTelemetrySummaryPayload,
  missionState: BackendMissionState | MissionState | null,
  worldState: WorldState | null,
  vehicleId: VehicleId,
  timestamp: string
): VehicleTelemetry {
  const x = firstFinite(
    telemetry.x,
    telemetry.pose?.position?.x,
    payload.x,
    0
  );
  const y = firstFinite(
    telemetry.y,
    telemetry.pose?.position?.y,
    payload.y,
    0
  );
  const z = firstFinite(
    telemetry.z,
    telemetry.pose?.position?.z,
    payload.z,
    0
  );

  const activeTarget = worldState?.activeObjectiveTarget;
  const activeRoute = worldState?.route?.find((point) => point.active);
  const nextRoute = worldState?.route?.find((point) => !point.completed);

  const targetX = firstFinite(
    activeTarget?.x,
    activeRoute?.x,
    nextRoute?.x,
    telemetry.targetX,
    x
  );
  const targetY = firstFinite(
    activeTarget?.y,
    activeRoute?.y,
    nextRoute?.y,
    telemetry.targetY,
    y
  );

  const missionDistance = readNumber(
    (missionState as BackendMissionState | null)?.remainingDistanceMeters
  );
  const telemetryDistance = readNumber(telemetry.distanceToGoalM);

  const remainingDistance =
    missionDistance ??
    telemetryDistance ??
    calculateDistance2d(x, y, targetX, targetY);

  const yawDeg = firstFinite(
    telemetry.yawDeg,
    telemetry.pose?.orientation?.yaw,
    payload.yawDeg,
    payload.headingDeg,
    0
  );
  const headingDeg = firstFinite(telemetry.headingDeg, payload.headingDeg, yawDeg);

  const telemetryHeadingError = readNumber(telemetry.headingErrorDeg);
  const headingError =
    telemetryHeadingError ??
    calculateHeadingErrorDeg(x, y, targetX, targetY, yawDeg);

  const vx = firstFinite(telemetry.vx, telemetry.motion?.linearVelocity?.x, payload.vx, 0);
  const vy = firstFinite(telemetry.vy, telemetry.motion?.linearVelocity?.y, payload.vy, 0);
  const vz = firstFinite(telemetry.vz, telemetry.motion?.linearVelocity?.z, payload.vz, 0);

  const rollRateDeg = firstFinite(
    telemetry.rollRateDeg,
    telemetry.motion?.angularVelocity?.x,
    payload.rollRateDeg,
    0
  );
  const pitchRateDeg = firstFinite(
    telemetry.pitchRateDeg,
    telemetry.motion?.angularVelocity?.y,
    payload.pitchRateDeg,
    0
  );
  const yawRateDeg = firstFinite(
    telemetry.yawRateDeg,
    telemetry.motion?.angularVelocity?.z,
    payload.yawRateDeg,
    0
  );

  const speed = firstFinite(
    telemetry.motion?.speed,
    readNumber((payload as { speed?: unknown }).speed),
    Math.sqrt(vx * vx + vy * vy + vz * vz),
    0
  );

  return {
    ...telemetry,
    vehicleId: telemetry.vehicleId ?? vehicleId,
    displayName: telemetry.displayName ?? telemetry.vehicleId ?? vehicleId,

    x,
    y,
    z,
    yawDeg,
    headingDeg,
    vx,
    vy,
    vz,
    rollRateDeg,
    pitchRateDeg,
    yawRateDeg,

    targetX,
    targetY,
    distanceToGoalM: remainingDistance,
    headingErrorDeg: headingError,

    pose: {
      ...telemetry.pose,
      position: {
        ...telemetry.pose?.position,
        x,
        y,
        z
      },
      orientation: {
        ...telemetry.pose?.orientation,
        yaw: yawDeg
      }
    },

    motion: {
      ...telemetry.motion,
      speed,
      linearVelocity: {
        ...telemetry.motion?.linearVelocity,
        x: vx,
        y: vy,
        z: vz
      },
      angularVelocity: {
        ...telemetry.motion?.angularVelocity,
        x: rollRateDeg,
        y: pitchRateDeg,
        z: yawRateDeg
      }
    },

    map: {
      ...telemetry.map,
      worldPosition: {
        x,
        y
      },
      headingDeg
    },

    freshness: {
      ...telemetry.freshness,
      timestamp: telemetry.freshness?.timestamp ?? timestamp,
      ageMs: telemetry.freshness?.ageMs ?? 0,
      source: telemetry.freshness?.source ?? "runtime"
    }
  };
}

function normalizeSnapshotMissionState(
  value: BackendMissionState | MissionState,
  worldState: WorldState | null,
  fallbackVehicleId: VehicleId,
  fallbackTimestamp: string
): MissionState {
  if (isFrontendMissionState(value)) {
    const route = worldState?.route ?? [];
    const activeRoute = route.find((point) => point.active);
    const nextRoute = route.find((point) => !point.completed);

    return {
      ...value,
      vehicleId: value.vehicleId ?? worldState?.vehicleId ?? fallbackVehicleId,
      missionId: value.missionId ?? worldState?.scenarioId ?? "runtime-mission",
      missionName: value.missionName ?? worldState?.scenarioName ?? "Runtime Mission",
      activeStepId:
        value.activeStepId ??
        activeRoute?.id ??
        nextRoute?.id ??
        worldState?.currentObjectiveId ??
        null,
      goalPosition:
        value.goalPosition ??
        (worldState?.activeObjectiveTarget
          ? {
              x: worldState.activeObjectiveTarget.x,
              y: worldState.activeObjectiveTarget.y
            }
          : activeRoute
            ? {
                x: activeRoute.x,
                y: activeRoute.y
              }
            : null),
      route:
        value.route?.length > 0
          ? value.route
          : route.map((point) => ({
              x: point.x,
              y: point.y
            })),
      waypoints:
        value.waypoints?.length > 0
          ? value.waypoints
          : route.map((point) => ({
              id: point.id,
              label: point.label ?? point.id,
              position: { x: point.x, y: point.y },
              reached: Boolean(point.completed)
            })),
      steps:
        value.steps?.length > 0
          ? value.steps
          : route.map((point): MissionStep => ({
              id: point.id,
              title: point.label ?? point.id,
              description: `Runtime objective: ${point.objectiveId ?? point.id}`,
              status: point.completed ? "completed" : point.active ? "active" : "pending",
              order: point.index + 1
            }))
    };
  }

  const route = (worldState?.route ?? [])
    .slice()
    .sort((a, b) => a.index - b.index);

  const routePoints = route.map((point) => ({
    x: point.x,
    y: point.y
  }));

  const worldCheckpointObjects = (worldState?.objects ?? [])
    .filter(
      (object) =>
        object.type === "checkpoint" ||
        object.type === "finish" ||
        object.type === "start"
    )
    .sort((a, b) => {
      const aRoute = route.find(
        (point) => point.objectiveId === a.objectiveId || point.id === a.objectiveId
      );
      const bRoute = route.find(
        (point) => point.objectiveId === b.objectiveId || point.id === b.objectiveId
      );
      return (aRoute?.index ?? 0) - (bRoute?.index ?? 0);
    });

  const waypoints =
    route.length > 0
      ? route.map((point) => ({
          id: point.id,
          label: point.label ?? point.id,
          position: { x: point.x, y: point.y },
          reached: Boolean(point.completed)
        }))
      : worldCheckpointObjects.map((object) => ({
          id: object.id,
          label: object.label ?? object.id,
          position: { x: object.x, y: object.y },
          reached: Boolean(object.completed)
        }));

  const activeRoute =
    route.find((point) => point.active) ??
    route.find((point) => !point.completed);

  const totalSteps = Math.max(0, value.totalStepCount ?? route.length);
  const currentStepIndex = Math.max(0, value.currentStepIndex ?? 0);
  const completedCount = waypoints.filter((waypoint) => waypoint.reached).length;

  const progressPercent =
    totalSteps > 0
      ? Math.min(100, Math.max(0, (completedCount / totalSteps) * 100))
      : 0;

  const activeStepId =
    activeRoute?.id ??
    value.currentStepTitle ??
    value.nextObjective ??
    worldState?.currentObjectiveId ??
    null;

  const steps: MissionStep[] =
    route.length > 0
      ? route.map((point): MissionStep => ({
          id: point.id,
          title: point.label ?? point.id,
          description: `Runtime objective: ${point.objectiveId ?? point.id}`,
          status: point.completed
            ? "completed"
            : point.active
              ? "active"
              : "pending",
          order: point.index + 1
        }))
      : activeStepId
        ? [
            {
              id: activeStepId,
              title: value.currentStepTitle ?? activeStepId,
              description: "Runtime scenario objective",
              status: "active",
              order: currentStepIndex
            }
          ]
        : [];

  return {
    vehicleId: value.vehicleId ?? worldState?.vehicleId ?? fallbackVehicleId,
    missionId: value.missionId ?? worldState?.scenarioId ?? "runtime-mission",
    missionName: value.missionName ?? worldState?.scenarioName ?? "Runtime Mission",
    status: normalizeMissionStatus(value.status),
    activeStepId,
    progressPercent,
    goalPosition:
      worldState?.activeObjectiveTarget
        ? {
            x: worldState.activeObjectiveTarget.x,
            y: worldState.activeObjectiveTarget.y
          }
        : activeRoute
          ? {
              x: activeRoute.x,
              y: activeRoute.y
            }
          : null,
    route: routePoints,
    waypoints,
    steps,
    recentEvents: [
      {
        id: `${value.missionId ?? "runtime"}-${fallbackTimestamp}`,
        timestamp: value.timestampUtc ?? fallbackTimestamp,
        level: "info",
        message:
          value.remainingDistanceMeters !== null &&
          typeof value.remainingDistanceMeters === "number"
            ? `Remaining distance: ${value.remainingDistanceMeters.toFixed(2)} m`
            : value.nextObjective ?? "Runtime mission state updated."
      },
      ...(value.warnings ?? []).slice(0, 3).map((warning, index) => ({
        id: `${value.missionId ?? "runtime"}-warning-${index}`,
        timestamp: value.timestampUtc ?? fallbackTimestamp,
        level: "warn" as const,
        message: warning
      }))
    ],
    freshness: normalizeFreshness(value.freshness, value.timestampUtc ?? fallbackTimestamp, "runtime")
  };
}

function normalizeSnapshotActuatorState(
  value: BackendActuatorState | ActuatorState,
  fallbackVehicleId: VehicleId,
  fallbackTimestamp: string
): ActuatorState {
  if (isFrontendActuatorState(value)) {
    return value;
  }

  const metrics = value.metrics ?? {};
  const fields = value.fields ?? {};

  const thrusterCount = Math.max(0, Math.floor(readNumber(metrics["thruster.count"]) ?? 0));
  const thrusters: ThrusterState[] = [];

  for (let index = 0; index < thrusterCount; index++) {
    const prefix = `thruster.${index}`;
    const id = fields[`${prefix}.id`] ?? `T${index + 1}`;
    const command = finite(metrics[`${prefix}.command`], 0);
    const rpm = finite(metrics[`${prefix}.rpm`], 0);
    const active = Math.abs(command) > 0.001 || rpm > 1;

    thrusters.push({
      id,
      label: id,
      normalizedCommand: command,
      appliedCommand: command,
      direction: normalizeThrusterDirection(command),
      rpm,
      active
    });
  }

  const forceBody = {
    x: finite(metrics["force.x"], 0),
    y: finite(metrics["force.y"], 0),
    z: finite(metrics["force.z"], 0)
  };

  const torqueBody = {
    x: finite(metrics["torque.x"], 0),
    y: finite(metrics["torque.y"], 0),
    z: finite(metrics["torque.z"], 0)
  };

  const limiter: ActuatorLimiterState = {
    satT:
      booleanFromString(fields["allocation.hadSaturation"]) ||
      finite(metrics["allocation.saturationRatio"], 0) > 0.001,
    satR: false,
    rlT: booleanFromString(fields["allocation.authorityLimited"]),
    rlR: false,
    dbT: false,
    dbR: false,
    assist: false,
    dt: false
  };

  return {
    vehicleId: value.vehicleId ?? fallbackVehicleId,
    thrusters,
    wrench: {
      forceBody,
      torqueBody
    },
    limiter,
    freshness: normalizeFreshness(value.freshness, value.timestampUtc ?? fallbackTimestamp, "runtime")
  };
}

function isFrontendMissionState(value: unknown): value is MissionState {
  return Boolean(
    value &&
      typeof value === "object" &&
      Array.isArray((value as MissionState).route) &&
      Array.isArray((value as MissionState).waypoints) &&
      Array.isArray((value as MissionState).steps)
  );
}

function isFrontendActuatorState(value: unknown): value is ActuatorState {
  return Boolean(
    value &&
      typeof value === "object" &&
      Array.isArray((value as ActuatorState).thrusters) &&
      (value as ActuatorState).wrench &&
      (value as ActuatorState).limiter
  );
}

function mapRuntimeSummaryToVehicleTelemetry(
  payload: GatewayRuntimeTelemetrySummaryPayload,
  missionState: BackendMissionState | MissionState | null,
  worldState: WorldState | null,
  vehicleId: VehicleId,
  timestamp: string
): VehicleTelemetry {
  const x = finite(payload.x, 0);
  const y = finite(payload.y, 0);
  const z = finite(payload.z, 0);

  const rollDeg = finite(payload.rollDeg, 0);
  const pitchDeg = finite(payload.pitchDeg, 0);
  const yawDeg = finite(payload.yawDeg ?? payload.headingDeg, 0);
  const headingDeg = finite(payload.headingDeg ?? payload.yawDeg, yawDeg);

  const vx = finite(payload.vx, 0);
  const vy = finite(payload.vy, 0);
  const vz = finite(payload.vz, 0);

  const rollRateDeg = finite(payload.rollRateDeg, 0);
  const pitchRateDeg = finite(payload.pitchRateDeg, 0);
  const yawRateDeg = finite(payload.yawRateDeg, 0);

  const speed = firstFinite(
    readNumber((payload as { speed?: unknown }).speed),
    Math.sqrt(vx * vx + vy * vy + vz * vz),
    0
  );

  const activeTarget = worldState?.activeObjectiveTarget;
  const activeRoute = worldState?.route?.find((point) => point.active);
  const nextRoute = worldState?.route?.find((point) => !point.completed);

  const targetX = firstFinite(activeTarget?.x, activeRoute?.x, nextRoute?.x, x);
  const targetY = firstFinite(activeTarget?.y, activeRoute?.y, nextRoute?.y, y);

  const missionDistance = readNumber(
    (missionState as BackendMissionState | null)?.remainingDistanceMeters
  );

  const distanceToGoalM =
    missionDistance ?? calculateDistance2d(x, y, targetX, targetY);

  const headingErrorDeg = calculateHeadingErrorDeg(x, y, targetX, targetY, yawDeg);

  const obstacleCount = Math.max(0, finite(payload.obstacleCount, 0));
  const obstacleAhead = Boolean(payload.obstacleAhead);

  const telemetry: VehicleTelemetry = {
    vehicleId,
    displayName: vehicleId,

    mode: "manual" as VehicleTelemetry["mode"],
    armState: "disarmed" as VehicleTelemetry["armState"],

    pose: {
      position: {
        x,
        y,
        z
      },
      orientation: {
        roll: rollDeg,
        pitch: pitchDeg,
        yaw: yawDeg
      }
    },

    motion: {
      speed,
      linearVelocity: {
        x: vx,
        y: vy,
        z: vz
      },
      angularVelocity: {
        x: rollRateDeg,
        y: pitchRateDeg,
        z: yawRateDeg
      },
      linearAcceleration: {
        x: 0,
        y: 0,
        z: 0
      }
    },

    map: {
      worldPosition: {
        x,
        y
      },
      headingDeg,
      trail: []
    },

    x,
    y,
    z,
    rollDeg,
    pitchDeg,
    yawDeg,
    headingDeg,
    vx,
    vy,
    vz,
    rollRateDeg,
    pitchRateDeg,
    yawRateDeg,

    targetX,
    targetY,

    distanceToGoalM,
    headingErrorDeg,
    obstacleAhead,
    obstacleCount,
    obstacles: [],
    landmarks: [],

    freshness: {
      timestamp,
      ageMs: 0,
      isStale: payload.runtimeConnected === false,
      source: "runtime"
    },

    health: {
      overall: normalizeHealthState(payload.overallHealth, payload.runtimeConnected),
      sensors: payload.sensorHealthy === false ? "warn" : "ok",
      actuators: "unknown",
      navigation: obstacleAhead || obstacleCount > 0 ? "warn" : "ok",
      autonomy: normalizeHealthState(payload.gatewayStatus, payload.runtimeConnected)
    },

    connections: {
      runtimeConnected: payload.runtimeConnected ?? false,
      gatewayConnected: true,
      pythonConnected: payload.pythonConnected ?? false,
      twinActive: false
    },

    flags: [
      {
        key: "runtimeConnected",
        label: "Runtime Connected",
        value: payload.runtimeConnected ?? false
      },
      {
        key: "messagesReceived",
        label: "Messages Received",
        value: payload.totalMessagesReceived ?? 0
      },
      {
        key: "gatewayStatus",
        label: "Gateway Status",
        value: payload.gatewayStatus ?? payload.overallHealth ?? "unknown"
      }
    ]
  };

  return telemetry;
}

function mapRuntimeSummaryToSensorState(
  payload: GatewayRuntimeTelemetrySummaryPayload,
  vehicleId: VehicleId,
  timestamp: string
): SensorState {
  const obstacleCount = Math.max(0, finite(payload.obstacleCount, 0));
  const sensorHealthy = payload.sensorHealthy !== false;

  const sensorState: SensorState = {
    vehicleId,
    lidarPoints: [],
    obstacles: [],
    occupancy: {
      width: 0,
      height: 0,
      resolution: 0,
      occupiedCellCount: obstacleCount
    },
    sensorHealth: {
      lidar: sensorHealthy ? "ok" : "warn",
      imu: sensorHealthy ? "ok" : "warn",
      gps: sensorHealthy ? "ok" : "warn",
      camera: "unknown"
    },
    freshness: {
      timestamp,
      ageMs: 0,
      isStale: payload.runtimeConnected === false,
      source: normalizeSourceKind(payload.sensorType ?? "runtime")
    }
  };

  return sensorState;
}

function normalizeMissionStatus(value?: string | null): MissionStatus {
  const normalized = (value ?? "idle").toLowerCase();

  if (
    normalized === "idle" ||
    normalized === "planning" ||
    normalized === "running" ||
    normalized === "paused" ||
    normalized === "completed" ||
    normalized === "aborted" ||
    normalized === "failed"
  ) {
    return normalized;
  }

  return "idle";
}

function normalizeThrusterDirection(command: number): ThrusterDirection {
  if (command > 0.001) {
    return "forward";
  }

  if (command < -0.001) {
    return "reverse";
  }

  return "neutral";
}

function normalizeFreshness(
  freshness: BackendFreshness | null | undefined,
  timestamp: string,
  source: SourceKind | string
) {
  return {
    timestamp: freshness?.timestamp ?? freshness?.timestampUtc ?? timestamp,
    ageMs: freshness?.ageMs ?? 0,
    isStale: freshness?.isStale ?? freshness?.isFresh === false,
    source: normalizeSourceKind(freshness?.source ?? source)
  };
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

function normalizeHealthState(value?: string, isAlive?: boolean): HealthState {
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

function normalizeSourceKind(value?: string): SourceKind {
  const normalized = (value ?? "").toLowerCase();

  if (normalized === "runtime") {
    return "runtime";
  }

  if (normalized === "external") {
    return "external";
  }

  if (normalized === "python") {
    return "python";
  }

  if (normalized === "twin") {
    return "twin";
  }

  if (normalized === "sim") {
    return "sim";
  }

  return "unknown";
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

function calculateDistance2d(
  x1: number,
  y1: number,
  x2: number,
  y2: number
) {
  const dx = x2 - x1;
  const dy = y2 - y1;
  return Math.sqrt(dx * dx + dy * dy);
}

function calculateHeadingErrorDeg(
  x: number,
  y: number,
  targetX: number,
  targetY: number,
  yawDeg: number
) {
  const desiredDeg = (Math.atan2(targetY - y, targetX - x) * 180) / Math.PI;
  let error = desiredDeg - yawDeg;

  while (error > 180) error -= 360;
  while (error < -180) error += 360;

  return error;
}

function booleanFromString(value?: string) {
  if (!value) {
    return false;
  }

  return value.toLowerCase() === "true";
}

function readNumber(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function finite(value: number | undefined | null, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function firstFinite(...values: Array<number | undefined | null>) {
  for (const value of values) {
    if (typeof value === "number" && Number.isFinite(value)) {
      return value;
    }
  }

  return 0;
}