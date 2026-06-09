using Hydronom.Core.Sensors.Common.Models;
using Hydronom.Core.Sensors.Pico.Frames;
using Hydronom.Core.Sensors.Pico.Protocol;
using Hydronom.Runtime.Sensors.Backends.PicoUsb;

namespace Hydronom.Runtime.PicoUsbSensorBackendSmokeTest;

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            await BackendPassiveMode_ShouldOpenReadNullAndClose();
            Decoder_ShouldRejectEmptyBufferSafely();
            Mapper_ShouldRejectInvalidFrameSafely();

            Console.WriteLine("PicoUsbSensorBackend smoke test passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("PicoUsbSensorBackend smoke test failed.");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task BackendPassiveMode_ShouldOpenReadNullAndClose()
    {
        var options = new PicoUsbSensorBackendOptions
        {
            BackendName = "pico_usb_smoke",
            DeviceId = "pico_smoke0",
            PortName = "auto",
            BaudRate = 115200,
            TargetRateHz = 50.0,
            StaleAfterMs = 1000.0,

            /*
             * Bu test gerçek donanım istememeli.
             * İlk mimari pakette PicoUsb backend pasif açılabilmeli.
             */
            AllowPassiveOpenWithoutPort = true,
            EnablePhysicalSerialRead = false,
            DropInvalidFrames = true,
            CalibrationId = "pico_smoke_calibration"
        };

        var backend = new PicoUsbSensorBackend(options);

        Check(!backend.IsOpen, "Backend başlangıçta kapalı olmalı.");
        Check(backend.Identity.SensorId == "pico_smoke0", "Backend identity DeviceId ile eşleşmeli.");
        Check(backend.Source.BackendKind == SensorBackendKind.PicoUsb, "Backend source PicoUsb olmalı.");
        Check(backend.Source.RuntimeMode == SensorRuntimeMode.CSharpPrimary, "PicoUsb CSharpPrimary runtime modunda olmalı.");
        Check(backend.Capabilities.Capabilities.Count > 0, "Backend en az bir capability üretmeli.");

        var healthBeforeOpen = backend.GetHealthSnapshot();
        Check(healthBeforeOpen.State == SensorHealthState.Offline, "Açılmadan önce health Offline olmalı.");
        Check(healthBeforeOpen.BackendKind == SensorBackendKind.PicoUsb, "Health backend kind PicoUsb olmalı.");

        await backend.OpenAsync();

        Check(backend.IsOpen, "OpenAsync sonrası backend açık olmalı.");

        var healthAfterOpen = await backend.CheckHealthAsync();
        Check(healthAfterOpen.BackendKind == SensorBackendKind.PicoUsb, "CheckHealthAsync PicoUsb health üretmeli.");
        Check(healthAfterOpen.State != SensorHealthState.Offline, "Açık backend Offline olmamalı.");

        var sample = await backend.ReadAsync();

        /*
         * EnablePhysicalSerialRead=false olduğu için bu testte gerçek veri beklemiyoruz.
         * Pasif modda null dönmesi doğru davranış.
         */
        Check(sample is null, "Pasif modda ReadAsync null dönmeli.");

        await backend.CloseAsync();

        Check(!backend.IsOpen, "CloseAsync sonrası backend kapalı olmalı.");

        var healthAfterClose = backend.GetHealthSnapshot();
        Check(healthAfterClose.State == SensorHealthState.Offline, "Kapanış sonrası health Offline olmalı.");
    }

    private static void Decoder_ShouldRejectEmptyBufferSafely()
    {
        var decoder = new PicoUsbFrameDecoder();

        var frame = decoder.Decode(ReadOnlySpan<byte>.Empty);

        Check(frame.Status == PicoSensorFrameStatus.EmptyPayload, "Boş buffer EmptyPayload dönmeli.");
        Check(!frame.IsValid, "Boş buffer valid frame olmamalı.");
        Check(!string.IsNullOrWhiteSpace(frame.Error), "Boş buffer hata açıklaması üretmeli.");
    }

    private static void Mapper_ShouldRejectInvalidFrameSafely()
    {
        var options = new PicoUsbSensorBackendOptions
        {
            BackendName = "pico_usb_mapper_smoke",
            DeviceId = "pico_mapper0"
        };

        var mapper = new PicoSensorSampleMapper(options);

        var sample = mapper.TryMap(PicoRawSensorFrame.Empty);

        Check(sample is null, "Invalid/empty Pico frame SensorSample'a map edilmemeli.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}