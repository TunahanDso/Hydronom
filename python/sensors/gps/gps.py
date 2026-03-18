# sensors/gps/gps.py

import time
from typing import Optional, Dict, Any

from sensors.base_sensor import BaseSensor
from sensors.gps.config import GpsConfig

# Ortak seri port bulucu (opsiyonel)
try:
    from sensors.serial_utils import find_serial_port  # type: ignore
except Exception:  # modül yoksa sorun etme
    def find_serial_port(*args, **kwargs):
        return None


def _make_backend(name: str):
    # Not: "csharp_sim" de sim backend kullanır; ayrımı backend içinde cfg.backend ile yapacağız.
    if name in ("sim", "csharp_sim"):
        from sensors.gps.backends.sim import SimBackend
        return SimBackend()
    elif name == "nmea":
        from sensors.gps.backends.nmea import NmeaBackend
        return NmeaBackend()
    elif name == "gpsd":
        from sensors.gps.backends.gpsd import GpsdBackend
        return GpsdBackend()
    raise ValueError(f"Unknown GPS backend: {name}")


class GpsSensor(BaseSensor):
    """Backend’ten gelen ham fix’i standart Sample’a dönüştüren taşıyıcı."""
    SENSOR = "gps"  # BaseSensor.sensor_name için

    def __init__(self, cfg: Optional[GpsConfig] = None):
        self.cfg = cfg or GpsConfig()
        super().__init__(
            source=self.cfg.source,
            frame_id=self.cfg.frame_id,
            # Hem klasik sim hem de C# twin sim simulated sayılıyor
            simulate=(self.cfg.backend in ("sim", "csharp_sim")),
        )
        self._backend = _make_backend(self.cfg.backend)

    # SensorManager dinamik hız ayarı için
    def set_rate_hz(self, hz: float):
        try:
            self.cfg.rate_hz = float(hz)
            super().set_rate_hz(self.cfg.rate_hz)
        except Exception:
            pass

    def open(self):
        # NMEA backend ise ve port "auto"/boşsa otomatik keşfet
        if self.cfg.backend == "nmea":
            port_str = (getattr(self.cfg, "port", None) or "").strip().lower()
            if port_str in ("", "auto"):
                auto = find_serial_port(
                    linux_candidates=["/dev/ttyUSB0", "/dev/ttyACM0", "/dev/serial0"],
                    windows_prefix="COM",
                    windows_range=(1, 32),
                )
                if auto:
                    self.cfg.port = auto
        self._backend.open(self.cfg)
        self.is_open = True

    def read(self):
        self._ensure_open()

        # backend.read_fix() non-blocking olmalı; yoksa backend thread’li tasarlanmalı
        fix: Dict[str, Any] = self._backend.read_fix() or {}

        # --- Savunmalı dönüşüm (float(None) patlamasını engeller) ---
        lat_raw = fix.get("lat")
        lon_raw = fix.get("lon")
        alt_raw = fix.get("alt")
        hdop_raw = fix.get("hdop")
        t_gps_raw = fix.get("t_gps")  # epoch(s) veya None

        lat = float(lat_raw) if lat_raw is not None else None
        lon = float(lon_raw) if lon_raw is not None else None
        alt = float(alt_raw) if alt_raw is not None else None
        hdop = float(hdop_raw) if hdop_raw is not None else None
        fix_type = int(fix.get("fix") or 0)

        now = time.time()
        t_gps = float(t_gps_raw) if t_gps_raw is not None else None
        age_ms = int(max(0.0, (now - t_gps) * 1000.0)) if t_gps is not None else None

        data = {
            "lat": lat,
            "lon": lon,
            "alt": alt,
            "fix": fix_type,   # 0: no-fix, 1: GPS, 2: DGPS/RTK vs.
            "hdop": hdop,
            "t_gps": t_gps,    # GPS epoch (UTC) — varsa
        }
        quality = {
            "valid": (lat is not None and lon is not None and fix_type > 0),
            "age_ms": age_ms,
            "backend": self.cfg.backend,  # "nmea", "gpsd", "sim" veya "csharp_sim"
        }

        from core.sample import Sample  # yerel import: döngü bağımlılığından kaçınır
        seq = self._new_seq()
        return Sample(
            sensor="gps",
            source=self.source,
            frame_id=self.frame_id,
            data=data,
            quality=quality,
            seq=seq,
            t=self._stamp(),
        )

    def close(self):
        try:
            self._backend.close()
        finally:
            self.is_open = False

    # StreamSubscribe için genişletme: backend/port/baud canlı güncellenebilsin
    def apply_stream_subscribe(self, spec: Dict[str, Any]) -> Dict[str, Any]:
        changed = super().apply_stream_subscribe(spec)
        reinit_needed = False

        # Backend değişimi
        if "backend" in spec:
            new_backend = str(spec["backend"]).strip().lower()
            # C# twin sim için "csharp_sim" de destekleniyor
            if new_backend in ("sim", "csharp_sim", "nmea", "gpsd") and new_backend != self.cfg.backend:
                self.cfg.backend = new_backend
                # simulate flag’ini de yeni backend’e göre güncelle
                self.simulate = (self.cfg.backend in ("sim", "csharp_sim"))
                reinit_needed = True
                changed["backend"] = new_backend

        # Port / baud değişimi
        for k in ("port", "baud"):
            if k in spec:
                try:
                    val = spec[k]
                    if k == "baud":
                        val = int(val)
                    setattr(self.cfg, k, val)
                    changed[k] = val
                    # NMEA/gpsd için yeniden açmak isteyebiliriz
                    reinit_needed = True
                except Exception as e:
                    changed[f"{k}_error"] = str(e)

        if reinit_needed:
            try:
                if self.is_open:
                    self.close()
                self._backend = _make_backend(self.cfg.backend)
                self.open()
                changed["reinitialized"] = True
            except Exception as e:
                changed["reinit_error"] = str(e)

        return changed
