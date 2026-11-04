using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// The Property app's tabs and detail pages as samples (the roll and the unit page are in UiSamples.Estate.cs).
internal static partial class UiSamples
{
    private static string Fx(string text, params string[] pairs)
    {
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
        }
        return text;
    }

        // A kind of building and its district in the language shown: the district is the game's own name, the kind is the locale's.
    private static string UseIn(UiWords t, string use, string district) => W(t, "reallife_estate_use_" + use.ToLowerInvariant(), use) + ", " + district;

    // A tenant's work as the people pool gives it in the language shown: "Nurse, Murray Hill Clinic", "A bakery". Names of places stay as they are.
    private static string JobLine(UiWords t, string line)
    {
        string[][] shops = { new[] { "A tailor's shop", "tailor" }, new[] { "A bakery", "bakery" }, new[] { "An import firm", "import" }, new[] { "A florist", "florist" }, new[] { "A pharmacy", "pharmacy" } };
        foreach (string[] shop in shops)
        {
            if (line == shop[0])
            {
                return W(t, "reallife_ui_estate_job_" + shop[1], shop[0]);
            }
        }
        int comma = line.IndexOf(", ", StringComparison.Ordinal);
        if (comma < 0)
        {
            return line;
        }
        string role = line.Substring(0, comma), word = role == "Software" ? "developer" : role.ToLowerInvariant();
        return W(t, "reallife_ui_estate_job_" + word, role == "Software" ? "Developer" : role) + line.Substring(comma);
    }

    // The name of a service the way the language words it, with its first letter a capital.
    private static string ServiceTitle(UiWords t, int fault, string english) => Capital(Fx(W(t, "reallife_ui_estate_service_name", "{fault} service"), "fault", W(t, "reallife_estate_fault_" + fault, english)));

    private static string HoursShort(UiWords t, string hours) => Fx(W(t, "reallife_ui_dealer_n_hours_short", "{hours} h"), "hours", hours.Split(' ')[0]);

    private static UiFigureModel Fig(string label, string value) => new UiFigureModel { Label = label, Value = value };

    private static void TableOf(EstateModel m, UiIcon icon, string title, string sub, UiDataColumn[] cols, List<UiDataRow> rows, UiFactCardModel side, string emptyTitle, string emptyBody)
    {
        m.Shape = EstateShape.Table;
        m.HeadIcon = icon;
        m.HeadTitle = string.IsNullOrEmpty(sub) ? title : sub;
        m.HeadSub = null;
        m.Table = new UiDataTableModel { Columns = cols, Rows = rows.ToArray(), RowCount = rows.Count, EmptyTitle = emptyTitle, EmptyBody = emptyBody, EmptyIcon = icon };
        m.RowKeys = new string[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            m.RowKeys[i] = "row:" + i;
        }
        m.Side = side;
        if (side != null)
        {
            side.Title = null;
        }
    }

    private static UiDataRow Row(string[] cells, int[] values, UiRole[] tones, string[] subs, float hue = 0f)
    {
        return new UiDataRow { Cells = cells, Values = values, Tones = tones, Subs = subs, Hue = hue, Ring = UiRole.None };
    }

    // The roll's units are the one list the roll, the tenants and the works all stand on: one tenant to a unit, one glyph to a use.
    private static UiIcon UnitIcon(string unit)
    {
        return unit.Contains("shop") ? UiIcon.Store : unit.Contains("office") || unit.Contains("suite") || unit.Contains("floor") ? UiIcon.Briefcase : UiIcon.Home;
    }

    private static void Tenants(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>();
        string late6 = W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "6"), late14 = W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "14");
        var needs = new List<EstateRow>();
        foreach (string[] u in RollUnits(t))
        {
            if (u[6] == "empty")
            {
                continue;
            }
            bool good = u[6] == "ok";
            string chip = u[6] == "ok" ? W(t, "reallife_estate_state_paying", "Paying") : u[6] == "late" ? late6 : u[6] == "late14" ? late14 : W(t, "reallife_ibox_chip_notice", "Notice given");
            UiRole tone = u[6] == "ok" ? UiRole.Ok : u[6] == "late14" ? UiRole.Danger : UiRole.Warn;
            rows.Add(Row(new[] { u[0], u[2], u[5], good ? W(t, "reallife_estate_record_good", "Good") : W(t, "reallife_estate_record_fair", "Fair"), chip, u[7] },
                new[] { (int)UnitIcon(u[0]), 0, 0, good ? 85 : 55, 0, int.Parse(u[7]) },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone, UiRole.None }, new[] { u[1], u[3], null, null, null, null }, UiColourHue(u[2])));
            if (u[6] == "late" || u[6] == "late14")
            {
                runs.Add(Yes);
                int open = runs.Count - 1;
                runs.Add(Yes);
                needs.Add(new EstateRow
                {
                    Face = u[2], Hue = UiColourHue(u[2]), Title = u[2], Sub = u[0], Chip = u[6] == "late" ? late6 : late14, ChipTone = tone,
                    Button = W(t, "reallife_estate_btn_remind", "Send a reminder"), ButtonRun = runs.Count - 1, Run = open
                });
            }
        }
        // The empty unit and the one who asked for it: an applicant is not a tenant, so no record or satisfaction is drawn for the row.
        rows.Add(Row(new[] { "1 Dock Street, shop unit", W(t, "reallife_estate_row_nobody", "Nobody"), string.Empty, string.Empty, Fx(W(t, "reallife_estate_state_empty", "Empty {days} days"), "days", "12"), string.Empty },
            new[] { (int)UiIcon.Store, UiDataTable.NoFace, 0, -1, 0, -1 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Warn, UiRole.None },
            new[] { "Uptown", W(t, "reallife_ui_estate_asking_note", "Looking for a tenant"), null, null, null, null }));
        rows.Add(Row(new[] { "1 Dock Street, shop unit", "Kofi Mensah", "$4,100", W(t, "reallife_estate_record_good", "Good"), W(t, "reallife_estate_filter_applicants", "Applicants"), string.Empty },
            new[] { (int)UiIcon.Store, 0, 0, 80, 0, -1 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok, UiRole.None },
            new[] { "Uptown", Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "424"), null, null, null, null }, UiColourHue("Kofi Mensah")));
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_unit", "Unit"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_tenant", "Tenant"), Kind = UiCellKind.Person, Width = 264f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rent_month", "Rent a month"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_record", "Record"), Kind = UiCellKind.Meter, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 144f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_satisfaction", "Satisfaction"), Kind = UiCellKind.Meter, Width = 128f }
        };
        TableOf(m, UiIcon.People, W(t, "reallife_estate_tab_tenants", "Tenants"), Fx(W(t, "reallife_ui_estate_tenants_sub", "{let} let, {empty} empty"), "let", "11", "empty", "1"), cols, rows, null,
            W(t, "reallife_estate_row_none_managed", "You let none yourself"), W(t, "reallife_ui_estate_empty_tenants_body", "The units you let yourself appear here with their tenants and the people who apply."));
        m.FigLabels = new[] { W(t, "reallife_ui_estate_fig_let", "Let"), W(t, "reallife_ui_estate_fig_empty_units", "Empty"), W(t, "reallife_ui_estate_fig_rent_owed", "Rent owed to you"), W(t, "reallife_ui_estate_fig_mean_happy", "Mean satisfaction") };
        m.FigValues = new[] { "11", "1", "$8,800", "63%" };
        m.FigInks = new[] { UiRole.None, UiRole.WarnText, UiRole.DangerText, UiRole.None };
        // The late payers, with the reminder to send: what needs the landlord now, from the same units as the roll's red and amber chips.
        m.FootFirst = true;
        m.RelatedTitle = W(t, "reallife_ui_estate_needs_you_list", "Needs you");
        m.RelatedCount = needs.Count.ToString();
        m.Related = needs.ToArray();
    }

    private static void Works(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>();
        var locate = new List<int>();
        string open = W(t, "reallife_ibox_chip_open", "Open"), booked = W(t, "reallife_ibox_chip_booked", "Booked");
        // A call: the fault, its state, its cost and the day it was reported; a building: its level and condition, what it would take to put right.
        string[][] calls =
        {
            new[] { "41 E 37th Street, flat 4A", "Murray Hill", "1", "open", "$340", "407" },
            new[] { "233 W 39th Street, unit 1", "Garment District", "4", "booked", "$410", "406" },
            new[] { "88 W 46th Street, flat 2", "Hell's Kitchen", "4", "booked", "$280", "408" },
            new[] { "75 W 125th Street, house", "Uptown", "1", "open", "$520", "409" }
        };
        foreach (string[] c in calls)
        {
            runs.Add(Yes);
            locate.Add(runs.Count - 1);
            string fault = Capital(W(t, "reallife_estate_fault_" + c[2], c[2] == "1" ? "the heating" : "a lock"));
            rows.Add(Row(new[] { c[0], fault, string.Empty, c[3] == "open" ? open : booked, c[4], Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", c[5]), string.Empty },
                new[] { (int)UiIcon.Wrench, 0, -1, 0, 0, 0, (int)UiIcon.Locate }, new[] { UiRole.None, UiRole.None, UiRole.None, c[3] == "open" ? UiRole.Warn : UiRole.Ok, UiRole.None, UiRole.None, UiRole.None },
                new[] { c[1], null, null, null, null, null, null }));
        }
        string[][] buildings =
        {
            new[] { "41 E 37th Street", UseIn(t, "Homes", "Murray Hill"), "1", "80", "sound", "$1,240", "home" },
            new[] { "520 8th Avenue", UseIn(t, "Offices", "Midtown"), "0", "32", "unfit", "$5,880", "office" },
            new[] { "12 W 39th Street", UseIn(t, "Shops", "Garment District"), "1", "64", "sound", "$2,060", "shop" },
            new[] { "17 E 41st Street", UseIn(t, "Offices", "Midtown"), "2", "71", "sound", "$1,780", "office" },
            new[] { "88 W 46th Street", UseIn(t, "Homes", "Hell's Kitchen"), "1", "76", "sound", "$1,420", "home" },
            new[] { "75 W 125th Street", UseIn(t, "Homes", "Uptown"), "0", "58", "sound", "$2,210", "home" }
        };
        foreach (string[] b in buildings)
        {
            runs.Add(Yes);
            locate.Add(runs.Count - 1);
            bool unfit = b[4] == "unfit";
            rows.Add(Row(new[] { b[0], Fx(W(t, "reallife_ui_estate_level_n", "Level {level}"), "level", b[2]), b[3], unfit ? W(t, "reallife_ui_estate_unfit", "Unfit") : W(t, "reallife_ui_estate_sound", "Sound"), b[5], string.Empty, string.Empty },
                new[] { (int)(b[6] == "home" ? UiIcon.Home : b[6] == "shop" ? UiIcon.Store : UiIcon.Briefcase), 0, int.Parse(b[3]), 0, 0, 0, (int)UiIcon.Locate },
                new[] { UiRole.None, UiRole.None, UiRole.None, unfit ? UiRole.Danger : UiRole.Ok, UiRole.None, UiRole.None, UiRole.None }, new[] { b[1], null, null, null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_building", "Building"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_what", "What"), Kind = UiCellKind.Text, Width = 176f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_condition", "Condition"), Kind = UiCellKind.Meter, Width = 136f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 144f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_cost", "Cost"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_day", "Day"), Kind = UiCellKind.Date, Width = 120f },
            new UiDataColumn { Title = string.Empty, Kind = UiCellKind.Action, Width = 56f }
        };
        TableOf(m, UiIcon.Wrench, W(t, "reallife_estate_tab_works", "Works"), Fx(W(t, "reallife_ui_estate_works_sub", "{count} to look at"), "count", rows.Count.ToString()), cols, rows, null, W(t, "reallife_estate_row_nothing_owned", "You own no building"),
            W(t, "reallife_ui_estate_empty_works_body", "Repair calls and works on your buildings appear here."));
        m.RowRuns = locate.ToArray();
        m.FigLabels = new[] { W(t, "reallife_ui_estate_fig_open_calls", "Open calls"), W(t, "reallife_ui_estate_fig_booked", "Booked"), W(t, "reallife_ui_estate_fig_unfit", "Unfit to let"), W(t, "reallife_ui_estate_fig_calls_cost", "Calls cost") };
        m.FigValues = new[] { "2", "2", "1", "$1,550" };
        m.FigInks = new[] { UiRole.WarnText, UiRole.None, UiRole.DangerText, UiRole.None };
        m.RelatedTitle = W(t, "reallife_ui_estate_booked_visits", "Booked servicing");
        m.RelatedCount = "3";
        m.Related = new[]
        {
            new EstateRow { Icon = UiIcon.Calendar, Title = ServiceTitle(t, 6, "the fittings"), Sub = "75 W 125th Street, Fenwick Fittings", Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "431"), ChipTone = UiRole.Notice, Amount = "$220", Run = -1 },
            new EstateRow { Icon = UiIcon.Calendar, Title = ServiceTitle(t, 1, "the heating"), Sub = "41 E 37th Street, Okonkwo Heating", Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "417"), ChipTone = UiRole.Notice, Amount = "$340", Run = -1 },
            new EstateRow { Icon = UiIcon.Calendar, Title = ServiceTitle(t, 4, "a lock"), Sub = "12 W 39th Street, Hartley Locks", Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "424"), ChipTone = UiRole.Notice, Amount = "$280", Run = -1 }
        };
        m.Related2Title = W(t, "reallife_ui_estate_inspections", "The city's inspections");
        m.Related2Count = "3";
        m.Related2 = new[]
        {
            new EstateRow { Icon = UiIcon.Eye, Title = "41 E 37th Street", Sub = W(t, "reallife_ui_estate_last_passed", "The last one passed"), Chip = Fx(W(t, "reallife_ui_estate_in_days", "in {days} days"), "days", "9"), ChipTone = UiRole.Notice, Run = -1 },
            new EstateRow { Icon = UiIcon.Eye, Title = "12 W 39th Street", Sub = W(t, "reallife_ui_estate_last_passed", "The last one passed"), Chip = Fx(W(t, "reallife_ui_estate_in_days", "in {days} days"), "days", "31"), ChipTone = UiRole.Notice, Run = -1 },
            new EstateRow { Icon = UiIcon.Eye, Title = "520 8th Avenue", Sub = W(t, "reallife_ui_estate_last_failed", "The last one failed"), Chip = Fx(W(t, "reallife_ui_estate_in_days", "in {days} days"), "days", "14"), ChipTone = UiRole.Warn, Run = -1 }
        };
    }

    private static void Market(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>();
        var locate = new List<int>();
        string[][] listings =
        {
            new[] { "301 W 54th Street", UseIn(t, "Homes", "Hell's Kitchen"), "$1,840,000", "80", "$7,420", "4.1%", "h" },
            new[] { "14 Pier Street", UseIn(t, "Shops", "Garment District"), "$3,260,000", "64", "$15,300", "4.7%", "s" },
            new[] { "99 Broadway", UseIn(t, "Offices", "Midtown"), "$12,400,000", "91", "$61,900", "5.0%", "o" },
            new[] { "6 Harlem Row", UseIn(t, "Homes", "Uptown"), "$980,000", "42", "$3,010", "3.1%", "h" },
            new[] { "412 10th Avenue", UseIn(t, "Homes", "Hell's Kitchen"), "$2,150,000", "73", "$8,600", "4.8%", "h" },
            new[] { "88 Fulton Street", UseIn(t, "Shops", "Midtown"), "$4,700,000", "58", "$19,400", "4.9%", "s" },
            new[] { "250 Park Avenue, floor 11", UseIn(t, "Offices", "Midtown"), "$9,300,000", "86", "$44,200", "5.7%", "o" },
            new[] { "17 Mercer Street", UseIn(t, "Homes", "Murray Hill"), "$1,420,000", "67", "$5,700", "4.2%", "h" },
            new[] { "3 Canal Row", UseIn(t, "Shops", "Garment District"), "$2,880,000", "49", "$11,900", "5.0%", "s" },
            new[] { "60 W 57th Street", UseIn(t, "Offices", "Midtown"), "$15,800,000", "93", "$72,500", "5.5%", "o" }
        };
        foreach (string[] l in listings)
        {
            runs.Add(Yes);
            locate.Add(runs.Count - 1);
            rows.Add(Row(new[] { l[0], l[2], l[3], l[4], l[5], string.Empty }, new[] { (int)(l[6] == "h" ? UiIcon.Home : l[6] == "s" ? UiIcon.Store : UiIcon.Briefcase), 0, int.Parse(l[3]), 0, 0, (int)UiIcon.Locate },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { l[1], null, null, null, null, null }));
        }
        runs.Add(Yes);
        locate.Add(runs.Count - 1);
        rows.Add(Row(new[] { "Pier House Hotel", "$8,900,000", string.Empty, string.Empty, string.Empty, string.Empty }, new[] { (int)UiIcon.Building, 0, -1, 0, 0, (int)UiIcon.Locate },
            new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_row_hotel", "Hotel") + ", Midtown, " + W(t, "reallife_estate_row_going_concern", "a going concern"), null, null, null, null, null }));
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_building", "Building"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_price", "Price"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_condition", "Condition"), Kind = UiCellKind.Meter, Width = 136f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rent_month", "Rent a month"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_yield", "Yield"), Kind = UiCellKind.Figure, Width = 88f },
            new UiDataColumn { Title = string.Empty, Kind = UiCellKind.Action, Width = 56f }
        };
        TableOf(m, UiIcon.Tag, W(t, "reallife_estate_tab_market", "Market"), Fx(W(t, "reallife_ui_estate_market_sub", "{count} listings on the market"), "count", "11"), cols, rows, null,
            W(t, "reallife_estate_row_no_market", "Nothing is for sale"), W(t, "reallife_ui_estate_empty_market_body", "No building is for sale just now. New listings come with the game's market."));
        m.RowRuns = locate.ToArray();
        m.TableLink = W(t, "reallife_ui_estate_view_map", "View on the map");
        runs.Add(Yes);
        m.TableLinkRun = runs.Count - 1;
        m.FigLabels = new[] { W(t, "reallife_ui_estate_fig_market_level", "Property market"), W(t, "reallife_ui_estate_fig_prices_since", "Prices since the start"), W(t, "reallife_ui_estate_fig_mean_yield", "Mean yield"), W(t, "reallife_ui_estate_fig_cycle", "The cycle") };
        // What the districts rent for per m2: the same read the rents map ranks, as meters with their figure.
        m.ShareTitle = W(t, "reallife_ui_estate_sh_district_rents", "What the districts rent for, per m2");
        m.ShareLabels = new[] { "Midtown", "Garment District", "Murray Hill", "Hell's Kitchen", "Uptown" };
        m.ShareValues = new[] { 100, 90, 81, 72, 50 };
        m.ShareTexts = new[] { "$38.50", "$34.80", "$31.20", "$27.90", "$19.40" };
        m.FigValues = new[] { "103%", "112%", "4.8%", W(t, "reallife_estate_cycle_steady", "Steady") };
    }

    private static void Rents(UiWords t, EstateModel m, bool fresh)
    {
        m.Shape = EstateShape.Rents;
        string notYet = W(t, "reallife_ui_estate_not_yet", "Not yet");
        var v = new UiInfoViewModel
        {
            Title = W(t, "reallife_ui_estate_rv_title", "The city's market rents"), Icon = UiIcon.Key, Views = new[] { UiIcon.Home, UiIcon.Store, UiIcon.Briefcase }, View = 0, HeadLabel = W(t, "reallife_ui_estate_rv_rent", "Rent"), HeadValue = W(t, "reallife_ui_estate_rv_per_month", "per month, all units"),
            Rows = new[]
            {
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_mean_flat", "Mean market rent, a flat"), Value = fresh ? notYet : "$2,860" },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_flat_sqm", "Mean rent per m2, flats"), Value = fresh ? notYet : "$34.20" },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_shop_sqm", "Shop rent per m2"), Value = fresh ? notYet : "$61.80" },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_office_sqm", "Office rent per m2"), Value = fresh ? notYet : "$48.10" },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_empty", "Units standing empty"), Value = fresh ? notYet : "4.8%", Ink = fresh ? UiRole.None : UiRole.WarnText },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_month", "Rents this month"), Value = fresh ? W(t, "reallife_ui_estate_rv_month_wait", "Not yet, after two months") : "+1.2%", Ink = fresh ? UiRole.None : UiRole.OkText },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_your_let", "Your units, let"), Value = Fx(W(t, "reallife_ui_estate_of", "{value} of {most}"), "value", "11", "most", "12") },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_your_mean", "Your mean rent"), Value = "$3,960" }
            },
            ChartTitle = W(t, "reallife_ui_estate_rv_for_sale", "For sale by kind"), ChartNote = Fx(W(t, "reallife_ui_estate_rv_listings", "{count} listings"), "count", "11"),
            ChartLabels = new[] { W(t, "reallife_estate_use_homes", "Homes"), W(t, "reallife_estate_use_shops", "Shops"), W(t, "reallife_estate_use_offices", "Offices"), W(t, "reallife_estate_row_hotel", "Hotel") },
            ChartValues = new[] { "36%", "27%", "27%", "9%" }, ChartShares = new[] { 0.36f, 0.27f, 0.27f, 0.09f },
            LegendTitle = W(t, "reallife_ui_estate_rv_legend", "Map legend"), ScaleLabel = W(t, "reallife_ui_estate_rv_scale", "Rent per m2"), ScaleLow = "$19.40", ScaleHigh = "$38.50",
            RankTitle = W(t, "reallife_ui_estate_rv_districts", "Districts"), RankNote = W(t, "reallife_ui_estate_rv_rank_note", "rent per m2, dearest first"),
            RankLabels = new[] { "Midtown", "Garment District", "Murray Hill", "Hell's Kitchen", "Uptown" }, RankValues = new[] { "$38.50", "$34.80", "$31.20", "$27.90", "$19.40" }, RankFills = new[] { 100, 90, 81, 72, 50 }, RankSteps = new[] { 4, 4, 3, 2, 0 }
        };
        if (fresh)
        {
            v.Rows = new[] { v.Rows[0], v.Rows[6], v.Rows[7] };   // one "Not yet" for the city's rents; the player's own figures stand
            v.ChartLabels = new string[0];
            v.ChartValues = new string[0];
            v.ChartShares = new float[0];
            v.ChartNote = null;
            v.RankLabels = new string[0];
            v.RankValues = new string[0];
            v.RankFills = new int[0];
            v.ScaleLow = string.Empty;
            v.ScaleHigh = string.Empty;
        }
        v.Prompts = EstatePrompts(t);
        v.PromptCount = v.Prompts.Length;
        m.Rents = v;
    }

    private static void Agency(UiWords t, EstateModel m, List<Func<bool>> runs, bool closed)
    {
        if (closed)
        {
            // Nothing is run yet: no empty table with its headers, but what the section is and the card that opens it, together.
            m.Shape = EstateShape.Empty;
            m.OffIcon = UiIcon.Key;
            m.OffTitle = W(t, "reallife_estate_row_no_agency", "No agency yet");
            m.OffBody = W(t, "reallife_estate_agency_open_what", "Run the agency from an office you hold.");
            m.Side = new UiFactCardModel
            {
                Lines = new[] { EstateStep(runs, W(t, "reallife_ui_estate_fig_office", "Office"), "7 5th Avenue", null, false, true) },
                Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_open_agency", "Open the agency"), UiButtonKind.Primary, UiIcon.Key, UiAction.Alt) }
            };
            return;
        }
        var rows = new List<UiDataRow>();
        rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_sale", "A sale in {district}"), "district", "Midtown"), "$2,400,000", "$48,000", "3.5", W(t, "reallife_estate_stage_offered", "Offered"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "419") },
            new[] { (int)UiIcon.Tag, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Warn, UiRole.None }, new[] { W(t, "reallife_estate_filter_sales", "Sales"), null, null, null, null, null }));
        rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_letting", "A letting in {district}"), "district", "Uptown"), "$3,100", "$1,550", "1.2", W(t, "reallife_estate_stage_marketing", "Marketing"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "422") },
            new[] { (int)UiIcon.Key, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Notice, UiRole.None }, new[] { W(t, "reallife_estate_filter_lettings", "Lettings"), null, null, null, null, null }));
        rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_sale", "A sale in {district}"), "district", "Garment District"), "$1,150,000", "$23,000", "2.4", W(t, "reallife_estate_stage_marketing", "Marketing"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "426") },
            new[] { (int)UiIcon.Tag, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Notice, UiRole.None }, new[] { W(t, "reallife_estate_filter_sales", "Sales"), null, null, null, null, null }));
        rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_letting", "A letting in {district}"), "district", "Midtown"), "$4,800", "$2,400", "1.6", W(t, "reallife_estate_stage_offered", "Offered"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "420") },
            new[] { (int)UiIcon.Key, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Warn, UiRole.None }, new[] { W(t, "reallife_estate_filter_lettings", "Lettings"), null, null, null, null, null }));
        rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_sale", "A sale in {district}"), "district", "Uptown"), "$1,980,000", "$39,600", "2.1", W(t, "reallife_estate_stage_completing", "Completing"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "413") },
            new[] { (int)UiIcon.Tag, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok, UiRole.None }, new[] { W(t, "reallife_estate_filter_sales", "Sales"), null, null, null, null, null }));
        rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_letting", "A letting in {district}"), "district", "Hell's Kitchen"), "$2,900", "$1,450", "1.0", W(t, "reallife_estate_stage_completing", "Completing"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "415") },
            new[] { (int)UiIcon.Key, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok, UiRole.None }, new[] { W(t, "reallife_estate_filter_lettings", "Lettings"), null, null, null, null, null }));
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_instruction", "Instruction"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_value", "Value"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_commission", "Commission"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_hours", "Hours"), Kind = UiCellKind.Figure, Width = 80f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 184f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_day", "Day"), Kind = UiCellKind.Date, Width = 104f }
        };
        var side = new UiFactCardModel
        {
            Title = W(t, "reallife_estate_tab_agency", "Agency"), Heading = true,
            Lines = new[]
            {
                UiFactLine.Row(W(t, "reallife_ui_estate_fig_deals", "Deals done"), "14"),
                UiFactLine.Row(W(t, "reallife_ui_estate_fig_lost", "Deals lost"), "3"),
                UiFactLine.Row(W(t, "reallife_ui_estate_fig_earned", "Commission earned"), "$212,400")
            },
            Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_close_agency", "Close the agency"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count) }
        };
        TableOf(m, UiIcon.Key, W(t, "reallife_estate_tab_agency", "Agency"), "7 5th Avenue", cols, rows, side, W(t, "reallife_estate_row_no_instructions", "No instructions"), W(t, "reallife_estate_agency_open_what", "Run the agency from an office you hold."));
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_instructions", "Open instructions"), "6"), Fig(W(t, "reallife_ui_estate_col_commission", "Commission"), "$74,950"),
            Fig(W(t, "reallife_ui_estate_fig_negotiators", "Negotiators"), "2"), Fig(W(t, "reallife_ui_estate_fig_hours_day", "Hours a day"), "11.5")
        };
        m.ShareTitle = W(t, "reallife_estate_agency_reputation", "What the districts think");
        m.ShareLabels = new[] { "Midtown", "Uptown", "Garment District", "Murray Hill" };
        m.ShareValues = new[] { 62, 48, 55, 31 };
        m.RelatedTitle = W(t, "reallife_ui_estate_offices", "Offices you hold");
        m.RelatedCount = "3";
        runs.Add(Yes);
        runs.Add(Yes);
        m.Related = new[]
        {
            new EstateRow { Icon = UiIcon.Briefcase, Title = "7 5th Avenue", Sub = "Midtown", Chip = W(t, "reallife_ui_estate_agency_here", "The agency is here"), ChipTone = UiRole.Ok, Run = -1 },
            new EstateRow { Icon = UiIcon.Briefcase, Title = "12 W 39th Street, first floor office", Sub = "Garment District", Run = runs.Count - 2 },
            new EstateRow { Icon = UiIcon.Briefcase, Title = "17 E 41st Street, floor 6", Sub = "Midtown", Run = runs.Count - 1 }
        };
        m.Offer = new EstateOffer
        {
            Icon = UiIcon.Tag, Title = Fx(W(t, "reallife_estate_row_sale", "A sale in {district}"), "district", "Midtown"), Chip = W(t, "reallife_estate_stage_offered", "Offered"),
            Sub = Fx(W(t, "reallife_ui_estate_offer_sub", "{sqm} m2, asking {ask} of its value"), "sqm", "180", "ask", "104%"),
            FigLabels = new[] { W(t, "reallife_ui_estate_offer_fee", "Fee to take it"), W(t, "reallife_ui_estate_offer_lapses", "Lapses in"), W(t, "reallife_ui_estate_offer_chance", "Chance of a deal"),
                W(t, "reallife_ui_estate_col_commission", "Commission") },
            FigValues = new[] { "$4,800", Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", "7"), "42%", "$48,000" },
            Actions = new[]
            {
                EstateButton(runs, W(t, "reallife_estate_btn_decline", "Decline"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                EstateButton(runs, W(t, "reallife_estate_btn_take", "Take it"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt)
            }
        };
        string[] districts = { "Midtown", "Uptown", "Garment District", "Murray Hill" };
        string[] chances = { "14%", "10%", "12%", "6%" };
        var chance = new EstateRow[districts.Length];
        for (int i = 0; i < chance.Length; i++)
        {
            chance[i] = new EstateRow
            {
                Icon = UiIcon.Locate, Title = districts[i],
                Amount = Fx(W(t, "reallife_ui_estate_chance_a_day", "{chance} a day"), "chance", chances[i]), Run = -1
            };
        }
        m.Related2Title = W(t, "reallife_ui_estate_new_instruction_chance", "The chance of a new instruction");
        m.Related2Count = chance.Length.ToString();
        m.Related2 = chance;
    }

    private static void Hotel(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        // name, district, stars in halves, rooms sold, takings, costs, takings year, costs year, clean, staff, campaign days left
        string[][] hotels =
        {
            new[] { "Pier House Hotel", "Midtown", "8", "78", "$6,200", "$4,900", "$44,000", "$36,100", "82", "70", "3" },
            new[] { "Canal Street Inn", "Garment District", "6", "60", "$2,700", "$2,300", "$19,600", "$17,500", "64", "50", "0" },
            new[] { "Hartley Suites", "Uptown", "10", "71", "$9,800", "$6,100", "$71,400", "$52,900", "91", "85", "0" },
            new[] { "Dockside Rooms", "Hell's Kitchen", "5", "52", "$3,100", "$2,800", "$22,000", "$19,500", "58", "40", "0" }
        };
        var rows = new List<UiDataRow>();
        var locate = new List<int>();
        foreach (string[] h in hotels)
        {
            runs.Add(Yes);
            locate.Add(runs.Count - 1);
            int days = int.Parse(h[10]);
            string campaign = days > 0 ? Fx(W(t, "reallife_ui_estate_campaign_on", "Running, {days} day(s) left"), "days", h[10]) : W(t, "reallife_ui_estate_campaign_off", "None running");
            rows.Add(Row(new[] { h[0], string.Empty, h[8], h[9], h[3], h[4], h[5], campaign, string.Empty },
                new[] { (int)UiIcon.Building, int.Parse(h[2]), int.Parse(h[8]), int.Parse(h[9]), int.Parse(h[3]), 0, 0, 0, (int)UiIcon.Locate },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, days > 0 ? UiRole.Ok : UiRole.Muted, UiRole.None },
                new[] { h[1], null, null, null, null, null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_hotel", "Hotel"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rating", "Rating"), Kind = UiCellKind.Stars, Width = 120f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_clean", "Clean"), Kind = UiCellKind.Meter, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_staff", "Staff"), Kind = UiCellKind.Meter, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rooms", "Rooms sold"), Kind = UiCellKind.Meter, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_takings", "Takings"), Kind = UiCellKind.Money, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_costs", "Costs"), Kind = UiCellKind.Money, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_campaign", "Campaign"), Kind = UiCellKind.Chip, Width = 176f },
            new UiDataColumn { Title = string.Empty, Kind = UiCellKind.Action, Width = 56f }
        };
        TableOf(m, UiIcon.Building, W(t, "reallife_estate_tab_hotel", "Hotel"), Fx(W(t, "reallife_ui_estate_hotels_sub", "{count} hotel(s) you run"), "count", "4"), cols, rows, null, W(t, "reallife_estate_row_no_hotel", "No hotel yet"),
            W(t, "reallife_ui_estate_empty_hotel_body", "A hotel you buy or lease from the Market tab is run from here."));
        m.RowRuns = locate.ToArray();
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_rooms_sold", "Rooms sold today"), "113 / 160"), Fig(W(t, "reallife_ui_estate_col_takings", "Takings"), "$21,800"),
            Fig(W(t, "reallife_ui_estate_col_costs", "Costs"), "$16,100"), Fig(W(t, "reallife_ui_estate_fig_kept_year", "Kept this year"), "$31,000")
        };
        // Each hotel's rates against the going rate for its room kinds, and what the guests say.
        string[] kinds = { "double", "single" };
        int[] sold = { 85, 62, 66, 48, 80, 70, 55, 41 };
        string[] yours = { "$128", "$96", "$104", "$74", "$210", "$160", "$88", "$66" };
        string[] going = { "$120", "$100", "$110", "$70", "$200", "$150", "$92", "$64" };
        string[] share = { "107%", "96%", "95%", "106%", "105%", "107%", "96%", "103%" };
        var rates = new EstateRow[8];
        for (int i = 0; i < rates.Length; i++)
        {
            rates[i] = new EstateRow
            {
                Icon = UiIcon.Building, Title = hotels[i / 2][0] + ", " + kinds[i % 2], Sub = Fx(W(t, "reallife_ui_estate_rate_going", "Going rate {going}, {share} sold"), "going", going[i], "share", sold[i] + "%"),
                Chip = Fx(W(t, "reallife_ui_estate_of_going", "{share} of the going rate"), "share", share[i]), ChipTone = UiRole.Notice, Amount = yours[i], Run = -1
            };
        }
        m.RelatedTitle = W(t, "reallife_ui_estate_rates_title", "Your rate against the going rate");
        m.RelatedCount = "8";
        m.RelatedRows = 8;
        m.Related = rates;
        string[] says = { "Clean rooms and a fair price", "Staff were kind, the lift was slow", "Too noisy at night" };
        string[] who = { "Pier House Hotel", "Pier House Hotel", "Canal Street Inn" };
        var reviews = new EstateRow[says.Length];
        for (int i = 0; i < reviews.Length; i++)
        {
            reviews[i] = new EstateRow { Icon = UiIcon.Star, Title = says[i], Sub = who[i], Run = -1 };
        }
        m.Related2Title = W(t, "reallife_ui_estate_guests_say", "Guests say");
        m.Related2Count = Fx(W(t, "reallife_ui_estate_reviews_n", "{count} reviews"), "count", "3");
        m.Related2 = reviews;
    }

    private static void Hire(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>
        {
            Row(new[] { "Brisa roadster", W(t, "reallife_estate_car_out", "Out"), "$210", "14", "$2,940", Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "416") }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.Chart7, UiRole.Ok, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_premium", "Premium"), null, null, null, null, null }),
            Row(new[] { "Sora hatchback", W(t, "reallife_estate_car_idle", "In"), "$74", "31", "$2,294", string.Empty }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.Chart4, UiRole.Notice, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_economy", "Economy"), null, null, null, null, null }),
            Row(new[] { "Halden sedan", W(t, "reallife_estate_car_out", "Out"), "$88", "22", "$1,936", Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "414") }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.Chart5, UiRole.Ok, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_standard", "Standard"), null, null, null, null, null }),
            Row(new[] { "Atlas pickup", W(t, "reallife_estate_car_idle", "In"), "$102", "9", "$918", string.Empty }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.Chart5, UiRole.Notice, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_standard", "Standard"), null, null, null, null, null }),
            Row(new[] { "Marlow sedan", W(t, "reallife_estate_car_out", "Out"), "$84", "18", "$1,512", Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "415") }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.Chart5, UiRole.Ok, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_standard", "Standard"), null, null, null, null, null }),
            Row(new[] { "Pinecrest saloon", W(t, "reallife_estate_car_idle", "In"), "$92", "11", "$1,012", string.Empty }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.Chart5, UiRole.Notice, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_standard", "Standard"), null, null, null, null, null }),
            Row(new[] { "Volta SUV", W(t, "reallife_estate_car_out", "Out"), "$118", "12", "$1,416", Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "417") }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.Chart5, UiRole.Ok, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_standard", "Standard"), null, null, null, null, null }),
            Row(new[] { "Marlow estate", W(t, "reallife_estate_car_idle", "In"), "$79", "8", "$632", string.Empty }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.Chart5, UiRole.Notice, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_standard", "Standard"), null, null, null, null, null })
        };
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_car", "Car"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 184f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rate_day", "Rate a day"), Kind = UiCellKind.Money, Width = 120f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_hires", "Hires"), Kind = UiCellKind.Figure, Width = 80f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_takings", "Takings"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_day", "Day"), Kind = UiCellKind.Date, Width = 104f }
        };
        var lines = new List<UiFactLine>
        {
            UiFactLine.Row(W(t, "reallife_ui_estate_fig_year_takings", "Takings this year"), "$12,660"),
            UiFactLine.Row(W(t, "reallife_ui_estate_fig_year_costs", "Costs this year"), "$3,480"),
            UiFactLine.Row(W(t, "reallife_ui_estate_hire_kept_year", "Kept this year"), "$9,180", UiRole.OkText),
            UiFactLine.Row(W(t, "reallife_ui_estate_hire_best", "Best earner"), "Brisa roadster, $2,940"),
            UiFactLine.Row(W(t, "reallife_ui_estate_hire_most", "Most hired"), "Sora hatchback, 31"),
            UiFactLine.Section(W(t, "reallife_ui_estate_hire_sec_cover", "Cover")),
            EstateSwitch(runs, W(t, "reallife_estate_hire_cover", "Fleet cover"), true),
            UiFactLine.Row(W(t, "reallife_ui_estate_fig_cover_cost", "Cover costs"), Fx(W(t, "reallife_ui_estate_amount_a_day", "{amount} a day"), "amount", "$38")),
            UiFactLine.Row(W(t, "reallife_ui_estate_cover_pays", "Pays of a claim"), "80%"),
            UiFactLine.Row(W(t, "reallife_ui_estate_cover_excess", "Excess"), "$100"),
            UiFactLine.Section(W(t, "reallife_ui_estate_hire_sec_lot", "The lot")),
            UiFactLine.Bar(W(t, "reallife_ibox_spaces", "Spaces taken"), 50), UiFactLine.Bar(W(t, "reallife_ui_estate_sh_in_hire", "Cars in hire"), 50)
        };
        var side = new UiFactCardModel { Title = W(t, "reallife_estate_tab_hire", "Car hire"), Heading = true, Lines = lines.ToArray() };
        TableOf(m, UiIcon.Car, W(t, "reallife_estate_tab_hire", "Car hire"), "88 W 46th Street", cols, rows, side, W(t, "reallife_estate_row_no_cars", "No cars on hire"), W(t, "reallife_estate_hire_what", "Hire cars out from a lot."));
        m.HeadStars = 7;
        m.HeadSub = W(t, "reallife_ui_estate_fleet_rating", "The fleet's rating, from the customers' opinion");
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_cars_out", "Cars out"), "4"), Fig(W(t, "reallife_ui_estate_col_hires", "Hires"), "125"),
            Fig(W(t, "reallife_ui_estate_col_takings", "Takings"), "$12,660"), Fig(W(t, "reallife_ui_estate_fig_mean_rate", "Mean rate a day"), "$106")
        };
        // The cars of yours that stand within reach of the lot and could join the fleet, with the going rate each would hire at.
        string[] adds = { "Courier van", "Dune camper van", "Ranger pickup", "Boxer box truck" };
        string[] rates = { "$96", "$110", "$102", "$140" };
        string[] chance = { "Standard", "Standard", "Standard", "Standard" };
        var could = new EstateRow[adds.Length];
        for (int i = 0; i < could.Length; i++)
        {
            runs.Add(Yes);
            CarKind kind = SampleKind(adds[i]);
            could[i] = new EstateRow
            {
                Icon = CarClass.Glyph(kind), Tint = CarClass.Tint(kind, 0), Title = adds[i], Sub = W(t, "reallife_estate_class_" + chance[i].ToLowerInvariant(), chance[i]),
                Chip = Fx(W(t, "reallife_ui_estate_going_rate_day", "Going rate {amount} a day"), "amount", rates[i]), ChipTone = UiRole.Notice,
                Button = W(t, "reallife_ui_estate_add_to_fleet", "Add to the fleet"), ButtonRun = runs.Count - 1, Run = runs.Count - 1
            };
        }
        m.RelatedTitle = W(t, "reallife_ui_estate_add_cars", "Cars you could add");
        m.RelatedCount = "4";
        m.Related = could;
    }

    private static void EstateDetail(UiWords t, EstateModel m, List<Func<bool>> runs, string state)
    {
        if (state == "call" || state == "applicant")
        {
            EstateDecide(t, m, runs, state);
            return;
        }
        m.Shape = EstateShape.Detail;
        m.Back = W(t, "reallife_co_back", "Back");
        switch (state)
        {
            case "building":
                m.Tab = 0;
                m.DetailIcon = UiIcon.Home;
                m.DetailTitle = "41 E 37th Street";
                m.DetailSub = Fx(W(t, "reallife_ibox_type_in", "{type} in {district}"), "type", W(t, "reallife_estate_use_homes", "Homes"), "district", "Murray Hill");
                m.DetailLocate = W(t, "reallife_ui_estate_locate", "Locate");
                runs.Add(Yes);
                m.DetailLocateRun = runs.Count - 1;
                m.Strip = new[]
                {
                    Fig(W(t, "reallife_ibox_worth", "Worth"), "$4,860,000"), Fig(W(t, "reallife_ui_estate_fig_bought", "Bought for"), "$3,900,000"),
                    Fig(W(t, "reallife_ui_estate_fig_works_spent", "Spent on works"), "$120,000"), Fig(W(t, "reallife_ui_estate_fig_gain", "Gain if sold today"), "+$500,000")
                };
                m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ui_estate_running", "Running it"), Heading = true,
                        Lines = new[]
                        {
                            EstateStep(runs, W(t, "reallife_estate_duty_who", "Who runs it"), W(t, "reallife_estate_manager_self", "Yourself"), null, false, true),
                            UiFactLine.Row(W(t, "reallife_ui_estate_fee_row", "Fee"), W(t, "reallife_ui_estate_fee_none", "None")),
                            EstateSwitch(runs, W(t, "reallife_estate_own_cover", "Landlord's cover"), true),
                            EstateLink(runs, W(t, "reallife_ui_estate_works_link", "Works and servicing"))
                        }
                    }
                };
                m.Right = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_estate_own_selling", "Selling"), Heading = true,
                        Lines = new[]
                        {
                            EstateStep(runs, W(t, "reallife_ui_estate_sale_asking_share", "Asking, of its worth"), "100%", null),
                            UiFactLine.Row(W(t, "reallife_ui_estate_sale_asking_price", "Asking price"), "$4,860,000"), UiFactLine.Row(W(t, "reallife_ui_estate_sale_chance", "Chance of an offer a day"), "18%"),
                            UiFactLine.Row(W(t, "reallife_ui_estate_sale_fee", "Selling fee"), "1.5%")
                        },
                        Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_list", "List for sale"), UiButtonKind.Primary, UiIcon.Tag, UiAction.Alt) }
                    }
                };
                m.ShareTitle = W(t, "reallife_ui_estate_sh_building", "The building");
                m.ShareLabels = new[] { W(t, "reallife_ibox_let", "Let"), W(t, "reallife_estate_duty_happy", "Satisfaction"), W(t, "reallife_ui_estate_m_condition", "Condition") };
                m.ShareValues = new[] { 100, 63, 80 };
                m.RelatedTitle = W(t, "reallife_ui_estate_rel_let_to", "Let to");
                m.RelatedCount = Fx(W(t, "reallife_ui_estate_units_n", "{count} unit(s)"), "count", "3");
                m.RelatedRows = 6;
                string[][] flats = { new[] { "Priya Nair", "flat 3A", "$2,600" }, new[] { "Daniel Ortiz", "flat 3B", "$3,300" }, new[] { "Sofia Marchetti", "flat 4A", "$2,600" } };
                var let = new EstateRow[flats.Length];
                for (int i = 0; i < let.Length; i++)
                {
                    runs.Add(Yes);
                    bool late = i == 2;
                    let[i] = new EstateRow
                    {
                        Face = flats[i][0], Hue = UiColourHue(flats[i][0]), Title = flats[i][0], Sub = "41 E 37th Street, " + flats[i][1], Chip = late ? W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "6") : W(t, "reallife_ui_estate_paid", "Paid"),
                        ChipTone = late ? UiRole.Warn : UiRole.Ok, Amount = flats[i][2], Run = runs.Count - 1
                    };
                }
                m.Related = let;
                m.Related2Title = W(t, "reallife_ui_estate_works_servicing", "Works and servicing");
                m.Related2Count = "2";
                m.Related2 = new[]
                {
                    new EstateRow { Icon = UiIcon.Wrench, Title = ServiceTitle(t, 1, "the heating"), Sub = Fx(W(t, "reallife_ui_estate_booked_with", "Booked with {who}"), "who", "Okonkwo Heating"), Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "417"), ChipTone = UiRole.Notice, Amount = "$340", Run = -1 },
                    new EstateRow { Icon = UiIcon.Eye, Title = W(t, "reallife_ui_estate_next_inspection", "Next inspection"), Sub = W(t, "reallife_ui_estate_last_passed", "The last one passed"), Chip = Fx(W(t, "reallife_ui_estate_in_days", "in {days} days"), "days", "9"), ChipTone = UiRole.Notice, Run = -1 }
                };
                break;
            case "listing":
                m.Tab = 3;
                m.DetailIcon = UiIcon.Home;
                m.DetailTitle = "301 W 54th Street";
                m.DetailSub = Fx(W(t, "reallife_ibox_type_in", "{type} in {district}"), "type", W(t, "reallife_estate_use_homes", "Homes"), "district", "Hell's Kitchen");
                m.DetailLocate = W(t, "reallife_ui_estate_locate", "Locate");
                runs.Add(Yes);
                m.DetailLocateRun = runs.Count - 1;
                m.Strip = new[]
                {
                    Fig(W(t, "reallife_ui_estate_fig_asking_price", "Asking price"), "$1,840,000"), Fig(W(t, "reallife_ui_estate_fig_game_value", "The game's value"), "$1,720,000"),
                    Fig(W(t, "reallife_ui_estate_fig_market_level", "Property market"), "103%"), Fig(W(t, "reallife_ui_estate_fig_prices_since", "Prices since the start"), "112%")
                };
                m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = Fx(W(t, "reallife_ui_estate_let_as", "Let as {use}"), "use", W(t, "reallife_estate_use_homes", "Homes")), Heading = true,
                        Lines = new[] { UiFactLine.Row(W(t, "reallife_ui_estate_listing_a_day", "Rent a day"), "$7,420"), UiFactLine.Row(W(t, "reallife_ui_estate_listing_yield", "Yield a year"), "4.1%"), UiFactLine.Bar(W(t, "reallife_ui_estate_m_condition", "Condition"), 80) },
                        Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_offer", "Make an offer"), UiButtonKind.Primary, UiIcon.Handshake, UiAction.Alt) }
                    }
                };
                m.Card = true;
                m.FootBack = W(t, "reallife_ui_estate_not_now", "Not now");
                m.ShareTitle = W(t, "reallife_ui_estate_sh_district_rents", "What the districts rent for, per m2");
                m.ShareLabels = new[] { "Midtown", "Garment District", "Murray Hill", "Hell's Kitchen", "Uptown" };
                m.ShareValues = new[] { 100, 90, 81, 72, 50 };
                m.ShareTexts = new[] { "$38.50", "$34.80", "$31.20", "$27.90", "$19.40" };

                m.Related2Title = Fx(W(t, "reallife_ui_estate_also_in", "Also for sale in {district}"), "district", "Hell's Kitchen");
                m.Related2Count = "2";
                runs.Add(Yes);
                runs.Add(Yes);
                m.Related2 = new[]
                {
                    new EstateRow { Icon = UiIcon.Home, Title = "412 10th Avenue", Sub = W(t, "reallife_estate_use_homes", "Homes"), Chip = Fx(W(t, "reallife_ui_estate_yield_year", "{yield} a year"), "yield", "4.8%"), ChipTone = UiRole.Notice, Amount = "$2,150,000", Run = runs.Count - 2 },
                    new EstateRow { Icon = UiIcon.Home, Title = "9 W 43rd Street", Sub = W(t, "reallife_estate_use_homes", "Homes"), Chip = Fx(W(t, "reallife_ui_estate_yield_year", "{yield} a year"), "yield", "3.9%"), ChipTone = UiRole.Notice, Amount = "$1,310,000", Run = runs.Count - 1 }
                };
                break;
            case "works_page":
                m.Card = true;
                m.FootBack = W(t, "reallife_ui_estate_not_now", "Not now");
                m.Tab = 2;
                m.DetailIcon = UiIcon.Wrench;
                m.DetailTitle = "41 E 37th Street";
                m.DetailSub = Fx(W(t, "reallife_ibox_type_in", "{type} in {district}"), "type", W(t, "reallife_estate_use_homes", "Homes"), "district", "Murray Hill");
                m.Strip = new[]
                {
                    Fig(W(t, "reallife_ui_estate_m_condition", "Condition"), "80%"), Fig(W(t, "reallife_ui_estate_fig_worth_of_best", "Worth, of its best"), "93%"),
                    Fig(W(t, "reallife_ui_estate_fig_rents_of_best", "Rents, of its best"), "96%"), Fig(W(t, "reallife_ui_estate_fig_booked", "Booked"), "1")
                };
                m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ibox_sec_works", "Works"), Heading = true,
                        Lines = new[]
                        {
                            UiFactLine.Row(W(t, "reallife_ui_estate_works_repair_costs", "A repair costs"), "$24,800"), UiFactLine.Row(W(t, "reallife_ui_estate_works_repair_takes", "A repair takes"), Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", "5")),
                            UiFactLine.Row(W(t, "reallife_ui_estate_works_level", "Fitted level"), "1")
                        },
                        Actions = new[]
                        {
                            EstateButton(runs, W(t, "reallife_estate_btn_refurbish", "Refurbish"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                            EstateButton(runs, W(t, "reallife_estate_btn_repair", "Repair"), UiButtonKind.Primary, UiIcon.Wrench, UiAction.Alt)
                        }
                    }
                };
                m.Right = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ui_estate_servicing", "Booked servicing"), Heading = true,
                        Lines = new[]
                        {
                            UiFactLine.Row(W(t, "reallife_ui_estate_service_keeps", "A visit keeps a fault down for"), Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", "90")),
                            EstateLink(runs, Fx(W(t, "reallife_ui_estate_service_line", "Cancel the {fault} service on day {day}, {who}, {amount}"), "fault", W(t, "reallife_estate_fault_1", "the heating"), "day", "417", "who", "Okonkwo Heating", "amount", "$340"))
                        }
                    }
                };
                m.GridTitle = W(t, "reallife_ui_estate_book_a_visit", "Book a visit");
                var book = new List<UiActionModel>();
                string[] faults = { W(t, "reallife_estate_fault_1", "the heating"), W(t, "reallife_estate_fault_2", "a leak"), W(t, "reallife_estate_fault_3", "the electrics"), W(t, "reallife_estate_fault_4", "a lock"), W(t, "reallife_estate_fault_5", "damp"), W(t, "reallife_estate_fault_6", "the fittings") };
                for (int i = 0; i < faults.Length; i++)
                {
                    book.Add(EstateButton(runs, Fx(W(t, "reallife_ui_estate_book_kind", "Book {fault}, {amount}"), "fault", faults[i], "amount", "$" + (180 + i * 40)), UiButtonKind.Secondary, UiIcon.Calendar, UiAction.Count, i == 0));
                }
                m.Grid = book.ToArray();
                m.RelatedTitle = W(t, "reallife_ui_estate_calls_here", "Calls on this building");
                m.RelatedCount = "1";
                runs.Add(Yes);
                runs.Add(Yes);
                m.Related = new[]
                {
                    new EstateRow { Face = "Sofia Marchetti", Hue = UiColourHue("Sofia Marchetti"), Title = "Heating, flat 4A", Sub = "Sofia Marchetti", Chip = W(t, "reallife_ibox_chip_open", "Open"), ChipTone = UiRole.Warn, Amount = "$340", Run = runs.Count - 2 }
                };
                break;
            case "call":
                m.Tab = 2;
                m.DetailIcon = UiIcon.Wrench;
                m.DetailTitle = "41 E 37th Street, flat 4A";
                m.DetailSub = Capital(W(t, "reallife_estate_fault_1", "the heating"));
                m.DetailChip = W(t, "reallife_ibox_chip_open", "Open");
                m.DetailChipTone = UiRole.Warn;
                m.Strip = new[]
                {
                    Fig(W(t, "reallife_ui_estate_ch_contractor", "A contractor"), "$340"), Fig(W(t, "reallife_ui_estate_ch_yourself", "Yourself, materials"), "$119"),
                    Fig(W(t, "reallife_ui_estate_fig_reported", "Reported"), Fx(W(t, "reallife_ui_estate_days_ago", "{days} days ago"), "days", "5")),
                    Fig(W(t, "reallife_ui_estate_fig_grace", "The tenant minds after"), Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", "3"))
                };
                m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ibox_sec_quote", "The quote"), Heading = true,
                        Lines = new[] { UiFactLine.Say(Fx(W(t, "reallife_ui_estate_call_minutes", "Doing it yourself takes about {minutes} minutes at the building's door."), "minutes", "120")) },
                        Actions = new[]
                        {
                            EstateButton(runs, W(t, "reallife_estate_btn_yourself", "Do it yourself"), UiButtonKind.Secondary, UiIcon.MapPin, UiAction.Option),
                            EstateButton(runs, W(t, "reallife_estate_btn_contractor", "Send a contractor"), UiButtonKind.Primary, UiIcon.Wrench, UiAction.Alt)
                        }
                    }
                };
                m.Card = true;
                m.FootBack = W(t, "reallife_ui_estate_not_now", "Not now");
                m.Person = new EstateUnitPage
                {
                    TenantTitle = W(t, "reallife_ui_estate_the_tenant", "The tenant"), TenantName = "Sofia Marchetti", TenantHue = UiColourHue("Sofia Marchetti"), TenantRing = UiRole.Warn,
                    TenantLine = "Nurse, Murray Hill Clinic, 34", TenantChip = W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "6"), TenantOpen = W(t, "reallife_ui_estate_open_page", "Open their page"),
                    MeterLabels = new[] { W(t, "reallife_ui_estate_m_satisfaction", "Satisfaction"), W(t, "reallife_ui_estate_m_repairs", "Repairs"), W(t, "reallife_ui_estate_m_value", "Value for rent"), W(t, "reallife_ui_estate_m_condition", "Condition") },
                    MeterValues = new[] { 51, 39, 62, 80 }
                };
                runs.Add(Yes);
                m.Person.TenantOpenRun = runs.Count - 1;
                m.RelatedTitle = W(t, "reallife_ui_estate_works_here", "Works on this unit");
                m.RelatedCount = "2";
                m.Related = new[]
                {
                    new EstateRow { Icon = UiIcon.Wrench, Title = ServiceTitle(t, 1, "the heating"), Sub = Fx(W(t, "reallife_ui_estate_booked_with", "Booked with {who}"), "who", "Okonkwo Heating"), Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "417"), ChipTone = UiRole.Notice, Amount = "$340", Run = -1 },
                    new EstateRow { Icon = UiIcon.Wrench, Title = W(t, "reallife_estate_improve_boiler", "a new boiler"), Sub = W(t, "reallife_ui_estate_asked_by_tenant", "Asked for by the tenant, you can say no"), Chip = W(t, "reallife_ui_estate_waiting_you", "Waiting for you"), ChipTone = UiRole.Warn, Amount = "$2,900", Run = -1 }
                };
                m.Related2Title = W(t, "reallife_ui_estate_record_title", "Rent record");
                m.Related2Count = Fx(W(t, "reallife_ui_estate_last_months", "last {count} months"), "count", "4");
                string[] due = { "406", "376", "346", "316" };
                var rec = new EstateRow[due.Length];
                for (int i = 0; i < rec.Length; i++)
                {
                    rec[i] = new EstateRow
                    {
                        Icon = UiIcon.Calendar, Title = Fx(W(t, "reallife_ui_estate_due_day", "Due day {day}"), "day", due[i]), Sub = i == 0 ? W(t, "reallife_ui_estate_not_paid_yet", "Not paid yet") : Fx(W(t, "reallife_ui_estate_paid_on", "Paid on day {day}"), "day", due[i]),
                        Chip = i == 0 ? Fx(W(t, "reallife_ui_estate_late_days", "Late {days} days"), "days", "6") : W(t, "reallife_ui_estate_paid", "Paid"), ChipTone = i == 0 ? UiRole.Warn : UiRole.Ok, Amount = "$2,600", Run = -1
                    };
                }
                m.Related2 = rec;
                break;
            default:
                m.Tab = 1;
                m.DetailIcon = UiIcon.People;
                m.DetailTitle = "Noor Haddad";
                m.DetailFace = "Noor Haddad";
                m.DetailSub = W(t, "reallife_estate_tenant_shops_2", "A baker") + ", 233 W 39th Street, unit 2";
                m.DetailChip = W(t, "reallife_estate_record_good", "Good");
                m.Strip = new[]
                {
                    Fig(W(t, "reallife_ui_estate_fig_their_offer", "Their offer a day"), "$206"), Fig(W(t, "reallife_ui_estate_ch_market_rent", "The market's rent"), "$214"),
                    Fig(W(t, "reallife_ui_estate_fig_of_market", "Of the market's rent"), "96%"), Fig(W(t, "reallife_ui_estate_fig_deposit", "Deposit"), "$1,236")
                };
                m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ibox_sec_terms", "Terms"), Heading = true,
                        Lines = new[] { UiFactLine.Row(W(t, "reallife_ui_estate_term_lease", "A lease of"), Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", "150")), UiFactLine.Row(W(t, "reallife_ui_estate_term_until", "Waits until"), Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "421")) },
                        Actions = new[]
                        {
                            EstateButton(runs, W(t, "reallife_estate_btn_refuse", "Refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                            EstateButton(runs, W(t, "reallife_estate_btn_accept", "Accept"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt)
                        }
                    }
                };
                m.Card = true;
                m.FootBack = W(t, "reallife_ui_estate_not_now", "Not now");
                m.ShareTitle = W(t, "reallife_ui_estate_sh_record", "What their record says");
                m.ShareLabels = new[] { W(t, "reallife_ui_estate_col_record", "Record"), W(t, "reallife_ui_estate_sh_pays", "Pays on its day"), W(t, "reallife_ui_estate_sh_catches", "Catches up when late") };
                m.ShareValues = new[] { 62, 91, 60 };
                m.RelatedTitle = W(t, "reallife_ui_estate_the_unit_they_want", "The unit they want");
                runs.Add(Yes);
                m.Related = new[]
                {
                    new EstateRow { Icon = UiIcon.Store, Title = "233 W 39th Street, unit 2", Sub = "Garment District", Chip = Fx(W(t, "reallife_estate_state_empty", "Empty {days} days"), "days", "12"), ChipTone = UiRole.Warn, Amount = "$214", Run = runs.Count - 1 }
                };
                m.Related2Title = W(t, "reallife_ui_estate_others_asked", "Others who asked for it");
                m.Related2Count = "2";
                runs.Add(Yes);
                runs.Add(Yes);
                m.Related2 = new[]
                {
                    new EstateRow { Face = "Ravi Shah", Hue = UiColourHue("Ravi Shah"), Title = "Ravi Shah", Sub = Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "424"), Chip = W(t, "reallife_estate_record_fair", "Fair"), ChipTone = UiRole.Warn, Amount = "$96", Run = runs.Count - 2 },
                    new EstateRow { Face = "Zoe Adler", Hue = UiColourHue("Zoe Adler"), Title = "Zoe Adler", Sub = Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "426"), Chip = W(t, "reallife_estate_record_good", "Good"), ChipTone = UiRole.Ok, Amount = "$91", Run = runs.Count - 1 }
                };
                break;
        }
    }
}
