use hydronom_sensor_pico_common::health::node_health::NodeHealth;
use hydronom_sensor_pico_common::node::node_identity::NodeIdentity;
use hydronom_sensor_pico_common::protocol::capability::SensorCapability;

use crate::node::frame_builders::{
    build_capability_frame,
    build_health_frame,
    build_hello_frame,
    SensorFrameBytes,
};

// BNO085 node açıldığında Hydronom'a göndereceği başlangıç paketi.
// Gerçek Pico tarafında bu paketler USB-UART üzerinden sırayla yazılacak.
//
// Sıra:
// 1. HELLO
// 2. CAPABILITY
// 3. HEALTH

#[derive(Debug)]
pub struct BootFrameSequence {
    pub hello: SensorFrameBytes,
    pub capability: SensorFrameBytes,
    pub health: SensorFrameBytes,
    pub next_sequence: u32,
}

pub fn build_boot_sequence(
    identity: &NodeIdentity,
    capability: &SensorCapability,
    health: &NodeHealth,
    first_sequence: u32,
    boot_timestamp_us: u64,
) -> BootFrameSequence {
    let mut sequence = first_sequence;

    let hello = build_hello_frame(identity, sequence, boot_timestamp_us);
    sequence = sequence.wrapping_add(1);

    let capability_frame = build_capability_frame(
        identity,
        capability,
        sequence,
        boot_timestamp_us + 100,
    );
    sequence = sequence.wrapping_add(1);

    let health_frame = build_health_frame(
        identity,
        health,
        sequence,
        boot_timestamp_us + 200,
    );
    sequence = sequence.wrapping_add(1);

    BootFrameSequence {
        hello,
        capability: capability_frame,
        health: health_frame,
        next_sequence: sequence,
    }
}