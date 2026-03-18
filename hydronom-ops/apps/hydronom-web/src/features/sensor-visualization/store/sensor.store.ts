import { create } from "zustand";
import type { SensorState } from "../../../entities/sensor/model/sensor.types";
import type { VehicleId, SourceKind } from "../../../shared/types/common.types";

interface GatewaySensorStateDto {
  timestampUtc: string;
  vehicleId: VehicleId;
  sensorName: string;
  sensorType: string;
  source?: string | null;
  backend?: string | null;
  isSimulated: boolean;
  isEnabled: boolean;
  isHealthy: boolean;
  configuredRateHz?: number | null;
  effectiveRateHz?: number | null;
  lastSampleUtc?: string | null;
  lastError?: string | null;
  metrics: Record<string, number>;
  fields: Record<string, string>;
  freshness?: {
    timestamp: string;
    ageMs: number;
    isStale?: boolean;
    isFresh?: boolean;
    source?: string;
  } | null;
}

type ExtendedSensorState = SensorState & {
  status?: string;
  camera?: {
    timestampUtc?: string | null;
    ageMs?: number | null;
    imageUrl?: string | null;
    backend?: string | null;
    source?: SourceKind;
    isSimulated?: boolean;
    isEnabled?: boolean;
    isHealthy?: boolean;
    configuredRateHz?: number | null;
    effectiveRateHz?: number | null;
    lastSampleUtc?: string | null;
    lastError?: string | null;
    metrics?: Record<string, number>;
    fields?: Record<string, string>;
  };
  lidar?: {
    timestampUtc?: string | null;
    ageMs?: number | null;
    backend?: string | null;
    source?: SourceKind;
    isSimulated?: boolean;
    isEnabled?: boolean;
    isHealthy?: boolean;
    configuredRateHz?: number | null;
    effectiveRateHz?: number | null;
    lastSampleUtc?: string | null;
    lastError?: string | null;
    metrics?: Record<string, number>;
    fields?: Record<string, string>;
  };
  imu?: {
    timestampUtc?: string | null;
    ageMs?: number | null;
    backend?: string | null;
    source?: SourceKind;
    isSimulated?: boolean;
    isEnabled?: boolean;
    isHealthy?: boolean;
    configuredRateHz?: number | null;
    effectiveRateHz?: number | null;
    lastSampleUtc?: string | null;
    lastError?: string | null;
    metrics?: Record<string, number>;
    fields?: Record<string, string>;
  };
  gps?: {
    timestampUtc?: string | null;
    ageMs?: number | null;
    backend?: string | null;
    source?: SourceKind;
    isSimulated?: boolean;
    isEnabled?: boolean;
    isHealthy?: boolean;
    configuredRateHz?: number | null;
    effectiveRateHz?: number | null;
    lastSampleUtc?: string | null;
    lastError?: string | null;
    metrics?: Record<string, number>;
    fields?: Record<string, string>;
  };
  occupancy?: SensorState["occupancy"] & {
    timestampUtc?: string | null;
    ageMs?: number | null;
    backend?: string | null;
    source?: SourceKind;
    isSimulated?: boolean;
    isEnabled?: boolean;
    isHealthy?: boolean;
    configuredRateHz?: number | null;
    effectiveRateHz?: number | null;
    lastSampleUtc?: string | null;
    lastError?: string | null;
    metrics?: Record<string, number>;
    fields?: Record<string, string>;
  };
  cameraFrameUrl?: string | null;
  cameraUrl?: string | null;
  cameraTimestampUtc?: string | null;
  lidarTimestampUtc?: string | null;
  occupancyTimestampUtc?: string | null;
  lastLidarTimestampUtc?: string | null;
  lastOccupancyTimestampUtc?: string | null;
  freshness?: SensorState["freshness"] & {
    cameraMs?: number | null;
    lidarMs?: number | null;
    occupancyMs?: number | null;
    imuMs?: number | null;
    gpsMs?: number | null;
  };
};

interface SensorStore {
  sensorByVehicleId: Record<VehicleId, ExtendedSensorState>;
  upsertSensorState: (state: SensorState | GatewaySensorStateDto) => void;
}

