from __future__ import annotations
import importlib
from typing import List, Any, Dict, Optional

# Yerleşik kısayol isimleri → (module_path, class_name)
_PLUGIN_ALIASES: Dict[str, tuple[str, str]] = {
    # Çizgi/iz
    "trail": ("fusion.plugins.trace_trail", "TrailPlugin"),
    "trace_trail": ("fusion.plugins.trace_trail", "TrailPlugin"),

    # LiDAR görselleştirme / preview
    "lidar": ("fusion.plugins.lidar_obstacles", "LidarObstaclePlugin"),
    "lidar_obstacles": ("fusion.plugins.lidar_obstacles", "LidarObstaclePlugin"),

    # LiDAR runtime obstacle çıkarımı
    "lidar_runtime": ("fusion.plugins.lidar_runtime_obstacles", "LidarRuntimeObstaclePlugin"),
    "lidar_runtime_obstacles": ("fusion.plugins.lidar_runtime_obstacles", "LidarRuntimeObstaclePlugin"),
    "runtime_obstacles": ("fusion.plugins.lidar_runtime_obstacles", "LidarRuntimeObstaclePlugin"),

    # Görsel şamandıra
    "vision_buoy": ("fusion.plugins.vision_buoy", "VisionBuoyPlugin"),
    "buoy": ("fusion.plugins.vision_buoy", "VisionBuoyPlugin"),

    # Haritalama (OGM)
    "occupancy_grid": ("fusion.plugins.occupancy_grid", "OccupancyGridPlugin"),
    "ogm": ("fusion.plugins.occupancy_grid", "OccupancyGridPlugin"),
    "gridmap": ("fusion.plugins.occupancy_grid", "OccupancyGridPlugin"),

    # SLAM/Odometri
    "slam_odom": ("fusion.plugins.slam_odometry", "SlamOdometryPlugin"),
    "odometry_slam": ("fusion.plugins.slam_odometry", "SlamOdometryPlugin"),
    "slam_odometry": ("fusion.plugins.slam_odometry", "SlamOdometryPlugin"),

    # EKF Lokalizasyon
    "ekf": ("fusion.plugins.ekf_localization", "EkfLocalizationPlugin"),
    "ekf_localization": ("fusion.plugins.ekf_localization", "EkfLocalizationPlugin"),
}


def _construct_from_name(name: str, args: Dict[str, Any]) -> Optional[Any]:
    """Alias tablosundan eklenti oluştur."""
    key = name.strip().lower()
    mod_cls = _PLUGIN_ALIASES.get(key)
    if not mod_cls:
        print(f"[plugins] bilinmeyen eklenti ismi: {name}")
        return None

    module_path, class_name = mod_cls

    try:
        mod = importlib.import_module(module_path)
        cls = getattr(mod, class_name)
        return cls(**(args or {}))
    except Exception as e:
        print(f"[plugins] '{name}' yüklenemedi ({module_path}.{class_name}): {e}")
        return None


def _construct_from_dict(spec: Dict[str, Any]) -> Optional[Any]:
    """
    Dict şemaları:
      {"name":"trail", "args":{...}, "enabled":true}
      {"module":"fusion.foo.bar", "class":"BazPlugin", "args":{...}, "enabled":true}
    """
    if spec.get("enabled") is False:
        return None

    module_path = spec.get("module")
    class_name = spec.get("class")
    args = spec.get("args", {}) or {}

    # module/class açıkça verildiyse önce onu dene
    if module_path and class_name:
        try:
            mod = importlib.import_module(module_path)
            cls = getattr(mod, class_name)
            return cls(**args)
        except Exception as e:
            print(f"[plugins] module/class yüklenemedi ({module_path}.{class_name}): {e}")
            # Düş: name varsa alias ile denemeye devam

    # alias ile dene
    name = str(spec.get("name", "")).strip().lower()
    if not name:
        print("[plugins] eklenti dict içinde 'name' ya da ('module','class') bekleniyordu.")
        return None

    return _construct_from_name(name, args)


def make_plugins(specs: List[Any]) -> List[Any]:
    """
    specs:
      - Sınıf örneği: TrailPlugin()
      - İsim: "trail"
      - Dict: {"name":"trail", "args":{...}} ya da {"module":"fusion.x.y", "class":"Z", "args":{...}}

    Dönüş:
      Başarıyla oluşturulan eklenti örnekleri listesi.
    """
    out: List[Any] = []
    if not specs:
        return out

    for s in specs:
        try:
            # Zaten örnek verilmişse direkt ekle
            if not isinstance(s, (str, dict)):
                out.append(s)
                continue

            if isinstance(s, str):
                inst = _construct_from_name(s, {})
                if inst is not None:
                    out.append(inst)
                continue

            inst = _construct_from_dict(s)
            if inst is not None:
                out.append(inst)

        except Exception as e:
            print(f"[plugins] eklenti oluşturulamadı ({s}): {e}")

    return out