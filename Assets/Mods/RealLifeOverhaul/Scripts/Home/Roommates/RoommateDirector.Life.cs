using System;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>
    /// The roommate's days and hours, and what the player's replies do. Everything here runs on the hour or the day
    /// that the director's look found changed, or on a button; see RoommateDirector for the cost.
    /// </summary>
    public sealed partial class RoommateDirector
    {
        // ---------------------------------------------------------------- the hour and the day

        // The game's hour has changed since the last look: every day that went by gets its day, then the hour's work is done.
        private void Rounds(int stamp)
        {
            RoommateState s = _state;
            int day = stamp / 24, hour = stamp % 24;
            if (day != s.LastDay)
            {
                bool inside = s.Living && RoommateGame.Inside(s.HomeStreet, s.HomeNumber);
                for (int d = Math.Max(s.LastDay + 1, day - CatchUpDays + 1); d <= day; d++)
                {
                    Daily(d, d == day && inside);
                }
                s.LastDay = day;
            }
            s.LastStamp = stamp;
            Hourly(day, hour, stamp);
        }

        private void Hourly(int day, int hour, int stamp)
        {
            RoommateState s = _state;
            if (s.AdUp)
            {
                for (int i = 0; i < s.Candidates.Count; i++)
                {
                    Flatmate c = s.Candidates[i];
                    if (!c.Told && stamp >= c.Answers)
                    {
                        c.Told = true;
                        RoommateText.Message("reallife_text_roommate_answer", Fill.For(c.Person.Name).With(RoommateText.Job(c.Person)));
                    }
                }
                if (stamp >= s.AdStamp + RoommateRules.AdDays * 24)
                {
                    s.ClearAd();
                    RoommateText.Message("reallife_text_roommate_ad_over", default);
                    BuildAlerts();
                }
            }
            for (int n = 0; n < 3 && s.Living && (day > s.RentDay || (day == s.RentDay && hour >= RoommateRules.RentHour)); n++)
            {
                RentDue(s.RentDay, false);
            }
        }

        // A rent day: their share for the week that ended, through the game's own statement; or less, or nothing yet.
        private void RentDue(int rentDay, bool forceShort)
        {
            RoommateState s = _state;
            Flatmate m = s.Mate;
            BuildingRegistration home = RoommateGame.HomeAt(s.HomeStreet, s.HomeNumber);
            float perDay = RoommateGame.RentPerDay(home, out bool owned);
            s.Lodger = owned;
            s.Weekly = RoommateRules.Weekly(perDay, m.SharePct);
            if (s.Weekly <= 0)
            {
                // The home has no rent to share just now (the game says it would let for nothing): the week goes by unpaid and unowed.
                s.LastRentDay = rentDay;
                s.RentDay = rentDay + RoommateRules.RentEveryDays;
                return;
            }
            RentResult r = RoommateRules.Rent(m.Person.Seed, rentDay, m.Traits, Level, s.Weekly);
            if (forceShort)
            {
                r.Outcome = RentOutcome.Short;
                r.Paid = s.Weekly * 60 / 100;
                r.Owed = s.Weekly - r.Paid;
                r.DueInDays = 2;
            }
            RoommateGame.Receive(r.Paid, owned ? LodgerKey : RentKey);
            Fill f = Fill.For(m.Person.Name).Cost(r.Paid);
            if (r.Outcome == RentOutcome.Paid)
            {
                RoommateText.Toast(NotificationType.Success, "reallife_roommate_rent_paid", f, 8f);
            }
            else
            {
                s.Owed += r.Owed;
                s.OwedDue = rentDay + r.DueInDays;
                s.OwedSlips = 0;
                s.Topics |= Topic.Rent;
                RoommateText.Message(r.Outcome == RentOutcome.Late ? "reallife_text_roommate_rent_late" : "reallife_text_roommate_rent_short",
                    Fill.For(m.Person.Name).Cost(r.Owed).On(RoommateText.When(s.OwedDue)));
                Seen(RoomTrait.LateWithRent);
            }
            s.LastRentDay = rentDay;
            s.RentDay = rentDay + RoommateRules.RentEveryDays;
            BuildAlerts();
            if (Verbose)
            {
                Logger?.Info("Roommates check (rent day " + rentDay + "): " + m.Person.Name + " owed " + s.Weekly + " (" + m.SharePct + "% of " + perDay.ToString("F0") + " a day for "
                             + RoommateRules.RentEveryDays + " days), " + r.Outcome + ": paid " + r.Paid + ", still owed " + s.Owed + (s.Owed > 0 ? " by day " + s.OwedDue : string.Empty)
                             + ": " + (r.Paid + r.Owed == s.Weekly ? "PASS" : "FAIL (paid and owed do not add up to the share)") + ".");
            }
        }

        // One day. heard: the player was in the home when the day began, so a noisy night was theirs too.
        private void Daily(int day, bool heard)
        {
            RoommateState s = _state;
            if (!s.Living)
            {
                return;
            }
            Flatmate m = s.Mate;
            string name = m.Person.Name;
            bool partner = PartnerIn;

            // The home: still theirs, moved, or given up.
            BuildingRegistration home = RoommateGame.HomeAt(s.HomeStreet, s.HomeNumber);
            if (home == null)
            {
                BuildingRegistration next = RoommateGame.AnyHome();
                RoomKind there = RoommateGame.Room(next, partner);
                EndWhy why = RoommateRules.OnMove(next != null, there, Level);
                if (why != EndWhy.None)
                {
                    End(why, day);
                    return;
                }
                RoommateGame.RentPerDay(next, out bool owned);
                s.HomeStreet = next.StreetName;
                s.HomeNumber = next.StreetNumber;
                s.Lodger = owned;
                s.OnSofa = there == RoomKind.Sofa;
                home = next;
                _flat.Leave();
                RoommateText.Message("reallife_text_roommate_moved_along", Fill.For(name));
                Logger?.Info("Roommates: the player moved home and " + name + " came along to " + next.StreetNumber + " " + next.StreetName + " (level " + Level + ").");
            }
            if (m.Friend >= 0 && RoommateBridges.IsPartner != null && RoommateBridges.IsPartner(m.Friend))
            {
                End(EndWhy.BecamePartner, day);
                return;
            }
            RoommateGame.Furniture(home, out int beds, out int sofas);
            if (partner && !s.PartnerSeen)
            {
                // The player's partner has moved in: the question comes up. With no bed of their own there is nothing to ask.
                if (RoommateRules.OnPartner(beds) == EndWhy.None)
                {
                    s.Topics |= Topic.PartnerIn;
                    RoommateText.Message("reallife_text_roommate_partner_talk", Fill.For(name).And(RoommateBridges.PartnerName != null ? RoommateBridges.PartnerName() : string.Empty));
                }
                else if (!s.OnNotice)
                {
                    s.GiveNotice(EndWhy.PartnerMovedIn, day);
                    RoommateText.Message("reallife_text_roommate_partner_go", Fill.For(name).On(RoommateText.When(s.LeaveDay)));
                }
            }
            else if (!s.OnNotice && RoommateRules.Room(beds, sofas, partner) == RoomKind.None)
            {
                // The bed or the sofa they had has been sold or taken away.
                s.GiveNotice(EndWhy.NoRoomAfterMove, day);
                RoommateText.Message("reallife_text_roommate_no_bed", Fill.For(name).On(RoommateText.When(s.LeaveDay)));
            }
            s.PartnerSeen = partner;
            s.OnSofa = beds < 2;

            if (s.OnNotice && RoommateRules.Gone(s.LeaveDay, day))
            {
                End(s.Notice, day);
                return;
            }

            int daysIn = day - s.MovedIn;
            Moment(RoomMoment.DayTogether);
            RoomTrait shown = RoommateRules.Reveal(m.Traits, m.Known, daysIn);
            if (shown != RoomTrait.None)
            {
                Seen(shown);
            }

            // Rent that was promised, and a loan.
            if (s.Owed > 0 && day >= s.OwedDue)
            {
                if (RoommateRules.PaysArrears(m.Person.Seed, day, m.Traits, s.OwedSlips))
                {
                    RoommateGame.Receive(s.Owed, s.Lodger ? LodgerKey : RentKey);
                    RoommateText.Toast(NotificationType.Success, "reallife_roommate_arrears_paid", Fill.For(name).Cost(s.Owed), 8f);
                    s.Owed = 0;
                    s.OwedDue = 0;
                    s.OwedSlips = 0;
                    s.Topics &= ~Topic.Rent;
                }
                else
                {
                    s.OwedSlips++;
                    s.OwedDue = day + RoommateRules.SlipDays;
                    s.Topics |= Topic.Rent;
                    Moment(RoomMoment.OverdueDay);
                    RoommateText.Message("reallife_text_roommate_rent_slips", Fill.For(name).Cost(s.Owed).On(RoommateText.When(s.OwedDue)));
                }
            }
            if (s.Loan > 0 && day >= s.LoanDue)
            {
                if (RoommateRules.Repays(m.Person.Seed, day, m.Traits))
                {
                    RoommateGame.Receive(s.Loan, "reallife_transaction_roommate_loan_back");
                    RoommateText.Toast(NotificationType.Success, "reallife_roommate_loan_back", Fill.For(name).Cost(s.Loan), 8f);
                    s.Loan = 0;
                    s.LoanDue = 0;
                    Moment(RoomMoment.Repaid);
                }
                else
                {
                    s.LoanDue = day + RoommateRules.SlipDays;
                    Moment(RoomMoment.NotRepaid);
                }
            }

            // What the day brings.
            int week = daysIn / 7;
            if (week != s.RaidWeek)
            {
                s.RaidWeek = week;
                s.Raids = 0;
            }
            int weekday = RoommateGame.Weekday(day);
            var context = new DayContext
            {
                InAtNight = RoommateRules.Where(m.Person.Job, m.Traits, m.Person.Seed, day - 1, (weekday + 6) % 7, 23) != RoomWhere.Work, QuietAsked = s.QuietAsked,
                RaidStopped = s.RaidStopped, RaidsThisWeek = s.Raids, Owes = s.Owed > 0, HasLoan = s.Loan > 0 || s.BorrowAsk > 0, OnNotice = s.OnNotice, DaysIn = daysIn,
                Affection = m.Person.Affection, Trust = m.Person.Trust, Level = Level
            };
            Events(RoommateRules.Day(m.Person.Seed, day, m.Traits, context), day, heard, daysIn);
            if (!s.Living)
            {
                return;
            }

            // Somebody the player gets on with becomes one of the people in their life, and may bring somebody along.
            if (!s.MadeFriend && Level >= RoommateRules.FriendLevel && RoommateBridges.MakeFriend != null)
            {
                int place = RoommateBridges.MakeFriend(m);
                if (place >= 0)
                {
                    m.Friend = place;
                    m.Person.Index = place;
                    s.MadeFriend = true;
                    Logger?.Info("Roommates: " + name + " is one of the people of social life now (place " + place + ", level " + Level + ").");
                }
            }
            else if (s.MadeFriend && !s.Introduced && m.Friend >= 0 && Level >= RoommateRules.FriendLevel && RoommateBridges.Introduce != null
                     && RoommateRules.Roll(m.Person.Seed, day, 7) < 2000)
            {
                string other = RoommateBridges.Introduce(m.Friend);
                if (other != null)
                {
                    s.Introduced = true;
                    RoommateText.Message("reallife_text_roommate_introduce", Fill.For(name).And(other));
                }
            }
            BuildAlerts();
        }

        // The day's events, each done once: this is also what the test buttons call, one event at a time.
        private void Events(DayEvents events, int day, bool heard, int daysIn)
        {
            RoommateState s = _state;
            Flatmate m = s.Mate;
            string name = m.Person.Name;
            if ((events & DayEvents.Noise) != 0 && heard)
            {
                bool slept = RoommateGame.BrokenSleep(RoommateRules.NoiseSleepHours);
                RoommateGame.Mood(ModState.MoodDown);
                Moment(RoomMoment.NoisyNight);
                s.Topics |= Topic.Noise;
                RoommateText.Toast(NotificationType.Warning, "reallife_roommate_noise", Fill.For(name), 9f);
                Seen(RoommateRules.Has(m.Traits, RoomTrait.Noisy) ? RoomTrait.Noisy : RoomTrait.NightOwl);
                if (Verbose)
                {
                    Logger?.Info("Roommates check (noisy night): the mood went down by the mod's own modifier and "
                                 + (slept ? RoommateRules.NoiseSleepHours.ToString("F1", CultureInfo.InvariantCulture) + " h of sleep is owed"
                                 : "no sleep is owed (home life's sleep is off)") + ": PASS.");
                }
            }
            if ((events & DayEvents.Cooked) != 0)
            {
                if (RoommateGame.LeaveMeal(RoommateRules.Roll(m.Person.Seed, day, 8)))
                {
                    s.Meals++;
                    s.Topics |= Topic.Cooked;
                    RoommateText.Message("reallife_text_roommate_cooked", Fill.For(name));
                    Seen(RoomTrait.Cooks);
                }
                else if (Verbose)
                {
                    Logger?.Info("Roommates check (cooked): no portion was left (home life or its routines are off, or the fridge is full of portions): PASS.");
                }
            }
            if ((events & DayEvents.Raid) != 0)
            {
                string taken = RoommateGame.TakeFood(s.Raids, s.RaidStopped, RoommateGame.Inside(s.HomeStreet, s.HomeNumber));
                if (taken != null)
                {
                    s.Raids++;
                    s.RaidItem = taken;
                    s.Topics |= Topic.Raid;
                    Seen(RoomTrait.RaidsFridge);
                    if (Verbose)
                    {
                        Logger?.Info("Roommates check (fridge): one unit of " + taken + " went, " + s.Raids + " of " + RoommateRules.RaidsPerWeek + " this week: "
                                     + (s.Raids <= RoommateRules.RaidsPerWeek ? "PASS" : "FAIL") + ".");
                    }
                }
            }
            if ((events & (DayEvents.Tidied | DayEvents.Mess)) != 0)
            {
                float change = RoommateRules.TidyChange(m.Traits);
                if (RoommateGame.Tidy(change) && daysIn > 0 && daysIn % RoommateRules.RevealDays == 0)
                {
                    Seen(change > 0f ? RoomTrait.Tidy : RoomTrait.Messy);
                }
            }
            if ((events & DayEvents.Borrow) != 0)
            {
                s.BorrowAsk = RoommateRules.BorrowAmount(s.Weekly);
                s.Topics |= Topic.Borrow;
                RoommateText.Message("reallife_text_roommate_borrow", Fill.For(name));
            }
            if ((events & DayEvents.Notice) != 0 && !s.OnNotice)
            {
                EndWhy why = RoommateRules.Leaving(m.Person.Seed, day, daysIn, Level, m.Person.Affection, m.Person.Trust);
                TheirNotice(why == EndWhy.None ? EndWhy.NewJob : why, day);
            }
        }

        private void TheirNotice(EndWhy why, int day)
        {
            RoommateState s = _state;
            s.GiveNotice(why, day);
            s.Topics |= Topic.Leaving;
            RoommateText.Message(why == EndWhy.Row ? "reallife_text_roommate_notice_row" : "reallife_text_roommate_notice",
                Fill.For(s.Mate.Person.Name).On(RoommateText.When(s.LeaveDay)).With(RoommateText.T(RoommateText.WhyKey(why))));
            BuildAlerts();
            Logger?.Info("Roommates: " + s.Mate.Person.Name + " gave notice (" + why + "), leaving on day " + s.LeaveDay + ".");
        }

        // Company at dinner: both in the flat in the evening, once a day. Asked once a second at home; two comparisons unless it is the hour.
        private void Company(RoomWhere where)
        {
            RoommateState s = _state;
            int day = RoommateGame.Day;
            int hour = RoommateGame.Hour;
            if (s.LastCompanyDay == day || hour < 18 || hour > 20 || where != RoomWhere.In || !_flat.Present || !LifeEvents.IsSafeForEvent())
            {
                return;
            }
            s.LastCompanyDay = day;
            if (RoommateRules.Has(s.Mate.Traits, RoomTrait.Private) || (!RoommateRules.Has(s.Mate.Traits, RoomTrait.Sociable) && Level < RoommateRules.GoodLevel))
            {
                return;
            }
            Moment(RoomMoment.Talk);
            RoommateGame.Mood(ModState.MoodUp);
            RoommateText.Toast(NotificationType.Success, "reallife_roommate_company", Fill.For(s.Mate.Person.Name), 8f);
            Seen(RoomTrait.Sociable);
        }

        // It is over: they pay for the days since the last rent day and what they will of what they owe, and are gone.
        private void End(EndWhy why, int day)
        {
            RoommateState s = _state;
            Flatmate m = s.Mate;
            if (m == null)
            {
                return;
            }
            int last = RoommateRules.FinalRent(s.Weekly, day - s.LastRentDay);
            int settled = RoommateRules.SettledOnLeaving(s.Owed + s.Loan, m.Traits);
            RoommateGame.Receive(last, s.Lodger ? LodgerKey : RentKey);
            RoommateGame.Receive(settled, "reallife_transaction_roommate_settled");
            _flat.Leave();
            if (why == EndWhy.Row)
            {
                RoommateGame.Mood(ModState.MoodDown);
            }
            RoommateText.Toast(why == EndWhy.Row ? NotificationType.Warning : NotificationType.Info, "reallife_roommate_gone",
                Fill.For(m.Person.Name).With(RoommateText.T(RoommateText.WhyKey(why))).Cost(last + settled), 12f);
            Logger?.Info("Roommates: " + m.Person.Name + " has gone (" + why + ") after " + (day - s.MovedIn) + " day(s): " + last + " for the last days, " + settled + " of "
                         + (s.Owed + s.Loan) + " owed, level " + Level + ".");
            s.ClearMate();
            BuildAlerts();
        }

        // ---------------------------------------------------------------- what the player's replies do

        private void Done(Topic topic)
        {
            _state.Topics &= ~topic;
            _state.LastTalkDay = RoommateGame.Day;
            BuildAlerts();
        }

        public void RentWait()
        {
            Moment(RoomMoment.Waited);
            Done(Topic.Rent);
        }

        /// <summary>The player asks for the rent now. True if it was paid on the spot.</summary>
        public bool RentChase()
        {
            RoommateState s = _state;
            bool paid = s.Owed > 0 && RoommateRules.PaysWhenChased(s.Mate.Person.Seed, RoommateGame.Day, s.Mate.Traits, Level);
            if (paid)
            {
                RoommateGame.Receive(s.Owed, s.Lodger ? LodgerKey : RentKey);
                s.Owed = 0;
                s.OwedDue = 0;
                s.OwedSlips = 0;
            }
            Moment(paid ? RoomMoment.Chased : RoomMoment.ChaseRefused);
            Done(Topic.Rent);
            return paid;
        }

        public void RentCover()
        {
            RoommateState s = _state;
            s.Owed = 0;
            s.OwedDue = 0;
            s.OwedSlips = 0;
            s.Covered++;
            Moment(RoomMoment.CoveredRent);
            Done(Topic.Rent);
        }

        public void NoiseAsk()
        {
            _state.QuietAsked = true;
            Moment(RoomMoment.AskedQuietly);
            Done(Topic.Noise);
        }

        public void NoiseFine()
        {
            Moment(RoomMoment.NoiseForgiven);
            Done(Topic.Noise);
        }

        public void NoiseRow()
        {
            _state.QuietAsked = true;
            Moment(RoomMoment.Row);
            Done(Topic.Noise);
        }

        public void RaidFine()
        {
            Moment(RoomMoment.RaidForgiven);
            Done(Topic.Raid);
        }

        public void RaidStop()
        {
            _state.RaidStopped = true;
            Moment(RoomMoment.AskedToStop);
            Done(Topic.Raid);
        }

        /// <summary>The player asks them to pay for what they took. True if they did; somebody tight with money does not.</summary>
        public bool RaidReplace()
        {
            bool pays = !RoommateRules.Has(_state.Mate.Traits, RoomTrait.Tight);
            if (pays)
            {
                RoommateGame.Receive(RoommateRules.FoodPrice, "reallife_transaction_roommate_food");
            }
            else
            {
                Moment(RoomMoment.ShortReply);
                Seen(RoomTrait.Tight);
            }
            Done(Topic.Raid);
            return pays;
        }

        /// <summary>The player lends what was asked. False, with the game's own notice, if they have not got it (the question then still stands).</summary>
        public bool BorrowYes()
        {
            RoommateState s = _state;
            if (s.BorrowAsk <= 0 || !RoommateGame.Pay(s.BorrowAsk, "reallife_transaction_roommate_loan"))
            {
                return false;
            }
            s.Loan += s.BorrowAsk;
            s.LoanDue = RoommateGame.Day + RoommateRules.BorrowDays;
            s.BorrowAsk = 0;
            Moment(RoomMoment.Lent);
            Done(Topic.Borrow);
            return true;
        }

        public void BorrowNo()
        {
            _state.BorrowAsk = 0;
            Moment(RoomMoment.RefusedLoan);
            Done(Topic.Borrow);
        }

        /// <summary>The two eat the portion that was left, together. False if it has gone already.</summary>
        public bool CookedEat()
        {
            bool ate = RoommateGame.EatMeal();
            if (ate)
            {
                _state.LastMealDay = RoommateGame.Day;
                Moment(RoomMoment.SharedMeal);
                RoommateGame.Mood(ModState.HomeCooked);
            }
            Done(Topic.Cooked);
            return ate;
        }

        public void CookedLater()
        {
            Moment(RoomMoment.Thanked);
            Done(Topic.Cooked);
        }

        public void PartnerStay()
        {
            Moment(RoomMoment.WarmReply);
            Done(Topic.PartnerIn);
        }

        public void PartnerGo()
        {
            if (!_state.OnNotice)
            {
                _state.GiveNotice(EndWhy.PartnerMovedIn, RoommateGame.Day);
            }
            Done(Topic.PartnerIn);
        }

        public void LeavingLuck()
        {
            Moment(RoomMoment.GoodLuck);
            Done(Topic.Leaving);
        }

        public void LeavingHeard()
        {
            Done(Topic.Leaving);
        }

        /// <summary>The player asks them to stay after all. True if they do.</summary>
        public bool LeavingAskStay()
        {
            RoommateState s = _state;
            bool stays = s.OnNotice && RoommateRules.StaysWhenAsked(s.Mate.Person.Seed, RoommateGame.Day, Level, s.Notice);
            if (stays)
            {
                s.TakeBackNotice();
                Moment(RoomMoment.WarmReply);
            }
            Done(Topic.Leaving);
            return stays;
        }

        public void DayWarm()
        {
            Moment(RoomMoment.Talk);
            Moment(RoomMoment.WarmReply);
            Done(Topic.None);
        }

        public void DayShort()
        {
            Moment(RoomMoment.ShortReply);
            Done(Topic.None);
        }

        public void GiveNoticeByPlayer()
        {
            RoommateState s = _state;
            if (!s.Living || s.OnNotice)
            {
                return;
            }
            s.GiveNotice(EndWhy.PlayerNotice, RoommateGame.Day);
            Moment(RoomMoment.ToldToGo);
            BuildAlerts();
            Logger?.Info("Roommates: the player gave " + s.Mate.Person.Name + " notice, leaving on day " + s.LeaveDay + ".");
        }

        /// <summary>The player takes their own notice back. False if it was not theirs, or things are too strained for it.</summary>
        public bool TakeBackNotice()
        {
            RoommateState s = _state;
            if (!s.Living || s.Notice != EndWhy.PlayerNotice || Level < RoommateRules.LowLevel)
            {
                return false;
            }
            s.TakeBackNotice();
            BuildAlerts();
            return true;
        }
    }
}
