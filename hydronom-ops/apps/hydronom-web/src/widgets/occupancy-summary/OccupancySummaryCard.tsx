import type { SensorState } from "../../entities/sensor/model/sensor.types";

interface OccupancySummaryCardProps {
  sensor?: SensorState;
}

// Occupancy-grid özetini göstermek için kart
export function OccupancySummaryCard({ sensor }: OccupancySummaryCardProps) {
  const occupancy = sensor?.occupancy;

  return (
    <div className="rounded-3xl border border-slate-800 bg-slate-900 p-5 shadow-panel">
      <h3 className="text-lg font-semibold">Occupancy Summary</h3>
      <p className="mt-1 text-sm text-slate-400">
        Grid boyutu, çözünürlük ve dolu hücre özeti
      </p>

      <div className="mt-4 space-y-3">
        <InfoRow label="Grid Width" value={String(occupancy?.width ?? 0)} />
        <InfoRow label="Grid Height" value={String(occupancy?.height ?? 0)} />
        <InfoRow
          label="Resolution"
          value={`${occupancy?.resolution ?? 0} m/cell`}
        />
        <InfoRow
          label="Occupied Cells"
          value={String(occupancy?.occupiedCellCount ?? 0)}
        />
      </div>

      <div className="mt-4 rounded-2xl border border-dashed border-slate-700 bg-slate-950/60 p-4 text-sm text-slate-400">
        Bir sonraki adımda burada mini occupancy-grid önizleme ve layer kontrolü olacak.
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