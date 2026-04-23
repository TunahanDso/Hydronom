from __future__ import annotations
import math
from typing import List, Tuple, Dict, Any

from fusion.context import FusionContext
from core.sample import Sample
from core.fused_state import FusedState
from fusion.plugins.base import IFuserPlugin


class LidarObstaclePlugin(IFuserPlugin):
    """
    TEKNOFEST parkurunu sentetik olarak world/map frame içinde oluşturan plugin.

    Not:
    - Bu sürüm LiDAR verisine bağlı değildir.
    - Ama sınıf adı korunmuştur ki registry / mevcut entegrasyon bozulmasın.
    - İstenirse ileride adı ayrıca StaticCoursePlugin olarak ayrılabilir.

    Üretilen içerikler:
    - Parkur 1 kenar dubaları
    - Parkur 2 kenar dubaları
    - Parkur 2 engel dubaları
    - Parkur 3 hedef dubaları
    - Başlangıç / giriş / kıyı çizgileri
    - Runtime obstacles listesi
    """

    name = "lidar_obstacles"

    def __init__(
        self,
        landmark_id: str = "teknofest_course",
        obstacle_id: str = "teknofest_runtime_obstacles",
        course_origin_x: float = 0.0,
        course_origin_y: float = 0.0,
        scale: float = 1.0,
        emit_obstacles: bool = True,
        emit_dense_points: bool = True,
        dense_id: str = "teknofest_course_dense",
        downsample_step: int = 1,
    ):
        self.landmark_id = landmark_id
        self.obstacle_id = obstacle_id
        self.course_origin_x = float(course_origin_x)
        self.course_origin_y = float(course_origin_y)
        self.scale = float(scale)
        self.emit_obstacles = bool(emit_obstacles)
        self.emit_dense_points = bool(emit_dense_points)
        self.dense_id = dense_id
        self.downsample_step = max(1, int(downsample_step))

        self._built = False
        self._all_points: List[Tuple[float, float]] = []
        self._obstacles: List[Dict[str, Any]] = []
        self._landmarks: List[Dict[str, Any]] = []

    def _p(self, x: float, y: float) -> Tuple[float, float]:
        return (
            self.course_origin_x + x * self.scale,
            self.course_origin_y + y * self.scale,
        )

    def _interp_polyline(self, pts: List[Tuple[float, float]], step: float = 1.5) -> List[Tuple[float, float]]:
        if len(pts) < 2:
            return pts[:]

        out: List[Tuple[float, float]] = []
        for i in range(len(pts) - 1):
            x1, y1 = pts[i]
            x2, y2 = pts[i + 1]
            dx = x2 - x1
            dy = y2 - y1
            dist = math.hypot(dx, dy)
            n = max(1, int(dist / max(0.1, step)))
            for k in range(n):
                t = k / n
                out.append((x1 + dx * t, y1 + dy * t))
        out.append(pts[-1])
        return out

    def _add_buoy_line(
        self,
        points: List[Tuple[float, float]],
        buoy_type: str,
        radius: float,
        id_prefix: str,
    ) -> None:
        for i, (x, y) in enumerate(points):
            self._obstacles.append({
                "x": x,
                "y": y,
                "radius": radius,
                "points": 1,
                "kind": buoy_type,
                "id": f"{id_prefix}_{i}",
            })

    def _add_polyline_landmark(
        self,
        landmark_id: str,
        points: List[Tuple[float, float]],
        landmark_type: str,
        width: int = 1,
        color: str = "#ffffff",
    ) -> None:
        self._landmarks.append({
            "id": landmark_id,
            "type": landmark_type,
            "shape": "polyline",
            "points": points,
            "style": {
                "width": width,
                "color": color,
            }
        })

    def _add_point_landmark(
        self,
        landmark_id: str,
        points: List[Tuple[float, float]],
        landmark_type: str,
        color: str = "#ffffff",
    ) -> None:
        self._landmarks.append({
            "id": landmark_id,
            "type": landmark_type,
            "shape": "points",
            "points": points,
            "style": {
                "width": 2,
                "color": color,
            }
        })

    def _build_course_once(self) -> None:
        if self._built:
            return

        self._all_points = []
        self._obstacles = []
        self._landmarks = []

        sea_left = self._p(0, 0)
        sea_right = self._p(220, 0)
        shoreline_left = self._p(0, -18)
        shoreline_right = self._p(220, -18)

        self._add_polyline_landmark(
            "shoreline",
            [shoreline_left, shoreline_right],
            "shoreline",
            width=2,
            color="#cccccc",
        )

        self._add_polyline_landmark(
            "sea_axis",
            [sea_left, sea_right],
            "sea_axis",
            width=1,
            color="#88bbff",
        )

        start_box = [
            self._p(4, 10),
            self._p(10, 10),
            self._p(10, 16),
            self._p(4, 16),
            self._p(4, 10),
        ]
        self._add_polyline_landmark(
            "start_box",
            start_box,
            "start_zone",
            width=2,
            color="#000000",
        )

        bb = self._p(8, 13)
        self._obstacles.append({
            "x": bb[0],
            "y": bb[1],
            "radius": 1.2 * self.scale,
            "points": 1,
            "kind": "start",
            "id": "BB"
        })

        gn_points = [
            self._p(25, 18),
            self._p(38, 32),
            self._p(54, 18),
            self._p(66, 36),
            self._p(82, 22),
        ]
        self._add_point_landmark(
            "entry_nodes",
            gn_points,
            "entry_nodes",
            color="#4477ff",
        )

        for i, pt in enumerate(gn_points):
            self._obstacles.append({
                "x": pt[0],
                "y": pt[1],
                "radius": 1.0 * self.scale,
                "points": 1,
                "kind": "entry",
                "id": f"GN{i+1}",
            })

        p1_left_ctrl = [
            self._p(20, 14),
            self._p(30, 28),
            self._p(40, 16),
            self._p(52, 31),
            self._p(62, 15),
            self._p(74, 30),
        ]
        p1_right_ctrl = [
            self._p(24, 24),
            self._p(34, 38),
            self._p(46, 26),
            self._p(56, 41),
            self._p(68, 24),
            self._p(80, 40),
        ]

        p1_left = self._interp_polyline(p1_left_ctrl, step=4.0 * self.scale)
        p1_right = self._interp_polyline(p1_right_ctrl, step=4.0 * self.scale)

        self._add_polyline_landmark("parkur1_left", p1_left, "parkur1_edge_left", width=1, color="#ff9955")
        self._add_polyline_landmark("parkur1_right", p1_right, "parkur1_edge_right", width=1, color="#ff9955")

        self._add_buoy_line(p1_left, "edge_buoy", 0.55 * self.scale, "p1l")
        self._add_buoy_line(p1_right, "edge_buoy", 0.55 * self.scale, "p1r")

        p2_left_ctrl = [
            self._p(92, 34),
            self._p(112, 31),
            self._p(132, 30),
            self._p(152, 29),
            self._p(172, 31),
            self._p(192, 34),
        ]
        p2_right_ctrl = [
            self._p(94, 14),
            self._p(114, 12),
            self._p(134, 12),
            self._p(154, 13),
            self._p(174, 14),
            self._p(194, 15),
        ]

        p2_left = self._interp_polyline(p2_left_ctrl, step=5.0 * self.scale)
        p2_right = self._interp_polyline(p2_right_ctrl, step=5.0 * self.scale)

        self._add_polyline_landmark("parkur2_left", p2_left, "parkur2_edge_left", width=1, color="#ff9955")
        self._add_polyline_landmark("parkur2_right", p2_right, "parkur2_edge_right", width=1, color="#ff9955")

        self._add_buoy_line(p2_left, "edge_buoy", 0.55 * self.scale, "p2l")
        self._add_buoy_line(p2_right, "edge_buoy", 0.55 * self.scale, "p2r")

        obstacle_points = [
            self._p(112, 24),
            self._p(126, 22),
            self._p(140, 26),
            self._p(154, 20),
            self._p(168, 24),
            self._p(182, 27),
        ]
        self._add_point_landmark(
            "parkur2_obstacles",
            obstacle_points,
            "parkur2_obstacles",
            color="#ffcc22",
        )
        for i, pt in enumerate(obstacle_points):
            self._obstacles.append({
                "x": pt[0],
                "y": pt[1],
                "radius": 0.85 * self.scale,
                "points": 1,
                "kind": "obstacle_buoy",
                "id": f"ENG{i+1}",
            })

        p3_target_points = [
            self._p(210, 33),
            self._p(210, 25),
            self._p(210, 17),
        ]
        self._add_point_landmark(
            "parkur3_targets",
            p3_target_points,
            "parkur3_targets",
            color="#55ff55",
        )

        target_kinds = ["target_red", "target_green", "target_black"]
        for i, pt in enumerate(p3_target_points):
            self._obstacles.append({
                "x": pt[0],
                "y": pt[1],
                "radius": 0.95 * self.scale,
                "points": 1,
                "kind": target_kinds[i],
                "id": f"Hedef-{i+1}",
            })

        iha_box = [
            self._p(86, -48),
            self._p(114, -48),
            self._p(114, -22),
            self._p(86, -22),
            self._p(86, -48),
        ]
        self._add_polyline_landmark(
            "iha_zone",
            iha_box,
            "iha_zone",
            width=1,
            color="#aa88ff",
        )

        self._all_points = [(o["x"], o["y"]) for o in self._obstacles]
        self._built = True

    def on_init(self, ctx: FusionContext) -> None:
        self._build_course_once()

    def on_samples(self, ctx: FusionContext, samples: List[Sample]) -> None:
        self._build_course_once()

    def on_before_emit(self, ctx: FusionContext, out_state: FusedState) -> None:
        self._build_course_once()

        if self._all_points:
            ctx.add_landmark({
                "id": self.landmark_id,
                "type": "obstacles",
                "shape": "points",
                "points": self._all_points,
                "style": {
                    "width": 2,
                    "color": "#ffffff"
                }
            })

        if self.emit_dense_points and self._all_points:
            dense = self._all_points[::self.downsample_step]
            if dense:
                ctx.add_landmark({
                    "id": self.dense_id,
                    "type": "obstacles_dense",
                    "shape": "points",
                    "points": dense,
                    "style": {
                        "width": 2,
                        "color": "#dddddd"
                    }
                })

        for lm in self._landmarks:
            ctx.add_landmark(lm)

        if self.emit_obstacles and self._obstacles:
            obstacle_points = [(o["x"], o["y"]) for o in self._obstacles]
            obstacle_payload = [
                {
                    "x": o["x"],
                    "y": o["y"],
                    "r": o["radius"],
                    "radius": o["radius"],
                    "kind": o.get("kind", ""),
                    "id": o.get("id", "")
                }
                for o in self._obstacles
            ]

            ctx.add_landmark({
                "id": self.obstacle_id,
                "type": "runtime_obstacles",
                "shape": "points",
                "points": obstacle_points,
                "obstacles": self._obstacles,
                "style": {
                    "width": 2,
                    "color": "#ffaa33"
                }
            })

            if not hasattr(out_state, "inputs") or out_state.inputs is None:
                out_state.inputs = []

            out_state.inputs.append({
                "_source": "runtime_obstacles",
                "data": {
                    "obstacles": obstacle_payload
                }
            })

    def on_close(self, ctx: FusionContext) -> None:
        self._built = False
        self._all_points = []
        self._obstacles = []
        self._landmarks = []