from dataclasses import dataclass, field
from typing import Optional, Dict, Tuple
from datetime import datetime

@dataclass
class Pose:
    x: float
    y: float
    z: float

@dataclass
class Rpy:
    roll: float   # derece
    pitch: float  # derece
    yaw: float    # derece

@dataclass
class Velocity:
    vx: float
    vy: float
    vz: float

@dataclass
class ActuatorData:
    val: float    # Komut değeri (throttle/servo)
    rpm: int      # Dönüş hızı
    current: float # Akım (mA)

@dataclass
class WorldFrame:
    """
    Canlı 3D sahne ve CSM Analizi için tam kare veri yapısı.
    """
    stamp: Optional[datetime]
    pos: Pose
    rpy: Rpy
    
    # CSM Dinamikleri için eklenen alanlar:
    vel: Velocity = field(default_factory=lambda: Velocity(0.0, 0.0, 0.0))
    fb: Tuple[float, float, float] = (0.0, 0.0, 0.0) # Body Force (Action: Delta Psi)
    tb: Tuple[float, float, float] = (0.0, 0.0, 0.0) # Body Torque
    
    # Sistem Durumu (Jitter Analizi için)
    actuators: Dict[str, Dict] = field(default_factory=dict)
    is_armed: bool = False
    mode: str = "DISARMED"
    jitter: float = 0.0  # Hesaplanan anlık Jitter değeri