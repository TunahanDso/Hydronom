# sensors/lidar/backends/ouster.py

import math
import time
from typing import List, Tuple, Optional

try:
    # Ouster SDK: pip install ouster-sdk
    from ouster import client, pcap  # type: ignore
    import numpy as np  # type: ignore
except Exception:
    client = None
    pcap = None
    np = None


class OusterBackend:
    """
    Ouster LiDAR backend (OS0/OS1/OS2).

    Modlar:
      - Canlı ağ sensörü (cfg.lidar_ip [+ cfg.udp_dest])
      - PCAP replay (cfg.pcap_path + cfg.meta_path)

    Çıkış:
      - read_scan() → [(x,y), ...]  (metre; gövde ekseni, +X ileri, +Y iskele)
      - read_ranges(...) → sabit açısal ızgara (ranges[])
    """

    def __init__(self):
        self.cfg = None
        self._source = None           # client.Sensor | pcap.Pcap | None
        self._scans_iter = None       # iterator over client.Scans
        self._meta: Optional["client.SensorInfo"] = None
        self._xyz_lut = None
        self._opened = False

    # ----------------- Lifecycle -----------------

    def open(self, cfg: Optional[object] = None, *_, **__) -> None:
        """
        cfg alanları (opsiyonel):
          - lidar_ip: "10.5.5.87"   (canlı)
          - udp_dest: "0.0.0.0"
          - pcap_path: "/path/to/file.pcap"
          - meta_path: "/path/to/metadata.json"
          - range_min / min_range_m, range_max / max_range_m, fov_deg
        """
        if client is None or np is None:
            raise ImportError("Ouster backend için 'ouster-sdk' ve 'numpy' gerekir. Kurulum: pip install ouster-sdk numpy")

        self.cfg = cfg

        pcap_path: Optional[str] = getattr(cfg, "pcap_path", None) if cfg else None
        meta_path: Optional[str] = getattr(cfg, "meta_path", None) if cfg else None
        lidar_ip: Optional[str] = getattr(cfg, "lidar_ip", None) if cfg else None
        udp_dest: str = getattr(cfg, "udp_dest", "0.0.0.0") if cfg else "0.0.0.0"

        # PCAP modu
        if pcap_path:
            if not meta_path:
                raise ValueError("PCAP kullanımı için 'meta_path' (sensor metadata JSON) gereklidir.")
            with open(meta_path, "r", encoding="utf-8") as f:
                meta_json = f.read()
            self._meta = client.SensorInfo(meta_json)
            self._source = pcap.Pcap(pcap_path, self._meta)
            self._scans_iter = iter(client.Scans(self._source))
        else:
            # Canlı sensör
            if not lidar_ip:
                raise ValueError("Canlı kullanım için 'lidar_ip' gerekir (örn. 10.5.5.87).")
            self._source = client.Sensor(lidar_ip, udp_dest=udp_dest)
            self._meta = self._source.metadata
            self._scans_iter = iter(client.Scans(self._source))

        # XYZ LUT hazırla
        self._xyz_lut = client.XYZLut(self._meta)
        self._opened = True

    def close(self) -> None:
        if not self._opened:
            return
        try:
            if self._source is not None:
                # client.Sensor ve pcap.Pcap ikisi de close() destekler
                try:
                    self._source.close()  # type: ignore[attr-defined]
                except Exception:
                    pass
        finally:
            self._source = None
            self._scans_iter = None
            self._xyz_lut = None
            self._meta = None
            self._opened = False

    # ----------------- I/O -----------------

    def read_scan(self, timeout_s: Optional[float] = None) -> List[Tuple[float, float]]:
        """
        Bir LidarScan alır → 3D noktaları XY düzlemine projeler → filtreler → [(x,y), ...] döndürür.

        Filtreler:
          - range_min <= r <= range_max
          - |atan2(y,x)| <= fov_deg/2
          - NaN/inf ve (x,y)=(0,0) atılır
        """
        if not self._opened or self._scans_iter is None or self._xyz_lut is None:
            raise RuntimeError("Ouster açılmadı (open() çağrılmalı).")

        deadline = time.time() + (timeout_s or 0.0)
        while True:
            try:
                scan = next(self._scans_iter)  # ouster.client.data.LidarScan
                break
            except StopIteration:
                if timeout_s is not None and time.time() > deadline:
                    return []
                time.sleep(0.005)

        # LUT → XYZ (metre)
        try:
            xyz = self._xyz_lut(scan)  # (H, W, 3) float32
        except Exception:
            # Eski API uyumluluğu: RANGE alanı ile
            rng = scan.field(client.ChanField.RANGE)  # (H, W)
            xyz = self._xyz_lut(rng)                  # (H, W, 3)

        pts = xyz.reshape(-1, 3)  # (N, 3)
        x = pts[:, 0]
        y = pts[:, 1]
        # z = pts[:, 2]  # 2D projeksiyon için kullanılmıyor

        # Eşikler (cfg varsa ondan, yoksa varsayılan)
        rmin = float(getattr(self.cfg, "range_min", getattr(self.cfg, "min_range_m", 0.20)) if self.cfg else 0.20)
        rmax = float(getattr(self.cfg, "range_max", getattr(self.cfg, "max_range_m", 100.0)) if self.cfg else 100.0)
        fov_deg = float(getattr(self.cfg, "fov_deg", 360.0) if self.cfg else 360.0)
        half = fov_deg * 0.5

        r = np.sqrt(x * x + y * y)
        a = np.degrees(np.arctan2(y, x))

        mask = (
            np.isfinite(x) & np.isfinite(y) &
            (r >= rmin) & (r <= rmax) &
            (np.abs(a) <= half)
        )

        x_f = x[mask]
        y_f = y[mask]

        if x_f.size == 0:
            return []

        nz = ~((x_f == 0.0) & (y_f == 0.0))
        x_f = x_f[nz]
        y_f = y_f[nz]

        return list(zip(x_f.tolist(), y_f.tolist()))

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
        # kwargs formu
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

            # Varsayılan 270° FOV → angle_inc tahmini (LidarSensor doğru açıları gönderir)
            angle_min = -math.radians(270.0) / 2.0
            angle_max = +math.radians(270.0) / 2.0
            angle_inc = (angle_max - angle_min) / max(1, (n - 1))

        pts = self.read_scan(timeout_s=timeout_s)
        return self._bin_points_to_ranges(pts, angle_min, angle_max, angle_inc, range_min, range_max)

    # ----------------- Helpers -----------------

    def _bin_points_to_ranges(
        self,
        points: List[Tuple[float, float]],
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
