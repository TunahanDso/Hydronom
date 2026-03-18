# tests/smoke_ekf.py (güncelle)
import time, math
from core.sample import Sample
from fusion.fuser import Fuser
from fusion.plugins.registry import make_plugins

fuser = Fuser(period_hz=10.0, plugins=make_plugins(["ekf"]))

lat0, lon0 = 41.000000, 29.000000
cos_lat = math.cos(math.radians(lat0))
M_PER_DEG_LAT = 111_320.0
M_PER_DEG_LON = 111_320.0 * cos_lat

x_m = 0.0
t0 = time.time()
for k in range(20):
    now = time.time()
    t0 = now

    imu = Sample(sensor="imu", source="imu0", data={"gz": 0.05})
    x_m += 0.2  # 20 cm ileri (doğu)
    lat = lat0
    lon = lon0 + (x_m / M_PER_DEG_LON)

    gps = Sample(sensor="gps", source="gps0", data={"lat": lat, "lon": lon, "hdop": 1.0})

    fuser.update([imu, gps])
    out = fuser.maybe_emit()
    if out:
        print(f"pose=({out.pose['x']:.2f},{out.pose['y']:.2f}) yaw={out.pose['yaw']:.1f}° "
              f"landmarks={len(out.landmarks)} inputs={len(out.inputs)}")
    time.sleep(0.1)
