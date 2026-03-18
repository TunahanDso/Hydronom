export function MapLegend() {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950/80 p-3 shadow-panel">
      <div className="text-[10px] uppercase tracking-[0.25em] text-slate-500">
        Katmanlar
      </div>

      <div className="mt-3 grid grid-cols-2 gap-2 text-xs text-slate-300">
        <LegendItem colorClass="bg-sky-400" label="Araç" />
        <LegendItem colorClass="bg-cyan-300" label="Trail" />
        <LegendItem colorClass="bg-emerald-400" label="Route" />
        <LegendItem colorClass="bg-amber-400" label="Goal" />
        <LegendItem colorClass="bg-rose-400" label="Obstacle" />
        <LegendItem colorClass="bg-violet-400" label="Lidar" />
      </div>
    </div>
  );
}

function LegendItem(props: { colorClass: string; label: string }) {
  return (
    <div className="flex items-center gap-2 rounded-xl border border-slate-800 bg-slate-900/70 px-2 py-2">
      <span className={`inline-flex h-3 w-3 rounded-full ${props.colorClass}`} />
      <span>{props.label}</span>
    </div>
  );
}