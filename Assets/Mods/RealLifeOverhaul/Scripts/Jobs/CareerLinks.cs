namespace RealLifeOverhaul
{
    /// <summary>Somebody the player came to know at work, told to a social feature through <see cref="CareerLinks.Social"/>.</summary>
    public interface ICareerAcquaintances
    {
        /// <summary>The player worked a shift alongside this person, or reports to them; at most once a shift per person.</summary>
        /// <param name="personId">Stable across games and saves: "career:courier:mate_1", "career:courier:boss".</param>
        void Met(string personId, string nameKey, string employerNameKey, bool isBoss);
    }

    /// <summary>The player's record with the law as an employer reads it on a CV, set through <see cref="CareerLinks.Law"/>.</summary>
    public interface ICareerRecord
    {
        /// <summary>True if there is nothing on the record an employer would mind.</summary>
        bool Clean { get; }
        /// <summary>One line for the CV, already in the player's language; null or empty for nothing to show.</summary>
        string Summary { get; }
    }

    /// <summary>Whether the player is signed off sick, set by a health feature through <see cref="CareerLinks.Health"/>; else ModState.IsSick counts.</summary>
    public interface ICareerSickNote
    {
        /// <summary>True if the player has a sick note that covers this day.</summary>
        bool Covers(int day);
    }

    /// <summary>
    /// Where an employer with no help-wanted board has its shifts worked, and what a rank of it asks, set by the building trades
    /// (Scripts/BuildTrades) through <see cref="CareerLinks.Workplace"/>: the nearest works site for a builder or the city's
    /// building control, the practice's floor for the architects. Without it those employers have no office and ask nothing.
    /// </summary>
    public interface ICareerWorkplace
    {
        /// <summary>The place the employer's shifts are worked nearest a point, and its address as the game writes it; false for none.</summary>
        bool Depot(string employerId, UnityEngine.Vector3 near, out UnityEngine.Vector3 at, out string address);

        /// <summary>The locale key of what the player still lacks for a rank's <see cref="CareerRank.Needs"/>, or null when they hold it.</summary>
        string Lacks(string needs);
    }

    /// <summary>The career's optional doors to other features, null until set; every use is wrapped, so an absent or faulty feature costs nothing.</summary>
    public static class CareerLinks
    {
        public static ICareerAcquaintances Social;
        public static ICareerRecord Law;
        public static ICareerSickNote Health;
        public static ICareerWorkplace Workplace;

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

        /// <summary>The place an employer with no board works from, nearest the player; false for none (or no building trades).</summary>
        internal static bool WorkplaceOf(Employer employer, UnityEngine.Vector3 near, out UnityEngine.Vector3 at, out string address)
        {
            at = default;
            address = null;
            ICareerWorkplace place = Workplace;
            if (place == null || employer == null)
            {
                return false;
            }
            try
            {
                return place.Depot(employer.Id, near, out at, out address);
            }
            catch (System.Exception e)
            {
                JobDirector.Logger?.Error(e);
                return false;
            }
        }

        /// <summary>The locale key of what the player lacks for a rank, or null when it asks nothing or they hold it. Without the building trades a rank that asks something is refused.</summary>
        internal static string Lacks(CareerRank rank)
        {
            if (rank == null || rank.Needs == null)
            {
                return null;
            }
            ICareerWorkplace place = Workplace;
            if (place == null)
            {
                return "reallife_career_needs_trades_off";
            }
            try
            {
                return place.Lacks(rank.Needs);
            }
            catch (System.Exception e)
            {
                JobDirector.Logger?.Error(e);
                return "reallife_career_needs_trades_off";
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
            // The mod's own sickness: sick now, or for a day gone by, still sick when that day began.
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
