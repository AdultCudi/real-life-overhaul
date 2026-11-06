using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    // The tech firm's page on the app window (--studio-ui): the player's studio and another firm's page built from the real views, every
    // fact and action of the page on it, each action reaching the director's own act, the page worked out again after a press, and the tables scrolling.
    private static void StudioUiChecks()
    {
        UiWords en = UiWords("en");
        var fmt = new PlainWords();
        StudioFile studio = RichStudio();
        var reading = new FirmReading();
        for (int i = 0; i < reading.Takings.Length; i++)
        {
            reading.Takings[i] = 100000 + i * 5000;
        }
        reading.Add(TalentCatalogue.Role(0), 60);
        reading.Add(TalentCatalogue.Role(0), 70);
        reading.Add(TalentCatalogue.Role(18), 50);
        var firms = new FirmsFile();
        foreach (GameDev g in studio.Games)
        {
            firms.Products.Add(new Product { Id = g.Id, Place = g.Place, Name = "Game " + g.Id });
        }
        FirmPage mine = StudioViews.Page(studio, firms, new StudioPlace { Place = StudioPlace, Name = "Kestrel Works", Tickets = 300, Answered = 0, People = 3 }, reading, 900, fmt);
        SectorFile sector = SectorFresh(3);
        var sectorFirms = new FirmsFile { Seed = 3 };
        Product made = FirmsRules.NewProduct(sectorFirms, "p", ProductKind.Saas, Segment.Business, 0, "Shown");
        made.Features = 9;
        made.Users = 500;
        FirmsRules.Launch(made, 1);
        sectorFirms.Products.Add(made);
        var software = new SectorPlace { Place = "p", Name = "Firm", Kind = (int)FirmKind.Software, Products = 1, Customers = 2000, SupportNeeded = 2, Team = new SectorTeam { People = 1, Builders = 1 } };
        FirmPage other = SectorViews.Page(sector, sectorFirms, software, reading, 10, fmt);
        var agencyFirms = new FirmsFile { Seed = 3 };
        agencyFirms.Contracts.Add(new Contract { Id = 1, Place = "w", Kind = (int)WorkKind.Website, Client = "Elm Street Dental", Value = 400000, Due = 40, Stage = (int)WorkStage.Active });
        FirmPage agency = SectorViews.Page(sector, agencyFirms, new SectorPlace { Place = "w", Name = "Agency", Kind = (int)FirmKind.WebAgency, Contracts = 1 }, reading, 10, fmt);

        foreach (KeyValuePair<string, FirmPage> pair in new[]
        {
            new KeyValuePair<string, FirmPage>("studio", mine), new KeyValuePair<string, FirmPage>("software firm", other), new KeyValuePair<string, FirmPage>("agency", agency)
        })
        {
            FirmPage page = pair.Value;
            string name = "studio page (" + pair.Key + ")";
            var acts = new List<ViewAction>();
            var extras = new StudioExtras
            {
                Hero = new UiHeroModel { Icon = UiIcon.GenreSimulation, Kicker = "In development", Title = "Cinder Valley", Progress = 64, Line = "Simulation" },
                People = new[]
                {
                    new UiPersonRow { Name = "Ava Lindqvist", Role = "Lead engineer", First = 86, Second = 72 }, new UiPersonRow { Name = "Marcus Hale", Role = "Engineer", First = 64, Second = 58 },
                    new UiPersonRow { Name = "Priya Raman", Role = "Game designer", First = 77, Second = 81 }
                },
                Tickets = "Support is needed: 120 tickets a day go unanswered."
            };
            StudioFaceModel m = StudioPages.Page(page, en, acts, extras);
            // Every fact of the page is in the model, in the words the page wrote.
            bool figures = m.Figures.Length == page.Figures.Count, tables = m.Main.RowCount == page.Main.Rows.Count && m.More.RowCount == page.More.Rows.Count;
            for (int i = 0; figures && i < page.Figures.Count; i++)
            {
                figures &= m.Figures[i].Value == page.Figures[i].Value && m.Figures[i].Trend == page.Figures[i].Trend && m.Figures[i].Label == en(page.Figures[i].LabelKey, page.Figures[i].LabelKey);
            }
            for (int r = 0; tables && r < page.Main.Rows.Count; r++)
            {
                for (int c = 0; c < page.Main.ColumnKeys.Length; c++)
                {
                    tables &= m.Main.Rows[r].Cells[c] == page.Main.Rows[r][c];
                }
            }
            for (int r = 0; tables && r < page.More.Rows.Count; r++)
            {
                for (int c = 0; c < page.More.ColumnKeys.Length; c++)
                {
                    tables &= m.More.Rows[r].Cells[c] == page.More.Rows[r][c];
                }
            }
            for (int c = 0; c < page.Main.ColumnKeys.Length; c++)
            {
                tables &= m.Main.Columns[c].Title == en(page.Main.ColumnKeys[c], page.Main.ColumnKeys[c]);
            }
            bool team = m.Team.RowCount == page.Team.Count, needed = false;
            for (int i = 0; team && i < page.Team.Count; i++)
            {
                needed |= page.Team[i].Needed;
                team &= m.Team.Rows[i].Values[1] == page.Team[i].Skill && m.Team.Rows[i].Cells[0].Contains(page.Team[i].Count.ToString())
                        && (page.Team[i].Needed ? m.Team.Rows[i].Cells[1] == en(StudioViews.NeededKey, "Needed") : m.Team.Rows[i].Cells[1] == page.Team[i].Skill.ToString());
            }
            bool facts = m.Facts != null && m.Facts.Lines.Length == page.Facts.Count && m.Facts.Title == en(page.FactsKey, page.FactsKey);
            for (int i = 0; facts && i < page.Facts.Count; i++)
            {
                facts &= m.Facts.Lines[i].Label == en(page.Facts[i].LabelKey, page.Facts[i].LabelKey) && string.Equals(m.Facts.Lines[i].Value, page.Facts[i].Value, StringComparison.OrdinalIgnoreCase);
            }
            Check(figures && tables && team && facts && (m.Note != null) == needed && !string.IsNullOrEmpty(m.Title),
                name + ": the title, the figures with their lines, both tables, the team's bars with what it lacks, the facts and the note are all in the model (" + figures + tables + team + facts + (m.Note != null) + needed + ")");

            // Every action the page offers and the page can run is a button, in order, the main one last; each is a press of the page's command with its place in the list.
            var expected = new List<ViewAction>();
            for (int i = 0; i < page.Screen.Others.Count && expected.Count < StudioPages.MostOthers; i++)
            {
                if (StudioPages.Runs(page.Screen.Others[i].Act))
                {
                    expected.Add(page.Screen.Others[i]);
                }
            }
            bool primary = page.Screen.Primary != null && StudioPages.Runs(page.Screen.Primary.Act);
            if (primary)
            {
                expected.Add(page.Screen.Primary);
            }
            bool buttons = acts.Count == expected.Count && m.Actions.Length == expected.Count && (!primary || m.Actions[m.Actions.Length - 1].Kind == UiButtonKind.Primary);
            for (int i = 0; buttons && i < expected.Count; i++)
            {
                buttons &= ReferenceEquals(acts[i], expected[i]) && m.Actions[i].Command == StudioFace.ActionCommand && m.Actions[i].Arg == i && m.Actions[i].Label == en(expected[i].Key, expected[i].Key)
                           && m.Actions[i].Disabled == !expected[i].Enabled;
            }
            Check(buttons, name + ": its actions are buttons in the order the page gives them, the main one last (" + expected.Count + " of them)");

            // Laid out at every aspect: every button, the numbers and the words of the page are on it, nothing overlaps or spills.
            bool laid = true, nodes = true, texts = true;
            string said = "none";
            foreach (float aspect in UiAspects)
            {
                var scene = new UiScene();
                StudioFaceModel rebuilt = StudioPages.Page(page, en, new List<ViewAction>(), extras);
                StudioFace.Lay(scene, rebuilt, UiFrame.FullMenu(aspect, rebuilt.Title), true, UiDefaultKeys.Keyboard);
                laid &= UiCheckRules.Overlaps(scene, l => said = l) + UiCheckRules.Spills(scene, l => said = l) + UiCheckRules.Unfit(scene, l => said = l)
                        + UiCheckRules.EmptyPanels(scene, l => said = l) + UiCheckRules.NoPrompts(scene, l => said = l) + UiCheckRules.TypeLevels(scene, l => said = l) == 0;
                var presses = new HashSet<int>();
                for (int i = 0; i < scene.NodeCount; i++)
                {
                    if (scene.Nodes[i].Command == StudioFace.ActionCommand)
                    {
                        presses.Add(scene.Nodes[i].Arg);
                    }
                }
                for (int i = 0; i < expected.Count; i++)
                {
                    nodes &= presses.Contains(i);
                }
                string all = TechTexts(scene);
                foreach (ViewFigure f in page.Figures)
                {
                    if (!all.Contains(f.Value)) { texts = false; said = "figure " + f.Value; }
                }
                if (aspect >= 1.7f)
                {
                    foreach (ViewPair p in page.Facts)
                    {
                        if (!all.Contains(en(p.LabelKey, p.LabelKey)) || (p.Value.Length < 40 && !all.ToLowerInvariant().Contains(p.Value.ToLowerInvariant()))) { texts = false; said = "fact " + p.LabelKey + "=" + p.Value + " at " + aspect; }
                    }
                    foreach (string[] first in page.Main.Rows.GetRange(0, Math.Min(3, page.Main.Rows.Count)))
                    {
                        if (!all.Contains(first[0])) { texts = false; said = "row " + first[0]; }
                    }
                }
            }
            Check(laid && nodes && texts, name + ": laid at every aspect with every button a target, the figures, the facts and the first rows on it, nothing overlapping (" + said + laid + nodes + texts + ")");
            // What the Tech app's overview has and the firm's own page lacked: the hero over the figures, the people on a Team view, and the support note with its way to Open Roles.
            bool extra = m.Hero == extras.Hero && m.Tabs.Length == 2 && m.People.RowCount == 3 && m.Note != null && m.Note.Lines[0].Value == extras.Tickets && m.Note.Lines[m.Note.Lines.Length - 1].Command == StudioFace.NoteCommand;
            var overview = new UiScene();
            m.Tab = 0;
            StudioFace.Lay(overview, m, UiFrame.FullMenu(16f / 9f, m.Title), true, UiDefaultKeys.Keyboard);
            string heroText = TechTexts(overview);
            extra &= heroText.Contains("Cinder Valley");
            var teamScene = new UiScene();
            m.Tab = 1;
            StudioFace.Lay(teamScene, m, UiFrame.FullMenu(16f / 9f, m.Title), true, UiDefaultKeys.Keyboard);
            string teamText = TechTexts(teamScene);
            bool rowsOnTab = false, tabs = false;
            for (int i = 0; i < teamScene.NodeCount; i++)
            {
                rowsOnTab |= teamScene.Nodes[i].Command == StudioFace.PeopleBase + UiDataTable.Row;
                tabs |= teamScene.Nodes[i].Command == UiAppWindow.NavCommand;
            }
            extra &= teamText.Contains(extras.Tickets.Substring(0, 20)) && teamText.Contains("Ava Lindqvist") && teamText.Contains("Priya Raman") && rowsOnTab && tabs && m.Lists[3] >= 0 && m.Clips[3] >= 0;
            m.Tab = 0;
            Check(extra, name + ": the in-development hero, the people on a Team view that scrolls and the support note with its Open Roles link are on the page");
        }

        // The tables scroll: a long second table keeps its place when the page is laid again, brings a row asked for into view, and each table has a list and a clip of its own.
        {
            var long_ = new List<string[]>();
            for (int i = 0; i < 60; i++)
            {
                long_.Add(new[] { "Rival " + i, "Studio " + i, "Action", (50 + i % 40).ToString(), (1000 + i).ToString() });
            }
            string[] titles = { "Rival game", "Studio", "Genre", "Score", "Copies" }, keys = { "page_col_rival", "page_col_studio", "page_col_genre", "page_col_score", "page_col_copies" };
            bool[] figs = { false, false, false, true, true };
            var m = new StudioFaceModel
            {
                Title = "Kestrel Works",
                Main = StudioFace.Table(titles, keys, figs, long_, "None", UiIcon.Flag),
                More = StudioFace.Table(titles, keys, figs, long_, "None", UiIcon.Flag),
                Team = StudioFace.TeamTable("Team", "Skill", new[] { "Engineers (2)", "Support (0)" }, new[] { 65, 0 }, new[] { "65", "Needed" }, "None"),
                Facts = StudioFace.FactsCard("Facts", new[] { "A" }, new[] { "1" })
            };
            var scene = new UiScene();
            StudioFace.Lay(scene, m, UiFrame.FullMenu(16f / 9f, m.Title), true, UiDefaultKeys.Keyboard);
            bool lists = m.Lists[0] >= 0 && m.Lists[1] >= 0 && m.Lists[2] >= 0 && m.Lists[0] != m.Lists[1] && m.Lists[1] != m.Lists[2]
                         && m.Clips[0] >= 0 && m.Clips[1] >= 0 && m.Clips[2] >= 0 && m.Clips[0] != m.Clips[1] && m.Clips[1] != m.Clips[2];
            m.More.Offset = 200f;
            m.Main.Reveal = 59;
            scene = new UiScene();
            StudioFace.Lay(scene, m, UiFrame.FullMenu(16f / 9f, m.Title), true, UiDefaultKeys.Keyboard);
            Check(lists && m.More.Offset == 200f && m.Main.Offset > 0f && m.Team.Offset == 0f,
                "studio page: each of the three tables has a list and a clip of its own, keeps the offset the wheel gave it and scrolls a row the keys asked for into view (" + m.More.Offset + ", " + m.Main.Offset + ")");
            bool rows = true;
            for (int i = 0; i < scene.NodeCount; i++)
            {
                rows &= scene.Nodes[i].Command != StudioFace.ActionCommand;
            }
            Check(rows && Array.IndexOf(StudioFace.Commands, StudioFace.MainBase + UiDataTable.Row) >= 0, "studio page: a page with no action draws no button, and its rows are targets the keys and the pad walk");
        }

        // The page's own commands stand in 9840 to 9859 and meet no other screen's.
        bool block = true;
        foreach (int c in StudioFace.Commands)
        {
            block &= (c >= 9840 && c <= 9859) || c == UiAppWindow.NavCommand;
        }
        Check(block && StudioFace.MainBase >= 9840 && StudioFace.TeamBase + UiDataTable.Row < StudioFace.ActionCommand && StudioFace.ActionCommand <= 9859, "studio page: its commands are the block 9840 to 9859");

        // The screen presses as the Tech app does: the director's own acts with the act and value the page carried, the page worked out again, the table's wheel and keys reaching its tables.
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string screen = File.ReadAllText(System.IO.Path.Combine(root, @"Ui\Pages\StudioPageScreen.cs")), live = File.ReadAllText(System.IO.Path.Combine(root, @"Ui\Tech\TechLive.FirmPage.cs"));
        bool reaches = screen.Contains("d.StudioDo(act, _place, 0, value)") && screen.Contains("d.SectorDo(act, _place, value)") && live.Contains("_d.StudioDo(act, place, id, arg)")
                       && live.Contains("_d.SectorDo(act, place, arg)") && screen.Contains("Do(a.Act, a.Arg)") && screen.Contains("_acts[arg]") && screen.Contains("StudioPages.Page(page, UiText.Words, _acts, TechLive.Extras(_place))");
        string doBody = screen.Substring(screen.IndexOf("private bool Do(", StringComparison.Ordinal));
        bool rewritten = doBody.Contains("_dirty = true;") && screen.Contains("public bool Dirty => _dirty;") && screen.Contains("_m.Lists[i]") && screen.Contains("_m.Clips[i]")
                     && screen.Contains("_reveal[i] = index") && screen.Contains("_offset[i]");
        Check(reaches && rewritten, "studio page: each action reaches the director's own act (StudioDo, SectorDo) as the Tech app presses it, and the page is written again after a press (" + reaches + ", " + rewritten + ")");
        string extraSource = File.ReadAllText(System.IO.Path.Combine(root, @"Ui\Tech\TechLive.FirmExtras.cs"));
        bool opens = screen.Contains("d.OpenTech(act == SectorAct.Contracts ? TechDirector.TabContracts : TechDirector.TabProducts") && screen.Contains("TechApp.NewProductRow + _place")
                     && live.Contains("act == StudioAct.Concept || (!studio && act == SectorAct.NewProduct)") && live.Contains("v.Open(\"newprod:\" + place)")
                     && live.Contains("v.Open(\"tab:\" + TabContracts)") && screen.Contains("d.OpenRoles(TechDirector.TabTeam, null)") && live.Contains("_d.OpenRoles(TechDirector.TabTeam, null)")
                     && extraSource.Contains("HeroOf(place, Studio)") && extraSource.Contains("PeopleOf(place)") && extraSource.Contains("reallife_ui_tech_note_support");
        Check(opens && StudioPages.Opens(StudioAct.Concept) && StudioPages.Opens(SectorAct.NewProduct) && StudioPages.Opens(SectorAct.Contracts) && !StudioPages.Direct(StudioAct.Concept),
            "studio page: the acts only the Tech app's screens carry (a new game's concept, a new product, the contracts) are buttons that open the Tech app on the firm's own screen, as the Tech app's button does (" + opens + ")");
        UiScreenDef def = null;
        foreach (UiScreenDef d in UiScreens.All)
        {
            def = d.Id == "studio" ? d : def;
        }
        SweepRow row = default;
        foreach (SweepRow r in SweepLedger.Rows)
        {
            row = r.Screen == "studio" ? r : row;
        }
        Check(def != null && def.Template == UiTemplate.AppWindow && def.Section != null && row.State == SweepState.Kit, "studio page: the registry has it on the app window with its switch, and the sweep ledger counts it as drawn");
    }
}
