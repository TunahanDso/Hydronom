using System.Text.Json;

namespace HydronomOps.Gateway.Infrastructure.Serialization;

/// <summary>
/// Gateway genel JSON ayarları.
/// </summary>
public static class JsonDefaults
{
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}