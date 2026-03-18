# sensors/imu/backends/sim.py
import math
import time
import random
from typing import Dict, Any, Optional

from .base import IImuBackend
from sensors.twin_bus import TwinBus  # ← C# dijital ikizinden IMU verisi buradan okunacak


class SimBackend(IImuBackend):
    """
    IMU simülasyon backend'i (iki modlu):

    1) backend = "sim"
       - Sabit bir yaw hızı (gz) etrafında döner.
       - Roll/Pitch küçük salınımlar + Gauss gürültü.
       - İvmeölçer çıktıları yerçekimiyle (9.81 m/s^2) tutarlı üretilir.
       - Gyro çıktıları rad/s (gx, gy, gz).
       - İsteğe bağlı manyetometre alanları (mx,my,mz) eklenebilir (şimdilik None).

    2) backend = "csharp_sim"
       - Dijital ikiz / gölge sim modu:
         * KESİNLİKLE rastgele veri üretmez.
         * C# runtime'ın gönderdiği TwinImu mesajlarını kullanır.
         * Taze twin verisi yoksa boş dict döndürür (ImuSensor tarafında valid=False olur).
    """

    def __init__(
        self,
        rate_hz: float = 100.0,
        yaw_rate_deg_s: float = 10.0,      # sabit yaw hızı (deg/s)
        roll_base_deg: float = 2.0,        # temel roll (deg)
        pitch_base_deg: float = 1.0,       # temel pitch (deg)
        wobble_amp_deg: float = 0.8,       # roll/pitch için küçük salınım genliği
        accel_noise_std: float = 0.05,     # m/s^2
        gyro_noise_std: float = 0.01       # rad/s
    ):
        # --- Örnekleme hızı ---
        self._hz = float(rate_hz)
        self._dt_target = 1.0 / max(1e-3, self._hz)

        # --- İç durum ---
        self._t0: Optional[float] = None
        self._t_last: Optional[float] = None
        self._yaw_deg: float = 0.0
        self._yaw_rate_rad: float = float(yaw_rate_deg_s) * math.pi / 180.0

        # --- Parametreler ---
        self._roll_base = float(roll_base_deg)
        self._pitch_base = float(pitch_base_deg)
        self._wobble_amp = float(wobble_amp_deg)
        self._accel_noise_std = float(accel_noise_std)
        self._gyro_noise_std = float(gyro_noise_std)

        # --- Sabitler ---
        self._g = 9.81  # m/s^2

        # --- Durum bayrakları ---
        self._opened = False

        # Hangi moddayız? ("sim" mi, "csharp_sim" mi)
        self._twin_mode: bool = False

    # ---------------- Yaşam döngüsü ----------------

    def open(self, *args, **kwargs) -> None:
        """
        Not: ImuSensor bazı backend'lere cfg/source gibi argümanlar geçebilir.
        Uyumluluk için esnek imza kullanıyoruz.
        Burada aynı zamanda backend == "csharp_sim" mi diye bakıp twin modunu açıyoruz.
        """
        # cfg varsa backend adına göre mod seç
        cfg = args[0] if args else None
        backend_name = None
        if cfg is not None and hasattr(cfg, "backend"):
            try:
                backend_name = getattr(cfg, "backend", None)
            except Exception:
                backend_name = None
        backend_name = (backend_name or "sim").lower()
        self._twin_mode = (backend_name == "csharp_sim")

        now = time.time()
        self._t0 = now
        self._t_last = now
        # twin modda olsa bile burada bir başlangıç değerimiz olsun (telemetri için)
        self._yaw_deg = random.uniform(0.0, 360.0)
        self._opened = True

    def close(self) -> None:
        self._opened = False

    # ---------------- Ayarlar ----------------

    def set_rate_hz(self, hz: float) -> bool:
        """Örnekleme hızını güncelle."""
        try:
            self._hz = float(hz)
            self._dt_target = 1.0 / max(1e-3, self._hz)
            return True
        except Exception:
            return False

    # ---------------- Yardımcılar ----------------

    def _step(self) -> float:
        """Zamanı ilerlet, dt döndür."""
        now = time.time()
        if self._t_last is None:
            self._t_last = now
            return 0.0
        dt = now - self._t_last
        self._t_last = now
        return max(0.0, dt)

    def _roll_pitch_deg(self, t_rel: float) -> (float, float):
        """
        Küçük salınımlar eklenmiş roll/pitch üret.
        roll = base + A*sin(ωt)
        pitch = base + A*cos(ωt')
        """
        w1 = 2.0 * math.pi * 0.2   # 0.2 Hz
        w2 = 2.0 * math.pi * 0.12  # 0.12 Hz
        roll = self._roll_base + self._wobble_amp * math.sin(w1 * t_rel)
        pitch = self._pitch_base + self._wobble_amp * math.cos(w2 * t_rel)
        return roll, pitch

    def _accel_from_attitude(self, roll_deg: float, pitch_deg: float) -> (float, float, float):
        """
        Basit yerçekimi projeksiyonu:
        - IMU gövde eksenlerinde yalnızca yerçekimi (hareket yok varsayımı).
        - Küçük açılar için yeterli doğruluk.
        """
        roll = math.radians(roll_deg)
        pitch = math.radians(pitch_deg)

        # Gövde eksenleri: x (ileri), y (sağ), z (aşağı)
        # Yerçekimi vektörü dünyada aşağı doğru (0,0,+g). Gövdeye projeksiyon:
        ax =  self._g * math.sin(pitch)                  # ileri-geri
        ay = -self._g * math.sin(roll) * math.cos(pitch) # sağ-sol
        az =  self._g * math.cos(roll) * math.cos(pitch) # aşağı

        # Gürültü ekle
        ax += random.gauss(0.0, self._accel_noise_std)
        ay += random.gauss(0.0, self._accel_noise_std)
        az += random.gauss(0.0, self._accel_noise_std)
        return ax, ay, az

    def _gyro_from_rates(self, yaw_rate_rad: float) -> (float, float, float):
        """Gyro rad/s: burada yalnızca yaw ekseninde sabit hız + gürültü var."""
        gx = random.gauss(0.0, self._gyro_noise_std)
        gy = random.gauss(0.0, self._gyro_noise_std)
        gz = yaw_rate_rad + random.gauss(0.0, self._gyro_noise_std)
        return gx, gy, gz

    # ---------------- Twin modu okuma ----------------

    def _read_twin(self) -> Dict[str, Any]:
        """
        C# dijital ikizinden gelen TwinImu verisini kullanır.
        TwinBus üzerinde taze bir kayıt yoksa boş dict döndürür.
        (ImuSensor bu durumda valid=False sample üretecek.)
        """
        twin = TwinBus.get_imu(max_age_s=0.5)
        if twin is None:
            return {}
        # TwinBus, C# mesajını olduğu gibi taşıyor:
        # Beklenen alanlar: ax, ay, az, gx, gy, gz, mx, my, mz, temp_c, t_imu, ...
        return twin

    # ---------------- Okuma ----------------

    def read(self) -> Dict[str, Any]:
        """
        IMU ham ölçümlerini döndürür:
          - ax, ay, az (m/s^2)
          - gx, gy, gz (rad/s)
          - roll_deg, pitch_deg, yaw_deg (opsiyonel/telemetri)
        """
        # Twin modda isek C# verisini kullan, kesinlikle random üretme
        if self._twin_mode:
            return self._read_twin()

        # Güvenlik: open() başarısız çağrıldıysa, burada kendi kendini başlat.
        if not self._opened or self._t0 is None or self._t_last is None:
            self.open()

        dt = self._step()

        # Hedef örnekleme hızını kabaca yakalamak için kısa gecikme (opsiyonel)
        if dt < self._dt_target * 0.5:
            time.sleep(max(0.0, self._dt_target * 0.5 - dt))
            dt += self._step()

        # Yaw güncelle (deg)
        self._yaw_deg = (self._yaw_deg + (self._yaw_rate_rad * 180.0 / math.pi) * dt) % 360.0

        # Roll/Pitch üret
        t_rel = (self._t_last - self._t0) if (self._t0 and self._t_last) else 0.0
        roll_deg, pitch_deg = self._roll_pitch_deg(t_rel)

        # Ölçümler
        ax, ay, az = self._accel_from_attitude(roll_deg, pitch_deg)
        gx, gy, gz = self._gyro_from_rates(self._yaw_rate_rad)

        return {
            "ax": ax, "ay": ay, "az": az,           # m/s^2
            "gx": gx, "gy": gy, "gz": gz,           # rad/s
            "roll_deg": roll_deg,
            "pitch_deg": pitch_deg,
            "yaw_deg": self._yaw_deg                # opsiyonel/telemetri
        }

    # Bazı ImuSensor sürümleri read_imu() arayabilir; uyumluluk için alias.
    def read_imu(self) -> Dict[str, Any]:
        return self.read()
