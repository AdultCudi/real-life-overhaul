using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The overhaul's own pages, each sample's walk as UiSamples writes it. The desk session's panel stands indoors.
    static partial void AddPages(List<Screen> screens)
    {
        foreach (UiSamples.Sample sample in UiSamples.Overhaul(English))
        {
            UiSamples.Sample x = sample;
            bool firm = x.Name.StartsWith("studio") || x.Name == "tech_firm";
            screens.Add(new Screen { Name = x.Name, AppName = x.App, Lay = (s, f, d, k) => x.Lay(s, f, d, k), Walk = Moves(x.Walk),
                HoverCommand = firm ? StudioFace.MainBase + UiDataTable.Row : UiDashboard.RowCommand, HoverArg = 1, Backdrop = x.Name.StartsWith("session_panel") ? "03.jpg" : "13.jpg" });
        }
        // The firm pages in Brazilian Portuguese and Chinese, whose words run longest.
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            UiWords words = ModWords(lang);
            string suffix = lang == "pt" ? "pt" : "zh";
            foreach (UiSamples.Sample sample in UiSamples.Overhaul(words))
            {
                UiSamples.Sample x = sample;
                if (x.Name == "studio" || x.Name == "tech_firm")
                {
                    screens.Add(new Screen { Name = x.Name + "_" + suffix, AppName = x.App, Lay = (s, f, d, k) => x.Lay(s, f, d, k), Walk = Moves(x.Walk), Single = true,
                        HoverCommand = StudioFace.MainBase + UiDataTable.Row, HoverArg = 1, Backdrop = "13.jpg" });
                }
            }
        }
    }
}
