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
    private static string ServiceTitle(UiWords t, int fault, string english) => Capital(Fx(W(t, "reallife_ui_estate_service_name", "{fault} service"), "fault", W(t, "reallife_estate_fault_noun_" + fault, english)));

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
    private static UiIcon UseGlyph(string use) => use == "s" ? UiIcon.Store : use == "o" ? UiIcon.Briefcase : UiIcon.Home;

    // A unit of a building of several the way the live page names it, through the locale's key: the pt and zh shots say "unidade 2" and "2号单元".
    private static string UnitName(UiWords t, string building, int unit) => Fx(W(t, "reallife_estate_unit_name", "{building}, unit {unit}"), "building", building, "unit", unit.ToString());

    private static string ServiceTitle(UiWords t, int kind) => ServiceTitle(t, kind, "fault");

    // What a repair of the building costs and takes, by its condition: the one rule the works table, the building's works page and the roll's unfit count read.
    private static int RepairOf(EBuilding b) => (int)((long)b.Value * (100 - b.Cond) / 80000);

    private static int RepairDays(EBuilding b) => 3 + (100 - b.Cond) / 10;

    private static int WorthOfBest(int cond) => 100 - (100 - cond) * 35 / 100;

    private static int RentsOfBest(int cond) => 100 - (100 - cond) * 20 / 100;

    private static string ConditionWord(UiWords t, int cond, out UiRole tone)
    {
        tone = cond < 40 ? UiRole.Danger : cond < 60 ? UiRole.Warn : UiRole.Ok;
        return cond < 40 ? W(t, "reallife_ui_estate_unfit", "Unfit") : cond < 60 ? W(t, "reallife_ui_estate_poor", "Poor") : W(t, "reallife_ui_estate_sound", "Sound");
    }

    private static int CatchUp(int record) => Math.Max(5, record - 2);

    private static string Money2(long amount) => Usd(amount);

    // ------------------------------------------------------------------------------------------------- the tenants

    private static void Tenants(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w)
    {
        var rows = new List<UiDataRow>();
        var needs = new List<EstateRow>();
        var happy = new List<int>();
        int let = 0, empty = 0, owed = 0;
        foreach (EBuilding b in w.Buildings)
        {
            if (!b.Managed)
            {
                continue;
            }
            for (int n = 0; n < b.Units.Count; n++)
            {
                EUnit u = b.Units[n];
                let += u.Let ? 1 : 0;
                empty += u.Let ? 0 : 1;
                owed += u.Arrears;
                happy.Add(u.Let ? u.Happy : -1);
                string chip = u.Let && u.Arrears == 0 && !u.Notice ? W(t, "reallife_estate_state_paying", "Paying") : StateWord(t, u, out UiRole tone0);
                UiRole tone = StateTone(u);
                int sat = u.Let && u.Happy >= 0 ? u.Happy / 10 : -1;
                rows.Add(Row(new[] { UnitLabel(t, b, n), u.Let ? u.Tenant.Name : W(t, "reallife_estate_row_nobody", "Nobody"), u.Let ? Mo(u.RentDay) : "-", u.Let ? RecordWord(t, u.Record) : "-", chip, sat >= 0 ? sat.ToString() : string.Empty },
                    new[] { (int)UseGlyphOf(b.Use), u.Let ? 0 : UiDataTable.GlyphFace(UiIcon.People), 0, u.Let ? u.Record : -1, 0, sat },
                    new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone, UiRole.None }, new[] { b.District, u.Let ? JobLine(t, u.Tenant.Job) : null, null, null, null, null },
                    u.Let ? UiColourHue(u.Tenant.Name) : 0f));
                if (u.Let && u.Arrears > 0)
                {
                    runs.Add(Yes);
                    int open = runs.Count - 1;
                    runs.Add(Yes);
                    needs.Add(new EstateRow
                    {
                        Face = u.Tenant.Name, Hue = UiColourHue(u.Tenant.Name), Title = u.Tenant.Name, Sub = UnitLabel(t, b, n), Chip = StateWord(t, u, out UiRole lateTone), ChipTone = lateTone,
                        Button = W(t, "reallife_estate_btn_remind", "Send a reminder"), ButtonRun = runs.Count - 1, Run = open
                    });
                }
                foreach (EApplicant a in u.Applicants)
                {
                    string who = TenantWords(t, b.Use, a.Kind);
                    rows.Add(Row(new[] { UnitLabel(t, b, n), who, Mo(a.Offer), RecordWord(t, a.Record), W(t, "reallife_estate_filter_applicants", "Applicants"), string.Empty },
                        new[] { (int)UseGlyphOf(b.Use), 0, 0, a.Record, 0, -1 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok, UiRole.None },
                        new[] { b.District, Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", a.Lapse.ToString()), null, null, null, null }, UiColourHue(who)));
                }
            }
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_unit", "Unit"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_tenant", "Tenant"), Kind = UiCellKind.Person, Width = 264f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rent_month", "Rent a month"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_record", "Record"), Kind = UiCellKind.Meter, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 144f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_satisfaction", "Satisfaction"), Kind = UiCellKind.Meter, Width = 128f }
        };
        int mean = EstateFigures.MeanHappy(happy);
        TableOf(m, UiIcon.People, W(t, "reallife_estate_tab_tenants", "Tenants"), Fx(W(t, "reallife_ui_estate_tenants_sub", "{let} let, {empty} empty"), "let", let.ToString(), "empty", empty.ToString()), cols, rows, null,
            W(t, "reallife_estate_row_none_managed", "You let none yourself"), W(t, "reallife_ui_estate_empty_tenants_body", "The units you let yourself appear here with their tenants and the people who apply."));
        m.FigLabels = new[] { W(t, "reallife_ui_estate_fig_let", "Let"), W(t, "reallife_ui_estate_fig_empty_units", "Empty"), W(t, "reallife_ui_estate_fig_rent_owed", "Rent owed to you"), W(t, "reallife_ui_estate_fig_mean_happy", "Mean satisfaction") };
        m.FigValues = new[] { let.ToString(), empty.ToString(), Usd(owed), mean >= 0 ? (mean / 10f).ToString("0.#", InvD) + "%" : string.Empty };
        m.FigInks = new[] { UiRole.None, empty > 0 ? UiRole.WarnText : UiRole.None, owed > 0 ? UiRole.DangerText : UiRole.None, UiRole.None };
        // The late payers, with the reminder to send: what needs the landlord now, from the same units as the roll's red and amber chips.
        m.FootFirst = true;
        m.RelatedTitle = W(t, "reallife_ui_estate_needs_you_list", "Needs you");
        m.RelatedCount = needs.Count.ToString();
        m.Related = needs.ToArray();
    }

    private static UiRole StateTone(EUnit u)
    {
        StateWordTone(u, out UiRole tone);
        return tone;
    }

    private static void StateWordTone(EUnit u, out UiRole tone)
    {
        tone = !u.Let ? UiRole.Warn : u.Notice ? UiRole.Warn : u.Arrears > 0 ? (u.ArrearsDays >= 10 ? UiRole.Danger : UiRole.Warn) : UiRole.Ok;
    }

    // ------------------------------------------------------------------------------------------------- the works

    private static void Works(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w)
    {
        var rows = new List<UiDataRow>();
        var locate = new List<int>();
        int open = 0, booked = 0, unfit = 0, callsCost = 0;
        foreach (EBuilding b in w.Buildings)
        {
            for (int n = 0; b.Managed && n < b.Units.Count; n++)
            {
                EUnit u = b.Units[n];
                if (!u.Let || u.Call <= 0)
                {
                    continue;
                }
                runs.Add(Yes);
                locate.Add(runs.Count - 1);
                open += u.CallBooked ? 0 : 1;
                booked += u.CallBooked ? 1 : 0;
                callsCost += u.CallCost;
                rows.Add(Row(new[] { UnitLabel(t, b, n), Capital(FaultNounOf(t, u.Call)), string.Empty, u.CallBooked ? W(t, "reallife_ibox_chip_booked", "Booked") : W(t, "reallife_ibox_chip_open", "Open"), Usd(u.CallCost),
                        Fx(W(t, "reallife_estate_row_since", "Since day {day}"), "day", u.CallDay.ToString()), string.Empty },
                    new[] { (int)UiIcon.Wrench, 0, -1, 0, 0, 0, (int)UiIcon.Locate }, new[] { UiRole.None, UiRole.None, UiRole.None, u.CallBooked ? UiRole.Ok : UiRole.Warn, UiRole.None, UiRole.None, UiRole.None },
                    new[] { b.District, null, null, null, null, null, null }));
            }
        }
        foreach (EBuilding b in w.Buildings)
        {
            runs.Add(Yes);
            locate.Add(runs.Count - 1);
            string state = ConditionWord(t, b.Cond, out UiRole tone);
            unfit += b.Cond < 40 ? 1 : 0;
            rows.Add(Row(new[] { b.Place, Fx(W(t, "reallife_ui_estate_level_n", "Level {level}"), "level", b.Level.ToString()), b.Cond.ToString(), state, RepairOf(b) > 0 ? Usd(RepairOf(b)) : "-", "-", string.Empty },
                new[] { (int)UseGlyphOf(b.Use), 0, b.Cond, 0, 0, 0, (int)UiIcon.Locate }, new[] { UiRole.None, UiRole.None, UiRole.None, tone, UiRole.None, UiRole.None, UiRole.None },
                new[] { UseWordOf(t, b.Use) + ", " + b.District, null, null, null, null, null, null }));
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
        m.FigValues = new[] { open.ToString(), booked.ToString(), unfit.ToString(), Usd(callsCost) };
        m.FigInks = new[] { open > 0 ? UiRole.WarnText : UiRole.None, UiRole.None, unfit > 0 ? UiRole.DangerText : UiRole.None, UiRole.None };
        // The services booked ahead, the soonest first, and the city's inspections, the nearest first.
        var services = new List<KeyValuePair<EBuilding, EService>>();
        foreach (EBuilding b in w.Buildings)
        {
            foreach (EService v in b.Services)
            {
                services.Add(new KeyValuePair<EBuilding, EService>(b, v));
            }
        }
        services.Sort((a, b) => a.Value.Day.CompareTo(b.Value.Day));
        var booking = new List<EstateRow>();
        for (int i = 0; i < services.Count && booking.Count < 3; i++)
        {
            EService v = services[i].Value;
            booking.Add(new EstateRow { Icon = UiIcon.Calendar, Title = ServiceTitle(t, v.Kind), Sub = services[i].Key.Place + ", " + v.Who, Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", v.Day.ToString()),
                ChipTone = UiRole.Notice, Amount = Usd(v.Fee), Run = -1 });
        }
        m.RelatedTitle = W(t, "reallife_ui_estate_booked_visits", "Booked servicing");
        m.RelatedCount = booking.Count > 0 ? booking.Count.ToString() : null;
        m.Related = booking.ToArray();
        var due = new List<EBuilding>();
        foreach (EBuilding b in w.Buildings)
        {
            if (b.InspectIn > 0)
            {
                due.Add(b);
            }
        }
        due.Sort((a, b) => a.InspectIn.CompareTo(b.InspectIn));
        var inspections = new List<EstateRow>();
        for (int i = 0; i < due.Count && inspections.Count < 3; i++)
        {
            inspections.Add(new EstateRow
            {
                Icon = UiIcon.Eye, Title = due[i].Place, Sub = due[i].InspectPassed ? W(t, "reallife_ui_estate_last_passed", "The last one passed") : W(t, "reallife_ui_estate_last_failed", "The last one failed"),
                Chip = Fx(W(t, "reallife_ui_estate_in_days", "in {days} days"), "days", due[i].InspectIn.ToString()), ChipTone = due[i].InspectPassed ? UiRole.Notice : UiRole.Warn, Run = -1
            });
        }
        m.Related2Title = W(t, "reallife_ui_estate_inspections", "The city's inspections");
        m.Related2Count = inspections.Count > 0 ? inspections.Count.ToString() : null;
        m.Related2 = inspections.ToArray();
    }

    // ------------------------------------------------------------------------------------------------- the market

    private static string YieldWords(long rentDay, long price) => (EstateFigures.YieldHundredths(rentDay, DaysPerYear, price) / 100f).ToString("0.0", InvD) + "%";

    private static void Market(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w)
    {
        var rows = new List<UiDataRow>();
        var locate = new List<int>();
        foreach (EListing l in w.Market)
        {
            runs.Add(Yes);
            locate.Add(runs.Count - 1);
            rows.Add(Row(new[] { l.Place, Usd(l.Price), l.Cond.ToString(), Mo(l.RentDay), YieldWords(l.RentDay, l.Price), string.Empty }, new[] { (int)UseGlyphOf(l.Use), 0, l.Cond, 0, 0, (int)UiIcon.Locate },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { UseWordOf(t, l.Use) + ", " + l.District, null, null, null, null, null }));
        }
        foreach (EListing h in w.HotelsForSale)
        {
            runs.Add(Yes);
            locate.Add(runs.Count - 1);
            rows.Add(Row(new[] { h.Place, Usd(h.Price), string.Empty, string.Empty, W(t, "reallife_estate_row_going_concern", "going concern"), string.Empty }, new[] { (int)UiIcon.Building, 0, -1, 0, 0, (int)UiIcon.Locate },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_estate_row_hotel", "hotel") + ", " + h.District, null, null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_building", "Building"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_price", "Price"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_condition", "Condition"), Kind = UiCellKind.Meter, Width = 136f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rent_month", "Rent a month"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_yield", "Yield"), Kind = UiCellKind.Figure, Width = 88f },
            new UiDataColumn { Title = string.Empty, Kind = UiCellKind.Action, Width = 56f }
        };
        TableOf(m, UiIcon.Tag, W(t, "reallife_estate_tab_market", "Market"), Fx(W(t, "reallife_ui_estate_market_sub", "{count} listings on the market"), "count", rows.Count.ToString()), cols, rows, null,
            W(t, "reallife_estate_row_no_market", "Nothing is for sale"), W(t, "reallife_ui_estate_empty_market_body", "No building is for sale just now. New listings come with the game's market."));
        m.RowRuns = locate.ToArray();
        m.TableLink = W(t, "reallife_ui_estate_view_map", "View on the map");
        runs.Add(Yes);
        m.TableLinkRun = runs.Count - 1;
        m.FigLabels = new[] { W(t, "reallife_ui_estate_fig_market_level", "Property market"), W(t, "reallife_ui_estate_fig_prices_since", "Prices since the start"), W(t, "reallife_ui_estate_fig_cycle", "The cycle") };
        m.FigValues = new[] { (w.MarketLevel / 10f).ToString("0.#", InvD) + "%", (w.PricesSince / 10f).ToString("0.#", InvD) + "%", W(t, "reallife_estate_cycle_steady", "Steady") };
        // What the districts rent for per m2: the same read the rents map ranks, as meters with their figure.
        var ranked = new List<EDistrict>(w.Districts);
        ranked.Sort((a, b) => b.PerSqm.CompareTo(a.PerSqm));
        m.ShareTitle = W(t, "reallife_ui_estate_sh_district_rents", "What the districts rent for, per m2");
        m.ShareLabels = new string[ranked.Count];
        m.ShareValues = new int[ranked.Count];
        m.ShareTexts = new string[ranked.Count];
        for (int i = 0; i < ranked.Count; i++)
        {
            m.ShareLabels[i] = ranked[i].Name;
            m.ShareValues[i] = ranked[i].PerSqm * 100 / ranked[0].PerSqm;
            m.ShareTexts[i] = "$" + (ranked[i].PerSqm / 100f).ToString("0.00", InvD);
        }
    }

    private static void Rents(UiWords t, EstateModel m, EstateWorldData w, bool fresh)
    {
        m.Shape = EstateShape.Rents;
        string notYet = W(t, "reallife_ui_estate_not_yet", "Not yet");
        var ranked = new List<EDistrict>(w.Districts);
        ranked.Sort((a, b) => b.PerSqm.CompareTo(a.PerSqm));
        int sqmFlat = 0;
        foreach (EDistrict d in ranked)
        {
            sqmFlat += d.PerSqm;
        }
        sqmFlat /= Math.Max(1, ranked.Count);
        // Your own units: how many are let of all, and what a let one pays a month on average.
        RollTotals mine = EstateFigures.Roll(RollLines(w), MonthDays);
        long yourMean = mine.Let > 0 ? mine.Due / mine.Let : 0;
        int forSale = w.Market.Count + w.HotelsForSale.Count, homes = 0, shops = 0, offices = 0;
        foreach (EListing l in w.Market)
        {
            homes += l.Use == 'h' ? 1 : 0;
            shops += l.Use == 's' ? 1 : 0;
            offices += l.Use == 'o' ? 1 : 0;
        }
        int[] counts = { homes, shops, offices, w.HotelsForSale.Count };
        var v = new UiInfoViewModel
        {
            Title = W(t, "reallife_ui_estate_rv_title", "The city's market rents"), Icon = UiIcon.Key, Views = new[] { UiIcon.Home, UiIcon.Store, UiIcon.Briefcase }, View = 0, HeadLabel = W(t, "reallife_ui_estate_rv_rent", "Rent"),
            HeadValue = W(t, "reallife_ui_estate_rv_per_month", "per month, all units"),
            Rows = new[]
            {
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_mean_flat", "Mean market rent, a flat"), Value = fresh ? notYet : Usd(w.CityFlatRent) },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_flat_sqm", "Mean rent per m2, flats"), Value = fresh ? notYet : "$" + (sqmFlat / 100f).ToString("0.00", InvD) },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_shop_sqm", "Shop rent per m2"), Value = fresh ? notYet : "$" + (sqmFlat * 18 / 1000f).ToString("0.00", InvD) },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_office_sqm", "Office rent per m2"), Value = fresh ? notYet : "$" + (sqmFlat * 14 / 1000f).ToString("0.00", InvD) },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_empty", "Units standing empty"), Value = fresh ? notYet : "4.8%", Ink = fresh ? UiRole.None : UiRole.WarnText },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_month", "Rents this month"), Value = fresh ? W(t, "reallife_ui_estate_rv_month_wait", "Not yet, after two months") : (w.RentMonthChange >= 0 ? "+" : string.Empty) + (w.RentMonthChange / 10f).ToString("0.#", InvD) + "%", Ink = fresh ? UiRole.None : UiRole.OkText },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_your_let", "Your units, let"), Value = Fx(W(t, "reallife_ui_estate_of", "{value} of {most}"), "value", mine.Let.ToString(), "most", mine.Units.ToString()) },
                new UiInfoRow { Label = W(t, "reallife_ui_estate_rv_your_mean", "Your mean rent"), Value = Usd(yourMean) }
            },
            ChartTitle = W(t, "reallife_ui_estate_rv_for_sale", "For sale by kind"), ChartNote = Fx(W(t, "reallife_ui_estate_rv_listings", "{count} listings"), "count", forSale.ToString()),
            ChartLabels = new[] { W(t, "reallife_estate_use_homes", "Homes"), W(t, "reallife_estate_use_shops", "Shops"), W(t, "reallife_estate_use_offices", "Offices"), W(t, "reallife_estate_row_hotel", "Hotel") },
            ChartValues = new string[4], ChartShares = new float[4],
            LegendTitle = W(t, "reallife_ui_estate_rv_legend", "Map legend"), ScaleLabel = W(t, "reallife_ui_estate_rv_scale", "Rent per m2"),
            ScaleLow = "$" + (ranked[ranked.Count - 1].PerSqm / 100f).ToString("0.00", InvD), ScaleHigh = "$" + (ranked[0].PerSqm / 100f).ToString("0.00", InvD),
            RankTitle = W(t, "reallife_ui_estate_rv_districts", "Districts"), RankNote = W(t, "reallife_ui_estate_rv_rank_note", "rent per m2, dearest first"),
            RankLabels = new string[ranked.Count], RankValues = new string[ranked.Count], RankFills = new int[ranked.Count], RankSteps = new int[ranked.Count]
        };
        for (int i = 0; i < 4; i++)
        {
            v.ChartShares[i] = counts[i] / (float)Math.Max(1, forSale);
            v.ChartValues[i] = Math.Round(counts[i] * 100.0 / Math.Max(1, forSale)).ToString("0", InvD) + "%";
        }
        int low = ranked[ranked.Count - 1].PerSqm, high = ranked[0].PerSqm;
        for (int i = 0; i < ranked.Count; i++)
        {
            v.RankLabels[i] = ranked[i].Name;
            v.RankValues[i] = "$" + (ranked[i].PerSqm / 100f).ToString("0.00", InvD);
            v.RankFills[i] = ranked[i].PerSqm * 100 / high;
            v.RankSteps[i] = (int)Math.Round((ranked[i].PerSqm - low) * 4.0 / Math.Max(1, high - low));
        }
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

    // ------------------------------------------------------------------------------------------------- the agency

    private static void Agency(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w, bool closed)
    {
        if (closed)
        {
            // Nothing is run yet: no empty table with its headers, but what the section is and the card that opens it, together.
            m.Shape = EstateShape.Empty;
            m.Nav[4].Count = null;   // an agency that is not run has no instructions to count
            m.OffIcon = UiIcon.Key;
            m.OffTitle = W(t, "reallife_estate_row_no_agency", "No agency yet");
            m.OffBody = W(t, "reallife_estate_agency_open_what", "Run the agency from an office you hold.");
            m.Side = new UiFactCardModel
            {
                Lines = new[] { EstateStep(runs, W(t, "reallife_ui_estate_fig_office", "Office"), w.Office, null, false, true) },
                Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_open_agency", "Open the agency"), UiButtonKind.Primary, UiIcon.Key, UiAction.Alt) }
            };
            return;
        }
        var rows = new List<UiDataRow>();
        int pending = 0;
        foreach (EInstruction n in w.Instructions)
        {
            pending += n.Commission;
            string stage = W(t, n.Stage == 0 ? "reallife_estate_stage_offered" : n.Stage == 1 ? "reallife_estate_stage_marketing" : "reallife_estate_stage_completing", "stage");
            rows.Add(Row(new[] { Fx(W(t, n.Sale ? "reallife_estate_row_sale" : "reallife_estate_row_letting", "An instruction in {district}"), "district", n.District), Usd(n.Value), Usd(n.Commission), (n.Hours / 10f).ToString("0.0", InvD), stage,
                    Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", n.Day.ToString()) },
                new[] { (int)(n.Sale ? UiIcon.Tag : UiIcon.Key), 0, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, n.Stage == 0 ? UiRole.Warn : n.Stage == 1 ? UiRole.Notice : UiRole.Ok, UiRole.None },
                new[] { W(t, n.Sale ? "reallife_estate_filter_sales" : "reallife_estate_filter_lettings", "Instructions"), null, null, null, null, null }));
        }
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
                UiFactLine.Row(W(t, "reallife_ui_estate_fig_deals", "Deals done"), w.AgencyDeals.ToString()), UiFactLine.Row(W(t, "reallife_ui_estate_fig_lost", "Deals lost"), w.AgencyLost.ToString()),
                UiFactLine.Row(W(t, "reallife_ui_estate_fig_earned", "Commission earned"), Usd(w.AgencyEarned))
            },
            Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_close_agency", "Close the agency"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count) }
        };
        TableOf(m, UiIcon.Key, W(t, "reallife_estate_tab_agency", "Agency"), w.Office, cols, rows, side, W(t, "reallife_estate_row_no_instructions", "No instructions"), W(t, "reallife_estate_agency_open_what", "Run the agency from an office you hold."));
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_instructions", "Open instructions"), w.Instructions.Count.ToString()), Fig(W(t, "reallife_ui_estate_col_commission", "Commission"), Usd(pending)),
            Fig(W(t, "reallife_ui_estate_fig_negotiators", "Negotiators"), w.Negotiators.ToString()), Fig(W(t, "reallife_ui_estate_fig_hours_day", "Hours a day"), (w.HoursTenths / 10f).ToString("0.#", InvD))
        };
        m.ShareTitle = W(t, "reallife_estate_agency_reputation", "What the districts think");
        m.ShareLabels = new string[w.Districts.Count];
        m.ShareValues = new int[w.Districts.Count];
        for (int i = 0; i < w.Districts.Count; i++)
        {
            m.ShareLabels[i] = w.Districts[i].Name;
            m.ShareValues[i] = w.Districts[i].Rep;
        }
        // The offices you hold that are free for the agency: the game lets them, so no tenant of yours is in them.
        var offices = new List<EstateRow>();
        foreach (EBuilding b in w.Buildings)
        {
            if (b.Use != 'o' || b.Managed)
            {
                continue;
            }
            bool here = b.Place == w.Office;
            runs.Add(Yes);
            offices.Add(new EstateRow { Icon = UiIcon.Briefcase, Title = b.Place, Sub = b.District, Chip = here ? W(t, "reallife_ui_estate_agency_here", "The agency is here") : null, ChipTone = UiRole.Ok, Run = here ? -1 : runs.Count - 1 });
        }
        m.RelatedTitle = W(t, "reallife_ui_estate_offices", "Offices you hold");
        m.RelatedCount = offices.Count.ToString();
        m.Related = offices.ToArray();
        EInstruction offered = null;
        foreach (EInstruction n in w.Instructions)
        {
            offered = offered == null && n.Stage == 0 ? n : offered;
        }
        if (offered != null)
        {
            m.Offer = new EstateOffer
            {
                Icon = offered.Sale ? UiIcon.Tag : UiIcon.Key, Title = Fx(W(t, offered.Sale ? "reallife_estate_row_sale" : "reallife_estate_row_letting", "An instruction in {district}"), "district", offered.District),
                Chip = W(t, "reallife_estate_stage_offered", "Offered"), Sub = Fx(W(t, "reallife_ui_estate_offer_sub", "{sqm} m2, asking {ask} of its value"), "sqm", offered.Sqm.ToString(), "ask", Percent(t, (offered.Ask + 5) / 10)),
                FigLabels = new[] { W(t, "reallife_ui_estate_offer_fee", "Fee to take it"), W(t, "reallife_ui_estate_offer_lapses", "Lapses in"), W(t, "reallife_ui_estate_offer_chance", "Chance of a deal"), W(t, "reallife_ui_estate_col_commission", "Commission") },
                FigValues = new[] { Usd(offered.Fee), Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", Math.Max(0, offered.Day - Today).ToString()), Percent(t, (offered.Chance + 5) / 10), Usd(offered.Commission) },
                Actions = new[]
                {
                    EstateButton(runs, W(t, "reallife_estate_btn_decline", "Decline"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                    EstateButton(runs, W(t, "reallife_estate_btn_take", "Take it"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt)
                }
            };
        }
        var chance = new EstateRow[w.Districts.Count];
        for (int i = 0; i < chance.Length; i++)
        {
            chance[i] = new EstateRow { Icon = UiIcon.Locate, Title = w.Districts[i].Name, Amount = Fx(W(t, "reallife_ui_estate_chance_a_day", "{chance} a day"), "chance", Percent(t, (w.Districts[i].Chance + 5) / 10)), Run = -1 };
        }
        m.Related2Title = W(t, "reallife_ui_estate_new_instruction_chance", "The chance of a new instruction");
        m.Related2Count = chance.Length.ToString();
        m.Related2 = chance;
        m.RelatedRows = 6;
    }

    // ------------------------------------------------------------------------------------------------- the hotels

    private static void Hotel(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w)
    {
        var rows = new List<UiDataRow>();
        var locate = new List<int>();
        int soldAll = 0, roomsAll = 0;
        long takingsAll = 0, costsAll = 0, keptYear = 0;
        var rates = new List<EstateRow>();
        var said = new List<EstateRow>();
        string[] kinds = { W(t, "reallife_travel_room_standard", "standard double"), W(t, "reallife_travel_room_single", "private single") };
        foreach (EHotel h in w.Hotels)
        {
            runs.Add(Yes);
            locate.Add(runs.Count - 1);
            int soldShare = h.RoomsAll > 0 ? h.SoldAll * 100 / h.RoomsAll : 0;
            soldAll += h.SoldAll;
            roomsAll += h.RoomsAll;
            takingsAll += h.Takings;
            costsAll += h.Costs;
            keptYear += h.TakingsYear - h.CostsYear;
            string campaign = h.PushDays > 0 ? Fx(W(t, "reallife_ui_estate_campaign_on", "Running, {days} day(s) left"), "days", h.PushDays.ToString()) : W(t, "reallife_ui_estate_campaign_off", "None running");
            rows.Add(Row(new[] { h.Name, string.Empty, h.Clean.ToString(), h.Staff.ToString(), soldShare.ToString(), Usd(h.Takings), Usd(h.Costs), campaign, string.Empty },
                new[] { (int)UiIcon.Building, h.Stars, h.Clean, h.Staff, soldShare, 0, 0, 0, (int)UiIcon.Locate },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, h.Takings < h.Costs ? UiRole.DangerText : UiRole.None, h.PushDays > 0 ? UiRole.Ok : UiRole.Muted, UiRole.None },
                new[] { h.District, null, null, null, null, null, null, null, null }));
            for (int type = 0; type < 2; type++)
            {
                int count = h.Rooms[type], sold = h.Sold[type];
                runs.Add(Yes);
                rates.Add(new EstateRow
                {
                    Icon = UiIcon.Building, Title = h.Name + ", " + kinds[type], Sub = Fx(W(t, "reallife_ui_estate_rate_going", "Going rate {going}, {share} sold"), "going", Usd(h.Going[type]), "share", Percent(t, count > 0 ? sold * 100 / count : 0)),
                    Chip = Fx(W(t, "reallife_ui_estate_of_going", "{share} of the going rate"), "share", Percent(t, h.Yours[type] * 100 / h.Going[type])), ChipTone = UiRole.Notice, Amount = Usd(h.Yours[type]), Run = runs.Count - 1
                });
            }
            foreach (int review in h.Reviews)
            {
                said.Add(new EstateRow { Icon = UiIcon.Message, Title = W(t, "reallife_estate_review_" + ReviewKey(review), "A guest wrote."), Sub = h.Name, Stars = EstateFigures.ReviewHalves(review), Run = -1 });
            }
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
        TableOf(m, UiIcon.Building, W(t, "reallife_estate_tab_hotel", "Hotel"), Fx(W(t, "reallife_ui_estate_hotels_sub", "{count} hotel(s) you run"), "count", w.Hotels.Count.ToString()), cols, rows, null, W(t, "reallife_estate_row_no_hotel", "No hotel yet"),
            W(t, "reallife_ui_estate_empty_hotel_body", "A hotel you buy or lease from the Market tab is run from here."));
        m.RowRuns = locate.ToArray();
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_rooms_sold", "Rooms sold today"), soldAll + " / " + roomsAll), Fig(W(t, "reallife_ui_estate_col_takings", "Takings"), Usd(takingsAll)),
            Fig(W(t, "reallife_ui_estate_col_costs", "Costs"), Usd(costsAll)), Fig(W(t, "reallife_ui_estate_fig_kept_year", "Kept this year"), Usd(keptYear))
        };
        m.RelatedTitle = W(t, "reallife_ui_estate_rates_title", "Your rate against the going rate");
        m.RelatedCount = rates.Count.ToString();
        m.RelatedRows = 8;
        m.Related = rates.ToArray();
        m.Related2Title = W(t, "reallife_ui_estate_guests_say", "Guests say");
        m.Related2Count = said.Count > 0 ? Fx(W(t, "reallife_ui_estate_reviews_n", "{count} reviews"), "count", said.Count.ToString()) : null;
        m.Related2 = said.ToArray();
    }

    private static string ReviewKey(int review) => review == 1 ? "clean" : review == 2 ? "dirty" : review == 3 ? "staff" : review == 4 ? "no_staff" : review == 5 ? "value" : "dear";

    // ------------------------------------------------------------------------------------------------- car hire

    private static void Hire(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w)
    {
        var rows = new List<UiDataRow>();
        int hiresAll = 0, outNow = 0, rateSum = 0, takingsAll = 0;
        ECar best = null, most = null;
        long premium = 0;
        foreach (ECar c in w.Fleet)
        {
            hiresAll += c.Hires;
            outNow += c.Out ? 1 : 0;
            rateSum += c.Rate;
            takingsAll += c.Takings;
            premium += c.Cover;
            best = best == null || c.Takings > best.Takings ? c : best;
            most = most == null || c.Hires > most.Hires ? c : most;
            CarKind kind = SampleKind(c.Name);
            rows.Add(Row(new[] { c.Name, c.Out ? W(t, "reallife_estate_car_out", "Out") : W(t, "reallife_estate_car_idle", "In"), Usd(c.Rate), c.Hires.ToString(), Usd(c.Takings),
                    c.Out ? Fx(W(t, "reallife_estate_row_until", "Until day {day}"), "day", c.Day.ToString()) : string.Empty },
                new[] { (int)CarClass.Glyph(kind), 0, 0, 0, 0, 0 }, new[] { CarClass.Tint(kind, c.Rate * 400), c.Out ? UiRole.Ok : UiRole.Notice, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                new[] { W(t, "reallife_estate_class_" + c.Class, c.Class), null, null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_car", "Car"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_state", "State"), Kind = UiCellKind.Chip, Width = 184f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_rate_day", "Rate a day"), Kind = UiCellKind.Money, Width = 120f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_hires", "Hires"), Kind = UiCellKind.Figure, Width = 80f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_takings", "Takings"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_estate_col_day", "Day"), Kind = UiCellKind.Date, Width = 104f }
        };
        int yearCosts = w.HireCostsYear;
        var lines = new List<UiFactLine>
        {
            UiFactLine.Row(W(t, "reallife_ui_estate_fig_year_takings", "Takings this year"), Usd(takingsAll)), UiFactLine.Row(W(t, "reallife_ui_estate_fig_year_costs", "Costs this year"), Usd(yearCosts)),
            UiFactLine.Row(W(t, "reallife_ui_estate_hire_kept_year", "Kept this year"), Usd(takingsAll - yearCosts), takingsAll >= yearCosts ? UiRole.OkText : UiRole.DangerText),
            UiFactLine.Row(W(t, "reallife_ui_estate_hire_best", "Best earner"), best.Name + ", " + Usd(best.Takings)), UiFactLine.Row(W(t, "reallife_ui_estate_hire_most", "Most hired"), most.Name + ", " + most.Hires),
            UiFactLine.Section(W(t, "reallife_ui_estate_hire_sec_cover", "Cover")), EstateSwitch(runs, W(t, "reallife_estate_hire_cover", "Fleet cover"), true),
            UiFactLine.Row(W(t, "reallife_ui_estate_fig_cover_cost", "Cover costs"), Fx(W(t, "reallife_ui_estate_amount_a_day", "{amount} a day"), "amount", Usd(premium))),
            UiFactLine.Row(W(t, "reallife_ui_estate_cover_pays", "Pays of a claim"), "80%"), UiFactLine.Row(W(t, "reallife_ui_estate_cover_excess", "Excess"), "$100"),
            UiFactLine.Section(W(t, "reallife_ui_estate_hire_sec_lot", "The lot")),
            UiFactLine.Bar(W(t, "reallife_ibox_spaces", "Spaces taken"), w.Fleet.Count * 100 / w.HireSpaces), UiFactLine.Bar(W(t, "reallife_ui_estate_sh_in_hire", "Cars in hire"), outNow * 100 / Math.Max(1, w.Fleet.Count))
        };
        var side = new UiFactCardModel { Title = W(t, "reallife_estate_tab_hire", "Car hire"), Heading = true, Lines = lines.ToArray() };
        TableOf(m, UiIcon.Car, W(t, "reallife_estate_tab_hire", "Car hire"), w.HireLot, cols, rows, side, W(t, "reallife_estate_row_no_cars", "No cars on hire"), W(t, "reallife_estate_hire_what", "Hire cars out from a lot."));
        m.HeadStars = w.HireRating;
        m.HeadSub = W(t, "reallife_ui_estate_fleet_rating", "The fleet's rating, from the customers' opinion");
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_cars_out", "Cars out"), outNow.ToString()), Fig(W(t, "reallife_ui_estate_col_hires", "Hires"), hiresAll.ToString()),
            Fig(W(t, "reallife_ui_estate_col_takings", "Takings"), Usd(takingsAll)), Fig(W(t, "reallife_ui_estate_fig_mean_rate", "Mean rate a day"), Usd(rateSum / Math.Max(1, w.Fleet.Count)))
        };
        // The cars of yours that stand within reach of the lot and could join the fleet, with the going rate each would hire at.
        var could = new EstateRow[w.Adds.Count];
        for (int i = 0; i < could.Length; i++)
        {
            runs.Add(Yes);
            CarKind kind = SampleKind(w.Adds[i].Name);
            could[i] = new EstateRow
            {
                Icon = CarClass.Glyph(kind), Tint = CarClass.Tint(kind, 0), Title = w.Adds[i].Name, Sub = W(t, "reallife_estate_class_" + w.Adds[i].Class, w.Adds[i].Class),
                Chip = Fx(W(t, "reallife_ui_estate_going_rate_day", "Going rate {amount} a day"), "amount", Usd(w.Adds[i].Rate)), ChipTone = UiRole.Notice,
                Button = W(t, "reallife_ui_estate_add_to_fleet", "Add to the fleet"), ButtonRun = runs.Count - 1, Run = runs.Count - 1
            };
        }
        m.RelatedTitle = W(t, "reallife_ui_estate_add_cars", "Cars you could add");
        m.RelatedCount = could.Length.ToString();
        m.RelatedRows = 8;
        m.Related = could;
    }

    // ------------------------------------------------------------------------------------------------- the pages about one thing

    private static void EstateDetail(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w, string state)
    {
        if (state == "call" || state == "applicant")
        {
            EstateDecide(t, m, runs, w, state);
            return;
        }
        m.Shape = EstateShape.Detail;
        m.Back = W(t, "reallife_co_back", "Back");
        switch (state)
        {
            case "building":
                BuildingPageOf(t, m, runs, w, w.Buildings[0]);
                break;
            case "listing":
                ListingPageOf(t, m, runs, w, w.Market[0]);
                break;
            default:
                WorksPageOf(t, m, runs, w, w.Buildings[0]);
                break;
        }
    }

    private static void BuildingPageOf(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w, EBuilding b)
    {
        m.Tab = 0;
        m.DetailIcon = UseGlyphOf(b.Use);
        m.DetailTitle = b.Place;
        m.DetailSub = Fx(W(t, "reallife_ibox_type_in", "{type} in {district}"), "type", UseWordOf(t, b.Use), "district", b.District);
        m.DetailLocate = W(t, "reallife_ui_estate_locate", "Locate");
        runs.Add(Yes);
        m.DetailLocateRun = runs.Count - 1;
        long fee = b.Value * 15L / 1000, tax = Math.Max(0, (b.Value - fee - b.Paid - b.Spent) * 25L / 100), gain = b.Value - fee - tax - b.Paid - b.Spent;
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ibox_worth", "Worth"), Usd(b.Value)), Fig(W(t, "reallife_ui_estate_fig_bought", "Bought for"), Usd(b.Paid)), Fig(W(t, "reallife_ui_estate_fig_works_spent", "Spent on works"), Usd(b.Spent)),
            Fig(W(t, "reallife_ui_estate_fig_gain", "Gain if sold today"), (gain >= 0 ? "+" : "-") + Usd(Math.Abs(gain)))
        };
        m.Left = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ui_estate_running", "Running it"), Heading = true,
                Lines = new[]
                {
                    EstateStep(runs, W(t, "reallife_estate_duty_who", "Who runs it"), W(t, "reallife_estate_manager_self", "Yourself"), null, false, true),
                    UiFactLine.Row(W(t, "reallife_ui_estate_fee_row", "Fee"), W(t, "reallife_ui_estate_fee_none", "None")), EstateSwitch(runs, W(t, "reallife_estate_own_cover", "Landlord's cover"), b.Cover)
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
                    EstateStep(runs, W(t, "reallife_ui_estate_sale_asking_share", "Asking, of its worth"), "100%", null), UiFactLine.Row(W(t, "reallife_ui_estate_sale_asking_price", "Asking price"), Usd(b.Value)),
                    UiFactLine.Row(W(t, "reallife_ui_estate_sale_chance", "Chance of an offer a day"), "18%"), UiFactLine.Row(W(t, "reallife_ui_estate_sale_fee", "Selling fee"), "1.5%")
                },
                Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_list", "List for sale"), UiButtonKind.Primary, UiIcon.Tag, UiAction.Alt) }
            },
            // What the building's value is made of and what its cover says, under the way to its works: the live page's third card.
            new UiFactCardModel
            {
                Lines = new[]   // a card of its own with no title: its first line is the way to the works
                {
                    EstateLink(runs, W(t, "reallife_ui_estate_works_link", "Works and servicing")),
                    UiFactLine.Say(Fx(W(t, "reallife_estate_own_cover_what", "Cover costs {amount} a day."), "amount", Usd(b.Value / 25000), "excess", Percent(t, 10)))
                }
            }
        };
        var happy = new List<int>();
        int let = 0;
        foreach (EUnit u in b.Units)
        {
            happy.Add(u.Let ? u.Happy : -1);
            let += u.Let ? 1 : 0;
        }
        int mean = EstateFigures.MeanHappy(happy);
        var labels = new List<string> { W(t, "reallife_ibox_let", "Let") };
        var values = new List<int> { b.Units.Count > 0 ? let * 100 / b.Units.Count : 0 };
        if (mean >= 0)
        {
            labels.Add(W(t, "reallife_estate_duty_happy", "Satisfaction"));
            values.Add(mean / 10);
        }
        labels.Add(W(t, "reallife_ui_estate_m_condition", "Condition"));
        values.Add(b.Cond);
        m.ShareTitle = W(t, "reallife_ui_estate_sh_building", "The building");
        m.ShareLabels = labels.ToArray();
        m.ShareValues = values.ToArray();
        m.RelatedTitle = W(t, "reallife_ui_estate_rel_let_to", "Let to");
        m.RelatedCount = Fx(W(t, "reallife_ui_estate_units_n", "{count} unit(s)"), "count", b.Units.Count.ToString());
        m.RelatedRows = 6;
        var flats = new List<EstateRow>();
        for (int n = 0; n < b.Units.Count; n++)
        {
            EUnit u = b.Units[n];
            runs.Add(Yes);
            flats.Add(new EstateRow
            {
                Face = u.Let ? u.Tenant.Name : null, Hue = u.Let ? UiColourHue(u.Tenant.Name) : 0f, Icon = u.Let ? UiIcon.None : UseGlyphOf(b.Use), Title = u.Let ? u.Tenant.Name : W(t, "reallife_estate_row_nobody", "Nobody"),
                Sub = UnitLabel(t, b, n), Chip = StateWord(t, u, out UiRole tone), ChipTone = tone, Amount = Mo(u.Let ? u.RentDay : u.AskDay), Run = runs.Count - 1
            });
        }
        m.Related = flats.ToArray();
        var works = new List<EstateRow>();
        foreach (EService v in b.Services)
        {
            if (works.Count < 3)
            {
                works.Add(new EstateRow { Icon = UiIcon.Wrench, Title = ServiceTitle(t, v.Kind), Sub = v.Who, Chip = Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", v.Day.ToString()), ChipTone = UiRole.Notice, Amount = Usd(v.Fee), Run = -1 });
            }
        }
        if (b.InspectIn > 0 && works.Count < 4)
        {
            works.Add(new EstateRow
            {
                Icon = UiIcon.Eye, Title = W(t, "reallife_ui_estate_next_inspection", "Next inspection"), Sub = b.InspectPassed ? W(t, "reallife_ui_estate_last_passed", "The last one passed") : W(t, "reallife_ui_estate_last_failed", "The last one failed"),
                Chip = Fx(W(t, "reallife_ui_estate_in_days", "in {days} days"), "days", b.InspectIn.ToString()), ChipTone = UiRole.Notice, Run = -1
            });
        }
        m.Related2Title = W(t, "reallife_ui_estate_works_servicing", "Works and servicing");
        m.Related2Count = works.Count > 0 ? works.Count.ToString() : null;
        m.Related2 = works.ToArray();
    }

    private static void ListingPageOf(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w, EListing l)
    {
        m.Tab = 3;
        m.DetailIcon = UseGlyphOf(l.Use);
        m.DetailTitle = l.Place;
        m.DetailSub = Fx(W(t, "reallife_ibox_type_in", "{type} in {district}"), "type", UseWordOf(t, l.Use), "district", l.District);
        m.DetailLocate = W(t, "reallife_ui_estate_locate", "Locate");
        runs.Add(Yes);
        m.DetailLocateRun = runs.Count - 1;
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_fig_asking_price", "Asking price"), Usd(l.Price)), Fig(W(t, "reallife_ui_estate_fig_game_value", "The game's value"), Usd(l.Value)),
            Fig(W(t, "reallife_ui_estate_fig_market_level", "Property market"), (w.MarketLevel / 10f).ToString("0.#", InvD) + "%"), Fig(W(t, "reallife_ui_estate_fig_prices_since", "Prices since the start"), (w.PricesSince / 10f).ToString("0.#", InvD) + "%")
        };
        m.Left = new[]
        {
            new UiFactCardModel
            {
                Title = Fx(W(t, "reallife_ui_estate_let_as", "Let as {use}"), "use", UseWordOf(t, l.Use)), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_ui_estate_col_rent_month", "Rent a month"), Mo(l.RentDay)), UiFactLine.Row(W(t, "reallife_ui_estate_listing_yield", "Yield a year"), YieldWords(l.RentDay, l.Price)),
                    UiFactLine.Bar(W(t, "reallife_ui_estate_m_condition", "Condition"), l.Cond)
                },
                Actions = new[] { EstateButton(runs, W(t, "reallife_estate_btn_offer", "Make an offer"), UiButtonKind.Primary, UiIcon.Handshake, UiAction.Alt) }
            }
        };
        m.Card = true;
        m.FootBack = W(t, "reallife_ui_estate_not_now", "Not now");
        var ranked = new List<EDistrict>(w.Districts);
        ranked.Sort((a, b) => b.PerSqm.CompareTo(a.PerSqm));
        m.ShareTitle = W(t, "reallife_ui_estate_sh_district_rents", "What the districts rent for, per m2");
        m.ShareLabels = new string[ranked.Count];
        m.ShareValues = new int[ranked.Count];
        m.ShareTexts = new string[ranked.Count];
        for (int i = 0; i < ranked.Count; i++)
        {
            m.ShareLabels[i] = ranked[i].Name;
            m.ShareValues[i] = ranked[i].PerSqm * 100 / ranked[0].PerSqm;
            m.ShareTexts[i] = "$" + (ranked[i].PerSqm / 100f).ToString("0.00", InvD);
        }
        // The other buildings for sale in the district, as short rows that open their own listing.
        var others = new List<EstateRow>();
        foreach (EListing o in w.Market)
        {
            if (o.Place == l.Place || o.District != l.District || others.Count >= 3)
            {
                continue;
            }
            runs.Add(Yes);
            others.Add(new EstateRow { Icon = UseGlyphOf(o.Use), Title = o.Place, Sub = UseWordOf(t, o.Use), Chip = Fx(W(t, "reallife_ui_estate_yield_year", "{yield} a year"), "yield", YieldWords(o.RentDay, o.Price)),
                ChipTone = UiRole.Notice, Amount = Usd(o.Price), Run = runs.Count - 1 });
        }
        m.Related2Title = Fx(W(t, "reallife_ui_estate_also_in", "Also for sale in {district}"), "district", l.District);
        m.Related2Count = others.Count > 0 ? others.Count.ToString() : null;
        m.Related2 = others.ToArray();
    }

    private static void WorksPageOf(UiWords t, EstateModel m, List<Func<bool>> runs, EstateWorldData w, EBuilding b)
    {
        m.Card = true;
        m.FootBack = W(t, "reallife_ui_estate_not_now", "Not now");
        m.Tab = 2;
        m.DetailIcon = UiIcon.Wrench;
        m.DetailTitle = b.Place;
        m.DetailSub = Fx(W(t, "reallife_ibox_type_in", "{type} in {district}"), "type", UseWordOf(t, b.Use), "district", b.District);
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ui_estate_m_condition", "Condition"), b.Cond + "%"), Fig(W(t, "reallife_ui_estate_fig_worth_of_best", "Worth, of its best"), WorthOfBest(b.Cond) + "%"),
            Fig(W(t, "reallife_ui_estate_fig_rents_of_best", "Rents, of its best"), RentsOfBest(b.Cond) + "%"), Fig(W(t, "reallife_ui_estate_fig_booked", "Booked"), b.Services.Count.ToString())
        };
        m.Left = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ibox_sec_works", "Works"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_ui_estate_works_repair_costs", "A repair costs"), Usd(RepairOf(b))),
                    UiFactLine.Row(W(t, "reallife_ui_estate_works_repair_takes", "A repair takes"), Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", RepairDays(b).ToString())),
                    UiFactLine.Row(W(t, "reallife_ui_estate_works_level", "Fitted level"), b.Level.ToString())
                },
                Actions = new[]
                {
                    EstateButton(runs, W(t, "reallife_estate_btn_refurbish", "Refurbish"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                    EstateButton(runs, W(t, "reallife_estate_btn_repair", "Repair"), UiButtonKind.Primary, UiIcon.Wrench, UiAction.Alt)
                }
            }
        };
        var servicing = new List<UiFactLine> { UiFactLine.Row(W(t, "reallife_ui_estate_service_keeps", "A visit keeps a fault down for"), Fx(W(t, "reallife_ui_estate_n_days", "{days} days"), "days", "90")) };
        foreach (EService v in b.Services)
        {
            servicing.Add(EstateLink(runs, Fx(W(t, "reallife_ui_estate_service_line", "Cancel the {fault} service on day {day}, {who}, {amount}"), "fault", FaultNounOf(t, v.Kind), "day", v.Day.ToString(), "who", v.Who, "amount", Usd(v.Fee))));
        }
        m.Right = new[] { new UiFactCardModel { Title = W(t, "reallife_ui_estate_servicing", "Booked servicing"), Heading = true, Lines = servicing.ToArray() } };
        m.GridTitle = W(t, "reallife_ui_estate_book_a_visit", "Book a visit");
        var book = new List<UiActionModel>();
        for (int kind = 1; kind <= 6; kind++)
        {
            bool booked = false;
            foreach (EService v in b.Services)
            {
                booked |= v.Kind == kind;
            }
            book.Add(EstateButton(runs, Fx(W(t, "reallife_ui_estate_book_kind", "Book {fault}, {amount}"), "fault", FaultNounOf(t, kind), "amount", Usd(140 + kind * 40)), UiButtonKind.Secondary, UiIcon.Calendar, UiAction.Count, booked));
        }
        m.Grid = book.ToArray();
        var calls = new List<EstateRow>();
        for (int n = 0; n < b.Units.Count; n++)
        {
            EUnit u = b.Units[n];
            if (!u.Let || u.Call <= 0)
            {
                continue;
            }
            runs.Add(Yes);
            runs.Add(Yes);
            calls.Add(new EstateRow
            {
                Face = u.Tenant.Name, Hue = UiColourHue(u.Tenant.Name), Title = Capital(FaultNounOf(t, u.Call)) + ", " + UnitLabel(t, b, n), Sub = u.Tenant.Name,
                Chip = u.CallBooked ? W(t, "reallife_ibox_chip_booked", "Booked") : W(t, "reallife_ibox_chip_open", "Open"), ChipTone = u.CallBooked ? UiRole.Ok : UiRole.Warn, Amount = Usd(u.CallCost), Run = runs.Count - 2
            });
        }
        m.RelatedTitle = W(t, "reallife_ui_estate_calls_here", "Calls on this building");
        m.RelatedCount = calls.Count.ToString();
        m.Related = calls.ToArray();
    }
}
