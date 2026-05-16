// Hydronom MCU - Watchdog Model
//
// NOT:
// Gerçek hardware watchdog daha sonra eklenecek.
// Bu dosya şimdilik runtime-level watchdog durumunu temsil eder.
//
// MİMARİ NOT:
// Watchdog iki seviyeli düşünülür:
// 1. Runtime-level watchdog: firmware döngüsü, haberleşme ve state akışı izlenir.
// 2. Hardware watchdog: MCU gerçekten kilitlenirse donanımsal reset üretir.
//
// Bu dosya şu an birinci seviyeyi temsil eder.

#[derive(Clone, Copy, Debug, defmt::Format, PartialEq, Eq)]
pub enum WatchdogState {
    Healthy,
    Warning,
    Expired,
}

#[derive(Clone, Copy, Debug)]
pub struct WatchdogStatus {
    pub state: WatchdogState,
    pub last_pet_ms: u64,
    pub timeout_ms: u64,
}

impl WatchdogStatus {
    pub const fn new(timeout_ms: u64) -> Self {
        Self {
            state: WatchdogState::Healthy,
            last_pet_ms: 0,
            timeout_ms,
        }
    }

    pub fn pet(&mut self, now_ms: u64) {
        self.last_pet_ms = now_ms;
        self.state = WatchdogState::Healthy;
    }

    pub fn tick(&mut self, now_ms: u64) {
        let age = now_ms.saturating_sub(self.last_pet_ms);

        if age > self.timeout_ms {
            self.state = WatchdogState::Expired;
        } else if age > self.timeout_ms / 2 {
            self.state = WatchdogState::Warning;
        } else {
            self.state = WatchdogState::Healthy;
        }
    }
}