#![no_std]
#![no_main]

// Hydronom Communication Pico Node - SiK 433
//
// Bu firmware karar vermez.
// Gorev bilmez.
// Sadece haberlesme donanimini Hydronom'a tak-cikar node olarak temsil eder.
//
// Saha gorevi:
// Host USB CDC <-> Pico 2 W <-> UART0 GP0/GP1 <-> SiK Telemetry Radio
//
// Net baglanti:
// Pico GP0 / UART0 TX -> SiK RX
// SiK TX              -> Pico GP1 / UART0 RX
// SiK GND             -> Pico GND
// SiK 5V              -> Pico VBUS
//
// Not:
// Bu katman Hydronom paketlerini yorumlamaz.
// Sadece byte tasir.
// Komut, telemetry, ACK, security, compact frame isleri Host tarafindadir.

use core::fmt::Write as FmtWrite;

use heapless::String;
use panic_halt as _;

use fugit::RateExtU32;
use rp235x_hal as hal;
use usb_device::class_prelude::UsbBusAllocator;
use usb_device::device::StringDescriptors;
use usb_device::prelude::*;
use usbd_serial::SerialPort;

use hal::block::ImageDef;
use hal::clocks::init_clocks_and_plls;
use hal::gpio::{FunctionUart, Pins};
use hal::pac;
use hal::Clock;
use hal::Sio;
use hal::Watchdog;

mod bridge;
mod diagnostics;
mod protocol;

use bridge::radio_bridge::RadioBridge;
use protocol::node_identity::{
    BACKEND_KIND, DEFAULT_BAUDRATE, FIRMWARE_NAME, FIRMWARE_VERSION, FREQUENCY_BAND,
    HOST_INTERFACE, NODE_ID, NODE_NAME, RADIO_INTERFACE, USB_MANUFACTURER, USB_PRODUCT,
    USB_SERIAL,
};

const RADIO_TX_MAX_BYTES_PER_TICK: usize = 32;
const RADIO_RX_MAX_BYTES_PER_TICK: usize = 32;

// RP2350 / Pico 2 W icin boot ROM uygulama tanimi.
// memory.x icinde .start_block dogru yere yerlestirilmelidir.
#[link_section = ".start_block"]
#[used]
pub static IMAGE_DEF: ImageDef = ImageDef::secure_exe();

#[hal::entry]
fn main() -> ! {
    let mut pac = pac::Peripherals::take().unwrap();
    let _core = cortex_m::Peripherals::take().unwrap();

    let mut watchdog = Watchdog::new(pac.WATCHDOG);

    let clocks = init_clocks_and_plls(
        12_000_000,
        pac.XOSC,
        pac.CLOCKS,
        pac.PLL_SYS,
        pac.PLL_USB,
        &mut pac.RESETS,
        &mut watchdog,
    )
    .ok()
    .unwrap();

    let sio = Sio::new(pac.SIO);

    let pins = Pins::new(
        pac.IO_BANK0,
        pac.PADS_BANK0,
        sio.gpio_bank0,
        &mut pac.RESETS,
    );

    let uart_tx = pins.gpio0.into_function::<FunctionUart>();
    let uart_rx = pins.gpio1.into_function::<FunctionUart>();

    let mut uart = hal::uart::UartPeripheral::new(
        pac.UART0,
        (uart_tx, uart_rx),
        &mut pac.RESETS,
    )
    .enable(
        hal::uart::UartConfig::new(
            DEFAULT_BAUDRATE.Hz(),
            hal::uart::DataBits::Eight,
            None,
            hal::uart::StopBits::One,
        ),
        clocks.peripheral_clock.freq(),
    )
    .unwrap();

    let usb_bus = UsbBusAllocator::new(hal::usb::UsbBus::new(
        pac.USB,
        pac.USB_DPRAM,
        clocks.usb_clock,
        true,
        &mut pac.RESETS,
    ));

    let mut usb_serial = SerialPort::new(&usb_bus);

    let mut usb_dev = UsbDeviceBuilder::new(
        &usb_bus,
        UsbVidPid(0x1209, 0x4330),
    )
    .strings(&[
        StringDescriptors::default()
            .manufacturer(USB_MANUFACTURER)
            .product(USB_PRODUCT)
            .serial_number(USB_SERIAL),
    ])
    .unwrap()
    .device_class(usbd_serial::USB_CLASS_CDC)
    .build();

    let mut identity_line: String<256> = String::new();

    let _ = writeln!(
        identity_line,
        "HYDRONOM_NODE node_id=0x{:04X} name={} firmware={} version={} backend={} band={} host={} radio={} baud={}",
        NODE_ID,
        NODE_NAME,
        FIRMWARE_NAME,
        FIRMWARE_VERSION,
        BACKEND_KIND,
        FREQUENCY_BAND,
        HOST_INTERFACE,
        RADIO_INTERFACE,
        DEFAULT_BAUDRATE,
    );

    let mut bridge = RadioBridge::new();

    loop {
        let _usb_active = usb_dev.poll(&mut [&mut usb_serial]);

        bridge.send_identity_once(
            &mut usb_serial,
            identity_line.as_bytes(),
        );

        bridge.pump_usb_to_radio(&mut usb_serial);

        bridge.flush_radio_tx_limited(
            &mut uart,
            RADIO_TX_MAX_BYTES_PER_TICK,
        );

        bridge.pump_radio_to_usb_limited(
            &mut usb_serial,
            &mut uart,
            RADIO_RX_MAX_BYTES_PER_TICK,
        );
    }
}