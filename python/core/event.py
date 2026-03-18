# core/event.py

import time
import json
from typing import Any, Dict, List

class Event:
    """
    Bilgi değerlendirme (Evaluator) tarafından üretilen olay nesnesi.
    Sistem çevrede belirli bir durumu tespit ettiğinde (örnek: kırmızı duba),
    bu sınıf üzerinden olayı üretir ve C# görev modülüne gönderir.
    """

    def __init__(self, name: str, payload: Dict[str, Any],
                 severity: str = "info", related: List[Dict[str, Any]] = None):
        self.type = "Event"
        self.schema_version = "1.0.0"
        self.t = time.time()
        self.name = name              # örn: "red-buoy-detected"
        self.payload = payload        # olay verisi (x,y,mesafe,conf)
        self.severity = severity      # "info" | "warn" | "crit"
        self.related = related or []  # hangi FusedState ile ilişkili

    def to_json(self) -> str:
        """Görev modülüne gidecek JSON çıktısı."""
        return json.dumps(self.__dict__, ensure_ascii=False) + "\n"

    @staticmethod
    def from_json(line: str):
        obj = json.loads(line)
        return Event(
            name=obj["name"],
            payload=obj["payload"],
            severity=obj.get("severity", "info"),
            related=obj.get("related", [])
        )