// Başlangıç için lidar ve obstacle görünümü sağlayan mock sensör verisi
const initialSensorState: ExtendedSensorState = {
  vehicleId: "HYD-01",
  lidarPoints: [
    { x: 43.2, y: -0.4 },
    { x: 43.8, y: -0.2 },
    { x: 44.4, y: 0.1 },
    { x: 45.1, y: 0.5 },
    { x: 45.9, y: 1.1 },
    { x: 46.5, y: 1.7 }
  ],
  obstacles: [
    {
      id: "obs-1",
      position: { x: 44.8, y: 0.3 },
      radius: 0.6,
      source: "lidar_runtime_obstacles"
    },
    {
      id: "obs-2",
      position: { x: 46.2, y: 1.5 },
      radius: 0.8,
      source: "occupancy_grid"
    }
  ],
  occupancy: {
    width: 80,
    height: 80,
    resolution: 0.25,
    occupiedCellCount: 126,
    timestampUtc: new Date().toISOString(),
    ageMs: 74,
    backend: "mock",
    source: "python",
    isSimulated: true,
    isEnabled: true,
    isHealthy: true,
    configuredRateHz: 4,
    effectiveRateHz: 4,
    lastSampleUtc: new Date().toISOString(),
    lastError: null,
    metrics: {},
    fields: {}
  },
  sensorHealth: {
    lidar: "ok",
    imu: "ok",
    gps: "ok",
    camera: "warn"
  },
  freshness: {
    timestamp: new Date().toISOString(),
    ageMs: 74,
    isStale: false,
    source: "python",
    lidarMs: 74,
    occupancyMs: 74,
    imuMs: 95,
    gpsMs: 110,
    cameraMs: null
  },
  status: "active",
  camera: {
    timestampUtc: null,
    ageMs: null,
    imageUrl: null,
    backend: "mock",
    source: "runtime",
    isSimulated: true,
    isEnabled: false,
    isHealthy: false,
    configuredRateHz: null,
    effectiveRateHz: null,
    lastSampleUtc: null,
    lastError: null,
    metrics: {},
    fields: {}
  },
  lidar: {
    timestampUtc: new Date().toISOString(),
    ageMs: 74,
    backend: "mock",
    source: "python",
    isSimulated: true,
    isEnabled: true,
    isHealthy: true,
    configuredRateHz: 10,
    effectiveRateHz: 10,
    lastSampleUtc: new Date().toISOString(),
    lastError: null,
    metrics: {
      pointCount: 6
    },
    fields: {}
  },
  imu: {
    timestampUtc: new Date().toISOString(),
    ageMs: 95,
    backend: "mock",
    source: "python",
    isSimulated: true,
    isEnabled: true,
    isHealthy: true,
    configuredRateHz: 50,
    effectiveRateHz: 50,
    lastSampleUtc: new Date().toISOString(),
    lastError: null,
    metrics: {},
    fields: {}
  },
  gps: {
    timestampUtc: new Date().toISOString(),
    ageMs: 110,
    backend: "mock",
    source: "python",
    isSimulated: true,
    isEnabled: true,
    isHealthy: true,
    configuredRateHz: 5,
    effectiveRateHz: 5,
    lastSampleUtc: new Date().toISOString(),
    lastError: null,
    metrics: {},
    fields: {}
  },
  cameraFrameUrl: null,
  cameraUrl: null,
  cameraTimestampUtc: null,
  lidarTimestampUtc: new Date().toISOString(),
  occupancyTimestampUtc: new Date().toISOString(),
  lastLidarTimestampUtc: new Date().toISOString(),
  lastOccupancyTimestampUtc: new Date().toISOString()
};

function isGatewaySensorStateDto(
  sensorState: SensorState | GatewaySensorStateDto
): sensorState is GatewaySensorStateDto {
  return (
    "timestampUtc" in sensorState &&
    "sensorName" in sensorState &&
    "sensorType" in sensorState
  );
}

function normalizeSourceKind(value?: string | null): SourceKind {
  switch ((value ?? "").toLowerCase()) {
    case "python":
      return "python";
    case "runtime":
      return "runtime";
    default:
      return "runtime";
  }
}

function buildFreshness(dto: GatewaySensorStateDto) {
  const source = normalizeSourceKind(dto.freshness?.source ?? dto.source);

  return dto.freshness
    ? {
        timestamp: dto.freshness.timestamp,
        ageMs: dto.freshness.ageMs,
        isStale:
          dto.freshness.isStale ??
          (dto.freshness.isFresh !== undefined ? !dto.freshness.isFresh : false),
        source
      }
    : {
        timestamp: dto.timestampUtc,
        ageMs: 0,
        isStale: false,
        source
      };
}

