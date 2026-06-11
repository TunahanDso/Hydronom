import type {
  FreshnessInfo,
  HealthState,
  Rpy,
  StatusFlag,
  Vec2,
  Vec3,
  VehicleId
} from "../../../shared/types/common.types";

// AraÃƒÂ§ modu tipleri
export type VehicleMode =
  | "unknown"
  | "manual"
  | "stabilize"
  | "hold"
  | "mission"
  | "return"
  | "failsafe";

// AraÃƒÂ§ arm durumu
export type ArmState = "armed" | "disarmed";

// Runtime obstacle ÃƒÂ¶zeti
export interface VehicleObstacle {
  x: number;
  y: number;
  r: number;
}

// Landmark nokta verisi
export interface VehicleLandmarkPoint {
  x: number;
  y: number;
}

// Gateway landmark ÃƒÂ¶zeti
export interface VehicleLandmark {
  id: string;
  type: string;
  shape: string;
  points: VehicleLandmarkPoint[];
}

export interface VehicleProfileCapabilities {
  hasThrusters: boolean;
  hasReverseAuthority: boolean;
  canGenerateLateralForce: boolean;
  canGenerateYawMoment: boolean;
}

export interface VehicleProfileInfo {
  profileId: string | null;
  platformKind: string | null;
  displayName: string | null;
  active: boolean;
  isUnderwater: boolean;
  isMiniRov: boolean;
  capabilitySummary: string | null;
  capabilities: VehicleProfileCapabilities;
}

// Gateway'den gelen dÃƒÂ¼z telemetri payload'Ã„Â±
export interface GatewayVehicleTelemetryDto {
  timestampUtc: string;
  vehicleId: VehicleId;
  vehicleProfileId?: string | null;
  vehiclePlatformKind?: string | null;
  vehicleDisplayName?: string | null;
  vehicleProfileActive?: boolean;
  vehicleIsUnderwater?: boolean;
  vehicleIsMiniRov?: boolean;
  vehicleProfile?: VehicleProfileInfo | null;
  x: number;
  y: number;
  z: number;
  rollDeg: number;
  pitchDeg: number;
  yawDeg: number;
  headingDeg: number;
  vx: number;
  vy: number;
  vz: number;
  rollRateDeg: number;
  pitchRateDeg: number;
  yawRateDeg: number;
  targetX: number | null;
  targetY: number | null;
  distanceToGoalM: number | null;
  headingErrorDeg: number | null;
  obstacleAhead: boolean;
  obstacleCount: number;
  obstacles?: VehicleObstacle[];
  landmarks?: VehicleLandmark[];
  metrics: Record<string, number>;
  fields: Record<string, string>;
  freshness: FreshnessInfo | null;
}

// AraÃƒÂ§ baÃ„Å¸lantÃ„Â± profili
export interface VehicleConnectionInfo {
  runtimeConnected: boolean;
  gatewayConnected: boolean;
  pythonConnected: boolean;
  twinActive: boolean;
}

// AraÃƒÂ§ health ÃƒÂ¶zeti
export interface VehicleHealthInfo {
  overall: HealthState;
  sensors: HealthState;
  actuators: HealthState;
  navigation: HealthState;
  autonomy: HealthState;
}

// AraÃƒÂ§ pose bilgisi
export interface VehiclePose {
  position: Vec3;
  orientation: Rpy;
}

// AraÃƒÂ§ hareket bilgisi
export interface VehicleMotion {
  linearVelocity: Vec3;
  angularVelocity: Vec3;
  linearAcceleration: Vec3;
  speed: number;
}

// Harita odaklÃ„Â± gÃƒÂ¶rÃƒÂ¼nÃƒÂ¼m iÃƒÂ§in sade durum modeli
export interface VehicleMapState {
  worldPosition: Vec2;
  headingDeg: number;
  trail: Vec2[];
}

// Ana araÃƒÂ§ telemetri modeli
export interface VehicleTelemetry {
  vehicleId: VehicleId;
  displayName: string;
  mode: VehicleMode;
  armState: ArmState;
  vehicleProfile: VehicleProfileInfo | null;

  pose: VehiclePose;
  motion: VehicleMotion;
  map: VehicleMapState;

  // Gateway dÃƒÂ¼z alanlarÃ„Â±nÃ„Â± da entity iÃƒÂ§inde tutalÃ„Â±m
  x: number;
  y: number;
  z: number;
  rollDeg: number;
  pitchDeg: number;
  yawDeg: number;
  headingDeg: number;
  vx: number;
  vy: number;
  vz: number;
  rollRateDeg: number;
  pitchRateDeg: number;
  yawRateDeg: number;
  targetX: number | null;
  targetY: number | null;
  distanceToGoalM: number | null;
  headingErrorDeg: number | null;
  obstacleAhead: boolean;
  obstacleCount: number;
  obstacles: VehicleObstacle[];
  landmarks: VehicleLandmark[];

