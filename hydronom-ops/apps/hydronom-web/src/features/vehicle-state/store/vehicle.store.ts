import { create } from "zustand";
import type {
  GatewayVehicleTelemetryDto,
  VehicleTelemetry
} from "../../../entities/vehicle/model/vehicle.types";
import { mapGatewayVehicleTelemetryToVehicleTelemetry } from "../../../entities/vehicle/model/vehicle.types";
import type { VehicleId } from "../../../shared/types/common.types";

interface VehicleStore {
  telemetryByVehicleId: Record<VehicleId, VehicleTelemetry>;
  upsertTelemetry: (
    telemetry: VehicleTelemetry | GatewayVehicleTelemetryDto
  ) => void;
}

function isGatewayVehicleTelemetryDto(
  telemetry: VehicleTelemetry | GatewayVehicleTelemetryDto
): telemetry is GatewayVehicleTelemetryDto {
  return (
    "timestampUtc" in telemetry &&
    "x" in telemetry &&
    "y" in telemetry &&
    "yawDeg" in telemetry
  );
}

function hasMeaningfulPositionChange(
  previous: { x: number; y: number } | undefined,
  next: { x: number; y: number } | undefined,
  epsilon = 0.001
) {
  if (!next) return false;
  if (!previous) return true;

  return (
    Math.abs(previous.x - next.x) > epsilon ||
    Math.abs(previous.y - next.y) > epsilon
  );
}

export const useVehicleStore = create<VehicleStore>((set) => ({
  telemetryByVehicleId: {},

  upsertTelemetry: (telemetry) =>
    set((state) => {
      const normalized = isGatewayVehicleTelemetryDto(telemetry)
        ? mapGatewayVehicleTelemetryToVehicleTelemetry(telemetry)
        : telemetry;

      const previous = state.telemetryByVehicleId[normalized.vehicleId];

      const nextWorldPosition =
        normalized.map?.worldPosition ?? previous?.map?.worldPosition;

      const previousTrail = previous?.map?.trail ?? [];
      const previousLastPoint =
        previousTrail.length > 0
          ? previousTrail[previousTrail.length - 1]
          : undefined;

      const shouldAppendTrail = hasMeaningfulPositionChange(
        previousLastPoint,
        nextWorldPosition
      );

      const nextTrail =
        shouldAppendTrail && nextWorldPosition
          ? [...previousTrail, nextWorldPosition].slice(-200)
          : previousTrail;

      return {
        telemetryByVehicleId: {
          ...state.telemetryByVehicleId,
          [normalized.vehicleId]: {
            ...previous,
            ...normalized,
            pose: {
              ...previous?.pose,
              ...normalized.pose,
              position: {
                ...previous?.pose?.position,
                ...normalized.pose?.position
              },
              orientation: {
                ...previous?.pose?.orientation,
                ...normalized.pose?.orientation
              }
            },
            motion: {
              ...previous?.motion,
              ...normalized.motion,
              linearVelocity: {
                ...previous?.motion?.linearVelocity,
                ...normalized.motion?.linearVelocity
              },
              angularVelocity: {
                ...previous?.motion?.angularVelocity,
                ...normalized.motion?.angularVelocity
              },
              linearAcceleration: {
                ...previous?.motion?.linearAcceleration,
                ...normalized.motion?.linearAcceleration
              }
            },
            map: {
              ...previous?.map,
              ...normalized.map,
              worldPosition: nextWorldPosition,
              trail: nextTrail
            },
            freshness: {
              ...previous?.freshness,
              ...normalized.freshness
            },
            health: {
              ...previous?.health,
              ...normalized.health
            },
            connections: {
              ...previous?.connections,
              ...normalized.connections
            },
            flags: normalized.flags ?? previous?.flags ?? []
          }
        }
      };
    })
}));