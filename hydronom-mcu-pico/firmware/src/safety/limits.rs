// Hydronom MCU - Limits
//
// GÜVENLİK:
// Bu dosya gelen actuator komutlarını fiziksel çıkış profiline göre kırpar.
// Tek yönlü ESC'ye negatif komut gönderilmez.
// PWM değerleri profil sınırları dışına çıkamaz.

use crate::config::actuator_profile::{ActuatorProfile, ReversePolicy};
use crate::config::defaults::{COMMAND_MAX, COMMAND_MIN};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct LimitedCommand {
    pub requested: i16,
    pub limited: i16,
    pub reverse_clamped: bool,
    pub range_clamped: bool,
}

pub fn limit_normalized_command(profile: ActuatorProfile, requested: i16) -> LimitedCommand {
    let mut value = requested;
    let mut reverse_clamped = false;
    let mut range_clamped = false;

    if value < COMMAND_MIN {
        value = COMMAND_MIN;
        range_clamped = true;
    }

    if value > COMMAND_MAX {
        value = COMMAND_MAX;
        range_clamped = true;
    }

    if profile.reverse_policy == ReversePolicy::ClampToZero && value < 0 {
        value = 0;
        reverse_clamped = true;
    }

    LimitedCommand {
        requested,
        limited: value,
        reverse_clamped,
        range_clamped,
    }
}

pub fn normalized_to_pwm_us(profile: ActuatorProfile, normalized: i16) -> u16 {
    let limited = limit_normalized_command(profile, normalized).limited;

    match profile.reverse_policy {
        ReversePolicy::ClampToZero => {
            // Tek yönlü ESC:
            // 0     -> min_us
            // 1000  -> max_us
            let span = profile.pwm.max_us.saturating_sub(profile.pwm.min_us) as i32;
            let value = limited.max(0) as i32;
            let pwm = profile.pwm.min_us as i32 + ((span * value) / 1000);
            pwm.clamp(profile.pwm.min_us as i32, profile.pwm.max_us as i32) as u16
        }

        ReversePolicy::AllowReverse => {
            // Çift yönlü ESC / servo:
            // -1000 -> min_us
            // 0     -> neutral_us
            // 1000  -> max_us
            if limited >= 0 {
                let span = profile.pwm.max_us.saturating_sub(profile.pwm.neutral_us) as i32;
                let pwm = profile.pwm.neutral_us as i32 + ((span * limited as i32) / 1000);
                pwm.clamp(profile.pwm.min_us as i32, profile.pwm.max_us as i32) as u16
            } else {
                let span = profile.pwm.neutral_us.saturating_sub(profile.pwm.min_us) as i32;
                let pwm = profile.pwm.neutral_us as i32 + ((span * limited as i32) / 1000);
                pwm.clamp(profile.pwm.min_us as i32, profile.pwm.max_us as i32) as u16
            }
        }
    }
}