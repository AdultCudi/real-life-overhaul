using System;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.Family;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The calendar's days of the people in the player's life, read from what they keep and changed in nothing:
        ///
        ///   people     the birthday of each person the player has met (SocialPerson.Birthday, a day of the year as the
        ///              calendar counts it), with the age they turn (SocialRules.AgeOn)
        ///   children   each child's next birthday, as the family's own year grows them (Children.Grow: a year of
        ///              Children.DaysPerYear days), and the day each week of what they cost is paid (FamilySave.LastWeekDay,
        ///              Children.WeekCharges)
        ///
        /// Asked only when the calendar is shown.
        /// </summary>
        internal static class CalendarPeople
        {
            internal static void Add(CalendarDirector calendar)
            {
                calendar.AddSource(Birthdays);
                calendar.AddSource(Kids);
            }

            private static readonly PeopleBirthdays Birthdays = new PeopleBirthdays();
            private static readonly ChildrenDays Kids = new ChildrenDays();

            /// <summary>The birthdays of the people the player has met (Social).</summary>
            private sealed class PeopleBirthdays : ICalendarSource
            {
                public string Id => "people";

                public bool Available => Settings.Social && SocialState.Loaded;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    int year = SocialPlaces.DaysPerYear;
                    for (int i = 0; i < SocialState.Count; i++)
                    {
                        SocialPerson p = SocialState.People[i];
                        if (p == null || !p.Met || string.IsNullOrEmpty(p.Name))
                        {
                            continue;
                        }
                        for (int day = MonthViewRules.DayOfYearFrom(p.Birthday, fromDay, year); day <= toDay; day += year)
                        {
                            CalendarEntry e = into.Add(Id, i, day, CalendarGroup.Time, day);
                            if (e != null)
                            {
                                e.Title = CalendarText.T("reallife_calendar_birthday_title", "name", p.Name);
                                e.Value = CalendarText.T("reallife_calendar_birthday_age", "age", SocialRules.AgeOn(p.Age, p.AgeDay, p.Birthday, day, year).ToString());
                            }
                        }
                    }
                }
            }

            /// <summary>The children's birthdays and the week they cost (Family: Children).</summary>
            private sealed class ChildrenDays : ICalendarSource
            {
                public string Id => "children";

                public bool Available => Settings.Family && Settings.FamilyChildren && FamilyState.Loaded && FamilyState.S != null && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    FamilySave s = FamilyState.S;
                    int today = SaveGameManager.Current.Day, year = Math.Max(1, Children.DaysPerYear);
                    bool any = false;
                    for (int i = 0; i < s.Children.Count; i++)
                    {
                        ChildRecord c = s.Children[i];
                        if (c == null || c.Grown)
                        {
                            continue;
                        }
                        any = true;
                        int turns = (int)Math.Floor(c.Age) + 1;
                        for (int day = today + MonthViewRules.ChildBirthdayIn(c.Age, year), age = turns; day <= toDay; day += year, age++)
                        {
                            CalendarEntry e = into.Add(Id, i, day, CalendarGroup.Time, day);
                            if (e != null)
                            {
                                e.Title = CalendarText.T("reallife_calendar_birthday_title", "name", c.Name);
                                e.Value = CalendarText.T("reallife_calendar_birthday_age", "age", age.ToString());
                            }
                        }
                    }
                    if (!any)
                    {
                        return;
                    }
                    int cost = Children.WeekCharges(out _, out _, out _);
                    if (cost <= 0)
                    {
                        return;
                    }
                    for (int day = MonthViewRules.FirstFrom(Math.Max(today, s.LastWeekDay + 7), 7, fromDay); day <= toDay; day += 7)
                    {
                        CalendarEntry e = into.Add(Id, 100, day, CalendarGroup.Money, day);
                        if (e != null)
                        {
                            e.Sign = -1;
                            e.Amount = cost;
                            e.Title = CalendarText.T("reallife_calendar_children_week_title");
                        }
                    }
                }
            }
        }
    }
}
