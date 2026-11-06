using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The overhaul's own pages, each sample's walk as UiSamples writes it. The desk session's panel stands indoors.
    static partial void AddPages(List<Screen> screens)
    {
        var samples = UiSamples.Overhaul(English);
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            samples.AddRange(UiSamples.Population(lang == "en" ? English : ModWords(lang), lang));
        }
        foreach (UiSamples.Sample sample in samples)
        {
            UiSamples.Sample x = sample;
            screens.Add(new Screen { Name = x.Name, AppName = x.App, Lay = (s, f, d, k) => x.Lay(s, f, d, k), Walk = Moves(x.Walk),
                HoverCommand = x.Name.StartsWith("population") ? PopulationFace.RowCommand : UiDashboard.RowCommand, HoverArg = 1,
                Backdrop = x.Name.StartsWith("session_panel") ? "03.jpg" : "13.jpg" });
        }
    }
}
