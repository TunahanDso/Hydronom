using System;
using System.Collections.Generic;
using HydronomOps.Gateway.Contracts.Common;

namespace HydronomOps.Gateway.Contracts.Actuators;

/// <summary>
/// AktÃ¼atÃ¶r durum Ã¶zetini taÅŸÄ±r.
/// </summary>
public sealed class ActuatorStateDto
{
    /// <summary>
    /// Paket zamanÄ±.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// AraÃ§ kimliÄŸi.
    /// </summary>
    public string VehicleId { get; set; } = "hydronom-main";

    /// <summary>
    /// AktÃ¼atÃ¶r adÄ±.
    /// Ã–rn: FL, FR, RL, RR.
    /// </summary>
    public string ActuatorName { get; set; } = string.Empty;

    /// <summary>
    /// AktÃ¼atÃ¶r tipi.
    /// Ã–rn: thruster, rudder, servo.
    /// </summary>
    public string ActuatorType { get; set; } = "thruster";

    /// <summary>
    /// AktÃ¼atÃ¶r etkin mi.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// AktÃ¼atÃ¶r saÄŸlÄ±klÄ± mÄ±.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Uygulanan normalize komut deÄŸeri.
    /// Genelde -1 ile +1 arasÄ±.
    /// </summary>
    public double Command { get; set; }

    /// <summary>
    /// Ham PWM / mikro saniye gibi dÃ¼ÅŸÃ¼k seviye komut.
    /// </summary>
    public double? RawCommand { get; set; }

    /// <summary>
    /// Tahmini veya Ã¶lÃ§Ã¼len RPM.
    /// </summary>
    public double? Rpm { get; set; }

    /// <summary>
    /// Ã–lÃ§Ã¼len veya tahmini akÄ±m.
    /// </summary>
    public double? CurrentMa { get; set; }

    /// <summary>
    /// Ã–lÃ§Ã¼len veya tahmini voltaj.
    /// </summary>
    public double? Voltage { get; set; }

    /// <summary>
    /// Ã–lÃ§Ã¼len veya tahmini sÄ±caklÄ±k.
    /// </summary>
    public double? TemperatureC { get; set; }

    /// <summary>
    /// Son hata mesajÄ±.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// AktÃ¼atÃ¶re ait sayÄ±sal ek metrikler.
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new();

    /// <summary>
    /// AktÃ¼atÃ¶re ait metinsel alanlar.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new();

    /// <summary>
    /// Veri tazelik Ã¶zeti.
    /// </summary>
    public FreshnessDto? Freshness { get; set; }
}
