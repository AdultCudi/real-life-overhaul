using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's Black card tier (Economy/OwnBank/Cards), game-free: a top client tier the owner offers on lines of their own, a least wealth and a least credit
    /// score, for a yearly fee and a rate of rewards. It sits beside the five kinds of card (<see cref="CardTerms"/>) and runs through the same day, the same
    /// books (interest, the shops' fee, fees, rewards and running costs, fraud, write-offs) and the same risk model, the clients being the bank's richest savers.
    /// Money in cents at the start's prices unless said; shares in thousandths; rates in hundredths of a per cent.
    /// </summary>
    public static class BlackTerms
    {
        public const string NameKey = "reallife_ob_black_tier";   // the tier's name as the page shows it

        // The lines the owner sets: the least a client holds (cents at the start's prices), the least credit score, the yearly fee and the cashback paid on the spend.
        public static readonly long[] WealthLines = { 10000000L, 25000000L, 50000000L, 100000000L };   // $100,000 to $1,000,000
        public const int WealthDefault = 1;
        public const int ScoreLeast = 700, ScoreMost = 840, ScoreStep = 20, ScoreDefault = 760;
        public static readonly long[] FeeSteps = { 25000L, 50000L, 75000L, 150000L };                // $250 to $1,500 a year
        public const int FeeDefault = 1;
        public static readonly int[] RewardBp = { 100, 150, 200, 300 };                              // 1% to 3% of the spend, hundredths of a per cent
        public const int RewardDefault = 1;

        // Who the clients are: savers of the people's kind whose holdings fall in the tail of a Pareto spread about what a saver holds on average.
        public const int TailAlpha = 125;                     // the spread's shape, hundredths: 1.25 puts about one saver in 770 over $100,000 at the start's prices
        public const int TakeUp = 500;                        // of the qualifying clients who take the card at the default fee and rewards, thousandths
        public const int FeePull = 1;                         // thousandths of the take-up lost for each dollar of the yearly fee
        public const int RewardPull = 50;                     // e to this many hundredths for each hundredth of a point of rewards over the default
        public const int ScoreMean = 790, ScoreSd = 50;       // the credit scores of the rich: better than the bank's best kind of card client

        // What a card does in a day, and costs.
        public const long LimitCents = 5000000L;              // $50,000 a card
        public const long SpendDay = 45000L;                  // spent on one card, cents a day: $450
        public const int Util = 120;                          // the balance carried, thousandths of the limit: the rich pay off
        public const int Revolve = 150;                       // of the balance, the part that bears interest
        public const int InterchangeBp = 180;                 // the shops pay more on a premium card
        public const long ConciergeDayCents = 60L;            // the concierge, lounges and insurance of one card a day, on top of the ordinary processing
        public const int DefaultMult = 800;                   // defaults against a loan to the bank's best kind of borrower, thousandths
        public const int CureShare = 250;                     // of what a month settles in arrears, the part put right
        public const int Follow = 250;                        // a month's way to the cards wanted, thousandths

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        public static long FeeCents(int step, int level) => OwnBankRules.At(FeeSteps[Clamp(step, 0, FeeSteps.Length - 1)], level);

        public static int Rewards(int step) => RewardBp[Clamp(step, 0, RewardBp.Length - 1)];

        public static long Wealth(int step) => WealthLines[Clamp(step, 0, WealthLines.Length - 1)];

        public static int ScoreOf(int score) => Clamp(score / ScoreStep * ScoreStep, ScoreLeast, ScoreMost);

        /// <summary>The share of savers holding at least a line, thousandths of a thousandth (parts per million): the tail of a Pareto spread whose mean is what a saver holds.</summary>
        public static int TailPpm(long lineCents)
        {
            double alpha = TailAlpha / 100.0, mean = CardTerms.SaverCents[OwnBankCatalogue.People];
            double least = mean * (alpha - 1.0) / alpha;
            double share = lineCents <= least ? 1.0 : Math.Pow(least / lineCents, alpha);
            return (int)Math.Round(share * 1000000.0);
        }
    }
}
