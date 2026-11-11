using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Construction app's samples: the model each page writes, with real-looking names, long names, empty and busy states. Every word goes through the screen's text source so
// the checks measure it in all eleven languages. Names here are placeholders and never ship.
internal static partial class UiSamples
{
    public static readonly string[] ConstructionStates =
    {
        "overview", "overview_new", "overview_empty", "land", "land_plot", "projects", "project_plans", "project_ready", "project_site", "project_quotes", "tenders", "tender_bid", "crews", "crew", "company",
        "licence", "speciality", "rivals", "city", "off"
    };

    private static UiPrompt[] ConstructionPrompts(UiWords t) => new[]
    {
        new UiPrompt { Action = UiAction.Move, Label = W(t, "reallife_ui_prompt_move", "Move") },
        new UiPrompt { Action = UiAction.Confirm, Label = W(t, "reallife_ui_prompt_select", "Select") },
        new UiPrompt { Action = UiAction.Back, Label = W(t, "reallife_ui_prompt_back", "Back") }
    };

    private static UiNavEntry[] ConstructionNav(UiWords t, string[] counts)
    {
        UiIcon[] icons = { UiIcon.Home, UiIcon.MapTrifold, UiIcon.Hammer, UiIcon.ClipboardText, UiIcon.People, UiIcon.Briefcase, UiIcon.Flag, UiIcon.Scales };
        string[] labels =
        {
            W(t, "reallife_bld_tab_overview", "Overview"), W(t, "reallife_bld_tab_land", "Land"), W(t, "reallife_bld_tab_projects", "Projects"), W(t, "reallife_bld_tab_tenders", "Tenders"),
            W(t, "reallife_bld_tab_crews", "Crews"), W(t, "reallife_bld_tab_company", "Company"), W(t, "reallife_bld_tab_rivals", "Rivals"), W(t, "reallife_bld_tab_city", "City")
        };
        string[] groups =
        {
            W(t, "reallife_ui_cons_group_office", "Site office"), null, null, null, W(t, "reallife_ui_cons_group_builder", "The builder"), null, null, null
        };
        var nav = new UiNavEntry[8];
        for (int i = 0; i < nav.Length; i++)
        {
            nav[i] = new UiNavEntry { Icon = icons[i], Label = labels[i], Count = counts != null && i < counts.Length ? counts[i] : null, Group = groups[i] };
        }
        return nav;
    }

    private static ConstructionModel ConstructionBase(UiWords t, ConstructionRuns runs, int tab, string[] counts)
    {
        return new ConstructionModel
        {
            Title = W(t, "reallife_bld_app", "Construction"), Icon = UiIcon.Building, Nav = ConstructionNav(t, counts), Tab = tab, Prompts = ConstructionPrompts(t)
        };
    }

    private static UiFigureModel[] NoFigures = new UiFigureModel[0];

    private static string[] StepLabels(UiWords t) => new[]
    {
        W(t, "reallife_ui_cons_step_design", "Design"), W(t, "reallife_ui_cons_step_planning", "Planning"), W(t, "reallife_ui_cons_step_ready", "Ready"),
        W(t, "reallife_ui_cons_step_building", "Building"), W(t, "reallife_ui_cons_step_done", "Done")
    };

    private static UiActionModel ConsButton(ConstructionRuns runs, string label, UiButtonKind kind, UiAction key, bool disabled = false)
    {
        return runs.Button(label, kind, UiIcon.None, key, () => { }, disabled);
    }

    public static ConstructionModel Construction(UiWords t, string state)
    {
        var runs = new ConstructionRuns();
        string[] counts = { null, null, "5", "3", "3", null, null, null };
        ConstructionModel m = ConstructionBase(t, runs, 0, counts);
        switch (state)
        {
            case "off":
                m.Shape = ConstructionShape.Off;
                m.Nav = new UiNavEntry[0];
                m.OffTitle = W(t, "reallife_bld_off_title", "The construction company is not running");
                m.OffBody = W(t, "reallife_bld_off_body", "Open a city to see the sites, the land and the tenders.");
                m.OffAction = W(t, "reallife_ui_cons_close", "Close");
                break;
            case "overview_new":
            case "overview_empty":
                ConsOverviewNew(t, runs, m, state == "overview_empty");
                break;
            case "land":
            case "land_plot":
                m.Tab = 1;
                ConsLand(t, runs, m, state == "land_plot");
                break;
            case "projects":
                m.Tab = 2;
                ConsProjects(t, runs, m);
                break;
            case "project_plans":
            case "project_ready":
            case "project_site":
                m.Tab = 2;
                ConsProject(t, runs, m, state);
                break;
            case "project_quotes":
                m.Tab = 2;
                ConsQuotes(t, runs, m);
                break;
            case "tenders":
            case "tender_bid":
                m.Tab = 3;
                ConsTenders(t, runs, m, state == "tender_bid");
                break;
            case "crews":
            case "crew":
                m.Tab = 4;
                ConsCrews(t, runs, m, state == "crew");
                break;
            case "company":
            case "licence":
                m.Tab = 5;
                ConsCompany(t, runs, m, state == "licence");
                break;
            case "speciality":
                m.Tab = 5;
                ConsSpeciality(t, runs, m);
                break;
            case "rivals":
                m.Tab = 6;
                ConsRivals(t, runs, m);
                break;
            case "city":
                m.Tab = 7;
                ConsCity(t, runs, m);
                break;
            default:
                ConsOverview(t, runs, m);
                break;
        }
        if (m.Shape == ConstructionShape.Table && m.Table != null && m.Table.Rows.Length > 0)
        {
            ConsPreview(runs, m);
        }
        m.Runs = runs.ToArray();
        return m;
    }

