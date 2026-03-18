# sensors/gps/config.py
from dataclasses import dataclass

@dataclass
class GpsConfig:
    # Backend: "sim" | "nmea" | "gpsd"
    backend: str = "sim"
    source: str = "gps0"
    frame_id: str = "map"
    rate_hz: float = 5.0  # hedef okuma frekansı (uygulama üstü kullanabilir)

    # --- NMEA (seri) ayarları ---
    port: str = "/dev/ttyUSB0"   # Windows: "COM9"
    baud: int = 9600
    timeout_s: float = 0.5

    # --- gpsd ayarları ---
    gpsd_host: str = "127.0.0.1"
    gpsd_port: int = 2947

    # --- Simülasyon parametreleri ---
    origin_lat: float = 41.022150
    origin_lon: float = 28.832030
    origin_alt: float = 44.0
    sim_speed_mps: float = 0.0       # 0 ise sabit durur
    sim_heading_deg: float = 90.0    # doğu
    sim_noise_m: float = 0.5         # ölçüm gürültüsü (1σ, metre)
    sim_drift_mpm: float = 0.2       # yavaş drift (metre/dakika)
