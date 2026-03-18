import serial
import struct
import math
import time
import os
from typing import List, Tuple, Optional

# Varsa projedeki ortak port bulucuyu kullan, yoksa kendi içine fallback yap
try:
    from sensors.common.port_locator import find_serial_port
except ImportError:
    find_serial_port = None

class LDRobotBackend:
    """
    LDROBOT D500 / STL27L Protokolü için Endüstriyel Backend.
    Otomatik Port Keşfi (Auto-Discovery) ve Platform Bağımsız Mimari.
    Baud: 230400, Paket Boyutu: 47 Byte
    """
    def __init__(self, port: Optional[str] = None, baud: int = 230400):
        self.port = port
        self.baud = baud
        self._ser: Optional[serial.Serial] = None
        self._is_open = False
        self._buffer = bytearray()

    def _autodetect_port(self) -> str:
        """
        Sistemi tarar ve LDROBOT'un (Silicon Labs CP210x) takılı olduğu portu bulur.
        """
        # 1. Ortak Port Bulucu (Varsa)
        if callable(find_serial_port):
            # LDROBOT genelde CP210x kullanır. Bu anahtar kelimeleri arıyoruz.
            port = find_serial_port(contains=["cp210", "silabs", "uart", "ldrobot"])
            if port:
                return port

        # 2. Linux Native (/dev/serial/by-id)
        # Linux'ta cihazlar ID'leri ile kendini belli eder, port ismi değişse bile ID sabittir.
        by_id_path = "/dev/serial/by-id"
        if os.path.isdir(by_id_path):
            try:
                for name in os.listdir(by_id_path):
                    low_name = name.lower()
                    if any(k in low_name for k in ("cp210", "silabs", "uart", "usb-to-uart")):
                        found_path = os.path.join(by_id_path, name)
                        # Gerçek device yolunu çöz (symlink resolution)
                        return os.path.realpath(found_path)
            except OSError:
                pass

        # 3. Windows & Linux Brute-Force Fallback
        # Bilinen olası portları manuel kontrol et
        candidates = []
        
        # Linux Standartları
        candidates.extend(["/dev/ttyUSB0", "/dev/ttyUSB1", "/dev/ttyACM0"])
        
        # Windows Standartları (COM3 - COM20 arası tara)
        # Windows'ta os.path.exists("COM3") her zaman doğru çalışmaz, try-except ile denenecek.
        if os.name == 'nt': 
            for i in range(3, 21):
                candidates.append(f"COM{i}")

        # Aday listesinden ilk geçerli olanı döndür (basit euristik)
        for p in candidates:
            if os.name != 'nt' and not os.path.exists(p):
                continue
            # Windows'ta portun varlığını serial.Serial denemesi ile anlayacağız (open aşamasında)
            # Burada sadece Linux için dosya kontrolü yaptık, Windows için adayı direkt dönüyoruz.
            return p
            
        return ""

    def open(self, cfg: Optional[object] = None):
        # 1. Öncelik: Constructor'dan gelen port
        target_port = self.port
        
        # 2. Öncelik: Config nesnesinden gelen port
        if not target_port and cfg and hasattr(cfg, "port"):
            target_port = cfg.port

        # 3. Öncelik: Environment Variable (Override amaçlı)
        if not target_port:
            target_port = os.getenv("HYDRONOM_LIDAR_PORT")
        
        # 4. Öncelik: Hydronom Felsefesi -> Auto Detect
        if not target_port:
            print("[LDROBOT] Port belirtilmedi, donanım taranıyor...")
            target_port = self._autodetect_port()

        if not target_port:
             raise ValueError("LDROBOT donanımı otomatik algılanamadı ve manuel port belirtilmedi.")

        print(f"[LDROBOT] Bağlanılıyor: {target_port} @ {self.baud}")
        
        try:
            self._ser = serial.Serial(target_port, self.baud, timeout=1)
            self._ser.reset_input_buffer()
            self._is_open = True
        except serial.SerialException as e:
            raise RuntimeError(f"LDROBOT Seri Port Hatası ({target_port}): {e}")

    def close(self):
        if self._ser:
            try:
                self._ser.close()
            except Exception:
                pass
            self._ser = None
        self._is_open = False

    def set_rate_hz(self, hz):
        # PWM kontrolü gerekirse buraya eklenir. Şimdilik pasif.
        pass

    def read_scan(self, timeout_s: float = 1.0) -> List[Tuple[float, float]]:
        """
        Sensörden veri paketlerini okur ve [(x,y), (x,y)...] listesi döndürür.
        Bloklayıcı olmayan (Non-blocking) okuma girişimleri içerir.
        """
        if not self._ser or not self._is_open:
            return []

        points = []
        t_end = time.time() + (timeout_s or 1.0)
        
        while time.time() < t_end:
            # Buffer'da yeterli veri yoksa işlemciyi yormamak için minik bekleme
            if self._ser.in_waiting < 47:
                time.sleep(0.001)
                continue
            
            # Header senkronizasyonu (0x54)
            # Veri kayması durumunda header'ı bulana kadar tek tek oku
            byte = self._ser.read(1)
            if byte != b'\x54':
                continue
            
            # Paketin geri kalanını (46 byte) oku
            data = self._ser.read(46)
            if len(data) != 46:
                continue

            # --- Protokol Çözümleme (D500 / STL27L) ---
            # data yapısı: [VerLen, Speed(2), StartAng(2), Data(36), EndAng(2), Timestamp(2), CRC]
            
            # Start/End Angle (Little Endian Unsigned Short, birim: 0.01 derece)
            start_angle_raw = struct.unpack("<H", data[3:5])[0]
            end_angle_raw = struct.unpack("<H", data[41:43])[0]
            
            start_angle = start_angle_raw / 100.0
            end_angle = end_angle_raw / 100.0

            # Açı farkı ve interpolasyon adımı
            diff = end_angle - start_angle
            if diff < 0:
                diff += 360
            
            # Bir pakette 12 ölçüm noktası vardır
            step = diff / 11.0 if diff > 0 else 0

            for i in range(12):
                # Her nokta 3 byte: Dist(2) + Conf(1)
                offset = 5 + (i * 3)
                dist_mm = struct.unpack("<H", data[offset:offset+2])[0]
                conf = data[offset+2]
                
                # Kalite Filtresi: LDROBOT için genelde > 100 veya > 200 iyidir.
                if dist_mm > 0 and conf > 100: 
                    # Lineer interpolasyon ile anlık açıyı bul
                    angle_deg = start_angle + (step * i)
                    angle_deg = angle_deg % 360
                    
                    # Koordinat Dönüşümü (Polar -> Cartesian)
                    # Not: Robotik koordinat sisteminde X ileri, Y sol.
                    # LDROBOT 0 derecesi kablo çıkışıdır (genelde arkası).
                    # Montaja göre buraya offset (+180 vb.) eklenebilir.
                    angle_rad = math.radians(angle_deg)
                    dist_m = dist_mm / 1000.0
                    
                    x = dist_m * math.cos(angle_rad)
                    y = dist_m * math.sin(angle_rad)
                    points.append((x, y))
            
            # Veri akışı sağlandı, döngüyü kırıp veriyi teslim et
            # Çok fazla bekleyip 'lag' oluşturmamak için batch size limiti
            if len(points) > 360: # Yaklaşık 1 tur
                break
                
        return points