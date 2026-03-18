# fusion/plugins/trace_trail.py
from __future__ import annotations
from collections import deque
from typing import List, Tuple, Optional

from fusion.context import FusionContext
from core.sample import Sample
from core.fused_state import FusedState
from fusion.plugins.base import IFuserPlugin

def _dist2(a: Tuple[float, float], b: Tuple[float, float]) -> float:
    dx = a[0] - b[0]
    dy = a[1] - b[1]
    return dx*dx + dy*dy

class TrailPlugin(IFuserPlugin):
    """
    Harita üzerinde iz (polyline) çizer.
    - Landmark standardı: shape="polyline", points=[(x,y), ...]
    - Gürültü ve yoğunluk kontrolü için min adım mesafesi ve downsample desteği.
    """
    name = "trail"

    def __init__(
        self,
        max_points: int = 800,          # iz tamponu uzunluğu
        min_step_m: float = 0.25,       # ardışık noktalar arası min mesafe
        preview_id: str = "trail",      # landmark id
        downsample_step: int = 1,       # 1 → downsample yok; 2 → her 2 noktada 1 örnek vb.
        style: dict | None = None
    ):
        self._trail = deque(maxlen=int(max_points))
        self._min_step2 = float(min_step_m) ** 2
        self._preview_id = str(preview_id)
        self._down = max(1, int(downsample_step))
        self._style = style or {"width": 2}
        self._last: Optional[Tuple[float, float]] = None

    def on_init(self, ctx: FusionContext) -> None:
        # Başlangıçta tamponu temizle
        self._trail.clear()
        self._last = None

    def on_samples(self, ctx: FusionContext, samples: List[Sample]) -> None:
        # Pozdan nokta ekle (ctx.x/ctx.y güncel kabul)
        pt = (ctx.x, ctx.y)
        if self._last is None or _dist2(pt, self._last) >= self._min_step2:
            self._trail.append(pt)
            self._last = pt

    def on_before_emit(self, ctx: FusionContext, out_state: FusedState) -> None:
        if len(self._trail) < 2:
            return
        pts = list(self._trail)
        if self._down > 1:
            pts = pts[::self._down]

        ctx.add_landmark({
            "id": self._preview_id,
            "type": "trail",
            "shape": "polyline",
            "points": pts,
            "style": self._style
        })

    def on_close(self, ctx: FusionContext) -> None:
        self._trail.clear()
        self._last = None
