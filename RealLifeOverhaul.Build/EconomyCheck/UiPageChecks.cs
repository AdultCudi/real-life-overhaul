using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Ui;

// The kit's pages from the features' own views, as the owner met them in beta 3: no record's key on a page (an address
// as the game writes it), every target a press answers with the game's own action (View on map and Call reached
// nothing), the pages shaped to their jobs (a household's ledger, a person's profile, Flare's feed), and the Contacts
// buttons each routed to the game. Every page is laid out from a real view (the offline city, Flare's views, the firms'
// acts) and held to the kit's layout rules.
internal static partial class Program
{
    private static void UiPageChecks()
    {
        UiPlaceChecks();
        UiPopulationPageChecks();
        UiStudioPageChecks();
    }

    private static void UiPlaceChecks()
    {
        Check(UiPlaces.IsKey("ba:street_fifthavenue|70") && UiPlaces.IsKey("ba:street_seventhstreet|17|t") && UiPlaces.IsKey("ba:businesstype_coffeeshop")
              && UiPlaces.IsKey("reallife_pop_fig_people") && !UiPlaces.IsKey("70 Fifth Avenue") && !UiPlaces.IsKey("Midtown") && !UiPlaces.IsKey("$1,112,059"),
            "ui places: a record's key is told from words");
        string fifth = UiPlaces.Address("ba:street_fifthavenue|70", null), seventh = UiPlaces.Address("ba:street_seventhstreet|17|t", null);
        Check(fifth == "70 Fifth Avenue" && seventh == "17 Seventh Street" && UiPlaces.Address("ba:street_broadwaystreet|12|t", k => k == "ba:street_broadwaystreet" ? "Broadway" : null) == "12 Broadway",
            "ui places: an address key reads as the game writes an address, the street by the game's own words where it has them (" + fifth + ", " + seventh + ")");
        Check(UiPlaces.Words("ba:businesstype_coffeeshop") == "Coffeeshop" && !UiPlaces.IsKey(UiPlaces.Words("reallife_pop_fig_people")), "ui places: a key with no words of its own reads as plain words, never as itself");
    }

