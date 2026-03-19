# Contributing to Hydronom

Hydronom, sıradan bir açık kaynak proje değildir.  
Bu sistem, modüler ve platform bağımsız bir otonom mimari geliştirme sürecinin parçasıdır ve kontrollü şekilde ilerlemektedir.

Bu nedenle katkı süreci, klasik open-source projelerden farklıdır.

---

## 🚫 Doğrudan Katkı (Pull Request)

Bu repo **herkese açık katkıya kapalıdır**.

- Rastgele Pull Request (PR) gönderimleri kabul edilmez
- Ön onay olmadan yapılan katkılar değerlendirmeye alınmayabilir
- Kod tabanının bütünlüğünü korumak önceliklidir

---

## ✅ Katkı Sağlamak İstiyorsanız

Hydronom sistemine katkı sağlamak isteyen kişilerin aşağıdaki süreci takip etmesi gerekmektedir:

1. **Sistemi inceleyin**
   - Repo yapısını anlayın
   - Mimari yaklaşımı kavrayın
   - Modüller arası veri akışını analiz edin

2. **Başvuru yapın**
   - Resmi ekip başvuru formunu doldurun

3. **Teknik değerlendirme**
   - Sistem anlayışınızı ölçen teknik sınava girin

4. **Mülakat süreci**
   - Uygun adaylar görüntülü mülakata alınır

5. **Ekip içi katkı**
   - Kabul edilen adaylar ekip içinde aktif geliştirme sürecine dahil edilir

---

## 🧠 Katkı Felsefesi

Hydronom’da katkı demek:

- Sadece kod yazmak değil
- Sistemi anlamak
- Doğru mimari kararlar almak
- Mevcut yapıyı bozmadan geliştirmek

Beklenen yaklaşım:

- Modüler düşünme
- Sistem bütünlüğünü koruma
- Performans ve güvenlik bilinci
- Gerçek dünya koşullarını dikkate alma

---

## ⚙️ Teknik Standartlar

Katkı sağlayan ekip üyelerinden beklenenler:

- Katmanlı mimariye uyum
- Dil dağılımına sadakat:
  - Python → sensör & veri pipeline
  - C# → runtime & sistem yönetimi
  - C/C++ → gömülü sistem & kontrol
- Standart veri yapılarının korunması (Sample, FusedState, Event vb.)
- TCP / NDJSON haberleşme yapısına uyum
- 6-DoF fiziksel model yaklaşımının korunması

---

## 🔒 Gizlilik ve Sorumluluk

Hydronom sistemi:

- Aktif olarak geliştirilmektedir
- Tüm bileşenleri public olmayabilir
- Kritik modüller ekip içi tutulabilir

Ekip üyeleri:

- Kodları izinsiz paylaşamaz
- Sistemi üçüncü taraflara aktaramaz
- Projeyi yarışma veya ticari amaçla izinsiz kullanamaz

---

## 📩 İletişim

Katkı süreci, ekip başvurusu ve teknik değerlendirme ile ilgilidir.

İletişim:  
**tdelisalihoglu@outlook.com.tr**

---

> Hydronom, kod yazılan bir repo değil,  
> bir sistem inşa etme sürecidir.
