use crate::framing::byte_reader::{ByteReadError, ByteReader};
use crate::imu::imu_sample::{
    ImuFusionSample,
    ImuQuaternion,
    ImuVector3,
};

pub fn decode_imu_fusion_payload(
    payload: &[u8],
) -> Result<ImuFusionSample, ByteReadError> {
    let mut reader = ByteReader::new(payload);

    let timestamp_us = reader.read_u64_le()?;

    let orientation = read_quaternion(&mut reader)?;
    let accel_mps2 = read_vector3(&mut reader)?;
    let gyro_radps = read_vector3(&mut reader)?;
    let mag_ut = read_vector3(&mut reader)?;
    let linear_accel_mps2 = read_vector3(&mut reader)?;

    let orientation_accuracy = reader.read_u8()?;
    let gyro_accuracy = reader.read_u8()?;
    let accel_accuracy = reader.read_u8()?;
    let mag_accuracy = reader.read_u8()?;
    let quality = reader.read_u8()?;

    Ok(ImuFusionSample {
        timestamp_us,
        orientation,
        accel_mps2,
        gyro_radps,
        mag_ut,
        linear_accel_mps2,
        orientation_accuracy,
        gyro_accuracy,
        accel_accuracy,
        mag_accuracy,
        quality,
    })
}

fn read_vector3(reader: &mut ByteReader<'_>) -> Result<ImuVector3, ByteReadError> {
    Ok(ImuVector3 {
        x: reader.read_f32_le()?,
        y: reader.read_f32_le()?,
        z: reader.read_f32_le()?,
    })
}

fn read_quaternion(reader: &mut ByteReader<'_>) -> Result<ImuQuaternion, ByteReadError> {
    Ok(ImuQuaternion {
        w: reader.read_f32_le()?,
        x: reader.read_f32_le()?,
        y: reader.read_f32_le()?,
        z: reader.read_f32_le()?,
    })
}