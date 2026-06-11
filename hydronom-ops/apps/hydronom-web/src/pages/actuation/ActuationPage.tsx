import { useFleetStore } from "../../features/fleet-management/store/fleet.store";
import { useVehicleStore } from "../../features/vehicle-state/store/vehicle.store";
import { useActuatorStore } from "../../features/actuator-monitoring/store/actuator.store";
import type { ActuatorLimiterState } from "../../entities/actuator/model/actuator.types";

export function ActuationPage() {
  const selectedVehicleId = useFleetStore((state) => state.selectedVehicleId);

  const telemetry = useVehicleStore((state) =>
    selectedVehicleId ? state.telemetryByVehicleId[selectedVehicleId] : undefined
  );

  const actuator = useActuatorStore((state) =>
    selectedVehicleId ? state.actuatorByVehicleId[selectedVehicleId] : undefined
  );

  const profile = telemetry?.vehicleProfile ?? null;
  const caps = profile?.capabilities ?? null;

  const vehicleName =
    profile?.displayName ?? telemetry?.displayName ?? telemetry?.vehicleId ?? selectedVehicleId ?? "N/A";

  const vehicleMode = profile
    ? profile.isMiniRov
      ? "Mini ROV"
      : profile.isUnderwater
        ? "Underwater"
        : "Surface"
    : "Unknown";

  const activeThrusters =
    actuator?.thrusters?.filter((thruster) => thruster.active).length ?? 0;

  const totalThrusters = actuator?.thrusters?.length ?? 0;

  const profileWarnings = buildProfileWarnings(profile);

  return (
    <section className="space-y-6">
      <PageHeader
        title="Actuation & Dynamics"
        description="AraÃƒÆ’Ã‚Â§ profil kabiliyetleri, thruster komutlarÃƒâ€Ã‚Â±, body wrench ve limiter durumlarÃƒâ€Ã‚Â±."
      />

      <div className="grid gap-4 xl:grid-cols-4">
        <CapabilityCard
          label="Thrusters"
          value={caps?.hasThrusters ?? false}
          description={caps?.hasThrusters ? "Thruster tabanlÃƒâ€Ã‚Â± hareket mevcut" : "Thruster kabiliyeti bildirilmedi"}
        />
        <CapabilityCard
          label="Reverse"
          value={caps?.hasReverseAuthority ?? false}
          description={
            caps?.hasReverseAuthority
              ? "Geri itki/negatif Fx kullanÃƒâ€Ã‚Â±labilir"
              : "Geri itki yok veya kÃƒâ€Ã‚Â±sÃƒâ€Ã‚Â±tlÃƒâ€Ã‚Â±; reverse komutlarÃƒâ€Ã‚Â± clamp edilebilir"
          }
        />
        <CapabilityCard
          label="Lateral"
          value={caps?.canGenerateLateralForce ?? false}
          description={
            caps?.canGenerateLateralForce
              ? "Yanal kuvvet ÃƒÆ’Ã‚Â¼retilebilir"
              : "Yanal kuvvet yok; underactuated davranÃƒâ€Ã‚Â±Ãƒâ€¦Ã…Â¸ beklenir"
          }
        />
        <CapabilityCard
          label="Yaw"
          value={caps?.canGenerateYawMoment ?? false}
          description={
            caps?.canGenerateYawMoment
              ? "Yaw moment ÃƒÆ’Ã‚Â¼retilebilir"
              : "Yaw otoritesi yok veya ÃƒÆ’Ã‚Â§ok dÃƒÆ’Ã‚Â¼Ãƒâ€¦Ã…Â¸ÃƒÆ’Ã‚Â¼k"
          }
        />
      </div>

      <div className="grid gap-6 xl:grid-cols-[1fr_1.2fr]">
        <Panel title="Vehicle Profile" subtitle="Runtime tarafÃƒâ€Ã‚Â±ndan seÃƒÆ’Ã‚Â§ilen araÃƒÆ’Ã‚Â§ kimliÃƒâ€Ã…Â¸i ve kabiliyetleri">
          <div className="grid gap-3 md:grid-cols-2">
            <InfoRow label="Vehicle" value={vehicleName} />
            <InfoRow label="Profile ID" value={profile?.profileId ?? "N/A"} />
            <InfoRow label="Platform" value={profile?.platformKind ?? "N/A"} />
            <InfoRow label="Mode" value={vehicleMode} />
            <InfoRow label="Active" value={profile?.active ? "YES" : "NO"} />
            <InfoRow label="Capability Summary" value={profile?.capabilitySummary ?? "N/A"} />
          </div>

          {profileWarnings.length > 0 ? (
            <div className="mt-5 space-y-2">
              {profileWarnings.map((warning) => (
                <div
                  key={warning}
                  className="rounded-2xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-200"
                >
                  {warning}
                </div>
              ))}
            </div>
          ) : (
            <div className="mt-5 rounded-2xl border border-emerald-500/25 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-200">
              Profile capability tarafÃƒâ€Ã‚Â±nda kritik bir kÃƒâ€Ã‚Â±sÃƒâ€Ã‚Â±t gÃƒÆ’Ã‚Â¶rÃƒÆ’Ã‚Â¼nmÃƒÆ’Ã‚Â¼yor.
            </div>
          )}
        </Panel>

        <Panel title="Actuator Summary" subtitle="Thruster ve body wrench ÃƒÆ’Ã‚Â¶zeti">
          <div className="grid gap-3 md:grid-cols-3">
            <InfoRow label="Active Thrusters" value={`${activeThrusters}/${totalThrusters}`} />
            <InfoRow label="Force X" value={formatNumber(actuator?.wrench?.forceBody?.x)} />
            <InfoRow label="Force Y" value={formatNumber(actuator?.wrench?.forceBody?.y)} />
            <InfoRow label="Force Z" value={formatNumber(actuator?.wrench?.forceBody?.z)} />
            <InfoRow label="Torque X" value={formatNumber(actuator?.wrench?.torqueBody?.x)} />
            <InfoRow label="Torque Y" value={formatNumber(actuator?.wrench?.torqueBody?.y)} />
            <InfoRow label="Torque Z" value={formatNumber(actuator?.wrench?.torqueBody?.z)} />
            <InfoRow label="Freshness" value={actuator?.freshness?.source ?? "unknown"} />
            <InfoRow label="Updated" value={actuator?.freshness?.timestamp ?? "N/A"} />
          </div>

          <div className="mt-5 flex flex-wrap gap-2">
            <LimiterTag label="satT" active={actuator?.limiter?.satT ?? false} />
            <LimiterTag label="satR" active={actuator?.limiter?.satR ?? false} />
            <LimiterTag label="rlT" active={actuator?.limiter?.rlT ?? false} />
            <LimiterTag label="rlR" active={actuator?.limiter?.rlR ?? false} />
            <LimiterTag label="dbT" active={actuator?.limiter?.dbT ?? false} />
            <LimiterTag label="dbR" active={actuator?.limiter?.dbR ?? false} />
            <LimiterTag label="assist" active={actuator?.limiter?.assist ?? false} />
            <LimiterTag label="dt" active={actuator?.limiter?.dt ?? false} />
          </div>
        </Panel>
      </div>

      <Panel title="Thruster Map" subtitle="Runtime actuator frame iÃƒÆ’Ã‚Â§erisindeki thruster komutlarÃƒâ€Ã‚Â±">
        {(actuator?.thrusters ?? []).length > 0 ? (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {(actuator?.thrusters ?? []).map((thruster) => (
              <div
                key={thruster.id}
                className="rounded-2xl border border-slate-800 bg-slate-950/70 p-4"
              >
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <div className="font-semibold text-slate-100">
                      {thruster.label ?? thruster.id}
                    </div>
                    <div className="mt-1 text-xs uppercase tracking-[0.2em] text-slate-500">
                      {thruster.direction ?? "unknown"}
                    </div>
                  </div>
                  <span
                    className={[
                      "rounded-full px-3 py-1 text-xs font-semibold",
                      thruster.active
                        ? "bg-emerald-500/15 text-emerald-200"
                        : "bg-slate-800 text-slate-400"
                    ].join(" ")}
                  >
                    {thruster.active ? "ACTIVE" : "IDLE"}
                  </span>
                </div>

                <div className="mt-4 h-2 overflow-hidden rounded-full bg-slate-800">
                  <div
                    className="h-full rounded-full bg-sky-400"
                    style={{
                      width: `${Math.min(100, Math.max(0, Math.abs(thruster.normalizedCommand ?? 0) * 100))}%`
                    }}
                  />
                </div>

                <div className="mt-4 grid grid-cols-2 gap-2 text-xs text-slate-400">
                  <div>Cmd: {formatNumber(thruster.normalizedCommand)}</div>
                  <div>Applied: {formatNumber(thruster.appliedCommand)}</div>
                  <div>RPM: {formatNumber(thruster.rpm, 0)}</div>
                  <div>Dir: {(thruster.direction ?? "neutral").toUpperCase()}</div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="rounded-2xl border border-dashed border-slate-700 bg-slate-950/50 p-8 text-center text-sm text-slate-500">
            Bu araÃƒÆ’Ã‚Â§ iÃƒÆ’Ã‚Â§in actuator/thruster frame henÃƒÆ’Ã‚Â¼z gelmedi.
          </div>
        )}
      </Panel>
    </section>
  );
}

function buildProfileWarnings(
  profile: NonNullable<ReturnType<typeof useVehicleStore.getState>["telemetryByVehicleId"][string]["vehicleProfile"]> | null
): string[] {
  if (!profile) {
    return ["Vehicle profile henÃƒÆ’Ã‚Â¼z gelmedi; actuator yorumlarÃƒâ€Ã‚Â± generic modda gÃƒÆ’Ã‚Â¶steriliyor."];
  }

  const warnings: string[] = [];
  const caps = profile.capabilities;

  if (!caps.hasThrusters) {
    warnings.push("Bu profil thruster kabiliyeti bildirmiyor; actuator komutlarÃƒâ€Ã‚Â± beklenmeyebilir.");
  }

  if (!caps.hasReverseAuthority) {
    warnings.push("Reverse authority yok: negatif Fx veya geri manevra komutlarÃƒâ€Ã‚Â± runtime tarafÃƒâ€Ã‚Â±nda sÃƒâ€Ã‚Â±nÃƒâ€Ã‚Â±rlandÃƒâ€Ã‚Â±rÃƒâ€Ã‚Â±labilir.");
  }

  if (!caps.canGenerateLateralForce) {
    warnings.push("Lateral force yok: araÃƒÆ’Ã‚Â§ yanal kayma/strafe yerine yaw + forward hareket ile dÃƒÆ’Ã‚Â¶nmelidir.");
  }

  if (!caps.canGenerateYawMoment) {
    warnings.push("Yaw moment yok: rota takibi ve slalom dÃƒÆ’Ã‚Â¶nÃƒÆ’Ã‚Â¼Ãƒâ€¦Ã…Â¸leri ciddi Ãƒâ€¦Ã…Â¸ekilde kÃƒâ€Ã‚Â±sÃƒâ€Ã‚Â±tlanabilir.");
  }

  return warnings;
}

function PageHeader(props: { title: string; description: string }) {
  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight">{props.title}</h1>
      <p className="mt-2 max-w-3xl text-sm text-slate-400">{props.description}</p>
    </div>
  );
}

function Panel(props: {
  title: string;
  subtitle?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6 shadow-panel">
      <div>
        <h2 className="text-lg font-semibold text-slate-100">{props.title}</h2>
        {props.subtitle ? (
          <p className="mt-1 text-sm text-slate-400">{props.subtitle}</p>
        ) : null}
      </div>
      <div className="mt-5">{props.children}</div>
    </section>
  );
}

function CapabilityCard(props: {
  label: string;
  value: boolean;
  description: string;
}) {
  return (
    <div
      className={[
        "rounded-3xl border p-5 shadow-panel",
        props.value
          ? "border-emerald-500/25 bg-emerald-500/10"
          : "border-amber-500/25 bg-amber-500/10"
      ].join(" ")}
    >
      <div className="text-xs uppercase tracking-[0.25em] text-slate-400">
        {props.label}
      </div>
      <div className={["mt-2 text-2xl font-bold", props.value ? "text-emerald-200" : "text-amber-200"].join(" ")}>
        {props.value ? "YES" : "NO"}
      </div>
      <p className="mt-2 text-sm text-slate-300">{props.description}</p>
    </div>
  );
}

function InfoRow(props: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950/70 px-4 py-3">
      <div className="text-[10px] uppercase tracking-[0.25em] text-slate-500">
        {props.label}
      </div>
      <div className="mt-1 break-words text-sm font-semibold text-slate-100">
        {props.value}
      </div>
    </div>
  );
}

function LimiterTag(props: {
  label: keyof ActuatorLimiterState | string;
  active: boolean;
}) {
  return (
    <span
      className={[
        "rounded-full border px-3 py-1 text-xs font-semibold",
        props.active
          ? "border-rose-500/30 bg-rose-500/15 text-rose-200"
          : "border-slate-700 bg-slate-950 text-slate-500"
      ].join(" ")}
    >
      {props.label}: {props.active ? "ON" : "off"}
    </span>
  );
}

function formatNumber(value: number | null | undefined, digits = 2): string {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return "0";
  }

  return value.toFixed(digits);
}