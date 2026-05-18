mod diagnostics;
mod mock;
mod node;
mod output;
mod runtime;

use crate::diagnostics::console_debug::{
    print_node_summary,
    print_stream_event,
    print_stream_stats,
};
use crate::mock::mock_bno085::MockBno085;
use crate::output::binary_stream_reader::{
    print_stream_read_stats,
    read_and_validate_stream_file,
};
use crate::output::binary_stream_writer::BinaryStreamWriter;
use crate::runtime::stream_config::Bno085StreamConfig;
use crate::runtime::stream_runtime::Bno085StreamRuntime;

const OUTPUT_STREAM_PATH: &str = "bno085_mock_stream.bin";

fn main() {
    let sensor = MockBno085::new();

    let identity = sensor.identity();
    let capability = sensor.capability();
    let health = sensor.health();

    print_node_summary(&identity, &capability, &health);

    let config = Bno085StreamConfig::default_50hz_mock();
    let mut runtime = Bno085StreamRuntime::new(sensor, config);

    let mut writer = BinaryStreamWriter::create(OUTPUT_STREAM_PATH)
        .expect("failed to create binary stream output file");

    println!();
    println!("BOOT STREAM:");

    let boot_events = runtime.emit_boot_sequence();

    for (index, event) in boot_events.iter().enumerate() {
        print_stream_event(index + 1, event);
        writer
            .write_event(event)
            .expect("failed to write boot event to binary stream");
    }

    println!();
    println!("LIVE 50HZ MOCK STREAM:");

    let mut event_index = boot_events.len() + 1;

    while let Some(event) = runtime.next_event() {
        print_stream_event(event_index, &event);

        writer
            .write_event(&event)
            .expect("failed to write stream event to binary file");

        event_index += 1;
    }

    writer.flush().expect("failed to flush binary stream file");

    let stats = runtime.stats();
    print_stream_stats(&stats);

    println!();
    println!("BINARY STREAM OUTPUT:");
    println!("path: {}", OUTPUT_STREAM_PATH);
    println!("frames_written: {}", writer.frames_written());
    println!("bytes_written: {}", writer.bytes_written());

    let readback_stats = read_and_validate_stream_file(OUTPUT_STREAM_PATH)
        .expect("failed to read back binary stream file");

    print_stream_read_stats(&readback_stats);
}