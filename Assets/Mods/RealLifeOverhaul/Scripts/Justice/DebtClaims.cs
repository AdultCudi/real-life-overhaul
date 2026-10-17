using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>A lender's civil claim for a debt, one of Justice's own civil suits once filed. Nothing here runs on a timer.</summary>
    public static class DebtClaims
    {
        public const int AnswerDays = 5;

        /// <summary>True while the justice system is on, has read the save and is working: only then is a claim taken.</summary>
        public static bool Available => Justice.Active;

        /// <summary>Files a claim for an amount. Returns its number, or 0 if there is no court to file with.</summary>
        public static int File(int amount)
        {
            if (!Available || amount <= 0)
            {
                return 0;
            }
            JusticeSuit suit = Lodge(amount, "debt");
            LegalPhone.Send("reallife_text_justice_suit_debt", new Dictionary<string, string>
            {
                { "amount", JusticeText.Money(amount) }, { "day", JusticeText.N(suit.DueDay) }
            });
            JusticeDirector.Logger?.Info("Justice: civil claim " + suit.Number + " for a debt of " + amount + " filed by a lender, answer by day "
                                         + suit.DueDay + ".");
            return suit.Number;
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

        /// <summary>The lender withdraws a claim not yet judged (the debt was written off). True if there was one.</summary>
        public static bool Withdraw(int number)
        {
            if (!Remove(number, out int amount))
            {
                return false;
            }
            LegalPhone.Send("reallife_text_justice_suit_withdrawn", new Dictionary<string, string> { { "amount", JusticeText.Money(amount) } });
            JusticeDirector.Logger?.Info("Justice: civil claim " + number + " for a debt of " + amount + " was withdrawn by the lender.");
            return true;
        }

        /// <summary>A new suit due in <see cref="AnswerDays"/>, seeded by its claimant's tag and its number.</summary>
        internal static JusticeSuit Lodge(int amount, string seedTag)
        {
            int today = TimeHelper.CurrentDay;
            var suit = new JusticeSuit { Number = JusticeState.NextNumber++, Claim = amount, DueDay = today + AnswerDays };
            suit.Seed = JusticeLaw.Hash(seedTag + suit.Number) ^ (today * 7919);
            JusticeState.Suits.Add(suit);
            return suit;
        }

        /// <summary>Takes a suit not yet judged off the books. False if there was none with this number.</summary>
        internal static bool Remove(int number, out int amount)
        {
            List<JusticeSuit> suits = JusticeState.Suits;
            for (int i = suits.Count - 1; number > 0 && i >= 0; i--)
            {
                if (suits[i].Number == number)
                {
                    amount = suits[i].Claim;
                    suits.RemoveAt(i);
                    return true;
                }
            }
            amount = 0;
            return false;
        }
    }
}