    // A table's tab shows its first row beside it: the row as a head and the rest of its cells as facts, with the button that opens it.
    private static void ConsPreview(ConstructionRuns runs, ConstructionModel m)
    {
        UiDataRow row = m.Table.Rows[0];
        var lines = new List<UiFactLine>(8);
        for (int c = 1; c < m.Table.Columns.Length && c < row.Cells.Length; c++)
        {
            if (!string.IsNullOrEmpty(row.Cells[c]))
            {
                lines.Add(UiFactLine.Row(m.Table.Columns[c].Title, row.Cells[c]));
            }
        }
        m.Table.Selected = 0;
        m.Card = null;
        if (m.HeadTitle != null && (m.Tab == 2 || m.Tab == 3))
        {
            m.Foot = new UiFactCardModel
            {
                Title = "On the sites soon", Heading = true, Lines = new[]
                {
                    UiFactLine.Row("Day 215  Materials for Office block arrive", string.Empty), UiFactLine.Row("Day 216  The inspector visits Cafe refit", "No cost"),
                    UiFactLine.Row("Day 218  The council decides on Car park", string.Empty)
                }
            };
        }
        m.Side = new[]
        {
            new UiFactCardModel { Title = row.Cells[0], Sub = m.HeadTitle },
            new UiFactCardModel { Lines = lines.ToArray() },
            new UiFactCardModel { Actions = new[] { runs.Button(m.HeadTitle, UiButtonKind.Primary, UiIcon.None, UiAction.Alt, () => { }) } }
        };
    }

    // ------------------------------------------------------------------------------------------------------------------ the overview

    private static ConstructionProject Proj(UiWords t, ConstructionRuns runs, string name, string place, int at, int progress, string spent, string of, string chip, UiRole tone)
    {
        return new ConstructionProject
        {
            Icon = UiIcon.Hammer, Title = name, Sub = place, StepLabels = StepLabels(t), StepAt = at, Progress = progress, Spent = spent, Of = of, Chip = chip, ChipTone = tone,
            Run = runs.Do(() => { })
        };
    }

    private static void ConsOverview(UiWords t, ConstructionRuns runs, ConstructionModel m)
    {
        m.Shape = ConstructionShape.Overview;
        m.HeadMark = "PC";
        m.HeadTitle = "Pioneer Construction";
        m.HeadSub = W(t, "reallife_ui_cons_head_sub", "Licence for {floors} floors, {crews} crews, office at {address}").Replace("{floors}", "10").Replace("{crews}", "3")
            .Replace("{address}", "520 8th Avenue, Midtown");
        m.FigLabels = new[] { W(t, "reallife_ui_cons_fig_worth", "Worth"), W(t, "reallife_ui_cons_fig_under_way", "Under way"), W(t, "reallife_ui_cons_fig_on_site", "Crews on site"),
            W(t, "reallife_ui_cons_fig_done", "Completed") };
        m.FigValues = new[] { "$2,410,000", "5", W(t, "reallife_ui_cons_x_of_y", "{n} of {of}").Replace("{n}", "3").Replace("{of}", "3"), "14" };
        m.Lead = new ConstructionLead
        {
            Icon = UiIcon.Hammer, Kicker = W(t, "reallife_ui_cons_lead_next", "Next decision, {project}").Replace("{project}", "Show flats, 41 E 37th Street"),
            Title = W(t, "reallife_bld_next_start", "Start the works"), Body = W(t, "reallife_bld_stage_ready", "Ready to build"),
            Actions = new[]
            {
                ConsButton(runs, W(t, "reallife_ui_cons_open", "Open"), UiButtonKind.Primary, UiAction.Alt),
                ConsButton(runs, W(t, "reallife_bld_button_build_mode", "Build mode"), UiButtonKind.Secondary, UiAction.Option)
            }
        };
        m.RowsTitle = W(t, "reallife_ui_cons_projects_title", "Projects under way");
        m.RowsLink = W(t, "reallife_ui_cons_all_projects", "All projects");
        m.RowsLinkRun = runs.Do(() => { });
        m.Projects = new[]
        {
            Proj(t, runs, "Show flats", W(t, "reallife_bld_next_start", "Start building"), 2, -1, "$312,000", W(t, "reallife_ui_cons_of", "of {amount}").Replace("{amount}", "$498,000"),
                W(t, "reallife_ui_cons_st_waits", "Waits on you"), UiRole.Warn),
            Proj(t, runs, "Office block, 14 floors", W(t, "reallife_bld_next_working", "Crews at work"), 3, 46, "$790,000", W(t, "reallife_ui_cons_of", "of {amount}").Replace("{amount}", "$1,580,000"),
                W(t, "reallife_ui_cons_st_on_time", "On time, {day}").Replace("{day}", "day 238"), UiRole.Ok),
            Proj(t, runs, "Car park, 120 bays", W(t, "reallife_bld_next_wait_decision", "Council deciding"), 1, -1, "$64,000", W(t, "reallife_ui_cons_of", "of {amount}").Replace("{amount}", "$290,000"),
                W(t, "reallife_bld_stage_planning", "Planning"), UiRole.Muted),
            Proj(t, runs, "Cafe refit", W(t, "reallife_bld_next_book_short", "Book the inspector"), 3, 81, "$96,000", W(t, "reallife_ui_cons_of", "of {amount}").Replace("{amount}", "$110,000"),
                W(t, "reallife_ui_cons_st_waits", "Waits on you"), UiRole.Warn),
            Proj(t, runs, "Shop front and unit", W(t, "reallife_bld_next_done", "Done"), 5, 100, "$340,000", W(t, "reallife_ui_cons_of", "of {amount}").Replace("{amount}", "$340,000"),
                W(t, "reallife_ui_cons_st_handover", "Hand over today"), UiRole.Ok)
        };
        m.Side = ConsSide(t, runs);
        m.WeekTitle = W(t, "reallife_ui_cons_week_title", "On the sites soon");
        m.Week = new[]
        {
            new ConstructionDay { When = "Day 215", What = W(t, "reallife_ui_cons_wk_delivery", "Materials for {project} arrive from {merchant}").Replace("{project}", "Office block").Replace("{merchant}", "Harbourside Freight"), Amount = null, Run = runs.Do(() => { }) },
            new ConstructionDay { When = "Day 216", What = W(t, "reallife_ui_cons_wk_inspect", "The inspector visits {project}: {what}").Replace("{project}", "Cafe refit").Replace("{what}", "Final inspection"),
                Amount = W(t, "reallife_ui_cons_no_cost", "No cost"), Run = runs.Do(() => { }) },
            new ConstructionDay { When = "Day 218", What = W(t, "reallife_ui_cons_wk_council", "The council decides on {project}").Replace("{project}", "Car park"), Amount = null, Run = runs.Do(() => { }) },
            new ConstructionDay { When = "Day 219", What = W(t, "reallife_ui_cons_wk_opening", "Opening day of {project}").Replace("{project}", "Shop front and unit"), Amount = null, Run = runs.Do(() => { }) }
        };
    }

