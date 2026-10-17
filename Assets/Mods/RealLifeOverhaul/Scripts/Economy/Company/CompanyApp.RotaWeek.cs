using Entities;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Rota tab's pages of one plan, day by day against the hours kept now, and of one day of it, hour by hour.</summary>
    public sealed partial class CompanyApp
    {
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
                string coming = !shop.HasCustomers ? string.Empty
                    : come == 0 ? CompanyText.T("reallife_rota_none_come")
                    : CompanyText.T("reallife_rota_day_served", "served", served.ToString(), "come", come.ToString());
                if (shop.HasCustomers)
                {
                    Pick("rday:" + place + ":" + (index * 10 + d), RotaDayName(d), RotaHours(shop.Open, d), RotaHours(r.Open, d), coming,
                        differs ? 1 : served < come ? 2 : 0, ref none);
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
                bool opens = c.Why == RotaWhy.Opened || c.Why == RotaWhy.Fits;
                row.B = CompanyText.T(opens ? "reallife_rota_change_open" : "reallife_rota_change_close", "from", RotaHour(c.From), "to", RotaHour(c.To))
                        + ". " + CompanyText.T("reallife_rota_change_money", "was", RotaMoney(c.Was), "will", RotaMoney(c.Will));
                row.C = string.Empty;
                row.D = c.Why == RotaWhy.Opened ? CompanyText.T("reallife_rota_change_why_open")
                    : c.Why == RotaWhy.NoCrew
                        ? CompanyText.T("reallife_rota_change_why_crew", "station", RotaGroup(sheet, c.Group), "skill", RotaGame.GroupSkill(sheet, c.Group))
                    : c.Why == RotaWhy.NoPay ? CompanyText.T("reallife_rota_change_why_pay")
                    : c.Why == RotaWhy.Barred ? CompanyText.T("reallife_rota_change_why_barred")
                    : c.Why == RotaWhy.Fits ? CompanyText.T("reallife_rota_change_why_fits", "name",
                        c.Group >= 0 && c.Group < sheet.Workers.Count ? RotaGame.WorkerName(sheet.Workers[c.Group]) : "?")   // Group holds the worker here
                    : CompanyText.T("reallife_rota_change_why_day");
                row.Tone = opens ? TableRow.Green : TableRow.Yellow;
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
                row.Set(3, hired ? string.Empty : range ?? RotaDemandState(sheet, r, w),
                    range != null ? tone : !hired && RotaUnmetOf(sheet, r.Plan, w) != null ? TableRow.Yellow : hired ? TableRow.Green : TableRow.Plain);
            }
            RotaWaysOut(sheet, r);
            RotaGaps(sheet, r.Plan);
            RotaGoods(sheet, r, "pgoods", false);

            box.Title(RotaPlanName(sheet, r));
            box.Subtitle(sheet.Place.Name);
            if (index == l.Recommended && l.Count > 1)
            {
                box.Chip(CompanyText.T("reallife_ibox_chip_advised"), CellTone.Green);
            }
            box.Note(RotaWhyPlan(sheet, r, index));
            box.Section("reallife_ibox_sec_week");
            if (shop.HasCustomers)
            {
                RotaWeekSum s = r.Sum;
                box.Line(CompanyText.Fill("reallife_rota_sum_customers", "served", s.Served.ToString(), "come", s.Come.ToString(), "nobody", s.LostUnmanned.ToString(),
                    "full", s.LostFull.ToString()));
                box.Row("reallife_rota_sum_sales", RotaMoney(s.Sales));
                box.Row("reallife_rota_sum_wages", RotaMoney(-s.Wages));
                RotaSumLine(box, "reallife_rota_sum_fees", s.Fees);
                RotaSumLine(box, "reallife_rota_sum_theft", s.Theft);
                RotaSumLine(box, "reallife_rota_sum_night", s.Night);
                RotaSumLine(box, "reallife_rota_sum_demands", s.Demands);
                box.Row("reallife_rota_sum_net", RotaMoney(s.Net), s.Net >= 0 ? CellTone.Green : CellTone.Red);
                if (s.StockShort > 0)
                {
                    box.Line(CompanyText.T("reallife_rota_sum_stock", "count", s.StockShort.ToString(), "amount", RotaMoney(s.StockLost)), CellTone.Yellow);
                }
                if (s.Clean > 0)
                {
                    box.Line(CompanyText.T("reallife_rota_sum_clean", "amount", RotaMoney(s.Clean), "hours", s.CleanHours.ToString()));
                }
                if (index > 0)
                {
                    box.Line(r.Gain > 0 ? CompanyText.T("reallife_rota_sum_gain", "amount", RotaMoney(r.Gain), "plan", RotaPlanName(sheet, l.Rungs[index - 1]))
                            : CompanyText.T("reallife_rota_plan_gain_none"),
                        r.Gain > 0 ? CellTone.Green : CellTone.Plain);
                }
            }
            else
            {
                box.Row("reallife_rota_sum_wages", RotaMoney(-r.Sum.Wages));
                box.Line(CompanyText.T("reallife_rota_box_rota", "covered", r.Plan.CoveredHours.ToString(), "wanted", r.Plan.WantedHours.ToString(),
                    "unmet", r.Plan.UnmetCount.ToString()));
            }
            var buttons = box.ButtonRow();
            if (r.Hires > 0)
            {
                for (int g = 0; g < shop.Groups; g++)
                {
                    int count = RotaHiresFor(r, g);
                    if (count > 0)
                    {
                        box.Line(CompanyText.T("reallife_rota_hire_who", "count", count.ToString(), "skill", RotaGame.GroupSkill(sheet, g), "wage",
                            CompanyText.Wage(RotaLadder.Going(shop, g))));
                    }
                }
                box.Line(CompanyText.T("reallife_rota_hire_where"));
                box.Line(r.OneOff > 0 ? CompanyText.T("reallife_rota_hire_cost_once", "amount", RotaMoney(r.OneOff), "weeks", r.WeeksBack.ToString())
                    : CompanyText.T("reallife_rota_hire_cost"));
                UnityEngine.UI.Button wait = InfoBox.Button(buttons, InfoBox.Positive, "reallife_rota_use_after", () => { });
                if (wait != null)
                {
                    wait.interactable = false;   // hours nobody is there to keep are never written
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

        private static bool RotaOpensOn(bool[] open, int day)
        {
            for (int h = 0; h < RotaCatalogue.Hours; h++)
            {
                if (open[day * RotaCatalogue.Hours + h])
                {
                    return true;
                }
            }
            return false;
        }

        private static void RotaSumLine(InfoBox box, string key, long amount)
        {
            if (amount != 0)
            {
                box.Row(key, RotaMoney(-amount));
            }
        }

        private string RotaWhyPlan(RotaSheet sheet, RotaRung r, int index)
        {
            if (r.Kind == RotaRungKind.AsItIs)
            {
                return CompanyText.T("reallife_rota_why_now");
            }
            if (r.Kind == RotaRungKind.Best)
            {
                return CompanyText.T(r.Source == 0 ? "reallife_rota_why_best_same" : r.Source == 1 ? "reallife_rota_why_best_rota"
                    : r.Source == 2 ? "reallife_rota_why_best_cut" : r.Source == 4 ? "reallife_rota_why_best_fits" : "reallife_rota_why_best_hours");
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
                string words = busier != null
                    ? CompanyText.T("reallife_rota_way_busier", "name", RotaGame.WorkerName(who), "business", busier.Name, "hours", busier.GapHours.ToString())
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

        private void RotaDay(InfoBox box, RotaSheet sheet, int index, int d)
        {
            RotaShop shop = sheet.Shop;
            RotaRung r = sheet.Ladder.Rungs[index];
            string place = sheet.Place.Place;
            int stations = shop.Stations, come = 0, served = 0;
            long earns = 0, costs = 0;
            _spec = CompanyTables.RotaDay;
            for (int h = 0; h < RotaCatalogue.Hours; h++)
            {
                int cell = d * RotaCatalogue.Hours + h, wanting = System.Math.Max(shop.Customers[cell], shop.Wanting[cell]);
                bool now = shop.Open[cell], open = r.Open[cell];
                if (wanting <= 0 && !now && !open && r.Cost[cell] == 0)
                {
                    continue;   // (an hour shut that still pays somebody on a shift is listed: its cost is in the day's)
                }
                come += r.Come[cell];
                served += r.Served[cell];
                earns += r.Earn[cell];
                costs += r.Cost[cell];
                TableRow row = Row("hour" + h);
                row.A = RotaHour(h);
                row.B = !open ? CompanyText.T("reallife_rota_hour_shut", "come", wanting.ToString())
                    : r.Served[cell] == 0 && r.Come[cell] > 0 ? CompanyText.T("reallife_rota_hour_nobody", "come", r.Come[cell].ToString())
                    : CompanyText.T("reallife_rota_hour_come", "come", r.Come[cell].ToString(), "cap", RotaAdvisor.Serves(shop, cell, r.Plan.At).ToString(),
                        "served", r.Served[cell].ToString());
                row.C = open || r.Cost[cell] != 0
                    ? CompanyText.T("reallife_rota_hour_money", "earn", RotaMoney(r.Earn[cell]), "cost", RotaMoney(r.Cost[cell])) : string.Empty;
                string who = string.Empty;
                for (int s = 0; s < stations; s++)
                {
                    int w = r.Plan.At[cell * stations + s];
                    if (w >= 0)
                    {
                        who += (who.Length > 0 ? "; " : string.Empty) + RotaGame.Name(sheet.Stations[s]) + ": "
                               + (w < sheet.Workers.Count ? RotaGame.WorkerName(sheet.Workers[w]) : CompanyText.T("reallife_rota_hour_new"));
                    }
                }
                row.D = !open ? CompanyText.T("reallife_rota_closed") : who.Length > 0 ? who : CompanyText.T("reallife_rota_hour_nobody_at_work");
                row.Tone = open != now ? (open ? TableRow.Green : TableRow.Yellow) : open && r.Served[cell] < r.Come[cell] ? TableRow.Red : TableRow.Plain;
            }
            box.Title(CompanyText.T("reallife_rota_day_title", "day", RotaDayName(d), "business", sheet.Place.Name));
            box.Subtitle(RotaPlanName(sheet, r));
            box.Section("reallife_ibox_sec_day");
            box.Row("reallife_rota_day_now", RotaHours(shop.Open, d));
            box.Row("reallife_rota_day_plan", RotaHours(r.Open, d));
            box.Line(CompanyText.Fill("reallife_rota_day_sum", "served", served.ToString(), "come", come.ToString(), "earn", RotaMoney(earns), "cost", RotaMoney(costs)));
            if (shop.DayFee[d] > 0 && RotaOpensOn(r.Open, d))
            {
                box.Row("reallife_rota_day_fee", RotaMoney(shop.DayFee[d]));   // charged only on a day it opens (RotaLadder.Evaluate)
            }
            box.Note(CompanyText.T("reallife_rota_day_explain"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_rota_day_back", () =>
            {
                _pick = "rung:" + place + ":" + index;
                _table.ClearSelection();
                Refresh();
            });
        }
    }
}
