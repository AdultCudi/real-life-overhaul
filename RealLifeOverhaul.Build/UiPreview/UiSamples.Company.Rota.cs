using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Ui;

// The Company app's Rota tab. Samples' own names and figures; the live source reads the player's businesses. A place's week is worked out from its people by the sample world
// (UiSamples.Company.World.cs RotaOf), so that the week, the day, the sums, the rows of the list and the advice agree: Speedy Bites has ten people on eight hour shifts, Corner Cafe two on
// seven hour ones.
internal static partial class UiSamples
{
    // ------------------------------------------------------------------------------------------------- Rota

    /// <summary>What a rota state has picked in its list: a place, or one of its plans.</summary>
    public static string RotaPickOf(string state) => state == "rota_one" ? "p:Corner Cafe~12" : state == "rota_refused" ? "p:Hudson Warehouse~4" : state == "rota_edit" ? "pl:Speedy Bites~7:1"
        : state == "rota_edit_other" ? "pl:Speedy Bites~7:2" : "p:Speedy Bites~7";

    private const int HireWage = 1800;   // what one more person for customer service costs an hour, in hundredths

    // A person who works more than they will: the cell of their last day and their hours stand in the warning colour.
    private static bool Overworked(CPerson p, int hours) => p != null && p.Most > 0 && hours > p.Most;

    // A place's week as the board draws it: everybody on their shifts, and the day totals adding up.
    private static CompanyRotaPerson[] RotaPeople(CompanyWords w, CompanyWorld.CRota rota)
    {
        var people = new CompanyRotaPerson[rota.People.Length];
        for (int i = 0; i < people.Length; i++)
        {
            CPerson p = rota.People[i];
            bool hire = rota.ToHire[i];
            var tones = new UiRole[7];
            bool over = Overworked(p, rota.Hours[i]);
            int lastDay = -1;
            for (int d = 0; d < 7; d++)
            {
                lastDay = rota.Shifts[i][d] != null ? d : lastDay;
            }
            if (over && lastDay >= 0)
            {
                tones[lastDay] = UiRole.WarnText;
            }
            people[i] = new CompanyRotaPerson
            {
                Name = hire ? Tr(w, "To hire") : p.Name, Role = hire ? Tr(w, "Customer service") : Pn(w, p), Hue = hire ? 0.1f + i * 0.14f : p.Hue, Ring = UiRole.Ok,
                Shifts = rota.Shifts[i], Tones = tones, Hours = rota.Hours[i] + "h", HoursTone = over ? UiRole.WarnText : UiRole.None, ToHire = hire
            };
        }
        return people;
    }

    private static int WagesOf(CompanyWorld.CRota rota)
    {
        int hundredths = 0;
        for (int i = 0; i < rota.People.Length; i++)
        {
            hundredths += rota.Hours[i] * (rota.ToHire[i] ? HireWage : rota.People[i].Paid);
        }
        return (hundredths + 50) / 100;
    }

    // The row of a place in the list: its kind, its people, and whether the week is covered.
    private static CompanyRow RotaPlaceRow(CompanyWords w, CPlace pl)
    {
        if (pl.NoRota)
        {
            return new CompanyRow { Id = "p:" + pl.Key, Title = pl.Name, Sub = Kind(w, pl.Kind) + ", " + w.F("reallife_co_tree_staff", "{count} staff", "count", Cw.StaffAt(pl.Key).Count.ToString()), Icon = UiIcon.Calendar,
                Figure = w.T("reallife_rota_row_none", "No rota"), FigureTone = UiRole.Muted };
        }
        CompanyWorld.CRota rota = Cw.RotaOf(pl);
        return new CompanyRow
        {
            Id = "p:" + pl.Key, Title = pl.Name, Sub = Kind(w, pl.Kind) + ", " + w.F("reallife_co_tree_staff", "{count} staff", "count", rota.People.Length.ToString()), Icon = UiIcon.Calendar,
            Figure = rota.Short == 0 ? w.T("reallife_rota_row_covered", "Covered") : w.F("reallife_co_row_gap_short", "{hours} h short", "hours", rota.Short.ToString()), FigureTone = rota.Short == 0 ? UiRole.OkText : UiRole.WarnText
        };
    }

