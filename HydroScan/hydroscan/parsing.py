# hydroscan/parsing.py
# ---------------------------------------------------------------------------
# Hydronom runtime log parser
#
# Bu dosya hem:
#   - Offline modda tek seferde bir log dosyasını okuyup tüm STATE frame'lerini
#     çıkarıyor (parse_log_content),
#   - Hem de canlı modda satır satır gelen log akışını işleyip
#     her STATE satırında tek bir frame üretiyor (parse_stream_line).
#
# Ortak mantık:
#   - ACTUATOR, FORCE, CTL, DBG TASK satırları "durum" (state) üzerinde
#     birikiyor.
#   - [TCP] JSON satırları (Sample, FusedState, ExternalState, LaserScan,
#     Health, Event vb.) sensör snapshot’larını ve füzyon çıktısını güncelliyor.
#   - STATE satırı geldiği anda o ana kadar biriken tüm bilgiler kullanılarak
#     tek bir "frame" (data_point) oluşturuluyor.
#
# Frontend (hydroscan.js) tarafı bu frame'leri doğrudan JSON olarak kullanıyor.
# ---------------------------------------------------------------------------

import re
import math
import json
import copy
from typing import Any, Dict, List, Optional, Tuple

# ---------------------------------------------------------------------------
# REGEX PATTERNLERİ
# ---------------------------------------------------------------------------

# STATE satırı:
# [timestamp] pos=(X,Y,Z) rpy=(R,P,Y) vel=(VX,VY,VZ)
STATE_PATTERN = re.compile(
    r'.*\[?(\d{4}-\d{2}-\d{2}T\S+Z?)\]?\s+pos=\(([^,]+),([^,]+),([^,]+)\)\s+'
    r'rpy=\(([^,]+),([^,]+),([^,]+)\)\s+vel=\(([^,]+),([^,]+),([^,]+)\)'
)

# [STATE] pos=... rpy=... vel=... satırındaki cmd(thr=..., rud=...) kısmı
STATE_CMD_PATTERN = re.compile(
    r'cmd\(thr=([^,]+),\s*rud=([^)]+)\)'
)

# CTL: [CTL] dist=D dHead=DH cmdPre(t=T,r=R) -> cmdPost(...)
# Yeni C# log formatına göre, cmdPost içinde artık r= yok; dist, dHead ve
# cmdPre(t,r) kısmını okuyup limiter’i olduğu gibi bırakıyoruz.
CTL_COMMAND_PATTERN = re.compile(
    r'^\[CTL\].*dist=([^ ]+)m\s+dHead=([^°]+)°\s+cmdPre\(t=([^,]+),r=([^)]+)\)'
)

# DBG TASK: [DBG] task=Name → (TargetX, TargetY)
DBG_TASK_PATTERN = re.compile(
    r'^\[DBG\]\s+task=([^ ]+)\s+(?:→\s+\(([^,]+),([^)]+)\))?'
)

# FORCE satırı: [FORCE] Fb=(Fx,Fy,Fz) Tb=(Tx,Ty,Tz)
FORCE_PATTERN = re.compile(
    r'^\[FORCE\]\s+Fb=\(([^,]+),([^,]+),([^,]+)\)\s+Tb=\(([^,]+),([^,]+),([^)]+)\)'
)

# İtici layout satırları:
#   SIM_CH0@ch0: Pos=(x,y,z) Dir=(dx,dy,dz)
THRUSTER_LAYOUT_PATTERN = re.compile(
    r'(SIM_CH\d+)@ch(\d+):\s+Pos=\(([^,]+),([^,]+),([^,]+)\)\s+Dir=\(([^,]+),([^,]+),([^)]+)\)'
)

# SIM modda X thruster oluşturuldu.
THRUSTER_COUNT_PATTERN = re.compile(
    r'SIM modda\s+(\d+)\s+thruster oluşturuldu\.'
)


# ---------------------------------------------------------------------------
# YARDIMCI FONKSİYONLAR
# ---------------------------------------------------------------------------

def safe_float(s: Any, default: float = 0.0) -> float:
    """
    Veriyi float'a çevirirken hata olursa varsayılan değeri döndürür.
    - Virgüllü yazımları (3,14 gibi) noktalıya çevirir.
    """
    try:
        return float(str(s).replace(",", "."))
    except (ValueError, TypeError, AttributeError):
        return default


