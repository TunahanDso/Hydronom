import { useFleetStore } from "../../features/fleet-management/store/fleet.store";
import { useDiagnosticsStore } from "../../features/diagnostics-monitoring/store/diagnostics.store";

// Teşhis ve runtime sağlığı için özet kart
export function DiagnosticsSummaryCard() {
  const selectedVehicleId = useFleetStore((state) => state.selectedVehicleId);

  const diagnostics = useDiagnosticsStore(
    (state) => state.diagnosticsByVehicleId[selectedVehicleId]
  );

  const activeWarnings =
    diagnostics?.logs.filter((log) => log.level === "warn" || log.level === "error") ?? [];

  const runtimeMetric = diagnostics?.streamMetrics.find(
    (metric) => metric.key === "runtime"
  );

  const lidarMetric = diagnostics?.streamMetrics.find(
    (metric) => metric.key === "lidar"
  );

  const twinMetric = diagnostics?.streamMetrics.find(
    (metric) => metric.key === "twin"
  );

  return (
    <div className="rounded-3xl border border-slate-800 bg-slate-900 p-5 shadow-panel">
      <h3 className="text-lg font-semibold">Diagnostics Summary</h3>
      <p className="mt-1 text-sm text-slate-400">
        Runtime sağlık, bağlantı ve akış özeti
      </p>

      <div className="mt-4 space-y-3">
        <InfoRow
          label="Overall Connection"
          value={(diagnostics?.overallConnection ?? "disconnected").toUpperCase()}
        />
        <InfoRow
          label="Overall Health"
          value={(diagnostics?.overallHealth ?? "unknown").toUpperCase()}
        />
        <InfoRow
          label="Runtime Hz"
          value={`${runtimeMetric?.rateHz ?? 0} Hz`}
        />
        <InfoRow
          label="Lidar Hz"
          value={`${lidarMetric?.rateHz ?? 0} Hz`}
        />
        <InfoRow
          label="Twin Hz"
          value={`${twinMetric?.rateHz ?? 0} Hz`}
        />
        <InfoRow
          label="Freshness"
          value={`${diagnostics?.freshness.ageMs ?? 0} ms`}
        />
      </div>

      <div className="mt-4">
        <div className="text-xs uppercase tracking-[0.25em] text-slate-500">
          Source Inspector
        </div>

        <div className="mt-3 space-y-2">
          {(diagnostics?.sourceInspector ?? []).slice(0, 3).map((item) => (
            <div
              key={item.key}
              className="rounded-xl border border-slate-800 bg-slate-950/60 px-3 py-2"
            >
              <div className="flex items-center justify-between gap-3">
                <div className="text-sm font-semibold text-slate-200">
                  {item.label}
                </div>
                <span
                  className={[
                    "rounded-full px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.2em]",
                    item.state === "ok"
                      ? "bg-emerald-500/15 text-emerald-300"
                      : item.state === "warn"
                        ? "bg-amber-500/15 text-amber-300"
                        : item.state === "error"
                          ? "bg-rose-500/15 text-rose-300"
                          : "bg-slate-800 text-slate-400"
                  ].join(" ")}
                >
                  {item.state}
                </span>
              </div>

              <div className="mt-2 text-xs text-slate-400">
                Source: {item.source} · Freshness: {item.freshnessMs} ms
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="mt-4">
        <div className="text-xs uppercase tracking-[0.25em] text-slate-500">
          Active Alerts
        </div>

        <div className="mt-3 space-y-2">
          {activeWarnings.slice(0, 3).map((alert) => (
            <div
              key={alert.id}
              className="rounded-xl border border-slate-800 bg-slate-950/60 px-3 py-2"
            >
              <div className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-400">
                {alert.level}
              </div>
              <div className="mt-1 text-sm text-slate-200">{alert.message}</div>
            </div>
          ))}

          {activeWarnings.length === 0 && (
            <div className="rounded-xl border border-slate-800 bg-slate-950/60 px-3 py-3 text-sm text-slate-500">
              Aktif alarm yok
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function InfoRow(props: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-slate-800 py-2 last:border-b-0">
      <div className="text-sm text-slate-400">{props.label}</div>
      <div className="text-right text-sm font-medium text-slate-100">
        {props.value}
      </div>
    </div>
  );
}