// Hydronom Pico sensör capability modeli.
//
// Her node Hydronom'a bağlandığında:
// - Ben hangi sensörüm?
// - Hangi verileri verebilirim?
// - Önerilen örnekleme hızım nedir?
// - Maksimum güvenli hızım nedir?
// bilgisini bildirir.
//
// Bu sayede Hydronom sensörleri hard-code etmeden keşfedebilir.

pub const IMU_CAP_ACCEL: u32 = 1 << 0;
pub const IMU_CAP_GYRO: u32 = 1 << 1;
pub const IMU_CAP_MAG: u32 = 1 << 2;
pub const IMU_CAP_QUATERNION: u32 = 1 << 3;
pub const IMU_CAP_LINEAR_ACCEL: u32 = 1 << 4;
pub const IMU_CAP_BAROMETER: u32 = 1 << 5;
pub const IMU_CAP_TEMPERATURE: u32 = 1 << 6;
pub const IMU_CAP_CALIBRATION: u32 = 1 << 7;
pub const IMU_CAP_ACCURACY_STATUS: u32 = 1 << 8;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SensorCapability {
    pub capability_bits: u32,

    // Node'un normalde çalışmasını istediğimiz örnekleme hızı.
    pub recommended_rate_hz: u16,

    // Node'un güvenli şekilde çıkabileceği üst sınır.
    pub max_rate_hz: u16,
}

impl SensorCapability {
    pub const fn empty() -> Self {
        Self {
            capability_bits: 0,
            recommended_rate_hz: 0,
            max_rate_hz: 0,
        }
    }

    pub const fn bno085_default() -> Self {
        Self {
            capability_bits:
                IMU_CAP_ACCEL
                | IMU_CAP_GYRO
                | IMU_CAP_MAG
                | IMU_CAP_QUATERNION
                | IMU_CAP_LINEAR_ACCEL
                | IMU_CAP_CALIBRATION
                | IMU_CAP_ACCURACY_STATUS,
            recommended_rate_hz: 50,
            max_rate_hz: 100,
        }
    }

    pub const fn imu_10dof_default() -> Self {
        Self {
            capability_bits:
                IMU_CAP_ACCEL
                | IMU_CAP_GYRO
                | IMU_CAP_MAG
                | IMU_CAP_BAROMETER
                | IMU_CAP_TEMPERATURE,
            recommended_rate_hz: 50,
            max_rate_hz: 100,
        }
    }

    pub const fn has(&self, flag: u32) -> bool {
        (self.capability_bits & flag) != 0
    }
}