#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Bno085StreamStats {
    pub boot_frame_count: u32,
    pub sample_frame_count: u32,
    pub health_frame_count: u32,
    pub total_frame_count: u32,

    pub first_timestamp_us: u64,
    pub last_timestamp_us: u64,

    pub last_sequence: u32,
}

impl Bno085StreamStats {
    pub const fn new() -> Self {
        Self {
            boot_frame_count: 0,
            sample_frame_count: 0,
            health_frame_count: 0,
            total_frame_count: 0,

            first_timestamp_us: 0,
            last_timestamp_us: 0,

            last_sequence: 0,
        }
    }

    pub fn record_boot_frame(&mut self, sequence: u32, timestamp_us: u64) {
        self.boot_frame_count = self.boot_frame_count.wrapping_add(1);
        self.total_frame_count = self.total_frame_count.wrapping_add(1);
        self.record_time(sequence, timestamp_us);
    }

    pub fn record_sample_frame(&mut self, sequence: u32, timestamp_us: u64) {
        self.sample_frame_count = self.sample_frame_count.wrapping_add(1);
        self.total_frame_count = self.total_frame_count.wrapping_add(1);
        self.record_time(sequence, timestamp_us);
    }

    pub fn record_health_frame(&mut self, sequence: u32, timestamp_us: u64) {
        self.health_frame_count = self.health_frame_count.wrapping_add(1);
        self.total_frame_count = self.total_frame_count.wrapping_add(1);
        self.record_time(sequence, timestamp_us);
    }

    fn record_time(&mut self, sequence: u32, timestamp_us: u64) {
        if self.first_timestamp_us == 0 {
            self.first_timestamp_us = timestamp_us;
        }

        self.last_timestamp_us = timestamp_us;
        self.last_sequence = sequence;
    }

    pub fn duration_us(&self) -> u64 {
        self.last_timestamp_us.saturating_sub(self.first_timestamp_us)
    }
}