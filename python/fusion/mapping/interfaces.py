# fusion/mapping/interfaces.py
from typing import Protocol, Optional, Tuple, List

class IMapper(Protocol):
    """
    Basit haritalama arayüzü (occupancy grid vb.)
    """
    def reset(self) -> None: ...
    def update_from_scan(self,
                         pose_xy_yaw: Tuple[float, float, float],
                         ranges: List[float],
                         angle_min: float,
                         angle_increment: float,
                         range_min: float,
                         range_max: float) -> None: ...
    def get_preview_polyline(self) -> List[Tuple[float, float]]: ...
