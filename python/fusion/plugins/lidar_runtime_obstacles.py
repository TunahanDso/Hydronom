from __future__ import annotations

import math
from typing import Any, Dict, List, Optional, Tuple

from core.fused_state import FusedState
from core.sample import Sample
from fusion.context import FusionContext
from fusion.plugins.base import IFuserPlugin

Point2D = Tuple[float, float]
Mat3 = Tuple[Tuple[float, float, float], Tuple[float, float, float], Tuple[float, float, float]]
Obstacle = Dict[str, float]


def deg2rad(angle_deg: float) -> float:
    return angle_deg * math.pi / 180.0


def rot_x(rad: float) -> Mat3:
    c, s = math.cos(rad), math.sin(rad)
    return (
        (1.0, 0.0, 0.0),
        (0.0, c, -s),
        (0.0, s, c),
    )


def rot_y(rad: float) -> Mat3:
    c, s = math.cos(rad), math.sin(rad)
    return (
        (c, 0.0, s),
        (0.0, 1.0, 0.0),
        (-s, 0.0, c),
    )


def rot_z(rad: float) -> Mat3:
    c, s = math.cos(rad), math.sin(rad)
    return (
        (c, -s, 0.0),
        (s, c, 0.0),
        (0.0, 0.0, 1.0),
    )


def mat3_mul(a: Mat3, b: Mat3) -> Mat3:
    return tuple(
        tuple(
            a[i][0] * b[0][j] + a[i][1] * b[1][j] + a[i][2] * b[2][j]
            for j in range(3)
        )
        for i in range(3)
    )  # type: ignore[return-value]


def mat3_vec3(m: Mat3, v: Tuple[float, float, float]) -> Tuple[float, float, float]:
    return (
        m[0][0] * v[0] + m[0][1] * v[1] + m[0][2] * v[2],
        m[1][0] * v[0] + m[1][1] * v[1] + m[1][2] * v[2],
        m[2][0] * v[0] + m[2][1] * v[1] + m[2][2] * v[2],
    )


