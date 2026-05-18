namespace Hydronom.Core.Communication.Envelope;

public enum HydronomMessagePriority : byte
{
    Bulk = 0,
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4,
    Emergency = 5
}