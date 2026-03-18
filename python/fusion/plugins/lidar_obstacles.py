# fusion/plugins/lidar_obstacles.py
from __future__ import annotations
import math
from typing import List, Tuple, Optional, Dict, Any

from fusion.context import FusionContext
from core.sample import Sample
from core.fused_state import FusedState
from fusion.plugins.base import IFuserPlugin


def _deg2rad(a: float) -> float:
    return a * math.pi / 180.0


def _Rz(rad: float) -> Tuple[Tuple[float, float, float], Tuple[float, float, float], Tuple[float, float, float]]:
    c, s = math.cos(rad), math.sin(rad)
    # fmt: off
    return ((c, -s, 0.0),
            (s,  c, 0.0),
            (0.0, 0.0, 1.0))
    # fmt: on


def _Ry(rad: float):
    c, s = math.cos(rad), math.sin(rad)
    # fmt: off
    return (( c, 0.0,  s),
            (0.0, 1.0, 0.0),
            (-s, 0.0,  c))
    # fmt: on


def _Rx(rad: float):
    c, s = math.cos(rad), math.sin(rad)
    # fmt: off
    return ((1.0, 0.0, 0.0),
            (0.0,  c, -s),
            (0.0,  s,  c))
    # fmt: on


def _mat3_mul(A, B):
    return tuple(
        tuple(A[i][0] * B[0][j] + A[i][1] * B[1][j] + A[i][2] * B[2][j] for j in range(3))
        for i in range(3)
    )


def _mat3_vec3(M, v):
    return (
        M[0][0] * v[0] + M[0][1] * v[1] + M[0][2] * v[2],
        M[1][0] * v[0] + M[1][1] * v[1] + M[1][2] * v[2],
        M[2][0] * v[0] + M[2][1] * v[1] + M[2][2] * v[2],
    )


