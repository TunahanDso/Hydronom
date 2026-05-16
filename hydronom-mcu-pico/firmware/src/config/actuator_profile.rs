// Hydronom MCU - Actuator Profile
//
// MİMARİ NOT:
// Bu dosya Pico üzerindeki fiziksel çıkışların "ne olduğunu" tarif eder.
// Burada araç tipi değil, actuator davranışı tanımlanır.
//
// Örnekler:
// - Tek yönlü ESC
// - Çift yönlü ESC
// - Servo
// - Röle / dijital çıkış
//
// Pico karar vermez; sadece bu profillere göre gelen komutu güvenli çıkışa çevirir.

#[allow(dead_code)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ActuatorKind {
    PwmEsc,
    Servo,
    Digital,
}

#[allow(dead_code)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ReversePolicy {
    // Negatif komut güvenli şekilde 0'a kırpılır.
    ClampToZero,

    // Negatif komut desteklenir.
    AllowReverse,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct PwmRange {
    pub min_us: u16,
    pub neutral_us: u16,
    pub max_us: u16,
    pub safe_us: u16,
}

#[allow(dead_code)]
impl PwmRange {
    pub const fn one_way_esc() -> Self {
        Self {
            min_us: 1000,
            neutral_us: 1000,
            max_us: 2000,
            safe_us: 1000,
        }
    }

    pub const fn bidirectional_esc() -> Self {
        Self {
            min_us: 1000,
            neutral_us: 1500,
            max_us: 2000,
            safe_us: 1500,
        }
    }

    pub const fn standard_servo() -> Self {
        Self {
            min_us: 1000,
            neutral_us: 1500,
            max_us: 2000,
            safe_us: 1500,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ActuatorProfile {
    pub id: u8,
    pub kind: ActuatorKind,
    pub gpio_pin: u8,
    pub reverse_policy: ReversePolicy,
    pub pwm: PwmRange,
    pub enabled: bool,
}

#[allow(dead_code)]
impl ActuatorProfile {
    pub const fn one_way_esc(id: u8, gpio_pin: u8) -> Self {
        Self {
            id,
            kind: ActuatorKind::PwmEsc,
            gpio_pin,
            reverse_policy: ReversePolicy::ClampToZero,
            pwm: PwmRange::one_way_esc(),
            enabled: true,
        }
    }

    pub const fn bidirectional_esc(id: u8, gpio_pin: u8) -> Self {
        Self {
            id,
            kind: ActuatorKind::PwmEsc,
            gpio_pin,
            reverse_policy: ReversePolicy::AllowReverse,
            pwm: PwmRange::bidirectional_esc(),
            enabled: true,
        }
    }

    pub const fn servo(id: u8, gpio_pin: u8) -> Self {
        Self {
            id,
            kind: ActuatorKind::Servo,
            gpio_pin,
            reverse_policy: ReversePolicy::AllowReverse,
            pwm: PwmRange::standard_servo(),
            enabled: true,
        }
    }
}

pub const MAX_ACTUATORS: usize = 8;

#[derive(Clone, Copy, Debug)]
pub struct ActuatorBankProfile {
    pub actuators: [Option<ActuatorProfile>; MAX_ACTUATORS],
}

#[allow(dead_code)]
impl ActuatorBankProfile {
    pub const fn empty() -> Self {
        Self {
            actuators: [None; MAX_ACTUATORS],
        }
    }

    pub fn get(&self, id: u8) -> Option<ActuatorProfile> {
        let index = id as usize;

        if index >= MAX_ACTUATORS {
            return None;
        }

        self.actuators[index]
    }

    pub fn enabled_count(&self) -> usize {
        let mut count = 0;

        for actuator in self.actuators {
            if let Some(profile) = actuator {
                if profile.enabled {
                    count += 1;
                }
            }
        }

        count
    }
}