using System;
using System.Collections.Generic;
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

    private static void Tenants(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>();
        string[][] people =
        {
            new[] { "41 E 37th Street, flat 3A", "Murray Hill", "Priya Nair", "Nurse, Murray Hill Clinic", "$2,600", "good", "82", "ok" },
            new[] { "41 E 37th Street, flat 3B", "Murray Hill", "Daniel Ortiz", "Cook, Pier Street Diner", "$3,300", "good", "74", "ok" },
            new[] { "41 E 37th Street, flat 4A", "Murray Hill", "Sofia Marchetti", "Nurse, Murray Hill Clinic", "$2,600", "fair", "51", "late6" },
            new[] { "12 W 39th Street, ground floor shop", "Garment District", "Lena Hoffmann", "A tailor's shop", "$5,400", "good", "68", "ok" },
            new[] { "12 W 39th Street, first floor office", "Garment District", "Marcus Bell", "Accountant, Bell and Rowe", "$3,900", "good", "77", "ok" },
            new[] { "233 W 39th Street, unit 1", "Garment District", "Noor Haddad", "A bakery", "$6,200", "fair", "39", "late14" },
            new[] { "88 W 46th Street, flat 2", "Hell's Kitchen", "Theo Brandt", "Courier, Eastside Parcels", "$2,200", "good", "85", "ok" },
            new[] { "75 W 125th Street, house", "Uptown", "Imani Cole", "Teacher, Lincoln School", "$3,100", "good", "90", "ok" },
            new[] { "17 E 41st Street, floor 6", "Midtown", "Owen Park", "Designer, Park and Lane", "$7,800", "good", "71", "ok" },
            new[] { "520 8th Avenue, suite 12", "Midtown", "Yuki Tanaka", "An import firm", "$5,600", "fair", "33", "notice" },
            new[] { "412 10th Avenue, unit B", "Hell's Kitchen", "Chen Wei", "Chef, Harbour Kitchen", "$4,800", "good", "63", "ok" }
        };
        foreach (string[] p in people)
        {
            string record = p[5] == "good" ? W(t, "reallife_estate_record_good", "Good") : W(t, "reallife_estate_record_fair", "Fair");
            string chip = p[7] == "late6" ? W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "6") : p[7] == "late14" ? W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "14")
                : p[7] == "notice" ? W(t, "reallife_ibox_chip_notice", "Notice given") : W(t, "reallife_estate_state_paying", "Paying");
            UiRole tone = p[7] == "ok" ? UiRole.Ok : p[7] == "late14" ? UiRole.Danger : UiRole.Warn;
            rows.Add(Row(new[] { p[0], p[2], p[4], record, chip, p[6] }, new[] { (int)UiIcon.Home, 0, 0, p[5] == "good" ? 85 : 55, 0, int.Parse(p[6]) },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone, UiRole.None }, new[] { p[1], p[3], null, null, null, null }, UiColourHue(p[2])));
        }
        rows.Add(Row(new[] { "1 Dock Street, shop unit", W(t, "reallife_estate_tenant_shops_2", "A baker"), "$4,100", W(t, "reallife_estate_record_good", "Good"), W(t, "reallife_estate_filter_applicants", "Applicants"), string.Empty },
            new[] { (int)UiIcon.Store, 0, 0, 80, 0, -1 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok, UiRole.None }, new[] { "Uptown", Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "224"), null, null, null, null }));
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_unit", "Unit"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_tenant", "Tenant"), Kind = UiCellKind.Person, Width = 208f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rent_month", "Rent a month"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_record", "Record"), Kind = UiCellKind.Meter, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_satisfaction", "Satisfaction"), Kind = UiCellKind.Meter, Width = 128f }
        };
        TableOf(m, UiIcon.People, W(t, "reallife_estate_tab_tenants", "Tenants"), Fx(W(t, "reallife_ui_estate_tenants_sub", "{let} let, {empty} empty"), "let", "11", "empty", "1"), cols, rows, null,
            W(t, "reallife_estate_row_none_managed", "You let none yourself"), W(t, "reallife_ui_estate_empty_tenants_body", "The units you let yourself appear here with their tenants and the people who apply."));
        m.FigLabels = new[] { W(t, "reallife_estate_tenants_let", "Let"), W(t, "reallife_estate_tenants_empty", "Empty"), W(t, "reallife_estate_tenants_owed", "Owed"), W(t, "reallife_ui_estate_fig_mean_happy", "Mean satisfaction") };
        m.FigValues = new[] { "11", "1", "$8,800", "63%" };
        m.FigInks = new[] { UiRole.None, UiRole.WarnText, UiRole.DangerText, UiRole.None };
    }

    private static void Works(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>
        {
            Row(new[] { "41 E 37th Street, flat 4A", "Heating", W(t, "reallife_ibox_chip_open", "Open"), "$340", Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", "209") }, new[] { (int)UiIcon.Wrench, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.Warn, UiRole.None, UiRole.None }, new[] { "Murray Hill", null, null, null, null }),
            Row(new[] { "233 W 39th Street, unit 1", "A lock", W(t, "reallife_ibox_chip_booked", "Booked"), "$410", Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", "210") }, new[] { (int)UiIcon.Wrench, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.Ok, UiRole.None, UiRole.None }, new[] { "Garment District", null, null, null, null }),
            Row(new[] { "41 E 37th Street", Fx(W(t, "reallife_ui_estate_condition_level", "{condition}, level {level}"), "condition", "80%", "level", "1"), W(t, "reallife_ui_estate_sound", "Sound"), "$1,240", string.Empty },
                new[] { (int)UiIcon.Home, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.Ok, UiRole.None, UiRole.None }, new[] { "Homes, Murray Hill", null, null, null, null }),
            Row(new[] { "520 8th Avenue", Fx(W(t, "reallife_ui_estate_condition_level", "{condition}, level {level}"), "condition", "32%", "level", "0"), W(t, "reallife_ui_estate_unfit", "Unfit"), "$5,880", string.Empty },
                new[] { (int)UiIcon.Briefcase, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.Danger, UiRole.None, UiRole.None }, new[] { "Offices, Midtown", null, null, null, null }),
            Row(new[] { "88 W 46th Street, flat 2", "A lock", W(t, "reallife_ibox_chip_booked", "Booked"), "$280", Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", "211") }, new[] { (int)UiIcon.Wrench, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.Ok, UiRole.None, UiRole.None }, new[] { "Hell's Kitchen", null, null, null, null }),
            Row(new[] { "75 W 125th Street, house", "Boiler", W(t, "reallife_ibox_chip_open", "Open"), "$520", Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", "212") }, new[] { (int)UiIcon.Wrench, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.Warn, UiRole.None, UiRole.None }, new[] { "Uptown", null, null, null, null }),
            Row(new[] { "12 W 39th Street", Fx(W(t, "reallife_ui_estate_condition_level", "{condition}, level {level}"), "condition", "64%", "level", "1"), W(t, "reallife_ui_estate_sound", "Sound"), "$2,060", string.Empty },
                new[] { (int)UiIcon.Store, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.Ok, UiRole.None, UiRole.None }, new[] { "Shops, Garment District", null, null, null, null }),
            Row(new[] { "17 E 41st Street", Fx(W(t, "reallife_ui_estate_condition_level", "{condition}, level {level}"), "condition", "71%", "level", "2"), W(t, "reallife_ui_estate_sound", "Sound"), "$1,780", string.Empty },
                new[] { (int)UiIcon.Briefcase, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.Ok, UiRole.None, UiRole.None }, new[] { "Offices, Midtown", null, null, null, null })
        };
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_building", "Building"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_what", "What"), Kind = UiCellKind.Text, Width = 200f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 144f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_cost", "Cost"), Kind = UiCellKind.Money, Width = 120f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_day", "Day"), Kind = UiCellKind.Date, Width = 112f }
        };
        TableOf(m, UiIcon.Wrench, W(t, "reallife_estate_tab_works", "Works"), Fx(W(t, "reallife_ui_estate_works_sub", "{count} to look at"), "count", rows.Count.ToString()), cols, rows, null, W(t, "reallife_estate_row_nothing_owned", "You own no building"),
            W(t, "reallife_ui_estate_empty_works_body", "Repair calls and works on your buildings appear here."));
        m.FigLabels = new[] { W(t, "reallife_ui_estate_fig_open_calls", "Open calls"), W(t, "reallife_ui_estate_fig_booked", "Booked"), W(t, "reallife_ui_estate_fig_unfit", "Unfit to let"), W(t, "reallife_ui_estate_fig_calls_cost", "Calls cost") };
        m.FigValues = new[] { "2", "2", "1", "$1,550" };
        m.FigInks = new[] { UiRole.WarnText, UiRole.None, UiRole.DangerText, UiRole.None };
        m.RelatedTitle = W(t, "reallife_ui_estate_booked_visits", "Booked servicing");
        m.RelatedCount = "2";
        m.Related = new[]
        {
            new EstateRow { Icon = UiIcon.Calendar, Title = Fx(W(t, "reallife_ui_estate_service_name", "{fault} service"), "fault", "Heating"), Sub = "41 E 37th Street, Okonkwo Heating", Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "217"), ChipTone = UiRole.Notice, Amount = "$340", Run = -1 },
            new EstateRow { Icon = UiIcon.Calendar, Title = Fx(W(t, "reallife_ui_estate_service_name", "{fault} service"), "fault", "A lock"), Sub = "12 W 39th Street, Hartley Locks", Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "224"), ChipTone = UiRole.Notice, Amount = "$280", Run = -1 }
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
            new[] { "301 W 54th Street", "Homes, Hell's Kitchen", "$1,840,000", "80", "$7,420", "4.1%", "h" },
            new[] { "14 Pier Street", "Shops, Garment District", "$3,260,000", "64", "$15,300", "4.7%", "s" },
            new[] { "99 Broadway", "Offices, Midtown", "$12,400,000", "91", "$61,900", "5.0%", "o" },
            new[] { "6 Harlem Row, a house with a very long name that must be cut", "Homes, Uptown", "$980,000", "42", "$3,010", "3.1%", "h" },
            new[] { "412 10th Avenue", "Homes, Hell's Kitchen", "$2,150,000", "73", "$8,600", "4.8%", "h" },
            new[] { "88 Fulton Street", "Shops, Midtown", "$4,700,000", "58", "$19,400", "4.9%", "s" },
            new[] { "250 Park Avenue, floor 11", "Offices, Midtown", "$9,300,000", "86", "$44,200", "5.7%", "o" },
            new[] { "17 Mercer Street", "Homes, Murray Hill", "$1,420,000", "67", "$5,700", "4.2%", "h" },
            new[] { "3 Canal Row", "Shops, Garment District", "$2,880,000", "49", "$11,900", "5.0%", "s" },
            new[] { "60 W 57th Street", "Offices, Midtown", "$15,800,000", "93", "$72,500", "5.5%", "o" }
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
        rows.Add(Row(new[] { "Pier House Hotel", "$8,900,000", string.Empty, string.Empty, W(t, "reallife_estate_row_going_concern", "A going concern"), string.Empty }, new[] { (int)UiIcon.Building, 0, -1, 0, 0, (int)UiIcon.Locate },
            new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_row_hotel", "Hotel") + ", Midtown", null, null, null, null, null }));
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_building", "Building"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_price", "Price"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_condition", "Condition"), Kind = UiCellKind.Meter, Width = 136f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rent_month", "Rent a month"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_yield", "Yield"), Kind = UiCellKind.Figure, Width = 88f },
            new UiDataColumn { Title = string.Empty, Kind = UiCellKind.Action, Width = 56f }
        };
        TableOf(m, UiIcon.Tag, W(t, "reallife_estate_tab_market", "Market"), Fx(W(t, "reallife_ui_estate_market_sub", "{count} listings on the market"), "count", "612"), cols, rows, null,
            W(t, "reallife_estate_row_no_market", "Nothing is for sale"), W(t, "reallife_ui_estate_empty_market_body", "No building is for sale just now. New listings come with the game's market."));
        m.RowRuns = locate.ToArray();
        m.TableLink = W(t, "reallife_ui_estate_view_map", "View on the map");
        runs.Add(Yes);
        m.TableLinkRun = runs.Count - 1;
        m.FigLabels = new[] { W(t, "reallife_estate_market_level", "Market level"), W(t, "reallife_estate_market_prices", "Prices"), W(t, "reallife_ui_estate_fig_mean_yield", "Mean yield"), W(t, "reallife_ui_estate_fig_cycle", "The cycle") };
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
            ChartTitle = W(t, "reallife_ui_estate_rv_for_sale", "For sale by kind"), ChartNote = Fx(W(t, "reallife_ui_estate_rv_listings", "{count} listings"), "count", "612"),
            ChartLabels = new[] { W(t, "reallife_estate_use_homes", "Homes"), W(t, "reallife_estate_use_shops", "Shops"), W(t, "reallife_estate_use_offices", "Offices"), W(t, "reallife_estate_use_storage", "Storage") },
            ChartValues = new[] { "47%", "22%", "17%", "14%" }, ChartShares = new[] { 0.47f, 0.22f, 0.17f, 0.14f },
            LegendTitle = W(t, "reallife_ui_estate_rv_legend", "Map legend"), ScaleLabel = W(t, "reallife_ui_estate_rv_scale", "Rent per m2"), ScaleLow = "$19.40", ScaleHigh = "$38.50",
            RankTitle = W(t, "reallife_ui_estate_rv_districts", "Districts"), RankNote = W(t, "reallife_ui_estate_rv_rank_note", "rent per m2, dearest first"),
            RankLabels = new[] { "Midtown", "Garment District", "Murray Hill", "Hell's Kitchen", "Uptown" }, RankValues = new[] { "$38.50", "$34.80", "$31.20", "$27.90", "$19.40" }, RankFills = new[] { 100, 90, 81, 72, 50 }, RankSteps = new[] { 4, 4, 3, 2, 0 }
        };
        if (fresh)
        {
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
                Title = W(t, "reallife_estate_tab_agency", "Agency"), Heading = true,
                Lines = new[] { EstateStep(runs, W(t, "reallife_estate_agency_office", "Office"), "7 5th Avenue", null, false, true) },
                Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_open_agency", "Open the agency"), UiButtonKind.Primary, UiIcon.Key, UiAction.Alt) }
            };
            return;
        }
        var rows = new List<UiDataRow>();
        rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_sale", "A sale in {district}"), "district", "Midtown"), "$2,400,000", "$48,000", "3.5", W(t, "reallife_estate_stage_offered", "Offered"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "219") },
            new[] { (int)UiIcon.Tag, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Warn, UiRole.None }, new[] { W(t, "reallife_estate_filter_sales", "Sales"), null, null, null, null, null }));
        rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_letting", "A letting in {district}"), "district", "Uptown"), "$3,100", "$1,550", "1.2", W(t, "reallife_estate_stage_marketing", "Marketing"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "222") },
            new[] { (int)UiIcon.Key, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Notice, UiRole.None }, new[] { W(t, "reallife_estate_filter_lettings", "Lettings"), null, null, null, null, null }));
        rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_sale", "A sale in {district}"), "district", "Garment District"), "$1,150,000", "$23,000", "2.4", W(t, "reallife_estate_stage_marketing", "Marketing"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "226") },
            new[] { (int)UiIcon.Tag, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Notice, UiRole.None }, new[] { W(t, "reallife_estate_filter_sales", "Sales"), null, null, null, null, null }));
        rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_letting", "A letting in {district}"), "district", "Midtown"), "$4,800", "$2,400", "1.6", W(t, "reallife_estate_stage_offered", "Offered"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "220") },
            new[] { (int)UiIcon.Key, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Warn, UiRole.None }, new[] { W(t, "reallife_estate_filter_lettings", "Lettings"), null, null, null, null, null }));
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
                UiFactLine.Row(W(t, "reallife_estate_agency_office", "Office"), "7 5th Avenue"),
                UiFactLine.Row(W(t, "reallife_ui_estate_fig_deals", "Deals done"), "14"),
                UiFactLine.Row(W(t, "reallife_ui_estate_fig_lost", "Deals lost"), "3"),
                UiFactLine.Row(W(t, "reallife_ui_estate_fig_earned", "Commission earned"), "$212,400")
            },
            Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_close_agency", "Close the agency"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count) }
        };
        TableOf(m, UiIcon.Key, W(t, "reallife_estate_tab_agency", "Agency"), "7 5th Avenue", cols, rows, side, W(t, "reallife_estate_row_no_instructions", "No instructions"), W(t, "reallife_estate_agency_open_what", "Run the agency from an office you hold."));
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_instructions", "Open instructions"), "4"), Fig(W(t, "reallife_ui_estate_col_commission", "Commission"), "$74,950"),
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
    }

    private static void Hotel(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        string[][] hotels =
        {
            new[] { "Pier House Hotel", "4", "78", "$6,200", "$4,900", "$44,000", "$36,100" },
            new[] { "Canal Street Inn", "3", "60", "$2,700", "$2,300", "$19,600", "$17,500" },
            new[] { "Hartley Suites", "5", "71", "$9,800", "$6,100", "$71,400", "$52,900" }
        };
        var rows = new List<UiDataRow>();
        foreach (string[] h in hotels)
        {
            rows.Add(Row(new[] { h[0], h[2], h[3], h[4], h[5], h[6] }, new[] { (int)UiIcon.Building, int.Parse(h[2]), 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { Fx(W(t, "reallife_ibox_stars", "{stars} stars"), "stars", h[1]), null, null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_hotel", "Hotel"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rooms", "Rooms sold"), Kind = UiCellKind.Meter, Width = 160f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_takings", "Takings"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_costs", "Costs"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_takings_year", "Takings this year"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_costs_year", "Costs this year"), Kind = UiCellKind.Money, Width = 128f }
        };
        TableOf(m, UiIcon.Building, W(t, "reallife_estate_tab_hotel", "Hotel"), Fx(W(t, "reallife_ui_estate_hotels_sub", "{count} hotel(s) you run"), "count", "3"), cols, rows, null, W(t, "reallife_estate_row_no_hotel", "No hotel yet"),
            W(t, "reallife_ui_estate_empty_hotel_body", "A hotel you buy or lease from the Market tab is run from here."));
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_rooms_sold", "Rooms sold today"), "91 / 130"), Fig(W(t, "reallife_ui_estate_col_takings", "Takings"), "$18,700"),
            Fig(W(t, "reallife_ui_estate_col_costs", "Costs"), "$13,300"), Fig(W(t, "reallife_ui_estate_fig_kept_year", "Kept this year"), "$28,500")
        };
        string[] says = { "Clean rooms and a fair price", "Staff were kind, the lift was slow", "Too noisy at night" };
        string[] who = { "Pier House Hotel", "Pier House Hotel", "Canal Street Inn" };
        var reviews = new EstateRow[says.Length];
        for (int i = 0; i < reviews.Length; i++)
        {
            reviews[i] = new EstateRow { Icon = UiIcon.Star, Title = says[i], Sub = who[i], Run = -1 };
        }
        m.ShareTitle = W(t, "reallife_ui_estate_rooms_by_kind", "Rooms sold today, by kind");
        m.ShareLabels = new[] { "Pier House Hotel, double", "Pier House Hotel, suite", "Canal Street Inn, double", "Canal Street Inn, single" };
        m.ShareValues = new[] { 85, 62, 66, 48 };
        m.RelatedTitle = W(t, "reallife_ui_estate_guests_say", "Guests say");
        m.RelatedCount = Fx(W(t, "reallife_ui_estate_reviews_n", "{count} reviews"), "count", "3");
        m.Related = reviews;
    }

    private static void Hire(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>
        {
            Row(new[] { "Brisa roadster", W(t, "reallife_estate_car_out", "Out"), "$210", "14", "$2,940", Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "216") }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.Ok, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_premium", "Premium"), null, null, null, null, null }),
            Row(new[] { "Sora hatchback", W(t, "reallife_estate_car_idle", "In"), "$74", "31", "$2,294", string.Empty }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.Notice, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_economy", "Economy"), null, null, null, null, null }),
            Row(new[] { "Halden sedan", W(t, "reallife_estate_car_out", "Out"), "$88", "22", "$1,936", Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "214") }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.Ok, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_standard", "Standard"), null, null, null, null, null }),
            Row(new[] { "Atlas pickup", W(t, "reallife_estate_car_idle", "In"), "$102", "9", "$918", string.Empty }, new[] { (int)UiIcon.Truck, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.Notice, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_standard", "Standard"), null, null, null, null, null }),
            Row(new[] { "Courier van", W(t, "reallife_estate_row_on_lot", "On the lot"), "$96", string.Empty, string.Empty, string.Empty }, new[] { (int)UiIcon.Truck, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_standard", "Standard"), null, null, null, null, null })
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
            UiFactLine.Row(W(t, "reallife_estate_hire_lot", "The lot"), "88 W 46th Street"), EstateSwitch(runs, W(t, "reallife_estate_hire_cover", "Fleet cover"), true)
        };
        var side = new UiFactCardModel { Title = W(t, "reallife_estate_tab_hire", "Car hire"), Heading = true, Lines = lines.ToArray() };
        TableOf(m, UiIcon.Car, W(t, "reallife_estate_tab_hire", "Car hire"), "88 W 46th Street", cols, rows, side, W(t, "reallife_estate_row_no_cars", "No cars on hire"), W(t, "reallife_estate_hire_what", "Hire cars out from a lot."));
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_cars_out", "Cars out"), "2"), Fig(W(t, "reallife_ui_estate_col_hires", "Hires"), "76"),
            Fig(W(t, "reallife_ui_estate_col_takings", "Takings"), "$8,088"), Fig(W(t, "reallife_ui_estate_fig_mean_rate", "Mean rate a day"), "$114")
        };
        m.ShareTitle = W(t, "reallife_ui_estate_sh_lot", "The lot");
        m.ShareLabels = new[] { W(t, "reallife_ibox_spaces", "Spaces taken"), W(t, "reallife_ui_estate_sh_opinion", "Customers' opinion"), W(t, "reallife_ui_estate_sh_in_hire", "Cars in hire") };
        m.ShareValues = new[] { 25, 72, 33 };
    }

    private static void EstateDetail(UiWords t, EstateModel m, List<Func<bool>> runs, string state)
    {
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
                            EstateStep(runs, W(t, "reallife_estate_duty_who", "Who runs it"), W(t, "reallife_estate_manager_self", "Yourself"), W(t, "reallife_estate_duty_self_what", "You answer every call yourself."), false, true),
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
                        Lines = new[] { EstateStep(runs, W(t, "reallife_estate_own_selling", "Selling"), "100%", W(t, "reallife_estate_own_list_what", "Listed at {amount}, a sale comes about {chance} of the days, with a fee of {fee}.").Replace("{amount}", "$4,860,000").Replace("{chance}", "18%").Replace("{fee}", "1.5%")) },
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
                    new EstateRow { Icon = UiIcon.Wrench, Title = Fx(W(t, "reallife_ui_estate_service_name", "{fault} service"), "fault", "Heating"), Sub = Fx(W(t, "reallife_ui_estate_booked_with", "Booked with {who}"), "who", "Okonkwo Heating"), Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "217"), ChipTone = UiRole.Notice, Amount = "$340", Run = -1 },
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
                    Fig(W(t, "reallife_estate_listing_price", "Price"), "$1,840,000"), Fig(W(t, "reallife_ui_estate_fig_game_value", "The game's value"), "$1,720,000"),
                    Fig(W(t, "reallife_estate_market_level", "Market level"), "103%"), Fig(W(t, "reallife_estate_market_prices", "Prices"), "112%")
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
                m.ShareTitle = W(t, "reallife_ui_estate_sh_district_rents", "What the districts rent for, per m2");
                m.ShareLabels = new[] { "Midtown", "Garment District", "Murray Hill", "Hell's Kitchen", "Uptown" };
                m.ShareValues = new[] { 100, 90, 81, 72, 50 };
                m.ShareTexts = new[] { "$38.50", "$34.80", "$31.20", "$27.90", "$19.40" };

                m.RelatedTitle = Fx(W(t, "reallife_ui_estate_also_in", "Also for sale in {district}"), "district", "Hell's Kitchen");
                m.RelatedCount = "2";
                runs.Add(Yes);
                runs.Add(Yes);
                m.Related = new[]
                {
                    new EstateRow { Icon = UiIcon.Home, Title = "412 10th Avenue", Sub = W(t, "reallife_estate_use_homes", "Homes"), Chip = Fx(W(t, "reallife_ui_estate_yield_year", "{yield} a year"), "yield", "4.8%"), ChipTone = UiRole.Notice, Amount = "$2,150,000", Run = runs.Count - 2 },
                    new EstateRow { Icon = UiIcon.Home, Title = "9 W 43rd Street", Sub = W(t, "reallife_estate_use_homes", "Homes"), Chip = Fx(W(t, "reallife_ui_estate_yield_year", "{yield} a year"), "yield", "3.9%"), ChipTone = UiRole.Notice, Amount = "$1,310,000", Run = runs.Count - 1 }
                };
                break;
            case "works_page":
                m.Card = true;
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
                            EstateLink(runs, Fx(W(t, "reallife_ui_estate_service_line", "Cancel the {fault} service on day {day}, {who}, {amount}"), "fault", "Heating", "day", "217", "who", "Okonkwo Heating", "amount", "$340"))
                        }
                    }
                };
                m.GridTitle = W(t, "reallife_ui_estate_book_a_visit", "Book a visit");
                var book = new List<UiActionModel>();
                string[] faults = { "Heating", "A leak", "Electrics", "A lock", "Damp", "Fittings" };
                for (int i = 0; i < faults.Length; i++)
                {
                    book.Add(EstateButton(runs, Fx(W(t, "reallife_ui_estate_book_kind", "Book {fault}, {amount}"), "fault", faults[i], "amount", "$" + (180 + i * 40)), UiButtonKind.Secondary, UiIcon.Calendar, UiAction.Count, i == 0));
                }
                m.Grid = book.ToArray();
                m.RelatedTitle = W(t, "reallife_ui_estate_calls_here", "Calls on this building");
                m.RelatedCount = "2";
                runs.Add(Yes);
                runs.Add(Yes);
                m.Related = new[]
                {
                    new EstateRow { Face = "Sofia Marchetti", Hue = UiColourHue("Sofia Marchetti"), Title = "Heating, flat 4A", Sub = "Sofia Marchetti", Chip = W(t, "reallife_ibox_chip_open", "Open"), ChipTone = UiRole.Warn, Amount = "$340", Run = runs.Count - 2 },
                    new EstateRow { Face = "Priya Nair", Hue = UiColourHue("Priya Nair"), Title = "A lock, flat 3A", Sub = "Priya Nair", Chip = W(t, "reallife_ibox_chip_booked", "Booked"), ChipTone = UiRole.Ok, Amount = "$410", Run = runs.Count - 1 }
                };
                break;
            case "call":
                m.Tab = 2;
                m.DetailIcon = UiIcon.Wrench;
                m.DetailTitle = "41 E 37th Street, flat 4A";
                m.DetailSub = "Heating";
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
                    new EstateRow { Icon = UiIcon.Wrench, Title = Fx(W(t, "reallife_ui_estate_service_name", "{fault} service"), "fault", "Heating"), Sub = Fx(W(t, "reallife_ui_estate_booked_with", "Booked with {who}"), "who", "Okonkwo Heating"), Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "217"), ChipTone = UiRole.Notice, Amount = "$340", Run = -1 },
                    new EstateRow { Icon = UiIcon.Wrench, Title = W(t, "reallife_estate_improve_boiler", "a new boiler"), Sub = W(t, "reallife_ui_estate_asked_by_tenant", "Asked for by the tenant, you can say no"), Chip = W(t, "reallife_ui_estate_waiting_you", "Waiting for you"), ChipTone = UiRole.Warn, Amount = "$2,900", Run = -1 }
                };
                m.Related2Title = W(t, "reallife_ui_estate_record_title", "Rent record");
                m.Related2Count = Fx(W(t, "reallife_ui_estate_last_months", "last {count} months"), "count", "4");
                string[] due = { "208", "178", "148", "118" };
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
                        Lines = new[] { UiFactLine.Row(W(t, "reallife_ui_estate_term_lease", "A lease of"), Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", "150")), UiFactLine.Row(W(t, "reallife_ui_estate_term_until", "Waits until"), Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", "221")) },
                        Actions = new[]
                        {
                            EstateButton(runs, W(t, "reallife_estate_btn_refuse", "Refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                            EstateButton(runs, W(t, "reallife_estate_btn_accept", "Accept"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt)
                        }
                    }
                };
                m.Card = true;
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
                    new EstateRow { Face = "Ravi Shah", Hue = UiColourHue("Ravi Shah"), Title = "Ravi Shah", Sub = Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "224"), Chip = W(t, "reallife_estate_record_fair", "Fair"), ChipTone = UiRole.Warn, Amount = "$96", Run = runs.Count - 2 },
                    new EstateRow { Face = "Zoe Adler", Hue = UiColourHue("Zoe Adler"), Title = "Zoe Adler", Sub = Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "226"), Chip = W(t, "reallife_estate_record_good", "Good"), ChipTone = UiRole.Ok, Amount = "$91", Run = runs.Count - 1 }
                };
                break;
        }
    }
}
