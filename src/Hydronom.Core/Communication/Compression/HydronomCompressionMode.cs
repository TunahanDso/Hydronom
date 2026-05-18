namespace Hydronom.Core.Communication.Compression;

public enum HydronomCompressionMode : byte
{
    None = 0,

    FieldMask = 1,

    Delta = 2,

    Quantized = 3,

    FieldMaskDeltaQuantized = 4,

    BulkCompressed = 5
}