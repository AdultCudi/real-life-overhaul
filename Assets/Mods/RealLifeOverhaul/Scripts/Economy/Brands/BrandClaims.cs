using System.Collections.Generic;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A franchisee's civil claim against the brand's owner, filed with the courts from outside the justice system
    /// (brands and franchises, through Wiring/BrandLinks, when a franchisee disputes the terms). It is one of
    /// Justice's own civil suits and nothing else, exactly as a lender's claim for a debt is (Justice/DebtClaims.cs):
    /// listed in the Legal file with the others, answered there (settled or contested) or left unanswered, judged by
    /// Justice on the day after its date, and what is then owed is one of Justice's civil debts, collected as Justice
    /// collects every debt. This file adds the three things brands need and Justice had no entry for: file one for a
    /// stated amount with a text that says who claims and why, ask whether it is still before the court, and
    /// withdraw it (the dispute was settled by giving way, or the agreement was ended for compensation).
    ///
    /// Nothing here runs on a timer. A claim is only taken while the justice system is on, has read the save and is
    /// working; otherwise <see cref="File"/> returns 0 and the dispute is answered in the Brands app alone.
    /// </summary>
    public static class BrandClaims
    {
        public static bool Available => DebtClaims.Available;

        /// <summary>Files a claim for an amount. Returns its number in the court's books, or 0 if there is no court to file with.</summary>
        public static int File(int amount)
        {
            if (!Available || amount <= 0)
            {
                return 0;
            }
            int today = TimeHelper.CurrentDay;
            int number = JusticeState.NextNumber++;
            JusticeState.Suits.Add(new JusticeSuit
            {
                Number = number, Claim = amount, DueDay = today + DebtClaims.AnswerDays, Injury = false, Seed = JusticeLaw.Hash("franchise" + number) ^ (today * 7919)
            });
            LegalPhone.Send("reallife_text_justice_suit_franchise", new Dictionary<string, string>
            {
                { "amount", JusticeText.Money(amount) }, { "day", JusticeText.N(today + DebtClaims.AnswerDays) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for " + amount + " filed by a franchisee who disputes the terms, answer by day " + (today + DebtClaims.AnswerDays) + ".");
            return number;
        }

        /// <summary>True while a claim with this number is still before the court.</summary>
        public static bool Open(int number)
        {
            return DebtClaims.Open(number);
        }

        /// <summary>The franchisee withdraws a claim that has not been judged. True if there was one.</summary>
        public static bool Withdraw(int number)
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
                LegalPhone.Send("reallife_text_justice_suit_franchise_withdrawn", new Dictionary<string, string> { { "amount", JusticeText.Money(amount) } });
                JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for " + amount + " was withdrawn by the franchisee.");
                return true;
            }
            return false;
        }
    }
}
