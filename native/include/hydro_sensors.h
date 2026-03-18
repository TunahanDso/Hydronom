// hydro_sensors.h
#pragma once

// -----------------------------------------------------------------------------
// Ortak API makroları
// -----------------------------------------------------------------------------
#ifdef _WIN32
  #ifdef HYDRO_SENSORS_EXPORTS
    #define HS_API __declspec(dllexport)
  #else
    #define HS_API __declspec(dllimport)
  #endif
#else
  #define HS_API
#endif

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// -----------------------------------------------------------------------------
// Temel sabitler
// -----------------------------------------------------------------------------

// Aynı anda yönetilecek maksimum sensör sayısı
#define HS_MAX_SENSORS        16
// Aynı anda tutulacak maksimum event sayısı (halka buffer için)
#define HS_MAX_EVENTS         64

// -----------------------------------------------------------------------------
// Temel tipler (Python tarafındaki domain tiplerinin C karşılıkları)
// -----------------------------------------------------------------------------

// Sensör tipi (Python'daki enum/str karşılığı)
typedef enum HsSensorType
{
    HS_SENSOR_UNKNOWN = 0,
    HS_SENSOR_IMU     = 1,
    HS_SENSOR_GPS     = 2,
    HS_SENSOR_LIDAR   = 3,
    HS_SENSOR_CAMERA  = 4,
    HS_SENSOR_CUSTOM  = 100
} HsSensorType;

// Fonksiyon dönüş kodları
typedef enum HsResult
{
    HS_OK            = 0,
    HS_ERR_GENERIC   = -1,
    HS_ERR_INVALID   = -2,
    HS_ERR_IO        = -3,
    HS_ERR_TIMEOUT   = -4,
} HsResult;

// Health durumu (Python'daki health dosyalarının C karşılığı)
typedef enum HsHealthStatus
{
    HS_HEALTH_UNKNOWN = 0,
    HS_HEALTH_OK      = 1,
    HS_HEALTH_WARN    = 2,
    HS_HEALTH_ERROR   = 3,
} HsHealthStatus;

// Event tipi (Python Event sistemi)
typedef enum HsEventType
{
    HS_EVENT_NONE          = 0,
    HS_EVENT_SENSOR_ERROR  = 1,
    HS_EVENT_SENSOR_WARN   = 2,
    HS_EVENT_FUSION_RESET  = 3,
    HS_EVENT_CUSTOM        = 100
} HsEventType;

// Event şiddeti
typedef enum HsEventSeverity
{
    HS_SEV_INFO  = 0,
    HS_SEV_WARN  = 1,
    HS_SEV_ERROR = 2,
} HsEventSeverity;

// Zaman damgası (ms cinsinden, epoch ya da runtime start fark etmez)
typedef uint64_t HsTimestampMs;

// -----------------------------------------------------------------------------
// FusedState ve örnek (Sample) tipleri
// -----------------------------------------------------------------------------

// Python'daki FusedState'in C karşılığı (SLAM / fusion için genişleyebilir yapı)
// Not: struct_size + version ile ikili uyumluluk korunur (ileride alan eklemek için).
typedef struct HsFusedState
{
    // Versiyonlama / API uyumluluğu
    uint32_t     struct_size;   // sizeof(HsFusedState)
    uint32_t     version;       // 1,2,3...

    // Zaman serisi takibi için sıra numarası
    uint64_t     seq;           // her hs_tick çağrısında arttırılabilir

    // Pozisyon ve hız (dünya ekseninde)
    double pos_x;
    double pos_y;
    double pos_z;

    double vel_x;
    double vel_y;
    double vel_z;

    // Yönelim (Euler, derece cinsinden)
    double yaw_deg;
    double pitch_deg;
    double roll_deg;

    // Türetilmiş büyüklükler
    double speed_mps;   // |v| = sqrt(vx^2 + vy^2 + vz^2)

    // Fix / kalite bilgisi
    int    has_fix;     // 0 = güvenilmez, 1 = güvenilir
    double quality;     // 0.0–1.0 arası güven skoru

    // Zaman damgası
    HsTimestampMs timestamp;

    // Gelecekteki SLAM / mapping / ek meta veriler için rezerv alanlar
    uint32_t reserved_u32[4];
    double   reserved_f64[8];

} HsFusedState;

