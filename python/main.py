# main.py
# Sensör → Sample → (Fuser+Plugins) → FusedState + ExternalState → (Evaluator) → Event → Health
#
# Güçlendirmeler:
# - NDJSON framing (varsayılan açık): her mesaj tek satır + "\n"
# - Tek noktadan send: string/object fark etmeksizin normalize et
# - OFFLINE mod: bağlı değilken gönderimleri güvenle atla
# - uptime gerçek uptime: monotonic tabanlı
# - monotonic zaman ölçümü: jitter/latency daha güvenli
# - loop scheduler drift azaltma: next_tick yönetimi iyileştirildi
# - Subscribe uygulaması: güvenli, hataya dayanıklı
# - ExternalState için ayrı rate (rate_hz["ExternalState"]) desteği
# - Twin mesajları TwinBus'a aktarılır
# - Plugin kısayolları: trail,lidar,lidar_runtime,vision,ogm,slam_odom,ekf
# - NDJSON güvenliği için payload normalize + send lock
#
# Hydronom mapping güncellemesi:
# - occupancy_grid plugin yeni arayüzüyle uyumlu
# - slam_odometry + ekf_localization + occupancy_grid birlikte çalışacak şekilde varsayılanlaştırıldı
# - Hydronom Ops için occupancy preview / occupancy cells akışına uygun temel hazırlandı

import os
import json
import time
import argparse
import platform
import threading
from collections import defaultdict
from typing import Any, Dict, Optional, List

# Windows konsol + emoji sorunları için stdout'u UTF-8'e zorla
os.environ.setdefault("PYTHONUTF8", "1")

from sensors.twin_bus import TwinBus
from sensors.sensor_manager import SensorManager
from gateway.tcp_client import TcpClient
from core.health import Health
from core.capability import Capability
from core.external_state import ExternalState
from fusion.fuser import Fuser
from evaluation.evaluator import Evaluator

# Plugin registry
from fusion.plugins.registry import make_plugins


def parse_args():
    p = argparse.ArgumentParser(description="Hydronom Sensör Akışı (NDJSON over TCP)")

    p.add_argument(
        "--duration",
        type=float,
        default=0.0,
        help="Koşu süresi (s). <=0 ise sınırsız çalışır"
    )
    p.add_argument(
        "--rate",
        type=float,
        default=20.0,
        help="Ana döngü hızı (Hz)"
    )
    p.add_argument(
        "--host",
        type=str,
        default="127.0.0.1",
        help="TCP hedef host"
    )
    p.add_argument(
        "--port",
        type=int,
        default=5055,
        help="TCP hedef port"
    )
    p.add_argument(
        "--health-period",
        type=float,
        default=1.0,
        help="Health gönderim periyodu (s)"
    )
    p.add_argument(
        "--no-health",
        dest="health",
        action="store_false",
        help="Health satırlarını kapat (varsayılan: açık)"
    )
    p.add_argument(
        "--fusion-hz",
        type=float,
        default=10.0,
        help="FusedState hedef frekansı (Hz)"
    )
    p.add_argument(
        "--eval-hz",
        type=float,
        default=5.0,
        help="Evaluator hedef frekansı (Hz)"
    )
    p.add_argument(
        "--publish-samples",
        type=str,
        default="all",
        choices=["all", "imu-gps", "none"],
        help="Sample yayın kapsamı"
    )
    p.add_argument(
        "--sample-div",
        type=int,
        default=1,
        help="Sample downsample bölücüsü"
    )
    p.add_argument(
        "--no-external",
        dest="external",
        action="store_false",
        help="ExternalState yayınını kapat"
    )

    # GPS’i kapatma bayrağı
    p.add_argument(
        "--no-gps",
        dest="gps",
        action="store_false",
        help="GPS örneklerini devre dışı bırak"
    )

    # Fuser plugin listesi
    p.add_argument(
        "--plugins",
        type=str,
        default="trail,lidar_runtime,ogm,slam_odom,ekf",
        help="Fuser plugin listesi: trail,lidar,lidar_runtime,vision,ogm,slam_odom,ekf"
    )

    p.set_defaults(health=True, external=True, gps=True)
    return p.parse_args()


def _env_float(name: str, default: float) -> float:
    raw = os.getenv(name)
    if raw is None:
        return default
    try:
        return float(raw)
    except Exception:
        return default


def _env_int(name: str, default: int) -> int:
    raw = os.getenv(name)
    if raw is None:
        return default
    try:
        return int(raw)
    except Exception:
        return default


