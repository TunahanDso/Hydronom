# fusion/slam/odometry.py
from __future__ import annotations
import math, time
from typing import Optional, Dict, Any, List
from fusion.plugins.base import IFuserPlugin
from fusion.context import FusionContext

class OdometrySlamPlugin(IFuserPlugin):
    """
    Hafif SLAM-odometri iskeleti:
      - IMU gz’den yaw integrasyonu, GPS hız vektörü ile yavaş düzeltme
      - Gelecekte ICP/scan-matching ile dx,dy düzeltmeleri eklenecek
    Şimdilik sadece yaw drift’ini yumuşatır ve küçük düzeltmeler uygular.
    """
    name = "slam_odom"

    def __init__(self, alpha_gps_heading: float = 0.08):
        # IMU ağırlığı yüksek, GPS yönüyle küçük karışım
        self.alpha = float(alpha_gps_heading)
        self._last_gps: Optional[Dict[str, float]] = None  # {t,x,y}
        self._last_apply_ts: float = 0.0

    def init(self, ctx: FusionContext) -> None:
        self._last_gps = None
        self._last_apply_ts = 0.0

    def on_samples(self, ctx: FusionContext, samples) -> None:
        # GPS örneğini (lat/lon → Fuser XY’ye dönmüş değil; ctx’te pose var)
        # Bu eklenti Fuser’ın pozundan ilerler; GPS yönünden düzeltme çıkarır.
        now = ctx.t_now
        gx = ctx.pose_x
        gy = ctx.pose_y

        if self._last_gps is not None:
            dt = max(1e-3, now - self._last_gps["t"])
            vx = (gx - self._last_gps["x"]) / dt
            vy = (gy - self._last_gps["y"]) / dt
            if abs(vx) + abs(vy) > 1e-3:
                gps_head = math.degrees(math.atan2(vy, vx)) % 360.0
                # IMU yaw (ctx.yaw_deg) → GPS yönüne doğru minik adım
                dyaw = ((gps_head - ctx.yaw_deg + 540.0) % 360.0) - 180.0
                corr = self.alpha * dyaw  # küçük düzeltme
                if abs(corr) > 0.01:
                    ctx.apply_pose_correction(dyaw_deg=corr)

        self._last_gps = {"t": now, "x": gx, "y": gy}

    def before_emit(self, ctx: FusionContext, out_state) -> None:
        # Telemetriye küçük bir bilgi ekleyelim
        out_state.inputs.append({"sensor": "odom", "source": "slam", "note": "yaw-correction", "alpha": self.alpha})
