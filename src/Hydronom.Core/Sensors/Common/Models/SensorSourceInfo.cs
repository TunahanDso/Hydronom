using System;

namespace Hydronom.Core.Sensors.Common.Models
{
    /// <summary>
    /// SensÃ¶r verisinin kaynak/backend bilgisi.
    ///
    /// Bu model sample'Ä±n sim, real, replay, serial, network, Python backup gibi
    /// hangi kaynaktan geldiÄŸini aÃ§Ä±kÃ§a taÅŸÄ±r.
    /// </summary>
    public readonly record struct SensorSourceInfo(
        SensorBackendKind BackendKind,
        SensorRuntimeMode RuntimeMode,
        string BackendName,
        string Transport,
        string Endpoint,
        bool Simulated,
        bool Replay,
        bool External,
        string TraceSource
    )
    {
        public static SensorSourceInfo Sim(string backendName = "sim")
        {
            return new SensorSourceInfo(
                BackendKind: SensorBackendKind.Sim,
                RuntimeMode: SensorRuntimeMode.Simulation,
                BackendName: Normalize(backendName, "sim"),
                Transport: "inproc",
                Endpoint: "physics_truth",
                Simulated: true,
                Replay: false,
                External: false,
                TraceSource: "CSharpSim"
            );
        }

        public static SensorSourceInfo Real(
            SensorBackendKind backendKind,
            string backendName,
            string transport,
            string endpoint
        )
        {
            return new SensorSourceInfo(
                BackendKind: backendKind,
                RuntimeMode: SensorRuntimeMode.CSharpPrimary,
                BackendName: Normalize(backendName, "real"),
                Transport: Normalize(transport, "unknown"),
                Endpoint: Normalize(endpoint, "unknown"),
                Simulated: false,
                Replay: false,
                External: false,
                TraceSource: "CSharpReal"
            );
        }

        public static SensorSourceInfo ReplaySource(string backendName = "replay")
        {
            return new SensorSourceInfo(
                BackendKind: SensorBackendKind.Replay,
                RuntimeMode: SensorRuntimeMode.Replay,
                BackendName: Normalize(backendName, "replay"),
                Transport: "file",
                Endpoint: "blackbox",
                Simulated: false,
                Replay: true,
                External: false,
                TraceSource: "Replay"
            );
        }

        public static SensorSourceInfo PythonBackup(string endpoint = "python")
        {
            return new SensorSourceInfo(
                BackendKind: SensorBackendKind.PythonBackup,
                RuntimeMode: SensorRuntimeMode.PythonBackup,
                BackendName: "python_backup",
                Transport: "tcp_ndjson",
                Endpoint: Normalize(endpoint, "python"),
                Simulated: false,
                Replay: false,
                External: true,
                TraceSource: "PythonBackup"
            );
        }

        public SensorSourceInfo Sanitized()
        {
            return new SensorSourceInfo(
                BackendKind: BackendKind,
                RuntimeMode: RuntimeMode,
                BackendName: Normalize(BackendName, BackendKind.ToString()),
                Transport: Normalize(Transport, "unknown"),
                Endpoint: Normalize(Endpoint, "unknown"),
                Simulated: Simulated,
                Replay: Replay,
                External: External,
                TraceSource: Normalize(TraceSource, "unknown")
            );
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}

