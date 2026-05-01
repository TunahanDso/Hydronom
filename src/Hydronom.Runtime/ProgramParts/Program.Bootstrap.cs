using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Configuration;

partial class Program
{
    /// <summary>
    /// Runtime config builder.
    /// appsettings.json ve Configs klasöründeki tüm json dosyalarını yükler.
    /// </summary>
    private static IConfigurationRoot BuildRuntimeConfiguration()
    {
        var cb = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        var cfgDir = Path.Combine(AppContext.BaseDirectory, "Configs");
        if (Directory.Exists(cfgDir))
        {
            foreach (var f in Directory.EnumerateFiles(cfgDir, "*.json", SearchOption.TopDirectoryOnly))
                cb.AddJsonFile(Path.Combine("Configs", Path.GetFileName(f)), optional: true, reloadOnChange: true);
        }

        return cb.Build();
    }

    /// <summary>
    /// Runtime genelinde invariant culture kullanılır.
    /// Log, JSON, double parse ve komut değerlerinde virgül/nokta karışıklığını azaltır.
    /// </summary>
    private static void ConfigureRuntimeCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Runtime mode ve logging seçeneklerini config üzerinden okur.
    /// </summary>
    private static RuntimeOptions ReadRuntimeOptions(IConfiguration config)
    {
        bool devMode = ReadBool(config, "Runtime:DevMode", false);

        var simModeStr = config["Simulation:Mode"] ?? "";
        bool simMode =
            simModeStr.Equals("Sim", StringComparison.OrdinalIgnoreCase) ||
            simModeStr.Equals("Hybrid", StringComparison.OrdinalIgnoreCase);

        bool allowExternalPoseOverrideInSim =
            ReadBool(config, "Simulation:AllowExternalPoseOverride", false);

        bool useSyntheticStateWhenNoExternal =
            ReadBool(config, "Runtime:UseSyntheticStateWhenNoExternal", true);

        bool enableNativeTick =
            ReadBool(config, "Runtime:NativeSensors:EnableTick", true);

        var logMode = ReadString(config, "Logging:Mode", "Compact");
        bool logVerbose = logMode.Equals("Verbose", StringComparison.OrdinalIgnoreCase);

        int loopLogEvery = ClampInt(
            ReadInt(config, "Logging:LoopEvery", logVerbose ? 1 : 5),
            1,
            10_000
        );

        int heartbeatEvery = ClampInt(
            ReadInt(config, "Logging:HeartbeatEvery", 10),
            1,
            10_000
        );

        return new RuntimeOptions(
            DevMode: devMode,
            SimMode: simMode,
            AllowExternalPoseOverrideInSim: allowExternalPoseOverrideInSim,
            UseSyntheticStateWhenNoExternal: useSyntheticStateWhenNoExternal,
            EnableNativeTick: enableNativeTick,
            LogMode: logMode,
            LogVerbose: logVerbose,
            LoopLogEvery: loopLogEvery,
            HeartbeatEvery: heartbeatEvery
        );
    }

    /// <summary>
    /// 6-DoF synthetic physics parametrelerini config üzerinden okur.
    /// </summary>
    private static PhysicsOptions ReadPhysicsOptions(IConfiguration config)
    {
        double massKg = ReadDouble(config, "Physics:MassKg", 25.0);

        var inertia = new Hydronom.Core.Domain.Vec3(
            ReadDouble(config, "Physics:Inertia:Ix", 0.2),
            ReadDouble(config, "Physics:Inertia:Iy", 0.2),
            ReadDouble(config, "Physics:Inertia:Iz", 0.1)
        );

        var linearDragBody = new Hydronom.Core.Domain.Vec3(
            ReadDouble(config, "Physics:Drag:Linear:X", 2.0),
            ReadDouble(config, "Physics:Drag:Linear:Y", 6.0),
            ReadDouble(config, "Physics:Drag:Linear:Z", 6.0)
        );

        var quadraticDragBody = new Hydronom.Core.Domain.Vec3(
            ReadDouble(config, "Physics:Drag:Quadratic:X", 0.25),
            ReadDouble(config, "Physics:Drag:Quadratic:Y", 0.8),
            ReadDouble(config, "Physics:Drag:Quadratic:Z", 0.8)
        );

        var angularLinearDragBody = new Hydronom.Core.Domain.Vec3(
            ReadDouble(config, "Physics:AngularDrag:Linear:X", 0.50),
            ReadDouble(config, "Physics:AngularDrag:Linear:Y", 0.50),
            ReadDouble(config, "Physics:AngularDrag:Linear:Z", 1.80)
        );

        var angularQuadraticDragBody = new Hydronom.Core.Domain.Vec3(
            ReadDouble(config, "Physics:AngularDrag:Quadratic:X", 0.02),
            ReadDouble(config, "Physics:AngularDrag:Quadratic:Y", 0.02),
            ReadDouble(config, "Physics:AngularDrag:Quadratic:Z", 0.18)
        );

        double maxSyntheticLinearSpeed = ReadDouble(config, "Physics:MaxLinearSpeedMps", 8.0);
        double maxSyntheticAngularSpeedDeg = ReadDouble(config, "Physics:MaxAngularSpeedDegPerSec", 220.0);

        return new PhysicsOptions(
            MassKg: massKg,
            Inertia: inertia,
            LinearDragBody: linearDragBody,
            QuadraticDragBody: quadraticDragBody,
            AngularLinearDragBody: angularLinearDragBody,
            AngularQuadraticDragBody: angularQuadraticDragBody,
            MaxSyntheticLinearSpeed: maxSyntheticLinearSpeed,
            MaxSyntheticAngularSpeedDeg: maxSyntheticAngularSpeedDeg
        );
    }