    private static UiFactCardModel[] ConsSide(UiWords t, ConstructionRuns runs)
    {
        return new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ui_cons_money_title", "Money on the jobs"), Heading = true, Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_ui_cons_budgets", "Budgets, {count} open").Replace("{count}", "4"), "$2,478,000"),
                    UiFactLine.Row(W(t, "reallife_ui_cons_spent_far", "Spent so far"), "$1,262,000"),
                    UiFactLine.Row(W(t, "reallife_ui_cons_still_pay", "Still to pay"), "$1,216,000", UiRole.WarnText),
                    UiFactLine.Row(W(t, "reallife_ui_cons_cash_held", "Cash held for it"), "$1,240,000", UiRole.OkText)
                }
            },
            new UiFactCardModel
            {
                Title = W(t, "reallife_ui_cons_crews_title", "Crews"), Heading = true, Lines = new[]
                {
                    UiFactLine.Who("Joe Marsh's frame crew", "Office block, 14 floors", W(t, "reallife_ui_cons_workers", "{count} workers").Replace("{count}", "8")),
                    UiFactLine.Who("Lucia Park's finishers", "Cafe refit", W(t, "reallife_ui_cons_workers", "{count} workers").Replace("{count}", "6")),
                    UiFactLine.Who("Kofi Owusu's groundworks", "Car park, 120 bays", W(t, "reallife_ui_cons_workers", "{count} workers").Replace("{count}", "5"))
                }
            },
            new UiFactCardModel
            {
                Title = W(t, "reallife_ui_cons_planning_title", "Planning"), Heading = true, Lines = new[]
                {
                    UiFactLine.Row("Car park, 120 bays", "Day 218"), UiFactLine.Row("Show flats", W(t, "reallife_bld_stage_ready", "Ready to build"))
                }
            },
            new UiFactCardModel
            {
                Title = W(t, "reallife_ui_cons_rivals_title", "Rival builders"), Heading = true, Lines = new[]
                {
                    UiFactLine.Who("Huang Guo Developments", "Offices", W(t, "reallife_ui_cons_rep", "Standing {n}").Replace("{n}", "78")),
                    UiFactLine.Who("Hartigan & Sons", "Homes", W(t, "reallife_ui_cons_rep", "Standing {n}").Replace("{n}", "64"))
                }
            }
        };
    }

    private static void ConsOverviewNew(UiWords t, ConstructionRuns runs, ConstructionModel m, bool founded)
    {
        m.Shape = ConstructionShape.Overview;
        m.HeadTitle = W(t, "reallife_bld_glance_title", "Your construction company");
        m.HeadSub = W(t, "reallife_bld_glance_client", "No company is needed to build: the city's builders can be hired.");
        m.Lead = new ConstructionLead
        {
            Icon = UiIcon.Briefcase, Kicker = W(t, "reallife_ui_cons_lead_start", "Start your company"), Title = W(t, "reallife_bld_glance_found", "Found {name} as a builder").Replace("{name}", "Pioneer Construction"),
            Actions = new[]
            {
                ConsButton(runs, W(t, "reallife_bld_button_found", "Found the company"), UiButtonKind.Primary, UiAction.Alt),
                ConsButton(runs, W(t, "reallife_bld_button_land", "See the land"), UiButtonKind.Secondary, UiAction.Option)
            }
        };
        m.RowsTitle = W(t, "reallife_ui_cons_projects_title", "Projects under way");
        m.RowsEmpty = W(t, "reallife_bld_empty_projects", "No projects yet. Buy a lot, or pick a building of yours, on the Land tab.");
        m.Side = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ui_cons_rivals_title", "Rival builders"), Heading = true, Lines = new[]
                {
                    UiFactLine.Who("Huang Guo Developments", "Offices", W(t, "reallife_ui_cons_rep", "Standing {n}").Replace("{n}", "78")),
                    UiFactLine.Who("Hartigan & Sons", "Homes", W(t, "reallife_ui_cons_rep", "Standing {n}").Replace("{n}", "64")),
                    UiFactLine.Who("Eastside Builders", "Renovation", W(t, "reallife_ui_cons_rep", "Standing {n}").Replace("{n}", "58"))
                }
            }
        };
    }

    // ------------------------------------------------------------------------------------------------------------------ the tabs

    private static UiDataTableModel ConsTable(UiColumn0[] columns, string[][] rows, UiCellKind[] kinds, UiRole[][] tones, int[][] values)
    {
        var cols = new UiDataColumn[columns.Length];
        for (int c = 0; c < cols.Length; c++)
        {
            cols[c] = new UiDataColumn { Title = columns[c].Title, Kind = kinds[c], Width = columns[c].Width, Sortable = columns[c].Sortable };
        }
        var data = new UiDataRow[rows.Length];
        for (int r = 0; r < data.Length; r++)
        {
            data[r] = new UiDataRow { Cells = rows[r], Tones = tones != null ? tones[r] : new UiRole[rows[r].Length], Values = values != null ? values[r] : new int[rows[r].Length] };
        }
        return new UiDataTableModel { Columns = cols, Rows = data, RowCount = data.Length, Selected = -1 };
    }

    private struct UiColumn0
    {
        public string Title;
        public float Width;
        public bool Sortable;
    }

    private static UiColumn0 Col(string title, float width = 0f, bool sortable = true) => new UiColumn0 { Title = title, Width = width, Sortable = sortable };

    private static int Icon(UiIcon icon) => (int)icon;

    private static void ConsHead(UiWords t, ConstructionModel m, UiIcon icon, string key, string english, string[] labels, string[] values)
    {
        m.Shape = ConstructionShape.Table;
        m.HeadIcon = icon;
        m.HeadTitle = W(t, key, english);
        m.HeadSub = W(t, "reallife_bld_headline", "{name}: reputation {reputation}, sites going up {sites}").Replace("{name}", "Pioneer Construction").Replace("{reputation}", "72").Replace("{sites}", "5");
        m.FigLabels = labels;
        m.FigValues = values;
    }

    private static UiFactCardModel ConsAbout(UiWords t, ConstructionRuns runs, string note, string actionKey, string actionEnglish)
    {
        return new UiFactCardModel
        {
            Title = null, Lines = new[] { UiFactLine.Say(W(t, note, note)) },
            Actions = actionKey == null ? new UiActionModel[0] : new[] { ConsButton(runs, W(t, actionKey, actionEnglish), UiButtonKind.Primary, UiAction.Alt) }
        };
    }

    private static void ConsLand(UiWords t, ConstructionRuns runs, ConstructionModel m, bool plot)
    {
        ConsHead(t, m, UiIcon.MapTrifold, "reallife_bld_tab_land", "Land", new[] { W(t, "reallife_ui_cons_fig_for_sale", "For sale"), W(t, "reallife_ui_cons_fig_yours", "Yours") }, new[] { "7", "2" });
        string[][] rows =
        {
            new[] { "17 E 41st Street", "40 x 31 m", "Mixed use", "$2,480,000", "$2,000", "For sale" },
            new[] { "233 W 39th Street", "28 x 31 m", "Commercial", "$1,376,000", "$1,586", "For sale" },
            new[] { "9 E 33rd Street", "36 x 31 m", "Homes", "$1,430,000", "$1,281", "For sale" },
            new[] { "75 W 125th Street", "60 x 30 m", "Homes", "$1,440,000", "$800", "Yours" },
            new[] { "Eastgate yard", "18 x 18 m", "Shops, offices", string.Empty, string.Empty, "Held for a landmark" }
        };
        UiRole[][] tones = new UiRole[rows.Length][];
        int[][] values = new int[rows.Length][];
        for (int i = 0; i < rows.Length; i++)
        {
            tones[i] = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, i == 3 ? UiRole.Ok : UiRole.Muted };
            values[i] = new[] { Icon(i == 3 ? UiIcon.Key : UiIcon.MapTrifold), 0, 0, 0, 0, 0 };
        }
        m.Table = ConsTable(new[]
        {
            Col(W(t, "reallife_bld_col_place", "Place")), Col(W(t, "reallife_bld_col_size", "Size"), 168f), Col(W(t, "reallife_bld_col_zoned", "Zoned for"), 168f),
            Col(W(t, "reallife_bld_col_price", "Price"), 144f), Col(W(t, "reallife_ui_cons_col_sqm", "A m2"), 120f), Col(W(t, "reallife_bld_col_state", "State"), 232f)
        }, rows, new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Text, UiCellKind.Money, UiCellKind.Money, UiCellKind.Chip }, tones, values);
        m.Table.Filters = new[] { W(t, "reallife_bld_all_land", "All land") };
        m.Foot = new UiFactCardModel
        {
            Title = W(t, "reallife_ui_cons_asks_title", "Asking a m2 by district"), Heading = true, Lines = new[]
            {
                UiFactLine.Row("Uptown", "$1,520  +3.1%"), UiFactLine.Row("Cedar Heights", "$940  -0.8%")
            }
        };
        m.FilterKinds = 6;
        m.RowKeys = new[] { "lot:a", "lot:b", "lot:c", "own:d", "lmk:e" };
        m.Card = new UiFactCardModel
        {
            Lines = new[] { UiFactLine.Say(W(t, "reallife_bld_land_pick", "Pick a lot to see what it is, what it costs and what can stand on it.")) }
        };
        if (plot)
        {
            m.Shape = ConstructionShape.Detail;
            m.Back = W(t, "reallife_bld_back", "Back");
            m.DetailIcon = UiIcon.MapTrifold;
            m.DetailTitle = "17 E 41st Street";
            m.DetailSub = "Mixed use";
            m.DetailLocate = W(t, "reallife_bld_button_map", "Show on map");
            m.DetailLocateRun = runs.Do(() => { });
            m.Actions = new[]
            {
                ConsButton(runs, W(t, "reallife_bld_button_buy_lot", "Buy the lot"), UiButtonKind.Primary, UiAction.Alt)
            };
            m.Cards = new[]
            {
                new UiFactCardModel
                {
                    Lines = new[]
                    {
                        UiFactLine.Row(W(t, "reallife_bld_row_size", "Size"), "40 x 31 m"), UiFactLine.Row(W(t, "reallife_bld_row_traffic", "Traffic"), "62"),
                        UiFactLine.Row(W(t, "reallife_bld_row_zoned", "Zoned for"), "Shops, offices, homes"), UiFactLine.Row(W(t, "reallife_bld_row_usual", "Usual floor area"), "3,720 m2"),
                        UiFactLine.Row(W(t, "reallife_bld_row_price", "Price"), "$2,480,000"), UiFactLine.Row(W(t, "reallife_bld_row_land_tax", "Land tax"), "$12,400 a year")
                    }
                }
            };
        }
    }

    private static void ConsProjects(UiWords t, ConstructionRuns runs, ConstructionModel m)
    {
        ConsHead(t, m, UiIcon.Hammer, "reallife_bld_tab_projects", "Projects", new[] { W(t, "reallife_ui_cons_fig_under_way", "Under way"), W(t, "reallife_ui_cons_fig_done", "Completed"), W(t, "reallife_ui_cons_fig_spent", "Spent") },
            new[] { "5", "14", "$1,602,000" });
        string[][] rows =
        {
            new[] { "Show flats", "Ready to build", "", "$312,000", "Start the works" },
            new[] { "Office block, 14 floors", "Frame", "46%", "$790,000", "Works are going on" },
            new[] { "Car park, 120 bays", "Planning", "", "$64,000", "Waits for the council" },
            new[] { "Cafe refit", "Fit out", "81%", "$96,000", "Book the inspection" },
            new[] { "Shop front and unit", "Finished", "", "$340,000", "Handed over" }
        };
        var tones = new UiRole[rows.Length][];
        var values = new int[rows.Length][];
        int[] progress = { 0, 46, 0, 81, 100 };
        for (int i = 0; i < rows.Length; i++)
        {
            tones[i] = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, i == 0 || i == 3 ? UiRole.Warn : i == 4 ? UiRole.Ok : UiRole.Muted };
            values[i] = new[] { Icon(UiIcon.Hammer), 0, progress[i], 0, 0 };
        }
        m.Table = ConsTable(new[]
        {
            Col(W(t, "reallife_bld_col_project", "Project")), Col(W(t, "reallife_bld_col_stage", "Stage"), 168f), Col(W(t, "reallife_bld_col_progress", "Progress"), 144f),
            Col(W(t, "reallife_bld_col_spent", "Spent"), 144f), Col(W(t, "reallife_bld_col_next", "Next"), 232f)
        }, rows, new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Meter, UiCellKind.Money, UiCellKind.Chip }, tones, values);
        m.RowKeys = new[] { "prj:1", "prj:2", "prj:3", "prj:4", "prj:5" };
        m.Card = new UiFactCardModel { Lines = new[] { UiFactLine.Say(W(t, "reallife_bld_projects_pick", "Pick a project to see where it stands and what to do next.")) } };
    }

    private static void ConsProject(UiWords t, ConstructionRuns runs, ConstructionModel m, string state)
    {
        m.Shape = ConstructionShape.Detail;
        m.Back = W(t, "reallife_bld_back", "Back");
        m.DetailIcon = UiIcon.Hammer;
        m.DetailTitle = "Show flats";
        m.DetailSub = state == "project_site" ? W(t, "reallife_bld_stage_frame", "Frame") : W(t, "reallife_bld_stage_ready", "Ready to build");
        m.DetailLocate = W(t, "reallife_bld_button_map", "Show on map");
        m.DetailLocateRun = runs.Do(() => { });
        int at = state == "project_plans" ? 0 : state == "project_ready" ? 2 : 3;
        var steps = new UiTrackStep[5];
        UiIcon[] icons = { UiIcon.Pencil, UiIcon.Scales, UiIcon.Key, UiIcon.Hammer, UiIcon.Flag };
        string[] labels = StepLabels(t);
        for (int i = 0; i < steps.Length; i++)
        {
            steps[i] = new UiTrackStep { Icon = icons[i], Label = labels[i], Note = i == at ? "Day 245" : string.Empty, State = i < at ? UiStepState.Done : i == at ? UiStepState.Now : i == at + 1 ? UiStepState.Next : UiStepState.Later };
        }
        m.Track = new UiTrackModel
        {
            Title = W(t, "reallife_ui_cons_track", "Where it stands"), Line = m.DetailSub, Steps = steps,
            StateWords = new[] { W(t, "reallife_ui_cons_state_done", "Done"), W(t, "reallife_ui_cons_state_now", "Now"), W(t, "reallife_ui_cons_state_next", "Next"),
                W(t, "reallife_ui_cons_state_later", "Later"), W(t, "reallife_ui_cons_state_later", "Later") }
        };
        UiActionModel start = ConsButton(runs, W(t, "reallife_bld_button_start", "Start building"), UiButtonKind.Primary, UiAction.Alt);
        UiActionModel money = ConsButton(runs, W(t, "reallife_bld_button_money", "Money"), UiButtonKind.Secondary, UiAction.Option);
        m.Actions = state == "project_ready" ? new[] { money, start } : new UiActionModel[0];
        var cards = new List<UiFactCardModel>
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_bld_sec_ready", "Ready to build"), Heading = true, Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_bld_row_estimate", "Estimate"), "$498,000"), UiFactLine.Row(W(t, "reallife_bld_row_crews", "Crews on it"), "1"),
                    UiFactLine.Amount(null, "Keep the flats to let", null, true, true, ConstructionFace.StepLess, ConstructionFace.StepMore, runs.Do(() => { }))
                }
            },
            new UiFactCardModel
            {
                Title = W(t, "reallife_bld_sec_materials", "Materials"), Heading = true, Lines = new[]
                {
                    UiFactLine.Tick("Concrete: 4.2 of 6.0 loads", false), UiFactLine.Tick("Steel: 3.0 of 3.0 loads", true),
                    UiFactLine.Say(W(t, "reallife_bld_delivery_coming", "{merchant} bring the next load on {day}").Replace("{merchant}", "Harbourside Freight").Replace("{day}", "day 219"))
                }
            }
        };
        runs.Do(() => { });
        m.Cards = cards.ToArray();
    }

    private static void ConsTenders(UiWords t, ConstructionRuns runs, ConstructionModel m, bool bid)
    {
        ConsHead(t, m, UiIcon.ClipboardText, "reallife_bld_tab_tenders", "Tenders", new[] { W(t, "reallife_ui_cons_fig_open", "Open"), W(t, "reallife_ui_cons_fig_won", "Won"), W(t, "reallife_ui_cons_fig_lost", "Lost") },
            new[] { "3", "6", "4" });
        string[][] rows =
        {
            new[] { "Resurfacing of Broadway", "$412,000", "Day 219, 18:00", "98%", "Open" },
            new[] { "Library extension", "$1,900,000", "Day 224, 12:00", "", "Open" },
            new[] { "Flats for a client, 12 W 33rd Street", "$740,000", "Day 201, 18:00", "104%", "Lost to Hartigan & Sons" }
        };
        var tones = new UiRole[rows.Length][];
        var values = new int[rows.Length][];
        for (int i = 0; i < rows.Length; i++)
        {
            tones[i] = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, i == 2 ? UiRole.Danger : UiRole.Muted };
            values[i] = new[] { Icon(UiIcon.ClipboardText), 0, 0, 0, 0 };
        }
        m.Table = ConsTable(new[]
        {
            Col(W(t, "reallife_bld_col_works", "Works")), Col(W(t, "reallife_bld_col_value", "Value"), 144f), Col(W(t, "reallife_bld_col_closes", "Closes"), 168f),
            Col(W(t, "reallife_bld_col_bid", "Your bid"), 144f), Col(W(t, "reallife_bld_col_state", "State"), 232f)
        }, rows, new[] { UiCellKind.Tile, UiCellKind.Money, UiCellKind.Text, UiCellKind.Text, UiCellKind.Chip }, tones, values);
        m.RowKeys = new[] { "tnd:1", "tnd:2", "tnd:3" };
        m.Card = new UiFactCardModel { Lines = new[] { UiFactLine.Say(W(t, "reallife_bld_tenders_pick", "Pick works to see what they ask and to put in a bid.")) } };
        if (bid)
        {
            m.Shape = ConstructionShape.Detail;
            m.Table = null;
            m.Back = W(t, "reallife_bld_back", "Back");
            m.DetailIcon = UiIcon.ClipboardText;
            m.DetailTitle = "Resurfacing of Broadway";
            m.DetailSub = W(t, "reallife_bld_source_city", "The city's works");
            m.Actions = new[] { ConsButton(runs, W(t, "reallife_bld_button_bid", "Put in the bid"), UiButtonKind.Primary, UiAction.Alt) };
            m.Cards = new[]
            {
                new UiFactCardModel
                {
                    Lines = new[]
                    {
                        UiFactLine.Row(W(t, "reallife_bld_row_value", "Value"), "$412,000"), UiFactLine.Row(W(t, "reallife_bld_row_hours", "Working hours"), "2,400"),
                        UiFactLine.Row(W(t, "reallife_bld_row_closes", "Closes"), "Day 219, 18:00"), UiFactLine.Tick("Suits your speciality: roads", true)
                    }
                },
                new UiFactCardModel
                {
                    Title = W(t, "reallife_bld_sec_bid", "Your bid"), Heading = true, Lines = new[]
                    {
                        UiFactLine.Amount(null, "98% of the value, $403,760", null, true, true, ConstructionFace.StepLess, ConstructionFace.StepMore, runs.Do(() => { })),
                        UiFactLine.Toggle(W(t, "reallife_bld_switch_fast", "Finish fast"), false, ConstructionFace.ActionCommand, runs.Do(() => { })),
                        UiFactLine.Say(W(t, "reallife_bld_bid_note", "The works take {days} days. Your standing is {reputation}.").Replace("{days}", "34").Replace("{reputation}", "72"))
                    }
                }
            };
        }
    }

    private static void ConsCrews(UiWords t, ConstructionRuns runs, ConstructionModel m, bool crew)
    {
        ConsHead(t, m, UiIcon.People, "reallife_bld_tab_crews", "Crews", new[] { W(t, "reallife_ui_cons_fig_crews", "Crews"), W(t, "reallife_ui_cons_fig_on_site", "Crews on site"), W(t, "reallife_ui_cons_fig_a_day", "A day") },
            new[] { "3", "3", "$3,240" });
        string[][] rows =
        {
            new[] { "Joe Marsh's frame crew", "Frame gang", "74", "62", "$1,260", "Office block" },
            new[] { "Lucia Park's finishers", "Finishing gang", "81", "35", "$1,080", "Cafe refit" },
            new[] { "Kofi Owusu's groundworks", "Ground gang", "58", "18", "$900", "Car park" }
        };
        var tones = new UiRole[rows.Length][];
        var values = new int[rows.Length][];
        int[][] meters = { new[] { 74, 62 }, new[] { 81, 35 }, new[] { 58, 18 } };
        for (int i = 0; i < rows.Length; i++)
        {
            tones[i] = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Muted };
            values[i] = new[] { Icon(UiIcon.People), 0, meters[i][0], meters[i][1], 0, 0 };
        }
        m.Table = ConsTable(new[]
        {
            Col(W(t, "reallife_bld_col_name", "Name")), Col(W(t, "reallife_bld_col_kind", "Kind"), 168f), Col(W(t, "reallife_bld_col_skill", "Skill"), 144f),
            Col(W(t, "reallife_bld_col_morale", "Morale"), 144f), Col(W(t, "reallife_bld_col_day", "A day"), 144f), Col(W(t, "reallife_bld_col_where", "Where"), 232f)
        }, rows, new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Meter, UiCellKind.Meter, UiCellKind.Money, UiCellKind.Chip }, tones, values);
        m.RowKeys = new[] { "crew:1", "crew:2", "crew:3" };
        m.FilterKinds = 4;
        m.Table.Filters = new[] { W(t, "reallife_bld_all_people", "All people") };
        m.Card = new UiFactCardModel
        {
            Lines = new[] { UiFactLine.Say(W(t, "reallife_bld_crews_pick", "Pick a crew to see its people, its mood and where it works.")) },
            Actions = new[] { ConsButton(runs, W(t, "reallife_bld_button_hire", "Hire"), UiButtonKind.Primary, UiAction.Alt) }
        };
        if (crew)
        {
            m.Shape = ConstructionShape.Detail;
            m.Table = null;
            m.Card = null;
            m.Back = W(t, "reallife_bld_back", "Back");
            m.DetailIcon = UiIcon.People;
            m.DetailTitle = "Joe Marsh's frame crew";
            m.DetailSub = "Frame gang";
            m.Actions = new[] { ConsButton(runs, W(t, "reallife_bld_button_release", "Let the crew go"), UiButtonKind.Secondary, UiAction.Option) };
            m.Cards = new[]
            {
                new UiFactCardModel
                {
                    Lines = new[]
                    {
                        UiFactLine.Row(W(t, "reallife_bld_row_skill", "Skill"), "74"), UiFactLine.Bar(W(t, "reallife_bld_row_morale", "Morale"), 62, "62"),
                        UiFactLine.Row(W(t, "reallife_bld_row_rate", "Pay a day"), "$1,260"), UiFactLine.Say("4 labourers, a mason, a carpenter")
                    }
                }
            };
        }
    }

    private static void ConsCompany(UiWords t, ConstructionRuns runs, ConstructionModel m, bool licence)
    {
        ConsHead(t, m, UiIcon.Briefcase, "reallife_bld_tab_company", "Company", new[] { W(t, "reallife_ui_cons_fig_standing", "Standing"), W(t, "reallife_ui_cons_fig_done", "Completed"),
            W(t, "reallife_ui_cons_fig_books", "Books") }, new[] { "72", "14", "$412,000" });
        string[][] rows =
        {
            new[] { "Office", "Pioneer Construction" }, new[] { "Licence", "Licensed until day 412" }, new[] { "Reputation", "72" }, new[] { "Speciality", "Offices" },
            new[] { "Cover", "Full cover" }, new[] { "Yard", "Dock Street warehouse" }, new[] { "Record", "14 built, 12 on time" }, new[] { "Books", "$412,000" }
        };
        m.Table = ConsTable(new[] { Col(W(t, "reallife_bld_col_what", "What"), 0f, false), Col(W(t, "reallife_bld_col_state", "How it stands"), 0f, false) }, rows,
            new[] { UiCellKind.Text, UiCellKind.Text }, null, null);
        m.RowKeys = new[] { "co:office", "co:licence", "co:reputation", "co:speciality", "co:cover", "co:yard", "co:record", "co:books" };
        m.Card = new UiFactCardModel { Lines = new[] { UiFactLine.Say(W(t, "reallife_bld_company_pick", "Pick a part of the company to see it and change it.")) } };
        if (licence)
        {
            m.Shape = ConstructionShape.Detail;
            m.Table = null;
            m.Card = null;
            m.Back = W(t, "reallife_bld_back", "Back");
            m.DetailIcon = UiIcon.Briefcase;
            m.DetailTitle = "Licence";
            m.DetailSub = "Licensed until day 412";
            m.Actions = new[] { ConsButton(runs, W(t, "reallife_bld_button_city_hall", "Go to City Hall"), UiButtonKind.Primary, UiAction.Alt) };
            m.Cards = new[]
            {
                new UiFactCardModel
                {
                    Lines = new[]
                    {
                        UiFactLine.Row(W(t, "reallife_bld_row_fee", "Fee"), "$8,000"), UiFactLine.Row(W(t, "reallife_bld_row_decided", "Decided in"), "5 days"),
                        UiFactLine.Row(W(t, "reallife_bld_row_until", "Holds until"), "Day 412"), UiFactLine.Say(W(t, "reallife_bld_licence_note", "A licence lets the company build for others and bid for the city's works."))
                    }
                }
            };
        }
    }

    // The speciality's page of a company that has chosen nothing yet, as the page writes it: what it is known for now under the title, the note, the words that say the first
    // choice has no wait, and every other speciality as a line of its own that is pressed (the game's question box names the wait before it is set).
    private static void ConsSpeciality(UiWords t, ConstructionRuns runs, ConstructionModel m)
    {
        m.Shape = ConstructionShape.Detail;
        m.Back = W(t, "reallife_bld_back", "Back");
        m.DetailIcon = UiIcon.Briefcase;
        m.DetailTitle = W(t, "reallife_bld_co_speciality", "Speciality");
        m.DetailSub = W(t, "reallife_bld_spec_none", "Nothing in particular");
        m.Cards = new[]
        {
            new UiFactCardModel
            {
                Lines = new[]
                {
                    UiFactLine.Say(W(t, "reallife_bld_speciality_note", "A speciality makes its kind of work faster and its materials cheaper, and wins its tenders more often. It can change once in {days} days.")
                        .Replace("{days}", "30")),
                    UiFactLine.Say(W(t, "reallife_bld_speciality_first", "Pick what your company is known for. The first choice has no wait.")),
                    runs.Link(W(t, "reallife_bld_spec_homes", "Homes"), () => { }),
                    runs.Link(W(t, "reallife_bld_spec_offices", "Offices"), () => { }),
                    runs.Link(W(t, "reallife_bld_spec_roads", "Roads and utilities"), () => { }),
                    runs.Link(W(t, "reallife_bld_spec_renovation", "Rebuilds and refits"), () => { }),
                    runs.Link(W(t, "reallife_bld_spec_demolition", "Demolition and groundworks"), () => { })
                }
            }
        };
    }

    private static void ConsRivals(UiWords t, ConstructionRuns runs, ConstructionModel m)
    {
        ConsHead(t, m, UiIcon.Flag, "reallife_bld_tab_rivals", "Rivals", new[] { W(t, "reallife_ui_cons_fig_builders", "Builders"), W(t, "reallife_ui_cons_fig_rank", "Your rank") }, new[] { "8", "3 of 9" });
        string[][] rows =
        {
            new[] { "Huang Guo Developments", "Offices", "78", "9", "21", "12 floors", "3" }, new[] { "Hartigan & Sons", "Homes", "64", "6", "17", "10 floors", "" },
            new[] { "Eastside Builders", "Renovation", "58", "4", "12", "10 floors", "1" }, new[] { "Northgate Roadworks", "Roads", "51", "3", "10", "8 floors", "" }
        };
        var values = new int[rows.Length][];
        int[] rep = { 78, 64, 58, 51 };
        for (int i = 0; i < rows.Length; i++)
        {
            values[i] = new[] { Icon(UiIcon.Flag), 0, rep[i], 0, 0, 0, 0 };
        }
        m.Table = ConsTable(new[]
        {
            Col(W(t, "reallife_bld_col_builder", "Builder")), Col(W(t, "reallife_bld_col_speciality", "Speciality"), 168f), Col(W(t, "reallife_bld_col_reputation", "Reputation"), 144f),
            Col(W(t, "reallife_ui_cons_col_floors", "Floors"), 144f), Col(W(t, "reallife_bld_col_won", "Won"), 96f), Col(W(t, "reallife_bld_col_built", "Built"), 96f),
            Col(W(t, "reallife_ui_cons_col_jobs", "Your jobs"), 120f)
        }, rows, new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Meter, UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure }, null, values);
        m.RowKeys = new[] { "rival:0", "rival:1", "rival:2", "rival:3" };
        m.Card = new UiFactCardModel { Lines = new[] { UiFactLine.Say(W(t, "reallife_bld_rivals_pick", "Pick a builder to see what it is known for.")) } };
    }

    private static void ConsCity(UiWords t, ConstructionRuns runs, ConstructionModel m)
    {
        ConsHead(t, m, UiIcon.Scales, "reallife_bld_tab_city", "City", new[] { W(t, "reallife_ui_cons_fig_bills", "Bills"), W(t, "reallife_ui_cons_fig_owed", "Owed") }, new[] { "2", "$64,000" });
        string[][] rows =
        {
            new[] { "Height limit, Uptown", "12 floors", "", "" }, new[] { "Rezoning of 75 W 125th Street", "Heard on day 218", "", "Day 218" },
            new[] { "Land tax", "Due", "$12,400", "Day 220" }, new[] { "Building of the year", "Won by Hartigan & Sons", "", "Day 180" }
        };
        var values = new int[rows.Length][];
        UiIcon[] icons = { UiIcon.ListChecks, UiIcon.Scales, UiIcon.Receipt, UiIcon.Trophy };
        for (int i = 0; i < rows.Length; i++)
        {
            values[i] = new[] { Icon(icons[i]), 0, 0, 0 };
        }
        m.Table = ConsTable(new[]
        {
            Col(W(t, "reallife_bld_col_what", "What")), Col(W(t, "reallife_bld_col_state", "How it stands"), 232f), Col(W(t, "reallife_bld_col_value", "Value"), 144f), Col(W(t, "reallife_bld_col_when", "When"), 168f)
        }, rows, new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Money, UiCellKind.Text }, null, values);
        m.FilterKinds = 4;
        m.Table.Filters = new[] { W(t, "reallife_bld_all_city", "Everything") };
        m.RowKeys = new[] { "rule:0", "council:1", "bill:2", "award:3" };
        m.Card = new UiFactCardModel { Lines = new[] { UiFactLine.Say(W(t, "reallife_bld_city_note", "The building code in force: {code}.").Replace("{code}", "Standard code")) } };
    }

    private static void ConsQuotes(UiWords t, ConstructionRuns runs, ConstructionModel m)
    {
        m.Shape = ConstructionShape.Detail;
        m.Back = W(t, "reallife_bld_back", "Back");
        m.DetailIcon = UiIcon.Hammer;
        m.DetailTitle = "Show flats";
        m.DetailSub = W(t, "reallife_bld_stage_ready", "Ready to build");
        m.DetailLocate = W(t, "reallife_bld_button_map", "Show on map");
        m.DetailLocateRun = runs.Do(() => { });
        string[][] rows =
        {
            new[] { "Pioneer Hire", "$186,000", W(t, "reallife_ui_cons_days_n", "{count} days").Replace("{count}", "42"), "", W(t, "reallife_ui_cons_risk_low", "Low") },
            new[] { "Hartigan & Sons", "$171,500", W(t, "reallife_ui_cons_days_n", "{count} days").Replace("{count}", "56"), "", W(t, "reallife_ui_cons_risk_medium", "Medium") },
            new[] { "Eastside Builders", "$194,000", W(t, "reallife_ui_cons_days_n", "{count} days").Replace("{count}", "35"), "", W(t, "reallife_ui_cons_risk_low", "Low") },
            new[] { "Huang Guo Developments", "$169,000", W(t, "reallife_ui_cons_days_n", "{count} days").Replace("{count}", "63"), "", W(t, "reallife_ui_cons_risk_unlicensed", "Not licensed") }
        };
        var tones = new UiRole[rows.Length][];
        var values = new int[rows.Length][];
        int[] quality = { 84, 68, 91, 41 };
        UiRole[] risk = { UiRole.Ok, UiRole.Warn, UiRole.Ok, UiRole.Danger };
        for (int i = 0; i < rows.Length; i++)
        {
            tones[i] = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, risk[i] };
            values[i] = new[] { Icon(UiIcon.Flag), 0, 0, quality[i], 0 };
        }
        m.DetailTable = ConsTable(new[]
        {
            Col(W(t, "reallife_bld_col_builder", "Builder"), 0f, false), Col(W(t, "reallife_bld_row_quote", "Quote"), 144f, false), Col(W(t, "reallife_ui_cons_col_time", "Time"), 120f, false),
            Col(W(t, "reallife_ui_cons_col_quality", "Quality"), 168f, false), Col(W(t, "reallife_ui_cons_col_risk", "Risk"), 200f, false)
        }, rows, new[] { UiCellKind.Tile, UiCellKind.Money, UiCellKind.Text, UiCellKind.Meter, UiCellKind.Chip }, tones, values);
        m.DetailTable.Selected = 0;
        m.RowKeys = new[] { "q:0", "q:1", "q:2", "q:3" };
        m.Actions = new[]
        {
            ConsButton(runs, W(t, "reallife_bld_button_cheapest", "The cheapest"), UiButtonKind.Secondary, UiAction.Option),
            ConsButton(runs, W(t, "reallife_bld_button_hire_this", "Hire this builder"), UiButtonKind.Primary, UiAction.Alt)
        };
        m.Cards = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ui_cons_sec_quote", "The quote"), Heading = true, Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_bld_row_quote", "Quote"), "$186,000"), UiFactLine.Row(W(t, "reallife_bld_row_quote_days", "Working days"), "42"),
                    UiFactLine.Row(W(t, "reallife_bld_row_deposit", "Deposit"), "$18,600"), UiFactLine.Row(W(t, "reallife_bld_row_retention", "Retention"), "$9,300")
                }
            },
            new UiFactCardModel
            {
                Title = W(t, "reallife_ui_cons_sec_means", "What the quote means"), Heading = true, Lines = new[]
                {
                    UiFactLine.Say(W(t, "reallife_ui_cons_means_cheap_sooner", "{builder} asks {amount} more than {other}, the cheapest, but finishes sooner.")
                        .Replace("{builder}", "Pioneer Hire").Replace("{amount}", "$14,500").Replace("{other}", "Hartigan & Sons")),
                    UiFactLine.Say(W(t, "reallife_ui_cons_means_rent", "{count} days of waiting costs a building about {amount} in rent.").Replace("{count}", "14").Replace("{amount}", "$17,200"))
                }
            }
        };
    }
}
