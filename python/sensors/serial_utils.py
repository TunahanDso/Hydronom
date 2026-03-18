# sensors/serial_utils.py
# -*- coding: utf-8 -*-
"""
Genel seri-port bulma yardımcıları.
- PySerial varsa ayrıntılı tarama yapar (VID/PID, açıklama, HWID).
- Yoksa güvenli fallback ile tipik yolları dener.
- Windows, Linux (Jetson/RPi) uyumlu.
"""

from typing import Iterable, List, Optional, Sequence, Tuple, Union
import os
import sys

try:
    from serial.tools import list_ports
except Exception:
    list_ports = None  # graceful fallback

# USB kimlik tipi: "1a86:7523" veya (0x1A86, 0x7523)
UsbId = Union[str, Tuple[int, int]]

def _parse_usbid(s: UsbId) -> Tuple[Optional[int], Optional[int]]:
    if isinstance(s, tuple) and len(s) == 2:
        vid, pid = s
        return int(vid), int(pid)
    if isinstance(s, str) and ":" in s:
        vs, ps = s.split(":", 1)
        try:
            return int(vs, 16), int(ps, 16)
        except Exception:
            return None, None
    return None, None

def _win_port_exists(port: str) -> bool:
    # Windows'ta os.path.exists("COM5") çoğu zaman False döner.
    # PySerial yoksa kabaca "COMx" formatını kabul et.
    p = port.upper()
    return p.startswith("COM") and p[3:].isdigit()

def port_exists(path: str) -> bool:
    if os.name == "nt":
        # PySerial varsa enumerate ile kontrol daha doğru
        if list_ports is not None:
            return any(p.device.lower() == path.lower() for p in list_ports.comports())
        return _win_port_exists(path)
    # POSIX
    try:
        return os.path.exists(path)
    except Exception:
        return False

def list_serial_ports() -> List[str]:
    """Bulunan port cihaz isimleri (device path) listesi döndürür."""
    if list_ports is not None:
        return [p.device for p in list_ports.comports()]
    # Fallback (PySerial yoksa)
    candidates = [
        # Linux / Jetson / RPi
        "/dev/ttyUSB0", "/dev/ttyUSB1", "/dev/ttyUSB2",
        "/dev/ttyACM0", "/dev/ttyACM1",
        # macOS (opsiyonel)
        "/dev/tty.SLAB_USBtoUART", "/dev/tty.usbserial", "/dev/tty.usbmodem",
    ]
    # Windows COM olasılıkları
    candidates += [f"COM{n}" for n in range(1, 33)]
    return [c for c in candidates if port_exists(c)]

def list_serial_ports_detailed() -> List[dict]:
    """PySerial varsa ayrıntılı alanlarla; yoksa sadece 'device' ile döner."""
    out: List[dict] = []
    if list_ports is not None:
        for p in list_ports.comports():
            out.append({
                "device": p.device,
                "name": getattr(p, "name", None),
                "description": getattr(p, "description", None),
                "hwid": getattr(p, "hwid", None),
                "vid": getattr(p, "vid", None),
                "pid": getattr(p, "pid", None),
                "serial_number": getattr(p, "serial_number", None),
                "manufacturer": getattr(p, "manufacturer", None),
                "product": getattr(p, "product", None),
                "location": getattr(p, "location", None),
            })
        return out
    # Fallback
    return [{"device": d} for d in list_serial_ports()]

def _match_by_usb_ids(rec: dict, usb_ids: Sequence[UsbId]) -> bool:
    vid = rec.get("vid")
    pid = rec.get("pid")
    if vid is None or pid is None:
        return False
    for u in usb_ids:
        uvid, upid = _parse_usbid(u)
        if uvid is not None and upid is not None and (vid == uvid and pid == upid):
            return True
    return False

def _match_by_hints(rec: dict, hints: Sequence[str]) -> bool:
    text = " ".join(str(rec.get(k, "") or "") for k in ("device", "name", "description", "hwid", "manufacturer", "product")).lower()
    return any(h.lower() in text for h in hints)

def find_serial_port(
    *,
    env_var: Optional[str] = None,
    preferred: Optional[Sequence[str]] = None,
    usb_ids: Optional[Sequence[UsbId]] = None,
    name_hints: Optional[Sequence[str]] = None,
    index: Optional[int] = None,
) -> Optional[str]:
    """
    Öncelik sırası:
      1) env_var (örn. HYDRONOM_GPS_PORT) set ise ve mevcutsa onu döner.
      2) PySerial ayrıntılı listesinde usb_ids eşleşmesi varsa onu döner.
      3) PySerial ayrıntılı listesinde name_hints geçen ilk portu döner.
      4) preferred listesinde var olan ilk portu döner.
      5) index verilmişse sıralı portlar arasından seçer.
      6) Aksi halde bulunan ilk portu döner.
    """
    # 1) ENV override
    if env_var:
        val = os.getenv(env_var, "").strip()
        if val:
            # Windows'ta "COMx" genelde exists dönmez → yine de kabul
            if os.name == "nt" and val.upper().startswith("COM"):
                return val
            if port_exists(val):
                return val

    detailed = list_serial_ports_detailed()

    # 2) USB ID eşleşmesi
    if usb_ids:
        for rec in detailed:
            try:
                if _match_by_usb_ids(rec, usb_ids):
                    return rec["device"]
            except Exception:
                continue

    # 3) İsim/ipucu eşleşmesi
    if name_hints:
        for rec in detailed:
            try:
                if _match_by_hints(rec, name_hints):
                    return rec["device"]
            except Exception:
                continue

    # 4) Tercih listesi
    if preferred:
        for p in preferred:
            if os.name == "nt" and p.upper().startswith("COM"):
                # COM isimlerini kabul et (pyserial open sırasında gerçek kontrol yapılır)
                return p
            if port_exists(p):
                return p

    # 5) index ile seçim
    devices = [rec["device"] for rec in detailed if "device" in rec]
    devices.sort()
    if devices:
        if index is not None and 0 <= index < len(devices):
            return devices[index]
        # 6) fallback: ilkini döner
        return devices[0]

    return None

__all__ = [
    "find_serial_port",
    "list_serial_ports",
    "list_serial_ports_detailed",
    "port_exists",
]
