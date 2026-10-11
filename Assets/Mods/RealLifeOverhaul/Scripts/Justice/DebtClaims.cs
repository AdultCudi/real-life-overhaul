using System.Collections.Generic;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A lender's civil claim for a debt, filed with the courts from outside the justice system (the economy, through
    /// Wiring/EconomyLinks). It is one of Justice's own civil suits and nothing else: it is listed in the Legal file
    /// with the others, answered there (settled or contested) or left unanswered, judged by Justice on the day after
    /// its date, and what is then owed is one of Justice's civil debts, collected as Justice collects every debt.
    /// This file adds the three things an outside feature needs and Justice had no entry for: file one for a stated
    /// amount, ask whether it is still before the court, and withdraw it.
    ///
    /// Nothing here runs on a timer. A claim is only taken while the justice system is on, has read the save and is
    /// working; otherwise <see cref="File"/> returns 0 and the lender keeps its debt.
    /// </summary>
    public static class DebtClaims
    {
        /// <summary>Days to answer a claim, as for the claims Justice files itself.</summary>
        public const int AnswerDays = 5;

        public static bool Available
        {
            get
            {
                JusticeDirector director = JusticeDirector.Instance;
                return JusticeSettings.Enabled && JusticeState.Loaded && director != null && director.Working;
            }
        }

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
                Number = number, Claim = amount, DueDay = today + AnswerDays, Injury = false, Seed = JusticeLaw.Hash("debt" + number) ^ (today * 7919)
            });
            LegalPhone.Send("reallife_text_justice_suit_debt", new Dictionary<string, string>
            {
                { "amount", JusticeText.Money(amount) }, { "day", JusticeText.N(today + AnswerDays) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for a debt of " + amount + " filed by a lender, answer by day " + (today + AnswerDays) + ".");
            return number;
        }

        /// <summary>True while a claim with this number is still before the court.</summary>
        public static bool Open(int number)
        {
            List<JusticeSuit> suits = JusticeState.Suits;
            for (int i = 0; number > 0 && i < suits.Count; i++)
            {
                if (suits[i].Number == number)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The lender withdraws a claim that has not been judged (the debt behind it has been written off). True if there was one.</summary>
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
                LegalPhone.Send("reallife_text_justice_suit_withdrawn", new Dictionary<string, string> { { "amount", JusticeText.Money(amount) } });
                JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for a debt of " + amount + " was withdrawn by the lender.");
                return true;
            }
            return false;
        }
    }
}
