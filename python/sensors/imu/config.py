# sensors/imu/config.py
from dataclasses import dataclass

@dataclass
class ImuConfig:
    backend: str = "sim"        # "sim" | "serial" (ileride: "icm20948", "mpu9250" vs.)
    source: str = "imu0"
    frame_id: str = "base_link"
    rate_hz: float = 100.0

    # serial backend için:
    port: str = "/dev/ttyUSB0"
    baud: int = 115200

    # sim backend için:
    sim_noise_acc: float = 0.03     # m/s^2 (1σ)
    sim_noise_gyro: float = 0.002   # rad/s (1σ)
    sim_drift_gyro: float = 0.0003  # rad/s yavaş sürüklenme
