using Hydronom.Core.Sensors.Pico.Frames;
using Hydronom.Core.Sensors.Pico.Protocol;

namespace Hydronom.Runtime.Sensors.Backends.PicoUsb;

/// <summary>
/// Pico USB raw frame decoder.
/// 
/// İlk mimari pakette gerçek binary protocol parser burada tamamlanmıyor.
/// Bu sınıfın amacı decoder sınırını netleştirmek:
/// byte/line input alır, PicoRawSensorFrame üretir.
/// </summary>
public sealed class PicoUsbFrameDecoder
{
    /// <summary>
    /// İleride gerçek binary frame parser burada uygulanacak.
    /// Şimdilik boş/verisiz inputları güvenli biçimde reddeder.
    /// </summary>
    public PicoRawSensorFrame Decode(ReadOnlySpan<byte> buffer, DateTime? receiveUtc = null)
    {
        var received = receiveUtc ?? DateTime.UtcNow;

        if (buffer.Length == 0)
        {
            return PicoRawSensorFrame.Create(
                header: PicoSensorFrameHeader.Empty,
                payload: Array.Empty<byte>(),
                status: PicoSensorFrameStatus.EmptyPayload,
                error: "Pico frame buffer is empty.",
                receiveUtc: received,
                decodeUtc: DateTime.UtcNow
            );
        }

        /*
         * Profesyonel not:
         * Burada kalıcı binary framing tasarımı bağlanacak.
         *
         * Önerilen ileriki frame:
         * magic[2] + version[3] + packetKind + channelKind + deviceIndex + channelIndex
         * + sequence[8] + captureMicros[8] + payloadLength[2/4]
         * + payload + checksum
         *
         * Bu ilk pakette bilerek fake parse yapmıyoruz.
         * Yanlış parser, hiç parser olmamasından daha tehlikelidir.
         */
        return PicoRawSensorFrame.Create(
            header: PicoSensorFrameHeader.Empty,
            payload: buffer.ToArray(),
            status: PicoSensorFrameStatus.DecodeError,
            error: "Pico binary decoder is not implemented yet.",
            receiveUtc: received,
            decodeUtc: DateTime.UtcNow
        );
    }
}