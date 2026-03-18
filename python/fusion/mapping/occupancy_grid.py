from __future__ import annotations

import math
from typing import List, Tuple, Optional, Dict, Any


class OccupancyGrid:
    """
    Güçlendirilmiş 2D occupancy grid motoru.

    Amaç:
    - LiDAR scan verisini dünya koordinatına işlemek
    - Serbest alan / engel hücresi log-odds güncellemesi yapmak
    - UI / Hydronom Ops için hafif önizleme ve dışa aktarım üretmek

    Notlar:
    - Bu sınıf plugin değildir, saf mapping motorudur.
    - Plugin katmanı fusion/plugins/occupancy_grid.py içinde kalır.
    - Grid dünya frame'inde sabit tutulur.
    """

    def __init__(
        self,
        resolution: float = 0.10,
        size: Tuple[int, int] = (400, 400),
        origin: Tuple[float, float] = (-20.0, -20.0),
        logit_hit: float = 0.55,
        logit_free: float = -0.40,
        logit_min: float = -4.0,
        logit_max: float = 4.0,
        occ_threshold: float = 0.0,
        preview_max_points: int = 500,
        preview_min_probability: float = 0.70,
        decay_per_update: float = 0.0,
        max_updates_before_decay: int = 0,
        no_return_margin: float = 0.15,
    ):
        self.resolution = float(resolution)
        self.width = int(size[0])
        self.height = int(size[1])

        self.origin_x = float(origin[0])
        self.origin_y = float(origin[1])

        self.logit_hit = float(logit_hit)
        self.logit_free = float(logit_free)
        self.logit_min = float(logit_min)
        self.logit_max = float(logit_max)
        self.occ_threshold = float(occ_threshold)

        self.preview_max_points = int(preview_max_points)
        self.preview_min_probability = float(preview_min_probability)

        # Her update sonrası tüm grid'e çok hafif sönüm uygulanabilir
        self.decay_per_update = float(decay_per_update)
        self.max_updates_before_decay = int(max_updates_before_decay)

        # range_max civarı okuma geldiyse bunu "uçta gerçek hit" gibi değil,
        # çoğu zaman "no-return / free-only" gibi ele almak daha sağlıklı olabilir
        self.no_return_margin = float(no_return_margin)

        self._grid: List[List[float]] = [
            [0.0 for _ in range(self.width)]
            for _ in range(self.height)
        ]

        self._update_count = 0

    # ------------------------------------------------------------
    # Dünya <-> Grid dönüşümleri
    # ------------------------------------------------------------

    def world_to_grid(self, x: float, y: float) -> Optional[Tuple[int, int]]:
        i = int((x - self.origin_x) / self.resolution)
        j = int((y - self.origin_y) / self.resolution)

        if 0 <= i < self.width and 0 <= j < self.height:
            return i, j
        return None

    def grid_to_world(self, i: int, j: int) -> Tuple[float, float]:
        x = self.origin_x + (i + 0.5) * self.resolution
        y = self.origin_y + (j + 0.5) * self.resolution
        return x, y

    # ------------------------------------------------------------
    # Log-odds işlemleri
    # ------------------------------------------------------------

    def _accumulate(self, i: int, j: int, delta: float) -> None:
        v = self._grid[j][i] + delta
        if v < self.logit_min:
            v = self.logit_min
        elif v > self.logit_max:
            v = self.logit_max
        self._grid[j][i] = v

    def _apply_global_decay(self) -> None:
        """
        Tüm hücreleri sıfıra doğru hafifçe yaklaştır.
        Dinamik ortamda eski izlerin sonsuza kadar kalmasını azaltır.
        """
        d = self.decay_per_update
        if d <= 0.0:
            return

        for j in range(self.height):
            row = self._grid[j]
            for i in range(self.width):
                v = row[i]
                if v > 0.0:
                    v = max(0.0, v - d)
                elif v < 0.0:
                    v = min(0.0, v + d)
                row[i] = v

    def clear(self) -> None:
        for j in range(self.height):
            row = self._grid[j]
            for i in range(self.width):
                row[i] = 0.0

    # ------------------------------------------------------------
    # Ana scan güncellemesi
    # ------------------------------------------------------------

    def update_from_scan(
        self,
        pose: Tuple[float, float, float],
        ranges: List[float],
        angle_min: float,
        angle_increment: float,
        range_min: float,
        range_max: float,
    ) -> None:
        """
        pose = (x, y, yaw_deg)
        ranges = lidar mesafeleri
        açı birimleri radyan
        """

        if not ranges:
            return
        if angle_increment <= 0.0:
            return
        if range_max <= range_min:
            return

        self._update_count += 1
        if self.max_updates_before_decay > 0:
            if (self._update_count % self.max_updates_before_decay) == 0:
                self._apply_global_decay()
        elif self.decay_per_update > 0.0:
            self._apply_global_decay()

        x0, y0, yaw_deg = pose
        yaw_rad = math.radians(yaw_deg)

        angle = angle_min
        for r in ranges:
            try:
                r = float(r)
            except Exception:
                angle += angle_increment
                continue

            if not math.isfinite(r) or r <= 0.0:
                angle += angle_increment
                continue

            if r < range_min:
                angle += angle_increment
                continue

            # max-range civarı değerler çoğu zaman "uçta gerçek hit" değildir
            # bu durumda sadece free-space güncellemesi yap
            is_no_return = r >= (range_max - self.no_return_margin)

            r_eff = min(r, range_max)
            beam_world = yaw_rad + angle

            x1 = x0 + r_eff * math.cos(beam_world)
            y1 = y0 + r_eff * math.sin(beam_world)

            self._raytrace_update(
                x0=x0,
                y0=y0,
                x1=x1,
                y1=y1,
                hit=not is_no_return and (r <= range_max),
            )

            angle += angle_increment

    def _raytrace_update(
        self,
        x0: float,
        y0: float,
        x1: float,
        y1: float,
        hit: bool = True
    ) -> None:
        """
        Sensörden hedef noktaya kadar serbest alan günceller,
        uç noktayı işgal hücresi olarak işaretler.

        Not:
        - Aynı ışın üzerinde aynı hücreye tekrar tekrar free update yapılmaz.
        - Hit hücresi free yol güncellemesinden ayrılır.
        """

        dx = x1 - x0
        dy = y1 - y0
        dist = math.hypot(dx, dy)
        if dist <= 1e-9:
            return

        # Daha düzgün traversal için çözünürlüğe yakın adım
        step = max(self.resolution * 0.5, 1e-4)
        steps = max(1, int(dist / step))

        hit_ij = self.world_to_grid(x1, y1) if hit else None

        visited_free = set()

        for s in range(steps):
            t = s / float(steps)
            xs = x0 + t * dx
            ys = y0 + t * dy

            ij = self.world_to_grid(xs, ys)
            if ij is None:
                continue

            if hit_ij is not None and ij == hit_ij:
                continue

            if ij in visited_free:
                continue

            visited_free.add(ij)
            self._accumulate(ij[0], ij[1], self.logit_free)

        if hit and hit_ij is not None:
            self._accumulate(hit_ij[0], hit_ij[1], self.logit_hit)

    # ------------------------------------------------------------
    # Sorgu / Dışa aktarım
    # ------------------------------------------------------------

    def get_cell_logit(self, i: int, j: int) -> float:
        return self._grid[j][i]

    def get_probability(self, i: int, j: int) -> float:
        """
        Log-odds -> probability
        """
        l = self._grid[j][i]
        return 1.0 / (1.0 + math.exp(-l))

    def get_probability_grid(self) -> List[List[float]]:
        out: List[List[float]] = []
        for j in range(self.height):
            row_out: List[float] = []
            row = self._grid[j]
            for i in range(self.width):
                row_out.append(1.0 / (1.0 + math.exp(-row[i])))
            out.append(row_out)
        return out

    def get_metadata(self) -> Dict[str, Any]:
        return {
            "width": self.width,
            "height": self.height,
            "resolution": self.resolution,
            "origin_x": self.origin_x,
            "origin_y": self.origin_y,
            "occ_threshold": self.occ_threshold,
            "preview_max_points": self.preview_max_points,
            "preview_min_probability": self.preview_min_probability,
        }

    # ------------------------------------------------------------
    # Görselleştirme / önizleme
    # ------------------------------------------------------------

    def get_occupied_points(self) -> List[Tuple[float, float]]:
        pts: List[Tuple[float, float]] = []

        min_prob = self.preview_min_probability
        for j in range(self.height):
            row = self._grid[j]
            for i in range(self.width):
                v = row[i]
                if v <= self.occ_threshold:
                    continue

                p = 1.0 / (1.0 + math.exp(-v))
                if p < min_prob:
                    continue

                pts.append(self.grid_to_world(i, j))

        return pts

    def get_preview_polyline(self) -> List[Tuple[float, float]]:
        """
        UI için hafif önizleme.
        İşgal hücrelerini toplar, grid merkezi etrafında açısal sıralar,
        sonra downsample eder.
        """

        pts = self.get_occupied_points()
        if not pts:
            return []

        cx = self.origin_x + 0.5 * self.width * self.resolution
        cy = self.origin_y + 0.5 * self.height * self.resolution

        pts.sort(key=lambda p: math.atan2(p[1] - cy, p[0] - cx))

        if len(pts) > self.preview_max_points:
            step = max(1, len(pts) // self.preview_max_points)
            pts = pts[::step]

        return pts

    def get_preview_points(self) -> List[Dict[str, float]]:
        """
        Hydronom Ops / Gateway tarafında daha kolay taşınabilsin diye
        nokta listesini sözlük biçiminde döndürür.
        """
        out: List[Dict[str, float]] = []
        for x, y in self.get_preview_polyline():
            out.append({"x": float(x), "y": float(y)})
        return out

    def export_occupied_cells(
        self,
        min_probability: Optional[float] = None,
        max_points: Optional[int] = None,
    ) -> List[Dict[str, float]]:
        """
        Occupied hücreleri dışa aktar.
        Ops tarafında contour / point-cloud / map overlay için uygundur.
        """
        if min_probability is None:
            min_probability = self.preview_min_probability

        pts: List[Dict[str, float]] = []

        for j in range(self.height):
            row = self._grid[j]
            for i in range(self.width):
                p = 1.0 / (1.0 + math.exp(-row[i]))
                if p < min_probability:
                    continue

                x, y = self.grid_to_world(i, j)
                pts.append({
                    "x": float(x),
                    "y": float(y),
                    "p": float(p),
                })

        if max_points is not None and max_points > 0 and len(pts) > max_points:
            step = max(1, len(pts) // max_points)
            pts = pts[::step]

        return pts