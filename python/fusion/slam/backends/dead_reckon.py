# fusion/slam/backends/dead_reckon.py
import time
from typing import Optional, List, Tuple, Any

class DeadReckonBackend:
    """
    Çok basit odometri arka ucu (iskelet):
    - IMU z-ekseni açısal hız (gz, rad/s) entegrasyonundan kademeli yaw farkı (deg) üretir.
    - dt sıçramalarına karşı tavan, per-step yaw sınırlama ve hafif alçak geçiren filtre içerir.
    - dx/dy entegrasyonu bu sürümde kapalı (ileride hız/tam imu verisi ile eklenebilir).
    """

    def __init__(
        self,
        max_dt: float = 0.2,           # saniye; dt bu değeri aşarsa kesilir (spike koruması)
        max_step_deg: float = 8.0,     # tek update'te izin verilen en büyük yaw adımı (deg)
        yaw_lpf_alpha: float = 0.2     # gz (deg/s) için basit EMA katsayısı (0..1)
    ):
        self.max_dt = float(max_dt)
        self.max_step_deg = float(max_step_deg)
        self.yaw_lpf_alpha = float(yaw_lpf_alpha)

        self._last_t: Optional[float] = None
        self._gz_deg_s_filt: float = 0.0  # filtrelenmiş açısal hız (deg/s)

        # Kademeli delta akümülatörleri
        self._dyaw_acc: float = 0.0
        self._dx_acc: float = 0.0
        self._dy_acc: float = 0.0

    def open(self) -> None:
        self._last_t = time.time()
        self._gz_deg_s_filt = 0.0
        self._dyaw_acc = 0.0
        self._dx_acc = 0.0
        self._dy_acc = 0.0

    def update(self, samples: List[Any], pose_guess_xy_yaw: Tuple[float, float, float]) -> None:
        now = time.time()
        last = self._last_t or now
        dt = now - last
        if dt < 0:
            dt = 0.0
        if dt > self.max_dt:
            dt = self.max_dt  # aşırı uzun araları kes (spike koruması)
        self._last_t = now

        # İlgili sensörleri topla
        imu = next((s for s in samples if getattr(s, "sensor", None) == "imu"), None)

        # IMU gz (rad/s) → deg/s ve EMA ile yumuşatma
        if imu is not None:
            try:
                gz_rad_s = float((imu.data or {}).get("gz", 0.0))
            except Exception:
                gz_rad_s = 0.0
            gz_deg_s = gz_rad_s * (180.0 / 3.141592653589793)

            # EMA: filt = alpha*in + (1-alpha)*old
            a = self.yaw_lpf_alpha
            self._gz_deg_s_filt = a * gz_deg_s + (1.0 - a) * self._gz_deg_s_filt

            # Entegre et → dyaw (deg)
            step = self._gz_deg_s_filt * dt

            # Tek adım sınırlama (gürültü/sıçrama bastırma)
            if step > self.max_step_deg:
                step = self.max_step_deg
            elif step < -self.max_step_deg:
                step = -self.max_step_deg

            self._dyaw_acc += step

        # dx/dy için şimdilik bir şey yapmıyoruz (ileride hız/odometri ile eklenebilir)

    def get_pose_delta(self) -> Optional[Tuple[float, float, float]]:
        total = abs(self._dx_acc) + abs(self._dy_acc) + abs(self._dyaw_acc)
        if total == 0.0:
            return None
        dx, dy, dyaw = self._dx_acc, self._dy_acc, self._dyaw_acc
        # Sıfırla (kademeli delta veriyoruz)
        self._dx_acc = 0.0
        self._dy_acc = 0.0
        self._dyaw_acc = 0.0
        return (dx, dy, dyaw)

    def close(self) -> None:
        # Şimdilik özel temizlik yok
        pass
