import { create } from "zustand";
import type { VehicleId } from "../../../shared/types/common.types";

interface FleetVehicleSummary {
  vehicleId: VehicleId;
  displayName: string;
  online: boolean;
  mode: string;
  armState: string;
}

interface FleetStore {
  selectedVehicleId: VehicleId;
  vehicles: FleetVehicleSummary[];
  setSelectedVehicleId: (vehicleId: VehicleId) => void;
}

// Çoklu araç desteği için temel seçici store
export const useFleetStore = create<FleetStore>((set) => ({
  selectedVehicleId: "hydronom-main",
  vehicles: [
    {
      vehicleId: "hydronom-main",
      displayName: "Hydronom Main",
      online: true,
      mode: "mission",
      armState: "armed"
    }
  ],
  setSelectedVehicleId: (vehicleId) => set({ selectedVehicleId: vehicleId })
}));