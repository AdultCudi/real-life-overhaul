using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>Children's part for growing up: milestones, check-ups due, starting school, birthdays and leaving home.</summary>
    internal static partial class Children
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

        private static readonly string[] Drawings =
        {
            "reallife_family_drawing_1", "reallife_family_drawing_2", "reallife_family_drawing_3", "reallife_family_drawing_4", "reallife_family_drawing_5",
            "reallife_family_drawing_6", "reallife_family_drawing_7", "reallife_family_drawing_8"
        };

        private static readonly string[] Reports = { "reallife_family_report_great", "reallife_family_report_good", "reallife_family_report_fair", "reallife_family_report_struggling" };
        private static int _lastDrawing = -1;

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
                    float standing = c.Closeness + SchoolStanding(c) + c.Pocket * 2f;
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
                    AtSchool(Errands.Add(Errands.ParentsEvening, day + 3, 16, 20, i), c);
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
    }
}
