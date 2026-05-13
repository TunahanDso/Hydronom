import { create } from "zustand";
import type { VehicleId } from "../../../shared/types/common.types";
import type { WorldState } from "../../../entities/world/model/world.types";

interface WorldStore {
  worldByVehicleId: Record<VehicleId, WorldState>;
  upsertWorldState: (world: WorldState) => void;
  clearWorldState: (vehicleId: VehicleId) => void;
}

export const useWorldStore = create<WorldStore>((set) => ({
  worldByVehicleId: {},

  upsertWorldState: (world) =>
    set((state) => {
      const previous = state.worldByVehicleId[world.vehicleId];

      const mergedWorld: WorldState = {
        timestampUtc: world.timestampUtc ?? previous?.timestampUtc ?? new Date().toISOString(),
        vehicleId: world.vehicleId,
        source: world.source ?? previous?.source ?? "gateway",
        scenarioId: world.scenarioId ?? previous?.scenarioId ?? null,
        scenarioName: world.scenarioName ?? previous?.scenarioName ?? null,
        runId: world.runId ?? previous?.runId ?? null,
        currentObjectiveId: world.currentObjectiveId ?? previous?.currentObjectiveId ?? null,
        activeObjectiveTarget:
          world.activeObjectiveTarget ?? previous?.activeObjectiveTarget ?? null,
        route: world.route ?? previous?.route ?? [],
        objects: world.objects ?? previous?.objects ?? [],
        metrics: {
          ...(previous?.metrics ?? {}),
          ...(world.metrics ?? {})
        },
        fields: {
          ...(previous?.fields ?? {}),
          ...(world.fields ?? {})
        },
        freshness: world.freshness ?? previous?.freshness ?? null
      };

      return {
        worldByVehicleId: {
          ...state.worldByVehicleId,
          [world.vehicleId]: mergedWorld
        }
      };
    }),

  clearWorldState: (vehicleId) =>
    set((state) => {
      const next = { ...state.worldByVehicleId };
      delete next[vehicleId];

      return {
        worldByVehicleId: next
      };
    })
}));