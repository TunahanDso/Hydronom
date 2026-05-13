using HydronomOps.Gateway.Contracts.Vehicle;

namespace HydronomOps.Gateway.Infrastructure.TcpIngress;

public sealed partial class RuntimeFrameParser
{
    private VehicleTelemetryDto GetOrCreateVehicleTelemetry()
    {
        var current = _stateStore.GetVehicleTelemetry();
        if (current is not null)
        {
            return CloneVehicleTelemetry(current);
        }

        return new VehicleTelemetryDto
        {
            TimestampUtc = DateTime.UtcNow,
            VehicleId = "hydronom-main",
            Obstacles = new List<ObstacleDto>(),
            Landmarks = new List<LandmarkDto>(),
            Metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static VehicleTelemetryDto CloneVehicleTelemetry(VehicleTelemetryDto source)
    {
        return new VehicleTelemetryDto
        {
            TimestampUtc = source.TimestampUtc,
            VehicleId = source.VehicleId,
            X = source.X,
            Y = source.Y,
            Z = source.Z,
            RollDeg = source.RollDeg,
            PitchDeg = source.PitchDeg,
            YawDeg = source.YawDeg,
            HeadingDeg = source.HeadingDeg,
            Vx = source.Vx,
            Vy = source.Vy,
            Vz = source.Vz,
            RollRateDeg = source.RollRateDeg,
            PitchRateDeg = source.PitchRateDeg,
            YawRateDeg = source.YawRateDeg,
            TargetX = source.TargetX,
            TargetY = source.TargetY,
            DistanceToGoalM = source.DistanceToGoalM,
            HeadingErrorDeg = source.HeadingErrorDeg,
            ObstacleAhead = source.ObstacleAhead,
            ObstacleCount = source.ObstacleCount,
            Obstacles = CloneObstacles(source.Obstacles),
            Landmarks = CloneLandmarks(source.Landmarks),
            Metrics = source.Metrics is null ? null : new Dictionary<string, double>(source.Metrics),
            Fields = source.Fields is null ? null : new Dictionary<string, string>(source.Fields),
            Freshness = source.Freshness
        };
    }

    private static List<ObstacleDto> CloneObstacles(IReadOnlyCollection<ObstacleDto>? source)
    {
        if (source is null || source.Count == 0)
        {
            return new List<ObstacleDto>();
        }

        return source
            .Select(o => new ObstacleDto
            {
                X = o.X,
                Y = o.Y,
                R = o.R
            })
            .ToList();
    }

    private static List<LandmarkDto> CloneLandmarks(IReadOnlyCollection<LandmarkDto>? source)
    {
        if (source is null || source.Count == 0)
        {
            return new List<LandmarkDto>();
        }

        return source.Select(CloneLandmark).ToList();
    }

    private static LandmarkDto CloneLandmark(LandmarkDto source)
    {
        return new LandmarkDto
        {
            Id = source.Id,
            Type = source.Type,
            Shape = source.Shape,
            Points = source.Points is null
                ? new List<ObstaclePointDto>()
                : source.Points
                    .Select(p => new ObstaclePointDto
                    {
                        X = p.X,
                        Y = p.Y
                    })
                    .ToList(),
            Style = source.Style is null
                ? null
                : new LandmarkStyleDto
                {
                    Color = source.Style.Color,
                    Width = source.Style.Width,
                    Radius = source.Style.Radius,
                    Label = source.Style.Label,
                    Fields = source.Style.Fields is null
                        ? new Dictionary<string, string>()
                        : new Dictionary<string, string>(source.Style.Fields)
                },
            Metrics = source.Metrics is null
                ? new Dictionary<string, double>()
                : new Dictionary<string, double>(source.Metrics),
            Fields = source.Fields is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(source.Fields)
        };
    }
}