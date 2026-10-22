using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UI.Notification;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>The calendar's test button: today's month from every source into Player.log, checked, and the app opened on it.</summary>
    public sealed partial class CalendarDirector
    {
        private const string TestName = "Calendar";

        public void TestMonth()
        {
            if (CalendarGame.Loading())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.Calendar || _broken)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return;
            }
            int today = CalendarGame.Today, year = GameCalendar.DaysPerYear;
            int month = CalendarRules.MonthOf(today, year);
            Look(month);
            int first = CalendarRules.MonthStart(month, year), last = CalendarRules.MonthEnd(month, year);
            var text = new StringBuilder(1024);
            text.Append("Calendar test: day ").Append(today).Append(", a year of ").Append(year).Append(" days; month ").Append(CalendarRules.MonthInYear(month, year) + 1)
                .Append(" of year ").Append(CalendarRules.YearOfMonth(month, year)).Append(", days ").Append(first).Append(" to ").Append(last).Append(" (")
                .Append(last - first + 1).Append(" days): this month ends on day ").Append(last).Append(". Sources: ").Append(LookSummary()).Append('.');
            for (int i = 0; i < List.Items.Count; i++)
            {
                CalendarEntry e = List.Items[i];
                text.Append("\n  day ").Append(e.Day).Append(e.Start >= 0 ? " " + CalendarText.Clock(e.Start) : string.Empty).Append(" [").Append(e.Source).Append(' ')
                    .Append(e.Group).Append(e.Sign > 0 ? " in" : e.Sign < 0 ? " out" : string.Empty)
                    .Append(e.Amount > 0f ? " " + e.Amount.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty).Append("] ")
                    .Append(e.Title).Append(string.IsNullOrEmpty(e.Value) ? string.Empty : ": " + e.Value).Append(e.App != null ? " (" + e.App + ")" : string.Empty);
            }
            for (int i = 0; i < List.Nights.Count; i++)
            {
                NightLine n = List.Nights[i];
                text.Append("\n  every night [").Append(n.Source).Append(n.Sign > 0 ? " in] " : " out] ").Append(n.Title).Append(": ")
                    .Append(n.Amount.ToString("0.##", CultureInfo.InvariantCulture));
            }
            bool whole = first <= today && today <= last && CalendarRules.MonthLength(month, year) == last - first + 1;
            text.Append("\nCalendar test: today is in the month read, and the month is whole: ").Append(whole ? "PASS" : "FAIL");
            Logger?.Info(text.ToString());
            Notifications.Show(NotificationType.Info, "reallife_calendar_test_done", new Dictionary<string, string>(3)
            {
                { "count", List.Items.Count.ToString() }, { "sources", _sources.Length.ToString() }, { "day", last.ToString() }
            }, 10f);
            OpenAt(month);
        }

        internal static void Notice(string key)
        {
            Notifications.Show(NotificationType.Warning, key, null, 8f);
        }
    }
}
