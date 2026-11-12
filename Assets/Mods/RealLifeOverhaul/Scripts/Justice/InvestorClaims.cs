using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The investors' civil claim against the player for the money of a firm that was spent elsewhere and not paid back, filed like
    /// <see cref="DebtClaims"/> and one of Justice's own civil suits from then on. Nothing here runs on a timer.
    /// </summary>
    public static class InvestorClaims
    {
        /// <summary>Files a claim for an amount in whole money by the firm's investors. Returns its number, or 0 with no court to file with.</summary>
        public static int File(long amount, string firm, string lead)
        {
            if (!DebtClaims.Available || amount <= 0)
            {
                return 0;
            }
            int claim = amount > int.MaxValue ? int.MaxValue : (int)amount;
            JusticeSuit suit = DebtClaims.Lodge(claim, "investor");
            LegalPhone.Send("reallife_text_justice_suit_investor", new Dictionary<string, string>
            {
                { "lead", lead ?? string.Empty }, { "firm", firm ?? string.Empty }, { "amount", JusticeText.Money(claim) }, { "day", JusticeText.N(suit.DueDay) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + suit.Number + " for " + claim + " filed by the investors of " + firm + ", answer by day " + suit.DueDay + ".");
            return suit.Number;
        }

        /// <summary>A claim not yet judged is taken back (a test put back). True if there was one.</summary>
        public static bool Withdraw(int number)
        {
            if (!DebtClaims.Remove(number, out int amount))
            {
                return false;
            }
            JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for " + amount + " by investors was taken back unheard (a test put back).");
            return true;
        }
    }
}
