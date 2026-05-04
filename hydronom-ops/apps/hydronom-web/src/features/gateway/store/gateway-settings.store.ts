import { create } from "zustand";

export type GatewayMode = "mock" | "ws";

interface GatewaySettingsStore {
  mode: GatewayMode;
  url: string;
  reconnectIntervalMs: number;
  maxReconnectAttempts: number;

  setMode: (mode: GatewayMode) => void;
  setUrl: (url: string) => void;
  setReconnectIntervalMs: (value: number) => void;
  setMaxReconnectAttempts: (value: number) => void;

  setGatewaySettings: (payload: {
    mode: GatewayMode;
    url: string;
    reconnectIntervalMs: number;
    maxReconnectAttempts: number;
  }) => void;

  resetGatewaySettings: () => void;
}

/*
 * Hydronom Ops artık varsayılan olarak gerçek Gateway websocket hattına bağlanır.
 *
 * Gateway:
 *   http://localhost:5186
 *
 * WebSocket:
 *   ws://localhost:5186/ws
 *
 * Mock mod hâlâ desteklenir; fakat default mock olmamalı.
 * Çünkü C# Primary Runtime / Gateway / Scenario TCP Replay hattını canlı izlemek için
 * Ops'un açılışta gerçek Gateway'e bağlanması gerekir.
 */
const defaultGatewaySettings = {
  mode: "ws" as GatewayMode,
  url: "ws://localhost:5186/ws",
  reconnectIntervalMs: 3000,
  maxReconnectAttempts: 20
};

export const useGatewaySettingsStore = create<GatewaySettingsStore>((set) => ({
  ...defaultGatewaySettings,

  setMode: (mode) =>
    set(() => ({
      mode
    })),

  setUrl: (url) =>
    set(() => ({
      url: normalizeGatewayUrl(url)
    })),

  setReconnectIntervalMs: (value) =>
    set(() => ({
      reconnectIntervalMs: sanitizePositiveInteger(value, 3000)
    })),

  setMaxReconnectAttempts: (value) =>
    set(() => ({
      maxReconnectAttempts: sanitizePositiveInteger(value, 20)
    })),

  setGatewaySettings: (payload) =>
    set(() => ({
      mode: payload.mode,
      url: normalizeGatewayUrl(payload.url),
      reconnectIntervalMs: sanitizePositiveInteger(
        payload.reconnectIntervalMs,
        defaultGatewaySettings.reconnectIntervalMs
      ),
      maxReconnectAttempts: sanitizePositiveInteger(
        payload.maxReconnectAttempts,
        defaultGatewaySettings.maxReconnectAttempts
      )
    })),

  resetGatewaySettings: () =>
    set(() => ({
      ...defaultGatewaySettings
    }))
}));

function normalizeGatewayUrl(value: string) {
  const trimmed = value.trim();

  if (!trimmed) {
    return defaultGatewaySettings.url;
  }

  return trimmed;
}

function sanitizePositiveInteger(value: number, fallback: number) {
  if (!Number.isFinite(value)) {
    return fallback;
  }

  const rounded = Math.round(value);

  return rounded > 0 ? rounded : fallback;
}