using Entities;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's own hire of a candidate pays what the negotiation's Accept pays (CandidateSalaryNegotiation.AcceptOffer sets the wage to the offer's:
    /// the stored wage rounded up to 5 cents), so the kit's list, its review, the negotiation and the hire say one figure. Called after the game's
    /// hire with the figure asked beforehand; a person not hired, not this machine's to pay (Going Public) or asking nothing more is left as they
    /// are. The game's own Hire button is not touched, and nothing is written for a candidate who is not hired. COST: a few comparisons a hire.
    /// </summary>
    internal static class AskedHire
    {
        public static void After(EmployeeInstance e, float asked)
        {
            if (e != null && e.candidateInfo == null && asked > e.hourlyWage && WageShown.Ours(e))
            {
                e.hourlyWage = asked;
            }
        }
    }
}
