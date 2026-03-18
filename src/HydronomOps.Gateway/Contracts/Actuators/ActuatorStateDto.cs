using System;
using System.Collections.Generic;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Actuators;

/// <summary>
/// Aktüatör durum özetini taşır.
/// </summary>
public sealed class ActuatorStateDto
{
    /// <summary>
    /// Paket zamanı.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Araç kimliği.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// Aktüatör adı.
    /// Örn: FL, FR, RL, RR.
    /// </summary>
    public string ActuatorName { get; set; } = string.Empty;

    /// <summary>
    /// Aktüatör tipi.
    /// Örn: thruster, rudder, servo.
    /// </summary>
    public string ActuatorType { get; set; } = "thruster";

    /// <summary>
    /// Aktüatör etkin mi.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Aktüatör sağlıklı mı.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Uygulanan normalize komut değeri.
    /// Genelde -1 ile +1 arası.
    /// </summary>
    public double Command { get; set; }

    /// <summary>
    /// Ham PWM / mikro saniye gibi düşük seviye komut.
    /// </summary>
    public double? RawCommand { get; set; }

    /// <summary>
    /// Tahmini veya ölçülen RPM.
    /// </summary>
    public double? Rpm { get; set; }

    /// <summary>
    /// Ölçülen veya tahmini akım.
    /// </summary>
    public double? CurrentMa { get; set; }

    /// <summary>
    /// Ölçülen veya tahmini voltaj.
    /// </summary>
    public double? Voltage { get; set; }

    /// <summary>
    /// Ölçülen veya tahmini sıcaklık.
    /// </summary>
    public double? TemperatureC { get; set; }

    /// <summary>
    /// Son hata mesajı.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Aktüatöre ait sayısal ek metrikler.
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new();

    /// <summary>
    /// Aktüatöre ait metinsel alanlar.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new();

    /// <summary>
    /// Veri tazelik özeti.
    /// </summary>
    public FreshnessDto? Freshness { get; set; }
}