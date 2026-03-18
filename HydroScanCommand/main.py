import re
import math
import statistics
import os
from datetime import datetime

# ==========================================
# RENKLENDİRME VE STİL SINIFI
# ==========================================
class Colors:
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'
    UNDERLINE = '\033[4m'
    GREY = '\033[90m'

# ==========================================
# ANALİZ MOTORU
# ==========================================
class LogAnalyzer:
    def __init__(self):
        # Temel Veriler
        self.headers = []
        self.positions = []
        self.timestamps = []
        self.actuators = set()
        
        # İstatistiksel Veriler
        self.total_distance = 0.0
        self.throttle_values = []
        self.heading_errors = []
        self.obstacle_events = 0
        self.max_force_x = 0.0
        
        # Durum Bayrakları
        self.first_timestamp_found = False

    def draw_bar(self, value, max_val=1.0, width=10, color=Colors.OKGREEN):
        """Konsolda görsel bar çizer: [|||||     ]"""
        try:
            val_norm = max(0, min(1, abs(float(value)) / max_val))
            filled = int(val_norm * width)
            bar = "█" * filled + "░" * (width - filled)
            return f"{color}[{bar}]{Colors.ENDC}"
        except:
            return ""

    def process_line(self, line):
        """Satırı işler: Header mı yoksa Veri mi karar verir."""
        line = line.strip()
        if not line: return None

        # Zaman damgası kontrolü (Veri akışı başladı mı?)
        ts_match = re.search(r'\[(\d{4}-\d{2}-\d{2}T.*?)\.?\d*Z\]', line)
        
        if ts_match:
            self.first_timestamp_found = True
            self.parse_data(line, ts_match)
            return self.format_data_line(line)
        elif not self.first_timestamp_found:
            # Henüz zaman damgası görmedik, bu bir başlık/metadata satırı
            self.headers.append(line)
            return f"{Colors.GREY}║ INFO ║ {line}{Colors.ENDC}"
        else:
            # Zaman damgası yok ama veri akışı içinde (örn: [CTL], [DBG])
            self.parse_sub_data(line)
            return self.format_data_line(line)

    def parse_data(self, line, ts_match):
        """Ana veri satırlarını ayrıştırır."""
        # Tarih/Saat
        try:
            dt_str = ts_match.group(1)[:19] # Sadece saniyeye kadar al
            dt = datetime.strptime(dt_str, "%Y-%m-%dT%H:%M:%S")
            self.timestamps.append(dt)
        except ValueError:
            pass

        # Pozisyon (Mesafe hesabı için)
        pos_match = re.search(r'pos=\(([\d\.-]+),([\d\.-]+),([\d\.-]+)\)', line)
        if pos_match:
            x, y, z = map(float, pos_match.groups())
            if self.positions:
                lx, ly, lz = self.positions[-1]
                dist = math.sqrt((x-lx)**2 + (y-ly)**2 + (z-lz)**2)
                self.total_distance += dist
            self.positions.append((x, y, z))

        # Komut Verileri (Throttle takibi)
        cmd_match = re.search(r'cmd\(thr=([\d\.-]+)', line)
        if cmd_match:
            self.throttle_values.append(float(cmd_match.group(1)))

        # Engel Tespiti
        if "obsAhead=True" in line:
            self.obstacle_events += 1

    def parse_sub_data(self, line):
        """Alt veri satırlarını ([CTL], [Actuator]) ayrıştırır."""
        # Heading Hatası (dHead)
        dhead_match = re.search(r'dHead=([\d\.-]+)', line)
        if dhead_match:
            self.heading_errors.append(abs(float(dhead_match.group(1))))

        # Aktüatör İsimleri
        if "[Actuator]" in line:
            acts = re.findall(r'(H_[A-Z]{2}|V_[A-Z]{2})', line)
            self.actuators.update(acts)
            
            # Gövde Kuvveti Max (F_body x)
            fb_match = re.search(r'F_body=\(([\d\.-]+)', line)
            if fb_match:
                fx = float(fb_match.group(1))
                if fx > self.max_force_x: self.max_force_x = fx

    def format_data_line(self, line):
        """Satırı renklendirir ve görsel barlar ekler."""
        # Görsel Barlar Ekle (Eğer cmd satırıysa)
        viz_suffix = ""
        cmd_match = re.search(r'cmd\(thr=([\d\.-]+), rud=([\d\.-]+)\)', line)
        if cmd_match:
            t, r = float(cmd_match.group(1)), float(cmd_match.group(2))
            viz_suffix = f" {self.draw_bar(t, 1.0, 5, Colors.FAIL)} {self.draw_bar(r, 1.0, 5, Colors.OKCYAN)}"
        
        # Regex ile Renklendirme
        line = re.sub(r'(\[\d{4}-\d{2}-\d{2}T.*?\])', f'{Colors.OKCYAN}\\1{Colors.ENDC}', line) # Zaman
        line = re.sub(r'(\[[A-Z_]+\])', f'{Colors.WARNING}{Colors.BOLD}\\1{Colors.ENDC}', line) # Etiketler
        line = re.sub(r'\b([a-zA-Z_]+)=', f'{Colors.OKBLUE}\\1{Colors.ENDC}=', line) # Anahtarlar
        line = re.sub(r'(=|:|\s)([-+]?\d*\.\d+|[-+]?\d+)', f'\\1{Colors.OKGREEN}\\2{Colors.ENDC}', line) # Sayılar
        line = re.sub(r'(True)', f'{Colors.FAIL}True{Colors.ENDC}', line)
        line = re.sub(r'(False)', f'{Colors.OKGREEN}False{Colors.ENDC}', line)
        
        return line + viz_suffix

    def print_report(self):
        print("\n" + "="*60)
        print(f"{Colors.HEADER} 🌊  HYDRONOM - DETAYLI SİSTEM RAPORU  🌊{Colors.ENDC}")
        print("="*60)

        # 1. BÖLÜM: BAŞLIK BİLGİLERİ (Header)
        if self.headers:
            print(f"\n{Colors.BOLD}📋 [SİSTEM BAŞLANGIÇ BİLGİLERİ]{Colors.ENDC}")
            for h in self.headers:
                print(f"   > {h}")
            print("-" * 60)

        if not self.timestamps:
            print(f"{Colors.FAIL}❌ Yeterli zaman damgalı veri bulunamadı.{Colors.ENDC}")
            return

        # Hesaplamalar
        duration = (self.timestamps[-1] - self.timestamps[0]).total_seconds()
        avg_speed = self.total_distance / duration if duration > 0 else 0
        avg_thr = statistics.mean(self.throttle_values) if self.throttle_values else 0
        avg_err = statistics.mean(self.heading_errors) if self.heading_errors else 0
        
        # 2. BÖLÜM: PERFORMANS METRİKLERİ
        print(f"\n{Colors.BOLD}🚀 [SEYİR PERFORMANSI]{Colors.ENDC}")
        print(f"   ⏱️  Toplam Süre:       {Colors.OKCYAN}{duration:.3f} sn{Colors.ENDC}")
        print(f"   📏  Toplam Mesafe:     {Colors.OKCYAN}{self.total_distance:.3f} m{Colors.ENDC}")
        print(f"   🏎️  Ortalama Hız:      {Colors.OKGREEN}{avg_speed:.3f} m/s{Colors.ENDC} ({avg_speed*3.6:.1f} km/h)")
        print(f"   💪  Max Gövde Kuvveti: {Colors.FAIL}{self.max_force_x:.2f} N{Colors.ENDC}")

        # 3. BÖLÜM: KONTROL VE KARARLILIK
        print(f"\n{Colors.BOLD}🎮 [KONTROL & OTONOMİ]{Colors.ENDC}")
        print(f"   🎯  Ort. Yönelim Hatası: {avg_err:.2f}°")
        print(f"   ⚡  Motor Efor Skoru:    %{avg_thr*100:.1f}")
        print(f"   🚧  Engel Tespit Sayısı: {Colors.FAIL if self.obstacle_events > 0 else Colors.OKGREEN}{self.obstacle_events}{Colors.ENDC}")

        # 4. BÖLÜM: DONANIM DURUMU
        print(f"\n{Colors.BOLD}⚙️  [AKTİF DONANIMLAR]{Colors.ENDC}")
        act_list = sorted(list(self.actuators))
        if act_list:
            print(f"   Tespit Edilen: {', '.join(act_list)}")
        else:
            print("   Tespit edilen aktüatör yok.")

        print("\n" + "="*60 + "\n")

