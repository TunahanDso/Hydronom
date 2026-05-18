use crate::bno085::rvc_packet::Bno085RvcPacket;
use crate::imu::imu_sample::{
    ImuFusionSample,
    ImuQuaternion,
    ImuVector3,
};

// UART-RVC mode gyro/mag/linear-accel vermez.
// Bu yüzden Hydronom sample içinde:
// - orientation quaternion gerçek RVC yaw/pitch/roll'dan üretilir.
// - accel_mps2 gerçek RVC accel değeridir.
// - gyro_radps = zero
// - mag_ut = zero
// - linear_accel_mps2 = zero
//
// Daha sonra SPI/SHTP full driver eklersek gyro/mag/linear accel alanları gerçek dolabilir.

pub fn rvc_packet_to_imu_fusion_sample(
    packet: &Bno085RvcPacket,
    timestamp_us: u64,
) -> ImuFusionSample {
    let orientation = euler_yaw_pitch_roll_deg_to_quaternion(
        packet.yaw_deg,
        packet.pitch_deg,
        packet.roll_deg,
    );

    ImuFusionSample {
        timestamp_us,

        orientation,

        accel_mps2: ImuVector3 {
            x: packet.accel_x_mps2,
            y: packet.accel_y_mps2,
            z: packet.accel_z_mps2,
        },

        gyro_radps: ImuVector3::zero(),
        mag_ut: ImuVector3::zero(),
        linear_accel_mps2: ImuVector3::zero(),

        // RVC packet doğrudan accuracy alanı taşımaz.
        // Paket checksum doğruysa şimdilik orientation/accel güvenilir kabul edilir.
        orientation_accuracy: 2,
        gyro_accuracy: 0,
        accel_accuracy: 2,
        mag_accuracy: 0,

        quality: estimate_rvc_quality(packet),
    }
}

pub fn euler_yaw_pitch_roll_deg_to_quaternion(
    yaw_deg: f32,
    pitch_deg: f32,
    roll_deg: f32,
) -> ImuQuaternion {
    // BNO08X datasheet RVC orientation için rotasyonların yaw, pitch, roll
    // sırasıyla uygulanmasını söyler. Burada ZYX convention kullanıyoruz:
    // yaw around Z, pitch around Y, roll around X.

    let yaw = deg_to_rad(yaw_deg);
    let pitch = deg_to_rad(pitch_deg);
    let roll = deg_to_rad(roll_deg);

    let cy = libm::cosf(yaw * 0.5);
    let sy = libm::sinf(yaw * 0.5);

    let cp = libm::cosf(pitch * 0.5);
    let sp = libm::sinf(pitch * 0.5);

    let cr = libm::cosf(roll * 0.5);
    let sr = libm::sinf(roll * 0.5);

    let q = ImuQuaternion {
        w: cr * cp * cy + sr * sp * sy,
        x: sr * cp * cy - cr * sp * sy,
        y: cr * sp * cy + sr * cp * sy,
        z: cr * cp * sy - sr * sp * cy,
    };

    normalize_quaternion(q)
}

fn estimate_rvc_quality(packet: &Bno085RvcPacket) -> u8 {
    // RVC'de explicit calibration accuracy yok.
    // Checksum parse aşamasında doğrulandığı için temel kalite yüksek verilir.
    // Çok saçma accel büyüklüğü varsa kaliteyi biraz düşürürüz.
    let accel_norm = libm::sqrtf(
        packet.accel_x_mps2 * packet.accel_x_mps2
            + packet.accel_y_mps2 * packet.accel_y_mps2
            + packet.accel_z_mps2 * packet.accel_z_mps2,
    );

    if accel_norm < 0.5 || accel_norm > 40.0 {
        160
    } else {
        220
    }
}

fn normalize_quaternion(q: ImuQuaternion) -> ImuQuaternion {
    let norm = libm::sqrtf(q.w * q.w + q.x * q.x + q.y * q.y + q.z * q.z);

    if norm <= 0.000_001 {
        return ImuQuaternion::identity();
    }

    ImuQuaternion {
        w: q.w / norm,
        x: q.x / norm,
        y: q.y / norm,
        z: q.z / norm,
    }
}

fn deg_to_rad(deg: f32) -> f32 {
    deg * 0.017_453_292_519_943_295
}