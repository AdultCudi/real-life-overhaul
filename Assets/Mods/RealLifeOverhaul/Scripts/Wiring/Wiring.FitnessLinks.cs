using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Health;
using RealLifeOverhaul.Leisure;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The fitness meter with the rest of the mod (Home/HomeFitness is the one place that reads it):
        ///
        ///   HealthBridges.Fitness    the Health app's You page asks for the figure, the fourteen days and what moved it
        ///   CareersBridges.Fitness   a physical job asks what the player's fitness is (-1: not kept, so it asks nothing)
        ///   LeisureState.Drank       a drink poured at a bar is counted for the day's night of drinks
        ///
        /// Each is asked on a page's opening, an application or a day's turn, never per frame. A part that is away answers as nothing known.
        /// </summary>
        public static class FitnessLinks
        {
            internal static void Connect()
            {
                HealthBridges.Fitness = HomeFitness.Fill;
                CareersBridges.Fitness = () => HomeFitness.Level;
                LeisureState.Drank = units =>
                {
                    if (HomeState.Loaded && Settings.HomeLife && Settings.HomeFitness)
                    {
                        HomeState.Drank(units);
                    }
                };
            }

            internal static void Disconnect()
            {
                HealthBridges.Fitness = null;
                CareersBridges.Fitness = null;
                LeisureState.Drank = null;
            }
        }
    }
}
