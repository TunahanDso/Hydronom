# sensors/camera/config.py
from dataclasses import dataclass
from typing import Optional, Any


@dataclass
class CameraConfig:
    """
    Kamera sensörü yapılandırması.

    Amaç:
    - Farklı backend'ler için ortak ve tutarlı bir config modeli sağlamak
    - Varsayılanları güvenli ve makul tutmak
    - Kamera katmanında normalize edilmiş erişim sunmak

    Desteklenen backend'ler:
    - sim
    - opencv
    - rtsp
    """

    # ---------------------------------------------------------------------
    # Genel
    # ---------------------------------------------------------------------

    backend: str = "sim"            # "sim" | "opencv" | "rtsp"
    source: str = "cam0"
    frame_id: str = "camera_link"

    width: int = 640
    height: int = 480
    fps: float = 15.0

    # Görüntü formatı
    # Not:
    # - OpenCV backend doğal olarak çoğu zaman BGR üretir.
    # - rgb8 gerekiyorsa üst katmanda dönüştürülür.
    color: str = "bgr8"             # "bgr8" | "rgb8"

    # Frame taşıma davranışı
    # Not:
    # - inline_jpeg=True ise payload şişer.
    # - Mümkünse production kullanımında ayrı frame transport tercih edilmelidir.
    inline_jpeg: bool = False
    jpeg_quality: int = 85

    # Frame tazelik eşiği
    # Üst katmanda "stale frame" kararları için kullanılabilir.
    stale_timeout_ms: int = 1500

    # ---------------------------------------------------------------------
    # OpenCV
    # ---------------------------------------------------------------------

    # device önceliklidir:
    # - Linux: "/dev/video0"
    # - Windows: bazen "0", bazen pipeline string
    # - GStreamer: tam pipeline string
    device: Optional[str] = None

    # device verilmezse kullanılacak kamera indeksi
    index: int = 0

    # OpenCV capture API seçimi:
    # "auto" | "any" | "dshow" | "msmf" | "v4l2" | "gstreamer" | "ffmpeg"
    capture_api: str = "auto"

    # OpenCV buffer isteği (her backend garanti etmez)
    buffer_size: Optional[int] = 1

    # Okuma hatalarında üst katmanın yeniden açma denemesi yapmasına izin verir
    reopen_on_fail: bool = True

    # Art arda hata durumunda backend health alarmı için eşik
    max_consecutive_failures: int = 30

    # ---------------------------------------------------------------------
    # RTSP
    # ---------------------------------------------------------------------

    rtsp_url: Optional[str] = None

    # RTSP / ağ akışlarında read timeout mantığı için kullanılabilir
    read_timeout_ms: int = 3000

    # Ağ akışı koparsa tekrar bağlanma denemesi
    auto_reconnect: bool = True

    # ---------------------------------------------------------------------
    # Yardımcılar
    # ---------------------------------------------------------------------

    def normalize(self) -> "CameraConfig":
        """
        Alanları güvenli ve tutarlı hale getirir.
        """
        self.backend = str(self.backend or "sim").strip().lower()
        self.source = str(self.source or "cam0").strip()
        self.frame_id = str(self.frame_id or "camera_link").strip()

        self.width = max(1, int(self.width))
        self.height = max(1, int(self.height))
        self.fps = max(0.1, float(self.fps))

        self.color = str(self.color or "bgr8").strip().lower()
        if self.color not in ("bgr8", "rgb8"):
            raise ValueError(f"Unsupported camera color format: {self.color}")

        self.inline_jpeg = bool(self.inline_jpeg)
        self.jpeg_quality = max(1, min(100, int(self.jpeg_quality)))
        self.stale_timeout_ms = max(0, int(self.stale_timeout_ms))

        self.index = int(self.index)

        self.capture_api = str(self.capture_api or "auto").strip().lower()
        if self.capture_api not in ("auto", "any", "dshow", "msmf", "v4l2", "gstreamer", "ffmpeg"):
            raise ValueError(f"Unsupported capture_api: {self.capture_api}")

        if self.device is not None:
            self.device = str(self.device).strip()
            if self.device == "":
                self.device = None

        if self.buffer_size is not None:
            self.buffer_size = max(1, int(self.buffer_size))

        self.reopen_on_fail = bool(self.reopen_on_fail)
        self.max_consecutive_failures = max(1, int(self.max_consecutive_failures))

        if self.rtsp_url is not None:
            self.rtsp_url = str(self.rtsp_url).strip()
            if self.rtsp_url == "":
                self.rtsp_url = None

        self.read_timeout_ms = max(0, int(self.read_timeout_ms))
        self.auto_reconnect = bool(self.auto_reconnect)

        self.validate()
        return self

    def validate(self) -> None:
        """
        Yapılandırma tutarlılığını kontrol eder.
        """
        if self.backend not in ("sim", "opencv", "rtsp"):
            raise ValueError(f"Unsupported camera backend: {self.backend}")

        if self.backend == "rtsp" and not self.rtsp_url:
            raise ValueError("RTSP backend için rtsp_url zorunludur.")

        if self.backend == "opencv":
            # device veya index kullanılabilir; ikisi de yok gibi bir durum olmasın.
            if self.device is None and self.index is None:
                raise ValueError("OpenCV backend için device veya index tanımlı olmalıdır.")

        if self.inline_jpeg and self.jpeg_quality < 1:
            raise ValueError("jpeg_quality 1..100 aralığında olmalıdır.")

    @property
    def is_sim(self) -> bool:
        return self.backend == "sim"

    @property
    def is_opencv(self) -> bool:
        return self.backend == "opencv"

    @property
    def is_rtsp(self) -> bool:
        return self.backend == "rtsp"

    @property
    def preferred_device(self) -> Any:
        """
        Backend açarken kullanılacak tercih edilen cihaz girdisi.

        Öncelik:
        - device
        - index
        """
        return self.device if self.device not in (None, "") else self.index

    def to_dict(self) -> dict:
        """
        Diagnostics / capability / log amaçlı sözlük görünümü.
        """
        return {
            "backend": self.backend,
            "source": self.source,
            "frame_id": self.frame_id,
            "width": self.width,
            "height": self.height,
            "fps": self.fps,
            "color": self.color,
            "inline_jpeg": self.inline_jpeg,
            "jpeg_quality": self.jpeg_quality,
            "stale_timeout_ms": self.stale_timeout_ms,
            "device": self.device,
            "index": self.index,
            "capture_api": self.capture_api,
            "buffer_size": self.buffer_size,
            "reopen_on_fail": self.reopen_on_fail,
            "max_consecutive_failures": self.max_consecutive_failures,
            "rtsp_url": self.rtsp_url,
            "read_timeout_ms": self.read_timeout_ms,
            "auto_reconnect": self.auto_reconnect,
        }

    @classmethod
    def from_dict(cls, data: Optional[dict]) -> "CameraConfig":
        """
        Sözlükten config üretir ve normalize eder.
        """
        cfg = cls(**(data or {}))
        return cfg.normalize()