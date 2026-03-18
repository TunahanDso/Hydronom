using System;
using System.IO;
using System.Text.Json;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AutoDiscovery çıktısından 6×N B matrisini okuyan köprü.
    /// 
    /// Beklenen JSON şekli (özet):
    /// {
    ///   "Channels": [
    ///     { "Theta": [ Fx, Fy, Fz, Tx, Ty, Tz ] },
    ///     ...
    ///   ]
    /// }
    /// </summary>
    public sealed class AutoDiscoveryBridge
    {
        /// <summary>
        /// 6×N boyutlu motor etki matrisi. Satırlar:
        ///  [0]=Fx, [1]=Fy, [2]=Fz, [3]=Tx, [4]=Ty, [5]=Tz
        /// </summary>
        public double[,]? BMatrix { get; private set; }

        /// <summary>Geçerli bir matris yüklendi mi?</summary>
        public bool Loaded => BMatrix is not null;

        /// <summary>Yüklenen thruster (kanal) sayısı.</summary>
        public int ThrusterCount => BMatrix?.GetLength(1) ?? 0;

        /// <summary>
        /// Verilen JSON dosyasından B matrisini yükler.
        /// Başarısızlıkta BMatrix'i değiştirmez; anlamlı exception fırlatır.
        /// </summary>
        public void LoadFrom(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Geçersiz dosya yolu", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException("AutoDiscovery JSON dosyası bulunamadı", path);

            var txt = File.ReadAllText(path);

            using var doc = JsonDocument.Parse(txt);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Channels", out var channelsElement) ||
                channelsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("JSON içinde 'Channels' dizisi bulunamadı veya dizi değil.");
            }

            int n = channelsElement.GetArrayLength();
            if (n == 0)
                throw new InvalidDataException("'Channels' dizisi boş (hiç thruster yok).");

            // Önce lokal matris oluştur, her şey başarılıysa BMatrix'e ata
            var localB = new double[6, n];

            for (int j = 0; j < n; j++)
            {
                var chElem = channelsElement[j];

                if (!chElem.TryGetProperty("Theta", out var thetaElem) ||
                    thetaElem.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException($"Channels[{j}] içinde 'Theta' dizisi yok veya dizi değil.");
                }

                if (thetaElem.GetArrayLength() != 6)
                {
                    throw new InvalidDataException(
                        $"Channels[{j}].Theta uzunluğu 6 olmalı (Fx,Fy,Fz,Tx,Ty,Tz). Mevcut: {thetaElem.GetArrayLength()}");
                }

                for (int i = 0; i < 6; i++)
                {
                    localB[i, j] = thetaElem[i].GetDouble();
                }
            }

            // Buraya kadar geldiysek her şey OK, artık geçerli kabul edip atayabiliriz
            BMatrix = localB;
        }
    }
}
