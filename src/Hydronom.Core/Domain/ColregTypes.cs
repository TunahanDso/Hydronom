namespace Hydronom.Core.Domain
{
    public enum EncounterType
    {
        None = 0,
        HeadOn,                 // Karşıdan karşıya (Rule 14) → isabetli ise STBD'a
        CrossingFromStarboard,  // Diğer tekne sancak tarafımızda (Rule 15) → biz give-way, STBD'a
        CrossingFromPort,       // Diğer tekne iskele tarafımızda → genelde stand-on
        Overtaking              // Arkadan yetişiyoruz (Rule 13) → dikkatle geçiş, çoğunlukla STBD'dan
    }

    /// <summary>
    /// Basit tavsiye: hangi kural çalıştı, stand-on mu give-way mi,
    /// ve dümen için tercih edilen işaret ( -1 = iskele, +1 = sancak, 0 = serbest ).
    /// </summary>
    public readonly record struct ColregAdvisory(
        EncounterType Type,
        bool IsStandOn,
        double PreferredRudderSign,
        string RuleTag  // örn: "R14", "R15", "R13"
    )
    {
        public static ColregAdvisory None => new(EncounterType.None, true, 0.0, "NONE");
    }
}
