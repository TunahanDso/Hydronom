#![no_std]
#![no_main]

// Hydronom MCU Pico Firmware
//
// MİMARİ HEDEF:
// Bu firmware bir "motor döndürme kodu" değildir.
// Hydronom'un platform bağımsız gömülü aktüasyon düğümüdür.
//
// Pico karar vermez.
// Pico görev bilmez.
// Pico rota planlamaz.
// Pico yalnızca üst seviye Hydronom Runtime tarafından gönderilen
// güvenli actuator komutlarını uygular ve kendi yerel güvenlik kapılarını korur.

use defmt::*;
use defmt_rtt as _;
use panic_probe as _;

mod actuators;
mod app;
mod board;
mod config;
mod diagnostics;
mod protocol;
mod safety;
mod transport;

use actuators::output_bank::OutputBank;
use actuators::pwm_esc::{esc_pwm_config, prepare_pwm_esc_output, PwmEscHardware4};
use app::runtime::HydronomMcuRuntime;
use app::scheduler::PeriodicTask;
use board::pico2w::DEFAULT_BOARD_PROFILE;
use board::pins::{default_pin_for_actuator, default_pin_sum};
use config::defaults::{FIRMWARE_NAME, FIRMWARE_VERSION, TELEMETRY_PERIOD_MS};
use config::vehicle_profile::VehicleFamily;
use embassy_rp::pwm::Pwm;
use protocol::crc::verify_crc16_ccitt_false;
use protocol::frame::{FrameType, HydronomFrame, HYDRONOM_MAX_FRAME_LEN};
use safety::failsafe::FailsafeReason;
use safety::watchdog::WatchdogStatus;

#[embassy_executor::main]
async fn main(_spawner: embassy_executor::Spawner) {
    // GÜVENLİK:
    // İlk boot aşamasında bütün PWM çıkışları 1000 us safe seviyesinde başlar.
    let p = embassy_rp::init(Default::default());

    let pwm01 = Pwm::new_output_ab(
        p.PWM_SLICE1,
        p.PIN_2,
        p.PIN_3,
        esc_pwm_config(1000, 1000),
    );

    let pwm23 = Pwm::new_output_ab(
        p.PWM_SLICE2,
        p.PIN_4,
        p.PIN_5,
        esc_pwm_config(1000, 1000),
    );

    let mut pwm_hardware = PwmEscHardware4::new(pwm01, pwm23);
    pwm_hardware.apply_safe();

    let board_act0_pin = DEFAULT_BOARD_PROFILE.actuator_pin(0).unwrap_or(255);
    let board_pin_count = DEFAULT_BOARD_PROFILE.actuator_pin_count();

    info!("BOOT {} v{}", FIRMWARE_NAME, FIRMWARE_VERSION);
    info!("ROLE Embedded Actuation Node");
    info!(
        "BOARD kind={} usb_cdc={} pwm_hz={} pin_count={} act0_gpio={} pin_sum={}",
        DEFAULT_BOARD_PROFILE.kind as u8,
        DEFAULT_BOARD_PROFILE.usb_cdc_enabled,
        DEFAULT_BOARD_PROFILE.default_pwm_frequency_hz,
        board_pin_count,
        board_act0_pin,
        default_pin_sum()
    );
    info!("SAFETY initial_state=DISARMED outputs=SAFE pwm=[1000,1000,1000,1000]");

    let mut runtime = HydronomMcuRuntime::new();
    let mut telemetry_task = PeriodicTask::new(TELEMETRY_PERIOD_MS);
    let mut output_bank = OutputBank::new();

    // MİMARİ NOT:
    // Bu watchdog şimdilik yazılımsal durum takibi yapar.
    // Gerçek hardware watchdog daha sonra ayrı katmanda etkinleştirilecek.
    let mut watchdog = WatchdogStatus::new(1000);

    // MİMARİ TEST:
    // USB CDC henüz bağlı değilken binary frame -> parser -> command -> state akışını
    // içeriden test ediyoruz. Bu test PWM hattına da güvenli şekilde yansır:
    // self-test sonunda DISARM komutu geldiği için çıkışlar safe değerde kalır.
    run_boot_protocol_self_test(&mut runtime);

    loop {
        let now_ms = embassy_time::Instant::now().as_millis();

        runtime.tick(now_ms);
        output_bank.refresh_from_state(&runtime.state);

        // GERÇEK DONANIM ÇIKIŞI:
        // OutputBank güvenlik durumunu zaten hesaba katar.
        // DISARM / failsafe sırasında bu çağrı 1000 us safe PWM uygular.
        pwm_hardware.apply_output_bank(&output_bank);

        watchdog.pet(now_ms);
        watchdog.tick(now_ms);

        if telemetry_task.should_run(now_ms) {
            let telemetry = runtime.state.telemetry(now_ms);
            let health = runtime.health();
            let first_actuator = telemetry.actuators[0];
            let first_output = output_bank.first();
            let first_pwm_output = prepare_pwm_esc_output(first_output);
            let last_pwm = pwm_hardware.last_pwm_us();

            let last_command_age_ms = age_or_zero(now_ms, runtime.state.failsafe.last_command_ms());
            let last_heartbeat_age_ms =
                age_or_zero(now_ms, runtime.state.failsafe.last_heartbeat_ms());

            let vehicle_family_code = vehicle_family_to_code(runtime.state.vehicle_profile.family);
            let health_failsafe_code = failsafe_reason_to_code(health.failsafe_reason);
            let default_act0_pin = default_pin_for_actuator(0).unwrap_or(255);
            let board_act0_pin = DEFAULT_BOARD_PROFILE.actuator_pin(0).unwrap_or(255);

            info!(
                "TEL uptime_ms={} seq={} vehicle_family={} armed={:?} failsafe={} reason={:?} actuator_count={} watchdog={:?}",
                telemetry.firmware_uptime_ms,
                telemetry.last_seq,
                vehicle_family_code,
                telemetry.arm_state,
                telemetry.failsafe_active,
                telemetry.failsafe_reason,
                telemetry.actuator_count,
                watchdog.state
            );

            info!(
                "ACT0 id={} requested={} limited={} pwm_us={} reverse_clamped={} range_clamped={}",
                first_actuator.actuator_id,
                first_actuator.requested,
                first_actuator.limited,
                first_actuator.pwm_us,
                first_actuator.reverse_clamped,
                first_actuator.range_clamped
            );

            info!(
                "OUT0 id={} gpio={} default_gpio={} board_gpio={} pwm_us={} apply_result={}",
                first_pwm_output.actuator_id,
                first_pwm_output.gpio_pin,
                default_act0_pin,
                board_act0_pin,
                first_pwm_output.pwm_us,
                first_pwm_output.result as u8
            );

            info!(
                "PWM last=[{},{},{},{}]",
                last_pwm[0],
                last_pwm[1],
                last_pwm[2],
                last_pwm[3]
            );

            info!(
                "DIAG rx={} parsed={} rejected_frame={} crc={} proto={} applied={} rejected_cmd={} failsafe_events={} estop_events={} reverse_clamps={} range_clamps={} cmd_age_ms={} hb_age_ms={}",
                runtime.counters.received_frames,
                runtime.counters.parsed_commands,
                runtime.counters.rejected_frames,
                runtime.counters.crc_errors,
                runtime.counters.protocol_errors,
                runtime.counters.applied_commands,
                runtime.counters.rejected_commands,
                runtime.counters.failsafe_events,
                runtime.counters.emergency_stop_events,
                runtime.counters.reverse_clamp_events,
                runtime.counters.range_clamp_events,
                last_command_age_ms,
                last_heartbeat_age_ms
            );

            info!(
                "HEALTH level_code={} health_code={} crc_errors={} protocol_errors={} failsafe_reason_code={}",
                health.level as u8,
                health.code as u8,
                health.crc_errors,
                health.protocol_errors,
                health_failsafe_code
            );
        }

        embassy_time::Timer::after_millis(10).await;
    }
}

