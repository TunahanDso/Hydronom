using System.Threading.Channels;

namespace Hydronom.Runtime.Buses
{
    // Satır-bazlı NDJSON mesajları için hafif giriş kuyruğu
    public class SensorInbox
    {
        // Bounded: patlama olursa en eskileri düşür (backpressure)
        private readonly Channel<string> _chan = Channel.CreateBounded<string>(
            new BoundedChannelOptions(2048) { FullMode = BoundedChannelFullMode.DropOldest });

        public ChannelReader<string> Reader => _chan.Reader;

        // Arka plandaki okuyucu buraya yazar (non-blocking)
        public bool TryPublish(string line) => _chan.Writer.TryWrite(line);
    }
}
