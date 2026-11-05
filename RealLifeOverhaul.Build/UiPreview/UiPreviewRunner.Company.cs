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

    // The pad shot's walk is the one the pad check takes: laid out as the pad shot lays the page out (the keys leading, the pad's glyphs), so the numbered stops are the d-pad's own path in.
    private static string PadWalkOf(string state, string lang)
    {
        UiWords words = lang == "en" ? ModEnglish : DialogWords(lang);
        var scene = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Company");
        frame.Pointer = false;
        CompanyFace.Lay(scene, UiSamples.CompanyFactsOf(words, state), UiSamples.CompanyViewOf(state), UiSamples.CompanyWordsOf(words), new CompanyLayout(), frame, true, UiDefaultKeys.XboxPad);
        return CompanyFace.PadWalkOf(scene);
    }

    private static Screen CompanyScreenOf(int i, string lang)
    {
        string st = UiSamples.CompanyStates[i];
        bool card = st.EndsWith("question", System.StringComparison.Ordinal), one = st.EndsWith("_one", System.StringComparison.Ordinal);
        // The mouse rests on a target that is neither the one chosen nor the one the keys would focus, or the shot shows no fill (the Staff page's own entry; on a card the second button, as the
        // answers' rows take no hover of their own).
        CompanyView view = UiSamples.CompanyViewOf(st);
        int hoverArg = card ? 1 : view.Tab == CompanyView.Staff ? 2 : 1;
        return new Screen
        {
            Name = "company_" + (lang == "en" ? string.Empty : (lang == "zh-cn" ? "zh" : lang) + "_") + st,
            AppName = "Company",
            Bar = () =>
            {
                UiStatusModel bar = UiSamples.Bar(-1);
                bar.Day = UiSamples.CompanyDayText(st);   // the sample world's today, as the bar writes it
                return bar;
            },
            Icon = UiIcon.Briefcase,
            Backdrop = card ? "05.jpg" : one ? "02.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
            HoverCommand = card ? CompanyFace.Act : UiAppWindow.NavCommand,
            HoverArg = hoverArg,
            Walk = Moves(PadWalkOf(st, lang)),
            Lay = (s, f, d, k) =>
            {
                UiWords words = lang == "en" ? ModEnglish : DialogWords(lang);
                CompanyFace.Lay(s, UiSamples.CompanyFactsOf(words, st), UiSamples.CompanyViewOf(st), UiSamples.CompanyWordsOf(words), new CompanyLayout(), f, d, k);
            }
        };
    }
}
