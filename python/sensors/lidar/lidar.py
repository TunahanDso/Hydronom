# sensors/lidar/lidar.py
import math
from typing import Optional, List, Iterable, Tuple, Union

from sensors.base_sensor import BaseSensor
from sensors.lidar.config import LidarConfig
from sensors.lidar.laser_scan import LaserScanSample


def _make_backend(name: str):
    if name == "sim":
        from sensors.lidar.backends.sim import SimBackend
        return SimBackend()
    elif name == "ldrobot":  # <-- YENİ EKLENEN KISIM
        from sensors.lidar.backends.ldrobot import LDRobotBackend
        return LDRobotBackend()
    elif name == "rplidar":
        from sensors.lidar.backends.rplidar import RPLidarBackend
        return RPLidarBackend()
    elif name == "ouster":
        from sensors.lidar.backends.ouster import OusterBackend
        return OusterBackend()
    raise ValueError(f"Unknown LiDAR backend: {name}")


class LidarSensor(BaseSensor):
    """Ana taşıyıcı: Backend’i yönetir, LaserScan üretir (ranges[])."""
    kind = "lidar"

    def __init__(self, cfg: Optional[LidarConfig] = None):
        cfg = cfg or LidarConfig()
        super().__init__(source=cfg.source, frame_id=cfg.frame_id, simulate=(cfg.backend == "sim"))
        self.cfg = cfg
        self._backend = _make_backend(cfg.backend)
        self.enabled = True  # SensorManager bu alanı okuyor
        self._rate_hz = float(getattr(cfg, "rate_hz", 10.0) or 10.0)

        # Backend hız ayarı destekliyorsa kurulumda ilet
        if hasattr(self._backend, "set_rate_hz"):
            try:
                self._backend.set_rate_hz(self._rate_hz)
            except Exception:
                pass

    # --- Runtime konfig (SensorManager.apply_stream_subscribe ile uyumlu) ---

    def set_rate_hz(self, hz: float) -> None:
        """Yayın/okuma hedef hızını (Hz) ayarla; cfg ve backend’e yansıt."""
        self._rate_hz = max(0.1, float(hz))
        # cfg tarafında da sakla (capability’de görünsün)
        try:
            self.cfg.rate_hz = self._rate_hz
        except Exception:
            pass
        # Backend’e de yansıt (varsa)
        if hasattr(self._backend, "set_rate_hz"):
            try:
                self._backend.set_rate_hz(self._rate_hz)
            except Exception:
                pass

    def get_rate_hz(self) -> float:
        return self._rate_hz

    def _switch_backend(self, name: str) -> None:
        """Çalışırken backend değiştirmeyi destekler."""
        if name == self.cfg.backend:
            return
        # Eski backend’i kapat
        try:
            self._backend.close()
        except Exception:
            pass
        # Yeni backend’i hazırla
        self._backend = _make_backend(name)
        self.cfg.backend = name
        # Yeni backend’e mevcut hızı ilet
        if hasattr(self._backend, "set_rate_hz"):
            try:
                self._backend.set_rate_hz(self._rate_hz)
            except Exception:
                pass
        # Açık ise yeniden aç
        if self.is_open:
            self.open()

    def apply_stream_subscribe(self, spec: dict) -> dict:
        if isinstance(spec, dict):
            if "enable" in spec:
                self.enabled = bool(spec["enable"])
            if "enabled" in spec:
                self.enabled = bool(spec["enabled"])
            if "rate_hz" in spec:
                try:
                    self.set_rate_hz(float(spec["rate_hz"]))
                except Exception:
                    pass
            # tolerans: "hz" alanını da kabul et
            if "hz" in spec and "rate_hz" not in spec:
                try:
                    self.set_rate_hz(float(spec["hz"]))
                except Exception:
                    pass
            # backend değişimi (hot-swap)
            if "backend" in spec and isinstance(spec["backend"], str):
                try:
                    self._switch_backend(spec["backend"].strip().lower())
                except Exception:
                    pass
            # LiDAR özgü parametreler (cfg üzerinde sakla)
            for k in ("fov_deg", "angle_increment_deg", "range_min", "range_max", "timeout_s"):
                if k in spec:
                    try:
                        setattr(self.cfg, k, float(spec[k]))
                    except Exception:
                        try:
                            setattr(self.cfg, k, spec[k])
                        except Exception:
                            pass

        return {
            "enabled": self.enabled,
            "rate_hz": self._rate_hz,
            "backend": self.cfg.backend,
            "fov_deg": self.cfg.fov_deg,
            "angle_increment_deg": self.cfg.angle_increment_deg,
            "range_min": self.cfg.range_min,
            "range_max": self.cfg.range_max,
        }

    # --- Yaşam Döngüsü ---

    def open(self):
        """Backend’i aç. open(cfg) imzası varsa onu kullan."""
        try:
            self._backend.open(self.cfg)  # yeni imza
        except TypeError:
            self._backend.open()          # geriye dönük imza
        # Açılıştan sonra da hızı backend’e ilet (backend open sırasında sıfırlayabilir)
        if hasattr(self._backend, "set_rate_hz"):
            try:
                self._backend.set_rate_hz(self._rate_hz)
            except Exception:
                pass
        self.is_open = True

    def close(self):
        try:
            self._backend.close()
        finally:
            self.is_open = False

    # --- İç yardımcılar ---

    def _bin_points_to_ranges(
        self,
        points: Iterable[Union[Tuple[float, float], Tuple[float, float, float]]],
        angle_min: float,
        angle_max: float,
        angle_inc: float,
        range_min: float,
        range_max: float,
    ) -> List[float]:
        """
        Çeşitli nokta formatlarını (x,y[,z]) veya (angle,r) → sabit açısal ızgaraya (ranges[]) dönüştürür.
        """
        n = int(round((angle_max - angle_min) / angle_inc)) + 1
        ranges = [math.inf] * n

        def add_hit(theta: float, r: float):
            if r <= 0 or r < range_min or r > range_max:
                return
            k = int((theta - angle_min) / angle_inc)
            if 0 <= k < n:
                if r < ranges[k]:
                    ranges[k] = r

        for p in points:
            if isinstance(p, (tuple, list)):
                if len(p) == 2:
                    a, b = float(p[0]), float(p[1])
                    # Heuristik: |a|<=pi && b>=0 ise (angle,r); aksi (x,y)
                    if -math.pi <= a <= math.pi and b >= 0:
                        add_hit(a, b)
                    else:
                        x, y = a, b
                        add_hit(math.atan2(y, x), math.hypot(x, y))
                elif len(p) >= 3:
                    x, y = float(p[0]), float(p[1])
                    add_hit(math.atan2(y, x), math.hypot(x, y))

        # inf → 0.0 (okuma yok)
        out = []
        for v in ranges:
            out.append(0.0 if math.isinf(v) else float(v))
        return out

    def _sanitize_ranges(self, arr: Iterable[float], rmin: float, rmax: float) -> List[float]:
        out = []
        for r in arr:
            try:
                r = float(r)
            except Exception:
                r = 0.0
            if not math.isfinite(r) or r < 0:
                r = 0.0
            elif r > 0.0:
                r = min(max(r, rmin), rmax)
            out.append(r)
        return out

    # --- Okuma ---

    def read(self) -> LaserScanSample:
        if not self.is_open:
            raise RuntimeError("LiDAR açılmadı.")

        # Açı ızgarası
        angle_min = -math.radians(self.cfg.fov_deg) / 2.0
        angle_inc = math.radians(self.cfg.angle_increment_deg)
        angle_max = +math.radians(self.cfg.fov_deg) / 2.0
        n = int(round((angle_max - angle_min) / angle_inc)) + 1

        # Rate'e göre makul timeout: cfg.timeout_s verilmişse onu kullan; yoksa 0.9/rate
        timeout_s = getattr(self.cfg, "timeout_s", None)
        if not isinstance(timeout_s, (int, float)) or timeout_s <= 0:
            timeout_s = 0.9 / max(self._rate_hz, 0.1)

        # Backend’ten ranges alma: öncelik read_ranges, değilse read_scan + binning
        ranges: Optional[List[float]] = None
        if hasattr(self._backend, "read_ranges"):
            try:
                # Zengin imza (adlı argümanlar)
                ranges = self._backend.read_ranges(
                    angle_min=angle_min,
                    angle_max=angle_max,
                    angle_increment=angle_inc,
                    range_min=self.cfg.range_min,
                    range_max=self.cfg.range_max,
                    timeout_s=timeout_s,
                )
            except TypeError:
                # Basit imzalar: n, rmin, rmax, [timeout]
                try:
                    ranges = self._backend.read_ranges(
                        n, self.cfg.range_min, self.cfg.range_max, timeout_s
                    )
                except TypeError:
                    ranges = self._backend.read_ranges(n, self.cfg.range_min, self.cfg.range_max)
        elif hasattr(self._backend, "read_scan"):
            pts = self._backend.read_scan(timeout_s=timeout_s)
            ranges = self._bin_points_to_ranges(
                pts, angle_min, angle_max, angle_inc, self.cfg.range_min, self.cfg.range_max
            )
        else:
            raise RuntimeError("LiDAR backend, ne read_ranges ne de read_scan sağlıyor.")

        ranges = ranges or []
        # Boyutu ızgaraya uydur (eksikse doldur, fazlaysa kes)
        if len(ranges) < n:
            ranges = list(ranges) + [0.0] * (n - len(ranges))
        elif len(ranges) > n:
            ranges = list(ranges[:n])

        # Temizle/Clamp et
        ranges = self._sanitize_ranges(ranges, self.cfg.range_min, self.cfg.range_max)

        seq = self._new_seq()
        return LaserScanSample(
            ranges=ranges,
            angle_min=angle_min,
            angle_increment=angle_inc,
            range_min=self.cfg.range_min,
            range_max=self.cfg.range_max,
            sensor="lidar",
            source=self.source,
            frame_id=self.frame_id,
            seq=seq,
            t=self._stamp(),
        )
