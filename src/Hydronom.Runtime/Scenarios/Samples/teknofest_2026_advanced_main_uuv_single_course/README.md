# TEKNOFEST 2026 İleri Kategori - Ana UUV Tek Parkur

Bu senaryo Hydronom ana su altı aracı için hazırlanmış tek aşamalı, ana araç odaklı ileri kategori test parkurudur.

## Amaç

Bu paket, ana UUV'nin tek başına yapması gereken davranışları izole eder:

1. Başlangıç alanından çıkış
2. Yönlendirme tahtası / hat takibi
3. Mini ROV bırakma / ipucu-handoff bölgesine varış
4. İpucu koordinatlarının verilmiş kabul edilmesi
5. Otonom intikal
6. Şamandıra etrafından dönüş
7. Bitiş koordinatındaki karesel alana giriş
8. Sadece bitiş karesi içinde yüzeye çıkış

## Bilerek çıkarılanlar

- Mini ROV boru içi navigasyon objective'leri yoktur.
- Ana aracın boruya girmesi objective değildir.
- Eski stage-2 no-go zone objeleri yoktur.
- Gate/no-go/controlled-zone temsili kaldırılmıştır.
- Tüm planner/judge objeleri tek stage kapsamındadır.

## Senaryo ID

teknofest_2026_advanced_main_uuv_single_course

## Ana tasarım ilkesi

Bu senaryo resmi yarışma full senaryosunun yerine geçmez; ana UUV kontrol, planner, line-following ve otonom intikal testini temiz görmek için kullanılır.