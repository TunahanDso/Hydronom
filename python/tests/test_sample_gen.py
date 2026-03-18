# tests/test_sample_gen.py
from sensors.imu_sensor import ImuSensor

if __name__ == "__main__":
    imu = ImuSensor()
    imu.open()
    for _ in range(3):
        sample = imu.read()
        print(sample.to_json().strip())
    imu.close()
