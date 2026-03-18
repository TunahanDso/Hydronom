# sensors/gps/backends/nmea.py
import time
import threading
from typing import Optional, Dict, Any

from sensors.gps.config import GpsConfig
from .base import IGpsBackend

# Ortak port bulucu (varsa kullan, yoksa yerel fallback)
try:
    from utils.ports import find_serial_port  # type: ignore
except Exception:
    def find_serial_port(preferred: Optional[str] = None,
                         usb_id_hints: Optional[list] = None) -> Optional[str]:
        import os
        # Linux: /dev/serial/by-id tercih et
        by_id = "/dev/serial/by-id"
        if os.path.isdir(by_id):
            try:
                for name in os.listdir(by_id):
                    low = name.lower()
                    if not usb_id_hints or any(h in low for h in usb_id_hints):
                        return os.path.join(by_id, name)
            except Exception:
                pass
        # Yaygın adaylar
        for p in ("/dev/ttyUSB0", "/dev/ttyACM0"):
            if os.path.exists(p):
                return p
        # Windows kaba adaylar
        for n in range(1, 21):
            p = f"COM{n}"
            # os.path.exists COM için güvenilir değil; yine de ilk adayı döndür
            return p
        return preferred

try:
    import serial  # pyserial
    from serial import SerialException
    try:
        # Port listesini daha iyi seçmek için (varsa)
        from serial.tools import list_ports
    except Exception:
        list_ports = None  # type: ignore
except Exception:
    serial = None
    SerialException = Exception  # type: ignore
    list_ports = None  # type: ignore

try:
    import pynmea2
except Exception:
    pynmea2 = None


