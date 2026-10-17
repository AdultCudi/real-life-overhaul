using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>A rival tycoon's claim against the player, filed like <see cref="DebtClaims"/>. Nothing here runs on a timer.</summary>
    public static class RivalClaims
    {
        public static int File(long amount, string claimant)
        {
            if (!DebtClaims.Available || amount <= 0)
            {
                return 0;
            }
            int claim = amount > int.MaxValue ? int.MaxValue : (int)amount;
            JusticeSuit suit = DebtClaims.Lodge(claim, "rival");
            LegalPhone.Send("reallife_text_justice_suit_rival", new Dictionary<string, string>
            {
                { "rival", claimant ?? string.Empty }, { "amount", JusticeText.Money(claim) }, { "day", JusticeText.N(suit.DueDay) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + suit.Number + " for " + claim + " filed by " + claimant
                                         + " (a rival tycoon's holding), answer by day " + suit.DueDay + ".");
            return suit.Number;
        }

        /// <summary>A rival's claim not yet judged is withdrawn (a test put back). True if there was one.</summary>
        public static bool Withdraw(int number, string claimant)
        {
            if (!DebtClaims.Remove(number, out int amount))
            {
                return false;
            }
            LegalPhone.Send("reallife_text_justice_suit_rival_withdrawn", new Dictionary<string, string>
            {
                { "rival", claimant ?? string.Empty }, { "amount", JusticeText.Money(amount) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for " + amount + " by " + claimant + " was withdrawn (a test put back).");
            return true;
        }
    }
}
