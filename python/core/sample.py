# core/sample.py
import time
import json
from typing import Any, Dict, Optional

class Sample:
    """
    Hydronom veri felsefesine göre her sensörün ürettiği standart örnek.

    Şema (schema_version = "1.1.0"):
      sensor:   str        # "imu" | "gps" | "lidar" | "vision" | ...
      source:   str        # sensör kaynağı/portu/cihaz adı ("imu0","gps0"...)
      data:     dict       # sensöre özgü payload (örn. imu: {"gz":rad/s}, gps: {"lat","lon"...})
      t:        float      # host epoch saniye
      seq:      int        # artan örnek numarası (varsayılan: ms epoch)
      frame_id: str        # koordinat çerçevesi (vars: "base_link")
      quality:  dict       # {"valid": bool, ...} ek sinyaller
      calib_id: str|None   # kalibrasyon seti anahtarı (opsiyonel)
    """

    def __init__(
        self,
        sensor: str,
        source: str,
        data: Dict[str, Any],
        frame_id: str = "base_link",
        quality: Optional[Dict[str, Any]] = None,
        seq: Optional[int] = None,
        t: Optional[float] = None,
        calib_id: Optional[str] = None,
    ):
        # Meta
        self.type: str = "Sample"
        self.schema_version: str = "1.1.0"

        # Zorunlu alanlar
        self.sensor: str = str(sensor)
        self.source: str = str(source)

        # Zaman/ sıra
        self.t: float = float(t) if t is not None else time.time()
        self.seq: int = int(seq) if seq is not None else int(self.t * 1000)

        # Çerçeve ve veri
        self.frame_id: str = str(frame_id) if frame_id else "base_link"
        self.data: Dict[str, Any] = dict(data or {})  # savunmalı kopya

        # Kalite ve kalibrasyon
        q = dict(quality or {})
        if "valid" not in q:
            q["valid"] = True
        self.quality: Dict[str, Any] = q
        self.calib_id: Optional[str] = calib_id

    # --------- Serileştirme ---------

    def to_json(self) -> str:
        """JSON formatına çevirir (tek satır, kompakt)."""
        return json.dumps(
            self.__dict__,
            ensure_ascii=False,
            separators=(",", ":")
        ) + "\n"

    @staticmethod
    def from_json(line: str) -> "Sample":
        """JSON satırını Sample nesnesine dönüştürür."""
        obj = json.loads(line)
        return Sample(
            sensor=obj["sensor"],
            source=obj["source"],
            data=obj.get("data", {}),
            frame_id=obj.get("frame_id", "base_link"),
            quality=obj.get("quality"),
            seq=obj.get("seq"),
            t=obj.get("t"),
            calib_id=obj.get("calib_id"),
        )

    # --------- Kolay oluşturucular (opsiyonel) ---------

    @staticmethod
    def imu(gz_rad_s: float, source: str = "imu0", **extra) -> "Sample":
        """IMU-Z (yaw rate) için pratik oluşturucu."""
        data = {"gz": float(gz_rad_s)}
        data.update(extra)
        return Sample(sensor="imu", source=source, data=data)

    @staticmethod
    def gps(lat: float, lon: float, hdop: float = 1.0, source: str = "gps0", **extra) -> "Sample":
        """GPS için pratik oluşturucu."""
        data = {"lat": float(lat), "lon": float(lon), "hdop": float(hdop)}
        data.update(extra)
        return Sample(sensor="gps", source=source, data=data)

    @staticmethod
    def lidar(ranges, angle_min: float, angle_increment: float, source: str = "lidar0", **extra) -> "Sample":
        """LiDAR LaserScan için pratik oluşturucu."""
        data = {
            "ranges": list(ranges),
            "angle_min": float(angle_min),
            "angle_increment": float(angle_increment),
        }
        data.update(extra)
        return Sample(sensor="lidar", source=source, data=data)

    @staticmethod
    def vision(detections, source: str = "cam0", **extra) -> "Sample":
        """Görsel algılama için pratik oluşturucu."""
        data = {"detections": list(detections)}
        data.update(extra)
        return Sample(sensor="vision", source=source, data=data)
