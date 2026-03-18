# hydroscan/model.py
# ---------------------------------------------------------------------------
# StateFrame veri modeli
#
# Amaç:
#  - Hydronom runtime log satırlarından elde edilen "tek anlık frame"i
#    anlamlı bir Python nesnesi olarak tutmak.
#  - Hem backend (parsing / analiz) hem de frontend (HydroScan JS) için
#    tutarlı alan isimleri sağlamak.
#
# Notlar:
#  - JS tarafı bazı alanları farklı isimlerle bekliyor:
#       roll/pitch/yaw  -> rpy_r / rpy_p / heading_deg
#       time_str        -> time
#    to_dict() bu eşlemeyi otomatik yapıyor.
#  - Limiter alanı parsing tarafında artık "active" / "none" gibi
#    lower-case stringlerle geliyor; model tarafında da buna uyuyoruz.
# ---------------------------------------------------------------------------

from dataclasses import dataclass, field, asdict
from typing import List, Dict, Optional, Any


@dataclass
class StateFrame:
    # Zaman alanları
    t: float                 # epoch seconds veya göreli zaman (s)
    time_str: str            # orijinal timestamp (string, log satırından geldiği gibi)

    # Konum ve yönelim
    pos_x: float
    pos_y: float
    pos_z: float

    roll: float              # X ekseni etrafında dönüş (deg)
    pitch: float             # Y ekseni etrafında dönüş (deg)
    yaw: float               # Z ekseni (heading, deg)

    # Hız bilgisi
    vel_x: float
    vel_y: float
    vel_z: float
    velocity_mag: float      # hız büyüklüğü (m/s)

    # Kontrol komutları
    cmd_thr: float           # throttle komutu [-1..1] veya [0..1]
    cmd_rud: float           # rudder komutu  [-1..1]

    dist_to_target: float    # hedefe uzaklık (m)
    dhead_to_target: float   # hedefe göre baş farkı (deg)
    limiter: str             # "none", "active" vb. limiter özeti

    # Görev / durum
    task_name: str
    target_x: Optional[float]
    target_y: Optional[float]
    obs_ahead_status: str    # "TEMİZ", "ENGEL VAR", "--" vb.

    # Aktüatör / itici çıkışları
    actuators: List[float] = field(default_factory=list)

    # Toplam gövde kuvvet / tork bilgisi
    force_x: float = 0.0
    force_y: float = 0.0
    force_z: float = 0.0
    torque_x: float = 0.0
    torque_y: float = 0.0
    torque_z: float = 0.0

    # Ek alanlar (sensör ve sağlık snapshot'ları)
    # Burada "sensors_snapshot" parser tarafındaki "sensor_snapshot.samples"
    # ile, "health_snapshot" ise "sensor_snapshot.health" ile uyumludur.
    sensors_snapshot: Dict[str, dict] = field(default_factory=dict)  # imu/gps/cam/lidar son sample
    health_snapshot: Dict[str, dict] = field(default_factory=dict)   # sistem sağlık bilgisi

    # Orijinal satır (debug için)
    raw_line: str = ""

    # -----------------------------------------------------------------------
    # Yardımcı metodlar
    # -----------------------------------------------------------------------

    def to_dict(self, include_snapshots: bool = True, include_raw: bool = True) -> Dict[str, Any]:
        """
        StateFrame nesnesini JSON dostu bir dict'e çevirir.

        - JS tarafının beklediği ek alias alanları da eklenir:
            time_str -> time
            roll     -> rpy_r
            pitch    -> rpy_p
            yaw      -> heading_deg

        - sensors_snapshot / health_snapshot / raw_line alanlarını
          istersen include_* parametreleriyle kapatabilirsin.
        """
        d: Dict[str, Any] = asdict(self)

        # Frontend'in doğrudan kullandığı alias alanlar
        d["time"] = self.time_str
        d["rpy_r"] = self.roll
        d["rpy_p"] = self.pitch
        d["heading_deg"] = self.yaw

        # Snapshot alanlarını isteğe bağlı dahil et
        if not include_snapshots:
            d.pop("sensors_snapshot", None)
            d.pop("health_snapshot", None)

        if not include_raw:
            d.pop("raw_line", None)

        return d

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "StateFrame":
        """
        Gerekirse dışarıdan gelen bir dict'ten StateFrame üretmek için
        basit yardımcı. (Şu an zorunlu değil ama ileride iş görebilir.)

        Beklenen key'ler:
          - Bu sınıftaki alan isimleri ile birebir uyumlu olmalı.
          - Eksik alanlar için varsayılanlar kullanılmaz, bu yüzden
            parser tarafında doldurup göndermek daha sağlıklı.

        Geriye dönük uyumluluk:
          - Eski format: "sensors_snapshot" + "health_snapshot"
          - Yeni format: "sensor_snapshot" ->
                { "samples": {...}, "health": {...}, ... }
        """
        # Yeni parser'ın ürettiği birleşik snapshot yapısını oku (varsa)
        sensor_snapshot_full: Dict[str, Any] = data.get("sensor_snapshot", {}) or {}
        # Eski alanları da fallback olarak kullan
        sensors_snapshot = data.get("sensors_snapshot")
        health_snapshot = data.get("health_snapshot")

        # Eğer eski alanlar yoksa yeni yapının içinden türet
        if sensors_snapshot is None:
            samples = sensor_snapshot_full.get("samples") or {}
            # samples dict'i doğrudan sensors_snapshot olarak tutuyoruz
            sensors_snapshot = dict(samples)

        if health_snapshot is None:
            # Yeni yapıda health doğrudan saklanıyor
            health_raw = sensor_snapshot_full.get("health") or {}
            health_snapshot = dict(health_raw)

        return cls(
            t=data["t"],
            time_str=data.get("time_str", data.get("time", "")),

            pos_x=data["pos_x"],
            pos_y=data["pos_y"],
            pos_z=data.get("pos_z", 0.0),

            roll=data.get("roll", data.get("rpy_r", 0.0)),
            pitch=data.get("pitch", data.get("rpy_p", 0.0)),
            yaw=data.get("yaw", data.get("heading_deg", 0.0)),

            vel_x=data.get("vel_x", 0.0),
            vel_y=data.get("vel_y", 0.0),
            vel_z=data.get("vel_z", 0.0),
            velocity_mag=data.get("velocity_mag", 0.0),

            cmd_thr=data.get("cmd_thr", 0.0),
            cmd_rud=data.get("cmd_rud", 0.0),

            dist_to_target=data.get("dist_to_target", 0.0),
            dhead_to_target=data.get("dhead_to_target", 0.0),
            # Yeni parser "active"/"none" gönderiyor; yoksa "none" al.
            limiter=data.get("limiter", "none"),

            task_name=data.get("task_name", "--"),
            target_x=data.get("target_x"),
            target_y=data.get("target_y"),
            obs_ahead_status=data.get("obs_ahead_status", "--"),

            actuators=list(data.get("actuators", [])),

            force_x=data.get("force_x", 0.0),
            force_y=data.get("force_y", 0.0),
            force_z=data.get("force_z", 0.0),
            torque_x=data.get("torque_x", 0.0),
            torque_y=data.get("torque_y", 0.0),
            torque_z=data.get("torque_z", 0.0),

            sensors_snapshot=dict(sensors_snapshot or {}),
            health_snapshot=dict(health_snapshot or {}),
            raw_line=data.get("raw_line", ""),
        )
