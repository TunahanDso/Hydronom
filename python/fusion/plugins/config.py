# fusion/config.py
from __future__ import annotations
import os, json
from typing import Any, Dict, List, Tuple, Union

try:
    import yaml  # PyYAML opsiyonel
except Exception:
    yaml = None  # YAML yoksa sadece JSON/dict desteklenir

# ---- Şema ----
# {
#   "fuser": {
#       "period_hz": 10.0,
#       "heading_alpha": 0.92,
#       "vel_ema_alpha": 0.2,
#       "gps_dt_min": 0.05,
#       "gps_dt_max": 1.5,
#       "speed_floor": 0.02,
#       "plugin_slow_budget_ms": 50.0,
#       "plugin_err_threshold": 5,
#       "plugin_err_window_s": 60.0
#   },
#   "plugins": [
#       "trail",
#       {"name":"lidar_obstacles","args":{"emit_dense_points":true,"downsample_step":3}},
#       {"name":"vision_buoy","args":{"min_conf":0.6,"gate_dist_m":2.0}},
#       {"name":"ekf","args":{"r_gps_pos":1.5,"r_heading_deg":4.0}}
#   ]
# }

JsonLike = Union[Dict[str, Any], List[Any]]

def _load_file(path: str) -> Dict[str, Any]:
    ext = os.path.splitext(path)[1].lower()
    with open(path, "r", encoding="utf-8") as f:
        txt = f.read()
    if ext in (".yaml", ".yml"):
        if yaml is None:
            raise RuntimeError("YAML desteği yok; 'pip install pyyaml' gerekli.")
        return yaml.safe_load(txt) or {}
    return json.loads(txt or "{}")

def load_config(cfg: Union[str, Dict[str, Any]]) -> Dict[str, Any]:
    """YAML/JSON dosyası veya zaten dict olan konfigürasyonu yükle."""
    if isinstance(cfg, str):
        return _load_file(cfg) or {}
    return dict(cfg or {})

def get_fuser_params(cfg: Dict[str, Any]) -> Dict[str, Any]:
    """Konfig içinden Fuser kurucu parametreleri (bulunanları) döndürür."""
    allowed = {
        "period_hz", "heading_alpha",
        "vel_ema_alpha", "gps_dt_min", "gps_dt_max", "speed_floor",
        "plugin_slow_budget_ms", "plugin_err_threshold", "plugin_err_window_s"
    }
    fuser_cfg = dict(cfg.get("fuser", {}) or {})
    return {k: v for k, v in fuser_cfg.items() if k in allowed}

def get_plugin_specs(cfg: Dict[str, Any]) -> List[Any]:
    """Konfig içinden plugin tanımlarını (string/dict karışık) döndürür."""
    arr = cfg.get("plugins", [])
    if not isinstance(arr, list):
        raise ValueError("'plugins' bir liste olmalı")
    out: List[Any] = []
    for item in arr:
        if isinstance(item, str):
            out.append(item)
        elif isinstance(item, dict):
            # normalize: name/module/class/args/enabled dışında alan uyarısı bas
            known = {"name","module","class","args","enabled"}
            extra = {k:v for k,v in item.items() if k not in known}
            if extra:
                print(f"[config] uyarı: bilinmeyen anahtarlar: {list(extra.keys())}")
            out.append({
                "name": item.get("name"),
                "module": item.get("module"),
                "class": item.get("class"),
                "args": item.get("args", {}) or {},
                "enabled": item.get("enabled", True)
            })
        else:
            raise ValueError(f"[config] plugin girdisi desteklenmiyor: {type(item)}")
    return out

def build_fuser_from_config(cfg_or_path: Union[str, Dict[str, Any]]):
    """
    Konfig’den Fuser örneği oluşturur.
    Dönen tuple: (fuser, applied_config_dict)
    """
    cfg = load_config(cfg_or_path)
    fuser_params = get_fuser_params(cfg)
    plugin_specs = get_plugin_specs(cfg)

    # Dairesel importu önlemek için burada import et
    from fusion.fuser import Fuser
    from fusion.plugins.registry import make_plugins

    plugins = make_plugins(plugin_specs)
    fuser = Fuser(plugins=plugins, **fuser_params)
    # Uygulanan konfig’ü geri ver (izlenebilirlik)
    applied = {
        "fuser": fuser_params,
        "plugins": plugin_specs
    }
    return fuser, applied
