mod node;
mod real;

use crate::real::firmware_core_self_test::run_bno085_firmware_core_self_test;
use crate::real::real_node_self_test::run_real_bno085_rvc_node_self_test;
use crate::real::rvc_parser_self_test::run_bno085_rvc_parser_self_test;
use crate::real::rvc_stream_self_test::run_bno085_rvc_stream_self_test;

fn main() {
    println!("HYDRONOM SENSOR PICO IMU BNO085 REAL READINESS CHECK");

    run_bno085_rvc_parser_self_test();
    run_bno085_rvc_stream_self_test();
    run_real_bno085_rvc_node_self_test();
    run_bno085_firmware_core_self_test();

    println!();
    println!("[OK] BNO085 real UART-RVC firmware core is ready for Pico transport integration.");
}