import { Outlet, NavLink } from "react-router-dom";
import {
  Activity,
  Compass,
  Radar,
  Route,
  Wrench,
  Bug,
  Ship,
  Settings,
  Map
} from "lucide-react";
import { useFleetStore } from "../../features/fleet-management/store/fleet.store";
import { useVehicleStore } from "../../features/vehicle-state/store/vehicle.store";
import { useGatewayConnectionStore } from "../../features/gateway/store/gateway-connection.store";

const navItems = [
  { to: "/mission-control", label: "Mission Control", icon: Compass },
  { to: "/mission-map-canvas", label: "Map Canvas", icon: Map },
  { to: "/tactical-3d", label: "3D Tactical", icon: Ship },
  { to: "/sensors", label: "Sensors", icon: Radar },
  { to: "/actuation", label: "Actuation", icon: Wrench },
  { to: "/mission", label: "Mission", icon: Route },
  { to: "/diagnostics", label: "Diagnostics", icon: Bug },
  { to: "/fleet", label: "Fleet", icon: Activity },
  { to: "/settings", label: "Settings", icon: Settings }
];

// Uygulamanın genel çerçevesi, menüsü ve üst durum satırı burada yer alır
export function AppShell() {
  const selectedVehicleId = useFleetStore((state) => state.selectedVehicleId);
  const telemetry = useVehicleStore(
    (state) => state.telemetryByVehicleId[selectedVehicleId]
  );

  const gatewayStatus = useGatewayConnectionStore((state) => state.status);
  const gatewayIsConnected = useGatewayConnectionStore(
    (state) => state.isConnected
  );
  const gatewayMode = useGatewayConnectionStore((state) => state.mode);
  const gatewayLastError = useGatewayConnectionStore((state) => state.lastError);

  const connectionText = gatewayIsConnected
    ? gatewayMode === "mock"
      ? "Mock gateway bağlı ve veri akışı hazır"
      : "Gateway bağlı ve veri akışı hazır"
    : gatewayLastError
      ? `Gateway hatası: ${gatewayLastError}`
      : gatewayStatus === "connecting"
        ? "Gateway bağlantısı kuruluyor"
        : gatewayStatus === "reconnecting"
          ? "Gateway yeniden bağlanıyor"
          : "Gateway henüz bağlı değil";

  const connectionDotClass = gatewayIsConnected
    ? "bg-emerald-400"
    : gatewayStatus === "connecting" || gatewayStatus === "reconnecting"
      ? "bg-sky-400"
      : gatewayStatus === "error"
        ? "bg-rose-400"
        : "bg-amber-400";

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="flex min-h-screen">
        <aside className="hidden w-72 border-r border-slate-800 bg-slate-900/70 backdrop-blur xl:flex xl:flex-col">
          <div className="border-b border-slate-800 px-6 py-5">
            <div className="text-xs font-semibold uppercase tracking-[0.3em] text-sky-400">
              Tunix
            </div>
            <div className="mt-2 text-2xl font-bold">Hydronom Ops</div>
            <div className="mt-2 text-sm text-slate-400">
              Operasyon · Görselleştirme · Teşhis
            </div>
          </div>

          <nav className="flex-1 space-y-2 p-4">
            {navItems.map((item) => {
              const Icon = item.icon;

              return (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) =>
                    [
                      "flex items-center gap-3 rounded-2xl border px-4 py-3 transition",
                      isActive
                        ? "border-sky-500/40 bg-sky-500/10 text-sky-300"
                        : "border-transparent bg-slate-900 text-slate-300 hover:border-slate-700 hover:bg-slate-800"
                    ].join(" ")
                  }
                >
                  <Icon size={18} />
                  <span className="text-sm font-medium">{item.label}</span>
                </NavLink>
              );
            })}
          </nav>
        </aside>

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="border-b border-slate-800 bg-slate-950/80 px-6 py-4 backdrop-blur">
            <div className="flex flex-wrap items-center justify-between gap-4">
              <div>
                <div className="text-xs uppercase tracking-[0.3em] text-slate-500">
                  Hydronom Runtime Connection
                </div>
                <div className="mt-1 flex items-center gap-3">
                  <span
                    className={`inline-flex h-3 w-3 rounded-full ${connectionDotClass}`}
                  />
                  <span className="text-sm text-slate-300">{connectionText}</span>
                </div>
              </div>

              <div className="flex flex-wrap items-center gap-3">
                <TopBadge label="Gateway" value={gatewayStatus.toUpperCase()} />
                <TopBadge label="Mode" value={gatewayMode.toUpperCase()} />
                <TopBadge label="Vehicle" value={telemetry?.vehicleId ?? "N/A"} />
                <TopBadge
                  label="Mission Mode"
                  value={(telemetry?.mode ?? "unknown").toUpperCase()}
                />
                <TopBadge
                  label="Arm"
                  value={(telemetry?.armState ?? "disarmed").toUpperCase()}
                />
                <TopBadge
                  label="Source"
                  value={(telemetry?.freshness?.source ?? "unknown").toUpperCase()}
                />
              </div>
            </div>
          </header>

          <main className="min-h-0 flex-1 overflow-auto p-6">
            <Outlet />
          </main>
        </div>
      </div>
    </div>
  );
}

interface TopBadgeProps {
  label: string;
  value: string;
}

// Üst çubuktaki küçük durum kartları için ortak bileşen
function TopBadge({ label, value }: TopBadgeProps) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-900 px-4 py-2 shadow-panel">
      <div className="text-[10px] uppercase tracking-[0.25em] text-slate-500">
        {label}
      </div>
      <div className="mt-1 text-sm font-semibold text-slate-100">{value}</div>
    </div>
  );
}