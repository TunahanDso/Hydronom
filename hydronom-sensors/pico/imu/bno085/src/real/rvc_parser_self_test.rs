use hydronom_sensor_pico_common::bno085::rvc_packet::{
    Bno085RvcPacket,
    BNO085_RVC_PACKET_LEN,
};
use hydronom_sensor_pico_common::bno085::rvc_to_imu::rvc_packet_to_imu_fusion_sample;

// Datasheet örneği:
// AA AA DE 01 00 92 FF 25 08 8D FE EC FF D1 03 00 00 00 E7
//
// Beklenen:
// index = 222
// yaw = 0.01 degree
// pitch = -1.10 degree
// roll = 20.85 degree
// accel x = -371 mg ~= -3.638 m/s^2
// accel y = -20 mg ~= -0.196 m/s^2
// accel z = 977 mg ~= 9.581 m/s^2

pub fn run_bno085_rvc_parser_self_test() {
    let packet_bytes: [u8; BNO085_RVC_PACKET_LEN] = [
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

    let packet = Bno085RvcPacket::parse(&packet_bytes)
        .expect("BNO085 RVC datasheet example parse failed");

    let sample = rvc_packet_to_imu_fusion_sample(&packet, 555_000);

    println!();
    println!("BNO085 UART-RVC PARSER SELF TEST:");
    println!("index: {}", packet.index);
    println!(
        "euler_deg: yaw={:.2} pitch={:.2} roll={:.2}",
        packet.yaw_deg,
        packet.pitch_deg,
        packet.roll_deg
    );
    println!(
        "accel_mps2: x={:.3} y={:.3} z={:.3}",
        packet.accel_x_mps2,
        packet.accel_y_mps2,
        packet.accel_z_mps2
    );
    println!("checksum: 0x{:02X}", packet.checksum);

    println!(
        "sample_quat: w={:.6} x={:.6} y={:.6} z={:.6}",
        sample.orientation.w,
        sample.orientation.x,
        sample.orientation.y,
        sample.orientation.z
    );

    println!("sample_quality: {}", sample.quality);

    assert_eq!(packet.index, 222);
    assert_close(packet.yaw_deg, 0.01, 0.001);
    assert_close(packet.pitch_deg, -1.10, 0.001);
    assert_close(packet.roll_deg, 20.85, 0.001);

    assert_close(packet.accel_x_mps2, -3.638, 0.01);
    assert_close(packet.accel_y_mps2, -0.196, 0.01);
    assert_close(packet.accel_z_mps2, 9.581, 0.01);

    println!("[OK] BNO085 UART-RVC parser decoded datasheet example.");
}

fn assert_close(actual: f32, expected: f32, tolerance: f32) {
    let diff = libm::fabsf(actual - expected);

    assert!(
        diff <= tolerance,
        "assert_close failed: actual={} expected={} diff={} tolerance={}",
        actual,
        expected,
        diff,
        tolerance
    );
}