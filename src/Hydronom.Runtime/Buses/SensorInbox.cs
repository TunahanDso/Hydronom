using System.Threading.Channels;

namespace Hydronom.Runtime.Buses
{
    // SatÄ±r-bazlÄ± NDJSON mesajlarÄ± iÃ§in hafif giriÅŸ kuyruÄŸu
    public class SensorInbox
    {
        // Bounded: patlama olursa en eskileri dÃ¼ÅŸÃ¼r (backpressure)
        private readonly Channel<string> _chan = Channel.CreateBounded<string>(
            new BoundedChannelOptions(2048) { FullMode = BoundedChannelFullMode.DropOldest });

        public ChannelReader<string> Reader => _chan.Reader;

        // Arka plandaki okuyucu buraya yazar (non-blocking)
        public bool TryPublish(string line) => _chan.Writer.TryWrite(line);
    }
}

