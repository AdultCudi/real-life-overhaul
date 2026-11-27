using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the rest of the mod reads of the player's fitness and diet: one place, with the options applied, so nothing else reads HomeState's fields or Settings for it. Social
    /// reads <see cref="Level"/> and <see cref="DietBand"/> (a person who likes someone athletic), Careers reads <see cref="Level"/> (a post that asks for fitness), Health
    /// reads <see cref="Fill"/> (its You page). Each answers -1 (nothing known) while home life or its fitness option is off or no save is loaded, so a reader treats that as
    /// "no opinion" and never as unfit. Cost: a few field reads a call, nothing allocated except by <see cref="Fill"/>'s view, which a page owns.
    /// </summary>
    public static class HomeFitness
    {
        /// <summary>True while fitness is being kept.</summary>
        public static bool On => HomeState.Loaded && Settings.HomeLife && Settings.HomeFitness;

        /// <summary>The player's fitness, 0 to 100 as the needs bar shows it, or -1 when it is not kept.</summary>
        public static int Level => On ? FitnessRules.Whole(HomeState.Fitness) : -1;

        /// <summary>The fitness band, 0 out of shape to 5 athletic (<see cref="FitnessRules.Band"/>), or -1 when it is not kept.</summary>
        public static int Band => On ? FitnessRules.Band(HomeState.Fitness) : -1;

        /// <summary>
        /// A simple reading of the last five days' meals: 0 poor (mostly bought ready to eat or skipped), 1 fair, 2 good (mostly cooked at home), or -1 when fitness is not
        /// kept. The same three steps Health's way of living uses (<c>Wiring.HealthLinks</c>), so every part of the mod calls a diet the same thing.
        /// </summary>
        public static int DietBand
        {
            get
            {
                if (!On)
                {
                    return -1;
                }
                float diet = HomeState.DietScore;
                return diet <= HomeBalance.DietPoor ? 0 : diet >= HomeBalance.DietGood ? 2 : 1;
            }
        }

        /// <summary>The diet's score, 0 to 100 (50 when nothing is known yet), or -1 when fitness is not kept.</summary>
        public static int DietScore => On ? FitnessRules.Whole(HomeState.DietScore) : -1;

        /// <summary>
        /// Fills a page's view: the figure and its band, the days' trend (oldest first) and the "why" lines of the last week. A page calls it when it is built or opened,
        /// never per frame.
        /// </summary>
        public static void Fill(FitnessView v)
        {
            v.Known = On;
            v.WhyCount = 0;
            v.TrendCount = 0;
            v.Change = 0;
            v.DietCounts = Settings.HomeDiet;
            if (!v.Known)
            {
                v.Level = 0;
                v.Band = 0;
                return;
            }
            v.Day = TimeHelper.CurrentDay;
            v.Level = FitnessRules.Whole(HomeState.Fitness);
            v.Band = FitnessRules.Band(HomeState.Fitness);
            int known = HomeState.FitKnown;
            for (int i = 0; i < known && i < FitnessView.Days; i++)
            {
                v.Trend[i] = FitnessRules.Whole(HomeState.StartOf(known - 1 - i));
            }
            v.TrendCount = known > FitnessView.Days ? FitnessView.Days : known;
            v.Change = v.TrendCount >= 2 ? v.Level - v.Trend[0] : 0;
            v.WhyCount = FitnessRules.Why(HomeState.LedgerDays, 7, v.Why);
        }
    }
}
