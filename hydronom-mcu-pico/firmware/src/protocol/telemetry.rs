// Hydronom MCU - Telemetry
//
// Pico üst sisteme sadece "çalışıyorum" demez.
// Güvenlik ve actuator durumunu açıkça raporlar.
//
// Bu veri ileride C# Runtime / Hydronom Ops / Gateway tarafında okunabilir.
//
// MİMARİ NOT:
// Telemetry modeli platform bağımsız tutulur.
// Yani burada "motor1, motor2" gibi sabit isimler yerine actuator dizisi kullanılır.
// Böylece tekne, su altı aracı, roket, VTOL veya kara aracı aynı telemetri modelini paylaşabilir.

use crate::config::actuator_profile::MAX_ACTUATORS;
use crate::safety::arming::ArmState;
use crate::safety::failsafe::FailsafeReason;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ActuatorTelemetry {
    pub actuator_id: u8,
    pub requested: i16,
    pub limited: i16,
    pub pwm_us: u16,
    pub reverse_clamped: bool,
    pub range_clamped: bool,
}

pub const EMPTY_ACTUATOR_TELEMETRY: ActuatorTelemetry = ActuatorTelemetry {
    actuator_id: 0,
    requested: 0,
    limited: 0,
    pwm_us: 1000,
    reverse_clamped: false,
    range_clamped: false,
};

#[derive(Clone, Copy, Debug)]
pub struct McuTelemetry {
    pub firmware_uptime_ms: u64,
    pub last_seq: u32,
    pub arm_state: ArmState,
    pub failsafe_active: bool,
    pub failsafe_reason: FailsafeReason,
    pub actuator_count: usize,
    pub actuators: [ActuatorTelemetry; MAX_ACTUATORS],
}