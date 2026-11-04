using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Property app's and the Car Showroom's samples: the model each page writes, with real-looking names, long names, empty, full and late states. Every word
// goes through the screen's text source so the checks measure it in all eleven languages. Names here are placeholders and never ship.
internal static partial class UiSamples
{
    public static readonly string[] EstateStates =
    {
        "roll", "roll_twenty", "roll_first", "roll_empty", "unit_late", "unit_request", "unit_empty", "tenants", "works", "market", "building", "listing", "works_page", "call", "applicant", "agency", "agency_closed", "hotel",
        "hire", "rents", "rents_new", "off"
    };

    public static readonly string[] DealerStates =
    {
        "stock", "stock_empty", "stock_full", "market", "used", "auction", "auction_new", "offers", "deal", "finance", "growth", "event", "reports", "reports_new", "lots", "yard", "off"
    };

    private static UiPrompt[] EstatePrompts(UiWords t) => new[]
    {
        new UiPrompt { Action = UiAction.Move, Label = W(t, "reallife_ui_prompt_move", "Move") },
        new UiPrompt { Action = UiAction.Confirm, Label = W(t, "reallife_ui_prompt_select", "Select") },
        new UiPrompt { Action = UiAction.Back, Label = W(t, "reallife_ui_prompt_back", "Back") }
    };

    private static Func<bool> Yes => () => true;

