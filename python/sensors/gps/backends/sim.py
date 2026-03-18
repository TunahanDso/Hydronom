# sensors/gps/backends/sim.py
from __future__ import annotations

import math
import random
import time
from typing import Dict, Any, Optional

from sensors.gps.config import GpsConfig
from .base import IGpsBackend
from sensors.twin_bus import TwinBus  # ← C# twin'den gelen veriyi buradan okuyacağız

# Yaklaşık dönüştürme: 1° enlem ≈ 111.32 km
EARTH_M_PER_DEG_LAT = 111_320.0


def _meters_to_deg(lat_deg: float, dx_m: float, dy_m: float):
    """Yerel düzlemde dx (doğu-batı), dy (kuzey-güney) metreyi dereceye çevirir."""
    dlat = dy_m / EARTH_M_PER_DEG_LAT
    # cos(lat) kutuplara yakın küçülür; 0'a bölmeyi önlemek için küçük eps ekleyelim
    dlon = dx_m / (EARTH_M_PER_DEG_LAT * max(1e-9, math.cos(math.radians(lat_deg))))
    return dlat, dlon


def _cfg_get(cfg: GpsConfig, *names: str, default=None):
    for n in names:
        if hasattr(cfg, n):
            v = getattr(cfg, n)
            if v is not None:
                return v
    return default


class SimBackend(IGpsBackend):
    """
    GPS sim backend'i iki modlu çalışır:

      1) backend = "sim"
         - Eski tarz lokal simülasyon:
           * Sabit hız ve başlıkla ilerleme
           * Metre cinsinden Gauss gürültüsü
           * Çok yavaş sürüklenme (drift)
           * Non-blocking (read_fix() anlık snapshot döndürür)

      2) backend = "csharp_sim"
         - Dijital ikiz / gölge sim modu:
           * KESİNLİKLE random üretmez.
           * C# runtime'ın gönderdiği TwinGps mesajlarını kullanır.
           * Twin verisi yoksa "no-fix" döndürür (lat/lon=None, fix=0).
    """

    def __init__(self) -> None:
        # Dinamik olarak open(cfg) ile doldurulacak alanlar
        self.lat: float = 0.0
        self.lon: float = 0.0
        self.alt: float = 0.0
        self.v_mps: float = 0.0
        self.hdg_rad: float = 0.0
        self.noise_m: float = 0.5
        self.drift_mps: float = 0.0  # m/s
        self._hz: float = 5.0
        self._last_t: Optional[float] = None

        # Hangi moddayız? ("sim" mi, "csharp_sim" mi)
        self._twin_mode: bool = False

    # -------- Lifecycle --------

    def open(self, cfg: GpsConfig) -> None:
        # backend alanına göre mod seç
        backend_name = getattr(cfg, "backend", "sim") or "sim"
        backend_name = str(backend_name).lower()
        self._twin_mode = (backend_name == "csharp_sim")

        # Eğer twin moddaysak, konum parametreleri sadece "fallback" olur;
        # aslında TwinBus'tan gelen GPS fix'ini kullanacağız.
        self.lat = float(_cfg_get(cfg, "sim_start_lat", "start_lat_deg", "lat0", default=41.0224))
        self.lon = float(_cfg_get(cfg, "sim_start_lon", "start_lon_deg", "lon0", default=28.8321))
        self.alt = float(_cfg_get(cfg, "sim_start_alt", "start_alt_m", "alt0", default=40.0))

        self.v_mps = float(_cfg_get(cfg, "sim_speed_mps", "speed_mps", default=1.0))
        self.hdg_rad = math.radians(float(_cfg_get(cfg, "sim_heading_deg", "heading_deg", default=90.0)))

        self.noise_m = float(_cfg_get(cfg, "sim_noise_m", "noise_m", default=0.8))
        drift_mpm = float(_cfg_get(cfg, "sim_drift_mpm", "drift_mpm", default=0.0))
        self.drift_mps = drift_mpm / 60.0

        self._hz = float(getattr(cfg, "rate_hz", 5.0) or 5.0)
        self._last_t = time.time()

    def set_rate_hz(self, hz: float) -> bool:
        try:
            self._hz = float(hz)
            return True
        except Exception:
            return False

    def close(self) -> None:
        self._last_t = None

    # -------- I/O --------

    def _read_fix_twin(self) -> Dict[str, Any]:
        """
        C# dijital ikizinden gelen TwinGps verisini kullanır.
        TwinBus üzerinde taze bir kayıt yoksa "no-fix" döndürür.
        """
        fix = TwinBus.get_gps_fix(max_age_s=1.0)

        if fix is None:
            # Taze twin verisi yoksa, kesinlikle random üretmiyoruz.
            # GpsSensor tarafı bunu "no-fix" olarak algılar.
            return {
                "lat": None,
                "lon": None,
                "alt": None,
                "fix": 0,
                "hdop": None,
                "t_gps": None,
            }

        # TwinBus, lat/lon/alt/fix/hdop/t_gps alanlarını zaten normalize ediyor.
        return fix

    def _read_fix_local_sim(self, now: float) -> Dict[str, Any]:
        """
        Eski lokal sim davranışı ("sim" backend’i).
        """
        if self._last_t is None:
            self._last_t = now
        dt = max(0.0, now - self._last_t)
        self._last_t = now

        # Sabit hız + başlık vektörüyle ilerleme
        dx = self.v_mps * math.cos(self.hdg_rad) * dt
        dy = self.v_mps * math.sin(self.hdg_rad) * dt

        # Çok düşük genlikte rastgele sürüklenme
        dx += (random.uniform(-1.0, 1.0) * self.drift_mps * dt)
        dy += (random.uniform(-1.0, 1.0) * self.drift_mps * dt)

        dlat, dlon = _meters_to_deg(self.lat, dx, dy)
        self.lat += dlat
        self.lon += dlon

        # Ölçüm gürültüsü (metre → derece)
        nx = random.gauss(0.0, self.noise_m)
        ny = random.gauss(0.0, self.noise_m)
        nlat, nlon = _meters_to_deg(self.lat, nx, ny)

        # Basit fix & hdop modeli
        fix = 3 if self.v_mps >= 0.0 else 2
        hdop = max(0.6, 0.6 + 0.1 * random.random())

        return {
            "lat": self.lat + nlat,
            "lon": self.lon + nlon,
            "alt": self.alt,
            "fix": fix,
            "hdop": hdop,
            "t_gps": now,  # sim için host zamanı yeterli
        }

    def read_fix(self) -> Dict[str, Any]:
        now = time.time()

        # Dijital ikiz / C# twin modu
        if self._twin_mode:
            return self._read_fix_twin()

        # Klasik lokal sim modu
        return self._read_fix_local_sim(now)