  freshness: FreshnessInfo;
  health: VehicleHealthInfo;
  connections: VehicleConnectionInfo;
  flags: StatusFlag[];

  // GatewayÃ¢â‚¬â„¢den gelen ham alanlarÃ„Â± da saklayalÃ„Â±m
  raw?: GatewayVehicleTelemetryDto;
}

function toFiniteNumber(value: number | null | undefined, fallback = 0) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function normalizeObstacles(input: VehicleObstacle[] | undefined): VehicleObstacle[] {
  if (!Array.isArray(input)) return [];

  return input
    .filter(
      (item) =>
        item &&
        typeof item.x === "number" &&
        Number.isFinite(item.x) &&
        typeof item.y === "number" &&
        Number.isFinite(item.y) &&
        typeof item.r === "number" &&
        Number.isFinite(item.r)
    )
    .map((item) => ({
      x: item.x,
      y: item.y,
      r: item.r
    }));
}

function normalizeLandmarks(input: VehicleLandmark[] | undefined): VehicleLandmark[] {
  if (!Array.isArray(input)) return [];

  return input.map((landmark) => ({
    id: landmark?.id ?? "",
    type: landmark?.type ?? "",
    shape: landmark?.shape ?? "",
    points: Array.isArray(landmark?.points)
      ? landmark.points
          .filter(
            (p) =>
              p &&
              typeof p.x === "number" &&
              Number.isFinite(p.x) &&
              typeof p.y === "number" &&
              Number.isFinite(p.y)
          )
          .map((p) => ({
            x: p.x,
            y: p.y
          }))
      : []
  }));
}
function normalizeVehicleProfileInfo(
  dto: Partial<GatewayVehicleTelemetryDto>,
  fallbackDisplayName: string
): VehicleProfileInfo | null {
  const raw = dto.vehicleProfile;

  const profileId = raw?.profileId ?? dto.vehicleProfileId ?? null;
  const platformKind = raw?.platformKind ?? dto.vehiclePlatformKind ?? null;
  const displayName = raw?.displayName ?? dto.vehicleDisplayName ?? fallbackDisplayName;

  const active = raw?.active ?? Boolean(dto.vehicleProfileActive);
  const isUnderwater = raw?.isUnderwater ?? Boolean(dto.vehicleIsUnderwater);
  const isMiniRov = raw?.isMiniRov ?? Boolean(dto.vehicleIsMiniRov);

  const capabilitySummary = raw?.capabilitySummary ?? null;

  const capabilities = {
    hasThrusters: Boolean(raw?.capabilities?.hasThrusters),
    hasReverseAuthority: Boolean(raw?.capabilities?.hasReverseAuthority),
    canGenerateLateralForce: Boolean(raw?.capabilities?.canGenerateLateralForce),
    canGenerateYawMoment: Boolean(raw?.capabilities?.canGenerateYawMoment)
  };

  if (
    !profileId &&
    !platformKind &&
    !dto.vehicleProfileId &&
    !dto.vehiclePlatformKind &&
    !dto.vehicleProfileActive &&
    !dto.vehicleIsUnderwater &&
    !dto.vehicleIsMiniRov &&
    !raw
  ) {
    return null;
  }

  return {
    profileId,
    platformKind,
    displayName,
    active,
    isUnderwater,
    isMiniRov,
    capabilitySummary,
    capabilities
  };
}


