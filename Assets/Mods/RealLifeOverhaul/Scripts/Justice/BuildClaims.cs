using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A claim against the player's construction company for faults in a building it put up, by its owner, its residents or
    /// the city, filed like <see cref="DebtClaims"/> and one of Justice's own civil suits from then on. Nothing here runs on a timer.
    /// </summary>
    public static class BuildClaims
    {
        /// <summary>Files a claim for an amount by a claimant (a name as words). Returns its number, or 0 with no court to file with.</summary>
        public static int File(long amount, string claimant, string building)
        {
            if (!DebtClaims.Available || amount <= 0)
            {
                return 0;
            }
            int claim = amount > int.MaxValue ? int.MaxValue : (int)amount;
            JusticeSuit suit = DebtClaims.Lodge(claim, "build");
            LegalPhone.Send("reallife_text_justice_suit_build", new Dictionary<string, string>
            {
                { "claimant", claimant ?? string.Empty }, { "building", building ?? string.Empty }, { "amount", JusticeText.Money(claim) }, { "day", JusticeText.N(suit.DueDay) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + suit.Number + " for " + claim + " filed by " + claimant + " over faults at " + building + ", answer by day "
                                         + suit.DueDay + ".");
            return suit.Number;
        }

        /// <summary>
        /// A claim against the player's company by somebody it left unpaid: a crew's wages, a merchant's account. Filed like a
        /// claim for faults. Returns its number, or 0 with no court to file with.
        /// </summary>
        public static int FileUnpaid(long amount, string claimant, string what)
        {
            if (!DebtClaims.Available || amount <= 0)
            {
                return 0;
            }
            int claim = amount > int.MaxValue ? int.MaxValue : (int)amount;
            JusticeSuit suit = DebtClaims.Lodge(claim, "unpaid");
            LegalPhone.Send("reallife_text_justice_suit_unpaid", new Dictionary<string, string>
            {
                { "claimant", claimant ?? string.Empty }, { "what", what ?? string.Empty }, { "amount", JusticeText.Money(claim) }, { "day", JusticeText.N(suit.DueDay) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + suit.Number + " for " + claim + " filed by " + claimant + " over " + what + " left unpaid, answer by day "
                                         + suit.DueDay + ".");
            return suit.Number;
        }

        /// <summary>A claim against the company not yet judged is taken back (a test put back). True if there was one.</summary>
        public static bool Withdraw(int number)
        {
            if (!DebtClaims.Remove(number, out int amount))
            {
                return false;
            }
            JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for " + amount + " was taken back unheard (a test put back).");
            return true;
        }
    }
}
