// Hydronom MCU - Scheduler
//
// MİMARİ NOT:
// Pico tarafında da her işi tek karmaşık loop'a yığmayacağız.
// USB okuma, safety tick, telemetry ve actuator apply işleri zamanla ayrılacak.
//
// Şimdilik basit periyot yardımcıları burada duruyor.

#[derive(Clone, Copy, Debug)]
pub struct PeriodicTask {
    period_ms: u64,
    last_run_ms: u64,
}

impl PeriodicTask {
    pub const fn new(period_ms: u64) -> Self {
        Self {
            period_ms,
            last_run_ms: 0,
        }
    }

    pub fn should_run(&mut self, now_ms: u64) -> bool {
        if now_ms.saturating_sub(self.last_run_ms) >= self.period_ms {
            self.last_run_ms = now_ms;
            return true;
        }

        false
    }
}