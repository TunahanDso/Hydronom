# sensors/gps/backends/gpsd.py
import threading
import time
from typing import Optional, Dict, Any

from .base import IGpsBackend
from sensors.gps.config import GpsConfig

try:
    # gpsd-py3
    from gps import gps as gpsd_gps, WATCH_ENABLE, WATCH_JSON  # type: ignore
except Exception:
    gpsd_gps = None
    WATCH_ENABLE = 0
    WATCH_JSON = 0


class GpsdBackend(IGpsBackend):
    """
    gpsd üzerinden GPS okuma (non-blocking).
    - Arkaplanda gpsd stream'ini okuyup son TPV fix'ini saklar.
    - read_fix() her çağrıda en son snapshot'ı döndürür.
    """

    def __init__(self) -> None:
        self._cfg: Optional[GpsConfig] = None
        self._session = None
        self._thr: Optional[threading.Thread] = None
        self._stop = threading.Event()
        self._lock = threading.Lock()
        self._last_fix: Dict[str, Any] = {
            "lat": None, "lon": None, "alt": None, "fix": 0, "hdop": None, "t_gps": None
        }

    # ---------------- Lifecycle ----------------

    def open(self, cfg: GpsConfig) -> None:
        self._cfg = cfg
        if gpsd_gps is None:
            raise ImportError("gpsd backend için 'gpsd-py3' gerekli. Kurulum: pip install gpsd-py3")

        host = getattr(cfg, "gpsd_host", "127.0.0.1")
        port = int(getattr(cfg, "gpsd_port", 2947))
        try:
            self._session = gpsd_gps(host=host, port=port)
            self._session.stream(WATCH_ENABLE | WATCH_JSON)
        except Exception as e:
            self._session = None
            raise RuntimeError(f"gpsd oturumu açılamadı ({host}:{port}): {e}")

        self._stop.clear()
        self._thr = threading.Thread(target=self._reader_loop, name="GPSD-Reader", daemon=True)
        self._thr.start()

    def close(self) -> None:
        self._stop.set()
        try:
            if self._thr and self._thr.is_alive():
                self._thr.join(timeout=1.0)
        except Exception:
            pass
        try:
            if self._session:
                self._session.close()
        except Exception:
            pass
        self._session = None
        self._thr = None

    # ---------------- Public API ----------------

    def read_fix(self) -> Dict[str, Any]:
        # Non-blocking snapshot
        with self._lock:
            return dict(self._last_fix)

    # ---------------- Internal ----------------

    def _reader_loop(self) -> None:
        sess = self._session
        if sess is None:
            return

        backoff = 0.25
        while not self._stop.is_set():
            try:
                # gpsd-py3: waiting() var; yoksa next() kısa blok yapabilir
                has_data = False
                try:
                    has_data = bool(sess.waiting())
                except Exception:
                    # bazı sürümlerde waiting() olmayabilir → devam et
                    has_data = True

                if not has_data:
                    time.sleep(0.02)
                    continue

                report = sess.next()  # dict benzeri
                self._handle_report(report)
                backoff = 0.25
            except Exception:
                # gpsd reset/bağlantı kopması → kısa bekle, tekrar dene
                time.sleep(backoff)
                backoff = min(2.0, backoff * 1.5)

    def _handle_report(self, report: Any) -> None:
        """
        gpsd TPV/SKY raporlarını işle. TPV'den:
          - lat, lon, alt
          - mode (0/1/2/3) → fix
          - time (ISO8601) → t_gps
        SKY/TPV'den DOP türevleri (epx, epy, eph) → hdop yaklaşık
        """
        def get(rep, key, default=None):
            # gpsd-py3 bazen dict-like, bazen attribute
            try:
                if isinstance(rep, dict):
                    return rep.get(key, default)
                return getattr(rep, key, default)
            except Exception:
                return default

        clazz = get(report, "class")
        if clazz == "TPV":
            lat = get(report, "lat")
            lon = get(report, "lon")
            alt = get(report, "alt")
            mode = int(get(report, "mode", 0) or 0)  # 2=2D, 3=3D
            # Zaman (ISO8601)
            t_iso = get(report, "time")
            t_gps = self._iso_to_epoch(t_iso) if t_iso else None

            # hdop yaklaşık: gpsd bazı sürümlerde epx/epy/eph verir
            hdop = get(report, "epx")
            if hdop is None:
                hdop = get(report, "eph")
            try:
                hdop = float(hdop) if hdop is not None else None
            except Exception:
                hdop = None

            with self._lock:
                if lat is not None: self._last_fix["lat"] = float(lat)
                if lon is not None: self._last_fix["lon"] = float(lon)
                self._last_fix["alt"] = float(alt) if alt is not None else self._last_fix["alt"]
                self._last_fix["fix"] = 3 if mode == 3 else (2 if mode == 2 else 0)
                if hdop is not None:
                    self._last_fix["hdop"] = hdop
                if t_gps is not None:
                    self._last_fix["t_gps"] = t_gps

        elif clazz == "SKY":
            # DOP bilgisi buradan da gelebilir
            hdop = get(report, "hdop")
            try:
                hdop = float(hdop) if hdop is not None else None
            except Exception:
                hdop = None
            if hdop is not None:
                with self._lock:
                    self._last_fix["hdop"] = hdop

    def _iso_to_epoch(self, iso_str: str) -> Optional[float]:
        # "2025-10-25T19:41:22.000Z" → epoch
        try:
            import datetime
            s = iso_str.replace("Z", "+00:00")
            dt = datetime.datetime.fromisoformat(s)
            if dt.tzinfo is None:
                dt = dt.replace(tzinfo=datetime.timezone.utc)
            return dt.timestamp()
        except Exception:
            return None
