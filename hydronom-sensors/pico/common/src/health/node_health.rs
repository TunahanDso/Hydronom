#[repr(u8)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum NodeHealthState {
    Unknown = 0,
    Booting = 1,
    Healthy = 2,
    Degraded = 3,
    SensorMissing = 4,
    BusError = 5,
    Fatal = 6,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct NodeHealth {
    pub state: NodeHealthState,

    // Pico açıldığından beri geçen süre.
    pub uptime_ms: u32,

    // Başarılı sample sayısı.
    pub sample_count: u32,

    // Sensör okuma hatası.
    pub read_error_count: u32,

    // I2C/SPI/UART bus hatası.
    pub bus_error_count: u32,

    // Son hata kodu.
    pub last_error_code: u16,
}

impl NodeHealth {
    pub const fn booting() -> Self {
        Self {
            state: NodeHealthState::Booting,
            uptime_ms: 0,
            sample_count: 0,
            read_error_count: 0,
            bus_error_count: 0,
            last_error_code: 0,
        }
    }

    pub const fn healthy(uptime_ms: u32, sample_count: u32) -> Self {
        Self {
            state: NodeHealthState::Healthy,
            uptime_ms,
            sample_count,
            read_error_count: 0,
            bus_error_count: 0,
            last_error_code: 0,
        }
    }

    pub const fn sensor_missing(uptime_ms: u32, read_error_count: u32) -> Self {
        Self {
            state: NodeHealthState::SensorMissing,
            uptime_ms,
            sample_count: 0,
            read_error_count,
            bus_error_count: 0,
            last_error_code: 1001,
        }
    }
}