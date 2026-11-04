using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class CompanyLive
    {
        private static readonly string[] RepeatKeys = { "reallife_rota_repeat_every", "reallife_rota_repeat_odd", "reallife_rota_repeat_even" };
        private static readonly string[] ModeKeys = { "reallife_rota_mode_auto", "reallife_rota_mode_staff", "reallife_rota_mode_lean" };

        // The Rota tab: the places at the left, each with its saved plans under it, and at the right the chosen place's week by person and day, the day hour by hour and the
        // needs; the day, the planner's advice (and each rung's week, read only) and the plan's edit card are sub pages. A place is worked out only when picked (the director's sheet).
        private void ReadRota(CompanyFacts f, CompanyDirector d, CompanyView v, Words w)
        {
            CompanyRotaFacts r = f.Rota;
            if (!d.RotaOn)
            {
                r.PartOff = true;
                r.OffTitle = CompanyText.T("reallife_rota_tab");
                r.OffBody = CompanyText.T("reallife_rota_off");
                return;
            }
            IReadOnlyList<RotaPlace> places = d.RotaPlaces;
            string pick = v.Pick[CompanyView.Rota] ?? string.Empty;
            string place = pick.StartsWith("p:", StringComparison.Ordinal) ? pick.Substring(2) : pick.StartsWith("pl:", StringComparison.Ordinal) ? PlaceOfPlanPick(pick) : string.Empty;
            int planId = pick.StartsWith("pl:", StringComparison.Ordinal) ? PlanOfPick(pick) : 0;
            if (d.RotaPlaceAt(place) == null && places.Count > 0)
            {
                place = places[0].Place;   // the tab opens on the first place, as a list opens on its first row
                pick = "p:" + place;
                v.Pick[CompanyView.Rota] = pick;
                planId = 0;
            }
            var rows = new List<CompanyRow>(places.Count * 3);
            for (int i = 0; i < places.Count; i++)
            {
                RotaPlace p = places[i];
                bool gaps = p.Type != RotaType.Warehouse && !p.Shut && p.Staff > 0 && p.GapHours > 0;
                string hours = p.OpenHours > 0 ? CompanyText.T("reallife_co_rota_open", "hours", p.OpenHours.ToString()) : CompanyText.T("reallife_rota_closed");
                string state = p.Type == RotaType.Warehouse ? CompanyText.T("reallife_rota_row_none") : p.Staff == 0 ? CompanyText.T("reallife_rota_row_no_staff")
                    : gaps ? w.F("reallife_co_row_gap_short", "{hours} h short", "hours", p.GapHours.ToString()) : CompanyText.T("reallife_rota_row_covered");
                rows.Add(new CompanyRow
                {
                    Id = "p:" + p.Place, Title = p.Name, Sub = RotaGame.TypeName(p.Reg) + ", " + hours, Icon = UiIcon.Calendar, Figure = state, Note = w.F("reallife_co_tree_staff", "{count} staff",
                        "count", p.Staff.ToString()),
                    FigureTone = p.Type == RotaType.Warehouse ? UiRole.Muted : p.Staff == 0 ? UiRole.DangerText : gaps ? UiRole.WarnText : UiRole.OkText
                });
                AddPlanRows(rows, d, p.Place, w);
            }
            r.Places = new CompanyList
            {
                Title = w.T("reallife_rota_tab", "Rota"), Count = places.Count.ToString(), Rows = rows.ToArray(), Empty = CompanyText.T("reallife_co_row_no_business"),
                Chosen = ChosenRowId(rows, pick)
            };
            RotaPlace at = d.RotaPlaceAt(place);
            if (at == null)
            {
                return;
            }
            r.Place = RotaPlaceFacts(d, v, w, at, planId, r);
        }

        private static string PlaceOfPlanPick(string pick)
        {
            int last = pick.LastIndexOf(':');
            return last > 3 ? pick.Substring(3, last - 3) : string.Empty;
        }

        private static int PlanOfPick(string pick)
        {
            int last = pick.LastIndexOf(':');
            int id;
            return last > 0 && int.TryParse(pick.Substring(last + 1), out id) ? id : 0;
        }

        private static int ChosenRowId(List<CompanyRow> rows, string id)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        // The saved plans of a place under its row, a step in and dim: the one in use marked.
        private static void AddPlanRows(List<CompanyRow> rows, CompanyDirector d, string place, Words w)
        {
            RotaPlanSet set = d.RotaPlansOf(place, false);
            if (set == null)
            {
                return;
            }
            RotaPlanFile inUse = d.RotaPlanFor(place, d.RotaPlanDay());
            for (int i = 0; i < set.Plans.Count; i++)
            {
                RotaPlanFile p = set.Plans[i];
                string range = p.Dated ? CompanyText.T("reallife_rota_plan_range", "from", p.From.ToString(), "to", p.To.ToString()) : CompanyText.T(RepeatKeys[CompanyCatalogue.Clamp(p.Parity, 0,
                    2)]);
                rows.Add(new CompanyRow
                {
                    Id = "pl:" + place + ":" + p.Id, Title = p.Name, Sub = range, Icon = UiIcon.ClipboardText, Dim = true, Nested = true,
                    Chip = inUse != null && inUse.Id == p.Id ? w.T("reallife_rota_in_use", "In use") : null, ChipTone = UiRole.Ok
                });
            }
        }

        // The page of one place and one of its plans.
        private CompanyRotaPlace RotaPlaceFacts(CompanyDirector d, CompanyView v, Words w, RotaPlace at, int planId, CompanyRotaFacts r)
        {
            string place = at.Place;
            bool gaps = !at.Shut && at.Staff > 0 && at.GapHours > 0;
            string hoursWords = at.OpenHours > 0 ? CompanyText.T("reallife_co_rota_open", "hours", at.OpenHours.ToString()) : CompanyText.T("reallife_rota_closed");
            var p = new CompanyRotaPlace { Id = place, Name = at.Name, Sub = RotaGame.TypeName(at.Reg) + ", " + hoursWords };
            RotaSheet sheet = d.RotaSheetFor(at);
            if (sheet == null)
            {
                p.Refused = CompanyText.T(d.RotaUnread(place) ? "reallife_rota_cannot" : d.RotaWorking(place) ? "reallife_rota_working" : "reallife_rota_off");
                p.RefusedCard = RefusedCard(at, p.Refused, null, w);
                return p;
            }
            string refusal = CompanyDirector.RotaRefusal(sheet);
            if (at.Type == RotaType.Warehouse || sheet.Stations.Count == 0)
            {
                p.Refused = CompanyApp.RotaRefusedWords(refusal);
                p.RefusedCard = RefusedCard(at, p.Refused, sheet, w);
                return p;
            }
            RotaShop shop = sheet.Shop;
            RotaLadderResult l = sheet.Ladder;
            RotaRung now = l.Rungs[0];
            int day = d.RotaPlanDay();
            // The plan in view: the one picked, else the one in use; a place with no saved plan shows the standard week, made when the player first changes it.
            RotaPlanSet set = d.RotaPlansOf(place, false);
            RotaPlanFile plan = set != null && planId > 0 ? set.Find(planId) : d.RotaPlanFor(place, day);
            if (plan == null && set != null && set.Plans.Count > 0)
            {
                plan = set.Plans[0];
            }
            int id = plan != null ? plan.Id : 0;
            p.Weekly = CompanyText.T("reallife_rota_weekly", "week", RotaPlanRules.WeekOf(day).ToString(), "hours", at.OpenHours.ToString());
            // The plan's settings.
            string[] names = new string[set != null ? set.Plans.Count : 1];
            for (int i = 0; set != null && i < set.Plans.Count; i++)
            {
                names[i] = set.Plans[i].Name;
                p.PlanChosen = set.Plans[i].Id == id ? i : p.PlanChosen;
            }
            if (set == null)
            {
                names[0] = CompanyText.T("reallife_rota_plan_standard");
            }
            p.PlanNames = names;
            p.RepeatOptions = new string[3];
            p.RepeatActs = new CompanyAct[3];
            for (int i = 0; i < 3; i++)
            {
                p.RepeatOptions[i] = CompanyText.T(RepeatKeys[i]);
                p.RepeatActs[i] = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldParity, i);
            }
            p.Repeat = plan != null ? CompanyCatalogue.Clamp(plan.Parity, 0, 2) : 0;
            int hours = plan != null ? plan.Hours : RotaPlanRules.HoursStart;
            p.HoursValue = hours <= 0 ? CompanyText.T("reallife_rota_hours_own") : CompanyText.T("reallife_rota_hours_n", "hours", hours.ToString());
            p.HoursLess = hours > 0;
            p.HoursMore = hours < RotaPlanRules.HoursTop;
            int less = hours <= RotaPlanRules.HoursLeast ? 0 : hours - 4, more = hours <= 0 ? 40 : Math.Min(RotaPlanRules.HoursTop, hours + 4);
            p.HoursLessAct = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldHours, less);
            p.HoursMoreAct = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldHours, more);
            p.HoursTypeAct = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldHours);
            p.Overtime = plan != null && plan.Overtime;
            p.OvertimeAct = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldOvertime, p.Overtime ? 0 : 1);
            p.Edit = Btn(w.T("reallife_rota_edit", "Edit"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, null, CompanyView.Rota, CompanyView.SubEdit), UiAction.Count, UiIcon.Sliders);
            p.Copy = Btn(w.T("reallife_rota_copy", "Copy plan"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.PlanCopy, place, id), UiAction.Count, UiIcon.ClipboardText, false,
                set != null && set.Plans.Count >= RotaPlanRules.PlansMost, CompanyText.T("reallife_rota_plans_most", "count", RotaPlanRules.PlansMost.ToString()));
            p.Fill = Btn(CompanyText.T("reallife_rota_fill"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.RotaFill, place, -1), UiAction.Alt, UiIcon.Check, false, refusal != null, refusal != null
                ? CompanyApp.RotaRefusedWords(refusal) : null);
            if (d.RotaHasUndo(place))
            {
                p.Back = Btn(CompanyText.T("reallife_rota_back"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RotaBack, place), UiAction.Count, UiIcon.Back, true);
            }
            if (l.ModeMatters && refusal == null)
            {
                p.ModeTitle = w.T("reallife_rota_mode_title", "Mode");
                p.ModeOptions = new[] { w.T("reallife_rota_mode_auto_short", "Auto"), CompanyText.T(ModeKeys[1]), CompanyText.T(ModeKeys[2]) };
                p.ModeActs = new CompanyAct[ModeKeys.Length];
                for (int i = 0; i < ModeKeys.Length; i++)
                {
                    p.ModeActs[i] = CompanyAct.Of(CompanyDo.RotaMode, place, i);
                }
                p.Mode = d.RotaModeOf(place);
                if (p.Mode == RotaMode.Auto)
                {
                    p.ModePicked = w.F("reallife_rota_mode_picked", "Auto chose {mode}", "mode", CompanyText.T(l.Mode == RotaMode.StaffFirst ? "reallife_rota_mode_staff" : "reallife_rota_mode_lean"));
                }
            }
            // The week as the place has it now.
            p.Day = Math.Max(0, Math.Min(6, v.Day));
            WeekFacts(p, sheet, now, d, w);
            p.Ahead = PlanAhead(set, plan, day, w);
            DayFacts(p, sheet, now, p.Day, w);
            NeedsFacts(p, plan, place, w);
            int sub = v.Sub[CompanyView.Rota];
            if (sub == CompanyView.SubDay)
            {
                p.Board = BoardOf(sheet, now, p.Day, w);
            }
            if (sub == CompanyView.SubAdvice)
            {
                AdviceFacts(p, d, sheet, w);
                int rung = v.SubArg[CompanyView.Rota] - 1;
                if (rung >= 0 && rung < l.Count)
                {
                    p.Look = LookFacts(p, d, sheet, rung, w);
                }
            }
            if (sub == CompanyView.SubEdit)
            {
                r.Edit = EditCard(d, place, plan, set, p, w);
                r.Floor = FloorOf(plan, w);
            }
            if (!gaps && at.Staff == 0)
            {
                p.Weekly = CompanyText.T("reallife_rota_row_no_staff");
            }
            return p;
        }

        // What a place with no rota has instead of one: why, what it does have (a warehouse's own page, where its drivers get their vehicles) and its crew, with the way on to Staff.
        private static CompanyCard RefusedCard(RotaPlace at, string reason, RotaSheet sheet, Words w)
        {
            CompanyAct staff = CompanyAct.Of(CompanyDo.Go, null, CompanyView.Staff, CompanyView.SubMain);
            var blocks = new List<CompanyBlock>(6) { Note(reason) };
            if (at.Type == RotaType.Warehouse)
            {
                blocks.Add(Sec(w.T("reallife_rota_has_title", "What it has")));
                blocks.Add(new CompanyBlock
                {
                    Kind = CompanyBlockKind.Items, Items = new[]
                    {
                        new CompanyItem
                        {
                            Title = w.T("reallife_rota_has_page", "Its own page"), Sub = w.T("reallife_rota_has_page_what", "Where its drivers get their vehicles"), Icon = UiIcon.Truck, Press = true,
                            Act = CompanyAct.Of(CompanyDo.BizMan, at.Place), Grade = -1
                        }
                    }
                });
            }
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Section, Text = w.T("reallife_rota_has_crew", "Its crew"), Value = w.T("reallife_rota_has_staff_link", "Open Staff"), Act = staff });
            int crew = sheet != null ? Math.Min(sheet.Workers.Count, 8) : 0;
            var people = new CompanyItem[crew];
            for (int i = 0; i < crew; i++)
            {
                string name = RotaGame.WorkerName(sheet.Workers[i]);
                people[i] = new CompanyItem { Title = name, Sub = RotaGame.Skills(sheet.Workers[i]), Face = true, Hue = UiColour.HueOf(name), Ring = UiRole.Ok, Press = true, Act = staff, Grade = -1 };
            }
            if (crew > 0)
            {
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = people });
            }
            else
            {
                blocks.Add(Row(w.T("reallife_co_tab_staff", "Staff"), w.F("reallife_co_tree_staff", "{count} staff", "count", at.Staff.ToString())));
            }
            return new CompanyCard
            {
                Title = at.Name, Sub = RotaGame.TypeName(at.Reg), Icon = UiIcon.Calendar, Chips = new[] { w.T("reallife_rota_row_none", "No rota") }, ChipTones = new[] { UiRole.Muted },
                Blocks = blocks.ToArray()
            };
        }

        // The editable figures of a plan, as the director numbers them (CompanyDirector.PlanName and the rest).
        private const int RotaPlanFieldName = CompanyDirector.PlanName, RotaPlanFieldFrom = CompanyDirector.PlanFrom,
            RotaPlanFieldTo = CompanyDirector.PlanTo, RotaPlanFieldParity = CompanyDirector.PlanParity, RotaPlanFieldHours = CompanyDirector.PlanHours,
            RotaPlanFieldOvertime = CompanyDirector.PlanOvertime, RotaPlanFieldBlock = CompanyDirector.PlanBlock, RotaPlanFieldBlockCrew = CompanyDirector.PlanBlockCrew,
            RotaPlanFieldBlockDelete = CompanyDirector.PlanBlockDelete;

        // One person's week under a plan: their shift on each day as runs of hours, a day that breaks a demand marked, the hours and the day totals added to. A worker beyond the people
        // there are now is somebody the plan would take on.
        private static CompanyRotaPerson WeekPerson(RotaSheet sheet, RotaPlan plan, int wk, int[] dayTotals, out int hours)
        {
            int stations = sheet.Stations.Count;
            var person = new CompanyRotaPerson { Shifts = new string[7], Tones = new UiRole[7] };
            hours = 0;
            for (int dd = 0; dd < RotaCatalogue.Days; dd++)
            {
                var runs = new List<int>(4);
                int from = -1;
                for (int h = 0; h <= RotaCatalogue.Hours; h++)
                {
                    bool works = false;
                    for (int s = 0; h < RotaCatalogue.Hours && s < stations && !works; s++)
                    {
                        works = plan.At[(dd * RotaCatalogue.Hours + h) * stations + s] == wk;
                    }
                    if (works)
                    {
                        hours++;
                        dayTotals[dd]++;
                        from = from < 0 ? h : from;
                    }
                    else if (from >= 0)
                    {
                        runs.Add(from * 100 + h);
                        from = -1;
                    }
                }
                if (runs.Count > 0)
                {
                    var text = new System.Text.StringBuilder(16);
                    for (int k = 0; k < runs.Count; k++)
                    {
                        text.Append(k > 0 ? ", " : string.Empty).Append((runs[k] / 100).ToString("00")).Append('-').Append((runs[k] % 100).ToString("00"));
                    }
                    person.Shifts[dd] = text.ToString();
                }
                for (int u = 0; u < plan.Unmet.Count; u++)
                {
                    person.Tones[dd] = plan.Unmet[u].Worker == wk && plan.Unmet[u].Day == dd ? UiRole.WarnText : person.Tones[dd];
                }
            }
            return person;
        }

        // Each person of the rota, their shift on each day, the hours of each day and the sums.
        private void WeekFacts(CompanyRotaPlace p, RotaSheet sheet, RotaRung now, CompanyDirector d, Words w)
        {
            RotaShop shop = sheet.Shop;
            RotaPlan plan = now.Plan;
            p.DayNames = new string[7];
            for (int i = 0; i < 7; i++)
            {
                p.DayNames[i] = Truncate3(CompanyApp.RotaDayWords(i));
            }
            var people = new List<CompanyRotaPerson>(sheet.Workers.Count);
            var dayTotals = new int[7];
            int total = 0;
            for (int wk = 0; wk < sheet.Workers.Count && wk < sheet.Problem.Workers; wk++)
            {
                EmployeeInstance who = sheet.Workers[wk];
                string name = RotaGame.WorkerName(who);
                int hours;
                CompanyRotaPerson person = WeekPerson(sheet, plan, wk, dayTotals, out hours);
                person.Name = name;
                person.Role = RotaGame.Skills(who);
                person.Hue = UiColour.HueOf(name);
                person.Ring = sheet.Problem.Worker[wk].Mood < 40 ? UiRole.Danger : sheet.Problem.Worker[wk].Mood < 70 ? UiRole.Warn : UiRole.Ok;
                total += hours;
                person.Hours = hours + "h";
                CompanyApp.RotaRangeWords(sheet, now, wk, out int standing);
                person.HoursTone = standing == 2 || CompanyApp.RotaUnmetWords(sheet, plan, wk) != null ? UiRole.WarnText : UiRole.None;
                people.Add(person);
            }
            p.People = people.ToArray();
            p.DayTotals = new string[7];
            for (int i = 0; i < 7; i++)
            {
                p.DayTotals[i] = dayTotals[i] > 0 ? dayTotals[i] + "h" : "-";
            }
            p.TotalHours = total + "h";
            p.SummaryTitle = w.T("reallife_rota_week_comes_to", "What the week comes to");
            RotaWeekSum s2 = now.Sum;
            if (shop.HasCustomers)
            {
                p.SummaryLabels = new[] { w.T("reallife_rota_sum_served", "Served"), w.T("reallife_rota_sum_lost", "Lost"), w.T("reallife_rota_sum_wages_label", "Wages"),
                    w.T("reallife_rota_sum_net_label", "Net") };
                p.SummaryValues = new[] { s2.Served.ToString(), (s2.LostUnmanned + s2.LostFull).ToString(), CompanyApp.RotaMoneyWords(-s2.Wages), CompanyApp.RotaMoneyWords(s2.Net) };
                p.SummaryTones = new[] { UiRole.None, s2.LostUnmanned + s2.LostFull > 0 ? UiRole.WarnText : UiRole.None, UiRole.None, s2.Net >= 0 ? UiRole.OkText : UiRole.DangerText };
            }
            else
            {
                p.SummaryLabels = new[] { w.T("reallife_rota_sum_wages_label", "Wages"), w.T("reallife_rota_sum_covered", "Covered"), w.T("reallife_rota_sum_unmet", "Unmet") };
                p.SummaryValues = new[] { CompanyApp.RotaMoneyWords(-s2.Wages), plan.CoveredHours + "/" + plan.WantedHours, plan.UnmetCount.ToString() };
                p.SummaryTones = new[] { UiRole.None, plan.CoveredHours < plan.WantedHours ? UiRole.WarnText : UiRole.None, plan.UnmetCount > 0 ? UiRole.WarnText : UiRole.None };
            }
        }

        // The dated plan that holds next (or holds now and is not the one in view), against the one in view: the days it holds for, the hours it caps each person at and the
        // floor of crew it puts under the planner, in crew hours a week more or fewer than the plan in view has. Null where no dated plan is ahead.
        private static string PlanAhead(RotaPlanSet set, RotaPlanFile inView, int day, Words w)
        {
            RotaPlanFile next = null;
            for (int i = 0; set != null && i < set.Plans.Count; i++)
            {
                RotaPlanFile c = set.Plans[i];
                if (c.Dated && (inView == null || c.Id != inView.Id) && (c.To <= 0 || c.To >= day) && (next == null || c.From < next.From))
                {
                    next = c;
                }
            }
            if (next == null)
            {
                return null;
            }
            int floor = RotaPlanRules.FloorHours(next) - (inView != null ? RotaPlanRules.FloorHours(inView) : 0);
            string cap = next.Hours <= 0 ? CompanyText.T("reallife_rota_hours_own") : CompanyText.T("reallife_rota_hours_n", "hours", next.Hours.ToString());
            return w.F("reallife_co_plan_ahead", "{plan} holds from day {from} to day {to}: up to {cap} each, and {floor} crew hours of floor a week against this plan",
                "plan", next.Name, "from", Math.Max(1, next.From).ToString(), "to", next.To > 0 ? next.To.ToString() : "-", "cap", cap, "floor", (floor > 0 ? "+" : string.Empty) + floor);
        }

        private static string Truncate3(string day) => day != null && day.Length > 3 ? day.Substring(0, 3) : day ?? string.Empty;

        // The chosen day hour by hour: who is on shift against where the planner needs somebody, and the day's own figures.
        private void DayFacts(CompanyRotaPlace p, RotaSheet sheet, RotaRung now, int dd, Words w)
        {
            RotaShop shop = sheet.Shop;
            int stations = sheet.Stations.Count;
            var labels = new List<string>(24);
            var on = new List<int>(24);
            var need = new List<int>(24);
            int come = 0, served = 0;
            long earn = 0, cost = 0;
            for (int h = 0; h < RotaCatalogue.Hours; h++)
            {
                int cell = dd * RotaCatalogue.Hours + h, present = 0, wanted = 0;
                for (int s = 0; s < stations; s++)
                {
                    present += now.Plan.At[cell * stations + s] >= 0 ? 1 : 0;
                    wanted += sheet.Problem.Value[cell * stations + s] > 0 ? 1 : 0;
                }
                come += now.Come[cell];
                served += now.Served[cell];
                earn += now.Earn[cell];
                cost += now.Cost[cell];
                if (!shop.Open[cell] && present == 0 && wanted == 0)
                {
                    continue;
                }
                labels.Add(h.ToString("00"));
                on.Add(present);
                need.Add(wanted);
            }
            p.DayTitle = CompanyText.T("reallife_rota_day_title_short", "day", CompanyApp.RotaDayWords(dd));
            p.DayShort = CompanyApp.RotaDayWords(dd);
            p.DayNote = CompanyApp.RotaOpenWords(shop.Open, dd);
            p.HourLabels = labels.ToArray();
            p.OnShift = on.ToArray();
            p.Needed = need.ToArray();
            p.Legend = new[] { w.T("reallife_rota_legend_on", "On shift"), w.T("reallife_rota_legend_short", "Short"), w.T("reallife_rota_legend_need", "Needed") };
            if (shop.HasCustomers)
            {
                p.DayFigureLabels = new[] { w.T("reallife_rota_sum_served", "Served"), w.T("reallife_rota_sum_lost", "Lost"), w.T("reallife_rota_day_takings", "Takings"),
                    w.T("reallife_rota_day_costs", "Wages and theft") };
                p.DayFigureValues = new[] { served.ToString(), Math.Max(0, come - served).ToString(), CompanyApp.RotaMoneyWords(earn), CompanyApp.RotaMoneyWords(cost) };
            }
        }

        // What the plan asks for: a floor of crew in each block of hours; with none, the planner's own need.
        private void NeedsFacts(CompanyRotaPlace p, RotaPlanFile plan, string place, Words w)
        {
            p.NeedsTitle = w.T("reallife_rota_needs", "Needs by time");
            if (plan == null || plan.Blocks.Count == 0)
            {
                p.NeedWhen = new[] { w.T("reallife_rota_needs_own_when", "All hours") };
                p.NeedWho = new[] { w.T("reallife_rota_needs_own", "The planner's own need") };
                return;
            }
            p.NeedWhen = new string[plan.Blocks.Count];
            p.NeedWho = new string[plan.Blocks.Count];
            for (int i = 0; i < plan.Blocks.Count; i++)
            {
                RotaBlock b = plan.Blocks[i];
                p.NeedWhen[i] = b.From.ToString("00") + "-" + b.To.ToString("00");
                p.NeedWho[i] = CompanyText.T("reallife_rota_needs_crew", "count", b.Crew.ToString());
            }
        }

        // The day as lanes of people on an hour ruler: the board of the Day view. A bar carries its hours only; the lane's head says whose it is.
        private UiBoardModel BoardOf(RotaSheet sheet, RotaRung now, int dd, Words w)
        {
            RotaShop shop = sheet.Shop;
            int stations = sheet.Stations.Count;
            var days = new string[7];
            for (int i = 0; i < 7; i++)
            {
                days[i] = Truncate3(CompanyApp.RotaDayWords(i));
            }
            int first = 24, last = 0;
            for (int h = 0; h < RotaCatalogue.Hours; h++)
            {
                if (shop.Open[dd * RotaCatalogue.Hours + h])
                {
                    first = Math.Min(first, h);
                    last = Math.Max(last, h + 1);
                }
            }
            first = first == 24 ? 6 : Math.Max(0, first - 1);
            last = last == 0 ? 24 : Math.Min(24, last + 1);
            var hours = new string[last - first + 1];
            for (int h = first; h <= last; h++)
            {
                hours[h - first] = CompanyApp.RotaHourWords(h % 24);
            }
            var lanes = new List<UiBoardLane>(sheet.Workers.Count);
            var shifts = new List<UiBoardShift>(32);
            for (int wk = 0; wk < sheet.Workers.Count && wk < sheet.Problem.Workers; wk++)
            {
                string name = RotaGame.WorkerName(sheet.Workers[wk]);
                lanes.Add(new UiBoardLane { Name = name, Sub = RotaGame.Skills(sheet.Workers[wk]), Hue = UiColour.HueOf(name), Tint = UiRole.None });
                int from = -1;
                for (int h = 0; h <= RotaCatalogue.Hours; h++)
                {
                    bool works = false;
                    for (int s = 0; h < RotaCatalogue.Hours && s < stations && !works; s++)
                    {
                        works = now.Plan.At[(dd * RotaCatalogue.Hours + h) * stations + s] == wk;
                    }
                    if (works && from < 0)
                    {
                        from = h;
                    }
                    else if (!works && from >= 0)
                    {
                        shifts.Add(new UiBoardShift
                        {
                            Lane = lanes.Count - 1, Start = from * 60, End = h * 60, Name = null, Ring = UiRole.None,
                            Span = CompanyText.T("reallife_rota_from_to", "from", CompanyApp.RotaHourWords(from), "to", CompanyApp.RotaHourWords(h % 24))
                        });
                        from = -1;
                    }
                }
            }
            var cover = new string[24];
            var shortHere = new bool[24];
            for (int h = 0; h < 24; h++)
            {
                int cell = dd * RotaCatalogue.Hours + h, present = 0, wanted = 0;
                for (int s = 0; s < stations; s++)
                {
                    present += now.Plan.At[cell * stations + s] >= 0 ? 1 : 0;
                    wanted += sheet.Problem.Value[cell * stations + s] > 0 ? 1 : 0;
                }
                cover[h] = present == 0 && wanted == 0 ? null : present + "/" + wanted;
                shortHere[h] = present < wanted;
            }
            return new UiBoardModel
            {
                Days = days, Day = dd, FirstHour = first, LastHour = last, Hours = hours, OpenFrom = first * 60, OpenTo = last * 60, Lanes = lanes.ToArray(), Shifts = shifts.ToArray(), Faces = false,
                CoverTitle = w.T("reallife_rota_cover_title", "Staff / needed"), Cover = cover, Short = shortHere, EmptyTitle = CompanyText.T("reallife_rota_row_no_staff"),
                EmptyBody = CompanyText.T("reallife_rota_day_explain"), LegendOpen = w.T("reallife_ui_bizops_legend_open", "Open hours"),
                LegendClosed = w.T("reallife_ui_bizops_legend_closed", "Closed hours")
            };
        }

        // The planner's ladder from the week as it is to the last person worth hiring, your people against the advised plan, and what stands beside it.
        private void AdviceFacts(CompanyRotaPlace p, CompanyDirector d, RotaSheet sheet, Words w)
        {
            RotaLadderResult l = sheet.Ladder;
            RotaShop shop = sheet.Shop;
            string place = sheet.Place.Place;
            string refusal = CompanyDirector.RotaRefusal(sheet);
            p.AdviceTitle = w.T("reallife_rota_advice_title", "The planner's advice");
            if (l.Count >= 2)
            {
                RotaRung advised = l.Rungs[l.Recommended];
                long more = advised.Sum.Net - l.Rungs[0].Sum.Net;
                p.AdviceNote = l.Recommended == 0 ? CompanyText.T("reallife_rota_advice_none")
                    : advised.Hires > 0 ? CompanyText.T("reallife_rota_advice_hire", "plan", CompanyApp.RotaPlanWords(sheet, advised), "amount", CompanyApp.RotaMoneyWords(more))
                    : CompanyText.T(more < 0 ? "reallife_rota_advice_best_less" : "reallife_rota_advice_best", "amount", CompanyApp.RotaMoneyWords(Math.Abs(more)));
            }
            var rungs = new CompanyRung[l.Count];
            for (int i = 0; i < l.Count; i++)
            {
                RotaRung r = l.Rungs[i];
                bool unmanned = shop.HasCustomers && r.Sum.Manned != r.Sum.Hours;
                rungs[i] = new CompanyRung
                {
                    Name = CompanyApp.RotaPlanWords(sheet, r), Line = CompanyApp.RotaNeedsWords(sheet, r) + (r.Left.Count > 0 ? " " + CompanyText.T("reallife_rota_plan_leaves", "count",
                        r.Left.Count.ToString())
                        : r.Out > 0 ? " " + CompanyText.T("reallife_rota_plan_outside", "count", r.Out.ToString()) : string.Empty),
                    Hours = unmanned ? CompanyText.T("reallife_rota_plan_hours", "hours", r.Sum.Hours.ToString(), "manned", r.Sum.Manned.ToString()) : CompanyText.T("reallife_rota_plan_hours_all",
                        "hours", r.Sum.Hours.ToString()),
                    Net = shop.HasCustomers ? CompanyApp.RotaMoneyWords(r.Sum.Net) : CompanyApp.RotaMoneyWords(-r.Sum.Wages), Gain = i == 0 ? null : r.Gain > 0
                        ? CompanyText.T("reallife_rota_plan_gain", "amount", CompanyApp.RotaMoneyWords(r.Gain)) : null,
                    Advised = i == l.Recommended && l.Count > 1
                };
                SetUse(ref rungs[i], sheet, i, place, refusal, w);
                rungs[i].Look = Btn(w.T("reallife_rota_look", "Look"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, null, CompanyView.Rota, CompanyView.SubAdvice, i + 1), UiAction.Count,
                    UiIcon.Eye);
            }
            p.Rungs = rungs;
            RotaRung advisedRung = l.Rungs[l.Recommended];
            p.FolkTitle = w.T("reallife_rota_folk_title", "Your people and the advised plan");
            var folk = new List<CompanyRotaFolk>(8);
            var beside = new List<CompanyBlock>(16);
            for (int wk = 0; wk < sheet.Workers.Count && wk < sheet.Problem.Workers && wk < 8; wk++)
            {
                folk.Add(FolkOf(sheet, l, advisedRung, wk, w, beside));
            }
            var ordered = new List<CompanyRotaFolk>(folk.Count);   // the people the plan puts outside their range come first: only so many rows fit
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < folk.Count; i++)
                {
                    if ((folk[i].Note != null) == (pass == 0))
                    {
                        ordered.Add(folk[i]);
                    }
                }
            }
            p.Folk = ordered.ToArray();
            RotaOutlook o = sheet.Outlook;
            bool runsOut = l.Rungs[l.Recommended].Sum.StockShort > 0;
            var stations = new List<CompanyItem>(3);
            for (int i = 0; i < o.Missing.Count && i < 3; i++)
            {
                RotaMissing missing = o.Missing[i];
                stations.Add(new CompanyItem
                {
                    Title = CompanyText.T("reallife_rota_missing", "station", CompanyApp.RotaGroupWords(sheet, missing.Group)),
                    Sub = CompanyText.T(runsOut ? "reallife_rota_missing_stock" : "reallife_rota_missing_what", "customers", missing.Customers.ToString()), Figure = "+" + missing.Customers,
                    Note = w.T("reallife_rota_unit_customers", "customers a week"), FigureTone = runsOut ? UiRole.WarnText : UiRole.OkText, Icon = UiIcon.Plus,
                    Tint = runsOut ? UiRole.Warn : UiRole.Ok,
                    Grade = -1
                });
            }
            if (stations.Count > 0)
            {
                beside.Add(Sec(w.T("reallife_rota_head_stations", "Stations that would pay")));
                beside.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = stations.ToArray() });
            }
            for (int i = 0; i < sheet.Broken.Count && i < 3; i++)
            {
                beside.Add(Note(CompanyText.T("reallife_rota_broken", "station", sheet.Broken[i]), UiRole.WarnText));
            }
            if (shop.Stock.Products > 0 && runsOut)
            {
                RotaWeekSum adv = l.Rungs[l.Recommended].Sum;
                beside.Add(Note(CompanyText.T("reallife_rota_box_stock_short", "count", adv.StockShort.ToString(), "amount", CompanyApp.RotaMoneyWords(adv.StockLost)), UiRole.WarnText));
            }
            AddWaysOut(beside, d, sheet, l.Rungs[l.Recommended]);
            if (sheet.Late)
            {
                beside.Add(Note(CompanyText.T("reallife_rota_note_late", "fee", CompanyText.Money(LicenceRules.Fee((int)LicenceKind.Late, d.Index)), "days",
                    CompanyCatalogue.LicenceTerm[(int)LicenceKind.Late].ToString()), UiRole.WarnText));
            }
            if (shop.Cleaned)
            {
                beside.Add(Note(CompanyText.T("reallife_rota_cleaned")));
            }
            p.Beside = beside.ToArray();
        }

        // The button that uses a rung, or the short word in its place: the week as it is is in use already, a plan that takes on people waits for them to be hired, and the
        // director writes only a plan its people can keep.
        private static void SetUse(ref CompanyRung rung, RotaSheet sheet, int i, string place, string refusal, Words w)
        {
            RotaRung r = sheet.Ladder.Rungs[i];
            bool hires = r.Hires > 0;
            bool usable = !hires && CompanyDirector.RotaRungOf(sheet, i) != null && refusal == null;
            rung.Use = Btn(CompanyText.T(hires ? "reallife_rota_use_after" : "reallife_rota_use_plan"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RotaFill, place, i), UiAction.Count,
                UiIcon.Check, false, !usable, hires ? CompanyText.T("reallife_rota_hire_where") : refusal != null ? CompanyApp.RotaRefusedWords(refusal) : null);
            if (hires)
            {
                rung.Chip = w.T("reallife_rota_hire_first", "Hire first");
                rung.ChipTone = UiRole.Warn;
            }
            else if (i == 0)
            {
                rung.Chip = w.T("reallife_rota_in_use", "In use");
                rung.ChipTone = UiRole.Ok;
            }
        }

        // One person against the advised plan: what they ask for as a range of hours, the hours the plan gives and how far outside the range that is. What keeps somebody from their
        // hours is a line beside the people.
        private static CompanyRotaFolk FolkOf(RotaSheet sheet, RotaLadderResult l, RotaRung advised, int wk, Words w, List<CompanyBlock> beside)
        {
            EmployeeInstance who = sheet.Workers[wk];
            string name = RotaGame.WorkerName(who);
            int hours = CompanyApp.RotaWorkerHours(advised, wk), least, most;
            RotaFit.Range(sheet.Problem.Worker[wk], out least, out most);
            bool fixedOne = wk < sheet.Fixed.Length && sheet.Fixed[wk];
            bool ranged = !fixedOne && (least > 0 || most != int.MaxValue);
            string words = CompanyApp.RotaRangeWords(sheet, advised, wk, out int standing);
            bool left = false;
            for (int i = 0; i < advised.Left.Count; i++)
            {
                left |= advised.Left[i].Worker == wk;
            }
            var folk = new CompanyRotaFolk
            {
                Name = name, Role = RotaGame.Skills(who), Hue = UiColour.HueOf(name), Ring = sheet.Problem.Worker[wk].Mood < 40 ? UiRole.Danger : sheet.Problem.Worker[wk].Mood < 70 ? UiRole.Warn
                    : UiRole.Ok,
                Asks = fixedOne ? CompanyText.T("reallife_rota_person_fixed") : CompanyApp.RotaAsksWords(sheet.Problem.Worker[wk]),
                Figure = CompanyText.T("reallife_rota_hours_n", "hours", hours.ToString()), Plan = hours, Least = ranged ? least : 0, Most = ranged && most != int.MaxValue ? most : 0,
                Tone = standing == 2 ? UiRole.WarnText : UiRole.None
            };
            if (left)
            {
                folk.Note = w.T("reallife_rota_hours_none", "Gets no hours");
                folk.Tone = UiRole.DangerText;
                beside.Add(Note(words, UiRole.WarnText));
            }
            else if (ranged && standing == 2)
            {
                folk.Note = hours < least ? w.F("reallife_rota_hours_under", "{hours} h under", "hours", (least - hours).ToString())
                    : w.F("reallife_rota_hours_over", "{hours} h over", "hours", (hours - most).ToString());
            }
            return folk;
        }

        // A rung's plan read as a week, applying nothing: who works when under it (and who it would take on), what it comes to, and what it changes in the opening hours and why.
        private CompanyRotaLook LookFacts(CompanyRotaPlace p, CompanyDirector d, RotaSheet sheet, int index, Words w)
        {
            RotaLadderResult l = sheet.Ladder;
            RotaShop shop = sheet.Shop;
            RotaRung r = l.Rungs[index], now = l.Rungs[0];
            string place = sheet.Place.Place;
            string refusal = CompanyDirector.RotaRefusal(sheet);
            var look = new CompanyRotaLook
            {
                Name = CompanyApp.RotaPlanWords(sheet, r), Line = CompanyApp.RotaNeedsWords(sheet, r), Why = CompanyApp.RotaWhyPlan(sheet, r, index), Advised = index == l.Recommended && l.Count > 1,
                ChangesTitle = w.T("reallife_rota_look_changes", "What it changes"), ChangesNone = w.T("reallife_rota_look_same", "It keeps the opening hours you have."),
                Back = Btn(w.T("reallife_rota_look_back", "Back to the advice"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, null, CompanyView.Rota, CompanyView.SubAdvice, 0), UiAction.Count,
                    UiIcon.Back)
            };
            long diff = r.Sum.Net - now.Sum.Net;
            look.FigureLabels = new[] { w.T("reallife_rota_look_open", "Open a week"), w.T("reallife_rota_sum_wages_label", "Wages"), shop.HasCustomers ? w.T("reallife_rota_sum_net_label", "Net")
                : w.T("reallife_rota_sum_covered", "Covered"), w.T("reallife_rota_look_gain", "Against now") };
            look.FigureValues = new[] { CompanyText.T("reallife_rota_hours_n", "hours", r.Sum.Hours.ToString()), CompanyApp.RotaMoneyWords(-r.Sum.Wages), shop.HasCustomers
                ? CompanyApp.RotaMoneyWords(r.Sum.Net) : r.Plan.CoveredHours + "/" + r.Plan.WantedHours, (diff > 0 ? "+" : string.Empty) + CompanyApp.RotaMoneyWords(diff) };
            var people = new List<CompanyRotaPerson>(r.Workers);
            var dayTotals = new int[7];
            int total = 0;
            for (int wk = 0; wk < r.Workers; wk++)
            {
                int hours;
                CompanyRotaPerson person = WeekPerson(sheet, r.Plan, wk, dayTotals, out hours);
                if (wk < sheet.Workers.Count && wk < sheet.Problem.Workers)
                {
                    EmployeeInstance who = sheet.Workers[wk];
                    string name = RotaGame.WorkerName(who);
                    person.Name = name;
                    person.Role = RotaGame.Skills(who) + ", " + w.F("reallife_rota_was_hours", "was {hours} h", "hours", CompanyApp.RotaWorkerHours(now, wk).ToString());
                    person.Hue = UiColour.HueOf(name);
                    person.Ring = sheet.Problem.Worker[wk].Mood < 40 ? UiRole.Danger : sheet.Problem.Worker[wk].Mood < 70 ? UiRole.Warn : UiRole.Ok;
                    CompanyApp.RotaRangeWords(sheet, r, wk, out int standing);
                    person.HoursTone = standing == 2 || CompanyApp.RotaUnmetWords(sheet, r.Plan, wk) != null ? UiRole.WarnText : UiRole.None;
                }
                else
                {
                    int group = wk - sheet.Workers.Count < r.Hires ? r.Hire[wk - sheet.Workers.Count] : -1;
                    person.ToHire = true;
                    person.Name = CompanyText.T("reallife_rota_plan_hired", "skill", RotaGame.GroupSkill(sheet, group));
                    person.Role = CompanyText.T("reallife_rota_person_wage", "wage", CompanyText.Wage(RotaLadder.Going(shop, group)));
                }
                total += hours;
                person.Hours = hours + "h";
                people.Add(person);
            }
            look.People = people.ToArray();
            look.DayTotals = new string[7];
            for (int i = 0; i < 7; i++)
            {
                look.DayTotals[i] = dayTotals[i] > 0 ? dayTotals[i] + "h" : "-";
            }
            look.TotalHours = total + "h";
            var changes = new List<CompanyRotaChange>(r.Changes.Count);
            for (int i = 0; i < r.Changes.Count; i++)
            {
                RotaChange c = r.Changes[i];
                bool opens = c.Why == RotaWhy.Opened || c.Why == RotaWhy.Fits;
                long delta = c.Will - c.Was;
                changes.Add(new CompanyRotaChange
                {
                    Title = CompanyApp.RotaDayWords(c.Day) + ": " + CompanyText.T(opens ? "reallife_rota_change_open" : "reallife_rota_change_close", "from", CompanyApp.RotaHourWords(c.From), "to",
                        CompanyApp.RotaHourWords(c.To)),
                    Sub = ChangeWhy(sheet, c), Figure = (delta > 0 ? "+" : string.Empty) + CompanyApp.RotaMoneyWords(delta), Opens = opens
                });
            }
            look.Changes = changes.ToArray();
            if (r.Hires > 0)
            {
                look.Chip = w.T("reallife_rota_hire_first", "Hire first");
            }
            else if (index == 0)
            {
                look.Chip = w.T("reallife_rota_in_use", "In use");
            }
            else
            {
                bool usable = CompanyDirector.RotaRungOf(sheet, index) != null && refusal == null;
                look.Use = Btn(CompanyText.T("reallife_rota_use_plan"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.RotaFill, place, index), UiAction.Alt, UiIcon.Check, false, !usable,
                    refusal != null ? CompanyApp.RotaRefusedWords(refusal) : null);
            }
            return look;
        }

        // Why a plan opens or closes a run of hours, as the old plan page says it.
        private static string ChangeWhy(RotaSheet sheet, RotaChange c)
        {
            return c.Why == RotaWhy.Opened ? CompanyText.T("reallife_rota_change_why_open")
                : c.Why == RotaWhy.NoCrew ? CompanyText.T("reallife_rota_change_why_crew", "station", CompanyApp.RotaGroupWords(sheet, c.Group), "skill", RotaGame.GroupSkill(sheet, c.Group))
                : c.Why == RotaWhy.NoPay ? CompanyText.T("reallife_rota_change_why_pay")
                : c.Why == RotaWhy.Barred ? CompanyText.T("reallife_rota_change_why_barred")
                : c.Why == RotaWhy.Fits ? CompanyText.T("reallife_rota_change_why_fits", "name", c.Group >= 0 && c.Group < sheet.Workers.Count ? RotaGame.WorkerName(sheet.Workers[c.Group]) : "?")
                : CompanyText.T("reallife_rota_change_why_day");
        }

        // For somebody whose hours the advised plan cannot meet: where else they could go, in the order the mod can help with them (the business that is short of people, the cleaning
        // service), and that a replacement would fit better. As the old week page says it.
        private static void AddWaysOut(List<CompanyBlock> beside, CompanyDirector d, RotaSheet sheet, RotaRung r)
        {
            int rows = 0;
            for (int i = 0; i < r.Plan.Unmet.Count && rows < 3; i++)
            {
                RotaUnmet unmet = r.Plan.Unmet[i];
                if (unmet.Worker >= sheet.Workers.Count || unmet.Reason != RotaReason.Short)
                {
                    continue;
                }
                EmployeeInstance who = sheet.Workers[unmet.Worker];
                RotaPlace busier = d.RotaBusier(sheet.Place, who);
                bool cleans = false;
                for (int s = 0; s < sheet.Stations.Count; s++)
                {
                    cleans |= sheet.Shop.GroupKind[sheet.Shop.Group[s]] == RotaGroupKind.Cleaning && sheet.Problem.Can[unmet.Worker * sheet.Stations.Count + s] != 0;
                }
                string words = busier != null ? CompanyText.T("reallife_rota_way_busier", "name", RotaGame.WorkerName(who), "business", busier.Name, "hours", busier.GapHours.ToString())
                    : CompanyText.T("reallife_rota_way_none", "name", RotaGame.WorkerName(who));
                words += cleans && CompanyDirector.RotaCleaningService ? " " + CompanyText.T("reallife_rota_way_service") : string.Empty;
                beside.Add(Note(words + " " + CompanyText.T("reallife_rota_way_replace")));
                rows++;
            }
        }

        // The plan's own page in two columns: at the left its name, how it repeats, the days it holds for, the most hours and overtime, and its buttons; at the right the floor of crew in
        // each block of hours, one row a block, counted as they stand.
        private CompanyCard EditCard(CompanyDirector d, string place, RotaPlanFile plan, RotaPlanSet set, CompanyRotaPlace shown, Words w)
        {
            var card = new CompanyCard
            {
                Kicker = d.ManagedName(place), Title = plan != null ? plan.Name : CompanyText.T("reallife_rota_plan_standard"), Icon = UiIcon.Calendar,
                Back = new[] { Btn(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, null, CompanyView.Rota, CompanyView.SubMain), UiAction.Count, UiIcon.Back) }
            };
            if (plan == null)
            {
                card.Blocks = new[] { Note(CompanyText.T("reallife_rota_edit_none")) };
                return card;
            }
            int id = plan.Id;
            bool inUse = d.RotaPlanFor(place, d.RotaPlanDay()) == plan;
            card.Chips = new[] { inUse ? w.T("reallife_rota_in_use", "In use") : plan.Dated ? CompanyText.T("reallife_rota_plan_range", "from", plan.From.ToString(), "to", plan.To.ToString())
                : w.T("reallife_rota_not_in_use", "Not in use") };
            card.ChipTones = new[] { inUse ? UiRole.Ok : UiRole.Muted };
            card.FactLabels = new[] { w.T("reallife_rota_most_hours", "Most hours each"), w.T("reallife_rota_floor_hours", "Floor hours a week") };
            card.FactValues = new[] { shown.HoursValue, CompanyText.T("reallife_rota_hours_n", "hours", RotaPlanRules.FloorHours(plan).ToString()) };
            var blocks = new List<CompanyBlock>(24);
            blocks.Add(Sec(w.T("reallife_rota_plan", "Plan")));
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Field, Text = w.T("reallife_rota_plan_name", "Name"), Value = plan.Name, Sub = CompanyText.T("reallife_rota_plan_name_hint"),
                Act = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldName, 0, plan.Name)
            });
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Choice, Text = w.T("reallife_rota_repeats", "Repeats"), Options = shown.RepeatOptions, Chosen = shown.Repeat, Acts = shown.RepeatActs
            });
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_rota_first_day", "First day it holds for"), Sub = CompanyText.T("reallife_rota_range_hint"),
                Value = plan.From > 0 ? CompanyText.Day(plan.From) : CompanyText.T("reallife_rota_always"), CanLess = plan.From > 0, CanMore = true,
                Act = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldFrom, plan.From <= 1 ? 0 : plan.From - 7),
                Act2 = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldFrom, plan.From <= 0 ? d.Today : plan.From + 7),
                Act3 = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldFrom)
            });
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_rota_last_day", "Last day it holds for"),
                Value = plan.To > 0 ? CompanyText.Day(plan.To) : CompanyText.T("reallife_rota_always"), CanLess = plan.To > 0, CanMore = plan.From > 0,
                Act = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldTo, plan.To <= 1 ? 0 : plan.To - 7),
                Act2 = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldTo, plan.To <= 0 ? Math.Max(plan.From, d.Today) + 7 : plan.To + 7),
                Act3 = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldTo)
            });
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_rota_most_hours", "Most hours each"), Value = shown.HoursValue, CanLess = shown.HoursLess, CanMore = shown.HoursMore,
                Act = shown.HoursLessAct, Act2 = shown.HoursMoreAct, Act3 = shown.HoursTypeAct
            });
            blocks.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Switch, Text = w.T("reallife_rota_overtime", "Overtime"), On = shown.Overtime,
                Value = shown.Overtime ? w.T("reallife_co_on", "On") : w.T("reallife_co_off", "Off"),
                Act = shown.OvertimeAct
            });
            var plansButtons = new List<CompanyButton>(2);
            if (!plan.Dated && !inUse)
            {
                plansButtons.Add(Btn(CompanyText.T("reallife_rota_use_this"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.PlanUse, place, id), UiAction.Alt, UiIcon.Check));
            }
            plansButtons.Add(Btn(CompanyText.T("reallife_rota_plan_new"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.PlanNew, place), UiAction.Count, UiIcon.Plus, false,
                set != null && set.Plans.Count >= RotaPlanRules.PlansMost, CompanyText.T("reallife_rota_plans_most", "count", RotaPlanRules.PlansMost.ToString())));
            blocks.Add(Buttons(plansButtons.ToArray()));
            int undated = 0;
            for (int i = 0; set != null && i < set.Plans.Count; i++)
            {
                undated += set.Plans[i].Dated ? 0 : 1;
            }
            blocks.Add(Buttons(shown.Copy, Btn(CompanyText.T("reallife_rota_plan_delete"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.PlanDelete, place, id), UiAction.Count, UiIcon.Close, true,
                set == null || (!plan.Dated && undated <= 1), CompanyText.T("reallife_rota_plan_delete_last"))));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            blocks.Add(Sec(w.T("reallife_rota_needs", "Needs by time"), CompanyText.T("reallife_rota_blocks_count", "count", plan.Blocks.Count.ToString(), "most",
                RotaPlanRules.BlocksMost.ToString())));
            blocks.Add(Note(CompanyText.T("reallife_rota_needs_what")));
            for (int i = 0; i < plan.Blocks.Count; i++)
            {
                RotaBlock b = plan.Blocks[i];
                blocks.Add(new CompanyBlock
                {
                    Kind = CompanyBlockKind.Stepper, Text = b.From.ToString("00") + "-" + b.To.ToString("00"), Value = CompanyText.T("reallife_rota_needs_crew", "count", b.Crew.ToString()),
                    CanLess = b.Crew > 0, CanMore = b.Crew < RotaPlanRules.CrewMost,
                    Act = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldBlockCrew, Math.Max(0, b.Crew - 1), i.ToString()),
                    Act2 = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldBlockCrew, Math.Min(RotaPlanRules.CrewMost, b.Crew + 1), i.ToString()),
                    Act3 = CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldBlockCrew, 0, i.ToString())
                });
            }
            var buttons = new List<CompanyButton>(2);
            if (plan.Blocks.Count < RotaPlanRules.BlocksMost)
            {
                buttons.Add(Btn(CompanyText.T("reallife_rota_block_add"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldBlock, 700 + 15), UiAction.Count,
                    UiIcon.Plus));
            }
            if (plan.Blocks.Count > 0)
            {
                buttons.Add(Btn(CompanyText.T("reallife_rota_block_remove"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.PlanChange, place, id, RotaPlanFieldBlockDelete, 0,
                    (plan.Blocks.Count - 1).ToString()), UiAction.Count, UiIcon.Close, true));
            }
            if (buttons.Count > 0)
            {
                blocks.Add(Buttons(buttons.ToArray()));
            }
            card.Blocks = blocks.ToArray();
            return card;
        }

        // The floor of crew by hour of the plan's blocks, from the first block's start to the last one's end; null where the plan has none.
        private static CompanyFloor FloorOf(RotaPlanFile plan, Words w)
        {
            if (plan == null || plan.Blocks.Count == 0)
            {
                return null;
            }
            int from = RotaCatalogue.Hours, to = 0;
            for (int i = 0; i < plan.Blocks.Count; i++)
            {
                from = Math.Min(from, Math.Max(0, plan.Blocks[i].From));
                to = Math.Max(to, Math.Min(RotaCatalogue.Hours, plan.Blocks[i].To));
            }
            if (to <= from)
            {
                return null;
            }
            var floor = new CompanyFloor { Title = w.T("reallife_rota_floor_chart", "The floor by hour"), Hours = new string[to - from], Crew = new int[to - from] };
            for (int h = from; h < to; h++)
            {
                floor.Hours[h - from] = h.ToString("00");
                for (int i = 0; i < plan.Blocks.Count; i++)
                {
                    if (h >= plan.Blocks[i].From && h < plan.Blocks[i].To)
                    {
                        floor.Crew[h - from] = Math.Max(floor.Crew[h - from], plan.Blocks[i].Crew);
                    }
                }
            }
            return floor;
        }
    }
}
