import { create } from "zustand";
import type { GatewayConnectionStatus } from "../model/GatewayClient";

interface GatewayConnectionStore {
  status: GatewayConnectionStatus;
  isConnected: boolean;
  lastError: string | null;
  mode: "mock" | "ws";
  url: string | null;

  setConnectionState: (payload: {
    status: GatewayConnectionStatus;
    isConnected: boolean;
    lastError: string | null;
  }) => void;

  setGatewayConfig: (payload: {
    mode: "mock" | "ws";
    url: string | null;
  }) => void;

  resetConnectionState: () => void;
}

export const useGatewayConnectionStore = create<GatewayConnectionStore>((set) => ({
  status: "idle",
  isConnected: false,
  lastError: null,
  mode: "mock",
  url: null,

  setConnectionState: (payload) =>
    set(() => ({
      status: payload.status,
      isConnected: payload.isConnected,
      lastError: payload.lastError
    })),

  setGatewayConfig: (payload) =>
    set(() => ({
      mode: payload.mode,
      url: payload.url
    })),

  resetConnectionState: () =>
    set((state) => ({
      status: "idle",
      isConnected: false,
      lastError: null,
      mode: state.mode,
      url: state.url
    }))
}));