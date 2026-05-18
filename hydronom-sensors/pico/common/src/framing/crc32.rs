// Küçük ve dependency gerektirmeyen CRC32 implementasyonu.
// Polynomial: 0xEDB88320
// Init: 0xFFFF_FFFF
// Final XOR: 0xFFFF_FFFF

pub fn crc32(bytes: &[u8]) -> u32 {
    let mut crc: u32 = 0xFFFF_FFFF;

    for &byte in bytes {
        crc ^= byte as u32;

        let mut i = 0;
        while i < 8 {
            let mask = if (crc & 1) != 0 { 0xEDB8_8320 } else { 0 };
            crc = (crc >> 1) ^ mask;
            i += 1;
        }
    }

    !crc
}