# ==========================================
# ANA PROGRAM
# ==========================================
def main():
    analyzer = LogAnalyzer()
    
    file_path = "log.txt"
    
    print(f"{Colors.HEADER}Hydronom Log Oynatıcı Başlatılıyor...{Colors.ENDC}")
    print(f"Dosya okunuyor: {Colors.UNDERLINE}{file_path}{Colors.ENDC}\n")
    
    # Dosya okuma işlemi
    if not os.path.exists(file_path):
        print(f"{Colors.FAIL}HATA: '{file_path}' dosyası bulunamadı!{Colors.ENDC}")
        print("Lütfen bu script'in olduğu klasöre log verilerini içeren 'log.txt' dosyasını oluşturun.")
        return

    try:
        with open(file_path, 'r', encoding='utf-8') as file:
            for line in file:
                output = analyzer.process_line(line)
                if output:
                    print(output)
                    # Gerçek zamanlı akış hissi için aşağıdaki satırı açabilirsin:
                    # import time; time.sleep(0.02)
        
        # Dosya bittikten sonra raporu bas
        analyzer.print_report()
        
    except Exception as e:
        print(f"\n{Colors.FAIL}Beklenmeyen bir hata oluştu: {e}{Colors.ENDC}")

if __name__ == "__main__":
    main()