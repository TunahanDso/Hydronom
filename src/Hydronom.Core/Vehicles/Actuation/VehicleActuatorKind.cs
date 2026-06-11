namespace Hydronom.Core.Vehicles.Actuation
{
    /// <summary>
    /// Vehicle Profile içindeki aktüatör ailelerini tanımlar.
    ///
    /// İlk hedef thruster/motor tabanlı deniz araçlarıdır.
    /// İleride:
    /// - Servo
    /// - Rudder
    /// - Wheel
    /// - Joint
    /// - Pump
    /// - Industrial actuator
    /// aynı sistemin içine alınabilir.
    /// </summary>
    public enum VehicleActuatorKind
    {
        Unknown = 0,

        Thruster = 10,
        Propeller = 11,
        Rudder = 12,

        Wheel = 20,
        Steering = 21,

        Servo = 30,
        LinearActuator = 31,
        RoboticJoint = 32,

        Pump = 40,
        Valve = 41,

        IndustrialMotor = 50,

        Custom = 1000
    }
}