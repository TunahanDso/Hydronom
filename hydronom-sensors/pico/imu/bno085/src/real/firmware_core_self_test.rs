use hydronom_sensor_pico_common::bno085::rvc_packet::BNO085_RVC_PACKET_LEN;
use hydronom_sensor_pico_common::framing::frame_decoder::decode_sensor_frame;
use hydronom_sensor_pico_common::protocol::frame_type::FrameType;

use crate::real::firmware_core::{
    Bno085FirmwareConfig,
    Bno085FirmwareCore,
    Bno085FirmwareEvent,
    HydronomFrameSink,
};

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

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum SinkTestError {
    ForcedFailure,
}

#[derive(Debug)]
struct CountingFrameSink {
    frame_count: u32,
    boot_hello_count: u32,
    capability_count: u32,
    health_count: u32,
    sample_count: u32,
    total_bytes: u32,
    last_sequence: u32,

    fail_next_write: bool,
}

impl CountingFrameSink {
    const fn new() -> Self {
        Self {
            frame_count: 0,
            boot_hello_count: 0,
            capability_count: 0,
            health_count: 0,
            sample_count: 0,
            total_bytes: 0,
            last_sequence: 0,

            fail_next_write: false,
        }
    }

    fn fail_next_write(&mut self) {
        self.fail_next_write = true;
    }
}

impl HydronomFrameSink for CountingFrameSink {
    type Error = SinkTestError;

    fn write_frame(&mut self, frame: &[u8]) -> Result<(), Self::Error> {
        if self.fail_next_write {
            self.fail_next_write = false;
            return Err(SinkTestError::ForcedFailure);
        }

        let decoded = decode_sensor_frame(frame)
            .expect("CountingFrameSink received invalid Hydronom frame");

        self.frame_count = self.frame_count.wrapping_add(1);
        self.total_bytes = self.total_bytes.wrapping_add(frame.len() as u32);
        self.last_sequence = decoded.header.sequence;

        match decoded.header.frame_type {
            FrameType::Hello => {
                self.boot_hello_count = self.boot_hello_count.wrapping_add(1);
            }
            FrameType::Capability => {
                self.capability_count = self.capability_count.wrapping_add(1);
            }
            FrameType::Health => {
                self.health_count = self.health_count.wrapping_add(1);
            }
            FrameType::Sample => {
                self.sample_count = self.sample_count.wrapping_add(1);
            }
            FrameType::Error | FrameType::Unknown => {}
        }

        Ok(())
    }
}

