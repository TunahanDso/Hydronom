namespace Hydronom.Core.Simulation.Faults
{
    /// <summary>
    /// SimÃ¼lasyonda hata enjeksiyonunun nasÄ±l uygulanacaÄŸÄ±nÄ± belirler.
    /// </summary>
    public enum SimFaultInjectionMode
    {
        Disabled = 0,

        /// <summary>
        /// Hatalar belirlenen profile gÃ¶re olasÄ±lÄ±ksal uygulanÄ±r.
        /// </summary>
        Probabilistic = 10,

        /// <summary>
        /// Hatalar gÃ¶rev veya test senaryosunun belirlediÄŸi zamana gÃ¶re uygulanÄ±r.
        /// </summary>
        ScenarioTimeline = 20,

        /// <summary>
        /// Hatalar dÄ±ÅŸ komutla manuel tetiklenir.
        /// </summary>
        Manual = 30,

        /// <summary>
        /// SÃ¼rekli zorlanmÄ±ÅŸ hata modu.
        /// </summary>
        Forced = 40
    }
}
