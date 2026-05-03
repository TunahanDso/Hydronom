namespace Hydronom.Core.Domain
{
    public enum EncounterType
    {
        None = 0,
        HeadOn,                 // KarÅŸÄ±dan karÅŸÄ±ya (Rule 14) â†’ isabetli ise STBD'a
        CrossingFromStarboard,  // DiÄŸer tekne sancak tarafÄ±mÄ±zda (Rule 15) â†’ biz give-way, STBD'a
        CrossingFromPort,       // DiÄŸer tekne iskele tarafÄ±mÄ±zda â†’ genelde stand-on
        Overtaking              // Arkadan yetiÅŸiyoruz (Rule 13) â†’ dikkatle geÃ§iÅŸ, Ã§oÄŸunlukla STBD'dan
    }

    /// <summary>
    /// Basit tavsiye: hangi kural Ã§alÄ±ÅŸtÄ±, stand-on mu give-way mi,
    /// ve dÃ¼men iÃ§in tercih edilen iÅŸaret ( -1 = iskele, +1 = sancak, 0 = serbest ).
    /// </summary>
    public readonly record struct ColregAdvisory(
        EncounterType Type,
        bool IsStandOn,
        double PreferredRudderSign,
        string RuleTag  // Ã¶rn: "R14", "R15", "R13"
    )
    {
        public static ColregAdvisory None => new(EncounterType.None, true, 0.0, "NONE");
    }
}

