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

const defaultGatewaySettings = {
  mode: "mock" as GatewayMode,
  url: "ws://localhost:8080/ws",
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
      url
    })),

  setReconnectIntervalMs: (value) =>
    set(() => ({
      reconnectIntervalMs: value
    })),

  setMaxReconnectAttempts: (value) =>
    set(() => ({
      maxReconnectAttempts: value
    })),

  setGatewaySettings: (payload) =>
    set(() => ({
      mode: payload.mode,
      url: payload.url,
      reconnectIntervalMs: payload.reconnectIntervalMs,
      maxReconnectAttempts: payload.maxReconnectAttempts
    })),

  resetGatewaySettings: () =>
    set(() => ({
      ...defaultGatewaySettings
    }))
}));