def _build_plugins(spec_str: str) -> List[Any]:
    """
    CLI'dan gelen plugin listesini eklenti nesnelerine dönüştürür.

    Desteklenen kısa isimler:
      - trail
      - lidar
      - lidar_runtime
      - vision
      - ogm / occupancy_grid
      - slam_odom / slam_odometry
      - ekf / ekf_localization

    Not:
      - "lidar" görselleştirme/önizleme içindir.
      - "lidar_runtime" runtime'ın anlayacağı obstacle adaylarını üretir.
      - "ogm" occupancy grid / harita üretir.
    """
    names = [s.strip().lower() for s in (spec_str or "").split(",") if s.strip()]
    specs: List[Any] = []

    preview_max_points = _env_int("HYDRONOM_OGM_PREVIEW_MAX_POINTS", 350)
    export_max_points = _env_int("HYDRONOM_OGM_EXPORT_MAX_POINTS", 900)

    for n in names:
        if n in ("trail", "trace_trail"):
            specs.append({
                "name": "trail",
                "args": {
                    "max_points": 1500
                }
            })

        elif n in ("lidar", "lidar_obstacles"):
            specs.append({
                "name": "lidar_obstacles",
                "args": {
                    "landmark_id": "lidar_scan",
                    "emit_dense_points": False,
                    "downsample_step": 2
                }
            })

        elif n in ("lidar_runtime", "lidar_runtime_obstacles", "runtime_obstacles"):
            specs.append({
                "name": "lidar_runtime_obstacles",
                "args": {
                    "cluster_gap_m": 0.90,
                    "min_cluster_points": 3,
                    "max_obstacles": 24,
                    "min_radius_m": 0.15,
                    "max_radius_m": 3.0,
                    "downsample_step": 1
                }
            })

        elif n in ("vision", "vision_buoy", "buoy"):
            specs.append({
                "name": "vision_buoy",
                "args": {
                    "ttl_s": 6.0,
                    "min_conf": 0.6
                }
            })

        elif n in ("ogm", "occupancy_grid", "gridmap"):
            specs.append({
                "name": "occupancy_grid",
                "args": {
                    "resolution": 0.15,
                    "size_w": 320,
                    "size_h": 320,
                    "origin_x": -24.0,
                    "origin_y": -24.0,
                    "landmark_id": "ogm_preview",
                    "color": "#ff8800",
                    "preview_max_points": preview_max_points,
                    "preview_min_probability": 0.62,
                    "logit_hit": 0.60,
                    "logit_free": -0.35,
                    "logit_min": -3.5,
                    "logit_max": 4.0,
                    "occ_threshold": 0.55,
                    "decay_per_update": 0.01,
                    "max_updates_before_decay": 8,
                    "no_return_margin": 0.22,
                    "emit_preview_landmark": True,
                    "emit_points_landmark": True,
                    "export_max_points": export_max_points,
                    "input_info_period_s": 0.40
                }
            })

        elif n in ("slam_odom", "slam_odometry", "odometry_slam"):
            specs.append({
                "name": "slam_odometry",
                "args": {
                    "alpha_gps_heading": 0.08,
                    "v_min_mps": 0.70,
                    "max_gps_step_deg": 2.5,
                    "use_lidar_scan_match": True,
                    "lidar_match_min_valid": 24,
                    "lidar_yaw_search_deg": 6.0,
                    "lidar_yaw_step_deg": 0.5,
                    "lidar_weight": 0.65,
                    "max_lidar_step_deg": 1.5,
                    "heading_lowpass_alpha": 0.25,
                    "use_dead_reckon": True,
                    "max_total_step_deg": 3.0
                }
            })

        elif n in ("ekf", "ekf_localization"):
            specs.append({
                "name": "ekf_localization",
                "args": {
                    "q_pos": 0.08,
                    "q_yaw_deg": 2.5,
                    "q_vel": 0.45,
                    "q_bias": 0.003,
                    "r_gps_pos": 2.2,
                    "r_gps_vel": 0.9,
                    "r_heading_deg": 7.0,
                    "v_heading_min": 0.9,
                    "gps_gate_sigma": 4.0,
                    "gps_vel_gate_sigma": 4.0,
                    "max_trail_points": 400,
                    "tele_period_s": 0.25
                }
            })

        else:
            print(f"[MAIN] Uyarı: Bilinmeyen plugin ismi atlandı: {n}")

    return make_plugins(specs)