// Gateway DTO Ã¢â€ â€™ frontend entity dÃƒÂ¶nÃƒÂ¼Ã…Å¸ÃƒÂ¼mÃƒÂ¼
export const mapGatewayVehicleTelemetryToVehicleTelemetry = (
  dto: GatewayVehicleTelemetryDto
): VehicleTelemetry => {
  const x = toFiniteNumber(dto.x);
  const y = toFiniteNumber(dto.y);
  const z = toFiniteNumber(dto.z);

  const rollDeg = toFiniteNumber(dto.rollDeg);
  const pitchDeg = toFiniteNumber(dto.pitchDeg);
  const yawDeg = toFiniteNumber(dto.yawDeg);
  const headingDeg = toFiniteNumber(dto.headingDeg, yawDeg);

  const vx = toFiniteNumber(dto.vx);
  const vy = toFiniteNumber(dto.vy);
  const vz = toFiniteNumber(dto.vz);

  const rollRateDeg = toFiniteNumber(dto.rollRateDeg);
  const pitchRateDeg = toFiniteNumber(dto.pitchRateDeg);
  const yawRateDeg = toFiniteNumber(dto.yawRateDeg);

  const obstacleAhead = Boolean(dto.obstacleAhead);
  const obstacleCount = toFiniteNumber(dto.obstacleCount);
  const obstacles = normalizeObstacles(dto.obstacles);
  const landmarks = normalizeLandmarks(dto.landmarks);

  const vehicleProfile = normalizeVehicleProfileInfo(dto, dto.vehicleId);
  const resolvedDisplayName = vehicleProfile?.displayName ?? dto.vehicleId;

  const speed = Math.sqrt(vx * vx + vy * vy + vz * vz);

  const navigationHealth: HealthState = obstacleAhead ? "warn" : "ok";
  const overallHealth: HealthState = obstacleAhead ? "warn" : "ok";

  const flags: StatusFlag[] = [
    {
      key: "obsAhead",
      label: "Obstacle Ahead",
      value: obstacleAhead
    },
    {
      key: "obstacleCount",
      label: "Obstacle Count",
      value: obstacleCount
    }
  ];

  if (dto.distanceToGoalM !== null) {
    flags.push({
      key: "distanceToGoalM",
      label: "Distance To Goal",
      value: dto.distanceToGoalM
    });
  }

  if (dto.headingErrorDeg !== null) {
    flags.push({
      key: "headingErrorDeg",
      label: "Heading Error",
      value: dto.headingErrorDeg
    });
  }

  if (dto.targetX !== null && dto.targetY !== null) {
    flags.push({
      key: "target",
      label: "Target",
      value: `(${dto.targetX}, ${dto.targetY})`
    });
  }

  return {
    vehicleId: dto.vehicleId,
    displayName: resolvedDisplayName,
    mode: "unknown",
    armState: "disarmed",
    vehicleProfile,

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
      },
      speed
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
    targetX: dto.targetX,
    targetY: dto.targetY,
    distanceToGoalM: dto.distanceToGoalM,
    headingErrorDeg: dto.headingErrorDeg,
    obstacleAhead,
    obstacleCount,
    obstacles,
    landmarks,

    freshness:
      dto.freshness ?? {
        timestamp: dto.timestampUtc,
        ageMs: 0,
        isStale: false,
        source: "runtime"
      },

    health: {
      overall: overallHealth,
      sensors: "ok",
      actuators: "ok",
      navigation: navigationHealth,
      autonomy: "ok"
    },

    connections: {
      runtimeConnected: true,
      gatewayConnected: true,
      pythonConnected: false,
      twinActive: true
    },

    flags,
    raw: dto
  };
};

// Ã„Â°steÃ„Å¸e baÃ„Å¸lÃ„Â±: baÃ…Å¸langÃ„Â±ÃƒÂ§ / boÃ…Å¸ telemetri ÃƒÂ¼retmek iÃƒÂ§in yardÃ„Â±mcÃ„Â± sabitler
export const createEmptyVehicleTelemetry = (
  vehicleId: VehicleId,
  displayName = "Unknown Vehicle"
): VehicleTelemetry => ({
  vehicleId,
  displayName,
  mode: "unknown",
  armState: "disarmed",
  vehicleProfile: null,

  pose: {
    position: { x: 0, y: 0, z: 0 },
    orientation: { roll: 0, pitch: 0, yaw: 0 }
  },

  motion: {
    linearVelocity: { x: 0, y: 0, z: 0 },
    angularVelocity: { x: 0, y: 0, z: 0 },
    linearAcceleration: { x: 0, y: 0, z: 0 },
    speed: 0
  },

  map: {
    worldPosition: { x: 0, y: 0 },
    headingDeg: 0,
    trail: []
  },

  x: 0,
  y: 0,
  z: 0,
  rollDeg: 0,
  pitchDeg: 0,
  yawDeg: 0,
  headingDeg: 0,
  vx: 0,
  vy: 0,
  vz: 0,
  rollRateDeg: 0,
  pitchRateDeg: 0,
  yawRateDeg: 0,
  targetX: null,
  targetY: null,
  distanceToGoalM: null,
  headingErrorDeg: null,
  obstacleAhead: false,
  obstacleCount: 0,
  obstacles: [],
  landmarks: [],

  freshness: {
    timestamp: new Date().toISOString(),
    ageMs: 0,
    isStale: true,
    source: "unknown"
  },

  health: {
    overall: "warn",
    sensors: "warn",
    actuators: "warn",
    navigation: "warn",
    autonomy: "warn"
  },

  connections: {
    runtimeConnected: false,
    gatewayConnected: false,
    pythonConnected: false,
    twinActive: false
  },

  flags: []
});