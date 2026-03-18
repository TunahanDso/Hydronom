# core/fused_state.py
import time
import json
from typing import Any, Dict, List

class FusedState:
    """
    Füzyon modülü tarafından üretilen birleşik durum nesnesi.

    Şema (schema_version = "1.1.0"):
      pose:  {"x":float, "y":float, "z":float, "yaw":float_deg}
      twist: {"vx":float, "vy":float, "vz":float, "yaw_rate":float_rad_s}

      landmarks: List[Landmark]
        Landmark:
          {
            "id": str,
            "type": str,                         # örn: "trail", "odometry", "obstacles", "ogm", "red_buoy"
            "shape": "point"|"polyline"|"polygon"|"grid_preview",
            "points": List[ [x,y] | (x,y) ],    # shape="point" ise genelde tek nokta
            "confidence": float (opsiyonel),
            "style": dict (opsiyonel)           # {radius?, width?, color?, label?, ...}
          }

      inputs: List[Dict[str, Any]]  # bu emite katkı veren sensör/eklenti bilgileri
    """

    def __init__(
        self,
        pose: Dict[str, Any],
        twist: Dict[str, Any],
        landmarks: List[Dict[str, Any]] | None = None,
        confidence: float = 1.0,
        inputs: List[Dict[str, Any]] | None = None
    ):
        # Meta
        self.type = "FusedState"
        self.schema_version = "1.1.0"
        self.t = time.time()

        # Durum
        self.pose = pose      # {'x','y','z','yaw'}
        self.twist = twist    # {'vx','vy','vz','yaw_rate'}

        # Çevresel varlıklar / görselleştirme verisi
        self.landmarks = landmarks or []

        # Füzyon güveni (0.0..1.0)
        self.confidence = float(confidence)

        # Telemetri / hangi girdiler kullanıldı
        self.inputs = inputs or []

    def to_json(self) -> str:
        """C# tarafınca doğrudan tüketilecek JSON satırı üretir (newline sonlu)."""
        return json.dumps(
            self.__dict__,
            ensure_ascii=False,
            separators=(",", ":")  # daha kompakt
        ) 

    @staticmethod
    def from_json(line: str) -> "FusedState":
        """JSON satırından FusedState nesnesi oluşturur."""
        obj = json.loads(line)
        return FusedState(
            pose=obj["pose"],
            twist=obj["twist"],
            landmarks=obj.get("landmarks", []),
            confidence=obj.get("confidence", 1.0),
            inputs=obj.get("inputs", [])
        )
