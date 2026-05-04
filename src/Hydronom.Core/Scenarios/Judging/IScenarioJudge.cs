namespace Hydronom.Core.Scenarios.Judging;

/// <summary>
/// Senaryo koşusunu değerlendiren judge arayüzüdür.
/// Digital Proving Ground içinde görev başarısı, ihlal, çarpışma,
/// no-go zone ve skor hesaplama akışını soyutlar.
/// </summary>
public interface IScenarioJudge
{
    /// <summary>
    /// Judge adı.
    /// Örnek: DefaultScenarioJudge, TeknofestUsvJudge.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Judge sürümü.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Verilen context'e göre senaryonun anlık/final değerlendirme sonucunu üretir.
    /// </summary>
    /// <param name="context">Senaryo, koşu ve araç durumunu içeren değerlendirme girdisi.</param>
    /// <returns>Judge değerlendirme sonucu.</returns>
    ScenarioJudgeResult Evaluate(ScenarioJudgeContext context);
}