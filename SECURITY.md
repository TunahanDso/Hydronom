# Security Policy

## Supported Versions

Hydronom aktif olarak gelişen bir projedir. Bu nedenle yalnızca en güncel sürüm güvenlik, kararlılık ve bakım açısından desteklenmektedir.

| Version | Supported |
| ------- | --------- |
| Latest  | ✅ |
| Older versions | ❌ |

## Reporting a Vulnerability

Hydronom sistemiyle ilgili bir güvenlik açığı, kritik hata, yetkisiz erişim riski, veri sızıntısı ihtimali veya güvenlik odaklı mimari zafiyet tespit ederseniz, lütfen bunu kamuya açık issue olarak paylaşmayın.

Bunun yerine doğrudan geliştirici ile iletişime geçin:

- **İletişim:** tdelisalihoglu@outlook.com.tr

Lütfen bildiriminizde mümkün olduğunca şu bilgileri paylaşın:

- Açığın veya zafiyetin kısa açıklaması
- Etkilenen modül, dosya veya bileşen
- Varsa tekrar üretim adımları
- Olası etki seviyesi
- Varsa ekran görüntüsü, log veya örnek veri

## Response Process

Bir güvenlik bildirimi alındığında süreç genel olarak şu şekilde ilerler:

1. Bildirim alınır ve incelenir.
2. Sorunun doğrulanabilirliği değerlendirilir.
3. Gerekirse ek teknik bilgi talep edilir.
4. Uygun görülürse düzeltme hazırlanır.
5. Kritik durumlarda ilgili bileşen geçici olarak sınırlandırılabilir veya kullanım dışı bırakılabilir.
6. Gerekli düzeltmeler tamamlandıktan sonra uygun şekilde güncelleme yapılır.

## Scope

Bu güvenlik politikası özellikle aşağıdaki alanları kapsar:

- Runtime kontrol akışı
- Gateway ve dış haberleşme katmanları
- TCP / JSON / NDJSON veri akışı
- Telemetri ve dış yayın mekanizmaları
- Sensör veri işleme zinciri
- Harici komut, görev ve kontrol girişleri
- Yetkisiz erişim veya kontrol riski oluşturabilecek bileşenler

## Responsible Disclosure

Güvenlik açıklarının sorumlu şekilde bildirilmesi beklenmektedir. Güvenlik zafiyetlerinin kamuya açık şekilde paylaşılması, kötüye kullanılması veya proje bütünlüğünü zedeleyecek biçimde dağıtılması kabul edilmez.

Bildirim yapan kişilerden beklentimiz:

- Sorunu önce özel olarak bildirmeleri
- Düzeltme için makul süre tanımaları
- Açığı istismar etmeye çalışmamaları
- Üçüncü taraflarla izinsiz paylaşmamaları

## Notes

Hydronom aktif olarak gelişen deneysel ve mühendislik odaklı bir sistemdir. Bu nedenle bazı bileşenler araştırma, prototipleme veya saha testine yönelik olabilir. Üretim ortamında veya kritik görevlerde kullanılmadan önce ek güvenlik, doğrulama ve saha testi yapılması önerilir.