pub fn run_bno085_firmware_core_self_test() {
    println!();
    println!("BNO085 FIRMWARE CORE SELF TEST:");

    let config = Bno085FirmwareConfig::default_1hz_health();
    let mut core = Bno085FirmwareCore::new(config);
    let mut sink = CountingFrameSink::new();

    let boot_event = core
        .emit_boot_frames(
            10,
            1_000,
            &mut sink,
        )
        .expect("boot frame send failed");

    match boot_event {
        Bno085FirmwareEvent::BootFramesSent {
            count,
            next_sequence,
        } => {
            assert_eq!(count, 3);
            assert_eq!(next_sequence, 4);
        }
        _ => panic!("unexpected boot event: {:?}", boot_event),
    }

    assert!(core.boot_sent());
    assert_eq!(sink.frame_count, 3);
    assert_eq!(sink.boot_hello_count, 1);
    assert_eq!(sink.capability_count, 1);
    assert_eq!(sink.health_count, 1);
    assert_eq!(sink.sample_count, 0);

    let mut sample_event_seen = false;

    for (i, byte) in EXAMPLE_PACKET.iter().copied().enumerate() {
        let timestamp_us = 500_000 + i as u64 * 100;

        let event = core
            .push_bno085_uart_byte(
                20,
                timestamp_us,
                byte,
                &mut sink,
            )
            .expect("uart byte processing failed");

        if let Bno085FirmwareEvent::SampleFrameSent {
            sequence,
            timestamp_us,
        } = event
        {
            sample_event_seen = true;
            assert_eq!(sequence, 4);
            assert_eq!(timestamp_us, 500_000);
        }
    }

    assert!(sample_event_seen);
    assert_eq!(core.sample_count(), 1);
    assert_eq!(core.parse_error_count(), 0);
    assert_eq!(sink.sample_count, 1);
    assert_eq!(sink.frame_count, 4);

    // Zaman geriye saparsa health hemen tetiklenmemeli.
    let rollback_health_event = core
        .emit_due_health(
            500,
            500,
            &mut sink,
        )
        .expect("rollback health tick failed");

    assert_eq!(rollback_health_event, Bno085FirmwareEvent::Waiting);
    assert_eq!(sink.health_count, 1);

    let early_health_event = core
        .emit_due_health(
            500,
            500_500,
            &mut sink,
        )
        .expect("early health tick failed");

    assert_eq!(early_health_event, Bno085FirmwareEvent::Waiting);
    assert_eq!(sink.health_count, 1);

    let health_event = core
        .emit_due_health(
            1_100,
            1_001_000,
            &mut sink,
        )
        .expect("due health tick failed");

    match health_event {
        Bno085FirmwareEvent::HealthFrameSent {
            timestamp_us,
        } => {
            assert_eq!(timestamp_us, 1_001_000);
        }
        _ => panic!("unexpected health event: {:?}", health_event),
    }

    assert_eq!(sink.health_count, 2);
    assert_eq!(sink.frame_count, 5);

    // Sink write error test:
    //
    // Health zamanı geldiğinde write başarısız olsa bile scheduler zamanı ilerlemeli.
    // Fonksiyon Result::Err dönmemeli; HealthFrameDropped event'i dönmeli.
    // Böylece firmware loop health frame yüzünden kesintiye uğramaz.
    sink.fail_next_write();

    let failed_health = core
        .emit_due_health(
            2_200,
            2_001_000,
            &mut sink,
        )
        .expect("health drop should not return Result::Err");

    match failed_health {
        Bno085FirmwareEvent::HealthFrameDropped {
            timestamp_us,
        } => {
            assert_eq!(timestamp_us, 2_001_000);
        }
        _ => panic!("unexpected failed health event: {:?}", failed_health),
    }

    let stats_after_failure = core.stats();
    assert_eq!(stats_after_failure.sink_write_errors, 1);
    assert_eq!(stats_after_failure.health_frames_dropped, 1);
    assert_eq!(stats_after_failure.last_health_schedule_timestamp_us, 2_001_000);

    let retry_too_early = core
        .emit_due_health(
            2_201,
            2_001_100,
            &mut sink,
        )
        .expect("post-failure early health tick failed");

    assert_eq!(retry_too_early, Bno085FirmwareEvent::Waiting);
    assert_eq!(sink.health_count, 2);

    let next_valid_health = core
        .emit_due_health(
            3_300,
            3_001_000,
            &mut sink,
        )
        .expect("post-failure next health tick failed");

    match next_valid_health {
        Bno085FirmwareEvent::HealthFrameSent {
            timestamp_us,
        } => {
            assert_eq!(timestamp_us, 3_001_000);
        }
        _ => panic!("unexpected post-failure health event: {:?}", next_valid_health),
    }

    assert_eq!(sink.health_count, 3);

    let stats = core.stats();

    assert_eq!(stats.boot_frames_sent, 3);
    assert_eq!(stats.sample_frames_sent, 1);
    assert_eq!(stats.health_frames_sent, 3);
    assert_eq!(stats.health_frames_dropped, 1);
    assert_eq!(stats.total_frames_sent, 6);
    assert_eq!(stats.bytes_received_from_bno085, 19);
    assert_eq!(stats.parse_errors, 0);
    assert_eq!(stats.sink_write_errors, 1);
    assert_eq!(stats.last_sample_timestamp_us, 500_000);
    assert_eq!(stats.last_health_timestamp_us, 3_001_000);
    assert_eq!(stats.last_health_schedule_timestamp_us, 3_001_000);

    println!(
        "firmware_core_stats: boot={} sample={} health={} health_dropped={} total={} rx_bytes={} sink_errors={} last_seq={} total_bytes={}",
        stats.boot_frames_sent,
        stats.sample_frames_sent,
        stats.health_frames_sent,
        stats.health_frames_dropped,
        stats.total_frames_sent,
        stats.bytes_received_from_bno085,
        stats.sink_write_errors,
        sink.last_sequence,
        sink.total_bytes
    );

    println!("[OK] BNO085 firmware core emitted boot/sample/health frames through sink trait.");
}