function createEmptySensorState(vehicleId: VehicleId): ExtendedSensorState {
  return {
    vehicleId,
    lidarPoints: [],
    obstacles: [],
    occupancy: {
      width: 0,
      height: 0,
      resolution: 0,
      occupiedCellCount: 0,
      timestampUtc: null,
      ageMs: null,
      backend: null,
      source: "runtime",
      isSimulated: false,
      isEnabled: false,
      isHealthy: false,
      configuredRateHz: null,
      effectiveRateHz: null,
      lastSampleUtc: null,
      lastError: null,
      metrics: {},
      fields: {}
    },
    sensorHealth: {
      lidar: "warn",
      imu: "warn",
      gps: "warn",
      camera: "warn"
    },
    freshness: {
      timestamp: new Date().toISOString(),
      ageMs: 0,
      isStale: false,
      source: "runtime",
      lidarMs: null,
      occupancyMs: null,
      imuMs: null,
      gpsMs: null,
      cameraMs: null
    },
    status: "waiting",
    camera: {
      timestampUtc: null,
      ageMs: null,
      imageUrl: null,
      backend: null,
      source: "runtime",
      isSimulated: false,
      isEnabled: false,
      isHealthy: false,
      configuredRateHz: null,
      effectiveRateHz: null,
      lastSampleUtc: null,
      lastError: null,
      metrics: {},
      fields: {}
    },
    lidar: {
      timestampUtc: null,
      ageMs: null,
      backend: null,
      source: "runtime",
      isSimulated: false,
      isEnabled: false,
      isHealthy: false,
      configuredRateHz: null,
      effectiveRateHz: null,
      lastSampleUtc: null,
      lastError: null,
      metrics: {},
      fields: {}
    },
    imu: {
      timestampUtc: null,
      ageMs: null,
      backend: null,
      source: "runtime",
      isSimulated: false,
      isEnabled: false,
      isHealthy: false,
      configuredRateHz: null,
      effectiveRateHz: null,
      lastSampleUtc: null,
      lastError: null,
      metrics: {},
      fields: {}
    },
    gps: {
      timestampUtc: null,
      ageMs: null,
      backend: null,
      source: "runtime",
      isSimulated: false,
      isEnabled: false,
      isHealthy: false,
      configuredRateHz: null,
      effectiveRateHz: null,
      lastSampleUtc: null,
      lastError: null,
      metrics: {},
      fields: {}
    },
    cameraFrameUrl: null,
    cameraUrl: null,
    cameraTimestampUtc: null,
    lidarTimestampUtc: null,
    occupancyTimestampUtc: null,
    lastLidarTimestampUtc: null,
    lastOccupancyTimestampUtc: null
  };
}

function toHealthState(dto: GatewaySensorStateDto): "ok" | "warn" | "error" {
  if (!dto.isEnabled) {
    return "error";
  }

  return dto.isHealthy ? "ok" : "warn";
}

function buildSensorMeta(dto: GatewaySensorStateDto) {
  const source = normalizeSourceKind(dto.freshness?.source ?? dto.source);
  const ageMs = dto.freshness?.ageMs ?? 0;

  return {
    timestampUtc: dto.timestampUtc,
    ageMs,
    backend: dto.backend ?? null,
    source,
    isSimulated: dto.isSimulated,
    isEnabled: dto.isEnabled,
    isHealthy: dto.isHealthy,
    configuredRateHz: dto.configuredRateHz ?? null,
    effectiveRateHz: dto.effectiveRateHz ?? null,
    lastSampleUtc: dto.lastSampleUtc ?? null,
    lastError: dto.lastError ?? null,
    metrics: dto.metrics ?? {},
    fields: dto.fields ?? {}
  };
}

