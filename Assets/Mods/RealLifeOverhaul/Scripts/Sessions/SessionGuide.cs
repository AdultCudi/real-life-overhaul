using System.Globalization;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>
    /// The living economy's entry in the mod's feature list: its master option Living population and the desk sessions'
    /// own switch, their save line, their two tests and the Guide page's figures, each {name} filled from the rules when the
    /// page opens. Called once when the feature list is built.
    /// </summary>
    public static class SessionGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("living", "reallife_meta_title_living")
                .About("reallife_guide_living_about").Help("reallife_guide_living")
                .Owns(Settings.LivingPopulationId, Settings.LivingDesksId)
                .Data(SessionStore.Key)
                .Test("reallife_session_test_meeting", "reallife_session_test_managers")
                .Cleanup(() => SessionDirector.Instance?.Release(), 0)
                .ResetData(() => SessionDirector.Instance?.Forget())
                .Fact("session_remind", () => SessionRules.RemindHour.ToString("00", Inv) + ":00")
                .Fact("session_manager_least", () => Whole(SessionRules.ManagerLeast / 10) + "%")
                .Fact("meeting_every", () => Whole(MeetingRules.WeekDays))
                .Fact("meeting_morale", () => Whole(MeetingRules.MoraleLift / 10) + "%")
                .Fact("meeting_ease", () => Whole(MeetingRules.BurnoutEase / 10) + "%")
                .Fact("meeting_push", () => Whole(MeetingRules.PushOutput / 10) + "%")
                .Fact("meeting_cost", () => Whole(-MeetingRules.MoraleWeekOutput / 10) + "%")
                .Fact("meeting_short", () => Whole(MeetingRules.ShortStrength / 10) + "%")
                .Fact("meeting_desks", () => Whole(SessionRegistry.Find(MeetingRules.Id) != null ? SessionRegistry.Find(MeetingRules.Id).DesksLeast : 0));
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}
