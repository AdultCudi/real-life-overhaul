using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>Children's part for the appointments, the birth or the arrival, the name, and the adoption's steps.</summary>
    internal static partial class Children
    {
        private static readonly string[] Girls = { "Lily", "Maya", "Zoe", "Ava", "Nina", "Ruby", "Iris", "Ella", "Sara", "Mila", "Anna", "Cora" };
        private static readonly string[] Boys = { "Leo", "Noah", "Sam", "Eli", "Max", "Theo", "Finn", "Luca", "Owen", "Jude", "Kai", "Ben" };
        private static int _nameTries;

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
            director.Pass(scan ? "reallife_family_passage_scan" : "reallife_family_passage_class", scan ? 1 : 2, null, () =>
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
            });
        }

        public static void AppointmentMissed(Errand errand)
        {
            FamilySave s = FamilyState.S;
            if (s.PCarrier == 1)
            {
                // The player's own: booked again for tomorrow, as the notice says (the birth drops it if it comes first).
                Errands.Add(errand.Kind, TimeHelper.CurrentDay + 1, errand.FromHour, errand.ToHour).Number = errand.Number;
                FamilyText.Toast(NotificationType.Info, "reallife_family_appointment_missed_me", Args.Us);
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
            ChildRecord c = Of(errand);
            s.Errands.Remove(errand);
            FamilyPlaces.Unguide();
            director.Pass("reallife_family_passage_checkup", 1, null, () =>
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
            });
        }

        public static void CheckupMissed(Errand errand)
        {
            ChildRecord c = Of(errand);
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
            director.Pass("reallife_family_passage_birth", 8, null, () => Arrive(false, 0f, present));
        }

        public static void BirthMissed(Errand errand)
        {
            FamilyDirector director = FamilyDirector.Instance;
            if (FamilyState.S.PCarrier == 1)
            {
                director.WhenFree(() => Deliver(director, true));   // the player's own: an ambulance sees to it, wherever they were
                return;
            }
            FamilyTalk.Move(-FamilyBalance.BirthPrice, "reallife_transaction_family_clinic");
            FamilyState.MoneyOut(FamilyBalance.BirthPrice, 4);
            Arrive(false, 0f, false);
        }

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

        // A name, with second thoughts (eight at most); a child who already answers to theirs keeps it.
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
            director.Pass("reallife_family_passage_home_visit", 1, null, () =>
            {
                FamilyState.S.PStep = 2;
                HomeLife.Shared(3f);
                FamilyText.Good("reallife_family_adopt_visit_done", Args.Us);
            });
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
            director.Pass("reallife_family_passage_adopt", 3, null, () => Arrive(true, age, true));
        }

        public static void AdoptMeetMissed(Errand errand)
        {
            FamilySave s = FamilyState.S;
            int day = TimeHelper.CurrentDay + 1;
            s.PDueDay = day;
            Errands.Add(Errands.AdoptMeet, day, 9, 18);
            FamilyText.Message(FamilyPhone.FamilyContact, "reallife_family_adopt_meet_moved", Args.Us.On(FamilyText.When(day)));
        }
    }
}
