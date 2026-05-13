import type { ReactNode } from "react";

import type { ActuatorState, ThrusterState } from "../../../entities/actuator/model/actuator.types";
import type { SensorState } from "../../../entities/sensor/model/sensor.types";

import type { MetricTileProps, StatusBadgeProps } from "../types";

import { formatNumber } from "../lib/tactical3d-utils";

export function PanelCard(props: {
  title: string;
  subtitle: string;
  children: ReactNode;
  collapsed?: boolean;
  onToggleCollapsed?: () => void;
}) {
  return (
    <div
      className={[
        "rounded-2xl border border-slate-700/60",
        "bg-slate-950/35 backdrop-blur-md",
        "shadow-[0_12px_36px_rgba(0,0,0,0.30)]",
        "overflow-hidden"
      ].join(" ")}
    >
      <div className="flex items-start justify-between gap-3 px-4 py-3">
        <div>
          <h3 className="text-sm font-bold text-slate-100">{props.title}</h3>
          {!props.collapsed ? (
            <p className="mt-0.5 text-xs text-slate-400">{props.subtitle}</p>
          ) : null}
        </div>

        {props.onToggleCollapsed ? (
          <button
            type="button"
            onClick={props.onToggleCollapsed}
            className={[
              "rounded-lg border border-slate-700/70 px-2 py-1",
              "text-[11px] font-semibold text-slate-300",
              "bg-slate-950/50 hover:bg-slate-800/70"
            ].join(" ")}
          >
            {props.collapsed ? "AÇ" : "ALT"}
          </button>
        ) : null}
      </div>

      {!props.collapsed ? (
        <div className="border-t border-slate-800/80 px-4 pb-4 pt-2">
          {props.children}
        </div>
      ) : null}
    </div>
  );
}

export function InfoRow(props: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3 border-b border-slate-700/55 py-1.5 last:border-b-0">
      <div className="text-xs text-slate-400">{props.label}</div>
      <div className="text-right text-xs font-semibold text-slate-100">{props.value}</div>
    </div>
  );
}

export function MetricTile(props: MetricTileProps) {
  return (
    <div
      className={[
        "rounded-2xl border border-slate-700/60",
        "bg-slate-950/35 backdrop-blur-md",
        "shadow-[0_10px_28px_rgba(0,0,0,0.26)]",
        "px-3 py-3"
      ].join(" ")}
    >
      <div className="text-[10px] uppercase tracking-[0.18em] text-slate-400">{props.label}</div>
      <div className="mt-1.5 text-base font-bold text-slate-100">{props.value}</div>
    </div>
  );
}

export function StatusBadge(props: StatusBadgeProps) {
  const className =
    props.state === "ok"
      ? "border-emerald-500/40 bg-emerald-950/50 text-emerald-200"
      : props.state === "warn"
        ? "border-amber-500/40 bg-amber-950/50 text-amber-200"
        : props.state === "error"
          ? "border-red-500/40 bg-red-950/50 text-red-200"
          : "border-slate-600/70 bg-slate-950/50 text-slate-300";

  return (
    <span
      className={[
        "rounded-full border px-2.5 py-0.5 text-[11px] font-semibold",
        "backdrop-blur-sm",
        className
      ].join(" ")}
    >
      {props.label}
    </span>
  );
}

export function LimiterGrid(props: { limiter: ActuatorState["limiter"] | undefined }) {
  const limiter = props.limiter;

  if (!limiter) {
    return <div className="mt-3 text-xs text-slate-500">Limiter verisi yok.</div>;
  }

  const items = [
    ["satT", limiter.satT],
    ["satR", limiter.satR],
    ["rlT", limiter.rlT],
    ["rlR", limiter.rlR],
    ["dbT", limiter.dbT],
    ["dbR", limiter.dbR],
    ["assist", limiter.assist],
    ["dt", limiter.dt]
  ] as const;

  return (
    <div className="mt-3 grid grid-cols-4 gap-1.5">
      {items.map(([key, active]) => (
        <div
          key={key}
          className={[
            "rounded-lg border px-1.5 py-1 text-center text-[11px] font-semibold backdrop-blur-sm",
            active
              ? "border-amber-500/40 bg-amber-950/45 text-amber-200"
              : "border-slate-700/70 bg-slate-950/35 text-slate-400"
          ].join(" ")}
        >
          {key}
        </div>
      ))}
    </div>
  );
}

export function ThrusterList(props: { thrusters: ThrusterState[] }) {
  if (props.thrusters.length === 0) {
    return <div className="mt-3 text-xs text-slate-500">Thruster verisi yok.</div>;
  }

  return (
    <div className="mt-3 space-y-1.5">
      {props.thrusters.slice(0, 6).map((thruster) => (
        <div
          key={thruster.id}
          className={[
            "rounded-xl border border-slate-700/70",
            "bg-slate-950/30 backdrop-blur-sm",
            "px-3 py-2"
          ].join(" ")}
        >
          <div className="flex items-center justify-between gap-3">
            <span className="text-[11px] font-semibold text-slate-200">
              {thruster.label ?? thruster.id}
            </span>
            <span className={thruster.active ? "text-[11px] text-emerald-300" : "text-[11px] text-slate-500"}>
              {thruster.active ? "ACTIVE" : "IDLE"}
            </span>
          </div>

          <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-slate-800/80">
            <div
              className="h-full rounded-full bg-cyan-400"
              style={{
                width: `${Math.min(100, Math.max(0, Math.abs(thruster.normalizedCommand) * 100))}%`
              }}
            />
          </div>

          <div className="mt-1 flex justify-between text-[10px] text-slate-400">
            <span>cmd {formatNumber(thruster.normalizedCommand, 2)}</span>
            <span>{formatNumber(thruster.rpm, 0)} rpm</span>
          </div>
        </div>
      ))}
    </div>
  );
}

export function SensorHealthGrid(props: { sensor: SensorState | undefined }) {
  const health = props.sensor?.sensorHealth;

  if (!health) {
    return <div className="mt-3 text-xs text-slate-500">Sensor health verisi yok.</div>;
  }

  const items = [
    ["LiDAR", health.lidar],
    ["IMU", health.imu],
    ["GPS", health.gps],
    ["Camera", health.camera]
  ] as const;

  return (
    <div className="mt-3 grid grid-cols-2 gap-1.5">
      {items.map(([label, state]) => (
        <div
          key={label}
          className={[
            "rounded-lg border px-2 py-1.5 text-[11px] font-semibold backdrop-blur-sm",
            state === "ok"
              ? "border-emerald-500/40 bg-emerald-950/40 text-emerald-200"
              : state === "warn"
                ? "border-amber-500/40 bg-amber-950/40 text-amber-200"
                : "border-red-500/40 bg-red-950/40 text-red-200"
          ].join(" ")}
        >
          <div className="text-slate-400">{label}</div>
          <div className="mt-0.5 uppercase">{state}</div>
        </div>
      ))}
    </div>
  );
}

export function Placeholder(props: {
  heightClass: string;
  children?: ReactNode;
}) {
  return (
    <div
      className={[
        "flex w-full items-center justify-center rounded-2xl border border-dashed border-slate-700/70",
        "bg-slate-950/35 backdrop-blur-md text-sm text-slate-400",
        props.heightClass
      ].join(" ")}
    >
      {props.children ?? "İçerik bu alana gelecek"}
    </div>
  );
}