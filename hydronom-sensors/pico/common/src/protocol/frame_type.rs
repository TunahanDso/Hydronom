#[repr(u8)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FrameType {
    Unknown = 0,

    // Node ilk bağlandığında Hydronom'a kim olduğunu söyler.
    Hello = 1,

    // Node hangi veri tiplerini, hangi hızlarda verebildiğini söyler.
    Capability = 2,

    // Asıl sensör verisi.
    Sample = 3,

    // Node sağlığı, hata sayıları, uptime, sensör bağlantı durumu.
    Health = 4,

    // Hata bildirimi.
    Error = 5,
}

impl FrameType {
    pub const fn as_u8(self) -> u8 {
        self as u8
    }

    pub const fn from_u8(value: u8) -> Self {
        match value {
            1 => Self::Hello,
            2 => Self::Capability,
            3 => Self::Sample,
            4 => Self::Health,
            5 => Self::Error,
            _ => Self::Unknown,
        }
    }
}