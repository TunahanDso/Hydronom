# sensors/lidar/laser_scan.py
from typing import List
from core.sample import Sample

class LaserScanSample(Sample):
    """ROS benzeri LaserScan JSON’u üretir."""
    def __init__(self, *,
                 ranges: List[float],
                 angle_min: float,
                 angle_increment: float,
                 range_min: float,
                 range_max: float,
                 sensor: str, source: str, frame_id: str,
                 seq: int, t: float):
        data = {
            "angle_min": angle_min,
            "angle_max": angle_min + angle_increment * (len(ranges) - 1),
            "angle_increment": angle_increment,
            "range_min": range_min,
            "range_max": range_max,
            "ranges": ranges,
        }
        super().__init__(sensor=sensor, source=source, data=data,
                         frame_id=frame_id, quality={"valid": True},
                         seq=seq, t=t)
        self.type = "LaserScan"
