use hydronom_sensor_pico_common::imu::imu_sample::{
    ImuQuaternion,
    ImuVector3,
};

// PC mock ve ileride simülasyon testleri için hafif hareket profili.
//
// Bu profil gerçek BNO085 driver değildir.
// Sadece Hydronom tarafına canlı IMU akışı gibi veri göndermek için kullanılır.
//
// Hareket:
// - Yaw yavaşça artar.
// - Gyro Z sabit küçük açısal hız verir.
// - Accel yaklaşık yerçekimi vektörünü taşır.
// - Linear accel küçük ileri titreşim üretir.
// - Magnetometer yönü yaw'a göre döner.

#[derive(Clone, Copy, Debug)]
pub struct MockMotionProfile {
    pub yaw_rate_radps: f32,
}

impl MockMotionProfile {
    pub const fn slow_yaw() -> Self {
        Self {
            yaw_rate_radps: 0.15,
        }
    }

    pub fn sample_at(&self, timestamp_us: u64) -> MockMotionSample {
        let t = timestamp_us as f32 / 1_000_000.0;
        let yaw = self.yaw_rate_radps * t;

        let half_yaw = yaw * 0.5;
        let sin_half = fast_sin(half_yaw);
        let cos_half = fast_cos(half_yaw);

        let orientation = ImuQuaternion {
            w: cos_half,
            x: 0.0,
            y: 0.0,
            z: sin_half,
        };

        let gyro_radps = ImuVector3 {
            x: 0.0,
            y: 0.0,
            z: self.yaw_rate_radps,
        };

        let accel_mps2 = ImuVector3 {
            x: 0.0,
            y: 0.0,
            z: 9.80665,
        };

        let linear_accel_mps2 = ImuVector3 {
            x: 0.08 * fast_sin(t * 1.7),
            y: 0.03 * fast_cos(t * 1.1),
            z: 0.0,
        };

        let mag_ut = ImuVector3 {
            x: 25.0 * fast_cos(yaw),
            y: 25.0 * fast_sin(yaw),
            z: 40.0,
        };

        MockMotionSample {
            orientation,
            accel_mps2,
            gyro_radps,
            mag_ut,
            linear_accel_mps2,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct MockMotionSample {
    pub orientation: ImuQuaternion,
    pub accel_mps2: ImuVector3,
    pub gyro_radps: ImuVector3,
    pub mag_ut: ImuVector3,
    pub linear_accel_mps2: ImuVector3,
}

// no_std tarafını bozmayalım diye std trig fonksiyonlarına yaslanmıyoruz.
// Bu yaklaşım mock için yeterli. Gerçek BNO085 zaten quaternion verecek.
//
// Küçük açı / periyodik test için basit Taylor + normalize edilmiş aralık.
// Burada amaç fizik motoru değil, canlı görünen deterministik veri üretmek.

fn fast_sin(x: f32) -> f32 {
    let x = wrap_pi(x);
    let x2 = x * x;

    x * (1.0 - x2 / 6.0 + (x2 * x2) / 120.0)
}

fn fast_cos(x: f32) -> f32 {
    let x = wrap_pi(x);
    let x2 = x * x;

    1.0 - x2 / 2.0 + (x2 * x2) / 24.0
}

fn wrap_pi(mut x: f32) -> f32 {
    const PI: f32 = 3.1415927;
    const TWO_PI: f32 = 6.2831855;

    while x > PI {
        x -= TWO_PI;
    }

    while x < -PI {
        x += TWO_PI;
    }

    x
}