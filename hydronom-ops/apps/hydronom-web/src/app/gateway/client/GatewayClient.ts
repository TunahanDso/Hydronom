import type { GatewayMessage } from "../../../shared/types/gateway.types";

export type GatewayConnectionStatus =
  | "idle"
  | "connecting"
  | "connected"
  | "reconnecting"
  | "disconnected"
  | "error";

export interface GatewayClientOptions {
  url: string;
  reconnectIntervalMs?: number;
  maxReconnectAttempts?: number;
  onOpen?: () => void;
  onClose?: () => void;
  onError?: (error: Event) => void;
  onStatusChange?: (status: GatewayConnectionStatus) => void;
  onMessage?: (message: GatewayMessage) => void;
}

export class GatewayClient {
  private socket: WebSocket | null = null;
  private reconnectTimerId: number | null = null;
  private reconnectAttempts = 0;
  private manuallyClosed = false;
  private status: GatewayConnectionStatus = "idle";

  private readonly url: string;
  private readonly reconnectIntervalMs: number;
  private readonly maxReconnectAttempts: number;
  private readonly onOpen?: () => void;
  private readonly onClose?: () => void;
  private readonly onError?: (error: Event) => void;
  private readonly onStatusChange?: (status: GatewayConnectionStatus) => void;
  private readonly onMessage?: (message: GatewayMessage) => void;

  constructor(options: GatewayClientOptions) {
    this.url = options.url;
    this.reconnectIntervalMs = options.reconnectIntervalMs ?? 3000;
    this.maxReconnectAttempts = options.maxReconnectAttempts ?? Infinity;
    this.onOpen = options.onOpen;
    this.onClose = options.onClose;
    this.onError = options.onError;
    this.onStatusChange = options.onStatusChange;
    this.onMessage = options.onMessage;
  }

  public connect() {
    if (
      this.socket &&
      (this.socket.readyState === WebSocket.OPEN ||
        this.socket.readyState === WebSocket.CONNECTING)
    ) {
      return;
    }

    this.clearReconnectTimer();
    this.manuallyClosed = false;
    this.setStatus(this.reconnectAttempts > 0 ? "reconnecting" : "connecting");

    console.log("[GatewayClient] Connecting:", this.url);

    this.socket = new WebSocket(this.url);

    this.socket.onopen = () => {
      this.reconnectAttempts = 0;
      this.setStatus("connected");
      console.log("[GatewayClient] Connected");
      this.onOpen?.();
    };

    this.socket.onmessage = (event) => {
      const raw = String(event.data);

      console.log("[GatewayClient] RAW GATEWAY DATA:", raw);

      try {
        const parsed = JSON.parse(raw) as unknown;
        console.log("[GatewayClient] PARSED GATEWAY DATA:", parsed);
        this.dispatchParsedMessage(parsed);
      } catch (error) {
        console.warn("[GatewayClient] Gateway mesajı parse edilemedi:", raw, error);
      }
    };

    this.socket.onerror = (error) => {
      this.setStatus("error");
      console.error("[GatewayClient] Socket error:", error);
      this.onError?.(error);
    };

    this.socket.onclose = () => {
      this.socket = null;
      this.onClose?.();
      console.warn("[GatewayClient] Connection closed");

      if (this.manuallyClosed) {
        this.setStatus("disconnected");
        return;
      }

      this.tryReconnect();
    };
  }

  public disconnect() {
    this.manuallyClosed = true;
    this.clearReconnectTimer();

    if (this.socket) {
      this.socket.close();
      this.socket = null;
    }

    this.setStatus("disconnected");
    console.log("[GatewayClient] Disconnected manually");
  }

  public send(data: unknown) {
    if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
      console.warn("[GatewayClient] Gateway bağlantısı açık değil, mesaj gönderilemedi.");
      return;
    }

    this.socket.send(JSON.stringify(data));
  }

  public getStatus() {
    return this.status;
  }

  private dispatchParsedMessage(parsed: unknown) {
    console.log("[GatewayClient] DISPATCH CANDIDATE:", parsed);

    if (Array.isArray(parsed)) {
      for (const item of parsed) {
        if (this.isGatewayMessage(item)) {
          console.log("[GatewayClient] Dispatching array item:", item.type);
          this.onMessage?.(item);
        } else {
          console.warn("[GatewayClient] Geçersiz gateway mesajı (array item):", item);
        }
      }
      return;
    }

    if (this.isGatewayMessage(parsed)) {
      console.log("[GatewayClient] Dispatching single message:", parsed.type);
      this.onMessage?.(parsed);
      return;
    }

    if (this.isObject(parsed)) {
      const candidateArrays = [parsed.messages, parsed.items, parsed.data];

      for (const candidate of candidateArrays) {
        if (Array.isArray(candidate)) {
          for (const item of candidate) {
            if (this.isGatewayMessage(item)) {
              console.log("[GatewayClient] Dispatching wrapped item:", item.type);
              this.onMessage?.(item);
            } else {
              console.warn("[GatewayClient] Geçersiz gateway mesajı (wrapped item):", item);
            }
          }
          return;
        }
      }
    }

    console.warn("[GatewayClient] Gateway mesaj formatı tanınmadı:", parsed);
  }

  private isGatewayMessage(value: unknown): value is GatewayMessage {
    if (!this.isObject(value)) {
      return false;
    }

    return (
      typeof value.type === "string" &&
      typeof value.vehicleId === "string" &&
      typeof value.payload !== "undefined"
    );
  }

  private isObject(
    value: unknown
  ): value is Record<string, unknown> & {
    messages?: unknown;
    items?: unknown;
    data?: unknown;
    type?: unknown;
    vehicleId?: unknown;
    payload?: unknown;
  } {
    return typeof value === "object" && value !== null;
  }

  private tryReconnect() {
    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      this.setStatus("disconnected");
      return;
    }

    this.reconnectAttempts += 1;
    this.setStatus("reconnecting");
    this.clearReconnectTimer();

    console.warn(
      `[GatewayClient] Reconnecting in ${this.reconnectIntervalMs} ms (attempt ${this.reconnectAttempts})`
    );

    this.reconnectTimerId = window.setTimeout(() => {
      this.connect();
    }, this.reconnectIntervalMs);
  }

  private clearReconnectTimer() {
    if (this.reconnectTimerId !== null) {
      window.clearTimeout(this.reconnectTimerId);
      this.reconnectTimerId = null;
    }
  }

  private setStatus(status: GatewayConnectionStatus) {
    if (this.status === status) {
      return;
    }

    this.status = status;
    console.log("[GatewayClient] Status:", status);
    this.onStatusChange?.(status);
  }
}