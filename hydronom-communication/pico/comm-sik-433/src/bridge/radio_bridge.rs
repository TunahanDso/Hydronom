use embedded_hal_nb::serial::{Read, Write};
use usbd_serial::SerialPort;

use crate::diagnostics::BridgeCounters;

const RADIO_TX_QUEUE_CAPACITY: usize = 256;

pub struct RadioBridge {
    usb_buffer: [u8; 64],
    radio_tx_queue: [u8; RADIO_TX_QUEUE_CAPACITY],
    radio_tx_start: usize,
    radio_tx_len: usize,
    pub counters: BridgeCounters,
    identity_usb_offset: usize,
    identity_radio_offset: usize,
}

impl RadioBridge {
    pub const fn new() -> Self {
        Self {
            usb_buffer: [0u8; 64],
            radio_tx_queue: [0u8; RADIO_TX_QUEUE_CAPACITY],
            radio_tx_start: 0,
            radio_tx_len: 0,
            counters: BridgeCounters {
                usb_to_radio_bytes: 0,
                radio_to_usb_bytes: 0,
                usb_read_events: 0,
                radio_read_events: 0,
                usb_write_errors: 0,
                radio_write_errors: 0,
            },
            identity_usb_offset: 0,
            identity_radio_offset: 0,
        }
    }

    pub fn send_identity_once<USB>(
        &mut self,
        usb_serial: &mut SerialPort<USB>,
        identity_line: &[u8],
    )
    where
        USB: usb_device::bus::UsbBus,
    {
        if self.identity_usb_offset < identity_line.len() {
            match usb_serial.write(&identity_line[self.identity_usb_offset..]) {
                Ok(count) if count > 0 => {
                    self.identity_usb_offset =
                        self.identity_usb_offset.saturating_add(count);
                }
                Ok(_) => {}
                Err(_) => {
                    self.counters.add_usb_write_error();
                }
            }
        }

        if self.identity_radio_offset < identity_line.len() {
            let queued = self.queue_radio_bytes(&identity_line[self.identity_radio_offset..]);

            if queued > 0 {
                self.identity_radio_offset =
                    self.identity_radio_offset.saturating_add(queued);
            }
        }
    }

    pub fn pump_usb_to_radio<USB>(&mut self, usb_serial: &mut SerialPort<USB>)
    where
        USB: usb_device::bus::UsbBus,
    {
        match usb_serial.read(&mut self.usb_buffer) {
            Ok(count) if count > 0 => {
                let copied = self.queue_radio_bytes_from_usb_buffer(count);

                if copied > 0 {
                    self.counters.add_usb_to_radio(copied);
                }

                if copied < count {
                    self.counters.add_radio_write_error();
                }
            }
            _ => {}
        }
    }

    pub fn flush_radio_tx_limited<UART>(
        &mut self,
        uart: &mut UART,
        max_bytes_per_tick: usize,
    )
    where
        UART: Write<u8>,
    {
        let mut written = 0usize;

        while self.radio_tx_len > 0 && written < max_bytes_per_tick {
            let byte = self.radio_tx_queue[self.radio_tx_start];

            match uart.write(byte) {
                Ok(()) => {
                    self.radio_tx_start += 1;
                    self.radio_tx_len -= 1;
                    written += 1;

                    if self.radio_tx_len == 0 {
                        self.radio_tx_start = 0;
                    }
                }
                Err(nb::Error::WouldBlock) => {
                    break;
                }
                Err(_) => {
                    self.counters.add_radio_write_error();

                    self.radio_tx_start += 1;
                    self.radio_tx_len -= 1;

                    if self.radio_tx_len == 0 {
                        self.radio_tx_start = 0;
                    }

                    break;
                }
            }
        }

        if self.radio_tx_len == 0 {
            let _ = uart.flush();
        }
    }

    pub fn pump_radio_to_usb_limited<USB, UART>(
        &mut self,
        usb_serial: &mut SerialPort<USB>,
        uart: &mut UART,
        max_bytes_per_tick: usize,
    )
    where
        USB: usb_device::bus::UsbBus,
        UART: Read<u8>,
    {
        let mut local_buf = [0u8; 64];
        let limit = min_usize(max_bytes_per_tick, local_buf.len());
        let mut moved = 0usize;

        while moved < limit {
            match uart.read() {
                Ok(byte) => {
                    local_buf[moved] = byte;
                    moved += 1;
                }
                Err(nb::Error::WouldBlock) => {
                    break;
                }
                Err(_) => {
                    break;
                }
            }
        }

        if moved == 0 {
            return;
        }

        match usb_serial.write(&local_buf[..moved]) {
            Ok(written) if written > 0 => {
                self.counters.add_radio_to_usb(written);

                if written < moved {
                    self.counters.add_usb_write_error();
                }
            }
            Ok(_) => {}
            Err(_) => {
                self.counters.add_usb_write_error();
            }
        }
    }

    fn queue_radio_bytes_from_usb_buffer(&mut self, count: usize) -> usize {
        let mut copied = 0usize;

        while copied < count {
            if !self.radio_tx_has_space() {
                self.compact_radio_tx_queue();

                if !self.radio_tx_has_space() {
                    break;
                }
            }

            let index = self.radio_tx_start + self.radio_tx_len;
            self.radio_tx_queue[index] = self.usb_buffer[copied];
            self.radio_tx_len += 1;
            copied += 1;
        }

        copied
    }

    fn queue_radio_bytes(&mut self, bytes: &[u8]) -> usize {
        let mut copied = 0usize;

        while copied < bytes.len() {
            if !self.radio_tx_has_space() {
                self.compact_radio_tx_queue();

                if !self.radio_tx_has_space() {
                    self.counters.add_radio_write_error();
                    break;
                }
            }

            let index = self.radio_tx_start + self.radio_tx_len;
            self.radio_tx_queue[index] = bytes[copied];
            self.radio_tx_len += 1;
            copied += 1;
        }

        copied
    }

    fn radio_tx_has_space(&self) -> bool {
        self.radio_tx_start + self.radio_tx_len < self.radio_tx_queue.len()
    }

    fn compact_radio_tx_queue(&mut self) {
        if self.radio_tx_start == 0 {
            return;
        }

        if self.radio_tx_len == 0 {
            self.radio_tx_start = 0;
            return;
        }

        let mut i = 0usize;

        while i < self.radio_tx_len {
            self.radio_tx_queue[i] = self.radio_tx_queue[self.radio_tx_start + i];
            i += 1;
        }

        self.radio_tx_start = 0;
    }
}

const fn min_usize(a: usize, b: usize) -> usize {
    if a < b {
        a
    } else {
        b
    }
}