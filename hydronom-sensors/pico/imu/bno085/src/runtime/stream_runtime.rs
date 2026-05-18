use crate::mock::mock_bno085::MockBno085;
use crate::node::boot_sequence::build_boot_sequence;
use crate::node::frame_builders::{
    build_health_frame,
    build_sample_frame,
    SensorFrameBytes,
};
use crate::runtime::stream_config::Bno085StreamConfig;
use crate::runtime::stream_stats::Bno085StreamStats;

#[derive(Debug)]
pub enum Bno085StreamEvent {
    BootHello {
        sequence: u32,
        timestamp_us: u64,
        frame: SensorFrameBytes,
    },
    BootCapability {
        sequence: u32,
        timestamp_us: u64,
        frame: SensorFrameBytes,
    },
    BootHealth {
        sequence: u32,
        timestamp_us: u64,
        frame: SensorFrameBytes,
    },
    Sample {
        sequence: u32,
        timestamp_us: u64,
        frame: SensorFrameBytes,
    },
    Health {
        sequence: u32,
        timestamp_us: u64,
        frame: SensorFrameBytes,
    },
}

impl Bno085StreamEvent {
    pub fn frame_bytes(&self) -> &[u8] {
        match self {
            Self::BootHello { frame, .. } => frame,
            Self::BootCapability { frame, .. } => frame,
            Self::BootHealth { frame, .. } => frame,
            Self::Sample { frame, .. } => frame,
            Self::Health { frame, .. } => frame,
        }
    }
}

pub struct Bno085StreamRuntime {
    sensor: MockBno085,
    config: Bno085StreamConfig,

    next_sequence: u32,
    next_sample_timestamp_us: u64,
    next_health_timestamp_us: u64,

    samples_emitted: u32,
    boot_emitted: bool,

    stats: Bno085StreamStats,
}

impl Bno085StreamRuntime {
    pub fn new(sensor: MockBno085, config: Bno085StreamConfig) -> Self {
        Self {
            sensor,
            config,

            next_sequence: 1,
            next_sample_timestamp_us: 123_456,
            next_health_timestamp_us: 1_000_000,

            samples_emitted: 0,
            boot_emitted: false,

            stats: Bno085StreamStats::new(),
        }
    }

    pub fn stats(&self) -> Bno085StreamStats {
        self.stats
    }

    pub fn emit_boot_sequence(&mut self) -> heapless::Vec<Bno085StreamEvent, 4> {
        let identity = self.sensor.identity();
        let capability = self.sensor.capability();
        let health = self.sensor.health();

        let boot = build_boot_sequence(
            &identity,
            &capability,
            &health,
            self.next_sequence,
            1_000,
        );

        let hello_sequence = self.next_sequence;
        let capability_sequence = self.next_sequence.wrapping_add(1);
        let health_sequence = self.next_sequence.wrapping_add(2);

        self.stats.record_boot_frame(hello_sequence, 1_000);
        self.stats.record_boot_frame(capability_sequence, 1_100);
        self.stats.record_boot_frame(health_sequence, 1_200);

        self.next_sequence = boot.next_sequence;
        self.boot_emitted = true;

        let mut events = heapless::Vec::<Bno085StreamEvent, 4>::new();

        events
            .push(Bno085StreamEvent::BootHello {
                sequence: hello_sequence,
                timestamp_us: 1_000,
                frame: boot.hello,
            })
            .expect("boot event capacity exceeded");

        events
            .push(Bno085StreamEvent::BootCapability {
                sequence: capability_sequence,
                timestamp_us: 1_100,
                frame: boot.capability,
            })
            .expect("boot event capacity exceeded");

        events
            .push(Bno085StreamEvent::BootHealth {
                sequence: health_sequence,
                timestamp_us: 1_200,
                frame: boot.health,
            })
            .expect("boot event capacity exceeded");

        events
    }

    pub fn next_event(&mut self) -> Option<Bno085StreamEvent> {
        if !self.boot_emitted {
            return None;
        }

        if self.samples_emitted >= self.config.max_mock_samples {
            return None;
        }

        let should_emit_health =
            self.next_sample_timestamp_us >= self.next_health_timestamp_us;

        if should_emit_health {
            return Some(self.emit_health_event());
        }

        Some(self.emit_sample_event())
    }

    fn emit_sample_event(&mut self) -> Bno085StreamEvent {
        let identity = self.sensor.identity();

        let sequence = self.next_sequence;
        let timestamp_us = self.next_sample_timestamp_us;

        let sample = self.sensor.read_sample(timestamp_us);
        let frame = build_sample_frame(&identity, &sample, sequence);

        self.next_sequence = self.next_sequence.wrapping_add(1);
        self.next_sample_timestamp_us = self
            .next_sample_timestamp_us
            .wrapping_add(self.config.sample_period_us);
        self.samples_emitted = self.samples_emitted.wrapping_add(1);

        self.stats.record_sample_frame(sequence, timestamp_us);

        Bno085StreamEvent::Sample {
            sequence,
            timestamp_us,
            frame,
        }
    }

    fn emit_health_event(&mut self) -> Bno085StreamEvent {
        let identity = self.sensor.identity();
        let health = self.sensor.health();

        let sequence = self.next_sequence;
        let timestamp_us = self.next_health_timestamp_us;

        let frame = build_health_frame(
            &identity,
            &health,
            sequence,
            timestamp_us,
        );

        self.next_sequence = self.next_sequence.wrapping_add(1);
        self.next_health_timestamp_us = self
            .next_health_timestamp_us
            .wrapping_add(self.config.health_period_us);

        self.stats.record_health_frame(sequence, timestamp_us);

        Bno085StreamEvent::Health {
            sequence,
            timestamp_us,
            frame,
        }
    }
}