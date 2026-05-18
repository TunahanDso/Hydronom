namespace Hydronom.Core.Communication.Integrity;

public static class HydronomCrc32
{
    private const uint Polynomial = 0xEDB88320u;

    private static readonly uint[] Table = CreateTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        for (var i = 0; i < data.Length; i++)
        {
            var index = (crc ^ data[i]) & 0xFFu;
            crc = (crc >> 8) ^ Table[index];
        }

        return ~crc;
    }

    public static bool Verify(ReadOnlySpan<byte> data, uint expectedCrc)
    {
        return Compute(data) == expectedCrc;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];

        for (uint i = 0; i < table.Length; i++)
        {
            var crc = i;

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1u) != 0u
                    ? (crc >> 1) ^ Polynomial
                    : crc >> 1;
            }

            table[i] = crc;
        }

        return table;
    }
}