using Localizor;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How a rota shift's time is said, in one place: always start and end in the game's own clock format (its 12 or 24
    /// hour option), as "13:00 to 17:00". The Planner writes the same times with its own formatter.
    /// </summary>
    public static class CareerClock
    {
        /// <summary>An hour of the day on the game's clock: "13:00", or "01:00 PM" with the game's 12 hour option.</summary>
        public static string Time(int hour)
        {
            return (((hour % 24) + 24) % 24).GetFormattedTime();
        }

        /// <summary>A shift that starts at this hour, start to end: "13:00 to 17:00".</summary>
        public static string Span(int startHour)
        {
            return "reallife_career_span".GetLocalization().Replace("{from}", Time(startHour)).Replace("{to}", Time(startHour + CareerCatalogue.ShiftHours));
        }

        /// <summary>When clocking on opens for a shift that starts at this hour.</summary>
        public static string Opens(int startHour)
        {
            return Time(startHour - CareerCatalogue.EarlyHours);
        }

        /// <summary>From when clocking on counts as late.</summary>
        public static string LateFrom(int startHour)
        {
            return Time(startHour + CareerCatalogue.OnTimeHours);
        }

        /// <summary>The span with the clocking-on window after it: "13:00 to 17:00 (clock on from 11:00; late from 14:00)".</summary>
        public static string SpanWithWindow(int startHour)
        {
            return "reallife_career_span_window".GetLocalization().Replace("{time}", Span(startHour)).Replace("{open}", Opens(startHour)).Replace("{late}", LateFrom(startHour));
        }
    }
}
