using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// Children. The game has no child-sized character (its people are adults of 18 to 80, and
    /// age only changes the skin of the face), and a scaled-down adult looks wrong, so no child
    /// is ever shown as a walking character: a baby or a toddler is a pushchair on a walk, a cot
    /// and a high chair at home and a sound now and then; a school-age child is a name, an age,
    /// reports, drawings, a school play to go to, a parents' evening. What is lived is whole all
    /// the same: deciding together, a pregnancy or an adoption with its appointments, a nursery
    /// to get ready from real shops, the birth or the arrival, broken nights, who looks after
    /// them, check-ups, first words and steps kept as dated memories, school, birthdays, pocket
    /// money, a college fund; up to three. They age on the options page's scale (one year of
    /// age per four in-game weeks by default).
    /// </summary>
    internal static class Children
    {
        private struct Milestone
        {
            public float Age;
            public int Bit;
            public string Memory;
            public Milestone(float age, int bit, string memory) { Age = age; Bit = 1 << bit; Memory = memory; }
        }

        private static readonly Milestone[] Milestones =
        {
            new Milestone(0.15f, 0, "reallife_family_memory_first_smile"),
            new Milestone(0.5f, 1, "reallife_family_memory_sat_up"),
            new Milestone(0.9f, 2, "reallife_family_memory_first_word"),
            new Milestone(1.1f, 3, "reallife_family_memory_first_steps"),
            new Milestone(2f, 4, "reallife_family_memory_first_sentence"),
            new Milestone(3f, 5, "reallife_family_memory_out_of_pushchair"),
            new Milestone(6f, 6, "reallife_family_memory_lost_tooth"),
            new Milestone(7f, 7, "reallife_family_memory_rode_bike"),
            new Milestone(10f, 8, "reallife_family_memory_double_digits"),
            new Milestone(13f, 9, "reallife_family_memory_teenager"),
            new Milestone(16f, 10, "reallife_family_memory_first_job")
        };

        private static readonly float[] CheckupAges = { 0.25f, 1f, 2f, 4f };

        private static readonly string[] Girls = { "Lily", "Maya", "Zoe", "Ava", "Nina", "Ruby", "Iris", "Ella", "Sara", "Mila", "Anna", "Cora" };
        private static readonly string[] Boys = { "Leo", "Noah", "Sam", "Eli", "Max", "Theo", "Finn", "Luca", "Owen", "Jude", "Kai", "Ben" };

        private static readonly string[] Expecting = { "reallife_family_expecting_1", "reallife_family_expecting_2", "reallife_family_expecting_3", "reallife_family_expecting_4", "reallife_family_expecting_5", "reallife_family_expecting_6" };
        private static readonly string[] Waiting = { "reallife_family_adopt_wait_1", "reallife_family_adopt_wait_2", "reallife_family_adopt_wait_3" };
        private static readonly string[] Nights = { "reallife_family_night_1", "reallife_family_night_2", "reallife_family_night_3", "reallife_family_night_4" };
        private static readonly string[] Drawings = { "reallife_family_drawing_1", "reallife_family_drawing_2", "reallife_family_drawing_3", "reallife_family_drawing_4", "reallife_family_drawing_5", "reallife_family_drawing_6", "reallife_family_drawing_7", "reallife_family_drawing_8" };
        private static readonly string[] Reports = { "reallife_family_report_great", "reallife_family_report_good", "reallife_family_report_fair", "reallife_family_report_struggling" };
        private static readonly string[] Runs = { "reallife_family_school_run_done_1", "reallife_family_school_run_done_2", "reallife_family_school_run_done_3" };
        private static readonly string[] Plays = { "reallife_family_school_play_done_1", "reallife_family_school_play_done_2", "reallife_family_school_play_done_3" };
        private static readonly string[] Evenings = { "reallife_family_parents_evening_great", "reallife_family_parents_evening_good", "reallife_family_parents_evening_fair" };
        private static readonly string[] GrownCalls = { "reallife_family_grown_call_1", "reallife_family_grown_call_2", "reallife_family_grown_call_3" };

        private static int _lastExpecting = -1, _lastWaiting = -1, _lastNight = -1, _lastDrawing = -1, _lastRun = -1, _lastPlay = -1, _lastGrown = -1;
        private static int _nameTries;

        /// <summary>Days of the game that make a year of a child's age (the options page: weeks per year).</summary>
        public static int DaysPerYear => Mathf.Clamp(Settings.FamilyYearWeeks, 1, 12) * 7;

        private static float Share => FamilyState.Bond == Bond.Divorced ? 1f - FamilyBalance.SupportShare : 1f;

        // ---------------------------------------------------------------- deciding together

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

        // The nursery: what is not in the house yet is on the shopping list.
        private static void Shopping(bool cot, bool pushchair)
        {
            FamilySave s = FamilyState.S;
            if (cot && !s.Cot) Errands.Add(Errands.BuyCot, 0, 0, 24);
            if (pushchair && !s.Pushchair) Errands.Add(Errands.BuyPushchair, 0, 0, 24);
            if (!s.Supplies) Errands.Add(Errands.BuySupplies, 0, 0, 24);
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

        // ---------------------------------------------------------------- the day and the hour

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
                    EnergyHelper.SpentEnergyOnce(6f);   // the hours of the day that went on them
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
                // An adoption is for the two of them: it lapses quietly if they part. (A baby on the way comes all the same.)
                ClearProcess();
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
            // Adoption.
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
            // It is time.
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
                            Errands.Add(Errands.SchoolRun, day, hour, 10, i);
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

        // Once a week: what the children cost. A quarter of each monthly figure, so a month of four weeks comes out right.
        private static void Weekly()
        {
            FamilySave s = FamilyState.S;
            int small = 0, privateSchool = 0, pocket = 0;
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                if (c.Grown)
                {
                    continue;
                }
                if (c.Age < FamilyBalance.SchoolFrom) small++;
                if (c.School == 2) privateSchool++;
                if (c.Age >= FamilyBalance.PocketFrom && c.Pocket > 0)
                {
                    pocket += FamilyBalance.PocketMoney[Mathf.Clamp(c.Pocket, 0, 3)];
                    c.Closeness = Mathf.Min(100f, c.Closeness + 0.4f * c.Pocket);
                }
            }
            int care = 0;
            if (small > 0 && FamilyState.Care == Care.Nursery) care = FamilyBalance.NurseryPerMonth / 4 * small;
            if (small > 0 && FamilyState.Care == Care.Nanny) care = FamilyBalance.NannyPerMonth / 4;
            care = Mathf.RoundToInt(care * Share);
            int school = Mathf.RoundToInt(FamilyBalance.PrivateSchoolPerMonth / 4 * privateSchool * Share);
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
            // Somebody who has left home rings now and then.
            for (int i = 0; i < s.Children.Count; i++)
            {
                if (s.Children[i].Grown && Random.value < 0.25f)
                {
                    FamilyText.Message(FamilyPhone.FamilyContact, FamilyText.Pick(GrownCalls, ref _lastGrown), Args.Us.Kid(s.Children[i].Name));
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- growing up

        /// <summary>Every child is so much older; what that brings is remembered and, with <paramref name="live"/>, asked about.</summary>
        public static void Grow(float years, bool live = false)
        {
            FamilySave s = FamilyState.S;
            int day = TimeHelper.CurrentDay;
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                if (c.Grown)
                {
                    continue;
                }
                float before = c.Age;
                c.Age += years;
                Args fill = Args.Us.Kid(c.Name);
                for (int m = 0; m < Milestones.Length; m++)
                {
                    if (c.Age >= Milestones[m].Age && before < Milestones[m].Age && (c.Seen & Milestones[m].Bit) == 0)
                    {
                        c.Seen |= Milestones[m].Bit;
                        FamilyState.Remember(Milestones[m].Memory, c.Name);
                        FamilyText.Good(Milestones[m].Memory, fill);
                        FamilyModifiers.Add(FamilyModifiers.Joy);
                        c.Closeness = Mathf.Min(100f, c.Closeness + 1f);
                    }
                }
                for (int k = 0; k < CheckupAges.Length; k++)
                {
                    if (c.Age >= CheckupAges[k] && c.LastCheckup < CheckupAges[k])
                    {
                        c.LastCheckup = CheckupAges[k];
                        if (live)
                        {
                            Errands.Add(Errands.Checkup, day + 1, 9, 18, i);
                            FamilyText.Toast(NotificationType.Info, "reallife_family_checkup_due", fill);
                        }
                        break;
                    }
                }
                if (before < FamilyBalance.SchoolFrom && c.Age >= FamilyBalance.SchoolFrom && c.School == 0)
                {
                    StartSchool(c, live);
                }
                int birthday = Mathf.FloorToInt(c.Age);
                if (birthday > c.Birthdays && birthday >= 1)
                {
                    c.Birthdays = birthday;
                    Birthday(c, i, birthday, live);
                }
                if (c.Age >= FamilyBalance.GrownUp)
                {
                    GrowUp(c);
                    continue;
                }
                if (!live)
                {
                    continue;
                }
                if (c.School > 0 && c.Age - c.LastReport >= 0.34f)
                {
                    c.LastReport = c.Age;
                    float standing = c.Closeness + (c.School == 2 ? 12f : 0f) + c.Pocket * 2f;
                    string grade = Reports[standing >= 80f ? 0 : standing >= 60f ? 1 : standing >= 40f ? 2 : 3];
                    FamilyText.Message(FamilyPhone.FamilyContact, grade, fill);
                }
                if (c.Age >= 3f && c.Age < 9f && c.Age - c.LastDrawing >= 0.4f)
                {
                    c.LastDrawing = c.Age;
                    FamilyText.Message(FamilyPhone.FamilyContact, FamilyText.Pick(Drawings, ref _lastDrawing), fill);
                }
                if (c.Age >= 6f && c.Age < 12f && c.Age - c.LastPlay >= 1f && FamilyState.FindErrand(Errands.SchoolPlay) == null)
                {
                    c.LastPlay = c.Age;
                    Errands.Add(Errands.SchoolPlay, day + 2, 17, 21, i);
                    FamilyText.Message(FamilyPhone.FamilyContact, "reallife_family_school_play_invite", fill.On(FamilyText.When(day + 2)));
                }
                if (c.School > 0 && c.Age - c.LastEvening >= 1f && FamilyState.FindErrand(Errands.ParentsEvening) == null)
                {
                    c.LastEvening = c.Age;
                    Errands.Add(Errands.ParentsEvening, day + 3, 16, 20, i);
                    FamilyText.Message(FamilyPhone.FamilyContact, "reallife_family_parents_evening_invite", fill.On(FamilyText.When(day + 3)));
                }
            }
        }

        private static void StartSchool(ChildRecord c, bool live)
        {
            c.School = 1;
            c.LastReport = c.Age;
            c.LastEvening = c.Age - 0.6f;
            FamilyState.Remember("reallife_family_memory_first_school_day", c.Name);
            FamilyText.Good("reallife_family_memory_first_school_day", Args.Us.Kid(c.Name));
            if (!live)
            {
                return;
            }
            FamilyDirector.Instance.Ask("reallife_family_school_header", "reallife_family_school_body", Args.Us.Kid(c.Name).Cost(FamilyBalance.PrivateSchoolPerMonth),
                "reallife_family_school_public", "reallife_family_school_private", null, () =>
                {
                    c.School = 2;
                    FamilyText.Toast(NotificationType.Info, "reallife_family_school_private_chosen", Args.Us.Kid(c.Name).Cost(FamilyBalance.PrivateSchoolPerMonth));
                });
        }

        private static void Birthday(ChildRecord c, int index, int years, bool live)
        {
            Args fill = Args.Us.Kid(c.Name).Count(years);
            FamilyState.Remember("reallife_family_memory_birthday", c.Name);
            if (!live)
            {
                FamilyText.Toast(NotificationType.Success, "reallife_family_birthday_quiet", fill);
                return;
            }
            Relatives.MomGift(40, "reallife_family_mom_birthday", fill);
            FamilyDirector director = FamilyDirector.Instance;
            director.Ask("reallife_family_birthday_header", "reallife_family_birthday_body", fill, "reallife_family_birthday_party", "reallife_family_birthday_cake",
                () => director.Ask("reallife_family_birthday_header", "reallife_family_birthday_where", fill.Cost(FamilyBalance.PartyHomePrice).With(FamilyText.Money(FamilyBalance.PartyVenuePrice)),
                    "reallife_family_birthday_home", "reallife_family_birthday_venue",
                    () =>
                    {
                        if (!FamilyTalk.Pay(FamilyBalance.PartyHomePrice, "reallife_transaction_family_party"))
                        {
                            Cake(c, fill);
                            return;
                        }
                        FamilyState.MoneyOut(FamilyBalance.PartyHomePrice, 4);
                        c.Closeness = Mathf.Min(100f, c.Closeness + 5f);
                        FamilyModifiers.Add(FamilyModifiers.Joy);
                        FamilyText.Good("reallife_family_birthday_home_done", fill);
                    },
                    () =>
                    {
                        if (!FamilyTalk.Pay(FamilyBalance.PartyVenuePrice, "reallife_transaction_family_party"))
                        {
                            Cake(c, fill);
                            return;
                        }
                        FamilyState.MoneyOut(FamilyBalance.PartyVenuePrice, 4);
                        if (FamilyBridges.Leisure != null && FamilyBridges.Leisure.FamilyOuting("birthday", c.Name, went => director.Guarded(() => PartyOver(c, went))))
                        {
                            return;
                        }
                        int day = TimeHelper.CurrentHour < 17 ? TimeHelper.CurrentDay : TimeHelper.CurrentDay + 1;
                        Errands.Add(Errands.Party, day, 11, 20, index);
                        FamilyText.Toast(NotificationType.Info, "reallife_family_birthday_venue_booked", fill.On(FamilyText.When(day)));
                    }),
                () => Cake(c, fill));
        }

        private static void Cake(ChildRecord c, Args fill)
        {
            c.Closeness = Mathf.Min(100f, c.Closeness + 2f);
            FamilyText.Toast(NotificationType.Success, "reallife_family_birthday_quiet", fill);
        }

        private static void GrowUp(ChildRecord c)
        {
            c.Grown = true;
            c.Age = FamilyBalance.GrownUp;
            FamilyState.Remember("reallife_family_memory_grown", c.Name);
            string key = c.Fund >= FamilyBalance.FundFull ? "reallife_family_grown_fund_full" : c.Fund > 0 ? "reallife_family_grown_fund_part" : "reallife_family_grown_fund_none";
            FamilyText.Good(key, Args.Us.Kid(c.Name).Cost(c.Fund));
            FamilyModifiers.Add(FamilyModifiers.Joy, 24);
        }

        // ---------------------------------------------------------------- appointments

        public static void Appointment(Errand errand)
        {
            FamilySave s = FamilyState.S;
            FamilyDirector director = FamilyDirector.Instance;
            bool scan = errand.Kind == Errands.Scan;
            if (scan && !FamilyTalk.Pay(FamilyBalance.ScanPrice, "reallife_transaction_family_clinic"))
            {
                return;
            }
            if (scan)
            {
                FamilyState.MoneyOut(FamilyBalance.ScanPrice, 4);
            }
            int which = errand.Number;
            s.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            director.Run(director.Passage(scan ? "reallife_family_passage_scan" : "reallife_family_passage_class", scan ? 1 : 2, null, () =>
            {
                if (FamilyBridges.Home != null)
                {
                    FamilyBridges.Home.HealthVisit(scan ? "reallife_family_errand_scan" : "reallife_family_errand_class");
                }
                HomeLife.Shared(4f);
                string key = !scan ? "reallife_family_class_done" : which >= 2 ? "reallife_family_scan_done_2" : "reallife_family_scan_done_1";
                if (scan && which < 2)
                {
                    FamilyState.Remember("reallife_family_memory_first_scan", Partner.Name);
                }
                FamilyText.Good(key, Args.Us);
            }));
        }

        public static void AppointmentMissed(Errand errand)
        {
            FamilySave s = FamilyState.S;
            if (s.PCarrier == 1)
            {
                FamilyText.Toast(NotificationType.Info, "reallife_family_appointment_missed_me", Args.Us);   // rebooked without fuss: it was the player's own
                return;
            }
            s.BrokenPromises++;
            FamilyState.Harmony(-4f);
            Partner.Tell(FamilyEvent.BrokenPromise, -3f, -3f);
            FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_appointment_missed", Args.Us);
        }

        public static void Checkup(Errand errand)
        {
            FamilySave s = FamilyState.S;
            FamilyDirector director = FamilyDirector.Instance;
            int price = Mathf.RoundToInt(FamilyBalance.CheckupPrice * Share);
            if (!FamilyTalk.Pay(price, "reallife_transaction_family_clinic"))
            {
                return;
            }
            FamilyState.MoneyOut(price, 4);
            ChildRecord c = errand.Child >= 0 && errand.Child < s.Children.Count ? s.Children[errand.Child] : null;
            s.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            director.Run(director.Passage("reallife_family_passage_checkup", 1, null, () =>
            {
                if (c == null)
                {
                    return;
                }
                if (FamilyBridges.Home != null)
                {
                    FamilyBridges.Home.HealthVisit("reallife_family_errand_checkup");
                }
                c.Closeness = Mathf.Min(100f, c.Closeness + 2f);
                FamilyText.Good("reallife_family_checkup_done", Args.Us.Kid(c.Name).With(FamilyText.Age(c.Age)));
            }));
        }

        public static void CheckupMissed(Errand errand)
        {
            FamilySave s = FamilyState.S;
            ChildRecord c = errand.Child >= 0 && errand.Child < s.Children.Count ? s.Children[errand.Child] : null;
            if (c == null)
            {
                return;
            }
            if (FamilyState.Together)
            {
                c.Closeness = Mathf.Max(0f, c.Closeness - 1f);
                FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_checkup_missed", Args.Us.Kid(c.Name));
            }
            else
            {
                Errands.Add(Errands.Checkup, TimeHelper.CurrentDay + 1, 9, 18, errand.Child);   // nobody else to take them: tomorrow, then
            }
        }

        // ---------------------------------------------------------------- birth and arrival

        public static void Birth(Errand errand)
        {
            FamilyDirector director = FamilyDirector.Instance;
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            Deliver(director, true);
        }

        private static void Deliver(FamilyDirector director, bool present)
        {
            int price = FamilyBalance.BirthPrice;
            FamilyTalk.Move(-price, "reallife_transaction_family_clinic");
            FamilyState.MoneyOut(price, 4);
            if (!director.Run(director.Passage("reallife_family_passage_birth", 8, null, () => Arrive(false, 0f, present))))
            {
                Arrive(false, 0f, present);
            }
        }

        public static void BirthMissed(Errand errand)
        {
            FamilyDirector director = FamilyDirector.Instance;
            if (FamilyState.S.PCarrier == 1)
            {
                // The player's own: an ambulance sees to it, wherever they were.
                director.WhenFree(() => Deliver(director, true));
                return;
            }
            FamilyTalk.Move(-FamilyBalance.BirthPrice, "reallife_transaction_family_clinic");
            FamilyState.MoneyOut(FamilyBalance.BirthPrice, 4);
            Arrive(false, 0f, false);
        }

        /// <summary>Somebody new in the family: born, or brought home.</summary>
        private static void Arrive(bool adopted, float age, bool present)
        {
            FamilySave s = FamilyState.S;
            if (s.Children.Count >= FamilyBalance.MaxChildren)
            {
                ClearProcess();
                return;
            }
            bool girl = Random.value < 0.5f;
            var child = new ChildRecord
            {
                Girl = girl, Name = PickName(girl), Age = age, Adopted = adopted, ArrivedDay = TimeHelper.CurrentDay,
                Closeness = present ? 65f : 55f, Birthdays = Mathf.FloorToInt(age), LastCheckup = age >= 0.25f ? age : -1f,
                LastPlay = age, LastEvening = age, LastDrawing = age, LastReport = age
            };
            for (int m = 0; m < Milestones.Length; m++)
            {
                if (Milestones[m].Age <= age)
                {
                    child.Seen |= Milestones[m].Bit;   // before they came: not ours to remember
                }
            }
            if (age >= FamilyBalance.SchoolFrom)
            {
                child.School = 1;
            }
            s.Children.Add(child);
            ClearProcess();
            s.NewParentUntil = TimeHelper.CurrentDay + 3;
            Args fill = Args.Us.Kid(child.Name);
            FamilyState.Remember(adopted ? "reallife_family_memory_adopted" : "reallife_family_memory_born", child.Name);
            FamilyModifiers.Add(FamilyModifiers.NewParent);
            FamilyState.Harmony(present ? 10f : -6f);
            Partner.Tell(FamilyEvent.ChildArrived, present ? 8f : -2f, present ? 6f : -4f);
            Relatives.MomGift(250, adopted ? "reallife_family_mom_adopted" : "reallife_family_mom_born", fill);
            Relatives.InLawsSay("reallife_family_inlaws_grandchild");
            FamilyDirector.Logger?.Info($"Family: a child arrived ({(adopted ? "adopted" : "born")}, present {present}); {s.Children.Count} in all.");
            if (!present)
            {
                if (FamilyState.Together)
                {
                    s.BrokenPromises++;
                }
                FamilyText.Message(FamilyPhone.PartnerContact, "reallife_family_birth_missed", fill);
                return;
            }
            _nameTries = 0;
            Naming(child, adopted && age >= 1f);
        }

        // A name, with as many second thoughts as wanted (a child who already answers to theirs keeps it).
        private static void Naming(ChildRecord child, bool hasName)
        {
            FamilyDirector director = FamilyDirector.Instance;
            Args fill = Args.Us.Kid(child.Name);
            if (hasName)
            {
                director.WhenFree(() => FamilyText.Good("reallife_family_adopt_home", fill.With(FamilyText.Age(child.Age))));
                return;
            }
            director.Ask(child.Adopted ? "reallife_family_name_header_adopted" : "reallife_family_name_header", child.Girl ? "reallife_family_name_body_girl" : "reallife_family_name_body_boy", fill,
                "reallife_family_name_yes", "reallife_family_name_other",
                () => FamilyText.Good(child.Adopted ? "reallife_family_adopt_home" : "reallife_family_born", Args.Us.Kid(child.Name).With(FamilyText.Age(child.Age))),
                () =>
                {
                    _nameTries++;
                    child.Name = PickName(child.Girl);
                    if (_nameTries >= 8)
                    {
                        FamilyText.Good(child.Adopted ? "reallife_family_adopt_home" : "reallife_family_born", Args.Us.Kid(child.Name).With(FamilyText.Age(child.Age)));
                        return;
                    }
                    Naming(child, false);
                });
        }

        private static string PickName(bool girl)
        {
            FamilySave s = FamilyState.S;
            string[] names = girl ? Girls : Boys;
            int start = Random.Range(0, names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[(start + i) % names.Length];
                bool used = name == s.PName;
                for (int c = 0; c < s.Children.Count && !used; c++)
                {
                    used = s.Children[c].Name == name;
                }
                if (!used)
                {
                    return name;
                }
            }
            return names[start];
        }

        private static void ClearProcess()
        {
            FamilySave s = FamilyState.S;
            s.PKind = 0;
            s.PStep = 0;
            s.PDueDay = 0;
            FamilyState.DropErrand(Errands.Scan);
            FamilyState.DropErrand(Errands.BirthClass);
            FamilyState.DropErrand(Errands.Birth);
            FamilyState.DropErrand(Errands.AdoptFile);
            FamilyState.DropErrand(Errands.HomeVisit);
            FamilyState.DropErrand(Errands.AdoptMeet);
        }

        // ---------------------------------------------------------------- adoption

        public static void AdoptFile(Errand errand)
        {
            FamilySave s = FamilyState.S;
            if (!FamilyTalk.Pay(FamilyBalance.AdoptionFee, "reallife_transaction_family_adoption"))
            {
                return;
            }
            FamilyState.MoneyOut(FamilyBalance.AdoptionFee, 4);
            s.Errands.Remove(errand);
            Filed();
            FamilyText.Good("reallife_family_adopt_filed", Args.Us.On(FamilyText.When(FamilyState.FindErrand(Errands.HomeVisit).Day)));
        }

        private static void Filed()
        {
            FamilySave s = FamilyState.S;
            int day = TimeHelper.CurrentDay;
            int length = Mathf.Max(6, Mathf.RoundToInt(FamilyBalance.AdoptionYears * DaysPerYear));
            s.PStep = 1;
            s.PStartDay = day;
            s.PDueDay = day + length;
            Errands.Add(Errands.HomeVisit, day + Mathf.Max(2, length / 3), 10, 19);
        }

        public static void HomeVisit(Errand errand)
        {
            FamilyDirector director = FamilyDirector.Instance;
            FamilyState.S.Errands.Remove(errand);
            director.Run(director.Passage("reallife_family_passage_home_visit", 1, null, () =>
            {
                FamilyState.S.PStep = 2;
                HomeLife.Shared(3f);
                FamilyText.Good("reallife_family_adopt_visit_done", Args.Us);
            }));
        }

        public static void HomeVisitMissed(Errand errand)
        {
            FamilySave s = FamilyState.S;
            int day = TimeHelper.CurrentDay + 2;
            s.PDueDay += 2;
            Errands.Add(Errands.HomeVisit, day, 10, 19);
            FamilyText.Message(FamilyPhone.FamilyContact, "reallife_family_adopt_visit_moved", Args.Us.On(FamilyText.When(day)));
        }

        public static void AdoptArrive(Errand errand)
        {
            FamilySave s = FamilyState.S;
            FamilyDirector director = FamilyDirector.Instance;
            s.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            float age = s.PAdoptAge == 0 ? 0.3f : s.PAdoptAge == 1 ? 2f : 6f;
            if (!director.Run(director.Passage("reallife_family_passage_adopt", 3, null, () => Arrive(true, age, true))))
            {
                Arrive(true, age, true);
            }
        }

        public static void AdoptMeetMissed(Errand errand)
        {
            FamilySave s = FamilyState.S;
            int day = TimeHelper.CurrentDay + 1;
            s.PDueDay = day;
            Errands.Add(Errands.AdoptMeet, day, 9, 18);
            FamilyText.Message(FamilyPhone.FamilyContact, "reallife_family_adopt_meet_moved", Args.Us.On(FamilyText.When(day)));
        }

        // ---------------------------------------------------------------- school

        private static ChildRecord Of(Errand errand)
        {
            FamilySave s = FamilyState.S;
            return errand.Child >= 0 && errand.Child < s.Children.Count ? s.Children[errand.Child] : null;
        }

        public static void SchoolRun(Errand errand)
        {
            ChildRecord c = Of(errand);
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            if (c == null)
            {
                return;
            }
            c.Closeness = Mathf.Min(100f, c.Closeness + 3f);
            ModState.AddModifier(ModState.MoodUp);
            FamilyText.Good(FamilyText.Pick(Runs, ref _lastRun), Args.Us.Kid(c.Name));
        }

        public static void SchoolRunMissed(Errand errand)
        {
            // Nothing is owed: the bus took them. Said only once in a while, so it never nags.
        }

        public static void ParentsEvening(Errand errand)
        {
            ChildRecord c = Of(errand);
            FamilyDirector director = FamilyDirector.Instance;
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            if (c == null)
            {
                return;
            }
            director.Run(director.Passage("reallife_family_passage_parents_evening", 1, null, () =>
            {
                c.Closeness = Mathf.Min(100f, c.Closeness + 3f);
                float standing = c.Closeness + (c.School == 2 ? 12f : 0f);
                FamilyText.Good(Evenings[standing >= 78f ? 0 : standing >= 55f ? 1 : 2], Args.Us.Kid(c.Name));
            }));
        }

        public static void SchoolPlay(Errand errand)
        {
            ChildRecord c = Of(errand);
            FamilyDirector director = FamilyDirector.Instance;
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            if (c == null)
            {
                return;
            }
            if (FamilyBridges.Leisure != null && FamilyBridges.Leisure.FamilyOuting("school_play", c.Name, went => director.Guarded(() => PlayOver(c, went))))
            {
                return;
            }
            director.Run(director.Passage("reallife_family_passage_school_play", 2, null, () => PlayOver(c, true)));
        }

        private static void PlayOver(ChildRecord c, bool went)
        {
            if (!went)
            {
                c.Closeness = Mathf.Max(0f, c.Closeness - 3f);
                return;
            }
            c.Closeness = Mathf.Min(100f, c.Closeness + 6f);
            FamilyModifiers.Add(FamilyModifiers.Joy);
            FamilyState.Remember("reallife_family_memory_school_play", c.Name);
            FamilyText.Good(FamilyText.Pick(Plays, ref _lastPlay), Args.Us.Kid(c.Name));
        }

        public static void SchoolEventMissed(Errand errand)
        {
            ChildRecord c = Of(errand);
            if (c == null)
            {
                return;
            }
            c.Closeness = Mathf.Max(0f, c.Closeness - 3f);
            FamilyText.Message(FamilyPhone.FamilyContact, errand.Kind == Errands.SchoolPlay ? "reallife_family_school_play_missed" : "reallife_family_parents_evening_missed", Args.Us.Kid(c.Name));
        }

        public static void VenueParty(Errand errand)
        {
            ChildRecord c = Of(errand);
            FamilyDirector director = FamilyDirector.Instance;
            FamilyState.S.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            if (c == null)
            {
                return;
            }
            director.Run(director.Passage("reallife_family_passage_party", 3, () => EnergyHelper.SetCurrentHunger(Mathf.Max(SaveGameManager.Current.Hunger, 80f)), () => PartyOver(c, true)));
        }

        private static void PartyOver(ChildRecord c, bool went)
        {
            if (!went)
            {
                return;
            }
            c.Closeness = Mathf.Min(100f, c.Closeness + 8f);
            FamilyModifiers.Add(FamilyModifiers.Joy);
            FamilyState.Remember("reallife_family_memory_party", c.Name);
            FamilyText.Good("reallife_family_birthday_venue_done", Args.Us.Kid(c.Name));
        }

        public static void PartyMissed(Errand errand)
        {
            ChildRecord c = Of(errand);
            if (c == null)
            {
                return;
            }
            c.Closeness = Mathf.Max(0f, c.Closeness - 4f);
            FamilyText.Toast(NotificationType.Info, "reallife_family_birthday_venue_missed", Args.Us.Kid(c.Name));
        }

        // ---------------------------------------------------------------- choices from the phone

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

        // ---------------------------------------------------------------- the test buttons

        /// <summary>Starts a pregnancy (or an adoption), or moves the one in progress on by one step. Null if it did; else why not.</summary>
        public static string TestAdvance(bool adoption)
        {
            FamilySave s = FamilyState.S;
            FamilyDirector director = FamilyDirector.Instance;
            int day = TimeHelper.CurrentDay;
            if (!Settings.FamilyChildren)
            {
                return "reallife_family_no_children_off";
            }
            if (FamilyState.Process == ProcessKind.None)
            {
                if (FamilyState.AtHome() >= FamilyBalance.MaxChildren)
                {
                    return "reallife_family_no_three";
                }
                if (!s.LivesIn)
                {
                    string why = HomeLife.MoveIn(true);
                    if (why != null)
                    {
                        return why;
                    }
                }
                s.PWantsChildren = true;
                if (adoption)
                {
                    BeginAdoption(0);
                    FamilyText.Toast(NotificationType.Info, "reallife_family_test_adopt_started", Args.Us);
                }
                else
                {
                    BeginPregnancy(0);
                    FamilyText.Toast(NotificationType.Info, "reallife_family_test_child_started", Args.Us);
                }
                return null;
            }
            if (director.Busy)
            {
                return "reallife_family_no_busy";
            }
            if (FamilyState.Process == ProcessKind.Pregnancy)
            {
                Errand next = FamilyState.FindErrand(Errands.Birth) ?? FamilyState.FindErrand(Errands.Scan) ?? FamilyState.FindErrand(Errands.BirthClass);
                if (next != null && next.Kind == Errands.Birth)
                {
                    Birth(next);
                }
                else if (next != null)
                {
                    // The appointment itself, wherever the player stands, with nothing charged twice.
                    s.Errands.Remove(next);
                    FamilyText.Good(next.Kind == Errands.BirthClass ? "reallife_family_class_done" : next.Number >= 2 ? "reallife_family_scan_done_2" : "reallife_family_scan_done_1", Args.Us);
                }
                else if (s.PStep < 3)
                {
                    // On to the next appointment: the calendar is moved so that it falls today.
                    int length = Mathf.Max(1, s.PDueDay - s.PStartDay);
                    float target = s.PStep == 0 ? 0.2f : s.PStep == 1 ? 0.5f : 0.75f;
                    int shift = Mathf.CeilToInt(target * length) - (day - s.PStartDay);
                    s.PStartDay -= Mathf.Max(0, shift);
                    s.PDueDay -= Mathf.Max(0, shift);
                    Timeline(day);
                    FamilyText.Toast(NotificationType.Info, "reallife_family_test_child_step", Args.Us);
                }
                else
                {
                    s.PDueDay = day;
                    s.BirthHour = 0;
                    Hourly(day, TimeHelper.CurrentHour);
                }
                return null;
            }
            // Adoption.
            Errand step = FamilyState.FindErrand(Errands.AdoptFile);
            if (step != null)
            {
                s.Errands.Remove(step);
                Filed();
                FamilyText.Toast(NotificationType.Info, "reallife_family_test_child_step", Args.Us);
                return null;
            }
            step = FamilyState.FindErrand(Errands.HomeVisit);
            if (step != null)
            {
                s.Errands.Remove(step);
                s.PStep = 2;
                s.PDueDay = day + 1;
                Timeline(day);
                return null;
            }
            step = FamilyState.FindErrand(Errands.AdoptMeet);
            if (step != null)
            {
                AdoptArrive(step);
                return null;
            }
            s.PStep = 2;
            s.PDueDay = day + 1;
            Timeline(day);
            return null;
        }
    }

    /// <summary>
    /// A pushchair walk: the player pushes the pushchair (the modelled prop, in front of them at
    /// the reach the pushing clip was made for, their arms on its handle over the game's own
    /// walk), a couple of hundred metres wherever they like; the partner, when they are home
    /// and free, walks along. Good for the mood, the child and the two of them. The pushchair
    /// is folded away the moment the walk ends, however it ends.
    /// </summary>
    internal sealed class WalkScene : FamilyScene
    {
        private const float HandleAhead = 0.88f;   // the pushchair's middle ahead of the feet: its handle is then where push_cart has the hands

        private static readonly string[] Sights = { "reallife_family_walk_sight_1", "reallife_family_walk_sight_2", "reallife_family_walk_sight_3", "reallife_family_walk_sight_4", "reallife_family_walk_sight_5", "reallife_family_walk_sight_6" };
        private static int _lastSight = -1;

        private readonly bool _test;
        private FamilyDirector _d;
        private Transform _chair;
        private CityPerson _partner;
        private ChildRecord _child;
        private Vector3 _last;
        private float _left;
        private float _nextSight;
        private float _nextFollow;
        private string _shownLeft;
        private int _shownTens = -1;
        private bool _parting;
        private float _partBy;
        private bool _finished;

        private readonly System.Action _endByKey;

        public WalkScene(bool test)
        {
            _test = test;
            _endByKey = EndByKey;
        }

        public override string Name => "the pushchair walk";

        private void EndByKey()
        {
            Finish(Time.time, _left < FamilyBalance.WalkMetres * 0.5f);
        }

        public override bool Begin(FamilyDirector director, out string refusal)
        {
            _d = director;
            FamilySave s = FamilyState.S;
            refusal = null;
            _child = FamilyState.Smallest();
            if (_child == null)
            {
                refusal = "reallife_family_no_baby";
                return false;
            }
            if (Children.AwayThisWeekend(TimeHelper.CurrentDay))
            {
                refusal = "reallife_family_no_away";
                return false;
            }
            if (!s.Pushchair)
            {
                if (!_test)
                {
                    refusal = "reallife_family_no_pushchair";
                    return false;
                }
                s.Pushchair = true;
                FamilyState.DropErrand(Errands.BuyPushchair);
            }
            if (BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle)
            {
                refusal = "reallife_family_no_walk_indoors";
                return false;
            }
            PlayerController controller = PlayerHelper.PlayerController;
            if (controller == null || controller.Character == null || !ClipLibrary.Has(SceneClips.PushCart))
            {
                refusal = "reallife_family_no_prop";
                return false;
            }
            _chair = director.Cast.Props.Take("pushchair");
            if (_chair == null)
            {
                refusal = "reallife_family_no_prop";
                return false;
            }
            director.Body.Bind(controller);
            Transform body = controller.Character.transform;
            Vector3 scale = body.lossyScale;
            if (Mathf.Abs(scale.x) < 0.001f || Mathf.Abs(scale.y) < 0.001f || Mathf.Abs(scale.z) < 0.001f || !director.Body.Over(SceneClips.PushCart, BodyMask.Arms, 0.3f))
            {
                refusal = "reallife_family_no_prop";
                return false;
            }
            _chair.SetParent(body, false);
            _chair.localPosition = new Vector3(0f, 0f, HandleAhead / scale.z);
            _chair.localRotation = Quaternion.identity;
            _chair.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

            _last = controller.transform.position;
            _left = FamilyBalance.WalkMetres;
            _nextSight = Time.time + 25f;
            Along(controller.transform.position);
            FamilyText.Toast(NotificationType.Info, _partner != null ? "reallife_family_walk_start_both" : "reallife_family_walk_start", Args.Us.Kid(_child.Name), 8f);
            return true;
        }

        // The partner comes too when they are home and free: in from out of view, then beside the player.
        private void Along(Vector3 player)
        {
            FamilySave s = FamilyState.S;
            if (!FamilyState.Together || !s.LivesIn || _d.Cast.Failed || HomeLife.Where(TimeHelper.CurrentDay, TimeHelper.CurrentHour) != Whereabouts.Home)
            {
                return;
            }
            if (!CitySpots.Pavement(player, 3f, out Vector3 near))
            {
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                if (CitySpots.PavementRing(near, 22f, 40f, true, out Vector3 from) && CitySpots.Reachable(from, near))
                {
                    _partner = _d.Cast.Put(0, FamilyLook.Partner, from, near, NavMeshHelperIds.Npc);
                    if (_partner != null)
                    {
                        _partner.WalkTo(near);
                    }
                    return;
                }
            }
        }

        public override void Tick(float now)
        {
            PlayerController controller = PlayerHelper.PlayerController;
            if (_parting)
            {
                // The walk is over; the partner goes on home, and is gone once nobody can see them.
                bool gone = _partner == null || _partner.Lost;
                if (!gone)
                {
                    Vector3 offset = _partner.Position - PlayerHelper.GetPosition();
                    bool unseen = !CitySpots.InView(_partner.Position);
                    gone = (unseen && CitySpots.Flat(offset) > 18f * 18f) || (now >= _partBy && unseen) || now >= _partBy + 40f;
                }
                Done = gone;
                return;
            }
            if (controller == null || _chair == null || PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding)
            {
                Finish(now, false);
                return;
            }
            Vector3 at = controller.transform.position;
            float step = Mathf.Sqrt(CitySpots.Flat(at - _last));
            _last = at;
            if (step < 5f)
            {
                _left -= step;
            }
            if (_left <= 0f)
            {
                Finish(now, true);
                return;
            }
            int tens = Mathf.CeilToInt(_left / 10f);
            if (tens != _shownTens)
            {
                _shownTens = tens;
                _shownLeft = (tens * 10).ToString();
            }
            _d.Objective("reallife_family_objective_title", "reallife_family_objective_walk", _shownLeft, _child.Name);
            if (_left < FamilyBalance.WalkMetres - 30f)
            {
                _d.Prompt("reallife_family_prompt_walk_end", _child.Name, _endByKey);
            }
            if (now >= _nextSight && step > 0.05f)
            {
                _nextSight = now + Random.Range(28f, 50f);
                FamilyText.Toast(NotificationType.Info, FamilyText.Pick(Sights, ref _lastSight), Args.Us.Kid(_child.Name), 6f);
            }
            if (_partner != null && !_partner.Lost && now >= _nextFollow)
            {
                _nextFollow = now + 1f;
                Transform body = controller.Character.transform;
                Vector3 beside = at - body.forward * 0.9f + body.right * 1.0f;
                if (CitySpots.Flat(_partner.Position - beside) > 1.6f * 1.6f && CitySpots.Pavement(beside, 2f, out Vector3 spot))
                {
                    _partner.WalkTo(spot, CitySpots.Flat(_partner.Position - beside) > 7f * 7f);
                }
            }
        }

        private void Finish(float now, bool whole)
        {
            if (_finished)
            {
                return;
            }
            _finished = true;
            FamilySave s = FamilyState.S;
            Fold();
            bool along = _partner != null && !_partner.Lost;
            if (whole)
            {
                bool first = s.LastWalkDay != TimeHelper.CurrentDay;
                s.LastWalkDay = TimeHelper.CurrentDay;
                s.Walks++;
                _child.Closeness = Mathf.Min(100f, _child.Closeness + (first ? 4f : 1f));
                FamilyModifiers.Add(FamilyModifiers.Joy);
                if (along)
                {
                    HomeLife.Shared(first ? 3f : 1f);
                }
                if (s.Walks == 1)
                {
                    FamilyState.Remember("reallife_family_memory_first_walk", _child.Name);
                }
                FamilyText.Good(along ? "reallife_family_walk_done_both" : "reallife_family_walk_done", Args.Us.Kid(_child.Name));
            }
            else
            {
                FamilyText.Toast(NotificationType.Info, "reallife_family_walk_cut_short", Args.Us.Kid(_child.Name), 6f);
            }
            if (!along)
            {
                Done = true;
                return;
            }
            _parting = true;
            _partBy = now + 40f;
            if (!CitySpots.PavementRing(_partner.Position, 26f, 46f, false, out Vector3 away) || !_partner.WalkTo(away))
            {
                _partBy = now + 10f;
            }
        }

        private void Fold()
        {
            _d.Body.StopOver(0.3f);
            if (_chair != null)
            {
                _chair.SetParent(null, true);
                _chair.localScale = Vector3.one;
                _d.Cast.Props.Return(_chair);
                _chair = null;
            }
        }

        public override void End()
        {
            if (_d == null)
            {
                return;
            }
            Fold();
            _d.Body.Release();
            if (_partner != null)
            {
                _d.Cast.Release(_partner);
                _partner = null;
            }
        }
    }
}
