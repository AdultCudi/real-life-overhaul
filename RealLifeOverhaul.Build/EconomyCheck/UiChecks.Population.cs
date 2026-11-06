using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;

// The city's people on the kit (--population-ui, and the regular run through UiPageChecks): every view of the offline city laid out as the
// screen lays it, each row leading to the record it names, Back stepping up with the focus on the row it left from, Estate's entry landing on
// the person, and every fact of every view on its page. The command numbers 9820 to 9839 were set aside for this page; it needed none of its own,
// as the window's action and the big table's rows carry every press.
internal static partial class Program
{
    private static void PopulationUiChecks()
    {
        UiPopulationPageChecks();
    }

    private static void UiPopulationPageChecks()
    {
        PopulationSim sim = PopulationMade(42, out PopulationState s, out _);
        PoolRules.DemandDay dd = sim.Demand;
        UiWords en = UiWords("en");
        Func<long, string> money = v => "$" + v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        Func<int, bool, string> nameOf = (seed, female) => (female ? "Rosa" : "John") + " Stanley" + (seed % 7);
        var source = new PopulationSource { Open = to => PopulationViews.Open(s, dd, to), Words = en, Money = money, NameOf = nameOf, Place = null };
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
        PopulationView business = PopulationViews.Business(s, dd, busiest);
        var views = new[] { city, district, building, household, person, business, who, PopulationViews.Household(s, dd, -1), PopulationViews.Person(s, -1) };
        var scene = new UiScene();
        int keys = 0, dead = 0, unknown = 0, layout = 0, repeats = 0, missing = 0;
        string said = "none", layoutSaid = "none", lost = "none";
        foreach (PopulationView v in views)
        {
            PopulationModel m = PopulationPages.Model(v, en, money, nameOf, null);
            PopulationFace.Lay(scene, m, frame, true, UiDefaultKeys.Keyboard);
            keys += UiCheckRules.RawKeys(scene, line => said = line);
            repeats += UiCheckRules.Repeats(scene, frame, line => said = line);
            unknown += UiCheckRules.UnknownCommands(scene, PopulationCommands, line => said = line);
            layout += UiCheckRules.Overlaps(scene, line => layoutSaid = line) + UiCheckRules.Spills(scene, line => layoutSaid = line) + UiCheckRules.OffGrid(scene, line => layoutSaid = line)
                      + UiCheckRules.Unfit(scene, line => layoutSaid = line) + UiCheckRules.EmptyPanels(scene, line => layoutSaid = line) + UiCheckRules.Stacked(scene, line => layoutSaid = line)
                      + UiCheckRules.TypeLevels(scene, line => layoutSaid = line) + UiCheckRules.DeadSpace(scene, line => layoutSaid = line) + UiCheckRules.Unfilled(scene, frame,
                          line => layoutSaid = line);
            for (int n = 0; n < scene.NodeCount; n++)
            {
                ref UiNode node = ref scene.Nodes[n];
                bool leads = node.Command == UiDraw.CloseCommand || (node.Command == PopulationFace.RowCommand ? node.Arg >= 0 && node.Arg < v.Rows.Count && v.Rows[node.Arg].Target.Valid
                    : node.Command == UiAppWindow.ActionCommand && v.PrimaryTarget.Valid);   // the window's close is the host's Back
                dead += leads ? 0 : 1;
            }
            missing += Missing(scene, v, en, money, m, line => lost = line);
        }
        Check(keys == 0, "population ui: no page shows a record's key, each place by its name or its address (" + said + ")");
        Check(repeats == 0, "population ui: no page repeats its window's title under it (" + said + ")");
        Check(unknown == 0 && dead == 0, "population ui: every target of a page opens something, a row its record and the action its page (" + dead + " that do nothing)");
        Check(layout == 0, "population ui: every page from the offline city keeps the layout rules (" + layoutSaid + ")");
        Check(missing == 0, "population ui: every figure, label and row each view holds is on its page (" + lost + ")");
        PopulationModel cityPage = PopulationPages.Model(city, en, money, nameOf, null);
        Check(cityPage.Head == null && cityPage.Figures.Length == city.Hero.Count && cityPage.List.Columns.Length == city.Columns.Length + 1 && cityPage.List.RowCount == city.Rows.Count
              && cityPage.Sections.Length == 1 && cityPage.Sections[0].Lines.Length == city.Details.Count && cityPage.HasAction,
            "population ui: the city is its figures with its districts in the big table, each district's four figures a column, its facts beside them and the way to who contributes");
        PopulationModel householdPage = PopulationPages.Model(household, en, money, nameOf, null);
        Check(householdPage.Head != null && householdPage.Head.Name.Contains("Stanley") && householdPage.Head.Name != nameOf(household.NameSeed, household.Female)
              && householdPage.List.Columns[0].Kind == UiCellKind.Person && householdPage.Sections.Length == 2 && householdPage.Sections[0].Lines.Length == household.Hero.Count,
            "population ui: a household is under its family's name (" + householdPage.Head.Name + "), never the one person it lists, its people with faces beside its month and its home");
        PopulationModel personPage = PopulationPages.Model(person, en, money, nameOf, null);
        PopulationFace.Lay(scene, personPage, frame, true, UiDefaultKeys.Keyboard);
        bool movedOn = false;
        for (int i = 0; i < scene.ItemCount; i++)
        {
            movedOn |= scene.Items[i].Kind == UiKind.Text && scene.Items[i].Text == en("reallife_pop_empty_person", "This person has moved on.");
        }
        Check(personPage.Head != null && personPage.Head.Glyph == UiIcon.None && !personPage.Gone && !movedOn,
            "population ui: a person is a profile with their face, and one still in the city is never said to have moved on (beta 3)");
        PopulationModel gone = PopulationPages.Model(PopulationViews.Person(s, -1), en, money, nameOf, null);
        PopulationModel named = PopulationPages.Model(PopulationViews.Person(s, -1), en, money, nameOf, null, "Thomas Sanders");
        Check(gone.Gone && gone.Head == null && gone.GoneTitle == en("reallife_pop_empty_person", "This person has moved on.") && named.GoneTitle == "Thomas Sanders has moved on."
              && !string.IsNullOrEmpty(named.GoneBody), "population ui: one who has moved on says so in words alone, named as the row that opened the page named them, with the way on");
        bool selected = false;
        foreach (PopulationView v in views)
        {
            selected |= PopulationPages.Model(v, en, money, nameOf, null).List.Selected >= 0;
        }
        Check(!selected, "population ui: a list marks no row as selected, so no false highlight stands beside the focus (beta 3)");
        PopulationWalkChecks(source, frame, city, district, building, household, person, who, busiest, s);
        PopulationShortChecks(source, frame, city.Rows.Count);
    }

