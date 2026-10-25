using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Seasons;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The calendar's seasons: the first day of each, with what sells more in it, and the snow of the days the forecast
        /// reaches (SeasonRules.ForecastDays). Read only, from the day and the save's number; asked when the calendar is shown.
        /// </summary>
        internal static class CalendarSeasons
        {
            private const int RowSeason = 1, RowSnow = 2;

            internal static void Add(CalendarDirector calendar)
            {
                calendar.AddSource(Days);
            }

            private static readonly SeasonDays Days = new SeasonDays();

            private sealed class SeasonDays : ICalendarSource
            {
                public string Id => "seasons";

                public bool Available => Settings.Seasons && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, CalendarList into)
                {
                    int year = GameCalendar.DaysPerYear, today = SaveGameManager.Current.Day, salt = EventCalendar.Salt;
                    for (int day = fromDay; day <= toDay; day++)
                    {
                        if (day > 1 && SeasonRules.BeginsOn(day, year))
                        {
                            int season = SeasonRules.SeasonOf(day, year);
                            CalendarEntry e = into.Add(Id, RowSeason, season, CalendarGroup.City, day);
                            if (e != null)
                            {
                                e.Title = CalendarText.T("reallife_season_calendar_begins", "season", SeasonText.Title(season));
                                e.Value = CalendarText.T("reallife_season_calendar_more", "more", SeasonText.Goods(season, true));
                            }
                        }
                        if (!Settings.WeatherMatters || day < today || day > today + SeasonRules.ForecastDays)
                        {
                            continue;
                        }
                        SnowDay snow = SeasonRules.SnowOn(day, year, salt);
                        if (snow.Falls)
                        {
                            CalendarEntry e = into.Add(Id, RowSnow, day, CalendarGroup.City, day);
                            if (e != null)
                            {
                                e.Start = snow.From * 60;
                                e.Title = CalendarText.T(snow.Heavy ? "reallife_season_calendar_snow_heavy" : "reallife_season_calendar_snow_light");
                                e.Value = CalendarText.T("reallife_season_calendar_snow_hours", "from", SeasonText.Clock(snow.From), "to", SeasonText.Clock(snow.To));
                            }
                        }
                    }
                }
            }
        }
    }
}
