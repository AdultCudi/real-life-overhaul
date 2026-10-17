using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>A former employer's claim over a restraint clause, filed like <see cref="DebtClaims"/>. Nothing here runs on a timer.</summary>
    public static class RestraintClaims
    {
        public static int File(int amount, string employer)
        {
            if (!DebtClaims.Available || amount <= 0)
            {
                return 0;
            }
            JusticeSuit suit = DebtClaims.Lodge(amount, "restraint");
            LegalPhone.Send("reallife_text_justice_suit_restraint", new Dictionary<string, string>
            {
                { "employer", employer ?? string.Empty }, { "amount", JusticeText.Money(amount) }, { "day", JusticeText.N(suit.DueDay) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + suit.Number + " for " + amount
                                         + " filed by a former employer over a restraint clause, answer by day " + suit.DueDay + ".");
            return suit.Number;
        }
    }
}
