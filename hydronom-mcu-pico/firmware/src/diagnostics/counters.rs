// Hydronom MCU - Diagnostics Counters
//
// MİMARİ NOT:
// MCU tarafında sadece "çalışıyor" demek yetmez.
// Kaç komut geldi, kaç CRC hatası oldu, kaç clamp yaşandı,
// kaç failsafe tetiklendi gibi bilgiler yarışma ve saha testlerinde çok değerlidir.

#[derive(Clone, Copy, Debug, Default)]
pub struct DiagnosticsCounters {
    pub received_frames: u32,
    pub parsed_commands: u32,
    pub rejected_frames: u32,
    pub crc_errors: u32,
    pub protocol_errors: u32,
    pub applied_commands: u32,
    pub rejected_commands: u32,
    pub failsafe_events: u32,
    pub emergency_stop_events: u32,
    pub reverse_clamp_events: u32,
    pub range_clamp_events: u32,
}

impl DiagnosticsCounters {
    pub const fn new() -> Self {
        Self {
            received_frames: 0,
            parsed_commands: 0,
            rejected_frames: 0,
            crc_errors: 0,
            protocol_errors: 0,
            applied_commands: 0,
            rejected_commands: 0,
            failsafe_events: 0,
            emergency_stop_events: 0,
            reverse_clamp_events: 0,
            range_clamp_events: 0,
        }
    }

    pub fn mark_received_frame(&mut self) {
        self.received_frames = self.received_frames.saturating_add(1);
    }

    pub fn mark_parsed_command(&mut self) {
        self.parsed_commands = self.parsed_commands.saturating_add(1);
    }

    pub fn mark_rejected_frame(&mut self) {
        self.rejected_frames = self.rejected_frames.saturating_add(1);
    }

    pub fn mark_crc_error(&mut self) {
        self.crc_errors = self.crc_errors.saturating_add(1);
        self.mark_rejected_frame();
    }

    pub fn mark_protocol_error(&mut self) {
        self.protocol_errors = self.protocol_errors.saturating_add(1);
        self.mark_rejected_frame();
    }

    pub fn mark_applied_command(&mut self) {
        self.applied_commands = self.applied_commands.saturating_add(1);
    }

    pub fn mark_rejected_command(&mut self) {
        self.rejected_commands = self.rejected_commands.saturating_add(1);
    }

    pub fn mark_failsafe_event(&mut self) {
        self.failsafe_events = self.failsafe_events.saturating_add(1);
    }

    pub fn mark_emergency_stop_event(&mut self) {
        self.emergency_stop_events = self.emergency_stop_events.saturating_add(1);
    }

    pub fn mark_reverse_clamp_event(&mut self) {
        self.reverse_clamp_events = self.reverse_clamp_events.saturating_add(1);
    }

    pub fn mark_range_clamp_event(&mut self) {
        self.range_clamp_events = self.range_clamp_events.saturating_add(1);
    }
}