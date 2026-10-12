using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The wider family. Mom (the mod's own phone contact) reacts to every milestone, sends
    /// small presents and drops by; the in-laws arrive with the engagement as a contact of their
    /// own, to chat with, to ask for advice, and now and then to ask a favour; four times a
    /// year (on the game's own calendar) there is a family dinner, at home if there is a
    /// household, at Mom's otherwise. A next of kin is kept as a line of text and nothing more.
    /// </summary>
    internal static class Relatives
    {
        private static readonly string[] HolidayNames =
        {
            "reallife_family_holiday_spring", "reallife_family_holiday_summer", "reallife_family_holiday_thanksgiving", "reallife_family_holiday_winter"
        };

        private static readonly float[] HolidayAt = { 0.22f, 0.47f, 0.72f, 0.97f };   // of the game's year
        private static readonly string[] Invites = { "reallife_family_holiday_invite_1", "reallife_family_holiday_invite_2", "reallife_family_holiday_invite_3" };
        private static readonly string[] DinnerLines = { "reallife_family_holiday_done_1", "reallife_family_holiday_done_2", "reallife_family_holiday_done_3", "reallife_family_holiday_done_4" };
        private static readonly string[] MissedLines = { "reallife_family_holiday_missed_1", "reallife_family_holiday_missed_2" };
        private static readonly string[] Visits = { "reallife_family_mom_visit_1", "reallife_family_mom_visit_2", "reallife_family_mom_visit_3" };
        private static readonly string[] InLawChats = { "reallife_family_inlaws_chat_1", "reallife_family_inlaws_chat_2", "reallife_family_inlaws_chat_3", "reallife_family_inlaws_chat_4" };
        private static readonly string[] InLawAsks = { null, "reallife_family_inlaws_ask_lift", "reallife_family_inlaws_ask_loan", "reallife_family_inlaws_ask_move" };
        private static readonly string[] InLawThanks = { null, "reallife_family_inlaws_thanks_lift", "reallife_family_inlaws_thanks_loan", "reallife_family_inlaws_thanks_move" };

        internal static int HostCost => Prices.At(120);
        private const int LoanAmount = 300;

        private static int _lastInvite = -1, _lastDinner = -1, _lastMissed = -1, _lastVisit = -1, _lastChat = -1;

        private static bool On => Settings.FamilyRelatives;

        /// <summary>A text from Mom. Nothing if she is not in the phone (the contacts option is off) or relatives are switched off.</summary>
        public static void MomSays(string key)
        {
            if (On && PhoneContacts.Mom != null)
            {
                FamilyText.Message(PhoneContacts.Mom, key, Args.Us);
            }
        }

        /// <summary>A present of money from Mom, with a note.</summary>
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

        // ---------------------------------------------------------------- the calendar

        public static void Daily(int day)
        {
            FamilySave s = FamilyState.S;
            if (!On)
            {
                return;
            }
            int year = GameCalendar.DaysPerYear;
            int count = (day - 1) / year;
            int dayOfYear = (day - 1) % year;
            for (int i = 0; i < 4; i++)
            {
                int on = Mathf.Clamp(Mathf.RoundToInt(year * HolidayAt[i]), 1, year - 1);
                int stamp = count * 4 + i;
                if (dayOfYear == on - 1 && s.LastHolidayTold < stamp)
                {
                    s.LastHolidayTold = stamp;
                    if (PhoneContacts.Mom != null)
                    {
                        FamilyText.Message(PhoneContacts.Mom, FamilyText.Pick(Invites, ref _lastInvite), Args.Us.With(FamilyText.T(HolidayNames[i])));
                    }
                }
                if (dayOfYear == on && s.LastHoliday < stamp)
                {
                    s.LastHoliday = stamp;
                    if (FamilyState.Together && s.LivesIn || FamilyState.AtHome() > 0)
                    {
                        Errands.Add(Errands.Holiday, day, 15, 23).Number = i;
                    }
                    else
                    {
                        s.MomDinnerDay = day;
                        s.MomDinnerWhich = i;
                        if (PhoneContacts.Mom != null)
                        {
                            FamilyText.Message(PhoneContacts.Mom, "reallife_family_holiday_at_moms", Args.Us.With(FamilyText.T(HolidayNames[i])));
                        }
                    }
                }
            }

            // Mom drops by on a Sunday now and then, once there is a household to drop in on.
            if (TimeHelper.GetDayOfWeek(day) == DayOfWeekOrdered.Sunday && day - s.LastMomVisitDay >= 14 && PhoneContacts.Mom != null
                && (FamilyState.Bond == Bond.Married || FamilyState.AtHome() > 0) && Random.value < 0.5f)
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
                FamilyText.Message(PhoneContacts.Mom, FamilyText.Pick(Visits, ref _lastVisit), Args.Us);
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
                s.InLawAskDay = day;
                s.InLawAsk = Random.Range(1, 4);
                InLawsSay(InLawAsks[s.InLawAsk]);
            }
        }

        // ---------------------------------------------------------------- the holiday dinner

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
            director.Run(director.Passage("reallife_family_passage_holiday", 3, () => EnergyHelper.SetCurrentHunger(100f), () => AfterDinner(which)));
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

        public static bool CanGoToMoms => On && FamilyState.S.MomDinnerDay == TimeHelper.CurrentDay && TimeHelper.CurrentHour >= 12;

        public static DialogEntry GoToMoms()
        {
            FamilySave s = FamilyState.S;
            FamilyDirector director = FamilyDirector.Instance;
            int which = s.MomDinnerWhich;
            s.MomDinnerDay = 0;
            director.WhenFree(() => director.Run(director.Passage("reallife_family_passage_moms", 4, () => EnergyHelper.SetCurrentHunger(100f), () => AfterDinner(which))));
            return FamilyTalk.Close("reallife_family_holiday_going", Args.Us.With(FamilyText.T(HolidayName(which))));
        }

        // ---------------------------------------------------------------- the in-laws

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

        // ---------------------------------------------------------------- next of kin (words only)

        public static string NextOfKin()
        {
            FamilySave s = FamilyState.S;
            if (!string.IsNullOrEmpty(s.NextOfKin))
            {
                return s.NextOfKin;
            }
            return FamilyState.Together ? Partner.Name : FamilyText.T("reallife-mom");
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
                if (FamilyState.Together)
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
