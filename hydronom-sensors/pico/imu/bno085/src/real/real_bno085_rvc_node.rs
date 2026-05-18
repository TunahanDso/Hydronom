use hydronom_sensor_pico_common::bno085::rvc_packet::{
    Bno085RvcParseError,
    BNO085_RVC_HEADER_0,
};
use hydronom_sensor_pico_common::bno085::rvc_stream_reader::{
    Bno085RvcStreamEvent,
    Bno085RvcStreamReader,
    Bno085RvcStreamState,
};
use hydronom_sensor_pico_common::bno085::rvc_to_imu::rvc_packet_to_imu_fusion_sample;
use hydronom_sensor_pico_common::health::node_health::{
    NodeHealth,
    NodeHealthState,
};
use hydronom_sensor_pico_common::imu::imu_sample::ImuFusionSample;
use hydronom_sensor_pico_common::node::node_identity::NodeIdentity;
use hydronom_sensor_pico_common::protocol::capability::SensorCapability;

use crate::node::boot_sequence::{
    build_boot_sequence,
    BootFrameSequence,
};
use crate::node::frame_builders::{
    build_health_frame,
    build_sample_frame,
    SensorFrameBytes,
};

// RealBno085RvcNode
//
// Bu yapı mock sensör değildir.
// Gerçek BNO085 UART-RVC byte stream'ini Hydronom sensör frame'lerine çeviren
// donanımdan bağımsız node çekirdeğidir.
//
// Gerçek Pico firmware'de kullanım mantığı:
//
// 1. Pico boot eder.
// 2. BNO085 UART-RVC hattı hazırlanır.
// 3. node.build_boot_frames(...) çağrılır.
// 4. Dönen HELLO / CAPABILITY / HEALTH frame'leri USB-UART üzerinden Hydronom'a yazılır.
// 5. BNO085 UART RX'ten gelen her byte node.push_uart_byte(timestamp_us, byte) içine verilir.
// 6. Eğer paket tamamlanırsa Hydronom SAMPLE frame döner.
// 7. O frame doğrudan Hydronom'a yazılır.
//
// Önemli zaman damgası kararı:
// UART-RVC paketi 19 byte'tır. push_uart_byte() içine gelen timestamp_us her byte'ın
// UART'tan alındığı zamanı temsil eder. Paket tamamlandığında gelen timestamp son byte
// zamanıdır. Füzyon kalitesi için sample timestamp'i olarak son byte zamanı değil,
// paket başlangıcı yani ilk 0xAA byte'ının yakalandığı zaman kullanılır.
//
// Health kararı:
// Tekil UART glitch'i node'u sonsuza kadar Degraded yapmaz.
// Son 128 olay bitset olarak tutulur. Hata oranı eşik altına inerse node tekrar Healthy olur.
//
// Bu dosya bilerek Embassy/rp-hal kullanmaz.
// Çünkü burada gerçek sensör verisi işleme çekirdeğini donanım katmanından ayırıyoruz.

const HEALTH_WINDOW_SIZE: usize = 128;
const HEALTH_WINDOW_SIZE_U32: u32 = HEALTH_WINDOW_SIZE as u32;

// HEALTH_WINDOW_SIZE 2'nin kuvveti olduğu için modulo yerine mask kullanıyoruz.
// 128 - 1 = 127, yani index 0..127 aralığında döner.
const HEALTH_WINDOW_INDEX_MASK: usize = HEALTH_WINDOW_SIZE - 1;

// 128 bit = 4 * u32 = 16 byte.
// Eski [u8; 128] yerine bu yapı RAM'i ciddi azaltır.
const HEALTH_WINDOW_WORD_COUNT: usize = HEALTH_WINDOW_SIZE >> 5;

// u32 içinde 32 bit olduğu için:
// word_index = index / 32 yerine index >> 5
// bit_index  = index % 32 yerine index & 31
const HEALTH_WORD_SHIFT: usize = 5;
const HEALTH_BIT_MASK: usize = 31;

// Son 128 olay içinde 4 veya daha az hata varsa node tekrar Healthy kabul edilir.
// Bu yaklaşık %3.125 hata oranıdır.
// Yarışma sırasında UART'ta tekil glitch'ler kalıcı Degraded yaratmasın diye seçildi.
const HEALTH_MAX_ERRORS_IN_WINDOW: u32 = 4;

#[derive(Debug)]
pub struct RealBno085RvcNode {
    identity: NodeIdentity,
    capability: SensorCapability,
    reader: Bno085RvcStreamReader,

    sequence: u32,

    sample_count: u32,
    parse_error_count: u32,
    bus_error_count: u32,
    last_error_code: u16,