def _normalize_capability_sensors(raw_sensors: Any) -> Optional[List[Dict[str, Any]]]:
    """
    SensorManager.describe_for_capability() çıktısını normalize eder.

    Amaç:
      - Her sensör için backend ve sim_source alanları olsun
      - JSON tarafında C# TcpJsonServer.CapabilitySensorInfo ile uyuşsun
    """
    if raw_sensors is None:
        return None

    out: List[Dict[str, Any]] = []

    if not isinstance(raw_sensors, (list, tuple)):
        raw_sensors = [raw_sensors]

    for item in raw_sensors:
        if isinstance(item, dict):
            d = dict(item)
        else:
            if hasattr(item, "to_dict"):
                try:
                    d = dict(item.to_dict())
                except Exception:
                    d = {}
            else:
                d = getattr(item, "__dict__", {}) or {}

        sensor_name = d.get("sensor") or d.get("name") or "unknown"
        sensor_type = d.get("source") or d.get("type") or "unknown"

        d.setdefault("sensor", sensor_name)
        d.setdefault("source", sensor_type)

        backend = d.get("backend") or d.get("backend_name")
        if not backend:
            if d.get("simulate") is True:
                backend = "sim_python"
            else:
                backend = "hardware_or_driver"
        d["backend"] = backend

        sim_source = d.get("sim_source")
        if not sim_source:
            if d.get("simulate") is True:
                sim_source = "python"
            else:
                sim_source = "hardware"
        d["sim_source"] = sim_source

        if "rate_hz" not in d and "rate" in d:
            d["rate_hz"] = d["rate"]
        if "frame_id" not in d and "frame" in d:
            d["frame_id"] = d["frame"]

        out.append(d)

    return out or None


def _json_dumps_one_line(obj: Any) -> str:
    """
    JSON'u tek satır olacak şekilde üretir.
    """
    return json.dumps(obj, ensure_ascii=False, separators=(",", ":"))


def _sanitize_text_for_ndjson(text: str) -> str:
    """
    Ham metni NDJSON tek satır uyumlu hale getirir.
    """
    if not text:
        return ""

    # BOM / null / CR temizliği
    text = text.replace("\ufeff", "")
    text = text.replace("\x00", "")
    text = text.replace("\r", "")

    # Gerçek newline istemiyoruz.
    # JSON string içinde literal newline varsa kaçışlı hale getir.
    text = text.replace("\n", "\\n")

    return text.strip()


def _payload_to_object(payload: Any) -> Any:
    """
    Farklı tip payload'ları güvenli JSON objesine normalize eder.
    """
    if payload is None:
        return None

    if isinstance(payload, dict):
        return payload

    if isinstance(payload, (list, tuple)):
        return payload

    if isinstance(payload, bytes):
        payload = payload.decode("utf-8", errors="replace")

    if isinstance(payload, str):
        s = _sanitize_text_for_ndjson(payload)
        if not s:
            return None

        # Eğer zaten JSON string ise parse etmeyi dene.
        try:
            return json.loads(s)
        except Exception:
            # JSON olmayan düz string gelirse string olarak taşı.
            return {"type": "TextMessage", "payload": s}

    if hasattr(payload, "to_dict"):
        try:
            return payload.to_dict()
        except Exception:
            pass

    if hasattr(payload, "as_dict"):
        try:
            return payload.as_dict()
        except Exception:
            pass

    if hasattr(payload, "__dict__"):
        try:
            return dict(payload.__dict__)
        except Exception:
            pass

    if hasattr(payload, "to_json"):
        try:
            tj = payload.to_json()
            return _payload_to_object(tj)
        except Exception:
            pass

    return payload


def _payload_to_ndjson_line(payload: Any) -> str:
    """
    Payload'ı tek satır JSON + '\n' formuna dönüştürür.
    """
    obj = _payload_to_object(payload)

    if obj is None:
        return "\n"

    if isinstance(obj, str):
        obj = {"type": "TextMessage", "payload": obj}

    line = _json_dumps_one_line(obj)
    line = _sanitize_text_for_ndjson(line)
    return line + "\n"


