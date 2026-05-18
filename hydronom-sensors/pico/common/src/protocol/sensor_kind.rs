#[repr(u8)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SensorKind {
    Unknown = 0,

    Imu = 1,
    Gps = 2,
    Depth = 3,
    Lidar = 4,
    Power = 5,
    Encoder = 6,
    Environment = 7,
    Auxiliary = 8,
}

impl SensorKind {
    pub const fn as_u8(self) -> u8 {
        self as u8
    }

    pub const fn from_u8(value: u8) -> Self {
        match value {
            1 => Self::Imu,
            2 => Self::Gps,
            3 => Self::Depth,
            4 => Self::Lidar,
            5 => Self::Power,
            6 => Self::Encoder,
            7 => Self::Environment,
            8 => Self::Auxiliary,
            _ => Self::Unknown,
        }
    }
}