    // Every population view laid out through PopulationPages from the offline city, against the rules beta 3 broke.
    private static void UiPopulationPageChecks()
    {
        PopulationSim sim = PopulationMade(42, out PopulationState s, out _);
        PoolRules.DemandDay dd = sim.Demand;
        UiWords en = UiWords("en");
        Func<long, string> money = v => "$" + v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        Func<int, bool, string> nameOf = (seed, female) => (female ? "Rosa" : "John") + " Stanley";
        UiFrame frame = UiFrame.FullMenu(16f / 9f, en("reallife_ui_page_population", "The city's people"));
        PopulationView city = PopulationViews.City(s);
        PopulationView district = PopulationViews.District(s, city.Rows[0].Target.Index);
        PopulationView building = PopulationViews.Building(s, district.Rows[0].Target.Index);
        PopulationView household = PopulationViews.Household(s, dd, building.Rows[0].Target.Index);
        PopulationView person = PopulationViews.Person(s, household.Rows[0].Target.Index);
        int busiest = 0;
        for (int b = 0; b < s.Businesses.Count; b++)
        {
            busiest = s.Businesses.Takings[b] > s.Businesses.Takings[busiest] ? b : busiest;
        }
        PopulationView who = PopulationViews.Contributors(s, dd);
        var views = new[] { city, district, building, household, person, PopulationViews.Business(s, dd, busiest), who, PopulationViews.Household(s, dd, -1), PopulationViews.Person(s, -1) };
        var scene = new UiScene();
        int keys = 0, dead = 0, unknown = 0, layout = 0, repeats = 0;
        string said = "none", layoutSaid = "none";
        foreach (PopulationView v in views)
        {
            UiDashboardModel m = PopulationPages.Model(v, en, money, nameOf, null);
            UiDashboard.Lay(scene, m, frame, true, UiDefaultKeys.Keyboard);
            keys += UiCheckRules.RawKeys(scene, line => said = line);
            repeats += UiCheckRules.Repeats(scene, frame, line => said = line);
            unknown += UiCheckRules.UnknownCommands(scene, DashboardCommands, line => said = line);
            layout += UiCheckRules.Overlaps(scene, line => layoutSaid = line) + UiCheckRules.Spills(scene, line => layoutSaid = line) + UiCheckRules.OffGrid(scene, line => layoutSaid = line)
                      + UiCheckRules.Unfit(scene, line => layoutSaid = line) + UiCheckRules.EmptyPanels(scene, line => layoutSaid = line) + UiCheckRules.Stacked(scene, line => layoutSaid = line)
                      + UiCheckRules.TypeLevels(scene, line => layoutSaid = line) + UiCheckRules.DeadSpace(scene, line => layoutSaid = line) + UiCheckRules.Unfilled(scene, frame, line => layoutSaid = line);
            for (int n = 0; n < scene.NodeCount; n++)
            {
                ref UiNode node = ref scene.Nodes[n];
                bool leads = node.Command == UiDraw.CloseCommand || (node.Command == UiDashboard.RowCommand ? node.Arg >= 0 && node.Arg < v.Rows.Count && v.Rows[node.Arg].Target.Valid
                    : node.Command == UiDashboard.ActionCommand && v.PrimaryTarget.Valid);   // the window's close is the host's Back
                dead += leads ? 0 : 1;
            }
        }
        Check(keys == 0, "ui pages: no population page shows a record's key, each place by its name or its address (" + said + ")");
        Check(repeats == 0, "ui pages: no population page repeats its window's title under it (" + said + ")");
        Check(unknown == 0 && dead == 0, "ui pages: every target of a population page opens something, a row its record and the action its page (" + dead + " that do nothing)");
        Check(layout == 0, "ui pages: every population page from the offline city keeps the layout rules (" + layoutSaid + ")");
        UiDashboardModel wm = PopulationPages.Model(who, en, money, nameOf, null);
        bool addressed = wm.Table.RowCount > 0;
        for (int i = 0; i < wm.Table.RowCount; i++)
        {
            addressed &= !UiPlaces.IsKey(wm.Table.Rows[i][0]) && wm.Table.Rows[i][0].Length > 0;
        }
        Check(addressed, "ui pages: who contributes lists its employers by name or address, never their keys (first: " + (wm.Table.RowCount > 0 ? wm.Table.Rows[0][0] : "none") + ")");
        UiDashboardModel cm = PopulationPages.Model(city, en, money, nameOf, null);
        Check(cm.Table.Columns.Length == 5 && cm.Title == en("reallife_pop_view_city", "The city's people") && !UiDraw.Crumb(frame.Title, cm.Title),
            "ui pages: the city's districts are a ledger of their households, people, out of work and rent, and the city's page puts no second title under the masthead");
        UiDashboardModel hm = PopulationPages.Model(household, en, money, nameOf, null);
        Check(hm.Shape == UiPageShape.Ledger && hm.Title != nameOf(household.NameSeed, household.Female) && hm.Title.Contains("Stanley") && hm.EntryCount == household.Rows.Count,
            "ui pages: a household reads as a ledger entry under its family's name (" + hm.Title + "), never the one person it lists, its people beside it");
        UiDashboardModel pm = PopulationPages.Model(person, en, money, nameOf, null);
        UiDashboard.Lay(scene, pm, frame, true, UiDefaultKeys.Keyboard);
        bool movedOn = false;
        for (int i = 0; i < scene.ItemCount; i++)
        {
            movedOn |= scene.Items[i].Kind == UiKind.Text && scene.Items[i].Text == en("reallife_pop_empty_person", "This person has moved on.");
        }
        Check(pm.Shape == UiPageShape.Profile && !movedOn && pm.FigureCount == person.Hero.Count,
            "ui pages: a person reads as a profile, and one still in the city is never said to have moved on (beta 3)");
        UiDashboardModel gm = PopulationPages.Model(PopulationViews.Person(s, -1), en, money, nameOf, null);
        Check(gm.Title == null && gm.Table.EmptyTitle == en("reallife_pop_empty_person", "This person has moved on.") && gm.Table.RowCount == 0,
            "ui pages: one who has moved on says so in words alone, with no title over nothing");
        UiDashboardModel named = PopulationPages.Model(PopulationViews.Person(s, -1), en, money, nameOf, null, "Thomas Sanders");
        Check(named.Table.EmptyTitle == "Thomas Sanders has moved on." && !string.IsNullOrEmpty(named.Table.EmptyBody),
            "ui pages: one who has moved on is named as the row that opened the page named them, with the way on (the critic, 1 Nov)");
        bool selected = false;
        foreach (PopulationView v in views)
        {
            selected |= PopulationPages.Model(v, en, money, nameOf, null).Table.Selected >= 0;
        }
        Check(!selected, "ui pages: a population list marks no row as selected, so no false highlight stands beside the focus (beta 3)");
    }

