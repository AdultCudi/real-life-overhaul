using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the credit file's own-bank card tells the bank (the owner's top tier card), written by the account's arithmetic as it happens (AccountRules) into the
    /// bank's record of what the card did since its last day, so the bank's books and the owner's account never part. Game-free. Nothing while no bank hears.
    /// </summary>
    public static class OwnCardSink
    {
        /// <summary>The running bank whose owner holds the card; set and cleared by the bank's director.</summary>
        public static OwnBank Bank;

        /// <summary>The credit side's answers, set while it runs: whole money owed on the owner's card (-1: no such card), and the card let go of the bank (its debt stays).</summary>
        public static Func<int> Owed;
        public static Action Orphan;
        public static Func<int, int, string, bool> Open;      // limit in whole money, rate in hundredths of a per cent, the name it is shown under
        public static Func<bool> Close;                        // closed if nothing is owed

        public const int Drawn = 0, Paid = 1, Interest = 2, Fee = 3, Lost = 4;

        /// <summary>A movement on the owner's card, in whole money; heard only for the card of a bank that runs.</summary>
        public static void Moved(bool own, int kind, int amount)
        {
            OwnBank b = Bank;
            if (!own || amount <= 0 || b == null || !b.Running || !b.OwnCard)
            {
                return;
            }
            long cents = amount * 100L;
            switch (kind)
            {
                case Drawn: b.OwnDrawn += cents; break;
                case Paid: b.OwnPaid += cents; break;
                case Interest: b.OwnInterest += cents; break;
                case Fee: b.OwnFees += cents; break;
                default: b.OwnLost += cents; break;
            }
        }

        /// <summary>True if the bank can fund a draw of this many whole money now: its cash over the reserve and the fixed pay it keeps back.</summary>
        public static bool CanFund(int amount)
        {
            OwnBank b = Bank;
            return b != null && b.Running && b.OwnCard && !b.Run && b.Stage != Regulator.Breach && amount * 100L <= OwnBankRules.FreeCash(b) - (b.OwnDrawn - b.OwnPaid);
        }
    }

    /// <summary>
    /// The bank's credit card business in its day (CardTerms): applicants of each kind of client in proportion to the clients the bank has and to how its
    /// card is priced, approved at once, queued or declined by credit score; what a card earns (interest on what is carried, interchange on what is spent, a
    /// yearly fee) and costs (rewards, processing, fraud, defaults by the loans' risk model); balances moving towards what each kind carries; the owner's own
    /// card as the credit file moves it. Game-free; a few sums a kind a day and nothing allocated.
    /// </summary>
    public static partial class OwnBankRules
    {
        /// <summary>What the rival banks charge on a card, hundredths of a per cent a year: the fair borrowers' loan rate and the premium a card carries.</summary>
        public static int CardRivalBp(BankCity c) => RivalLoanBp(c, null, OwnBankCatalogue.Fair) + CardTerms.CardPremium;

        /// <summary>What the bank charges on its cards: the rivals' rate and the owner's points over it, never under the loans' floor.</summary>
        public static int CardBp(BankCity c, OwnBank b) => Math.Max(CreditCatalogue.RateFloor * 100, CardRivalBp(c) + b.CardRate);

        /// <summary>The clients of a kind the bank has: its savers of that kind (the people's, or the firms'), by what a saver holds, and the kind's share of them.</summary>
        public static int CardClients(OwnBank b, int kind)
        {
            int saver = OwnBankCatalogue.IsBusiness(kind) ? OwnBankCatalogue.Firms : OwnBankCatalogue.People;
            long clients = b.Deposits[saver] / CardTerms.SaverCents[saver];
            return (int)Math.Min(int.MaxValue / 4, clients * CardTerms.KindShare[kind] / 1000);
        }

        /// <summary>The cards of a kind its clients would hold at the bank's price: its take-up, less for a dearer card and a yearly fee.</summary>
        public static int CardWanted(BankCity c, OwnBank b, int kind)
        {
            double points = (CardBp(c, b) - CardRivalBp(c)) / 100.0;
            double fee = CardTerms.FeeSteps[Math.Max(0, Math.Min(CardTerms.FeeSteps.Length - 1, b.CardFee))] / 100.0;
            double price = Math.Exp(-CardTerms.Pull / 1000.0 * points) * Math.Max(0.1, 1.0 - CardTerms.FeePull * fee / 1000.0);
            return (int)Math.Min(int.MaxValue / 4, Math.Round(CardClients(b, kind) * CardTerms.TakeUp[kind] / 1000.0 * price));
        }

        // ------------------------------------------------------------------------------------------ approval by score

        /// <summary>The standard normal distribution function.</summary>
        public static double Phi(double z)
        {
            // Abramowitz and Stegun 7.1.26: an error under 1.5e-7, plenty for shares of a few thousand applicants, and the same on every machine
            double x = Math.Abs(z) / Math.Sqrt(2.0), t = 1.0 / (1.0 + 0.3275911 * x);
            double y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return z >= 0 ? 0.5 * (1.0 + y) : 0.5 * (1.0 - y);
        }

        private static double Pdf(double z) => Math.Exp(-0.5 * z * z) / Math.Sqrt(2.0 * Math.PI);

        /// <summary>
        /// The applicants of a kind by what the owner's lines do with them, thousandths: approved at once (their score is the line's or over it), queued (between the
        /// lines) and declined (under the lower). Scores are spread about the kind's typical one. <paramref name="autoMult"/> and <paramref name="queueMult"/> are what
        /// each group's defaults are against the kind's, thousandths: a lower score is a worse client.
        /// </summary>
        public static void CardShares(int kind, int auto, int decline, out int autoShare, out int queueShare, out int autoMult, out int queueMult)
        {
            int k = CardTerms.KindOf(kind);
            double mu = CardTerms.KindScore[k], sd = CardTerms.ScoreSd;
            int a = Math.Max(auto, decline), d = Math.Min(auto, decline);
            double za = (a - mu) / sd, zd = (d - mu) / sd;
            double pa = 1.0 - Phi(za), pq = Phi(za) - Phi(zd);
            autoShare = (int)Math.Round(pa * 1000.0);
            queueShare = (int)Math.Round(pq * 1000.0);
            double meanAuto = pa > 1e-9 ? mu + sd * Pdf(za) / pa : a;
            double meanQueue = pq > 1e-9 ? mu + sd * (Pdf(zd) - Pdf(za)) / pq : (a + d) / 2.0;
            autoMult = (int)Math.Round(1000.0 * Math.Exp(-(meanAuto - mu) / CardTerms.ScoreScale));
            queueMult = (int)Math.Round(1000.0 * Math.Exp(-(meanQueue - mu) / CardTerms.ScoreScale));
        }

        private static void TakeCards(OwnBank b, int kind, int count, int mult)
        {
            if (count <= 0)
            {
                return;
            }
            long have = b.CardHolders[kind];
            b.CardRisk[kind] = (int)((b.CardRisk[kind] * have + (long)mult * count) / (have + count));
            b.CardHolders[kind] += count;
        }

        /// <summary>
        /// The owner's answer to the applicants waiting between the lines in a kind: approved (they become holders, at the worse defaults their scores bring) or
        /// declined. Returns how many were decided; nothing if none wait or the bank cannot issue.
        /// </summary>
        public static int CardDecide(OwnBank b, int kind, bool approve)
        {
            int k = CardTerms.KindOf(kind);
            int n = b.CardQueue[k];
            if (n <= 0 || !b.Running)
            {
                return 0;
            }
            if (approve)
            {
                CardShares(k, b.CardAuto, b.CardDecline, out _, out _, out _, out int queueMult);
                TakeCards(b, k, n, queueMult);
            }
            b.CardQueue[k] = 0;
            return n;
        }

        // ------------------------------------------------------------------------------------------ the day

        /// <summary>The cards' day: the owner's card taken in, then each kind's applicants, spending, interest, defaults and balance.</summary>
        internal static void CardsDay(OwnBank b, BankCity c, int day, BankDay into, long hold)
        {
            ApplyOwnCard(b, into);
            if (!b.CardsOn && b.CardsHeld == 0 && b.CardBook == 0 && b.CardArrears == 0)
            {
                return;
            }
            int y = Math.Max(1, c.YearDays), m = Math.Max(1, c.MonthDays), cycle = Cycle(c);
            long level = Level(c.Level);
            BankCaps caps = Caps(b, c.Level, c.Scale);
            bool stopped = b.Run || b.Stage == Regulator.Breach;
            long fixedDay = b.CardsOn ? CardTerms.FixedDayCents * level / PriceRules.Start : 0;
            b.Reserves -= fixedDay;
            b.Capital -= fixedDay;
            into.CardCosts += fixedDay;
            b.CardFig[CardTerms.FigCosts] += fixedDay;
            for (int j = 0; j < CardTerms.Kinds; j++)
            {
                int h = b.CardHolders[j];
                long bal = b.CardBal[j], arr = b.CardArr[j];
                // Spending and what it earns and costs.
                long spend = h * CardTerms.SpendDay[j] * level / PriceRules.Start * CardTerms.SpendCycle[cycle] / 1000;
                long interchange = spend * CardTerms.InterchangeBp / 10000;
                long interest = bal * CardTerms.Revolve[j] / 1000 * CardBp(c, b) / (10000L * y);
                long fee = h * CardTerms.FeeCents(b.CardFee, c.Level) / y;
                long rewards = spend * CardTerms.Rewards[j] / 10000;
                long process = h * CardTerms.CardDayCents * level / PriceRules.Start;
                long fraud = spend * CardTerms.FraudBp / 10000;
                b.Reserves += interchange + interest + fee - rewards - process - fraud;
                b.Capital += interchange + interest + fee - rewards - process - fraud;
                into.CardInterchange += interchange;
                into.CardInterest += interest;
                into.CardFees += fee;
                into.CardCosts += rewards + process;
                into.CardFraud += fraud;
                b.CardLostAll += fraud;
                b.CardFig[CardTerms.FigInterchange] += interchange;
                b.CardFig[CardTerms.FigInterest] += interest;
                b.CardFig[CardTerms.FigFees] += fee;
                b.CardFig[CardTerms.FigCosts] += rewards + process;
                b.CardFig[CardTerms.FigFraud] += fraud;
                b.CardFig[CardTerms.FigSpend] += spend;
                // Defaults by the loans' own model, worse for a card with no security and for the scores approved; arrears settled over a month.
                int rate = (int)Math.Round(DefaultPerMille(c, b, j) * (double)CardTerms.DefaultMult[j] / 1000.0 * Math.Max(1, b.CardRisk[j]) / 1000.0);
                long defaulted = Math.Min(bal, (long)Math.Round(bal * (double)rate / (1000.0 * y)));
                bal -= defaulted;
                arr += defaulted;
                long settled = arr < 100 * m ? arr : arr / m;
                long cured = settled * CardTerms.CureShare[j] / 1000, written = settled - cured;
                arr -= settled;
                bal += cured;
                b.Capital -= written;
                into.CardLost += written;
                b.CardLostAll += written;
                b.CardFig[CardTerms.FigLost] += written;
                // Clients take up the bank's card as it is priced; the owner's lines decide what happens to the applicants. Clients who move their savings elsewhere take their card with them.
                int demand = CardWanted(c, b, j);
                if (b.CardsOn && b.CardIssue[j] && !stopped)
                {
                    int gap = demand - h - b.CardQueue[j];
                    if (gap > 0)
                    {
                        int applicants = (int)Math.Ceiling(gap * (double)CardTerms.Follow / (1000.0 * m));
                        CardShares(j, b.CardAuto, b.CardDecline, out int auto, out int queue, out int autoMult, out _);
                        int toTake = (int)Math.Round(applicants * auto / 1000.0), toQueue = (int)Math.Round(applicants * queue / 1000.0);
                        TakeCards(b, j, toTake, autoMult);
                        b.CardQueue[j] = Math.Min(CardTerms.QueueMost, b.CardQueue[j] + toQueue);
                    }
                }
                if (b.CardQueue[j] > 0)
                {
                    b.CardQueue[j] -= Math.Max(1, (int)((long)b.CardQueue[j] * CardTerms.QueueFade / 1000));
                }
                if (b.CardHolders[j] > demand)
                {
                    int lost = (int)Math.Ceiling((b.CardHolders[j] - demand) * (double)CardTerms.Follow / (1000.0 * m));
                    b.CardHolders[j] = Math.Max(demand, b.CardHolders[j] - lost);
                }
                h = b.CardHolders[j];
                // The balance carried moves towards what the cards' limits and the cycle make of it.
                long limits = h * CardTerms.LimitCents(j, b.CardLimit[j], c.Level);
                long target = limits * CardTerms.Util[j] / 1000 * CardTerms.UtilCycle[cycle] / 1000;
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
                b.CardBal[j] = bal;
                b.CardArr[j] = arr;
            }
        }

        // The owner's card: what it did since the last day, into the bank's balance sheet and the day's books.
        private static void ApplyOwnCard(OwnBank b, BankDay into)
        {
            if (b.OwnDrawn == 0 && b.OwnPaid == 0 && b.OwnInterest == 0 && b.OwnFees == 0 && b.OwnLost == 0)
            {
                return;
            }
            b.Reserves += b.OwnPaid - b.OwnDrawn;
            long owed = b.OwnOwed + b.OwnDrawn + b.OwnInterest + b.OwnFees - b.OwnPaid - b.OwnLost;
            if (owed < 0)
            {
                b.Reserves += owed;   // a balance cannot be under nothing: what the books overpaid is taken back, so they still balance
                owed = 0;
            }
            b.OwnOwed = owed;
            b.Capital += b.OwnInterest + b.OwnFees - b.OwnLost;
            into.CardInterest += b.OwnInterest;
            into.CardFees += b.OwnFees;
            into.CardLost += b.OwnLost;
            into.OwnerCardPaid = b.OwnPaid;
            b.CardFig[CardTerms.FigInterest] += b.OwnInterest;
            b.CardFig[CardTerms.FigFees] += b.OwnFees;
            b.CardFig[CardTerms.FigLost] += b.OwnLost;
            b.OwnLostAll += b.OwnLost;
            b.CardLostAll += b.OwnLost;
            b.OwnDrawn = b.OwnPaid = b.OwnInterest = b.OwnFees = b.OwnLost = 0;
        }

        /// <summary>The most the owner's own card may be given as a limit, whole money: a share of the capital, to the step, and not under the least.</summary>
        public static long OwnCardMost(OwnBank b, int level)
        {
            long most = Math.Max(0, CapitalNow(b)) * CardTerms.BlackMostShare / 1000;
            long step = OwnBankRules.At(CardTerms.BlackStepCents, level), least = OwnBankRules.At(CardTerms.BlackLeastCents, level);
            long stepped = most / Math.Max(1, step) * Math.Max(1, step);
            return stepped >= least ? stepped : 0;
        }

        /// <summary>A month's card figures closed with the month: kept for the page as last month's.</summary>
        internal static void CloseCardMonth(OwnBank b)
        {
            Array.Copy(b.CardFig, b.CardLast, b.CardFig.Length);
            Array.Clear(b.CardFig, 0, b.CardFig.Length);
        }
    }
}
