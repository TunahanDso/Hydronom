# sensors/lidar/backends/rplidar.py

import os
import math
import time
from typing import List, Tuple, Optional, Iterable

try:
    from rplidar import RPLidar, RPLidarException  # pip install rplidar
except Exception:  # paket yoksa graceful fallback
    RPLidar = None

# Ortak seri port bulucu (tüm sensörler için aynı modül)
try:
    from sensors.common.port_locator import find_serial_port
except Exception:
    find_serial_port = None  # yoksa kendi basit otodetect'imize düşer


class RPLidarBackend:
    """
    RPLIDAR A1/A2/A3 backend.
    - read_scan() → [(x,y), ...] (metre, gövde ekseni; +X ileri, +Y iskele)
    - LidarSensor isterse bunu ranges[] ızgarasına dönüştürür; ayrıca read_ranges(...) da sunuyoruz.
    """

    def __init__(self, port: Optional[str] = None, baud: Optional[int] = None):
        self.port = port
        self.baud = int(baud or 115200)
        self._lidar: Optional["RPLidar"] = None
        self._scan_iter = None
        self._opened = False
        self._last_scan_time = 0.0

        # Opsiyonel konfig (open(cfg) ile gelebilir)
        self.cfg = None  # LidarConfig veya benzeri (fov_deg, min/max range vs.)

    # ------------ Lifecycle ------------

    def open(self, cfg: Optional[object] = None, *args, **kwargs) -> None:
        """
        cfg: LidarConfig benzeri bir nesne olabilir. (fov_deg, min/max, port, baud varsayılanları)
        """
        if RPLidar is None:
            raise ImportError("RPLIDAR backend için 'rplidar' paketi gerekli. Kurulum: pip install rplidar")

        self.cfg = cfg

        # Port/baud öncelik sırası: parametre -> cfg -> env -> autodetect -> fallback
        port = (
            self.port
            or (getattr(cfg, "port", None) if cfg else None)
            or os.getenv("HYDRONOM_LIDAR_PORT")
        )

        baud = int(
            self.baud
            or (getattr(cfg, "baud", None) if cfg and getattr(cfg, "baud", None) else 115200)
        )

        if not port:
            port = self._autodetect_port()

        self.port, self.baud = port, baud

        # Bağlan
        self._lidar = RPLidar(self.port, baudrate=self.baud, timeout=3)
        # Motor
        try:
            self._lidar.start_motor()
        except Exception:
            pass
        # Buffer ısınması
        time.sleep(0.2)
        # Tam tarama iterator’ü
        # max_buf_meas değerini yüksek tutmak, eksik dönüşleri azaltır
        self._scan_iter = self._lidar.iter_scans(max_buf_meas=6000)
        self._opened = True

    def close(self) -> None:
        if not self._lidar:
            return
        try:
            try:
                self._lidar.stop()
            except Exception:
                pass
            try:
                self._lidar.stop_motor()
            except Exception:
                pass
        finally:
            try:
                self._lidar.disconnect()
            except Exception:
                pass
            self._lidar = None
            self._scan_iter = None
            self._opened = False

    # ------------ I/O ------------

    def read_scan(self, timeout_s: Optional[float] = None) -> List[Tuple[float, float]]:
        """
        Tek “full scan” alır ve filtrelenmiş XY nokta listesi (m) döndürür.
        Filtreler:
          - kalite>0
          - min_range_m <= r <= max_range_m
          - |a| <= fov_deg/2  (a: [-180,180] normalize)
        """
        if not self._opened or self._scan_iter is None:
            raise RuntimeError("RPLidar açılmadı (open() çağrılmalı).")

        deadline = time.time() + (timeout_s or 0.0)
        while True:
            try:
                raw_scan = next(self._scan_iter)
                self._last_scan_time = time.time()
                return self._to_xy(raw_scan)
            except StopIteration:
                # teorik olarak iter_scans generator'ü stop edebilir; küçük bir bekleme ile deneyelim
                time.sleep(0.01)
            except RPLidarException as e:
                # Geçici okuma hataları için kısa bekleme
                time.sleep(0.02)
                if (timeout_s is not None) and (time.time() > deadline):
                    return []
            except Exception:
                if (timeout_s is not None) and (time.time() > deadline):
                    return []
                time.sleep(0.01)

    def read_ranges(
        self,
        *args,
        **kwargs,
    ) -> List[float]:
        """
        İki çağrı şekli:
          read_ranges(n, range_min, range_max, timeout_s=None)
          read_ranges(angle_min=..., angle_max=..., angle_increment=..., range_min=..., range_max=..., timeout_s=None)
        """
        # --- kwargs (zengin) imzası mı? ---
        if "angle_min" in kwargs:
            angle_min = float(kwargs["angle_min"])
            angle_max = float(kwargs["angle_max"])
            angle_inc = float(kwargs["angle_increment"])
            range_min = float(kwargs["range_min"])
            range_max = float(kwargs["range_max"])
            timeout_s = kwargs.get("timeout_s", None)

            n = int(round((angle_max - angle_min) / angle_inc)) + 1
        else:
            # positional: (n, range_min, range_max[, timeout_s])
            if len(args) < 3:
                raise TypeError("read_ranges expects either kwargs form or (n, range_min, range_max[, timeout_s])")
            n = int(args[0])
            range_min = float(args[1])
            range_max = float(args[2])
            timeout_s = args[3] if len(args) >= 4 else None

            # Varsayılan 270° FOV ve bu n’e göre inc tahmini (LidarSensor zaten doğru açıları gönderir)
            angle_min = -math.radians(270.0) / 2.0
            angle_max = +math.radians(270.0) / 2.0
            angle_inc = (angle_max - angle_min) / max(1, (n - 1))

        pts = self.read_scan(timeout_s=timeout_s)
        return self._bin_points_to_ranges(pts, angle_min, angle_max, angle_inc, range_min, range_max)

    # ------------ Helpers ------------

    def _to_xy(self, scan_rows: Iterable[Tuple[int, float, float]]) -> List[Tuple[float, float]]:
        """
        iter_scans satırlarını XY (metre) listesine çevirir.
        scan_rows elemanları: (quality:int, angle_deg:float, distance_mm:float)
        """
        pts: List[Tuple[float, float]] = []

        # cfg varsa al, yoksa makul varsayılanlar
        min_r = float(getattr(self.cfg, "range_min", getattr(self.cfg, "min_range_m", 0.10)) if self.cfg else 0.10)
        max_r = float(getattr(self.cfg, "range_max", getattr(self.cfg, "max_range_m", 30.0)) if self.cfg else 30.0)
        fov = float(getattr(self.cfg, "fov_deg", 360.0) if self.cfg else 360.0)
        half = fov * 0.5

        for row in scan_rows:
            try:
                q, ang_deg, dist_mm = row
            except Exception:
                # Bazı rplidar sürümlerinde tuple farklı gelebilir; atla
                continue
            if q <= 0:
                continue

            r = float(dist_mm) * 0.001  # mm → m
            if r < min_r or r > max_r:
                continue

            # Açıyı [-180,180] normalize et
            a = ((float(ang_deg) + 180.0) % 360.0) - 180.0
            if abs(a) > half:
                continue

            rad = math.radians(a)
            x = r * math.cos(rad)
            y = r * math.sin(rad)
            pts.append((x, y))

        return pts

    def _bin_points_to_ranges(
        self,
        points: Iterable[Tuple[float, float]],
        angle_min: float,
        angle_max: float,
        angle_inc: float,
        range_min: float,
        range_max: float,
    ) -> List[float]:
        """(x,y) noktalarını sabit açısal ızgaraya (ranges[]) dönüştürür."""
        n = int(round((angle_max - angle_min) / angle_inc)) + 1
        ranges = [math.inf] * n

        for (x, y) in points:
            r = math.hypot(x, y)
            if r <= 0 or r < range_min or r > range_max:
                continue
            theta = math.atan2(y, x)
            k = int((theta - angle_min) / angle_inc)
            if 0 <= k < n:
                if r < ranges[k]:
                    ranges[k] = r

        out: List[float] = []
        for v in ranges:
            out.append(0.0 if math.isinf(v) else float(v))
        return out

    def _autodetect_port(self) -> str:
        """
        Port keşfi:
        - Önce sensors.common.port_locator.find_serial_port() varsa onu kullan.
        - Yoksa Linux by-id → ttyUSB/ttyACM → Windows COM3..COM20 adayları.
        """
        if callable(find_serial_port):
            port = find_serial_port(
                contains=["lidar", "rplidar", "silabs", "cp210", "wch", "ftdi", "prolific", "ch340"]
            )
            if port:
                return port

        # Linux'ta stable isimler
        by_id = "/dev/serial/by-id"
        if os.path.isdir(by_id):
            try:
                for name in os.listdir(by_id):
                    low = name.lower()
                    if any(key in low for key in ("lidar", "rplidar", "silabs", "cp210", "wch", "ftdi", "pl2303", "ch340")):
                        return os.path.join(by_id, name)
            except Exception:
                pass

        candidates = [
            "/dev/ttyUSB1",
            "/dev/ttyUSB0",
            "/dev/ttyACM0",
        ]
        # Windows
        for n in range(3, 21):
            candidates.append(f"COM{n}")

        for p in candidates:
            try:
                # COM varlık kontrolü güvenilir değil; ilk makul aday döndürülür
                if p.startswith("COM") or os.path.exists(p):
                    return p
            except Exception:
                continue

        # Son çare
        return "/dev/ttyUSB1"
