#[repr(u8)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PayloadKind {
    Unknown = 0,

    // Genel node payloadları.
    NodeHello = 1,
    NodeCapability = 2,
    NodeHealth = 3,
    NodeError = 4,

    // IMU payloadları.
    ImuFusionQuaternion = 20,
    ImuRaw9Dof = 21,
    ImuCombined10Dof = 22,
    ImuBarometer = 23,
}

impl PayloadKind {
    pub const fn as_u8(self) -> u8 {
        self as u8
    }

    pub const fn from_u8(value: u8) -> Self {
        match value {
            1 => Self::NodeHello,
            2 => Self::NodeCapability,
            3 => Self::NodeHealth,
            4 => Self::NodeError,

            20 => Self::ImuFusionQuaternion,
            21 => Self::ImuRaw9Dof,
            22 => Self::ImuCombined10Dof,
            23 => Self::ImuBarometer,

            _ => Self::Unknown,
        }
    }
}