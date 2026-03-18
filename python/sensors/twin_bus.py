from __future__ import annotations

import time
import threading
from typing import Dict, Any, Optional


class TwinBus:
    """
    C# dijital ikiz/twin mesajları için hafif bir cache.

    Beklenen mesaj tipleri:
      - type="TwinGps" → {lat, lon, alt, fix, hdop, t_gps, ...}
      - type="TwinImu" → {ax, ay, az, gx, gy, gz, mx, my, mz, ...}

    main.py içindeki handle_message(msg) TwinBus.update(msg) çağıracak.
    Sim backend'ler de TwinBus.get_gps_fix() / get_imu() ile veriyi okuyacak.
    """

    _gps: Optional[Dict[str, Any]] = None
    _gps_stamp: float = 0.0

    _imu: Optional[Dict[str, Any]] = None
    _imu_stamp: float = 0.0

    _lock = threading.Lock()

    @classmethod
    def update(cls, msg: Dict[str, Any]) -> None:
        """C#'tan gelen twin mesajını cache'e yazar."""
        t = msg.get("type")
        if not t:
            return

        now = time.time()

        with cls._lock:
            if t == "TwinGps":
                # GPS twin mesajı
                cls._gps = dict(msg)
                cls._gps_stamp = now

            elif t == "TwinImu":
                # IMU twin mesajı
                cls._imu = dict(msg)
                cls._imu_stamp = now

    @classmethod
    def get_gps_fix(cls, max_age_s: float = 1.0) -> Optional[Dict[str, Any]]:
        """
        Son TwinGps kaydını döndürür. Kayıt yoksa veya çok eskiyse None döner.
        """
        with cls._lock:
            if cls._gps is None:
                return None
            age = time.time() - cls._gps_stamp
            if age > max_age_s:
                return None

            g = cls._gps

        # Sadece GPS ile ilgili alanları filtreleyelim
        return {
            "lat": g.get("lat"),
            "lon": g.get("lon"),
            "alt": g.get("alt"),
            "fix": g.get("fix", 0),
            "hdop": g.get("hdop"),
            "t_gps": g.get("t_gps"),
        }

    @classmethod
    def get_imu(cls, max_age_s: float = 0.5) -> Optional[Dict[str, Any]]:
        """
        IMU twin verisini döndürür (ham dict). IMU backend'i bunu uygun sample'a çevirecek.
        """
        with cls._lock:
            if cls._imu is None:
                return None
            age = time.time() - cls._imu_stamp
            if age > max_age_s:
                return None
            return dict(cls._imu)
