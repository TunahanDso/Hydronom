# hydroscan/live.py
# ---------------------------------------------------------------------------
# Hydroscan "canlı mod" modülü
#
# Amaç:
#  - Hydronom runtime'ın log dosyasını arka planda "tail" edip
#    satır satır parse etmek.
#  - Parse edilen STATE frame'lerini halka buffer'da (deque) saklamak.
#  - Flask app'i de /live/snapshot ile bu buffer'dan son N frame'i okuyup
#    frontend'e gönderecek.
#
# Notlar:
#  - Şimdilik sadece DOSYA tabanlı kaynak (file tail).
#  - Gerçek sensör / TCP streaming gibi modlar ileride buraya eklenebilir.
# ---------------------------------------------------------------------------

import os
import threading
import time
from collections import deque
from typing import Any, Deque, Dict, List, Optional, Tuple

from .parsing import (
    init_stream_state,
    parse_stream_line,
    build_synthetic_thruster_layout,
    extract_thruster_info,  # Yeni: offline ile aynı thruster çıkarım mantığı
)

# ---------------------------------------------------------------------------
# SABİTLER
# ---------------------------------------------------------------------------

# Canlı buffer kapasitesi (maksimum kaç frame saklansın?)
LIVE_BUFFER_MAX: int = 5000

# Yeni satır gelmediğinde ne kadar uyuyalım? (saniye)
DEFAULT_POLL_INTERVAL: float = 0.10


# ---------------------------------------------------------------------------
# GLOBAL DURUM (BASİT STATE)
# ---------------------------------------------------------------------------

_live_thread: Optional[threading.Thread] = None          # Arka plan thread'i
_live_running: bool = False                              # Canlı mod açık mı?
_live_buffer: Deque[Dict[str, Any]] = deque(maxlen=LIVE_BUFFER_MAX)
_live_state: Optional[Dict[str, Any]] = None             # Akış içi parser state
_live_lock = threading.Lock()                            # Buffer için kilit

_live_source_path: Optional[str] = None                  # Okunacak log dosyası
_live_poll_interval: float = DEFAULT_POLL_INTERVAL       # Bekleme süresi


# ---------------------------------------------------------------------------
# İÇ FONKSİYON: DOSYADAN TAIL OKUYAN THREAD
# ---------------------------------------------------------------------------

def _file_tail_worker() -> None:
    """
    Arka planda çalışan thread.
    Verilen log dosyasının sonuna gider, yeni satır geldikçe okur ve parse eder.

    Akış:
      - Dosya henüz yoksa bekleyerek tekrar deniyor.
      - Dosya açıldıktan sonra satır satır okuyor.
      - parse_stream_line(...) ile satırı işleyip frame üretirse buffer'a ekliyor.
    """
    global _live_running, _live_state

    path = _live_source_path
    if not path:
        # Kaynak yoksa çalışmaya gerek yok
        _live_running = False
        return

    # Dosya yoksa bekleyip tekrar dene (runtime geç açılabilir)
    while _live_running and not os.path.exists(path):
        time.sleep(0.5)

    if not _live_running:
        return

    # Dosyayı read-only aç
    try:
        f = open(path, "r", encoding="utf-8", errors="ignore")
    except OSError:
        # Dosya açılamadıysa canlı modu kapat
        _live_running = False
        return

    # Stream parser state'i başlat
    _live_state = init_stream_state()

    # Eğer sadece yeni gelen satırları görmek istiyorsan:
    # f.seek(0, os.SEEK_END)
    # Şimdilik: BAŞTAN itibaren okuyoruz, böylece runtime daha önce yazdıysa
    # o kayıtlar da ilk canlı testte görülebiliyor.

    try:
        while _live_running:
            line = f.readline()

            if not line:
                # Yeni satır yok, biraz uyuyup tekrar dene
                time.sleep(_live_poll_interval)
                continue

            # Her satırı stream parser ile işle
            frame = parse_stream_line(line, _live_state)

            if frame is not None:
                # Üretilen frame'i buffer'a ekle
                with _live_lock:
                    _live_buffer.append(frame)

    finally:
        try:
            f.close()
        except Exception:
            pass
        _live_running = False


# ---------------------------------------------------------------------------
# DIŞA AÇIK API: app.py BURADAN KULLANIYOR
# ---------------------------------------------------------------------------

