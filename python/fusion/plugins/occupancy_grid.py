from __future__ import annotations

from typing import Optional, List, Dict, Any

from fusion.plugins.base import IFuserPlugin
from fusion.context import FusionContext
from fusion.mapping.occupancy_grid import OccupancyGrid


class OccupancyGridPlugin(IFuserPlugin):
    """
    LiDAR LaserScan verisini occupancy grid motoruna işler ve
    GUI / Hydronom Ops için hafif önizleme + export verisi üretir.

    Notlar:
    - Bu sınıf plugin katmanıdır.
    - Asıl grid mantığı fusion.mapping.occupancy_grid.OccupancyGrid içindedir.
    - LiDAR örneğini sample.data içinden okur.
    """

    name = "occupancy_grid"

    def __init__(
        self,
        resolution: float = 0.10,
        size_w: int = 400,
        size_h: int = 400,
        origin_x: float = -20.0,
        origin_y: float = -20.0,
        landmark_id: str = "occ_poly",
        color: str = "#ff8800",
        preview_max_points: int = 500,
        preview_min_probability: float = 0.70,
        logit_hit: float = 0.85,
        logit_free: float = -0.40,
        logit_min: float = -4.0,
        logit_max: float = 4.0,
        occ_threshold: float = 0.0,
        decay_per_update: float = 0.0,
        max_updates_before_decay: int = 0,
        no_return_margin: float = 0.15,
        emit_preview_landmark: bool = True,
        emit_points_landmark: bool = False,
        export_max_points: int = 800,
        input_info_period_s: float = 0.35,
    ):
        self.grid = OccupancyGrid(
            resolution=resolution,
            size=(size_w, size_h),
            origin=(origin_x, origin_y),
            logit_hit=logit_hit,
            logit_free=logit_free,
            logit_min=logit_min,
            logit_max=logit_max,
            occ_threshold=occ_threshold,
            preview_max_points=preview_max_points,
            preview_min_probability=preview_min_probability,
            decay_per_update=decay_per_update,
            max_updates_before_decay=max_updates_before_decay,
            no_return_margin=no_return_margin,
        )

        self._last_scan: Optional[Dict[str, Any]] = None
        self._landmark_id = landmark_id
        self._color = color

        self._emit_preview_landmark = bool(emit_preview_landmark)
        self._emit_points_landmark = bool(emit_points_landmark)
        self._export_max_points = int(export_max_points)
        self._input_info_period_s = float(input_info_period_s)

        self._tele_last = 0.0
        self._scan_count = 0
        self._last_preview_count = 0
        self._last_export_count = 0
        self._last_pose: Optional[Dict[str, float]] = None

    def on_init(self, ctx: FusionContext) -> None:
        self._last_scan = None
        self._tele_last = 0.0
        self._scan_count = 0
        self._last_preview_count = 0
        self._last_export_count = 0
        self._last_pose = None

    def _pick_lidar_scan(self, samples: List[object]) -> Optional[Dict[str, Any]]:
        """
        En son LiDAR sample'ını bulur ve data sözlüğünü döndürür.
        """
        for s in reversed(samples):
            if getattr(s, "sensor", None) != "lidar":
                continue

            data = getattr(s, "data", None)
            if not isinstance(data, dict):
                continue

            if "ranges" in data and "angle_min" in data and "angle_increment" in data:
                return data

        return None

    def on_samples(self, ctx: FusionContext, samples: List[object]) -> None:
        self._last_scan = self._pick_lidar_scan(samples)

    def _emit_preview(self, ctx: FusionContext, preview_pts: List[tuple[float, float]]) -> None:
        if not self._emit_preview_landmark:
            return
        if not preview_pts:
            return

        try:
            ctx.add_landmark({
                "id": self._landmark_id,
                "type": "occupancy_preview",
                "shape": "polyline",
                "points": preview_pts,
                "style": {
                    "color": self._color,
                    "width": 1.0,
                    "label": "occ"
                },
            })
        except Exception:
            pass

    def _emit_points_export(self, ctx: FusionContext) -> None:
        """
        Hydronom Ops / Gateway tarafında point-cloud benzeri overlay olarak
        kullanılabilecek occupied cell export'unu landmark olarak bırakır.
        """
        if not self._emit_points_landmark:
            return

        try:
            pts = self.grid.export_occupied_cells(
                min_probability=self.grid.preview_min_probability,
                max_points=self._export_max_points,
            )
        except Exception:
            return

        self._last_export_count = len(pts)

        if not pts:
            return

        try:
            ctx.add_landmark({
                "id": f"{self._landmark_id}_cells",
                "type": "occupancy_cells",
                "shape": "points",
                "points": [(float(p["x"]), float(p["y"])) for p in pts],
                "style": {
                    "color": self._color,
                    "radius": 0.06,
                    "label": "occ_cells"
                },
            })
        except Exception:
            pass

    def _emit_input_info(self, ctx: FusionContext) -> None:
        now = float(getattr(ctx, "now", 0.0))
        if (now - self._tele_last) < self._input_info_period_s:
            return

        meta = self.grid.get_metadata()

        info = {
            "cells_w": int(self.grid.width),
            "cells_h": int(self.grid.height),
            "resolution_m": float(self.grid.resolution),
            "origin_x": float(meta.get("origin_x", 0.0)),
            "origin_y": float(meta.get("origin_y", 0.0)),
            "occ_threshold": float(meta.get("occ_threshold", 0.0)),
            "preview_points": int(self._last_preview_count),
            "export_points": int(self._last_export_count),
            "scan_count": int(self._scan_count),
        }

        if self._last_pose is not None:
            info["pose_x"] = round(float(self._last_pose.get("x", 0.0)), 3)
            info["pose_y"] = round(float(self._last_pose.get("y", 0.0)), 3)
            info["pose_yaw_deg"] = round(float(self._last_pose.get("yaw", 0.0)), 3)

        try:
            if hasattr(ctx, "add_input_info"):
                ctx.add_input_info("occupancy_grid", info)
            elif hasattr(ctx, "add_input"):
                payload = dict(info)
                payload["source"] = "occupancy_grid"
                ctx.add_input(payload)
        except Exception:
            pass

        self._tele_last = now

    def on_before_emit(self, ctx: FusionContext, fused) -> None:
        scan = self._last_scan
        if not scan:
            return

        try:
            ranges = list(scan.get("ranges", []))
            angle_min = float(scan.get("angle_min", 0.0))
            angle_increment = float(scan.get("angle_increment", 0.0))
            range_min = float(scan.get("range_min", 0.05))
            range_max = float(scan.get("range_max", 30.0))
        except Exception:
            return

        if not ranges or angle_increment <= 0.0:
            return

        try:
            pose = ctx.get_pose()
            px = float(pose["x"])
            py = float(pose["y"])
            pyaw = float(pose["yaw"])
        except Exception:
            return

        self._last_pose = {
            "x": px,
            "y": py,
            "yaw": pyaw,
        }

        try:
            self.grid.update_from_scan(
                pose=(px, py, pyaw),
                ranges=ranges,
                angle_min=angle_min,
                angle_increment=angle_increment,
                range_min=range_min,
                range_max=range_max,
            )
            self._scan_count += 1
        except Exception:
            return

        preview_pts = []
        try:
            preview_pts = self.grid.get_preview_polyline()
        except Exception:
            preview_pts = []

        self._last_preview_count = len(preview_pts)

        self._emit_preview(ctx, preview_pts)
        self._emit_points_export(ctx)
        self._emit_input_info(ctx)

    def on_close(self, ctx: FusionContext) -> None:
        self._last_scan = None
        self._last_pose = None