use crate::protocol::sensor_kind::SensorKind;

#[repr(u8)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum NodeRole {
    Unknown = 0,

    // BNO085 gibi hazır quaternion/orientation veren node.
    PrimaryFusedOrientation = 1,

    // Ham IMU veren yedek/referans node.
    RawImuReference = 2,

    // Yedek kaynak.
    BackupSensor = 3,

    // Sadece sağlık/diagnostics için kullanılan yardımcı node.
    DiagnosticsOnly = 4,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct NodeIdentity {
    pub node_id: u16,
    pub sensor_kind: SensorKind,
    pub role: NodeRole,

    pub node_name: &'static str,
    pub firmware_name: &'static str,
    pub firmware_version: &'static str,
    pub sensor_model: &'static str,
    pub sensor_vendor: &'static str,
}

impl NodeIdentity {
    pub const fn bno085_default() -> Self {
        Self {
            node_id: 0x0101,
            sensor_kind: SensorKind::Imu,
            role: NodeRole::PrimaryFusedOrientation,

            node_name: "imu_bno085",
            firmware_name: "hydronom-sensor-pico-imu-bno085",
            firmware_version: "0.1.0",
            sensor_model: "BNO085",
            sensor_vendor: "Adafruit 4754",
        }
    }

    pub const fn imu_10dof_default() -> Self {
        Self {
            node_id: 0x0102,
            sensor_kind: SensorKind::Imu,
            role: NodeRole::RawImuReference,

            node_name: "imu_lsm303d_l3gd20_bmp180",
            firmware_name: "hydronom-sensor-pico-imu-10dof",
            firmware_version: "0.1.0",
            sensor_model: "LSM303D + L3GD20 + BMP180",
            sensor_vendor: "Generic 10DOF IMU Module",
        }
    }
}