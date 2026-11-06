using System;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Seasons;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The seasons with the street events' calendar (the save's number the snow is rolled from, shared in Going Public),
        /// the weather (the snow on the roads, which slows traffic and takes grip as rain does) and the Calendar (the season's
        /// line on a day's page).
        /// </summary>
        public static class SeasonLinks
        {
            internal static void Connect()
            {
                SeasonBridges.Salt = Salt;
                Weather.SnowNow = Snow;
                CalendarDirector.SeasonWords = Words;
            }

            internal static void Disconnect()
            {
                SeasonBridges.Reset();
                Weather.SnowNow = null;
                CalendarDirector.SeasonWords = null;
            }

            private static int Salt()
            {
                try
                {
                    return EventCalendar.Salt;
                }
                catch (Exception e)
                {
                    Fault("SeasonLinks.Salt", e);
                    return 0;
                }
            }

            private static int Snow()
            {
                try
                {
                    SeasonDirector director = SeasonDirector.Instance;
                    return director != null && Settings.Seasons ? director.Step : 0;
                }
                catch (Exception e)
                {
                    Fault("SeasonLinks.Snow", e);
                    return 0;
                }
            }

            private static string Words(int day)
            {
                try
                {
                    return SeasonDirector.CalendarWords(day);
                }
                catch (Exception e)
                {
                    Fault("SeasonLinks.Words", e);
                    return null;
                }
            }
        }
    }
}
