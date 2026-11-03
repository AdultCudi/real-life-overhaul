using Helpers;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: open the Planner, the Calendar and the Gallery on the kit, and put their switches back on.
    /// </summary>
    public static class LifeUiTests
    {
        private static readonly string[] Screens = { "planner", "calendar", "camera", "photo_gallery", "viewfinder" };

        public static void OpenPlanner()
        {
            Planner.PlannerDirector.Instance?.Open();
        }

        public static void OpenCalendar()
        {
            int year = GameCalendar.DaysPerYear;
            int day = TimeHelper.CurrentDay;
            Calendar.CalendarDirector.Instance?.OpenAt(CalendarRules.MonthOf(day, year));
        }

        public static void OpenGallery()
        {
            Photos.PhotosDirector.Instance?.OpenGallery();
        }

        /// <summary>Turns the five screens off and straight back on, so a broken one can be tried and restored.</summary>
        public static void Switches()
        {
            for (int i = 0; i < Screens.Length; i++)
            {
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(Screens[i]), false);
            }
            for (int i = 0; i < Screens.Length; i++)
            {
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(Screens[i]), true);
            }
            Notifications.Show(NotificationType.Info, "reallife_ui_life_test_switches_done", null, 8f);
        }
    }
}
