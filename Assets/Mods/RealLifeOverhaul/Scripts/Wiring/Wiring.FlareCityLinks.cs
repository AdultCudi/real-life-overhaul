using System;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Flare in the city with the living population (DRAFT2.md section 4; the brief's second step: "Flare's effect leaves BusinessGame.Ambient's
        /// clamp and becomes an attention source through PopulationAttention"): Flare reads the population this machine runs and its pools
        /// (FlareBridges.Residents, Pools), hears of each day the population lived (CityPopulation.DayLived) and is one of the population's
        /// attention sources, so what residents heard of a business draws people to it from its rivals in the same pools. Nothing per frame: the
        /// population asks the source once a game day a business, and its day's event comes once a game day. The game studio's news, devlogs and
        /// crowdfunding (StudioBridges) and the other tech firms' news (SectorBridges.Talk) are Flare's posts, heard by the residents.
        /// </summary>
        public static class FlareCityLinks
        {
            private const string Source = "Flare's residents";

            private static readonly Action Lived = DayLived;

            internal static void Connect()
            {
                FlareBridges.Residents = Residents;
                FlareBridges.Pools = Pools;
                CityPopulation.DayLived += Lived;
                PopulationAttention.Register(Source, Attention);
                StudioBridges.Talk = Talk;
                StudioBridges.Devlog = Devlog;
                StudioBridges.Crowdfund = Crowdfund;
                SectorBridges.Talk = SectorTalk;
            }

            internal static void Disconnect()
            {
                SectorBridges.Talk = null;
                StudioBridges.Talk = null;
                StudioBridges.Devlog = null;
                StudioBridges.Crowdfund = null;
                PopulationAttention.Unregister(Source);
                CityPopulation.DayLived -= Lived;
                FlareBridges.ResetCity();
            }

            // The game studio on Flare (Tech/Studio/StudioBridges): its news as its account's posts, a devlog's hype, a campaign's pledges; Flare off, nothing.
            private static void Talk(int news, string place, string game, long figure)
            {
                try
                {
                    FlareDirector flare = FlareDirector.Instance;
                    if (flare != null && flare.Running)
                    {
                        flare.StudioNews(news, place, game, figure);
                    }
                }
                catch (Exception e)
                {
                    Fault("FlareCityLinks.Talk", e);
                }
            }

            // The other tech firms on Flare (Tech/Sector/SectorBridges): a firm of the player's posts its news on its page, a rival's is the city's; Flare off, nothing.
            private static void SectorTalk(int news, string place, string name, long figure)
            {
                try
                {
                    FlareDirector flare = FlareDirector.Instance;
                    if (flare != null && flare.Running)
                    {
                        flare.SectorNews(news, place, name, figure);
                    }
                }
                catch (Exception e)
                {
                    Fault("FlareCityLinks.SectorTalk", e);
                }
            }

            private static int Devlog(string place, string game)
            {
                try
                {
                    FlareDirector flare = FlareDirector.Instance;
                    return flare != null && flare.Running ? flare.StudioDevlog(place, game) : 0;
                }
                catch (Exception e)
                {
                    Fault("FlareCityLinks.Devlog", e);
                    return 0;
                }
            }

            private static long Crowdfund(string place, long goal)
            {
                try
                {
                    FlareDirector flare = FlareDirector.Instance;
                    return flare != null && flare.Running ? flare.StudioCrowdfund(place, goal) : 0;
                }
                catch (Exception e)
                {
                    Fault("FlareCityLinks.Crowdfund", e);
                    return 0;
                }
            }

            // The population this machine runs: none while Living population is off, on a city it does not know, or on a guest following the host.
            private static PopulationState Residents()
            {
                try
                {
                    return Settings.LivingPopulation && CityPopulation.Running && !CityPopulation.Following ? CityPopulation.Live : null;
                }
                catch (Exception e)
                {
                    Fault("FlareCityLinks.Residents", e);
                    return null;
                }
            }

            private static PoolRules.DemandDay Pools()
            {
                try
                {
                    return CityPopulation.Running ? CityPopulation.Pools : null;
                }
                catch (Exception e)
                {
                    Fault("FlareCityLinks.Pools", e);
                    return null;
                }
            }

            private static void DayLived()
            {
                try
                {
                    FlareDirector.Instance?.ResidentsDayLived();
                }
                catch (Exception e)
                {
                    Fault("FlareCityLinks.DayLived", e);
                }
            }

            // What residents heard of a business, thousandths of its pull (0 while the residents' side does not run): the population takes a source that throws off with one line.
            private static int Attention(BuildingRegistration reg)
            {
                return Settings.LivingFlare ? FlareDirector.AttentionOf(reg) : 0;
            }
        }
    }
}