    /// <summary>
    /// External pose reconciliation ayarlarını config üzerinden okur.
    /// </summary>
    private static ExternalPoseOptions ReadExternalPoseOptions(
        IConfiguration config,
        RuntimeOptions runtime)
    {
        bool preferExternalCfg = ReadBool(config, "SensorSource:PreferExternal", true);

        bool preferExternal = preferExternalCfg;
        if (runtime.SimMode && !runtime.AllowExternalPoseOverrideInSim)
            preferExternal = false;

        double externalVelBlend = ClampDouble(
            ReadDouble(config, "SensorSource:ExternalPose:VelocityBlend", 0.65),
            0.0,
            1.0,
            0.65
        );

        double externalYawRateBlend = ClampDouble(
            ReadDouble(config, "SensorSource:ExternalPose:YawRateBlend", 0.70),
            0.0,
            1.0,
            0.70
        );

        bool resetVelOnExternalTeleport =
            ReadBool(config, "SensorSource:ExternalPose:ResetVelocityOnTeleport", true);

        double externalTeleportDistanceM =
            ReadDouble(config, "SensorSource:ExternalPose:TeleportDistanceM", 2.5);

        double externalTeleportYawDeg =
            ReadDouble(config, "SensorSource:ExternalPose:TeleportYawDeg", 35.0);

        return new ExternalPoseOptions(
            PreferExternalConfig: preferExternalCfg,
            PreferExternalEffective: preferExternal,
            VelocityBlend: externalVelBlend,
            YawRateBlend: externalYawRateBlend,
            ResetVelocityOnTeleport: resetVelOnExternalTeleport,
            TeleportDistanceM: externalTeleportDistanceM,
            TeleportYawDeg: externalTeleportYawDeg
        );
    }

    /// <summary>
    /// Bootstrap aşamasında okunmuş temel runtime ayarlarını konsola basar.
    /// </summary>
    private static void PrintBootstrapSummary(
        RuntimeOptions runtime,
        PhysicsOptions physics,
        ExternalPoseOptions externalPose)
    {
        Console.WriteLine($"[CFG] Logging → Mode={runtime.LogMode}, LoopEvery={runtime.LoopLogEvery}, HeartbeatEvery={runtime.HeartbeatEvery}");

        Console.WriteLine(
            $"[CFG] Modes → Dev={runtime.DevMode} Sim={runtime.SimMode} " +
            $"AllowExtInSim={runtime.AllowExternalPoseOverrideInSim} " +
            $"SyntheticState={runtime.UseSyntheticStateWhenNoExternal}"
        );

        Console.WriteLine("[CFG] Obstacle Policy → Runtime obstacle üretmez. Obstacle yalnızca Python/TcpJson fresh frame'den alınır.");

        Console.WriteLine(
            $"[CFG] Physics → Mass={physics.MassKg:F2}kg " +
            $"Inertia=({physics.Inertia.X:F2},{physics.Inertia.Y:F2},{physics.Inertia.Z:F2}) " +
            $"LinDrag=({physics.LinearDragBody.X:F2},{physics.LinearDragBody.Y:F2},{physics.LinearDragBody.Z:F2}) " +
            $"QuadDrag=({physics.QuadraticDragBody.X:F2},{physics.QuadraticDragBody.Y:F2},{physics.QuadraticDragBody.Z:F2})"
        );

        Console.WriteLine(
            $"[CFG] AngularDrag → Lin=({physics.AngularLinearDragBody.X:F2},{physics.AngularLinearDragBody.Y:F2},{physics.AngularLinearDragBody.Z:F2}) " +
            $"Quad=({physics.AngularQuadraticDragBody.X:F2},{physics.AngularQuadraticDragBody.Y:F2},{physics.AngularQuadraticDragBody.Z:F2}) " +
            $"MaxLinSpeed={physics.MaxSyntheticLinearSpeed:F2}m/s " +
            $"MaxAngSpeed={physics.MaxSyntheticAngularSpeedDeg:F1}deg/s"
        );

        Console.WriteLine(
            $"[CFG] ExternalPose → VelBlend={externalPose.VelocityBlend:F2} " +
            $"YawRateBlend={externalPose.YawRateBlend:F2} " +
            $"ResetOnTeleport={externalPose.ResetVelocityOnTeleport} " +
            $"TeleportDist={externalPose.TeleportDistanceM:F2}m " +
            $"TeleportYaw={externalPose.TeleportYawDeg:F1}°"
        );

        if (runtime.SimMode && !runtime.AllowExternalPoseOverrideInSim && externalPose.PreferExternalConfig)
            Console.WriteLine("[CFG] PreferExternal → Sim/Hybrid mod: DISABLED (Simulation:AllowExternalPoseOverride=false).");
        else
            Console.WriteLine($"[CFG] PreferExternal → {externalPose.PreferExternalEffective} (cfg={externalPose.PreferExternalConfig}, simMode={runtime.SimMode}, allowInSim={runtime.AllowExternalPoseOverrideInSim})");
    }
}