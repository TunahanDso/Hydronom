# gateway/tcp_client.py
# Amaç: JSON satırlarını C# çekirdeğe (SensorSource) güvenli şekilde göndermek.
# Özellikler: yeniden bağlanma (exponential backoff), kuyruk sınırı, \n çerçeveleme,
# düşük gecikme (TCP_NODELAY), keepalive, bağlantı logları, bekle-bağlan,
# durdururken kuyruk boşaltma (graceful stop), gönderim/düşen istatistikleri.
# + Güncelleme: RX iş parçacığı eklendi (server→client mesajları okunur),
#   'StreamSubscribe' mesajı yakalanır ve opsiyonel callback'lere iletilir.
# + Sertleştirme:
#   - Socket/reader state kilitli yönetilir
#   - Eski socket ve eski makefile kalıntıları temizlenir
#   - NDJSON normalize daha katı
#   - TX/RX yarış koşulları azaltıldı
#   - wait_connected ve is_connected daha güvenli hale getirildi

import socket
import time
import threading
import queue
import json
from typing import Optional, Dict, Callable, Any


class TcpClient:
    def __init__(
        self,
        host: str = "127.0.0.1",
        port: int = 5055,
        max_queue: int = 1000,
        reconnect_initial: float = 0.5,
        reconnect_max: float = 5.0,
        on_message: Optional[Callable[[Dict[str, Any]], None]] = None,
        on_subscribe: Optional[Callable[[Dict[str, Any]], None]] = None
    ):
        # Hedef soket
        self.host = host
        self.port = port

        # Gönderim kuyruğu (satır başına bir mesaj)
        self.q: "queue.Queue[str]" = queue.Queue(maxsize=max_queue)

        # Bağlantı/iş parçacığı
        self._sock: Optional[socket.socket] = None
        self._tx_thread: Optional[threading.Thread] = None
        self._rx_thread: Optional[threading.Thread] = None
        self._stop = threading.Event()

        # Yeniden bağlanma parametreleri
        self._reconnect_initial = max(0.1, float(reconnect_initial))
        self._reconnect_max = max(self._reconnect_initial, float(reconnect_max))

        # İstatistikler
        self._sent_lines = 0
        self._dropped_lines = 0
        self._rx_lines = 0

        # Callback'ler
        self._on_message = on_message
        self._on_subscribe = on_subscribe

        # Son alınan subscribe
        self._last_subscribe: Optional[Dict[str, Any]] = None

        # RX tarafında kullanılan makefile objesi
        self._rx_file = None
        self._rx_sock_seen: Optional[socket.socket] = None

        # Ortak durum kilitleri
        self._gate = threading.RLock()

    # -------------------- Dış API --------------------

    def start(self):
        """Gönderim ve alım iş parçacıklarını başlatır."""
        self._stop.clear()

        if self._tx_thread is None or not self._tx_thread.is_alive():
            self._tx_thread = threading.Thread(target=self._run_tx, daemon=True, name="TcpClient-TX")
            self._tx_thread.start()

        if self._rx_thread is None or not self._rx_thread.is_alive():
            self._rx_thread = threading.Thread(target=self._run_rx, daemon=True, name="TcpClient-RX")
            self._rx_thread.start()

    def stop(self, flush_seconds: float = 1.0):
        """
        İletişimi durdurur. Kapanmadan önce kısa bir süre kuyruğun boşalmasını bekler.
        RX'i de güvenli şekilde kapatır.
        """
        self._stop.set()

        deadline = time.time() + max(0.0, flush_seconds)
        while flush_seconds > 0 and not self.q.empty() and self.is_connected() and time.time() < deadline:
            time.sleep(0.05)

        self._close()

        if self._tx_thread:
            self._tx_thread.join(timeout=2.0)
        if self._rx_thread:
            self._rx_thread.join(timeout=2.0)

        remaining = self.q.qsize()
        print(
            f"[TCP] Stopped; sent={self._sent_lines}, recv={self._rx_lines}, "
            f"dropped={self._dropped_lines}, qsize={remaining}"
        )

    def send(self, line: str):
        """
        Tek satır NDJSON gönderimi.
        Satır sonunda \n yoksa eklenir.
        Kuyruk doluysa en eski mesaj düşürülür.
        """
        normalized = self._normalize_ndjson_line(line)

        try:
            self.q.put_nowait(normalized)
        except queue.Full:
            try:
                _ = self.q.get_nowait()  # en eskiyi at
                self.q.put_nowait(normalized)
                self._dropped_lines += 1
            except queue.Empty:
                self._dropped_lines += 1

    def wait_connected(self, timeout: float = 3.0) -> bool:
        """Bağlantının kurulmasını kısa süre bekler. True/False döner."""
        t0 = time.time()
        while time.time() - t0 < timeout:
            if self.is_connected():
                return True
            time.sleep(0.05)
        return False

    def is_connected(self) -> bool:
        """Anlık bağlantı durumu."""
        with self._gate:
            return self._sock is not None

    def get_stats(self) -> Dict[str, int]:
        """Gönderim/alım istatistikleri."""
        return {
            "sent": self._sent_lines,
            "received": self._rx_lines,
            "dropped": self._dropped_lines,
            "qsize": self.q.qsize(),
        }

    def get_last_subscribe(self) -> Optional[Dict[str, Any]]:
        """Son alınan StreamSubscribe içeriği (varsa)."""
        with self._gate:
            return dict(self._last_subscribe) if self._last_subscribe else None

    # -------------------- İç yardımcılar --------------------

    @staticmethod
    def _normalize_ndjson_line(line: str) -> str:
        """
        NDJSON için satırı tek gerçek satır olacak şekilde normalize eder.
        """
        if line is None:
            return "\n"

        if not isinstance(line, str):
            line = str(line)

        line = line.replace("\ufeff", "")
        line = line.replace("\x00", "")
        line = line.replace("\r", "")
        line = line.rstrip("\n")

        if "\n" in line:
            line = line.replace("\n", "\\n")

        return line + "\n"

    def _connect(self):
        """Yeni soket aç ve bağlan. Düşük gecikme/keepalive ayarlarını uygular."""
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(3.0)

        try:
            s.connect((self.host, self.port))
            try:
                s.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            except Exception:
                pass

            try:
                s.setsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE, 1)
            except Exception:
                pass

            s.settimeout(None)  # bloklu yazım/okuma

            with self._gate:
                # Yeni bağlantıdan önce eski durum tamamen temizlenir.
                self._close_socket_locked()
                self._sock = s
                self._rx_sock_seen = None
                self._rx_file = None

            print(f"[TCP] Connected to {self.host}:{self.port}")

        except Exception:
            try:
                s.close()
            except Exception:
                pass
            raise

    def _close_socket_locked(self):
        """
        self._gate tutulurken çağrılır.
        Sadece iç state/soket kapatır.
        """
        if self._rx_file is not None:
            try:
                self._rx_file.close()
            except Exception:
                pass
            self._rx_file = None

        if self._sock is not None:
            try:
                self._sock.shutdown(socket.SHUT_RDWR)
            except Exception:
                pass
            try:
                self._sock.close()
            except Exception:
                pass
            self._sock = None

        self._rx_sock_seen = None

    def _close(self):
        """Soketi ve RX makefile'ını güvenli kapat."""
        with self._gate:
            self._close_socket_locked()

    def _disconnect_if_same_socket(self, sock: socket.socket):
        """
        Yalnızca verilen socket hâlâ aktif socket ise bağlantıyı kapatır.
        RX/TX yarışlarında yanlışlıkla yeni bağlantıyı kapatmamak için.
        """
        with self._gate:
            if self._sock is sock:
                self._close_socket_locked()

    def _get_socket_snapshot(self) -> Optional[socket.socket]:
        with self._gate:
            return self._sock

    # -------------------- TX loop --------------------

    def _run_tx(self):
        """Kuyruktan satır al, gönder; hata olursa yeniden bağlanmayı dene."""
        backoff = self._reconnect_initial

        while not self._stop.is_set():
            sock = self._get_socket_snapshot()

            if sock is None:
                try:
                    self._connect()
                    backoff = self._reconnect_initial
                except Exception as e:
                    print(f"[TCP] Connect failed: {e} (retry in {backoff:.1f}s)")
                    if self._stop.wait(backoff):
                        break
                    backoff = min(self._reconnect_max, backoff * 2.0)
                    continue

            try:
                line = self.q.get(timeout=0.2)
            except queue.Empty:
                continue

            sock = self._get_socket_snapshot()
            if sock is None:
                try:
                    self.q.put_nowait(line)
                except queue.Full:
                    self._dropped_lines += 1
                continue

            try:
                sock.sendall(line.encode("utf-8"))
                self._sent_lines += 1
                backoff = self._reconnect_initial
            except Exception as e:
                print(f"[TCP] Send failed: {e} → reconnecting")
                self._disconnect_if_same_socket(sock)

                try:
                    self.q.put_nowait(line)
                except queue.Full:
                    self._dropped_lines += 1
                    print("[TCP] Warning: TX queue full during reconnect; dropping one line")

                if self._stop.wait(backoff):
                    break
                backoff = min(self._reconnect_max, backoff * 2.0)

    # -------------------- RX loop --------------------

    def _run_rx(self):
        """
        Server → client yönünü okur.
        - Yeni bir soket oluştuğunda makefile('r') yeniden kurulur.
        - JSON satırlarını parse eder, type==StreamSubscribe ise callback çağırır.
        """
        while not self._stop.is_set():
            sock = self._get_socket_snapshot()
            if sock is None:
                time.sleep(0.05)
                continue

            with self._gate:
                if sock is not self._sock:
                    time.sleep(0.05)
                    continue

                if sock is not self._rx_sock_seen or self._rx_file is None:
                    try:
                        if self._rx_file is not None:
                            try:
                                self._rx_file.close()
                            except Exception:
                                pass
                            self._rx_file = None

                        self._rx_file = sock.makefile(
                            "r",
                            buffering=1,
                            encoding="utf-8",
                            newline="\n"
                        )
                        self._rx_sock_seen = sock
                    except Exception:
                        self._rx_file = None
                        self._rx_sock_seen = None
                        time.sleep(0.05)
                        continue

                rx_file = self._rx_file

            if rx_file is None:
                time.sleep(0.05)
                continue

            try:
                line = rx_file.readline()

                if line == "":
                    self._disconnect_if_same_socket(sock)
                    continue

                line = line.strip()
                if not line:
                    continue

                self._rx_lines += 1

                try:
                    msg = json.loads(line)
                except Exception:
                    # Geçersiz JSON → sessiz geç
                    continue

                mtype = msg.get("type")
                if mtype == "StreamSubscribe":
                    with self._gate:
                        self._last_subscribe = dict(msg)
                    if self._on_subscribe:
                        self._safe_call(self._on_subscribe, msg)
                else:
                    if self._on_message:
                        self._safe_call(self._on_message, msg)

            except Exception:
                # Okuma tarafı düştüyse aktif socket hâlâ buysa kapat.
                self._disconnect_if_same_socket(sock)
                time.sleep(0.05)

    # -------------------- yardımcı --------------------

    @staticmethod
    def _safe_call(cb: Callable[[Dict[str, Any]], None], payload: Dict[str, Any]):
        try:
            cb(payload)
        except Exception as e:
            # Callback'te hata akışı bozmasın
            print(f"[TCP] Callback error: {e}")