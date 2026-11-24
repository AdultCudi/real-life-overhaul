using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Black card tier in the bank's day (BlackTerms): the richest of the bank's savers who meet the owner's wealth and score lines, the card's take-up by its fee and
    /// rewards, what it earns and costs in the card books, its defaults by the loans' risk model, and its balance moving to what its limits make of it. Game-free; a few sums
    /// a day and nothing allocated.
    /// </summary>
    public static partial class OwnBankRules
    {
        /// <summary>The bank's savers who hold at least the wealth line: its people clients' number by the tail of what savers hold.</summary>
        public static int BlackRich(OwnBank b, int wealthStep)
        {
            long clients = b.Deposits[OwnBankCatalogue.People] / CardTerms.SaverCents[OwnBankCatalogue.People];
            return (int)Math.Min(int.MaxValue / 4, clients * BlackTerms.TailPpm(BlackTerms.Wealth(wealthStep)) / 1000000L);
        }

        /// <summary>
        /// The share of the rich whose credit score is at or over the line, thousandths, and their defaults against the best kind's (thousandths) for the scores that clear it:
        /// a lower line lets in worse clients.
        /// </summary>
        public static void BlackScores(int line, out int share, out int mult)
        {
            double mu = BlackTerms.ScoreMean, sd = BlackTerms.ScoreSd, z = (line - mu) / sd, pa = 1.0 - Phi(z);
            share = (int)Math.Round(pa * 1000.0);
            double mean = pa > 1e-9 ? mu + sd * Math.Exp(-0.5 * z * z) / Math.Sqrt(2.0 * Math.PI) / pa : line;
            mult = (int)Math.Round(1000.0 * Math.Exp(-(mean - mu) / CardTerms.ScoreScale));
        }

        /// <summary>The Black cards its clients would hold at the owner's lines, fee and rewards: the qualifying rich, their take-up, less for a dearer fee and more for richer rewards.</summary>
        public static int BlackWanted(OwnBank b)
        {
            BlackScores(b.BlackScore, out int share, out _);
            double fee = BlackTerms.FeeSteps[BlackTerms.Clamp(b.BlackFee, 0, BlackTerms.FeeSteps.Length - 1)] / 100.0;
            double fees = Math.Max(0.1, 1.0 - BlackTerms.FeePull * fee / 1000.0);
            double rewards = Math.Exp(BlackTerms.RewardPull / 100.0 * (BlackTerms.Rewards(b.BlackReward) - BlackTerms.Rewards(BlackTerms.RewardDefault)) / 100.0);
            double wanted = BlackRich(b, b.BlackWealth) * (share / 1000.0) * (BlackTerms.TakeUp / 1000.0) * fees * rewards;
            return (int)Math.Min(int.MaxValue / 4, Math.Round(wanted));
        }

        /// <summary>The rate a Black card bears: the bank's card rate less the top tier's privilege, never under the loans' floor.</summary>
        public static int BlackBp(BankCity c, OwnBank b) => Math.Max(CreditCatalogue.RateFloor * 100, CardBp(c, b) - CardTerms.BlackDiscountBp);

        /// <summary>The Black card's defaults, thousandths a year, by the loans' model for the best kind of borrower and the scores approved.</summary>
        public static int BlackDefaultPerMille(BankCity c, OwnBank b)
        {
            return (int)Math.Round(DefaultPerMille(c, b, OwnBankCatalogue.Good) * (double)BlackTerms.DefaultMult / 1000.0 * Math.Max(1, b.BlackRisk) / 1000.0);
        }

        /// <summary>What the tier does in a month's figures, for the page: earnings less rewards and running costs, fraud and write-offs, as the other kinds' are counted (CardMonthProfit).</summary>
        public static long BlackMonth(OwnBank b) => b.BlackFig[CardTerms.FigInterest] + b.BlackFig[CardTerms.FigInterchange] + b.BlackFig[CardTerms.FigFees]
                                                    - b.BlackFig[CardTerms.FigLost] - b.BlackFig[CardTerms.FigFraud] - b.BlackFig[CardTerms.FigCosts];

        private static void TakeBlack(OwnBank b, int count, int mult)
        {
            if (count <= 0)
            {
                return;
            }
            long have = b.BlackHolders;
            b.BlackRisk = (int)((b.BlackRisk * have + (long)mult * count) / (have + count));
            b.BlackHolders += count;
        }

        /// <summary>
        /// The tier's day, after the five kinds': spending and what it earns and costs go into the day's card lines and the capital as the kinds' do (the month's figures
        /// for the tier are kept apart too), defaults by the loans' model, the clients the bank's lines draw, and the balance carried. Nothing while none is out and the tier is off.
        /// </summary>
        private static void BlackDay(OwnBank b, BankCity c, BankDay into, long hold, int cycle, in BankCaps caps, bool stopped)
        {
            if (!b.BlackOn && b.BlackHolders == 0 && b.BlackBal == 0 && b.BlackArr == 0)
            {
                return;
            }
            int y = Math.Max(1, c.YearDays), m = Math.Max(1, c.MonthDays);
            long level = Level(c.Level);
            int h = b.BlackHolders;
            long bal = b.BlackBal, arr = b.BlackArr;
            long spend = h * BlackTerms.SpendDay * level / PriceRules.Start * CardTerms.SpendCycle[cycle] / 1000;
            long interchange = spend * BlackTerms.InterchangeBp / 10000;
            long interest = bal * BlackTerms.Revolve / 1000 * BlackBp(c, b) / (10000L * y);
            long fee = h * BlackTerms.FeeCents(b.BlackFee, c.Level) / y;
            long rewards = spend * BlackTerms.Rewards(b.BlackReward) / 10000;
            long process = h * (CardTerms.CardDayCents + BlackTerms.ConciergeDayCents) * level / PriceRules.Start;
            long fraud = spend * CardTerms.FraudBp / 10000;
            b.Reserves += interchange + interest + fee - rewards - process - fraud;
            b.Capital += interchange + interest + fee - rewards - process - fraud;
            into.CardInterchange += interchange;
            into.CardInterest += interest;
            into.CardFees += fee;
            into.CardCosts += rewards + process;
            into.CardFraud += fraud;
            b.CardLostAll += fraud;
            b.BlackFig[CardTerms.FigInterchange] += interchange;
            b.BlackFig[CardTerms.FigInterest] += interest;
            b.BlackFig[CardTerms.FigFees] += fee;
            b.BlackFig[CardTerms.FigCosts] += rewards + process;
            b.BlackFig[CardTerms.FigFraud] += fraud;
            b.BlackFig[CardTerms.FigSpend] += spend;
            // The same figures go to the month's card lines of every kind, so the Cards page's month and the bank's books agree.
            b.CardFig[CardTerms.FigInterchange] += interchange;
            b.CardFig[CardTerms.FigInterest] += interest;
            b.CardFig[CardTerms.FigFees] += fee;
            b.CardFig[CardTerms.FigCosts] += rewards + process;
            b.CardFig[CardTerms.FigFraud] += fraud;
            b.CardFig[CardTerms.FigSpend] += spend;
            // Defaults, arrears settled over a month.
            long defaulted = Math.Min(bal, (long)Math.Round(bal * (double)BlackDefaultPerMille(c, b) / (1000.0 * y)));
            bal -= defaulted;
            arr += defaulted;
            long settled = arr < 100 * m ? arr : arr / m;
            long cured = settled * BlackTerms.CureShare / 1000, written = settled - cured;
            arr -= settled;
            bal += cured;
            b.Capital -= written;
            into.CardLost += written;
            b.CardLostAll += written;
            b.BlackFig[CardTerms.FigLost] += written;
            b.CardFig[CardTerms.FigLost] += written;
            // The rich the bank's lines draw take the card at once; those the lines or the price lose, or whose savings left, give it up.
            int demand = b.CardsOn && b.BlackOn ? BlackWanted(b) : 0;
            if (b.CardsOn && b.BlackOn && !stopped)
            {
                int gap = demand - h;
                if (gap > 0)
                {
                    BlackScores(b.BlackScore, out _, out int mult);
                    TakeBlack(b, (int)Math.Ceiling(gap * (double)BlackTerms.Follow / (1000.0 * m)), mult);
                }
            }
            if (b.BlackHolders > demand)
            {
                int lost = (int)Math.Ceiling((b.BlackHolders - demand) * (double)BlackTerms.Follow / (1000.0 * m));
                b.BlackHolders = Math.Max(demand, b.BlackHolders - lost);
            }
            h = b.BlackHolders;
            long limits = h * OwnBankRules.At(BlackTerms.LimitCents, c.Level);
            long target = limits * BlackTerms.Util / 1000 * CardTerms.UtilCycle[cycle] / 1000;
            long move = (target - bal) * CardTerms.UtilFollow / (1000L * m);
            if (move > 0)
            {
                long cash = FreeCash(b) - hold;
                long room = FreeCapital(b) * 1000 / LendRatioOf(b) - RiskWeighted(b);
                long byBook = LoanRoom(b, caps);
                long most = Math.Min(Math.Max(0, cash), Math.Max(0, room) * 1000 / CardTerms.CardWeight);
                if (byBook != BankTiers.NoLimit)
                {
                    most = Math.Min(most, byBook);
                }
                move = stopped ? 0 : Math.Min(move * LendScale(b) / 1000, Math.Max(0, most));
            }
            else
            {
                move = Math.Max(move, -bal);
            }
            bal += move;
            b.Reserves -= move;
            b.BlackBal = bal;
            b.BlackArr = arr;
        }
    }
}
