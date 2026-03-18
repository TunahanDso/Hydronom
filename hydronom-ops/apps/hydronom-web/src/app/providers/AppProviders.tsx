import { useEffect, type PropsWithChildren } from "react";
import { startMockGatewayStream } from "../gateway/startMockGatewayStream";
import { useGatewayConnection } from "../gateway/client/useGatewayConnection";
import { useGatewayConnectionStore } from "../../features/gateway/store/gateway-connection.store";
import { useGatewaySettingsStore } from "../../features/gateway/store/gateway-settings.store";

// İleride tema, query client, websocket ve benzeri sağlayıcılar burada toplanacak
export function AppProviders({ children }: PropsWithChildren) {
  const mode = useGatewaySettingsStore((state) => state.mode);
  const url = useGatewaySettingsStore((state) => state.url);
  const reconnectIntervalMs = useGatewaySettingsStore(
    (state) => state.reconnectIntervalMs
  );
  const maxReconnectAttempts = useGatewaySettingsStore(
    (state) => state.maxReconnectAttempts
  );

  const isMockMode = mode === "mock";

  const { status, isConnected, lastError } = useGatewayConnection({
    enabled: !isMockMode,
    useMock: false,
    url,
    reconnectIntervalMs,
    maxReconnectAttempts
  });

  const setConnectionState = useGatewayConnectionStore(
    (state) => state.setConnectionState
  );
  const setGatewayConfig = useGatewayConnectionStore(
    (state) => state.setGatewayConfig
  );
  const resetConnectionState = useGatewayConnectionStore(
    (state) => state.resetConnectionState
  );

  useEffect(() => {
    // Aktif gateway yapılandırmasını connection store içine de yazar
    // Böylece Settings / Runtime Snapshot tarafı aynı gerçek konfigürasyonu görür
    setGatewayConfig({
      mode,
      url
    });
  }, [mode, url, setGatewayConfig]);

  useEffect(() => {
    if (isMockMode) {
      setConnectionState({
        status: "connected",
        isConnected: true,
        lastError: null
      });

      const stopStream = startMockGatewayStream();

      return () => {
        stopStream();
        resetConnectionState();
      };
    }

    return;
  }, [isMockMode, setConnectionState, resetConnectionState]);

  useEffect(() => {
    if (isMockMode) {
      return;
    }

    setConnectionState({
      status,
      isConnected,
      lastError
    });
  }, [isMockMode, status, isConnected, lastError, setConnectionState]);

  return <>{children}</>;
}