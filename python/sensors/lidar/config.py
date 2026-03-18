# sensors/lidar/config.py
from dataclasses import dataclass

@dataclass
class LidarConfig:
    """Lidar sensör konfigürasyonu.
    Sim, RPLIDAR veya Ouster gibi farklı backend'lerde ortak kullanılır.
    """

    source: str = "lidar0"
    frame_id: str = "lidar_link"

    # Görüş alanı ve çözünürlük
    fov_deg: float = 270.0
    angle_increment_deg: float = 1.0

    # Menzil sınırları
    range_min: float = 0.15
    range_max: float = 12.0

    # Backend tipi: "sim" | "rplidar" | "ouster"
    backend: str = "sim"

    # Yeni eklenen alan: Sensör yayın hızı (Hz)
    rate_hz: float = 5.0  # SensorManager detect_hardware() bunu gönderiyor
