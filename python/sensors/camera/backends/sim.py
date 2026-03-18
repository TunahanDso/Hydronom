# sensors/camera/backends/sim.py
import time, numpy as np
from typing import Optional, Tuple, Any
from .base import ICameraBackend

class SimBackend(ICameraBackend):
    def __init__(self):
        self._cfg = None
        self._t0 = time.time()
        self._last = None

    def open(self, cfg: Any) -> None:
        self._cfg = cfg
        self._t0 = time.time()
        self._last = None

    def read_frame(self) -> Optional[Tuple[Any, float]]:
        if self._cfg is None:
            return None
        # Basit gradient + hareketli kare
        w, h = int(self._cfg.width), int(self._cfg.height)
        t = time.time() - self._t0
        img = np.zeros((h, w, 3), dtype=np.uint8)
        # arka plan gradient
        x = np.linspace(0, 255, w, dtype=np.uint8)
        img[:, :, 0] = x
        img[:, :, 1] = x[::-1]
        # hareketli kutu
        cx = int((w/2) + (w/3)*np.sin(t*0.8))
        cy = int((h/2) + (h/4)*np.cos(t*0.6))
        sz = max(10, min(w, h)//8)
        x0, x1 = max(0, cx-sz), min(w, cx+sz)
        y0, y1 = max(0, cy-sz), min(h, cy+sz)
        img[y0:y1, x0:x1, :] = (40, 200, 40)
        t_cam = time.time()
        self._last = (img, t_cam)
        return self._last

    def close(self) -> None:
        self._last = None