    private static CompanyRow PlanRow(CompanyWords w, CPlace pl, int id, string title, string sub, bool inUse) => new CompanyRow
    {
        Id = "pl:" + pl.Key + ":" + id, Title = title, Sub = sub, Icon = UiIcon.ClipboardText, Dim = true, Nested = true, Chip = inUse ? w.T("reallife_rota_in_use", "In use") : null, ChipTone = UiRole.Ok
    };

    private static void CompanyRota(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyRotaFacts r = f.Rota;
        if (state == "rota_off")
        {
            r.PartOff = true;
            r.OffTitle = w.T("reallife_rota_tab", "Rota");
            r.OffBody = w.T("reallife_rota_off", "The rota planner is switched off in the mod's settings.");
            return;
        }
        bool one = Cw.Places.Count == 1;
        CPlace pl0 = one ? Cw.Places[0] : Cw.Pl("Speedy Bites");
        string id = pl0.Key;
        CompanyWorld.CRota rota = Cw.RotaOf(pl0);
        string pick = RotaPickOf(state);
        var rows = new List<CompanyRow>();
        string weekly = rota.Open.ToString();
        if (one)
        {
            rows.Add(RotaPlaceRow(w, pl0));
            rows.Add(PlanRow(w, pl0, 1, w.T("reallife_rota_plan_standard", "Standard week"), w.T("reallife_rota_repeat_every", "Every week"), true));
            rows.Add(PlanRow(w, pl0, 2, "Quiet week", w.F("reallife_rota_plan_range", "Days {from} to {to}", "from", Dn(22), "to", Dn(28)), false));
        }
        else
        {
            CPlace bloom = Cw.Pl("Bloom & Co"), hudson = Cw.Pl("Hudson Warehouse");
            rows.Add(RotaPlaceRow(w, pl0));
            rows.Add(PlanRow(w, pl0, 1, w.T("reallife_rota_plan_standard", "Standard week"), w.T("reallife_rota_repeat_every", "Every week"), true));
            rows.Add(PlanRow(w, pl0, 2, "Quiet week", w.T("reallife_rota_repeat_odd", "Odd weeks"), false));
            rows.Add(PlanRow(w, pl0, 3, "Holiday week", w.F("reallife_rota_plan_range", "Days {from} to {to}", "from", Dn(220), "to", Dn(226)), false));
            rows.Add(RotaPlaceRow(w, bloom));
            rows.Add(PlanRow(w, bloom, 1, w.T("reallife_rota_plan_standard", "Standard week"), w.T("reallife_rota_repeat_every", "Every week"), true));
            rows.Add(RotaPlaceRow(w, Cw.Pl("Pioneer Game Studio")));
            rows.Add(RotaPlaceRow(w, hudson));
        }
        // Each place that keeps a rota; a warehouse is told so but is a place of the company all the same.
        r.Places = new CompanyList { Title = w.T("reallife_rota_tab", "Rota"), Count = rows.Count(x => x.Id.StartsWith("p:", StringComparison.Ordinal)).ToString(), Rows = rows.ToArray(), Chosen = -1 };
        for (int i = 0; i < rows.Count; i++)
        {
            r.Places.Chosen = rows[i].Id == pick ? i : r.Places.Chosen;
        }
        var p = new CompanyRotaPlace { Id = id, Name = pl0.Name, Sub = Kind(w, pl0.Kind) + ", " + w.F("reallife_co_rota_open", "open {hours} h a week", "hours", weekly) };
        if (state == "rota_refused")
        {
            CPlace hw = Cw.Pl("Hudson Warehouse");
            p.Name = hw.Name;
            p.Id = hw.Key;
            p.Refused = w.T("reallife_rota_warehouse", "A warehouse keeps no rota. The game gives its drivers their vehicles on the warehouse's own page.");
            CompanyAct staff = CA(CompanyDo.Go, null, CompanyView.Staff, CompanyView.SubMain);
            List<CPerson> crew = Cw.StaffAt(hw.Key);
            var crewItems = new CompanyItem[crew.Count];
            for (int i = 0; i < crew.Count; i++)
            {
                crewItems[i] = new CompanyItem { Title = crew[i].Name, Sub = Pn(w, crew[i]), Face = true, Hue = crew[i].Hue, Ring = UiRole.Ok, Press = true, Act = staff, Grade = -1 };
            }
            p.RefusedCard = new CompanyCard
            {
                Title = hw.Name, Sub = w.T("reallife_mgr_kind_warehouse", "Warehouse"), Icon = UiIcon.Calendar, Chips = new[] { w.T("reallife_rota_row_none", "No rota") }, ChipTones = new[] { UiRole.Muted },
                Blocks = new[]
                {
                    CNote(p.Refused), CSec(w.T("reallife_rota_has_title", "What it has")),
                    new CompanyBlock
                    {
                        Kind = CompanyBlockKind.Items, Items = new[]
                        {
                            new CompanyItem { Title = w.T("reallife_rota_has_page", "Its own page"), Sub = w.T("reallife_rota_has_page_what", "Where its drivers get their vehicles"), Icon = UiIcon.Truck,
                                Press = true, Act = CA(CompanyDo.BizMan, hw.Key), Grade = -1 }
                        }
                    },
                    new CompanyBlock { Kind = CompanyBlockKind.Section, Text = w.T("reallife_rota_has_crew", "Its crew"), Value = w.T("reallife_rota_has_staff_link", "Open Staff"), Act = staff },
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = crewItems }
                }
            };
            r.Place = p;
            return;
        }
        // The people and their week.
        int length = rota.Length;
        int from = rota.From, to = rota.To;
        CompanyRotaPerson[] people = RotaPeople(w, rota);
        int[] dayTotals = rota.DayTotals;
        int total = rota.Total;
        p.Weekly = w.F("reallife_rota_weekly", "Week {week}, open {hours} hours", "week", Cw.Week.ToString(), "hours", weekly);
        p.PlanNames = one ? new[] { Tr(w, "Standard week"), "Quiet week" } : new[] { Tr(w, "Standard week"), "Quiet week", "Holiday week" };
        p.PlanChosen = state == "rota_edit_other" ? 1 : 0;
        string ahead = one ? "Quiet week" : "Holiday week";
        int aheadFrom = one ? 22 : 220, aheadTo = one ? 28 : 226;
        p.Ahead = w.F("reallife_co_plan_ahead", "{plan} holds from day {from} to day {to}: up to {cap} each, and {floor} crew hours of floor a week against this plan", "plan", ahead, "from", Dn(aheadFrom), "to", Dn(aheadTo),
            "cap", w.F("reallife_rota_hours_n", "{hours} h", "hours", one ? "28" : "32"), "floor", one ? "-14" : "+8");
        p.RepeatOptions = new[] { w.T("reallife_rota_repeat_every", "Every week"), w.T("reallife_rota_repeat_odd", "Odd weeks"), w.T("reallife_rota_repeat_even", "Even weeks") };
        int planId = state == "rota_edit_other" ? 2 : 1;
        p.Repeat = state == "rota_edit_other" ? 1 : 0;
        p.RepeatActs = new[] { CA(CompanyDo.PlanChange, p.Id, planId, 3, 0), CA(CompanyDo.PlanChange, p.Id, planId, 3, 1), CA(CompanyDo.PlanChange, p.Id, planId, 3, 2) };
        p.HoursValue = w.F("reallife_rota_hours_n", "{hours} h", "hours", "40");
        p.HoursLess = true;
        p.HoursMore = true;
        p.HoursLessAct = CA(CompanyDo.PlanChange, p.Id, planId, 4, 36);
        p.HoursMoreAct = CA(CompanyDo.PlanChange, p.Id, planId, 4, 44);
        p.HoursTypeAct = CA(CompanyDo.PlanChange, p.Id, planId, 4);
        p.OvertimeAct = CA(CompanyDo.PlanChange, p.Id, planId, 5, 1);
        p.Edit = CB(w.T("reallife_rota_edit", "Edit"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, null, CompanyView.Rota, CompanyView.SubEdit), UiIcon.Sliders);
        p.Copy = CB(w.T("reallife_rota_copy", "Copy plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanCopy, p.Id, planId), UiIcon.ClipboardText);
        if (one)
        {
            p.Back = CB(w.T("reallife_rota_back", "Put the old rota back"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RotaBack, p.Id), UiIcon.Back, false, null, true);   // just filled
        }
        p.Fill = CB(w.T("reallife_rota_fill", "Fill the rota"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RotaFill, p.Id, -1), UiIcon.Check);
        p.ModeTitle = w.T("reallife_rota_mode_title", "Mode");
        p.ModePicked = w.F("reallife_rota_mode_picked", "Auto chose {mode}", "mode", w.T("reallife_rota_mode_staff", "Staff first"));
        p.ModeOptions = new[] { w.T("reallife_rota_mode_auto_short", "Auto"), w.T("reallife_rota_mode_staff", "Staff first"), w.T("reallife_rota_mode_lean", "Lean") };
        p.ModeActs = new[] { CA(CompanyDo.RotaMode, p.Id, 0), CA(CompanyDo.RotaMode, p.Id, 1), CA(CompanyDo.RotaMode, p.Id, 2) };
        p.Mode = 0;
        p.DayNames = new[] { Wd(w, 0, true), Wd(w, 1, true), Wd(w, 2, true), Wd(w, 3, true), Wd(w, 4, true), Wd(w, 5, true), Wd(w, 6, true) };
        p.Day = 1;
        p.People = people;
        p.DayTotals = new string[7];
        for (int d = 0; d < 7; d++)
        {
            p.DayTotals[d] = dayTotals[d] + "h";
        }
        p.TotalHours = total + "h";
        int wages = WagesOf(rota);
        p.SummaryTitle = w.T("reallife_rota_week_comes_to", "What the week comes to");
        p.SummaryLabels = new[] { w.T("reallife_rota_sum_served", "Served"), w.T("reallife_rota_sum_lost", "Lost"), w.T("reallife_rota_sum_wages_label", "Wages"), w.T("reallife_rota_sum_net_label", "Net") };
        p.SummaryValues = one ? new[] { "612", "38", CM(wages), CM(1430) } : new[] { "1,420", "36", CM(wages), CM(2480) };
        p.SummaryTones = new[] { UiRole.None, UiRole.WarnText, UiRole.None, UiRole.OkText };
        // The chosen day (Tuesday) hour by hour, from the shifts above.
        int[][] blocks = rota.Need;
        int hoursOpen = to - from;
        p.HourLabels = new string[hoursOpen];
        p.OnShift = new int[hoursOpen];
        p.Needed = new int[hoursOpen];
        for (int h = from; h < to; h++)
        {
            p.HourLabels[h - from] = h.ToString("00");
            for (int i = 0; i < people.Length; i++)
            {
                p.OnShift[h - from] += CompanyWorld.OnShiftAt(people[i].Shifts[1], h) ? 1 : 0;
            }
            foreach (int[] b in blocks)
            {
                p.Needed[h - from] = h >= b[0] && h < b[1] ? b[2] : p.Needed[h - from];
            }
        }
        p.DayTitle = w.F("reallife_rota_day_title_short", "{day}, hour by hour", "day", Wd(w, 1, false));
        p.DayShort = Wd(w, 1, false);
        p.DayNote = w.F("reallife_rota_from_to", "{from} to {to}", "from", from.ToString("00") + ":00", "to", to.ToString("00") + ":00");
        p.Legend = new[] { w.T("reallife_rota_legend_on", "On shift"), w.T("reallife_rota_legend_short", "Short"), w.T("reallife_rota_legend_need", "Needed") };
        p.NeedsTitle = w.T("reallife_rota_needs", "Needs by time");
        p.NeedWhen = new string[blocks.Length];
        p.NeedWho = new string[blocks.Length];
        for (int i = 0; i < blocks.Length; i++)
        {
            p.NeedWhen[i] = blocks[i][0].ToString("00") + "-" + blocks[i][1].ToString("00");
            p.NeedWho[i] = w.F("reallife_rota_needs_crew", "{count} on shift", "count", blocks[i][2].ToString());
        }
        p.DayFigureLabels = new[] { w.T("reallife_rota_sum_served", "Served"), w.T("reallife_rota_sum_lost", "Lost"), w.T("reallife_rota_day_takings", "Takings"), w.T("reallife_rota_day_costs", "Wages and theft") };
        p.DayFigureValues = one ? new[] { "86", "5", CM(412), CM(wages / 7) } : new[] { "214", "22", CM(1010), CM(wages / 7) };
        if (state == "rota_day")
        {
            p.Board = DayBoard(w, p, people, length, from, to);
        }
        if (state == "rota_advice" || state.StartsWith("rota_look", StringComparison.Ordinal))
        {
            RotaAdvice(p, w, rota, pl0);
        }
        if (state.StartsWith("rota_look", StringComparison.Ordinal))
        {
            p.Look = RotaLook(p, w, state == "rota_look" ? 1 : 2, pl0);
        }
        if (state == "rota_edit" || state == "rota_edit_other")
        {
            RotaEdit(r, p, w, state == "rota_edit_other", rota);
        }
        r.Place = p;
    }

    // The day on the board: a lane for each person, a bar for each shift of the day carrying its hours only, and the staff against the need under it.
    private static UiBoardModel DayBoard(CompanyWords w, CompanyRotaPlace p, CompanyRotaPerson[] people, int length, int from, int to)
    {
        var lanes = new List<UiBoardLane>();
        var shifts = new List<UiBoardShift>();
        for (int i = 0; i < people.Length; i++)
        {
            lanes.Add(new UiBoardLane { Name = people[i].Name, Sub = people[i].Role, Hue = people[i].Hue });
            string shift = people[i].Shifts[1];
            if (shift != null)
            {
                int start = int.Parse(shift.Substring(0, 2));
                shifts.Add(new UiBoardShift
                {
                    Lane = i, Start = start * 60, End = (start + length) * 60, Name = null, Ring = UiRole.Ok,
                    Span = w.F("reallife_rota_from_to", "{from} to {to}", "from", start.ToString("00") + ":00", "to", (start + length).ToString("00") + ":00")
                });
            }
        }
        var cover = new string[24];
        var shortH = new bool[24];
        for (int h = 0; h < 24; h++)
        {
            int k = h - from;
            cover[h] = k >= 0 && k < p.OnShift.Length ? p.OnShift[k] + "/" + p.Needed[k] : null;
            shortH[h] = k >= 0 && k < p.OnShift.Length && p.OnShift[k] < p.Needed[k];
        }
        var hours = new string[to - from + 3];
        for (int h = 0; h < hours.Length; h++)
        {
            hours[h] = (from - 1 + h).ToString("00") + ":00";
        }
        return new UiBoardModel
        {
            Days = p.DayNames, Day = 1, FirstHour = from - 1, LastHour = to + 1, Hours = hours, OpenFrom = from * 60, OpenTo = to * 60, Lanes = lanes.ToArray(), Shifts = shifts.ToArray(), Faces = false,
            CoverTitle = w.T("reallife_rota_cover_title", "Staff / needed"), Cover = cover, Short = shortH, LegendOpen = w.T("reallife_ui_bizops_legend_open", "Open hours"),
            LegendClosed = w.T("reallife_ui_bizops_legend_closed", "Closed hours")
        };
    }

    // The planner's ladder of three plans, your people against the advised one, and a station that would pay.
    private static void RotaAdvice(CompanyRotaPlace p, CompanyWords w, CompanyWorld.CRota rota, CPlace pl)
    {
        p.AdviceTitle = w.T("reallife_rota_advice_title", "The planner's advice");
        p.AdviceNote = w.F("reallife_rota_advice_hire", "Advised: {plan}. It is worth about {amount} a week more than the week as it is.", "plan", w.F("reallife_rota_plan_hire", "Hire {count}: {who}",
            "count", "1", "who", Tr(w, "Customer service")), "amount", CM(310));
        p.Rungs = new[]
        {
            new CompanyRung
            {
                Name = w.T("reallife_rota_plan_now", "As it is"), Line = w.T("reallife_rota_needs_nothing", "Nothing: it is the week you have"),
                Hours = w.F("reallife_rota_plan_hours", "{hours} open, {manned} with the crew there", "hours", rota.Open.ToString(), "manned", (rota.Open - rota.Short).ToString()), Net = CM(2480),
                Use = CB(w.T("reallife_rota_use_plan", "Use this plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RotaFill, p.Id, 0), UiIcon.Check, true),
                Look = CB(w.T("reallife_rota_look", "Look"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, null, CompanyView.Rota, CompanyView.SubAdvice, 1), UiIcon.Eye),
                Chip = w.T("reallife_rota_in_use", "In use"), ChipTone = UiRole.Ok
            },
            new CompanyRung
            {
                Name = w.T("reallife_rota_plan_best", "Best with the people you have"), Line = w.T("reallife_rota_needs_rota", "A new rota for the hours you keep"),
                Hours = w.F("reallife_rota_plan_hours_all", "{hours} open", "hours", rota.Open.ToString()), Net = CM(2640), Gain = w.F("reallife_rota_plan_gain", "{amount} a week more", "amount", CM(160)),
                Use = CB(w.T("reallife_rota_use_plan", "Use this plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RotaFill, p.Id, 1), UiIcon.Check),
                Look = CB(w.T("reallife_rota_look", "Look"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, null, CompanyView.Rota, CompanyView.SubAdvice, 2), UiIcon.Eye)
            },
            new CompanyRung
            {
                Name = w.F("reallife_rota_plan_hire", "Hire {count}: {who}", "count", "1", "who", Tr(w, "Customer service")), Line = w.F("reallife_rota_needs_hire", "One more for {who} at about {wage} an hour", "who",
                    Tr(w, "Customer service"), "wage", CompanyWorld.Wage(HireWage)),
                Hours = w.F("reallife_rota_plan_hours_all", "{hours} open", "hours", rota.Open.ToString()), Net = CM(2790), Gain = w.F("reallife_rota_plan_gain", "{amount} a week more", "amount", CM(150)), Advised = true,
                Use = CB(w.T("reallife_rota_use_after", "Use these hours once you have hired"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RotaFill, p.Id, 2), UiIcon.Check, true,
                    w.T("reallife_rota_hire_where", "Hire in the MyEmployees app on your phone, or from a job board in the building.")),
                Look = CB(w.T("reallife_rota_look", "Look"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, null, CompanyView.Rota, CompanyView.SubAdvice, 3), UiIcon.Eye),
                Chip = w.T("reallife_rota_hire_first", "Hire first"), ChipTone = UiRole.Warn
            }
        };
        p.FolkTitle = w.T("reallife_rota_folk_title", "Your people and the advised plan");
        string between(string least, string most) => w.F("reallife_rota_asks_between", "{least} to {most} hours a week", "least", least, "most", most);
        string hoursOf(int n) => w.F("reallife_rota_hours_n", "{hours} h", "hours", n.ToString());
        var folk = new List<CompanyRotaFolk>();
        for (int i = 0; i < rota.People.Length; i++)
        {
            CPerson person = rota.People[i];
            if (person == null || person.Least == 0)
            {
                continue;
            }
            int hours = rota.Hours[i];
            bool over = person.Most > 0 && hours > person.Most, under = hours < person.Least;
            folk.Add(new CompanyRotaFolk
            {
                Name = person.Name, Role = Pn(w, person), Asks = person.Most > 0 ? between(person.Least.ToString(), person.Most.ToString()) : w.F("reallife_rota_asks_least", "at least {least} hours a week", "least", person.Least.ToString()),
                Figure = hoursOf(hours), Plan = hours, Least = person.Least, Most = person.Most, Hue = person.Hue, Ring = over || under ? UiRole.Warn : UiRole.Ok, Tone = over || under ? UiRole.WarnText : UiRole.None,
                Note = over ? w.F("reallife_rota_hours_over", "{hours} h over", "hours", (hours - person.Most).ToString()) : under ? w.F("reallife_rota_hours_under", "{hours} h under", "hours", (person.Least - hours).ToString()) : null
            });
        }
        p.Folk = folk.ToArray();
        p.Beside = new[]
        {
            CSec(w.T("reallife_rota_head_stations", "Stations that would pay")),
            new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Title = w.F("reallife_rota_missing", "One more {station}", "station", "Grill"), Sub = w.F("reallife_rota_missing_what", "would serve {customers} more customers", "customers", "140"),
                        Figure = "+140", Note = w.T("reallife_rota_unit_customers", "customers a week"), FigureTone = UiRole.OkText, Icon = UiIcon.Plus, Tint = UiRole.Ok, Grade = -1 }
                }
            }
        };
    }

    // A rung's week to read: rung 1 is the best week with the people there are, rung 2 the one that takes on a hire.
    private static CompanyRotaLook RotaLook(CompanyRotaPlace p, CompanyWords w, int rung, CPlace pl)
    {
        // The week of the rung, from the same people: the hire is one more person on the shifts. A person's row says only their work; their hours stand in the column beside it.
        CompanyWorld.CRota week = Cw.RotaOf(pl, rung, rung == 2);
        CompanyRotaPerson[] people = RotaPeople(w, week);
        int total = week.Total;
        int[] dayTotals = week.DayTotals;
        var look = new CompanyRotaLook
        {
            Name = rung == 1 ? w.T("reallife_rota_plan_best", "Best with the people you have") : w.F("reallife_rota_plan_hire", "Hire {count}: {who}", "count", "1", "who", Tr(w, "Customer service")),
            Line = rung == 1 ? w.T("reallife_rota_needs_rota", "A new rota for the hours you keep") : w.F("reallife_rota_needs_hire", "One more for {who} at about {wage} an hour", "who", Tr(w, "Customer service"),
                "wage", CompanyWorld.Wage(HireWage)),
            Why = rung == 1 ? w.T("reallife_rota_why_best_rota", "The opening hours stay. The rota is made again so that more customers are served, the floor is cleaned when it matters and nobody's demands are passed over.")
                : w.F("reallife_rota_why_hire", "One more for {who} keeps {hours} more hours with the crew there. Customers served a week more: {customers}.", "who", Tr(w, "Customer service"), "hours", "8", "customers",
                    "140"),
            Advised = rung == 2, People = people, TotalHours = total + "h", DayTotals = new string[7],
            FigureLabels = new[] { w.T("reallife_rota_look_open", "Open a week"), w.T("reallife_rota_sum_wages_label", "Wages"), w.T("reallife_rota_sum_net_label", "Net"),
                w.T("reallife_rota_look_gain", "Against now") },
            FigureValues = new[] { w.F("reallife_rota_hours_n", "{hours} h", "hours", week.Open.ToString()), CM(WagesOf(week)), CM(rung == 1 ? 2640 : 2790), "+" + CM(rung == 1 ? 160 : 310) },
            ChangesTitle = w.T("reallife_rota_look_changes", "What it changes"), ChangesNone = w.T("reallife_rota_look_same", "It keeps the opening hours you have."),
            Back = CB(w.T("reallife_rota_look_back", "Back to the advice"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, null, CompanyView.Rota, CompanyView.SubAdvice, 0), UiIcon.Back)
        };
        for (int d = 0; d < 7; d++)
        {
            look.DayTotals[d] = dayTotals[d] + "h";
        }
        look.Changes = new[]
        {
            new CompanyRotaChange { Title = Wd(w, 5, false) + ": " + w.F("reallife_rota_change_open", "Opens {from} to {to}", "from", "22:00", "to", "23:00"), Sub = w.T("reallife_rota_change_why_open",
                "They pay with the crew this plan has."), Figure = "+" + CM(120), Opens = true },
            new CompanyRotaChange { Title = Wd(w, 1, false) + ": " + w.F("reallife_rota_change_close", "Closes {from} to {to}", "from", "07:00", "to", "08:00"), Sub = w.T("reallife_rota_change_why_pay",
                "The customers bring less than the smallest crew is paid. More customers in these hours would keep them."), Figure = "-" + CM(30) }
        };
        if (rung == 2)
        {
            look.Chip = w.T("reallife_rota_hire_first", "Hire first");
        }
        else
        {
            look.Use = CB(w.T("reallife_rota_use_plan", "Use this plan"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RotaFill, p.Id, rung), UiIcon.Check);
        }
        return look;
    }

    // The plan's own page in two columns, and the floor it asks for by hour under them.
    private static void RotaEdit(CompanyRotaFacts r, CompanyRotaPlace p, CompanyWords w, bool other, CompanyWorld.CRota rota)
    {
        int id = other ? 2 : 1;
        int[][] blocks = other ? new[] { new[] { 7, 15, 2 }, new[] { 15, 23, 2 } } : new[] { new[] { 7, 11, 2 }, new[] { 11, 15, 5 }, new[] { 15, 23, 3 } };
        string name = other ? "Quiet week" : Tr(w, "Standard week");
        var c = new CompanyCard
        {
            Kicker = p.Name, Title = name, Icon = UiIcon.Calendar, Chips = new[] { other ? w.T("reallife_rota_not_in_use", "Not in use") : w.T("reallife_rota_in_use", "In use") },
            ChipTones = new[] { other ? UiRole.Muted : UiRole.Ok }, Back = CBack(w, null, CompanyView.Rota),
            FactLabels = new[] { w.T("reallife_rota_most_hours", "Most hours each"), w.T("reallife_rota_floor_hours", "Floor hours a week") }
        };
        int floorHours = 0;
        foreach (int[] b in blocks)
        {
            floorHours += (b[1] - b[0]) * b[2] * 7;
        }
        c.FactValues = new[] { p.HoursValue, w.F("reallife_rota_hours_n", "{hours} h", "hours", floorHours.ToString()) };
        var list = new List<CompanyBlock>
        {
            CSec(w.T("reallife_rota_plan", "Plan")),
            new CompanyBlock { Kind = CompanyBlockKind.Field, Text = w.T("reallife_rota_plan_name", "Name"), Value = name, Sub = w.T("reallife_rota_plan_name_hint", "Up to 24 letters"),
                Act = CA(CompanyDo.PlanChange, p.Id, id, 0, 0, name) },
            new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_rota_repeats", "Repeats"), Options = p.RepeatOptions, Chosen = p.Repeat, Acts = p.RepeatActs },
            new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_rota_first_day", "First day it holds for"), Sub = w.T("reallife_rota_range_hint", "Leave both empty for every week"),
                Value = w.T("reallife_rota_always", "Always"), CanLess = false, CanMore = true, Act = CA(CompanyDo.PlanChange, p.Id, id, 1, 0), Act2 = CA(CompanyDo.PlanChange, p.Id, id, 1, 214),
                Act3 = CA(CompanyDo.PlanChange, p.Id, id, 1) },
            new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_rota_last_day", "Last day it holds for"), Value = w.T("reallife_rota_always", "Always"), CanLess = false,
                CanMore = false, Act = CA(CompanyDo.PlanChange, p.Id, id, 2, 0), Act2 = CA(CompanyDo.PlanChange, p.Id, id, 2, 221), Act3 = CA(CompanyDo.PlanChange, p.Id, id, 2) },
            new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_rota_most_hours", "Most hours each"), Value = p.HoursValue, CanLess = true, CanMore = true, Act = p.HoursLessAct,
                Act2 = p.HoursMoreAct, Act3 = p.HoursTypeAct },
            new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_rota_overtime", "Overtime"), On = false, Value = w.T("reallife_co_off", "Off"), Act = p.OvertimeAct },
            // The main action alone on its row where there is one (use this plan); new, copy and delete are the one row of tools on both pages.
            other
                ? CBtns(CB(w.T("reallife_rota_use_this", "Use this plan"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.PlanUse, p.Id, id), UiIcon.Check))
                : null,
            CBtns(CB(w.T("reallife_rota_plan_new", "New plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanNew, p.Id), UiIcon.Plus), p.Copy,
                CB(w.T("reallife_rota_plan_delete_short", "Delete"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanDelete, p.Id, id), UiIcon.Close, false, null, true)),
            new CompanyBlock { Kind = CompanyBlockKind.Break },
            CSec(w.T("reallife_rota_needs", "Needs by time"), w.F("reallife_rota_blocks_count", "{count} of {most}", "count", blocks.Length.ToString(), "most", "4")),
            CNote(w.T("reallife_rota_needs_what", "A floor of crew in a block of hours, whatever the customers say."))
        };
        list.RemoveAll(b => b == null);   // (no use-this button on the plan in use)
        for (int i = 0; i < blocks.Length; i++)
        {
            list.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Stepper, Text = blocks[i][0].ToString("00") + "-" + blocks[i][1].ToString("00"), Value = w.F("reallife_rota_needs_crew", "{count} on shift", "count", blocks[i][2].ToString()),
                CanLess = true, CanMore = true, Act = CA(CompanyDo.PlanChange, p.Id, id, 7, blocks[i][2] - 1, i.ToString()), Act2 = CA(CompanyDo.PlanChange, p.Id, id, 7, blocks[i][2] + 1, i.ToString()),
                Act3 = CA(CompanyDo.PlanChange, p.Id, id, 7, 0, i.ToString())
            });
        }
        list.Add(CBtns(CB(w.T("reallife_rota_block_add", "Add a block"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanChange, p.Id, id, 6, 715), UiIcon.Plus),
            CB(w.T("reallife_rota_block_remove", "Remove the last"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanChange, p.Id, id, 8, 0, (blocks.Length - 1).ToString()), UiIcon.Close, true)));
        var from = new int[blocks.Length];
        var to = new int[blocks.Length];
        var crew = new int[blocks.Length];
        for (int i = 0; i < blocks.Length; i++)
        {
            from[i] = blocks[i][0];
            to[i] = blocks[i][1];
            crew[i] = blocks[i][2];
        }
        list.AddRange(CompanyBlocks.FloorAsks(w, from, to, crew, rota.People.Length));   // the people of the place
        c.Blocks = list.ToArray();
        r.Edit = c;
        var floor = new CompanyFloor { Title = w.T("reallife_rota_floor_chart", "The floor by hour"), Hours = new string[16], Crew = new int[16] };
        for (int h = 7; h < 23; h++)
        {
            floor.Hours[h - 7] = h.ToString("00");
            foreach (int[] b in blocks)
            {
                floor.Crew[h - 7] = h >= b[0] && h < b[1] ? b[2] : floor.Crew[h - 7];
            }
        }
        r.Floor = floor;
    }
}
