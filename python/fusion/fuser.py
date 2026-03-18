# fusion/fuser.py
import math
import time
from typing import List, Optional, Dict, Any, Tuple

from core.fused_state import FusedState
from fusion.context import FusionContext
from fusion.plugins.base import IFuserPlugin

try:
    # Opsiyonel: dict tabanlı plugin tanımlarını sınıfa çevirir
    from fusion.plugins.registry import make_plugins as _make_plugins
except Exception:  # registry yoksa doğrudan listeyle çalışırız
    def _make_plugins(specs): return specs  # type: ignore

_DEG2DEG = 1.0
_DEG2RAD = math.pi / 180.0
_RAD2DEG = 180.0 / math.pi
_LAT_M   = 111_320.0  # 1° enlem ≈ 111.32 km


class Fuser:
    """
    IMU + GPS tabanlı füzyon çekirdeği (büyütülmüş sürüm).

    Özellikler:
      - Plugin yaşam döngüsü: on_init → on_samples → on_before_emit → on_close
      - GPS türevi + EMA ile vx,vy üretimi (twist artık dolu!)
      - Plugin oran kontrolü (max_hz), öncelik (priority)
      - Plugin health: hata/circuit-breaker + yavaş-kare sayacı
      - Telemetri: inputs içine sensör ve plugin_health özeti düşülür

    Birim sözleşmesi:
      - pose.yaw: derece
      - twist.yaw_rate: rad/s
      - apply_pose_correction dyaw_deg: derece
    """

    def __init__(
        self,
        period_hz: float = 10.0,
        heading_alpha: float = 0.92,   # IMU ağırlığı (GPS heading ile tamamlayıcı filtre)
        plugins: Optional[List[IFuserPlugin] | List[Dict[str, Any]]] = None,

        # ---- Twist (vx,vy) üretimi parametreleri ----
        vel_ema_alpha: float = 0.2,    # GPS türevinden gelen hız için EMA katsayısı
        gps_dt_min: float = 0.05,      # GPS ardışık örnek aralığı alt sınır (s)
        gps_dt_max: float = 1.5,       # GPS ardışık örnek aralığı üst sınır (s)
        speed_floor: float = 0.02,     # çok küçük hızları 0’a bastır (m/s)

        # ---- Plugin yürütme kontrolü ----
        plugin_slow_budget_ms: float = 50.0,  # tek çağrıda ms üstü → slow_count++
        plugin_err_threshold: int = 5,        # eşik üstü hata → circuit-breaker
        plugin_err_window_s: float = 60.0     # hata penceresi
    ):
        # Periyot
        self.period = 1.0 / max(1e-3, float(period_hz))
        self.heading_alpha = float(heading_alpha)

        # Emit zamanlayıcı
        self._next_emit_ts: float = 0.0

        # IMU integrasyonu için zaman
        self._last_t: Optional[float] = None

        # Lat/Lon → XY referans
        self._lat0: Optional[float] = None
        self._lon0: Optional[float] = None
        self._cos_lat0: float = 1.0

        # Durum
        self._x: float = 0.0
        self._y: float = 0.0
        self._z: float = 0.0
        self._yaw_deg: float = 0.0
        self._yaw_rate: float = 0.0  # rad/s

        # YENİ: IMU’dan gelen gövde oryantasyonu
        self._roll_deg: float = 0.0
        self._pitch_deg: float = 0.0

        # Hız (EMA ile tutulur)
        self._vx: float = 0.0
        self._vy: float = 0.0
        self._vel_ema_alpha = float(vel_ema_alpha)
        self._gps_dt_min = float(gps_dt_min)
        self._gps_dt_max = float(gps_dt_max)
        self._speed_floor = float(speed_floor)

        # Son GPS
        self._last_gps: Optional[Dict[str, Any]] = None  # {t,x,y,hdop,source}

        # Son frame örnekleri
        self._last_samples: Optional[List[object]] = None

        # Pluginler (oluştur + meta: priority/max_hz)
        self._plugins: List[IFuserPlugin] = _make_plugins(plugins or [])  # type: ignore[arg-type]
        self._plugins.sort(key=lambda p: getattr(p, "priority", 100))

        # Plugin çalışma zamanları (rate limit) ve sağlık
        self._plugin_last_ts: Dict[int, float] = {}
        self._plugin_health: Dict[int, Dict[str, Any]] = {}
        self._slow_budget_ms = float(plugin_slow_budget_ms)
        self._err_threshold = int(plugin_err_threshold)
        self._err_window_s = float(plugin_err_window_s)

        # Emit inputs
        self._last_emit_inputs: List[Dict[str, Any]] = []

        # Init
        self._init_plugins_once = False
        self._ctx_last: Optional[FusionContext] = None

    # ------------------ Yardımcılar ------------------

    def _xy_from_latlon(self, lat: float, lon: float) -> Tuple[float, float]:
        """Basit equirectangular projeksiyon ile metre cinsinden XY."""
        if self._lat0 is None:
            self._lat0 = float(lat)
            self._lon0 = float(lon)
            self._cos_lat0 = math.cos(float(lat) * _DEG2RAD)
        dx = (float(lon) - self._lon0) * (_LAT_M * self._cos_lat0)
        dy = (float(lat) - self._lat0) * _LAT_M
        return dx, dy

    def _ema(self, old: float, new: float, alpha: float) -> float:
        return alpha * new + (1.0 - alpha) * old

    def set_rate_hz(self, hz: float) -> None:
        self.period = 1.0 / max(1e-3, float(hz))

    # ------------------ Plugin yaşam döngüsü ------------------

    def _ensure_plugins_inited(self, ctx: FusionContext) -> None:
        if self._init_plugins_once:
            return
        for idx, p in enumerate(self._plugins):
            # Sağlık kaydı
            self._plugin_health[idx] = {
                "name": getattr(p, "name", f"plugin_{idx}"),
                "enabled": True,
                "err_count": 0,
                "last_err_ts": 0.0,
                "slow_count": 0,
                "hz": 0.0,
                "last_dt": 0.0,
            }
            try:
                p.on_init(ctx)  # yeni imza
            except AttributeError:
                try:
                    getattr(p, "init")(ctx)  # type: ignore
                except Exception as e:
                    print(f"[Fuser] plugin init fallback error {getattr(p,'name','?')}: {e}")
                    self._mark_plugin_error(idx)
                except Exception as e:
                    print(f"[Fuser] plugin on_init error {getattr(p,'name','?')}: {e}")
                    self._mark_plugin_error(idx)
        self._init_plugins_once = True

    # >>>>>>>>>>>>>>>>>>>> EKLENDİ: public init_plugins() <<<<<<<<<<<<<<<<<<<<
    def init_plugins(self) -> List[str]:
        """
        main() tarafından çağrılır. Plugin'leri güvenli şekilde başlatır.
        Dönen liste: başlatılan plugin adları (telemetri/diagnostic için).
        """
        now = time.time()
        ctx = FusionContext(
            now=now,
            x=self._x, y=self._y, z=self._z,
            yaw_deg=self._yaw_deg, yaw_rate=self._yaw_rate,
            vx=self._vx, vy=self._vy,
            roll_deg=self._roll_deg, pitch_deg=self._pitch_deg,
            lat0=self._lat0, lon0=self._lon0, cos_lat0=self._cos_lat0
        )
        self._ensure_plugins_inited(ctx)
        return [getattr(p, "name", f"plugin_{i}") for i, p in enumerate(self._plugins)]
    # >>>>>>>>>>>>>>>>>>>> EK BİTİŞ <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<

    def close_plugins(self) -> None:
        # Kapanışta çağır (örn. main finally’de)
        now = time.time()
        ctx = FusionContext(
            now=now, x=self._x, y=self._y, z=self._z,
            yaw_deg=self._yaw_deg, yaw_rate=self._yaw_rate,
            vx=self._vx, vy=self._vy,
            roll_deg=self._roll_deg, pitch_deg=self._pitch_deg,
            lat0=self._lat0, lon0=self._lon0, cos_lat0=self._cos_lat0
        )
        for idx, p in enumerate(self._plugins):
            if not self._plugin_health.get(idx, {}).get("enabled", True):
                continue
            try:
                p.on_close(ctx)
            except Exception:
                pass

    # ------------------ Sağlık yardımcıları ------------------

    def _mark_plugin_error(self, idx: int) -> None:
        h = self._plugin_health.get(idx)
        if not h:
            return
        now = time.time()
        # pencere dışındaki eski hataları sıfırla
        if (now - h.get("last_err_ts", 0.0)) > self._err_window_s:
            h["err_count"] = 0
        h["err_count"] = int(h.get("err_count", 0)) + 1
        h["last_err_ts"] = now
        if h["err_count"] >= self._err_threshold:
            h["enabled"] = False
            print(f"[Fuser] circuit-breaker: disabling plugin '{h.get('name')}' (idx={idx})")

    def _mark_plugin_timing(self, idx: int, dt_ms: float, phase: str) -> None:
        h = self._plugin_health.get(idx)
        if not h:
            return
        h["last_dt"] = float(dt_ms)
        # basit hz kestirimi
        if dt_ms > 0:
            inst_hz = 1000.0 / dt_ms
            # yumuşak güncelle
            h["hz"] = 0.2 * inst_hz + 0.8 * h.get("hz", 0.0)
        if dt_ms > self._slow_budget_ms:
            h["slow_count"] = int(h.get("slow_count", 0)) + 1

    # ------------------ Dış API ------------------

    def update(self, samples: List[object]) -> None:
        """
        Sensör örnekleri ile iç durumu günceller.
        Beklenen örnekler:
          - IMU: sensor == "imu", data: {"gz": rad/s, "roll_deg","pitch_deg" ...}
          - GPS: sensor == "gps", data: {"lat","lon","hdop",...} veya {"x","y","hdop",...}
          - Diğerleri: plugin’ler tarafından tüketilir (LiDAR, kamera, vb.)
        """
        now = time.time()
        self._last_samples = samples
        imu = None
        gps = None
        frame_inputs: List[Dict[str, Any]] = []

        for s in samples:
            sn = getattr(s, "sensor", None)
            if sn == "imu":
                imu = s
            elif sn == "gps":
                gps = s

        # IMU → yaw integrasyonu + roll/pitch okuma
        if imu is not None:
            idata = (imu.data or {})
            try:
                gz = float(idata.get("gz", 0.0))  # rad/s
            except Exception:
                gz = 0.0

            # roll_deg / pitch_deg (çeşitli alan isimlerine tolerans)
            try:
                if "roll_deg" in idata:
                    self._roll_deg = float(idata["roll_deg"])
                elif "roll" in idata:
                    self._roll_deg = float(idata["roll"])
                elif "roll_rad" in idata:
                    self._roll_deg = float(idata["roll_rad"]) * _RAD2DEG
            except Exception:
                pass
            try:
                if "pitch_deg" in idata:
                    self._pitch_deg = float(idata["pitch_deg"])
                elif "pitch" in idata:
                    self._pitch_deg = float(idata["pitch"])
                elif "pitch_rad" in idata:
                    self._pitch_deg = float(idata["pitch_rad"]) * _RAD2DEG
            except Exception:
                pass

            self._yaw_rate = gz
            if self._last_t is not None:
                dt = max(0.0, now - self._last_t)
                self._yaw_deg = (self._yaw_deg + gz * _RAD2DEG * dt) % 360.0
            self._last_t = now

            frame_inputs.append({
                "sensor": "imu",
                "source": getattr(imu, "source", "imu0"),
                "roll_deg": round(self._roll_deg, 2),
                "pitch_deg": round(self._pitch_deg, 2),
            })

        # GPS → pozisyon (XY) + heading düzeltmesi + hız (vx,vy)
        if gps is not None:
            gd = (gps.data or {})
            lat_raw = gd.get("lat")
            lon_raw = gd.get("lon")
            hdop_raw = gd.get("hdop", 99.9)

            gx = gy = None
            # 1) Tercihen lat/lon → XY
            if (lat_raw is not None) and (lon_raw is not None):
                try:
                    lat = float(lat_raw); lon = float(lon_raw)
                    gx, gy = self._xy_from_latlon(lat, lon)
                except Exception:
                    gx = gy = None
            # 2) Yoksa x/y fallback
            if (gx is None or gy is None) and ("x" in gd) and ("y" in gd):
                try:
                    gx = float(gd["x"]); gy = float(gd["y"])
                except Exception:
                    gx = gy = None

            if (gx is not None) and (gy is not None):
                try:
                    hdop = float(hdop_raw) if hdop_raw is not None else 99.9

                    # Heading düzeltmesi + hız tahmini (hem lat/lon hem x/y için ortak)
                    if self._last_gps is not None:
                        dtg = now - self._last_gps["t"]
                        if self._gps_dt_min <= dtg <= self._gps_dt_max:
                            vx_raw = (gx - self._last_gps["x"]) / dtg
                            vy_raw = (gy - self._last_gps["y"]) / dtg

                            # EMA ile yumuşat
                            self._vx = self._ema(self._vx, vx_raw, self._vel_ema_alpha)
                            self._vy = self._ema(self._vy, vy_raw, self._vel_ema_alpha)

                            # Küçük gürültüleri bastır
                            if abs(self._vx) < self._speed_floor: self._vx = 0.0
                            if abs(self._vy) < self._speed_floor: self._vy = 0.0

                            # GPS hız vektöründen heading
                            if (self._vx != 0.0) or (self._vy != 0.0):
                                gps_heading_deg = (math.atan2(self._vy, self._vx) * _RAD2DEG) % 360.0
                                self._yaw_deg = (self.heading_alpha * self._yaw_deg +
                                                 (1.0 - self.heading_alpha) * gps_heading_deg) % 360.0

                    # Pozisyonu güncelle
                    self._x, self._y = gx, gy
                    self._last_gps = {"t": now, "x": gx, "y": gy, "hdop": hdop,
                                      "source": getattr(gps, "source", "gps0")}
                    frame_inputs.append({"sensor": "gps", "source": self._last_gps["source"], "hdop": hdop})
                except Exception:
                    # GPS verisi bozuksa atla
                    pass

        # --- Plugin çağrıları (samples aşaması) ---
        ctx = FusionContext(
            now=now,
            x=self._x, y=self._y, z=self._z,
            yaw_deg=self._yaw_deg, yaw_rate=self._yaw_rate,
            vx=self._vx, vy=self._vy,
            roll_deg=self._roll_deg, pitch_deg=self._pitch_deg,
            lat0=self._lat0, lon0=self._lon0, cos_lat0=self._cos_lat0
        )
        self._ensure_plugins_inited(ctx)

        for idx, p in enumerate(self._plugins):
            h = self._plugin_health.get(idx)
            if h and not h.get("enabled", True):
                continue  # circuit-breaker ile kapatılmış

            # max_hz oran kontrolü
            max_hz = getattr(p, "max_hz", None)
            if isinstance(max_hz, (int, float)) and max_hz > 0:
                last = self._plugin_last_ts.get(idx, 0.0)
                if now - last < (1.0 / max_hz) - 1e-6:
                    continue  # bu karede atla
                self._plugin_last_ts[idx] = now

            t0 = time.perf_counter()
            try:
                p.on_samples(ctx, samples)
            except AttributeError:
                try:
                    getattr(p, "on_samples")(ctx, samples)  # type: ignore
                except Exception as e:
                    print(f"[Fuser] plugin samples fallback error {getattr(p,'name','?')}: {e}")
                    self._mark_plugin_error(idx)
            except Exception as e:
                print(f"[Fuser] plugin samples error {getattr(p,'name','?')}: {e}")
                self._mark_plugin_error(idx)
            dt_ms = (time.perf_counter() - t0) * 1000.0
            self._mark_plugin_timing(idx, dt_ms, "samples")

        # Plugin’lerin poz düzeltmelerini uygula
        for corr in ctx._drain_pose_corrections():
            self._x += corr.get("dx", 0.0)
            self._y += corr.get("dy", 0.0)
            self._yaw_deg = (self._yaw_deg + corr.get("dyaw_deg", 0.0)) % 360.0

        # Emit sırasında yazılacak inputs’u güncelle
        if frame_inputs:
            self._last_emit_inputs = frame_inputs

        # Fuser’ın referans projeksiyonunu ctx üzerinden güncel tut
        self._lat0 = ctx.lat0
        self._lon0 = ctx.lon0
        self._cos_lat0 = ctx.cos_lat0

        # ctx’yi sakla
        self._ctx_last = ctx

    def maybe_emit(self) -> Optional[FusedState]:
        """
        Periyodik olarak FusedState üretir. Periyot dolmadıysa None döner.
        """
        now = time.time()
        if now < self._next_emit_ts:
            return None
        self._next_emit_ts = now + self.period

        pose = {"x": self._x, "y": self._y, "z": self._z, "yaw": self._yaw_deg}  # yaw derece
        twist = {"vx": self._vx, "vy": self._vy, "vz": 0.0, "yaw_rate": self._yaw_rate}  # yaw_rate rad/s

        # Basit güven: GPS varsa 1.0, yoksa 0.6
        confidence = 1.0 if self._last_gps is not None else 0.6
        fused = FusedState(
            pose=pose,
            twist=twist,
            landmarks=[],
            confidence=confidence,
            inputs=list(self._last_emit_inputs) if self._last_emit_inputs else []
        )

        # Bu frame’in context’i
        ctx = self._ctx_last or FusionContext(
            now=now, x=self._x, y=self._y, z=self._z,
            yaw_deg=self._yaw_deg, yaw_rate=self._yaw_rate,
            vx=self._vx, vy=self._vy,
            roll_deg=self._roll_deg, pitch_deg=self._pitch_deg,
            lat0=self._lat0, lon0=self._lon0, cos_lat0=self._cos_lat0
        )

        # --- Plugin’lere son dokunuş izni ---
        for idx, p in enumerate(self._plugins):
            h = self._plugin_health.get(idx)
            if h and not h.get("enabled", True):
                continue

            # max_hz oran kontrolü (emit fazı için ~idx anahtarı)
            max_hz = getattr(p, "max_hz", None)
            if isinstance(max_hz, (int, float)) and max_hz > 0:
                last = self._plugin_last_ts.get(~idx, 0.0)
                if now - last < (1.0 / max_hz) - 1e-6:
                    continue
                self._plugin_last_ts[~idx] = now

            t0 = time.perf_counter()
            try:
                p.on_before_emit(ctx, fused)
            except AttributeError:
                try:
                    getattr(p, "before_emit")(ctx, fused)  # type: ignore
                except Exception as e:
                    print(f"[Fuser] plugin before_emit fallback error {getattr(p,'name','?')}: {e}")
                    self._mark_plugin_error(idx)
            except Exception as e:
                print(f"[Fuser] plugin on_before_emit error {getattr(p,'name','?')}: {e}")
                self._mark_plugin_error(idx)
            dt_ms = (time.perf_counter() - t0) * 1000.0
            self._mark_plugin_timing(idx, dt_ms, "emit")

        # ctx’den gelen landmark’ları ve input ipuçlarını ekle
        if ctx.landmarks_out:
            fused.landmarks.extend(ctx.landmarks_out)
            ctx.landmarks_out.clear()
        if ctx.inputs_out:
            fused.inputs.extend(ctx.inputs_out)
            ctx.inputs_out.clear()

        # Plugin health’i telemetriye ekle (özet)
        if self._plugin_health:
            health_summary = {
                h.get("name", f"p{idx}"): {
                    "enabled": h.get("enabled", True),
                    "hz": round(h.get("hz", 0.0), 2),
                    "slow": int(h.get("slow_count", 0)),
                    "err": int(h.get("err_count", 0)),
                }
                for idx, h in self._plugin_health.items()
            }
            fused.inputs.append({"_source": "plugin_health", "data": health_summary})

        return fused