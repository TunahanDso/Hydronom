use crate::framing::byte_writer::{ByteWriteError, ByteWriter};

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct ImuVector3 {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

impl ImuVector3 {
    pub const fn zero() -> Self {
        Self {
            x: 0.0,
            y: 0.0,
            z: 0.0,
        }
    }

    pub fn encode_into(&self, writer: &mut ByteWriter<'_>) -> Result<(), ByteWriteError> {
        writer.write_f32_le(self.x)?;
        writer.write_f32_le(self.y)?;
        writer.write_f32_le(self.z)?;
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct ImuQuaternion {
    pub w: f32,
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

impl ImuQuaternion {
    pub const fn identity() -> Self {
        Self {
            w: 1.0,
            x: 0.0,
            y: 0.0,
            z: 0.0,
        }
    }

    pub fn encode_into(&self, writer: &mut ByteWriter<'_>) -> Result<(), ByteWriteError> {
        writer.write_f32_le(self.w)?;
        writer.write_f32_le(self.x)?;
        writer.write_f32_le(self.y)?;
        writer.write_f32_le(self.z)?;
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct ImuFusionSample {
    pub timestamp_us: u64,

    // BNO085 gibi sensörlerden gelen hazır orientation.
    pub orientation: ImuQuaternion,

    // Yardımcı vektörler.
    pub accel_mps2: ImuVector3,
    pub gyro_radps: ImuVector3,
    pub mag_ut: ImuVector3,
    pub linear_accel_mps2: ImuVector3,

    // Sensör accuracy / calibration durumları.
    pub orientation_accuracy: u8,
    pub gyro_accuracy: u8,
    pub accel_accuracy: u8,
    pub mag_accuracy: u8,

    // Hydronom kalite skoru.
    // 255 en iyi, 0 geçersiz.
    pub quality: u8,
}

impl ImuFusionSample {
    pub const fn mock_identity(timestamp_us: u64) -> Self {
        Self {
            timestamp_us,
            orientation: ImuQuaternion::identity(),

            accel_mps2: ImuVector3 {
                x: 0.0,
                y: 0.0,
                z: 9.80665,
            },
            gyro_radps: ImuVector3::zero(),
            mag_ut: ImuVector3 {
                x: 25.0,
                y: 0.0,
                z: 40.0,
            },
            linear_accel_mps2: ImuVector3::zero(),

            orientation_accuracy: 3,
            gyro_accuracy: 3,
            accel_accuracy: 3,
            mag_accuracy: 2,

            quality: 240,
        }
    }

    pub fn encode_into(&self, writer: &mut ByteWriter<'_>) -> Result<(), ByteWriteError> {
        writer.write_u64_le(self.timestamp_us)?;

        self.orientation.encode_into(writer)?;
        self.accel_mps2.encode_into(writer)?;
        self.gyro_radps.encode_into(writer)?;
        self.mag_ut.encode_into(writer)?;
        self.linear_accel_mps2.encode_into(writer)?;

        writer.write_u8(self.orientation_accuracy)?;
        writer.write_u8(self.gyro_accuracy)?;
        writer.write_u8(self.accel_accuracy)?;
        writer.write_u8(self.mag_accuracy)?;
        writer.write_u8(self.quality)?;

        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct ImuRaw9DofSample {
    pub timestamp_us: u64,

    pub accel_mps2: ImuVector3,
    pub gyro_radps: ImuVector3,
    pub mag_ut: ImuVector3,

    pub quality: u8,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct ImuBarometerSample {
    pub timestamp_us: u64,

    pub pressure_pa: f32,
    pub temperature_c: f32,
    pub estimated_altitude_m: f32,

    pub quality: u8,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct ImuCombined10DofSample {
    pub raw: ImuRaw9DofSample,
    pub barometer: ImuBarometerSample,
}