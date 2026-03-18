import { create } from "zustand";
import type { MissionState } from "../../../entities/mission/model/mission.types";
import type { VehicleId } from "../../../shared/types/common.types";

interface MissionStore {
  missionByVehicleId: Record<VehicleId, MissionState>;
  upsertMissionState: (mission: MissionState) => void;
}

// Başlangıç için mock görev durumu
const initialMission: MissionState = {
  vehicleId: "HYD-01",
  missionId: "mission-alpha",
  missionName: "Harbor Scan Alpha",
  status: "running",
  activeStepId: "step-2",
  progressPercent: 46,
  goalPosition: { x: 48.0, y: 3.2 },
  route: [
    { x: 40.0, y: -2.0 },
    { x: 42.0, y: -1.0 },
    { x: 45.0, y: 1.2 },
    { x: 48.0, y: 3.2 }
  ],
  waypoints: [
    { id: "wp-1", label: "WP-1", position: { x: 42.0, y: -1.0 }, reached: true },
    { id: "wp-2", label: "WP-2", position: { x: 45.0, y: 1.2 }, reached: false },
    { id: "wp-3", label: "Goal", position: { x: 48.0, y: 3.2 }, reached: false }
  ],
  steps: [
    {
      id: "step-1",
      title: "Launch & Stabilize",
      description: "Araç su üstünde başlangıç stabilitesini kuruyor.",
      status: "completed",
      order: 1
    },
    {
      id: "step-2",
      title: "Approach Scan Route",
      description: "Tanımlı tarama rotasına ilerleniyor.",
      status: "active",
      order: 2
    },
    {
      id: "step-3",
      title: "Obstacle-Aware Traverse",
      description: "Obstacle etkileriyle rota takibi yapılıyor.",
      status: "pending",
      order: 3
    }
  ],
  recentEvents: [
    {
      id: "evt-1",
      timestamp: new Date().toISOString(),
      level: "info",
      message: "Mission route loaded successfully."
    },
    {
      id: "evt-2",
      timestamp: new Date().toISOString(),
      level: "warn",
      message: "obsAhead became true from lidar runtime obstacle layer."
    }
  ],
  freshness: {
    timestamp: new Date().toISOString(),
    ageMs: 65,
    isStale: false,
    source: "runtime"
  }
};

export const useMissionStore = create<MissionStore>((set) => ({
  missionByVehicleId: {
    [initialMission.vehicleId]: initialMission
  },

  upsertMissionState: (mission) =>
    set((state) => {
      const previous = state.missionByVehicleId[mission.vehicleId];

      return {
        missionByVehicleId: {
          ...state.missionByVehicleId,
          [mission.vehicleId]: {
            ...previous,
            ...mission,
            goalPosition: mission.goalPosition ?? previous?.goalPosition,
            route: mission.route ?? previous?.route ?? [],
            waypoints: mission.waypoints ?? previous?.waypoints ?? [],
            steps: mission.steps ?? previous?.steps ?? [],
            recentEvents: mission.recentEvents ?? previous?.recentEvents ?? [],
            freshness: {
              ...previous?.freshness,
              ...mission.freshness
            }
          }
        }
      };
    })
}));