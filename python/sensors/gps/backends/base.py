# sensors/gps/backends/base.py

from __future__ import annotations

import threading
import time
from typing import Optional, Dict, Any, Protocol, runtime_checkable

from sensors.gps.config import GpsConfig


@runtime_checkable
class IGpsBackend(Protocol):
    """
    Tüm GPS backend'leri için arayüz (non-blocking read_fix).

    read_fix() mümkünse *non-blocking* olmalı ve şu alanları (opsiyonel) döndürmeli:
      {
        "lat": float|None,
        "lon": float|None,
        "alt": float|None,
        "fix": int|None,      # 0 yok, 1 GPS, 2 DGPS/RTK/...
        "hdop": float|None,
        "t_gps": float|None   # epoch(UTC, s) — RMC/GGA'dan türetilebilirse; aksi None
      }
    """
    def open(self, cfg: GpsConfig) -> None: ...
    def read_fix(self) -> Optional[Dict[str, Any]]: ...
    def close(self) -> None: ...


class ThreadedGpsBackend(IGpsBackend):
    """
    İsteğe bağlı kolaylık sınıfı:
    - Alt sınıf sadece _open/_read_blocking/_close kancalarını uygular.
    - open() bir okuma thread'i başlatır.
    - read_fix() en son veriyi *non-blocking* döndürür.
    """
    def __init__(self) -> None:
        self._cfg: Optional[GpsConfig] = None
        self._thread: Optional[threading.Thread] = None
        self._stop_evt = threading.Event()
        self._latest: Optional[Dict[str, Any]] = None
        self._lock = threading.Lock()

    # --- IGpsBackend ---

    def open(self, cfg: GpsConfig) -> None:
        self._cfg = cfg
        self._open(cfg)
        self._stop_evt.clear()
        self._thread = threading.Thread(target=self._loop, name="GpsReader", daemon=True)
        self._thread.start()

    def read_fix(self) -> Optional[Dict[str, Any]]:
        with self._lock:
            return dict(self._latest) if self._latest is not None else None

    def close(self) -> None:
        self._stop_evt.set()
        if self._thread is not None and self._thread.is_alive():
            self._thread.join(timeout=1.5)
        self._close()

    # --- Alt sınıfların ezmesi gereken kancalar ---

    def _open(self, cfg: GpsConfig) -> None:
        """Port/socket açma vb. (blocking olmayabilir)."""
        pass

    def _read_blocking(self) -> Optional[Dict[str, Any]]:
        """
        Donanımdan *blocking* tek okuma yap ve fix dict döndür.
        Yoksa None döndür. Hata durumunda istisna fırlatılabilir.
        """
        raise NotImplementedError

    def _close(self) -> None:
        """Kaynakları serbest bırak."""
        pass

    # --- İç döngü ---

    def _loop(self) -> None:
        backoff = 0.01  # hızlı başla; hata olursa kademeli artar
        while not self._stop_evt.is_set():
            try:
                fix = self._read_blocking()
                if fix is not None:
                    # Basit normalize: anahtarlar eksikse dokunma; üst katman korunmacı.
                    with self._lock:
                        self._latest = fix
                backoff = 0.0  # başarılı okumada bekleme yapma
            except Exception:
                # Aşırı log gürültüsünü önlemek için burada loglamıyoruz; alt sınıf loglayabilir.
                backoff = min(0.2, (backoff + 0.01))
            finally:
                # CPU'yu yakmamak için küçük bir uyku
                time.sleep(backoff)
