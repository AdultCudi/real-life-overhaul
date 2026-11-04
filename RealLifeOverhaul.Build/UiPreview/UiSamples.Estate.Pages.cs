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

    private static void TableOf(EstateModel m, UiIcon icon, string title, string sub, UiDataColumn[] cols, List<UiDataRow> rows, UiFactCardModel side, string emptyTitle, string emptyBody)
    {
        m.Shape = EstateShape.Table;
        m.HeadIcon = icon;
        m.HeadTitle = title;
        m.HeadSub = sub;
        m.Table = new UiDataTableModel { Columns = cols, Rows = rows.ToArray(), RowCount = rows.Count, EmptyTitle = emptyTitle, EmptyBody = emptyBody, EmptyIcon = icon };
        m.RowKeys = new string[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            m.RowKeys[i] = "row:" + i;
        }
        m.Side = side;
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
            new[] { "41 E 37th Street, flat 3A", "Murray Hill", "Priya Nair", "Nurse, Murray Hill Clinic", "$2,600", "good", "82" },
            new[] { "41 E 37th Street, flat 4A", "Murray Hill", "Sofia Marchetti", "Nurse, Murray Hill Clinic", "$2,600", "fair", "51" },
            new[] { "88 W 46th Street, flat 2", "Hell's Kitchen", "Theo Brandt", "Courier, Eastside Parcels", "$2,200", "good", "85" },
            new[] { "75 W 125th Street, house", "Uptown", "Imani Cole", "Teacher, Lincoln School", "$3,100", "good", "90" },
            new[] { "41 E 37th Street, flat 3B", "Murray Hill", "Daniel Ortiz", "Cook, Pier Street Diner", "$3,300", "good", "74" },
            new[] { "12 W 39th Street, first floor office", "Garment District", "Marcus Bell", "Accountant, Bell and Rowe", "$3,900", "good", "77" },
            new[] { "17 E 41st Street, floor 6", "Midtown", "Owen Park", "Designer, Park and Lane", "$7,800", "good", "71" },
            new[] { "520 8th Avenue, suite 12", "Midtown", "Yuki Tanaka", "An import firm", "$5,600", "fair", "33" },
            new[] { "12 W 39th Street, ground floor shop", "Garment District", "Lena Hoffmann", "A tailor's shop", "$5,400", "good", "68" }
        };
        foreach (string[] p in people)
        {
            string record = p[5] == "good" ? W(t, "reallife_estate_record_good", "Good") : W(t, "reallife_estate_record_fair", "Fair");
            string chip = p[6] == "51" ? W(t, "reallife_ui_estate_late_days", "Late {days} days").Replace("{days}", "6") : W(t, "reallife_estate_state_paying", "Paying");
            rows.Add(Row(new[] { p[0], p[2], p[4], record, chip, p[6] }, new[] { (int)UiIcon.Home, 0, 0, p[5] == "good" ? 85 : 55, 0, int.Parse(p[6]) },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, p[6] == "51" ? UiRole.Warn : UiRole.Ok, UiRole.None }, new[] { p[1], p[3], null, null, null, null }, UiColourHue(p[2])));
        }
        rows.Add(Row(new[] { "233 W 39th Street, unit 1", W(t, "reallife_estate_tenant_shops_2", "A baker"), "$6,200", W(t, "reallife_estate_record_good", "Good"), W(t, "reallife_estate_filter_applicants", "Applicants"), string.Empty },
            new[] { (int)UiIcon.Store, 0, 0, 80, 0, -1 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok, UiRole.None }, new[] { "Garment District", Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "224"), null, null, null, null }));
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_unit", "Unit"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_tenant", "Tenant"), Kind = UiCellKind.Person, Width = 208f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rent_month", "Rent a month"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_record", "Record"), Kind = UiCellKind.Meter, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_satisfaction", "Satisfaction"), Kind = UiCellKind.Meter, Width = 128f }
        };
        var side = new UiFactCardModel
        {
            Title = W(t, "reallife_estate_tab_tenants", "Tenants"), Heading = true,
            Lines = new[]
            {
                UiFactLine.Row(W(t, "reallife_estate_tenants_let", "Let"), "11"), UiFactLine.Row(W(t, "reallife_estate_tenants_empty", "Empty"), "1", UiRole.WarnText), UiFactLine.Row(W(t, "reallife_estate_tenants_owed", "Owed"), "$8,800", UiRole.DangerText),
                UiFactLine.Say(Fx(W(t, "reallife_estate_tenants_notice_what", "A tenant {days} days behind may be given notice, which runs {notice} days, and then taken to court within {court}."), "days", "7", "notice", "14", "court", "10"))
            }
        };
        TableOf(m, UiIcon.People, W(t, "reallife_estate_tab_tenants", "Tenants"), Fx(W(t, "reallife_ui_estate_tenants_sub", "{let} let, {empty} empty"), "let", "11", "empty", "1"), cols, rows, side,
            W(t, "reallife_estate_row_none_managed", "You let none yourself"), W(t, "reallife_ui_estate_empty_tenants_body", "The units you let yourself appear here with their tenants and the people who apply."));
    }

    private static void Works(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>
        {
            Row(new[] { "41 E 37th Street, flat 4A", "Heating", W(t, "reallife_ibox_chip_open", "Open"), "$340", Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", "209") }, new[] { (int)UiIcon.Wrench, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.Warn, UiRole.None, UiRole.None }, new[] { "Murray Hill", null, null, null, null }),
            Row(new[] { "233 W 39th Street, unit 1", "A lock", W(t, "reallife_ibox_chip_booked", "Booked"), "$410", Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", "210") }, new[] { (int)UiIcon.Wrench, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.Ok, UiRole.None, UiRole.None }, new[] { "Garment District", null, null, null, null }),
            Row(new[] { "41 E 37th Street", Fx(W(t, "reallife_ui_estate_condition_level", "{condition}, level {level}"), "condition", "80%", "level", "1"), W(t, "reallife_ui_estate_sound", "Sound"), "$1,240", "-" },
                new[] { (int)UiIcon.Home, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.Ok, UiRole.None, UiRole.None }, new[] { "Homes, Murray Hill", null, null, null, null }),
            Row(new[] { "520 8th Avenue", Fx(W(t, "reallife_ui_estate_condition_level", "{condition}, level {level}"), "condition", "32%", "level", "0"), W(t, "reallife_ui_estate_unfit", "Unfit"), "$5,880", "-" },
                new[] { (int)UiIcon.Briefcase, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.Danger, UiRole.None, UiRole.None }, new[] { "Offices, Midtown", null, null, null, null }),
            Row(new[] { "88 W 46th Street, flat 2", "A lock", W(t, "reallife_ibox_chip_booked", "Booked"), "$280", Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", "211") }, new[] { (int)UiIcon.Wrench, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.Ok, UiRole.None, UiRole.None }, new[] { "Hell's Kitchen", null, null, null, null }),
            Row(new[] { "75 W 125th Street, house", "Boiler", W(t, "reallife_ibox_chip_open", "Open"), "$520", Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", "212") }, new[] { (int)UiIcon.Wrench, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.Warn, UiRole.None, UiRole.None }, new[] { "Uptown", null, null, null, null }),
            Row(new[] { "12 W 39th Street", Fx(W(t, "reallife_ui_estate_condition_level", "{condition}, level {level}"), "condition", "64%", "level", "1"), W(t, "reallife_ui_estate_sound", "Sound"), "$2,060", "-" },
                new[] { (int)UiIcon.Store, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.Ok, UiRole.None, UiRole.None }, new[] { "Shops, Garment District", null, null, null, null }),
            Row(new[] { "17 E 41st Street", Fx(W(t, "reallife_ui_estate_condition_level", "{condition}, level {level}"), "condition", "71%", "level", "2"), W(t, "reallife_ui_estate_sound", "Sound"), "$1,780", "-" },
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
        var side = new UiFactCardModel
        {
            Title = W(t, "reallife_ibox_sec_how", "How it works"), Heading = true,
            Lines = new[]
            {
                UiFactLine.Say(Fx(W(t, "reallife_estate_works_wear", "A building wears. Below {poor} it is poor, and below {unfit} it is unfit to let."), "poor", "50%", "unfit", "30%")),
                UiFactLine.Say(Fx(W(t, "reallife_estate_works_market", "Works cost more when the market is dear. It stands at {market}."), "market", "104%")), UiFactLine.Say(W(t, "reallife_estate_works_what", "Pick a building to repair it, refurbish it or change what it is used for."))
            }
        };
        TableOf(m, UiIcon.Wrench, W(t, "reallife_estate_tab_works", "Works"), Fx(W(t, "reallife_ui_estate_works_sub", "{count} to look at"), "count", "4"), cols, rows, side, W(t, "reallife_estate_row_nothing_owned", "You own no building"),
            W(t, "reallife_ui_estate_empty_works_body", "Repair calls and works on your buildings appear here."));
    }

    private static void Market(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>();
        string[][] listings =
        {
            new[] { "301 W 54th Street", "Homes, Hell's Kitchen", "$1,840,000", "80%", "$7,420", "4.1%", "h" },
            new[] { "14 Pier Street", "Shops, Garment District", "$3,260,000", "64%", "$15,300", "4.7%", "s" },
            new[] { "99 Broadway", "Offices, Midtown", "$12,400,000", "91%", "$61,900", "5.0%", "o" },
            new[] { "6 Harlem Row, a house with a very long name that must be cut", "Homes, Uptown", "$980,000", "42%", "$3,010", "3.1%", "h" },
            new[] { "412 10th Avenue", "Homes, Hell's Kitchen", "$2,150,000", "73%", "$8,600", "4.8%", "h" },
            new[] { "88 Fulton Street", "Shops, Midtown", "$4,700,000", "58%", "$19,400", "4.9%", "s" },
            new[] { "250 Park Avenue, floor 11", "Offices, Midtown", "$9,300,000", "86%", "$44,200", "5.7%", "o" },
            new[] { "17 Mercer Street", "Homes, Murray Hill", "$1,420,000", "67%", "$5,700", "4.2%", "h" },
            new[] { "3 Canal Row", "Shops, Garment District", "$2,880,000", "49%", "$11,900", "5.0%", "s" },
            new[] { "60 W 57th Street", "Offices, Midtown", "$15,800,000", "93%", "$72,500", "5.5%", "o" }
        };
        foreach (string[] l in listings)
        {
            rows.Add(Row(new[] { l[0], l[2], l[3], l[4], l[5] }, new[] { (int)(l[6] == "h" ? UiIcon.Home : l[6] == "s" ? UiIcon.Store : UiIcon.Briefcase), 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, l[3] == "42%" ? UiRole.Warn : UiRole.None, UiRole.None, UiRole.None }, new[] { l[1], null, null, null, null }));
        }
        rows.Add(Row(new[] { "Pier House Hotel", "$8,900,000", "-", "-", W(t, "reallife_estate_row_going_concern", "A going concern") }, new[] { (int)UiIcon.Building, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
            new[] { W(t, "reallife_estate_row_hotel", "Hotel") + ", Midtown", null, null, null, null }));
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_building", "Building"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_price", "Price"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_condition", "Condition"), Kind = UiCellKind.Text, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rent_month", "Rent a month"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_yield", "Yield"), Kind = UiCellKind.Figure, Width = 96f }
        };
        var side = new UiFactCardModel
        {
            Title = W(t, "reallife_estate_tab_market", "Market"), Heading = true, Chip = W(t, "reallife_estate_cycle_steady", "Steady"), ChipTone = UiRole.Notice,
            Lines = new[]
            {
                UiFactLine.Row(W(t, "reallife_estate_market_level", "Market level"), "103%"),
                UiFactLine.Say(Fx(W(t, "reallife_estate_market_heading", "The cycle is {cycle}. The market heads for {target}."), "cycle", W(t, "reallife_estate_cycle_steady", "Steady"), "target", "100%")),
                UiFactLine.Row(W(t, "reallife_estate_market_prices", "Prices"), "112%"), UiFactLine.Say(W(t, "reallife_estate_market_what", "Buildings are priced by the game and moved by the market."))
            }
        };
        TableOf(m, UiIcon.Tag, W(t, "reallife_estate_tab_market", "Market"), Fx(W(t, "reallife_ui_estate_market_sub", "{count} listings on the market"), "count", "612"), cols, rows, side,
            W(t, "reallife_estate_row_no_market", "Nothing is for sale"), W(t, "reallife_ui_estate_empty_market_body", "No building is for sale just now. New listings come with the game's market."));
        m.TableLink = W(t, "reallife_ui_estate_view_map", "View on the map");
        runs.Add(Yes);
        m.TableLinkRun = runs.Count - 1;
    }

    private static void Rents(UiWords t, EstateModel m, bool fresh)
    {
        m.Shape = EstateShape.Rents;
        string notYet = W(t, "reallife_ui_estate_not_yet", "Not yet");
        var v = new UiInfoViewModel
        {
            Title = W(t, "reallife_ui_estate_rv_title", "The city's rents"), Icon = UiIcon.Key, HeadLabel = W(t, "reallife_ui_estate_rv_rent", "Rent"), HeadValue = W(t, "reallife_ui_estate_rv_per_month", "per month, all units"),
            Rows = new[]
            {
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_mean_flat", "Mean rent, a flat"), Value = fresh ? notYet : "$2,860" },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_flat_sqm", "Mean rent per m2, flats"), Value = fresh ? notYet : "$34.20" },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_shop_sqm", "Shop rent per m2"), Value = fresh ? notYet : "$61.80" },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_office_sqm", "Office rent per m2"), Value = fresh ? notYet : "$48.10" },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_empty", "Units standing empty"), Value = fresh ? notYet : "4.8%", Ink = UiRole.WarnText },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_month", "Rents this month"), Value = fresh ? W(t, "reallife_ui_estate_rv_month_wait", "Not yet, after two months") : "+1.2%", Ink = fresh ? UiRole.None : UiRole.WarnText },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_your_let", "Your units, let"), Value = Fx(W(t, "reallife_ui_estate_of", "{value} of {most}"), "value", "11", "most", "12") },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_your_mean", "Your mean rent"), Value = "$3,960" }
            },
            ChartTitle = W(t, "reallife_ui_estate_rv_for_sale", "For sale by kind"), ChartNote = Fx(W(t, "reallife_ui_estate_rv_listings", "{count} listings"), "count", "612"),
            ChartLabels = new[] { W(t, "reallife_estate_use_homes", "Homes"), W(t, "reallife_estate_use_shops", "Shops"), W(t, "reallife_estate_use_offices", "Offices"), W(t, "reallife_estate_use_storage", "Storage") },
            ChartValues = new[] { "47%", "22%", "17%", "14%" }, ChartShares = new[] { 0.47f, 0.22f, 0.17f, 0.14f },
            LegendTitle = W(t, "reallife_ui_estate_rv_legend", "Map legend"), ScaleLabel = W(t, "reallife_ui_estate_rv_scale", "Rent per m2"), ScaleLow = "$19.40", ScaleHigh = "$38.50",
            RankTitle = W(t, "reallife_ui_estate_rv_districts", "Districts"), RankNote = W(t, "reallife_ui_estate_rv_rank_note", "rent per m2, dearest first"),
            RankLabels = new[] { "Midtown", "Garment District", "Murray Hill", "Hell's Kitchen", "Uptown" }, RankValues = new[] { "$38.50", "$34.80", "$31.20", "$27.90", "$19.40" }, RankFills = new[] { 100, 90, 81, 72, 50 }
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
        var rows = new List<UiDataRow>();
        if (!closed)
        {
            rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_sale", "A sale in {district}"), "district", "Midtown"), "$2,400,000", "$48,000", "3.5", W(t, "reallife_estate_stage_offered", "Offered"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "219") },
                new[] { (int)UiIcon.Tag, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Warn, UiRole.None }, new[] { W(t, "reallife_estate_filter_sales", "Sales"), null, null, null, null, null }));
            rows.Add(Row(new[] { Fx(W(t, "reallife_estate_row_letting", "A letting in {district}"), "district", "Uptown"), "$3,100", "$1,550", "1.2", W(t, "reallife_estate_stage_marketing", "Marketing"), Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "222") },
                new[] { (int)UiIcon.Key, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Notice, UiRole.None }, new[] { W(t, "reallife_estate_filter_lettings", "Lettings"), null, null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_instruction", "Instruction"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_value", "Value"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_commission", "Commission"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_hours", "Hours"), Kind = UiCellKind.Figure, Width = 80f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_day", "Day"), Kind = UiCellKind.Date, Width = 104f }
        };
        var lines = new List<UiFactLine>();
        var actions = new List<UiActionModel>();
        if (closed)
        {
            lines.Add(EstateStep(runs, W(t, "reallife_estate_agency_office", "Office"), "7 5th Avenue", null, false, true));
            lines.Add(UiFactLine.Say(W(t, "reallife_estate_agency_open_what", "Run the agency from an office you hold.")));
            actions.Add(EstateButton(runs, W(t, "reallife_estate_btn_open_agency", "Open the agency"), UiButtonKind.Primary, UiIcon.Key, UiAction.Alt));
        }
        else
        {
            lines.Add(UiFactLine.Row(W(t, "reallife_estate_agency_office", "Office"), "7 5th Avenue"));
            lines.Add(UiFactLine.Say(Fx(W(t, "reallife_estate_agency_staff", "{count} negotiators give {hours} hours a day."), "count", "2", "hours", "11.5")));
            lines.Add(UiFactLine.Section(W(t, "reallife_estate_agency_reputation", "What the districts think")));
            lines.Add(UiFactLine.Row("Midtown", "62%, " + Fx(W(t, "reallife_estate_agency_chance", "{chance} of instructions"), "chance", "14%")));
            lines.Add(UiFactLine.Row("Uptown", "48%, " + Fx(W(t, "reallife_estate_agency_chance", "{chance} of instructions"), "chance", "11%")));
            actions.Add(EstateButton(runs, W(t, "reallife_estate_btn_close_agency", "Close the agency"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count));
        }
        var side = new UiFactCardModel { Title = W(t, "reallife_estate_tab_agency", "Agency"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() };
        TableOf(m, UiIcon.Key, W(t, "reallife_estate_tab_agency", "Agency"), closed ? W(t, "reallife_estate_row_no_agency", "No agency yet") : "7 5th Avenue", cols, rows, side,
            closed ? W(t, "reallife_estate_row_no_agency", "No agency yet") : W(t, "reallife_estate_row_no_instructions", "No instructions"), W(t, "reallife_estate_agency_open_what", "Run the agency from an office you hold."));
    }

    private static void Hotel(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>
        {
            Row(new[] { "Pier House Hotel", Fx(W(t, "reallife_estate_row_sold", "{sold} of {rooms} rooms"), "sold", "31", "rooms", "40"), "$6,200", "$4,900", "$44,000", "$36,100" },
                new[] { (int)UiIcon.Building, 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { Fx(W(t, "reallife_ibox_stars", "{stars} stars"), "stars", "4"), null, null, null, null, null })
        };
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_hotel", "Hotel"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rooms", "Rooms sold"), Kind = UiCellKind.Text, Width = 144f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_takings", "Takings"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_costs", "Costs"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_takings_year", "Takings this year"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_costs_year", "Costs this year"), Kind = UiCellKind.Money, Width = 128f }
        };
        var side = new UiFactCardModel
        {
            Title = W(t, "reallife_estate_tab_hotel", "Hotel"), Heading = true,
            Lines = new[] { UiFactLine.Say(W(t, "reallife_estate_hotel_what", "A hotel you buy or lease is run from here.")), UiFactLine.Say(Fx(W(t, "reallife_estate_hotel_night_what", "A night fills about {share} of its rooms."), "share", "70%")) }
        };
        TableOf(m, UiIcon.Building, W(t, "reallife_estate_tab_hotel", "Hotel"), Fx(W(t, "reallife_ui_estate_hotels_sub", "{count} hotels you run"), "count", "1"), cols, rows, side, W(t, "reallife_estate_row_no_hotel", "No hotel yet"),
            W(t, "reallife_ui_estate_empty_hotel_body", "A hotel you buy or lease from the Market tab is run from here."));
    }

    private static void Hire(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>
        {
            Row(new[] { "Brisa roadster", W(t, "reallife_estate_car_out", "Out"), "$210", "14", "$2,940", Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", "216") }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.Ok, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_premium", "Premium"), null, null, null, null, null }),
            Row(new[] { "Sora hatchback", W(t, "reallife_estate_car_idle", "In"), "$74", "31", "$2,294", "-" }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.Notice, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_economy", "Economy"), null, null, null, null, null }),
            Row(new[] { "Courier van", W(t, "reallife_estate_row_on_lot", "On the lot"), "$96", "-", "-", "-" }, new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_class_standard", "Standard"), null, null, null, null, null })
        };
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_car", "Car"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rate_day", "Rate a day"), Kind = UiCellKind.Money, Width = 120f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_hires", "Hires"), Kind = UiCellKind.Figure, Width = 80f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_takings", "Takings"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_day", "Day"), Kind = UiCellKind.Date, Width = 104f }
        };
        var lines = new List<UiFactLine>
        {
            UiFactLine.Row(W(t, "reallife_estate_hire_lot", "The lot"), "88 W 46th Street"), UiFactLine.Bar(W(t, "reallife_ibox_spaces", "Spaces"), 25),
            UiFactLine.Say(Fx(W(t, "reallife_estate_hire_rating", "Customers rate it {rating}."), "rating", "72%")), EstateSwitch(runs, W(t, "reallife_estate_hire_cover", "Fleet cover"), true)
        };
        var side = new UiFactCardModel { Title = W(t, "reallife_estate_tab_hire", "Car hire"), Heading = true, Lines = lines.ToArray() };
        TableOf(m, UiIcon.Car, W(t, "reallife_estate_tab_hire", "Car hire"), "88 W 46th Street", cols, rows, side, W(t, "reallife_estate_row_no_cars", "No cars on hire"), W(t, "reallife_estate_hire_what", "Hire cars out from a lot."));
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
                m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ibox_sec_money", "Money"), Heading = true,
                        Lines = new[]
                        {
                            UiFactLine.Row(W(t, "reallife_ibox_worth", "Worth"), "$4,860,000"),
                            UiFactLine.Say(Fx(W(t, "reallife_estate_own_worth", "Worth {amount} today. Bought for {paid} on day {day}."), "amount", "$4,860,000", "paid", "$3,900,000", "day", "41")),
                            UiFactLine.Say(Fx(W(t, "reallife_estate_flip_gain", "Cost {paid} and {spent} of works. Sold today it would leave {amount}, a gain of {gain}."), "paid", "$3,900,000", "spent", "$120,000", "amount", "$4,520,000", "gain", "$500,000"), UiRole.OkText)
                        }
                    },
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ibox_sec_letting", "Letting"), Heading = true,
                        Lines = new[]
                        {
                            UiFactLine.Bar(W(t, "reallife_ibox_let", "Let"), 100), EstateLink(runs, "41 E 37th Street, flat 3A, Priya Nair, $2,600"), EstateLink(runs, "41 E 37th Street, flat 3B, Daniel Ortiz, $3,300"),
                            EstateLink(runs, "41 E 37th Street, flat 4A, Sofia Marchetti, $2,600"), UiFactLine.Row(W(t, "reallife_estate_duty_roll", "The roll"), Fx(W(t, "reallife_estate_row_a_day", "{amount} a day"), "amount", "$270")),
                            UiFactLine.Bar(W(t, "reallife_estate_duty_happy", "Satisfaction"), 63),
                            EstateStep(runs, W(t, "reallife_estate_duty_who", "Who runs it"), W(t, "reallife_estate_manager_self", "Yourself"), W(t, "reallife_estate_duty_self_what", "You answer every call yourself."), false, true),
                            EstateSwitch(runs, W(t, "reallife_estate_own_cover", "Landlord's cover"), true)
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
                    },
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ui_estate_more", "More"), Heading = true, Lines = new[] { EstateLink(runs, W(t, "reallife_ui_estate_works_link", "Works and servicing")), UiFactLine.Say(Fx(W(t, "reallife_estate_own_cover_what", "Cover costs a little a day and pays for a fire."), "amount", "$26", "excess", "10%")) }
                    }
                };
                break;
            case "listing":
                m.Tab = 3;
                m.DetailIcon = UiIcon.Home;
                m.DetailTitle = "301 W 54th Street";
                m.DetailSub = Fx(W(t, "reallife_ibox_type_in", "{type} in {district}"), "type", W(t, "reallife_estate_use_homes", "Homes"), "district", "Hell's Kitchen");
                m.DetailChip = "80%";
                m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ibox_sec_price_made", "What the price is made of"), Heading = true,
                        Lines = new[]
                        {
                            UiFactLine.Row(W(t, "reallife_estate_listing_price", "Price"), "$1,840,000"), UiFactLine.Say(Fx(W(t, "reallife_estate_listing_base", "The game values it at {amount}."), "amount", "$1,720,000")),
                            UiFactLine.Say(Fx(W(t, "reallife_estate_listing_market", "The property market stands at {market} and prices at {prices}."), "market", "103%", "prices", "112%"))
                        }
                    }
                };
                m.Right = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ibox_sec_letting", "Letting"), Heading = true,
                        Lines = new[] { UiFactLine.Say(Fx(W(t, "reallife_estate_listing_rent", "Let as {use} it would bring {amount} a day, a yield of {yield}."), "use", W(t, "reallife_estate_use_homes", "Homes"), "amount", "$7,420", "yield", "4.1%")) },
                        Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_offer", "Make an offer"), UiButtonKind.Primary, UiIcon.Handshake, UiAction.Alt) }
                    }
                };
                break;
            case "works_page":
                m.Tab = 2;
                m.DetailIcon = UiIcon.Wrench;
                m.DetailTitle = "41 E 37th Street";
                m.DetailSub = Fx(W(t, "reallife_ibox_type_in", "{type} in {district}"), "type", W(t, "reallife_estate_use_homes", "Homes"), "district", "Murray Hill");
                m.DetailChip = "80%";
                m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ibox_sec_works", "Works"), Heading = true,
                        Lines = new[]
                        {
                            UiFactLine.Say(Fx(W(t, "reallife_estate_works_cond", "Condition {condition}: worth {value} of its best, rents {rent} of it."), "condition", "80%", "value", "93%", "rent", "96%")),
                            UiFactLine.Say(Fx(W(t, "reallife_estate_works_repair", "A repair costs {amount} and takes {days} days."), "amount", "$24,800", "days", "5"))
                        },
                        Actions = new[]
                        {
                            EstateButton(runs, W(t, "reallife_estate_btn_refurbish", "Refurbish"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                            EstateButton(runs, W(t, "reallife_estate_btn_repair", "Repair"), UiButtonKind.Primary, UiIcon.Wrench, UiAction.Alt)
                        }
                    }
                };
                var svc = new List<UiFactLine>
                {
                    UiFactLine.Say(Fx(W(t, "reallife_ui_estate_service_what", "A booked visit keeps a kind of fault down for a season of {days} days: about {share} of its calls are not made. It is paid on its day."), "days", "90", "share", "70%")),
                    EstateLink(runs, Fx(W(t, "reallife_ui_estate_service_line", "Cancel the {fault} service on day {day}, {who}, {amount}"), "fault", "Heating", "day", "217", "who", "Okonkwo Heating", "amount", "$340"))
                };
                var book = new List<UiActionModel>();
                string[] faults = { "Heating", "A leak", "Electrics", "A lock", "Damp", "Fittings" };
                for (int i = 0; i < faults.Length; i++)
                {
                    book.Add(EstateButton(runs, Fx(W(t, "reallife_ui_estate_book_kind", "Book {fault}, {amount}"), "fault", faults[i], "amount", "$" + (180 + i * 40)), UiButtonKind.Secondary, UiIcon.Calendar, UiAction.Count, i == 0));
                }
                m.Right = new[] { new UiFactCardModel { Title = W(t, "reallife_ui_estate_servicing", "Booked servicing"), Heading = true, Lines = svc.ToArray(), Actions = book.ToArray() } };
                break;
            case "call":
                m.Tab = 2;
                m.DetailIcon = UiIcon.Wrench;
                m.DetailTitle = "41 E 37th Street, flat 4A";
                m.DetailSub = "Heating";
                m.DetailChip = W(t, "reallife_ibox_chip_open", "Open");
                m.DetailChipTone = UiRole.Danger;
                m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ibox_sec_quote", "The quote"), Heading = true,
                        Lines = new[]
                        {
                            UiFactLine.Say(Fx(W(t, "reallife_estate_call_since", "Reported on day {day}, {days} days ago. A call waits {grace} days before the tenant minds."), "day", "209", "days", "5", "grace", "3"), UiRole.WarnText),
                            UiFactLine.Say(Fx(W(t, "reallife_estate_call_contractor", "A contractor quotes {amount}."), "amount", "$340")),
                            UiFactLine.Say(Fx(W(t, "reallife_estate_call_yourself", "Doing it yourself costs {amount} of materials and {minutes} minutes."), "amount", "$119", "minutes", "120", "days", "3"))
                        },
                        Actions = new[]
                        {
                            EstateButton(runs, W(t, "reallife_estate_btn_yourself", "Do it yourself"), UiButtonKind.Secondary, UiIcon.MapPin, UiAction.Option),
                            EstateButton(runs, W(t, "reallife_estate_btn_contractor", "Send a contractor"), UiButtonKind.Primary, UiIcon.Wrench, UiAction.Alt)
                        }
                    }
                };
                break;
            default:
                m.Tab = 1;
                m.DetailIcon = UiIcon.People;
                m.DetailTitle = W(t, "reallife_estate_tenant_shops_2", "A baker");
                m.DetailSub = "233 W 39th Street, unit 1";
                m.DetailChip = W(t, "reallife_estate_record_good", "Good");
                m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = W(t, "reallife_ibox_sec_terms", "Terms"), Heading = true,
                        Lines = new[]
                        {
                            UiFactLine.Say(Fx(W(t, "reallife_estate_app_offer", "They offer {amount} a day against the market's {market}, {share} of it."), "amount", "$206", "market", "$214", "share", "96%")),
                            UiFactLine.Say(Fx(W(t, "reallife_estate_app_term", "A lease of {days} days with a deposit of {deposit}."), "days", "150", "deposit", "$1,236"))
                        },
                        Actions = new[]
                        {
                            EstateButton(runs, W(t, "reallife_estate_btn_refuse", "Refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                            EstateButton(runs, W(t, "reallife_estate_btn_accept", "Accept"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt)
                        }
                    }
                };
                m.Right = new[] { new UiFactCardModel { Lines = new[] { UiFactLine.Say(W(t, "reallife_estate_app_note", "An applicant waits until their last day. Refuse, and another may come.")) } } };
                break;
        }
    }
}