def start_live_file(path: str, poll_interval: float = DEFAULT_POLL_INTERVAL) -> bool:
    """
    Dosya tabanlı canlı modu başlatır.

    path:
        Hydronom runtime'ın log dosyası
        Örnek:
            C:/Users/TunahanDELİSALİHOĞLU/Desktop/Hydronom/runtime.log

    poll_interval:
        Yeni satır gelmediğinde thread'in ne kadar uyuyacağı (saniye).
    """
    global _live_thread, _live_running, _live_source_path, _live_poll_interval
    global _live_buffer, _live_state

    # Dosya gerçekten var mı? (Runtime henüz açılmamış olabilir.)
    # Burada yoksa False döndürüp frontend'e düzgün hata göstermek daha net.
    if not os.path.exists(path):
        return False

    # Zaten bir canlı thread çalışıyorsa tekrar başlatma
    if _live_running:
        # Path farklı ise ileride "kaynağı değiştir" mantığı da eklenebilir.
        return True

    # Yeni oturum için buffer ve state'i sıfırla
    with _live_lock:
        _live_buffer = deque(maxlen=LIVE_BUFFER_MAX)
    _live_state = None

    _live_source_path = path
    _live_poll_interval = poll_interval
    _live_running = True

    # Arka plan thread'ini başlat
    _live_thread = threading.Thread(
        target=_file_tail_worker,
        daemon=True,
        name="HydroScanLiveTail",
    )
    _live_thread.start()

    return True


def stop_live() -> None:
    """
    Canlı modu durdurur (thread'e nazikçe stop sinyali verir).
    """
    global _live_running, _live_thread, _live_source_path, _live_state

    _live_running = False
    _live_source_path = None
    _live_state = None

    # Thread hâlâ yaşıyorsa kısa bir join ile temizle
    t = _live_thread
    if t is not None and t.is_alive():
        try:
            t.join(timeout=1.0)
        except Exception:
            # Thread kapanırken istisna olursa çok dert etmiyoruz
            pass

    _live_thread = None


def is_live_running() -> bool:
    """
    Canlı mod şu anda aktif mi?

    /live/snapshot cevabında "running" alanı için kullanılıyor.
    """
    return _live_running


def get_live_snapshot(limit: int = 500) -> List[Dict[str, Any]]:
    """
    Canlı buffer içindeki son N frame'i döndürür.

    limit:
        Maksimum kaç frame dönülsün (varsayılan 500).

    Dönüş:
        [
          {
            "time": "...",
            "pos_x": ...,
            "pos_y": ...,
            ...
          },
          ...
        ]
    """
    with _live_lock:
        data = list(_live_buffer)

    if limit and len(data) > limit:
        data = data[-limit:]

    return data


def get_live_thruster_info() -> Tuple[int, List[Dict[str, Any]]]:
    """
    Canlı buffer ve log içeriğine bakarak:

      1) Log içindeki:
           - "SIM modda X thruster oluşturuldu."
           - "SIM_CHn@chN: Pos=(...) Dir=(...)"
         satırlarından itici sayısı ve layout'u çıkarmaya çalışır.
      2) Eğer log tarafında bilgi yoksa,
         canlı frame'lerdeki actuator dizisi uzunluğundan tahmini
         thruster_count hesaplar.
      3) Hâlâ layout yok ama thruster_count > 0 ise
         sentetik dairesel layout üretir.

    Dönüş:
        (thruster_count, thruster_layout)

        thruster_count: int

        thruster_layout:
            [
              { "name": "...", "channel": 0, "pos_x": ..., "pos_y": ..., ... },
              ...
            ]
    """
    # Canlı buffer'dan bir miktar frame al
    frames = get_live_snapshot(limit=1000)

    log_content = ""
    # Mümkünse log dosyasının tamamını oku ve parsing.extract_thruster_info ile
    # offline/live tarafında tek bir mantık kullan
    if _live_source_path and os.path.exists(_live_source_path):
        try:
            with open(_live_source_path, "r", encoding="utf-8", errors="ignore") as f:
                log_content = f.read()
        except OSError:
            # Log okunamazsa en azından frame'lerden tahmin yaparız
            log_content = ""

    thruster_count = 0
    thruster_layout: List[Dict[str, Any]] = []

    if log_content:
        # Offline taraf ile aynı fonksiyonu kullan
        thruster_count, thruster_layout = extract_thruster_info(
            log_content=log_content,
            parsed_data=frames,
        )

    # Eğer log üzerinden bilgi çıkarılamadıysa, eski fallback davranışına dön
    if thruster_count == 0:
        for f in frames:
            arr = f.get("actuators") or []
            if isinstance(arr, list):
                thruster_count = max(thruster_count, len(arr))

    if not thruster_layout and thruster_count > 0:
        thruster_layout = build_synthetic_thruster_layout(thruster_count)

    return thruster_count, thruster_layout
