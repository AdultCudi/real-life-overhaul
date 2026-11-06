using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Who the player's companies hire as executives, and the levels' codes, beside the one list of ranks both ladders
    /// share (Careers/Ladder/LadderRanks: Rung and ExecSeat, their names and numbers): the professions of the talent
    /// catalogue each seat is hired from, and the least level. Game-free: EconomyCheck compiles it.
    /// </summary>
    public static class ExecutiveHiring
    {
        /// <summary>The three executives' seats, in their order on the pages.</summary>
        public static readonly ExecSeat[] Seats = { ExecSeat.Coo, ExecSeat.Chro, ExecSeat.Cfo };

        /// <summary>
        /// The professions of the talent catalogue (Tech/People/TalentCatalogue) each seat is hired from, by ExecSeat:
        /// operations from those who run delivery, people from HR and recruiting, money from accounting. None for no seat.
        /// </summary>
        public static readonly int[][] SeatRoles =
        {
            new int[0],
            new[] { TalentCatalogue.ChiefExecutive, TalentCatalogue.EngineeringManager, TalentCatalogue.ProductManager, 30 /* business_analyst */ },
            new[] { 25 /* hr_manager */, 26 /* recruiter */ },
            new[] { TalentCatalogue.Accountant, TalentCatalogue.FinancialAnalyst }
        };

        /// <summary>The least level of the talent catalogue an executive is hired at (TalentCatalogue.Senior).</summary>
        public const int LeastLevel = TalentCatalogue.Senior;

        /// <summary>Whether a seat is one of the three.</summary>
        public static bool Real(int seat) => seat >= (int)ExecSeat.Coo && seat <= (int)ExecSeat.Cfo;

        /// <summary>Whether a profession of the talent catalogue may hold a seat.</summary>
        public static bool Fits(ExecSeat seat, int role, int level)
        {
            if (!Real((int)seat) || level < LeastLevel)
            {
                return false;
            }
            int[] roles = SeatRoles[(int)seat];
            for (int i = 0; i < roles.Length; i++)
            {
                if (roles[i] == role)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A rung's code as the suggestion's levels have them, "L0" to "L4": the floor and its shift supervisor L0, a
        /// store manager L1, a regional manager L2, an executive L3, the owner L4.
        /// </summary>
        public static string Code(Rung rung)
        {
            int l = rung <= Rung.Supervisor ? 0 : (int)rung - 1;
            return "L" + l.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
