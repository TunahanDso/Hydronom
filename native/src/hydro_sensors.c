// hydro_sensors.c

#include "../include/hydro_sensors.h"
#include <string.h>
#include <math.h>

// -----------------------------------------------------------------------------
// Dahili durumlar
// -----------------------------------------------------------------------------

// Kayıtlı sensörler
static HsSensor*      g_sensors[HS_MAX_SENSORS];
static int            g_sensor_count = 0;

// Event halkası
static HsEvent        g_events[HS_MAX_EVENTS];
static int            g_event_head = 0;  // yazma noktası
static int            g_event_tail = 0;  // okuma noktası

// Son fused state
static HsFusedState   g_fused_state;

// Genel health
static HsHealth       g_health;

// Çekirdek init edildi mi?
static int            g_inited = 0;

// -----------------------------------------------------------------------------
// Dahili yardımcılar
// -----------------------------------------------------------------------------

// Event halkasına event ekleme
static void hs_push_event_internal(const HsEvent* ev)
{
    if (!ev)
        return;

    g_events[g_event_head] = *ev;
    g_event_head = (g_event_head + 1) % HS_MAX_EVENTS;

    // Kuyruk tam dolarsa en eski event'i ezmiş oluruz (basit politika)
    if (g_event_head == g_event_tail)
    {
        g_event_tail = (g_event_tail + 1) % HS_MAX_EVENTS;
    }
}

// Fused state'i başlangıç değerlerine getir (versiyon + meta)
// Bu fonksiyon sadece g_fused_state üzerinde çalışır.
static void hs_reset_fused_state(void)
{
    memset(&g_fused_state, 0, sizeof(g_fused_state));
    g_fused_state.struct_size = (uint32_t)sizeof(HsFusedState);
    g_fused_state.version     = 1;
    g_fused_state.seq         = 0;
    g_fused_state.has_fix     = 0;
    g_fused_state.quality     = 0.0;
    g_fused_state.timestamp   = 0;
    // reserved alanlar zaten memset ile 0landı
}

// -----------------------------------------------------------------------------
// API implementasyonu
// -----------------------------------------------------------------------------

HS_API void hs_init(void)
{
    // Sensör listesi ve sayaç sıfırlanır
    g_sensor_count = 0;
    for (int i = 0; i < HS_MAX_SENSORS; ++i)
    {
        g_sensors[i] = NULL;
    }

    // Event halkası sıfırlanır
    g_event_head = 0;
    g_event_tail = 0;

    // Fused state varsayılan hale getirilir (versiyonlu)
    hs_reset_fused_state();

    // Health varsayılan hale getirilir
    memset(&g_health, 0, sizeof(g_health));
    g_health.status = HS_HEALTH_UNKNOWN;
    g_health.timestamp = 0;
    g_health.message[0] = '\0';

    g_inited = 1;
}

HS_API void hs_shutdown(void)
{
    // Şimdilik özel bir kaynak serbest bırakma yok.
    // İleride sensör portları, thread'ler vs. burada kapatılabilir.
    g_inited = 0;
}

HS_API HsResult hs_register_sensor(HsSensor* sensor)
{
    if (!g_inited)
        return HS_ERR_GENERIC;

    if (!sensor || !sensor->vtable)
        return HS_ERR_INVALID;

    if (g_sensor_count >= HS_MAX_SENSORS)
        return HS_ERR_GENERIC;

    g_sensors[g_sensor_count++] = sensor;
    return HS_OK;
}

