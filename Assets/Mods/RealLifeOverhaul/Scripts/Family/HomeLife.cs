using System;
using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>Where the partner is at an hour of a day.</summary>
    internal enum Whereabouts { Home, Asleep, Work, Out }

    /// <summary>Living together: the timetable, moving in, routines, their share of the rent, chores, support, evenings out, the partner at home.</summary>
    internal static partial class HomeLife
    {
        /// <summary>What a home's power and water come to a day (Utilities, through Wiring/UtilityLinks); null: none known.</summary>
        internal static Func<BuildingRegistration, float> Bills;

        private static readonly string[] Encourage =
        {
            "reallife_family_encourage_1", "reallife_family_encourage_2", "reallife_family_encourage_3", "reallife_family_encourage_4", "reallife_family_encourage_5"
        };
        private static readonly string[] SickCare = { "reallife_family_sickcare_1", "reallife_family_sickcare_2", "reallife_family_sickcare_3" };
        private static readonly string[] CookLines = { "reallife_family_cook_1", "reallife_family_cook_2", "reallife_family_cook_3", "reallife_family_cook_4" };
        private static readonly string[] WatchLines = { "reallife_family_watch_1", "reallife_family_watch_2", "reallife_family_watch_3", "reallife_family_watch_4" };
        private static readonly string[] DinnerLines = { "reallife_family_dinner_1", "reallife_family_dinner_2", "reallife_family_dinner_3", "reallife_family_dinner_4", "reallife_family_dinner_5" };
        private static readonly string[] StoodUp = { "reallife_family_stoodup_1", "reallife_family_stoodup_2", "reallife_family_stoodup_3" };
        private static readonly string[] Missing = { "reallife_family_neglect_1", "reallife_family_neglect_2", "reallife_family_neglect_3" };
        private static readonly string[] ChoreAsks =
        {
            "reallife_family_chore_1", "reallife_family_chore_2", "reallife_family_chore_3", "reallife_family_chore_4", "reallife_family_chore_5", "reallife_family_chore_6"
        };
        private static readonly string[] ChoreThanks = { "reallife_family_chore_thanks_1", "reallife_family_chore_thanks_2", "reallife_family_chore_thanks_3" };
        private static readonly string[] ChoreSighs = { "reallife_family_chore_sigh_1", "reallife_family_chore_sigh_2", "reallife_family_chore_sigh_3" };

        private static int _lastEncourage = -1, _lastSick = -1, _lastCook = -1, _lastWatch = -1, _lastDinner = -1, _lastStood = -1, _lastMissing = -1,
            _lastChore = -1, _lastThanks = -1, _lastSigh = -1;

        public static Whereabouts Where(int day, int hour)
        {
            FamilySave s = FamilyState.S;
            if (hour >= 23 || hour < 6)
            {
                return Whereabouts.Asleep;
            }
            if (s.NewParentUntil >= day)
            {
                return Whereabouts.Home;   // at home with the new arrival
            }
            DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(day);
            bool weekend = weekday == DayOfWeekOrdered.Saturday || weekday == DayOfWeekOrdered.Sunday;
            int from, to;
            bool works;
            switch (s.PJob)
            {
                case 0:
                case 7:   // nurse, paramedic: earlies, lates, a day off
                    int turn = day % 3;
                    works = turn != 2;
                    from = turn == 0 ? 6 : 14;
                    to = turn == 0 ? 14 : 22;
                    break;
                case 2:   // chef: evenings, Tuesday to Saturday
                    works = weekday != DayOfWeekOrdered.Sunday && weekday != DayOfWeekOrdered.Monday;
                    from = 14;
                    to = 23;
                    break;
                case 9:   // barista: mornings, six days
                    works = weekday != DayOfWeekOrdered.Sunday;
                    from = 6;
                    to = 13;
                    break;
                default:
                    works = !weekend;
                    from = 8;
                    to = 17;
                    break;
            }
            to = PartnerWorkRules.Until(from, to, PartnerWork.Work);   // half days once they have cut back (PartnerWork)
            works = works && !Partner.Retired;   // retired in People: at home
            if (FamilyState.Care == Care.Partner && FamilyState.AnyUnder(FamilyBalance.SchoolFrom))
            {
                to = Mathf.Min(to, from + 4);   // short days: they are the one at home with the children
            }
            if (works && hour >= from && hour < to)
            {
                return Whereabouts.Work;
            }
            // An evening of their own now and then: the gym, friends.
            if (hour >= 18 && hour < 21 && (day * 7 + (s.PSeed & 0xff)) % 5 == 0)
            {
                return Whereabouts.Out;
            }
            return Whereabouts.Home;
        }

        /// <summary>The partner moves in. Null if they did; otherwise why not (a locale key).</summary>
        public static string MoveIn(bool test)
        {
            FamilySave s = FamilyState.S;
            if (!FamilyState.Together)
            {
                return "reallife_family_no_partner";
            }
            if (s.LivesIn)
            {
                return "reallife_family_no_already_in";
            }
            BuildingRegistration home = FamilyPlaces.Home();
            if (home == null)
            {
                return "reallife_family_no_home";
            }
            if (!FamilyPlaces.HasRoom(home, 0))
            {
                return "reallife_family_no_room";
            }
            if (!test && FamilyState.Bond == Bond.Dating && !Ready())
            {
                return "reallife_family_no_not_yet";
            }
            s.LivesIn = true;
            s.MovedInDay = TimeHelper.CurrentDay;
            s.LastSharedDay = TimeHelper.CurrentDay;
            FamilyPlaces.MakeHome(home);
            FamilyState.Remember("reallife_family_memory_moved_in", Partner.Name);
            Partner.Tell(FamilyEvent.MovedIn, 5f, 5f);
            FamilyModifiers.Add(FamilyModifiers.Together);
            Relatives.MomSays("reallife_family_mom_moved_in");
            FamilyText.Good("reallife_family_moved_in", Args.Us);
            FamilyDirector.Logger?.Info("Family: the partner moved in.");
            return null;
        }

        public static bool Ready()
        {
            if (Partner.Kept(out PersonInfo p))
            {
                return p.Stage >= 5 || (p.Affection >= FamilyBalance.MoveInAffection && Partner.DaysTogether >= FamilyBalance.MoveInDaysTogether);
            }
            return Partner.Affection >= FamilyBalance.MoveInAffection && Partner.DaysTogether >= FamilyBalance.MoveInDaysTogether;
        }

        public static void MoveOut()
        {
            FamilyState.S.LivesIn = false;
            ReleasePartner();
            FamilyModifiers.Remove(FamilyModifiers.Together);
            Partner.Tell(FamilyEvent.MovedOut, 0f);
        }

        public static bool CanCook => FamilyState.S.LivesIn && Present && FamilyState.S.LastCookDay != TimeHelper.CurrentDay;
        public static bool CanWatch => FamilyState.S.LivesIn && Present && FamilyState.S.LastWatchDay != TimeHelper.CurrentDay;

        public static DialogEntry Cook()
        {
            FamilySave s = FamilyState.S;
            s.LastCookDay = TimeHelper.CurrentDay;
            Shared(FamilyBalance.HarmonyRoutine);
            if (FamilyBridges.Home != null && FamilyBridges.Home.CookTogether(Partner.Name))
            {
                return FamilyTalk.Close("reallife_family_cook_home");
            }
            EnergyHelper.SetCurrentHunger(Mathf.Min(100f, SaveGameManager.Current.Hunger + 30f));
            FamilyModifiers.Add(FamilyModifiers.Joy, 8);
            return FamilyTalk.Close(FamilyText.Pick(CookLines, ref _lastCook));
        }

        public static DialogEntry Watch()
        {
            FamilySave s = FamilyState.S;
            s.LastWatchDay = TimeHelper.CurrentDay;
            Shared(FamilyBalance.HarmonyRoutine);
            if (FamilyBridges.Home != null && FamilyBridges.Home.WatchTogether(Partner.Name))
            {
                return FamilyTalk.Close("reallife_family_watch_home");
            }
            FamilyModifiers.Add(FamilyModifiers.Joy, 8);
            return FamilyTalk.Close(FamilyText.Pick(WatchLines, ref _lastWatch));
        }

        /// <summary>Time spent together: the home is happier, and Social (or the default record) hears of it.</summary>
        public static void Shared(float harmony)
        {
            FamilyState.Harmony(harmony);
            FamilyState.S.LastSharedDay = TimeHelper.CurrentDay;
            FamilyState.S.LastTalkDay = TimeHelper.CurrentDay;
            Partner.Tell(FamilyEvent.SharedTime, harmony * 0.6f, harmony * 0.3f);
        }

        public static bool CanPlanDate => FamilyState.Together && FamilyState.FindErrand(Errands.Dinner) == null && FamilyState.S.LastDateDay != TimeHelper.CurrentDay;

        public static DialogEntry PlanDate()
        {
            string name = Partner.Name;
            if (FamilyBridges.Leisure != null && FamilyBridges.Leisure.DateNight(name, DateDone))
            {
                return FamilyTalk.Close("reallife_family_date_leisure");
            }
            if (Partner.Kept(out _) && FamilyBridges.Social.StartOuting("dinner", DateDone))
            {
                return FamilyTalk.Close("reallife_family_date_leisure");
            }
            if (!FamilyPlaces.Nearest(FamilyPlaces.Restaurant, PlayerHelper.GetCityPosition(), out Venue venue))
            {
                return FamilyTalk.Close("reallife_family_date_no_place");
            }
            int hour = TimeHelper.CurrentHour;
            int day = hour < 21 ? TimeHelper.CurrentDay : TimeHelper.CurrentDay + 1;
            FamilyState.S.Errands.Add(new Errand
            {
                Kind = Errands.Dinner, Day = day, FromHour = day == TimeHelper.CurrentDay ? Mathf.Max(hour, 12) : 17, ToHour = 23,
                Street = venue.Street, Number = venue.Number
            });
            if (day == TimeHelper.CurrentDay)
            {
                FamilyPlaces.Guide(venue);
            }
            return FamilyTalk.Close("reallife_family_date_set", Args.Us.At(venue.Name).On(FamilyText.When(day)).Cost(FamilyBalance.DinnerCost));
        }

        // An outing that Leisure or Social ran for us has ended.
        private static void DateDone(bool went)
        {
            FamilyDirector.Instance?.Guarded(() =>
            {
                if (went)
                {
                    AfterDate();
                }
                else
                {
                    StoodThemUp();
                }
            });
        }

        /// <summary>Dinner at the restaurant's door: the bill, two hours, and how the evening went.</summary>
        public static void Dinner(Errand errand)
        {
            FamilyDirector director = FamilyDirector.Instance;
            if (!FamilyTalk.Pay(FamilyBalance.DinnerCost, "reallife_transaction_family_dinner"))
            {
                return;   // the errand stays: come back with the money
            }
            FamilyState.MoneyOut(FamilyBalance.DinnerCost, 4);
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            director.Pass("reallife_family_passage_dinner", FamilyBalance.DinnerHours,
                () => EnergyHelper.SetCurrentHunger(100f),
                () =>
                {
                    AfterDate();
                    FamilyText.Toast(NotificationType.Success, FamilyText.Pick(DinnerLines, ref _lastDinner), Args.Us, 10f);
                });
        }

        private static void AfterDate()
        {
            FamilySave s = FamilyState.S;
            s.LastDateDay = TimeHelper.CurrentDay;
            s.Dates++;
            Shared(FamilyBalance.HarmonyDate);
            Partner.Tell(FamilyEvent.SharedTime, FamilyBalance.AffectionDate, 3f);
            FamilyModifiers.Add(FamilyModifiers.Joy);
            if (s.Dates == 1)
            {
                FamilyState.Remember("reallife_family_memory_first_date", Partner.Name);
            }
        }

        public static void StoodThemUp()
        {
            FamilySave s = FamilyState.S;
            s.BrokenPromises++;
            FamilyState.Harmony(FamilyBalance.HarmonyBrokenPromise);
            Partner.Tell(FamilyEvent.BrokenPromise, -6f, -6f);
            FamilyText.Message(FamilyPhone.PartnerContact, FamilyText.Pick(StoodUp, ref _lastStood), Args.Us);
        }

        /// <summary>Their share of the rent and bills, once a day in the evening, as a line of income.</summary>
        public static void Contribution(int day, int hour)
        {
            FamilySave s = FamilyState.S;
            if (!s.LivesIn || !FamilyState.Together || hour < 18 || s.LastContributionDay == day)
            {
                return;
            }
            s.LastContributionDay = day;
            BuildingRegistration home = FamilyPlaces.Home();
            if (home == null)
            {
                return;   // no home just now: nothing to share the cost of
            }
            // The home's day: the rent at today's prices and its power and water; with no bills known, an owned home's are a set sum.
            float rent = Mathf.Max(0f, home.RentPerDay) * Prices.Level / 1000f;
            Func<BuildingRegistration, float> bills = Bills;
            float cost = rent + (bills != null ? Mathf.Max(0f, bills(home)) : 0f);
            float amount = rent > 0f || bills != null ? Mathf.Min(cost * FamilyBalance.RentShare, Partner.Income * FamilyBalance.IncomeShare)
                : Mathf.Min(FamilyBalance.BillsCap, Partner.Income * 0.2f);
            if (FamilyState.Care == Care.Partner && FamilyState.AnyUnder(FamilyBalance.SchoolFrom))
            {
                amount *= 0.5f;   // short days at work
            }
            int paid = Mathf.Clamp(Mathf.RoundToInt(amount), 0, FamilyBalance.ContributionCap);
            if (paid > 0)
            {
                FamilyTalk.Move(paid, "reallife_transaction_family_contribution");
                FamilyState.MoneyIn(paid);
            }
        }

        /// <summary>A small thing asked of the player now and then; doing it is noticed, and so is never doing it.</summary>
        public static void Chore(int day, int hour)
        {
            FamilySave s = FamilyState.S;
            if (!s.LivesIn || !FamilyState.Together || !Settings.FamilyFriction || hour < 9 || hour > 21 || s.LastChoreDay == day)
            {
                return;
            }
            s.LastChoreDay = day;
            if (Random.value > 0.3f || FamilyDirector.Instance.HasQuestions)
            {
                return;
            }
            FamilyDirector.Instance.Ask("reallife_family_chore_header", FamilyText.Pick(ChoreAsks, ref _lastChore), Args.Us,
                "reallife_family_chore_yes", "reallife_family_chore_no",
                () =>
                {
                    if (SaveGameManager.Current.Energy > 25f)
                    {
                        EnergyHelper.SpentEnergyOnce(4f);
                    }
                    FamilyState.Harmony(FamilyBalance.HarmonyChore);
                    FamilyState.S.LastSharedDay = TimeHelper.CurrentDay;
                    FamilyText.Toast(NotificationType.Success, FamilyText.Pick(ChoreThanks, ref _lastThanks), Args.Us, 6f);
                },
                () =>
                {
                    FamilyState.Harmony(-2f);
                    FamilyText.Toast(NotificationType.Info, FamilyText.Pick(ChoreSighs, ref _lastSigh), Args.Us, 6f);
                });
        }

        /// <summary>After a bad day, or when the player is ill, the person at home notices.</summary>
        public static void Support(int day, int hour)
        {
            FamilySave s = FamilyState.S;
            if (!s.LivesIn || !FamilyState.Together || hour < 19 || s.LastSupportDay == day || Where(day, hour) != Whereabouts.Home || !LifeEvents.IsSafeForEvent())
            {
                return;
            }
            if (ModState.IsSick && s.LastSickCareDay != day)
            {
                s.LastSupportDay = day;
                s.LastSickCareDay = day;
                if (FamilyBridges.Home == null || !FamilyBridges.Home.CareWhenSick(Partner.Name))
                {
                    ModState.SickUntil = Mathf.Max(TimeHelper.NowInMinutes() + 60f, ModState.SickUntil - 8f * 60f);   // a night of being looked after
                    ModState.AddModifier(ModState.MoodUp);
                }
                FamilyState.Harmony(2f);
                FamilyText.Toast(NotificationType.Success, FamilyText.Pick(SickCare, ref _lastSick), Args.Us, 9f);
                return;
            }
            if (SaveGameManager.Current.Happiness < 35f)
            {
                s.LastSupportDay = day;
                ModState.AddModifier(ModState.MoodUp);
                FamilyText.Toast(NotificationType.Success, FamilyText.Pick(Encourage, ref _lastEncourage), Args.Us, 9f);
            }
        }

        // While Social supplies the partner, its calls, texts, outings and meetings with them count as talking too: they never
        // reach this file's own last talk. 0 when Social is off or has no partner.
        private static int SocialContactDay()
        {
            ISocialBridge social = FamilyBridges.Social;
            return social != null && social.Active && social.TryGetPartner(out PersonInfo partner) ? partner.LastContactDay : 0;
        }

        /// <summary>Once a day: a home is kept warm by time spent in it, and cools without.</summary>
        public static void Daily(int day)
        {
            FamilySave s = FamilyState.S;
            if (!FamilyState.Together)
            {
                return;
            }
            int quiet = day - TalkRules.LastTalked(s.LastTalkDay, s.LastSharedDay, SocialContactDay());
            if (quiet <= 1)
            {
                FamilyState.Harmony(0.8f);
            }
            else if (s.LivesIn)
            {
                FamilyState.Harmony(FamilyBalance.HarmonyDriftPerDay);
            }
            if (quiet >= FamilyBalance.NeglectDays)
            {
                FamilyState.Harmony(-1.5f);
                Partner.Tell(FamilyEvent.BrokenPromise, -FamilyBalance.AffectionNeglectPerDay, -0.5f);
                if (day - s.LastNeglectDay >= 3)
                {
                    s.LastNeglectDay = day;
                    FamilyText.Message(FamilyPhone.PartnerContact, FamilyText.Pick(Missing, ref _lastMissing), Args.Us);
                }
            }
            FamilyModifiers.Keep(FamilyModifiers.Together, s.LivesIn && FamilyState.Bond == Bond.Dating && s.Harmony >= 40f);
        }
    }
}
