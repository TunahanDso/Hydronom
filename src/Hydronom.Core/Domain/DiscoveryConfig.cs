using System;
using System.Collections.Generic;

namespace Hydronom.Core.Domain
{
    /// <summary>
    /// PWMâ€“IMU tabanlÄ± motor keÅŸif sÃ¼recinin konfigÃ¼rasyon parametreleri.
    /// AutoDiscoveryEngine tarafÄ±ndan referans alÄ±nÄ±r.
    /// </summary>
    public record DiscoveryConfig
    {
        // --- ZAMANLAMA ---

        /// <summary>
        /// Bir test darbesinden sonra suyun durulmasÄ± iÃ§in beklenecek sÃ¼re [ms].
        /// Su altÄ± araÃ§larÄ±nda atalet yÃ¼ksektir, bu sÃ¼re ChannelDelay'den uzun olmalÄ±dÄ±r.
        /// </summary>
        public int SettlingTimeMs { get; init; } = 2000;

        /// <summary>
        /// Her kanal iÃ§in uygulanacak PWM "ON" sÃ¼resi [ms].
        /// </summary>
        public int PulseDurationMs { get; init; } = 800;

        /// <summary>
        /// PWM kanallarÄ± arasÄ± minimum geÃ§iÅŸ beklemesi [ms].
        /// </summary>
        public int ChannelDelayMs { get; init; } = 500;

        /// <summary>
        /// IMU Ã¶rnekleme aralÄ±ÄŸÄ± [ms].
        /// </summary>
        public int ImuSampleIntervalMs { get; init; } = 10;


        // --- EÅÄ°K DEÄERLERÄ° (Hassasiyet) ---

        /// <summary>
        /// EÅŸik lineer hÄ±zlanma deÄŸeri [m/sÂ²]. GÃ¼rÃ¼ltÃ¼ filtresi.
        /// </summary>
        public double AccelThreshold { get; init; } = 0.02;

        /// <summary>
        /// EÅŸik aÃ§Ä±sal hÄ±zlanma deÄŸeri [rad/s].
        /// Tork Ã§Ä±karÄ±mÄ± iÃ§in bu deÄŸerin aÅŸÄ±lmasÄ± gerekir.
        /// </summary>
        public double GyroThreshold { get; init; } = 0.05;

        /// <summary>
        /// Bir Ã¶lÃ§Ã¼mÃ¼n geÃ§erli sayÄ±lmasÄ± iÃ§in gereken minimum gÃ¼ven skoru (0.0â€“1.0).
        /// </summary>
        public double MinConfidenceScore { get; init; } = 0.3;


        // --- TEST PROSEDÃœRÃœ ---

        /// <summary>
        /// PWM taramasÄ± sÄ±rasÄ±nda kullanÄ±lacak minimum sinyal oranÄ± (0.0â€“1.0).
        /// </summary>
        public double MinThrottle { get; init; } = 0.25;

        /// <summary>
        /// PWM taramasÄ± sÄ±rasÄ±nda kullanÄ±lacak maksimum sinyal oranÄ± (0.0â€“1.0).
        /// </summary>
        public double MaxThrottle { get; init; } = 0.50;

        /// <summary>
        /// MotorlarÄ±n ters yÃ¶nde de test edilip edilmeyeceÄŸi.
        /// (ReverseEfficiencyRatio iÃ§in gereklidir.)
        /// </summary>
        public bool TestReverseDirection { get; init; } = true;

        /// <summary>
        /// Her kanalÄ±n kaÃ§ kez tekrar test edileceÄŸi.
        /// </summary>
        public int Repeats { get; init; } = 3;


        // --- VARSAYIMLAR (Fallback) ---

        /// <summary>
        /// Fiziksel Ã§Ä±karÄ±m yapÄ±lamazsa varsayÄ±lan motor sayÄ±sÄ±.
        /// </summary>
        public int AssumedMotorCount { get; init; } = 4;

        /// <summary>
        /// Fiziksel Ã§Ä±karÄ±m yapÄ±lamazsa varsayÄ±lan motor yarÄ±Ã§apÄ± (metre).
        /// </summary>
        public double AssumedRadiusM { get; init; } = 0.5;


        // --- SÄ°STEM ---

        /// <summary>
        /// Taranacak toplam PWM kanalÄ± (PCA9685 = 16).
        /// </summary>
        public int TotalChannels { get; init; } = 16;

        /// <summary>
        /// Sadece belirli kanallarÄ± taramak iÃ§in whitelist. BoÅŸ ise tÃ¼m kanallar taranÄ±r.
        /// </summary>
        public List<int> ChannelWhitelist { get; init; } = new();

        /// <summary>
        /// SimÃ¼lasyon modunun aÃ§Ä±k olup olmadÄ±ÄŸÄ±.
        /// </summary>
        public bool SimulationMode { get; init; } = false;

        /// <summary>
        /// KeÅŸif raporlarÄ±nÄ±n yazÄ±lacaÄŸÄ± dizin.
        /// </summary>
        public string OutputDirectory { get; init; } = "Configs/AutoDiscovery";


        public override string ToString()
        {
            return $"Scan={TotalChannels}ch, Pulse={PulseDurationMs}ms, Settle={SettlingTimeMs}ms, " +
                   $"Thresh(Acc/Gyr)={AccelThreshold:F3}/{GyroThreshold:F3}, Rev={TestReverseDirection}";
        }
    }
}

