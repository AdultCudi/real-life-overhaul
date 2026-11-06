using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static readonly int[] PopulationCommands = { UiAppWindow.ActionCommand, PopulationFace.RowCommand };

    // The overhaul's own pages: each page of a screen shaped to its job is checked, and its zero state with it.
    static partial void UiCasesPages(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "studio")
        {
            // The one screen draws the studio's page and the other tech firms' pages: both are checked.
            cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "Tech", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.StudioFirm(t), f, dark, k) });
            cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (another firm)", Title = "Tech", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.TechFirm(t), f, dark, k) });
            // Beta 3's empty studio: no staff, no game, thirty days of nothing taken. Every page has its zero state checked too.
            cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (empty, new)", Title = "Your firm", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.StudioEmpty(t), f, dark, k) });
            return;
        }
        if (def.Id == "session_panel")
        {
            cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "Desk", Lay = (s, f, dark, k, t) => UiDecision.Lay(s, UiSamples.DeskCard(t), f, dark, k) });
            cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a page without choices)", Title = "Desk", Lay = (s, f, dark, k, t) => UiDecision.Lay(s, UiSamples.DeskStart(t), f, dark, k) });
            return;
        }
        if (def.Id == "population")
        {
            // Each page shaped to its job: the city and its districts, who contributes, a district, a building, a household, a person (alone or not), a business, one who moved on.
            foreach (string state in UiSamples.PopulationStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = st == "city" ? def.Id : def.Id + " (" + st.Replace('_', ' ') + ")", Title = "The city's people", Commands = PopulationCommands,
                    Lay = (s, f, dark, k, t) => PopulationFace.Lay(s, UiSamples.PopulationPage(t, st), f, dark, k)
                });
            }
            return;
        }
    }
}
