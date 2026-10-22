using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>The calendar's entry in the feature list; its Guide page's {calendar_ahead} comes from the code.</summary>
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
                .Cleanup(() => CalendarDirector.Instance?.StopAll());   // put back on the next second, as the option says
        }
    }

    /// <summary>The calendar's place on the options page.</summary>
    public static class CalendarOptions
    {
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
