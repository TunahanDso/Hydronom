import { useEffect, useRef, useState } from "react";
import { dispatchGatewayMessage } from "../dispatchGatewayMessage";
import { startMockGatewayStream } from "../startMockGatewayStream";
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
}

interface UseGatewayConnectionResult {
  status: GatewayConnectionStatus;
  isConnected: boolean;
  lastError: string | null;
}

// Uygulama açıldığında mock stream veya gerçek websocket bağlantısını yönetir
export function useGatewayConnection(
  options: UseGatewayConnectionOptions = {}
): UseGatewayConnectionResult {
  const {
    enabled = true,
    useMock = true,
    url = "ws://localhost:5186/ws",
    reconnectIntervalMs = 3000,
    maxReconnectAttempts = Infinity
  } = options;

  const [status, setStatus] = useState<GatewayConnectionStatus>(
    useMock ? "connected" : "idle"
  );
  const [lastError, setLastError] = useState<string | null>(null);

  const clientRef = useRef<GatewayClient | null>(null);

  useEffect(() => {
    if (!enabled) {
      setStatus("idle");
      setLastError(null);
      return;
    }

    // Mock mod açıksa websocket yerine sahte veri akışı başlatılır
    if (useMock) {
      setStatus("connected");
      setLastError(null);

      const stopMockStream = startMockGatewayStream();

      return () => {
        stopMockStream();
      };
    }

    // Gerçek gateway bağlantısı
    const client = new GatewayClient({
      url,
      reconnectIntervalMs,
      maxReconnectAttempts,
      onOpen: () => {
        setLastError(null);
      },
      onClose: () => {
        // Status zaten GatewayClient içinden güncelleniyor
      },
      onError: () => {
        setLastError("Gateway bağlantısında bir hata oluştu.");
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
    client.connect();

    return () => {
      client.disconnect();
      clientRef.current = null;
    };
  }, [enabled, useMock, url, reconnectIntervalMs, maxReconnectAttempts]);

  return {
    status,
    isConnected: status === "connected",
    lastError
  };
}