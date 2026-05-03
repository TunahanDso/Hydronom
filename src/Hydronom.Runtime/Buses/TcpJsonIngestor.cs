using System.Net.Sockets;
using System.Text;
using System.IO;

namespace Hydronom.Runtime.Buses
{
    public static class TcpJsonIngestor
    {
        // TÃ¼rkÃ§e yorum: Bu gÃ¶rev ana dÃ¶ngÃ¼den baÄŸÄ±msÄ±z Ã§alÄ±ÅŸÄ±r, bloklamaz.
        public static async Task RunAsync(string host, int port, SensorInbox inbox, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var client = new TcpClient();
                    client.NoDelay = true; // kÃ¼Ã§Ã¼k paketlerde gecikmeyi azalt
                    await client.ConnectAsync(host, port, ct);

                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1 << 16);

                    string? line;
                    while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Length == 0) continue;
                        inbox.TryPublish(line); // TaÅŸarsa en eskiyi dÃ¼ÅŸÃ¼rÃ¼r (DropOldest)
                    }
                }
                catch (Exception)
                {
                    // TÃ¼rkÃ§e yorum: BaÄŸlantÄ± koptuysa kÄ±sa bir bekleme ve tekrar dene.
                    await Task.Delay(500, ct);
                }
            }
        }
    }
}

