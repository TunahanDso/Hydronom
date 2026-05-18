use hydronom_sensor_pico_common::bno085::rvc_packet::BNO085_RVC_PACKET_LEN;
use hydronom_sensor_pico_common::framing::frame_decoder::decode_sensor_frame;
use hydronom_sensor_pico_common::health::node_health::NodeHealthState;
use hydronom_sensor_pico_common::imu::imu_payload_decoder::decode_imu_fusion_payload;
use hydronom_sensor_pico_common::protocol::frame_type::FrameType;
use hydronom_sensor_pico_common::protocol::payload_kind::PayloadKind;

use crate::real::real_bno085_rvc_node::{
    RealBno085RvcNode,
    RealBno085RvcNodeEvent,
};

// Datasheet örnek BNO085 UART-RVC paketi.
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

pub fn run_real_bno085_rvc_node_self_test() {
    println!();
    println!("REAL BNO085 RVC NODE SELF TEST:");

    let mut node = RealBno085RvcNode::new();

    let boot = node.build_boot_frames(
        10,
        1_000,
    );

    assert_eq!(boot.hello.len(), 222);
    assert_eq!(boot.capability.len(), 34);
    assert_eq!(boot.health.len(), 45);
    assert_eq!(node.sequence(), 4);

    println!(
        "boot_frames: hello={} capability={} health={} next_sequence={}",
        boot.hello.len(),
        boot.capability.len(),
        boot.health.len(),
        node.sequence()
    );

    let mut produced_sample = false;
    let mut last_frame_len = 0usize;
    let mut sample_timestamp = 0u64;

    for (i, byte) in EXAMPLE_PACKET.iter().copied().enumerate() {
        let timestamp_us = 500_000 + i as u64 * 100;

        match node.push_uart_byte(timestamp_us, byte) {
            RealBno085RvcNodeEvent::Waiting => {}

            RealBno085RvcNodeEvent::SampleFrame {
                sequence,
                timestamp_us,
                sample,
                frame,
            } => {
                produced_sample = true;
                last_frame_len = frame.len();
                sample_timestamp = timestamp_us;

                println!(
                    "[REAL_SAMPLE_FRAME] seq={} ts={} len={} quat_w={:.6} accel_z={:.3} quality={}",
                    sequence,
                    timestamp_us,
                    frame.len(),
                    sample.orientation.w,
                    sample.accel_mps2.z,
                    sample.quality
                );

                let decoded = decode_sensor_frame(&frame)
                    .expect("real sample Hydronom frame decode failed");

                assert_eq!(decoded.header.frame_type, FrameType::Sample);
                assert_eq!(decoded.header.payload_kind, PayloadKind::ImuFusionQuaternion);
                assert_eq!(decoded.header.sequence, sequence);
                assert_eq!(decoded.header.timestamp_us, 500_000);

                let decoded_sample = decode_imu_fusion_payload(decoded.payload)
                    .expect("real sample payload decode failed");

                assert_eq!(decoded_sample.timestamp_us, 500_000);
                assert_close(decoded_sample.accel_mps2.z, 9.581, 0.01);
                assert_eq!(decoded_sample.quality, 220);
            }

            RealBno085RvcNodeEvent::ParseError {
                error,
                error_count,
            } => {
                panic!(
                    "unexpected parse error in real node self test: {:?} count={}",
                    error,
                    error_count
                );
            }
        }
    }

    assert!(produced_sample);
    assert_eq!(sample_timestamp, 500_000);
    assert_eq!(last_frame_len, 103);
    assert_eq!(node.sample_count(), 1);
    assert_eq!(node.parse_error_count(), 0);
    assert_eq!(node.health_error_count_in_window(), 0);
    assert_eq!(node.health_window_filled(), 1);
    assert_eq!(node.sequence(), 5);

    let health_frame = node.build_health_frame(
        20,
        600_000,
    );

    let decoded_health = decode_sensor_frame(&health_frame)
        .expect("real health frame decode failed");

    assert_eq!(decoded_health.header.frame_type, FrameType::Health);

    println!(
        "health_frame_len={} final_sequence={} sample_count={} parse_errors={} health_window_errors={}",
        health_frame.len(),
        node.sequence(),
        node.sample_count(),
        node.parse_error_count(),
        node.health_error_count_in_window()
    );

    run_health_window_recovery_self_test();

    println!("[OK] RealBno085RvcNode converted UART-RVC bytes into Hydronom SAMPLE frame.");
}

fn run_health_window_recovery_self_test() {
    let mut node = RealBno085RvcNode::new();

    // Eşik 4 hata. 5 hata ile Degraded olmalı.
    for i in 0..5 {
        node.record_bus_error(9000 + i);
    }

    assert_eq!(node.health_error_count_in_window(), 5);
    assert_eq!(node.health(1_000).state, NodeHealthState::Degraded);

    // 128 başarılı paket ile pencere tamamen sağlıklı örneklerle dolar.
    // Eski hatalar bitset ring buffer'dan çıkmalı.
    for i in 0..128 {
        feed_example_packet(
            &mut node,
            1_000_000 + i as u64 * 20_000,
        );
    }

    assert_eq!(node.health_window_filled(), 128);
    assert_eq!(node.health_error_count_in_window(), 0);
    assert_eq!(node.health(4_000).state, NodeHealthState::Healthy);

    println!(
        "health_window_recovery: filled={} errors={} state={:?}",
        node.health_window_filled(),
        node.health_error_count_in_window(),
        node.health(4_000).state
    );
}

fn feed_example_packet(node: &mut RealBno085RvcNode, start_timestamp_us: u64) {
    for (i, byte) in EXAMPLE_PACKET.iter().copied().enumerate() {
        let timestamp_us = start_timestamp_us + i as u64 * 100;

        match node.push_uart_byte(timestamp_us, byte) {
            RealBno085RvcNodeEvent::Waiting => {}

            RealBno085RvcNodeEvent::SampleFrame { .. } => {}

            RealBno085RvcNodeEvent::ParseError {
                error,
                error_count,
            } => {
                panic!(
                    "unexpected parse error while feeding health recovery packet: {:?} count={}",
                    error,
                    error_count
                );
            }
        }
    }
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