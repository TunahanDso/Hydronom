// Hydronom MCU - PWM ESC Driver
//
// MİMARİ NOT:
// Bu dosya PWM ESC sürüş katmanıdır.
// OutputBank tarafından hazırlanan güvenli actuator çıkışlarını gerçek PWM donanımına uygular.
//
// Şu an desteklenen fiziksel çıkışlar:
// - ACT0 / GP2 / PWM_SLICE1 Channel A
// - ACT1 / GP3 / PWM_SLICE1 Channel B
// - ACT2 / GP4 / PWM_SLICE2 Channel A
// - ACT3 / GP5 / PWM_SLICE2 Channel B
//
// GÜVENLİK:
// Bu katman DISARM / failsafe mantığını kendisi üretmez.
// O iş AppState + OutputBank tarafından yapılır.
// Ama burası yine de PWM değerlerini 1000..2000 us arasında son kez kırpar.
//
// ÖNEMLİ:
// ESC sinyal standardı için 50 Hz PWM kullanılır.
// 20 ms periyot içinde:
// - 1000 us: stop / minimum
// - 2000 us: maksimum
//
// YARIŞMA NOTU:
// Şu an ilk gerçek sürüm tek yönlü ESC güvenliği için 1000 us safe değerini temel alır.
// 1500 us nötr davranışı çift yönlü ESC / servo aşamasında ayrı profil olarak eklenecek.

use crate::actuators::actuator::{ActuatorOutputCommand, ActuatorOutputMode};
use crate::actuators::output_bank::OutputBank;
use crate::config::actuator_profile::ActuatorKind;
use embassy_rp::pwm::{Config as PwmConfig, Pwm};
use fixed::traits::ToFixed;

pub const ESC_PWM_MIN_US: u16 = 1000;
pub const ESC_PWM_MAX_US: u16 = 2000;

// 50 Hz ESC PWM için 20 ms periyot.
// 1 tick yaklaşık 1 us olacak şekilde top=19999 kullanıyoruz.
pub const ESC_PWM_TOP: u16 = 19_999;

// Varsayılan RP clock varsayımıyla 50 Hz üretmek için kullanılır.
// Gerekirse gerçek osiloskop/logic analyzer ölçümüne göre kalibre edilecek.
pub const ESC_PWM_CLOCK_DIVIDER: u32 = 125;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PwmEscApplyResult {
    Applied,
    IgnoredUnsupportedKind,
    Disabled,
    Clamped,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct PwmEscAppliedOutput {
    pub actuator_id: u8,
    pub gpio_pin: u8,
    pub pwm_us: u16,
    pub result: PwmEscApplyResult,
}

impl PwmEscAppliedOutput {
    pub const fn disabled(actuator_id: u8, gpio_pin: u8) -> Self {
        Self {
            actuator_id,
            gpio_pin,
            pwm_us: ESC_PWM_MIN_US,
            result: PwmEscApplyResult::Disabled,
        }
    }
}

pub fn prepare_pwm_esc_output(command: ActuatorOutputCommand) -> PwmEscAppliedOutput {
    if command.kind != ActuatorKind::PwmEsc {
        return PwmEscAppliedOutput {
            actuator_id: command.actuator_id,
            gpio_pin: command.gpio_pin,
            pwm_us: command.pwm_us,
            result: PwmEscApplyResult::IgnoredUnsupportedKind,
        };
    }

    if command.mode == ActuatorOutputMode::Disabled {
        return PwmEscAppliedOutput::disabled(command.actuator_id, command.gpio_pin);
    }

    let mut pwm_us = command.pwm_us;
    let mut clamped = false;

    if pwm_us < ESC_PWM_MIN_US {
        pwm_us = ESC_PWM_MIN_US;
        clamped = true;
    }

    if pwm_us > ESC_PWM_MAX_US {
        pwm_us = ESC_PWM_MAX_US;
        clamped = true;
    }

    PwmEscAppliedOutput {
        actuator_id: command.actuator_id,
        gpio_pin: command.gpio_pin,
        pwm_us,
        result: if clamped {
            PwmEscApplyResult::Clamped
        } else {
            PwmEscApplyResult::Applied
        },
    }
}

pub fn esc_pwm_config(compare_a_us: u16, compare_b_us: u16) -> PwmConfig {
    let mut config = PwmConfig::default();

    config.top = ESC_PWM_TOP;
    config.compare_a = clamp_pwm_us(compare_a_us);
    config.compare_b = clamp_pwm_us(compare_b_us);
    config.divider = ESC_PWM_CLOCK_DIVIDER.to_fixed();
    config.enable = true;

    config
}

pub fn clamp_pwm_us(value: u16) -> u16 {
    if value < ESC_PWM_MIN_US {
        return ESC_PWM_MIN_US;
    }

    if value > ESC_PWM_MAX_US {
        return ESC_PWM_MAX_US;
    }

    value
}

pub struct PwmEscHardware4<'d> {
    pwm01: Pwm<'d>,
    pwm23: Pwm<'d>,
    last_pwm_us: [u16; 4],
}

impl<'d> PwmEscHardware4<'d> {
    pub fn new(mut pwm01: Pwm<'d>, mut pwm23: Pwm<'d>) -> Self {
        let safe_config = esc_pwm_config(ESC_PWM_MIN_US, ESC_PWM_MIN_US);

        pwm01.set_config(&safe_config);
        pwm23.set_config(&safe_config);

        Self {
            pwm01,
            pwm23,
            last_pwm_us: [
                ESC_PWM_MIN_US,
                ESC_PWM_MIN_US,
                ESC_PWM_MIN_US,
                ESC_PWM_MIN_US,
            ],
        }
    }

    pub fn apply_output_bank(&mut self, output_bank: &OutputBank) {
        let mut desired_pwm = [
            ESC_PWM_MIN_US,
            ESC_PWM_MIN_US,
            ESC_PWM_MIN_US,
            ESC_PWM_MIN_US,
        ];

        for output in output_bank.outputs {
            let prepared = prepare_pwm_esc_output(output);

            match prepared.gpio_pin {
                2 => desired_pwm[0] = prepared.pwm_us,
                3 => desired_pwm[1] = prepared.pwm_us,
                4 => desired_pwm[2] = prepared.pwm_us,
                5 => desired_pwm[3] = prepared.pwm_us,
                _ => {}
            }
        }

        self.apply_pwm_us(desired_pwm);
    }

    pub fn apply_safe(&mut self) {
        self.apply_pwm_us([
            ESC_PWM_MIN_US,
            ESC_PWM_MIN_US,
            ESC_PWM_MIN_US,
            ESC_PWM_MIN_US,
        ]);
    }

    pub fn last_pwm_us(&self) -> [u16; 4] {
        self.last_pwm_us
    }

    fn apply_pwm_us(&mut self, pwm_us: [u16; 4]) {
        let m0 = clamp_pwm_us(pwm_us[0]);
        let m1 = clamp_pwm_us(pwm_us[1]);
        let m2 = clamp_pwm_us(pwm_us[2]);
        let m3 = clamp_pwm_us(pwm_us[3]);

        self.pwm01.set_config(&esc_pwm_config(m0, m1));
        self.pwm23.set_config(&esc_pwm_config(m2, m3));

        self.last_pwm_us = [m0, m1, m2, m3];
    }
}