#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ByteReadError {
    UnexpectedEnd,
}

pub struct ByteReader<'a> {
    buffer: &'a [u8],
    position: usize,
}

impl<'a> ByteReader<'a> {
    pub const fn new(buffer: &'a [u8]) -> Self {
        Self {
            buffer,
            position: 0,
        }
    }

    pub const fn position(&self) -> usize {
        self.position
    }

    pub const fn remaining(&self) -> usize {
        self.buffer.len().saturating_sub(self.position)
    }

    pub fn read_u8(&mut self) -> Result<u8, ByteReadError> {
        if self.position + 1 > self.buffer.len() {
            return Err(ByteReadError::UnexpectedEnd);
        }

        let value = self.buffer[self.position];
        self.position += 1;
        Ok(value)
    }

    pub fn read_u16_le(&mut self) -> Result<u16, ByteReadError> {
        let bytes = self.read_array::<2>()?;
        Ok(u16::from_le_bytes(bytes))
    }

    pub fn read_u32_le(&mut self) -> Result<u32, ByteReadError> {
        let bytes = self.read_array::<4>()?;
        Ok(u32::from_le_bytes(bytes))
    }

    pub fn read_u64_le(&mut self) -> Result<u64, ByteReadError> {
        let bytes = self.read_array::<8>()?;
        Ok(u64::from_le_bytes(bytes))
    }

    pub fn read_f32_le(&mut self) -> Result<f32, ByteReadError> {
        let bytes = self.read_array::<4>()?;
        Ok(f32::from_le_bytes(bytes))
    }

    pub fn read_bytes(&mut self, len: usize) -> Result<&'a [u8], ByteReadError> {
        if self.position + len > self.buffer.len() {
            return Err(ByteReadError::UnexpectedEnd);
        }

        let start = self.position;
        let end = start + len;
        self.position = end;

        Ok(&self.buffer[start..end])
    }

    fn read_array<const N: usize>(&mut self) -> Result<[u8; N], ByteReadError> {
        let bytes = self.read_bytes(N)?;
        let mut result = [0u8; N];
        result.copy_from_slice(bytes);
        Ok(result)
    }
}