function readNumber(value: string | undefined | null): number | null {
  if (value === undefined || value === null) {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function mapGatewaySensorStateToSensorState(
  dto: GatewaySensorStateDto,
  previous?: ExtendedSensorState
): ExtendedSensorState {
  const base = previous
    ? {
        ...previous,
        freshness: {
          ...previous.freshness,
          ...buildFreshness(dto)
        }
      }
    : {
        ...createEmptySensorState(dto.vehicleId),
        freshness: {
          ...createEmptySensorState(dto.vehicleId).freshness,
          ...buildFreshness(dto)
        }
      };

  const next: ExtendedSensorState = {
    ...base,
    status: dto.isEnabled ? (dto.isHealthy ? "active" : "warning") : "disabled"
  };

  const normalizedHealth = toHealthState(dto);
  const meta = buildSensorMeta(dto);
  const sensorType = dto.sensorType.toLowerCase();
  const sensorName = dto.sensorName.toLowerCase();

  if (sensorType === "lidar") {
    next.sensorHealth = {
      ...next.sensorHealth,
      lidar: normalizedHealth
    };

    next.lidar = {
      ...next.lidar,
      ...meta
    };

    next.freshness = {
      ...next.freshness,
      lidarMs: meta.ageMs
    };

    next.lidarTimestampUtc = dto.timestampUtc;
    next.lastLidarTimestampUtc = dto.lastSampleUtc ?? dto.timestampUtc;

    const metricPointCount = dto.metrics?.pointCount;
    if (typeof metricPointCount === "number" && metricPointCount >= 0) {
      next.lidar = {
        ...next.lidar,
        metrics: {
          ...next.lidar?.metrics,
          pointCount: metricPointCount
        }
      };
    }
  }

  if (sensorType === "imu" || sensorType === "twinimu") {
    next.sensorHealth = {
      ...next.sensorHealth,
      imu: normalizedHealth
    };

    next.imu = {
      ...next.imu,
      ...meta
    };

    next.freshness = {
      ...next.freshness,
      imuMs: meta.ageMs
    };
  }

  if (sensorType === "gps" || sensorType === "twingps") {
    next.sensorHealth = {
      ...next.sensorHealth,
      gps: normalizedHealth
    };

    next.gps = {
      ...next.gps,
      ...meta
    };

    next.freshness = {
      ...next.freshness,
      gpsMs: meta.ageMs
    };
  }

  if (sensorType === "camera") {
    next.sensorHealth = {
      ...next.sensorHealth,
      camera: normalizedHealth
    };

    const imageUrl =
      dto.fields?.imageUrl ??
      dto.fields?.frameUrl ??
      dto.fields?.cameraUrl ??
      dto.fields?.snapshotUrl ??
      null;

    next.camera = {
      ...next.camera,
      ...meta,
      imageUrl
    };

    next.cameraFrameUrl = imageUrl;
    next.cameraUrl = imageUrl;
    next.cameraTimestampUtc = dto.timestampUtc;

    next.freshness = {
      ...next.freshness,
      cameraMs: meta.ageMs
    };
  }

  if (
    sensorType === "occupancy" ||
    sensorType === "grid" ||
    sensorType === "ogm" ||
    sensorName.includes("occupancy") ||
    sensorName.includes("grid") ||
    sensorName.includes("ogm")
  ) {
    next.occupancy = {
      ...next.occupancy,
      width:
        dto.metrics?.width ??
        readNumber(dto.fields?.width) ??
        next.occupancy?.width ??
        0,
      height:
        dto.metrics?.height ??
        readNumber(dto.fields?.height) ??
        next.occupancy?.height ??
        0,
      resolution:
        dto.metrics?.resolution ??
        readNumber(dto.fields?.resolution) ??
        next.occupancy?.resolution ??
        0,
      occupiedCellCount:
        dto.metrics?.occupiedCellCount ??
        dto.metrics?.cellCount ??
        readNumber(dto.fields?.occupiedCellCount) ??
        readNumber(dto.fields?.cellCount) ??
        next.occupancy?.occupiedCellCount ??
        0,
      ...meta
    };

    next.occupancyTimestampUtc = dto.timestampUtc;
    next.lastOccupancyTimestampUtc = dto.lastSampleUtc ?? dto.timestampUtc;

    next.freshness = {
      ...next.freshness,
      occupancyMs: meta.ageMs
    };
  }

  return next;
}

export const useSensorStore = create<SensorStore>((set) => ({
  sensorByVehicleId: {
    [initialSensorState.vehicleId]: initialSensorState
  },

  upsertSensorState: (sensorState) =>
    set((state) => {
      if (isGatewaySensorStateDto(sensorState)) {
        const previous = state.sensorByVehicleId[sensorState.vehicleId];

        return {
          sensorByVehicleId: {
            ...state.sensorByVehicleId,
            [sensorState.vehicleId]: mapGatewaySensorStateToSensorState(
              sensorState,
              previous
            )
          }
        };
      }

      const previous = state.sensorByVehicleId[sensorState.vehicleId];

      return {
        sensorByVehicleId: {
          ...state.sensorByVehicleId,
          [sensorState.vehicleId]: {
            ...previous,
            ...sensorState,
            lidarPoints: sensorState.lidarPoints ?? previous?.lidarPoints ?? [],
            obstacles: sensorState.obstacles ?? previous?.obstacles ?? [],
            occupancy: sensorState.occupancy ?? previous?.occupancy ?? null,
            sensorHealth: {
              ...previous?.sensorHealth,
              ...sensorState.sensorHealth
            },
            freshness: {
              ...previous?.freshness,
              ...sensorState.freshness
            }
          }
        }
      };
    })
}));