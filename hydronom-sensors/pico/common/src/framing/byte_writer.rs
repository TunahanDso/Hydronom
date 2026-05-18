#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ByteWriteError {
    BufferTooSmall,
}

pub struct ByteWriter<'a> {
    buffer: &'a mut [u8],
    position: usize,
}

impl<'a> ByteWriter<'a> {
    pub fn new(buffer: &'a mut [u8]) -> Self {
        Self {
            buffer,
            position: 0,
        }
    }

    pub fn position(&self) -> usize {
        self.position
    }

    pub fn as_written(&self) -> &[u8] {
        &self.buffer[..self.position]
    }

    pub fn write_u8(&mut self, value: u8) -> Result<(), ByteWriteError> {
        if self.position + 1 > self.buffer.len() {
            return Err(ByteWriteError::BufferTooSmall);
        }

        self.buffer[self.position] = value;
        self.position += 1;
        Ok(())
    }

    pub fn write_u16_le(&mut self, value: u16) -> Result<(), ByteWriteError> {
        self.write_bytes(&value.to_le_bytes())
    }

    pub fn write_u32_le(&mut self, value: u32) -> Result<(), ByteWriteError> {
        self.write_bytes(&value.to_le_bytes())
    }

    pub fn write_u64_le(&mut self, value: u64) -> Result<(), ByteWriteError> {
        self.write_bytes(&value.to_le_bytes())
    }

    pub fn write_f32_le(&mut self, value: f32) -> Result<(), ByteWriteError> {
        self.write_bytes(&value.to_le_bytes())
    }

    pub fn write_bytes(&mut self, bytes: &[u8]) -> Result<(), ByteWriteError> {
        if self.position + bytes.len() > self.buffer.len() {
            return Err(ByteWriteError::BufferTooSmall);
        }

        let end = self.position + bytes.len();
        self.buffer[self.position..end].copy_from_slice(bytes);
        self.position = end;
        Ok(())
    }

    pub fn write_fixed_ascii<const N: usize>(
        &mut self,
        value: &str,
    ) -> Result<(), ByteWriteError> {
        let bytes = value.as_bytes();
        let copy_len = if bytes.len() < N { bytes.len() } else { N };

        if self.position + N > self.buffer.len() {
            return Err(ByteWriteError::BufferTooSmall);
        }

        let start = self.position;
        let end = start + N;

        for i in start..end {
            self.buffer[i] = 0;
        }

        self.buffer[start..start + copy_len].copy_from_slice(&bytes[..copy_len]);
        self.position = end;

        Ok(())
    }
}