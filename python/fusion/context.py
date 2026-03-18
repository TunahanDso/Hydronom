# fusion/context.py
from __future__ import annotations
from dataclasses import dataclass, field
from typing import Dict, Any, List, Tuple, Optional
import math
import time

_DEG2RAD = math.pi / 180.0
_LAT_M   = 111_320.0

def _rpy_to_R(roll_deg: float, pitch_deg: float, yaw_deg: float) -> List[List[float]]:
    """World←Body için R = Rz(yaw) * Ry(pitch) * Rx(roll) (Z-Y-X)."""
    r = roll_deg  * _DEG2RAD
    p = pitch_deg * _DEG2RAD
    y = yaw_deg   * _DEG2RAD
    cr, sr = math.cos(r), math.sin(r)
    cp, sp = math.cos(p), math.sin(p)
    cy, sy = math.cos(y), math.sin(y)
    return [
        [ cy*cp,  cy*sp*sr - sy*cr,  cy*sp*cr + sy*sr ],
        [ sy*cp,  sy*sp*sr + cy*cr,  sy*sp*cr - cy*sr ],
        [   -sp,               cp*sr,               cp*cr ],
    ]

@dataclass
class FusionContext:
    # Zaman (host epoch, s)
    now: float

    # Durum (map frame)
    x: float
    y: float
    z: float
    yaw_deg: float
    yaw_rate: float
    vx: float
    vy: float

    # YENİ: IMU’dan roll/pitch (deniz için kritik kompanzasyon)
    roll_deg: float = 0.0
    pitch_deg: float = 0.0

    # Projeksiyon (lat/lon → XY)
    lat0: Optional[float] = None
    lon0: Optional[float] = None
    cos_lat0: float = 1.0

    # Plugin’lerin yayınlayacağı görselleştirme / meta
    landmarks_out: List[Dict[str, Any]] = field(default_factory=list)
    inputs_out: List[Dict[str, Any]] = field(default_factory=list)

    # Plugin → Fuser küçük poz düzeltmeleri (dx,dy,dyaw_deg)
    _pose_corrections: List[Dict[str, float]] = field(default_factory=list, repr=False)

    # Profiling işaretleri (plugin_name → t0)
    _prof_marks: Dict[str, float] = field(default_factory=dict, repr=False)

    # ---------------- Projeksiyon yardımcıları ----------------

    def set_origin(self, lat: float, lon: float) -> None:
        self.lat0 = float(lat)
        self.lon0 = float(lon)
        self.cos_lat0 = math.cos(lat * _DEG2RAD)

    def project(self, lat: float, lon: float) -> Tuple[float, float]:
        """Basit equirectangular projeksiyon (metre)."""
        if self.lat0 is None or self.lon0 is None:
            self.set_origin(lat, lon)
        dx = (lon - float(self.lon0)) * (_LAT_M * self.cos_lat0)
        dy = (lat - float(self.lat0)) * _LAT_M
        return dx, dy

    # ---------------- Frame dönüşüm yardımcıları ----------------

    def rpy_to_R(self) -> List[List[float]]:
        """World←Body 3×3 rotasyon matrisi (Rz*Ry*Rx)."""
        return _rpy_to_R(self.roll_deg, self.pitch_deg, self.yaw_deg)

    def body_to_world(self, xb: float, yb: float, zb: float = 0.0) -> Tuple[float, float, float]:
        """
        base_link (gövde) koordinatındaki bir noktayı world/map'e dönüştür.
        xb,yb,zb: metre (gövde eksenleri: x ileri, y iskele, z yukarı)
        """
        R = self.rpy_to_R()
        xw = self.x + R[0][0]*xb + R[0][1]*yb + R[0][2]*zb
        yw = self.y + R[1][0]*xb + R[1][1]*yb + R[1][2]*zb
        zw = self.z + R[2][0]*xb + R[2][1]*yb + R[2][2]*zb
        return xw, yw, zw

    # ---------------- Durum erişim API’leri ----------------

    def get_pose(self) -> Dict[str, float]:
        """Anlık poz (map frame)."""
        return {"x": self.x, "y": self.y, "z": self.z, "yaw": self.yaw_deg}

    def get_twist(self) -> Dict[str, float]:
        """Anlık hız bilgileri."""
        return {"vx": self.vx, "vy": self.vy, "yaw_rate": self.yaw_rate}

    # ---------------- Çıkış API’leri ----------------

    def add_landmark(self, lm: Dict[str, Any]) -> None:
        """GUI için landmark ekle (shape:'point'|'polyline' ...)."""
        self.landmarks_out.append(lm)

    def add_input(self, ip: Dict[str, Any]) -> None:
        """Bu emitte kullanılan ek girdi/ipuçlarını kaydet."""
        self.inputs_out.append(ip)

    def add_input_info(self, source: str, info: Dict[str, Any]) -> None:
        """Belirli bir eklenti/öğe adına bilgi ekle."""
        payload = dict(info)
        payload["_source"] = source
        self.inputs_out.append(payload)

    # ---------------- Poz düzeltme API’si ----------------

    def apply_pose_correction(self, dx: float = 0.0, dy: float = 0.0, dyaw_deg: float = 0.0) -> None:
        """Plugin’lerin küçük düzeltmeleri Fuser’a iletmesi için kuyruk."""
        self._pose_corrections.append({
            "dx": float(dx), "dy": float(dy), "dyaw_deg": float(dyaw_deg)
        })

    def apply_pose_delta(self, dx: float, dy: float, dyaw_deg: float) -> None:
        """Geriye dönük uyumluluk: delta uygulama ismi (aynı davranış)."""
        self.apply_pose_correction(dx, dy, dyaw_deg)

    # Fuser iç kullanımına yardımcı (plugin’ler çağırmaz)
    def _drain_pose_corrections(self) -> List[Dict[str, float]]:
        out = list(self._pose_corrections)
        self._pose_corrections.clear()
        return out

    # ---------------- Profiling (isteğe bağlı, hafif) ----------------

    def profile_begin(self, name: str) -> None:
        """Basit süre ölçümü başlat."""
        self._prof_marks[name] = time.perf_counter()

    def profile_end(self, name: str) -> Optional[float]:
        """Ölçümü bitir ve ms cinsinden süre döndür (bulunamazsa None)."""
        t0 = self._prof_marks.pop(name, None)
        if t0 is None:
            return None
        dt_ms = (time.perf_counter() - t0) * 1000.0
        return dt_ms

    # ---------------- Geriye dönük uyumluluk alias’ları ----------------
    # (Mevcut plugin/fuser iskeletleriyle sorunsuz çalışsın diye)

    # t_now ↔ now
    @property
    def t_now(self) -> float:
        return self.now

    @t_now.setter
    def t_now(self, v: float) -> None:
        self.now = float(v)

    # pose_x/y/z ↔ x/y/z
    @property
    def pose_x(self) -> float:
        return self.x

    @pose_x.setter
    def pose_x(self, v: float) -> None:
        self.x = float(v)

    @property
    def pose_y(self) -> float:
        return self.y

    @pose_y.setter
    def pose_y(self, v: float) -> None:
        self.y = float(v)

    @property
    def pose_z(self) -> float:
        return self.z

    @pose_z.setter
    def pose_z(self, v: float) -> None:
        self.z = float(v)

    # extra_landmarks alias’ı (okuma/yazma)
    @property
    def extra_landmarks(self) -> List[Dict[str, Any]]:
        return self.landmarks_out

    @extra_landmarks.setter
    def extra_landmarks(self, arr: List[Dict[str, Any]]) -> None:
        self.landmarks_out = arr
