// Hydronom MCU - CRC
//
// MİMARİ NOT:
// İlk geliştirme aşamasında USB CDC üzerinden text/debug protokolü de kullanacağız.
// Ama asıl profesyonel protokol CRC kontrollü frame yapısına gidecek.
//
// Bu dosya no_std uyumlu basit CRC-16/CCITT-FALSE hesaplayıcıdır.
//
// Parametreler:
// - Polynomial: 0x1021
// - Initial:    0xFFFF
// - XOR Out:    0x0000
//
// Amaç:
// Hatalı, eksik veya bozulmuş komutların actuator katmanına ulaşmasını engellemek.

pub const CRC16_CCITT_FALSE_INIT: u16 = 0xFFFF;
pub const CRC16_CCITT_FALSE_POLY: u16 = 0x1021;

pub fn crc16_ccitt_false(bytes: &[u8]) -> u16 {
    let mut crc = CRC16_CCITT_FALSE_INIT;

    for byte in bytes {
        crc ^= (*byte as u16) << 8;

        for _ in 0..8 {
            let high_bit_set = (crc & 0x8000) != 0;
            crc <<= 1;

            if high_bit_set {
                crc ^= CRC16_CCITT_FALSE_POLY;
            }
        }
    }

    crc
}

pub fn verify_crc16_ccitt_false(bytes: &[u8], expected: u16) -> bool {
    crc16_ccitt_false(bytes) == expected
}