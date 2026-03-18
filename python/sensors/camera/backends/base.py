# sensors/camera/backends/base.py
from typing import Optional, Tuple, Any, Protocol

class ICameraBackend(Protocol):
    def open(self, cfg: Any) -> None: ...
    def read_frame(self) -> Optional[Tuple[Any, float]]:
        """
        Dönüş: (frame, t_cam)
        - frame: numpy.ndarray (H,W,3) BGR
        - t_cam: epoch(s) float (yakalama anı)
        Non-blocking: en son frame’i döndürür; yoksa None.
        """
        ...
    def close(self) -> None: ...
