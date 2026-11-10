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
        "hire", "rents", "rents_new", "space", "space_full", "off"
    };

    public static readonly string[] DealerStates =
    {
        "stock", "stock_empty", "stock_full", "market", "suppliers", "used", "auction", "auction_new", "offers", "deal", "finance", "growth", "event", "reports", "reports_new", "lots", "yard", "off"
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

    // ------------------------------------------------------------------------------------------------- what the pages say of the world

    private static string Mo(int perDay) => Usd(EstateFigures.Month(perDay, MonthDays));

    private static UiIcon UseGlyphOf(char use) => use == 's' ? UiIcon.Store : use == 'o' ? UiIcon.Briefcase : UiIcon.Home;

    private static string UseWordOf(UiWords t, char use) => W(t, "reallife_estate_use_" + (use == 's' ? "shops" : use == 'o' ? "offices" : "homes"), use == 's' ? "shops" : use == 'o' ? "offices" : "homes");

    private static string UnitLabel(UiWords t, EBuilding b, int unit) => b.Units.Count > 1 ? UnitName(t, b.Place, unit + 1) : b.Place;

    private static string RecordWord(UiWords t, int record) =>
        W(t, record >= 80 ? "reallife_estate_record_good" : record >= 60 ? "reallife_estate_record_fair" : record >= 40 ? "reallife_estate_record_patchy" : "reallife_estate_record_poor", "fair");

    private static string FaultNounOf(UiWords t, int kind) => W(t, "reallife_estate_fault_noun_" + kind, "fault");

    private static string FaultPhraseOf(UiWords t, int kind) => W(t, "reallife_estate_fault_" + kind, "a fault");

    private static string TenantWords(UiWords t, char use, int kind) =>
        W(t, "reallife_estate_tenant_" + (use == 's' ? "shops" : use == 'o' ? "offices" : "homes") + "_" + kind, "a tenant");

    private static string Percent(UiWords t, int percent) => Fx(W(t, "reallife_dealer_share", "{percent}%"), "percent", percent.ToString());

    private static UiRole HappyRingOf(int happy) => happy < 0 ? UiRole.None : happy >= 700 ? UiRole.Ok : happy >= 450 ? UiRole.Warn : UiRole.Danger;

    // The state of a let unit as a chip: what the landlord should see at a glance.
    private static string StateWord(UiWords t, EUnit u, out UiRole tone)
    {
        if (!u.Let)
        {
            tone = UiRole.Warn;
            return Fx(W(t, "reallife_estate_state_empty", "Empty {days} days"), "days", u.VoidDays.ToString());
        }
        if (u.Notice)
        {
            tone = UiRole.Warn;
            return W(t, "reallife_ibox_chip_notice", "Notice given");
        }
        if (u.Arrears > 0)
        {
            tone = u.ArrearsDays >= 10 ? UiRole.Danger : UiRole.Warn;
            return Fx(W(t, "reallife_ui_estate_late_days", "Late {days} days"), "days", u.ArrearsDays.ToString());
        }
        tone = UiRole.Ok;
        return W(t, "reallife_ui_estate_paid", "Paid");
    }

    private static string WhoLine(UiWords t, EUnit u) => u.Let ? JobLine(t, u.Tenant.Job) : W(t, "reallife_ui_estate_asking_note", "Looking for a tenant");

    private static UiDataRow UnitRow(UiWords t, EBuilding b, int n)
    {
        EUnit u = b.Units[n];
        string chip = StateWord(t, u, out UiRole tone);
        int happy = u.Let && u.Happy >= 0 ? u.Happy / 10 : -1;
        return new UiDataRow
        {
            Cells = new[] { UnitLabel(t, b, n), u.Let ? u.Tenant.Name : W(t, "reallife_estate_row_nobody", "Nobody"),
                u.Let ? Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", u.Until.ToString()) : "-", u.Let ? Mo(u.RentDay) : Mo(u.AskDay), chip, happy >= 0 ? happy.ToString() : string.Empty },
            Values = new[] { (int)UseGlyphOf(b.Use), u.Let ? 0 : UiDataTable.GlyphFace(UiIcon.People), 0, 0, 0, happy },
            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, u.Let ? UiRole.None : UiRole.Muted, tone, UiRole.None },
            Subs = new[] { b.District, WhoLine(t, u), null, null, null, null }, Hue = u.Let ? UiColourHue(u.Tenant.Name) : 0f,
            Ring = u.Let ? HappyRingOf(u.Happy) : UiRole.None
        };
    }

    // A building the game lets: its own row, its income a month in the muted ink since it is not part of what is due.
    private static UiDataRow GameRow(UiWords t, EBuilding b) => new UiDataRow
    {
        Cells = new[] { b.Place, W(t, "reallife_estate_kind_building", "building"), "-", Mo(b.GameNight), b.Self ? W(t, "reallife_estate_state_self", "you use it") : W(t, "reallife_estate_state_game", "let by the game"), string.Empty },
        Values = new[] { (int)UseGlyphOf(b.Use), 0, 0, 0, 0, -1 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.Muted, UiRole.None, UiRole.None },
        Subs = new[] { b.District, UseWordOf(t, b.Use), null, null, null, null }
    };

    private static UiDataRow OtherRow(UiIcon icon, string name, string line, int perDay, string kind, UiRole tone) => new UiDataRow
    {
        Cells = new[] { name, kind, "-", perDay != 0 ? Mo(perDay) : "-", line, string.Empty }, Values = new[] { (int)icon, 0, 0, 0, 0, -1 },
        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, tone == UiRole.None ? UiRole.Muted : tone, UiRole.None, UiRole.None }, Subs = new[] { kind, string.Empty, null, null, null, null }
    };

    private static EstateModel EstateBase(UiWords t, EstateWorldData w)
    {
        int units = 0, let = 0, calls = 0;
        foreach (EBuilding b in w.Buildings)
        {
            if (!b.Managed)
            {
                units++;
                continue;
            }
            foreach (EUnit u in b.Units)
            {
                units++;
                let += u.Let ? 1 : 0;
                calls += u.Let && u.Call > 0 ? 1 : 0;
            }
        }
        string[] counts =
        {
            units > 0 ? units.ToString() : null, let > 0 ? let.ToString() : null, calls > 0 ? calls.ToString() : null, null, w.Agency && w.Instructions.Count > 0 ? w.Instructions.Count.ToString() : null,
            w.Hotels.Count > 0 ? w.Hotels.Count.ToString() : null, w.Fleet.Count > 0 ? w.Fleet.Count.ToString() : null
        };
        return new EstateModel { Title = W(t, "reallife_estate_app", "Property"), Icon = UiIcon.Home, Nav = EstateNav(t, counts), Prompts = EstatePrompts(t) };
    }

    public static EstateModel Estate(UiWords t, string state)
    {
        var runs = new List<Func<bool>>();
        string variant = state == "roll_twenty" ? "twenty" : state == "roll_first" || state == "rents_new" ? "first" : state == "roll_empty" ? "empty" : "std";
        EstateWorldData w = EstateWorld(variant);
        EstateModel m = EstateBase(t, w);
        switch (state)
        {
            case "off":
                m.Nav = new UiNavEntry[0];
                m.Shape = EstateShape.Off;
                m.OffIcon = UiIcon.Home;
                m.OffTitle = W(t, "reallife_estate_off_title", "The Property app is off");
                m.OffBody = W(t, "reallife_estate_off_body", "Turn it on in Mod settings, or load a city to see your property.");
                m.OffAction = W(t, "reallife_ui_estate_close", "Close");
                break;
            case "roll":
            case "roll_twenty":
            case "roll_first":
            case "roll_empty":
                Roll(t, m, runs, w);
                break;
            case "unit_late":
                UnitOf(t, m, runs, w, w.Buildings[0], 2);
                break;
            case "unit_request":
                UnitOf(t, m, runs, w, w.Buildings[0], 1);
                break;
            case "unit_empty":
                UnitOf(t, m, runs, w, w.Buildings[5], 0);
                break;
            case "tenants":
                m.Tab = 1;
                Tenants(t, m, runs, w);
                break;
            case "works":
                m.Tab = 2;
                Works(t, m, runs, w);
                break;
            case "market":
                m.Tab = 3;
                Market(t, m, runs, w);
                break;
            case "rents":
            case "rents_new":
                m.Tab = 3;
                Rents(t, m, w, state == "rents_new");
                break;
            case "agency":
            case "agency_closed":
                m.Tab = 4;
                Agency(t, m, runs, w, state == "agency_closed");
                break;
            case "hotel":
                m.Tab = 5;
                Hotel(t, m, runs, w);
                break;
            case "hire":
                m.Tab = 6;
                Hire(t, m, runs, w);
                break;
            case "space":
            case "space_full":
                m.Tab = 3;
                SpaceSample(t, m, runs, state == "space_full");   // a building's car park (UiSamples.BizAsks.cs)
                break;
            default:
                EstateDetail(t, m, runs, w, state);
                break;
        }
        m.Runs = runs.ToArray();
        return m;
    }

    // ------------------------------------------------------------------------------------------------- the rent roll

    private static void Roll(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w)
    {
        var rows = new List<UiDataRow>();
        int units = 0, buildings = 0;
        foreach (EBuilding b in w.Buildings)
        {
            buildings++;
            if (!b.Managed)
            {
                rows.Add(GameRow(t, b));
                units++;
                continue;
            }
            for (int n = 0; n < b.Units.Count; n++)
            {
                units++;
                rows.Add(UnitRow(t, b, n));
            }
        }
        foreach (EHotel h in w.Hotels)
        {
            rows.Add(OtherRow(UiIcon.Building, h.Name, W(t, "reallife_estate_row_hotel_owned", "hotel, owned"), h.Takings - h.Costs, W(t, "reallife_estate_kind_hotel", "hotel"), h.Takings < h.Costs ? UiRole.Danger : UiRole.None));
        }
        if (w.Fleet.Count > 0)
        {
            int outNow = 0;
            foreach (ECar c in w.Fleet)
            {
                outNow += c.Out ? 1 : 0;
            }
            rows.Add(OtherRow(UiIcon.Car, w.HireLot, Fx(W(t, "reallife_estate_row_lot", "hire lot, {out} of {count} out"), "out", outNow.ToString(), "count", w.Fleet.Count.ToString()), 0, W(t, "reallife_estate_kind_lot", "hire lot"), UiRole.None));
        }
        RollTotals roll = EstateFigures.Roll(RollLines(w), MonthDays);
        int waiting = WaitingOf(w);
        m.Shape = EstateShape.Roll;
        m.HeadIcon = UiIcon.Home;
        m.HeadTitle = W(t, "reallife_ui_estate_roll_title", "The rent roll");
        m.HeadSub = waiting > 0 ? Fx(W(t, "reallife_ui_estate_roll_sub", "{units} unit(s) in {buildings} building(s), {waiting} waiting for you"), "units", units.ToString(), "buildings", buildings.ToString(), "waiting", waiting.ToString())
            : Fx(W(t, "reallife_ui_estate_roll_sub_clear", "{units} unit(s) in {buildings} building(s), nothing waiting for you"), "units", units.ToString(), "buildings", buildings.ToString());
        m.HeadSub = w.Buildings.Count == 0 ? null : m.HeadSub;
        m.FigLabels = new[] { W(t, "reallife_ui_estate_fig_due", "Due"), W(t, "reallife_ui_estate_fig_collected", "Collected"), W(t, "reallife_ui_estate_fig_arrears", "In arrears"), W(t, "reallife_ui_estate_fig_empty", "Empty, asking") };
        m.FigValues = new[] { Usd(roll.Due), Usd(roll.Collected), Usd(roll.Owed), Usd(roll.EmptyAsking) };
        m.FigInks = new[] { UiRole.None, UiRole.None, roll.Owed > 0 ? UiRole.DangerText : UiRole.None, roll.EmptyAsking > 0 ? UiRole.WarnText : UiRole.None };
        var table = new UiDataTableModel
        {
            Columns = EstateRollColumns(t), Rows = rows.ToArray(), RowCount = rows.Count,
            EmptyTitle = W(t, "reallife_estate_row_nothing_owned", "You own no building"), EmptyBody = W(t, "reallife_ui_estate_empty_roll_body", "The buildings you own appear here, a line for each unit, with its tenant and its rent."), EmptyIcon = UiIcon.Home
        };
        if (rows.Count > 0)
        {
            table.TotalsLabel = Fx(W(t, "reallife_ui_estate_total_let", "{count} unit(s) let"), "count", roll.Let.ToString());
            table.Totals = new[] { string.Empty, string.Empty, string.Empty, Usd(roll.Due), string.Empty, string.Empty };
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
        // What is due for repair: a building's booked services, then its units' calls, the first three as the live page keeps them.
        var repairs = new List<EstateRow>();
        foreach (EBuilding b in w.Buildings)
        {
            foreach (EService v in b.Services)
            {
                repairs.Add(new EstateRow { Icon = UiIcon.Wrench, Title = Fx(W(t, "reallife_ui_estate_service_title", "{fault}, {building}"), "fault", Capital(FaultNounOf(t, v.Kind)), "building", b.Place),
                    Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", v.Day.ToString()), ChipTone = UiRole.Notice, Amount = Usd(v.Fee), Run = -1 });
            }
            for (int n = 0; b.Managed && n < b.Units.Count; n++)
            {
                EUnit u = b.Units[n];
                if (!u.Let || u.Call <= 0)
                {
                    continue;
                }
                repairs.Add(new EstateRow { Icon = UiIcon.Wrench, Title = Fx(W(t, "reallife_ui_estate_service_title", "{fault}, {building}"), "fault", Capital(FaultNounOf(t, u.Call)), "building", UnitLabel(t, b, n)),
                    Chip = u.CallBooked ? W(t, "reallife_ibox_chip_booked", "Booked") : W(t, "reallife_ui_estate_quote_asked", "Quote asked"), ChipTone = u.CallBooked ? UiRole.Ok : UiRole.Warn, Amount = Usd(u.CallCost), Run = -1 });
            }
        }
        m.ListA = repairs.Count > 3 ? repairs.GetRange(0, 3).ToArray() : repairs.ToArray();
        bool fresh = w.Buildings.Count == 0;   // a new save has no other landlord and no month of rents kept yet
        var lords = new List<ELandlord>(fresh ? new List<ELandlord>() : w.Landlords);
        lords.Sort((a, b) => b.Units.CompareTo(a.Units));
        var landlordRows = new List<EstateRow>();
        for (int i = 0; i < lords.Count && landlordRows.Count < 3; i++)
        {
            ELandlord l = lords[i];
            landlordRows.Add(new EstateRow
            {
                Face = l.Name, Hue = UiColourHue(l.Name), Title = Fx(W(t, "reallife_ui_estate_landlord_line", "{name}, {units} units, flats at the market rent of {rent}"), "name", l.Name, "units", l.Units.ToString(), "rent", Usd(l.MeanMonth)),
                Amount = (l.ChangeTenths >= 0 ? "+" : string.Empty) + (l.ChangeTenths / 10f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%", AmountInk = l.ChangeTenths > 0 ? UiRole.OkText : l.ChangeTenths < 0 ? UiRole.WarnText : UiRole.None, Run = -1
            });
        }
        m.ListB = landlordRows.ToArray();
        // The city's mean flat rent, a month at a time, from the ring.
        var months = new string[w.RentRing.Length];
        var rents = new float[w.RentRing.Length];
        for (int i = 0; i < months.Length; i++)
        {
            months[i] = Fx(W(t, "reallife_ui_estate_month_n", "Month {month}"), "month", (9 + i).ToString());
            rents[i] = w.RentRing[i];
        }
        m.Lower = fresh ? null : EstateCharts.Line(W(t, "reallife_ui_estate_ring_chart", "The city's rents, a month at a time"), months, rents, W(t, "reallife_ui_estate_rv_mean_flat", "Mean rent, a flat"), EstateCharts.Dollars);
        runs.Add(Yes);
        runs.Add(Yes);
    }

    // ------------------------------------------------------------------------------------------------- one unit

    private static void UnitOf(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w, EBuilding b, int n)
    {
        EUnit u = b.Units[n];
        int units = Math.Max(1, b.Units.Count);
        m.Shape = EstateShape.Unit;
        m.Back = W(t, "reallife_co_back", "Back");
        var p = new EstateUnitPage { Title = UnitLabel(t, b, n), District = b.District, Locate = W(t, "reallife_ui_estate_locate", "Locate"), Icon = UseGlyphOf(b.Use) };
        runs.Add(Yes);
        p.LocateRun = runs.Count - 1;
        p.Rent = u.Let ? Mo(u.RentDay) : Mo(u.AskDay);
        p.RentPer = W(t, "reallife_ui_estate_a_month", "a month");
        p.NextLabel = W(t, "reallife_ui_estate_next_inspection", "Next inspection");
        p.NextValue = Fx(W(t, "reallife_ui_estate_in_days", "in {days} days"), "days", b.InspectIn.ToString());
        p.NextFill = Math.Max(0, Math.Min(100, 100 - b.InspectIn * 100 / 30));
        int sqm = 40 + u.MarketDay * 3 / 5;
        p.FactLabels = new[] { W(t, "reallife_ui_estate_fact_bedrooms", "Bedrooms"), W(t, "reallife_ui_estate_fact_area", "Area"), W(t, "reallife_ui_estate_lease_ends", "Lease ends"), W(t, "reallife_ui_estate_deposit_held", "Deposit held"),
            W(t, "reallife_ui_estate_last_inspected", "Last inspected"), W(t, "reallife_ui_estate_fact_market", "On the market, a month") };
        p.FactValues = new[] { (1 + sqm / 40).ToString(), Fx(W(t, "reallife_ui_estate_sqm", "{area} m2"), "area", sqm.ToString()), Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", u.Until.ToString()), Usd(u.RentDay * 6),
            Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", (Today + b.InspectIn - 28).ToString()), Mo(u.MarketDay) };
        p.BuildingLink = W(t, "reallife_ui_estate_the_building", "The building");
        runs.Add(Yes);
        p.BuildingRun = runs.Count - 1;
        // The other units of the building, each with its tenant and what they pay.
        var others = new List<EstateRow>();
        for (int k = 0; k < b.Units.Count; k++)
        {
            EUnit o = b.Units[k];
            if (k == n || !o.Let)
            {
                continue;
            }
            runs.Add(Yes);
            others.Add(new EstateRow { Face = o.Tenant.Name, Hue = UiColourHue(o.Tenant.Name), Title = o.Tenant.Name, Sub = UnitLabel(t, b, k), Chip = StateWord(t, o, out UiRole tone), ChipTone = tone, Amount = Mo(o.RentDay), Run = runs.Count - 1 });
        }
        p.OthersTitle = W(t, "reallife_ui_estate_other_units", "Your other units here");
        p.OthersCount = others.Count > 0 ? others.Count.ToString() : null;
        p.Others = others.ToArray();
        p.YearTitle = W(t, "reallife_ui_estate_year_title", "This unit over the tax year");
        p.YearLabels = new[] { W(t, "reallife_ui_estate_year_rent", "Rent due, this year"), W(t, "reallife_ui_estate_year_works", "Works and repairs") };
        long rentYear = b.RentYear / units, costYear = b.CostYear / units;
        p.YearValues = new[] { Usd(rentYear), (costYear > 0 ? "-" : string.Empty) + Usd(costYear) };
        p.YearKeptLabel = W(t, "reallife_ui_estate_kept", "Kept");
        p.YearKept = Usd(rentYear - costYear);
        // The rent record, the running month first when rent is owed.
        var record = new List<EstateRecordRow>();
        if (u.Let && u.Arrears > 0)
        {
            record.Add(new EstateRecordRow
            {
                Due = Fx(W(t, "reallife_ui_estate_due_day", "Due day {day}"), "day", (Today - u.ArrearsDays).ToString()), What = W(t, "reallife_ui_estate_not_paid_yet", "Not paid yet"),
                Chip = Fx(W(t, "reallife_ui_estate_late_days", "Late {days} days"), "days", u.ArrearsDays.ToString()), ChipTone = u.ArrearsDays >= 10 ? UiRole.Danger : UiRole.Warn, Amount = Usd(u.Arrears)
            });
        }
        foreach (EPay pay in u.Pays)
        {
            record.Add(new EstateRecordRow
            {
                Due = Fx(W(t, "reallife_ui_estate_due_day", "Due day {day}"), "day", pay.Due.ToString()),
                What = pay.State == 2 ? W(t, "reallife_ui_estate_not_paid_yet", "Not paid yet") : Fx(W(t, "reallife_ui_estate_paid_on", "Paid on day {day}"), "day", pay.Paid.ToString()),
                Chip = pay.State == 0 ? W(t, "reallife_ui_estate_paid", "Paid") : pay.State == 1 ? W(t, "reallife_ui_estate_paid_late", "Paid late") : W(t, "reallife_ui_estate_missed", "Missed"),
                ChipTone = pay.State == 0 ? UiRole.Ok : pay.State == 1 ? UiRole.Warn : UiRole.Danger, Amount = Mo(u.Let ? u.RentDay : Math.Max(1, u.MarketDay))
            });
            if (record.Count >= 6 + (u.Arrears > 0 ? 1 : 0))
            {
                break;
            }
        }
        // The works on the unit: the building's booked services, its call, and an improvement the tenant asked for.
        var works = new List<EstateRow>();
        foreach (EService v in b.Services)
        {
            works.Add(new EstateRow { Icon = UiIcon.Wrench, Title = ServiceTitle(t, v.Kind), Sub = Fx(W(t, "reallife_ui_estate_booked_with", "Booked with {who}"), "who", v.Who),
                Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", v.Day.ToString()), ChipTone = UiRole.Notice, Amount = Usd(v.Fee), Run = -1 });
        }
        if (u.Let && u.Call > 0)
        {
            runs.Add(Yes);
            works.Add(new EstateRow { Icon = UiIcon.Wrench, Title = Capital(FaultNounOf(t, u.Call)), Sub = Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", u.CallDay.ToString()),
                Chip = u.CallBooked ? W(t, "reallife_ibox_chip_booked", "Booked") : W(t, "reallife_ibox_chip_open", "Open"), ChipTone = u.CallBooked ? UiRole.Ok : UiRole.Warn, Amount = Usd(u.CallCost), Run = runs.Count - 1 });
        }
        if (u.Let && u.Improve > 0)
        {
            works.Add(new EstateRow { Icon = UiIcon.Wrench, Title = W(t, "reallife_estate_improve_boiler", "a new boiler"), Sub = W(t, "reallife_ui_estate_asked_by_tenant", "Asked for by the tenant, you can say no"),
                Chip = W(t, "reallife_ui_estate_waiting_you", "Waiting for you"), ChipTone = UiRole.Warn, Amount = Usd(u.ImproveCost), Run = -1 });
        }
        p.WorksTitle = W(t, "reallife_ui_estate_works_here", "Works on this unit");
        p.Works = works.ToArray();
        p.WorksCount = works.Count > 0 ? works.Count.ToString() : null;
        if (!u.Let)
        {
            EmptyUnitOf(t, p, runs, w, b, u, record);
            m.Unit = p;
            return;
        }
        p.TenantTitle = W(t, "reallife_ui_estate_the_tenant", "The tenant");
        p.TenantName = u.Tenant.Name;
        p.TenantHue = UiColourHue(u.Tenant.Name);
        p.TenantRing = HappyRingOf(u.Happy);
        p.TenantLine = JobLine(t, u.Tenant.Job) + " · " + u.Tenant.Age + " · " + Fx(W(t, "reallife_ui_estate_here_since", "here since day {day}"), "day", u.Since.ToString());
        UiRole chipTone = UiRole.Warn;
        p.TenantChip = u.Arrears > 0 || u.Notice ? StateWord(t, u, out chipTone) : null;
        p.TenantChipTone = chipTone;
        p.TenantOpen = W(t, "reallife_ui_estate_open_page", "Open their page");
        runs.Add(Yes);
        p.TenantOpenRun = runs.Count - 1;
        p.MeterLabels = new[] { W(t, "reallife_ui_estate_m_satisfaction", "Satisfaction"), W(t, "reallife_ui_estate_m_repairs", "Repairs"), W(t, "reallife_ui_estate_m_value", "Value for rent"), W(t, "reallife_ui_estate_m_condition", "Condition") };
        p.MeterValues = new[] { u.Happy / 10, u.Repairs, u.ValueForRent, b.Cond };
        p.RecordTitle = W(t, "reallife_ui_estate_record_title", "Rent record");
        p.RecordNote = Fx(W(t, "reallife_ui_estate_last_months", "last {count} months"), "count", record.Count.ToString());
        p.Record = record.ToArray();
        // What needs the landlord, the most pressing first: a tenant's request, rent that is late, a repair call waiting.
        string first = u.Tenant.Name.Split(' ')[0];
        if (u.Improve > 0)
        {
            p.NeedKicker = Fx(W(t, "reallife_ui_estate_needs_you", "Needs you, {what}"), "what", W(t, "reallife_ui_estate_need_request", "a request"));
            p.NeedHead = Fx(W(t, "reallife_ui_estate_improve_head", "{name} asks for {what}, {amount}."), "name", first, "what", W(t, "reallife_estate_improve_boiler", "a new boiler"), "amount", Usd(u.ImproveCost));
            p.NeedBody = Fx(W(t, "reallife_ui_estate_improve_body", "You can say no, but refusing costs satisfaction and makes them likelier to leave. Refused so far in this lease: {refusals}."), "refusals", u.Refused.ToString());
            p.NeedActions = new[]
            {
                EstateButton(runs, W(t, "reallife_ui_estate_make_it", "Make it"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt),
                EstateButton(runs, W(t, "reallife_estate_btn_refuse", "Refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option)
            };
        }
        else if (u.Arrears > 0)
        {
            p.NeedKicker = Fx(W(t, "reallife_ui_estate_needs_you", "Needs you, {what}"), "what", W(t, "reallife_ui_estate_need_rent", "rent late"));
            p.NeedHead = Fx(W(t, "reallife_ui_estate_late_head", "{name} is {days} days late with {amount}."), "name", first, "days", u.ArrearsDays.ToString(), "amount", Usd(u.Arrears));
            p.NeedBody = Fx(W(t, "reallife_estate_ten_arrears", "{amount} is owed, {days} days of it. They catch up on about {back} of the days."), "amount", Usd(u.Arrears), "days", u.ArrearsDays.ToString(),
                "back", Percent(t, u.Record - 15));
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

    // A unit with nobody in: what it asks, whether applicants are looked for, the people who asked and what a tenant would weigh.
    private static void EmptyUnitOf(UiWords t, EstateUnitPage p, List<Func<bool>> runs, EstateWorldData w, EBuilding b, EUnit u, List<EstateRecordRow> record)
    {
        int chance = Math.Max(10, 170 - (u.Ask - 1000));
        p.FactLabels = new[] { p.FactLabels[1], p.FactLabels[4] };
        p.FactValues = new[] { p.FactValues[1], p.FactValues[4] };
        p.Others = new EstateRow[0];
        p.OthersCount = null;
        p.RecordTitle = W(t, "reallife_ui_estate_record_last_tenant", "The last tenant's rent record");
        p.RecordNote = Fx(W(t, "reallife_ui_estate_last_months", "last {count} months"), "count", record.Count.ToString());
        p.Record = record.ToArray();
        var lines = new List<UiFactLine>
        {
            EstateStep(runs, W(t, "reallife_estate_ten_asking", "Asking rent"), Percent(t, (u.Ask + 5) / 10),
                Fx(W(t, "reallife_estate_ten_asking_what", "Asks {amount} a day, about {chance} chance of an applicant a day"), "amount", Usd(u.AskDay), "chance", Percent(t, (chance + 5) / 10))),
            EstateSwitch(runs, W(t, "reallife_estate_ten_to_let", "Look for applicants"), u.ToLet)
        };
        p.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_asking_day", "Asking, a day"), Usd(u.AskDay)), Fig(W(t, "reallife_ui_estate_fig_market_day", "Market rent, a day"), Usd(u.MarketDay)),
            Fig(W(t, "reallife_ui_estate_fig_days_empty", "Days empty"), u.VoidDays.ToString())
        };
        p.ShareTitle = W(t, "reallife_ui_estate_sh_weigh", "What a tenant would weigh");
        p.ShareLabels = new[] { W(t, "reallife_ui_estate_m_repairs", "Repairs"), W(t, "reallife_ui_estate_m_value", "Value for rent"), W(t, "reallife_ui_estate_m_condition", "Condition") };
        p.ShareValues = new[] { u.Repairs, u.ValueForRent, b.Cond };
        p.ApplicantsTitle = W(t, "reallife_ui_estate_applicants_title", "Applicants");
        p.ApplicantsCount = u.Applicants.Count > 0 ? u.Applicants.Count.ToString() : null;
        var waiting = new List<EstateRow>();
        foreach (EApplicant a in u.Applicants)
        {
            runs.Add(Yes);
            string who = TenantWords(t, b.Use, a.Kind);
            waiting.Add(new EstateRow
            {
                Face = who, Hue = UiColourHue(who), Title = who, Sub = Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", a.Lapse.ToString()), Chip = RecordWord(t, a.Record),
                ChipTone = a.Record >= 60 ? UiRole.Ok : UiRole.Warn, Amount = Usd(a.Offer), Run = runs.Count - 1
            });
        }
        p.Applicants = waiting.ToArray();
        p.Empty = new UiFactCardModel { Title = Fx(W(t, "reallife_ui_estate_empty_title", "Empty for {days} days"), "days", u.VoidDays.ToString()), Chip = W(t, "reallife_ibox_chip_empty", "Empty"), ChipTone = UiRole.Warn, Lines = lines.ToArray() };
    }
}
