from __future__ import annotations

import math
from typing import List, Optional, Tuple, Dict, Any

import numpy as np

from fusion.context import FusionContext
from core.fused_state import FusedState
from fusion.plugins.base import IFuserPlugin

_RAD2DEG = 180.0 / math.pi
_DEG2RAD = math.pi / 180.0


def _wrap_deg(a: float) -> float:
    return (a + 180.0) % 360.0 - 180.0


def _is_num(v: Any) -> bool:
    return isinstance(v, (int, float))


class EkfLocalizationPlugin(IFuserPlugin):
    """
    Hydronom için güçlendirilmiş 2D EKF lokalizasyon eklentisi.

    State:
        x = [px, py, yaw_deg, vx, vy, wz_bias_rad_s]

    Amaç:
    - GPS pozisyonunu filtrelemek
    - IMU gz ile yaw tahmini yapmak
    - GPS hız bilgisinden vx/vy ve heading düzeltmesi almak
    - Poz düzeltmesini FusionContext'e uygulamak
    - Hydronom Ops için anlamlı telemetri üretmek

    Not:
    - Bu hâlâ tam küresel SLAM çözümü değildir
    - Ama mevcut mimaride pose stabilitesini ciddi şekilde yükseltir
    """

    name = "ekf_localization"
    priority = 20
    max_hz = None

    def __init__(
        self,
        q_pos: float = 0.08,
        q_yaw_deg: float = 2.5,
        q_vel: float = 0.45,
        q_bias: float = 0.003,
        r_gps_pos: float = 2.2,
        r_gps_vel: float = 0.9,
        r_heading_deg: float = 7.0,
        v_heading_min: float = 0.9,
        gps_gate_sigma: float = 4.0,
        gps_vel_gate_sigma: float = 4.0,
        max_trail_points: int = 400,
        tele_period_s: float = 0.25,
    ):
        self.q_pos = float(q_pos)
        self.q_yaw_deg = float(q_yaw_deg)
        self.q_vel = float(q_vel)
        self.q_bias = float(q_bias)

        self.r_gps_pos = float(r_gps_pos)
        self.r_gps_vel = float(r_gps_vel)
        self.r_heading_deg = float(r_heading_deg)
        self.v_heading_min = float(v_heading_min)

        self.gps_gate_sigma = float(gps_gate_sigma)
        self.gps_vel_gate_sigma = float(gps_vel_gate_sigma)

        self.max_trail_points = int(max_trail_points)
        self.tele_period_s = float(tele_period_s)

        self._x = np.zeros(6, dtype=np.float64)
        self._P = np.eye(6, dtype=np.float64) * 6.0
        self._I6 = np.eye(6, dtype=np.float64)

        self._last_t: Optional[float] = None
        self._last_gps_xy: Optional[Tuple[float, float]] = None
        self._last_gps_t: Optional[float] = None

        self._trail: List[Tuple[float, float]] = []
        self._tele_last = 0.0

        self._accepted_gps_pos = 0
        self._rejected_gps_pos = 0
        self._accepted_gps_vel = 0
        self._rejected_gps_vel = 0
        self._heading_updates = 0

    # ------------------------------------------------------------
    # Yaşam döngüsü
    # ------------------------------------------------------------

    def on_init(self, ctx: FusionContext) -> None:
        self._x[:] = np.array(
            [
                float(getattr(ctx, "x", 0.0)),
                float(getattr(ctx, "y", 0.0)),
                float(getattr(ctx, "yaw_deg", 0.0)),
                float(getattr(ctx, "vx", 0.0)),
                float(getattr(ctx, "vy", 0.0)),
                0.0,
            ],
            dtype=np.float64,
        )
        self._P[:] = np.eye(6, dtype=np.float64) * 6.0
        self._last_t = getattr(ctx, "now", None)
        self._last_gps_xy = None
        self._last_gps_t = None
        self._trail.clear()
        self._tele_last = 0.0

        self._accepted_gps_pos = 0
        self._rejected_gps_pos = 0
        self._accepted_gps_vel = 0
        self._rejected_gps_vel = 0
        self._heading_updates = 0

    # ------------------------------------------------------------
    # İç yardımcılar
    # ------------------------------------------------------------

    def _predict(self, gz_rad_s: float, dt: float) -> None:
        """
        Süreç modeli:
        - x,y hızla akar
        - yaw, (gz - bias) ile ilerler
        - vx,vy sabit hız varsayımıyla korunur
        - bias yavaş değişen kabul edilir
        """
        px, py, yaw_deg, vx, vy, bias = self._x.tolist()

        yaw_deg = _wrap_deg(yaw_deg + (gz_rad_s - bias) * _RAD2DEG * dt)
        px = px + vx * dt
        py = py + vy * dt

        self._x[:] = [px, py, yaw_deg, vx, vy, bias]

        F = self._I6.copy()
        F[0, 3] = dt
        F[1, 4] = dt
        F[2, 5] = -_RAD2DEG * dt

        Q = np.zeros((6, 6), dtype=np.float64)
        Q[0, 0] = (self.q_pos * dt) ** 2
        Q[1, 1] = (self.q_pos * dt) ** 2
        Q[2, 2] = (self.q_yaw_deg * dt) ** 2
        Q[3, 3] = (self.q_vel * dt) ** 2
        Q[4, 4] = (self.q_vel * dt) ** 2
        Q[5, 5] = (self.q_bias * dt) ** 2

        self._P = F @ self._P @ F.T + Q

    def _update_linear(
        self,
        z: np.ndarray,
        H: np.ndarray,
        R: np.ndarray,
        angle_index: Optional[int] = None,
        gate_sigma: Optional[float] = None,
    ) -> bool:
        """
        Genel lineer EKF update.
        İsteğe bağlı:
        - angle_index: innovation açısal ise wrap uygula
        - gate_sigma: kaba outlier engeli
        """
        zhat = H @ self._x
        innov = z - zhat

        if angle_index is not None:
            innov[angle_index] = _wrap_deg(float(innov[angle_index]))

        S = H @ self._P @ H.T + R

        if gate_sigma is not None and S.shape[0] == len(z):
            try:
                for i in range(len(z)):
                    sigma = math.sqrt(max(1e-9, float(S[i, i])))
                    if abs(float(innov[i])) > gate_sigma * sigma:
                        return False
            except Exception:
                pass

        K = self._P @ H.T @ np.linalg.inv(S)

        self._x = self._x + K @ innov
        self._x[2] = _wrap_deg(float(self._x[2]))

        # Joseph formu: sayısal olarak daha sağlam
        I_KH = self._I6 - K @ H
        self._P = I_KH @ self._P @ I_KH.T + K @ R @ K.T
        return True

    def _extract_imu_gz(self, samples: List[object]) -> float:
        for s in samples:
            if getattr(s, "sensor", None) != "imu":
                continue
            data = getattr(s, "data", None) or {}
            gz = data.get("gz", 0.0)
            if _is_num(gz):
                return float(gz)
        return 0.0

    def _extract_gps_xy(self, ctx: FusionContext, samples: List[object]) -> Optional[Tuple[float, float]]:
        for s in samples:
            if getattr(s, "sensor", None) != "gps":
                continue

            gd = getattr(s, "data", None) or {}

            lat = gd.get("lat")
            lon = gd.get("lon")
            if _is_num(lat) and _is_num(lon):
                try:
                    return ctx.project(float(lat), float(lon))
                except Exception:
                    pass

            if _is_num(gd.get("x")) and _is_num(gd.get("y")):
                return float(gd["x"]), float(gd["y"])

        return None

    def _extract_gps_vel(self, ctx: FusionContext, gps_xy: Optional[Tuple[float, float]], now: float) -> Optional[Tuple[float, float]]:
        """
        GPS'ten doğrudan hız yoksa iki konum farkından vx,vy tahmini üret.
        """
        if gps_xy is None:
            return None

        if self._last_gps_xy is None or self._last_gps_t is None:
            self._last_gps_xy = gps_xy
            self._last_gps_t = now
            return None

        dt = max(1e-3, now - self._last_gps_t)
        if dt < 0.15:
            return None

        vx = (gps_xy[0] - self._last_gps_xy[0]) / dt
        vy = (gps_xy[1] - self._last_gps_xy[1]) / dt

        self._last_gps_xy = gps_xy
        self._last_gps_t = now
        return vx, vy

    def _maybe_update_gps_position(self, gps_xy: Optional[Tuple[float, float]]) -> None:
        if gps_xy is None:
            return

        H = np.array(
            [
                [1, 0, 0, 0, 0, 0],
                [0, 1, 0, 0, 0, 0],
            ],
            dtype=np.float64,
        )
        z = np.array([gps_xy[0], gps_xy[1]], dtype=np.float64)
        R = np.eye(2, dtype=np.float64) * (self.r_gps_pos ** 2)

        ok = self._update_linear(z, H, R, gate_sigma=self.gps_gate_sigma)
        if ok:
            self._accepted_gps_pos += 1
        else:
            self._rejected_gps_pos += 1

    def _maybe_update_gps_velocity(self, gps_vel: Optional[Tuple[float, float]]) -> None:
        if gps_vel is None:
            return

        H = np.array(
            [
                [0, 0, 0, 1, 0, 0],
                [0, 0, 0, 0, 1, 0],
            ],
            dtype=np.float64,
        )
        z = np.array([gps_vel[0], gps_vel[1]], dtype=np.float64)
        R = np.eye(2, dtype=np.float64) * (self.r_gps_vel ** 2)

        ok = self._update_linear(z, H, R, gate_sigma=self.gps_vel_gate_sigma)
        if ok:
            self._accepted_gps_vel += 1
        else:
            self._rejected_gps_vel += 1

    def _maybe_update_heading_from_velocity(self) -> None:
        vx = float(self._x[3])
        vy = float(self._x[4])
        speed = math.hypot(vx, vy)

        if speed < self.v_heading_min:
            return

        yaw_meas = math.degrees(math.atan2(vy, vx))

        H = np.array([[0, 0, 1, 0, 0, 0]], dtype=np.float64)
        z = np.array([yaw_meas], dtype=np.float64)
        R = np.array([[self.r_heading_deg ** 2]], dtype=np.float64)

        ok = self._update_linear(z, H, R, angle_index=0, gate_sigma=5.0)
        if ok:
            self._heading_updates += 1

    def _append_trail(self) -> None:
        pt = (float(self._x[0]), float(self._x[1]))
        if not self._trail:
            self._trail.append(pt)
            return

        last = self._trail[-1]
        dist = math.hypot(pt[0] - last[0], pt[1] - last[1])

        # Çok sık nokta yığılmasını önle
        if dist >= 0.20:
            self._trail.append(pt)

        if len(self._trail) > self.max_trail_points:
            self._trail = self._trail[-self.max_trail_points:]

    # ------------------------------------------------------------
    # Ana akış
    # ------------------------------------------------------------

    def on_samples(self, ctx: FusionContext, samples: List[object]) -> None:
        now = float(getattr(ctx, "now", 0.0))

        if self._last_t is None:
            self._last_t = now
            return

        dt = max(1e-3, min(0.25, now - self._last_t))
        self._last_t = now

        gz = self._extract_imu_gz(samples)
        self._predict(gz, dt)

        gps_xy = self._extract_gps_xy(ctx, samples)
        gps_vel = self._extract_gps_vel(ctx, gps_xy, now)

        self._maybe_update_gps_position(gps_xy)
        self._maybe_update_gps_velocity(gps_vel)
        self._maybe_update_heading_from_velocity()

        self._append_trail()

    def on_before_emit(self, ctx: FusionContext, out_state: FusedState) -> None:
        dx = float(self._x[0] - getattr(ctx, "x", 0.0))
        dy = float(self._x[1] - getattr(ctx, "y", 0.0))
        dyaw = _wrap_deg(float(self._x[2] - getattr(ctx, "yaw_deg", 0.0)))

        try:
            ctx.apply_pose_correction(dx, dy, dyaw)
        except Exception:
            pass

        if (float(getattr(ctx, "now", 0.0)) - self._tele_last) >= self.tele_period_s:
            info = {
                "cov_x": round(float(self._P[0, 0]), 4),
                "cov_y": round(float(self._P[1, 1]), 4),
                "cov_yaw": round(float(self._P[2, 2]), 4),
                "cov_vx": round(float(self._P[3, 3]), 4),
                "cov_vy": round(float(self._P[4, 4]), 4),
                "vx": round(float(self._x[3]), 4),
                "vy": round(float(self._x[4]), 4),
                "speed_mps": round(float(math.hypot(self._x[3], self._x[4])), 4),
                "yaw_deg": round(float(self._x[2]), 3),
                "wz_bias_rad_s": round(float(self._x[5]), 6),
                "gps_pos_accept": int(self._accepted_gps_pos),
                "gps_pos_reject": int(self._rejected_gps_pos),
                "gps_vel_accept": int(self._accepted_gps_vel),
                "gps_vel_reject": int(self._rejected_gps_vel),
                "heading_updates": int(self._heading_updates),
            }

            try:
                if hasattr(ctx, "add_input_info"):
                    ctx.add_input_info("ekf_localization", info)
                elif hasattr(ctx, "add_input"):
                    payload = dict(info)
                    payload["source"] = "ekf_localization"
                    ctx.add_input(payload)
            except Exception:
                pass

            self._tele_last = float(getattr(ctx, "now", 0.0))

        if len(self._trail) >= 2:
            try:
                ctx.add_landmark(
                    {
                        "id": "ekf_trail",
                        "type": "trail_ekf",
                        "shape": "polyline",
                        "points": self._trail[-300:],
                        "style": {
                            "width": 2,
                            "label": "ekf",
                        },
                    }
                )
            except Exception:
                pass

            try:
                ctx.add_landmark(
                    {
                        "id": "ekf_pose",
                        "type": "ekf_pose",
                        "shape": "point",
                        "points": [(float(self._x[0]), float(self._x[1]))],
                        "style": {
                            "radius": 0.20,
                            "label": f"ekf {float(self._x[2]):.1f}°",
                        },
                    }
                )
            except Exception:
                pass

    def on_close(self, ctx: FusionContext) -> None:
        pass