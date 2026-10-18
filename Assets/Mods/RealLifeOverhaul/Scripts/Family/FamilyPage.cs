using System.Text;
using Entities;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>The Family page: five short pages read out one after another, and from any of them the things to do.</summary>
    internal static class FamilyPage
    {
        private const int Pages = 5;

        public static DialogEntry Page(int index)
        {
            int page = (index % Pages + Pages) % Pages;
            if (!Settings.Family)
            {
                return FamilyTalk.Close("reallife_family_menu_off");
            }
            string text;
            switch (page)
            {
                case 0: text = Household(); break;
                case 1: text = Members(); break;
                case 2: text = Dates(); break;
                case 3: text = Memories(); break;
                default: text = Budget(); break;
            }
            return FamilyTalk.Either(FamilyText.Built(text), "reallife_family_page_next", () => Page(page + 1), "reallife_family_page_do", () => FamilyPhone.FamilyMenu(0));
        }

        private static string Household()
        {
            FamilySave s = FamilyState.S;
            var text = new StringBuilder(300);
            text.Append(FamilyText.T("reallife_family_page_household"));
            if (!FamilyState.HasPartner)
            {
                text.Append('\n').Append(FamilyText.T(FamilyState.Bond == Bond.Divorced ? "reallife_family_page_divorced" : "reallife_family_page_single", new Args { Name = s.ExName }));
            }
            else
            {
                int days = Mathf.Max(0, TimeHelper.CurrentDay - (s.SinceDay > 0 ? s.SinceDay : s.PMetDay));
                text.Append('\n').Append(FamilyText.T("reallife_family_page_partner", Args.Us.With(FamilyText.T(FamilyText.BondWord(FamilyState.Bond))).Count(days)));
                text.Append('\n').Append(FamilyText.T("reallife_family_page_harmony", Args.Us.With(FamilyText.T(FamilyText.HarmonyWord(s.Harmony))).Count(Mathf.RoundToInt(s.Harmony))));
                if (s.LivesIn && FamilyState.Together)
                {
                    string where;
                    switch (HomeLife.Where(TimeHelper.CurrentDay, TimeHelper.CurrentHour))
                    {
                        case Whereabouts.Work: where = "reallife_family_page_where_work"; break;
                        case Whereabouts.Out: where = "reallife_family_page_where_out"; break;
                        case Whereabouts.Asleep: where = "reallife_family_page_where_asleep"; break;
                        default: where = "reallife_family_page_where_home"; break;
                    }
                    text.Append('\n').Append(FamilyText.T(where, Args.Us));
                }
                if (s.Ring > 0 && FamilyState.Bond == Bond.Dating)
                {
                    text.Append('\n').Append(FamilyText.T("reallife_family_page_ring", Args.Us.With(FamilyText.T(Courtship.RingName(s.Ring)))));
                }
            }
            BuildingRegistration home = FamilyPlaces.Home();
            if (home != null)
            {
                int sqm = FamilyPlaces.Sqm(home);
                text.Append('\n').Append(FamilyText.T(sqm > 0 ? "reallife_family_page_home" : "reallife_family_page_home_plain",
                    new Args { Place = home.Address.ToFormattedString(), N = sqm.ToString() }));
            }
            else
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_no_home"));
            }
            text.Append('\n').Append(FamilyText.T("reallife_family_page_children", new Args { N = FamilyState.AtHome().ToString() }));
            text.Append('\n').Append(FamilyText.T("reallife_family_page_kin", new Args { Text = Relatives.NextOfKin() }));
            return text.ToString();
        }

        private static string Members()
        {
            FamilySave s = FamilyState.S;
            var text = new StringBuilder(400);
            text.Append(FamilyText.T("reallife_family_page_members"));
            if (FamilyState.HasPartner)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_member_partner", Args.Us.With(FamilyText.T(Partner.JobKey)).Cost(Partner.Income)));
            }
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                string key = c.Grown ? "reallife_family_page_member_grown" : c.School == 2 ? "reallife_family_page_member_private" : c.School == 1 ? "reallife_family_page_member_school"
                    : c.Age < 1f ? "reallife_family_page_member_baby" : "reallife_family_page_member_small";
                string close = c.Closeness >= 75f ? "reallife_family_close_very" : c.Closeness >= 50f ? "reallife_family_close_close"
                    : c.Closeness >= 30f ? "reallife_family_close_distant" : "reallife_family_close_far";
                text.Append('\n').Append(FamilyText.T(key, new Args { Child = c.Name, Text = FamilyText.Age(c.Age), Name = FamilyText.T(close), Amount = FamilyText.Money(c.Fund) }));
            }
            if (PhoneContacts.Mom != null)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_member_mom"));
            }
            if (s.InLaws && FamilyState.HasPartner)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_member_inlaws", Args.Us));
            }
            if (s.Children.Count == 0 && !FamilyState.HasPartner && PhoneContacts.Mom == null)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_nobody"));
            }
            return text.ToString();
        }

        private static string Dates()
        {
            FamilySave s = FamilyState.S;
            var text = new StringBuilder(400);
            text.Append(FamilyText.T("reallife_family_page_dates"));
            int lines = 0;
            if (s.Wedding.Active)
            {
                text.Append('\n').Append(FamilyText.T(s.Wedding.Invited ? "reallife_family_page_wedding" : "reallife_family_page_wedding_uninvited", WeddingPlanner.PlanFill(s.Wedding)));
                lines++;
            }
            if (FamilyState.Process == ProcessKind.Pregnancy)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_due", Args.Us.On(FamilyText.When(s.PDueDay))));
                lines++;
            }
            else if (FamilyState.Process == ProcessKind.Adoption)
            {
                text.Append('\n').Append(FamilyText.T(s.PStep == 0 ? "reallife_family_page_adopt_file" : "reallife_family_page_adopt_wait", Args.Us));
                lines++;
            }
            string errands = Errands.Upcoming();
            if (errands.Length > 0)
            {
                text.Append('\n').Append(errands);
                lines++;
            }
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                if (!c.Grown)
                {
                    int days = Mathf.CeilToInt((Mathf.Floor(c.Age) + 1f - c.Age) * Children.DaysPerYear);
                    text.Append('\n').Append(FamilyText.T("reallife_family_page_birthday",
                        new Args { Child = c.Name, Day = FamilyText.When(TimeHelper.CurrentDay + days), N = (Mathf.FloorToInt(c.Age) + 1).ToString() }));
                    lines++;
                }
            }
            // The next family dinner on the game's calendar (Relatives.HolidayAt).
            int year = GameCalendar.DaysPerYear;
            int dayOfYear = (TimeHelper.CurrentDay - 1) % year;
            for (int k = 0; k < 5; k++)
            {
                int which = k % 4;
                int on = Mathf.Clamp(Mathf.RoundToInt(year * (which == 0 ? 0.22f : which == 1 ? 0.47f : which == 2 ? 0.72f : 0.97f)), 1, year - 1) + (k / 4) * year;
                if (on >= dayOfYear)
                {
                    text.Append('\n').Append(FamilyText.T("reallife_family_page_holiday",
                        new Args { Text = FamilyText.T(Relatives.HolidayName(which)), Day = FamilyText.When(TimeHelper.CurrentDay + on - dayOfYear) }));
                    lines++;
                    break;
                }
            }
            if (lines == 0)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_nothing_due"));
            }
            return text.ToString();
        }

        private static string Memories()
        {
            FamilySave s = FamilyState.S;
            var text = new StringBuilder(500);
            text.Append(FamilyText.T("reallife_family_page_memories"));
            if (s.Memories.Count == 0)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_no_memories"));
                return text.ToString();
            }
            for (int i = s.Memories.Count - 1; i >= 0 && i >= s.Memories.Count - 8; i--)
            {
                Memory m = s.Memories[i];
                string line = FamilyText.T(m.Key, new Args { Name = m.Who, Child = m.Who, Text = m.Who });
                text.Append('\n').Append(FamilyText.T("reallife_family_page_memory_line", new Args { N = m.Day.ToString(), Text = line }));
            }
            return text.ToString();
        }

        private static string Budget()
        {
            FamilySave s = FamilyState.S;
            var text = new StringBuilder(400);
            text.Append(FamilyText.T("reallife_family_page_budget"));
            text.Append('\n').Append(FamilyText.T("reallife_family_page_budget_in", new Args { Amount = FamilyText.Money(s.MIn) }));
            text.Append('\n').Append(FamilyText.T("reallife_family_page_budget_out", new Args
            {
                Amount = FamilyText.Money(s.MChildcare), Text = FamilyText.Money(s.MSchool), N = FamilyText.Money(s.MPocket), Place = FamilyText.Money(s.MFund), Day = FamilyText.Money(s.MOther)
            }));
            text.Append('\n').Append(FamilyText.T("reallife_family_page_budget_all", new Args { Amount = FamilyText.Money(s.TIn), Text = FamilyText.Money(s.TOut) }));
            int funds = 0;
            for (int i = 0; i < s.Children.Count; i++)
            {
                funds += s.Children[i].Grown ? 0 : s.Children[i].Fund;
            }
            if (funds > 0)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_budget_funds", new Args { Amount = FamilyText.Money(funds) }));
            }
            if (FamilyState.Together && s.LivesIn)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_budget_share", Args.Us));
            }
            return text.ToString();
        }
    }
}
