MEMORY
{
  /* RP2350 XIP flash ba?lang?c?. Firmware kodu burada ?al???r. */
  FLASH : ORIGIN = 0x10000000, LENGTH = 2048K

  /* RP2350 SRAM ba?lang?c?. Stack, global state ve runtime buffer burada ya?ar. */
  RAM : ORIGIN = 0x20000000, LENGTH = 512K
}
