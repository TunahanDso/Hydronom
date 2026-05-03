using System.Threading;
using System.Threading.Tasks;

namespace Hydronom.Core.Sensors.Common.Abstractions
{
    /// <summary>
    /// SensÃ¶r self-test sÃ¶zleÅŸmesi.
    ///
    /// GerÃ§ek donanÄ±m, sim backend veya replay backend kendi test sonucunu bu arayÃ¼zle verebilir.
    /// </summary>
    public interface ISensorSelfTest
    {
        ValueTask<SensorSelfTestResult> RunSelfTestAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// SensÃ¶r self-test sonucu.
    /// </summary>
    public readonly record struct SensorSelfTestResult(
        bool Passed,
        string Code,
        string Message
    )
    {
        public static SensorSelfTestResult Pass(string message = "Self-test passed.")
        {
            return new SensorSelfTestResult(
                Passed: true,
                Code: "PASS",
                Message: string.IsNullOrWhiteSpace(message) ? "Self-test passed." : message.Trim()
            );
        }

        public static SensorSelfTestResult Fail(string code, string message)
        {
            return new SensorSelfTestResult(
                Passed: false,
                Code: string.IsNullOrWhiteSpace(code) ? "FAIL" : code.Trim(),
                Message: string.IsNullOrWhiteSpace(message) ? "Self-test failed." : message.Trim()
            );
        }
    }
}

