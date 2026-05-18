#![no_std]

// Hydronom Pico sensör ortak katmanı.
//
// Bu crate bütün Pico tabanlı sensör node'ları tarafından kullanılır.
// Motor kontrol firmware'i bu yapının içinde değildir.
// Bu alan yalnızca sensör node mimarisi içindir.
//
// Ana sorumluluklar:
// - Node kimliği
// - Sensör tipi
// - Capability bildirimi
// - Health bildirimi
// - Ortak IMU payload modelleri
// - Binary frame encode/decode altyapısı

pub mod protocol {
    pub mod frame_type;
    pub mod sensor_kind;
    pub mod payload_kind;
    pub mod capability;
    pub mod capability_payload;
    pub mod sensor_frame;
}

pub mod framing {
    pub mod crc32;
    pub mod byte_writer;
    pub mod byte_reader;
    pub mod frame_encoder;
    pub mod frame_decoder;
}

pub mod node {
    pub mod node_identity;
    pub mod node_payload;
}

pub mod health {
    pub mod node_health;
    pub mod health_payload;
}

pub mod imu {
    pub mod imu_sample;
    pub mod imu_payload_decoder;
}