HS_API void hs_tick(
    double dt_seconds,
    const HsFusedState* state_input,
    double cmd_throttle,
    double cmd_rudder
)
{
    (void)cmd_throttle;
    (void)cmd_rudder;

    if (!g_inited)
        return;

    // -------------------------------------------------------------------------
    // 1) C# fiziğinden gelen state_input'u, C çekirdeğinin fused state'ine yedir
    //    (şimdilik "prior" olarak birebir kopyalayıp meta bilgileri kendimiz yönetiyoruz)
    // -------------------------------------------------------------------------
    if (state_input)
    {
        // Pozisyon ve hız
        g_fused_state.pos_x = state_input->pos_x;
        g_fused_state.pos_y = state_input->pos_y;
        g_fused_state.pos_z = state_input->pos_z;

        g_fused_state.vel_x = state_input->vel_x;
        g_fused_state.vel_y = state_input->vel_y;
        g_fused_state.vel_z = state_input->vel_z;

        // Yönelim
        g_fused_state.yaw_deg   = state_input->yaw_deg;
        g_fused_state.pitch_deg = state_input->pitch_deg;
        g_fused_state.roll_deg  = state_input->roll_deg;

        // Zaman damgası
        g_fused_state.timestamp = state_input->timestamp;

        // Hız büyüklüğü (m/s)
        const double vx = state_input->vel_x;
        const double vy = state_input->vel_y;
        const double vz = state_input->vel_z;
        g_fused_state.speed_mps = sqrt(vx * vx + vy * vy + vz * vz);

        // Şimdilik basit kabul: C# fiziği state veriyorsa "fix var" ve kalite orta-yüksek
        g_fused_state.has_fix = 1;
        if (g_fused_state.quality < 0.5)
            g_fused_state.quality = 0.8;

        // Sıra numarasını arttır
        g_fused_state.seq += 1;
    }
    else
    {
        // State yoksa fix'i düşür, kaliteyi azalt
        g_fused_state.has_fix = 0;
        if (g_fused_state.quality > 0.1)
            g_fused_state.quality *= 0.9;
    }

    // -------------------------------------------------------------------------
    // 2) Tick başında global health'i resetle
    //    - Varsayılan: OK, sonra sensörlerden gelen en kötü durumu yansıtıyoruz.
    // -------------------------------------------------------------------------
    g_health.status = (g_sensor_count > 0) ? HS_HEALTH_OK : HS_HEALTH_UNKNOWN;
    g_health.timestamp = g_fused_state.timestamp;
    g_health.message[0] = '\0';

    // -------------------------------------------------------------------------
    // 3) Sensörleri gez ve update / health topla
    // -------------------------------------------------------------------------
    for (int i = 0; i < g_sensor_count; ++i)
    {
        HsSensor* s = g_sensors[i];
        if (!s || !s->vtable)
            continue;

        if (s->vtable->update)
        {
            (void)s->vtable->update(s, dt_seconds);
        }

        // Health bilgisini çekip global health'e yansıtmak için basit örnek
        if (s->vtable->get_health)
        {
            HsHealth h;
            if (s->vtable->get_health(s, &h) == HS_OK)
            {
                // Daha kötü durum = daha büyük status değeri (UNKNOWN < OK < WARN < ERROR)
                if (h.status > g_health.status)
                {
                    g_health = h;
                }
            }
        }

        // İleride:
        //  - s->vtable->read_sample ile Sample çekilip fusion/SLAM'e beslenebilir
        //  - Sensör hata durumlarında hs_push_event_internal ile event üretilebilir
    }

    // -------------------------------------------------------------------------
    // 4) İleride burada:
    //    - Multi-sensör fusion
    //    - SLAM / odometry backend'leri
    //    - Map / pose graph güncellemeleri
    //    yapılacak. Şu an sadece C# fiziğinin state'ini "fused" olarak kabul ediyoruz.
    // -------------------------------------------------------------------------
}

HS_API void hs_get_fused_state(HsFusedState* out_state)
{
    if (!out_state)
        return;

    if (!g_inited)
    {
        memset(out_state, 0, sizeof(*out_state));
        return;
    }

    *out_state = g_fused_state;
}

HS_API void hs_pop_event(HsEvent* out_event)
{
    if (!out_event)
        return;

    if (!g_inited)
    {
        memset(out_event, 0, sizeof(*out_event));
        out_event->type = HS_EVENT_NONE;
        return;
    }

    if (g_event_head == g_event_tail)
    {
        // Event yok
        memset(out_event, 0, sizeof(*out_event));
        out_event->type = HS_EVENT_NONE;
        return;
    }

    *out_event = g_events[g_event_tail];
    g_event_tail = (g_event_tail + 1) % HS_MAX_EVENTS;
}

HS_API void hs_get_health(HsHealth* out_health)
{
    if (!out_health)
        return;

    if (!g_inited)
    {
        memset(out_health, 0, sizeof(*out_health));
        out_health->status = HS_HEALTH_UNKNOWN;
        return;
    }

    *out_health = g_health;
}
