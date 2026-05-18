use crate::bno085::rvc_packet::{
    Bno085RvcPacket,
    Bno085RvcParseError,
    BNO085_RVC_HEADER_0,
    BNO085_RVC_HEADER_1,
    BNO085_RVC_PACKET_LEN,
};

// BNO085 UART-RVC stream reader.
//
// Gerçek Pico tarafında UART RX'ten gelen byte'lar tek tek push_byte()
// fonksiyonuna verilecek.
//
// Reader şunları yapar:
// - 0xAA 0xAA header arar.
// - Header yakalayınca 19 byte paketi toplar.
// - Paket tamamlanınca checksum doğrular.
// - Başarılıysa Bno085RvcPacket döndürür.
// - Hatalı paketlerde tekrar senkron aramaya döner.
//
// Bu yapı allocation kullanmaz, no_std uyumludur.

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Bno085RvcStreamState {
    SearchingHeader0,
    SearchingHeader1,
    Collecting,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum Bno085RvcStreamEvent {
    Waiting,
    Packet(Bno085RvcPacket),
    ParseError(Bno085RvcParseError),
}

#[derive(Clone, Copy, Debug)]
pub struct Bno085RvcStreamReader {
    state: Bno085RvcStreamState,
    buffer: [u8; BNO085_RVC_PACKET_LEN],
    position: usize,

    packets_ok: u32,
    parse_errors: u32,
    skipped_bytes: u32,
    resync_count: u32,
}

impl Bno085RvcStreamReader {
    pub const fn new() -> Self {
        Self {
            state: Bno085RvcStreamState::SearchingHeader0,
            buffer: [0u8; BNO085_RVC_PACKET_LEN],
            position: 0,

            packets_ok: 0,
            parse_errors: 0,
            skipped_bytes: 0,
            resync_count: 0,
        }
    }

    pub const fn state(&self) -> Bno085RvcStreamState {
        self.state
    }

    pub const fn packets_ok(&self) -> u32 {
        self.packets_ok
    }

    pub const fn parse_errors(&self) -> u32 {
        self.parse_errors
    }

    pub const fn skipped_bytes(&self) -> u32 {
        self.skipped_bytes
    }

    pub const fn resync_count(&self) -> u32 {
        self.resync_count
    }

    pub fn reset(&mut self) {
        self.state = Bno085RvcStreamState::SearchingHeader0;
        self.position = 0;
        self.buffer = [0u8; BNO085_RVC_PACKET_LEN];
    }

    pub fn push_byte(&mut self, byte: u8) -> Bno085RvcStreamEvent {
        match self.state {
            Bno085RvcStreamState::SearchingHeader0 => self.handle_searching_header0(byte),
            Bno085RvcStreamState::SearchingHeader1 => self.handle_searching_header1(byte),
            Bno085RvcStreamState::Collecting => self.handle_collecting(byte),
        }
    }

    fn handle_searching_header0(&mut self, byte: u8) -> Bno085RvcStreamEvent {
        if byte == BNO085_RVC_HEADER_0 {
            self.buffer[0] = byte;
            self.position = 1;
            self.state = Bno085RvcStreamState::SearchingHeader1;
        } else {
            self.skipped_bytes = self.skipped_bytes.wrapping_add(1);
        }

        Bno085RvcStreamEvent::Waiting
    }

    fn handle_searching_header1(&mut self, byte: u8) -> Bno085RvcStreamEvent {
        if byte == BNO085_RVC_HEADER_1 {
            self.buffer[1] = byte;
            self.position = 2;
            self.state = Bno085RvcStreamState::Collecting;
            return Bno085RvcStreamEvent::Waiting;
        }

        // 0xAA 0xAA beklerken tekrar 0xAA geldiyse ikinci byte'ı
        // yeni header başlangıcı kabul ederiz. Böylece AA AA AA gibi
        // dizilerde senkron kaçmaz.
        if byte == BNO085_RVC_HEADER_0 {
            self.buffer[0] = byte;
            self.position = 1;
            self.state = Bno085RvcStreamState::SearchingHeader1;
            self.resync_count = self.resync_count.wrapping_add(1);
        } else {
            self.skipped_bytes = self.skipped_bytes.wrapping_add(1);
            self.position = 0;
            self.state = Bno085RvcStreamState::SearchingHeader0;
        }

        Bno085RvcStreamEvent::Waiting
    }

    fn handle_collecting(&mut self, byte: u8) -> Bno085RvcStreamEvent {
        if self.position >= BNO085_RVC_PACKET_LEN {
            self.reset();
            self.parse_errors = self.parse_errors.wrapping_add(1);
            return Bno085RvcStreamEvent::ParseError(Bno085RvcParseError::TooShort);
        }

        self.buffer[self.position] = byte;
        self.position += 1;

        if self.position < BNO085_RVC_PACKET_LEN {
            return Bno085RvcStreamEvent::Waiting;
        }

        let packet_bytes = self.buffer;

        match Bno085RvcPacket::parse(&packet_bytes) {
            Ok(packet) => {
                self.packets_ok = self.packets_ok.wrapping_add(1);
                self.reset();
                Bno085RvcStreamEvent::Packet(packet)
            }
            Err(error) => {
                self.parse_errors = self.parse_errors.wrapping_add(1);

                // Hatalı paket bitmiş olabilir; ama son byte yeni header başlangıcı
                // olabilir. Bu küçük resync hilesi UART gürültüsünde toparlanmayı
                // kolaylaştırır.
                let last = packet_bytes[BNO085_RVC_PACKET_LEN - 1];
                self.reset();

                if last == BNO085_RVC_HEADER_0 {
                    self.buffer[0] = last;
                    self.position = 1;
                    self.state = Bno085RvcStreamState::SearchingHeader1;
                    self.resync_count = self.resync_count.wrapping_add(1);
                }

                Bno085RvcStreamEvent::ParseError(error)
            }
        }
    }
}