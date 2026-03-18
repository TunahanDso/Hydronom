import asyncio
from pathlib import Path
from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware

# Modellerine yeni alanları eklediğini varsayıyorum (Force, Actuator vb.)
from .models import WorldFrame, Pose, Rpy 
from .tailer import tail_log

app = FastAPI(title="Hydronom 3D Live Backend - Enhanced")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

RUNTIME_LOG_PATH = Path(__file__).resolve().parents[1] / "logs" / "runtime.log"

def frame_to_dict(frame: WorldFrame) -> dict:
    """
    CSM Dinamiklerini ve Aktüatör verilerini de içerecek şekilde genişletildi.
    """
    return {
        "stamp": frame.stamp.isoformat() if frame.stamp else None,
        "mode": getattr(frame, 'mode', 'UNKNOWN'),
        "pos": {
            "x": frame.pos.x,
            "y": frame.pos.y,
            "z": frame.pos.z,
        },
        "rpy": {
            "roll": frame.rpy.roll,
            "pitch": frame.rpy.pitch,
            "yaw": frame.rpy.yaw,
        },
        "vel": {
            "x": getattr(frame.vel, 'x', 0.0),
            "y": getattr(frame.vel, 'y', 0.0),
            "z": getattr(frame.vel, 'z', 0.0),
        },
        # CSM Dinamikleri: Kuvvet ve Tork (Fb, Tb)
        "forces": {
            "fb": list(getattr(frame, 'fb', (0,0,0))),
            "tb": list(getattr(frame, 'tb', (0,0,0))),
        },
        # Aktüatör Verileri: Pervane durumları
        "actuators": getattr(frame, 'actuators', {}), 
        "heartbeat": getattr(frame, 'heartbeat', {}),
        "is_armed": getattr(frame, 'armed', False)
    }

@app.get("/")
async def root():
    return {"status": "ok", "message": "Hydronom 3D Live Backend (V2) Running"}

@app.websocket("/ws/live")
async def websocket_live(ws: WebSocket):
    """
    Gelişmiş veri setini frontend'e push eder.
    """
    await ws.accept()
    print("[ws] Hydronom connected to bridge")

    try:
        # Gerçek log tailing işlemi
        async for frame in tail_log(RUNTIME_LOG_PATH, poll_interval=0.1, start_at_end=False):
            data = frame_to_dict(frame)
            await ws.send_json(data)
            
    except WebSocketDisconnect:
        print("[ws] Client disconnected")
    except Exception as ex:
        print(f"[ws] Error: {ex}")
        try:
            await ws.close()
        except Exception:
            pass