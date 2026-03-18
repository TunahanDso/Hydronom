import math
import random
from typing import List, Iterable, Tuple, Optional


class SimBackend:
    """
    Senaryo tabanlı LiDAR simülasyonu.

    Amaç:
    - Karar ve analiz katmanını gerçekten zorlayacak obstacle desenleri üretmek
    - Tekne önünde net engel, koridor, duvar, slalom gibi test senaryoları sağlamak
    - LidarSensor'ın hem read_ranges(...) hem read_scan(...) çağrılarını desteklemek

    Desteklenen senaryolar:
    - front_block : tam ön hatta engel
    - left_wall   : solda duvar benzeri yakın yüzey
    - right_wall  : sağda duvar benzeri yakın yüzey
    - corridor    : iki yanda duvar, ortada geçit
    - slalom      : sırayla sağ-sol engeller
    - open_water  : neredeyse boş alan
    """

    def __init__(self):
        self._phase = 0.0
        self._open = False

        self._scenario = "front_block"
        self._base_range = 8.0
        self._noise = 0.04

        # Ön engel parametreleri
        self._front_distance = 3.0
        self._front_half_angle_deg = 12.0

        # Koridor/duvar parametreleri
        self._wall_distance = 2.2
        self._corridor_half_open_deg = 18.0

    def open(self, cfg=None, *args, **kwargs) -> None:
        self._open = True

        # Config içinden senaryo okunabiliyorsa al
        if cfg is not None:
            try:
                scenario = getattr(cfg, "sim_scenario", None)
                if scenario:
                    self._scenario = str(scenario).strip().lower()
            except Exception:
                pass

            try:
                v = getattr(cfg, "sim_base_range", None)
                if v is not None:
                    self._base_range = float(v)
            except Exception:
                pass

            try:
                v = getattr(cfg, "sim_noise", None)
                if v is not None:
                    self._noise = float(v)
            except Exception:
                pass

            try:
                v = getattr(cfg, "sim_front_distance", None)
                if v is not None:
                    self._front_distance = float(v)
            except Exception:
                pass

            try:
                v = getattr(cfg, "sim_front_half_angle_deg", None)
                if v is not None:
                    self._front_half_angle_deg = float(v)
            except Exception:
                pass

            try:
                v = getattr(cfg, "sim_wall_distance", None)
                if v is not None:
                    self._wall_distance = float(v)
            except Exception:
                pass

            try:
                v = getattr(cfg, "sim_corridor_half_open_deg", None)
                if v is not None:
                    self._corridor_half_open_deg = float(v)
            except Exception:
                pass

    def read_ranges(self, *args, **kwargs) -> List[float]:
        """
        Desteklenen çağrılar:
          read_ranges(n, range_min, range_max, timeout_s=None)
          read_ranges(angle_min=..., angle_max=..., angle_increment=..., range_min=..., range_max=..., timeout_s=None)
        """
        if not self._open:
            raise RuntimeError("LiDAR sim backend not open")

        self._phase += 0.12

        # Zengin imza
        if "angle_min" in kwargs:
            angle_min = float(kwargs["angle_min"])
            angle_max = float(kwargs["angle_max"])
            angle_inc = float(kwargs["angle_increment"])
            range_min = float(kwargs["range_min"])
            range_max = float(kwargs["range_max"])

            n = int(round((angle_max - angle_min) / angle_inc)) + 1
            return self._gen_ranges(angle_min, angle_inc, n, range_min, range_max)

        # Basit imza
        if len(args) < 3:
            raise TypeError("read_ranges expects either kwargs form or (n, range_min, range_max[, timeout_s])")

        n = int(args[0])
        range_min = float(args[1])
        range_max = float(args[2])

        # Varsayılan FOV varsayımı
        fov_deg = 270.0
        angle_min = -math.radians(fov_deg) / 2.0
        angle_inc = math.radians(fov_deg / max(1, n - 1))

        return self._gen_ranges(angle_min, angle_inc, n, range_min, range_max)

    def read_scan(self, timeout_s: Optional[float] = None) -> Iterable[Tuple[float, float]]:
        """
        (angle, r) noktaları döndürür.
        """
        if not self._open:
            raise RuntimeError("LiDAR sim backend not open")

        fov_deg = 270.0
        angle_min = -math.radians(fov_deg) / 2.0
        count = 181
        angle_inc = math.radians(fov_deg / max(1, count - 1))

        ranges = self._gen_ranges(angle_min, angle_inc, count, 0.15, 30.0)

        a = angle_min
        for r in ranges:
            yield (a, r)
            a += angle_inc

    def _gen_ranges(
        self,
        angle_min: float,
        angle_inc: float,
        n: int,
        range_min: float,
        range_max: float
    ) -> List[float]:
        ranges: List[float] = []

        for i in range(n):
            angle = angle_min + i * angle_inc
            angle_deg = math.degrees(angle)

            val = self._base_with_noise(range_max)

            if self._scenario == "front_block":
                val = min(val, self._scenario_front_block(angle_deg))

            elif self._scenario == "left_wall":
                val = min(val, self._scenario_left_wall(angle_deg))

            elif self._scenario == "right_wall":
                val = min(val, self._scenario_right_wall(angle_deg))

            elif self._scenario == "corridor":
                val = min(val, self._scenario_corridor(angle_deg))

            elif self._scenario == "slalom":
                val = min(val, self._scenario_slalom(angle_deg))

            elif self._scenario == "open_water":
                pass

            val = max(range_min, min(range_max, val))
            ranges.append(float(val))

        return ranges

    def _base_with_noise(self, range_max: float) -> float:
        """
        Arka plan mesafesi.
        """
        drift = 0.25 * math.sin(self._phase * 0.35)
        noise = random.uniform(-self._noise, self._noise)
        return min(range_max, self._base_range + drift + noise)

    def _scenario_front_block(self, angle_deg: float) -> float:
        """
        Tam ön hatta engel.
        """
        half = self._front_half_angle_deg

        if abs(angle_deg) <= half:
            edge_k = abs(angle_deg) / max(half, 1e-6)
            edge_boost = 0.35 * edge_k
            wave = 0.08 * math.sin(self._phase * 1.7 + angle_deg * 0.08)
            return self._front_distance + edge_boost + wave

        return 999.0

    def _scenario_left_wall(self, angle_deg: float) -> float:
        """
        Sol tarafta yaklaşık duvar.
        """
        if 25.0 <= angle_deg <= 85.0:
            wave = 0.10 * math.sin(self._phase + angle_deg * 0.05)
            return self._wall_distance + wave
        return 999.0

    def _scenario_right_wall(self, angle_deg: float) -> float:
        """
        Sağ tarafta yaklaşık duvar.
        """
        if -85.0 <= angle_deg <= -25.0:
            wave = 0.10 * math.sin(self._phase + abs(angle_deg) * 0.05)
            return self._wall_distance + wave
        return 999.0

    def _scenario_corridor(self, angle_deg: float) -> float:
        """
        Ortada geçit, iki yanda yakın yüzey.
        """
        open_half = self._corridor_half_open_deg

        if abs(angle_deg) <= open_half:
            return 999.0

        if 22.0 <= abs(angle_deg) <= 75.0:
            wave = 0.08 * math.sin(self._phase * 1.2 + angle_deg * 0.04)
            return self._wall_distance + wave

        return 999.0

    def _scenario_slalom(self, angle_deg: float) -> float:
        """
        Sağ-sol dönüşümlü engel hissi veren basit desen.
        """
        t = math.sin(self._phase * 0.9)

        # Bir fazda sağda, diğer fazda solda yakın engel
        if t >= 0:
            if -18.0 <= angle_deg <= -4.0:
                return 2.8 + 0.15 * math.sin(self._phase * 1.5)
            if 10.0 <= angle_deg <= 24.0:
                return 5.5 + 0.10 * math.sin(self._phase)
        else:
            if 4.0 <= angle_deg <= 18.0:
                return 2.8 + 0.15 * math.sin(self._phase * 1.5)
            if -24.0 <= angle_deg <= -10.0:
                return 5.5 + 0.10 * math.sin(self._phase)

        return 999.0

    def close(self) -> None:
        self._open = False