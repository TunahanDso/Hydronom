# sensors/sensor_manager.py
import os
import time
import platform
from typing import Any, Dict, Iterable, List, Optional, Tuple

# MODÜLER IMU/GPS/LiDAR/Kamera
from sensors.imu import ImuSensor, ImuConfig
from sensors.gps import GpsSensor, GpsConfig
from sensors.lidar import LidarSensor, LidarConfig
from sensors.camera import CameraSensor, CameraConfig


def _has_pyserial() -> bool:
    try:
        import serial  # noqa: F401
        return True
    except Exception:
        return False


def _has_rplidar() -> bool:
    try:
        import rplidar  # noqa: F401
        return True
    except Exception:
        return False


def _has_opencv() -> bool:
    try:
        import cv2  # noqa: F401
        return True
    except Exception:
        return False


def _is_windows() -> bool:
    try:
        return platform.system().lower().startswith("win")
    except Exception:
        return os.name == "nt"


IS_WINDOWS = _is_windows()


def _iter_serial_ports() -> List[str]:
    """
    Sistemde gerçekten görünen seri portları listeler.
    Windows + Linux için serial.tools.list_ports kullanır.
    """
    try:
        import serial.tools.list_ports as list_ports  # type: ignore
        return [p.device for p in list_ports.comports()]
    except Exception:
        return []


