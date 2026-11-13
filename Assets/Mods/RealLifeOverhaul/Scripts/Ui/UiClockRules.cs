using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A time of day as the game's own top bar writes it: "13:05" on the 24 hour clock and "01:05 PM" on the 12 hour one the game's options offer
    /// (midnight is "12:00 AM", noon "12:00 PM"). Game-free, so the checks can hold it to the game's own format; the kit reads the option in
    /// <see cref="UiClock"/>.
    /// </summary>
    public static class UiClockRules
    {
        public static string Format(int hour, int minute, bool twelveHour)
        {
            int h = ((hour % 24) + 24) % 24, m = minute < 0 ? 0 : minute > 59 ? 59 : minute;
            CultureInfo inv = CultureInfo.InvariantCulture;
            if (!twelveHour)
            {
                return h.ToString("00", inv) + ":" + m.ToString("00", inv);
            }
            int shown = h % 12 == 0 ? 12 : h % 12;
            return shown.ToString("00", inv) + ":" + m.ToString("00", inv) + (h >= 12 ? " PM" : " AM");
        }

        /// <summary>The same for minutes since midnight.</summary>
        public static string Format(int minuteOfDay, bool twelveHour)
        {
            int m = ((minuteOfDay % 1440) + 1440) % 1440;
            return Format(m / 60, m % 60, twelveHour);
        }
    }
}
