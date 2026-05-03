using System;
using System.IO;
using System.Text.Json;

namespace Hydronom.Core.Modules
{
    /// <summary>
    /// AutoDiscovery Ã§Ä±ktÄ±sÄ±ndan 6Ã—N B matrisini okuyan kÃ¶prÃ¼.
    /// 
    /// Beklenen JSON ÅŸekli (Ã¶zet):
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
        /// 6Ã—N boyutlu motor etki matrisi. SatÄ±rlar:
        ///  [0]=Fx, [1]=Fy, [2]=Fz, [3]=Tx, [4]=Ty, [5]=Tz
        /// </summary>
        public double[,]? BMatrix { get; private set; }

        /// <summary>GeÃ§erli bir matris yÃ¼klendi mi?</summary>
        public bool Loaded => BMatrix is not null;

        /// <summary>YÃ¼klenen thruster (kanal) sayÄ±sÄ±.</summary>
        public int ThrusterCount => BMatrix?.GetLength(1) ?? 0;

        /// <summary>
        /// Verilen JSON dosyasÄ±ndan B matrisini yÃ¼kler.
        /// BaÅŸarÄ±sÄ±zlÄ±kta BMatrix'i deÄŸiÅŸtirmez; anlamlÄ± exception fÄ±rlatÄ±r.
        /// </summary>
        public void LoadFrom(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("GeÃ§ersiz dosya yolu", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException("AutoDiscovery JSON dosyasÄ± bulunamadÄ±", path);

            var txt = File.ReadAllText(path);

            using var doc = JsonDocument.Parse(txt);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Channels", out var channelsElement) ||
                channelsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("JSON iÃ§inde 'Channels' dizisi bulunamadÄ± veya dizi deÄŸil.");
            }

            int n = channelsElement.GetArrayLength();
            if (n == 0)
                throw new InvalidDataException("'Channels' dizisi boÅŸ (hiÃ§ thruster yok).");

            // Ã–nce lokal matris oluÅŸtur, her ÅŸey baÅŸarÄ±lÄ±ysa BMatrix'e ata
            var localB = new double[6, n];

            for (int j = 0; j < n; j++)
            {
                var chElem = channelsElement[j];

                if (!chElem.TryGetProperty("Theta", out var thetaElem) ||
                    thetaElem.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException($"Channels[{j}] iÃ§inde 'Theta' dizisi yok veya dizi deÄŸil.");
                }

                if (thetaElem.GetArrayLength() != 6)
                {
                    throw new InvalidDataException(
                        $"Channels[{j}].Theta uzunluÄŸu 6 olmalÄ± (Fx,Fy,Fz,Tx,Ty,Tz). Mevcut: {thetaElem.GetArrayLength()}");
                }

                for (int i = 0; i < 6; i++)
                {
                    localB[i, j] = thetaElem[i].GetDouble();
                }
            }

            // Buraya kadar geldiysek her ÅŸey OK, artÄ±k geÃ§erli kabul edip atayabiliriz
            BMatrix = localB;
        }
    }
}

