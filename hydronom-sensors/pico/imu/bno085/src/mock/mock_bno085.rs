use hydronom_sensor_pico_common::health::node_health::NodeHealth;
use hydronom_sensor_pico_common::imu::imu_sample::ImuFusionSample;
use hydronom_sensor_pico_common::node::node_identity::NodeIdentity;
use hydronom_sensor_pico_common::protocol::capability::SensorCapability;

use crate::mock::motion_profile::MockMotionProfile;

// Bu yapı şimdilik PC üzerinde çalışan mock BNO085 kaynağıdır.
//
// Pico gelince buranın gerçek karşılığı:
// - I2C/SPI/UART-RVC init
// - BNO085 product ID kontrolü
// - report enable
// - rotation vector / gyro / accel okuma
// şeklinde ayrı bir gerçek driver katmanına dönüşecek.
//
// Bu mock kaynak şu anda:
// - Hydronom sensör protokolünü test eder.
// - Canlı IMU akışı taklidi üretir.
// - Sequence/timestamp/health davranışını gerçek node'a benzetir.

#[derive(Clone, Copy, Debug)]
pub struct MockBno085 {
    identity: NodeIdentity,
    capability: SensorCapability,
    sample_count: u32,
    motion: MockMotionProfile,
}

impl MockBno085 {
    pub const fn new() -> Self {
        Self {
            identity: NodeIdentity::bno085_default(),
            capability: SensorCapability::bno085_default(),
            sample_count: 0,
            motion: MockMotionProfile::slow_yaw(),
        }
    }

    pub const fn identity(&self) -> NodeIdentity {
        self.identity
    }

    pub const fn capability(&self) -> SensorCapability {
        self.capability
    }

    pub fn health(&self) -> NodeHealth {
        NodeHealth::healthy(
            1000 + self.sample_count.saturating_mul(20),
            self.sample_count,
        )
    }

    pub fn read_sample(&mut self, timestamp_us: u64) -> ImuFusionSample {
        self.sample_count = self.sample_count.wrapping_add(1);

        let motion = self.motion.sample_at(timestamp_us);

        ImuFusionSample {
            timestamp_us,

            orientation: motion.orientation,
            accel_mps2: motion.accel_mps2,
            gyro_radps: motion.gyro_radps,
            mag_ut: motion.mag_ut,
            linear_accel_mps2: motion.linear_accel_mps2,

            orientation_accuracy: 3,
            gyro_accuracy: 3,
            accel_accuracy: 3,
            mag_accuracy: 2,

            quality: 240,
        }
    }
}