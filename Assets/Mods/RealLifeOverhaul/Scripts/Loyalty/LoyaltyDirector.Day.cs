using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Loyalty
{
    /// <summary>The schemes' day and hour: yesterday lived and booked, the player's choices in force, today's lift written, the hours counted and the baskets given.</summary>
    public sealed partial class LoyaltyDirector
    {
        private static readonly int HotDay = HotSpots.Slot("Loyalty: the day's turn");
        private static readonly int HotHour = HotSpots.Slot("Loyalty: the hour");

        // GlobalEvents.onNewDay: after the game's daily run, so yesterday's books of every business are there, and before its tax
        // and daily summary read yesterday's statement: the rewards are booked into it here.
        private void OnDay()
        {
            if (!Running || !LoyaltyGame.Ready)
            {
                return;
            }
            try
            {
                Pass(LoyaltyGame.Day);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // The day's turn, once a day whoever asks first (the game's day, or the second's look after a load). Nothing but a
        // comparison while no scheme is kept.
        private void Pass(int day)
        {
            if (_passDay == day)
            {
                return;
            }
            _passDay = day;
            _due.Clear();
            if (_state.Files.Count == 0)
            {
                _byReg.Clear();
                return;
            }
            using var hotSpot = HotSpots.Time(HotDay);
            Map();
            int level = LoyaltyGame.Level, lived = 0, members = 0;
            long rewards = 0, costs = 0;
            bool sound = true, booked = false;
            foreach (KeyValuePair<BuildingRegistration, LoyaltyFile> pair in _byReg)
            {
                BuildingRegistration reg = pair.Key;
                LoyaltyFile f = pair.Value;
                if (f.On || f.Want)
                {
                    ReadRivals(reg, f, day);
                }
                LoyaltyGame.Average(reg, out f.MarginMille);
                if (f.LastDay >= day)
                {
                    continue;   // worked already (a save made after this morning's turn)
                }
                if (f.LastDay == day - 1)
                {
                    bool ran = f.On;
                    LoyaltyInput yesterday = LoyaltyGame.Yesterday(reg, day - 1);
                    LoyaltyResult r = LoyaltyRules.Day(f, yesterday, level);
                    long given = (r.Rewards + 99) / 100, upkeep = (r.Costs + 99) / 100;   // whole money, as the game books a business's costs
                    sound &= r.Rewards >= 0 && r.Rewards <= Math.Max(0L, yesterday.Takings) * LoyaltyRules.RewardMost / 1000;
                    if (ran)
                    {
                        // given and run up yesterday: they are owed whatever the cash is (the scheme stops below if there is none)
                        LoyaltyGame.Charge(reg, given, LoyaltyGame.RewardsKey, day - 1, true);
                        LoyaltyGame.Charge(reg, upkeep, LoyaltyGame.CostsKey, day - 1, true);
                        f.Paid += given + upkeep;
                        rewards += given;
                        costs += upkeep;
                        booked |= given + upkeep > 0;
                        lived++;
                    }
                }
                else
                {
                    f.OpenHours = f.BadHours = f.Added = 0;   // days went by without the feature: nothing is booked for them
                }
                if (LoyaltyRules.Turn(f, day))
                {
                    int print = Prices.At(LoyaltyRules.ChangeCost());
                    LoyaltyGame.Charge(reg, print, LoyaltyGame.ChangeKey, null, true);
                    f.Paid += print;
                }
                if (f.On && LoyaltyGame.Broke)
                {
                    f.On = f.Want = false;   // nothing left to give rewards from: the scheme stops, as the game stops a campaign it cannot pay
                    LoyaltyRules.Today(f, day);
                    LoyaltyGame.Tell(NotificationType.Warning, "reallife_loyalty_note_paused", LoyaltyGame.Data("business", LoyaltyGame.Name(reg)));
                    Logger?.Info("Loyalty: the scheme at " + LoyaltyGame.Name(reg) + " stopped: the cash is under nothing.");
                }
                sound &= f.Lift >= 0 && f.Lift <= LoyaltyRules.LiftMost && f.Members <= LoyaltyRules.Cap(f.Base, f.Sisters);
                members += f.Members / 100;
            }
            WriteFigures(true);
            if (booked)
            {
                LoyaltyGame.Refresh(day - 1);   // the game made yesterday's statement before these lines were booked
            }
            _revision++;
            LoyaltyTab.Stale();
            if (Verbose && _byReg.Count > 0)
            {
                Logger?.Info("Loyalty check (day " + day + "): " + _byReg.Count + " scheme(s), " + lived + " lived yesterday, " + members + " member(s) in all, " + rewards
                             + " of rewards and " + costs + " of card costs booked into yesterday's statement; every lift within 0 to " + LoyaltyRules.LiftMost
                             + "%, every day's rewards within " + LoyaltyRules.RewardMost / 10 + "% of its takings, no shop over its cap: " + (sound ? "PASS" : "FAIL"));
            }
            HourNow(day, LoyaltyGame.Hour);   // the new day's first hour came before the game's daily run: given now, on the day's own customers
        }

        // Every scheme's lift written into the game's figure where it moved, in one batch; a new day's is written whenever there is one.
        private void WriteFigures(bool newDay)
        {
            CustomerDraw.Hold(newDay ? "the loyalty cards' day" : "the loyalty cards' figures again");
            try
            {
                foreach (KeyValuePair<BuildingRegistration, LoyaltyFile> pair in _byReg)
                {
                    LoyaltyFile f = pair.Value;
                    int lift = f.On ? f.Lift : 0;
                    if (lift == f.Written && !(newDay && lift != 0))
                    {
                        continue;
                    }
                    f.Written = lift;   // before the write: FootfallOf is asked during it
                    WriteFigure(pair.Key, newDay);
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
        }

        // GlobalEvents.onNewHour: every game hour, and first thing in the game's new day, before its daily run.
        private void OnHour()
        {
            if (!Running || !LoyaltyGame.Ready || _byReg.Count == 0)
            {
                return;
            }
            try
            {
                int day = LoyaltyGame.Day;
                if (_passDay == day)
                {
                    HourNow(day, LoyaltyGame.Hour);   // otherwise the day's turn gives this hour, once the game has drawn the day's customers
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // The hour under way at each running scheme: an opening hour that can serve nobody is counted, and members' baskets take a
        // product more (after the redraw, where the business waits for one).
        private void HourNow(int day, int hour)
        {
            if (_byReg.Count == 0)
            {
                return;
            }
            using var hotSpot = HotSpots.Time(HotHour);
            float soonest = LoyaltyGame.Minute + LoyaltyGame.BasketLead;
            foreach (KeyValuePair<BuildingRegistration, LoyaltyFile> pair in _byReg)
            {
                LoyaltyFile f = pair.Value;
                if (!f.On || f.LastDay != day || !LoyaltyGame.IsShop(pair.Key))
                {
                    continue;
                }
                LoyaltyGame.HourOf(pair.Key, hour, out bool open, out bool bad);
                f.OpenHours += open ? 1 : 0;
                f.BadHours += bad ? 1 : 0;
                if (f.Basket <= 0)
                {
                    continue;
                }
                if (CustomerDraw.Waits(pair.Key))
                {
                    _due[pair.Key] = hour;   // about to be drawn again: the basket goes on the new customers (Redrawn, or failing it Due)
                    continue;
                }
                f.Added += LoyaltyGame.Basket(pair.Key, f.Basket, day, hour, soonest);
            }
        }

        // What waited for a redraw that was not made after all, given now within the hour it fell due in.
        private void Due(int day, int hour)
        {
            _scratch.Clear();
            foreach (KeyValuePair<BuildingRegistration, int> due in _due)
            {
                if (!CustomerDraw.Waits(due.Key))
                {
                    _scratch.Add(due.Key);
                }
            }
            for (int i = 0; i < _scratch.Count; i++)
            {
                Give(_scratch[i], day, hour);
            }
            _scratch.Clear();
        }

        // A redraw made a business's list of customers anew: a waiting basket goes on the new customers.
        private void Redrawn(BuildingRegistration reg)
        {
            if (_broken || _due.Count == 0 || reg == null || !_due.ContainsKey(reg))
            {
                return;
            }
            try
            {
                Give(reg, LoyaltyGame.Day, LoyaltyGame.Hour);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Give(BuildingRegistration reg, int day, int hour)
        {
            int dueHour = _due[reg];
            _due.Remove(reg);
            if (dueHour == hour && Running && _byReg.TryGetValue(reg, out LoyaltyFile f) && f.On && f.Basket > 0 && f.LastDay == day)
            {
                f.Added += LoyaltyGame.Basket(reg, f.Basket, day, hour, LoyaltyGame.Minute + LoyaltyGame.BasketLead);
            }
        }
    }
}
