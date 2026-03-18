import type { SensorState } from "../../entities/sensor/model/sensor.types";

interface SensorSummaryCardProps {
  sensor?: SensorState;
}

// Sensör özetini ana operasyonda göstermek için kart
export function SensorSummaryCard({ sensor }: SensorSummaryCardProps) {
  return (
    <div className="rounded-3xl border border-slate-800 bg-slate-900 p-5 shadow-panel">
      <h3 className="text-lg font-semibold">Sensor Summary</h3>
      <p className="mt-1 text-sm text-slate-400">
        LiDAR, IMU, GPS, kamera ve veri tazeliği özeti
      </p>

      <div className="mt-4 space-y-3">
        <SensorRow label="LiDAR Points" value={String(sensor?.lidarPoints.length ?? 0)} />
        <SensorRow label="Obstacles" value={String(sensor?.obstacles.length ?? 0)} />
        <SensorRow
          label="Freshness"
          value={`${sensor?.freshness.ageMs ?? 0} ms`}
        />
        <SensorRow
          label="Source"
          value={(sensor?.freshness.source ?? "unknown").toUpperCase()}
        />
      </div>

      <div className="mt-4 grid grid-cols-2 gap-3">
        <HealthBadge label="LiDAR" state={sensor?.sensorHealth.lidar ?? "unknown"} />
        <HealthBadge label="IMU" state={sensor?.sensorHealth.imu ?? "unknown"} />
        <HealthBadge label="GPS" state={sensor?.sensorHealth.gps ?? "unknown"} />
        <HealthBadge label="Camera" state={sensor?.sensorHealth.camera ?? "unknown"} />
      </div>
    </div>
  );
}

function SensorRow(props: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-slate-800 py-2 last:border-b-0">
      <div className="text-sm text-slate-400">{props.label}</div>
      <div className="text-right text-sm font-medium text-slate-100">
        {props.value}
      </div>
    </div>
  );
}

function HealthBadge(props: {
  label: string;
  state: "ok" | "warn" | "error" | "unknown";
}) {
  const className =
    props.state === "ok"
      ? "bg-emerald-500/15 text-emerald-300 border-emerald-500/30"
      : props.state === "warn"
        ? "bg-amber-500/15 text-amber-300 border-amber-500/30"
        : props.state === "error"
          ? "bg-rose-500/15 text-rose-300 border-rose-500/30"
          : "bg-slate-800 text-slate-400 border-slate-700";

  return (
    <div className={`rounded-2xl border px-3 py-3 ${className}`}>
      <div className="text-[10px] uppercase tracking-[0.25em]">{props.label}</div>
      <div className="mt-2 text-sm font-semibold uppercase">{props.state}</div>
    </div>
  );
}