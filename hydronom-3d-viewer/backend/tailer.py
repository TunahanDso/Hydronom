import asyncio
import os
from pathlib import Path
from typing import AsyncGenerator, Optional

from .models import WorldFrame
from .parser import parse_line_to_frame

async def tail_log(
    path: Path,
    poll_interval: float = 0.05, # Daha akıcı bir Jitter takibi için intervali düşürdüm
    start_at_end: bool = False,
) -> AsyncGenerator[WorldFrame, None]:
    """
    Hydronom Runtime Log takipçisi.
    Her satırı parse eder; eğer satır tam bir frame oluşturuyorsa veya 
    mevcut frame'i güncelliyorsa yield eder.
    """
    
    # Dosya var olana kadar bekle (Sistem init süreci)
    while not path.exists():
        print(f"[tailer] {path} henüz oluşmadı, bekleniyor...")
        await asyncio.sleep(1.0)

    # Dosyayı açarken buffer'ı küçültmek gerçek zamanlılık için kritiktir
    with path.open("r", encoding="utf-8", errors="ignore") as f:
        if start_at_end:
            f.seek(0, os.SEEK_END)
            print(f"[tailer] Doğrudan güncel verilere odaklanıldı.")
        else:
            print(f"[tailer] Geçmiş veriler taranıyor...")
            for line in f:
                frame: Optional[WorldFrame] = parse_line_to_frame(line)
                if frame:
                    yield frame
            print(f"[tailer] Mevcut içerik senkronize edildi, canlı moda geçiliyor.")

        # Gerçek Zamanlı Takip Döngüsü
        while True:
            where = f.tell()
            line = f.readline()
            
            if not line:
                # Yeni veri yoksa bekle
                await asyncio.sleep(poll_interval)
                f.seek(where)
                continue

            # Satırı işle
            # Not: parser.py içinde bir singleton veya state tutarak 
            # ardışık [FORCE], [Actuator] satırlarını tek bir WorldFrame'de birleştireceğiz.
            frame: Optional[WorldFrame] = parse_line_to_frame(line)
            
            if frame:
                yield frame