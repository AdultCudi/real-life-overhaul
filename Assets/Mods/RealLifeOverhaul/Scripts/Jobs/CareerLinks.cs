namespace RealLifeOverhaul
{
    /// <summary>
    /// Somebody the player has come to know at work. A social feature that wants acquaintances sets
    /// <see cref="CareerLinks.Social"/>; with nothing set, nothing is told to anyone.
    /// </summary>
    public interface ICareerAcquaintances
    {
        /// <summary>
        /// The player worked a shift alongside this person (or reports to them). Called at most once a shift per
        /// person, never per frame.
        /// </summary>
        /// <param name="personId">Stable across games and saves: "career:courier:mate_1", "career:courier:boss".</param>
        /// <param name="nameKey">A locale key for their name.</param>
        /// <param name="employerNameKey">A locale key for where they work.</param>
        /// <param name="isBoss">True for the employer's boss.</param>
        void Met(string personId, string nameKey, string employerNameKey, bool isBoss);
    }

    /// <summary>
    /// The player's record with the law, as an employer would read it on a CV. A law feature sets
    /// <see cref="CareerLinks.Law"/>; with nothing set, the CV shows only what the jobs themselves know
    /// (police stops while on a job).
    /// </summary>
    public interface ICareerRecord
    {
        /// <summary>True if there is nothing on the record an employer would mind.</summary>
        bool Clean { get; }
        /// <summary>One line for the CV, already in the player's language; null or empty for nothing to show.</summary>
        string Summary { get; }
    }

    /// <summary>
    /// Whether the player is signed off sick. A health feature sets <see cref="CareerLinks.Health"/>; with
    /// nothing set, the mod's own sickness (ModState.IsSick) is what counts.
    /// </summary>
    public interface ICareerSickNote
    {
        /// <summary>True if the player has a sick note that covers this day.</summary>
        bool Covers(int day);
    }

    /// <summary>
    /// The career ladder's three optional doors to other features. Each is a plain static that is null
    /// until somebody sets it; every use is null-safe and wrapped, so a feature that is absent, switched
    /// off or faulty costs the career nothing.
    /// </summary>
    public static class CareerLinks
    {
        public static ICareerAcquaintances Social;
        public static ICareerRecord Law;
        public static ICareerSickNote Health;

        /// <summary>The employer the player works for right now (CareerCatalogue id), or null. Safe at any time.</summary>
        public static string EmployerId => CareerState.Loaded && CareerState.Employed ? CareerState.Employer.Id : null;

        /// <summary>The locale key of the player's job title right now, or null.</summary>
        public static string TitleKey => CareerState.Loaded && CareerState.Employed ? CareerState.CurrentRank.TitleKey : null;

        /// <summary>True if today is a rota day with the shift still to be worked.</summary>
        public static bool ShiftDueToday(int day) => CareerState.Loaded && CareerState.ShiftOpen(day);

        internal static void TellMet(Employer employer, int mate)
        {
            ICareerAcquaintances social = Social;
            if (social == null || employer == null)
            {
                return;
            }
            try
            {
                if (mate < 0)
                {
                    social.Met("career:" + employer.Id + ":boss", employer.BossKey, employer.NameKey, true);
                }
                else
                {
                    social.Met("career:" + employer.Id + ":mate_" + (mate + 1), employer.Colleagues[mate % employer.Colleagues.Length], employer.NameKey, false);
                }
            }
            catch (System.Exception e)
            {
                JobDirector.Logger?.Error(e);
            }
        }

        internal static bool SickNote(int day)
        {
            ICareerSickNote health = Health;
            if (health != null)
            {
                try
                {
                    return health.Covers(day);
                }
                catch (System.Exception e)
                {
                    JobDirector.Logger?.Error(e);
                }
            }
            // The mod's own sickness: sick now, or (for a day gone by) still sick when that day began.
            int today = TimeHelper.CurrentDay;
            return day >= today ? ModState.IsSick : ModState.SickUntil > 0f && ModState.SickUntil >= TimeHelper.NowInMinutes() - (today - day) * 1440f;
        }

        internal static string RecordLine(out bool clean)
        {
            clean = true;
            ICareerRecord law = Law;
            if (law == null)
            {
                return null;
            }
            try
            {
                clean = law.Clean;
                return law.Summary;
            }
            catch (System.Exception e)
            {
                JobDirector.Logger?.Error(e);
                return null;
            }
        }
    }
}
