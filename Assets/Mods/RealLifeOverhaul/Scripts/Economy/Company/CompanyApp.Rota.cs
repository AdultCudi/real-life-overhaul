using System.Collections.Generic;
using Entities;
using Localizor;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Rota tab, four steps deep, each a table and what is picked in the white box:
    ///
    ///   the places     every place the player runs: its hours a week, its staff, and whether open hours have nobody
    ///                  on a shift. Pick one for
    ///   its plans      the ladder (RotaLadder): the week as it is, the best week the people there are can keep, and
    ///                  one plan for each further person worth hiring, each with its hours, its staff, what the week
    ///                  comes to, what it adds and what it needs. Under them the people, each with what they ask for
    ///                  and what they get, and the goods that run out in the advised plan, each with when and what
    ///                  would keep it on the shelves. The box: what a customer is worth and where that comes from, the
    ///                  customers against what the stations serve, whether the goods keep up, which plan is advised and
    ///                  why the ladder ends, the choice between staff first and lean, and "Fill the rota". Pick a plan for
    ///   one plan       its week day by day against the hours kept now, every change of hours with its reason, who
    ///                  works how much, and what is left unmet. The box: why, the week part by part, and "Use this
    ///                  plan" (or, for a plan that takes a hire, who to hire and where). Pick a day for
    ///   that day       hour by hour: who comes against what the stations serve, what the hour earns and costs
    ///
    /// A business is worked out when it is picked and when the director says something changed, never while the tab
    /// only shows the list (CompanyDirector.RotaSheetFor). Cost: nothing unless the tab is written.
    /// </summary>
    public sealed partial class CompanyApp
    {
        private static readonly string[] RotaDayKeys = { "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday" };   // the game's own
        private static readonly string[] RotaModeKeys = { "reallife_rota_mode_auto", "reallife_rota_mode_staff", "reallife_rota_mode_lean" };
        private const int RotaRowsMost = 8;     // unmet demands and gaps listed under a week

        partial void WriteRota(InfoBox box, ref string selected)
        {
            if (!_director.RotaOn)
            {
                box.Title(CompanyText.T(TabKeys[_tab]));
                box.Line(CompanyText.T("reallife_rota_off"));
                Say("off", CompanyText.T("reallife_co_part_off_row"));
                return;
            }
            string place;
            int code;
            bool onDay = Picked("rday:", out place, out code), onRung = !onDay && Picked("rung:", out place, out code);
            place = onDay || onRung ? place : Picked("rota:");
            RotaPlace at = _director.RotaPlaceAt(place);
            if (at == null)
            {
                selected = RotaList(box);
                return;
            }
            RotaSheet sheet = _director.RotaSheetFor(at);
            if (sheet == null)
            {
                box.Title(at.Name);
                box.Line(CompanyText.T(_director.RotaUnread(at.Place) ? "reallife_rota_cannot" : _director.RotaWorking(at.Place) ? "reallife_rota_working" : "reallife_rota_off"));
                BackButton(box);
                return;
            }
            int rung = onDay ? code / 10 : code, weekday = onDay ? code % 10 : -1;
            bool has = (onDay || onRung) && rung >= 0 && rung < sheet.Ladder.Count;
            if (has && onDay && weekday < RotaCatalogue.Days && sheet.Shop.HasCustomers)
            {
                RotaDay(box, sheet, rung, weekday);
            }
            else if (has)
            {
                RotaPlan(box, sheet, rung);
            }
            else
            {
                RotaPlans(box, sheet);
            }
        }

        // ---------------------------------------------------------------- the places

        private string RotaList(InfoBox box)
        {
            string selected = null;
            IReadOnlyList<RotaPlace> places = _director.RotaPlaces;
            for (int i = 0; i < places.Count; i++)
            {
                RotaPlace p = places[i];
                string hours = p.OpenHours > 0 ? CompanyText.T("reallife_rota_row_open", "hours", p.OpenHours.ToString()) : CompanyText.T("reallife_rota_closed");
                bool gaps = p.Type != RotaType.Warehouse && !p.Shut && p.Staff > 0 && p.GapHours > 0;
                string state = p.Type == RotaType.Warehouse ? CompanyText.T("reallife_rota_row_none")
                    : p.Staff == 0 ? CompanyText.T("reallife_rota_row_no_staff")
                    : gaps ? CompanyText.T("reallife_rota_row_gaps", "hours", p.GapHours.ToString())
                    : CompanyText.T("reallife_rota_row_covered");
                Pick("rota:" + p.Place, p.Name, RotaGame.TypeName(p.Reg) + ", " + hours, CompanyText.T("reallife_rota_row_staff", "count", p.Staff.ToString()), state, gaps ? 2 : 0, ref selected);
                Last.Key[2] = p.Staff;
            }
            if (places.Count == 0)
            {
                Say("none", CompanyText.T("reallife_co_row_no_business"));
            }
            box.Title(CompanyText.T("reallife_rota_tab"));
            box.Line(CompanyText.T("reallife_rota_intro"));
            box.Line(CompanyText.T("reallife_rota_intro_plans"));
            box.Line(CompanyText.T("reallife_rota_intro_fill"));
            box.Line(CompanyText.T("reallife_rota_intro_pick"));
            return selected;
        }

        // ---------------------------------------------------------------- one business's plans

        private void RotaPlans(InfoBox box, RotaSheet sheet)
        {
            RotaPlace at = sheet.Place;
            RotaShop shop = sheet.Shop;
            RotaOutlook o = sheet.Outlook;
            RotaLadderResult l = sheet.Ladder;
            string place = at.Place;
            string refusal = CompanyDirector.RotaRefusal(sheet);
            box.Title(at.Name);
            if (at.Type == RotaType.Warehouse || sheet.Stations.Count == 0)
            {
                _spec = CompanyTables.RotaWeek;
                Say("refused", RotaRefused(refusal));
                box.Line(RotaRefused(refusal));
                BackButton(box);
                return;
            }
            _spec = CompanyTables.RotaPlans;
            for (int i = 0; i < l.Count; i++)
            {
                RotaRung r = l.Rungs[i];
                bool advised = i == l.Recommended && l.Count > 1;
                string name = RotaPlanName(sheet, r);
                TableRow row = Row("rung:" + place + ":" + i);
                row.Pick = true;
                row.Set(0, advised ? CompanyText.T("reallife_rota_plan_advised", "plan", name) : name, advised ? TableRow.Green : TableRow.Plain);
                row.Set(1, shop.HasCustomers && r.Sum.Manned != r.Sum.Hours ? CompanyText.T("reallife_rota_plan_hours", "hours", r.Sum.Hours.ToString(), "manned", r.Sum.Manned.ToString())
                    : CompanyText.T("reallife_rota_plan_hours_all", "hours", r.Sum.Hours.ToString()), r.Sum.Manned != r.Sum.Hours && shop.HasCustomers ? TableRow.Yellow : TableRow.Plain);
                row.Set(2, r.Hires > 0 ? CompanyText.T("reallife_rota_plan_staff_hire", "count", r.Staff.ToString(), "hire", r.Hires.ToString()) : r.Staff.ToString());
                row.Set(3, shop.HasCustomers ? RotaMoney(r.Sum.Net) : RotaMoney(-r.Sum.Wages));
                row.Set(4, i == 0 ? string.Empty : r.Gain > 0 ? CompanyText.T("reallife_rota_plan_gain", "amount", RotaMoney(r.Gain)) : CompanyText.T("reallife_rota_plan_gain_none"), r.Gain > 0 ? TableRow.Green : TableRow.Plain);
                row.Set(5, RotaNeeds(sheet, r) + (r.Left.Count > 0 ? " " + CompanyText.T("reallife_rota_plan_leaves", "count", r.Left.Count.ToString()) : r.Out > 0 ? " " + CompanyText.T("reallife_rota_plan_outside", "count", r.Out.ToString()) : string.Empty),
                    r.Left.Count > 0 || r.Out > 0 ? TableRow.Red : TableRow.Plain);
            }
            RotaYourPeople(sheet, l.Rungs[l.Recommended]);
            RotaStations(sheet);
            RotaGoods(sheet, l.Rungs[l.Recommended], "goods", true);

            if (shop.HasCustomers)
            {
                box.Line(CompanyText.T(sheet.FromBooks ? "reallife_rota_box_worth_books" : "reallife_rota_box_worth_prices", "takings", CompanyText.Wage(shop.Takings), "margin", CompanyText.Wage(shop.Margin)));
                int peak = 0, cap = RotaServes(shop);
                for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
                {
                    peak = System.Math.Max(peak, System.Math.Max(shop.Customers[cell], shop.Wanting[cell]));
                }
                box.Line(CompanyText.Fill("reallife_rota_box_customers", "count", o.CustomersWeek.ToString(), "peak", peak.ToString(), "cap", cap.ToString()));
                RotaWeekSum now = l.Rungs[0].Sum;
                if (now.Hours > now.Manned)
                {
                    box.Line(CompanyText.T("reallife_rota_box_unmanned", "hours", (now.Hours - now.Manned).ToString(), "customers", now.LostUnmanned.ToString()));
                }
                RotaGoodsLine(box, sheet);
                string note = !sheet.RequirementsMet ? CompanyText.T("reallife_rota_note_requirements")
                    : at.Shut ? CompanyText.T("reallife_rota_note_shut")
                    : !o.Advised ? CompanyText.T("reallife_rota_note_no_worth")
                    : null;
                if (note != null)
                {
                    box.Line(note);
                }
                RotaAdvice(box, sheet);
                // The late opening permit, whenever it matters: the business opens late without one, or hours that would pay are kept out of the plans for want of one.
                string fee = CompanyText.Money(LicenceRules.Fee((int)LicenceKind.Late, _director.Index)), term = CompanyCatalogue.LicenceTerm[(int)LicenceKind.Late].ToString();
                if (sheet.Late)
                {
                    box.Line(CompanyText.T("reallife_rota_note_late", "fee", fee, "days", term));
                }
                if (o.Barred)
                {
                    box.Line(CompanyText.Fill("reallife_rota_note_permit", "fee", fee, "days", term, "hours", (o.HoursFree - o.HoursBest).ToString(), "amount", RotaMoney(o.ProfitFree - o.ProfitBest)));
                    if (l.Count > 0 && l.Rungs[l.Recommended].Sum.StockShort > 0)
                    {
                        box.Line(CompanyText.T("reallife_rota_note_permit_stock"));   // (that figure counts no stock: the goods already run out in the hours there are)
                    }
                }
            }
            else
            {
                box.Line(CompanyText.T(at.Type == RotaType.Headquarters ? "reallife_rota_hq" : at.Type == RotaType.Work ? "reallife_rota_work" : "reallife_rota_no_customers"));
            }
            if (shop.Cleaned)
            {
                box.Line(CompanyText.T("reallife_rota_cleaned"));
            }
            box.Line(refusal != null ? RotaRefused(refusal)
                : CompanyText.T("reallife_rota_box_rota", "covered", sheet.Now.CoveredHours.ToString(), "wanted", sheet.Now.WantedHours.ToString(), "unmet", sheet.Now.UnmetCount.ToString()));
            RotaChoice(box, sheet);
            var buttons = box.ButtonRow();
            if (refusal == null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_rota_fill", () => _director.FillRota(place, -1));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            if (_director.RotaHasUndo(place))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_rota_back", () => _director.RotaBack(place));
            }
        }

        // Which plan is advised and why the ladder ends where it does.
        private void RotaAdvice(InfoBox box, RotaSheet sheet)
        {
            RotaLadderResult l = sheet.Ladder;
            if (l.Count < 2)
            {
                return;
            }
            RotaRung advised = l.Rungs[l.Recommended];
            long more = advised.Sum.Net - l.Rungs[0].Sum.Net;
            box.Line(l.Recommended == 0 ? CompanyText.T("reallife_rota_advice_none")
                : advised.Hires > 0 ? CompanyText.T("reallife_rota_advice_hire", "plan", RotaPlanName(sheet, advised), "amount", RotaMoney(more))
                : CompanyText.T("reallife_rota_advice_best", "amount", RotaMoney(more)));
            if (l.Stop == RotaEnd.TooLittle)
            {
                box.Line(CompanyText.T("reallife_rota_stop_little", "skill", RotaGame.GroupSkill(sheet, l.StopGroup), "amount", RotaMoney(l.StopGain), "need", RotaMoney(l.StopNeed)));
            }
            else if (l.Stop == RotaEnd.Stock)
            {
                box.Line(CompanyText.T("reallife_rota_stop_stock", "skill", RotaGame.GroupSkill(sheet, l.StopGroup), "amount", RotaMoney(l.StopGain)));
            }
            else if (l.Stop == RotaEnd.Most)
            {
                box.Line(CompanyText.T("reallife_rota_stop_most", "count", RotaLadder.HiresMost.ToString()));
            }
            else if (l.Stop == RotaEnd.Covered)
            {
                box.Line(CompanyText.T("reallife_rota_stop_covered"));
            }
        }

        // Staff first or lean: the game's own selector in the box, with what each comes to for the hours kept now.
        private void RotaChoice(InfoBox box, RotaSheet sheet)
        {
            RotaLadderResult l = sheet.Ladder;
            string place = sheet.Place.Place;
            if (!l.ModeMatters || CompanyDirector.RotaRefusal(sheet) != null)
            {
                return;
            }
            int chosen = _director.RotaModeOf(place);
            string made = CompanyText.T(l.Mode == RotaMode.StaffFirst ? "reallife_rota_mode_staff" : "reallife_rota_mode_lean");
            box.Line(CompanyText.T("reallife_rota_mode_label"));
            if (!box.Selector(RotaModeKeys.Length, chosen, i => i == RotaMode.Auto ? CompanyText.T(RotaModeKeys[0], "mode", made) : CompanyText.T(RotaModeKeys[i]), i => _director.RotaChoose(place, i)))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, chosen == RotaMode.StaffFirst ? "reallife_rota_mode_to_lean" : "reallife_rota_mode_to_staff",
                    () => _director.RotaChoose(place, chosen == RotaMode.StaffFirst ? RotaMode.Lean : RotaMode.StaffFirst));
            }
            box.Line(CompanyText.T(l.Mode == RotaMode.StaffFirst ? "reallife_rota_mode_explain_staff" : "reallife_rota_mode_explain_lean"));
            if (l.ModeAuto)
            {
                box.Line(CompanyText.T("reallife_rota_mode_figures", "staff", RotaMoney(l.StaffNet), "lean", RotaMoney(l.LeanNet)));
            }
        }

        // Everybody who works there: what they can do, what they are paid, what they ask for, and what the advised plan gives them.
        private void RotaYourPeople(RotaSheet sheet, RotaRung plan)
        {
            if (sheet.Workers.Count == 0)
            {
                return;
            }
            RotaRung now = sheet.Ladder.Rungs[0];
            Say("yours", CompanyText.T("reallife_rota_head_your_people"));
            for (int w = 0; w < sheet.Workers.Count && w < sheet.Problem.Workers; w++)
            {
                EmployeeInstance who = sheet.Workers[w];
                TableRow row = Row("person" + w);
                bool unmet = RotaUnmetOf(sheet, plan.Plan, w) != null;
                row.Set(0, RotaGame.WorkerName(who));
                row.Set(1, RotaGame.Skills(who));
                row.Set(2, CompanyText.T("reallife_rota_person_wage", "wage", CompanyText.Wage(sheet.Problem.Worker[w].Wage)));
                row.Set(3, w < sheet.Fixed.Length && sheet.Fixed[w] ? CompanyText.T("reallife_rota_person_fixed") : RotaAsks(sheet.Problem.Worker[w]));
                CellTone tone;
                string range = RotaRange(sheet, plan, w, out tone);
                row.Set(4, range ?? CompanyText.T("reallife_rota_person_hours", "now", RotaHoursOf(now, w).ToString(), "plan", RotaHoursOf(plan, w).ToString()), tone);
                row.Set(5, RotaDemandState(sheet, plan, w), unmet ? TableRow.Yellow : TableRow.Plain);
            }
        }

        // A station that would pay for itself, and stations out of order.
        private void RotaStations(RotaSheet sheet)
        {
            RotaOutlook o = sheet.Outlook;
            RotaLadderResult l = sheet.Ladder;
            bool runsOut = l.Count > 0 && l.Rungs[l.Recommended].Sum.StockShort > 0;   // (more customers would find the goods gone)
            for (int i = 0; i < o.Missing.Count; i++)
            {
                RotaMissing missing = o.Missing[i];
                TableRow row = Row("more" + i);
                row.Set(0, CompanyText.T("reallife_rota_missing", "station", RotaGroup(sheet, missing.Group)));
                row.Set(1, RotaDayName(missing.Day));
                row.Set(5, CompanyText.T(runsOut ? "reallife_rota_missing_stock" : "reallife_rota_missing_what", "customers", missing.Customers.ToString()));
            }
            for (int i = 0; i < sheet.Broken.Count; i++)
            {
                Say("broken" + i, CompanyText.T("reallife_rota_broken", "station", sheet.Broken[i]));
            }
        }

        // ---------------------------------------------------------------- one plan

        private void RotaPlan(InfoBox box, RotaSheet sheet, int index)
        {
            RotaShop shop = sheet.Shop;
            RotaLadderResult l = sheet.Ladder;
            RotaRung r = l.Rungs[index], now = l.Rungs[0];
            string place = sheet.Place.Place, none = null;
            _spec = CompanyTables.RotaWeek;
            for (int d = 0; d < RotaCatalogue.Days; d++)
            {
                int come = 0, served = 0;
                bool differs = false;
                for (int h = 0; h < RotaCatalogue.Hours; h++)
                {
                    int cell = d * RotaCatalogue.Hours + h;
                    come += r.Come[cell];
                    served += r.Served[cell];
                    differs |= shop.Open[cell] != r.Open[cell];
                }
                string coming = !shop.HasCustomers ? string.Empty : come == 0 ? CompanyText.T("reallife_rota_none_come") : CompanyText.T("reallife_rota_day_served", "served", served.ToString(), "come", come.ToString());
                if (shop.HasCustomers)
                {
                    Pick("rday:" + place + ":" + (index * 10 + d), RotaDayName(d), RotaHours(shop.Open, d), RotaHours(r.Open, d), coming, differs ? 1 : served < come ? 2 : 0, ref none);
                }
                else
                {
                    TableRow row = Row("day" + d);
                    row.A = RotaDayName(d);
                    row.B = RotaHours(shop.Open, d);
                    row.C = RotaHours(r.Open, d);
                }
            }
            if (r.Changes.Count > 0)
            {
                Say("changes", CompanyText.T("reallife_rota_head_changes"));
            }
            for (int i = 0; i < r.Changes.Count; i++)
            {
                RotaChange c = r.Changes[i];
                TableRow row = Row("change" + i);
                row.A = RotaDayName(c.Day);
                row.B = CompanyText.T(c.Why == RotaWhy.Opened || c.Why == RotaWhy.Fits ? "reallife_rota_change_open" : "reallife_rota_change_close", "from", RotaHour(c.From), "to", RotaHour(c.To))
                        + ". " + CompanyText.T("reallife_rota_change_money", "was", RotaMoney(c.Was), "will", RotaMoney(c.Will));
                row.C = string.Empty;
                row.D = c.Why == RotaWhy.Opened ? CompanyText.T("reallife_rota_change_why_open")
                    : c.Why == RotaWhy.NoCrew ? CompanyText.T("reallife_rota_change_why_crew", "station", RotaGroup(sheet, c.Group), "skill", RotaGame.GroupSkill(sheet, c.Group))
                    : c.Why == RotaWhy.NoPay ? CompanyText.T("reallife_rota_change_why_pay")
                    : c.Why == RotaWhy.Barred ? CompanyText.T("reallife_rota_change_why_barred")
                    : c.Why == RotaWhy.Fits ? CompanyText.T("reallife_rota_change_why_fits", "name", c.Group >= 0 && c.Group < sheet.Workers.Count ? RotaGame.WorkerName(sheet.Workers[c.Group]) : "?")
                    : CompanyText.T("reallife_rota_change_why_day");
                row.Tone = c.Why == RotaWhy.Opened || c.Why == RotaWhy.Fits ? TableRow.Green : TableRow.Yellow;
            }
            Say("who", CompanyText.T("reallife_rota_head_plan_people"));
            for (int w = 0; w < r.Workers; w++)
            {
                bool hired = w >= sheet.Workers.Count;
                int group = hired && w - sheet.Workers.Count < r.Hires ? r.Hire[w - sheet.Workers.Count] : -1;
                TableRow row = Row("who" + w);
                row.A = hired ? CompanyText.T("reallife_rota_plan_hired", "skill", RotaGame.GroupSkill(sheet, group)) : RotaGame.WorkerName(sheet.Workers[w]);
                row.B = hired ? CompanyText.T("reallife_rota_person_wage", "wage", CompanyText.Wage(RotaLadder.Going(shop, group))) : RotaAsks(sheet.Problem.Worker[w]);
                CellTone tone = TableRow.Plain;
                string range = hired ? null : RotaRange(sheet, r, w, out tone);
                row.Set(2, CompanyText.T("reallife_rota_plan_person", "hours", RotaHoursOf(r, w).ToString(), "now", RotaHoursOf(now, w).ToString()));
                row.Set(3, hired ? string.Empty : range ?? RotaDemandState(sheet, r, w), range != null ? tone : !hired && RotaUnmetOf(sheet, r.Plan, w) != null ? TableRow.Yellow : hired ? TableRow.Green : TableRow.Plain);
            }
            RotaWaysOut(sheet, r);
            RotaGaps(sheet, r.Plan);
            RotaGoods(sheet, r, "pgoods", false);

            box.Title(CompanyText.T("reallife_rota_rung_title", "plan", RotaPlanName(sheet, r), "business", sheet.Place.Name));
            box.Line(RotaWhyPlan(sheet, r, index));
            if (shop.HasCustomers)
            {
                RotaWeekSum s = r.Sum;
                box.Line(CompanyText.Fill("reallife_rota_sum_customers", "served", s.Served.ToString(), "come", s.Come.ToString(), "nobody", s.LostUnmanned.ToString(), "full", s.LostFull.ToString()));
                box.Line(FinanceText.Pair("reallife_rota_sum_sales", RotaMoney(s.Sales)));
                box.Line(FinanceText.Pair("reallife_rota_sum_wages", RotaMoney(-s.Wages)));
                RotaSumLine(box, "reallife_rota_sum_fees", s.Fees);
                RotaSumLine(box, "reallife_rota_sum_theft", s.Theft);
                RotaSumLine(box, "reallife_rota_sum_night", s.Night);
                RotaSumLine(box, "reallife_rota_sum_demands", s.Demands);
                box.Line(FinanceText.Pair("reallife_rota_sum_net", RotaMoney(s.Net)));
                if (s.StockShort > 0)
                {
                    box.Line(CompanyText.T("reallife_rota_sum_stock", "count", s.StockShort.ToString(), "amount", RotaMoney(s.StockLost)));
                }
                if (s.Clean > 0)
                {
                    box.Line(CompanyText.T("reallife_rota_sum_clean", "amount", RotaMoney(s.Clean), "hours", s.CleanHours.ToString()));
                }
                if (index > 0)
                {
                    box.Line(r.Gain > 0 ? CompanyText.T("reallife_rota_sum_gain", "amount", RotaMoney(r.Gain), "plan", RotaPlanName(sheet, l.Rungs[index - 1])) : CompanyText.T("reallife_rota_plan_gain_none"));
                }
            }
            else
            {
                box.Line(FinanceText.Pair("reallife_rota_sum_wages", RotaMoney(-r.Sum.Wages)));
                box.Line(CompanyText.T("reallife_rota_box_rota", "covered", r.Plan.CoveredHours.ToString(), "wanted", r.Plan.WantedHours.ToString(), "unmet", r.Plan.UnmetCount.ToString()));
            }
            var buttons = box.ButtonRow();
            if (r.Hires > 0)
            {
                for (int g = 0; g < shop.Groups; g++)
                {
                    int count = 0;
                    for (int i = 0; i < r.Hires; i++)
                    {
                        count += r.Hire[i] == g ? 1 : 0;
                    }
                    if (count > 0)
                    {
                        box.Line(CompanyText.T("reallife_rota_hire_who", "count", count.ToString(), "skill", RotaGame.GroupSkill(sheet, g), "wage", CompanyText.Wage(RotaLadder.Going(shop, g))));
                    }
                }
                box.Line(CompanyText.T("reallife_rota_hire_where"));
                box.Line(r.OneOff > 0 ? CompanyText.T("reallife_rota_hire_cost_once", "amount", RotaMoney(r.OneOff), "weeks", r.WeeksBack.ToString()) : CompanyText.T("reallife_rota_hire_cost"));
                UnityEngine.UI.Button wait = InfoBox.Button(buttons, InfoBox.Positive, "reallife_rota_use_after", () => { });
                if (wait != null)
                {
                    wait.interactable = false;   // (hours nobody is there to keep are never written)
                }
            }
            else if (CompanyDirector.RotaRungOf(sheet, index) != null && CompanyDirector.RotaRefusal(sheet) == null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_rota_use_plan", () => _director.FillRota(place, index));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_rota_plan_back", () =>
            {
                _pick = "rota:" + place;
                _table.ClearSelection();
                Refresh();
            });
        }

        private static void RotaSumLine(InfoBox box, string key, long amount)
        {
            if (amount != 0)
            {
                box.Line(FinanceText.Pair(key, RotaMoney(-amount)));
            }
        }

        // Why a plan is what it is, in a sentence.
        private string RotaWhyPlan(RotaSheet sheet, RotaRung r, int index)
        {
            if (r.Kind == RotaRungKind.AsItIs)
            {
                return CompanyText.T("reallife_rota_why_now");
            }
            if (r.Kind == RotaRungKind.Best)
            {
                return CompanyText.T(r.Source == 0 ? "reallife_rota_why_best_same" : r.Source == 1 ? "reallife_rota_why_best_rota" : r.Source == 2 ? "reallife_rota_why_best_cut" : r.Source == 4 ? "reallife_rota_why_best_fits" : "reallife_rota_why_best_hours");
            }
            RotaRung before = sheet.Ladder.Rungs[index - 1];
            return CompanyText.Fill("reallife_rota_why_hire", "who", RotaGame.GroupSkill(sheet, r.Hire[r.Hires - 1]), "hours", (r.Sum.Manned - before.Sum.Manned).ToString(),
                "customers", (r.Sum.Served - before.Sum.Served).ToString());
        }

        // For somebody whose hours a plan cannot meet: where else they could go, in the order the mod can help with them.
        private void RotaWaysOut(RotaSheet sheet, RotaRung r)
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
                RotaPlace busier = _director.RotaBusier(sheet.Place, who);
                bool cleans = false;
                for (int s = 0; s < sheet.Stations.Count; s++)
                {
                    cleans |= sheet.Shop.GroupKind[sheet.Shop.Group[s]] == RotaGroupKind.Cleaning && sheet.Problem.Can[unmet.Worker * sheet.Stations.Count + s] != 0;
                }
                string words = busier != null ? CompanyText.T("reallife_rota_way_busier", "name", RotaGame.WorkerName(who), "business", busier.Name, "hours", busier.GapHours.ToString())
                    : CompanyText.T("reallife_rota_way_none", "name", RotaGame.WorkerName(who));
                words += cleans && CompanyDirector.RotaCleaningService ? " " + CompanyText.T("reallife_rota_way_service") : string.Empty;
                Say("way" + rows++, words + " " + CompanyText.T("reallife_rota_way_replace"));
            }
        }

        // What a rota leaves uncovered: which station, when, and why nobody is at it.
        private void RotaGaps(RotaSheet sheet, RotaPlan plan)
        {
            int rows = 0;
            for (int i = 0; i < plan.Gaps.Count && rows < RotaRowsMost; i++, rows++)
            {
                RotaGap gap = plan.Gaps[i];
                TableRow row = Row("gap" + i);
                row.A = gap.Station < sheet.Stations.Count ? RotaGame.Name(sheet.Stations[gap.Station]) : string.Empty;
                row.B = CompanyText.T(gap.Reason == RotaReason.NoSkill ? "reallife_rota_gap_no_skill" : "reallife_rota_gap_stretched");
                row.C = CompanyText.T("reallife_rota_from_to", "from", RotaHour(gap.From), "to", RotaHour(gap.To));
                row.D = RotaDayName(gap.Day);
                row.Tone = TableRow.Red;
            }
            if (plan.Gaps.Count > rows)
            {
                Say("left", CompanyText.T("reallife_rota_more", "count", (plan.Gaps.Count - rows).ToString()));
            }
        }

        // ---------------------------------------------------------------- one day of a plan

        private void RotaDay(InfoBox box, RotaSheet sheet, int index, int d)
        {
            RotaShop shop = sheet.Shop;
            RotaRung r = sheet.Ladder.Rungs[index];
            string place = sheet.Place.Place;
            int stations = shop.Stations, cap = RotaServes(shop), come = 0, served = 0;
            long earns = 0, costs = 0;
            _spec = CompanyTables.RotaDay;
            for (int h = 0; h < RotaCatalogue.Hours; h++)
            {
                int cell = d * RotaCatalogue.Hours + h, wanting = System.Math.Max(shop.Customers[cell], shop.Wanting[cell]);
                bool now = shop.Open[cell], open = r.Open[cell];
                if (wanting <= 0 && !now && !open)
                {
                    continue;
                }
                int have = 0;
                for (int s = 0; s < stations; s++)
                {
                    have += shop.GroupKind[shop.Group[s]] != RotaGroupKind.Cleaning && (r.Plan.At[cell * stations + s] >= 0 || shop.Taken[cell * stations + s]) ? 1 : 0;
                }
                come += r.Come[cell];
                served += r.Served[cell];
                earns += r.Earn[cell];
                costs += r.Cost[cell];
                TableRow row = Row("hour" + h);
                row.A = RotaHour(h);
                row.B = !open ? CompanyText.T("reallife_rota_hour_shut", "come", wanting.ToString())
                    : r.Served[cell] == 0 && r.Come[cell] > 0 ? CompanyText.T("reallife_rota_hour_nobody", "come", r.Come[cell].ToString())
                    : CompanyText.T("reallife_rota_hour_come", "come", r.Come[cell].ToString(), "cap", cap.ToString(), "served", r.Served[cell].ToString());
                row.C = open || r.Cost[cell] != 0 ? CompanyText.T("reallife_rota_hour_money", "earn", RotaMoney(r.Earn[cell]), "cost", RotaMoney(r.Cost[cell])) : string.Empty;
                string who = string.Empty;
                for (int s = 0; s < stations; s++)
                {
                    int w = r.Plan.At[cell * stations + s];
                    if (w >= 0)
                    {
                        who += (who.Length > 0 ? "; " : string.Empty) + RotaGame.Name(sheet.Stations[s]) + ": " + (w < sheet.Workers.Count ? RotaGame.WorkerName(sheet.Workers[w]) : CompanyText.T("reallife_rota_hour_new"));
                    }
                }
                row.D = !open ? CompanyText.T("reallife_rota_closed") : who.Length > 0 ? who : CompanyText.T("reallife_rota_hour_nobody_at_work");
                row.Tone = open != now ? (open ? TableRow.Green : TableRow.Yellow) : open && r.Served[cell] < r.Come[cell] ? TableRow.Red : TableRow.Plain;
            }
            box.Title(CompanyText.T("reallife_rota_day_title", "day", RotaDayName(d), "business", sheet.Place.Name));
            box.Line(RotaPlanName(sheet, r));
            box.Line(FinanceText.Pair("reallife_rota_day_now", RotaHours(shop.Open, d)));
            box.Line(FinanceText.Pair("reallife_rota_day_plan", RotaHours(r.Open, d)));
            box.Line(CompanyText.Fill("reallife_rota_day_sum", "served", served.ToString(), "come", come.ToString(), "earn", RotaMoney(earns), "cost", RotaMoney(costs)));
            if (shop.DayFee[d] > 0)
            {
                box.Line(FinanceText.Pair("reallife_rota_day_fee", RotaMoney(shop.DayFee[d])));
            }
            box.Line(CompanyText.T("reallife_rota_day_explain"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_rota_day_back", () =>
            {
                _pick = "rung:" + place + ":" + index;
                _table.ClearSelection();
                Refresh();
            });
        }

        // ---------------------------------------------------------------- words

        private static string RotaPlanName(RotaSheet sheet, RotaRung r)
        {
            if (r.Kind != RotaRungKind.Hire)
            {
                return CompanyText.T(r.Kind == RotaRungKind.AsItIs ? "reallife_rota_plan_now" : "reallife_rota_plan_best");
            }
            return CompanyText.T("reallife_rota_plan_hire", "count", r.Hires.ToString(), "who", RotaHireWho(sheet, r));
        }

        // The people a plan takes on, by the skill to look for: "2 Customer Service, 1 Cleaning".
        private static string RotaHireWho(RotaSheet sheet, RotaRung r)
        {
            string who = string.Empty;
            for (int g = 0; g < sheet.Shop.Groups; g++)
            {
                int count = 0;
                for (int i = 0; i < r.Hires; i++)
                {
                    count += r.Hire[i] == g ? 1 : 0;
                }
                if (count > 0)
                {
                    who += (who.Length > 0 ? ", " : string.Empty) + count + " " + RotaGame.GroupSkill(sheet, g);
                }
            }
            return who;
        }

        private static string RotaNeeds(RotaSheet sheet, RotaRung r)
        {
            if (r.Kind == RotaRungKind.AsItIs)
            {
                return CompanyText.T("reallife_rota_needs_nothing");
            }
            if (r.Kind == RotaRungKind.Best)
            {
                return CompanyText.T(r.Source == 0 ? "reallife_rota_needs_same" : r.Source == 1 ? "reallife_rota_needs_rota" : "reallife_rota_needs_hours");
            }
            int group = r.Hire[r.Hires - 1];
            return CompanyText.T("reallife_rota_needs_hire", "who", RotaGame.GroupSkill(sheet, group), "wage", CompanyText.Wage(RotaLadder.Going(sheet.Shop, group)));
        }

        // The customers an hour the stations serve when all are manned: the smallest group's sum (the game's own limit).
        private static int RotaServes(RotaShop shop)
        {
            int least = -1;
            for (int g = 0; g < shop.Groups; g++)
            {
                int sum = 0, n = 0;
                for (int s = 0; s < shop.Stations; s++)
                {
                    if (shop.Group[s] == g)
                    {
                        sum += shop.Cap[s];
                        n++;
                    }
                }
                if (shop.GroupKind[g] == RotaGroupKind.Service && n > 0 && (least < 0 || sum < least))
                {
                    least = sum;
                }
            }
            return least < 0 ? 0 : least;
        }

        private static int RotaHoursOf(RotaRung r, int worker)
        {
            return worker < r.Workers && worker < r.HoursOf.Length ? r.HoursOf[worker] : 0;
        }

        // What somebody asks for about their hours, in words: the hours a week, the days, the days off, the hours not worked.
        private static string RotaAsks(RotaWorker worker)
        {
            string words = string.Empty;
            for (int i = 0; i < worker.Demands.Length; i++)
            {
                RotaDemand d = worker.Demands[i];
                string one = null;
                if (d.Kind == RotaDemandKind.Hours)
                {
                    one = d.A > 0 && d.B > 0 ? CompanyText.T("reallife_rota_asks_between", "least", d.A.ToString(), "most", d.B.ToString())
                        : d.A > 0 ? CompanyText.T("reallife_rota_asks_least", "least", d.A.ToString())
                        : d.B > 0 ? CompanyText.T("reallife_rota_asks_most", "most", d.B.ToString()) : null;
                }
                else if (d.Kind == RotaDemandKind.Days)
                {
                    one = CompanyText.T("reallife_rota_asks_days", "count", d.A.ToString());
                }
                else if (d.Kind == RotaDemandKind.FreeDays)
                {
                    string days = string.Empty;
                    for (int k = 0; k < RotaCatalogue.Days; k++)
                    {
                        days += ((d.Mask >> k) & 1UL) != 0 ? (days.Length > 0 ? ", " : string.Empty) + RotaDayName(k) : string.Empty;
                    }
                    one = days.Length > 0 ? CompanyText.T("reallife_rota_asks_free", "days", days) : null;
                }
                else if (d.Kind == RotaDemandKind.NoHours)
                {
                    string hours = string.Empty;
                    for (int h = 0; h < RotaCatalogue.Hours;)
                    {
                        if (((d.Mask >> h) & 1UL) == 0)
                        {
                            h++;
                            continue;
                        }
                        int from = h;
                        while (h < RotaCatalogue.Hours && ((d.Mask >> h) & 1UL) != 0)
                        {
                            h++;
                        }
                        hours += (hours.Length > 0 ? ", " : string.Empty) + CompanyText.T("reallife_rota_from_to", "from", RotaHour(from), "to", RotaHour(h));
                    }
                    one = hours.Length > 0 ? CompanyText.T("reallife_rota_asks_not", "hours", hours) : null;
                }
                words += one != null ? (words.Length > 0 ? "; " : string.Empty) + one : string.Empty;
            }
            return words.Length > 0 ? words : CompanyText.T("reallife_rota_asks_any");
        }

        // The hours a week somebody asks for against what a plan gives: inside the range in green; outside it, or left out because
        // the least cannot be had, in red with the clash in a sentence and what would remove it. Null for somebody who asks for none.
        private static string RotaRange(RotaSheet sheet, RotaRung r, int worker, out CellTone tone)
        {
            int least, most, hours = RotaHoursOf(r, worker);
            tone = TableRow.Plain;
            RotaFit.Range(sheet.Problem.Worker[worker], out least, out most);
            if ((least <= 0 && most == int.MaxValue) || (worker < sheet.Fixed.Length && sheet.Fixed[worker]))
            {
                return null;
            }
            string asks = most == int.MaxValue ? CompanyText.T("reallife_rota_asks_least", "least", least.ToString()) : CompanyText.T("reallife_rota_asks_between", "least", least.ToString(), "most", most.ToString());
            for (int i = 0; i < r.Left.Count; i++)
            {
                RotaLeft left = r.Left[i];
                if (left.Worker != worker)
                {
                    continue;
                }
                tone = TableRow.Red;
                return left.Why == RotaClash.Service ? CompanyText.T("reallife_rota_range_service", "asks", asks)
                    : left.Why == RotaClash.Shared ? CompanyText.Fill("reallife_rota_range_shared", "asks", asks, "asked", left.Asked.ToString(), "held", left.Held.ToString())
                    : CompanyText.Fill("reallife_rota_range_hours", "asks", asks, "room", left.Room.ToString(), "short", (left.Least - left.Room).ToString());
            }
            if (hours > 0 && hours >= least && hours <= most)
            {
                tone = TableRow.Green;
                return CompanyText.T("reallife_rota_range_in", "asks", asks, "hours", hours.ToString());
            }
            if (hours == 0 && least <= 0)
            {
                return CompanyText.T("reallife_rota_range_in", "asks", asks, "hours", hours.ToString());
            }
            tone = TableRow.Red;
            return CompanyText.Fill("reallife_rota_range_out", "asks", asks, "hours", hours.ToString(), "off", (hours < least ? least - hours : hours - most).ToString());
        }

        // The first demand of somebody that a rota leaves unmet, in the game's own words; null if it meets them all.
        private static string RotaUnmetOf(RotaSheet sheet, RotaPlan plan, int worker)
        {
            for (int i = 0; i < plan.Unmet.Count; i++)
            {
                RotaUnmet unmet = plan.Unmet[i];
                if (unmet.Worker != worker || worker >= sheet.Workers.Count)
                {
                    continue;
                }
                RotaDemand[] demands = sheet.Problem.Worker[worker].Demands;
                string asks = unmet.Demand < demands.Length ? RotaGame.DemandName(sheet.Workers[worker], demands[unmet.Demand]) : string.Empty;
                string why = CompanyText.T(unmet.Reason == RotaReason.Short ? "reallife_rota_why_short" : unmet.Reason == RotaReason.NoStation ? "reallife_rota_why_no_station" : "reallife_rota_why_needed");
                return (asks.Length > 0 ? asks + ": " : string.Empty) + why;
            }
            return null;
        }

        // What a plan does to somebody's demands: all met, or which is not, why, and what the planner counts for it.
        private static string RotaDemandState(RotaSheet sheet, RotaRung r, int worker)
        {
            if (worker < sheet.Fixed.Length && sheet.Fixed[worker])
            {
                return string.Empty;
            }
            string unmet = RotaUnmetOf(sheet, r.Plan, worker);
            if (unmet == null)
            {
                return CompanyText.T(sheet.Problem.Worker[worker].Demands.Length > 0 ? "reallife_rota_demands_met" : "reallife_rota_demands_none");
            }
            long cost = worker < r.DemandCost.Length ? r.DemandCost[worker] : 0;
            int quit = worker < r.QuitHours.Length ? r.QuitHours[worker] : 0;
            return unmet + ". " + (quit > 0 ? CompanyText.T("reallife_rota_person_quit", "days", System.Math.Max(1, quit / RotaCatalogue.Hours).ToString(), "amount", RotaMoney(cost))
                : CompanyText.T("reallife_rota_person_settles", "amount", RotaMoney(cost)));
        }

        // ---------------------------------------------------------------- the goods

        // Whether the goods keep up with the advised plan, and what the wholesalers are short of this week.
        private static void RotaGoodsLine(InfoBox box, RotaSheet sheet)
        {
            RotaStock stock = sheet.Shop.Stock;
            RotaLadderResult l = sheet.Ladder;
            if (stock.Products == 0 || l.Count == 0)
            {
                return;
            }
            RotaWeekSum advised = l.Rungs[l.Recommended].Sum;
            box.Line(!stock.Any ? CompanyText.T("reallife_rota_box_stock_hand")
                : advised.StockShort > 0 ? CompanyText.T("reallife_rota_box_stock_short", "count", advised.StockShort.ToString(), "amount", RotaMoney(advised.StockLost))
                : CompanyText.T("reallife_rota_box_stock_ok"));
            for (int p = 0; p < stock.Products && p < sheet.Products.Count; p++)
            {
                if (stock.Backorder[p] || stock.Shortage[p])
                {
                    box.Line(CompanyText.T(stock.Backorder[p] ? "reallife_rota_stock_backorder" : "reallife_rota_stock_shortage", "product", RotaGame.Text(sheet.Products[p])));
                }
            }
        }

        // The goods that run out in a plan, each with when, what its customers would have spent, and what would keep it on the
        // shelves; and the goods no delivery brings that would not last the week at the plan's sales. In the plans' table
        // (plans) or in a week's.
        private void RotaGoods(RotaSheet sheet, RotaRung r, string id, bool plans)
        {
            RotaStock stock = sheet.Shop.Stock;
            int rows = 0;
            for (int p = 0; p < stock.Products && p < sheet.Products.Count && rows < RotaRowsMost; p++)
            {
                string when, lift;
                long lost = 0;
                if (stock.Capped(p))
                {
                    if (r.Stock.Products == 0 || r.Stock.Out[p] < 0)
                    {
                        continue;
                    }
                    int cell = r.Stock.Out[p];
                    when = CompanyText.T("reallife_rota_stock_out", "day", RotaDayName(cell / RotaCatalogue.Hours), "hour", RotaHour(cell % RotaCatalogue.Hours));
                    lift = RotaLiftText(sheet, r, p);
                    lost = r.Stock.Lost[p];
                }
                else
                {
                    int want = r.Stock.Products > 0 ? r.Stock.WantUnits(p) : (int)System.Math.Min(int.MaxValue, (long)r.Sum.Served * stock.PerCustomer[p] / 1000);
                    int days = want > 0 ? (int)System.Math.Min(7L, (long)stock.OnSite[p] * 7 / want) : 7;
                    if (days >= 7)
                    {
                        continue;
                    }
                    when = days == 0 ? CompanyText.T("reallife_rota_stock_none_now") : CompanyText.T("reallife_rota_stock_lasts", "days", days.ToString());
                    lift = CompanyText.T("reallife_rota_stock_hand_row");
                }
                if (rows++ == 0)
                {
                    Say(id + "head", CompanyText.T("reallife_rota_head_goods"));
                }
                string name = RotaGame.Text(sheet.Products[p]), money = lost > 0 ? RotaMoney(-lost) : string.Empty;
                TableRow row = Row(id + p);
                if (plans)
                {
                    row.Set(0, name);
                    row.Set(1, when, TableRow.Yellow);
                    row.Set(3, money, TableRow.Red);
                    row.Set(5, lift);
                }
                else
                {
                    row.A = name;
                    row.B = lift;
                    row.C = when;
                    row.D = money;
                    row.Tone = TableRow.Yellow;
                }
            }
        }

        // What would let a product that runs out sell to every customer, with the figures that say so.
        private static string RotaLiftText(RotaSheet sheet, RotaRung r, int p)
        {
            RotaStock s = sheet.Shop.Stock;
            RotaStockWeek w = r.Stock;
            string want = w.WantUnits(p).ToString(), delivered = s.Weekly[p].ToString();
            switch (RotaLift.Of(s, w, p))
            {
                case RotaLift.Room:
                    return CompanyText.T("reallife_rota_lift_room", "refused", w.Refused[p].ToString());
                case RotaLift.Target:
                    string target = CompanyText.T("reallife_rota_lift_target", "target", s.Target[p].ToString(), "day", w.DayUnits(p).ToString());
                    return s.Warehouse[p] < w.WantUnits(p) ? target + " " + CompanyText.T("reallife_rota_lift_warehouse", "warehouse", s.Warehouse[p].ToString()) : target;
                case RotaLift.Second:
                    return CompanyText.T("reallife_rota_lift_second", "delivered", delivered, "want", want);
                default:
                    return CompanyText.T("reallife_rota_lift_order", "delivered", delivered, "want", want, "percentage", sheet.UrgentPercent.ToString());
            }
        }

        private static string RotaDayName(int d)
        {
            return RotaDayKeys[d < 0 ? 0 : d >= RotaDayKeys.Length ? RotaDayKeys.Length - 1 : d].GetLocalization();
        }

        // An hour as the game writes the time of day, by its own twelve or twenty-four hour setting.
        private static string RotaHour(int hour)
        {
            return (hour % RotaCatalogue.Hours).GetFormattedTime(0f, false);
        }

        // The opening hours of one day in words: "09:00 to 18:00", two blocks joined by a comma, or "closed".
        private static string RotaHours(bool[] open, int d)
        {
            string words = null;
            for (int h = 0; h < RotaCatalogue.Hours;)
            {
                if (!open[d * RotaCatalogue.Hours + h])
                {
                    h++;
                    continue;
                }
                int from = h;
                while (h < RotaCatalogue.Hours && open[d * RotaCatalogue.Hours + h])
                {
                    h++;
                }
                words = (words != null ? words + ", " : string.Empty) + CompanyText.T("reallife_rota_from_to", "from", RotaHour(from), "to", RotaHour(h));
            }
            return words ?? CompanyText.T("reallife_rota_closed");
        }

        // Why a rota cannot be filled, with the most the planner keeps where the sentence names it.
        private static string RotaRefused(string key)
        {
            return CompanyText.T(key, "stations", RotaCatalogue.StationsMost.ToString(), "staff", RotaCatalogue.WorkersMost.ToString());
        }

        // Hundredths as the game writes money.
        private static string RotaMoney(long hundredths)
        {
            return CompanyText.Money(hundredths / 100f);
        }

        private static string RotaGroup(RotaSheet sheet, int group)
        {
            return group >= 0 && group < sheet.GroupNames.Count ? sheet.GroupNames[group] : string.Empty;
        }
    }
}
