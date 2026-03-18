from __future__ import annotations

from typing import Optional, List, Tuple, Dict, Any
from math import atan2, degrees

from .base import IFuserPlugin
from fusion.context import FusionContext

try:
    from fusion.slam.backends.dead_reckon import DeadReckonBackend
except Exception:
    DeadReckonBackend = None


def _wrap_angle_deg(a: float) -> float:
    # Açıyı [-180, 180) aralığına sar
    return (a + 180.0) % 360.0 - 180.0


def _clamp(x: float, lo: float, hi: float) -> float:
    return lo if x < lo else hi if x > hi else x


def _is_number(x: Any) -> bool:
    return isinstance(x, (int, float))


class SlamOdometryPlugin(IFuserPlugin):
    """
    Güçlendirilmiş SLAM/Odometri eklentisi.

    Amaç:
    - Dead-reckon backend varsa pose delta uygular
    - GPS hız vektöründen heading üretip IMU yaw drift'ini düzeltir
    - LiDAR scan-to-scan karşılaştırması ile küçük yaw düzeltmesi üretir
    - Tüm düzeltmeleri limitli ve yumuşak şekilde uygular
    - Hydronom Ops / GUI için anlamlı telemetri üretir

    Not:
    - Bu hâlâ tam ICP / NDT / pose-graph SLAM değildir
    - Ama mevcut mimari içinde map kalitesini ciddi biçimde yükseltir
    """

    name = "slam_odometry"

    def __init__(
        self,
        # GPS heading düzeltmesi
        alpha_gps_heading: float = 0.08,
        v_min_mps: float = 0.70,
        max_gps_step_deg: float = 2.5,

        # LiDAR yaw eşleştirme
        use_lidar_scan_match: bool = True,
        lidar_match_min_valid: int = 24,
        lidar_yaw_search_deg: float = 6.0,
        lidar_yaw_step_deg: float = 0.5,
        lidar_weight: float = 0.65,
        max_lidar_step_deg: float = 1.5,

        # Heading filtreleme
        heading_lowpass_alpha: float = 0.25,

        # Dead-reckon
        use_dead_reckon: bool = True,

        # Genel güvenlik limitleri
        max_total_step_deg: float = 3.0,
    ):
        # GPS heading parametreleri
        self.alpha_gps = float(alpha_gps_heading)
        self.v_min = float(v_min_mps)
        self.max_gps_step = float(max_gps_step_deg)

        # LiDAR scan matching parametreleri
        self.use_lidar_scan_match = bool(use_lidar_scan_match)
        self.lidar_match_min_valid = int(lidar_match_min_valid)
        self.lidar_yaw_search_deg = float(lidar_yaw_search_deg)
        self.lidar_yaw_step_deg = float(lidar_yaw_step_deg)
        self.lidar_weight = float(lidar_weight)
        self.max_lidar_step = float(max_lidar_step_deg)

        # Filtre / güvenlik
        self.heading_lowpass_alpha = float(heading_lowpass_alpha)
        self.max_total_step = float(max_total_step_deg)

        # Dead-reckon
        self.use_dead_reckon = bool(use_dead_reckon)
        self._backend = DeadReckonBackend() if (self.use_dead_reckon and DeadReckonBackend) else None
        self._ready_backend = False

        # Son lidar scan önbelleği
        self._last_scan_ranges: Optional[List[float]] = None
        self._last_scan_angle_min: Optional[float] = None
        self._last_scan_angle_inc: Optional[float] = None
        self._last_scan_range_min: Optional[float] = None
        self._last_scan_range_max: Optional[float] = None

        self._curr_scan_ranges: Optional[List[float]] = None
        self._curr_scan_angle_min: Optional[float] = None
        self._curr_scan_angle_inc: Optional[float] = None
        self._curr_scan_range_min: Optional[float] = None
        self._curr_scan_range_max: Optional[float] = None

        # Filtrelenmiş heading bilgisi
        self._filtered_heading_deg: Optional[float] = None

        # Basit telemetri
        self._slow_frames = 0

    def on_init(self, ctx: FusionContext) -> None:
        if self._backend:
            try:
                self._backend.open()
                self._ready_backend = True
            except Exception as e:
                print(f"[SlamOdometry] backend open error: {e}")
                self._ready_backend = False

        self._last_scan_ranges = None
        self._curr_scan_ranges = None
        self._filtered_heading_deg = None
        self._slow_frames = 0

    def _extract_lidar_scan(self, samples: List[object]) -> None:
        """
        Son LiDAR sample'ını yakala ve eşleştirme için sakla.
        """
        self._curr_scan_ranges = None
        self._curr_scan_angle_min = None
        self._curr_scan_angle_inc = None
        self._curr_scan_range_min = None
        self._curr_scan_range_max = None

        for s in reversed(samples):
            if getattr(s, "sensor", None) != "lidar":
                continue

            data = getattr(s, "data", None)
            if not isinstance(data, dict):
                continue

            ranges = data.get("ranges")
            angle_min = data.get("angle_min")
            angle_inc = data.get("angle_increment")
            range_min = data.get("range_min")
            range_max = data.get("range_max")

            if not isinstance(ranges, list):
                continue
            if not _is_number(angle_min) or not _is_number(angle_inc):
                continue
            if not _is_number(range_min) or not _is_number(range_max):
                continue
            if len(ranges) < 16:
                continue

            self._curr_scan_ranges = [float(x) if _is_number(x) else 0.0 for x in ranges]
            self._curr_scan_angle_min = float(angle_min)
            self._curr_scan_angle_inc = float(angle_inc)
            self._curr_scan_range_min = float(range_min)
            self._curr_scan_range_max = float(range_max)
            return

    def on_samples(self, ctx: FusionContext, samples: List[object]) -> None:
        # Dead-reckon backend güncelle
        if self._backend and self._ready_backend:
            try:
                pose = ctx.get_pose()
                guess = (
                    float(pose.get("x", 0.0)),
                    float(pose.get("y", 0.0)),
                    float(pose.get("yaw", 0.0)),
                )
                self._backend.update(samples, guess)
            except Exception as e:
                print(f"[SlamOdometry] backend update error: {e}")

        # LiDAR scan önbelleği
        self._extract_lidar_scan(samples)

        if hasattr(ctx, "profile_begin"):
            ctx.profile_begin("slam_odometry.on_samples")

        if hasattr(ctx, "profile_end"):
            dt_ms = ctx.profile_end("slam_odometry.on_samples")
            if isinstance(dt_ms, (int, float)) and dt_ms > 50.0:
                self._slow_frames += 1

    def _estimate_gps_heading_step(self, ctx: FusionContext, fused) -> Tuple[float, Dict[str, Any]]:
        """
        GPS hız vektöründen heading düzeltmesi üret.
        """
        info: Dict[str, Any] = {
            "enabled": True,
            "speed_mps": 0.0,
            "gps_heading_deg": None,
            "yaw_err_deg": None,
            "applied_step_deg": 0.0,
            "used": False,
        }

        vx = vy = None

        try:
            if hasattr(ctx, "get_twist"):
                tw = ctx.get_twist()
                if isinstance(tw, dict):
                    vx = tw.get("vx")
                    vy = tw.get("vy")
        except Exception:
            pass

        if vx is None or vy is None:
            vx = vx if vx is not None else getattr(ctx, "vx", None)
            vy = vy if vy is not None else getattr(ctx, "vy", None)

        if (vx is None or vy is None) and hasattr(fused, "twist"):
            try:
                if isinstance(fused.twist, dict):
                    vx = vx if vx is not None else fused.twist.get("vx")
                    vy = vy if vy is not None else fused.twist.get("vy")
            except Exception:
                pass

        if not _is_number(vx) or not _is_number(vy):
            info["enabled"] = False
            return 0.0, info

        speed = (float(vx) * float(vx) + float(vy) * float(vy)) ** 0.5
        info["speed_mps"] = round(speed, 3)

        if speed < self.v_min:
            return 0.0, info

        gps_heading_deg = degrees(atan2(float(vy), float(vx)))
        yaw_now = float(ctx.get_pose().get("yaw", 0.0))
        yaw_err = _wrap_angle_deg(gps_heading_deg - yaw_now)

        raw_step = self.alpha_gps * yaw_err
        step = _clamp(raw_step, -self.max_gps_step, self.max_gps_step)

        if self._filtered_heading_deg is None:
            self._filtered_heading_deg = gps_heading_deg
        else:
            err_f = _wrap_angle_deg(gps_heading_deg - self._filtered_heading_deg)
            self._filtered_heading_deg = _wrap_angle_deg(
                self._filtered_heading_deg + self.heading_lowpass_alpha * err_f
            )

        filtered_err = _wrap_angle_deg(self._filtered_heading_deg - yaw_now)
        step = _clamp(self.alpha_gps * filtered_err, -self.max_gps_step, self.max_gps_step)

        info["gps_heading_deg"] = round(gps_heading_deg, 2)
        info["filtered_heading_deg"] = round(self._filtered_heading_deg, 2)
        info["yaw_err_deg"] = round(filtered_err, 2)
        info["applied_step_deg"] = round(step, 3)
        info["used"] = True
        return step, info

    def _scan_match_score(self, prev_ranges: List[float], curr_ranges: List[float], shift_bins: int) -> Tuple[float, int]:
        """
        İki scan arasında kaydırmalı benzerlik skoru hesapla.
        Düşük skor daha iyi eşleşme demektir.
        """
        n = min(len(prev_ranges), len(curr_ranges))
        if n <= 0:
            return 1e18, 0

        total = 0.0
        valid = 0

        for i in range(n):
            j = i + shift_bins
            if j < 0 or j >= n:
                continue

            a = prev_ranges[i]
            b = curr_ranges[j]

            if a <= 0.0 or b <= 0.0:
                continue

            total += abs(a - b)
            valid += 1

        if valid <= 0:
            return 1e18, 0

        return total / float(valid), valid

    def _estimate_lidar_yaw_step(self) -> Tuple[float, Dict[str, Any]]:
        """
        Ardışık scan'ler arasında küçük yaw farkını tahmin et.
        Bu hafif bir scan-to-scan angular matching yaklaşımıdır.
        """
        info: Dict[str, Any] = {
            "enabled": self.use_lidar_scan_match,
            "valid_pairs": 0,
            "best_shift_bins": 0,
            "best_shift_deg": 0.0,
            "score": None,
            "applied_step_deg": 0.0,
            "used": False,
        }

        if not self.use_lidar_scan_match:
            return 0.0, info

        if self._last_scan_ranges is None or self._curr_scan_ranges is None:
            return 0.0, info

        if self._last_scan_angle_inc is None or self._curr_scan_angle_inc is None:
            return 0.0, info

        inc_deg = degrees(self._curr_scan_angle_inc)
        if inc_deg <= 1e-9:
            return 0.0, info

        max_bins = max(1, int(round(self.lidar_yaw_search_deg / inc_deg)))
        step_bins = max(1, int(round(self.lidar_yaw_step_deg / inc_deg)))

        best_score = 1e18
        best_shift = 0
        best_valid = 0

        for shift in range(-max_bins, max_bins + 1, step_bins):
            score, valid = self._scan_match_score(self._last_scan_ranges, self._curr_scan_ranges, shift)
            if valid < self.lidar_match_min_valid:
                continue
            if score < best_score:
                best_score = score
                best_shift = shift
                best_valid = valid

        if best_valid < self.lidar_match_min_valid:
            return 0.0, info

        best_shift_deg = best_shift * inc_deg

        # Ölçülen farkın tamamını değil, kontrollü kısmını uygula
        raw_step = -self.lidar_weight * best_shift_deg
        step = _clamp(raw_step, -self.max_lidar_step, self.max_lidar_step)

        info["valid_pairs"] = best_valid
        info["best_shift_bins"] = best_shift
        info["best_shift_deg"] = round(best_shift_deg, 3)
        info["score"] = round(best_score, 5)
        info["applied_step_deg"] = round(step, 3)
        info["used"] = True
        return step, info

    def _apply_dead_reckon_delta(self, ctx: FusionContext) -> Dict[str, Any]:
        info: Dict[str, Any] = {
            "enabled": bool(self._backend and self._ready_backend),
            "dx": 0.0,
            "dy": 0.0,
            "dyaw": 0.0,
            "used": False,
        }

        if not (self._backend and self._ready_backend):
            return info

        try:
            delta: Optional[Tuple[float, float, float]] = self._backend.get_pose_delta()
            if not delta:
                return info

            dx, dy, dyaw = delta
            dx = float(dx)
            dy = float(dy)
            dyaw = float(dyaw)

            ctx.apply_pose_delta(dx, dy, dyaw)

            info["dx"] = round(dx, 4)
            info["dy"] = round(dy, 4)
            info["dyaw"] = round(dyaw, 4)
            info["used"] = True
            return info
        except Exception as e:
            print(f"[SlamOdometry] get_pose_delta error: {e}")
            return info

    def on_before_emit(self, ctx: FusionContext, fused) -> None:
        # 1) Dead-reckon delta uygula
        dead_info = self._apply_dead_reckon_delta(ctx)

        # 2) GPS heading düzeltmesi
        gps_step, gps_info = self._estimate_gps_heading_step(ctx, fused)

        # 3) LiDAR angular matching düzeltmesi
        lidar_step, lidar_info = self._estimate_lidar_yaw_step()

        # 4) Birleşik yaw düzeltmesi
        total_step = gps_step + lidar_step
        total_step = _clamp(total_step, -self.max_total_step, self.max_total_step)

        if abs(total_step) > 1e-6:
            try:
                ctx.apply_pose_correction(0.0, 0.0, total_step)
            except Exception as e:
                print(f"[SlamOdometry] apply_pose_correction error: {e}")

        # 5) Telemetri
        try:
            if hasattr(ctx, "add_input_info"):
                ctx.add_input_info("slam_odometry", {
                    "gps": gps_info,
                    "lidar_match": lidar_info,
                    "dead_reckon": dead_info,
                    "total_yaw_step_deg": round(total_step, 3),
                    "slow_frames": int(self._slow_frames),
                })
        except Exception:
            pass

        # 6) Landmark
        try:
            pose = fused.pose if hasattr(fused, "pose") else ctx.get_pose()
            x = float(pose.get("x", 0.0))
            y = float(pose.get("y", 0.0))
            yaw = float(pose.get("yaw", 0.0))

            ctx.add_landmark({
                "id": "odom_hint",
                "type": "odometry",
                "shape": "point",
                "points": [(x, y)],
                "style": {
                    "radius": 0.18,
                    "label": f"odom {yaw:.1f}°"
                }
            })
        except Exception:
            pass

        # 7) Scan geçmişini güncelle
        if self._curr_scan_ranges is not None:
            self._last_scan_ranges = list(self._curr_scan_ranges)
            self._last_scan_angle_min = self._curr_scan_angle_min
            self._last_scan_angle_inc = self._curr_scan_angle_inc
            self._last_scan_range_min = self._curr_scan_range_min
            self._last_scan_range_max = self._curr_scan_range_max

    def on_close(self, ctx: FusionContext) -> None:
        if self._backend:
            try:
                self._backend.close()
            except Exception:
                pass