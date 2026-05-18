use hydronom_sensor_pico_common::framing::frame_decoder::decode_sensor_frame;
use hydronom_sensor_pico_common::health::health_payload::decode_health_payload;
use hydronom_sensor_pico_common::imu::imu_payload_decoder::decode_imu_fusion_payload;
use hydronom_sensor_pico_common::node::node_payload::decode_node_hello_payload;
use hydronom_sensor_pico_common::protocol::capability_payload::decode_capability_payload;

pub fn verify_roundtrip(label: &str, frame: &[u8]) {
    match decode_sensor_frame(frame) {
        Ok(decoded) => {
            println!(
                "[OK] {} type={:?} sensor={:?} payload={:?} node=0x{:04X} seq={} ts={} payload_len={} crc=0x{:08X}",
                label,
                decoded.header.frame_type,
                decoded.header.sensor_kind,
                decoded.header.payload_kind,
                decoded.header.node_id,
                decoded.header.sequence,
                decoded.header.timestamp_us,
                decoded.header.payload_len,
                decoded.received_crc32
            );
        }
        Err(error) => {
            println!("[ERR] {} decode failed: {:?}", label, error);
        }
    }
}

pub fn decode_and_print_hello(frame: &[u8]) {
    let decoded = decode_sensor_frame(frame).expect("hello frame decode failed");
    let hello = decode_node_hello_payload(decoded.payload)
        .expect("hello payload decode failed");

    println!(
        "[HELLO] node=0x{:04X} kind={:?} role={:?} name={} firmware={} version={} model={} vendor={}",
        hello.node_id,
        hello.sensor_kind,
        hello.role,
        ascii_to_str(hello.node_name),
        ascii_to_str(hello.firmware_name),
        ascii_to_str(hello.firmware_version),
        ascii_to_str(hello.sensor_model),
        ascii_to_str(hello.sensor_vendor)
    );
}

pub fn decode_and_print_capability(frame: &[u8]) {
    let decoded = decode_sensor_frame(frame).expect("capability frame decode failed");
    let capability = decode_capability_payload(decoded.payload)
        .expect("capability payload decode failed");

    println!(
        "[CAPABILITY] bits=0x{:08X} recommended={}Hz max={}Hz",
        capability.capability_bits,
        capability.recommended_rate_hz,
        capability.max_rate_hz
    );
}

pub fn decode_and_print_health(frame: &[u8]) {
    let decoded = decode_sensor_frame(frame).expect("health frame decode failed");
    let health = decode_health_payload(decoded.payload)
        .expect("health payload decode failed");

    println!(
        "[HEALTH] state={:?} uptime={}ms samples={} readErrors={} busErrors={} lastError={}",
        health.state,
        health.uptime_ms,
        health.sample_count,
        health.read_error_count,
        health.bus_error_count,
        health.last_error_code
    );
}

pub fn decode_and_print_sample(frame: &[u8]) {
    let decoded = decode_sensor_frame(frame).expect("sample frame decode failed");
    let sample = decode_imu_fusion_payload(decoded.payload)
        .expect("imu fusion payload decode failed");

    println!(
        "[SAMPLE] ts={} quat=({}, {}, {}, {}) accel=({}, {}, {}) gyro=({}, {}, {}) mag=({}, {}, {}) quality={}",
        sample.timestamp_us,
        sample.orientation.w,
        sample.orientation.x,
        sample.orientation.y,
        sample.orientation.z,
        sample.accel_mps2.x,
        sample.accel_mps2.y,
        sample.accel_mps2.z,
        sample.gyro_radps.x,
        sample.gyro_radps.y,
        sample.gyro_radps.z,
        sample.mag_ut.x,
        sample.mag_ut.y,
        sample.mag_ut.z,
        sample.quality
    );
}

fn ascii_to_str(bytes: &[u8]) -> &str {
    core::str::from_utf8(bytes).unwrap_or("<invalid-ascii>")
}