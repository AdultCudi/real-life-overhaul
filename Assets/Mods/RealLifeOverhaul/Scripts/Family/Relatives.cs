using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The wider family: Mom, the in-laws from the engagement on, four family dinners a year (DinnerRules: when, where and how
    /// each is attended), and a next of kin. COST: nothing per frame; the day's round, a call or a test button.
    /// </summary>
    internal static class Relatives
    {
        private static readonly string[] HolidayNames =
        {
            "reallife_family_holiday_spring", "reallife_family_holiday_summer", "reallife_family_holiday_thanksgiving", "reallife_family_holiday_winter"
        };

        // Mom's invitation the day before says how to come, by where the dinner will be: to hers, ring her from midday
        // (DinnerRules.MomsFrom); a household's own is at home in its hours. The day's text says it again.
        private static readonly string[] Invites = { "reallife_family_holiday_invite_1", "reallife_family_holiday_invite_2", "reallife_family_holiday_invite_3" };
        private static readonly string[] HomeInvites = { "reallife_family_holiday_home_invite_1", "reallife_family_holiday_home_invite_2" };
        private static readonly string[] DinnerLines = { "reallife_family_holiday_done_1", "reallife_family_holiday_done_2", "reallife_family_holiday_done_3", "reallife_family_holiday_done_4" };
        private static readonly string[] MissedLines = { "reallife_family_holiday_missed_1", "reallife_family_holiday_missed_2" };
        private static readonly string[] Visits = { "reallife_family_mom_visit_1", "reallife_family_mom_visit_2", "reallife_family_mom_visit_3" };
        private static readonly string[] InLawChats = { "reallife_family_inlaws_chat_1", "reallife_family_inlaws_chat_2", "reallife_family_inlaws_chat_3", "reallife_family_inlaws_chat_4" };
        private static readonly string[] InLawAsks = { null, "reallife_family_inlaws_ask_lift", "reallife_family_inlaws_ask_loan", "reallife_family_inlaws_ask_move" };
        private static readonly string[] InLawThanks = { null, "reallife_family_inlaws_thanks_lift", "reallife_family_inlaws_thanks_loan", "reallife_family_inlaws_thanks_move" };

        internal static int HostCost => Prices.At(120);
        private const int LoanAmount = 300;   // lent to the in-laws for a week, not at today's prices

        private static int _lastInvite = -1, _lastHomeInvite = -1, _lastDinner = -1, _lastMissed = -1, _lastVisit = -1, _lastChat = -1;
        private static int _tableNowDay;   // a test's dinner day at Mom's, her table set from the moment it was set; not saved

        private static bool On => Settings.FamilyRelatives;

        /// <summary>A text from Mom. Nothing if she is not in the phone (the contacts option is off) or relatives are switched off.</summary>
        public static void MomSays(string key)
        {
            if (On && PhoneContacts.Mom != null)
            {
                FamilyText.Message(PhoneContacts.Mom, key, Args.Us);
            }
        }

        public static void MomGift(int amount, string key, Args fill)
        {
            if (!On || PhoneContacts.Mom == null)
            {
                return;
            }
            FamilyTalk.Move(amount, "reallife_transaction_family_gift");
            FamilyState.MoneyIn(amount);
            fill.Amount = FamilyText.Money(amount);
            FamilyText.Message(PhoneContacts.Mom, key, fill);
        }

        public static void InLawsSay(string key)
        {
            if (On && FamilyState.S.InLaws && FamilyPhone.InLawContact != null)
            {
                FamilyText.Message(FamilyPhone.InLawContact, key, Args.Us);
            }
        }

        public static string HolidayName(int which)
        {
            return HolidayNames[Mathf.Clamp(which, 0, 3)];
        }

        public static void Daily(int day)
        {
            FamilySave s = FamilyState.S;
            if (!On)
            {
                return;
            }
            int year = GameCalendar.DaysPerYear;
            int which = DinnerRules.InviteOn(day, year, out int stamp);
            if (which >= 0 && s.LastHolidayTold < stamp)
            {
                s.LastHolidayTold = stamp;
                Invite(which);
            }
            which = DinnerRules.DinnerOn(day, year, out stamp);
            if (which >= 0 && s.LastHoliday < stamp)
            {
                s.LastHoliday = stamp;
                DinnerToday(day, which, false);
            }

            // Mom drops by on a Sunday now and then, once there is a household to drop in on, and a home to drop in on: every
            // visit's words are of the home, one of its fridge (Core/LineNeeds), so without a home she does not come.
            string visit;
            if (TimeHelper.GetDayOfWeek(day) == DayOfWeekOrdered.Sunday && day - s.LastMomVisitDay >= 14 && PhoneContacts.Mom != null
                && (FamilyState.Bond == Bond.Married || FamilyState.AtHome() > 0) && Random.value < 0.5f
                && (visit = FamilyText.Pick(Visits, ref _lastVisit)) != null)
            {
                s.LastMomVisitDay = day;
                ModState.AddModifier(ModState.Family);
                for (int i = 0; i < s.Children.Count; i++)
                {
                    if (!s.Children[i].Grown)
                    {
                        s.Children[i].Closeness = Mathf.Min(100f, s.Children[i].Closeness + 1f);
                    }
                }
                FamilyText.Message(PhoneContacts.Mom, visit, Args.Us);
            }

            // The in-laws: a favour asked, a loan paid back, a favour that went unanswered.
            if (!s.InLaws || !FamilyState.Together)
            {
                return;
            }
            if (s.InLawLoanDay > 0 && day >= s.InLawLoanDay)
            {
                s.InLawLoanDay = 0;
                FamilyTalk.Move(LoanAmount, "reallife_transaction_family_loan");
                FamilyState.MoneyIn(LoanAmount);
                InLawsSay("reallife_family_inlaws_repaid");
            }
            if (s.InLawAsk > 0 && day - s.InLawAskDay >= 3)
            {
                s.InLawAsk = 0;
                FamilyState.Harmony(-1f);
            }
            else if (s.InLawAsk == 0 && day - s.InLawAskDay >= 12 && Random.value < 0.25f)
            {
                // A favour the player can do: a lift only of somebody with a car (Core/LineNeeds); the empty first place is never drawn.
                int ask = LineNeeds.Pick(InLawAsks, -1, FamilyText.Random01);
                if (ask > 0)
                {
                    s.InLawAskDay = day;
                    s.InLawAsk = ask;
                    InLawsSay(InLawAsks[ask]);
                }
            }
        }

        // Where this year's dinner is, by the household as it is today.
        private static DinnerPlace Place => DinnerRules.PlaceFor(FamilyState.Together && FamilyState.S.LivesIn, FamilyState.AtHome());

        // The day before: Mom's invitation, saying how to come by where the dinner will be.
        private static void Invite(int which)
        {
            if (PhoneContacts.Mom == null)
            {
                return;
            }
            Args fill = Args.Us.With(FamilyText.T(HolidayNames[which]));
            if (Place == DinnerPlace.Home)
            {
                FamilyText.Message(PhoneContacts.Mom, FamilyText.Pick(HomeInvites, ref _lastHomeInvite), fill.Hours(DinnerRules.HomeFrom, DinnerRules.HomeTo));
                return;
            }
            FamilyText.Message(PhoneContacts.Mom, FamilyText.Pick(Invites, ref _lastInvite), fill);
        }

        // The day itself: a household's dinner at home (the errand, begun at the home's door in its hours), or Mom's table set
        // from midday, offered by her call and the Family contact. Mom's text says which, and how. A test sets it from now.
        private static DinnerPlace DinnerToday(int day, int which, bool fromNow)
        {
            FamilySave s = FamilyState.S;
            Args fill = Args.Us.With(FamilyText.T(HolidayNames[which]));
            if (Place == DinnerPlace.Home)
            {
                DinnerRules.HomeHours(TimeHelper.CurrentHour, fromNow, out int from, out int to);
                Errand errand = Errands.Add(Errands.Holiday, day, from, to);
                errand.Day = day;   // one of an earlier test that is still listed is moved to today
                errand.FromHour = from;
                errand.ToHour = to;
                errand.Number = which;
                if (PhoneContacts.Mom != null)
                {
                    FamilyText.Message(PhoneContacts.Mom, "reallife_family_holiday_at_home", fill.Hours(from, to));
                }
                return DinnerPlace.Home;
            }
            s.MomDinnerDay = day;
            s.MomDinnerWhich = which;
            _tableNowDay = fromNow ? day : 0;
            if (PhoneContacts.Mom != null)
            {
                bool now = fromNow && TimeHelper.CurrentHour < DinnerRules.MomsFrom;
                FamilyText.Message(PhoneContacts.Mom, now ? "reallife_family_holiday_at_moms_now" : "reallife_family_holiday_at_moms", fill);
            }
            return DinnerPlace.Moms;
        }

        /// <summary>
        /// Options, Tests: today is a family dinner, set as the day's round sets it (at home with a household, else at Mom's),
        /// and ready now: the errand from this hour, or her table set at once. The next dinner on the calendar is the one, and
        /// Mom's text for the day comes as it would. Where it is, and the dinner's name, for the test's notice.
        /// </summary>
        internal static DinnerPlace TestToday(out string name)
        {
            int which = DinnerRules.Next(TimeHelper.CurrentDay, GameCalendar.DaysPerYear, out _);
            which = which < 0 ? 2 : which;
            name = FamilyText.T(HolidayNames[which]);
            return DinnerToday(TimeHelper.CurrentDay, which, true);
        }

        public static void Dinner(Errand errand)
        {
            FamilyDirector director = FamilyDirector.Instance;
            int which = errand.Number;
            if (!FamilyTalk.Pay(HostCost, "reallife_transaction_family_dinner"))
            {
                return;
            }
            FamilyState.MoneyOut(HostCost, 4);
            FamilyState.S.Errands.Remove(errand);
            director.Pass("reallife_family_passage_holiday", DinnerRules.HomeLength, () => EnergyHelper.SetCurrentHunger(100f), () => AfterDinner(which));
        }

        private static void AfterDinner(int which)
        {
            FamilySave s = FamilyState.S;
            ModState.AddModifier(ModState.Family);
            FamilyModifiers.Add(FamilyModifiers.Joy);
            if (FamilyState.Together)
            {
                HomeLife.Shared(6f);
            }
            for (int i = 0; i < s.Children.Count; i++)
            {
                if (!s.Children[i].Grown)
                {
                    s.Children[i].Closeness = Mathf.Min(100f, s.Children[i].Closeness + 2f);
                }
            }
            FamilyState.Remember("reallife_family_memory_holiday", FamilyText.T(HolidayName(which)));
            FamilyText.Good(FamilyText.Pick(DinnerLines, ref _lastDinner), Args.Us.With(FamilyText.T(HolidayName(which))));
        }

        public static void DinnerMissed(Errand errand)
        {
            if (PhoneContacts.Mom != null && On)
            {
                FamilyText.Message(PhoneContacts.Mom, FamilyText.Pick(MissedLines, ref _lastMissed), Args.Us.With(FamilyText.T(HolidayName(errand.Number))));
            }
        }

        /// <summary>Mom's table today: set from midday on her dinner day (from the moment a test set it), until the player goes over.</summary>
        public static MomsTable Table => !On ? MomsTable.None
            : DinnerRules.Table(FamilyState.S.MomDinnerDay, TimeHelper.CurrentDay, TimeHelper.CurrentHour, _tableNowDay == TimeHelper.CurrentDay ? 0 : DinnerRules.MomsFrom);

        public static bool CanGoToMoms => Table == MomsTable.Set;

        /// <summary>Today's dinner at Mom's by name ("Thanksgiving") while her table is set, else null: for her own call.</summary>
        public static string MomsDinnerName => CanGoToMoms ? FamilyText.T(HolidayName(FamilyState.S.MomDinnerWhich)) : null;

        public static DialogEntry GoToMoms()
        {
            FamilySave s = FamilyState.S;
            FamilyDirector director = FamilyDirector.Instance;
            int which = s.MomDinnerWhich;
            s.MomDinnerDay = 0;
            _tableNowDay = 0;
            director.WhenFree(() => director.Pass("reallife_family_passage_moms", DinnerRules.MomsLength, () => EnergyHelper.SetCurrentHunger(100f), () => AfterDinner(which)));
            return FamilyTalk.Close("reallife_family_holiday_going", Args.Us.With(FamilyText.T(HolidayName(which))));
        }

        public static bool InLawFavourOpen => FamilyState.S.InLaws && FamilyState.S.InLawAsk > 0;

        public static DialogEntry InLawChat()
        {
            FamilySave s = FamilyState.S;
            if (s.LastInLawChatDay == TimeHelper.CurrentDay)
            {
                return FamilyTalk.Close("reallife_family_inlaws_again");
            }
            s.LastInLawChatDay = TimeHelper.CurrentDay;
            FamilyState.Harmony(1.5f);
            ModState.AddModifier(ModState.Social);
            return FamilyTalk.Close(FamilyText.Pick(InLawChats, ref _lastChat));
        }

        /// <summary>What they think the player should do, said kindly: it reads the household as it is.</summary>
        public static DialogEntry InLawAdvice()
        {
            FamilySave s = FamilyState.S;
            string key = FamilyState.Bond == Bond.Separated ? "reallife_family_inlaws_advice_apart"
                : s.Harmony < 45f ? "reallife_family_inlaws_advice_tense"
                : FamilyState.Bond == Bond.Engaged && !s.Wedding.Active ? "reallife_family_inlaws_advice_plan"
                : FamilyState.Process != ProcessKind.None ? "reallife_family_inlaws_advice_expecting"
                : FamilyState.AtHome() > 0 ? "reallife_family_inlaws_advice_children"
                : "reallife_family_inlaws_advice_fine";
            return FamilyTalk.Close(key);
        }

        public static DialogEntry InLawFavour()
        {
            FamilySave s = FamilyState.S;
            int ask = Mathf.Clamp(s.InLawAsk, 1, 3);
            if (ask == 2)
            {
                if (!FamilyTalk.Pay(LoanAmount, "reallife_transaction_family_loan"))
                {
                    return null;
                }
                s.InLawLoanDay = TimeHelper.CurrentDay + 7;
            }
            else if (SaveGameManager.Current.Energy > 25f)
            {
                EnergyHelper.SpentEnergyOnce(8f);
            }
            s.InLawAsk = 0;
            FamilyState.Harmony(5f);
            Partner.Tell(FamilyEvent.SharedTime, 2f, 3f);
            return FamilyTalk.Close(InLawThanks[ask], Args.Us.Cost(LoanAmount));
        }

        public static string NextOfKin()
        {
            // A spouse is next of kin, not somebody the player is dating or engaged to: Mom until the wedding.
            FamilySave s = FamilyState.S;
            bool spouse = FamilyState.Bond == Bond.Married;
            if (!string.IsNullOrEmpty(s.NextOfKin) && (spouse || s.NextOfKin != Partner.Name))
            {
                return s.NextOfKin;
            }
            return spouse ? Partner.Name : FamilyText.T("reallife-mom");
        }

        public static DialogEntry NextOfKinStep()
        {
            FamilySave s = FamilyState.S;
            // Round the people there are: the partner, Mom, each child.
            string current = NextOfKin();
            string mom = FamilyText.T("reallife-mom");
            string next = null;
            bool take = false;
            for (int pass = 0; pass < 2 && next == null; pass++)
            {
                if (FamilyState.Bond == Bond.Married)
                {
                    if (take) { next = Partner.Name; break; }
                    take |= current == Partner.Name;
                }
                if (take) { next = mom; break; }
                take |= current == mom;
                for (int i = 0; i < s.Children.Count; i++)
                {
                    if (take) { next = s.Children[i].Name; break; }
                    take |= current == s.Children[i].Name;
                }
                take = true;
            }
            s.NextOfKin = next ?? mom;
            return FamilyTalk.Either(FamilyText.Say("reallife_family_kin_set", Args.Us.With(s.NextOfKin)), "reallife_family_kin_change", NextOfKinStep,
                "reallife_family_menu_back", () => FamilyPhone.FamilyMenu(0));
        }
    }
}