    private static UiActionModel EstateButton(List<Func<bool>> runs, string label, UiButtonKind kind, UiIcon icon, UiAction key, bool disabled = false)
    {
        runs.Add(Yes);
        return new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = key, Command = EstateFace.ActionCommand, Arg = disabled ? -1 : runs.Count - 1, Disabled = disabled };
    }

    private static UiFactLine EstateLink(List<Func<bool>> runs, string words)
    {
        runs.Add(Yes);
        return UiFactLine.Go(words, EstateFace.ActionCommand, runs.Count - 1);
    }

    private static UiFactLine EstateStep(List<Func<bool>> runs, string label, string value, string hint, bool less = true, bool more = true)
    {
        runs.Add(Yes);
        runs.Add(Yes);
        return UiFactLine.Amount(label, value, hint, less, more, EstateFace.StepLess, EstateFace.StepMore, runs.Count - 2);
    }

    private static UiFactLine EstateSwitch(List<Func<bool>> runs, string label, bool on)
    {
        runs.Add(Yes);
        return UiFactLine.Toggle(label, on, EstateFace.ActionCommand, runs.Count - 1);
    }

    private static UiNavEntry[] EstateNav(UiWords t, string[] counts)
    {
        UiIcon[] icons = { UiIcon.Home, UiIcon.People, UiIcon.Wrench, UiIcon.Tag, UiIcon.Key, UiIcon.Building, UiIcon.Car };
        string[] labels =
        {
            W(t, "reallife_ui_estate_nav_mine", "Mine"), W(t, "reallife_ui_estate_nav_tenants", "Tenants"), W(t, "reallife_ui_estate_nav_works", "Works"), W(t, "reallife_ui_estate_nav_market", "Market"),
            W(t, "reallife_ui_estate_nav_agency", "Agency"), W(t, "reallife_ui_estate_nav_hotel", "Hotel"), W(t, "reallife_ui_estate_nav_hire", "Car hire")
        };
        string[] groups =
        {
            W(t, "reallife_ui_estate_group_mine", "Your property"), null, null, W(t, "reallife_ui_estate_group_market", "The market"), W(t, "reallife_ui_estate_group_business", "Run as a business"), null, null
        };
        var nav = new UiNavEntry[7];
        for (int i = 0; i < nav.Length; i++)
        {
            nav[i] = new UiNavEntry { Icon = icons[i], Label = labels[i], Count = counts != null && i < counts.Length ? counts[i] : null, Group = groups[i] };
        }
        return nav;
    }

    private static UiDataRow EstateUnitRow(UiWords t, string unit, string district, string tenant, string role, string lease, string rent, string chip, UiRole tone, int sat, UiIcon icon, bool empty)
    {
        return new UiDataRow
        {
            Cells = new[] { unit, empty ? W(t, "reallife_estate_row_nobody", "Nobody") : tenant, lease, rent, chip, sat >= 0 ? sat.ToString() : string.Empty },
            Values = new[] { (int)icon, empty ? UiDataTable.NoFace : 0, 0, 0, 0, sat },
            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, empty ? UiRole.Muted : UiRole.None, tone, UiRole.None },
            Subs = new[] { district, empty ? W(t, "reallife_ui_estate_asking_note", "Looking for a tenant") : role, null, null, null, null },
            Hue = UiColourHue(tenant), Ring = empty ? UiRole.None : sat >= 70 ? UiRole.Ok : sat >= 45 ? UiRole.Warn : UiRole.Danger
        };
    }

    private static float UiColourHue(string name) => UiColour.HueOf(name ?? string.Empty);

    private static UiDataColumn[] EstateRollColumns(UiWords t) => new[]
    {
        new UiDataColumn { Title = W(t, "reallife_ui_estate_col_unit", "Unit"), Kind = UiCellKind.Tile, Width = 0f },
        new UiDataColumn { Title = W(t, "reallife_ui_estate_col_tenant", "Tenant"), Kind = UiCellKind.Person, Width = 248f },
        new UiDataColumn { Title = W(t, "reallife_ui_estate_col_lease_ends", "Lease ends"), Kind = UiCellKind.Date, Width = 112f },
        new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rent_month", "Rent a month"), Kind = UiCellKind.Money, Width = 128f },
        new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 144f },
        new UiDataColumn { Title = W(t, "reallife_ui_estate_col_satisfaction", "Satisfaction"), Kind = UiCellKind.Meter, Width = 168f }
    };

    private static EstateModel EstateBase(UiWords t, string[] counts)
    {
        return new EstateModel { Title = W(t, "reallife_estate_app", "Property"), Icon = UiIcon.Home, Nav = EstateNav(t, counts), Prompts = EstatePrompts(t) };
    }

    public static EstateModel Estate(UiWords t, string state)
    {
        var runs = new List<Func<bool>>();
        EstateModel m = EstateBase(t, state == "roll_twenty" ? new[] { "20", "19", "10", null, null, null, null } : state == "roll_first" ? new[] { "1", "1", null, null, null, null, null } : state == "roll_empty" ? new string[7] : new[] { "12", "11", "10", null, null, null, null });
        switch (state)
        {
            case "off":
                m.Nav = new UiNavEntry[0];
                m.Shape = EstateShape.Off;
                m.OffIcon = UiIcon.Home;
                m.OffTitle = W(t, "reallife_estate_off_title", "The Property app is off");
                m.OffBody = W(t, "reallife_estate_off_body", "Turn it on in Mod settings, or load a city to see your property.");
                break;
            case "roll":
            case "roll_twenty":
            case "roll_first":
            case "roll_empty":
                Roll(t, m, runs, state);
                break;
            case "unit_late":
            case "unit_request":
            case "unit_empty":
                Unit(t, m, runs, state);
                break;
            case "tenants":
                m.Tab = 1;
                Tenants(t, m, runs);
                break;
            case "works":
                m.Tab = 2;
                Works(t, m, runs);
                break;
            case "market":
                m.Tab = 3;
                Market(t, m, runs);
                break;
            case "rents":
            case "rents_new":
                m.Tab = 3;
                Rents(t, m, state == "rents_new");
                break;
            case "agency":
            case "agency_closed":
                m.Tab = 4;
                Agency(t, m, runs, state == "agency_closed");
                break;
            case "hotel":
                m.Tab = 5;
                Hotel(t, m, runs);
                break;
            case "hire":
                m.Tab = 6;
                Hire(t, m, runs);
                break;
            default:
                EstateDetail(t, m, runs, state);
                break;
        }
        m.Runs = runs.ToArray();
        return m;
    }

    // The twelve units of the rent roll with their tenants: one tenant to a unit wherever the units are listed.
    private static string[][] RollUnits(UiWords t)
    {
        string[][] units = new[]
        {
            new[] { "41 E 37th Street, flat 3A", "Murray Hill", "Priya Nair", "Nurse, Murray Hill Clinic", "490", "$2,600", "ok", "82" },
            new[] { "41 E 37th Street, flat 3B", "Murray Hill", "Daniel Ortiz", "Cook, Pier Street Diner", "551", "$3,300", "ok", "74" },
            new[] { "41 E 37th Street, flat 4A", "Murray Hill", "Sofia Marchetti", "Nurse, Murray Hill Clinic", "462", "$2,600", "late", "51" },
            new[] { "12 W 39th Street, ground floor shop", "Garment District", "Lena Hoffmann", "A tailor's shop", "620", "$5,400", "ok", "68" },
            new[] { "12 W 39th Street, first floor office", "Garment District", "Marcus Bell", "Accountant, Bell and Rowe", "505", "$3,900", "ok", "77" },
            new[] { "233 W 39th Street, unit 1", "Garment District", "Amara Kimani", "A bakery", "480", "$6,200", "late14", "39" },
            new[] { "88 W 46th Street, flat 2", "Hell's Kitchen", "Theo Brandt", "Courier, Eastside Parcels", "533", "$2,200", "ok", "85" },
            new[] { "75 W 125th Street, house", "Uptown", "Imani Cole", "Teacher, Lincoln School", "650", "$3,100", "ok", "90" },
            new[] { "1 Dock Street, shop unit", "Uptown", null, null, "-", "$4,100", "empty", "-1" },
            new[] { "17 E 41st Street, floor 6", "Midtown", "Owen Park", "Designer, Park and Lane", "572", "$7,800", "ok", "71" },
            new[] { "520 8th Avenue, suite 12", "Midtown", "Yuki Tanaka", "An import firm", "430", "$5,600", "notice", "33" },
            new[] { "412 10th Avenue, unit B", "Hell's Kitchen", "Chen Wei", "Chef, Harbour Kitchen", "590", "$4,800", "ok", "63" }
        };
        foreach (string[] u in units)
        {
            u[3] = u[3] == null ? null : JobLine(t, u[3]);
        }
        return units;
    }

    private static void Roll(UiWords t, EstateModel m, List<Func<bool>> runs, string state)
    {
        var rows = new List<UiDataRow>();
        UiIcon home = UiIcon.Home;
        if (state == "roll_first")
        {
            rows.Add(EstateUnitRow(t, "12 W 39th Street, flat 2", "Garment District", "Priya Nair", JobLine(t, "Nurse, Murray Hill Clinic"), W(t, "reallife_estate_row_until", "Until day {day}").Replace("{day}", "490"), "$2,600", W(t, "reallife_ui_estate_paid", "Paid"), UiRole.Ok, 82, home, false));
        }
        else if (state == "roll" || state == "roll_twenty")
        {
            string[][] units = RollUnits(t);
            if (state == "roll_twenty")
            {
                string[][] more =
                {
                    new[] { "60 W 57th Street, flat 5", "Midtown", "Ana Souza", "Dentist, Park Smiles", "300", "$3,800", "ok", "79" },
                    new[] { "60 W 57th Street, flat 6", "Midtown", "Ben Carter", "Teacher, Lincoln School", "341", "$3,800", "ok", "72" },
                    new[] { "3 Canal Row, shop", "Garment District", "Ines Duarte", "A florist", "410", "$4,400", "ok", "81" },
                    new[] { "17 Mercer Street, house", "Murray Hill", "Tom Okafor", "Nurse, Murray Hill Clinic", "365", "$2,900", "ok", "66" },
                    new[] { "88 Fulton Street, shop", "Midtown", "Ravi Shah", "A pharmacy", "490", "$5,200", "ok", "84" },
                    new[] { "250 Park Avenue, floor 11", "Midtown", "Mia Lang", "Architect, Lang Studio", "455", "$8,100", "ok", "75" },
                    new[] { "250 Park Avenue, floor 12", "Midtown", "Luis Pena", "Software, Pena Labs", "430", "$8,100", "ok", "88" },
                    new[] { "6 Harlem Row, house", "Uptown", "Zoe Adler", "Teacher, Lincoln School", "380", "$2,700", "ok", "70" }
                };
                foreach (string[] extra in more)
                {
                    extra[3] = JobLine(t, extra[3]);
                }
                var all = new List<string[]>(units);
                all.AddRange(more);
                units = all.ToArray();
            }
            foreach (string[] u in units)
            {
                bool empty = u[6] == "empty";
                string chip = u[6] == "ok" ? W(t, "reallife_ui_estate_paid", "Paid") : u[6] == "late" ? W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "6")
                    : u[6] == "late14" ? W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "14") : u[6] == "notice" ? W(t, "reallife_ibox_chip_notice", "Notice given") : W(t, "reallife_estate_state_empty", "Empty {days} days").Replace("{days}", "12");
                UiRole tone = u[6] == "ok" ? UiRole.Ok : u[6] == "late14" ? UiRole.Danger : UiRole.Warn;
                rows.Add(EstateUnitRow(t, u[0], u[1], u[2], u[3], empty ? "-" : W(t, "reallife_estate_row_until", "Until day {day}").Replace("{day}", u[4]), u[5], chip, tone, int.Parse(u[7]),
                    u[0].Contains("shop") || u[0].Contains("bakery") ? UiIcon.Store : u[0].Contains("office") || u[0].Contains("suite") || u[0].Contains("floor") ? UiIcon.Briefcase : home, empty));
            }
        }
        m.Shape = EstateShape.Roll;
        m.HeadIcon = home;
        m.HeadTitle = W(t, "reallife_ui_estate_roll_title", "The rent roll");
        m.HeadSub = state == "roll_empty" ? null : state == "roll_first" ? W(t, "reallife_ui_estate_roll_sub_clear", "{units} unit(s) in {buildings} building(s), nothing waiting for you").Replace("{units}", "1").Replace("{buildings}", "1")
                : W(t, "reallife_ui_estate_roll_sub", "{units} unit(s) in {buildings} building(s), {waiting} waiting for you").Replace("{units}", state == "roll" ? "12" : "20").Replace("{buildings}", state == "roll" ? "9" : "13").Replace("{waiting}", "2");
        m.FigLabels = new[] { W(t, "reallife_ui_estate_fig_due", "Due"), W(t, "reallife_ui_estate_fig_collected", "Collected"), W(t, "reallife_ui_estate_fig_arrears", "In arrears"), W(t, "reallife_ui_estate_fig_empty", "Empty, asking") };
        m.FigValues = state == "roll_twenty" ? new[] { "$86,500", "$77,700", "$8,800", "$4,100" } : state == "roll" ? new[] { "$47,500", "$38,700", "$8,800", "$4,100" } : state == "roll_first" ? new[] { "$2,600", "$2,600", "$0", "$0" } : new[] { "$0", "$0", "$0", "$0" };
        m.FigInks = new[] { UiRole.None, UiRole.None, state.StartsWith("roll_t") || state == "roll" ? UiRole.DangerText : UiRole.None, state.StartsWith("roll_t") || state == "roll" ? UiRole.WarnText : UiRole.None };
        var table = new UiDataTableModel
        {
            Columns = EstateRollColumns(t), Rows = rows.ToArray(), RowCount = rows.Count,
            EmptyTitle = W(t, "reallife_estate_row_nothing_owned", "You own no building"), EmptyBody = W(t, "reallife_ui_estate_empty_roll_body", "The buildings you own appear here, a line for each unit, with its tenant and its rent."), EmptyIcon = home
        };
        if (rows.Count > 0)
        {
            table.TotalsLabel = W(t, "reallife_ui_estate_total_let", "{count} unit(s) let").Replace("{count}", state == "roll" ? "11" : state == "roll_twenty" ? "19" : "1");
            table.Totals = new[] { string.Empty, string.Empty, string.Empty, state == "roll_twenty" ? "$86,500" : state == "roll" ? "$47,500" : "$2,600", string.Empty, string.Empty };
        }
        m.Table = table;
        m.RowKeys = new string[rows.Count];
        for (int i = 0; i < m.RowKeys.Length; i++)
        {
            m.RowKeys[i] = "unt:p:" + i;
        }
        m.ListATitle = W(t, "reallife_ui_estate_due_repair", "Due for repair");
        m.ListAEmpty = W(t, "reallife_ui_estate_no_repairs", "Nothing is due for repair.");
        m.ListBTitle = W(t, "reallife_ui_estate_other_landlords", "Other landlords");
        m.ListBEmpty = W(t, "reallife_ui_estate_no_landlords", "No other landlord holds buildings in the city yet.");
        if (state != "roll_empty")
        {
            var months = new string[6];
            for (int i = 0; i < months.Length; i++)
            {
                months[i] = W(t, "reallife_ui_estate_month_n", "Month {month}").Replace("{month}", (9 + i).ToString());
            }
            m.Lower = EstateCharts.Line(W(t, "reallife_ui_estate_ring_chart", "The city's rents, a month at a time"), months, new[] { 2700f, 2740f, 2790f, 2820f, 2860f, 2900f }, W(t, "reallife_ui_estate_rv_mean_flat", "Mean rent, a flat"), EstateCharts.Dollars);
        }
        if (state == "roll_first")
        {
            m.ListA = new[]
            {
                new EstateRow { Icon = UiIcon.Wrench, Title = W(t, "reallife_ui_estate_service_title", "{fault}, {building}").Replace("{fault}", Capital(W(t, "reallife_estate_fault_1", "the heating"))).Replace("{building}", "12 W 39th Street, flat 2"), Chip = W(t, "reallife_ui_estate_day", "Day {day}").Replace("{day}", "431"), ChipTone = UiRole.Notice, Amount = "$320", Run = -1 }
            };
            m.ListB = new[]
            {
                new EstateRow { Face = "Gustavo Ruiz", Hue = UiColourHue("Gustavo Ruiz"), Title = W(t, "reallife_ui_estate_landlord_line", "{name}, {units} units, flats at the market rent of {rent}").Replace("{name}", "Gustavo Ruiz").Replace("{units}", "14").Replace("{rent}", "$2,930"), Amount = "+2.1%", AmountInk = UiRole.OkText, Run = -1 },
                new EstateRow { Face = "Halden Estates", Hue = UiColourHue("Halden Estates"), Title = W(t, "reallife_ui_estate_landlord_line", "{name}, {units} units, flats at the market rent of {rent}").Replace("{name}", "Halden Estates").Replace("{units}", "9").Replace("{rent}", "$2,640"), Amount = "+0.4%", AmountInk = UiRole.OkText, Run = -1 },
                new EstateRow { Face = "Okafor and Sons", Hue = UiColourHue("Okafor and Sons"), Title = W(t, "reallife_ui_estate_landlord_line", "{name}, {units} units, flats at the market rent of {rent}").Replace("{name}", "Okafor and Sons").Replace("{units}", "6").Replace("{rent}", "$2,410"), Amount = "-0.8%", AmountInk = UiRole.WarnText, Run = -1 }
            };
        }
        if (state == "roll" || state == "roll_twenty")
        {
            runs.Add(Yes);
            runs.Add(Yes);
            m.ListA = new[]
            {
                new EstateRow { Icon = UiIcon.Wrench, Title = W(t, "reallife_ui_estate_service_title", "{fault}, {building}").Replace("{fault}", Capital(W(t, "reallife_estate_fault_1", "the heating"))).Replace("{building}", "41 E 37th Street, flat 4A"), Chip = W(t, "reallife_ui_estate_day", "Day {day}").Replace("{day}", "417"), ChipTone = UiRole.Notice, Amount = "$340", Run = -1 },
                new EstateRow { Icon = UiIcon.Wrench, Title = W(t, "reallife_ui_estate_service_title", "{fault}, {building}").Replace("{fault}", Capital(W(t, "reallife_estate_fault_4", "a lock"))).Replace("{building}", "233 W 39th Street"), Chip = W(t, "reallife_ibox_chip_booked", "Booked"), ChipTone = UiRole.Ok, Amount = "$410", Run = -1 }
            };
            m.ListB = new[]
            {
                new EstateRow { Face = "Gustavo Ruiz", Hue = UiColourHue("Gustavo Ruiz"), Title = W(t, "reallife_ui_estate_landlord_line", "{name}, {units} units, flats average {rent}").Replace("{name}", "Gustavo Ruiz").Replace("{units}", "14").Replace("{rent}", "$2,930"), Amount = "+2.1%", AmountInk = UiRole.OkText, Run = -1 },
                new EstateRow { Face = "Halden Estates", Hue = UiColourHue("Halden Estates"), Title = W(t, "reallife_ui_estate_landlord_line", "{name}, {units} units, flats average {rent}").Replace("{name}", "Halden Estates").Replace("{units}", "9").Replace("{rent}", "$2,640"), Amount = "+0.4%", AmountInk = UiRole.OkText, Run = -1 }
            };
        }
    }

    private static void Unit(UiWords t, EstateModel m, List<Func<bool>> runs, string state)
    {
        m.Shape = EstateShape.Unit;
        m.Back = W(t, "reallife_co_back", "Back");
        var p = new EstateUnitPage { Title = "41 E 37th Street, flat 4A", District = "Murray Hill", Locate = W(t, "reallife_ui_estate_locate", "Locate"), Rent = "$2,600", RentPer = W(t, "reallife_ui_estate_a_month", "a month") };
        runs.Add(Yes);
        p.LocateRun = runs.Count - 1;
        p.NextLabel = W(t, "reallife_ui_estate_next_inspection", "Next inspection");
        p.NextValue = W(t, "reallife_ui_estate_in_days", "in {days} days").Replace("{days}", "9");
        p.NextFill = 70;
        p.FactLabels = new[] { W(t, "reallife_ui_estate_fact_bedrooms", "Bedrooms"), W(t, "reallife_ui_estate_fact_area", "Area"), W(t, "reallife_ui_estate_lease_ends", "Lease ends"), W(t, "reallife_ui_estate_deposit_held", "Deposit held"),
            W(t, "reallife_ui_estate_last_inspected", "Last inspected"), W(t, "reallife_ui_estate_fact_market", "On the market, a month") };
        p.FactValues = new[] { "2", W(t, "reallife_ui_estate_sqm", "{area} m2").Replace("{area}", "78"), W(t, "reallife_ui_estate_day", "Day {day}").Replace("{day}", "462"), "$5,200", W(t, "reallife_ui_estate_day", "Day {day}").Replace("{day}", "393"), "$2,750" };
        p.BuildingLink = W(t, "reallife_ui_estate_the_building", "The building");
        runs.Add(Yes);
        p.BuildingRun = runs.Count - 1;
        p.OthersTitle = W(t, "reallife_ui_estate_other_units", "Your other units here");
        p.OthersCount = "2";
        runs.Add(Yes);
        runs.Add(Yes);
        p.Others = new[]
        {
            new EstateRow { Face = "Priya Nair", Hue = UiColourHue("Priya Nair"), Title = "Priya Nair", Sub = "41 E 37th Street, flat 3A", Chip = W(t, "reallife_ui_estate_paid", "Paid"), ChipTone = UiRole.Ok, Amount = "$2,600", Run = runs.Count - 2 },
            new EstateRow { Face = "Daniel Ortiz", Hue = UiColourHue("Daniel Ortiz"), Title = "Daniel Ortiz", Sub = "41 E 37th Street, flat 3B", Chip = W(t, "reallife_ui_estate_paid", "Paid"), ChipTone = UiRole.Ok, Amount = "$3,300", Run = runs.Count - 1 }
        };
        if (state == "unit_empty")
        {
            // The one empty unit of the roll: the shop of 1 Dock Street, with its own tenancy history, its works and the people who asked for it.
            p.Title = "1 Dock Street, shop unit";
            p.District = "Uptown";
            p.Icon = UiIcon.Store;
            p.Rent = "$4,100";
            p.FactLabels = new[] { p.FactLabels[1], p.FactLabels[4] };
            p.FactValues = new[] { p.FactValues[1], p.FactValues[4] };
            p.Others = new EstateRow[0];
            p.OthersCount = null;
            p.RecordTitle = W(t, "reallife_ui_estate_record_last_tenant", "The last tenant's rent record");
            p.RecordNote = Fx(W(t, "reallife_ui_estate_last_months", "last {count} months"), "count", "5");
            string[] due = { "376", "346", "316", "286", "256" };
            p.Record = new EstateRecordRow[due.Length];
            for (int i = 0; i < due.Length; i++)
            {
                bool late = i == 3;
                p.Record[i] = new EstateRecordRow
                {
                    Due = Fx(W(t, "reallife_ui_estate_due_day", "Due day {day}"), "day", due[i]), What = Fx(W(t, "reallife_ui_estate_paid_on", "Paid on day {day}"), "day", (int.Parse(due[i]) + (late ? 4 : 0)).ToString()),
                    Chip = late ? W(t, "reallife_ui_estate_paid_late", "Paid late") : W(t, "reallife_ui_estate_paid", "Paid"), ChipTone = late ? UiRole.Warn : UiRole.Ok, Amount = "$4,100"
                };
            }
            p.YearTitle = W(t, "reallife_ui_estate_year_title", "This unit over the tax year");
            p.YearLabels = new[] { W(t, "reallife_ui_estate_year_rent", "Rent due, this year"), W(t, "reallife_ui_estate_year_works", "Works and repairs") };
            p.YearValues = new[] { "$36,900", "-$1,100" };
            p.YearKeptLabel = W(t, "reallife_ui_estate_kept", "Kept");
            p.YearKept = "$35,800";
            p.WorksTitle = W(t, "reallife_ui_estate_works_here", "Works on this unit");
            p.WorksCount = "2";
            runs.Add(Yes);
            runs.Add(Yes);
            p.Works = new[]
            {
                new EstateRow { Icon = UiIcon.Wrench, Title = ServiceTitle(t, 6, "the fittings"), Sub = Fx(W(t, "reallife_ui_estate_booked_with", "Booked with {who}"), "who", "Fenwick Fittings"), Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "431"), ChipTone = UiRole.Notice, Amount = "$220", Run = runs.Count - 2 },
                new EstateRow { Icon = UiIcon.Eye, Title = W(t, "reallife_ui_estate_next_inspection", "Next inspection"), Sub = W(t, "reallife_ui_estate_last_passed", "The last one passed"), Chip = Fx(W(t, "reallife_ui_estate_in_days", "in {days} days"), "days", "9"), ChipTone = UiRole.Notice, Run = runs.Count - 1 }
            };
            var lines = new List<UiFactLine>
            {
                EstateStep(runs, W(t, "reallife_estate_ten_asking", "Asking rent"), "105%", W(t, "reallife_estate_ten_asking_what", "Asks {amount} a month, and about {chance} of the days an applicant comes.").Replace("{amount}", "$137").Replace("{chance}", "12%")),
                EstateSwitch(runs, W(t, "reallife_estate_ten_to_let", "Look for applicants"), true)
            };
            p.Strip = new[]
            {
                Fig(W(t, "reallife_ui_estate_fig_asking_day", "Asking, a day"), "$137"), Fig(W(t, "reallife_ui_estate_fig_market_day", "Market rent, a day"), "$131"), Fig(W(t, "reallife_ui_estate_fig_days_empty", "Days empty"), "12")
            };
            p.ShareTitle = W(t, "reallife_ui_estate_sh_weigh", "What a tenant would weigh");
            p.ShareLabels = new[] { W(t, "reallife_ui_estate_m_repairs", "Repairs"), W(t, "reallife_ui_estate_m_value", "Value for rent"), W(t, "reallife_ui_estate_m_condition", "Condition") };
            p.ShareValues = new[] { 70, 58, 80 };
            p.ApplicantsTitle = W(t, "reallife_ui_estate_applicants_title", "Applicants");
            p.ApplicantsCount = "2";
            runs.Add(Yes);
            runs.Add(Yes);
            p.Applicants = new[]
            {
                new EstateRow { Face = "Kofi Mensah", Hue = UiColourHue("Kofi Mensah"), Title = "Kofi Mensah", Sub = W(t, "reallife_estate_row_until", "Until day {day}").Replace("{day}", "421"), Chip = W(t, "reallife_estate_record_good", "Good"), ChipTone = UiRole.Ok, Amount = "$133", Run = runs.Count - 2 },
                new EstateRow { Face = "Ravi Shah", Hue = UiColourHue("Ravi Shah"), Title = "Ravi Shah", Sub = W(t, "reallife_estate_row_until", "Until day {day}").Replace("{day}", "424"), Chip = W(t, "reallife_estate_record_fair", "Fair"), ChipTone = UiRole.Warn, Amount = "$139", Run = runs.Count - 1 }
            };
            p.Empty = new UiFactCardModel { Title = W(t, "reallife_ui_estate_empty_title", "Empty for {days} days").Replace("{days}", "12"), Chip = W(t, "reallife_ibox_chip_empty", "Empty"), ChipTone = UiRole.Warn, Lines = lines.ToArray() };
            m.Unit = p;
            return;
        }
        p.TenantTitle = W(t, "reallife_ui_estate_the_tenant", "The tenant");
        p.TenantName = "Sofia Marchetti";
        p.TenantHue = UiColourHue("Sofia Marchetti");
        p.TenantRing = UiRole.Warn;
        p.TenantLine = JobLine(t, "Nurse, Murray Hill Clinic") + " · 34 · " + W(t, "reallife_ui_estate_here_since", "here since day {day}").Replace("{day}", "58");
        p.TenantChip = W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "6");
        p.TenantOpen = W(t, "reallife_ui_estate_open_page", "Open their page");
        runs.Add(Yes);
        p.TenantOpenRun = runs.Count - 1;
        p.MeterLabels = new[] { W(t, "reallife_ui_estate_m_satisfaction", "Satisfaction"), W(t, "reallife_ui_estate_m_repairs", "Repairs"), W(t, "reallife_ui_estate_m_value", "Value for rent"), W(t, "reallife_ui_estate_m_condition", "Condition") };
        p.MeterValues = new[] { 51, 39, 62, 80 };
        p.YearTitle = W(t, "reallife_ui_estate_year_title", "This unit over the tax year");
        p.YearLabels = new[] { W(t, "reallife_ui_estate_year_rent", "Rent due, this year"), W(t, "reallife_ui_estate_year_works", "Works and repairs") };
        p.YearValues = new[] { "$31,200", "-$4,200" };
        p.YearKeptLabel = W(t, "reallife_ui_estate_kept", "Kept");
        p.YearKept = "$27,000";
        p.RecordTitle = W(t, "reallife_ui_estate_record_title", "Rent record");
        p.RecordNote = W(t, "reallife_ui_estate_last_months", "last {count} months").Replace("{count}", "5");
        var rec = new List<EstateRecordRow>
        {
            new EstateRecordRow { Due = W(t, "reallife_ui_estate_due_day", "Due day {day}").Replace("{day}", "406"), What = W(t, "reallife_ui_estate_not_paid_yet", "Not paid yet"), Chip = W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "6"), ChipTone = UiRole.Warn, Amount = "$2,600" }
        };
        string[] dues = { "376", "346", "316", "286" };
        for (int i = 0; i < dues.Length; i++)
        {
            rec.Add(new EstateRecordRow { Due = W(t, "reallife_ui_estate_due_day", "Due day {day}").Replace("{day}", dues[i]), What = W(t, "reallife_ui_estate_paid_on", "Paid on day {day}").Replace("{day}", dues[i]), Chip = W(t, "reallife_ui_estate_paid", "Paid"), ChipTone = UiRole.Ok, Amount = "$2,600" });
        }
        p.Record = rec.ToArray();
        p.WorksTitle = W(t, "reallife_ui_estate_works_here", "Works on this unit");
        runs.Add(Yes);
        runs.Add(Yes);
        p.Works = new[]
        {
            new EstateRow { Icon = UiIcon.Wrench, Title = ServiceTitle(t, 1, "the heating"), Sub = W(t, "reallife_ui_estate_booked_with", "Booked with {who}").Replace("{who}", "Okonkwo Heating"), Chip = W(t, "reallife_ui_estate_day", "Day {day}").Replace("{day}", "417"), ChipTone = UiRole.Notice, Amount = "$340", Run = -1 },
            new EstateRow { Icon = UiIcon.Wrench, Title = W(t, "reallife_estate_improve_boiler", "a new boiler"), Sub = W(t, "reallife_ui_estate_asked_by_tenant", "Asked for by the tenant, you can say no"), Chip = W(t, "reallife_ui_estate_waiting_you", "Waiting for you"), ChipTone = UiRole.Warn, Amount = "$2,900", Run = -1 }
        };
        p.WorksCount = p.Works.Length.ToString();
        if (state == "unit_request")
        {
            p.NeedKicker = W(t, "reallife_ui_estate_needs_you", "Needs you, {what}").Replace("{what}", W(t, "reallife_ui_estate_need_request", "a request"));
            p.NeedHead = W(t, "reallife_ui_estate_improve_head", "{name} asks for {what}, {amount}.").Replace("{name}", "Sofia").Replace("{what}", W(t, "reallife_estate_improve_boiler", "a new boiler")).Replace("{amount}", "$2,900");
            p.NeedBody = W(t, "reallife_ui_estate_improve_body", "You can say no, but refusing costs satisfaction and makes them likelier to leave. Refused so far in this lease: {refusals}.").Replace("{refusals}", "1");
            p.NeedActions = new[]
            {
                EstateButton(runs, W(t, "reallife_ui_estate_make_it", "Make it"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt),
                EstateButton(runs, W(t, "reallife_estate_btn_refuse", "Refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option)
            };
        }
        else
        {
            p.NeedKicker = W(t, "reallife_ui_estate_needs_you", "Needs you, {what}").Replace("{what}", W(t, "reallife_ui_estate_need_rent", "rent late"));
            p.NeedHead = W(t, "reallife_ui_estate_late_head", "{name} is {days} days late with {amount}.").Replace("{name}", "Sofia").Replace("{days}", "6").Replace("{amount}", "$2,600");
            p.NeedBody = W(t, "reallife_estate_ten_arrears", "{amount} is owed, {days} days of it. They catch up on about {back} of the days.").Replace("{amount}", "$2,600").Replace("{days}", "6").Replace("{back}", "40%");
            p.NeedActions = new[]
            {
                EstateButton(runs, W(t, "reallife_estate_btn_remind", "Send a reminder"), UiButtonKind.Primary, UiIcon.None, UiAction.Alt),
                EstateButton(runs, W(t, "reallife_estate_btn_plan", "Offer a payment plan"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option)
            };
            p.NeedLink = W(t, "reallife_estate_btn_notice", "Give notice");
            runs.Add(Yes);
            p.NeedLinkRun = runs.Count - 1;
        }
        m.Unit = p;
    }
}
