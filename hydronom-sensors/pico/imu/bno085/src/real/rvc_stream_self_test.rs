use hydronom_sensor_pico_common::bno085::rvc_packet::BNO085_RVC_PACKET_LEN;
use hydronom_sensor_pico_common::bno085::rvc_stream_reader::{
    Bno085RvcStreamEvent,
    Bno085RvcStreamReader,
};
use hydronom_sensor_pico_common::bno085::rvc_to_imu::rvc_packet_to_imu_fusion_sample;

// Aynı datasheet örnek paketi.
// Önüne/arasına noise koyarak stream reader'ın senkron yakalamasını test ediyoruz.
const EXAMPLE_PACKET: [u8; BNO085_RVC_PACKET_LEN] = [
    0xAA, 0xAA,
    0xDE,
    0x01, 0x00,
    0x92, 0xFF,
    0x25, 0x08,
    0x8D, 0xFE,
    0xEC, 0xFF,
    0xD1, 0x03,
    0x00,
    0x00,
    0x00,
    0xE7,
];

pub fn run_bno085_rvc_stream_self_test() {
    let mut reader = Bno085RvcStreamReader::new();

    let mut stream = heapless::Vec::<u8, 96>::new();

    // Başta gürültü.
    push_bytes(&mut stream, &[0x00, 0x55, 0x12, 0xAA, 0x10]);

    // İlk sağlam paket.
    push_bytes(&mut stream, &EXAMPLE_PACKET);

    // Arada küçük gürültü.
    push_bytes(&mut stream, &[0x42, 0xAA, 0x01, 0x99]);

    // İkinci sağlam paket.
    push_bytes(&mut stream, &EXAMPLE_PACKET);

    let mut packet_count = 0u32;
    let mut error_count = 0u32;

    println!();
    println!("BNO085 UART-RVC STREAM SELF TEST:");

    for byte in stream.iter().copied() {
        match reader.push_byte(byte) {
            Bno085RvcStreamEvent::Waiting => {}

            Bno085RvcStreamEvent::Packet(packet) => {
                packet_count = packet_count.wrapping_add(1);

                let sample = rvc_packet_to_imu_fusion_sample(
                    &packet,
                    700_000 + packet_count as u64 * 20_000,
                );

                println!(
                    "[RVC_PACKET] count={} index={} yaw={:.2} pitch={:.2} roll={:.2} accel_z={:.3} quat_w={:.6} quality={}",
                    packet_count,
                    packet.index,
                    packet.yaw_deg,
                    packet.pitch_deg,
                    packet.roll_deg,
                    packet.accel_z_mps2,
                    sample.orientation.w,
                    sample.quality
                );
            }

            Bno085RvcStreamEvent::ParseError(error) => {
                error_count = error_count.wrapping_add(1);
                println!("[RVC_PARSE_ERROR] {:?}", error);
            }
        }
    }

    println!(
        "stream_stats: packets_ok={} parse_errors={} skipped_bytes={} resync_count={}",
        reader.packets_ok(),
        reader.parse_errors(),
        reader.skipped_bytes(),
        reader.resync_count()
    );

    assert_eq!(packet_count, 2);
    assert_eq!(error_count, 0);
    assert_eq!(reader.packets_ok(), 2);
    assert_eq!(reader.parse_errors(), 0);

    println!("[OK] BNO085 UART-RVC stream reader recovered 2 packets from noisy stream.");
}

fn push_bytes<const N: usize>(target: &mut heapless::Vec<u8, N>, bytes: &[u8]) {
    for byte in bytes {
        target
            .push(*byte)
            .expect("test stream capacity exceeded");
    }
}