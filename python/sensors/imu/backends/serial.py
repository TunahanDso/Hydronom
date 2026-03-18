# sensors/imu/backends/serial.py
from __future__ import annotations

import time
import json
import threading
from typing import Optional, Dict, Any, List, Tuple

from sensors.imu.config import ImuConfig
from .base import IImuBackend, BaseImuBackend

try:
    import serial  # pyserial
except Exception:  # pyserial yoksa graceful
    serial = None


class SerialBackend(BaseImuBackend, IImuBackend):
    """
    UART üzerinden IMU akışı.

    Beklenen satırlar:
      - JSON: {"ax":..,"ay":..,"az":..,"gx":..,"gy":..,"gz":..,"temp_c":..,"t_ms":..}
               (t_ms: cihazın monotonic ms sayacı; epoch değildir)
      - CSV : ax,ay,az,gx,gy,gz[,temp_c]

    Non-blocking: ayrı bir thread sürekli satır okur; read_imu() her çağrıda
    son ölçümün bir kopyasını anında döndürür.
    """

    def __init__(self) -> None:
        super().__init__(cfg=None)
        self._ser: Optional["serial.Serial"] = None
        self._thr: Optional[threading.Thread] = None
        self._stop = threading.Event()
        self._lock = threading.Lock()
        self._last: Dict[str, Any] = {
            "ax": None, "ay": None, "az": None,
            "gx": None, "gy": None, "gz": None,
            "mx": None, "my": None, "mz": None,
            "temp_c": None,
            "t_imu": None,        # epoch (UTC, s) — host zaman damgası
            "t_mono_ms": None,    # cihazın/akışın monotonic sayacı varsa (opsiyonel)
        }

    # -------- Lifecycle --------

    def open(self, cfg: ImuConfig) -> None:
        super().open(cfg)
        if serial is None:
            print("⚠️ pyserial bulunamadı; Serial IMU backend çalışamaz.")
            return

        # Port çözümle: cfg.port 'auto' veya boş ise otomatik tara
        port = (getattr(cfg, "port", None) or "").strip()
        if not port or port.lower() in ("auto", "none", "off"):
            port = self._resolve_port(hints=["imu", "icm", "mpu", "bno", "lsm", "ttyusb", "ttyacm"]) or ""
        if not port:
            print("⚠️ IMU seri port bulunamadı (auto-detect başarısız).")
            return

        baud = int(getattr(cfg, "baud", 115200) or 115200)

        try:
            self._ser = serial.Serial(port=port, baudrate=baud, timeout=0.05)
            # Stale buffer’ı temizle
            try:
                self._ser.reset_input_buffer()
                self._ser.reset_output_buffer()
            except Exception:
                pass
        except Exception as e:
            print(f"⚠️ IMU seri açılamadı ({port} @ {baud}): {e}")
            self._ser = None
            return

        self._stop.clear()
        self._thr = threading.Thread(target=self._reader_loop, name="IMU-Serial-Reader", daemon=True)
        self._thr.start()

    def close(self) -> None:
        self._stop.set()
        try:
            if self._thr and self._thr.is_alive():
                self._thr.join(timeout=1.0)
        except Exception:
            pass
        try:
            if self._ser:
                self._ser.close()
        except Exception:
            pass
        self._thr = None
        self._ser = None

    # -------- I/O --------

    def _reader_loop(self) -> None:
        ser = self._ser
        if ser is None:
            return

        buf = b""
        while not self._stop.is_set():
            try:
                chunk = ser.read(256)
                if not chunk:
                    time.sleep(0.002)
                    continue
                buf += chunk
                # Hem LF hem CRLF destekle
                while b"\n" in buf:
                    line, buf = buf.split(b"\n", 1)
                    line = line.strip().decode(errors="ignore")
                    if not line:
                        continue
                    self._handle_line(line)
            except Exception:
                # Seri kesik/garbage vb. durumlarda çok kısa bekle
                time.sleep(0.005)

    def _handle_line(self, line: str) -> None:
        # Önce JSON dene
        if line.startswith("{"):
            try:
                obj = json.loads(line)
                self._ingest(obj)
                return
            except Exception:
                pass

        # CSV: ax,ay,az,gx,gy,gz[,temp]
        try:
            parts: List[str] = [p.strip() for p in line.split(",")]
            if len(parts) >= 6:
                ax, ay, az, gx, gy, gz = [float(p) for p in parts[:6]]
                temp = float(parts[6]) if len(parts) >= 7 and parts[6] else None
                obj = {"ax": ax, "ay": ay, "az": az, "gx": gx, "gy": gy, "gz": gz}
                if temp is not None:
                    obj["temp_c"] = temp
                self._ingest(obj)
        except Exception:
            # satır parse edilemediyse yut
            pass

    def _ingest(self, obj: Dict[str, Any]) -> None:
        """
        Tek bir ölçüm satırını _last buffer’ına güvenle işler.
        - JSON t_ms varsa t_mono_ms’e yazar, t_imu (epoch) için host now kullanır.
        - Sayısal olmayanları None’a çevirir.
        """
        t_host = time.time()
        t_mono_ms = None
        try:
            if isinstance(obj.get("t_ms"), (int, float)):
                t_mono_ms = float(obj["t_ms"])
        except Exception:
            t_mono_ms = None

        with self._lock:
            for k in ("ax", "ay", "az", "gx", "gy", "gz", "mx", "my", "mz", "temp_c"):
                if k in obj:
                    try:
                        self._last[k] = float(obj[k]) if obj[k] is not None else None
                    except Exception:
                        self._last[k] = None
            self._last["t_imu"] = t_host
            if t_mono_ms is not None:
                self._last["t_mono_ms"] = t_mono_ms

    # -------- Public API --------

    def read_imu(self) -> Optional[Dict[str, Any]]:
        # Non-blocking snapshot
        with self._lock:
            return dict(self._last)
