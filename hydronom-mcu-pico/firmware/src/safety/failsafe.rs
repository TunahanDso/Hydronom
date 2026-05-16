// Hydronom MCU - Failsafe
//
// GÜVENLİK:
// Üst sistem, Pi, Jetson, PC veya C# Runtime donarsa Pico son komutu
// sonsuza kadar uygulamaz. Komut yaşı timeout değerini geçerse safe output'a döner.

#[derive(Clone, Copy, Debug, defmt::Format, PartialEq, Eq)]
pub enum FailsafeReason {
    None,
    CommandTimeout,
    HeartbeatTimeout,
    EmergencyStop,
    InvalidCommand,
    ProtocolError,
}

#[derive(Clone, Copy, Debug)]
pub struct FailsafeController {
    active: bool,
    reason: FailsafeReason,
    last_command_ms: u64,
    last_heartbeat_ms: u64,
    command_timeout_ms: u64,
    heartbeat_timeout_ms: u64,
}

impl FailsafeController {
    pub const fn new(command_timeout_ms: u64, heartbeat_timeout_ms: u64) -> Self {
        Self {
            active: false,
            reason: FailsafeReason::None,
            last_command_ms: 0,
            last_heartbeat_ms: 0,
            command_timeout_ms,
            heartbeat_timeout_ms,
        }
    }

    pub fn active(&self) -> bool {
        self.active
    }

    pub fn reason(&self) -> FailsafeReason {
        self.reason
    }

    pub fn last_command_ms(&self) -> u64 {
        self.last_command_ms
    }

    pub fn last_heartbeat_ms(&self) -> u64 {
        self.last_heartbeat_ms
    }

    pub fn mark_command(&mut self, now_ms: u64) {
        self.last_command_ms = now_ms;

        if self.reason == FailsafeReason::CommandTimeout {
            self.clear();
        }
    }

    pub fn mark_heartbeat(&mut self, now_ms: u64) {
        self.last_heartbeat_ms = now_ms;

        if self.reason == FailsafeReason::HeartbeatTimeout {
            self.clear();
        }
    }

    pub fn trigger(&mut self, reason: FailsafeReason) {
        self.active = true;
        self.reason = reason;
    }

    pub fn clear(&mut self) {
        self.active = false;
        self.reason = FailsafeReason::None;
    }

    pub fn tick(&mut self, now_ms: u64) {
        if self.active {
            return;
        }

        if self.last_command_ms > 0 {
            let age = now_ms.saturating_sub(self.last_command_ms);
            if age > self.command_timeout_ms {
                self.trigger(FailsafeReason::CommandTimeout);
                return;
            }
        }

        if self.last_heartbeat_ms > 0 {
            let age = now_ms.saturating_sub(self.last_heartbeat_ms);
            if age > self.heartbeat_timeout_ms {
                self.trigger(FailsafeReason::HeartbeatTimeout);
            }
        }
    }
}