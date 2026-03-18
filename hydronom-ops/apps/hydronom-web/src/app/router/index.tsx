import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "../layout/AppShell";
import { MissionControlPage } from "../../pages/mission-control/MissionControlPage";
import { Tactical3DPage } from "../../pages/tactical-3d/Tactical3DPage";
import { SensorsPage } from "../../pages/sensors/SensorsPage";
import { ActuationPage } from "../../pages/actuation/ActuationPage";
import { MissionPage } from "../../pages/mission/MissionPage";
import { DiagnosticsPage } from "../../pages/diagnostics/DiagnosticsPage";
import { FleetPage } from "../../pages/fleet/FleetPage";
import { SettingsPage } from "../../pages/settings/SettingsPage";
import { MissionMapCanvasPage } from "../../pages/mission-map-canvas/MissionMapCanvasPage";

// Uygulamanın tüm sayfa yönlendirmelerini burada tanımlıyoruz
export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<Navigate to="/mission-control" replace />} />
          <Route path="/mission-control" element={<MissionControlPage />} />
          <Route path="/mission-map-canvas" element={<MissionMapCanvasPage />} />
          <Route path="/tactical-3d" element={<Tactical3DPage />} />
          <Route path="/sensors" element={<SensorsPage />} />
          <Route path="/actuation" element={<ActuationPage />} />
          <Route path="/mission" element={<MissionPage />} />
          <Route path="/diagnostics" element={<DiagnosticsPage />} />
          <Route path="/fleet" element={<FleetPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}