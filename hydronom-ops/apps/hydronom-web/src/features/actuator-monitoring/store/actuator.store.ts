import { create } from "zustand";
import type { ActuatorState } from "../../../entities/actuator/model/actuator.types";
import type { VehicleId } from "../../../shared/types/common.types";

interface ActuatorStore {
  actuatorByVehicleId: Record<VehicleId, ActuatorState>;
  upsertActuatorState: (state: ActuatorState) => void;
}

// Başlangıç için mock actuator verisi
const initialActuator: ActuatorState = {
  vehicleId: "HYD-01",
  thrusters: [
    {
      id: "FL",
      label: "Front Left",
      normalizedCommand: 0.34,
      appliedCommand: 0.32,
      direction: "forward",
      rpm: 1380,
      active: true
    },
    {
      id: "FR",
      label: "Front Right",
      normalizedCommand: 0.31,
      appliedCommand: 0.30,
      direction: "forward",
      rpm: 1325,
      active: true
    },
    {
      id: "RL",
      label: "Rear Left",
      normalizedCommand: 0.23,
      appliedCommand: 0.23,
      direction: "forward",
      rpm: 1100,
      active: true
    },
    {
      id: "RR",
      label: "Rear Right",
      normalizedCommand: 0.27,
      appliedCommand: 0.27,
      direction: "forward",
      rpm: 1180,
      active: true
    }
  ],
  wrench: {
    forceBody: { x: 11.04, y: -0.08, z: 0.0 },
    torqueBody: { x: 0.0, y: 0.0, z: 0.2 }
  },
  limiter: {
    satT: false,
    satR: false,
    rlT: false,
    rlR: false,
    dbT: true,
    dbR: true,
    assist: false,
    dt: false
  },
  freshness: {
    timestamp: new Date().toISOString(),
    ageMs: 28,
    isStale: false,
    source: "runtime"
  }
};

export const useActuatorStore = create<ActuatorStore>((set) => ({
  actuatorByVehicleId: {
    [initialActuator.vehicleId]: initialActuator
  },

  upsertActuatorState: (actuatorState) =>
    set((state) => {
      const previous = state.actuatorByVehicleId[actuatorState.vehicleId];

      return {
        actuatorByVehicleId: {
          ...state.actuatorByVehicleId,
          [actuatorState.vehicleId]: {
            ...previous,
            ...actuatorState,
            thrusters: actuatorState.thrusters ?? previous?.thrusters ?? [],
            wrench: {
              ...previous?.wrench,
              ...actuatorState.wrench,
              forceBody: {
                ...previous?.wrench?.forceBody,
                ...actuatorState.wrench?.forceBody
              },
              torqueBody: {
                ...previous?.wrench?.torqueBody,
                ...actuatorState.wrench?.torqueBody
              }
            },
            limiter: {
              ...previous?.limiter,
              ...actuatorState.limiter
            },
            freshness: {
              ...previous?.freshness,
              ...actuatorState.freshness
            }
          }
        }
      };
    })
}));