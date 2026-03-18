import socket
import json
import time
from datetime import datetime

# TCP hedefi (C# runtime'da TcpJsonServer dinliyor)
HOST = "127.0.0.1"
PORT = 5055

# Sonsuz döngüyle her 100ms'de bir veri gönderelim
def main():
    # 1) Soketi aç ve bağlan
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.connect((HOST, PORT))
    print(f"[PY] Connected to {HOST}:{PORT}")

    try:
        while True:
            # 2) Basit sahte frame üret
            frame = {
                "TimestampUtc": datetime.utcnow().isoformat() + "Z",
                "Position": {"X": 0.0, "Y": 0.0},
                "HeadingDeg": 0.0,
                "Obstacles": [
                    {"Position": {"X": 5.0, "Y": 1.5}, "RadiusM": 0.5}
                ],
                "Target": {"X": 30.0, "Y": 0.0}
            }

            # 3) JSON'a çevir ve satır sonu ekle (line-delimited protokol)
            line = json.dumps(frame) + "\n"
            sock.sendall(line.encode("utf-8"))

            # 4) 100ms bekle
            time.sleep(0.1)
    except KeyboardInterrupt:
        print("[PY] Stopped by user.")
    finally:
        sock.close()

if __name__ == "__main__":
    main()