class SensorManager:
    """
    Akıllı sensör yöneticisi.
    - Jetson/PC'de donanımı bulur, yoksa simülasyona düşer (HYDRONOM_FORCE_SIM=1 ile zorlanabilir).
    - HYDRONOM_DISABLE_* ile başlangıçta sensör devre dışı bırakılabilir.
    - IMU/GPS/LiDAR/Kamera için backend/env tabanlı seçim ve hız/FOV/menzil/çözünürlük ayarı yapılır.
    - StreamSubscribe ile canlı parametre güncellemeleri desteklenir.
    - Paket/donanım yoksa otomatik sim seçimi, open() hatasında sıcak sim fallback.
    - OS (Windows/Linux) platformunu algılar; varsayılan portları platforma göre seçer.
    """

    def __init__(self, debug: bool = False):
        self.sensors: List[Any] = []
        # Env ile debug açılabilsin
        self.debug = debug or os.getenv("HYDRONOM_SENSOR_DEBUG", "0") == "1"

        # Platform tespiti (eski davranış korunuyor)
        self.platform = platform.system()
        self.is_windows = IS_WINDOWS
        if self.debug:
            print(f"[DBG] Platform: {self.platform} (is_windows={self.is_windows})")

        self.detect_hardware()

    # ------------------------- Donanım tespiti -------------------------

    def _exists(self, path: str) -> bool:
        """OS bağımlı cihaz yolu kontrolü (sadece kaba ipucu)."""
        try:
            return os.path.exists(path)
        except Exception:
            return False

    def _any_com_exists(self, start=1, end=21) -> bool:
        """
        Windows'ta os.path.exists('COMx') çoğu zaman güvenilir değil.
        Önce serial.tools.list_ports ile bak, yoksa eski kaba yönteme düş.
        """
        ports = _iter_serial_ports()
        if ports:
            return True
        return any(self._exists(f"COM{n}") for n in range(start, end))

    def detect_hardware(self):
        print("🔍 Sensörler algılanıyor...")

        force_sim = os.getenv("HYDRONOM_FORCE_SIM", "0") == "1"

        have_pyserial = _has_pyserial()
        have_rplidar = _has_rplidar()
        have_opencv = _has_opencv()

        if self.debug:
            ports = _iter_serial_ports()
            print(f"[DBG] force_sim={force_sim} have_pyserial={have_pyserial} "
                  f"have_rplidar={have_rplidar} have_opencv={have_opencv} serial_ports={ports}")

        # ---------------- IMU (modüler) ----------------

        imu_disabled = os.getenv("HYDRONOM_DISABLE_IMU", "0") == "1"
        # Destekli env backend'leri: sim | csharp_sim | serial
        imu_backend_env = os.getenv("HYDRONOM_IMU_BACKEND", "").strip().lower()

        # Varsayılan port platforma göre seçiliyor
        imu_port_default = "COM3" if self.is_windows else "/dev/ttyUSB0"
        imu_port_env = os.getenv("HYDRONOM_IMU_PORT", imu_port_default)

        serial_ports = _iter_serial_ports()
        imu_port_exists = self._exists(imu_port_env) or bool(serial_ports)

        # Backend seçimi:
        if imu_backend_env in ("sim", "csharp_sim", "serial"):
            imu_backend = imu_backend_env
        else:
            if force_sim:
                imu_backend = "sim"
            else:
                # Port ipucu varsa serial, yoksa sim
                imu_backend = "serial" if (have_pyserial and imu_port_exists) else "sim"

        # Paket/donanım/force etkisi
        if force_sim or (imu_backend == "serial" and (not have_pyserial or not imu_port_exists)):
            imu_backend = "sim"

        imu_cfg = ImuConfig(
            backend=imu_backend,
            source="imu0",
            frame_id="base_link",
            rate_hz=float(os.getenv("HYDRONOM_IMU_RATE", "100")),
            port=imu_port_env,
            baud=int(os.getenv("HYDRONOM_IMU_BAUD", "115200")),
        )
        imu = ImuSensor(cfg=imu_cfg)
        imu.enabled = not imu_disabled
        # sim/real bayrağı → Capability için önemli (csharp_sim de sim kabul edilir)
        setattr(imu, "simulate", imu_backend in ("sim", "csharp_sim"))
        self.sensors.append(imu)
        print(f"IMU: backend={imu_backend} (enabled={imu.enabled})")
        if not have_pyserial and imu_backend == "serial":
            print("⚠️ pyserial bulunamadı; IMU serial backend open() sırasında sim’e düşecektir.")
        if self.debug:
            print(f"[DBG] IMU port={imu_port_env}, port_exists={imu_port_exists}, "
                  f"disabled={imu_disabled}, cfg={imu_cfg}")

        # ---------------- GPS (modüler) ----------------

        gps_disabled = os.getenv("HYDRONOM_DISABLE_GPS", "0") == "1"
        # Destekli env backend'leri: sim | csharp_sim | nmea | gpsd
        gps_backend_env = os.getenv("HYDRONOM_GPS_BACKEND", "").strip().lower()

        gps_port_default = "COM4" if self.is_windows else "/dev/ttyUSB0"
        gps_port_env = os.getenv("HYDRONOM_GPS_PORT", gps_port_default)

        gps_dev_exists = self._exists(gps_port_env) or bool(serial_ports)

        if gps_backend_env in ("sim", "csharp_sim", "nmea", "gpsd"):
            gps_backend = gps_backend_env
        else:
            if force_sim:
                gps_backend = "sim"
            else:
                gps_backend = "nmea" if (have_pyserial and gps_dev_exists) else "sim"

        if force_sim or (gps_backend in ("nmea", "gpsd") and (not have_pyserial or not gps_dev_exists)):
            gps_backend = "sim"

        gps_cfg = GpsConfig(
            backend=gps_backend,
            source="gps0",
            frame_id="map",
            rate_hz=float(os.getenv("HYDRONOM_GPS_RATE", "5")),
            port=gps_port_env,
            baud=int(os.getenv("HYDRONOM_GPS_BAUD", "9600")),
        )
        gps = GpsSensor(cfg=gps_cfg)
        gps.enabled = not gps_disabled
        # csharp_sim de sim kabul edilir
        setattr(gps, "simulate", gps_backend in ("sim", "csharp_sim"))
        self.sensors.append(gps)
        print(f"GPS: backend={gps_backend} (enabled={gps.enabled})")
        if not have_pyserial and gps_backend in ("nmea", "gpsd"):
            print("⚠️ pyserial yok; GPS backend open() sırasında büyük ihtimalle sim’e düşecektir.")
        if self.debug:
            print(f"[DBG] GPS port={gps_port_env}, dev_exists={gps_dev_exists}, "
                  f"disabled={gps_disabled}, cfg={gps_cfg}")

        # ---------------- Kamera (modüler) ----------------

        cam_disabled = os.getenv("HYDRONOM_DISABLE_CAMERA", "0") == "1"
        cam_backend_env = os.getenv("HYDRONOM_CAMERA_BACKEND", "").strip().lower()  # "sim"|"opencv"|"rtsp"
        cam_device_env = os.getenv("HYDRONOM_CAMERA_DEVICE",
                                   "0" if self.is_windows else "/dev/video0")
        cam_index_env = int(os.getenv("HYDRONOM_CAMERA_INDEX", "0"))
        cam_rtsp_url = os.getenv("HYDRONOM_CAMERA_RTSP", "").strip() or None
        cam_w = int(os.getenv("HYDRONOM_CAMERA_WIDTH", "640"))
        cam_h = int(os.getenv("HYDRONOM_CAMERA_HEIGHT", "480"))
        cam_fps = float(os.getenv("HYDRONOM_CAMERA_FPS", "15"))
        cam_color = os.getenv("HYDRONOM_CAMERA_COLOR", "bgr8")
        cam_inline_jpeg = os.getenv("HYDRONOM_CAMERA_INLINE_JPEG", "0") == "1"
        cam_jpeg_q = int(os.getenv("HYDRONOM_CAMERA_JPEG_Q", "85"))

        # Windows'ta /dev/video0 yok; kamera var/yok kararı open() + fallback üzerinden verilsin
        if self.is_windows:
            cam_dev_exists = True
        else:
            cam_dev_exists = self._exists(cam_device_env)

        if cam_backend_env in ("sim", "opencv", "rtsp"):
            cam_backend = cam_backend_env
        else:
            if cam_rtsp_url:
                cam_backend = "rtsp"
            else:
                cam_backend = "opencv" if cam_dev_exists else "sim"

        if force_sim or (cam_backend in ("opencv", "rtsp") and not have_opencv):
            cam_backend = "sim"

        cam_cfg = CameraConfig(
            backend=cam_backend,
            source="cam0",
            frame_id="camera_link",
            width=cam_w,
            height=cam_h,
            fps=cam_fps,
            color=cam_color,
            inline_jpeg=cam_inline_jpeg,
            jpeg_quality=cam_jpeg_q,
            device=cam_device_env,
            index=cam_index_env,
            rtsp_url=cam_rtsp_url,
        )
        cam = CameraSensor(cfg=cam_cfg)
        cam.enabled = not cam_disabled
        setattr(cam, "simulate", cam_backend == "sim")
        self.sensors.append(cam)
        print(f"Kamera: backend={cam_backend} (enabled={cam.enabled})")
        if not have_opencv and cam_backend != "sim":
            print("⚠️ OpenCV yok; kamera backend open() sırasında sim’e düşebilir.")
        if self.debug:
            print(f"[DBG] CAM dev={cam_device_env}, exists={cam_dev_exists}, "
                  f"disabled={cam_disabled}, cfg={cam_cfg}")

        # ---------------- LiDAR (modüler) ----------------

        lidar_disabled = os.getenv("HYDRONOM_DISABLE_LIDAR", "0") == "1"
        lidar_backend_env = os.getenv("HYDRONOM_LIDAR_BACKEND", "").strip().lower()  # sim|rplidar|ouster|ldrobot

        # Donanım var mı yok mu sadece ipucu; asıl karar open() + hot-fallback
        lidar_dev_exists = self._exists("/dev/ttyUSB1") or self._any_com_exists()

        if lidar_backend_env in ("sim", "rplidar", "ouster", "ldrobot"):
            lidar_backend = lidar_backend_env
        else:
            if force_sim:
                lidar_backend = "sim"
            else:
                # Varsayılan: rplidar paketi varsa ve ipucu varsa rplidar, yoksa sim
                lidar_backend = "rplidar" if (have_rplidar and lidar_dev_exists) else "sim"

        if force_sim or (lidar_backend == "rplidar" and not have_rplidar):
            lidar_backend = "sim"

        lidar_cfg = LidarConfig(
            source="lidar0",
            frame_id="lidar_link",
            backend=lidar_backend,
            rate_hz=float(os.getenv("HYDRONOM_LIDAR_RATE", "10")),
            fov_deg=float(os.getenv("HYDRONOM_LIDAR_FOV", "270")),
            angle_increment_deg=float(os.getenv("HYDRONOM_LIDAR_ANG_INC", "1.0")),
            range_min=float(os.getenv("HYDRONOM_LIDAR_MINR", "0.15")),
            range_max=float(os.getenv("HYDRONOM_LIDAR_MAXR", "30.0")),
        )
        lidar = LidarSensor(cfg=lidar_cfg)
        lidar.enabled = not lidar_disabled
        setattr(lidar, "simulate", lidar_backend == "sim")
        self.sensors.append(lidar)
        print(f"LiDAR: backend={lidar_backend}, {'Simülasyon' if lidar_backend=='sim' else 'Gerçek'} (enabled={lidar.enabled})")
        if not have_rplidar and lidar_backend != "sim":
            print("⚠️ 'rplidar' paketi yok; LiDAR open() sırasında sim’e düşebilir.")
        if self.debug:
            print(f"[DBG] LIDAR exists(ttyUSB1/COMx)={lidar_dev_exists}, disabled={lidar_disabled}, cfg={lidar_cfg}")

    # ------------------------- Yardımcı: Hot fallback -------------------------

    def _hot_fallback_to_sim(self, idx: int, err: Exception) -> bool:
        """
        open() hatasında çağrılır. Backend'i sim'e çevirip yeniden açmayı dener.
        True dönerse yer değiştirme yapıldı ve open başarılı denendi.
        """
        try:
            s = self.sensors[idx]
        except Exception:
            return False

        cfg = getattr(s, "cfg", None)
        backend_now = getattr(cfg, "backend", None) if cfg else None

        # Zaten sim ise yapacak bir şey yok
        if backend_now == "sim":
            return False

        print(f"⚠️ {getattr(s,'source','?')} için hot-fallback: {backend_now} → sim (sebep: {err})")

        # 1) Eğer sensör _switch_backend sağlıyorsa onu kullan
        if hasattr(s, "_switch_backend"):
            try:
                s._switch_backend("sim")  # type: ignore[attr-defined]
                if cfg and hasattr(cfg, "backend"):
                    cfg.backend = "sim"
                setattr(s, "simulate", True)
                s.open()
                return True
            except Exception:
                pass

        # 2) Aksi halde aynı tipten yeni bir sim sensör yarat
        try:
            if cfg and hasattr(cfg, "backend"):
                cfg.backend = "sim"
            name = self._name_of(s)
            if name == "imu":
                new = ImuSensor(cfg=cfg or ImuConfig(backend="sim", source="imu0", frame_id="base_link"))
            elif name == "gps":
                new = GpsSensor(cfg=cfg or GpsConfig(backend="sim", source="gps0", frame_id="map"))
            elif name == "camera":
                new = CameraSensor(cfg=cfg or CameraConfig(backend="sim", source="cam0", frame_id="camera_link"))
            elif name == "lidar":
                new = LidarSensor(cfg=cfg or LidarConfig(backend="sim", source="lidar0", frame_id="lidar_link"))
            else:
                return False

            new.enabled = getattr(s, "enabled", True)
            setattr(new, "simulate", True)
            self.sensors[idx] = new
            new.open()
            return True
        except Exception as e2:
            print(f"⚠️ Hot-fallback başarısız: {e2}")
            return False

    # ------------------------- Yaşam döngüsü -------------------------

    def open_all(self):
        for i, s in enumerate(self.sensors):
            if not getattr(s, "enabled", True):
                if self.debug:
                    print(f"[DBG] skip_open disabled source={getattr(s,'source',None)}")
                continue
            try:
                s.open()
                if self.debug:
                    print(
                        f"[DBG] open_ok source={getattr(s,'source',None)} "
                        f"frame={getattr(s,'frame_id',None)} "
                        f"simulate={getattr(s,'simulate',None)} "
                        f"enabled={getattr(s,'enabled',True)}"
                    )
            except Exception as e:
                print(f"⚠️ {getattr(s,'source','?')} sensörü açılırken hata: {e}")
                # Otomatik olarak sim'e düş ve yeniden dene
                if self._hot_fallback_to_sim(i, e):
                    if self.debug:
                        print(f"[DBG] hot-fallback to sim OK for {getattr(self.sensors[i],'source',None)}")

    def read_all(self):
        samples = []
        for s in self.sensors:
            if not getattr(s, "enabled", True):
                continue
            try:
                sample = s.read()
                samples.append(sample)
            except Exception as e:
                print(f"⚠️ {getattr(s,'source','?')} sensöründe okuma hatası: {e}")
        return samples

    def close_all(self):
        for s in self.sensors:
            try:
                s.close()
                if self.debug:
                    print(f"[DBG] close_ok source={getattr(s,'source',None)}")
            except Exception as e:
                print(f"⚠️ {getattr(s,'source','?')} sensörü kapanırken hata: {e}")

    # ------------------------- Capability özeti -------------------------

    def _name_of(self, s) -> str:
        name = getattr(s, "kind", None) or getattr(s, "SENSOR", None) or getattr(s, "sensor", None)
        if isinstance(name, str) and name:
            return name.lower()
        cls = type(s).__name__
        return cls[:-6].lower() if cls.endswith("Sensor") else cls.lower()

    def _sensor_key(self, s) -> Tuple[str, Optional[str]]:
        return (self._name_of(s), getattr(s, "source", None))

    def _get_rate_hz_of(self, s) -> Optional[float]:
        v = getattr(s, "rate_hz", None) or getattr(s, "poll_hz", None) or getattr(s, "sample_rate_hz", None)
        if v is not None:
            return v
        cfg = getattr(s, "cfg", None)
        # Kamera için fps
        fps = getattr(cfg, "fps", None) if cfg is not None else None
        return fps if fps is not None else (getattr(cfg, "rate_hz", None) if cfg is not None else None)

    def describe_for_capability(self):
        desc: List[Dict[str, Any]] = []
        for s in self.sensors:
            try:
                item = {
                    "sensor": self._name_of(s),
                    "source": getattr(s, "source", None),
                    "frame_id": getattr(s, "frame_id", None),
                    "rate_hz": self._get_rate_hz_of(s),
                    "simulate": getattr(s, "simulate", None),
                    "enabled": getattr(s, "enabled", True),
                    "fields": getattr(s, "fields", None),
                    "calib_id": getattr(s, "calib_id", None),
                    "quality_hints": getattr(s, "quality_hints", None)
                }

                cfg = getattr(s, "cfg", None)

                def cfg_get(k):
                    return getattr(s, k, None) if hasattr(s, k) else (getattr(cfg, k, None) if cfg else None)

                # Ortak
                for k in ("backend",):
                    v = cfg_get(k)
                    if v is not None:
                        item[k] = v

                # LiDAR özel
                for k in ("fov_deg", "angle_increment_deg", "range_min", "range_max"):
                    v = cfg_get(k)
                    if v is not None:
                        item[k] = v

                # GPS/IMU port/baud
                for k in ("port", "baud"):
                    v = cfg_get(k)
                    if v is not None:
                        item[k] = v

                # Kamera özel
                for k in ("width", "height", "fps", "color", "inline_jpeg",
                          "jpeg_quality", "device", "index", "rtsp_url"):
                    v = cfg_get(k)
                    if v is not None:
                        item[k] = v

                item = {k: v for k, v in item.items() if v is not None}
                desc.append(item)
            except Exception:
                continue

        desc.sort(key=lambda d: (d.get("sensor", ""), d.get("source", "")))
        return desc

    # ------------------------- Subscribe uygulama -------------------------

    def _find_matching(self, name: Optional[str], source: Optional[str]) -> Iterable[Any]:
        for s in self.sensors:
            n = self._name_of(s)
            src = getattr(s, "source", None)
            if name is not None and n != name:
                continue
            if source is not None and src != source:
                continue
            yield s

    def _try_set_rate(self, s: Any, hz: float) -> bool:
        try:
            if hasattr(s, "set_rate_hz"):
                s.set_rate_hz(float(hz))
                return True
            if hasattr(s, "set_rate"):
                s.set_rate(float(hz))
                return True
            for attr in ("rate_hz", "poll_hz", "sample_rate_hz"):
                if hasattr(s, attr):
                    setattr(s, attr, float(hz))
                    return True
            # cfg fallback
            cfg = getattr(s, "cfg", None)
            if cfg:
                # kamera için fps
                if hasattr(cfg, "fps"):
                    setattr(cfg, "fps", float(hz))
                    return True
                if hasattr(cfg, "rate_hz"):
                    setattr(cfg, "rate_hz", float(hz))
                    return True
        except Exception as e:
            if self.debug:
                print(f"[DBG] set_rate error on {getattr(s,'source','?')}: {e}")
        return False

    def apply_stream_subscribe(self, msg: Dict[str, Any]) -> Dict[str, Any]:
        """
        Toplu sensör ayarları.
        """
        summary: Dict[str, Any] = {"changed": []}

        # 1) Toplu enable/disable
        if "enable_all" in msg:
            val = bool(msg["enable_all"])
            for s in self.sensors:
                setattr(s, "enabled", val)
            summary["enable_all"] = val

        # 2) Basit disable listesi
        disabled = msg.get("disable")
        if isinstance(disabled, list):
            for token in disabled:
                token = str(token).lower()
                for s in self.sensors:
                    name = self._name_of(s)
                    src = (getattr(s, "source", None) or "").lower()
                    if token in (name, src):
                        setattr(s, "enabled", False)
                        summary["changed"].append(
                            {"sensor": name, "source": getattr(s, "source", None), "enabled": False}
                        )

        # 3) Ayrıntılı sensör konfigleri
        cfg_list = msg.get("sensors")
        if isinstance(cfg_list, list):
            for cfg in cfg_list:
                if not isinstance(cfg, dict):
                    continue
                name = cfg.get("sensor")
                source = cfg.get("source")
                if isinstance(name, str):
                    name = name.lower()

                target_sensors = list(self._find_matching(name, source))
                for s in target_sensors:
                    change = {"sensor": self._name_of(s), "source": getattr(s, "source", None)}

                    # enable/disable
                    if "enable" in cfg:
                        en = bool(cfg["enable"])
                        setattr(s, "enabled", en)
                        change["enabled"] = en

                    # rate (kamera için fps)
                    if "rate_hz" in cfg:
                        hz = cfg["rate_hz"]
                        if isinstance(hz, (int, float)) and hz > 0:
                            ok = self._try_set_rate(s, float(hz))
                            change["rate_hz"] = float(hz)
                            change["rate_set"] = bool(ok)
                    if "fps" in cfg and "rate_hz" not in cfg:
                        hz = cfg["fps"]
                        if isinstance(hz, (int, float)) and hz > 0:
                            ok = self._try_set_rate(s, float(hz))
                            change["fps"] = float(hz)
                            change["rate_set"] = bool(ok)

                    # Ortak backend/port/baud (varsa s üzerinde ya da cfg üzerinde set et)
                    for k in ("backend", "port", "baud"):
                        if k in cfg:
                            try:
                                if hasattr(s, k):
                                    setattr(s, k, cfg[k])
                                elif hasattr(getattr(s, "cfg", object()), k):
                                    setattr(s.cfg, k, cfg[k])  # type: ignore[attr-defined]
                                change[k] = cfg[k]
                            except Exception as e:
                                change[f"{k}_error"] = str(e)

                    # Kamera özel
                    if self._name_of(s) == "camera":
                        for key in ("width", "height", "color", "inline_jpeg",
                                    "jpeg_quality", "device", "index", "rtsp_url"):
                            if key in cfg:
                                try:
                                    if hasattr(s, key):
                                        setattr(s, key, cfg[key])
                                    elif hasattr(getattr(s, "cfg", object()), key):
                                        setattr(s.cfg, key, cfg[key])  # type: ignore[attr-defined]
                                    change[key] = cfg[key]
                                except Exception as e:
                                    change[f"{key}_error"] = str(e)

                    # LiDAR özel
                    if self._name_of(s) == "lidar":
                        for key in ("fov_deg", "angle_increment_deg", "range_min", "range_max"):
                            if key in cfg:
                                try:
                                    if hasattr(s, key):
                                        setattr(s, key, cfg[key])
                                    elif hasattr(getattr(s, "cfg", object()), key):
                                        setattr(s.cfg, key, cfg[key])  # type: ignore[attr-defined]
                                    change[key] = cfg[key]
                                except Exception as e:
                                    change[f"{key}_error"] = str(e)

                    summary["changed"].append(change)

        if self.debug:
            print(f"[DBG] apply_stream_subscribe summary: {summary}")
        return summary