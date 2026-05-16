// Hydronom MCU - Defaults
//
// Bu değerler yarışma güvenliği için muhafazakâr tutulur.
// Daha sonra flash config / runtime config ile değiştirilebilir.

use super::vehicle_profile::VehicleProfile;

pub const FIRMWARE_NAME: &str = "Hydronom MCU Pico Firmware";
pub const FIRMWARE_VERSION: &str = "0.1.0";

pub const COMMAND_TIMEOUT_MS: u64 = 300;
pub const HEARTBEAT_TIMEOUT_MS: u64 = 500;
pub const TELEMETRY_PERIOD_MS: u64 = 100;

pub const COMMAND_MIN: i16 = -1000;
pub const COMMAND_MAX: i16 = 1000;

pub const DEFAULT_VEHICLE_PROFILE: VehicleProfile = VehicleProfile::generic_surface_4esc();