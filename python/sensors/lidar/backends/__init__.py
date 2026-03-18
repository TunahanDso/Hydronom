# sensors/lidar/backends/__init__.py

# Taban arayüz ILidarBackend; eski kod uyumu için LidarBackend takma adı veriyoruz.
from .base import ILidarBackend as LidarBackend

# (İsteğe bağlı) Backend sınıflarını da dışa açmak istersen:
try:
    from .sim import SimBackend
except Exception:
    SimBackend = None

try:
    from .rplidar import RPLidarBackend
except Exception:
    RPLidarBackend = None

try:
    from .ouster import OusterBackend
except Exception:
    OusterBackend = None

__all__ = [
    "ILidarBackend",
    "LidarBackend",
    "SimBackend",
    "RPLidarBackend",
    "OusterBackend",
]
