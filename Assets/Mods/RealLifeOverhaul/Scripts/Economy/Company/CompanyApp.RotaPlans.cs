using Entities;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Rota tab's page of one business: its ladder of plans, its people, stations and goods, and the advice.</summary>
    public sealed partial class CompanyApp
    {
        private void RotaPlans(InfoBox box, RotaSheet sheet)
        {
            RotaPlace at = sheet.Place;
            RotaShop shop = sheet.Shop;
            RotaOutlook o = sheet.Outlook;
            RotaLadderResult l = sheet.Ladder;
            string place = at.Place;
            string refusal = CompanyDirector.RotaRefusal(sheet);
            box.Title(at.Name);
            box.Subtitle(RotaGame.TypeName(at.Reg));
            if (at.Type == RotaType.Warehouse || sheet.Stations.Count == 0)
            {
                _spec = CompanyTables.RotaWeek;
                Say("refused", RotaRefused(refusal));
                box.Line(RotaRefused(refusal), CellTone.Yellow);
                BackButton(box);
                return;
            }
            _spec = CompanyTables.RotaPlans;
            for (int i = 0; i < l.Count; i++)
            {
                RotaRung r = l.Rungs[i];
                bool advised = i == l.Recommended && l.Count > 1;
                string name = RotaPlanName(sheet, r);
                bool unmanned = shop.HasCustomers && r.Sum.Manned != r.Sum.Hours;
                TableRow row = Row("rung:" + place + ":" + i);
                row.Pick = true;
                row.Set(0, advised ? CompanyText.T("reallife_rota_plan_advised", "plan", name) : name, advised ? TableRow.Green : TableRow.Plain);
                row.Set(1, unmanned ? CompanyText.T("reallife_rota_plan_hours", "hours", r.Sum.Hours.ToString(), "manned", r.Sum.Manned.ToString())
                    : CompanyText.T("reallife_rota_plan_hours_all", "hours", r.Sum.Hours.ToString()), unmanned ? TableRow.Yellow : TableRow.Plain);
                row.Set(2, r.Hires > 0 ? CompanyText.T("reallife_rota_plan_staff_hire", "count", r.Staff.ToString(), "hire", r.Hires.ToString()) : r.Staff.ToString());
                row.Set(3, shop.HasCustomers ? RotaMoney(r.Sum.Net) : RotaMoney(-r.Sum.Wages));
                string gain = i == 0 ? string.Empty
                    : r.Gain > 0 ? CompanyText.T("reallife_rota_plan_gain", "amount", RotaMoney(r.Gain)) : CompanyText.T("reallife_rota_plan_gain_none");
                row.Set(4, gain, r.Gain > 0 ? TableRow.Green : TableRow.Plain);
                row.Set(5, RotaNeeds(sheet, r) + (r.Left.Count > 0 ? " " + CompanyText.T("reallife_rota_plan_leaves", "count", r.Left.Count.ToString())
                        : r.Out > 0 ? " " + CompanyText.T("reallife_rota_plan_outside", "count", r.Out.ToString()) : string.Empty),
                    r.Left.Count > 0 || r.Out > 0 ? TableRow.Red : TableRow.Plain);
            }
            RotaYourPeople(sheet, l.Rungs[l.Recommended]);
            RotaStations(sheet);
            RotaGoods(sheet, l.Rungs[l.Recommended], "goods", true);

            bool gaps = !at.Shut && at.Staff > 0 && at.GapHours > 0;
            box.Chip(at.Staff == 0 ? CompanyText.T("reallife_rota_row_no_staff") : gaps ? CompanyText.T("reallife_rota_row_gaps", "hours", at.GapHours.ToString())
                    : CompanyText.T("reallife_rota_row_covered"),
                at.Staff == 0 ? CellTone.Red : gaps ? CellTone.Yellow : CellTone.Green);
            if (shop.HasCustomers)
            {
                box.Section("reallife_ibox_sec_customers");
                box.Line(CompanyText.T(sheet.FromBooks ? "reallife_rota_box_worth_books" : "reallife_rota_box_worth_prices", "takings", CompanyText.Wage(shop.Takings),
                    "margin", CompanyText.Wage(shop.Margin)));
                int peak = 0, cap = RotaServes(shop);
                for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
                {
                    peak = System.Math.Max(peak, System.Math.Max(shop.Customers[cell], shop.Wanting[cell]));
                }
                box.Line(CompanyText.Fill("reallife_rota_box_customers", "count", o.CustomersWeek.ToString(), "peak", peak.ToString(), "cap", cap.ToString()));
                RotaWeekSum now = l.Rungs[0].Sum;
                if (now.Hours > now.Manned)
                {
                    box.Line(CompanyText.T("reallife_rota_box_unmanned", "hours", (now.Hours - now.Manned).ToString(), "customers", now.LostUnmanned.ToString()),
                        CellTone.Yellow);
                }
                RotaGoodsLine(box, sheet);
                box.Section("reallife_ibox_sec_advice");
                string note = !sheet.RequirementsMet ? CompanyText.T("reallife_rota_note_requirements")
                    : at.Shut ? CompanyText.T("reallife_rota_note_shut")
                    : !o.Advised ? CompanyText.T("reallife_rota_note_no_worth")
                    : null;
                if (note != null)
                {
                    box.Line(note, CellTone.Yellow);
                }
                RotaAdvice(box, sheet);
                // The late opening permit, whenever it matters: the business opens late without one, or paying hours are barred.
                string fee = CompanyText.Money(LicenceRules.Fee((int)LicenceKind.Late, _director.Index));
                string term = CompanyCatalogue.LicenceTerm[(int)LicenceKind.Late].ToString();
                if (sheet.Late)
                {
                    box.Line(CompanyText.T("reallife_rota_note_late", "fee", fee, "days", term), CellTone.Yellow);
                }
                if (o.Barred)
                {
                    box.Note(CompanyText.Fill("reallife_rota_note_permit", "fee", fee, "days", term, "hours", (o.HoursFree - o.HoursBest).ToString(),
                        "amount", RotaMoney(o.ProfitFree - o.ProfitBest)));
                    if (l.Rungs[l.Recommended].Sum.StockShort > 0)
                    {
                        box.Note(CompanyText.T("reallife_rota_note_permit_stock"));   // that figure counts no stock, and the goods run out already
                    }
                }
            }
            else
            {
                box.Note(CompanyText.T(at.Type == RotaType.Headquarters ? "reallife_rota_hq"
                    : at.Type == RotaType.Work ? "reallife_rota_work" : "reallife_rota_no_customers"));
            }
            if (shop.Cleaned)
            {
                box.Note(CompanyText.T("reallife_rota_cleaned"));
            }
            box.Section("reallife_ibox_sec_rota");
            if (refusal != null)
            {
                box.Line(RotaRefused(refusal), CellTone.Yellow);
            }
            else
            {
                box.Meter("reallife_ibox_covered", LookRules.Fill(sheet.Now.CoveredHours, sheet.Now.WantedHours),
                    CompanyText.T("reallife_ibox_of", "value", sheet.Now.CoveredHours.ToString(), "most", sheet.Now.WantedHours.ToString()),
                    sheet.Now.CoveredHours >= sheet.Now.WantedHours ? CellTone.Green : CellTone.Yellow);
                box.Note(CompanyText.T("reallife_rota_box_rota", "covered", sheet.Now.CoveredHours.ToString(), "wanted", sheet.Now.WantedHours.ToString(),
                    "unmet", sheet.Now.UnmetCount.ToString()));
            }
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
                : CompanyText.T(more < 0 ? "reallife_rota_advice_best_less" : "reallife_rota_advice_best", "amount", RotaMoney(System.Math.Abs(more))),
                l.Recommended == 0 ? CellTone.Plain : CellTone.Green);
            if (l.Stop == RotaEnd.TooLittle)
            {
                box.Line(CompanyText.T("reallife_rota_stop_little", "skill", RotaGame.GroupSkill(sheet, l.StopGroup), "amount", RotaMoney(l.StopGain),
                    "need", RotaMoney(l.StopNeed)));
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
            box.Section("reallife_rota_mode_label");
            if (!box.Selector(RotaModeKeys.Length, chosen, i => i == RotaMode.Auto ? CompanyText.T(RotaModeKeys[0], "mode", made) : CompanyText.T(RotaModeKeys[i]),
                i => _director.RotaChoose(place, i)))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, chosen == RotaMode.StaffFirst ? "reallife_rota_mode_to_lean" : "reallife_rota_mode_to_staff",
                    () => _director.RotaChoose(place, chosen == RotaMode.StaffFirst ? RotaMode.Lean : RotaMode.StaffFirst));
            }
            box.Note(CompanyText.T(l.Mode == RotaMode.StaffFirst ? "reallife_rota_mode_explain_staff" : "reallife_rota_mode_explain_lean"));
            if (l.ModeAuto)
            {
                box.Note(CompanyText.T("reallife_rota_mode_figures", "staff", RotaMoney(l.StaffNet), "lean", RotaMoney(l.LeanNet)));
            }
        }

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
            bool runsOut = l.Rungs[l.Recommended].Sum.StockShort > 0;   // more customers would find the goods gone
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

        private static void RotaGoodsLine(InfoBox box, RotaSheet sheet)
        {
            RotaStock stock = sheet.Shop.Stock;
            if (stock.Products == 0)
            {
                return;
            }
            RotaWeekSum advised = sheet.Ladder.Rungs[sheet.Ladder.Recommended].Sum;
            box.Line(!stock.Any ? CompanyText.T("reallife_rota_box_stock_hand")
                : advised.StockShort > 0 ? CompanyText.T("reallife_rota_box_stock_short", "count", advised.StockShort.ToString(), "amount", RotaMoney(advised.StockLost))
                : CompanyText.T("reallife_rota_box_stock_ok"), stock.Any && advised.StockShort > 0 ? CellTone.Yellow : CellTone.Plain);
            for (int p = 0; p < stock.Products && p < sheet.Products.Count; p++)
            {
                if (stock.Backorder[p] || stock.Shortage[p])
                {
                    string key = stock.Backorder[p] ? "reallife_rota_stock_backorder" : "reallife_rota_stock_shortage";
                    box.Line(CompanyText.T(key, "product", RotaGame.Text(sheet.Products[p])), CellTone.Yellow);
                }
            }
        }

        // The goods that run out in a plan, and those no delivery brings that would not last the week.
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
    }
}
