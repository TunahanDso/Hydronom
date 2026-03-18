import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useGatewayConnectionStore } from "../../features/gateway/store/gateway-connection.store";
import {
  useGatewaySettingsStore,
  type GatewayMode
} from "../../features/gateway/store/gateway-settings.store";

interface GatewaySettingsDraft {
  mode: GatewayMode;
  url: string;
  reconnectIntervalMs: string;
  maxReconnectAttempts: string;
}

export function SettingsPage() {
  const gatewayStatus = useGatewayConnectionStore((state) => state.status);
  const gatewayIsConnected = useGatewayConnectionStore(
    (state) => state.isConnected
  );
  const gatewayLastError = useGatewayConnectionStore((state) => state.lastError);
  const runtimeGatewayMode = useGatewayConnectionStore((state) => state.mode);
  const runtimeGatewayUrl = useGatewayConnectionStore((state) => state.url);

  const activeMode = useGatewaySettingsStore((state) => state.mode);
  const activeUrl = useGatewaySettingsStore((state) => state.url);
  const activeReconnectIntervalMs = useGatewaySettingsStore(
    (state) => state.reconnectIntervalMs
  );
  const activeMaxReconnectAttempts = useGatewaySettingsStore(
    (state) => state.maxReconnectAttempts
  );
  const setGatewaySettings = useGatewaySettingsStore(
    (state) => state.setGatewaySettings
  );

  const [draft, setDraft] = useState<GatewaySettingsDraft>({
    mode: activeMode,
    url: activeUrl,
    reconnectIntervalMs: String(activeReconnectIntervalMs),
    maxReconnectAttempts: String(activeMaxReconnectAttempts)
  });

  useEffect(() => {
    setDraft({
      mode: activeMode,
      url: activeUrl,
      reconnectIntervalMs: String(activeReconnectIntervalMs),
      maxReconnectAttempts: String(activeMaxReconnectAttempts)
    });
  }, [
    activeMode,
    activeUrl,
    activeReconnectIntervalMs,
    activeMaxReconnectAttempts
  ]);

  const statusTone =
    gatewayStatus === "connected"
      ? "ok"
      : gatewayStatus === "connecting" || gatewayStatus === "reconnecting"
        ? "info"
        : gatewayStatus === "error"
          ? "danger"
          : "neutral";

  const trimmedUrl = draft.url.trim();
  const parsedReconnectIntervalMs = Number(draft.reconnectIntervalMs);
  const parsedMaxReconnectAttempts = Number(draft.maxReconnectAttempts);

  const validationError = useMemo(() => {
    if (draft.mode === "ws" && trimmedUrl.length === 0) {
      return "WebSocket modu için gateway adresi boş bırakılamaz.";
    }

    if (
      draft.mode === "ws" &&
      !trimmedUrl.startsWith("ws://") &&
      !trimmedUrl.startsWith("wss://")
    ) {
      return "Gateway adresi ws:// veya wss:// ile başlamalı.";
    }

    if (
      !Number.isFinite(parsedReconnectIntervalMs) ||
      !Number.isInteger(parsedReconnectIntervalMs) ||
      parsedReconnectIntervalMs < 250
    ) {
      return "Reconnect interval en az 250 ms olan tam sayı bir değer olmalı.";
    }

    if (
      !Number.isFinite(parsedMaxReconnectAttempts) ||
      !Number.isInteger(parsedMaxReconnectAttempts) ||
      parsedMaxReconnectAttempts < 0
    ) {
      return "Max reconnect attempts 0 veya daha büyük tam sayı olmalı.";
    }

    return null;
  }, [
    draft.mode,
    trimmedUrl,
    parsedReconnectIntervalMs,
    parsedMaxReconnectAttempts
  ]);

  const hasChanges =
    draft.mode !== activeMode ||
    trimmedUrl !== activeUrl ||
    parsedReconnectIntervalMs !== activeReconnectIntervalMs ||
    parsedMaxReconnectAttempts !== activeMaxReconnectAttempts;

  const canApply = validationError === null && hasChanges;

  function handleApply() {
    if (!canApply) {
      return;
    }

    setGatewaySettings({
      mode: draft.mode,
      url: trimmedUrl,
      reconnectIntervalMs: parsedReconnectIntervalMs,
      maxReconnectAttempts: parsedMaxReconnectAttempts
    });
  }

  function handleReset() {
    setDraft({
      mode: activeMode,
      url: activeUrl,
      reconnectIntervalMs: String(activeReconnectIntervalMs),
      maxReconnectAttempts: String(activeMaxReconnectAttempts)
    });
  }

  return (
    <section className="space-y-6">
      <PageTitle
        title="Settings"
        description="Gateway modu, bağlantı adresi ve yeniden bağlanma davranışı bu ekrandan yönetilir."
      />

      <div className="grid grid-cols-1 gap-6 2xl:grid-cols-12">
        <div className="space-y-6 2xl:col-span-8">
          <PanelCard
            title="Gateway Configuration"
            subtitle="Hydronom Ops veri kaynağı ve bağlantı davranışı"
          >
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
              <InfoStat
                label="Active Mode"
                value={activeMode.toUpperCase()}
                tone={activeMode === "mock" ? "info" : "neutral"}
              />
              <InfoStat
                label="Status"
                value={gatewayStatus.toUpperCase()}
                tone={statusTone}
              />
              <InfoStat
                label="Connected"
                value={gatewayIsConnected ? "YES" : "NO"}
                tone={gatewayIsConnected ? "ok" : "warn"}
              />
              <InfoStat
                label="Source"
                value={activeMode === "mock" ? "SIMULATION" : "WEBSOCKET"}
                tone="neutral"
              />
            </div>

            <div className="mt-6 grid grid-cols-1 gap-4 xl:grid-cols-2">
              <FieldBlock
                label="Gateway Mode"
                description="Mock akış ile gerçek WebSocket bağlantısı arasında seçim yapılır."
              >
                <div className="grid grid-cols-2 gap-3">
                  <ModeButton
                    active={draft.mode === "mock"}
                    onClick={() =>
                      setDraft((current) => ({
                        ...current,
                        mode: "mock"
                      }))
                    }
                  >
                    Mock Stream
                  </ModeButton>
                  <ModeButton
                    active={draft.mode === "ws"}
                    onClick={() =>
                      setDraft((current) => ({
                        ...current,
                        mode: "ws"
                      }))
                    }
                  >
                    WebSocket
                  </ModeButton>
                </div>
              </FieldBlock>

              <FieldBlock
                label="Gateway URL"
                description="WebSocket modunda kullanılacak runtime/gateway adresi."
              >
                <input
                  type="text"
                  value={draft.url}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      url: event.target.value
                    }))
                  }
                  placeholder="ws://localhost:8080/ws"
                  disabled={draft.mode === "mock"}
                  className="w-full rounded-2xl border border-slate-800 bg-slate-950/70 px-4 py-3 text-sm text-slate-100 outline-none transition focus:border-sky-500/50 disabled:cursor-not-allowed disabled:opacity-60"
                />
              </FieldBlock>

              <FieldBlock
                label="Reconnect Interval (ms)"
                description="Bağlantı koparsa yeniden deneme aralığını belirler."
              >
                <input
                  type="number"
                  min={250}
                  step={250}
                  value={draft.reconnectIntervalMs}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      reconnectIntervalMs: event.target.value
                    }))
                  }
                  className="w-full rounded-2xl border border-slate-800 bg-slate-950/70 px-4 py-3 text-sm text-slate-100 outline-none transition focus:border-sky-500/50"
                />
              </FieldBlock>

              <FieldBlock
                label="Max Reconnect Attempts"
                description="Yeniden bağlanma denemesi üst sınırını belirler. 0 değeri denemeyi kapatır."
              >
                <input
                  type="number"
                  min={0}
                  step={1}
                  value={draft.maxReconnectAttempts}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      maxReconnectAttempts: event.target.value
                    }))
                  }
                  className="w-full rounded-2xl border border-slate-800 bg-slate-950/70 px-4 py-3 text-sm text-slate-100 outline-none transition focus:border-sky-500/50"
                />
              </FieldBlock>
            </div>

            <div className="mt-6 rounded-2xl border border-slate-800 bg-slate-950/50 p-4">
              <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
                <div className="space-y-2">
                  <div className="text-xs font-semibold uppercase tracking-[0.25em] text-slate-400">
                    Apply State
                  </div>
                  <p className="text-sm text-slate-300">
                    Değişiklikler yalnızca <strong>Apply</strong> ile aktif
                    bağlantı yapılandırmasına yazılır.
                  </p>
                  {validationError ? (
                    <p className="text-sm text-rose-300">{validationError}</p>
                  ) : hasChanges ? (
                    <p className="text-sm text-amber-300">
                      Kaydedilmemiş değişiklikler var.
                    </p>
                  ) : (
                    <p className="text-sm text-emerald-300">
                      Taslak değerler aktif ayarla aynı.
                    </p>
                  )}
                </div>

                <div className="flex flex-wrap gap-3">
                  <button
                    type="button"
                    onClick={handleReset}
                    disabled={!hasChanges}
                    className="rounded-2xl border border-slate-700 bg-slate-900 px-4 py-2 text-sm font-medium text-slate-200 transition hover:border-slate-600 hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Reset
                  </button>
                  <button
                    type="button"
                    onClick={handleApply}
                    disabled={!canApply}
                    className="rounded-2xl border border-sky-500/30 bg-sky-500/10 px-4 py-2 text-sm font-semibold text-sky-200 transition hover:border-sky-400/40 hover:bg-sky-500/15 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Apply
                  </button>
                </div>
              </div>
            </div>
          </PanelCard>

          <PanelCard
            title="Active Configuration"
            subtitle="Şu anda provider tarafından kullanılan canlı ayarlar"
          >
            <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
              <SettingBlock
                label="Active Mode"
                value={activeMode === "mock" ? "Mock Stream" : "WebSocket"}
                description="Provider katmanı şu anda bu modu kullanarak veri akışını başlatır."
              />
              <SettingBlock
                label="Active URL"
                value={activeUrl}
                description="WebSocket modunda kullanılacak aktif gateway adresi."
              />
              <SettingBlock
                label="Reconnect Interval"
                value={`${activeReconnectIntervalMs} ms`}
                description="Aktif yeniden bağlanma deneme aralığı."
              />
              <SettingBlock
                label="Max Reconnect Attempts"
                value={String(activeMaxReconnectAttempts)}
                description="Aktif yeniden bağlanma deneme üst sınırı."
              />
            </div>
          </PanelCard>
        </div>

        <div className="space-y-6 2xl:col-span-4">
          <PanelCard
            title="Runtime Snapshot"
            subtitle="Anlık bağlantı özeti"
          >
            <InfoRow label="Gateway Status" value={gatewayStatus.toUpperCase()} />
            <InfoRow label="Gateway Mode" value={activeMode.toUpperCase()} />
            <InfoRow
              label="Connected"
              value={gatewayIsConnected ? "YES" : "NO"}
            />
            <InfoRow label="Configured URL" value={activeUrl} />
            <InfoRow label="Runtime URL" value={runtimeGatewayUrl ?? "N/A"} />
            <InfoRow
              label="Runtime Source"
              value={
                runtimeGatewayMode === "mock" ? "SIMULATION" : "WEBSOCKET"
              }
            />
            <InfoRow label="Last Error" value={gatewayLastError ?? "Yok"} />
          </PanelCard>

          <PanelCard
            title="Development Notes"
            subtitle="Hydronom Ops ayar sistemi yaklaşımı"
          >
            <div className="space-y-3 text-sm text-slate-300">
              <NoteLine text="Bu ekran artık taslak ayarları düzenler, Apply ile aktif store'a yazar." />
              <NoteLine text="Provider katmanı aktif store değerlerini okuyarak mock veya websocket akışını yeniden kurar." />
              <NoteLine text="Her input değişiminde reconnect tetiklenmemesi için form alanları local draft state kullanır." />
              <NoteLine text="Bu aşamada sade kaldık; yalnızca gateway bağlantı yapılandırması yönetiliyor." />
            </div>
          </PanelCard>
        </div>
      </div>
    </section>
  );
}

