# sensors/imu/imu.py
import time
from typing import Optional, Dict, Any

from sensors.base_sensor import BaseSensor
from sensors.imu.config import ImuConfig

try:
    from core.sample import Sample
except Exception:
    Sample = None  # BaseSensor._make_sample yoksa fallback için kullanacağız


def _make_backend(name: str):
    name = (name or "sim").lower()
    # Not: "csharp_sim" de SimBackend kullanır; ayrımı backend içinde cfg.backend ile yapacağız.
    if name in ("sim", "csharp_sim"):
        from sensors.imu.backends.sim import SimBackend
        return SimBackend()
    elif name == "serial":
        from sensors.imu.backends.serial import SerialBackend
        return SerialBackend()
    raise ValueError(f"Unknown IMU backend: {name}")


class ImuSensor(BaseSensor):
    """IMU taşıyıcı: backend’ten gelen ham ölçümü standart Sample’a çevirir (non-blocking)."""
    kind = "imu"

    def __init__(self, cfg: Optional[ImuConfig] = None):
        self.cfg = cfg or ImuConfig()
        super().__init__(
            source=self.cfg.source,
            frame_id=self.cfg.frame_id,
            # Hem klasik sim hem de C# twin sim "simulate" sayılıyor
            simulate=(self.cfg.backend in ("sim", "csharp_sim")),
        )
        self._backend = _make_backend(self.cfg.backend)
        self.enabled = True
        self._seq_local = int(time.time() * 1000)

        # Backend örnekleme hızını ilet (varsa)
        if hasattr(self._backend, "set_rate_hz"):
            try:
                self._backend.set_rate_hz(float(getattr(self.cfg, "rate_hz", 0.0) or 0.0))  # type: ignore[attr-defined]
            except Exception:
                pass

    # ---------------- Fallback yardımcılar (BaseSensor'da yoksa) ----------------

    def _new_seq(self) -> int:
        if hasattr(super(), "_new_seq"):
            try:
                # type: ignore[attr-defined]
                return super()._new_seq()
            except Exception:
                pass
        self._seq_local += 1
        return self._seq_local

    def _stamp(self) -> float:
        if hasattr(super(), "_stamp"):
            try:
                # type: ignore[attr-defined]
                return super()._stamp()
            except Exception:
                pass
        return time.time()

    def _make_sample(
        self,
        *,
        sensor: str,
        data: Dict[str, Any],
        quality: Dict[str, Any],
        seq: Optional[int] = None,
        t: Optional[float] = None,
        calib_id: Optional[str] = None,
    ):
        # Önce BaseSensor’ın üreticisini dene
        if hasattr(super(), "_make_sample"):
            try:
                # type: ignore[attr-defined]
                return super()._make_sample(
                    sensor=sensor, data=data, quality=quality, seq=seq, t=t, calib_id=calib_id
                )
            except Exception:
                pass
        # Fallback: core.sample.Sample kullan
        if Sample is None:
            raise RuntimeError("core.sample.Sample import edilemedi ve BaseSensor._make_sample bulunamadı.")
        return Sample(
            sensor=sensor,
            source=self.cfg.source,
            data=data,
            frame_id=self.cfg.frame_id,
            quality=quality,
            seq=seq or self._new_seq(),
            t=t or self._stamp(),
            calib_id=calib_id,
        )

    # ---------------- İsteğe bağlı runtime API ----------------

    def set_rate_hz(self, hz: float):
        """Hedef örnekleme/okuma hızı (varsa backend’e iletir)."""
        try:
            self.cfg.rate_hz = float(hz)
            if hasattr(self._backend, "set_rate_hz"):
                try:
                    self._backend.set_rate_hz(self.cfg.rate_hz)  # type: ignore[attr-defined]
                except Exception:
                    pass
        except Exception:
            pass

    @property
    def port(self) -> Optional[str]:
        return getattr(self.cfg, "port", None)

    @port.setter
    def port(self, v: Optional[str]) -> None:
        if hasattr(self.cfg, "port"):
            self.cfg.port = v  # type: ignore[attr-defined]

    @property
    def baud(self) -> Optional[int]:
        return getattr(self.cfg, "baud", None)

    @baud.setter
    def baud(self, v: Optional[int]) -> None:
        if hasattr(self.cfg, "baud") and v is not None:
            self.cfg.baud = int(v)  # type: ignore[attr-defined]

    def apply_stream_subscribe(self, spec: dict) -> dict:
        """enable/rate/backend/port/baud gibi alanları toleranslı uygular."""
        changed: Dict[str, Any] = {}
        if not isinstance(spec, dict):
            return changed

        if "enable" in spec or "enabled" in spec:
            self.enabled = bool(spec.get("enable", spec.get("enabled", self.enabled)))
            changed["enabled"] = self.enabled

        if "rate_hz" in spec or "hz" in spec:
            try:
                hz = float(spec.get("rate_hz", spec.get("hz")))
                self.set_rate_hz(hz)
                changed["rate_hz"] = self.cfg.rate_hz
            except Exception:
                pass

        if "backend" in spec:
            new_backend = str(spec["backend"]).strip().lower()
            # C# twin sim için "csharp_sim" de destekleniyor
            if new_backend in ("sim", "csharp_sim", "serial") and new_backend != self.cfg.backend:
                was_open = self.is_open
                try:
                    if was_open and hasattr(self._backend, "close"):
                        self._backend.close()
                except Exception:
                    pass
                self.cfg.backend = new_backend
                # simulate flag’i hem sim hem csharp_sim için true
                self.simulate = (new_backend in ("sim", "csharp_sim"))
                self._backend = _make_backend(new_backend)
                try:
                    if was_open:
                        # open(cfg) veya open() imzalarına uy
                        try:
                            self._backend.open(self.cfg)
                        except TypeError:
                            self._backend.open()
                        if hasattr(self._backend, "set_rate_hz"):
                            self._backend.set_rate_hz(float(getattr(self.cfg, "rate_hz", 0.0) or 0.0))  # type: ignore[attr-defined]
                except Exception:
                    pass
                changed["backend"] = new_backend

        if "port" in spec:
            try:
                self.port = None if spec["port"] in (None, "", "none", "off") else str(spec["port"])
                changed["port"] = self.port
            except Exception:
                pass
        if "baud" in spec:
            try:
                self.baud = int(spec["baud"])
                changed["baud"] = self.baud
            except Exception:
                pass

        return changed

    # ---------------- Yaşam Döngüsü ----------------

    def open(self):
        # open(cfg) varsa onu, yoksa argsız open()
        try:
            self._backend.open(self.cfg)
        except TypeError:
            self._backend.open()
        # hız parametresini tekrar ilet (destekliyorsa)
        if hasattr(self._backend, "set_rate_hz"):
            try:
                self._backend.set_rate_hz(float(getattr(self.cfg, "rate_hz", 0.0) or 0.0))  # type: ignore[attr-defined]
            except Exception:
                pass
        self.is_open = True

    def read(self):
        if not self.is_open:
            raise RuntimeError("IMU açılmadı.")

        # read_imu() varsa onu kullan; yoksa read()
        try:
            if hasattr(self._backend, "read_imu"):
                m: Dict[str, Any] = self._backend.read_imu() or {}
            else:
                m = self._backend.read() or {}
        except Exception as e:
            return self._make_sample(
                sensor="imu",
                data={"error": str(e)},
                quality={"valid": False, "backend": self.cfg.backend},
                seq=self._new_seq(),
                t=self._stamp(),
            )

        def f(key):
            v = m.get(key)
            try:
                return float(v) if v is not None else None
            except Exception:
                return None

        ax, ay, az = f("ax"), f("ay"), f("az")
        gx, gy, gz = f("gx"), f("gy"), f("gz")
        mx, my, mz = f("mx"), f("my"), f("mz")
        temp_c = f("temp_c")

        t_imu = m.get("t_imu")
        # t_imu yoksa şimdiki zamanı kabul et
        t_imu_num = float(t_imu) if isinstance(t_imu, (int, float)) else self._stamp()
        age_ms = int(max(0.0, (self._stamp() - t_imu_num) * 1000.0))

        data = {
            "ax": ax, "ay": ay, "az": az,
            "gx": gx, "gy": gy, "gz": gz,
            "mx": mx, "my": my, "mz": mz,
            "temp_c": temp_c,
            "t_imu": t_imu_num,
        }

        quality = {
            "valid": all(v is not None for v in (ax, ay, az, gx, gy, gz)),
            "age_ms": age_ms,
            "backend": self.cfg.backend,
            "rate_hz": getattr(self.cfg, "rate_hz", None),
        }

        return self._make_sample(
            sensor="imu",
            data=data,
            quality=quality,
            seq=self._new_seq(),
            t=self._stamp(),
        )

    def close(self):
        try:
            if hasattr(self._backend, "close"):
                self._backend.close()
        finally:
            self.is_open = False
