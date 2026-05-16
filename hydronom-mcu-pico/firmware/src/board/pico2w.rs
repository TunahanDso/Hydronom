// Hydronom MCU - Raspberry Pi Pico 2W Board Profile
//
// MİMARİ NOT:
// Bu dosya Pico 2W kartının board-level kimliğini taşır.
// Donanım özel bilgisi burada tutulur; Hydronom'un üst actuator/safety/protocol
// katmanları mümkün olduğunca board bağımsız kalır.

use crate::board::pins::DEFAULT_ACTUATOR_GPIO_PINS;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum BoardKind {
    RaspberryPiPico2W,
}

#[derive(Clone, Copy, Debug)]
pub struct BoardProfile {
    pub kind: BoardKind,
    pub actuator_gpio_pins: [u8; 8],
    pub usb_cdc_enabled: bool,
    pub default_pwm_frequency_hz: u32,
}

impl BoardProfile {
    pub const fn pico2w_default() -> Self {
        Self {
            kind: BoardKind::RaspberryPiPico2W,
            actuator_gpio_pins: DEFAULT_ACTUATOR_GPIO_PINS,
            usb_cdc_enabled: true,
            default_pwm_frequency_hz: 50,
        }
    }

    pub fn actuator_pin(&self, actuator_id: u8) -> Option<u8> {
        let index = actuator_id as usize;

        if index >= self.actuator_gpio_pins.len() {
            return None;
        }

        Some(self.actuator_gpio_pins[index])
    }

    pub fn actuator_pin_count(&self) -> usize {
        self.actuator_gpio_pins.len()
    }
}

pub const DEFAULT_BOARD_PROFILE: BoardProfile = BoardProfile::pico2w_default();