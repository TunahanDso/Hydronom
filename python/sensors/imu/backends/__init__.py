# sensors/imu/backends/__init__.py

# Doğru taban arayüz: IImuBackend
from .base import IImuBackend as ImuBackend  # legacy alias: ImuBackend

# (İsteğe bağlı) backend sınıflarını dışa aç
try:
    from .sim import SimBackend
except Exception:
    SimBackend = None

try:
    from .serial import SerialBackend
except Exception:
    SerialBackend = None

try:
    from .mpu6050 import Mpu6050Backend
except Exception:
    Mpu6050Backend = None

__all__ = [
    "IImuBackend",
    "ImuBackend",
    "SimBackend",
    "SerialBackend",
    "Mpu6050Backend",
]