def main():
    args = parse_args()

    # OS tespiti
    os_name = platform.system()
    is_windows = os_name.lower().startswith("win")
    print(f"[MAIN] Detected OS: {os_name} (is_windows={is_windows})")

    # C# runtime env override
    env_host = os.getenv("HYDRONOM_TCP_HOST")
    env_port = os.getenv("HYDRONOM_TCP_PORT")
    env_mode = os.getenv("HYDRONOM_MODE", "standalone")

    if env_host:
        args.host = env_host
    if env_port:
        try:
            args.port = int(env_port)
        except ValueError:
            print(f"[MAIN] HYDRONOM_TCP_PORT geçersiz, arg port kullanılacak: {env_port!r}")

    print(f"[MAIN] Mode={env_mode} host={args.host} port={args.port}")

    # NDJSON zorlaması
    force_ndjson = os.getenv("HYDRONOM_FORCE_NDJSON", "1") != "0"
    print(f"[MAIN] force_ndjson={force_ndjson}")

    # Runtime ayarları
    publish_samples = args.publish_samples
    sample_div = max(1, int(args.sample_div))
    external = bool(args.external)

    loop_rate_hz = max(0.1, float(args.rate))
    period = 1.0 / loop_rate_hz

    eval_hz = max(0.1, float(args.eval_hz))
    eval_period = 1.0 / eval_hz

    external_hz = max(0.1, float(args.fusion_hz))
    external_period = 1.0 / external_hz

    gps_enabled = bool(args.gps)

    last_subscribe: Optional[Dict[str, Any]] = None
    send_lock = threading.Lock()

    # Sensör yöneticisi
    sensor_debug = os.getenv("HYDRONOM_SENSOR_DEBUG", "0") == "1"
    mgr = SensorManager(debug=sensor_debug)
    mgr.open_all()

    sensors_desc = None
    try:
        if hasattr(mgr, "describe_for_capability"):
            sensors_desc = mgr.describe_for_capability()
    except Exception:
        sensors_desc = None

    sensors_desc = _normalize_capability_sensors(sensors_desc)

    # Fuser sentinel
    fuser: Optional[Fuser] = None

    def handle_subscribe(msg: Dict[str, Any]) -> None:
        nonlocal publish_samples, sample_div, external
        nonlocal loop_rate_hz, period
        nonlocal eval_hz, eval_period
        nonlocal external_hz, external_period
        nonlocal last_subscribe, fuser, gps_enabled

        last_subscribe = dict(msg)
        print(f"[SUB] Received StreamSubscribe: {msg}")

        try:
            if hasattr(mgr, "apply_stream_subscribe"):
                summary = mgr.apply_stream_subscribe(msg)
                print(f"[SUB] sensors.apply → {summary}")
        except Exception as e:
            print(f"[SUB] sensors.apply error: {e}")

        ps = msg.get("publish_samples")
        if ps in ("all", "imu-gps", "none"):
            publish_samples = ps
            print(f"[SUB] publish_samples → {publish_samples}")

        sd = msg.get("sample_div")
        if isinstance(sd, int) and sd >= 1:
            sample_div = sd
            print(f"[SUB] sample_div → {sample_div}")

        ext = msg.get("external")
        if isinstance(ext, bool):
            external = ext
            print(f"[SUB] external(ExternalState) → {external}")

        rate_hz = msg.get("rate_hz")

        if isinstance(rate_hz, (int, float)) and rate_hz > 0:
            loop_rate_hz = float(rate_hz)
            period = 1.0 / loop_rate_hz
            print(f"[SUB] loop_rate_hz → {loop_rate_hz:.3f} Hz")

        if isinstance(rate_hz, dict):
            fs_hz = rate_hz.get("FusedState")
            if isinstance(fs_hz, (int, float)) and fs_hz > 0 and fuser is not None:
                if hasattr(fuser, "set_rate_hz"):
                    try:
                        fuser.set_rate_hz(float(fs_hz))
                        print(f"[SUB] fuser.set_rate_hz({fs_hz})")
                    except Exception as e:
                        print(f"[SUB] fuser.set_rate_hz error: {e}")
                else:
                    print("[SUB] (info) Fuser dinamik hız ayarı desteklemiyor; geçildi.")

            ev_hz = rate_hz.get("Evaluator")
            if isinstance(ev_hz, (int, float)) and ev_hz > 0:
                eval_hz = float(ev_hz)
                eval_period = 1.0 / eval_hz
                print(f"[SUB] eval_hz → {eval_hz:.3f} Hz")

            ex_hz = rate_hz.get("ExternalState")
            if isinstance(ex_hz, (int, float)) and ex_hz > 0:
                external_hz = float(ex_hz)
                external_period = 1.0 / external_hz
                print(f"[SUB] external_hz → {external_hz:.3f} Hz")

        streams = msg.get("streams")
        if isinstance(streams, list):
            print(f"[SUB] streams → {streams}")

        gps_en = msg.get("gps_enabled")
        if isinstance(gps_en, bool):
            gps_enabled = gps_en
            print(f"[SUB] gps_enabled → {gps_enabled}")

    def handle_message(msg: Dict[str, Any]) -> None:
        t = msg.get("type")
        if not t:
            return

        if t == "StreamSubscribe":
            return

        if t in ("TwinGps", "TwinImu"):
            TwinBus.update(msg)
            return

    client = TcpClient(
        host=args.host,
        port=args.port,
        on_message=handle_message,
        on_subscribe=handle_subscribe
    )
    client.start()

    def safe_send_json_line(payload: Any) -> bool:
        """
        Tek noktadan güvenli gönderim.
        NDJSON kuralı:
        - Her frame TEK SATIR JSON olacak
        - Sonunda yalnızca gerçek '\\n' olacak
        """
        nonlocal connected

        if not connected:
            return False

        try:
            line = _payload_to_ndjson_line(payload) if force_ndjson else _payload_to_ndjson_line(payload)

            with send_lock:
                client.send(line)

            return True
        except Exception as e:
            print(f"[SEND] error: {e}")
            connected = False
            return False

    hello = {
        "type": "Hello",
        "node": "py-data",
        "version": "1.3.0",
        "mode": env_mode,
        "timestamp_utc": time.time(),
    }

    cap = Capability(
        node="py-data",
        version="1.3.0",
        streams=["Sample", "FusedState", "ExternalState", "Event", "Health"],
        prefer_external_state=True,
        sensors=sensors_desc,
    )

    def capability_payload() -> Any:
        if hasattr(cap, "to_dict"):
            try:
                return cap.to_dict()
            except Exception:
                pass

        if hasattr(cap, "__dict__"):
            try:
                return dict(cap.__dict__)
            except Exception:
                pass

        if hasattr(cap, "to_json"):
            try:
                return cap.to_json()
            except Exception:
                pass

        return {
            "type": "Capability",
            "node": "py-data",
            "version": "1.3.0",
            "streams": ["Sample", "FusedState", "ExternalState", "Event", "Health"],
            "prefer_external_state": True,
            "sensors": sensors_desc,
        }

    def announce_presence() -> None:
        safe_send_json_line(hello)
        safe_send_json_line(capability_payload())

    connected = client.wait_connected(5.0)
    if not connected:
        print(f"[MAIN] Uyarı: {args.host}:{args.port} adresine bağlanılamadı, sensör döngüsü OFFLINE modda çalışıyor.")
    else:
        print(f"[MAIN] Runtime bağlantısı kuruldu: {args.host}:{args.port}")
        announce_presence()

    # Fuser + Plugins
    plugins = _build_plugins(args.plugins)
    if plugins:
        print("[MAIN] Fuser plugins:", [getattr(p, "name", type(p).__name__) for p in plugins])
    else:
        print("[MAIN] Fuser plugins: (yok)")

    fuser = Fuser(period_hz=args.fusion_hz, plugins=plugins)
    fuser.init_plugins()

    evaluator = Evaluator()

    total_sent = 0
    sent_by_type = defaultdict(int)
    sent_by_sensor = defaultdict(int)

    start_mono = time.monotonic()

    last_health_wall = 0.0
    last_eval_wall = 0.0
    last_external_mono = 0.0

    last_loop_mono: Optional[float] = None
    jitter_ms = 0.0

    t_end_wall = time.time() + args.duration if args.duration > 0 else None

    next_tick = time.monotonic()
    loop_i = 0

    last_offline_log = 0.0
    last_reconnect_try = 0.0

    try:
        while True:
            loop_start_wall = time.time()
            loop_start_mono = time.monotonic()

            if t_end_wall is not None and loop_start_wall >= t_end_wall:
                break

            if last_loop_mono is not None:
                actual = loop_start_mono - last_loop_mono
                jitter_ms = abs(actual - period) * 1000.0
            last_loop_mono = loop_start_mono

            if not connected:
                if (loop_start_wall - last_offline_log) > 2.0:
                    print("[MAIN] OFFLINE: Runtime bağlantısı yok, gönderimler drop ediliyor.")
                    last_offline_log = loop_start_wall

                if (loop_start_wall - last_reconnect_try) > 2.0:
                    last_reconnect_try = loop_start_wall
                    try:
                        connected = client.wait_connected(0.1)
                        if connected:
                            print(f"[MAIN] Reconnected: {args.host}:{args.port}")
                            announce_presence()
                    except Exception:
                        connected = False

            # 1) Sensör örnekleri
            samples = mgr.read_all()
            loop_i += 1

            if not gps_enabled:
                samples = [s for s in samples if getattr(s, "sensor", None) != "gps"]

            send_samples = True
            if publish_samples == "none":
                send_samples = False
            if sample_div > 1 and (loop_i % sample_div) != 0:
                send_samples = False

            if send_samples and samples:
                for s in samples:
                    if publish_samples == "imu-gps" and getattr(s, "sensor", None) not in ("imu", "gps"):
                        continue

                    ok = safe_send_json_line(s)
                    if ok:
                        total_sent += 1
                        sent_by_type["Sample"] += 1
                        try:
                            sent_by_sensor[s.sensor] += 1
                        except Exception:
                            pass

            # 2) Füzyon
            if samples and fuser is not None:
                try:
                    fuser.update(samples)
                except Exception as e:
                    print(f"[FUSER] update error: {e}")

            fused = None
            if fuser is not None:
                try:
                    fused = fuser.maybe_emit()
                except Exception as e:
                    print(f"[FUSER] maybe_emit error: {e}")
                    fused = None

            if fused is not None:
                ok = safe_send_json_line(fused)
                if ok:
                    total_sent += 1
                    sent_by_type["FusedState"] += 1

                now_mono = time.monotonic()
                if external and (now_mono - last_external_mono) >= external_period:
                    x = fused.pose.get("x", 0.0)
                    y = fused.pose.get("y", 0.0)
                    z = fused.pose.get("z", 0.0)
                    head_deg = fused.pose.get("yaw", 0.0)
                    yaw_rate = fused.twist.get("yaw_rate", 0.0)

                    est = ExternalState(
                        x=x,
                        y=y,
                        z=z,
                        head_deg=head_deg,
                        yaw_rate=yaw_rate,
                        source="py-data",
                    )
                    ok2 = safe_send_json_line(est)
                    if ok2:
                        total_sent += 1
                        sent_by_type["ExternalState"] += 1
                    last_external_mono = now_mono

            # 3) Değerlendirme
            now_wall = time.time()
            if (now_wall - last_eval_wall) >= eval_period:
                try:
                    evaluator.update(samples, fused)
                    for ev in evaluator.emit_events():
                        ok = safe_send_json_line(ev)
                        if ok:
                            total_sent += 1
                            sent_by_type["Event"] += 1
                except Exception as e:
                    print(f"[EVAL] error: {e}")
                last_eval_wall = now_wall

            # 4) Health
            if args.health and (now_wall - last_health_wall) >= args.health_period:
                stats = client.get_stats()

                latency_ms = max(0.0, (time.monotonic() - loop_start_mono) * 1000.0)
                drops = int(stats.get("dropped", 0))
                qsize = int(stats.get("qsize", 0))
                uptime_s = max(0.0, time.monotonic() - start_mono)

                h = Health(
                    node="py-data",
                    latency_ms=latency_ms,
                    jitter_ms=float(jitter_ms),
                    drops=drops,
                    queue=qsize,
                    uptime_s=uptime_s,
                )
                ok = safe_send_json_line(h)
                if ok:
                    total_sent += 1
                    sent_by_type["Health"] += 1
                last_health_wall = now_wall

            # 5) Oranlı uyku
            loop_rate_hz = max(0.1, float(loop_rate_hz))
            period = 1.0 / loop_rate_hz

            next_tick += period
            now_mono = time.monotonic()
            sleep_dur = next_tick - now_mono

            if sleep_dur > 0:
                time.sleep(sleep_dur)
            else:
                next_tick = time.monotonic()

    except KeyboardInterrupt:
        pass
    finally:
        try:
            mgr.close_all()
        except Exception:
            pass

        try:
            client.stop()
        except Exception:
            pass

        print(f"[MAIN] Toplam gönderilen satır: {total_sent}")
        if sent_by_type:
            print("[MAIN] Tür bazlı gönderim sayıları:")
            for k, v in sent_by_type.items():
                print(f"  - {k}: {v}")
        if sent_by_sensor:
            print("[MAIN] Sensör bazlı gönderim sayıları:")
            for k, v in sent_by_sensor.items():
                print(f"  - {k}: {v}")


if __name__ == "__main__":
    main()