// Python Sample yapısının sadeleştirilmiş C karşılığı
typedef struct HsSample
{
    HsSensorType  type;
    HsTimestampMs timestamp;

    // Basit genelleştirilmiş alanlar
    double v0;
    double v1;
    double v2;
    double v3;
} HsSample;

// Health snapshot
typedef struct HsHealth
{
    HsHealthStatus status;
    HsTimestampMs  timestamp;
    char           message[128]; // Kısa açıklama
} HsHealth;

// Event yapısı (Python Event sistemi)
typedef struct HsEvent
{
    HsEventType     type;
    HsEventSeverity severity;
    HsTimestampMs   timestamp;
    char            message[128]; // Kısa açıklama
} HsEvent;

// -----------------------------------------------------------------------------
// Sensör soyutlama katmanı (Python BaseSensor → C struct + vtable)
// -----------------------------------------------------------------------------

// İleri bildirim
struct HsSensor;

// Sensörün sanal fonksiyon tablosu (vtable)
// Python'daki BaseSensor metodlarının C karşılığı
typedef struct HsSensorVTable
{
    // Sensör ömrü yönetimi
    HsResult (*init)(struct HsSensor* self);
    HsResult (*start)(struct HsSensor* self);
    HsResult (*stop)(struct HsSensor* self);

    // Her tick çağrılacak update (dt saniye cinsinden)
    HsResult (*update)(struct HsSensor* self, double dt);

    // Okunabilir örnek üretme
    HsResult (*read_sample)(struct HsSensor* self, HsSample* out_sample);

    // Sensör için health bilgisi
    HsResult (*get_health)(struct HsSensor* self, HsHealth* out_health);
} HsSensorVTable;

// Sensör nesnesi (BaseSensor'ın C karşılığı)
typedef struct HsSensor
{
    const char*           name;    // Sensör adı (örn. "imu0", "gps0")
    HsSensorType          type;    // Sensör tipi
    const HsSensorVTable* vtable;  // Sanal fonksiyon tablosu
    void*                 impl;    // Sensöre özel verilerin tutulduğu alan
} HsSensor;

// -----------------------------------------------------------------------------
// Plugin / çekirdek API'si
// Bu fonksiyonlar C# tarafından çağrılacak ve C tarafındaki sensör dünyasını yönetir.
// -----------------------------------------------------------------------------

// Çekirdeği başlat (plugin kayıtlarını vs. sıfırlar)
HS_API void hs_init(void);

// Çekirdeği kapat (kaynakları serbest bırakmak için)
HS_API void hs_shutdown(void);

// Yeni bir sensör kaydet (Python'daki SensorManager.register gibi düşünebilirsin)
// Not: HsSensor nesnesinin ömrü çağıran tarafa aittir, çekirdek sadece pointer saklar.
HS_API HsResult hs_register_sensor(HsSensor* sensor);

// Her control tick'inde çağrılacak fonksiyon
//  - dt_seconds  : C# fiziğinin tick süresi
//  - state_input : C# tarafından hesaplanan anlık state (fizik sonucu, "prior")
//  - cmd_throttle / cmd_rudder : karar modülünün ürettiği komutlar
//    (şimdilik sadece bilgi amaçlı; ileride SLAM / odometry için de kullanılabilir)
HS_API void hs_tick(
    double dt_seconds,
    const HsFusedState* state_input,
    double cmd_throttle,
    double cmd_rudder
);

// Son hesaplanan fused state'i döndürür
//  - out_state NULL ise hiçbir şey yapmaz
//  - has_fix alanı 0 ise fused state henüz güvenilir değildir
HS_API void hs_get_fused_state(HsFusedState* out_state);

// Event halkasından bir event çeker (varsa)
//  - Eğer event yoksa type = HS_EVENT_NONE döner
HS_API void hs_pop_event(HsEvent* out_event);

// Genel health snapshot (tüm sensörlerin birleşik health'i)
HS_API void hs_get_health(HsHealth* out_health);

#ifdef __cplusplus
}
#endif
