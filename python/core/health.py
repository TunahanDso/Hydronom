# core/health.py

import time
import json

class Health:
    """
    Sistem performansını izleyen sağlık kaydı.
    Her modül (sensör, füzyon, değerlendirme, ağ geçidi) belirli aralıklarla
    bir Health kaydı gönderir. Bu sayede görev modülü gecikme, kuyruk, düşen
    paket oranı gibi metrikleri izleyebilir.
    """

    def __init__(self, node: str, latency_ms: float, jitter_ms: float,
                 drops: int, queue: int, uptime_s: float):
        self.type = "Health"
        self.schema_version = "1.0.0"
        self.t = time.time()
        self.node = node
        self.latency_ms = latency_ms
        self.jitter_ms = jitter_ms
        self.drops = drops
        self.queue = queue
        self.uptime_s = uptime_s

    def to_json(self) -> str:
        return json.dumps(self.__dict__, ensure_ascii=False) + "\n"
