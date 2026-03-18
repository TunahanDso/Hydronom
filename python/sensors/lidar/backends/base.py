# sensors/lidar/backends/base.py
from typing import (
    List,
    Iterable,
    Tuple,
    Union,
    Optional,
    Protocol,
    overload,
    runtime_checkable,
)

# Not: Backend'ler EITHER read_ranges OR read_scan sağlayabilir.
# LidarSensor, hasattr(...) ile her iki yolu da tolere eder.

Point2D = Tuple[float, float]                 # (x, y) veya (angle, r)
Point3D = Tuple[float, float, float]          # (x, y, z)
PointLike = Union[Point2D, Point3D]


@runtime_checkable
class ILidarBackend(Protocol):
    """
    LiDAR backend arayüzü.

    open():
      - Yeni API: open(cfg: LidarConfig)
      - Geriye dönük: open()

    read_ranges():
      - Basit imza: read_ranges(n, range_min, range_max, timeout_s=None) -> List[float]
      - Zengin imza: read_ranges(*, angle_min, angle_max, angle_increment,
                                 range_min, range_max, timeout_s=None) -> List[float]
      - Mesafeler metre cinsinden; 0.0 => isabet yok/ölçüm yok.

    read_scan():
      - Alternatif yol: Nokta listesi döndür (x,y[,z]) veya (angle,r)
      - LidarSensor bu noktaları sabit açı ızgarasına bind eder.

    close(): Kaynakları serbest bırak.
    """

    # --- open (overload'lar) ---
    @overload
    def open(self, cfg: "LidarConfig") -> None: ...
    @overload
    def open(self) -> None: ...

    # --- read_ranges (overload'lar) ---
    @overload
    def read_ranges(
        self,
        n: int,
        range_min: float,
        range_max: float,
        timeout_s: Optional[float] = ...,
    ) -> List[float]: ...
    @overload
    def read_ranges(
        self,
        *,
        angle_min: float,
        angle_max: float,
        angle_increment: float,
        range_min: float,
        range_max: float,
        timeout_s: Optional[float] = ...,
    ) -> List[float]: ...

    # --- read_scan (opsiyonel) ---
    def read_scan(self, timeout_s: Optional[float] = ...) -> Iterable[PointLike]: ...

    # --- close ---
    def close(self) -> None: ...


# -----------------------------------------------------------------------------
# GERİYE DÖNÜK UYUMLULUK (legacy)
# Eski modüller "from sensors.lidar.backends.base import LidarBackend" diyebilir.
# Bu yüzden ILidarBackend'e alias veriyoruz.
# -----------------------------------------------------------------------------
LidarBackend = ILidarBackend
