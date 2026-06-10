using Hydronom.Core.Domain;
using Hydronom.Core.Interfaces;

partial class Program
{
    private sealed class NullFrameSource : IFrameSource
    {
        public bool TryGetLatestFrame(out FusedFrame? frame)
        {
            frame = null;
            return false;
        }
    }
}