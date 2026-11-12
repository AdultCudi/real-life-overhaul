using System;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// The rival tycoons' cover. Each forward tycoon holds a health plan by the same plans and prices as the player's (the plan their fortune goes with), pays its
    /// month's premium from the worth over their floor, and loses it when they cannot. A hospital bill of theirs (Health gives them those) then costs them only what
    /// the plan leaves. They rest in a Going Public session, where the bridges answer that nobody is forward. COST: a few operations a rival a day.
    /// </summary>
    public sealed partial class InsuranceDirector
    {
        private int _rivalAt;
        private Func<int, long> _rivalQuote;   // made once, at the first day's pass
        private Func<long, long> _rivalPays;

        private void RivalDay(int day)
        {
            Func<int, long> worth = InsuranceBridges.RivalWorth, floor = InsuranceBridges.RivalFloor;
            if (worth == null || InsuranceBridges.RivalQuote == null || InsuranceBridges.RivalPay == null)
            {
                return;   // the rivals or Health are off: nobody holds anything new, nothing is taken
            }
            if (_rivalQuote == null)
            {
                _rivalQuote = plan => InsuranceBridges.RivalQuote != null ? InsuranceBridges.RivalQuote(plan) : 0L;
                _rivalPays = amount => InsuranceBridges.RivalPay != null ? InsuranceBridges.RivalPay(_rivalAt, amount) : 0L;
            }
            for (int i = 0; i < _state.Rivals.Length; i++)
            {
                RivalCover c = _state.Rivals[i];
                int plan = c.Plan;
                _rivalAt = i;
                long paid = InsuranceRules.RivalDay(c, worth(i), floor != null ? floor(i) : 0L, day, InsuranceGame.DaysPerYear, InsuranceGame.MonthDays, _rivalQuote, _rivalPays);
                if (Verbose && (paid > 0 || plan != c.Plan))
                {
                    Logger?.Info("Insurance check (a rival's cover, tycoon " + i + "): plan " + plan + " to " + c.Plan + ", " + paid + " paid today from the worth over their floor, "
                                 + "the next premium on day " + c.NextDue + ": " + (paid >= 0 ? "PASS" : "FAIL, a premium under nothing"));
                }
            }
        }

        /// <summary>
        /// A rival tycoon's hospital bill is about to leave their worth (Health's day): what is theirs to pay once their plan has paid its part by Health's own rule.
        /// The whole bill while insurance is off or they hold no plan.
        /// </summary>
        public long RivalBill(int tycoon, long bill)
        {
            if (!Running || tycoon < 0 || tycoon >= _state.Rivals.Length)
            {
                return bill;
            }
            RivalCover c = _state.Rivals[tycoon];
            long theirs = InsuranceRules.RivalBill(c, bill, InsuranceGame.Day, InsuranceGame.DaysPerYear, InsuranceBridges.RivalClaim);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Insurance check (a rival's hospital bill, tycoon " + tycoon + ", plan " + c.Plan + "): a bill of " + bill + ", their plan pays " + (bill - theirs) + ", they pay "
                             + theirs + ": " + (theirs >= 0 && theirs <= bill ? "PASS" : "FAIL, not between nothing and the bill"));
            }
            return theirs;
        }

        /// <summary>A rival's line for the rich list and their panel: the health plan they hold and what it has paid this year; empty with none.</summary>
        public string RivalLine(int tycoon)
        {
            if (!Running || tycoon < 0 || tycoon >= _state.Rivals.Length || _state.Rivals[tycoon].Plan <= 0)
            {
                return string.Empty;
            }
            return InsuranceText.RivalLine(tycoon, _state.Rivals[tycoon]);
        }
    }
}
