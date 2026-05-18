use crate::real::real_bno085_rvc_node::{
    RealBno085RvcNode,
    RealBno085RvcNodeEvent,
};

// HydronomFrameSink
//
// Gerçek Pico tarafında bu trait'in implementasyonu:
// - USB CDC writer
// - UART TX writer
// - ileride RS485/CAN bridge writer
// olabilir.
//
// Firmware çekirdeği frame byte'larını nereye yazdığını bilmez.
// Sadece "bu Hydronom binary frame'ini dışarı gönder" der.
//
// Bu sayede:
// - iş mantığı donanımdan bağımsız kalır
// - PC self-test yapılabilir
// - Embassy/rp-hal sadece dış adaptör olur

pub trait HydronomFrameSink {
    type Error;

    fn write_frame(&mut self, frame: &[u8]) -> Result<(), Self::Error>;
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Bno085FirmwareConfig {
    // HEALTH frame gönderim periyodu.
    // 1 Hz = 1_000_000 us.
    pub health_period_us: u64,
}

impl Bno085FirmwareConfig {
    pub const fn default_1hz_health() -> Self {
        Self {
            health_period_us: 1_000_000,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Bno085FirmwareStats {
    pub boot_frames_sent: u32,
    pub sample_frames_sent: u32,
    pub health_frames_sent: u32,
    pub health_frames_dropped: u32,
    pub total_frames_sent: u32,

    pub bytes_received_from_bno085: u32,

    pub parse_errors: u32,
    pub bus_errors: u32,
    pub sink_write_errors: u32,

    pub last_sample_timestamp_us: u64,
    pub last_health_timestamp_us: u64,
    pub last_event_timestamp_us: u64,

    // Health scheduler'ın son planladığı tick.
    //
    // Bu değer write_frame başarılı olmasa bile güncellenir.
    // Amaç: USB/UART sink bir süre hata verirse firmware loop'un aynı health frame
    // denemesine takılıp kalmasını engellemek.
    pub last_health_schedule_timestamp_us: u64,
}

impl Bno085FirmwareStats {
    pub const fn new() -> Self {
        Self {
            boot_frames_sent: 0,
            sample_frames_sent: 0,
            health_frames_sent: 0,
            health_frames_dropped: 0,
            total_frames_sent: 0,

            bytes_received_from_bno085: 0,

            parse_errors: 0,
            bus_errors: 0,
            sink_write_errors: 0,

            last_sample_timestamp_us: 0,
            last_health_timestamp_us: 0,
            last_event_timestamp_us: 0,

            last_health_schedule_timestamp_us: 0,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Bno085FirmwareEvent {
    Waiting,

    BootFramesSent {
        count: u32,
        next_sequence: u32,
    },

    SampleFrameSent {
        sequence: u32,
        timestamp_us: u64,
    },

    HealthFrameSent {
        timestamp_us: u64,
    },

    // Health karesi gönderilemedi ama scheduler ilerletildi.
    //
    // Bu durum fatal değildir. Health frame düşebilir; node ana veri akışına devam eder.
    // Üst katman isterse bunu loglar, isterse ignore eder.
    HealthFrameDropped {
        timestamp_us: u64,
    },

    ParseError {
        error_count: u32,
    },
}

#[derive(Debug)]
pub struct Bno085FirmwareCore {
    node: RealBno085RvcNode,
    config: Bno085FirmwareConfig,

    boot_sent: bool,

    // next_health_timestamp_us yerine son başarılı/planlanan health zamanı tutulur.
    //
    // Neden?
    // - timestamp kaynağı resetlenirse veya geriye saparsa sistem sonsuza kadar
    //   "hedef zamana ulaşamadım" diye health göndermeyi kesmesin.
    // - I/O write hata verirse scheduler aynı tick'e takılıp kalmasın.
    last_health_schedule_timestamp_us: u64,

    stats: Bno085FirmwareStats,
}

impl Bno085FirmwareCore {
    pub const fn new(config: Bno085FirmwareConfig) -> Self {
        Self {
            node: RealBno085RvcNode::new(),
            config,

            boot_sent: false,
            last_health_schedule_timestamp_us: 0,

            stats: Bno085FirmwareStats::new(),
        }
    }

    pub const fn stats(&self) -> Bno085FirmwareStats {
        self.stats
    }

    pub const fn boot_sent(&self) -> bool {
        self.boot_sent
    }

    // Gerçek Pico transport adaptöründe veya üst seviye diagnostics çıktısında
    // kullanılacak. PC self-test şu an çağırmadığı için dead_code uyarısını kapatıyoruz.
    #[allow(dead_code)]
    pub const fn next_sequence(&self) -> u32 {
        self.node.sequence()
    }

    pub const fn sample_count(&self) -> u32 {
        self.node.sample_count()
    }

    pub const fn parse_error_count(&self) -> u32 {
        self.node.parse_error_count()
    }

    // Boot frame gönderimi.
    //
    // Gerçek Pico firmware'de açılışta bir kez çağrılır:
    //
    // core.emit_boot_frames(uptime_ms, now_us, &mut usb_writer)?;
    //
    // Gönderilen frame sırası:
    // 1. HELLO
    // 2. CAPABILITY
    // 3. HEALTH

    pub fn emit_boot_frames<S>(
        &mut self,
        uptime_ms: u32,
        timestamp_us: u64,
        sink: &mut S,
    ) -> Result<Bno085FirmwareEvent, S::Error>
    where
        S: HydronomFrameSink,
    {
        let boot = self.node.build_boot_frames(
            uptime_ms,
            timestamp_us,
        );

        // Boot aşaması kritik olduğu için burada hata olursa üst katmanın görmesi doğru.
        // Bu frame'ler sensör node kimliğinin temelidir.
        sink.write_frame(&boot.hello)?;
        sink.write_frame(&boot.capability)?;
        sink.write_frame(&boot.health)?;

        self.boot_sent = true;
        self.last_health_schedule_timestamp_us = timestamp_us;

        self.stats.boot_frames_sent = self.stats.boot_frames_sent.wrapping_add(3);
        self.stats.health_frames_sent = self.stats.health_frames_sent.wrapping_add(1);
        self.stats.total_frames_sent = self.stats.total_frames_sent.wrapping_add(3);
        self.stats.last_health_timestamp_us = timestamp_us;
        self.stats.last_event_timestamp_us = timestamp_us;
        self.stats.last_health_schedule_timestamp_us = timestamp_us;

        Ok(Bno085FirmwareEvent::BootFramesSent {
            count: 3,
            next_sequence: boot.next_sequence,
        })
    }

    // UART RX'ten gelen tek byte'ı işler.
    //
    // Gerçek Pico firmware loop:
    //
    // while let Some(byte) = bno085_uart.read_byte() {
    //     core.push_bno085_uart_byte(uptime_ms, now_us, byte, &mut usb_writer)?;
    // }
    //
    // Eğer byte paket tamamlamadıysa Waiting döner.
    // Eğer paket tamamlandıysa SAMPLE frame üretir ve sink'e yazar.
    // Eğer parse hatası oluşursa health penceresine hata kaydeder.

    pub fn push_bno085_uart_byte<S>(
        &mut self,
        _uptime_ms: u32,
        timestamp_us: u64,
        byte: u8,
        sink: &mut S,
    ) -> Result<Bno085FirmwareEvent, S::Error>
    where
        S: HydronomFrameSink,
    {
        self.stats.bytes_received_from_bno085 =
            self.stats.bytes_received_from_bno085.wrapping_add(1);
        self.stats.last_event_timestamp_us = timestamp_us;

        match self.node.push_uart_byte(timestamp_us, byte) {
            RealBno085RvcNodeEvent::Waiting => Ok(Bno085FirmwareEvent::Waiting),

            RealBno085RvcNodeEvent::SampleFrame {
                sequence,
                timestamp_us,
                frame,
                ..
            } => {
                // Sample frame gerçek veri olduğu için write hatasını üst katmana döndürüyoruz.
                // Bu hata üst katmanda transport recovery/reset kararı aldırabilir.
                // İstatistik sadece başarılı write sonrası artar.
                sink.write_frame(&frame)?;

                self.stats.sample_frames_sent =
                    self.stats.sample_frames_sent.wrapping_add(1);
                self.stats.total_frames_sent =
                    self.stats.total_frames_sent.wrapping_add(1);
                self.stats.last_sample_timestamp_us = timestamp_us;

                Ok(Bno085FirmwareEvent::SampleFrameSent {
                    sequence,
                    timestamp_us,
                })
            }

            RealBno085RvcNodeEvent::ParseError { .. } => {
                // Dış core kendi lokal istatistiğini artırır.
                // Böylece iç node sayaçlarına doğrudan assignment bağımlılığı oluşmaz.
                self.stats.parse_errors = self.stats.parse_errors.wrapping_add(1);

                Ok(Bno085FirmwareEvent::ParseError {
                    error_count: self.stats.parse_errors,
                })
            }
        }
    }

    // Periyodik health tick.
    //
    // UART'tan veri gelmese bile ana firmware loop içinde düzenli çağrılır.
    // Delta-time mantığıyla çalışır:
    //
    // elapsed = timestamp_us - last_health_schedule_timestamp_us
    //
    // timestamp geriye saparsa saturating_sub 0 döndürür ve health bekler.
    // Sink write başarısız olsa bile schedule timestamp güncellenir.
    //
    // Önemli karar:
    // HEALTH frame write hatası fatal değildir. Bu frame düşmüş kabul edilir.
    // Fonksiyon Result::Err döndürmez, HealthFrameDropped event'i döndürür.
    // Böylece ana firmware loop health frame yüzünden kesintiye uğramaz.

    pub fn emit_due_health<S>(
        &mut self,
        uptime_ms: u32,
        timestamp_us: u64,
        sink: &mut S,
    ) -> Result<Bno085FirmwareEvent, S::Error>
    where
        S: HydronomFrameSink,
    {
        if !self.boot_sent {
            return Ok(Bno085FirmwareEvent::Waiting);
        }

        let elapsed_us = timestamp_us.saturating_sub(self.last_health_schedule_timestamp_us);

        if elapsed_us < self.config.health_period_us {
            return Ok(Bno085FirmwareEvent::Waiting);
        }

        // Planlama durumunu I/O'dan önce güncelliyoruz.
        // Sink hata verirse bir sonraki loop tekrar tekrar aynı health tick'e saplanmaz.
        self.last_health_schedule_timestamp_us = timestamp_us;
        self.stats.last_health_schedule_timestamp_us = timestamp_us;

        let frame = self.node.build_health_frame(
            uptime_ms,
            timestamp_us,
        );

        match sink.write_frame(&frame) {
            Ok(()) => {
                self.stats.health_frames_sent =
                    self.stats.health_frames_sent.wrapping_add(1);
                self.stats.total_frames_sent =
                    self.stats.total_frames_sent.wrapping_add(1);
                self.stats.last_health_timestamp_us = timestamp_us;
                self.stats.last_event_timestamp_us = timestamp_us;

                Ok(Bno085FirmwareEvent::HealthFrameSent {
                    timestamp_us,
                })
            }

            Err(_error) => {
                // HEALTH frame düşebilir. Bu tek başına firmware loop'u kesmemeli.
                // Hata istatistiğini artırıyor ve kontrollü event döndürüyoruz.
                self.stats.sink_write_errors =
                    self.stats.sink_write_errors.wrapping_add(1);
                self.stats.health_frames_dropped =
                    self.stats.health_frames_dropped.wrapping_add(1);
                self.stats.last_event_timestamp_us = timestamp_us;

                Ok(Bno085FirmwareEvent::HealthFrameDropped {
                    timestamp_us,
                })
            }
        }
    }

    // Donanım katmanından bus hatası bildirmek için.
    //
    // Örnek:
    // - UART overrun
    // - framing error
    // - DMA error
    // - USB writer timeout
    //
    // Bu fonksiyon frame göndermez, sadece node health durumunu günceller.
    //
    // Gerçek Pico transport adaptörü UART/DMA/USB hatalarını buraya bildirecek.
    // PC self-test şu an çağırmadığı için dead_code uyarısını kapatıyoruz.

    #[allow(dead_code)]
    pub fn record_bus_error(&mut self, error_code: u16) {
        self.node.record_bus_error(error_code);
        self.stats.bus_errors = self.stats.bus_errors.wrapping_add(1);
    }
}