fn run_boot_protocol_self_test(runtime: &mut HydronomMcuRuntime) {
    let mut buffer = [0u8; HYDRONOM_MAX_FRAME_LEN];

    encode_and_process(runtime, HydronomFrame::empty(FrameType::Heartbeat, 1), &mut buffer, 0);
    encode_and_process(runtime, HydronomFrame::empty(FrameType::Arm, 2), &mut buffer, 1);

    // ACT_BATCH payload formatı:
    // [count, id0, value0_lo, value0_hi, id1, value1_lo, value1_hi, ...]
    //
    // Burada tek yönlü ESC için negatif değer de gönderiyoruz.
    // Amaç reverse clamp güvenlik yolunun çalıştığını görmek.
    let mut payload = [0u8; 13];
    payload[0] = 4;

    write_setpoint(&mut payload, 0, 0, 120);
    write_setpoint(&mut payload, 1, 1, 120);
    write_setpoint(&mut payload, 2, 2, -50);
    write_setpoint(&mut payload, 3, 3, 90);

    if let Ok(frame) = HydronomFrame::with_payload(FrameType::ActuatorBatch, 3, &payload) {
        encode_and_process(runtime, frame, &mut buffer, 2);
    }

    encode_and_process(runtime, HydronomFrame::empty(FrameType::Disarm, 4), &mut buffer, 3);

    // CRC yardımcı doğrulama yolunu da boot self-test içinde canlı tutuyoruz.
    // Bu gerçek haberleşme gelmeden önce protokol bütünlük fonksiyonunun
    // build tarafından dışarıda kalmasını engeller.
    let _crc_ok = verify_crc16_ccitt_false(&[0x48, 0x59, 0x01], 0xFFFF);
}

fn encode_and_process(
    runtime: &mut HydronomMcuRuntime,
    frame: HydronomFrame,
    buffer: &mut [u8; HYDRONOM_MAX_FRAME_LEN],
    now_ms: u64,
) {
    if let Ok(len) = frame.encode(buffer) {
        let _ = runtime.process_frame_bytes(&buffer[..len], now_ms);
    }
}

fn write_setpoint(payload: &mut [u8; 13], index: usize, actuator_id: u8, value: i16) {
    let offset = 1 + index * 3;
    let bytes = value.to_le_bytes();

    payload[offset] = actuator_id;
    payload[offset + 1] = bytes[0];
    payload[offset + 2] = bytes[1];
}

fn age_or_zero(now_ms: u64, last_ms: u64) -> u64 {
    if last_ms == 0 {
        return 0;
    }

    now_ms.saturating_sub(last_ms)
}

fn vehicle_family_to_code(family: VehicleFamily) -> u8 {
    match family {
        VehicleFamily::SurfaceVessel => 1,
        VehicleFamily::UnderwaterVehicle => 2,
        VehicleFamily::UnderwaterRocket => 3,
        VehicleFamily::AerialVehicle => 4,
        VehicleFamily::GroundVehicle => 5,
        VehicleFamily::Generic => 255,
    }
}

fn failsafe_reason_to_code(reason: FailsafeReason) -> u8 {
    match reason {
        FailsafeReason::None => 0,
        FailsafeReason::CommandTimeout => 1,
        FailsafeReason::HeartbeatTimeout => 2,
        FailsafeReason::EmergencyStop => 3,
        FailsafeReason::InvalidCommand => 4,
        FailsafeReason::ProtocolError => 5,
    }
}