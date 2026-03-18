# backend/parser.py
import re
from datetime import datetime
from typing import Optional

from .models import WorldFrame, Pose, Rpy

# pos=(x,y,z) ve rpy=(roll,pitch,yaw) yakalamak için regex
_POS_RPY_PATTERN = re.compile(
    r"pos=\((?P<x>-?\d+\.?\d*),(?P<y>-?\d+\.?\d*),(?P<z>-?\d+\.?\d*)\)"
    r".*rpy=\((?P<roll>-?\d+\.?\d*),(?P<pitch>-?\d+\.?\d*),(?P<yaw>-?\d+\.?\d*)\)"
)

# Satır başındaki [2025-11-28T00:47:45.8096898Z] gibi timestamp'i almak için
_TS_PATTERN = re.compile(r"^\[(?P<t>.+?)\]")


def _try_parse_timestamp(line: str) -> Optional[datetime]:
    """
    Satır başında timestamp varsa datetime'e çevirir.
    Yoksa veya format anlaşılmazsa None döner.
    """
    m = _TS_PATTERN.match(line)
    if not m:
        return None

    text = m.group("t")
    # [STATE] gibi uyduruk şeyleri timestamp sanmayalım
    if not text[0].isdigit():
        return None

    # Hydronom log formatında tipik ISO-8601 kullanıyoruz, hata olursa boşver.
    try:
        # 'Z' varsa UTC kabul edelim
        if text.endswith("Z"):
            text = text[:-1]
        return datetime.fromisoformat(text)
    except Exception:
        return None


def parse_line_to_frame(line: str) -> Optional[WorldFrame]:
    """
    Tek bir log satırını WorldFrame'e dönüştürür.
    Eğer satırda pos/rpy yoksa None döner (ignore edilir).
    """
    m = _POS_RPY_PATTERN.search(line)
    if not m:
        return None

    stamp = _try_parse_timestamp(line)

    x = float(m.group("x"))
    y = float(m.group("y"))
    z = float(m.group("z"))
    roll = float(m.group("roll"))
    pitch = float(m.group("pitch"))
    yaw = float(m.group("yaw"))

    return WorldFrame(
        stamp=stamp,
        pos=Pose(x=x, y=y, z=z),
        rpy=Rpy(roll=roll, pitch=pitch, yaw=yaw),
    )
