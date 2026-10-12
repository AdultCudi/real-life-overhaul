using BigAmbitions.DayNightCycle;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// City hall with the rest of the mod (Civic/CivicBridges):
        ///
        ///   CriminalRecord, Reputation   <see cref="Standing"/>: an endorsement from somebody with a record is a scandal
        ///   LargeScene                   <see cref="Scenes"/>: no rally and no site crew beside another street scene
        ///   WeatherStopsWork             the game's rain, and a thunderstorm of Events/Weather
        ///   News                         the headline at once, and the story in tomorrow's City Ledger (Social/News)
        ///   CityPeopleOut                how many of the living city's eight are out, so that civic life yields to the cap
        ///
        /// Left at their defaults on purpose: Labouring (Jobs has no labouring shift), FinesScaledAtSource and
        /// FaresScaledAtSource (civic life already applies both policies itself after each charge; setting either flag
        /// without scaling every fine and fare at its source would switch the policy off). The policies that had no
        /// consumer are applied at their sources with one line each: CivicPolicy.EnforcementScale (PoliceAttention.Seen),
        /// ServiceScale (TransitDirector.Headway), EventScale (EventCalendar).
        /// </summary>
        public static class CivicLinks
        {
            internal static void Connect()
            {
                CivicBridges.CriminalRecord = () => Standing.HasRecord;
                CivicBridges.Reputation = () => Standing.PublicSigned;
                CivicBridges.LargeScene = () => Scenes.AnyBut(Scene.Rally);
                CivicBridges.WeatherStopsWork = () => RainHelper.isRaining || Events.Weather.TakeCover;
                CivicBridges.News = Story;
                CivicBridges.CityPeopleOut = PeopleOut;
                CivicBridges.Raise = RaiseOf;
            }

            internal static void Disconnect()
            {
                CivicBridges.CriminalRecord = null;
                CivicBridges.Reputation = null;
                CivicBridges.LargeScene = null;
                CivicBridges.WeatherStopsWork = null;
                CivicBridges.News = null;
                CivicBridges.CityPeopleOut = null;
                CivicBridges.Raise = null;
            }

            private static float RaiseOf(string id)
            {
                Economy.CompanyDirector company = Economy.CompanyDirector.Instance;
                return company != null ? company.RaiseOf(id) / 100f : 0f;
            }

            private static void Story(string headlineKey, string bodyKey)
            {
                Notifications.Show(NotificationType.Info, headlineKey, null, 10f);
                Paper.Print(bodyKey ?? headlineKey);
            }

            private static int PeopleOut()
            {
                CityLifeDirector city = CityLifeDirector.Instance;
                return city != null && city.People != null ? city.People.Count - city.People.Free : 0;
            }
        }

        /// <summary>
        /// The City Ledger's intake for a finished story: one locale key with no placeholders, printed as it stands
        /// in the next morning's paper (Social/News, NewsKind.Story). One call, nothing allocated; it does nothing
        /// with the paper switched off or no save loaded.
        ///     Wiring.Paper.Print("reallife_news_story_fire");
        /// </summary>
        public static class Paper
        {
            public static void Print(string storyKey)
            {
                if (storyKey != null && SaveGameManager.Current != null)
                {
                    News.Report(NewsKind.Story, PlayerHelper.GetCityPosition(), storyKey);
                }
            }
        }
    }
}
