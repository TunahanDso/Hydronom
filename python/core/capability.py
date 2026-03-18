import json, time
from typing import List, Dict, Any, Optional

class Capability:
    """
    Bağlantı başında bir kez gönderilir: kimlik, sürüm, akış kabiliyeti ve mod.
    C# tarafı bunu alınca kontrol kaynağını 'external' yapabilir.
    'sensors' alanı (opsiyonel): mevcut sensörlerin hafif özeti (sensor/source/frame_id/rate_hz ...)
    """
    def __init__(self,
                 node: str = "py-data",
                 version: str = "1.0.0",
                 streams: Optional[List[str]] = None,
                 prefer_external_state: bool = True,
                 sensors: Optional[List[Dict[str, Any]]] = None):
        self.type = "Capability"
        self.schema_version = "1.0.0"
        self.t = time.time()
        self.node = node
        self.version = version
        self.streams = streams or ["Sample", "FusedState", "Event", "Health"]
        self.prefer_external_state = prefer_external_state  # C# için ana sinyal
        self.sensors = sensors  # ← opsiyonel; None ise JSON'a eklenmez

    def to_json(self) -> str:
        msg = {
            "type": self.type,
            "schema_version": self.schema_version,
            "t": self.t,
            "node": self.node,
            "version": self.version,
            "streams": self.streams,
            "prefer_external_state": self.prefer_external_state,
        }
        if self.sensors is not None:
            msg["sensors"] = self.sensors
        return json.dumps(msg, ensure_ascii=False) + "\n"
