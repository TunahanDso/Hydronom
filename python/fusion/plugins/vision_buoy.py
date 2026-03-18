# fusion/plugins/vision_buoy.py
from __future__ import annotations
import math, time, hashlib
from typing import List, Dict, Any, Optional, Tuple

from fusion.context import FusionContext
from core.sample import Sample
from core.fused_state import FusedState
from fusion.plugins.base import IFuserPlugin

def _stable_hash(s: str) -> str:
    # Kısa ama tekrarlanabilir bir kimlik üretimi
    return hashlib.sha1(s.encode("utf-8")).hexdigest()[:12]

class VisionBuoyPlugin(IFuserPlugin):
    """
    Vision çıktısını landmark'a çevirir ve aynı dubayı zaman içinde tutarlı bir ID ile takip eder.

    Beklenen örnek (Sample):
      sensor="vision",
      data={"detections":[
        {"type":"red_buoy","distance":m,"bearing_deg":deg,"conf" (veya "confidence"):0..1}, ...
      ]}

    ID kalıcılığı:
      - Dünya çerçevesine (map) dönüştürülen (xw,yw) noktasını, aynı tipte mevcut kayıtlarla
        yakınlık eşiğine göre eşleştirir; eşleşirse aynı id korunur.
      - Eşleşme yoksa (yeni duba) stabil hash tabanlı yeni bir id atanır.
    """
    name = "vision_buoy"

    def __init__(
        self,
        ttl_s: float = 5.0,         # Görüş yaşam süresi (s)
        min_conf: float = 0.5,      # Minimum güven
        gate_dist_m: float = 2.0,   # Eşleştirme için mesafe kapısı
        keep_last_seen: bool = True # Son görüleni küçük driftlerle güncelle
    ):
        self.ttl_s = float(ttl_s)
        self.min_conf = float(min_conf)
        self.gate_dist_m = float(gate_dist_m)
        self.keep_last_seen = bool(keep_last_seen)

        # id -> kayıt
        # kayıt: {"type", "x", "y", "conf", "t_expire", "last_seen"}
        self._seen: Dict[str, Dict[str, Any]] = {}

        # Telemetri
        self._stats_last_emit = 0.0
        self._added = 0
        self._matched = 0
        self._expired = 0

    # ---- Lifecycle ----

    def on_init(self, ctx: FusionContext) -> None:
        self._seen.clear()
        self._added = self._matched = self._expired = 0

    def on_samples(self, ctx: FusionContext, samples: List[Sample]) -> None:
        now = ctx.now

        # Süresi dolanları sil
        for bid in list(self._seen.keys()):
            if self._seen[bid]["t_expire"] < now:
                del self._seen[bid]
                self._expired += 1

        # Vision örneklerini işle
        for s in samples:
            if getattr(s, "sensor", None) != "vision":
                continue

            dets = (getattr(s, "data", {}) or {}).get("detections", [])
            for d in dets:
                typ = str(d.get("type", "")).lower()
                conf = float(d.get("confidence", d.get("conf", 0.0)) or 0.0)
                if not typ or conf < self.min_conf:
                    continue

                dist = float(d.get("distance", 0.0) or 0.0)
                brg  = float(d.get("bearing_deg", 0.0) or 0.0)

                # body → world dönüşümü
                th = math.radians(ctx.yaw_deg + brg)
                xw = ctx.x + dist * math.cos(th)
                yw = ctx.y + dist * math.sin(th)

                # Aynı tipte en yakın adayı bul
                bid_match = self._match_existing(typ, xw, yw, self.gate_dist_m)

                if bid_match:
                    rec = self._seen[bid_match]
                    # Kayıtları güncelle (konumu hafifçe güncelleyebiliriz)
                    if self.keep_last_seen:
                        rec["x"] = xw
                        rec["y"] = yw
                    # Güveni son gözleme doğru çek (basit max veya EMA seçilebilir; burada max)
                    rec["conf"] = max(rec.get("conf", 0.0), conf)
                    rec["t_expire"] = now + self.ttl_s
                    rec["last_seen"] = now
                    self._matched += 1
                else:
                    # Stabil yeni kimlik üret
                    bid = self._new_id(typ, xw, yw, now)
                    self._seen[bid] = {
                        "type": typ,
                        "x": xw,
                        "y": yw,
                        "conf": conf,
                        "t_expire": now + self.ttl_s,
                        "last_seen": now,
                    }
                    self._added += 1

    def on_before_emit(self, ctx: FusionContext, out_state: FusedState) -> None:
        # Yaşayan dubaları landmark olarak bas
        for bid, rec in self._seen.items():
            ctx.add_landmark({
                "id": bid,
                "type": rec["type"],
                "shape": "point",
                "points": [(rec["x"], rec["y"])],
                "confidence": rec.get("conf", 1.0),
                "style": {"radius": 0.6, "label": rec["type"][:12]},
            })

        # Hafif telemetri (GUI’nin Inputs panelinde görülsün)
        now = ctx.now
        if now - self._stats_last_emit > 0.5:
            ctx.add_input_info("vision_buoy", {
                "tracked": len(self._seen),
                "added": self._added,
                "matched": self._matched,
                "expired": self._expired,
                "ttl_s": self.ttl_s,
                "gate_dist_m": self.gate_dist_m,
                "min_conf": self.min_conf,
            })
            self._stats_last_emit = now
            # per-emit sayaçlarını isteğe bağlı sıfırlamak istersen buraya koyabilirsin

    def on_close(self, ctx: FusionContext) -> None:
        # Temizlik (opsiyonel)
        pass

    # ---- İç yardımcılar ----

    def _match_existing(self, typ: str, xw: float, yw: float, gate_dist_m: float) -> Optional[str]:
        """Aynı tipte, gate_dist_m içinde en yakın mevcut ID'yi döndürür."""
        best_id: Optional[str] = None
        best_d2 = gate_dist_m * gate_dist_m
        for bid, rec in self._seen.items():
            if rec.get("type") != typ:
                continue
            dx = rec["x"] - xw
            dy = rec["y"] - yw
            d2 = dx * dx + dy * dy
            if d2 <= best_d2:
                best_d2 = d2
                best_id = bid
        return best_id

    def _new_id(self, typ: str, xw: float, yw: float, now: float) -> str:
        # Tip + kuadrant-sembolü + kaba koordinat ile stabil bir hash üret
        key = f"{typ}|{round(xw,2)}|{round(yw,2)}"
        return f"{typ}_{_stable_hash(key)}"
