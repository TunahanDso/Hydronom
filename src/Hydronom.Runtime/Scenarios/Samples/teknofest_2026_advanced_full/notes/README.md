# TEKNOFEST 2026 Advanced Full Scenario Package

Bu paket Hydronom Runtime/Ops için yarışma dokümanlarına göre hazırlanmış temsilî full parkur paketidir.

## İçerik

- Hat Takibi ve Kapalı Alan İncelemesi
- Mini ROV boru içi ipucu görevi
- Otonom Navigasyon, İntikal ve Kontrollü Alan Geçişi
- Gerçekçi Ops/Tactical 3D render metadata
- Vehicle access rules

## Kaynak notları

- Yönlendirme yolu malzemesi: ST37 sac
- Yönlendirme yolu rengi: RAL3020 kırmızı
- Takip şeridi: RAL9005 siyah
- Hat takibi ve ipucu bulma teması için süre temsili olarak 300 saniye alınmıştır.

## Sonraki bağlantı adımları

1. Runtime scenario package loader bu klasörü okuyacak.
2. Runtime world snapshot bu object tiplerini Ops'a gönderecek.
3. TacticalWorldLayer bu semantic objectleri gerçekçi çizecek.
4. Mission/Objective runtime bu objective zincirlerini okuyacak.