def build_synthetic_thruster_layout(thruster_count: int) -> List[Dict[str, Any]]:
    """
    Gerçek layout bilgisi yoksa, araç gövdesi etrafında dairesel sentetik
    bir itici yerleşimi üretir.

    Bu sadece görselleştirme içindir, fiziksel gerçeklikle bire bir olmak
    zorunda değildir.
    """
    layout: List[Dict[str, Any]] = []
    if thruster_count <= 0:
        return layout

    # Gövde etrafında dairesel yerleşim (X-Y düzleminde)
    R = 0.6  # Gövdeye göre keyfi yarıçap (metre)
    for i in range(thruster_count):
        angle = 2 * math.pi * i / max(thruster_count, 1)
        pos_x = R * math.cos(angle)
        pos_y = R * math.sin(angle)
        dir_x = math.cos(angle)
        dir_y = math.sin(angle)

        layout.append({
            "name": f"SIM_CH{i}",
            "channel": i,
            "pos_x": pos_x,
            "pos_y": pos_y,
            "pos_z": 0.0,
            "dir_x": dir_x,
            "dir_y": dir_y,
            "dir_z": 0.0,
        })
    return layout


# ---------------------------------------------------------------------------
# STREAM PARSE DURUMU (state) YAPISI
# ---------------------------------------------------------------------------

def init_stream_state() -> Dict[str, Any]:
    """
    Hem offline parse, hem de canlı stream parse için ortak kullanılan
    "durum" (state) yapısı.

    Bu yapı, son görülen:
      - ACTUATOR + force/tork,
      - CTL komutu,
      - Görev / hedef,
      - [TCP] sensör snapshot'ları (Sample, LaserScan),
      - FusedState + ExternalState,
      - Health + Event
    bilgilerini saklar. Her satır işlendiğinde güncellenir.
    """
    return {
        "latest_actuator_force": {
            "actuators": [],     # SIM_CH0..N değerleri (normalized çıkışlar)
            "force_x": 0.0, "force_y": 0.0, "force_z": 0.0,
            "torque_x": 0.0, "torque_y": 0.0, "torque_z": 0.0,
        },
        "latest_command": {
            "cmd_thr": 0.0,
            "cmd_rud": 0.0,
            "dist": 0.0,
            "dhead": 0.0,
            "limiter": "none",
        },
        "latest_task": {
            "task_name": "none",
            "target_x": None,
            "target_y": None,
        },
        # Sensör snapshot'ları
        # {"imu": {...}, "gps": {...}, "camera": {...}, "lidar": {...}, ...}
        "latest_sensor_samples": {},

        # Lidar LaserScan (ham ranges + parametreler)
        "latest_laserscan": None,

        # Füzyon ve dış state
        "latest_fused_state": None,
        "latest_external_state": None,

        # Sağlık ve olaylar
        "latest_health": None,
        "latest_events": [],   # Bu liste büyümesin diye event eklerken limitleriz.
    }


# ---------------------------------------------------------------------------
# [TCP] JSON SATIRLARI İÇİN İÇ HELPER
# ---------------------------------------------------------------------------

def _process_tcp_json(line: str, state: Dict[str, Any]) -> None:
    """
    [TCP] ... veya çıplak JSON satırını parse eder ve state içindeki
    sensör / füzyon alanlarını günceller. Frame üretmez.
    """
    idx = line.find("{")
    if idx < 0:
        return

    json_part = line[idx:]
    try:
        msg = json.loads(json_part)
    except json.JSONDecodeError:
        return

    msg_type = msg.get("type")
    sensor_samples = state["latest_sensor_samples"]

    # -----------------------------------------------------------------------
    # Sample: IMU / GPS / kamera / lidar vs.
    # -----------------------------------------------------------------------
    if msg_type == "Sample":
        sensor = msg.get("sensor") or "unknown"
        # Aynı sensör tipinden birden fazla source olursa ileride "sensor:source"
        # formatına genişletebiliriz.
        sensor_key = sensor

        sensor_samples[sensor_key] = {
            "sensor": sensor,
            "source": msg.get("source"),
            "t": msg.get("t"),
            "frame_id": msg.get("frame_id"),
            "data": msg.get("data") or {},
            "quality": msg.get("quality") or {},
            "schema_version": msg.get("schema_version"),
        }
        return

    # -----------------------------------------------------------------------
    # LaserScan: Lidar'ın ham range verisi
    # -----------------------------------------------------------------------
    if msg_type == "LaserScan":
        state["latest_laserscan"] = {
            "sensor": msg.get("sensor"),
            "source": msg.get("source"),
            "t": msg.get("t"),
            "frame_id": msg.get("frame_id"),
            "data": msg.get("data") or {},
            "quality": msg.get("quality") or {},
            "schema_version": msg.get("schema_version"),
        }
        # Lidar'ı sensör snapshot’ları altında da kısaca tutalım
        sensor_samples["lidar"] = state["latest_laserscan"]
        return

    # -----------------------------------------------------------------------
    # Füzyon: FusedState (pose, twist, landmarks, inputs)
    # -----------------------------------------------------------------------
    if msg_type == "FusedState":
        state["latest_fused_state"] = msg
        return

    # -----------------------------------------------------------------------
    # ExternalState: C# tarafına yollanan dış state
    # -----------------------------------------------------------------------
    if msg_type == "ExternalState":
        state["latest_external_state"] = msg
        return

    # -----------------------------------------------------------------------
    # Health / Event gibi gelecekteki tipler
    # -----------------------------------------------------------------------
    if msg_type == "Health":
        state["latest_health"] = msg
        return

    if msg_type == "Event":
        events = state["latest_events"]
        events.append(msg)
        # Liste boyutunu çok büyütmemek için basit bir limit
        if len(events) > 1000:
            del events[0:len(events) - 1000]
        return

    # Diğer tipleri şimdilik yok sayıyoruz (ileride genişletilebilir).
    return


