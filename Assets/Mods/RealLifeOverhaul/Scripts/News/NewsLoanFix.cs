using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One variable game loan the fix would help, with everything the question must show before it is changed.</summary>
    public struct LoanFixCase
    {
        public int Index;            // the loan's place in the game's list at the time it was read
        public int Remaining;        // what is left to repay, whole money
        public int OldDaily;         // its daily interest now
        public int NewDaily;         // and fixed
        public int OldRateBp;        // what it costs a year now, basis points of what is left
        public int NewRateBp;        // the fixed rate a year, basis points
        public int Saving;           // a month, whole money
        public int Fee;              // taken when it is fixed; there is none
    }

    /// <summary>The game's side of "Fix your loan's rate": the loan to fix, the question box, the change itself and the page the press falls back to.</summary>
    public interface ILoanFixDesk
    {
        /// <summary>The variable game loan the fix helps most today; false if no loan would gain.</summary>
        bool Find(out LoanFixCase found);

        /// <summary>The game's question box with the case; <paramref name="yes"/> runs only when the player says yes.</summary>
        void Ask(LoanFixCase asked, Action yes);

        /// <summary>Writes the fixed term onto the loan. The only place a loan is changed.</summary>
        void Fix(LoanFixCase fixing);

        /// <summary>The Economy app's own page, for a press when there is nothing to fix.</summary>
        bool OpenApp();
    }

    /// <summary>
    /// Fix your loan's rate (News, Insights): the case worked out from the loan the same way for the player and a rival, and the press that asks first.
    /// A loan is never changed on a press: the game's question shows the old rate, the new rate, the saving and the fee, and the loan is changed only on yes.
    /// Game-free.
    /// </summary>
    public static class NewsLoanFix
    {
        /// <summary>Taking the fixed rate costs nothing: the loan keeps its balance and its payment, only the day's interest is fixed.</summary>
        public static int FeeOf(int remaining) => 0;

        /// <summary>
        /// The case for one loan: the fixed rate from the same terms a consolidation uses, what a month saves, and whether the fix is wanted (a rise is set, or it
        /// saves anyway, and the fixed day is cheaper). False for a loan the fix would not help.
        /// </summary>
        public static bool Offer(int index, int remaining, int dailyInterest, int score, int ownRateBp, int daysPerYear, bool rising, out LoanFixCase made)
        {
            made = default(LoanFixCase);
            if (remaining <= 0 || dailyInterest <= 0 || daysPerYear <= 0)
            {
                return false;
            }
            int saving = NewsRules.Offer(remaining, dailyInterest, score, ownRateBp, daysPerYear, out int fixedDaily);
            if (!NewsRules.WillFix(rising || saving > 0, dailyInterest, fixedDaily) || saving <= 0)
            {
                return false;
            }
            made = new LoanFixCase
            {
                Index = index,
                Remaining = remaining,
                OldDaily = dailyInterest,
                NewDaily = fixedDaily,
                OldRateBp = (int)((long)dailyInterest * daysPerYear * 10000L / remaining),
                NewRateBp = ProductTerms.FixedRateBp(score, ownRateBp, 0, 1f),
                Saving = saving,
                Fee = FeeOf(remaining)
            };
            return true;
        }

        /// <summary>
        /// The press. With a loan to fix it asks first and changes nothing until yes; the loan is read again when yes comes, and is changed only if it is
        /// still the same place in the list and the fix still saves. With none to fix it opens the Economy app. True when something was opened or asked.
        /// </summary>
        public static bool Press(ILoanFixDesk desk)
        {
            if (!desk.Find(out LoanFixCase asked))
            {
                return desk.OpenApp();
            }
            desk.Ask(asked, () =>
            {
                if (desk.Find(out LoanFixCase now) && now.Index == asked.Index)
                {
                    desk.Fix(now);
                }
            });
            return true;
        }
    }
}
