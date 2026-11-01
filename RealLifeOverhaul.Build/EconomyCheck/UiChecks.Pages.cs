using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
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
        if (def.Id == "flare_city")
        {
            // The feed's three faces: the account's posts, a tab with nothing yet (its long words wrapped in its panel), a business's page.
            cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "Flare in the city", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.FlareCity(t), f, dark, k) });
            cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (trends, none yet)", Title = "Flare in the city", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.FlareTrends(t), f, dark, k) });
            cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a business)", Title = "Flare in the city", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.FlareBusiness(t), f, dark, k) });
            return;
        }
        if (def.Id == "session_panel")
        {
            cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "Desk", Lay = (s, f, dark, k, t) => UiSessionSteps.Lay(s, UiSamples.DeskPanel(t), f, dark, k) });
            return;
        }
        if (def.Id == "population")
        {
            // Each page shaped to its job: the city's districts, who contributes, a household's ledger, a person's profile, one who has moved on.
            cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "The city's people", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.PopulationCity(t), f, dark, k) });
            cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (who contributes)", Title = "The city's people", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.PopulationContributors(t), f, dark, k) });
            cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a household)", Title = "The city's people", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.PopulationHousehold(t), f, dark, k) });
            cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a person)", Title = "The city's people", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.PopulationPerson(t), f, dark, k) });
            cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (moved on)", Title = "The city's people", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.PopulationGone(t), f, dark, k) });
            return;
        }
    }
}
