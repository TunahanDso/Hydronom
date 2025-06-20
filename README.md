# 🚤 Hydronom – Otonom Su Üstü Aracı Sistemi

**Hydronom**, Yıldız Teknik Üniversitesi *Stars of Hydro* topluluğu tarafından geliştirilen, modüler ve platform bağımsız bir otonom sistem yazılımıdır. Su üstü araçlarında görev planlama, çevre algılama, karar verme ve kontrol fonksiyonlarını entegre bir yapıda sunar.

---

## 🎯 Amaç

Hydronom, gerçek zamanlı görev icrası ve otonom hareket kabiliyetiyle rekabetçi yarışmalarda (örneğin: Njord Challenge) ve akademik çalışmalarda kullanılmak üzere geliştirilmiştir. Sistem, görev tabanlı karar verme algoritmaları ve modüler mimarisiyle farklı platformlara kolayca entegre edilebilir.

---

## 🧠 Sistem Mimarisi

Hydronom, 3 ana katmandan oluşur:

### 1. Veri İşleme Katmanı (Python + ROS)
- Kamera, lidar, sonar, IMU gibi sensörlerden veri alır.
- Görüntü işleme, nesne tanıma, haritalama ve sensör füzyonu sağlar.
- C# ile iletişim kurarak analiz modülünü besler.

### 2. Karar ve Görev Yönetimi Katmanı (C#)
- Görev üretimi, atama ve yürütme modüllerini içerir.
- Karar ağacı ve durum yönetimi üzerinden senaryo işletimi sağlar.
- Manuel veya otonom modlar arasında geçiş yapılabilir.

### 3. Gömülü Kontrol Katmanı (C++)
- Jetson Nano'da çalışan düşük seviyeli motor ve yön kontrol kütüphanelerini barındırır.
- `.so` formatındaki C++ kütüphaneler C# tarafından çağrılır.

---

## 📡 Haberleşme Yapısı

- **Jetson – Bilgisayar:** TCP/UDP soket iletişimi
- **C# – Python:** JSON veri aktarımıyla gerçek zamanlı entegrasyon
- **Geri bildirim modülü:** Zaman damgalı kayıt tutma ve geçmiş veriye göre görev tamamlama

---

## 🛠 Donanım Bileşenleri

- **Ana Kart:** NVIDIA Jetson Nano (JN30D)
- **Kontrol Kartı:** STM32 tabanlı ESC/SERVO kontrolcüsü
- **Sensörler:**  
  - Pi Kamera  
  - Lidar  
  - Sonar  
  - IMU (9 DoF)  
- **Diğer:**  
  - 3D baskı gövde  
  - Su geçirmez dolap ve bağlantılar  
  - Uzaktan kontrol modülü (USB gamepad desteği)

---

## 💻 Uzak Kontrol Yazılımı

- C# ile geliştirilen masaüstü arayüz
- Özellikler:
  - Anlık veri ve görüntü akışı
  - Görev listesi ve durumu takibi
  - Manuel joystick kontrolü
  - Harita üzerinden görev atama

---

## 🧩 Ana Modüller

| Modül         | Açıklama |
|---------------|----------|
| Analiz Modülü | Görsel ve sayısal verilerden anlamlı bilgi üretir. |
| Karar Modülü  | Durumlara göre görev eşleşmesini yapar ve kontrol devrini sağlar. |
| Görev Modülü  | Saha ve nesne verisine dayalı görev üretir. |
| Kontrol Modülü| Yön, hız ve görev icra komutlarını verir. |
| Feedback Modülü | Geriye dönük verilerle sistemin güvenli çalışmasını sürdürür. |

---

## 📁 Klasör Yapısı

```text
Hydronom/
├── core_csharp/           # Karar ve görev sistemleri (C#)
├── sensor_processing/     # Sensör işleme ve ROS node’ları (Python)
├── embedded/              # Gömülü motor kontrol (C++)
├── remote_control_app/    # Manuel kontrol arayüzü (C#)
├── docs/                  # KTR, sistem şemaları, teknik belgeler
└── assets/                # Görevler, haritalar, örnek veriler
