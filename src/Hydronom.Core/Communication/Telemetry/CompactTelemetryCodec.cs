using System.Text;

namespace Hydronom.Core.Communication.Telemetry;

public sealed class CompactTelemetryCodec
{
    private const byte Magic0 = 0x48;
    private const byte Magic1 = 0x54;
    private const ushort Version = 1;

    public string CodecName => "hydronom-compact-telemetry-v1";

    public byte[] Encode(CompactTelemetryFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(Magic0);
        writer.Write(Magic1);
        writer.Write(Version);

        WriteString(writer, frame.VehicleId);

        writer.Write(frame.Sequence);
        writer.Write(frame.TimestampUnixMs);
        writer.Write((ulong)frame.FieldMask);

        if (frame.Has(CompactTelemetryField.PositionX))
        {
            writer.Write(CompactTelemetryQuantizer.MetersToCentimeters(frame.PositionXM));
        }

        if (frame.Has(CompactTelemetryField.PositionY))
        {
            writer.Write(CompactTelemetryQuantizer.MetersToCentimeters(frame.PositionYM));
        }

        if (frame.Has(CompactTelemetryField.PositionZ))
        {
            writer.Write(CompactTelemetryQuantizer.MetersToCentimeters(frame.PositionZM));
        }

        if (frame.Has(CompactTelemetryField.Roll))
        {
            writer.Write(CompactTelemetryQuantizer.RadToMillirad(frame.RollRad));
        }

        if (frame.Has(CompactTelemetryField.Pitch))
        {
            writer.Write(CompactTelemetryQuantizer.RadToMillirad(frame.PitchRad));
        }

        if (frame.Has(CompactTelemetryField.Yaw))
        {
            writer.Write(CompactTelemetryQuantizer.RadToMillirad(frame.YawRad));
        }

        if (frame.Has(CompactTelemetryField.VelocityX))
        {
            writer.Write(CompactTelemetryQuantizer.MpsToCentimetersPerSecond(frame.VelocityXMps));
        }

        if (frame.Has(CompactTelemetryField.VelocityY))
        {
            writer.Write(CompactTelemetryQuantizer.MpsToCentimetersPerSecond(frame.VelocityYMps));
        }

        if (frame.Has(CompactTelemetryField.VelocityZ))
        {
            writer.Write(CompactTelemetryQuantizer.MpsToCentimetersPerSecond(frame.VelocityZMps));
        }

        if (frame.Has(CompactTelemetryField.AngularVelocityX))
        {
            writer.Write(CompactTelemetryQuantizer.RadpsToMilliradPerSecond(frame.AngularVelocityXRadps));
        }

        if (frame.Has(CompactTelemetryField.AngularVelocityY))
        {
            writer.Write(CompactTelemetryQuantizer.RadpsToMilliradPerSecond(frame.AngularVelocityYRadps));
        }

        if (frame.Has(CompactTelemetryField.AngularVelocityZ))
        {
            writer.Write(CompactTelemetryQuantizer.RadpsToMilliradPerSecond(frame.AngularVelocityZRadps));
        }

        if (frame.Has(CompactTelemetryField.Speed))
        {
            writer.Write(CompactTelemetryQuantizer.MpsToCentimetersPerSecond(frame.SpeedMps));
        }

        if (frame.Has(CompactTelemetryField.HeadingError))
        {
            writer.Write(CompactTelemetryQuantizer.RadToMillirad(frame.HeadingErrorRad));
        }

        if (frame.Has(CompactTelemetryField.DistanceToTarget))
        {
            writer.Write(CompactTelemetryQuantizer.MetersToCentimeters(frame.DistanceToTargetM));
        }

        if (frame.Has(CompactTelemetryField.BatteryVoltage))
        {
            writer.Write(CompactTelemetryQuantizer.VoltageToCentivolt(frame.BatteryVoltageV));
        }

        if (frame.Has(CompactTelemetryField.BatteryPercent))
        {
            writer.Write(CompactTelemetryQuantizer.Ratio01ToPermille(frame.BatteryPercent / 100.0));
        }

        if (frame.Has(CompactTelemetryField.MissionProgress))
        {
            writer.Write(CompactTelemetryQuantizer.Ratio01ToPermille(frame.MissionProgress01));
        }

        if (frame.Has(CompactTelemetryField.RiskScore))
        {
            writer.Write(CompactTelemetryQuantizer.Ratio01ToPermille(frame.RiskScore01));
        }

        if (frame.Has(CompactTelemetryField.ForceX))
        {
            writer.Write(CompactTelemetryQuantizer.ForceToDecinewton(frame.ForceXN));
        }

        if (frame.Has(CompactTelemetryField.ForceY))
        {
            writer.Write(CompactTelemetryQuantizer.ForceToDecinewton(frame.ForceYN));
        }

        if (frame.Has(CompactTelemetryField.ForceZ))
        {
            writer.Write(CompactTelemetryQuantizer.ForceToDecinewton(frame.ForceZN));
        }

        if (frame.Has(CompactTelemetryField.TorqueX))
        {
            writer.Write(CompactTelemetryQuantizer.TorqueToCentinewtonMeter(frame.TorqueXNm));
        }

        if (frame.Has(CompactTelemetryField.TorqueY))
        {
            writer.Write(CompactTelemetryQuantizer.TorqueToCentinewtonMeter(frame.TorqueYNm));
        }

        if (frame.Has(CompactTelemetryField.TorqueZ))
        {
            writer.Write(CompactTelemetryQuantizer.TorqueToCentinewtonMeter(frame.TorqueZNm));
        }

        writer.Flush();
        return stream.ToArray();
    }

