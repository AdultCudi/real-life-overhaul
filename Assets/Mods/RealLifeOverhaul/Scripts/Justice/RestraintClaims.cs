using System.Collections.Generic;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A former employer's civil claim over a broken restraint clause (Careers, through Wiring/CareersLinks: the player left
    /// and worked for themselves in the same field, or took colleagues along, within the clause's time). It is one of
    /// Justice's own civil suits, as a lender's claim for a debt is (Justice/DebtClaims): listed in the Legal file with the
    /// others, answered there (settled or contested) or left unanswered, judged by Justice on the day after its date, and
    /// what is then owed is one of Justice's civil debts.
    ///
    /// Nothing here runs on a timer. A claim is only taken while the justice system is on, has read the save and is
    /// working; otherwise <see cref="File"/> returns 0 and nothing is claimed.
    /// </summary>
    public static class RestraintClaims
    {
        /// <summary>Files a claim for an amount by a former employer. Returns its number in the court's books, or 0 if there is no court to file with.</summary>
        public static int File(int amount, string employer)
        {
            if (!DebtClaims.Available || amount <= 0)
            {
                return 0;
            }
            int today = TimeHelper.CurrentDay;
            int number = JusticeState.NextNumber++;
            JusticeState.Suits.Add(new JusticeSuit
            {
                Number = number, Claim = amount, DueDay = today + DebtClaims.AnswerDays, Injury = false, Seed = JusticeLaw.Hash("restraint" + number) ^ (today * 7919)
            });
            LegalPhone.Send("reallife_text_justice_suit_restraint", new Dictionary<string, string>
            {
                { "employer", employer ?? string.Empty }, { "amount", JusticeText.Money(amount) }, { "day", JusticeText.N(today + DebtClaims.AnswerDays) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for " + amount + " filed by a former employer over a restraint clause, answer by day " + (today + DebtClaims.AnswerDays) + ".");
            return number;
        }
    }
}
