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
            // The old table's filters and sorts as three bars over the list: how a person stands (all, waiting, leaving, underpaid, in order) as chips, the business and the column the
            // staff are sorted by as one chip each that steps through its options. The list's filter number keeps all three (CompanyFilterBar).
            int filter = Math.Max(0, v.Filter), status = CompanyCatalogue.Clamp(filter & 7, 0, StaffStatuses - 1), sortBy = CompanyCatalogue.Clamp((filter >> 3) & 7, 0, StaffSorts - 1);
            int biz = CompanyCatalogue.Clamp((filter >> 6) & 127, 0, shops.Count > 1 ? shops.Count : 0);
            string place = biz > 0 ? shops[biz - 1].File.Place : null;
            var businesses = new List<string> { w.T("reallife_co_all_businesses", "All businesses") };
            for (int i = 0; shops.Count > 1 && i < shops.Count; i++)
            {
                businesses.Add(shops[i].Name);
            }
            for (int i = 0; (status == 0 || status == 1) && i < shops.Count; i++)
            {
                ShopFile file = shops[i].File;
                if (place != null && file.Place != place)
                {
                    continue;
                }
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
            // The staff in the order asked: the game's own with those who wait first, or by a column (ties keep the game's order).
            var order = new List<int>(s.Staff.Count);
            for (int i = 0; i < s.Staff.Count; i++)
            {
                order.Add(i);
            }
            if (sortBy > 0)
            {
                order.Sort((a, b) =>
                {
                    int c = StaffOrder(s.Staff[a], s.Staff[b], sortBy, d);
                    return c != 0 ? c : a.CompareTo(b);
                });
            }
            for (int pass = 0; pass < (sortBy == 0 ? 2 : 1); pass++)
            {
                for (int k = 0; k < order.Count; k++)
                {
                    StaffFile p = s.Staff[order[k]];
                    bool waits = p.AskDay > 0 || p.PoachDay > 0;
                    if (sortBy == 0 && waits != (pass == 0))
                    {
                        continue;
                    }
                    bool unpaid = StaffRules.Underpaid(p, d.Level);
                    under += unpaid ? 1 : 0;
                    low += StaffRules.StandsLow(p) ? 1 : 0;
                    bool shown = (status == 0 || (status == 1 && waits) || (status == 2 && p.LeaveDay > 0) || (status == 3 && unpaid) || (status == 4 && !waits && p.LeaveDay == 0 && !unpaid))
                                 && (place == null || place == p.Place);
                    if (!shown)
                    {
                        continue;
                    }
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
            var bars = new List<CompanyFilterBar>(3);
            if (s.Staff.Count > 6)
            {
                bars.Add(new CompanyFilterBar
                {
                    Options = new[] { w.T("reallife_co_f_all", "All"), w.T("reallife_co_f_waiting", "Waiting"), CompanyText.T("reallife_tab_co_staff_leaving"),
                        CompanyText.T("reallife_tab_co_staff_underpaid"),
                        CompanyText.T("reallife_tab_co_staff_fine") }, Chosen = status, Shift = 0, Bits = 3, Toggle = true
                });
                if (businesses.Count > 2)
                {
                    bars.Add(new CompanyFilterBar { Options = businesses.ToArray(), Chosen = biz, Shift = 6, Bits = 7, Step = true });
                }
                bars.Add(new CompanyFilterBar
                {
                    Options = new[] { w.T("reallife_co_sort_waiting", "waiting first"), w.T("reallife_co_sort_name", "name"), w.T("reallife_co_sort_business", "business"),
                        w.T("reallife_co_sort_pay", "pay"),
                        w.T("reallife_co_sort_standing", "standing"), w.T("reallife_co_sort_skill", "skill") }, Chosen = sortBy, Shift = 3, Bits = 3, Step = true, Label = w.T("reallife_co_f_sort",
                            "Sort: {choice}")
                });
            }
            page.List = new CompanyList
            {
                Title = w.T("reallife_co_staff_list", "Staff"), Count = s.Staff.Count.ToString(), Rows = rows.ToArray(), Empty = CompanyText.T("reallife_co_row_no_staff"),
                Bars = bars.ToArray(), Filter = filter
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

        private const int StaffStatuses = 5, StaffSorts = 6;

        // Two people in the order of a column of the old table: the name, the business, the pay (highest first), the standing (lowest first, who needs a word), the skill (highest first).
        private static int StaffOrder(StaffFile a, StaffFile b, int by, CompanyDirector d)
        {
            switch (by)
            {
                case 1: return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                case 2: return string.Compare(d.ShopName(a.Place), d.ShopName(b.Place), StringComparison.OrdinalIgnoreCase);
                case 3: return b.Paid.CompareTo(a.Paid);
                case 4: return a.Standing.CompareTo(b.Standing);
                default: return b.Skill.CompareTo(a.Skill);
            }
        }

        // The staff's own page: how many, who is underpaid and who stands low, the answer for all the raises asked at once, and the sick pay policy.
        private CompanyCard StaffSummary(CompanyDirector d, CompanyState s, int under, int low, Words w)
        {
            var c = new CompanyCard { Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = w.T("reallife_co_staff_title", "Your staff"), Icon = UiIcon.People };
            var labels = new List<string> { w.T("reallife_co_f_staff", "Staff") };
            var values = new List<string> { s.Staff.Count.ToString() };
            if (under > 0)
            {
                labels.Add(w.T("reallife_co_f_underpaid", "Underpaid"));
                values.Add(under.ToString());
            }
            if (low > 0)
            {
                labels.Add(w.T("reallife_co_f_low", "Standing low"));
                values.Add(low.ToString());
            }
            c.FactLabels = labels.ToArray();
            c.FactValues = values.ToArray();
            var blocks = new List<CompanyBlock>(12);
            bool few = s.Staff.Count > 0 && s.Staff.Count <= 4;
            if (few)
            {
                // A crew this small is shown whole: each person, how they are paid and how they feel.
                blocks.Add(Sec(w.T("reallife_co_staff_people", "Your people")));
                for (int i = 0; i < s.Staff.Count; i++)
                {
                    StaffFile p = s.Staff[i];
                    Face(p.Name, p.Id);
                    blocks.Add(new CompanyBlock
                    {
                        Kind = CompanyBlockKind.Items, Items = new[]
                        {
                            new CompanyItem
                            {
                                Title = p.Name, Sub = d.ShopName(p.Place), Face = true, Hue = UiColour.HueOf(p.Name), Press = true, Grade = -1,
                                Ring = p.AskDay > 0 || p.PoachDay > 0 || p.LeaveDay > 0 ? UiRole.Warn : StaffRules.StandsLow(p) ? UiRole.Danger : UiRole.Ok,
                                Figure = w.PerHour(p.Paid), Note = w.F("reallife_co_going", "going {wage}", "wage", CompanyText.Wage(p.Going)),
                                Act = CompanyAct.Of(CompanyDo.Go, "emp:" + p.Id, CompanyView.Staff)
                            }
                        }
                    });
                    blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_standing", "Standing"), Value = p.Standing.ToString(), Number = p.Standing,
                        Tone = StaffRules.StandsLow(p) ? UiRole.Danger : p.Standing < 60 ? UiRole.Warn : UiRole.Ok });
                    blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_mood", "Mood"), Value = p.Mood.ToString(), Number = p.Mood,
                        Tone = p.Mood < 30 ? UiRole.Danger : p.Mood < 60 ? UiRole.Warn : UiRole.Ok });
                }
            }
            int perHour, asks = d.RaisesWaiting(out perHour);
            if (asks > 1)
            {
                blocks.Add(Sec(w.T("reallife_co_staff_asks_sec", "Asking for a raise")));
                blocks.Add(Note(CompanyText.T("reallife_co_staff_asks", "count", asks.ToString(), "amount", CompanyText.Wage(perHour)), UiRole.WarnText));
                blocks.Add(Buttons(
                    Btn(CompanyText.T("reallife_co_staff_agree_all"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.RaisesAll, null, 0), UiAction.Alt, UiIcon.Check),
                    Btn(CompanyText.T("reallife_co_staff_refuse_all"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RaisesAll, null, 1), UiAction.Count, UiIcon.Close, true)));
                var asking = new List<CompanyItem>(5);
                for (int i = 0; i < s.Staff.Count && asking.Count < 5; i++)
                {
                    StaffFile p = s.Staff[i];
                    if (p.AskDay <= 0)
                    {
                        continue;
                    }
                    Face(p.Name, p.Id);
                    asking.Add(new CompanyItem
                    {
                        Title = p.Name, Sub = d.ShopName(p.Place), Face = true, Hue = UiColour.HueOf(p.Name), Ring = UiRole.Warn, Press = true, Grade = -1, Figure = w.PerHour(p.AskTo),
                        Note = w.F("reallife_co_staff_answer_by", "answer by day {day}", "day", (p.AskDay + CompanyCatalogue.RaiseAnswerDays).ToString()),
                        Act = CompanyAct.Of(CompanyDo.Go, "emp:" + p.Id, CompanyView.Staff)
                    });
                }
                if (asking.Count > 0)
                {
                    blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = asking.ToArray() });
                }
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
            if (!few)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_staff_picked")));
            }
            c.Blocks = blocks.ToArray();
            return c;
        }

        // A member of staff: who they are and what they earn, what they ask, an offer from a rival, and how they feel.
        private CompanyCard EmployeeCard(CompanyDirector d, StaffFile p, Words w)
        {
            string skill = CompanyGame.SkillName(p.Id);
            var c = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = p.Name, Sub = d.ShopName(p.Place), Face = true, Hue = UiColour.HueOf(p.Name),
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
                blocks.Add(Note(CompanyText.T("reallife_co_emp_ask_choices", "all", CompanyText.Wage(StaffRules.RaiseFor(p, RaiseAnswer.Agree)), "less", CompanyText.Wage(StaffRules.RaiseFor(p,
                    RaiseAnswer.Less)))));
                blocks.Add(Buttons(
                    Btn(CompanyText.T("reallife_co_emp_agree"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.RaiseAnswer, p.Id, (int)RaiseAnswer.Agree), UiAction.Alt, UiIcon.Check),
                    Btn(CompanyText.T("reallife_co_emp_less"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RaiseAnswer, p.Id, (int)RaiseAnswer.Less), UiAction.Option),
                    Btn(w.F("reallife_co_emp_bonus", "Bonus {amount}", "amount", w.Money(d.BonusOf(p.Place))), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RaiseAnswer, p.Id,
                        (int)RaiseAnswer.Bonus)),
                    Btn(CompanyText.T("reallife_co_emp_refuse"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RaiseAnswer, p.Id, (int)RaiseAnswer.Refuse), UiAction.Count, UiIcon.Close, true)));
            }
            else if (p.PoachDay > 0)
            {
                blocks.Add(Sec(CompanyText.T("reallife_co_emp_offer")));
                blocks.Add(Note(CompanyText.T("reallife_co_emp_offer_what", "rival", p.Rival, "wage", CompanyText.Wage(p.PoachOffer), "day", (p.PoachDay
                    + CompanyCatalogue.PoachAnswerDays).ToString())));
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
            int city = p.Wage - p.Base;
            if (city != 0 || p.Raise > 0)
            {
                blocks.Add(Sec(CompanyText.T("reallife_ibox_sec_pay")));
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
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            blocks.Add(Sec(CompanyText.T("reallife_ibox_sec_feeling")));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_standing", "Standing"), Value = p.Standing.ToString(), Number = p.Standing,
                Tone = StaffRules.StandsLow(p) ? UiRole.Danger : p.Standing < 60 ? UiRole.Warn : UiRole.Ok });
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_mood", "Mood"), Value = p.Mood.ToString(), Number = p.Mood, Tone = p.Mood < 30 ? UiRole.Danger
                : p.Mood < 60 ? UiRole.Warn : UiRole.Ok });
            if (p.LateDays > 0)
            {
                blocks.Add(Row(w.T("reallife_co_emp_late", "Short days running"), p.LateDays.ToString(), UiRole.WarnText));
            }
            blocks.Add(Sec(w.T("reallife_co_emp_work_sec", "Their work")));
            blocks.Add(Link(w.T("reallife_co_business", "Business"), d.ShopName(p.Place), CompanyAct.Of(CompanyDo.Go, p.Place, CompanyView.Overview, CompanyView.SubBusiness)));
            blocks.Add(Row(CompanyText.T("reallife_tab_co_col_skill"), skill));
            if (p.DayHours > 0)
            {
                blocks.Add(Row(w.T("reallife_co_emp_hours_day", "Hours a day"), w.F("reallife_rota_hours_n", "{hours} h", "hours", (p.DayHours / 100).ToString())));
                blocks.Add(Row(CompanyText.T("reallife_tab_co_col_day_pay"), CompanyText.Money(StaffRules.Pay(p.Paid, p.DayHours))));
            }
            ManagerState ms = d.ManagerData;
            int shown = 0;
            for (int i = ms != null ? ms.Decisions.Count - 1 : -1; i >= 0 && shown < 3; i--)
            {
                Decision dec = ms.Decisions[i];
                if (dec.Task == (int)ManagerTask.Raises && dec.Subject == p.Id && dec.A > 0 && dec.State != DecisionState.Undone)
                {
                    if (shown == 0)
                    {
                        blocks.Add(Sec(w.T("reallife_co_emp_raises_sec", "Recent raises")));
                    }
                    blocks.Add(Row(CompanyText.Day(dec.Day), "+" + CompanyText.Wage(dec.A), UiRole.OkText));
                    shown++;
                }
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
                Note(CompanyText.T(file.Striking(day) ? "reallife_co_strike_on" : "reallife_co_strike_called", "from", file.StrikeFrom.ToString(), "end", file.StrikeEnd.ToString()),
                    UiRole.DangerText),
                Note(CompanyText.T("reallife_co_strike_effect", "percent", (-CompanyCatalogue.StrikeFootfall).ToString(), "hours", CompanyCatalogue.StrikeClosedHours.ToString())),
                Note(CompanyText.T("reallife_co_strike_why", "low", shop.Low.ToString(), "count", shop.Staff.ToString())),
                Sec(w.T("reallife_co_strike_low_sec", "Who stands low")),
                new CompanyBlock { Kind = CompanyBlockKind.Items, Items = CrewItems(d, place, w, true, 6) },
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

        // The people of a place as rows that open their page: those who stand low (lowest first, with their standing as a bar), or the crew with their hours a day and their pay.
        private IEnumerable<StaffFile> CrewList(CompanyDirector d, string place, bool low, int most)
        {
            var list = new List<StaffFile>(most);
            IReadOnlyList<StaffFile> all = d.State.Staff;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Place == place && (!low || StaffRules.StandsLow(all[i])))
                {
                    list.Add(all[i]);
                }
            }
            if (low)
            {
                list.Sort((a, b) => a.Standing.CompareTo(b.Standing));
            }
            for (int i = 0; i < list.Count && i < most; i++)
            {
                yield return list[i];
            }
        }

        private CompanyItem[] CrewItems(CompanyDirector d, string place, Words w, bool low, int most)
        {
            var items = new List<CompanyItem>(most);
            foreach (StaffFile p in CrewList(d, place, low, most))
            {
                Face(p.Name, p.Id);
                items.Add(new CompanyItem
                {
                    Title = p.Name, Sub = CompanyGame.SkillName(p.Id), Face = true, Hue = UiColour.HueOf(p.Name), Ring = low || StaffRules.StandsLow(p) ? UiRole.Danger : UiRole.Ok, Press = true,
                    Figure = low ? p.Standing.ToString() : w.F("reallife_rota_hours_n", "{hours} h", "hours", p.DayHours.ToString()), Note = low ? null : w.PerHour(p.Paid), Grade = low ? p.Standing
                        : -1,
                    Act = CompanyAct.Of(CompanyDo.Go, "emp:" + p.Id, CompanyView.Staff)
                });
            }
            return items.ToArray();
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
                Sec(w.T("reallife_co_staff_people", "Your people")),
                new CompanyBlock { Kind = CompanyBlockKind.Items, Items = CrewItems(d, place, w, false, 6) },
                Buttons(
                    Btn(CompanyText.T("reallife_co_overtime_agree"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.OvertimeAnswer, place, 1), UiAction.Alt, UiIcon.Check),
                    Btn(CompanyText.T("reallife_co_overtime_refuse"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.OvertimeAnswer, place, 0), UiAction.Count, UiIcon.Close, true))
            };
            return c;
        }
    }
}