# ---------------------------------------------------------------------------
# TEK SATIR İŞLEYİCİ (offline + canlı ortak)
# ---------------------------------------------------------------------------

def process_log_line(raw_line: str, state: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    """
    Tek bir log satırını işler.

    Davranış:
      - ACTUATOR, FORCE, CTL, DBG TASK, [TCP] / JSON satırları:
          * Sadece `state` içindeki son değerleri günceller.
          * Bir "frame" üretmez -> `None` döndürür.
      - STATE satırı:
          * O ana kadar biriken son ACTUATOR / FORCE / CTL / TASK /
            sensör snapshot / fused_state / external_state bilgilerini
            kullanarak tek bir "frame" üretir.
          * Frontend'in doğrudan kullanabileceği bir dict döndürür.
    """
    # Log başında boşluk varsa sil
    line = raw_line.lstrip()

    latest_actuator_force = state["latest_actuator_force"]
    latest_command = state["latest_command"]
    latest_task = state["latest_task"]

    # -----------------------------------------------------------------------
    # 0) TCP JSON SATIRLARI veya ÇIPLAK JSON
    #    [TCP] {"type": "...", ...}
    #    {"type": "...", ...}
    # -----------------------------------------------------------------------
    if line.startswith("[TCP]") or (line.startswith("{") and '"type"' in line):
        _process_tcp_json(line, state)
        return None

    # -----------------------------------------------------------------------
    # 1) ACTUATOR SATIRLARI
    #    [Actuator] SIM_CH0=... SIM_CH1=... F=(...) T=(...)
    # -----------------------------------------------------------------------
    if line.startswith("[Actuator]"):
        # Tüm SIM_CHn=val çiftlerini yakala (CH0, CH1, CH2, ... dahil)
        ch_matches = re.findall(r"SIM_CH(\d+)=([^\s|]+)", line)
        if ch_matches:
            max_idx = max(int(idx) for idx, _ in ch_matches)
            actuators = [0.0] * (max_idx + 1)
            for idx_str, val in ch_matches:
                i = int(idx_str)
                actuators[i] = safe_float(val)
            latest_actuator_force["actuators"] = actuators

        # Aynı satırda F=(...) T=(...) varsa onları da çek
        m_ft = re.search(
            r"F=\(([^,]+),([^,]+),([^,]+)\)\s+T=\(([^,]+),([^,]+),([^)]+)\)",
            line,
        )
        if m_ft:
            latest_actuator_force.update({
                "force_x": safe_float(m_ft.group(1)),
                "force_y": safe_float(m_ft.group(2)),
                "force_z": safe_float(m_ft.group(3)),
                "torque_x": safe_float(m_ft.group(4)),
                "torque_y": safe_float(m_ft.group(5)),
                "torque_z": safe_float(m_ft.group(6)),
            })
        return None

    # -----------------------------------------------------------------------
    # 2) FORCE SATIRLARI (fallback / override)
    #    [FORCE] Fb=(Fx,Fy,Fz) Tb=(Tx,Ty,Tz)
    # -----------------------------------------------------------------------
    m_force = FORCE_PATTERN.search(line)
    if m_force:
        latest_actuator_force.update({
            "force_x": safe_float(m_force.group(1)),
            "force_y": safe_float(m_force.group(2)),
            "force_z": safe_float(m_force.group(3)),
            "torque_x": safe_float(m_force.group(4)),
            "torque_y": safe_float(m_force.group(5)),
            "torque_z": safe_float(m_force.group(6)),
        })
        return None

    # -----------------------------------------------------------------------
    # 3) CTL KOMUTLARI (yeni format)
    #    [CTL] dist=D dHead=DH cmdPre(t=T,r=R) -> cmdPost(...)
    # -----------------------------------------------------------------------
    m_cmd = CTL_COMMAND_PATTERN.search(line)
    if m_cmd:
        latest_command.update({
            "dist": safe_float(m_cmd.group(1)),
            "dhead": safe_float(m_cmd.group(2)),
            "cmd_thr": safe_float(m_cmd.group(3)),
            "cmd_rud": safe_float(m_cmd.group(4)),
            # limiter'ı burada eski değeriyle bırakmıyoruz, satırdan güncelliyoruz.
        })

        # Limiter alanı (heuristic): "lim=..." sonrası segmenti al
        # Örn: lim=satT=False, satR=False, rlT=False, rlR=False, dbT=True, ...
        m_lim = re.search(r"lim=([^\n\r]+)", line)
        if m_lim:
            lim_raw = m_lim.group(1).strip()
            # Eğer herhangi bir flag '=True' ise limiter'ı "active" kabul et,
            # aksi halde "none". JS tarafı "NONE" dışındaki her değeri
            # potansiyel olay olarak görebiliyor.
            if re.search(r"=True", lim_raw, re.IGNORECASE):
                latest_command["limiter"] = "active"
            else:
                latest_command["limiter"] = "none"

        return None

    # -----------------------------------------------------------------------
    # 4) GÖREV (DBG TASK)
    #    [DBG] task=Name → (TargetX, TargetY)
    # -----------------------------------------------------------------------
    m_task = DBG_TASK_PATTERN.search(line)
    if m_task:
        latest_task.update({
            "task_name": m_task.group(1).strip(),
            "target_x": safe_float(m_task.group(2), 0.0) if m_task.group(2) else None,
            "target_y": safe_float(m_task.group(3), 0.0) if m_task.group(3) else None,
        })
        return None

    # -----------------------------------------------------------------------
    # 5) STATE (timestamp + pos + rpy + vel)
    # -----------------------------------------------------------------------
    m_state = STATE_PATTERN.search(line)
    if m_state:
        # Engel bilgisi aynı satırdaysa yakala
        obs_ahead_status = "KAYIT YOK"
        if "obsAhead=False" in line:
            obs_ahead_status = "TEMİZ"
        elif "obsAhead=True" in line:
            obs_ahead_status = "ENGEL VAR"

        # STATE satırının kendi içindeki cmd(thr=..., rud=...) kısmı
        state_cmd = STATE_CMD_PATTERN.search(line)
        if state_cmd:
            latest_command["cmd_thr"] = safe_float(state_cmd.group(1))
            latest_command["cmd_rud"] = safe_float(state_cmd.group(2))

        vel_x = safe_float(m_state.group(8))
        vel_y = safe_float(m_state.group(9))
        vel_z = safe_float(m_state.group(10))
        velocity_mag = math.sqrt(vel_x**2 + vel_y**2 + vel_z**2)

        # Sensör snapshot'larını ve füzyon state'ini çek
        sensor_samples = state.get("latest_sensor_samples", {})
        fused_state = state.get("latest_fused_state")
        external_state = state.get("latest_external_state")
        laserscan = state.get("latest_laserscan")
        health = state.get("latest_health")
        events = state.get("latest_events", [])

        # IMU / GPS / Kamera kısa özet alanları
        imu = sensor_samples.get("imu") or sensor_samples.get("IMU") or {}
        imu_data = imu.get("data") or {}

        gps = sensor_samples.get("gps") or sensor_samples.get("GPS") or {}
        gps_data = gps.get("data") or {}

        cam = (
            sensor_samples.get("camera")
            or sensor_samples.get("cam")
            or sensor_samples.get("CAMERA")
            or sensor_samples.get("CAM")
            or {}
        )
        cam_data = cam.get("data") or {}

        lidar_data = (laserscan or {}).get("data") or {}
        lidar_ranges = lidar_data.get("ranges") or []

        # LiDAR için ekstra özetler (FOV, çözünürlük, min/max menzil)
        angle_min = safe_float(lidar_data.get("angle_min"))
        angle_max = safe_float(lidar_data.get("angle_max"))
        angle_increment = safe_float(lidar_data.get("angle_increment"))
        range_min = safe_float(lidar_data.get("range_min"))
        range_max = safe_float(lidar_data.get("range_max"))

        lidar_fov_deg = (
            (angle_max - angle_min) * 180.0 / math.pi
            if (angle_max or angle_min)
            else 0.0
        )
        lidar_res_deg = angle_increment * 180.0 / math.pi if angle_increment else 0.0

        # FusedState'ten kısa pozisyon / hız özeti (varsa)
        fused_pose = (fused_state or {}).get("pose") or {}
        fused_twist = (fused_state or {}).get("twist") or {}

        fused_x = safe_float(fused_pose.get("x"))
        fused_y = safe_float(fused_pose.get("y"))
        fused_z = safe_float(fused_pose.get("z"))
        fused_yaw_deg = safe_float(fused_pose.get("yaw_deg"))

        fused_vx = safe_float(fused_twist.get("vx"))
        fused_vy = safe_float(fused_twist.get("vy"))
        fused_vz = safe_float(fused_twist.get("vz"))
        fused_yaw_rate = safe_float(fused_twist.get("yaw_rate_deg"))

        data_point: Dict[str, Any] = {
            # Zaman
            "time": m_state.group(1),

            # Konum
            "pos_x": safe_float(m_state.group(2)),
            "pos_y": safe_float(m_state.group(3)),
            "pos_z": safe_float(m_state.group(4)),

            # Yönelim (frontend alias'larıyla uyumlu)
            "rpy_r": safe_float(m_state.group(5)),
            "rpy_p": safe_float(m_state.group(6)),
            "heading_deg": safe_float(m_state.group(7)),

            # Hız
            "vel_x": vel_x,
            "vel_y": vel_y,
            "vel_z": vel_z,
            "velocity_mag": velocity_mag,

            # Kontrol verileri
            "cmd_thr": latest_command["cmd_thr"],
            "cmd_rud": latest_command["cmd_rud"],
            "dist_to_target": latest_command["dist"],
            "dhead_to_target": latest_command["dhead"],
            "limiter": latest_command["limiter"],

            # Görev verileri
            "task_name": latest_task["task_name"],
            "target_x": latest_task["target_x"],
            "target_y": latest_task["target_y"],
            "obs_ahead_status": obs_ahead_status,

            # Aktüatör + kuvvet/tork
            "actuators": list(latest_actuator_force["actuators"]),
            "force_x": latest_actuator_force["force_x"],
            "force_y": latest_actuator_force["force_y"],
            "force_z": latest_actuator_force["force_z"],
            "torque_x": latest_actuator_force["torque_x"],
            "torque_y": latest_actuator_force["torque_y"],
            "torque_z": latest_actuator_force["torque_z"],

            # IMU özet
            "imu_ax": safe_float(imu_data.get("ax")),
            "imu_ay": safe_float(imu_data.get("ay")),
            "imu_az": safe_float(imu_data.get("az")),
            "imu_gx": safe_float(imu_data.get("gx")),
            "imu_gy": safe_float(imu_data.get("gy")),
            "imu_gz": safe_float(imu_data.get("gz")),

            # GPS özet
            "gps_lat": gps_data.get("lat"),
            "gps_lon": gps_data.get("lon"),
            "gps_alt": gps_data.get("alt"),
            "gps_fix": gps_data.get("fix"),
            "gps_hdop": gps_data.get("hdop"),

            # Kamera özet
            "camera_width": cam_data.get("w"),
            "camera_height": cam_data.get("h"),
            "camera_format": cam_data.get("format"),

            # LiDAR özet
            "lidar_range_count": len(lidar_ranges),
            "lidar_range_min": range_min,
            "lidar_range_max": range_max,
            "lidar_fov_deg": lidar_fov_deg,
            "lidar_res_deg": lidar_res_deg,

            # FusedState kısa özet
            "fused_x": fused_x,
            "fused_y": fused_y,
            "fused_z": fused_z,
            "fused_yaw_deg": fused_yaw_deg,
            "fused_vx": fused_vx,
            "fused_vy": fused_vy,
            "fused_vz": fused_vz,
            "fused_yaw_rate_deg": fused_yaw_rate,

            # Ham snapshot: frontend isterse buradan full JSON’a ulaşabilir.
            "sensor_snapshot": {
                "samples": copy.deepcopy(sensor_samples),
                "laserscan": copy.deepcopy(laserscan),
                "fused_state": copy.deepcopy(fused_state),
                "external_state": copy.deepcopy(external_state),
                "health": copy.deepcopy(health),
                "events": copy.deepcopy(events),
            },

            # Debug için ham satır
            "raw_line": raw_line.strip(),
        }

        return data_point

    # Bu satırdan bir frame oluşmadı
    return None


# ---------------------------------------------------------------------------
# OFFLINE PARSE (TÜM LOG'U BİR KEZDE OKUMAK İÇİN)
# ---------------------------------------------------------------------------

def parse_log_content(log_content: str) -> List[Dict[str, Any]]:
    """
    Ham log içeriğini satır satır ayrıştırır ve yalnızca STATE satırları
    üzerinden oluşturulmuş frame'leri (data_point) bir liste olarak döndürür.
    """
    parsed_data: List[Dict[str, Any]] = []
    state = init_stream_state()

    for raw_line in log_content.splitlines():
        dp = process_log_line(raw_line, state)
        if dp is not None:
            parsed_data.append(dp)

    return parsed_data


# ---------------------------------------------------------------------------
# STREAM PARSE (CANLI MOD İÇİN TEK SATIR)
# ---------------------------------------------------------------------------

def parse_stream_line(raw_line: str, state: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    """
    Canlı modda tek bir satırı işler.
    STATE satırına denk gelirse bir frame döndürür, yoksa None.
    """
    return process_log_line(raw_line, state)


# ---------------------------------------------------------------------------
# THRUSTER SAYISI VE GEOMETRİ ÇIKARIMI
# ---------------------------------------------------------------------------

def extract_thruster_info(
    log_content: str,
    parsed_data: List[Dict[str, Any]],
) -> Tuple[int, List[Dict[str, Any]]]:
    """
    Log içinden itici sayısını ve geometri bilgisini çıkarır.

    Adımlar:
      1) "SIM modda X thruster oluşturuldu." satırından X sayısını okumaya çalış.
      2) Layout satırlarını (SIM_CHn@chN: Pos=... Dir=...) parse et.
      3) Hâlâ thruster_count 0 ise, parsed_data içindeki actuator uzunluklarından
         tahmin et.
      4) Hâlâ layout yoksa ama thruster_count > 0 ise sentetik dairesel
         geometri üret.
      5) Sadece layout varsa ama thruster_count 0 ise, layout uzunluğundan set et.
    """
    thruster_count = 0
    thruster_layout: List[Dict[str, Any]] = []

    # 1) "SIM modda X thruster oluşturuldu." satırından sayıyı al
    match = THRUSTER_COUNT_PATTERN.search(log_content)
    if match:
        thruster_count = int(match.group(1))

    # 2) Layout satırlarını yakala (gerçek geometri)
    layout_matches = THRUSTER_LAYOUT_PATTERN.findall(log_content)
    if layout_matches:
        for m in layout_matches:
            name = m[0]
            channel = int(m[1])
            pos_x = safe_float(m[2])
            pos_y = safe_float(m[3])
            pos_z = safe_float(m[4])
            dir_x = safe_float(m[5])
            dir_y = safe_float(m[6])
            dir_z = safe_float(m[7])

            thruster_layout.append({
                "name": name,
                "channel": channel,
                "pos_x": pos_x,
                "pos_y": pos_y,
                "pos_z": pos_z,
                "dir_x": dir_x,
                "dir_y": dir_y,
                "dir_z": dir_z,
            })

    # 3) Hâlâ thruster_count 0 ise, parsed_data içindeki actuator uzunluklarından tahmin et
    if thruster_count == 0:
        thruster_count = max(
            (len(dp.get("actuators") or []) for dp in parsed_data),
            default=0,
        )

    # 4) Hâlâ layout yoksa ama thruster_count > 0 ise sentetik dairesel layout üret
    if not thruster_layout and thruster_count > 0:
        thruster_layout = build_synthetic_thruster_layout(thruster_count)

    # 5) Eğer sadece layout varsa ama thruster_count 0 kalmışsa, layout uzunluğundan set et
    if thruster_count == 0 and thruster_layout:
        thruster_count = len(thruster_layout)

    return thruster_count, thruster_layout
