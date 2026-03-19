# 🧠 Hydronom System Architecture

Hydronom, platform bağımsız, modüler ve katmanlı bir otonom sistem mimarisidir.  
Sistem; sensör verisinin toplanmasından, karar üretimine ve fiziksel kontrol çıktısına kadar uçtan uca bir yapı sunar.

Bu doküman, Hydronom sisteminin genel mimarisini, katmanlarını ve veri akışını açıklamaktadır.

---

## 🧩 Genel Mimari Yaklaşım

Hydronom aşağıdaki prensipler üzerine kuruludur:

- **Katmanlı Mimari (Layered Architecture)**
- **Modülerlik (Plug & Play Components)**
- **Platform Bağımsızlık**
- **Gerçek Zamanlı Veri İşleme**
- **6-DoF Fizik Tabanlı Kontrol**
- **Dağıtık Sistem Yapısı**

---

## 🏗️ Sistem Katmanları

Hydronom sistemi 4 ana katmandan oluşur:


[ Sensors (Python) ]
↓
[ Runtime (C# .NET) ]
↓
[ Actuators (C/C++ / Embedded) ]
↓
[ Physical World ]


Ayrıca sistemin dış gözlemlenmesi ve kontrolü için:


[ Gateway ] → [ Hydronom Ops (Web UI) ]


---

## 🔹 1. Sensör Katmanı (Python)

Bu katman, fiziksel sensörlerden veri toplar ve standart hale getirir.

### Desteklenen Sensörler
- IMU
- GPS
- LiDAR
- Kamera (opsiyonel)

### Özellikler
- Sensör sürücüleri (driver tabanlı yapı)
- Simülasyon / gerçek / hybrid mod
- Otomatik sensör keşfi (SensorManager)
- Zaman senkronizasyonu (TimeSync)

### Veri Yapıları
- `Sample` → Ham sensör verisi
- `FusedState` → Birleştirilmiş durum bilgisi
- `Event` → Anlamlı sistem olayları
- `Health` → Sensör sağlık durumu

---

## 🔹 2. Runtime Katmanı (C# .NET)

Sistemin ana beyni bu katmandır.

### Ana Modüller

#### 🧠 Decision (Karar)
- Araç ne yapmalı?
- Kuvvet / tork (wrench) üretimi

#### 📋 Task Manager (Görev)
- Aktif görev yönetimi
- FSM / görev geçişleri

#### 🔍 Analysis (Analiz)
- Çevreyi yorumlama
- Engel, hedef, risk analizi

#### 🔁 Feedback (Geri Besleme)
- Sistem davranışını izleme
- Telemetri üretimi

---

## ⚙️ 6-DoF Kontrol Modeli

Hydronom, klasik “ileri git / dön” yaklaşımı yerine fiziksel model kullanır:


Fx, Fy, Fz → Lineer kuvvetler
Tx, Ty, Tz → Açısal torklar


Bu sayede:

- Daha gerçekçi hareket
- Platform bağımsız kontrol
- Gelişmiş stabilite ve manevra kabiliyeti

---

## 🔹 3. Aktüatör Katmanı (Embedded / C/C++)

Bu katman fiziksel donanımı kontrol eder.

### Bileşenler
- ESC (Electronic Speed Controller)
- Motorlar
- Servolar

### Özellikler
- STM32 tabanlı yapı
- Donanımdan bağımsız kontrol (abstraction)
- Plug & Play sürücü sistemi
- Fallback ve güvenli durdurma mekanizmaları

---

## 🔹 4. Haberleşme Katmanı

Hydronom dağıtık bir sistemdir.

### İç Haberleşme
- TCP
- NDJSON (Newline Delimited JSON)

### Özellikler
- Düşük gecikme
- Gerçek zamanlı veri akışı
- Platformlar arası uyumluluk

### Alternatif İletişim
- RF
- LoRa

---

## 🌐 Gateway ve Hydronom Ops

### Gateway
- Runtime ile dış sistemler arasında köprü görevi görür
- Veri dönüştürme ve yayınlama

### Hydronom Ops (Web UI)
- Araç takibi (2D / 3D)
- Telemetri görselleştirme
- Sensör verisi izleme
- Aktüatör ve kuvvet çıktıları
- Diagnostik ekranları

---

## 🔄 Veri Akışı

Hydronom’da veri akışı şu şekildedir:


Sensors → Sample
→ Fusion
→ FusedState
→ Analysis
→ Decision
→ Wrench (Fx,Fy,Fz,Tx,Ty,Tz)
→ Actuators
→ Feedback


---

## 🧠 AI Katmanı (Hydronom.AI)

Hydronom, LLM tabanlı karar desteği içerir.

### Özellikler
- LLaMA tabanlı local AI
- ToolCall tabanlı görev üretimi
- JSON plan çıktıları
- Suggest Mode (öneri)
- Autopilot Mode (kontrollü otomasyon)

---

## 🔒 Güvenlik ve Dayanıklılık

- Watchdog mekanizmaları
- Veri kesintisi tespiti (data stall detection)
- SafeStop
- Merkezi limiter sistemi
- Health monitoring

---

## 🚀 Gelecek Genişletmeler

- Multi-vehicle coordination
- Twin simulation
- Event-based learning
- Adaptive tuning
- Full 6-DoF unified control for all platforms

---

## 💡 Sonuç

Hydronom:

- Sadece bir araç kontrol sistemi değil  
- Platform bağımsız bir otonom altyapıdır  

Amaç:

> Sensörden karara, karardan harekete uzanan  
> tam entegre bir otonom sistem standardı oluşturmak
