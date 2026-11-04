using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The Company app: every tab and its sub pages on a rich company and on a new one, and the question as a card over the interior of the business it is about.
    static partial void AddCompany(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.CompanyStates.Length; i++)
        {
            string st = UiSamples.CompanyStates[i];
            bool card = st == "question" || st == "lease_question";
            screens.Add(new Screen
            {
                Name = "company_" + st,
                AppName = "Company",
                Icon = UiIcon.Briefcase,
                Backdrop = card ? "05.jpg" : st.EndsWith("_one", System.StringComparison.Ordinal) ? "02.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = card ? UiDecision.ChoiceCommand : UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => CompanyFace.Lay(s, UiSamples.CompanyFactsOf(English, st), UiSamples.CompanyViewOf(st), UiSamples.CompanyWordsOf(English), new CompanyLayout(), f, d, k)
            });
        }
    }
}
