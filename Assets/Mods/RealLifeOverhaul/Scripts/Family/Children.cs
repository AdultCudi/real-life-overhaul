using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>Children, up to three, from deciding together to leaving home; never shown walking: a baby is a pushchair, a cot and a sound.</summary>
    internal static partial class Children
    {
        private static readonly string[] Expecting =
        {
            "reallife_family_expecting_1", "reallife_family_expecting_2", "reallife_family_expecting_3", "reallife_family_expecting_4", "reallife_family_expecting_5",
            "reallife_family_expecting_6"
        };
        private static readonly string[] Waiting = { "reallife_family_adopt_wait_1", "reallife_family_adopt_wait_2", "reallife_family_adopt_wait_3" };
        private static readonly string[] Nights = { "reallife_family_night_1", "reallife_family_night_2", "reallife_family_night_3", "reallife_family_night_4" };
        private static readonly string[] GrownCalls = { "reallife_family_grown_call_1", "reallife_family_grown_call_2", "reallife_family_grown_call_3" };

        private static int _lastExpecting = -1, _lastWaiting = -1, _lastNight = -1, _lastGrown = -1;

        /// <summary>In-game days to a year of a child's age (the options page's weeks per year).</summary>
        public static int DaysPerYear => Mathf.Clamp(Settings.FamilyYearWeeks, 1, 12) * 7;

        private static float Share => FamilyState.Bond == Bond.Divorced ? 1f - FamilyBalance.SupportShare : 1f;

        public static bool CanTalk => FamilyState.Together && FamilyState.S.LivesIn && FamilyState.Process == ProcessKind.None
                                      && FamilyState.AtHome() < FamilyBalance.MaxChildren && Settings.FamilyChildren;

        public static DialogEntry Talk()
        {
            FamilySave s = FamilyState.S;
            int day = TimeHelper.CurrentDay;
            if (!FamilyPlaces.HasRoom(FamilyPlaces.Home(), 1))
            {
                return FamilyTalk.Close("reallife_family_child_no_room");
            }
            if (s.Harmony < 55f)
            {
                return FamilyTalk.Close("reallife_family_child_not_now");
            }
            if (!s.PWantsChildren)
            {
                // They need time, not persuading: a second, later talk in a happy home and they have come round.
                if (s.ChildTalkDay > 0 && day - s.ChildTalkDay >= 10 && s.Harmony >= 70f)
                {
                    s.PWantsChildren = true;
                }
                else
                {
                    if (s.ChildTalkDay == 0)
                    {
                        s.ChildTalkDay = day;
                    }
                    return FamilyTalk.Close("reallife_family_child_unsure");
                }
            }
            s.ChildTalkDay = day;
            return FamilyTalk.Either(FamilyText.Say(s.Children.Count == 0 ? "reallife_family_child_yes_first" : "reallife_family_child_yes_another"),
                "reallife_family_child_baby", () => FamilyTalk.Either(FamilyText.Say("reallife_family_child_who_carries"),
                    "reallife_family_child_carry_me", () => StartPregnancy(1), "reallife_family_child_carry_them", () => StartPregnancy(0)),
                "reallife_family_child_adopt", () => AdoptAge(0));
        }

        private static DialogEntry AdoptAge(int index)
        {
            int pick = (index % 3 + 3) % 3;
            string key = pick == 0 ? "reallife_family_adopt_age_baby" : pick == 1 ? "reallife_family_adopt_age_toddler" : "reallife_family_adopt_age_child";
            return FamilyTalk.Either(FamilyText.Say(key, Args.Us.Cost(FamilyBalance.AdoptionFee)), "reallife_family_plan_choose", () => StartAdoption(pick),
                "reallife_family_plan_other", () => AdoptAge(pick + 1));
        }

        private static DialogEntry StartPregnancy(int carrier)
        {
            BeginPregnancy(carrier);
            return FamilyTalk.Close(carrier == 1 ? "reallife_family_child_started_me" : "reallife_family_child_started_them", Args.Us.On(FamilyText.When(FamilyState.S.PDueDay)));
        }

        private static void BeginPregnancy(int carrier)
        {
            FamilySave s = FamilyState.S;
            int day = TimeHelper.CurrentDay;
            s.PKind = (int)ProcessKind.Pregnancy;
            s.PStartDay = day;
            s.PDueDay = day + Mathf.Max(6, Mathf.RoundToInt(FamilyBalance.PregnancyYears * DaysPerYear));
            s.PStep = 0;
            s.PCarrier = carrier;
            s.BirthHour = Random.Range(7, 18);
            s.LastFlavourDay = day;
            Shopping(true, true);
            FamilyState.Harmony(6f);
            FamilyState.Remember("reallife_family_memory_expecting", Partner.Name);
            Partner.Tell(FamilyEvent.Expecting, 6f, 4f);
            Relatives.MomSays("reallife_family_mom_expecting");
            Relatives.InLawsSay("reallife_family_inlaws_expecting");
            FamilyDirector.Logger?.Info($"Family: expecting, due day {s.PDueDay}.");
        }

        private static DialogEntry StartAdoption(int age)
        {
            BeginAdoption(age);
            string where = FamilyPlaces.Nearest(FamilyPlaces.Agency, PlayerHelper.GetCityPosition(), out Venue venue) ? venue.Name : FamilyText.T("reallife_family_place_unknown");
            return FamilyTalk.Close("reallife_family_adopt_started", Args.Us.At(where).Cost(FamilyBalance.AdoptionFee));
        }

        private static void BeginAdoption(int age)
        {
            FamilySave s = FamilyState.S;
            s.PKind = (int)ProcessKind.Adoption;
            s.PStartDay = TimeHelper.CurrentDay;
            s.PDueDay = 0;
            s.PStep = 0;
            s.PAdoptAge = Mathf.Clamp(age, 0, 2);
            s.LastFlavourDay = TimeHelper.CurrentDay;
            Errands.Add(Errands.AdoptFile, 0, 0, 24);
            Shopping(age < 2, age < 2);
            FamilyState.Remember("reallife_family_memory_adopting", Partner.Name);
            Partner.Tell(FamilyEvent.Expecting, 5f, 4f);
            Relatives.MomSays("reallife_family_mom_adopting");
        }

        // The nursery: what is not in the house yet goes on the shopping list.
        private static void Shopping(bool cot, bool pushchair)
        {
            FamilySave s = FamilyState.S;
            if (cot && !s.Cot)
            {
                Errands.Add(Errands.BuyCot, 0, 0, 24);
            }
            if (pushchair && !s.Pushchair)
            {
                Errands.Add(Errands.BuyPushchair, 0, 0, 24);
            }
            if (!s.Supplies)
            {
                Errands.Add(Errands.BuySupplies, 0, 0, 24);
            }
        }

        public static void Buy(Errand errand)
        {
            FamilySave s = FamilyState.S;
            int price = errand.Kind == Errands.BuyCot ? FamilyBalance.CotPrice : errand.Kind == Errands.BuyPushchair ? FamilyBalance.PushchairPrice : FamilyBalance.SuppliesPrice;
            if (!FamilyTalk.Pay(price, "reallife_transaction_family_nursery"))
            {
                return;
            }
            FamilyState.MoneyOut(price, 4);
            s.Errands.Remove(errand);
            string key;
            if (errand.Kind == Errands.BuyCot) { s.Cot = true; key = "reallife_family_bought_cot"; }
            else if (errand.Kind == Errands.BuyPushchair) { s.Pushchair = true; key = "reallife_family_bought_pushchair"; }
            else { s.Supplies = true; key = "reallife_family_bought_supplies"; }
            FamilyText.Good(key, Args.Us.Cost(price));
            if (s.Supplies && (s.Cot || FamilyState.FindErrand(Errands.BuyCot) == null) && (s.Pushchair || FamilyState.FindErrand(Errands.BuyPushchair) == null))
            {
                FamilyState.Harmony(3f);
                FamilyText.Toast(NotificationType.Success, "reallife_family_nursery_ready", Args.Us);
            }
        }

        public static void Daily(int day, int elapsed)
        {
            FamilySave s = FamilyState.S;
            if (!Settings.FamilyChildren)
            {
                return;
            }
            Timeline(day);
            if (s.Children.Count > 0)
            {
                Grow(elapsed / (float)DaysPerYear, true);
                for (int i = 0; i < s.Children.Count; i++)
                {
                    ChildRecord c = s.Children[i];
                    if (c.Grown)
                    {
                        continue;
                    }
                    float care = FamilyState.Care == Care.You ? 0.6f : FamilyState.Care == Care.Partner ? 0.2f : FamilyState.Care == Care.Nursery ? 0.1f : 0f;
                    c.Closeness = Mathf.Clamp(c.Closeness + (care - 0.15f) * elapsed, 0f, 100f);
                }
                if (FamilyState.Care == Care.You && FamilyState.AnyUnder(FamilyBalance.SchoolFrom) && SaveGameManager.Current.Energy > 35f && !AwayThisWeekend(day))
                {
                    EnergyHelper.SpentEnergyOnce(6f);   // the hours of the day spent on them
                }
                if (day - s.LastWeekDay >= 7)
                {
                    s.LastWeekDay = day;
                    Weekly();
                }
            }
            FamilyModifiers.Keep(FamilyModifiers.NewParent, s.NewParentUntil >= day);
        }

        // Where a pregnancy or an adoption has got to, and what it asks next.
        private static void Timeline(int day)
        {
            FamilySave s = FamilyState.S;
            if (FamilyState.Process == ProcessKind.None)
            {
                return;
            }
            if (!FamilyState.Together && FamilyState.Process == ProcessKind.Adoption)
            {
                ClearProcess();   // an adoption is for the two of them; a baby on the way comes all the same
                return;
            }
            if (FamilyState.Process == ProcessKind.Pregnancy)
            {
                int length = Mathf.Max(1, s.PDueDay - s.PStartDay);
                float done = (day - s.PStartDay) / (float)length;
                if (s.PStep == 0 && done >= 0.2f) { s.PStep = 1; Errands.Add(Errands.Scan, day, 9, 18).Number = 1; }
                else if (s.PStep == 1 && done >= 0.5f && FamilyState.FindErrand(Errands.Scan) == null) { s.PStep = 2; Errands.Add(Errands.Scan, day, 9, 18).Number = 2; }
                else if (s.PStep == 2 && done >= 0.75f && FamilyState.FindErrand(Errands.Scan) == null) { s.PStep = 3; Errands.Add(Errands.BirthClass, day, 9, 20); }
                if (day - s.LastFlavourDay >= 4 && day < s.PDueDay)
                {
                    s.LastFlavourDay = day;
                    string line = FamilyText.Pick(Expecting, ref _lastExpecting);
                    if (s.PCarrier == 1)
                    {
                        FamilyText.Toast(NotificationType.Info, "reallife_family_expecting_me", Args.Us.With(FamilyText.T(line, Args.Us)));
                    }
                    else
                    {
                        FamilyText.Message(FamilyPhone.PartnerContact, line, Args.Us);
                    }
                }
                return;
            }
            if (s.PStep == 2 && s.PDueDay > 0 && day >= s.PDueDay - 1)
            {
                s.PStep = 3;
                int meet = Mathf.Max(day + 1, s.PDueDay);
                s.PDueDay = meet;
                Errands.Add(Errands.AdoptMeet, meet, 9, 18);
                FamilyText.Message(FamilyPhone.FamilyContact, "reallife_family_adopt_matched", Args.Us.On(FamilyText.When(meet)));
            }
            else if (s.PStep >= 1 && s.PStep < 3 && day - s.LastFlavourDay >= 5)
            {
                s.LastFlavourDay = day;
                FamilyText.Message(FamilyPhone.FamilyContact, FamilyText.Pick(Waiting, ref _lastWaiting), Args.Us);
            }
        }

        public static void Hourly(int day, int hour)
        {
            FamilySave s = FamilyState.S;
            if (!Settings.FamilyChildren)
            {
                return;
            }
            if (FamilyState.Process == ProcessKind.Pregnancy && s.PStep < 10 && (day > s.PDueDay || (day == s.PDueDay && hour >= s.BirthHour)))
            {
                s.PStep = 10;
                FamilyState.DropErrand(Errands.Scan);
                FamilyState.DropErrand(Errands.BirthClass);
                int from = Mathf.Min(hour, 17);
                Errands.Add(Errands.Birth, day, from, Mathf.Min(24, from + 7));
                FamilyText.Toast(NotificationType.Warning, s.PCarrier == 1 ? "reallife_family_birth_time_me" : "reallife_family_birth_time", Args.Us, 14f);
                if (s.PCarrier != 1)
                {
                    FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_birth_time_text", Args.Us);
                }
            }
            if (s.Children.Count == 0)
            {
                return;
            }
            // A broken night.
            if (hour >= 3 && hour < 9 && s.LastNightDay != day && FamilyState.AnyUnder(0.5f) && !AwayThisWeekend(day))
            {
                s.LastNightDay = day;
                bool theirTurn = FamilyState.Together && s.LivesIn && day % 2 == 0;
                if (FamilyState.Care != Care.Nanny && !theirTurn)
                {
                    if (FamilyBridges.Home == null || !FamilyBridges.Home.BrokenNight(2))
                    {
                        FamilyModifiers.Add(FamilyModifiers.Tired);
                    }
                    ChildRecord baby = FamilyState.Smallest();
                    if (baby != null)
                    {
                        baby.Closeness = Mathf.Min(100f, baby.Closeness + 0.5f);
                        FamilyText.Toast(NotificationType.Info, FamilyText.Pick(Nights, ref _lastNight), Args.Us.Kid(baby.Name), 7f);
                    }
                }
            }
            // The school run, some mornings, for whoever wants to do it.
            DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(day);
            if (hour >= 7 && hour < 9 && s.LastRunDay != day && weekday != DayOfWeekOrdered.Saturday && weekday != DayOfWeekOrdered.Sunday)
            {
                s.LastRunDay = day;
                if (Random.value < 0.35f)
                {
                    for (int i = 0; i < s.Children.Count; i++)
                    {
                        ChildRecord c = s.Children[i];
                        if (!c.Grown && c.School > 0 && c.Age < 12f)
                        {
                            AtSchool(Errands.Add(Errands.SchoolRun, day, hour, 10, i), c);
                            break;
                        }
                    }
                }
            }
            // Away at the other parent's, and back.
            if (FamilyState.Bond == Bond.Divorced && s.Custody > 0 && weekday == DayOfWeekOrdered.Saturday && hour >= 9 && s.LastCustodyDay != day && FamilyState.AtHome() > 0)
            {
                s.LastCustodyDay = day;
                if (AwayThisWeekend(day))
                {
                    FamilyText.Toast(NotificationType.Info, "reallife_family_custody_away", new Args { Name = s.ExName });
                }
                else
                {
                    FamilyText.Toast(NotificationType.Info, "reallife_family_custody_home", new Args { Name = s.ExName });
                }
            }
        }

        /// <summary>True on a weekend the children spend with the other parent after a divorce.</summary>
        public static bool AwayThisWeekend(int day)
        {
            FamilySave s = FamilyState.S;
            if (FamilyState.Bond != Bond.Divorced || s.Custody <= 0)
            {
                return false;
            }
            DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(day);
            if (weekday != DayOfWeekOrdered.Saturday && weekday != DayOfWeekOrdered.Sunday)
            {
                return false;
            }
            return s.Custody >= 2 || ((day - 1) / 7) % 2 == 0;
        }

        /// <summary>
        /// What the next week of the children costs, whole money: childcare for those under school age (a nursery's or a
        /// nanny's), private school and pocket money, the parent's share of it. Read only: the week's charge (Weekly) and the
        /// calendar's day for it (Scripts/Wiring) both ask it.
        /// </summary>
        internal static int WeekCharges(out int care, out int school, out int pocket)
        {
            FamilySave s = FamilyState.S;
            int small = 0, privateSchool = 0, campusDay = 0;
            pocket = 0;
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                if (c.Grown)
                {
                    continue;
                }
                if (c.Age < FamilyBalance.SchoolFrom)
                {
                    small++;
                }
                if (c.School == 2)
                {
                    int fee = CampusFee(c);
                    if (fee >= 0)
                    {
                        campusDay += fee;   // at the schools' campus: its own fee, none at the player's own school
                    }
                    else
                    {
                        privateSchool++;
                    }
                }
                if (c.Age >= FamilyBalance.PocketFrom && c.Pocket > 0)
                {
                    pocket += FamilyBalance.PocketMoney[Mathf.Clamp(c.Pocket, 0, 3)];
                }
            }
            care = 0;
            if (small > 0 && FamilyState.Care == Care.Nursery)
            {
                care = FamilyBalance.NurseryPerDay * 7 * small;
            }
            if (small > 0 && FamilyState.Care == Care.Nanny)
            {
                care = FamilyBalance.NannyPerDay * 7;
            }
            care = Mathf.RoundToInt(care * Share);
            school = Mathf.RoundToInt((FamilyBalance.PrivateSchoolPerDay * privateSchool + campusDay) * 7 * Share);
            return care + school + pocket;
        }

        // A child's day of fees at the schools' campus, or -1 when they are not there (or the schools are not wired).
        private static int CampusFee(ChildRecord c)
        {
            ISchoolBridge school = FamilyBridges.School;
            return school != null ? school.FeeFor(c.Name) : -1;
        }

        /// <summary>How a child's school lifts their reports and a parents' evening: the campus's results, else a private school's twelve points.</summary>
        internal static float SchoolStanding(ChildRecord c)
        {
            ISchoolBridge school = FamilyBridges.School;
            float campus = school != null ? school.StandingOf(c.Name) : float.NaN;
            return !float.IsNaN(campus) ? campus : c.School == 2 ? 12f : 0f;
        }

        /// <summary>An errand of a child's schooling sent to the schools' campus when they are there; else the nearest school, as before.</summary>
        internal static void AtSchool(Errand errand, ChildRecord c)
        {
            ISchoolBridge school = FamilyBridges.School;
            if (errand != null && school != null && school.AddressFor(c.Name, out string street, out int number))
            {
                errand.Street = street;
                errand.Number = number;
            }
        }

        // Once a week: seven days of what the children cost.
        private static void Weekly()
        {
            FamilySave s = FamilyState.S;
            int care, school, pocket;
            WeekCharges(out care, out school, out pocket);
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                if (!c.Grown && c.Age >= FamilyBalance.PocketFrom && c.Pocket > 0)
                {
                    c.Closeness = Mathf.Min(100f, c.Closeness + 0.4f * c.Pocket);
                }
            }
            if (care > 0)
            {
                FamilyTalk.Move(-care, "reallife_transaction_family_childcare");
                FamilyState.MoneyOut(care, 0);
            }
            if (school > 0)
            {
                FamilyTalk.Move(-school, "reallife_transaction_family_school");
                FamilyState.MoneyOut(school, 1);
            }
            if (pocket > 0)
            {
                FamilyTalk.Move(-pocket, "reallife_transaction_family_pocket");
                FamilyState.MoneyOut(pocket, 2);
            }
            for (int i = 0; i < s.Children.Count; i++)
            {
                if (s.Children[i].Grown && Random.value < 0.25f)
                {
                    FamilyText.Message(FamilyPhone.FamilyContact, FamilyText.Pick(GrownCalls, ref _lastGrown), Args.Us.Kid(s.Children[i].Name));
                    break;
                }
            }
        }

        public static string CareName(Care care)
        {
            switch (care)
            {
                case Care.Partner: return "reallife_family_care_partner";
                case Care.Nursery: return "reallife_family_care_nursery";
                case Care.Nanny: return "reallife_family_care_nanny";
                default: return "reallife_family_care_you";
            }
        }

        public static DialogEntry CareStep(int index)
        {
            var care = (Care)((index % 4 + 4) % 4);
            if (care == Care.Partner && (!FamilyState.Together || !FamilyState.S.LivesIn))
            {
                return CareStep(index + 1);
            }
            string key = care == Care.You ? "reallife_family_care_offer_you" : care == Care.Partner ? "reallife_family_care_offer_partner"
                : care == Care.Nursery ? "reallife_family_care_offer_nursery" : "reallife_family_care_offer_nanny";
            int cost = care == Care.Nursery ? FamilyBalance.NurseryPerMonth : care == Care.Nanny ? FamilyBalance.NannyPerMonth : 0;
            return FamilyTalk.Either(FamilyText.Say(key, Args.Us.Cost(cost)), "reallife_family_plan_choose", () =>
            {
                FamilyState.S.CareChoice = (int)care;
                return FamilyTalk.Close("reallife_family_care_set", Args.Us.With(FamilyText.T(CareName(care))));
            }, "reallife_family_plan_other", () => CareStep(index + 1));
        }

        public static DialogEntry PocketStep(int child)
        {
            FamilySave s = FamilyState.S;
            ChildRecord c = s.Children[child];
            c.Pocket = (c.Pocket + 1) % FamilyBalance.PocketMoney.Length;
            return FamilyTalk.Either(FamilyText.Say("reallife_family_pocket_set", Args.Us.Kid(c.Name).Cost(FamilyBalance.PocketMoney[c.Pocket])),
                "reallife_family_pocket_change", () => PocketStep(child), "reallife_family_menu_back", () => FamilyPhone.FamilyMenu(0));
        }

        public static DialogEntry FundStep(int child)
        {
            FamilySave s = FamilyState.S;
            ChildRecord c = s.Children[child];
            if (!FamilyTalk.Pay(FamilyBalance.FundStep, "reallife_transaction_family_fund"))
            {
                return null;
            }
            c.Fund += FamilyBalance.FundStep;
            FamilyState.MoneyOut(FamilyBalance.FundStep, 3);
            return FamilyTalk.Either(FamilyText.Say("reallife_family_fund_added", Args.Us.Kid(c.Name).Cost(c.Fund)),
                "reallife_family_fund_more", () => FundStep(child), "reallife_family_menu_back", () => FamilyPhone.FamilyMenu(0));
        }

    }
}
