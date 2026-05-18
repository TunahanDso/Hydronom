use std::fs::File;
use std::io::{self, BufWriter, Write};
use std::path::Path;

use crate::runtime::stream_runtime::Bno085StreamEvent;

#[derive(Debug)]
pub struct BinaryStreamWriter {
    writer: BufWriter<File>,
    bytes_written: u64,
    frames_written: u32,
}

impl BinaryStreamWriter {
    pub fn create<P: AsRef<Path>>(path: P) -> io::Result<Self> {
        let file = File::create(path)?;

        Ok(Self {
            writer: BufWriter::new(file),
            bytes_written: 0,
            frames_written: 0,
        })
    }

    pub fn write_event(&mut self, event: &Bno085StreamEvent) -> io::Result<()> {
        let frame = event.frame_bytes();

        self.writer.write_all(frame)?;
        self.bytes_written = self.bytes_written.saturating_add(frame.len() as u64);
        self.frames_written = self.frames_written.saturating_add(1);

        Ok(())
    }

    pub fn flush(&mut self) -> io::Result<()> {
        self.writer.flush()
    }

    pub const fn bytes_written(&self) -> u64 {
        self.bytes_written
    }

    pub const fn frames_written(&self) -> u32 {
        self.frames_written
    }
}