function PageTitle(props: { title: string; description: string }) {
  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight">{props.title}</h1>
      <p className="mt-2 max-w-3xl text-sm text-slate-400">
        {props.description}
      </p>
    </div>
  );
}

function PanelCard(props: {
  title: string;
  subtitle: string;
  children: ReactNode;
}) {
  return (
    <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6 shadow-panel">
      <h2 className="text-xl font-semibold">{props.title}</h2>
      <p className="mt-1 text-sm text-slate-400">{props.subtitle}</p>
      <div className="mt-5">{props.children}</div>
    </div>
  );
}

function InfoRow(props: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-slate-800 py-2 last:border-b-0">
      <div className="text-sm text-slate-400">{props.label}</div>
      <div className="max-w-[60%] break-all text-right text-sm font-medium text-slate-100">
        {props.value}
      </div>
    </div>
  );
}

function InfoStat(props: {
  label: string;
  value: string;
  tone: "neutral" | "info" | "ok" | "warn" | "danger";
}) {
  const toneClass =
    props.tone === "danger"
      ? "border-rose-500/30 bg-rose-500/10 text-rose-200"
      : props.tone === "warn"
        ? "border-amber-500/30 bg-amber-500/10 text-amber-200"
        : props.tone === "ok"
          ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-200"
          : props.tone === "info"
            ? "border-sky-500/30 bg-sky-500/10 text-sky-200"
            : "border-slate-800 bg-slate-950/60 text-slate-200";

  return (
    <div className={`rounded-2xl border p-4 ${toneClass}`}>
      <div className="text-[10px] uppercase tracking-[0.25em] text-slate-400">
        {props.label}
      </div>
      <div className="mt-2 text-sm font-semibold">{props.value}</div>
    </div>
  );
}

