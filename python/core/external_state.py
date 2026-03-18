# core/external_state.py
# Amaç: C# çekirdeğin beklediği "dış durum"u (basit alan adlarıyla) aynen vermek.

import json, time

class ExternalState:
    """
    Minimal ve "düz" alan adları:
      - x, y, z (metre)
      - head_deg (derece)
      - yaw_rate (rad/s)
      - t (epoch)
      - source: "py-data"
    İstersen burada hız (vx, vy) vb. alanları da açabiliriz.
    """
    def __init__(self, x: float, y: float, z: float,
                 head_deg: float, yaw_rate: float,
                 source: str = "py-data"):
        self.type = "ExternalState"
        self.schema_version = "1.0.0"
        self.t = time.time()
        self.x = float(x)
        self.y = float(y)
        self.z = float(z)
        self.head_deg = float(head_deg)
        self.yaw_rate = float(yaw_rate)
        self.source = source

    def to_json(self) -> str:
        return json.dumps(self.__dict__, ensure_ascii=False) + "\n"
