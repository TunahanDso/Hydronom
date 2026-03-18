# sensors/imu/backends/base.py
from __future__ import annotations

from typing import Optional, Dict, Any, Protocol, runtime_checkable

from sensors.imu.config import ImuConfig

# Ortak seri port yardımcıları (varsa kullan, yoksa tolere et)
try:
    from sensors.common.ports import find_serial_port, list_serial_ports  # type: ignore
except Exception:
    def find_serial_port(hints: Optional[list[str]] = None) -> Optional[str]:  # fallback
        return None
    def list_serial_ports() -> list[str]:  # fallback
        return []


@runtime_checkable
class IImuBackend(Protocol):
    """
    read_imu() NON-BLOCKING olmalı ve mümkünse şu alanları döndürmeli:

    {
      "ax": float|None, "ay": float|None, "az": float|None,   # m/s^2 (body frame)
      "gx": float|None, "gy": float|None, "gz": float|None,   # rad/s  (body frame)
      "mx": float|None, "my": float|None, "mz": float|None,   # µT     (opsiyonel)
      "temp_c": float|None,                                   # °C     (opsiyonel)
      "t_imu": float|None                                     # epoch(UTC, s) sensör TS; yoksa None
    }

    Zamanlama: open(cfg) çağrısından sonra backend kendi okuma iş parçacığını
    (varsa) başlatabilir. read_imu() en son ölçümü anında döndürmelidir.
    """
    def open(self, cfg: ImuConfig) -> None: ...
    def read_imu(self) -> Optional[Dict[str, Any]]: ...
    def close(self) -> None: ...


class BaseImuBackend:
    """
    İsteğe bağlı yardımcı temel sınıf.
    - cfg saklama
    - hız ayarı için no-op set_rate_hz()
    - seri port autodetect yardımcısı
    Subclass’lar open/read_imu/close metodlarını override etmelidir.
    """
    def __init__(self, cfg: Optional[ImuConfig] = None) -> None:
        self.cfg: Optional[ImuConfig] = cfg

    # Subclass genelde override eder; burada cfg’yi saklarız
    def open(self, cfg: ImuConfig) -> None:
        self.cfg = cfg

    # Backend desteklemiyorsa sessizce False döner
    def set_rate_hz(self, hz: float) -> bool:
        try:
            if self.cfg is not None:
                self.cfg.rate_hz = float(hz)
            return False
        except Exception:
            return False

    # Non-blocking son ölçüm (subclass zorunlu override)
    def read_imu(self) -> Optional[Dict[str, Any]]:
        raise NotImplementedError

    def close(self) -> None:
        pass

    # ---- Yardımcılar -------------------------------------------------

    def _resolve_port(self, hints: Optional[list[str]] = None) -> Optional[str]:
        """
        cfg.port 'auto'/None ise, hints kullanarak otomatik port bulmayı dener.
        Hints örnekleri: ["imu", "icm", "mpu", "bno", "lsm", "usb", "uart"]
        """
        port = getattr(self.cfg, "port", None) if self.cfg is not None else None
        if isinstance(port, str) and port and port.lower() not in ("auto", "none", "off"):
            return port
        # Ortak ipuçları birleşimi
        default_hints = ["imu", "icm", "mpu", "bno", "lsm", "usb", "uart", "ttyusb", "ttyacm"]
        probe_hints = (hints or []) + default_hints
        return find_serial_port(probe_hints)  # ports modülü yoksa None döner

    def _list_ports(self) -> list[str]:
        return list_serial_ports()
