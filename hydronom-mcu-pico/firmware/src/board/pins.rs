// Hydronom MCU - Board Pin Map
//
// MİMARİ NOT:
// Pin bilgisi dağınık şekilde actuator koduna gömülmez.
// Board katmanı hangi GPIO'nun hangi varsayılan role sahip olduğunu bildirir.
//
// Varsayılan Pico 2W motor pinleri:
// ACT0 -> GP2
// ACT1 -> GP3
// ACT2 -> GP4
// ACT3 -> GP5
//
// İleride su altı / roket / VTOL profilleri bu pinleri farklı kullanabilir.

pub const DEFAULT_ACTUATOR_GPIO_PINS: [u8; 8] = [2, 3, 4, 5, 6, 7, 8, 9];

pub fn default_pin_for_actuator(actuator_id: u8) -> Option<u8> {
    let index = actuator_id as usize;

    if index >= DEFAULT_ACTUATOR_GPIO_PINS.len() {
        return None;
    }

    Some(DEFAULT_ACTUATOR_GPIO_PINS[index])
}

pub fn default_pin_sum() -> u32 {
    let mut sum = 0;
    let mut index = 0;

    while index < DEFAULT_ACTUATOR_GPIO_PINS.len() {
        sum += DEFAULT_ACTUATOR_GPIO_PINS[index] as u32;
        index += 1;
    }

    sum
}