class LidarObstaclePlugin(IFuserPlugin):
    """
    LiDAR LaserScan → dünya (map) çerçevesinde 2D nokta önizlemesi.

    Önemli:
      - Roll/Pitch kompanzasyonu yapılır (ctx.roll_deg / ctx.pitch_deg kullanılır).
      - Sensör montaj “extrinsics” desteklenir (yaw/pitch/roll ofsetleri + x/y ofseti).
      - Bu eklenti 2D önizleme içindir; istenirse Z kullanılarak 3D kullanılabilir.

    Çıktılar:
      - Önizleme polyline: id=landmark_id
      - (opsiyonel) Seyreltilmiş yoğun set: id=dense_id
    """
    name = "lidar_obstacles"

    def __init__(
        self,
        # Görselleştirme
        landmark_id: str = "lidar_scan_preview",
        max_points: int = 2048,
        emit_dense_points: bool = False,
        dense_id: str = "lidar_scan_dense",
        downsample_step: int = 3,

        # Sensör extrinsics (body frame'e göre)
        sensor_x: float = 0.0,             # m
        sensor_y: float = 0.0,             # m
        sensor_z: float = 0.0,             # m (şimdilik XY projeksiyonda yalnızca XY kullanıyoruz)
        sensor_yaw_deg: float = 0.0,       # sensörün kendi Z ekseni etrafında ofseti
        sensor_pitch_deg: float = 0.0,     # sensörün kendi Y ekseni etrafında ofseti
        sensor_roll_deg: float = 0.0,      # sensörün kendi X ekseni etrafında ofseti

        # Filtre
        range_min: float = 0.05,
        range_max: float = 60.0
    ):
        # Önizleme
        self.landmark_id = landmark_id
        self.max_points = int(max_points)
        self.emit_dense_points = bool(emit_dense_points)
        self.dense_id = dense_id
        self.downsample_step = max(1, int(downsample_step))

        # Extrinsics
        self.sensor_xyz = (float(sensor_x), float(sensor_y), float(sensor_z))
        self.sensor_yaw = _deg2rad(float(sensor_yaw_deg))
        self.sensor_pitch = _deg2rad(float(sensor_pitch_deg))
        self.sensor_roll = _deg2rad(float(sensor_roll_deg))

        # Aralık filtresi
        self.rmin = float(range_min)
        self.rmax = float(range_max)

        # Yayınlanacak son dünya noktaları
        self._last_pts_world: List[Tuple[float, float]] = []

    # ---------- lifecycle ----------

    def on_init(self, ctx: FusionContext) -> None:
        self._last_pts_world = []

    def _build_rotation_world_from_sensor(self, ctx: FusionContext):
        """
        R_ws = R_world_from_body * R_body_from_sensor
        R_world_from_body = Rz(yaw) * Ry(pitch) * Rx(roll)
        R_body_from_sensor = Rz(syaw) * Ry(spitch) * Rx(sroll)
        """
        # body → world (ctx’den)
        Rwb = _Rz(_deg2rad(ctx.yaw_deg))
        # pitch ve roll ekle
        Rwb = _mat3_mul(Rwb, _Ry(_deg2rad(getattr(ctx, "pitch_deg", 0.0))))
        Rwb = _mat3_mul(Rwb, _Rx(_deg2rad(getattr(ctx, "roll_deg", 0.0))))

        # sensor → body (extrinsics)
        Rsb = _Rz(self.sensor_yaw)
        Rsb = _mat3_mul(Rsb, _Ry(self.sensor_pitch))
        Rsb = _mat3_mul(Rsb, _Rx(self.sensor_roll))

        # sensor → world
        Rws = _mat3_mul(Rwb, Rsb)
        return Rws, Rwb

    def on_samples(self, ctx: FusionContext, samples: List[Sample]) -> None:
        # En son LiDAR taramasını bul
        scan = None
        for s in reversed(samples):
            if getattr(s, "sensor", None) == "lidar":
                scan = s
                break
        if scan is None:
            return

        sd: Dict[str, Any] = scan.data or {}
        ranges = list(sd.get("ranges", []))[: self.max_points]
        if not ranges:
            self._last_pts_world = []
            return

        angle_min = float(sd.get("angle_min", -math.pi))
        angle_inc = float(sd.get("angle_increment", math.radians(1.0)))

        # Dönüş matrisi ve sensör konumunun world karşılığı
        Rws, Rwb = self._build_rotation_world_from_sensor(ctx)

        # sensör konumu (body frame’de verilen offsetin world karşılığı)
        sx, sy, sz = self.sensor_xyz
        sxw, syw, szw = _mat3_vec3(Rwb, (sx, sy, sz))  # body→world
        x0, y0 = ctx.x + sxw, ctx.y + syw

        # Sensör düzleminde (z=0) noktaları üret ve world’e dönüştür
        pts_world: List[Tuple[float, float]] = []
        a = angle_min
        for r in ranges:
            try:
                r = float(r)
            except Exception:
                r = 0.0
            if not (self.rmin <= r <= self.rmax) or not math.isfinite(r):
                a += angle_inc
                continue

            # sensor frame (z=0 düzleminde)
            xs = r * math.cos(a)
            ys = r * math.sin(a)
            zs = 0.0

            # world frame’e döndür
            Xw, Yw, Zw = _mat3_vec3(Rws, (xs, ys, zs))
            pts_world.append((x0 + Xw, y0 + Yw))
            a += angle_inc

        self._last_pts_world = pts_world

    def on_before_emit(self, ctx: FusionContext, out_state: FusedState) -> None:
        if not self._last_pts_world:
            return

        # Önizleme polyline
        ctx.add_landmark({
            "id": self.landmark_id,
            "type": "obstacles",
            "shape": "polyline",
            "points": self._last_pts_world,
            "style": {"width": 1}
        })

        # Yoğun nokta seti (opsiyonel, downsample)
        if self.emit_dense_points:
            dense = self._last_pts_world[::self.downsample_step]
            if dense:
                ctx.add_landmark({
                    "id": self.dense_id,
                    "type": "obstacles_dense",
                    "shape": "polyline",
                    "points": dense,
                    "style": {"width": 1}
                })

    def on_close(self, ctx: FusionContext) -> None:
        self._last_pts_world = []