    last_timestamp_us: u64,

    // Paket timestamp latch.
    //
    // 0xAA header'ın ilk byte'ı görüldüğünde bu alan set edilir.
    // Paket tamamlandığında SAMPLE timestamp'i olarak bu değer kullanılır.
    // Böylece son byte zamanı kaynaklı yaklaşık 1.65ms transfer gecikmesi sample'a
    // doğrudan yazılmaz.
    current_packet_start_timestamp_us: Option<u64>,

    // Kayan health penceresi.
    //
    // Her bit bir olayı temsil eder:
    // 0 = başarılı olay
    // 1 = hata olayı
    //
    // 128 olay = [u32; 4] = 16 byte.
    health_error_bits: [u32; HEALTH_WINDOW_WORD_COUNT],
    health_window_index: usize,
    health_window_filled: u32,
    health_error_count_in_window: u32,
}

#[derive(Debug)]
pub enum RealBno085RvcNodeEvent {
    Waiting,

    SampleFrame {
        sequence: u32,
        timestamp_us: u64,
        sample: ImuFusionSample,
        frame: SensorFrameBytes,
    },

    ParseError {
        error: Bno085RvcParseError,
        error_count: u32,
    },
}

impl RealBno085RvcNode {
    pub const fn new() -> Self {
        Self {
            identity: NodeIdentity::bno085_default(),
            capability: SensorCapability::bno085_default(),
            reader: Bno085RvcStreamReader::new(),

            sequence: 1,

            sample_count: 0,
            parse_error_count: 0,
            bus_error_count: 0,
            last_error_code: 0,

            last_timestamp_us: 0,
            current_packet_start_timestamp_us: None,

            health_error_bits: [0u32; HEALTH_WINDOW_WORD_COUNT],
            health_window_index: 0,
            health_window_filled: 0,
            health_error_count_in_window: 0,
        }
    }

    #[allow(dead_code)]
    pub const fn identity(&self) -> NodeIdentity {
        self.identity
    }

    #[allow(dead_code)]
    pub const fn capability(&self) -> SensorCapability {
        self.capability
    }

    pub const fn sequence(&self) -> u32 {
        self.sequence
    }

    pub const fn sample_count(&self) -> u32 {
        self.sample_count
    }

    pub const fn parse_error_count(&self) -> u32 {
        self.parse_error_count
    }

    #[allow(dead_code)]
    pub const fn bus_error_count(&self) -> u32 {
        self.bus_error_count
    }

    #[allow(dead_code)]
    pub const fn last_timestamp_us(&self) -> u64 {
        self.last_timestamp_us
    }

    pub const fn health_error_count_in_window(&self) -> u32 {
        self.health_error_count_in_window
    }

    pub const fn health_window_filled(&self) -> u32 {
        self.health_window_filled
    }

    pub fn health(&self, uptime_ms: u32) -> NodeHealth {
        NodeHealth {
            state: self.derive_health_state(),

            uptime_ms,
            sample_count: self.sample_count,
            read_error_count: self.parse_error_count,
            bus_error_count: self.bus_error_count,
            last_error_code: self.last_error_code,
        }
    }

    pub fn build_boot_frames(
        &mut self,
        uptime_ms: u32,
        boot_timestamp_us: u64,
    ) -> BootFrameSequence {
        let health = self.health(uptime_ms);

        let boot = build_boot_sequence(
            &self.identity,
            &self.capability,
            &health,
            self.sequence,
            boot_timestamp_us,
        );

        self.sequence = boot.next_sequence;
        boot
    }

    pub fn build_health_frame(
        &mut self,
        uptime_ms: u32,
        timestamp_us: u64,
    ) -> SensorFrameBytes {
        let sequence = self.sequence;
        self.sequence = self.sequence.wrapping_add(1);

        let health = self.health(uptime_ms);

        build_health_frame(
            &self.identity,
            &health,
            sequence,
            timestamp_us,
        )
    }

