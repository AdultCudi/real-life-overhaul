using System;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Flare in the city with the living population (DRAFT2.md section 4; the brief's second step: "Flare's effect leaves BusinessGame.Ambient's
        /// clamp and becomes an attention source through PopulationAttention"): Flare reads the population this machine runs and its pools
        /// (FlareBridges.Residents, Pools), hears of each day the population lived (CityPopulation.DayLived) and is one of the population's
        /// attention sources, so what residents heard of a business draws people to it from its rivals in the same pools. Nothing per frame: the
        /// population asks the source once a game day a business, and its day's event comes once a game day.
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
            }

            internal static void Disconnect()
            {
                PopulationAttention.Unregister(Source);
                CityPopulation.DayLived -= Lived;
                FlareBridges.ResetCity();
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
