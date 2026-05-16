use std::env;
use std::fs::File;
use std::io::Write;
use std::path::PathBuf;

fn main() {
    // MİMARİ NOT:
    // memory.x dosyasını build çıktısına kopyalıyoruz.
    // cortex-m-rt linker bu dosyayı arar ve RP2350 bellek haritasını buradan okur.
    let out = PathBuf::from(env::var_os("OUT_DIR").unwrap());
    File::create(out.join("memory.x"))
        .unwrap()
        .write_all(include_bytes!("memory.x"))
        .unwrap();

    println!("cargo:rustc-link-search={}", out.display());

    // Dosya değiştiğinde yeniden build aldırır.
    println!("cargo:rerun-if-changed=memory.x");
    println!("cargo:rerun-if-changed=build.rs");
}