    pub fn push_uart_byte(
        &mut self,
        timestamp_us: u64,
        byte: u8,
    ) -> RealBno085RvcNodeEvent {
        self.last_timestamp_us = timestamp_us;

        // Timestamp latch:
        //
        // Reader henüz paket arıyorken 0xAA görürsek bunu olası paket başlangıcı
        // kabul edip zamanı kilitliyoruz. Eğer devamında header bozulursa parse
        // tamamlanmayacağı için sonraki header aramasında yeni latch alınır.
        if self.should_latch_packet_start(byte) {
            self.current_packet_start_timestamp_us = Some(timestamp_us);
        }

        match self.reader.push_byte(byte) {
            Bno085RvcStreamEvent::Waiting => RealBno085RvcNodeEvent::Waiting,

            Bno085RvcStreamEvent::Packet(packet) => {
                let sample_timestamp_us = self
                    .current_packet_start_timestamp_us
                    .unwrap_or(timestamp_us);

                self.current_packet_start_timestamp_us = None;

                let sample = rvc_packet_to_imu_fusion_sample(
                    &packet,
                    sample_timestamp_us,
                );

                let sequence = self.sequence;
                self.sequence = self.sequence.wrapping_add(1);

                let frame = build_sample_frame(
                    &self.identity,
                    &sample,
                    sequence,
                );

                self.sample_count = self.sample_count.wrapping_add(1);
                self.record_health_event(false);

                RealBno085RvcNodeEvent::SampleFrame {
                    sequence,
                    timestamp_us: sample_timestamp_us,
                    sample,
                    frame,
                }
            }

            Bno085RvcStreamEvent::ParseError(error) => {
                self.current_packet_start_timestamp_us = None;

                self.parse_error_count = self.parse_error_count.wrapping_add(1);
                self.last_error_code = map_parse_error_to_code(error);
                self.record_health_event(true);

                RealBno085RvcNodeEvent::ParseError {
                    error,
                    error_count: self.parse_error_count,
                }
            }
        }
    }

    pub fn record_bus_error(&mut self, error_code: u16) {
        self.bus_error_count = self.bus_error_count.wrapping_add(1);
        self.last_error_code = error_code;
        self.record_health_event(true);
    }

    #[inline]
    fn should_latch_packet_start(&self, byte: u8) -> bool {
        byte == BNO085_RVC_HEADER_0
            && self.reader.state() == Bno085RvcStreamState::SearchingHeader0
    }

    #[inline]
    fn derive_health_state(&self) -> NodeHealthState {
        if self.health_window_filled == 0 {
            return NodeHealthState::Healthy;
        }

        if self.health_error_count_in_window <= HEALTH_MAX_ERRORS_IN_WINDOW {
            NodeHealthState::Healthy
        } else {
            NodeHealthState::Degraded
        }
    }

    #[inline]
    fn record_health_event(&mut self, is_error: bool) {
        let old_is_error = self.read_health_bit(self.health_window_index);

        // Eğer pencere tam doluysa, üzerine yazacağımız eski geçerli değeri önce toplamdan düş.
        // Eğer pencere henüz dolmadıysa o slot daha önce geçerli bir olay sayılmıyordur.
        if self.health_window_filled >= HEALTH_WINDOW_SIZE_U32 {
            if old_is_error {
                self.health_error_count_in_window =
                    self.health_error_count_in_window.saturating_sub(1);
            }
        } else {
            self.health_window_filled = self.health_window_filled.wrapping_add(1);
        }

        self.write_health_bit(self.health_window_index, is_error);

        if is_error {
            self.health_error_count_in_window =
                self.health_error_count_in_window.wrapping_add(1);
        }

        self.health_window_index =
            (self.health_window_index + 1) & HEALTH_WINDOW_INDEX_MASK;
    }

    #[inline]
    fn read_health_bit(&self, index: usize) -> bool {
        // HEALTH_WINDOW_SIZE = 128 olduğu için index 0..127 aralığında döner.
        // word_index = index / 32 yerine index >> 5
        // bit_index  = index % 32 yerine index & 31
        //
        // Bu ifade LLVM'in yapacağı optimizasyonu kaynak kod seviyesinde açık eder.
        let word_index = index >> HEALTH_WORD_SHIFT;
        let bit_index = index & HEALTH_BIT_MASK;
        let mask = 1u32 << bit_index;

        (self.health_error_bits[word_index] & mask) != 0
    }

    #[inline]
    fn write_health_bit(&mut self, index: usize, value: bool) {
        // HEALTH_WINDOW_SIZE = 128 olduğu için index 0..127 aralığında döner.
        // word_index = index / 32 yerine index >> 5
        // bit_index  = index % 32 yerine index & 31
        let word_index = index >> HEALTH_WORD_SHIFT;
        let bit_index = index & HEALTH_BIT_MASK;
        let mask = 1u32 << bit_index;

        if value {
            self.health_error_bits[word_index] |= mask;
        } else {
            self.health_error_bits[word_index] &= !mask;
        }
    }
}

#[inline]
fn map_parse_error_to_code(error: Bno085RvcParseError) -> u16 {
    match error {
        Bno085RvcParseError::TooShort => 2001,
        Bno085RvcParseError::InvalidHeader => 2002,
        Bno085RvcParseError::ChecksumMismatch { .. } => 2003,
    }
}