using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>
    /// The calendar's entry in the mod's feature list and its place on the options page. Its Guide page fills the months a
    /// year can be turned on ahead ({calendar_ahead}) from the code. Called once, when the feature list and the options
    /// page are built.
    /// </summary>
    public static class CalendarGuide
    {
        public static void Register()
        {
            Features.Add("calendar", "reallife_meta_title_calendar")
                .Group("reallife_calendar_header")
                .About("reallife_guide_calendar_about").Help("reallife_guide_calendar")
                .Owns(Settings.CalendarId)
                .Test("reallife_calendar_test_month")
                .Fact("calendar_ahead", () => MonthViewRules.AheadMonths.ToString(CultureInfo.InvariantCulture))
                .Cleanup(() => CalendarDirector.Instance?.StopAll());   // the director puts it back on its next second, as the option says
        }
    }

    /// <summary>The calendar's place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class CalendarOptions
    {
        /// <summary>The "Calendar" block of the options page: its switch and its test.</summary>
        public static ModOptions AddCalendar(this ModOptions options)
        {
            return options
                .AddHeader("reallife_calendar_header")
                .AddToggle(Settings.CalendarId, "reallife_calendar_enabled", Settings.Calendar,
                    v => { Settings.Calendar = v; if (!v) { CalendarDirector.Instance?.StopAll(); } })
                .AddButton("reallife_calendar_test_month", () => CalendarDirector.Instance?.TestMonth())
                .AddSplitter();
        }
    }
}
