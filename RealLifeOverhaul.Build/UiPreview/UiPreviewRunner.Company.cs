using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The Company app: every tab and its sub pages on a rich company and on a new one, and the question as a card over the interior of the business it is about.
    static partial void AddCompany(List<Screen> screens)
    {
        // English, and then Portuguese and Chinese: every state in each, with the words of that language (the mod's own locale files; the sample's English only where neither has a key).
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            for (int i = 0; i < UiSamples.CompanyStates.Length; i++)
            {
                screens.Add(CompanyScreenOf(i, lang));
            }
        }
    }

    private static Screen CompanyScreenOf(int i, string lang)
    {
        string st = UiSamples.CompanyStates[i];
        bool card = st.EndsWith("question", System.StringComparison.Ordinal), one = st.EndsWith("_one", System.StringComparison.Ordinal);
        return new Screen
        {
            Name = "company_" + (lang == "en" ? string.Empty : (lang == "zh-cn" ? "zh" : lang) + "_") + st,
            AppName = "Company",
            Bar = () =>
            {
                UiStatusModel bar = UiSamples.Bar(-1);
                bar.Day = one ? "Tuesday (Day 7)" : "Tuesday (Day 214)";   // the sample's today: a first week for the cafe, else the days around 214 (Week 31, Q3)
                return bar;
            },
            Icon = UiIcon.Briefcase,
            Backdrop = card ? "05.jpg" : one ? "02.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
            HoverCommand = card ? CompanyFace.Choice : UiAppWindow.NavCommand,
            HoverArg = 1,
            Walk = Moves("DDR"),
            Lay = (s, f, d, k) =>
            {
                UiWords words = lang == "en" ? ModEnglish : DialogWords(lang);
                CompanyFace.Lay(s, UiSamples.CompanyFactsOf(words, st), UiSamples.CompanyViewOf(st), UiSamples.CompanyWordsOf(words), new CompanyLayout(), f, d, k);
            }
        };
    }
}
