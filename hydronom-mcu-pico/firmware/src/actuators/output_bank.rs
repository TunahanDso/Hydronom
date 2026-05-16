// Hydronom MCU - Output Bank
//
// MİMARİ NOT:
// OutputBank, AppState içindeki actuator runtime durumunu fiziksel çıkış komutlarına çevirir.
// Burada hâlâ donanım sürülmez; sadece uygulanacak çıkış listesi hazırlanır.
//
// Bu ayrım önemli:
// AppState       -> güvenlik ve komut doğrulama
// OutputBank     -> actuator profiline göre çıkış komutu üretimi
// PwmEscDriver   -> gerçek PWM donanımı
//
// Böylece ileride aynı state hem PWM ESC'ye hem de servo/CAN/DShot sürücüsüne bağlanabilir.

use crate::actuators::actuator::{ActuatorOutputCommand, ActuatorOutputMode};
use crate::app::state::AppState;
use crate::config::actuator_profile::MAX_ACTUATORS;

#[derive(Clone, Copy, Debug)]
pub struct OutputBank {
    pub outputs: [ActuatorOutputCommand; MAX_ACTUATORS],
    pub output_count: usize,
}

impl OutputBank {
    pub const fn new() -> Self {
        Self {
            outputs: [
                ActuatorOutputCommand::disabled(0),
                ActuatorOutputCommand::disabled(1),
                ActuatorOutputCommand::disabled(2),
                ActuatorOutputCommand::disabled(3),
                ActuatorOutputCommand::disabled(4),
                ActuatorOutputCommand::disabled(5),
                ActuatorOutputCommand::disabled(6),
                ActuatorOutputCommand::disabled(7),
            ],
            output_count: 0,
        }
    }

    pub fn refresh_from_state(&mut self, state: &AppState) {
        self.output_count = 0;

        for index in 0..MAX_ACTUATORS {
            let Some(profile) = state.vehicle_profile.actuator_bank.get(index as u8) else {
                self.outputs[index] = ActuatorOutputCommand::disabled(index as u8);
                continue;
            };

            if !profile.enabled {
                self.outputs[index] = ActuatorOutputCommand::disabled(profile.id);
                continue;
            }

            let runtime = state.actuators[index];

            let mode = if state.arming.is_armed() && !state.failsafe.active() {
                ActuatorOutputMode::Active
            } else {
                ActuatorOutputMode::Safe
            };

            self.outputs[index] = match mode {
                ActuatorOutputMode::Active => ActuatorOutputCommand::active(
                    profile.id,
                    profile.kind,
                    profile.gpio_pin,
                    runtime.requested,
                    runtime.limited,
                    runtime.pwm_us,
                ),

                ActuatorOutputMode::Safe => ActuatorOutputCommand::safe(
                    profile.id,
                    profile.kind,
                    profile.gpio_pin,
                    profile.pwm.safe_us,
                ),

                ActuatorOutputMode::Disabled => ActuatorOutputCommand::disabled(profile.id),
            };

            self.output_count += 1;
        }
    }

    pub fn first(&self) -> ActuatorOutputCommand {
        self.outputs[0]
    }
}

impl Default for OutputBank {
    fn default() -> Self {
        Self::new()
    }
}