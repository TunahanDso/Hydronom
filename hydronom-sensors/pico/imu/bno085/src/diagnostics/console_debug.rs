use hydronom_sensor_pico_common::framing::frame_decoder::decode_sensor_frame;
use hydronom_sensor_pico_common::health::node_health::NodeHealth;
use hydronom_sensor_pico_common::imu::imu_payload_decoder::decode_imu_fusion_payload;
use hydronom_sensor_pico_common::node::node_identity::NodeIdentity;
use hydronom_sensor_pico_common::protocol::capability::SensorCapability;

use crate::runtime::stream_runtime::Bno085StreamEvent;
use crate::runtime::stream_stats::Bno085StreamStats;

pub fn print_node_summary(
    identity: &NodeIdentity,
    capability: &SensorCapability,
    health: &NodeHealth,
) {
    println!("HYDRONOM SENSOR PICO IMU BNO085 STREAM MOCK");
    println!("node_id: {}", identity.node_id);
    println!("node_name: {}", identity.node_name);
    println!("firmware: {} {}", identity.firmware_name, identity.firmware_version);
    println!("sensor_model: {}", identity.sensor_model);
    println!("sensor_vendor: {}", identity.sensor_vendor);

    println!("capability_bits: 0x{:08X}", capability.capability_bits);
    println!("recommended_rate_hz: {}", capability.recommended_rate_hz);
    println!("max_rate_hz: {}", capability.max_rate_hz);
    println!("health_state: {:?}", health.state);
}

pub fn print_stream_event(index: usize, event: &Bno085StreamEvent) {
    match event {
        Bno085StreamEvent::BootHello {
            sequence,
            timestamp_us,
            frame,
        } => {
            println!(
                "[BOOT_HELLO] index={} seq={} ts={} len={}",
                index,
                sequence,
                timestamp_us,
                frame.len()
            );
        }

        Bno085StreamEvent::BootCapability {
            sequence,
            timestamp_us,
            frame,
        } => {
            println!(
                "[BOOT_CAPABILITY] index={} seq={} ts={} len={}",
                index,
                sequence,
                timestamp_us,
                frame.len()
            );
        }

        Bno085StreamEvent::BootHealth {
            sequence,
            timestamp_us,
            frame,
        } => {
            println!(
                "[BOOT_HEALTH] index={} seq={} ts={} len={}",
                index,
                sequence,
                timestamp_us,
                frame.len()
            );
        }

        Bno085StreamEvent::Sample {
            sequence,
            timestamp_us,
            frame,
        } => {
            print_sample_event(index, *sequence, *timestamp_us, frame);
        }

        Bno085StreamEvent::Health {
            sequence,
            timestamp_us,
            frame,
        } => {
            println!(
                "[HEALTH] index={} seq={} ts={} len={}",
                index,
                sequence,
                timestamp_us,
                frame.len()
            );
        }
    }
}

pub fn print_stream_stats(stats: &Bno085StreamStats) {
    println!();
    println!("STREAM STATS:");
    println!("boot_frame_count: {}", stats.boot_frame_count);
    println!("sample_frame_count: {}", stats.sample_frame_count);
    println!("health_frame_count: {}", stats.health_frame_count);
    println!("total_frame_count: {}", stats.total_frame_count);
    println!("first_timestamp_us: {}", stats.first_timestamp_us);
    println!("last_timestamp_us: {}", stats.last_timestamp_us);
    println!("duration_us: {}", stats.duration_us());
    println!("last_sequence: {}", stats.last_sequence);
}

fn print_sample_event(
    index: usize,
    sequence: u32,
    timestamp_us: u64,
    frame: &[u8],
) {
    let decoded = decode_sensor_frame(frame).expect("sample frame decode failed");
    let sample = decode_imu_fusion_payload(decoded.payload)
        .expect("sample payload decode failed");

    println!(
        "[SAMPLE] index={} seq={} ts={} len={} quat=({:.5},{:.5},{:.5},{:.5}) gyro_z={:.5} lin_ax={:.5} quality={}",
        index,
        sequence,
        timestamp_us,
        frame.len(),
        sample.orientation.w,
        sample.orientation.x,
        sample.orientation.y,
        sample.orientation.z,
        sample.gyro_radps.z,
        sample.linear_accel_mps2.x,
        sample.quality
    );
}