#[derive(Clone, Copy, Debug, Default)]
pub struct BridgeCounters {
    pub usb_to_radio_bytes: u32,
    pub radio_to_usb_bytes: u32,
    pub usb_read_events: u32,
    pub radio_read_events: u32,
    pub usb_write_errors: u32,
    pub radio_write_errors: u32,
}

impl BridgeCounters {
    pub fn add_usb_to_radio(&mut self, count: usize) {
        self.usb_to_radio_bytes = self.usb_to_radio_bytes.saturating_add(count as u32);
        self.usb_read_events = self.usb_read_events.saturating_add(1);
    }

    pub fn add_radio_to_usb(&mut self, count: usize) {
        self.radio_to_usb_bytes = self.radio_to_usb_bytes.saturating_add(count as u32);
        self.radio_read_events = self.radio_read_events.saturating_add(1);
    }

    pub fn add_usb_write_error(&mut self) {
        self.usb_write_errors = self.usb_write_errors.saturating_add(1);
    }

    pub fn add_radio_write_error(&mut self) {
        self.radio_write_errors = self.radio_write_errors.saturating_add(1);
    }
}
