# sensors/camera/backends/rtsp.py
import time
import threading
from typing import Optional, Tuple, Any, Dict

from .base import ICameraBackend

try:
    import cv2  # type: ignore
except Exception:
    cv2 = None


class RtspBackend(ICameraBackend):
    """
    RTSP kamera backend'i.

    Tasarım hedefleri:
    - RTSP akışını arka planda sürekli okuyup en son frame'i saklamak
    - read_frame() çağrısını non-blocking tutmak
    - Thread-safe veri erişimi sağlamak
    - Ağ kaynaklı geçici bozulmalarda health bilgisi üretmek
    - İstenirse otomatik reconnect için zemin hazırlamak
    """

    def __init__(self):
        self._cfg = None
        self._cap = None

        self._thr: Optional[threading.Thread] = None
        self._stop = threading.Event()
        self._lock = threading.Lock()

        # Son başarılı frame: (frame, backend_receive_ts)
        self._last: Optional[Tuple[Any, float]] = None

        # Sağlık / istatistik
        self._opened_at: Optional[float] = None
        self._last_frame_ts: Optional[float] = None
        self._last_read_attempt_ts: Optional[float] = None
        self._last_error: Optional[str] = None

        self._frames_ok: int = 0
        self._read_fail_count: int = 0
        self._open_fail_count: int = 0
        self._consecutive_failures: int = 0
        self._reconnect_count: int = 0

        self._actual_width: Optional[int] = None
        self._actual_height: Optional[int] = None
        self._actual_fps: Optional[float] = None
        self._backend_name: Optional[str] = None

    # -------------------------------------------------------------------------
    # İç yardımcılar
    # -------------------------------------------------------------------------

    def _require_cv2(self) -> None:
        if cv2 is None:
            raise ImportError("OpenCV (cv2) gerekli: pip install opencv-python")

    def _resolve_capture_api(self, cfg: Any) -> int:
        """
        Config'ten OpenCV capture API seçer.

        RTSP için pratikte çoğu zaman:
        - ffmpeg
        - gstreamer
        - auto
        tercih edilir.
        """
        api_name = str(getattr(cfg, "capture_api", "auto") or "auto").strip().lower()

        if cv2 is None:
            return 0

        if api_name in ("auto", "any"):
            return cv2.CAP_ANY

        mapping = {
            "ffmpeg": getattr(cv2, "CAP_FFMPEG", cv2.CAP_ANY),
            "gstreamer": getattr(cv2, "CAP_GSTREAMER", cv2.CAP_ANY),
            "msmf": getattr(cv2, "CAP_MSMF", cv2.CAP_ANY),
            "dshow": getattr(cv2, "CAP_DSHOW", cv2.CAP_ANY),
        }

        return mapping.get(api_name, cv2.CAP_ANY)

    def _create_capture(self, url: str, api_pref: int):
        if api_pref:
            return cv2.VideoCapture(url, api_pref)
        return cv2.VideoCapture(url)

    def _configure_capture(self) -> None:
        """
        RTSP akışında bazı ayarların etkisi backend'e bağlıdır.
        Uygulanabilirse uygula, yoksa sessizce geç.
        """
        assert self._cap is not None
        cfg = self._cfg

        try:
            if hasattr(cfg, "buffer_size") and getattr(cfg, "buffer_size") is not None:
                self._cap.set(cv2.CAP_PROP_BUFFERSIZE, float(cfg.buffer_size))
        except Exception:
            pass

        # İstenen çözünürlük/fps her RTSP kaynağında geçerli olmayabilir.
        # Genelde stream ne veriyorsa onu alırız; ama yine de denemek zararsızdır.
        try:
            self._cap.set(cv2.CAP_PROP_FRAME_WIDTH, float(cfg.width))
        except Exception:
            pass

        try:
            self._cap.set(cv2.CAP_PROP_FRAME_HEIGHT, float(cfg.height))
        except Exception:
            pass

        try:
            self._cap.set(cv2.CAP_PROP_FPS, float(cfg.fps))
        except Exception:
            pass

    def _readback_capture_properties(self) -> None:
        assert self._cap is not None

        try:
            w = self._cap.get(cv2.CAP_PROP_FRAME_WIDTH)
            self._actual_width = int(w) if w and w > 0 else None
        except Exception:
            self._actual_width = None

        try:
            h = self._cap.get(cv2.CAP_PROP_FRAME_HEIGHT)
            self._actual_height = int(h) if h and h > 0 else None
        except Exception:
            self._actual_height = None

        try:
            fps = self._cap.get(cv2.CAP_PROP_FPS)
            self._actual_fps = float(fps) if fps and fps > 0 else None
        except Exception:
            self._actual_fps = None

        try:
            backend_id = self._cap.get(cv2.CAP_PROP_BACKEND)
            self._backend_name = str(int(backend_id)) if backend_id else None
        except Exception:
            self._backend_name = None

    def _reset_runtime_state(self) -> None:
        with self._lock:
            self._last = None
            self._last_frame_ts = None
            self._last_read_attempt_ts = None
            self._last_error = None
            self._consecutive_failures = 0

    def _stale_timeout_s(self) -> float:
        cfg = self._cfg
        timeout_ms = int(getattr(cfg, "read_timeout_ms", 3000) or 3000)
        return max(0.1, timeout_ms / 1000.0)

    def _should_reconnect(self) -> bool:
        cfg = self._cfg
        if cfg is None:
            return False

        if not bool(getattr(cfg, "auto_reconnect", True)):
            return False

        if self._cap is None:
            return True

        now = time.time()

        with self._lock:
            if self._last_frame_ts is None:
                # Henüz hiç frame gelmediyse ve yeterince zaman geçtiyse reconnect denenebilir
                if self._opened_at is None:
                    return False
                return (now - self._opened_at) > self._stale_timeout_s()

            # Son frame çok eskiyse reconnect gerekebilir
            return (now - self._last_frame_ts) > self._stale_timeout_s()

    def _reopen_internal(self) -> bool:
        """
        Loop içinden çağrılabilecek hafif reconnect mantığı.
        """
        cfg = self._cfg
        if cfg is None:
            return False

        rtsp_url = getattr(cfg, "rtsp_url", None)
        if not rtsp_url:
            return False

        try:
            if self._cap is not None:
                self._cap.release()
        except Exception:
            pass

        self._cap = None

        try:
            api_pref = self._resolve_capture_api(cfg)
            cap = self._create_capture(rtsp_url, api_pref)

            if cap is None or not cap.isOpened():
                self._open_fail_count += 1
                with self._lock:
                    self._last_error = "RTSP reconnect başarısız: VideoCapture açılamadı."
                return False

            self._cap = cap
            self._configure_capture()
            self._readback_capture_properties()
            self._reconnect_count += 1

            with self._lock:
                self._last_error = None
                self._consecutive_failures = 0

            return True

        except Exception as e:
            self._open_fail_count += 1
            with self._lock:
                self._last_error = f"RTSP reconnect hatası: {e}"
            return False

    # -------------------------------------------------------------------------
    # Genel backend API
    # -------------------------------------------------------------------------

    def open(self, cfg: Any) -> None:
        """
        RTSP akışını açar ve arka plan okuma thread'ini başlatır.
        """
        self._require_cv2()

        rtsp_url = getattr(cfg, "rtsp_url", None)
        if not rtsp_url:
            raise ValueError("rtsp_url gerekli")

        # Önce eski instance'ı kapat
        self.close()

        self._cfg = cfg
        api_pref = self._resolve_capture_api(cfg)
        cap = self._create_capture(rtsp_url, api_pref)

        if cap is None or not cap.isOpened():
            self._open_fail_count += 1
            raise RuntimeError(f"RTSP stream açılamadı: {rtsp_url!r}, api={api_pref}")

        self._cap = cap
        self._configure_capture()
        self._readback_capture_properties()

        self._stop.clear()
        self._reset_runtime_state()
        self._opened_at = time.time()

        self._thr = threading.Thread(
            target=self._loop,
            name="Camera-RTSP",
            daemon=True
        )
        self._thr.start()

    def _loop(self) -> None:
        """
        Arka planda RTSP akışını sürekli okur.
        Ağ kopmaları veya uzun stale durumlarında reconnect deneyebilir.
        """
        while not self._stop.is_set():
            if self._cap is None:
                if not self._reopen_internal():
                    time.sleep(0.2)
                    continue

            try:
                assert self._cap is not None

                self._last_read_attempt_ts = time.time()
                ok, frame = self._cap.read()

                if not ok or frame is None:
                    with self._lock:
                        self._read_fail_count += 1
                        self._consecutive_failures += 1
                        self._last_error = "RTSP read() başarısız veya frame boş geldi."

                    if self._should_reconnect():
                        self._reopen_internal()

                    time.sleep(0.02)
                    continue

                t_backend = time.time()

                # Güvenli tarafta olmak için kopya alıyoruz
                safe_frame = frame.copy()

                with self._lock:
                    self._last = (safe_frame, t_backend)
                    self._last_frame_ts = t_backend
                    self._frames_ok += 1
                    self._consecutive_failures = 0
                    self._last_error = None

            except Exception as e:
                with self._lock:
                    self._read_fail_count += 1
                    self._consecutive_failures += 1
                    self._last_error = f"RTSP loop hatası: {e}"

                if self._should_reconnect():
                    self._reopen_internal()

                time.sleep(0.05)

    def read_frame(self) -> Optional[Tuple[Any, float]]:
        """
        Son başarılı frame'i non-blocking döndürür.

        Dönüş:
        - (frame, t_backend_receive)
        - veya None

        Not:
        - Buradaki timestamp gerçek kamera hardware capture zamanı değildir.
        - Backend'in frame'i bu katmanda teslim aldığı zamandır.
        """
        with self._lock:
            if self._last is None:
                return None

            frame, ts = self._last
            return frame, ts

    def close(self) -> None:
        """
        Thread'i ve RTSP kaynağını temiz şekilde kapatır.
        """
        self._stop.set()

        try:
            if self._thr and self._thr.is_alive():
                self._thr.join(timeout=1.5)
        except Exception:
            pass

        try:
            if self._cap is not None:
                self._cap.release()
        except Exception:
            pass

        with self._lock:
            self._last = None
            self._last_frame_ts = None

        self._thr = None
        self._cap = None
        self._opened_at = None

    # -------------------------------------------------------------------------
    # Opsiyonel yardımcılar
    # -------------------------------------------------------------------------

    def get_health(self) -> Dict[str, Any]:
        """
        Backend sağlık/istatistik özeti döndürür.
        """
        now = time.time()

        with self._lock:
            last_frame_age_ms = None
            if self._last_frame_ts is not None:
                last_frame_age_ms = int(max(0.0, (now - self._last_frame_ts) * 1000.0))

            uptime_s = None
            if self._opened_at is not None:
                uptime_s = max(0.0, now - self._opened_at)

            return {
                "backend": "rtsp",
                "is_open": bool(self._cap is not None),
                "thread_alive": bool(self._thr is not None and self._thr.is_alive()),
                "frames_ok": self._frames_ok,
                "read_fail_count": self._read_fail_count,
                "open_fail_count": self._open_fail_count,
                "consecutive_failures": self._consecutive_failures,
                "reconnect_count": self._reconnect_count,
                "last_error": self._last_error,
                "last_frame_age_ms": last_frame_age_ms,
                "uptime_s": round(uptime_s, 3) if uptime_s is not None else None,
                "actual_width": self._actual_width,
                "actual_height": self._actual_height,
                "actual_fps": round(self._actual_fps, 3) if self._actual_fps is not None else None,
                "backend_name": self._backend_name,
                "read_timeout_ms": int(getattr(self._cfg, "read_timeout_ms", 3000) or 3000) if self._cfg is not None else None,
                "auto_reconnect": bool(getattr(self._cfg, "auto_reconnect", True)) if self._cfg is not None else None,
            }

    def try_reopen(self) -> bool:
        """
        Üst katmandan elle reconnect denemesi yapılabilir.
        """
        if self._cfg is None:
            return False
        return self._reopen_internal()

    def export_frame_ref(self, frame: Any, cfg: Any) -> Optional[str]:
        """
        Gelecekte frame URI / shared memory referansı için placeholder.
        """
        return None