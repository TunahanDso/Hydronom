// Hydronom MCU - Actuator Runtime Model
//
// MİMARİ NOT:
// Bu dosya fiziksel çıkıştan bağımsız actuator komut/çıkış modelini taşır.
// Burada PWM donanımı yoktur. Sadece "ne uygulanmalı?" sorusunun cevabı vardır.
//
// Bu sayede aynı üst model ileride şu çıkışlara bağlanabilir:
// - PWM ESC
// - Servo
// - Dijital çıkış
// - Röle
// - CAN ESC
// - DShot ESC

use crate::config::actuator_profile::ActuatorKind;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ActuatorOutputMode {
    Disabled,
    Safe,
    Active,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ActuatorOutputCommand {
    pub actuator_id: u8,
    pub kind: ActuatorKind,
    pub gpio_pin: u8,
    pub mode: ActuatorOutputMode,
    pub requested: i16,
    pub limited: i16,
    pub pwm_us: u16,
}

impl ActuatorOutputCommand {
    pub const fn disabled(actuator_id: u8) -> Self {
        Self {
            actuator_id,
            kind: ActuatorKind::PwmEsc,
            gpio_pin: 0,
            mode: ActuatorOutputMode::Disabled,
            requested: 0,
            limited: 0,
            pwm_us: 1000,
        }
    }

    pub const fn safe(actuator_id: u8, kind: ActuatorKind, gpio_pin: u8, pwm_us: u16) -> Self {
        Self {
            actuator_id,
            kind,
            gpio_pin,
            mode: ActuatorOutputMode::Safe,
            requested: 0,
            limited: 0,
            pwm_us,
        }
    }

    pub const fn active(
        actuator_id: u8,
        kind: ActuatorKind,
        gpio_pin: u8,
        requested: i16,
        limited: i16,
        pwm_us: u16,
    ) -> Self {
        Self {
            actuator_id,
            kind,
            gpio_pin,
            mode: ActuatorOutputMode::Active,
            requested,
            limited,
            pwm_us,
        }
    }
}