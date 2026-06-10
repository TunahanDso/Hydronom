namespace Hydronom.Runtime.Sensors.Diagnostics;

/// <summary>
/// Runtime seviyesinde bir capability'nin görev için kullanılabilirlik durumu.
/// </summary>
public enum RuntimeSensorCapabilityStatus
{
    Unknown = 0,

    /// <summary>
    /// Capability hiç yok ya da güveni sıfıra yakın.
    /// </summary>
    Missing = 1,

    /// <summary>
    /// Capability var ama düşük güvenli. Göreve göre dikkatli kullanılmalıdır.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// Capability kullanılabilir ve güveni normal/yüksek seviyededir.
    /// </summary>
    Available = 3
}