function SettingBlock(props: {
  label: string;
  value: string;
  description: string;
}) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-4">
      <div className="text-[10px] uppercase tracking-[0.25em] text-slate-500">
        {props.label}
      </div>
      <div className="mt-2 break-all text-sm font-semibold text-slate-100">
        {props.value}
      </div>
      <p className="mt-2 text-sm text-slate-400">{props.description}</p>
    </div>
  );
}

function FieldBlock(props: {
  label: string;
  description: string;
  children: ReactNode;
}) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-4">
      <div className="text-[10px] uppercase tracking-[0.25em] text-slate-500">
        {props.label}
      </div>
      <div className="mt-3">{props.children}</div>
      <p className="mt-2 text-sm text-slate-400">{props.description}</p>
    </div>
  );
}

function ModeButton(props: {
  active: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={props.onClick}
      className={`rounded-2xl border px-4 py-3 text-sm font-medium transition ${
        props.active
          ? "border-sky-500/40 bg-sky-500/10 text-sky-200"
          : "border-slate-800 bg-slate-950/70 text-slate-300 hover:border-slate-700 hover:bg-slate-900"
      }`}
    >
      {props.children}
    </button>
  );
}

function NoteLine(props: { text: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950/50 px-4 py-3">
      {props.text}
    </div>
  );
}