# eval/evaluator.py
import time
from typing import List, Optional, Dict, Any
from core.event import Event

def _to_float(val: Any, default: Optional[float] = None) -> Optional[float]:
    """Güvenli float dönüşümü: None/boş/çevrilemeyen değerler için default döner."""
    if val is None:
        return default
    try:
        return float(val)
    except (TypeError, ValueError):
        return default


class Evaluator:
    def __init__(self, sensor_timeout_s: float = 2.0, hdop_warn: float = 2.5, hdop_crit: float = 5.0):
        self.sensor_timeout_s = float(sensor_timeout_s)
        self.hdop_warn = float(hdop_warn)
        self.hdop_crit = float(hdop_crit)

        # Son görülme zamanları (sensor adı -> epoch saniye)
        self._last_seen: Dict[str, float] = {}

        # GPS yoksa dead-reckoning modunu işaretler
        self._dr_mode = False

        # Bu karede üretilen olaylar
        self._events: List[Event] = []

    def update(self, samples: List[object], fused_state: Optional[object]):
        """
        samples: Sample benzeri objeler (sensor, source, data, t vs.)
        fused_state: FusedState veya None
        """
        now = time.time()
        self._events.clear()

        # FusedState zaman damgasını (varsa) çıkar
        fused_t = getattr(fused_state, "t", None)

        # 1) Örnekleri işle: last_seen güncelle + GPS kalite kontrolü
        for s in samples:
            sensor_name = getattr(s, "sensor", None)
            if not sensor_name:
                continue

            # Sensörün son görülme zamanı
            self._last_seen[sensor_name] = now

            # GPS özel: HDOP kontrolü
            if sensor_name == "gps":
                data = getattr(s, "data", {}) or {}
                hdop = _to_float(data.get("hdop"), None)

                # hdop sayısal ise eşiğe göre olay üret
                if hdop is not None:
                    evt_ctx = [{"type": "FusedState", "t": fused_t}] if fused_state else []
                    src = getattr(s, "source", "gps0")
                    if hdop > self.hdop_crit:
                        self._events.append(
                            Event(
                                "gps-hdop-high",
                                {"hdop": hdop, "threshold": self.hdop_crit, "source": src},
                                "crit",
                                evt_ctx,
                            )
                        )
                    elif hdop > self.hdop_warn:
                        self._events.append(
                            Event(
                                "gps-hdop-high",
                                {"hdop": hdop, "threshold": self.hdop_warn, "source": src},
                                "warn",
                                evt_ctx,
                            )
                        )
                # hdop None ise sessizce atla (patlama yok)

        # 2) Sensör time-out kontrolleri
        # İstersen buraya yeni sensör adları ekleyebilirsin
        for sensor_name in ("gps", "imu", "camera", "lidar"):
            last = self._last_seen.get(sensor_name)
            if last is None:
                continue
            dt = now - last
            if dt > self.sensor_timeout_s:
                evt_ctx = [{"type": "FusedState", "t": fused_t}] if fused_state else []
                self._events.append(
                    Event(
                        "sensor-timeout",
                        {"sensor": sensor_name, "last_seen_s": round(dt, 2)},
                        "warn",
                        evt_ctx,
                    )
                )

        # 3) Dead-reckoning (DR) modu geçişleri: GPS akışı kesilirse gir, gelince çık
        gps_last = self._last_seen.get("gps")
        gps_ok = (gps_last is not None) and ((now - gps_last) <= self.sensor_timeout_s)

        if not gps_ok and not self._dr_mode:
            self._dr_mode = True
            self._events.append(Event("dead-reckoning-entered", {}, "warn", []))
        elif gps_ok and self._dr_mode:
            self._dr_mode = False
            self._events.append(Event("dead-reckoning-left", {}, "info", []))

    def emit_events(self) -> List[Event]:
        """Bu karede biriken olayları (kopyasını) döndürür."""
        return list(self._events)