    public CompactTelemetryFrame Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        var magic0 = reader.ReadByte();
        var magic1 = reader.ReadByte();

        if (magic0 != Magic0 || magic1 != Magic1)
        {
            throw new InvalidDataException("Compact telemetry magic header hatalı.");
        }

        var version = reader.ReadUInt16();

        if (version != Version)
        {
            throw new InvalidDataException($"Desteklenmeyen compact telemetry versiyonu: {version}");
        }

        var vehicleId = ReadString(reader);
        var sequence = reader.ReadUInt64();
        var timestamp = reader.ReadInt64();
        var mask = (CompactTelemetryField)reader.ReadUInt64();

        var frame = new CompactTelemetryFrame
        {
            VehicleId = vehicleId,
            Sequence = sequence,
            TimestampUnixMs = timestamp,
            FieldMask = mask
        };

        if (frame.Has(CompactTelemetryField.PositionX))
        {
            frame = frame with
            {
                PositionXM = CompactTelemetryQuantizer.CentimetersToMeters(reader.ReadInt32())
            };
        }

        if (frame.Has(CompactTelemetryField.PositionY))
        {
            frame = frame with
            {
                PositionYM = CompactTelemetryQuantizer.CentimetersToMeters(reader.ReadInt32())
            };
        }

        if (frame.Has(CompactTelemetryField.PositionZ))
        {
            frame = frame with
            {
                PositionZM = CompactTelemetryQuantizer.CentimetersToMeters(reader.ReadInt32())
            };
        }

        if (frame.Has(CompactTelemetryField.Roll))
        {
            frame = frame with
            {
                RollRad = CompactTelemetryQuantizer.MilliradToRad(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.Pitch))
        {
            frame = frame with
            {
                PitchRad = CompactTelemetryQuantizer.MilliradToRad(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.Yaw))
        {
            frame = frame with
            {
                YawRad = CompactTelemetryQuantizer.MilliradToRad(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.VelocityX))
        {
            frame = frame with
            {
                VelocityXMps = CompactTelemetryQuantizer.CentimetersPerSecondToMps(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.VelocityY))
        {
            frame = frame with
            {
                VelocityYMps = CompactTelemetryQuantizer.CentimetersPerSecondToMps(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.VelocityZ))
        {
            frame = frame with
            {
                VelocityZMps = CompactTelemetryQuantizer.CentimetersPerSecondToMps(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.AngularVelocityX))
        {
            frame = frame with
            {
                AngularVelocityXRadps = CompactTelemetryQuantizer.MilliradPerSecondToRadps(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.AngularVelocityY))
        {
            frame = frame with
            {
                AngularVelocityYRadps = CompactTelemetryQuantizer.MilliradPerSecondToRadps(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.AngularVelocityZ))
        {
            frame = frame with
            {
                AngularVelocityZRadps = CompactTelemetryQuantizer.MilliradPerSecondToRadps(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.Speed))
        {
            frame = frame with
            {
                SpeedMps = CompactTelemetryQuantizer.CentimetersPerSecondToMps(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.HeadingError))
        {
            frame = frame with
            {
                HeadingErrorRad = CompactTelemetryQuantizer.MilliradToRad(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.DistanceToTarget))
        {
            frame = frame with
            {
                DistanceToTargetM = CompactTelemetryQuantizer.CentimetersToMeters(reader.ReadInt32())
            };
        }

        if (frame.Has(CompactTelemetryField.BatteryVoltage))
        {
            frame = frame with
            {
                BatteryVoltageV = CompactTelemetryQuantizer.CentivoltToVoltage(reader.ReadUInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.BatteryPercent))
        {
            frame = frame with
            {
                BatteryPercent = CompactTelemetryQuantizer.PermilleToRatio01(reader.ReadUInt16()) * 100.0
            };
        }

        if (frame.Has(CompactTelemetryField.MissionProgress))
        {
            frame = frame with
            {
                MissionProgress01 = CompactTelemetryQuantizer.PermilleToRatio01(reader.ReadUInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.RiskScore))
        {
            frame = frame with
            {
                RiskScore01 = CompactTelemetryQuantizer.PermilleToRatio01(reader.ReadUInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.ForceX))
        {
            frame = frame with
            {
                ForceXN = CompactTelemetryQuantizer.DecinewtonToForce(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.ForceY))
        {
            frame = frame with
            {
                ForceYN = CompactTelemetryQuantizer.DecinewtonToForce(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.ForceZ))
        {
            frame = frame with
            {
                ForceZN = CompactTelemetryQuantizer.DecinewtonToForce(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.TorqueX))
        {
            frame = frame with
            {
                TorqueXNm = CompactTelemetryQuantizer.CentinewtonMeterToTorque(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.TorqueY))
        {
            frame = frame with
            {
                TorqueYNm = CompactTelemetryQuantizer.CentinewtonMeterToTorque(reader.ReadInt16())
            };
        }

        if (frame.Has(CompactTelemetryField.TorqueZ))
        {
            frame = frame with
            {
                TorqueZNm = CompactTelemetryQuantizer.CentinewtonMeterToTorque(reader.ReadInt16())
            };
        }

        return frame;
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? "");

        if (bytes.Length > ushort.MaxValue)
        {
            throw new InvalidDataException("Compact telemetry string alanı çok uzun.");
        }

        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadUInt16();

        if (length == 0)
        {
            return "";
        }

        var bytes = reader.ReadBytes(length);

        if (bytes.Length != length)
        {
            throw new EndOfStreamException("Compact telemetry string alanı eksik okundu.");
        }

        return Encoding.UTF8.GetString(bytes);
    }
}