    // Flare's views as feeds: the entries that lead somewhere are targets, a tab says where its page is, and no action opens its own page.
    // The firms' pages offer only what the page can run: the acts the director carries out itself. The Tech app's own
    // screens (a new product, the contracts, the concept) open only inside that app, so a button for them would do nothing.
    private static void UiStudioPageChecks()
    {
        string tech = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Tech"));
        string sector = File.ReadAllText(System.IO.Path.Combine(tech, "TechDirector.SectorDesk.cs"));
        sector = sector.Substring(Math.Max(0, sector.IndexOf("internal bool SectorDo(", StringComparison.Ordinal)));
        string studio = Between(File.ReadAllText(System.IO.Path.Combine(tech, "TechDirector.StudioDesk.cs")), "internal void StudioDo(", "private void ToDesk(");
        bool same = sector.Length > 0 && studio.Length > 0;
        string odd = "none";
        foreach ((Type type, string body) in new[] { (typeof(SectorAct), sector), (typeof(StudioAct), studio) })
        {
            var handled = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(body, @"case " + type.Name + @"\.(\w+):"))
            {
                handled.Add(match.Groups[1].Value);
            }
            foreach (System.Reflection.FieldInfo f in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (f.IsLiteral && f.FieldType == typeof(int) && StudioPages.Direct((int)f.GetValue(null)) != handled.Contains(f.Name))
                {
                    same = false;
                    odd = type.Name + "." + f.Name;
                }
            }
        }
        Check(same, "ui pages: a firm's page offers exactly the acts its director carries out, so no button of the Tech app's own screens does nothing on it (" + odd + ")");
        StudioFaceModel sample = StudioPages.Sample(UiWords("en"));
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Your firm");
        var scene = new UiScene();
        StudioFace.Lay(scene, sample, frame, true, UiDefaultKeys.Keyboard);
        string said = "none";
        int broken = UiCheckRules.Overlaps(scene, l => said = l) + UiCheckRules.Spills(scene, l => said = l) + UiCheckRules.Unfit(scene, l => said = l) + UiCheckRules.EmptyPanels(scene, l => said = l)
                     + UiCheckRules.NoPrompts(scene, l => said = l) + UiCheckRules.TypeLevels(scene, l => said = l);
        // Its rows are targets only so the keys and the pad can scroll them: the close and the rows are all there is to press, and no action stands on it.
        bool onlyRows = true;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            int c = scene.Nodes[i].Command;
            onlyRows &= c == UiDraw.CloseCommand || (System.Array.IndexOf(StudioFace.Commands, c) >= 0 && c != StudioFace.ActionCommand);
        }
        Check(onlyRows && sample.Actions.Length == 0 && broken == 0 && sample.Title == UiWords("en")("reallife_ui_sample_firm", "A sample studio"),
            "ui pages: on a save with no tech firm the firm's test opens a sample studio named as one, with nothing on it to act on (" + said + ")");
    }
}
