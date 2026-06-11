using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hydronom.Runtime.Vehicles.Loading
{
    /// <summary>
    /// Vehicle Profile Package JSON dosyaları için ortak serializer ayarlarıdır.
    ///
    /// Amaç:
    /// - Enum'ları string olarak okuyabilmek
    /// - Büyük/küçük harf duyarlılığını azaltmak
    /// - JSON paketlerini elle yazarken daha toleranslı olmak
    /// </summary>
    public static class VehicleProfileJsonOptions
    {
        public static JsonSerializerOptions Create()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                WriteIndented = true
            };

            options.Converters.Add(new JsonStringEnumConverter());

            return options;
        }
    }
}