class NmeaBackend(IGpsBackend):
    """
    - Seri hat ayrı bir thread’de satır satır okunur (non-blocking).
    - RMC (zaman+tarih+konum) ve GGA (fix quality, HDOP, alt) parse edilir.
    - read_fix() anında son fix’i döndürür (None alanlar normaldir).
    - Otomatik port bulma ve bağlantı koparsa yeniden bağlanma (auto-reconnect) içerir.
    """
    def __init__(self):
        self._cfg: Optional[GpsConfig] = None
        self._ser: Optional["serial.Serial"] = None
        self._thr: Optional[threading.Thread] = None
        self._stop = threading.Event()
        self._lock = threading.Lock()
        self._last_fix: Dict[str, Any] = {
            "lat": None, "lon": None, "alt": None, "fix": 0, "hdop": None, "t_gps": None
        }
        # RMC tarih-saat takibi (GGA zamanını epoch’a çevirmek için)
        self._last_date: Optional[str] = None  # "ddmmyy"
        self._last_time: Optional[str] = None  # "hhmmss"
        # Bağlantı bilgisi
        self._port_in_use: Optional[str] = None
        self._baud_in_use: Optional[int] = None

    # ---------------------- Lifecycle ----------------------

    def open(self, cfg: GpsConfig) -> None:
        self._cfg = cfg
        if serial is None:
            print("⚠️ pyserial yok; NMEA backend çalışamaz.")
            return

        # Port seçimi: cfg.port "auto" / None ise otomatik ara
        desired_port = getattr(cfg, "port", None)
        if not desired_port or str(desired_port).lower() == "auto":
            # list_ports varsa “GPS/u-blox/gnss” gibi ipuçlarıyla seç
            port = None
            if list_ports:
                hints = ("gps", "gnss", "ublox", "u-blox", "prolific", "silicon", "ftdi", "cp210")
                for p in list_ports.comports():
                    desc = f"{p.description or ''} {p.hwid or ''}".lower()
                    if any(h in desc for h in hints):
                        port = p.device
                        break
                if port is None:
                    # ilk portu seç (varsa)
                    ports = list(list_ports.comports())
                    if ports:
                        port = ports[0].device
            if port is None:
                port = find_serial_port(usb_id_hints=["gps", "gnss", "u-blox", "ublox", "ftdi", "cp210"])
            desired_port = port or "/dev/ttyUSB0"

        desired_baud = int(getattr(cfg, "baud", 9600))

        self._connect_serial(desired_port, desired_baud)

        # Reader thread
        self._stop.clear()
        self._thr = threading.Thread(target=self._reader_loop, name="NMEA-Reader", daemon=True)
        self._thr.start()

    def _connect_serial(self, port: str, baud: int) -> None:
        """Seri hatta bağlan (hata olursa _ser=None kalır)."""
        try:
            self._ser = serial.Serial(port=port, baudrate=baud, timeout=0.1)
            self._port_in_use = port
            self._baud_in_use = baud
            # Bazı dongle’larda DTR/RTS ayarı gerekebilir:
            try:
                self._ser.dtr = True
                self._ser.rts = False
            except Exception:
                pass
            print(f"[NMEA] Serial opened: {port} @ {baud}")
        except Exception as e:
            print(f"⚠️ NMEA seri açılamadı: {e}")
            self._ser = None
            self._port_in_use = None
            self._baud_in_use = None

    # ---------------------- Reader Loop ----------------------

    def _reader_loop(self):
        backoff = 0.5
        buf = b""
        while not self._stop.is_set():
            try:
                if self._ser is None:
                    # Reconnect denemesi
                    if not self._cfg:
                        time.sleep(0.5); continue
                    desired_port = getattr(self._cfg, "port", None)
                    if not desired_port or str(desired_port).lower() == "auto":
                        # Port değişmiş olabilir → tekrar ara
                        desired_port = None
                        if list_ports:
                            ports = list(list_ports.comports())
                            if ports:
                                desired_port = ports[0].device
                        if desired_port is None:
                            desired_port = find_serial_port(usb_id_hints=["gps", "gnss", "u-blox", "ublox", "ftdi", "cp210"])
                    baud = int(getattr(self._cfg, "baud", 9600))
                    self._connect_serial(desired_port or "/dev/ttyUSB0", baud)
                    time.sleep(backoff)
                    backoff = min(3.0, backoff * 1.5)
                    continue

                chunk = self._ser.read(256)
                if not chunk:
                    time.sleep(0.01)
                    continue
                buf += chunk
                # Satırları ayır (CR/LF tolerant)
                while b"\n" in buf:
                    line, buf = buf.split(b"\n", 1)
                    line = line.strip().decode(errors="ignore")
                    if not line:
                        continue
                    if not (line.startswith("$GP") or line.startswith("$GN") or line.startswith("$GA") or line.startswith("$GL")):
                        continue
                    self._handle_line(line)

                # Başarılı okuma → backoff reset
                backoff = 0.5

            except (SerialException, OSError):
                # Bağlantı koptu → yeniden dene
                try:
                    if self._ser:
                        self._ser.close()
                except Exception:
                    pass
                self._ser = None
                time.sleep(backoff)
                backoff = min(3.0, backoff * 1.5)
            except Exception:
                # Satır/parse hataları sessizce yutulabilir
                time.sleep(0.01)

    # ---------------------- Parsers ----------------------

    def _handle_line(self, line: str):
        # pynmea2 varsa onu kullan; yoksa minimal parser
        if pynmea2 is not None:
            try:
                msg = pynmea2.parse(line, check=True)

                if msg.sentence_type == "RMC":
                    lat = self._to_deg(msg.lat, msg.lat_dir)
                    lon = self._to_deg(msg.lon, msg.lon_dir)
                    # tarih/saat (UTC)
                    t_gps = None
                    if getattr(msg, "datestamp", None) and getattr(msg, "timestamp", None):
                        t_gps = self._pynmea_to_epoch(msg.datestamp, msg.timestamp)
                        # Son tarihi/saatı kaydet → GGA ile birleştirmek için
                        try:
                            self._last_date = msg.datestamp.strftime("%d%m%y")
                            self._last_time = msg.timestamp.strftime("%H%M%S")
                        except Exception:
                            pass

                    with self._lock:
                        if lat is not None: self._last_fix["lat"] = lat
                        if lon is not None: self._last_fix["lon"] = lon
                        self._last_fix["t_gps"] = t_gps or self._last_fix["t_gps"]
                        self._last_fix["fix"] = 1 if getattr(msg, "status", "V") == "A" else 0
                    return

                if msg.sentence_type == "GGA":
                    lat = self._to_deg(msg.lat, msg.lat_dir)
                    lon = self._to_deg(msg.lon, msg.lon_dir)
                    alt = float(msg.altitude) if getattr(msg, "altitude", None) else None
                    hdop = float(msg.horizontal_dil) if getattr(msg, "horizontal_dil", None) else None

                    # GGA’da tarih yok; RMC’den gelen son tarihi kullan
                    t_gps = None
                    try:
                        if getattr(msg, "timestamp", None) and self._last_date:
                            t_gps = self._compose_epoch(self._last_date, msg.timestamp.strftime("%H%M%S"))
                    except Exception:
                        t_gps = None

                    with self._lock:
                        if lat is not None: self._last_fix["lat"] = lat
                        if lon is not None: self._last_fix["lon"] = lon
                        self._last_fix["alt"] = alt
                        self._last_fix["hdop"] = hdop
                        # fix quality: 0=Invalid,1=GPS,2=DGPS,...
                        try:
                            self._last_fix["fix"] = int(msg.gps_qual or 0)
                        except Exception:
                            pass
                        if t_gps is not None:
                            self._last_fix["t_gps"] = t_gps
                    return

                # RMC dışındaki cümlelerden de tarih/saat yakalanmış olabilir (nadiren)
                if msg.sentence_type == "RMC" and getattr(msg, "datestamp", None):
                    try:
                        self._last_date = msg.datestamp.strftime("%d%m%y")
                        self._last_time = msg.timestamp.strftime("%H%M%S") if getattr(msg, "timestamp", None) else self._last_time
                    except Exception:
                        pass

            except Exception:
                self._minimal_parse(line)
        else:
            self._minimal_parse(line)

    def _minimal_parse(self, line: str):
        """pynmea2 yoksa hızlı basit ayrıştırıcı (RMC + GGA)."""
        try:
            if line.startswith(("$GPRMC", "$GNRMC", "$GARMC", "$GLRMC")):
                parts = line.split(",")
                # $xxRMC, time, status, lat, N/S, lon, E/W, speed, course, date, ...
                t_hhmmss = parts[1] if len(parts) > 1 else ""
                status = parts[2] if len(parts) > 2 else "V"
                lat = self._to_deg(parts[3] if len(parts) > 3 else "", parts[4] if len(parts) > 4 else "")
                lon = self._to_deg(parts[5] if len(parts) > 5 else "", parts[6] if len(parts) > 6 else "")
                date_ddmmyy = parts[9] if len(parts) > 9 else ""
                t_gps = self._compose_epoch(date_ddmmyy, t_hhmmss) if (date_ddmmyy and t_hhmmss) else None
                with self._lock:
                    if lat is not None: self._last_fix["lat"] = lat
                    if lon is not None: self._last_fix["lon"] = lon
                    self._last_fix["t_gps"] = t_gps or self._last_fix["t_gps"]
                    self._last_fix["fix"] = 1 if status == "A" else 0
                self._last_date = date_ddmmyy or self._last_date
                self._last_time = t_hhmmss or self._last_time
                return

            if line.startswith(("$GPGGA", "$GNGGA", "$GAGGA", "$GLGGA")):
                parts = line.split(",")
                # $xxGGA, time, lat, N/S, lon, E/W, fix, sat, hdop, alt, M, ...
                t_hhmmss = parts[1] if len(parts) > 1 else ""
                lat = self._to_deg(parts[2] if len(parts) > 2 else "", parts[3] if len(parts) > 3 else "")
                lon = self._to_deg(parts[4] if len(parts) > 4 else "", parts[5] if len(parts) > 5 else "")
                fix_q = int(parts[6]) if len(parts) > 6 and parts[6].isdigit() else 0
                hdop = float(parts[8]) if len(parts) > 8 and parts[8] else None
                alt  = float(parts[9]) if len(parts) > 9 and parts[9] else None

                t_gps = None
                if t_hhmmss and self._last_date:
                    t_gps = self._compose_epoch(self._last_date, t_hhmmss)

                with self._lock:
                    if lat is not None: self._last_fix["lat"] = lat
                    if lon is not None: self._last_fix["lon"] = lon
                    self._last_fix["alt"] = alt
                    self._last_fix["hdop"] = hdop
                    self._last_fix["fix"] = fix_q
                    if t_gps is not None:
                        self._last_fix["t_gps"] = t_gps
        except Exception:
            pass

    # ---------------------- Helpers ----------------------

    def _to_deg(self, dm: Optional[str], hemi: Optional[str]) -> Optional[float]:
        # NMEA ddmm.mmmm (lat) / dddmm.mmmm (lon) → degrees; hemi=[N,S,E,W]
        if not dm:
            return None
        try:
            dm = dm.strip()
            if not dm:
                return None
            # nokta öncesi basamak sayısına göre ayır
            head = dm.split(".")[0]
            if len(head) > 4:   # lon: dddmm.mmmm
                d = float(dm[:3]); m = float(dm[3:])
            else:               # lat: ddmm.mmmm
                d = float(dm[:2]); m = float(dm[2:])
            val = d + (m / 60.0)
            if (hemi or "").upper() in ("S", "W"):
                val = -val
            return val
        except Exception:
            return None

    def _compose_epoch(self, ddmmyy: str, hhmmss: str) -> Optional[float]:
        # ddmmyy + hhmmss → epoch(UTC)
        try:
            if len(ddmmyy) != 6 or len(hhmmss) < 6:
                return None
            dd = int(ddmmyy[0:2]); mm = int(ddmmyy[2:4]); yy = int(ddmmyy[4:6]) + 2000
            hh = int(hhmmss[0:2]); mi = int(hhmmss[2:4]); ss = int(hhmmss[4:6])
            import datetime
            dt = datetime.datetime(yy, mm, dd, hh, mi, ss, tzinfo=datetime.timezone.utc)
            return dt.timestamp()
        except Exception:
            return None

    def _pynmea_to_epoch(self, date_obj, time_obj) -> Optional[float]:
        try:
            import datetime
            dt = datetime.datetime.combine(date_obj, time_obj, tzinfo=datetime.timezone.utc)
            return dt.timestamp()
        except Exception:
            return None

    # ---------------------- Public API ----------------------

    def read_fix(self) -> Optional[Dict[str, Any]]:
        # Non-blocking snapshot
        with self._lock:
            return dict(self._last_fix)

    def close(self) -> None:
        self._stop.set()
        try:
            if self._thr and self._thr.is_alive():
                self._thr.join(timeout=1.0)
        except Exception:
            pass
        try:
            if self._ser:
                self._ser.close()
        except Exception:
            pass
        self._thr = None
        self._ser = None
        self._port_in_use = None
        self._baud_in_use = None
