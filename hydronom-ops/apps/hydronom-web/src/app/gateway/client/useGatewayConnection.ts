import { useEffect, useRef, useState } from "react";
import { dispatchGatewayMessage } from "../dispatchGatewayMessage";
import {
  GatewayClient,
  type GatewayConnectionStatus
} from "../../../features/gateway/model/GatewayClient";

interface UseGatewayConnectionOptions {
  enabled?: boolean;
  useMock?: boolean;
  url?: string;
  reconnectIntervalMs?: number;
  maxReconnectAttempts?: number;
  pollingIntervalMs?: number;
}

interface UseGatewayConnectionResult {
  status: GatewayConnectionStatus;
  isConnected: boolean;
  lastError: string | null;
}

// Uygulama açıldığında mock stream, websocket ve snapshot polling hattını yönetir.
export function useGatewayConnection(
  options: UseGatewayConnectionOptions = {}
): UseGatewayConnectionResult {
  const {
    enabled = true,
    useMock = false,
    url = "ws://localhost:5186/ws",
    reconnectIntervalMs = 3000,
    maxReconnectAttempts = Infinity,
    pollingIntervalMs = 1000
  } = options;

  const [status, setStatus] = useState<GatewayConnectionStatus>("idle");
  const [lastError, setLastError] = useState<string | null>(null);

  const clientRef = useRef<GatewayClient | null>(null);
  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    if (!enabled) {
      setStatus("idle");
      setLastError(null);
      return;
    }

    if (useMock) {
      setStatus("connected");
      setLastError(null);
      return;
    }

    const pollSnapshot = async () => {
      try {
        const resp = await fetch("http://localhost:5186/snapshot");

        if (!resp.ok) {
          throw new Error(`Snapshot HTTP ${resp.status}`);
        }

        const data = await resp.json();

        dispatchGatewayMessage({
          type: "runtime.telemetry-summary",
          vehicleId: data.vehicleId ?? "hydronom-main",
          timestampUtc:
            data.lastUpdatedUtc ??
            data.vehicleTelemetry?.timestampUtc ??
            data.missionState?.timestampUtc ??
            data.worldState?.timestampUtc ??
            data.actuatorState?.timestampUtc ??
            new Date().toISOString(),
          payload: data
        });
      } catch {
        // Snapshot polling geçici hata verdiğinde websocket bağlantısını düşürmeyelim.
      }
    };

    const startPolling = () => {
      if (pollingRef.current) {
        return;
      }

      void pollSnapshot();

      pollingRef.current = setInterval(() => {
        void pollSnapshot();
      }, pollingIntervalMs);
    };

    const stopPolling = () => {
      if (!pollingRef.current) {
        return;
      }

      clearInterval(pollingRef.current);
      pollingRef.current = null;
    };

    const client = new GatewayClient({
      url,
      reconnectIntervalMs,
      maxReconnectAttempts,
      onOpen: () => {
        setStatus("connected");
        setLastError(null);

        // WebSocket bağlı olsa bile snapshot polling açık kalıyor.
        // Çünkü şu an tam mission/world/actuator state snapshot endpointinden geliyor.
        startPolling();
      },
      onClose: () => {
        setStatus("idle");

        // WebSocket kapanırsa da snapshot polling devam eder.
        startPolling();
      },
      onError: () => {
        setLastError("Gateway bağlantısında bir hata oluştu.");
        startPolling();
      },
      onStatusChange: (nextStatus) => {
        setStatus(nextStatus);
      },
      onMessage: (message) => {
        dispatchGatewayMessage(message);
      }
    });

    clientRef.current = client;
    setLastError(null);

    // İlk veri için websocket beklemeyelim.
    startPolling();
    client.connect();

    return () => {
      client.disconnect();
      clientRef.current = null;
      stopPolling();
    };
  }, [enabled, useMock, url, reconnectIntervalMs, maxReconnectAttempts, pollingIntervalMs]);

  return {
    status,
    isConnected: status === "connected",
    lastError
  };
}