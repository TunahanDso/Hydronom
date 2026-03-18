# sensors/camera/camera.py
import base64
import time
from typing import Optional, Tuple, Any, Dict

from sensors.base_sensor import BaseSensor
from .config import CameraConfig

# OpenCV opsiyoneldir. Modül seviyesinde bir kez yüklemeye çalışıyoruz.
try:
    import cv2  # type: ignore
except Exception:
    cv2 = None

# Sample üretimi için doğrudan sınıfı kullanıyoruz (yerel fallback için)
try:
    from core.sample import Sample
except Exception:
    Sample = None  # Çok nadir durumda import sorunu olursa fallback path devreye girecek.


def _make_backend(name: str):
    """
    Kamera backend üreticisi.

    Bilinçli olarak basit tutuldu.
    İleride registry tabanlı genişletilebilir.
    """
    if name == "sim":
        from .backends.sim import SimBackend
        return SimBackend()

    if name == "opencv":
        from .backends.opencv import OpenCvBackend
        return OpenCvBackend()

    if name == "rtsp":
        from .backends.rtsp import RtspBackend
        return RtspBackend()

    raise ValueError(f"Unknown camera backend: {name}")


class CameraSensor(BaseSensor):
    """
    Backend’ten gelen frame’i standart Sample’a dönüştüren kamera taşıyıcısı.

    Tasarım hedefleri:
    - Backend farklarını tek yerde soyutlamak
    - Kamera verisini görev/telemetri hattına tutarlı biçimde vermek
    - Hata anlarında çökmeden invalid sample üretmek
    - Zaman damgası semantiğini açık tutmak
    - Büyük görüntü verisini mümkün olduğunca kontrollü taşımak

    Not:
    - Bu sınıf bir "vision pipeline" değildir.
    - Bu sınıf kamera erişim ve frame meta üretim katmanıdır.
    """

    kind = "camera"

    def __init__(self, cfg: Optional[CameraConfig] = None):
        self.cfg = cfg or CameraConfig()

        super().__init__(
            source=self.cfg.source,
            frame_id=self.cfg.frame_id,
            simulate=(self.cfg.backend == "sim")
        )

        self._backend = _make_backend(self.cfg.backend)

        # Yerel fallback sayaç
        self._seq = int(time.time() * 1000)

        # Basit sağlık/istatistik alanları
        self._open_monotonic: Optional[float] = None
        self._last_good_frame_ts: Optional[float] = None
        self._last_read_monotonic: Optional[float] = None
        self._last_error: Optional[str] = None

        self._frame_counter: int = 0
        self._invalid_counter: int = 0
        self._error_counter: int = 0
        self._consecutive_failures: int = 0
        self._reconnect_count: int = 0

        # Basit FPS ölçümü için
        self._fps_window_start: float = time.monotonic()
        self._fps_window_frames: int = 0
        self._effective_fps: float = 0.0

    # -------------------------------------------------------------------------
    # Fallback yardımcılar
    # -------------------------------------------------------------------------

    def _new_seq(self) -> int:
        if hasattr(super(), "_new_seq"):
            try:
                return super()._new_seq()  # type: ignore[attr-defined]
            except Exception:
                pass

        self._seq += 1
        return self._seq

    def _stamp(self) -> float:
        """
        Yayın/oluşturma zamanı.
        """
        if hasattr(super(), "_stamp"):
            try:
                return super()._stamp()  # type: ignore[attr-defined]
            except Exception:
                pass

        return time.time()

    def _make_sample(
        self,
        *,
        sensor: str,
        data: Dict[str, Any],
        quality: Dict[str, Any],
        seq: Optional[int] = None,
        t: Optional[float] = None,
        calib_id: Optional[str] = None
    ):
        """
        BaseSensor._make_sample yoksa, doğrudan core.sample.Sample ile üret.
        """
        if hasattr(super(), "_make_sample"):
            try:
                return super()._make_sample(  # type: ignore[attr-defined]
                    sensor=sensor,
                    data=data,
                    quality=quality,
                    seq=seq,
                    t=t,
                    calib_id=calib_id
                )
            except Exception:
                pass

        if Sample is None:
            raise RuntimeError(
                "core.sample.Sample import edilemedi ve BaseSensor._make_sample da yok."
            )

        return Sample(
            sensor=sensor,
            source=self.cfg.source,
            data=data,
            frame_id=self.cfg.frame_id,
            quality=quality,
            seq=seq or self._new_seq(),
            t=t or self._stamp(),
            calib_id=calib_id
        )

    # -------------------------------------------------------------------------
    # Dahili yardımcılar
    # -------------------------------------------------------------------------

    def _now_monotonic(self) -> float:
        return time.monotonic()

    def _safe_float(self, value: Any, default: float) -> float:
        try:
            return float(value)
        except Exception:
            return default

    def _cleanup_none(self, obj: Dict[str, Any]) -> Dict[str, Any]:
        return {k: v for k, v in obj.items() if v is not None}

    def _update_effective_fps(self) -> float:
        """
        Yaklaşık gerçek FPS hesabı.
        Kayan pencere yerine sade bir 1 saniyelik pencere kullanıyoruz.
        """
        self._fps_window_frames += 1
        now_mono = self._now_monotonic()
        dt = now_mono - self._fps_window_start

        if dt >= 1.0:
            self._effective_fps = self._fps_window_frames / max(dt, 1e-6)
            self._fps_window_start = now_mono
            self._fps_window_frames = 0

        return self._effective_fps

    def _build_error_sample(self, message: str, *, capture_ts: Optional[float] = None):
        """
        Profesyonel hata/invalid sample üretimi.
        """
        self._error_counter += 1
        self._invalid_counter += 1
        self._consecutive_failures += 1
        self._last_error = message

        publish_ts = self._stamp()
        recv_ts = time.time()
        capture_ts = capture_ts if capture_ts is not None else None

        data = self._cleanup_none({
            "w": None,
            "h": None,
            "format": None,
            "t_capture": capture_ts,
            "t_receive": recv_ts,
            "t_publish": publish_ts,
            "error": message,
        })

        quality = self._cleanup_none({
            "valid": False,
            "backend": self.cfg.backend,
            "fps_target": self.cfg.fps,
            "fps_effective": round(self._effective_fps, 3),
            "consecutive_failures": self._consecutive_failures,
            "error_count": self._error_counter,
            "frame_count": self._frame_counter,
            "invalid_count": self._invalid_counter,
            "last_good_frame_age_ms": (
                int(max(0.0, (time.time() - self._last_good_frame_ts) * 1000.0))
                if self._last_good_frame_ts is not None
                else None
            ),
        })

        seq = self._new_seq()
        return self._make_sample(
            sensor="camera",
            data=data,
            quality=quality,
            seq=seq,
            t=publish_ts
        )

    def _frame_to_payload(
        self,
        frame
    ) -> Tuple[Dict[str, Any], Optional[str], Optional[str]]:
        """
        Frame’den metadata ve opsiyonel payload üretir.

        Dönüş:
        - data_meta
        - jpeg_b64
        - frame_uri

        Not:
        - Ham bytes döndürmüyoruz. JSON/NDJSON için güvenli değil.
        - Inline payload gerekiyorsa base64 string üretiyoruz.
        - Bu yine pahalıdır; mümkünse üst katmanda ayrı frame transport tercih edilmelidir.
        """
        if frame is None:
            raise ValueError("Frame boş geldi.")

        if not hasattr(frame, "shape"):
            raise ValueError("Frame nesnesinin shape alanı yok.")

        if len(frame.shape) < 2:
            raise ValueError("Frame shape beklenen biçimde değil.")

        h, w = frame.shape[:2]
        fmt = str(self.cfg.color).lower()

        working_frame = frame
        jpeg_b64: Optional[str] = None
        frame_uri: Optional[str] = None

        # BGR -> RGB dönüşümü yalnız gerekiyorsa ve OpenCV varsa uygulanır.
        if fmt == "rgb8":
            if cv2 is None:
                raise RuntimeError("rgb8 istenmiş ancak OpenCV yüklenemedi.")
            working_frame = cv2.cvtColor(working_frame, cv2.COLOR_BGR2RGB)

        # Backend frame export desteği veriyorsa ve inline_jpeg kapalıysa
        # gelecekte ayrı frame transport için bu referansı kullanabiliriz.
        if not self.cfg.inline_jpeg and hasattr(self._backend, "export_frame_ref"):
            try:
                ref = self._backend.export_frame_ref(working_frame, self.cfg)  # type: ignore[attr-defined]
                if ref:
                    frame_uri = str(ref)
            except Exception:
                # Export başarısız olduysa sessizce devam ediyoruz.
                frame_uri = None

        # Inline JPEG gerekiyorsa JSON dostu base64 string üretiyoruz.
        if self.cfg.inline_jpeg:
            if cv2 is None:
                raise RuntimeError("inline_jpeg=True ancak OpenCV yüklenemedi.")

            ok, buf = cv2.imencode(
                ".jpg",
                working_frame,
                [
                    int(getattr(cv2, "IMWRITE_JPEG_QUALITY", 1)),
                    int(self.cfg.jpeg_quality)
                ]
            )

            if not ok:
                raise RuntimeError("JPEG encode başarısız oldu.")

            jpeg_b64 = base64.b64encode(buf.tobytes()).decode("ascii")
            fmt = "jpeg"

        data = self._cleanup_none({
            "w": int(w),
            "h": int(h),
            "format": fmt,   # "bgr8" | "rgb8" | "jpeg"
            "jpeg_q": int(self.cfg.jpeg_quality) if fmt == "jpeg" else None
        })

        return data, jpeg_b64, frame_uri

    # -------------------------------------------------------------------------
    # Genel sensör API
    # -------------------------------------------------------------------------

    def set_rate_hz(self, hz: float):
        """
        Kamera hedef FPS değerini günceller.
        Backend tarafı bu setter'ı destekliyorsa iletilir.
        """
        try:
            self.cfg.fps = float(hz)
        except Exception:
            pass

        if hasattr(self._backend, "set_rate_hz"):
            try:
                self._backend.set_rate_hz(float(self.cfg.fps))
            except Exception:
                pass

    def open(self):
        """
        Kamera backend’ini açar.
        """
        self._backend.open(self.cfg)
        self.is_open = True
        self._open_monotonic = self._now_monotonic()
        self._last_error = None

    def read(self):
        """
        Kameradan bir örnek okur ve standart Sample üretir.

        Zaman semantiği:
        - t_capture: frame'in backend/kamera tarafından üretildiği an
        - t_receive: bu katmanda frame'in teslim alındığı an
        - t_publish: Sample nesnesinin üretildiği an

        Sample.t alanında publish zamanı kullanıyoruz.
        Capture zamanı ayrıca data içinde açıkça taşınıyor.
        """
        if not self.is_open:
            raise RuntimeError("Kamera açılmadı.")

        read_started_mono = self._now_monotonic()
        recv_ts = time.time()

        try:
            snap = self._backend.read_frame()
        except Exception as e:
            return self._build_error_sample(f"read_frame hatası: {e}")

        if snap is None:
            return self._build_error_sample("Backend henüz frame döndürmedi.")

        # Beklenen imza: (frame, t_cam)
        if not isinstance(snap, tuple) or len(snap) != 2:
            return self._build_error_sample(
                "Backend read_frame çıktısı beklenen (frame, t_cam) biçiminde değil."
            )

        frame, t_cam = snap

        if frame is None:
            return self._build_error_sample("Frame boş geldi.")

        capture_ts = self._safe_float(t_cam, recv_ts)
        age_ms = int(max(0.0, (recv_ts - capture_ts) * 1000.0))
        read_latency_ms = int(max(0.0, (self._now_monotonic() - read_started_mono) * 1000.0))

        try:
            data, jpeg_b64, frame_uri = self._frame_to_payload(frame)
        except Exception as e:
            return self._build_error_sample(
                f"Frame işleme hatası: {e}",
                capture_ts=capture_ts
            )

        publish_ts = self._stamp()

        # Başarılı okuma istatistikleri
        self._frame_counter += 1
        self._consecutive_failures = 0
        self._last_error = None
        self._last_good_frame_ts = capture_ts
        self._last_read_monotonic = self._now_monotonic()

        fps_effective = self._update_effective_fps()

        # Büyük veriyi yalnız istenirse taşıyoruz.
        if jpeg_b64 is not None:
            data["jpeg_b64"] = jpeg_b64

        if frame_uri is not None:
            data["frame_uri"] = frame_uri

        # Zaman alanlarını açık taşıyoruz.
        data["t_capture"] = capture_ts
        data["t_receive"] = recv_ts
        data["t_publish"] = publish_ts

        uptime_s = None
        if self._open_monotonic is not None:
            uptime_s = max(0.0, self._now_monotonic() - self._open_monotonic)

        quality = self._cleanup_none({
            "valid": True,
            "age_ms": age_ms,
            "read_latency_ms": read_latency_ms,
            "backend": self.cfg.backend,
            "fps_target": self.cfg.fps,
            "fps_effective": round(fps_effective, 3),
            "simulate": bool(self.cfg.backend == "sim"),
            "frame_count": self._frame_counter,
            "invalid_count": self._invalid_counter,
            "error_count": self._error_counter,
            "consecutive_failures": self._consecutive_failures,
            "last_error": self._last_error,
            "uptime_s": round(uptime_s, 3) if uptime_s is not None else None,
        })

        seq = self._new_seq()

        return self._make_sample(
            sensor="camera",
            data=data,
            quality=quality,
            seq=seq,
            t=publish_ts
        )

    def close(self):
        """
        Kamera backend’ini kapatır.
        """
        try:
            self._backend.close()
        finally:
            self.is_open = False

    # -------------------------------------------------------------------------
    # Opsiyonel yardımcılar
    # -------------------------------------------------------------------------

    def get_health(self) -> Dict[str, Any]:
        """
        Kameranın son bilinen sağlık/istatistik özetini verir.
        Üst katman diagnostics için kullanabilir.
        """
        now = time.time()

        return self._cleanup_none({
            "sensor": "camera",
            "source": self.cfg.source,
            "backend": self.cfg.backend,
            "is_open": self.is_open,
            "simulate": bool(self.cfg.backend == "sim"),
            "fps_target": self.cfg.fps,
            "fps_effective": round(self._effective_fps, 3),
            "frame_count": self._frame_counter,
            "invalid_count": self._invalid_counter,
            "error_count": self._error_counter,
            "consecutive_failures": self._consecutive_failures,
            "reconnect_count": self._reconnect_count,
            "last_error": self._last_error,
            "last_good_frame_age_ms": (
                int(max(0.0, (now - self._last_good_frame_ts) * 1000.0))
                if self._last_good_frame_ts is not None
                else None
            ),
        })

    def try_reopen(self) -> bool:
        """
        Basit yeniden açma denemesi.
        Gerçek supervisor davranışı üst katmanda olmalıdır.
        """
        try:
            self.close()
        except Exception:
            pass

        try:
            self.open()
            self._reconnect_count += 1
            return True
        except Exception as e:
            self._last_error = f"reopen hatası: {e}"
            self._error_counter += 1
            return False