using System.Collections.Generic;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Staff as an economy: what the director does with <see cref="StaffRules"/>.
    ///
    /// WHAT THE GAME ALREADY HAS, AND IS LEFT TO IT: a wage per employee and the formula it comes from, satisfaction
    /// from job demands (a clean workplace, hours and days, health insurance, equipment), complaints, a bonus, training
    /// that raises the wage, sick days without pay (EmployeeHelper.RunDaily picks who is off), and its own rivals'
    /// poaching of whole teams. It has no going rate that moves, no request for a raise, no strike and no overtime.
    ///
    /// WHAT IS ADDED HERE, AND HOW: the going rate and each employee's standing are numbers of the mod's own. An
    /// agreed raise, sick pay for whoever the game has off sick, overtime and a strike's one-off payment are
    /// transactions of the mod's own (way c). A strike and overtime change footfall through business pressure's
    /// figure, and a strike shuts the doors each morning by business pressure's own closure (way a). Somebody who
    /// takes a rival's offer leaves by the game's own resignation, as business pressure has an employee leave.
    /// Nothing of the game's employee is written: no wage, no satisfaction, no schedule, no sick day.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private readonly List<StaffFile> _fit = new List<StaffFile>(8);      // who a rival would want, this morning
        private readonly List<StaffFile> _leaving = new List<StaffFile>(2);
        private int[] _raisePay = new int[16], _sickPay = new int[16];        // by business, for the day's pass

        partial void StaffDay(int day)
        {
            CompanyState s = _state;
            int yesterday = day - 1, asked = 0;
            StaffFile firstAsked = null;
            if (_raisePay.Length < _shops.Count)
            {
                _raisePay = new int[_shops.Count * 2];
                _sickPay = new int[_shops.Count * 2];
            }
            System.Array.Clear(_raisePay, 0, _raisePay.Length);
            System.Array.Clear(_sickPay, 0, _sickPay.Length);
            _fit.Clear();
            _leaving.Clear();
            for (int i = 0; i < s.Staff.Count; i++)
            {
                StaffFile f = s.Staff[i];
                int at = ShopIndex(f.Place);
                if (at < 0)
                {
                    continue;
                }
                if (f.Raise > 0 && f.Hours > 0)
                {
                    _raisePay[at] += StaffRules.Pay(f.Raise, f.Hours);
                }
                f.Hours = 0;
                // (the game picks the new day's absences after its day event, so this is still the day that has ended)
                if (f.Sick)
                {
                    _sickPay[at] += StaffRules.SickPay(f, s.SickPolicy);
                    StaffRules.SickDay(f, s.SickPolicy);
                }
                int standing = f.Standing;
                StaffNote note = StaffRules.Day(f, _level, day, CompanyBridges.ComfortAt(f.Place));
                if (note == StaffNote.Asked)
                {
                    asked++;
                    firstAsked = firstAsked ?? f;
                }
                else if (note == StaffNote.AskLapsed)
                {
                    Tell(NotificationType.Warning, "reallife_co_note_raise_lapsed", new Dictionary<string, string> { { "name", f.Name } }, TabStaff, "emp:" + f.Id);
                }
                if (Verbose && note != StaffNote.None)
                {
                    Logger?.Info("Company check (staff, " + f.Name + "): the game's wage at today's prices " + f.Wage + ", agreed raise " + f.Raise + ", the going rate " + f.Going + " (hundredths an hour), "
                                 + StaffRules.Gap(f.Going, f.Paid) + " thousandths under it for " + f.UnderDays + " day(s), strictness " + _level + ", standing " + standing + " to " + f.Standing + ": " + note + ": PASS");
                }
                if (StaffRules.OfferLapsed(f, day))
                {
                    StaffRules.OfferAnswer(f, false, day);
                    Tell(NotificationType.Warning, "reallife_co_note_offer_lapsed", new Dictionary<string, string> { { "name", f.Name }, { "rival", f.Rival }, { "day", f.LeaveDay.ToString() } }, TabStaff, "emp:" + f.Id);
                }
                if (f.LeaveDay > 0 && day >= f.LeaveDay)
                {
                    _leaving.Add(f);
                }
                else if (StaffRules.Wanted(f, _level, day))
                {
                    _fit.Add(f);
                }
            }
            for (int i = 0; i < _shops.Count; i++)
            {
                CompanyShop shop = _shops[i];
                // Wages are paid whatever the cash is, as the game pays its own: so is what was agreed on top of them.
                if (_raisePay[i] > 0)
                {
                    WagePaid(_raisePay[i], CompanyGame.RaiseKey, shop, yesterday, "the agreed raises of its staff for the hours the game counted on day " + yesterday);
                }
                if (_sickPay[i] > 0)
                {
                    WagePaid(_sickPay[i], CompanyGame.SickKey, shop, yesterday, shop.Sick + " off sick on day " + yesterday + " by the game's own absences, policy " + s.SickPolicy + " ("
                                                                           + CompanyText.Share(CompanyCatalogue.SickShare[s.SickPolicy]) + " of a day's pay)");
                }
            }
            if (asked == 1)
            {
                Tell(NotificationType.Info, "reallife_co_note_raise_one", new Dictionary<string, string> { { "name", firstAsked.Name }, { "business", ShopName(firstAsked.Place) } }, TabStaff, "emp:" + firstAsked.Id);
            }
            else if (asked > 1)
            {
                Tell(NotificationType.Info, "reallife_co_note_raise_many", new Dictionary<string, string> { { "count", asked.ToString() } }, TabStaff, null);
            }
            for (int i = 0; i < _leaving.Count; i++)
            {
                Leave(_leaving[i]);
            }
            _leaving.Clear();
            Approach(day);
            _fit.Clear();
            Recount();
            for (int i = 0; i < _shops.Count; i++)
            {
                ShopStaffDay(_shops[i], i, day);
            }
        }

        // A rival approaches one of those who fit, on a day the seed says so.
        private void Approach(int day)
        {
            CompanyState s = _state;
            if (_fit.Count == 0 || day < s.PoachRest || !StaffRules.Approach(s.Seed, s.Age, _level, CompanyBridges.EsteemNow()))
            {
                return;
            }
            StaffFile f = _fit[StaffRules.Who(s.Seed, s.Age, _fit.Count)];
            CompanyShop shop = ShopAt(f.Place);
            string rival = shop != null && CompanyBridges.Rival != null ? CompanyBridges.Rival(shop.Reg) : null;
            StaffRules.Offer(f, CompanyStore.Clean(string.IsNullOrEmpty(rival) ? CompanyText.T("reallife_co_rival_default") : rival), day);
            s.PoachRest = day + CompanyCatalogue.PoachCompanyRestDays;
            Tell(NotificationType.Warning, "reallife_co_note_offer", new Dictionary<string, string>
            {
                { "name", f.Name }, { "rival", f.Rival }, { "wage", CompanyText.Wage(f.PoachOffer) }, { "days", CompanyCatalogue.PoachAnswerDays.ToString() }
            }, TabStaff, "emp:" + f.Id);
            if (Verbose)
            {
                Logger?.Info("Company check (staff, a rival's offer): " + f.Name + " has skill " + f.Skill + " (from " + CompanyCatalogue.PoachSkill + "), is paid " + f.Paid + " against a going rate of " + f.Going
                             + ", one of " + _fit.Count + " who fit; " + f.Rival + " offers " + f.PoachOffer + ", to be answered within " + CompanyCatalogue.PoachAnswerDays + " day(s): PASS");
            }
        }

        // One business's staff for the day: overtime, and a strike that ends or is called.
        private void ShopStaffDay(CompanyShop shop, int at, int day)
        {
            ShopFile file = shop.File;
            if (file.Overtime(day - 1) && file.OvertimeCost > 0)
            {
                WagePaid(file.OvertimeCost, CompanyGame.OvertimeKey, shop, day - 1, CompanyCatalogue.OvertimeHours + " extra hours each at time and a half on day " + (day - 1));
            }
            StaffRules.OvertimeDay(file, day);
            bool busy = (file.Pushing(day) || _cycle == CycleState.Boom) && Promoted(shop);   // (where the game keeps no customer figure, extra hours would bring nobody)
            if (shop.Staff > 0 && StaffRules.OvertimeOffered(file, busy, _state.Seed, _state.Age, at, day))
            {
                file.OvertimeAsk = day;
                Tell(NotificationType.Info, "reallife_co_note_overtime", new Dictionary<string, string>
                {
                    { "business", shop.Name }, { "cost", CompanyText.Money(OvertimeCost(file.Place)) }, { "percent", CompanyCatalogue.OvertimeFootfall.ToString() }, { "days", CompanyCatalogue.OvertimeDays.ToString() }
                }, TabStaff, "ot:" + file.Place);
            }
            if (file.StrikeFrom > 0 && day >= file.StrikeEnd)
            {
                EndStrike(shop, StrikeAnswer.SitOut, day);
                Tell(NotificationType.Info, "reallife_co_note_strike_over", new Dictionary<string, string> { { "business", shop.Name } }, TabStaff, null);
            }
            else if (StaffRules.CanStrike(file, day) && StaffRules.StrikeDue(shop.Low, shop.Staff, _level) && !(CompanyBridges.Dispute != null && CompanyBridges.Dispute(shop.Reg)))
            {
                StaffRules.Call(file, _level, day);
                Tell(NotificationType.Warning, "reallife_co_note_strike_called", new Dictionary<string, string>
                {
                    { "business", shop.Name }, { "low", shop.Low.ToString() }, { "count", shop.Staff.ToString() }, { "days", (file.StrikeEnd - file.StrikeFrom).ToString() }
                }, TabStaff, "strike:" + file.Place);
                if (Verbose)
                {
                    Logger?.Info("Company check (staff, a strike at " + shop.Name + "): " + shop.Low + " of " + shop.Staff + " stand at " + CompanyCatalogue.StrikeStanding + " or lower (" + CompanyText.Share(CompanyCatalogue.StrikeShare)
                                 + " needed), strictness " + _level + ": called for day " + file.StrikeFrom + ", back on day " + file.StrikeEnd + ": PASS");
                }
            }
        }

        // The morning of a strike day: the doors are shut for some hours by business pressure's own closure, which reopens them itself.
        partial void StaffHour(int day, int hour)
        {
            if (hour != CompanyCatalogue.StrikeHour)
            {
                return;
            }
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].File.Striking(day))
                {
                    bool shut = CompanyBridges.Close != null && CompanyBridges.Close(_shops[i].Reg, CompanyCatalogue.StrikeClosedHours);
                    if (Verbose)
                    {
                        Logger?.Info("Company check (staff, a strike at " + _shops[i].Name + "): " + (shut ? "shut for " + CompanyCatalogue.StrikeClosedHours + " hours by business pressure's own closure, which reopens it"
                                         : "not shut (business pressure is off, or it was closed already)") + "; " + CompanyCatalogue.StrikeFootfall + "% of customers through its footfall figure: PASS");
                    }
                }
            }
        }

        // Relaxed has no strikes and no poaching: what is called or waiting is taken back when the strictness is set there.
        partial void StaffLevel()
        {
            if (_state == null || CompanyCatalogue.StrikeDays[_level] > 0)
            {
                return;
            }
            for (int i = 0; i < _state.Shops.Count; i++)
            {
                _state.Shops[i].StrikeFrom = _state.Shops[i].StrikeEnd = 0;
            }
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                _state.Staff[i].PoachDay = _state.Staff[i].PoachOffer = 0;
            }
        }

        /// <summary>
        /// The owner has walked in (the player's standing, through Wiring/StandingLinks): each of the business's staff's
        /// standing moved by so many points, proud or nervous, within its bounds. Returns how many it moved. The standing is
        /// the company's own number; nothing of the game's employee is written.
        /// </summary>
        public int Visited(string place, int points)
        {
            if (!On(CompanyPart.Staff) || _state == null || points == 0 || place == null)
            {
                return 0;
            }
            int moved = 0;
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffFile f = _state.Staff[i];
                if (f.Place == place)
                {
                    f.Standing = CompanyCatalogue.Clamp(f.Standing + points, 0, CompanyCatalogue.StandingMost);
                    moved++;
                }
            }
            if (moved > 0)
            {
                Recount();
            }
            return moved;
        }

        private void WagePaid(int amount, string key, CompanyShop shop, int? day, string how)
        {
            Cost(amount, key, shop, shop.Name, CostKind.Wages, day, how, true);
        }

        // They go, by the game's own resignation, and their file with them. With nothing to have them leave by, they stay.
        private void Leave(StaffFile f)
        {
            EmployeeInstance e = CompanyGame.Employee(f.Id);
            if (e == null || CompanyBridges.Leave == null)
            {
                f.LeaveDay = 0;
                Logger?.Info("Company: " + f.Name + " was to leave today and stays: " + (e == null ? "the game no longer has them." : "there is nothing to have them leave by."));
                return;
            }
            CompanyBridges.Leave(e);
            _state.Staff.Remove(f);
            Tell(NotificationType.Warning, "reallife_co_note_left", new Dictionary<string, string> { { "name", f.Name }, { "business", ShopName(f.Place) } }, TabStaff, null);
            Logger?.Info("Company: " + f.Name + " has left " + ShopName(f.Place) + " for a rival by the game's own resignation; the agreed raise of " + f.Raise + " went with them.");
        }

        private void EndStrike(CompanyShop shop, StrikeAnswer how, int day)
        {
            List<StaffFile> staff = _state.Staff;
            for (int i = 0; i < staff.Count; i++)
            {
                if (staff[i].Place == shop.File.Place)
                {
                    StaffRules.After(staff[i], how);
                }
            }
            StaffRules.End(shop.File, day);
            Recount();
            if (Verbose)
            {
                Logger?.Info("Company check (staff, the strike at " + shop.Name + "): ended by " + how + " on day " + day + ", " + shop.Staff + " employee(s), " + shop.Low + " still stand low, no new strike before day "
                             + shop.File.StrikeRest + ": PASS");
            }
        }

        // ---------------------------------------------------------------- what the app asks

        /// <summary>What a day of overtime costs at a business: the extra hours of every employee there at time and a half.</summary>
        public int OvertimeCost(string place)
        {
            int cost = 0;
            for (int i = 0; _state != null && i < _state.Staff.Count; i++)
            {
                cost += _state.Staff[i].Place == place ? StaffRules.OvertimePay(_state.Staff[i]) : 0;
            }
            return cost;
        }

        /// <summary>What ending a strike costs: the one-off payment in all, or what a raise for all adds to an hour's wages there, in hundredths.</summary>
        public int StrikeCost(string place, StrikeAnswer how)
        {
            int cost = 0;
            for (int i = 0; _state != null && i < _state.Staff.Count; i++)
            {
                StaffFile f = _state.Staff[i];
                cost += f.Place != place ? 0 : how == StrikeAnswer.Payment ? StaffRules.StrikePayment(f) : StaffRules.StrikeRaise(f);
            }
            return cost;
        }

        /// <summary>
        /// A raise to the going rate that nobody asked for: the request the rule would make, agreed at once. Not while the
        /// last answer's rest runs (<see cref="StaffRules.CanGive"/>): one raise for one employee in that time, as their own
        /// requests come.
        /// </summary>
        public void GiveRaise(string id)
        {
            StaffFile f = On(CompanyPart.Staff) ? _state.Employee(id) : null;
            if (f == null || !StaffRules.CanGive(f, Save.Day))
            {
                return;
            }
            f.AskDay = Save.Day;
            f.AskTo = f.Going;
            AnswerRaise(id, RaiseAnswer.Agree);
        }

        public void AnswerRaise(string id, RaiseAnswer answer)
        {
            StaffFile f = On(CompanyPart.Staff) ? _state.Employee(id) : null;
            if (f == null || f.AskDay == 0)
            {
                return;
            }
            int raise = StaffRules.RaiseFor(f, answer), standing = f.Standing;
            StaffRules.Answer(f, answer, Save.Day);
            Logger?.Info("Company: " + f.Name + "'s request for " + f.Going + " an hour (hundredths) answered: " + answer + ". Agreed raise now " + f.Raise + " (" + raise + " more), standing " + standing + " to " + f.Standing
                         + "; the game's wage of " + f.Wage + " is not changed.");
            Notifications.Show(answer == RaiseAnswer.Refuse ? NotificationType.Warning : NotificationType.Success,
                answer == RaiseAnswer.Agree ? "reallife_co_note_raise_agreed" : answer == RaiseAnswer.Less ? "reallife_co_note_raise_less" : "reallife_co_note_raise_refused",
                new Dictionary<string, string> { { "name", f.Name }, { "raise", CompanyText.Wage(raise) }, { "standing", f.Standing.ToString() } }, 8f, null, null, true, false);
            Recount();
            Changed();
        }

        /// <summary>How many requests for a raise wait, and what agreeing to all of them would add to an hour's wages, in hundredths.</summary>
        public int RaisesWaiting(out int perHour)
        {
            int waiting = 0;
            perHour = 0;
            for (int i = 0; _state != null && i < _state.Staff.Count; i++)
            {
                if (_state.Staff[i].AskDay > 0)
                {
                    waiting++;
                    perHour += StaffRules.RaiseFor(_state.Staff[i], RaiseAnswer.Agree);
                }
            }
            return waiting;
        }

        /// <summary>Every request that waits is given the same answer, each by the rule that answers one.</summary>
        public void AnswerAllRaises(RaiseAnswer answer)
        {
            if (!On(CompanyPart.Staff))
            {
                return;
            }
            int count = 0, raise = 0;
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffFile f = _state.Staff[i];
                if (f.AskDay > 0)
                {
                    raise += StaffRules.RaiseFor(f, answer);
                    StaffRules.Answer(f, answer, Save.Day);
                    count++;
                }
            }
            if (count == 0)
            {
                return;
            }
            Logger?.Info("Company: " + count + " request(s) for a raise answered together: " + answer + ", " + raise + " more an hour in all (hundredths); no wage of the game's is changed.");
            Notifications.Show(answer == RaiseAnswer.Refuse ? NotificationType.Warning : NotificationType.Success, answer == RaiseAnswer.Refuse ? "reallife_co_note_raise_all_refused" : "reallife_co_note_raise_all_agreed",
                new Dictionary<string, string> { { "count", count.ToString() }, { "raise", CompanyText.Wage(raise) } }, 8f, null, null, true, false);
            Recount();
            Changed();
        }

        public void AnswerOffer(string id, bool match)
        {
            StaffFile f = On(CompanyPart.Staff) ? _state.Employee(id) : null;
            if (f == null || f.PoachDay == 0)
            {
                return;
            }
            int offer = f.PoachOffer;
            StaffRules.OfferAnswer(f, match, Save.Day);
            Logger?.Info("Company: " + f.Rival + "'s offer of " + offer + " an hour (hundredths) to " + f.Name + " was " + (match ? "matched: agreed raise now " + f.Raise : "not matched: they leave on day " + f.LeaveDay) + ".");
            Notifications.Show(match ? NotificationType.Success : NotificationType.Warning, match ? "reallife_co_note_offer_matched" : "reallife_co_note_offer_declined",
                new Dictionary<string, string> { { "name", f.Name }, { "rival", f.Rival }, { "wage", CompanyText.Wage(offer) }, { "day", f.LeaveDay.ToString() } }, 8f, null, null, true, false);
            Changed();
        }

        /// <summary>A strike is settled by a raise for all or a one-off payment. (Sitting it out needs no call: it runs its length.)</summary>
        public void SettleStrike(string place, StrikeAnswer how)
        {
            CompanyShop shop = On(CompanyPart.Staff) ? ShopAt(place) : null;
            if (shop == null || shop.File.StrikeFrom == 0 || how == StrikeAnswer.SitOut)
            {
                return;
            }
            int cost = StrikeCost(place, how);
            if (how == StrikeAnswer.Payment && !Cost(cost, CompanyGame.StrikeKey, shop, shop.Name, CostKind.Wages, null, CompanyCatalogue.StrikePayHours + " hours' pay to each of " + shop.Staff + " employee(s)"))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            EndStrike(shop, how, Save.Day);
            Refoot(shop);
            Notifications.Show(NotificationType.Success, how == StrikeAnswer.Raise ? "reallife_co_note_strike_raise" : "reallife_co_note_strike_paid",
                new Dictionary<string, string> { { "business", shop.Name }, { "amount", how == StrikeAnswer.Raise ? CompanyText.Wage(cost) : CompanyText.Money(cost) } }, 9f, null, null, true, false);
            Changed();
        }

        public void AnswerOvertime(string place, bool agree)
        {
            CompanyShop shop = On(CompanyPart.Staff) ? ShopAt(place) : null;
            if (shop == null || shop.File.OvertimeAsk == 0)
            {
                return;
            }
            int cost = OvertimeCost(place);
            StaffRules.OvertimeAnswer(shop.File, agree, cost, Save.Day);
            for (int i = 0; agree && i < _state.Staff.Count; i++)
            {
                if (_state.Staff[i].Place == place)
                {
                    _state.Staff[i].Standing = CompanyCatalogue.Clamp(_state.Staff[i].Standing + CompanyCatalogue.OvertimeStanding, 0, CompanyCatalogue.StandingMost);
                }
            }
            Logger?.Info("Company: overtime at " + shop.Name + " " + (agree ? "agreed: " + cost + " a day for " + CompanyCatalogue.OvertimeDays + " day(s), +" + CompanyCatalogue.OvertimeFootfall + "% of customers until day "
                                                                               + shop.File.OvertimeEnd : "refused") + ".");
            Refoot(shop);
            Recount();
            Changed();
        }

        public void SetSickPolicy(int policy)
        {
            if (!On(CompanyPart.Staff))
            {
                return;
            }
            _state.SickPolicy = CompanyCatalogue.Clamp(policy, 0, CompanyCatalogue.SickShare.Length - 1);
            Logger?.Info("Company: sick pay policy set to " + _state.SickPolicy + " (" + CompanyText.Share(CompanyCatalogue.SickShare[_state.SickPolicy]) + " of a day's pay).");
            Changed();
        }

        /// <summary>The businesses whose strike starts on a day, for the morning paper.</summary>
        public void StrikesFrom(int day, List<string> names)
        {
            for (int i = 0; On(CompanyPart.Staff) && i < _shops.Count; i++)
            {
                if (_shops[i].File.StrikeFrom == day)
                {
                    names.Add(_shops[i].Name);
                }
            }
        }

        partial void StaffAlerts(int day)
        {
            int asks = 0;
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffFile f = _state.Staff[i];
                asks += f.AskDay > 0 ? 1 : 0;
                if (f.PoachDay > 0)
                {
                    Alert(9, PlaceNumber(f.Id), f.PoachDay + CompanyCatalogue.PoachAnswerDays, CompanyText.T("reallife_co_plan_offer_title", "name", f.Name),
                        CompanyText.T("reallife_co_plan_offer_what", "rival", f.Rival, "wage", CompanyText.Wage(f.PoachOffer)), null, CompanyText.T("reallife_co_plan_offer_missed"));
                }
            }
            if (asks > 0)
            {
                Alert(6, 0, day, CompanyText.T("reallife_co_plan_raise_title"), CompanyText.T("reallife_co_plan_raise_what", "count", asks.ToString()), null, CompanyText.T("reallife_co_plan_raise_missed"));
            }
            for (int i = 0; i < _shops.Count; i++)
            {
                ShopFile file = _shops[i].File;
                if (file.StrikeCalled(day))
                {
                    Alert(7, PlaceNumber(file.Place), file.StrikeFrom, CompanyText.T("reallife_co_plan_strike_title", "business", _shops[i].Name),
                        CompanyText.T("reallife_co_plan_strike_what", "days", (file.StrikeEnd - file.StrikeFrom).ToString()), null, CompanyText.T("reallife_co_plan_strike_missed"));
                }
                if (file.OvertimeAsk > 0)
                {
                    Alert(10, PlaceNumber(file.Place), file.OvertimeAsk + CompanyCatalogue.OvertimeAnswerDays, CompanyText.T("reallife_co_plan_overtime_title", "business", _shops[i].Name),
                        CompanyText.T("reallife_co_plan_overtime_what"), CompanyText.T("reallife_planner_costs", "amount", CompanyText.Money(OvertimeCost(file.Place))), null);
                }
            }
        }
    }
}
