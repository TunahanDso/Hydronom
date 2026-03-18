import type { FreshnessInfo, HealthState, Vec2, VehicleId, SourceKind } from "../../../shared/types/common.types";

// LiDAR noktası
export interface LidarPoint {
  x: number;
  y: number;
  intensity?: number;
}

// Runtime obstacle modeli
export interface RuntimeObstacle {
  id: string;
  position: Vec2;
  radius: number;
  source: string;
}

// Ortak sensör metadata modeli
export interface SensorMetaInfo {
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
}

// Occupancy grid kısa modeli
export interface OccupancyGridInfo extends SensorMetaInfo {
  width: number;
  height: number;
  resolution: number;
  occupiedCellCount: number;
}

// Kamera özeti
export interface CameraInfo extends SensorMetaInfo {
  imageUrl?: string | null;
}

// Sensör sağlık özeti
export interface SensorHealthSummary {
  lidar: HealthState;
  imu: HealthState;
  gps: HealthState;
  camera: HealthState;
}

// Genişletilmiş freshness modeli
export interface SensorFreshnessInfo extends FreshnessInfo {
  cameraMs?: number | null;
  lidarMs?: number | null;
  occupancyMs?: number | null;
  imuMs?: number | null;
  gpsMs?: number | null;
}

// Sensör birleşik görünüm modeli
export interface SensorState {
  vehicleId: VehicleId;

  lidarPoints: LidarPoint[];
  obstacles: RuntimeObstacle[];
  occupancy: OccupancyGridInfo | null;

  sensorHealth: SensorHealthSummary;
  freshness: SensorFreshnessInfo;

  // Sayfa seviyesi özet alanları
  status?: string;

  // Sensör bazlı metadata alanları
  camera?: CameraInfo;
  lidar?: SensorMetaInfo;
  imu?: SensorMetaInfo;
  gps?: SensorMetaInfo;

  // Esnek UI fallback alanları
  cameraFrameUrl?: string | null;
  cameraUrl?: string | null;
  cameraTimestampUtc?: string | null;
  lidarTimestampUtc?: string | null;
  occupancyTimestampUtc?: string | null;
  lastLidarTimestampUtc?: string | null;
  lastOccupancyTimestampUtc?: string | null;
}