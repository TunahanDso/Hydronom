using HydronomOps.Gateway.Infrastructure.Time;

namespace HydronomOps.Gateway.Services.Mapping;

/// <summary>
/// Runtime'tan gelen ham JSON verisini gateway DTO'larına dönüştürür.
/// Bu sınıf bilinçli olarak partial tutulur; vehicle, state, geometry,
/// occupancy, runtime summary ve helper bölümleri ayrı dosyalardadır.
/// </summary>
public sealed partial class RuntimeToGatewayMapper
{
    private readonly ISystemClock _clock;

    public RuntimeToGatewayMapper(ISystemClock clock)
    {
        _clock = clock;
    }
}
