using System.Net.Sockets;
using System.Text;
using System.IO;

namespace Hydronom.Runtime.Buses
{
    public static class TcpJsonIngestor
    {
        // Türkçe yorum: Bu görev ana döngüden bağımsız çalışır, bloklamaz.
        public static async Task RunAsync(string host, int port, SensorInbox inbox, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var client = new TcpClient();
                    client.NoDelay = true; // küçük paketlerde gecikmeyi azalt
                    await client.ConnectAsync(host, port, ct);

                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1 << 16);

                    string? line;
                    while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Length == 0) continue;
                        inbox.TryPublish(line); // Taşarsa en eskiyi düşürür (DropOldest)
                    }
                }
                catch (Exception)
                {
                    // Türkçe yorum: Bağlantı koptuysa kısa bir bekleme ve tekrar dene.
                    await Task.Delay(500, ct);
                }
            }
        }
    }
}
