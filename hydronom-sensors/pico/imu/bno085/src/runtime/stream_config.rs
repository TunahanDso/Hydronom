#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Bno085StreamConfig {
    // IMU sample periyodu.
    // 50 Hz = 20_000 us.
    pub sample_period_us: u64,

    // Health frame periyodu.
    // 1 Hz = 1_000_000 us.
    pub health_period_us: u64,

    // PC mock testte kaç sample üretileceği.
    // Gerçek Pico firmware'de bu limit olmayacak; sonsuz loop olacak.
    pub max_mock_samples: u32,
}

impl Bno085StreamConfig {
    pub const fn default_50hz_mock() -> Self {
        Self {
            sample_period_us: 20_000,
            health_period_us: 1_000_000,
            max_mock_samples: 60,
        }
    }
}