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

    this.manuallyClosed = false;
    this.setStatus(this.reconnectAttempts > 0 ? "reconnecting" : "connecting");

    this.socket = new WebSocket(this.url);

    this.socket.onopen = () => {
      this.reconnectAttempts = 0;
      this.setStatus("connected");
      this.onOpen?.();
    };

    this.socket.onmessage = (event) => {
      try {
        const parsed = JSON.parse(event.data) as GatewayMessage;
        this.onMessage?.(parsed);
      } catch {
        console.warn("Gateway mesajı parse edilemedi:", event.data);
      }
    };

    this.socket.onerror = (error) => {
      this.setStatus("error");
      this.onError?.(error);
    };

    this.socket.onclose = () => {
      this.socket = null;
      this.onClose?.();

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
  }

  public send(data: unknown) {
    if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
      console.warn("Gateway bağlantısı açık değil, mesaj gönderilemedi.");
      return;
    }

    this.socket.send(JSON.stringify(data));
  }

  public getStatus() {
    return this.status;
  }

  private tryReconnect() {
    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      this.setStatus("disconnected");
      return;
    }

    this.reconnectAttempts += 1;
    this.setStatus("reconnecting");
    this.clearReconnectTimer();

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
    this.status = status;
    this.onStatusChange?.(status);
  }
}