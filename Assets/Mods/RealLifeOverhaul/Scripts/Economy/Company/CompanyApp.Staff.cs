using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Staff tab: what waits for an answer, then the staff, and the sick pay policy.</summary>
    public sealed partial class CompanyApp
    {
        partial void WriteStaff(InfoBox box, CompanyState state, int day, ref string selected)
        {
            IReadOnlyList<CompanyShop> shops = _director.Shops;
            for (int i = 0; i < shops.Count; i++)
            {
                ShopFile file = shops[i].File;
                if (file.StrikeFrom > 0)
                {
                    Pick("strike:" + file.Place, CompanyText.T("reallife_co_row_strike"), shops[i].Name,
                        CompanyText.T(file.Striking(day) ? "reallife_co_row_strike_until" : "reallife_co_row_strike_from", "day",
                            StaffRules.StrikeShown(file, day).ToString()),
                        CompanyText.T("reallife_co_row_answer"), 2, ref selected);
                    Last.Pinned = true;   // something to answer, not a member of staff: it stays at the head of the list
                }
                if (file.OvertimeAsk > 0)
                {
                    Pick("ot:" + file.Place, CompanyText.T("reallife_co_row_overtime"), shops[i].Name,
                        CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(_director.OvertimeCost(file.Place))),
                        CompanyText.T("reallife_co_row_answer"), 1, ref selected);
                    Last.Pinned = true;
                }
            }
            int under = 0, low = 0;
            TableFilter businesses = CompanyTables.Staff.Filters[0];   // its kinds are the businesses the staff work at
            businesses.Names.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < state.Staff.Count; i++)
                {
                    StaffFile f = state.Staff[i];
                    bool waits = f.AskDay > 0 || f.PoachDay > 0;
                    if (waits != (pass == 0))
                    {
                        continue;
                    }
                    under += StaffRules.Underpaid(f, _director.Level) ? 1 : 0;
                    low += StaffRules.StandsLow(f) ? 1 : 0;
                    string stands = f.AskDay > 0 ? CompanyText.T("reallife_co_row_asks") : f.PoachDay > 0 ? CompanyText.T("reallife_co_row_offer")
                        : f.LeaveDay > 0 ? CompanyText.T("reallife_co_row_leaves", "day", f.LeaveDay.ToString()) : f.Standing.ToString();
                    string business = _director.ShopName(f.Place);
                    Pick("emp:" + f.Id, f.Name, business, CompanyText.T("reallife_co_row_pay", "paid", CompanyText.Wage(f.Paid), "going", CompanyText.Wage(f.Going)), stands,
                        waits || f.LeaveDay > 0 ? 2 : StaffRules.StandsLow(f) ? 1 : 0, ref selected);
                    // Sorted by the hourly pay, the standing, and a working day's pay (which the totals line adds up).
                    TableRow row = Last;
                    float aDay = f.Paid / 100f * (f.DayHours / 100f);
                    row.Kind = CompanyTables.KindOf(businesses, business);
                    row.Kind2 = waits ? CompanyTables.StaffWaiting : f.LeaveDay > 0 ? CompanyTables.StaffLeaving
                        : StaffRules.Underpaid(f, _director.Level) ? CompanyTables.StaffUnderpaid : CompanyTables.StaffFine;
                    row.Key[2] = f.Paid / 100.0;
                    row.Key[3] = f.Standing;
                    row.Set(4, CompanyText.Money(aDay), aDay);
                    row.Set(5, f.Skill.ToString(), f.Skill);
                }
            }
            if (_rows.Count == 0)
            {
                Say("none", CompanyText.T("reallife_co_row_no_staff"));
            }

            string id = Picked("emp:");
            StaffFile picked = id != null ? state.Employee(id) : null;
            string place = Picked("strike:");
            CompanyShop strike = place != null ? StrikeShop(place) : null;
            place = Picked("ot:");
            CompanyShop overtime = place != null ? OvertimeShop(place) : null;
            if (picked != null)
            {
                WriteEmployee(box, picked);
            }
            else if (strike != null)
            {
                WriteStrike(box, strike, day);
            }
            else if (overtime != null)
            {
                WriteOvertime(box, overtime);
            }
            else
            {
                box.Title(CompanyText.T("reallife_co_tab_staff"));
                int perHour, asks = _director.RaisesWaiting(out perHour);
                if (asks > 1)
                {
                    // Many at once (a district's income rose, prices moved): one answer for all.
                    box.Line(CompanyText.T("reallife_co_staff_asks", "count", asks.ToString(), "amount", CompanyText.Wage(perHour)), CellTone.Yellow);
                    var buttons = box.ButtonRow();
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_co_staff_agree_all", () => _director.AnswerAllRaises(RaiseAnswer.Agree));
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_co_staff_refuse_all", () => _director.AnswerAllRaises(RaiseAnswer.Refuse));
                }
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_co_staff_count", state.Staff.Count.ToString());
                box.Row("reallife_co_staff_under", under.ToString(), under > 0 ? CellTone.Yellow : CellTone.Plain);
                box.Row("reallife_co_staff_low", low.ToString(), low > 0 ? CellTone.Red : CellTone.Plain);
                box.Headline("reallife_co_staff_sick");
                if (!box.HasSelector || !box.Selector(CompanyText.SickKeys.Length, state.SickPolicy, i => CompanyText.T(CompanyText.SickKeys[i]),
                        i => _director.SetSickPolicy(i)))
                {
                    box.Line(CompanyText.T(CompanyText.SickKeys[state.SickPolicy]));
                }
                box.Note(CompanyText.T("reallife_co_staff_sick_what", "share", CompanyText.Share(CompanyCatalogue.SickShare[state.SickPolicy]),
                    "standing", CompanyText.Percent(CompanyCatalogue.SickStanding[state.SickPolicy]).TrimEnd('%')));
                box.Note(CompanyText.T("reallife_co_staff_market"));
                Guide(box);
            }
        }

        private CompanyShop StrikeShop(string place)
        {
            CompanyShop shop = _director.ShopAt(place);
            return shop != null && shop.File.StrikeFrom > 0 ? shop : null;
        }

        private CompanyShop OvertimeShop(string place)
        {
            CompanyShop shop = _director.ShopAt(place);
            return shop != null && shop.File.OvertimeAsk > 0 ? shop : null;
        }

        // Answer buttons come straight after the title: the box does not scroll, so a button at the foot of a long
        // page could be out of reach.
        private void WriteEmployee(InfoBox box, StaffFile f)
        {
            string skill = CompanyGame.SkillName(f.Id);
            string id = f.Id;
            box.Title(f.Name);
            box.Subtitle(CompanyText.T("reallife_co_emp_works", "business", _director.ShopName(f.Place), "skill", skill));
            if (f.AskDay > 0)
            {
                box.Chip(CompanyText.T("reallife_co_emp_asks"), CellTone.Yellow);
            }
            else if (f.PoachDay > 0)
            {
                box.Chip(CompanyText.T("reallife_co_emp_offer"), CellTone.Red);
            }
            else if (f.LeaveDay > 0)
            {
                box.Chip(CompanyText.T("reallife_ibox_chip_leaving"), CellTone.Red);
            }
            if (f.AskDay == 0 && f.PoachDay == 0 && f.LeaveDay > 0)
            {
                box.Line(CompanyText.T("reallife_co_emp_leaving", "day", f.LeaveDay.ToString(), "rival", f.Rival), CellTone.Red);
            }
            if (f.AskDay > 0)
            {
                box.Headline("reallife_co_emp_asks");
                box.Line(CompanyText.T("reallife_co_emp_ask", "wage", CompanyText.Wage(f.AskTo), "day", (f.AskDay + CompanyCatalogue.RaiseAnswerDays).ToString()));
                box.Line(CompanyText.T("reallife_co_emp_ask_choices", "all", CompanyText.Wage(StaffRules.RaiseFor(f, RaiseAnswer.Agree)),
                    "less", CompanyText.Wage(StaffRules.RaiseFor(f, RaiseAnswer.Less))));
                var buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_co_emp_agree", () => _director.AnswerRaise(id, RaiseAnswer.Agree));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_emp_less", () => _director.AnswerRaise(id, RaiseAnswer.Less));
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_co_emp_refuse", () => _director.AnswerRaise(id, RaiseAnswer.Refuse));
            }
            else if (f.PoachDay > 0)
            {
                box.Headline("reallife_co_emp_offer");
                box.Note(CompanyText.T("reallife_co_emp_offer_what", "rival", f.Rival, "wage", CompanyText.Wage(f.PoachOffer), "day",
                    (f.PoachDay + CompanyCatalogue.PoachAnswerDays).ToString()));
                box.Line(CompanyText.T("reallife_co_emp_offer_choices", "raise", CompanyText.Wage(System.Math.Max(0, f.PoachOffer - f.Paid)), "days",
                    CompanyCatalogue.PoachNoticeDays.ToString()));
                var buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_co_emp_match", () => _director.AnswerOffer(id, true));
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_co_emp_let_go", () => _director.AnswerOffer(id, false));
            }
            else if (StaffRules.CanGive(f, _director.Today))
            {
                // Nobody asked, or a request lapsed: the player may still bring them up to the going rate.
                box.Note(CompanyText.T("reallife_co_emp_give_what", "raise", CompanyText.Wage(f.Going - f.Paid)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_co_emp_give", () => _director.GiveRaise(id));
            }
            else if (f.LeaveDay == 0 && f.Going > f.Paid)
            {
                box.Note(CompanyText.T("reallife_wage_rested_line", "day", f.AskRest.ToString()));   // one raise in the rest's time
            }
            box.Section("reallife_ibox_sec_pay");
            box.Row("reallife_co_emp_wage", CompanyText.Wage(f.Wage));
            if (f.Raise > 0)
            {
                box.Row("reallife_co_emp_raise", CompanyText.Wage(f.Raise));
            }
            box.Row("reallife_co_emp_going", CompanyText.Wage(f.Going), f.Going > f.Paid ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_ibox_sec_feeling");
            box.Meter("reallife_co_emp_standing", f.Standing / 100f, f.Standing.ToString(),
                StaffRules.StandsLow(f) ? CellTone.Red : f.Standing < 60 ? CellTone.Yellow : CellTone.Green);
            box.Meter("reallife_co_emp_mood", f.Mood / 100f, f.Mood.ToString(), f.Mood < 30 ? CellTone.Red : f.Mood < 60 ? CellTone.Yellow : CellTone.Green);
            BackButton(box);
        }

        private void WriteStrike(InfoBox box, CompanyShop shop, int day)
        {
            ShopFile file = shop.File;
            string place = file.Place;
            box.Title(CompanyText.T("reallife_co_strike_title", "business", shop.Name));
            box.Chip(CompanyText.T(file.Striking(day) ? "reallife_ibox_chip_striking" : "reallife_ibox_chip_called"), CellTone.Red);
            box.Line(CompanyText.T(file.Striking(day) ? "reallife_co_strike_on" : "reallife_co_strike_called", "from", file.StrikeFrom.ToString(),
                "end", file.StrikeEnd.ToString()), CellTone.Red);
            box.Line(CompanyText.T("reallife_co_strike_effect", "percent", (-CompanyCatalogue.StrikeFootfall).ToString(), "hours", CompanyCatalogue.StrikeClosedHours.ToString()));
            box.Note(CompanyText.T("reallife_co_strike_why", "low", shop.Low.ToString(), "count", shop.Staff.ToString()));
            box.Headline("reallife_co_strike_ways");
            box.Line(CompanyText.T("reallife_co_strike_raise", "amount", CompanyText.Wage(_director.StrikeCost(place, StrikeAnswer.Raise))));
            box.Line(CompanyText.T("reallife_co_strike_payment", "amount", CompanyText.Money(_director.StrikeCost(place, StrikeAnswer.Payment)),
                "hours", CompanyCatalogue.StrikePayHours.ToString()));
            box.Line(CompanyText.T("reallife_co_strike_sit", "day", file.StrikeEnd.ToString()));
            var buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_co_strike_do_raise", () => _director.SettleStrike(place, StrikeAnswer.Raise));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_strike_do_payment", () => _director.SettleStrike(place, StrikeAnswer.Payment));
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_co_strike_do_sit", Back);
            Guide(box);
        }

        private void WriteOvertime(InfoBox box, CompanyShop shop)
        {
            string place = shop.File.Place;
            box.Title(CompanyText.T("reallife_co_overtime_title", "business", shop.Name));
            box.Line(CompanyText.T("reallife_co_overtime_by", "day", (shop.File.OvertimeAsk + CompanyCatalogue.OvertimeAnswerDays).ToString()), CellTone.Yellow);
            box.Note(CompanyText.T("reallife_co_overtime_what", "hours", CompanyCatalogue.OvertimeHours.ToString(), "days", CompanyCatalogue.OvertimeDays.ToString()));
            box.Section("reallife_ibox_sec_terms");
            box.Row("reallife_co_overtime_cost", CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(_director.OvertimeCost(place))));
            box.Row("reallife_co_overtime_gain", CompanyText.Percent(CompanyCatalogue.OvertimeFootfall));
            var buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_co_overtime_agree", () => _director.AnswerOvertime(place, true));
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_co_overtime_refuse", () => _director.AnswerOvertime(place, false));
            BackButton(box);
        }
    }
}
