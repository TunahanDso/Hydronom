namespace Hydronom.Core.State.Authority
{
    /// <summary>
    /// State authority Ã§alÄ±ÅŸma modu.
    ///
    /// Normal Hydronom Ã§alÄ±ÅŸma modu CSharpPrimary olmalÄ±dÄ±r.
    /// Python yalnÄ±zca backup veya compare/debug amacÄ±yla kullanÄ±labilir.
    /// </summary>
    public enum StateAuthorityMode
    {
        Disabled = 0,

        CSharpPrimary = 10,

        PythonBackup = 20,

        CompareOnly = 30,

        Replay = 40,

        Simulation = 50
    }
}
