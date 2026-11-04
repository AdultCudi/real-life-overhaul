using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class CompanyLive
    {
        // The Staff tab: what waits for an answer at the head of the list (a strike, an offered overtime), then the staff, those who ask first; at the right the picked one's page,
        // or the staff's own (how many, who is underpaid, the sick pay policy and the answer for all at once).
        private void ReadStaff(CompanyFacts f, CompanyDirector d, CompanyView v, Words w)
        {
            CompanyPage page = f.Staff;
            if (!d.On(CompanyPart.Staff))
            {
                PartOff(page, w.T("reallife_co_tab_staff", "Staff"));
                return;
            }
            CompanyState s = d.State;
            int day = d.Today;
            var rows = new List<CompanyRow>(s.Staff.Count + 4);
            int under = 0, low = 0;
            IReadOnlyList<CompanyShop> shops = d.Shops;
            for (int i = 0; i < shops.Count; i++)
            {
                ShopFile file = shops[i].File;
                if (file.StrikeFrom > 0)
                {
                    rows.Add(new CompanyRow
                    {
                        Id = "strike:" + file.Place, Title = CompanyText.T("reallife_co_row_strike"), Sub = shops[i].Name, Icon = UiIcon.Megaphone, Tint = UiRole.None,
                        Figure = CompanyText.T(file.Striking(day) ? "reallife_co_row_strike_until" : "reallife_co_row_strike_from", "day", StaffRules.StrikeShown(file, day).ToString()),
                        Chip = CompanyText.T("reallife_co_row_answer"), ChipTone = UiRole.Danger
                    });
                }
                if (file.OvertimeAsk > 0)
                {
                    rows.Add(new CompanyRow
                    {
                        Id = "ot:" + file.Place, Title = CompanyText.T("reallife_co_row_overtime"), Sub = shops[i].Name, Icon = UiIcon.Clock, Tint = UiRole.None,
                        Figure = CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(d.OvertimeCost(file.Place))), Chip = CompanyText.T("reallife_co_row_answer"), ChipTone = UiRole.Warn
                    });
                }
            }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < s.Staff.Count; i++)
                {
                    StaffFile p = s.Staff[i];
                    bool waits = p.AskDay > 0 || p.PoachDay > 0;
                    if (waits != (pass == 0))
                    {
                        continue;
                    }
                    under += StaffRules.Underpaid(p, d.Level) ? 1 : 0;
                    low += StaffRules.StandsLow(p) ? 1 : 0;
                    Face(p.Name, p.Id);
                    string stands = p.AskDay > 0 ? CompanyText.T("reallife_co_row_asks") : p.PoachDay > 0 ? CompanyText.T("reallife_co_row_offer")
                        : p.LeaveDay > 0 ? CompanyText.T("reallife_co_row_leaves", "day", p.LeaveDay.ToString()) : null;
                    rows.Add(new CompanyRow
                    {
                        Id = "emp:" + p.Id, Title = p.Name, Sub = d.ShopName(p.Place), Face = true, Hue = UiColour.HueOf(p.Name),
                        Ring = waits || p.LeaveDay > 0 ? UiRole.Warn : StaffRules.StandsLow(p) ? UiRole.Danger : UiRole.Ok,
                        Figure = w.PerHour(p.Paid), Note = w.F("reallife_co_going", "going {wage}", "wage", CompanyText.Wage(p.Going)),
                        Chip = stands, ChipTone = p.PoachDay > 0 || p.LeaveDay > 0 ? UiRole.Danger : UiRole.Warn
                    });
                }
            }
            page.List = new CompanyList
            {
                Title = w.T("reallife_co_staff_list", "Staff"), Count = s.Staff.Count.ToString(), Rows = rows.ToArray(), Empty = CompanyText.T("reallife_co_row_no_staff")
            };
            string pick = v.Pick[CompanyView.Staff] ?? string.Empty;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Id == pick)
                {
                    page.List.Chosen = i;
                }
            }
            StaffFile picked = pick.StartsWith("emp:", StringComparison.Ordinal) ? s.Employee(pick.Substring(4)) : null;
            CompanyShop strike = pick.StartsWith("strike:", StringComparison.Ordinal) ? d.ShopAt(pick.Substring(7)) : null;
            CompanyShop overtime = pick.StartsWith("ot:", StringComparison.Ordinal) ? d.ShopAt(pick.Substring(3)) : null;
            if (picked != null)
            {
                page.Card = EmployeeCard(d, picked, w);
            }
            else if (strike != null && strike.File.StrikeFrom > 0)
            {
                page.Card = StrikeCard(d, strike, day, w);
            }
            else if (overtime != null && overtime.File.OvertimeAsk > 0)
            {
                page.Card = OvertimeCard(d, overtime, w);
            }
            else
            {
                page.List.Chosen = -1;
                page.Top = StaffSummary(d, s, under, low, w);
            }
        }

        // The staff's own page: how many, who is underpaid and who stands low, the answer for all the raises asked at once, and the sick pay policy.
        private CompanyCard StaffSummary(CompanyDirector d, CompanyState s, int under, int low, Words w)
        {
            var c = new CompanyCard { Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = w.T("reallife_co_staff_title", "Your staff"), Icon = UiIcon.People };
            c.FactLabels = new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_underpaid", "Underpaid"), w.T("reallife_co_f_low", "Standing low") };
            c.FactValues = new[] { s.Staff.Count.ToString(), under.ToString(), low.ToString() };
            var blocks = new List<CompanyBlock>(12);
            int perHour, asks = d.RaisesWaiting(out perHour);
            if (asks > 1)
            {
                blocks.Add(Sec(w.T("reallife_co_staff_asks_sec", "Asking for a raise")));
                blocks.Add(Note(CompanyText.T("reallife_co_staff_asks", "count", asks.ToString(), "amount", CompanyText.Wage(perHour)), UiRole.WarnText));
                blocks.Add(Buttons(
                    Btn(CompanyText.T("reallife_co_staff_agree_all"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.RaisesAll, null, 0), UiAction.Alt, UiIcon.Check),
                    Btn(CompanyText.T("reallife_co_staff_refuse_all"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RaisesAll, null, 1), UiAction.Count, UiIcon.Close, true)));
            }
            blocks.Add(Sec(w.T("reallife_co_staff_sick", "Sick pay")));
            var options = new string[CompanyText.SickKeys.Length];
            var acts = new CompanyAct[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = CompanyText.T(CompanyText.SickKeys[i]);
                acts[i] = CompanyAct.Of(CompanyDo.SickPolicy, null, i);
            }
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_staff_sick", "Sick pay"), Options = options, Chosen = s.SickPolicy, Acts = acts });
            blocks.Add(Note(CompanyText.T("reallife_co_staff_sick_what", "share", CompanyText.Share(CompanyCatalogue.SickShare[s.SickPolicy]),
                "standing", CompanyText.Percent(CompanyCatalogue.SickStanding[s.SickPolicy]).TrimEnd('%'))));
            blocks.Add(Note(CompanyText.T("reallife_co_staff_market")));
            blocks.Add(Note(CompanyText.T("reallife_co_staff_picked")));
            c.Blocks = blocks.ToArray();
            return c;
        }

        // A member of staff: who they are and what they earn, what they ask, an offer from a rival, and how they feel.
        private CompanyCard EmployeeCard(CompanyDirector d, StaffFile p, Words w)
        {
            string skill = CompanyGame.SkillName(p.Id);
            var c = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = p.Name, Sub = CompanyText.T("reallife_co_emp_works", "business", d.ShopName(p.Place), "skill", skill), Face = true, Hue = UiColour.HueOf(p.Name),
                Ring = p.AskDay > 0 || p.PoachDay > 0 || p.LeaveDay > 0 ? UiRole.Warn : UiRole.Ok,
                FactLabels = new[] { w.T("reallife_co_f_paid", "Paid"), w.T("reallife_co_f_going", "Going rate"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_mood", "Mood") },
                FactValues = new[] { CompanyText.Wage(p.Paid), CompanyText.Wage(p.Going), p.Standing.ToString(), p.Mood.ToString() },
                Back = new[] { Btn(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "", CompanyView.Staff), UiAction.Count, UiIcon.Back) }
            };
            var chips = new List<string>(2);
            var tones = new List<UiRole>(2);
            if (p.AskDay > 0)
            {
                chips.Add(CompanyText.T("reallife_co_emp_asks"));
                tones.Add(UiRole.Warn);
            }
            else if (p.PoachDay > 0)
            {
                chips.Add(CompanyText.T("reallife_co_emp_offer"));
                tones.Add(UiRole.Danger);
            }
            else if (p.LeaveDay > 0)
            {
                chips.Add(CompanyText.T("reallife_ibox_chip_leaving"));
                tones.Add(UiRole.Danger);
            }
            c.Chips = chips.ToArray();
            c.ChipTones = tones.ToArray();
            var blocks = new List<CompanyBlock>(16);
            if (p.AskDay == 0 && p.PoachDay == 0 && p.LeaveDay > 0)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_emp_leaving", "day", p.LeaveDay.ToString(), "rival", p.Rival), UiRole.DangerText));
            }
            if (p.AskDay > 0)
            {
                blocks.Add(Sec(CompanyText.T("reallife_co_emp_asks")));
                blocks.Add(Note(CompanyText.T("reallife_co_emp_ask", "wage", CompanyText.Wage(p.AskTo), "day", (p.AskDay + CompanyCatalogue.RaiseAnswerDays).ToString())));
                blocks.Add(Note(CompanyText.T("reallife_co_emp_ask_choices", "all", CompanyText.Wage(StaffRules.RaiseFor(p, RaiseAnswer.Agree)), "less", CompanyText.Wage(StaffRules.RaiseFor(p, RaiseAnswer.Less)))));
                blocks.Add(Buttons(
                    Btn(CompanyText.T("reallife_co_emp_agree"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.RaiseAnswer, p.Id, (int)RaiseAnswer.Agree), UiAction.Alt, UiIcon.Check),
                    Btn(CompanyText.T("reallife_co_emp_less"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RaiseAnswer, p.Id, (int)RaiseAnswer.Less), UiAction.Option),
                    Btn(w.F("reallife_co_emp_bonus", "Bonus {amount}", "amount", w.Money(d.BonusOf(p.Place))), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RaiseAnswer, p.Id, (int)RaiseAnswer.Bonus)),
                    Btn(CompanyText.T("reallife_co_emp_refuse"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RaiseAnswer, p.Id, (int)RaiseAnswer.Refuse), UiAction.Count, UiIcon.Close, true)));
            }
            else if (p.PoachDay > 0)
            {
                blocks.Add(Sec(CompanyText.T("reallife_co_emp_offer")));
                blocks.Add(Note(CompanyText.T("reallife_co_emp_offer_what", "rival", p.Rival, "wage", CompanyText.Wage(p.PoachOffer), "day", (p.PoachDay + CompanyCatalogue.PoachAnswerDays).ToString())));
                blocks.Add(Note(CompanyText.T("reallife_co_emp_offer_choices", "raise", CompanyText.Wage(Math.Max(0, p.PoachOffer - p.Paid)), "days", CompanyCatalogue.PoachNoticeDays.ToString())));
                blocks.Add(Buttons(
                    Btn(CompanyText.T("reallife_co_emp_match"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.OfferAnswer, p.Id, 1), UiAction.Alt, UiIcon.Check),
                    Btn(CompanyText.T("reallife_co_emp_let_go"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.OfferAnswer, p.Id, 0), UiAction.Count, UiIcon.Close, true)));
            }
            else if (StaffRules.CanGive(p, d.Today))
            {
                blocks.Add(Note(CompanyText.T("reallife_co_emp_give_what", "raise", CompanyText.Wage(p.Going - p.Paid))));
                blocks.Add(Buttons(Btn(CompanyText.T("reallife_co_emp_give"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.RaiseGive, p.Id), UiAction.Alt, UiIcon.Check)));
            }
            else if (p.LeaveDay == 0 && p.Going > p.Paid)
            {
                blocks.Add(Note(CompanyText.T("reallife_wage_rested_line", "day", p.AskRest.ToString())));
            }
            blocks.Add(Sec(CompanyText.T("reallife_ibox_sec_pay")));
            int city = p.Wage - p.Base;
            if (city != 0 || p.Raise > 0)
            {
                blocks.Add(Row(CompanyText.Label("reallife_wage_row_game"), CompanyText.Wage(p.Base)));
                if (city != 0)
                {
                    blocks.Add(Row(CompanyText.Label("reallife_wage_row_city"), CompanyText.SignedWage(city)));
                }
                if (p.Raise > 0)
                {
                    blocks.Add(Row(CompanyText.Label("reallife_co_emp_raise"), CompanyText.Wage(p.Raise)));
                }
            }
            blocks.Add(Row(w.T("reallife_co_f_paid", "Paid"), CompanyText.Wage(p.Paid)));
            blocks.Add(Row(w.T("reallife_co_f_going", "Going rate"), CompanyText.Wage(p.Going), p.Going > p.Paid ? UiRole.WarnText : UiRole.None));
            blocks.Add(Sec(CompanyText.T("reallife_ibox_sec_feeling")));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_standing", "Standing"), Value = p.Standing.ToString(), Number = p.Standing, Tone = StaffRules.StandsLow(p) ? UiRole.Danger : p.Standing < 60 ? UiRole.Warn : UiRole.Ok });
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_mood", "Mood"), Value = p.Mood.ToString(), Number = p.Mood, Tone = p.Mood < 30 ? UiRole.Danger : p.Mood < 60 ? UiRole.Warn : UiRole.Ok });
            if (p.LateDays > 0)
            {
                blocks.Add(Row(w.T("reallife_co_emp_late", "Short days running"), p.LateDays.ToString(), UiRole.WarnText));
            }
            c.Blocks = blocks.ToArray();
            return c;
        }

        private CompanyCard StrikeCard(CompanyDirector d, CompanyShop shop, int day, Words w)
        {
            ShopFile file = shop.File;
            string place = file.Place;
            var c = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = CompanyText.T("reallife_co_strike_title", "business", shop.Name), Icon = UiIcon.Megaphone,
                Chips = new[] { CompanyText.T(file.Striking(day) ? "reallife_ibox_chip_striking" : "reallife_ibox_chip_called") }, ChipTones = new[] { UiRole.Danger },
                Back = new[] { Btn(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "", CompanyView.Staff), UiAction.Count, UiIcon.Back) }
            };
            c.Blocks = new[]
            {
                Note(CompanyText.T(file.Striking(day) ? "reallife_co_strike_on" : "reallife_co_strike_called", "from", file.StrikeFrom.ToString(), "end", file.StrikeEnd.ToString()), UiRole.DangerText),
                Note(CompanyText.T("reallife_co_strike_effect", "percent", (-CompanyCatalogue.StrikeFootfall).ToString(), "hours", CompanyCatalogue.StrikeClosedHours.ToString())),
                Note(CompanyText.T("reallife_co_strike_why", "low", shop.Low.ToString(), "count", shop.Staff.ToString())),
                Sec(CompanyText.T("reallife_co_strike_ways")),
                Row(CompanyText.T("reallife_co_strike_do_raise"), CompanyText.Wage(d.StrikeCost(place, StrikeAnswer.Raise))),
                Row(CompanyText.T("reallife_co_strike_do_payment"), CompanyText.Money(d.StrikeCost(place, StrikeAnswer.Payment))),
                Note(CompanyText.T("reallife_co_strike_sit", "day", file.StrikeEnd.ToString())),
                Buttons(
                    Btn(CompanyText.T("reallife_co_strike_do_raise"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.StrikeAnswer, place, 0), UiAction.Alt, UiIcon.Check),
                    Btn(CompanyText.T("reallife_co_strike_do_payment"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.StrikeAnswer, place, 1)),
                    Btn(CompanyText.T("reallife_co_strike_do_sit"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "", CompanyView.Staff), UiAction.Count, UiIcon.Close))
            };
            return c;
        }

        private CompanyCard OvertimeCard(CompanyDirector d, CompanyShop shop, Words w)
        {
            string place = shop.File.Place;
            var c = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = CompanyText.T("reallife_co_overtime_title", "business", shop.Name), Icon = UiIcon.Clock,
                Back = new[] { Btn(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "", CompanyView.Staff), UiAction.Count, UiIcon.Back) }
            };
            c.Blocks = new[]
            {
                Note(CompanyText.T("reallife_co_overtime_by", "day", (shop.File.OvertimeAsk + CompanyCatalogue.OvertimeAnswerDays).ToString()), UiRole.WarnText),
                Note(CompanyText.T("reallife_co_overtime_what", "hours", CompanyCatalogue.OvertimeHours.ToString(), "days", CompanyCatalogue.OvertimeDays.ToString())),
                Sec(CompanyText.T("reallife_ibox_sec_terms")),
                Row(CompanyText.Label("reallife_co_overtime_cost"), CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(d.OvertimeCost(place)))),
                Row(CompanyText.Label("reallife_co_overtime_gain"), CompanyText.Percent(CompanyCatalogue.OvertimeFootfall)),
                Buttons(
                    Btn(CompanyText.T("reallife_co_overtime_agree"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.OvertimeAnswer, place, 1), UiAction.Alt, UiIcon.Check),
                    Btn(CompanyText.T("reallife_co_overtime_refuse"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.OvertimeAnswer, place, 0), UiAction.Count, UiIcon.Close, true))
            };
            return c;
        }
    }
}
