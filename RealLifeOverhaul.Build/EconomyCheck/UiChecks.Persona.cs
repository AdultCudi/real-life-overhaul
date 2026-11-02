using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _personaChecked;

    // Persona, Rivals and Market Insider, every state of each: laid out by the screen checks in every language and frame,
    // with the commands each page answers. The views' own rules are checked once, beside them.
    static partial void UiCasesPersona(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "persona")
        {
            foreach (string state in UiSamples.PersonaStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "persona (" + st + ")", Title = "Persona", Commands = PersonaCommands,
                    Lay = (s, f, dark, k, t) => UiPersona.Lay(s, UiSamples.Persona(t, st), f, dark, k)
                });
            }
        }
        else if (def.Id == "rivals")
        {
            foreach (string state in UiSamples.RivalsStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "rivals (" + st + ")", Title = "Rivals", Commands = RivalsCommands,
                    Lay = (s, f, dark, k, t) => UiRivals.Lay(s, UiSamples.Rivals(t, st), f, dark, k)
                });
            }
        }
        else if (def.Id == "market_insider")
        {
            foreach (string state in UiSamples.MarketStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "market insider (" + st + ")", Title = "Market Insider", Commands = MarketCommands,
                    Lay = (s, f, dark, k, t) => UiMarket.Lay(s, UiSamples.Market(t, st), f, dark, k)
                });
            }
        }
        else
        {
            return;
        }
        if (!_personaChecked)
        {
            _personaChecked = true;
            PersonaChecks();
            RivalsChecks();
            MarketChecks();
            DataCellChecks();
        }
    }

    private static readonly int[] PersonaCommands =
    {
        UiAppWindow.NavCommand, UiPersona.LinkCommand, UiChart.First + UiChart.Point, UiDataTable.First + UiDataTable.Head, UiDataTable.First + UiDataTable.Row,
        UiDataTable.First + UiDataTable.Clear
    };

    private static readonly int[] RivalsCommands =
    {
        UiAppWindow.NavCommand, UiAppWindow.ActionCommand, UiRivals.ChartTabCommand, UiRivals.RowActionCommand, UiRivals.RowLinkCommand, UiChart.First + UiChart.Point,
        UiDataTable.First + UiDataTable.Head, UiDataTable.First + UiDataTable.Row, UiDataTable.First + UiDataTable.Clear
    };

    private static readonly int[] MarketCommands =
    {
        UiAppWindow.NavCommand, UiMarket.MapCommand, UiMarket.TableTabCommand, UiMarket.SwitchCommand, UiMarket.EventCommand, UiMarket.OptionCommand,
        UiDataTable.First + UiDataTable.Head, UiDataTable.First + UiDataTable.Filter, UiDataTable.First + UiDataTable.Search, UiDataTable.First + UiDataTable.Row,
        UiDataTable.First + UiDataTable.Clear
    };

    // What the Persona view promises beyond the layout: the worth as the game adds it, a debt kept out of the donut and shown
    // under it, the ring the happiness's grade, the modifiers' sign as the game writes it, the courses and goals counted as
    // the game's header counts them, and a course group's glyph by its heading.
    private static void PersonaChecks()
    {
        UiWords en = UiWords("en");
        var v = new PersonaView { Name = "Maya Okafor", Cash = 100f, Investments = 50f, Assets = 200f, Loans = 80f, Happiness = 81 };
        Check(PersonaViews.Worth(v) == 270f, "persona: the worth is cash, investments and assets less the loans, as the game adds it");
        var m = new UiPersonaModel();
        PersonaViews.Persona(m, en, v);
        Check(m.WealthShares.Length == 3 && m.WealthShares[0] == 100f && m.LoansValue != null && m.LoansValue.StartsWith("-"),
            "persona: what is owned are the donut's slices and the loans stand under them, with their minus");
        Check(m.Profile.Ring == UiTokens.Grade(81) && PersonaViews.Mood(-5) == UiTokens.Grade(0), "persona: the face's ring is the happiness on the mood scale");
        Check(PersonaViews.Signed(15) == "+15%" && PersonaViews.Signed(-4) == "-4%" && PersonaViews.Signed(0) == "+0%", "persona: a modifier's share is written with its sign, as the game writes it");
        var courses = new[]
        {
            new PersonaCourse { State = PersonaCourseState.Done }, new PersonaCourse { State = PersonaCourseState.Under }, new PersonaCourse { State = PersonaCourseState.Done },
        };
        Check(PersonaViews.Done(courses) == 2 && PersonaViews.Done(null) == 0, "persona: the courses done are counted, none for no list");
        Check(PersonaViews.GoalsDone(new[] { new PersonaGoal { Tiers = 3 }, new PersonaGoal { Tiers = 2 } }) == 1, "persona: a goal counts as done with its three tiers reached");
        Check(PersonaViews.GroupIcon(null) == UiIcon.Briefcase && PersonaViews.GroupIcon("reallife_persona_edu_group_nursing") == UiIcon.Heartbeat
              && PersonaViews.GroupIcon("reallife_persona_edu_group_some_new_one") == UiIcon.Cap,
            "persona: the game's diplomas wear the business school's glyph, each of the mod's groups its own, one the kit does not know the cap");
        Check(PersonaViews.StateTone(PersonaCourseState.Done) == UiRole.Ok && PersonaViews.StateTone(PersonaCourseState.Stopped) == UiRole.Danger
              && PersonaViews.StateTone(PersonaCourseState.NotStarted) == UiRole.Muted, "persona: a course's chip says done, under way, waiting or stopped in its tone");
        Check(PersonaViews.StateOf("persona_education_completed", false) == PersonaCourseState.Done && PersonaViews.StateOf("reallife_persona_edu_exam", false) == PersonaCourseState.Waiting
              && PersonaViews.StateOf("persona_education_completed_percentage", false) == PersonaCourseState.Under && PersonaViews.StateOf("reallife_persona_edu_withdrawn", false)
              == PersonaCourseState.Stopped && PersonaViews.StateOf("persona_education_not_started", false) == PersonaCourseState.NotStarted
              && PersonaViews.StateOf("reallife_persona_edu_classes", true) == PersonaCourseState.Done, "persona: a course's state is read from its status words' key, its tick meaning done");
        Check(System.Math.Abs(PersonaViews.Fraction("$186,420 / $1,000,000") - 0.18642f) < 1e-4f && System.Math.Abs(PersonaViews.Fraction("1.234 / 2.468") - 0.5f) < 1e-4f
              && PersonaViews.Fraction(null) < 0f && PersonaViews.Fraction("Done") < 0f && PersonaViews.Fraction("7 / 5") == 1f,
            "persona: a goal's way through its tier is read from the game's progress words, its separators passed over, none without them");
        var mods = new[] { new PersonaModifier { Name = "a", Amount = 5 }, new PersonaModifier { Name = "b", Amount = -12 }, new PersonaModifier { Name = "c", Amount = 8 },
            new PersonaModifier { Name = "d", Amount = 1 } };
        UiPersonaLine[] top = PersonaViews.TopMoods(mods);
        Check(top.Length == UiPersona.MostLines && top[0].Words == "b" && top[0].Sign == -1 && top[1].Words == "c", "persona: the overview shows the moods that move the happiness most, a fall as much as a lift");
        var next = PersonaViews.NextGoals(en, new[] { new PersonaGoal { Title = "done", Tiers = 3, Progress = "5 / 5" }, new PersonaGoal { Title = "far", Tiers = 1, Progress = "1 / 10" },
            new PersonaGoal { Title = "near", Tiers = 0, Progress = "9 / 10" } });
        Check(next.Length == 2 && next[0].Words == "near" && PersonaViews.GoalIcon("BankBalanceGoal") == UiIcon.Bank && PersonaViews.GoalIcon("SomeNewGoal") == UiIcon.Trophy,
            "persona: the goals on their way leave out those with every tier and put the nearest first, each with its kind's glyph");
        UiPersonaModel fresh = UiSamples.Persona(en, "new_overview");
        Check(fresh.Profile.FactValues.Length == 1 && fresh.WealthShares.Length == 1, "persona: a new character's head and donut show what there is, not rows of zeros");
        UiPersonaModel goals = UiSamples.Persona(en, "rich_goals");
        Check(goals.Table.SortColumn == 1 && goals.Table.Columns[1].Kind == UiCellKind.Steps && goals.Table.Columns[1].Steps == PersonaViews.GoalsTiers,
            "persona: the goals' tiers stand as three pips under the game's own sort of the current tier");
    }

    // The league's rules: stars by place, a week's change and its colour from the player's side, and the chosen rival's head.
    private static void RivalsChecks()
    {
        Check(RivalsViews.Stars(0, 9, false) == 10 && RivalsViews.Stars(8, 9, false) == 1 && RivalsViews.Stars(3, 9, true) == 0 && RivalsViews.Stars(0, 1, false) == 10,
            "rivals: the league's top rates five stars, its foot a half, a defeated rival none");
        Check(float.IsNaN(RivalsViews.Change(100f, float.NaN)) && float.IsNaN(RivalsViews.Change(100f, 0f)) && System.Math.Abs(RivalsViews.Change(150f, 100f) - 0.5f) < 1e-4f
              && System.Math.Abs(RivalsViews.Change(-50f, -100f) - 0.5f) < 1e-4f, "rivals: a week's change is a share of where it stood, none without a week's history or from nothing");
        UiWords en = UiWords("en");
        UiRivalsModel league = UiSamples.Rivals(en, "league");
        Check(league.Table.Selected == 1 && league.ProfileActions.Length == 1 && league.ProfileActions[0].Key == UiAction.Option && league.Profile.Ring == UiRole.None,
            "rivals: the chosen rival is the league's chosen row, Open in Contacts on P, and no mood ring on a rival's face");
        Check(league.Table.Rows[0].Tones[4] == UiRole.Danger && league.Table.Rows[2].Tones[4] == UiRole.Ok && league.Table.Rows[0].Values[4] == 1
              && league.Table.Columns[2].Kind == UiCellKind.Money && league.Table.Columns[4].Kind == UiCellKind.Trend,
            "rivals: good and bad are the player's: a rival's rise reads bad, the player's own good");
        UiRivalsModel businesses = UiSamples.Rivals(en, "businesses");
        Check(businesses.RowActions.Length == 2 && businesses.RowActions[1].Key == UiAction.Alt && businesses.RowLink != null && businesses.Table.Columns[0].Kind == UiCellKind.Tile,
            "rivals: a business's row offers its locate link, Open in BizMan and Set destination on F, its type on a tile");
        UiRivalsModel empty = UiSamples.Rivals(en, "league_empty");
        Check(empty.Table.RowCount == 0 && empty.ProfileActions.Length == 0 && empty.Profile.Rating == -1, "rivals: an empty league asks nothing of a rival and rates no one");
    }

    // Market Insider's rules: the import price graded, every district its colour with the mod's own apart, and the glyphs.
    private static void MarketChecks()
    {
        Check(MarketViews.ImportGrade(0.5f) == 100 && MarketViews.ImportGrade(1.3f) == 0 && MarketViews.ImportGrade(0.9f) == 50 && MarketViews.ImportGrade(3f) == 0,
            "market: an import price index grades best at the game's lowest and poorest at its highest");
        var tints = new HashSet<UiRole>();
        string[] districts =
        {
            "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity", "ba:neighborhood_lowermanhattan", "ba:neighborhood_midtown",
            "ba:neighborhood_murrayhill", "ba:neighborhood_thehamptons", "reallife_neighborhood_uptown", "reallife_neighborhood_cedarheights"
        };
        foreach (string d in districts)
        {
            tints.Add(MarketViews.DistrictTint(d));
        }
        Check(tints.Count == districts.Length && !tints.Contains(UiRole.Text2) && MarketViews.DistrictTint("ba:neighborhood_some_new_one") == UiRole.Text2,
            "market: every district the mod knows wears a colour of its own, the mod's two among them, and a new one the quiet grey");
        Check(MarketViews.EventIcon("Hype") == UiIcon.Fire && MarketViews.EventIcon("SomethingNew") == UiIcon.Info && MarketViews.BusinessIcon("ba:businesstype_coffeeshop") == UiIcon.Coffee
              && MarketViews.BusinessIcon("ba:businesstype_some_new_one") == UiIcon.Store && MarketViews.BuildingIcon("ba:buildingtype_residential") == UiIcon.Home,
            "market: events, business types and building types wear their glyphs, one the kit does not know a plain one");
        UiWords en = UiWords("en");
        UiMarketModel sale = UiSamples.Market(en, "estate_for_sale");
        Check(sale.SwitchLabel != null && sale.SwitchOn && sale.Table.Columns[0].Kind == UiCellKind.Tile, "market: real estate has its for sale switch, on, and each type on its tile");
        UiMarketModel demands = UiSamples.Market(en, "uptown_demands");
        Check(demands.SwitchLabel == null && demands.Districts.Length == 9 && demands.Table.Columns[3].Graded && demands.TableTabs.Length == 3,
            "market: the demand has no switch, every district is listed, the import price graded, and the events are a tab of their own");
        var scene = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Market Insider");
        UiMarketModel open = UiSamples.Market(en, "filter_open");
        UiMarket.Lay(scene, open, frame, true, UiDefaultKeys.Keyboard);
        Check(ListUnder(scene, UiDataTable.FilterLeft(open.Table, 0, open.TableX), open.TableY + UiTokens.Control), "market: the filter's list opens under its own choice");
        string longer = "Riverside Wholesale is currently experiencing a shortage of Croissant, Baguette, Muffin and Pain au Chocolat. Expected duration is 4 days and then some more.";
        string cut = UiEventList.Lines(longer, UiStyle.Small, 300f, 2);
        Check(UiTextRules.Lines(cut, UiStyle.Small, 300f, 0, out float _) <= 2 && cut.EndsWith("…") && UiEventList.Lines("Short", UiStyle.Small, 300f, 2) == "Short",
            "market: an event's sentence is cut at a whole word to its two lines, a short one left whole");
    }

    // The big table's picture cells: a trend's arrow follows its sign in good or bad's colour, a tile wears its type's glyph,
    // steps fill gold up to those reached.
    private static void DataCellChecks()
    {
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "Type", Kind = UiCellKind.Tile, Width = 200f }, new UiDataColumn { Title = "Change", Kind = UiCellKind.Trend, Width = 96f },
                new UiDataColumn { Title = "Tiers", Kind = UiCellKind.Steps, Steps = 3, Width = 144f },
            },
            Rows = new[]
            {
                new UiDataRow { Cells = new[] { "Office", "+12%", "Silver" }, Values = new[] { (int)UiIcon.Briefcase, 1, 2 } },
                new UiDataRow { Cells = new[] { "Retail", "-4%", null }, Values = new[] { (int)UiIcon.Store, -1, 0 } },
            },
            RowCount = 2, Selected = -1
        };
        var s = new UiScene();
        s.Begin("cells", 1920f, 1080f, true);
        UiDataTable.Lay(s, UiAt.Screen, table, 100f, 100f, 900f, 300f, UiDefaultKeys.Keyboard);
        int up = 0, down = 0, tiles = 0, gold = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            up += it.Kind == UiKind.Icon && it.Icon == UiIcon.TrendUp && it.Fill == UiRole.Ok ? 1 : 0;
            down += it.Kind == UiKind.Icon && it.Icon == UiIcon.TrendDown && it.Fill == UiRole.Danger ? 1 : 0;
            tiles += it.Kind == UiKind.Icon && (it.Icon == UiIcon.Briefcase || it.Icon == UiIcon.Store) ? 1 : 0;
            gold += it.Kind == UiKind.Box && it.Fill == UiRole.Gold ? 1 : 0;
        }
        Check(up == 1 && down == 1 && tiles == 2 && gold == 2,
            "kit: the big table's trend arrow follows its sign in its own colour, a type's glyph stands on its tile and steps fill gold to those reached ("
            + up + " up, " + down + " down, " + tiles + " tiles, " + gold + " gold pips)");
        Check(UiDataCells.StepsWidth(3) % UiTokens.Grid == 0f, "kit: a steps column's width is on the grid");
    }
}
