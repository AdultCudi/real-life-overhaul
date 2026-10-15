using System.Collections.Generic;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A rival tycoon's civil claim against the player (Scripts/Rivals, through Wiring/RivalLinks: a holding sues over the
    /// player's business dealings). It is one of Justice's own civil suits, as a lender's claim for a debt is
    /// (Justice/DebtClaims): listed in the Legal file with the others, answered there (settled or contested) or left
    /// unanswered, judged by Justice on the day after its date, and what is then owed is one of Justice's civil debts.
    ///
    /// Nothing here runs on a timer. A claim is only taken while the justice system is on, has read the save and is
    /// working; otherwise <see cref="File"/> returns 0 and nothing is claimed.
    /// </summary>
    public static class RivalClaims
    {
        /// <summary>Files a claim for an amount by a rival's holding. Returns its number in the court's books, or 0 if there is no court to file with.</summary>
        public static int File(long amount, string claimant)
        {
            if (!DebtClaims.Available || amount <= 0)
            {
                return 0;
            }
            int claim = amount > int.MaxValue ? int.MaxValue : (int)amount;
            int today = TimeHelper.CurrentDay;
            int number = JusticeState.NextNumber++;
            JusticeState.Suits.Add(new JusticeSuit
            {
                Number = number, Claim = claim, DueDay = today + DebtClaims.AnswerDays, Injury = false, Seed = JusticeLaw.Hash("rival" + number) ^ (today * 7919)
            });
            LegalPhone.Send("reallife_text_justice_suit_rival", new Dictionary<string, string>
            {
                { "rival", claimant ?? string.Empty }, { "amount", JusticeText.Money(claim) }, { "day", JusticeText.N(today + DebtClaims.AnswerDays) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for " + claim + " filed by " + claimant + " (a rival tycoon's holding), answer by day " + (today + DebtClaims.AnswerDays) + ".");
            return number;
        }

        /// <summary>A claim of a rival's not yet judged is withdrawn (a test put back). True if there was one.</summary>
        public static bool Withdraw(int number, string claimant)
        {
            List<JusticeSuit> suits = JusticeState.Suits;
            for (int i = suits.Count - 1; number > 0 && i >= 0; i--)
            {
                if (suits[i].Number != number)
                {
                    continue;
                }
                int amount = suits[i].Claim;
                suits.RemoveAt(i);
                LegalPhone.Send("reallife_text_justice_suit_rival_withdrawn", new Dictionary<string, string> { { "rival", claimant ?? string.Empty }, { "amount", JusticeText.Money(amount) } });
                JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for " + amount + " by " + claimant + " was withdrawn (a test put back).");
                return true;
            }
            return false;
        }
    }
}
