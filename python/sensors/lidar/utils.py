# sensors/lidar/utils.py
import math
from typing import List, Tuple

def cloud_to_scan(points_xy: List[Tuple[float,float]],
                  angle_min: float, angle_inc: float, bins: int,
                  rmin: float, rmax: float) -> List[float]:
    out = [rmax]*bins
    amax = angle_min + angle_inc*(bins-1)
    for x,y in points_xy:
        r = math.hypot(x,y)
        if r < rmin or r > rmax: continue
        ang = math.atan2(y,x)
        if ang < angle_min or ang > amax: continue
        i = int(round((ang-angle_min)/angle_inc))
        if 0 <= i < bins and r < out[i]:
            out[i] = r
    return out
