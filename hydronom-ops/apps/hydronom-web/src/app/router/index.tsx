import { lazy, Suspense } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";

import { AppShell } from "../layout/AppShell";
import { MissionControlPage } from "../../pages/mission-control/MissionControlPage";
import { SensorsPage } from "../../pages/sensors/SensorsPage";
import { ActuationPage } from "../../pages/actuation/ActuationPage";
import { MissionPage } from "../../pages/mission/MissionPage";
import { DiagnosticsPage } from "../../pages/diagnostics/DiagnosticsPage";
import { FleetPage } from "../../pages/fleet/FleetPage";
import { SettingsPage } from "../../pages/settings/SettingsPage";
import { MissionMapCanvasPage } from "../../pages/mission-map-canvas/MissionMapCanvasPage";

const Tactical3DPage = lazy(() =>
  import("../../pages/tactical-3d/Tactical3DPage").then((module) => ({
    default: module.Tactical3DPage
  }))
);

// Uygulamanın tüm sayfa yönlendirmelerini burada tanımlıyoruz.
export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<Navigate to="/mission-control" replace />} />
          <Route path="/mission-control" element={<MissionControlPage />} />
          <Route path="/mission-map-canvas" element={<MissionMapCanvasPage />} />
          <Route
            path="/tactical-3d"
            element={
              <Suspense fallback={<RouteLoadingFallback />}>
                <Tactical3DPage />
              </Suspense>
            }
          />
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

function RouteLoadingFallback() {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6 shadow-panel">
      <div className="flex h-[520px] items-center justify-center rounded-3xl border border-dashed border-slate-700 bg-slate-950/60">
        <div className="text-center">
          <div className="text-sm font-semibold uppercase tracking-[0.24em] text-cyan-300">
            Hydronom Ops
          </div>
          <div className="mt-3 text-2xl font-bold text-slate-100">
            3D Tactical Mission View yükleniyor...
          </div>
          <p className="mt-2 text-sm text-slate-500">
            Three.js sahne modülü ayrı paket olarak hazırlanıyor.
          </p>
        </div>
      </div>
    </section>
  );
}