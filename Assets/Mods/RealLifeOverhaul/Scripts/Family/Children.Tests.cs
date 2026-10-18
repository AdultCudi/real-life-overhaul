using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>Children's part for the options page's test button.</summary>
    internal static partial class Children
    {
        /// <summary>The test button: starts a pregnancy (or an adoption), or moves the one in progress on a step. Null if it did, else why not.</summary>
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
}