    // Every figure of a view with its label, and each row's words, found among the page's own words.
    private static int Missing(UiScene scene, PopulationView v, UiWords en, Func<long, string> money, PopulationModel m, Action<string> say)
    {
        if (m.Gone)
        {
            return 0;
        }
        var words = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < scene.ItemCount; i++)
        {
            if (scene.Items[i].Kind == UiKind.Text && scene.Items[i].Text != null)
            {
                words.Add(scene.Items[i].Text);
            }
        }
        int lost = 0;
        var figures = new List<ViewFigure>(v.Hero);
        figures.AddRange(v.Details);
        foreach (ViewFigure f in figures)
        {
            string label = en(f.Label, f.Label), value = PopulationPages.Write(f, en, money);
            bool skipped = v.Kind == ViewKind.Person && f.Value == 0 && f.Format != ViewFormat.Years;   // nothing to say: the state already says why
            if (!skipped && (!words.Contains(label) || (!UiPlaces.IsKey(value) && value.Length > 0 && !words.Contains(value))))
            {
                lost++;
                say(v.Kind + " lacks " + label + " " + value);
            }
        }
        for (int r = 0; r < v.Rows.Count; r++)
        {
            string name = m.List.Rows[r].Cells[0];
            if (!words.Contains(name))
            {
                lost++;
                say(v.Kind + " lacks the row " + name);
            }
            for (int c = 1; c < m.List.Rows[r].Cells.Length; c++)
            {
                string cell = m.List.Rows[r].Cells[c];
                if (!string.IsNullOrEmpty(cell) && !words.Contains(cell))
                {
                    lost++;
                    say(v.Kind + " lacks " + cell + " of " + name);
                }
            }
        }
        return lost;
    }

    // The screen as the player walks it: down by rows to a district, a building, a household and a person, to who contributes by the action; Back each way
    // with the focus on the row it left from; Estate's entry landing on a person; Back from there closing the screen.
    private static void PopulationWalkChecks(PopulationSource source, UiFrame frame, PopulationView city, PopulationView district, PopulationView building, PopulationView household,
        PopulationView person, PopulationView who, int busiest, PopulationState s)
    {
        var screen = new PopulationScreen(source);
        int closed = 0;
        screen.Close = () => closed++;
        var scene = new UiScene();
        Action draw = () => screen.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        draw();
        bool opens = screen.At.Kind == ViewKind.City && !screen.Dirty && scene.FindNode(PopulationFace.RowCommand, city.Rows.Count - 1) >= 0
                     && scene.FindNode(PopulationFace.RowCommand, city.Rows.Count) < 0 && scene.FindNode(UiAppWindow.ActionCommand,
                         0) >= 0 && scene.FocusFirst == scene.FindNode(PopulationFace.RowCommand, 0);
        Check(opens, "population ui: it opens on the city, its keys on the first district, its way to who contributes a target of its own");
        // each step: the press, the page it opens, and Back with the focus where it left from
        var steps = new List<(int Command, int Arg, ViewKind Kind, int Index)>
        {
            (PopulationFace.RowCommand, 0, ViewKind.District, city.Rows[0].Target.Index),
            (PopulationFace.RowCommand, 0, ViewKind.Building, district.Rows[0].Target.Index),
            (PopulationFace.RowCommand, 0, ViewKind.Household, building.Rows[0].Target.Index),
            (PopulationFace.RowCommand, 0, ViewKind.Person, household.Rows[0].Target.Index),
        };
        bool down = true, back = true;
        string where = "none";
        foreach ((int command, int arg, ViewKind kind, int index) in steps)
        {
            bool pressed = screen.Press(command, arg);
            draw();
            bool here = pressed && screen.At.Kind == kind && screen.At.Index == index;
            down &= here;
            where = here ? where : kind + " " + index;
        }
        Check(down, "population ui: each row opens the record it names, the city to a district to a building to a household to a person (" + where + ")");
        int depth = steps.Count;
        for (int i = steps.Count - 1; i >= 0; i--)
        {
            bool stepped = screen.Back();
            draw();
            ViewKind kind = i == 0 ? ViewKind.City : steps[i - 1].Kind;
            int row = steps[i].Arg;
            back &= stepped && screen.At.Kind == kind && scene.FocusFirst == scene.FindNode(steps[i].Command, row) && scene.FocusFirst >= 0;
        }
        Check(back && closed == 0 && depth == 4, "population ui: Back steps up the way it came and puts the focus on the row it left from, never closing before the city");
        bool action = screen.Press(UiAppWindow.ActionCommand, 0);
        draw();
        bool contributors = action && screen.At.Kind == ViewKind.Contributors;
        bool stepBack = screen.Back();
        draw();
        Check(contributors && stepBack && screen.At.Kind == ViewKind.City && scene.FocusFirst == scene.FindNode(UiAppWindow.ActionCommand, 0),
            "population ui: the way to who contributes opens it, and Back lands on that button");
        Check(!screen.Press(PopulationFace.RowCommand, 99) && !screen.Press(UiDraw.CloseCommand, 0), "population ui: a press that leads nowhere is refused");
        // Who contributes leads on to a business or a building, and a business to its best paid person.
        screen.Press(UiAppWindow.ActionCommand, 0);
        draw();
        bool lead = screen.Press(PopulationFace.RowCommand, 0);
        draw();
        Check(lead && (screen.At.Kind == ViewKind.Business || screen.At.Kind == ViewKind.Building) && !screen.Dirty,
            "population ui: an employer of who contributes opens its business or its building");
        // Estate's entry: a person of the pool lands on their page, and Back from there leaves the screen.
        int slot = household.Rows[0].Target.Index;
        PopulationScreen.Wanted = ViewTarget.To(ViewKind.Person, slot);
        draw();
        bool landed = screen.At.Kind == ViewKind.Person && screen.At.Index == slot && PopulationScreen.Wanted == null;
        bool left = screen.Back();
        Check(landed && left && closed == 1, "population ui: Estate's entry lands on the person, and Back from there closes the screen");
        // A slot used again after the person is gone opens as gone, never as somebody else.
        PopulationScreen.Wanted = ViewTarget.To(ViewKind.Person, slot, s.People.Id[slot] + 1000);
        var scene2 = new UiScene();
        var screen2 = new PopulationScreen(source);
        screen2.Lay(scene2, frame, true, UiDefaultKeys.Keyboard);
        bool goneText = false;
        for (int i = 0; i < scene2.ItemCount; i++)
        {
            goneText |= scene2.Items[i].Kind == UiKind.Text && scene2.Items[i].Text == source.Words("reallife_pop_empty_person", "This person has moved on.");
        }
        Check(goneText, "population ui: a slot the person has left opens as gone");
        PopulationView demand = new PopulationView { Kind = ViewKind.Demands };
        Check(demand.Rows.Count == 0 && PopulationPages.Model(demand, source.Words, source.Money, source.NameOf, null).Gone, "population ui: a view with nothing in it is its words alone");
    }

    // A short screen: the districts do not all fit, so the rest scroll, by the wheel and by a focus that walks past the last row.
    private static void PopulationShortChecks(PopulationSource source, UiFrame frame, int rows)
    {
        UiFrame small = frame;
        small.Height = 640f;
        var screen = new PopulationScreen(source);
        var scene = new UiScene();
        Func<int> shown = () =>
        {
            int n = 0;
            for (int i = 0; i < scene.NodeCount; i++)
            {
                n += scene.Nodes[i].Command == PopulationFace.RowCommand ? 1 : 0;
            }
            return n;
        };
        screen.Lay(scene, small, true, UiDefaultKeys.Keyboard);
        int first = shown();
        screen.Reveal(0, rows - 1);
        screen.Lay(scene, small, true, UiDefaultKeys.Keyboard);
        bool last = scene.FindNode(PopulationFace.RowCommand, rows - 1) >= 0;
        screen.Wheel(0, 40f);
        screen.Lay(scene, small, true, UiDefaultKeys.Keyboard);
        bool top = scene.FindNode(PopulationFace.RowCommand, 0) >= 0;
        Check(first > 0 && last && top,
            "population ui: on a short screen the districts that do not fit scroll into view by a focus past the last row and by the wheel (" + first + " of " + rows + " first)");
    }
}
