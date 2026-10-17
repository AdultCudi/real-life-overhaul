using System.Collections.Generic;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A franchisee's civil claim against the brand's owner (through Wiring/BrandLinks): one of Justice's own civil
    /// suits, as a lender's claim is (Justice/DebtClaims), filed, asked after and withdrawn here. While Justice is not
    /// working, <see cref="File"/> returns 0 and the dispute is answered in the Brands app alone. Nothing on a timer.
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
            JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for " + amount + " filed by a franchisee who disputes the terms, answer by day "
                                         + (today + DebtClaims.AnswerDays) + ".");
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