class LidarRuntimeObstaclePlugin(IFuserPlugin):
    """
    LiDAR taramasından runtime tarafı için sade obstacle listesi üretir.

    Akış:
    1. Son lidar sample'ını al
    2. Ranges verisini world koordinatına taşı
    3. Yakın noktaları kümelendir
    4. Her kümeyi dairesel obstacle adayına çevir
    5. Gerekirse duplicate obstacle'ları birleştir
    6. En yakın obstacle'ları FusedState içine göm
    """

    name = "lidar_runtime_obstacles"

    def __init__(
        self,
        # Sensor extrinsics
        sensor_x: float = 0.0,
        sensor_y: float = 0.0,
        sensor_z: float = 0.0,
        sensor_yaw_deg: float = 0.0,
        sensor_pitch_deg: float = 0.0,
        sensor_roll_deg: float = 0.0,
        # Range filters
        range_min: float = 0.05,
        range_max: float = 60.0,
        # Clustering
        cluster_gap_m: float = 1.20,
        min_cluster_points: int = 1,
        max_cluster_points: int = 10_000,
        max_obstacles: int = 24,
        # Obstacle size limits
        min_radius_m: float = 0.15,
        max_radius_m: float = 3.0,
        # Noise reduction
        downsample_step: int = 1,
        # Debug
        debug: bool = False,
    ):
        self.sensor_xyz = (float(sensor_x), float(sensor_y), float(sensor_z))
        self.sensor_yaw = deg2rad(float(sensor_yaw_deg))
        self.sensor_pitch = deg2rad(float(sensor_pitch_deg))
        self.sensor_roll = deg2rad(float(sensor_roll_deg))

        self.range_min = float(range_min)
        self.range_max = float(range_max)

        self.cluster_gap_m = float(cluster_gap_m)
        self.min_cluster_points = max(1, int(min_cluster_points))
        self.max_cluster_points = max(self.min_cluster_points, int(max_cluster_points))
        self.max_obstacles = max(1, int(max_obstacles))

        self.min_radius_m = float(min_radius_m)
        self.max_radius_m = float(max_radius_m)

        self.downsample_step = max(1, int(downsample_step))
        self.debug = bool(debug)

        self._last_obstacles: List[Obstacle] = []

    def _log(self, message: str) -> None:
        if self.debug:
            print(f"[LRO] {message}")

    def on_init(self, ctx: FusionContext) -> None:
        self._last_obstacles = []

    def on_close(self, ctx: FusionContext) -> None:
        self._last_obstacles = []

    def _build_world_rotations(self, ctx: FusionContext) -> Tuple[Mat3, Mat3]:
        """
        Rwb: Body -> World
        Rsb: Sensor -> Body
        Rws: Sensor -> World
        """
        yaw = deg2rad(getattr(ctx, "yaw_deg", 0.0))
        pitch = deg2rad(getattr(ctx, "pitch_deg", 0.0))
        roll = deg2rad(getattr(ctx, "roll_deg", 0.0))

        rwb = mat3_mul(rot_z(yaw), rot_y(pitch))
        rwb = mat3_mul(rwb, rot_x(roll))

        rsb = mat3_mul(rot_z(self.sensor_yaw), rot_y(self.sensor_pitch))
        rsb = mat3_mul(rsb, rot_x(self.sensor_roll))

        rws = mat3_mul(rwb, rsb)
        return rws, rwb

    def _find_latest_lidar_sample(self, samples: List[Sample]) -> Optional[Sample]:
        for sample in reversed(samples):
            if getattr(sample, "sensor", None) == "lidar":
                return sample
        return None

    def _extract_world_points(self, ctx: FusionContext, scan: Sample) -> List[Point2D]:
        data: Dict[str, Any] = scan.data or {}
        ranges = list(data.get("ranges", []))
        if not ranges:
            return []

        if self.downsample_step > 1:
            ranges = ranges[::self.downsample_step]

        angle_min = float(data.get("angle_min", -math.pi))
        angle_increment = float(data.get("angle_increment", math.radians(1.0))) * self.downsample_step

        rws, rwb = self._build_world_rotations(ctx)

        sensor_x, sensor_y, sensor_z = self.sensor_xyz
        sensor_world_x, sensor_world_y, _ = mat3_vec3(rwb, (sensor_x, sensor_y, sensor_z))

        origin_x = ctx.x + sensor_world_x
        origin_y = ctx.y + sensor_world_y

        points_world: List[Point2D] = []
        angle = angle_min

        for raw_range in ranges:
            try:
                r = float(raw_range)
            except Exception:
                r = 0.0

            if not math.isfinite(r) or r < self.range_min or r > self.range_max:
                angle += angle_increment
                continue

            sensor_px = r * math.cos(angle)
            sensor_py = r * math.sin(angle)
            sensor_pz = 0.0

            world_dx, world_dy, _ = mat3_vec3(rws, (sensor_px, sensor_py, sensor_pz))
            points_world.append((origin_x + world_dx, origin_y + world_dy))

            angle += angle_increment

        return points_world

    def _cluster_points(self, points: List[Point2D]) -> List[List[Point2D]]:
        if not points:
            return []

        clusters: List[List[Point2D]] = []
        current_cluster: List[Point2D] = [points[0]]

        for point in points[1:]:
            prev = current_cluster[-1]
            distance = math.hypot(point[0] - prev[0], point[1] - prev[1])

            if distance <= self.cluster_gap_m:
                current_cluster.append(point)
            else:
                clusters.append(current_cluster)
                current_cluster = [point]

        if current_cluster:
            clusters.append(current_cluster)

        return clusters

    def _cluster_to_obstacle(self, cluster: List[Point2D]) -> Optional[Obstacle]:
        point_count = len(cluster)
        if point_count < 1 or point_count > self.max_cluster_points:
            return None

        center_x = sum(px for px, _ in cluster) / point_count
        center_y = sum(py for _, py in cluster) / point_count

        radius = 0.0
        for px, py in cluster:
            radius = max(radius, math.hypot(px - center_x, py - center_y))

        if point_count < self.min_cluster_points:
            radius = max(radius, self.min_radius_m)

        radius = max(self.min_radius_m, min(self.max_radius_m, radius))

        return {
            "x": center_x,
            "y": center_y,
            "r": radius,
        }

    def _fallback_point_obstacles(self, points_world: List[Point2D]) -> List[Obstacle]:
        self._log("no cluster-derived obstacles, using point fallback")

        fallback: List[Obstacle] = []
        for px, py in points_world[: self.max_obstacles]:
            fallback.append({
                "x": px,
                "y": py,
                "r": self.min_radius_m,
            })
        return fallback

    def _deduplicate_obstacles(
        self,
        obstacles: List[Obstacle],
        merge_dist_m: float = 0.75,
    ) -> List[Obstacle]:
        if not obstacles:
            return []

        merged_list: List[Obstacle] = []

        for obstacle in obstacles:
            merged = False

            for kept in merged_list:
                distance = math.hypot(obstacle["x"] - kept["x"], obstacle["y"] - kept["y"])
                if distance <= merge_dist_m:
                    kept["x"] = 0.5 * (kept["x"] + obstacle["x"])
                    kept["y"] = 0.5 * (kept["y"] + obstacle["y"])
                    kept["r"] = max(kept["r"], obstacle["r"])
                    merged = True
                    break

            if not merged:
                merged_list.append(dict(obstacle))

        return merged_list

    def _sort_by_vehicle_distance(self, ctx: FusionContext, obstacles: List[Obstacle]) -> List[Obstacle]:
        return sorted(
            obstacles,
            key=lambda obs: math.hypot(obs["x"] - ctx.x, obs["y"] - ctx.y),
        )

    def on_samples(self, ctx: FusionContext, samples: List[Sample]) -> None:
        scan = self._find_latest_lidar_sample(samples)
        if scan is None:
            self._last_obstacles = []
            self._log("no lidar sample")
            return

        points_world = self._extract_world_points(ctx, scan)
        self._log(f"world_points={len(points_world)}")

        if not points_world:
            self._last_obstacles = []
            self._log("no valid points after range/filter transform")
            return

        clusters = self._cluster_points(points_world)
        self._log(f"clusters={len(clusters)} sizes={[len(c) for c in clusters[:12]]}")

        obstacles: List[Obstacle] = []
        for cluster in clusters:
            obstacle = self._cluster_to_obstacle(cluster)
            if obstacle is not None:
                obstacles.append(obstacle)

        if not obstacles:
            obstacles = self._fallback_point_obstacles(points_world)

        obstacles = self._deduplicate_obstacles(obstacles)
        obstacles = self._sort_by_vehicle_distance(ctx, obstacles)

        self._last_obstacles = obstacles[: self.max_obstacles]

        self._log(f"emitted_obstacles={len(self._last_obstacles)}")
        if self._last_obstacles:
            self._log(f"preview={self._last_obstacles[:5]}")

    def on_before_emit(self, ctx: FusionContext, out_state: FusedState) -> None:
        if not self._last_obstacles:
            self._log("emit skipped: no obstacles")
            return

        self._log(f"emit obstacles={len(self._last_obstacles)}")
        out_state.inputs.append({
            "_source": "runtime_obstacles",
            "data": {
                "obstacles": self._last_obstacles
            }
        })