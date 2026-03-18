# sensors/base_sensor.py

import abc
import time
from typing import Any, Dict, List, Optional

from core.sample import Sample

# Ortak seri port yardımcıları (tüm sensörler kullanabilsin diye burada da yansıtıyoruz)
try:
    # Yeni ortak modül: sensors/serial_utils.py
    from sensors.serial_utils import find_serial_port, list_serial_ports  # noqa: F401
except Exception:
    # Modül yoksa stub oluştur (çalışmayı engellemesin)
    def find_serial_port(*args, **kwargs) -> Optional[str]:  # type: ignore
        return None

    def list_serial_ports(*args, **kwargs) -> List[str]:  # type: ignore
        return []


class BaseSensor(abc.ABC):
    """
    Tüm sensör sürücüleri için temel soyut sınıf.
    - Birleşik yaşam döngüsü: open() / read() / close()
    - Simülasyon/gerçek mod farkı için 'simulate' bayrağı
    - Ortak alanlar: source, frame_id, enabled, rate_hz
    - StreamSubscribe için varsayılan apply_stream_subscribe()
    - Cross-platform port keşfi için serial_utils köprüsü
    """

    # İsteğe bağlı: alt sınıflar SENSOR adını override edebilir (örn: "imu", "gps", "lidar")
    SENSOR: Optional[str] = None

    def __init__(self, source: str, frame_id: str = "base_link", simulate: bool = False):
        self.source = source
        self.frame_id = frame_id
        self.simulate = simulate

        self.is_open: bool = False
        self.enabled: bool = True

        # Opsiyonel hedef yayın hızı (Hz). Alt sınıf kullanmak zorunda değil.
        self.rate_hz: Optional[float] = None
        self._last_emit_ts: float = 0.0

        # Telemetri/kimlik alanları (Capability için faydalı)
        self.fields: Optional[List[Dict[str, Any]]] = None
        self.calib_id: Optional[str] = None
        self.quality_hints: Optional[Dict[str, Any]] = None

        self.seq: int = 0

    # ---- Soyut arayüz ----
    @abc.abstractmethod
    def open(self) -> None:
        """Sensörü başlatır (seri port, soket, kamera vb.)."""
        raise NotImplementedError

    @abc.abstractmethod
    def read(self) -> Sample:
        """Bir ölçüm alır ve Sample nesnesi döner."""
        raise NotImplementedError

    @abc.abstractmethod
    def close(self) -> None:
        """Sensörü güvenli şekilde kapatır."""
        raise NotImplementedError

    # ---- Yaygın yardımcılar ----
    def _new_seq(self) -> int:
        """Her ölçüm için benzersiz artan sıra numarası üretir."""
        self.seq += 1
        return self.seq

    def _stamp(self) -> float:
        """Gerçek zaman damgası (POSIX)."""
        return time.time()

    @property
    def sensor_name(self) -> str:
        """SENSOR sabiti varsa onu, yoksa sınıf adından türetilmiş adı döner."""
        if isinstance(self.SENSOR, str) and self.SENSOR:
            return self.SENSOR
        cls = type(self).__name__
        return cls[:-6].lower() if cls.endswith("Sensor") else cls.lower()

    # ---- Hız sınırlama için opsiyonel yardımcı (alt sınıflar isterse kullanır) ----
    def _should_emit(self, now_ts: Optional[float] = None) -> bool:
        """
        rate_hz ayarlıysa yayın aralığını korumak için True/False döner.
        Alt sınıf read() içinde kullanabilir:
            if not self._should_emit(): return self._cached_sample
        """
        if self.rate_hz is None or self.rate_hz <= 0:
            return True
        now = self._stamp() if now_ts is None else now_ts
        period = 1.0 / float(self.rate_hz)
        if (now - self._last_emit_ts) >= period:
            self._last_emit_ts = now
            return True
        return False

    def set_rate_hz(self, hz: float) -> None:
        """Çalışma zamanı hedef yayın hızını ayarla."""
        try:
            hz = float(hz)
            self.rate_hz = max(0.0, hz)
        except Exception:
            # Sessiz geç; sensörler bu özelliği kullanmayabilir
            pass

    # ---- StreamSubscribe için varsayılan uygulama ----
    def apply_stream_subscribe(self, spec: Dict[str, Any]) -> Dict[str, Any]:
        """
        Ortak alanları yorumlar: enable, rate_hz
        Alt sınıflar genişletebilir (backend, port, baud vb.).
        """
        changed: Dict[str, Any] = {}
        if not isinstance(spec, dict):
            return changed

        if "enable" in spec:
            self.enabled = bool(spec["enable"])
            changed["enabled"] = self.enabled

        if "rate_hz" in spec:
            try:
                self.set_rate_hz(float(spec["rate_hz"]))
                changed["rate_hz"] = self.rate_hz
            except Exception as e:
                changed["rate_error"] = str(e)

        return changed

    # ---- Context manager kolaylığı ----
    def __enter__(self):
        self.open()
        return self

    def __exit__(self, exc_type, exc, tb):
        try:
            self.close()
        finally:
            # Hata bastırma yok; exception varsa yukarı aksın
            return False

    # ---- Temel güvenlik yardımcıları ----
    def _ensure_open(self) -> None:
        if not self.is_open:
            raise RuntimeError(f"{self.sensor_name} not opened (source={self.source}).")
