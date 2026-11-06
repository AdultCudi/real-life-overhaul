using System;
using BigAmbitions.DayNightCycle;
using Helpers;
using UI.Notification;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// City hall (CivicBridges) with the record, the scenes, the weather, the paper and the living city, and for running
        /// for office the economy, Standing, a let building and the courts. Left at their defaults on purpose: Labouring
        /// (Jobs has no labouring shift), and FinesScaledAtSource and FaresScaledAtSource (civic life scales both itself
        /// after each charge; either flag without scaling at every source would switch the policy off).
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
                CivicBridges.Economy = () => Economy.CityEconomy.Running ? (int)Economy.CityEconomy.State : 1;
                CivicBridges.Influence = () => Fame.FameDirector.Instance != null && Fame.FameDirector.Instance.File != null ? Fame.FameCity.Influence(Fame.FameDirector.Instance.File) : 0;
                CivicBridges.Recognition = () => Standing.Recognition;
                CivicBridges.Integrity = () => Standing.Integrity;
                CivicBridges.Convicted = () => Standing.Convictions > 0;
                CivicBridges.CaseOpen = () => Standing.Charged;
                CivicBridges.Landlord = Landlord;
                CivicBridges.StandingMoved = (integrity, press) =>
                {
                    Fame.FameDirector fame = Fame.FameDirector.Instance;
                    if (fame != null)
                    {
                        if (integrity != 0)
                        {
                            fame.Moved(Fame.FamePart.Integrity, Fame.FameCause.Office, integrity);
                        }
                        fame.Publicised(press, Fame.FameCause.Office);
                    }
                };
                CivicBridges.FileCase = (kind, amount) => kind == 0 ? TaxCases.FileCampaignFinance(amount) : TaxCases.FileMisconduct();
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
                CivicBridges.Economy = null;
                CivicBridges.Influence = null;
                CivicBridges.Recognition = null;
                CivicBridges.Integrity = null;
                CivicBridges.Convicted = null;
                CivicBridges.CaseOpen = null;
                CivicBridges.Landlord = null;
                CivicBridges.StandingMoved = null;
                CivicBridges.FileCase = null;
            }

            // A building of the player's let to a tenant (any unit of it).
            private static bool Landlord()
            {
                try
                {
                    Economy.EstateState s = Economy.EstateDirector.Instance != null ? Economy.EstateDirector.Instance.State : null;
                    for (int i = 0; s != null && i < s.Holdings.Count; i++)
                    {
                        for (int u = 0; u < s.Holdings[i].Units.Count; u++)
                        {
                            if (s.Holdings[i].Units[u].Let)
                            {
                                return true;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("CivicLinks.Landlord", e);
                }
                return false;
            }

            private static float RaiseOf(string id)
            {
                try
                {
                    Economy.CompanyDirector company = Economy.CompanyDirector.Instance;
                    return company != null ? company.RaiseOf(id) / 100f : 0f;
                }
                catch (Exception e)
                {
                    Fault("CivicLinks.RaiseOf", e);
                    return 0f;
                }
            }

            private static void Story(string headlineKey, string bodyKey)
            {
                try
                {
                    Notifications.Show(NotificationType.Info, headlineKey, null, 10f);
                    Paper.Print(bodyKey ?? headlineKey);
                }
                catch (Exception e)
                {
                    Fault("CivicLinks.Story", e);
                }
            }

            private static int PeopleOut()
            {
                try
                {
                    CityLifeDirector city = CityLifeDirector.Instance;
                    return city != null && city.People != null ? city.People.Count - city.People.Free : 0;
                }
                catch (Exception e)
                {
                    Fault("CivicLinks.PeopleOut", e);
                    return 0;
                }
            }
        }

        /// <summary>
        /// A finished story for the next morning's City Ledger: a locale key with no placeholders, printed as it stands.
        /// Nothing allocated; does nothing with the paper off or no save loaded.
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
