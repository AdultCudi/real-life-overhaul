using System;
using BigAmbitions.DayNightCycle;
using PlayerActivity;
using RealLifeOverhaul.Careers;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the mod knows of an activity of its own beyond what the game's panel says: filled into the kit's model after
    /// the panel's own words. A working day at a desk (Careers/DeskWork) has its employer at the side, its pay, its week and
    /// the player's standing with the team as rows, the hours to stay with the time it ends and its pay before tax, and
    /// while it runs the hour it is in and what it has earned. Game side. Cost: a few reads a look; nothing for the game's
    /// own activities.
    /// </summary>
    internal static class ActivityFacts
    {
        private static readonly string[] DayKeys =
        {
            "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday"
        };

        /// <summary>The mod's facts for its own activities; the game's own are left as the panel says them.</summary>
        public static void Fill(IPlayerActivity a, UiActivityModel m)
        {
            if (a is TestActivity test)
            {
                m.Icon = test.What == TestActivity.Kind.Class ? UiIcon.Cap : test.What == TestActivity.Kind.Chair ? UiIcon.Presentation : UiIcon.Briefcase;
                return;
            }
            if (a is DeskWork desk)
            {
                Desk(desk, m);
            }
        }

        // A working day at a desk: the job's own figures, read from the careers' file as the job's pages show them.
        private static void Desk(DeskWork desk, UiActivityModel m)
        {
            CareersDirector director = CareersDirector.Instance;
            Careers.Job j = director != null && director.Work != null ? director.Work.Job : null;
            if (j == null || !desk.Day)
            {
                return;   // overtime and a day with no job file left as the panel says them
            }
            Profession p = j.Def;
            int rate = Prices.At(j.Pay);   // cents an hour at today's prices
            m.Icon = UiIcon.Briefcase;
            m.Place = CareersText.Employer(j.Post.Employer);
            m.PlaceLines = string.IsNullOrEmpty(j.Post.Street) ? new string[0] : new[] { j.Post.Number > 0 ? j.Post.Number + " " + j.Post.Street : j.Post.Street };
            if (m.State == UiActivityState.NotStarted)
            {
                int day = Math.Max(0, Math.Min(DayKeys.Length - 1, TimeHelper.GetDayOfWeekIndex(TimeHelper.GetDayOfWeek())));
                m.Kicker = UiText.Fill("reallife_ui_activity_work_kicker", "day", ActivityRead.Words(DayKeys[day], null));
                m.Title = UiText.T("reallife_ui_activity_work_title");
                m.Lines = new[]
                {
                    new UiInfoRow { Label = UiText.T("reallife_ui_activity_pay"), Value = UiText.Fill("reallife_ui_activity_pay_hour", "money", CareersText.Rate(rate)) },
                    new UiInfoRow { Label = UiText.T("reallife_ui_activity_hours"), Value = CareersText.Hours(p) },
                    new UiInfoRow
                    {
                        Label = UiText.T("reallife_ui_activity_standing"), Value = (j.Favour > 0 ? "+" : string.Empty) + UiText.Whole(j.Favour),
                        Ink = j.Favour > 0 ? UiRole.OkText : j.Favour < 0 ? UiRole.WarnText : UiRole.None,
                    },
                };
                if (m.HasDuration)
                {
                    int minutes = desk.ChosenMinutes;
                    m.Body = UiText.T("reallife_ui_activity_work_body");
                    m.DurationLabel = UiText.T("reallife_ui_activity_work_for");
                    m.DurationFrom = ActivityRead.Words("common_hours_abbr", new { hours = 1 });
                    m.DurationTo = ActivityRead.Words("common_hours_abbr", new { hours = desk.MostHours });
                    m.Finish = UiText.Fill("reallife_ui_activity_finish", "time", TimeHelper.Now().AddMinutes(minutes).GetFormattedTime());
                    m.Sum = UiText.Fill("reallife_ui_activity_before_tax", "money", CareersText.Money((long)rate * minutes / 60 / 100));
                }
                return;
            }
            if (m.State == UiActivityState.Started || m.State == UiActivityState.Running)
            {
                int length = Math.Max(1, desk.Length), done = Math.Max(0, Math.Min(desk.Done, length));
                int hours = (length + 59) / 60, hour = Math.Min(hours, done / 60 + 1);
                m.Kicker = UiText.T("reallife_ui_activity_work_strip");
                m.MeterLeft = UiText.Fill("reallife_ui_activity_hour_of", "hour", UiText.Whole(hour), "hours", UiText.Whole(hours));
                m.MeterRight = UiText.Fill("reallife_ui_activity_earned", "money", CareersText.Money((long)rate * done / 60 / 100));
                m.SkipLabel = UiText.T("reallife_ui_activity_skip_end");
            }
        }
    }
}
