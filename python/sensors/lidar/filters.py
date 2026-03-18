# sensors/lidar/filters.py
from typing import List

def clamp_ranges(r: List[float], lo: float, hi: float) -> List[float]:
    return [min(hi, max(lo, v)) for v in r]
