using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How a bank's assets are shared out when the regulator takes it: what was sold, who was paid and what the owner keeps. Every figure in cents.</summary>
    public struct Waterfall
    {
        public long Assets;        // what everything it held fetched
        public long Savers;        // paid to its savers first...
        public long SaversLoss;    // ...and what they lost if there was not enough
        public long Banks;         // then to the banks it had borrowed from
        public long BanksLoss;
        public long Fee;           // the regulator's charge on what was left
        public long Owner;         // and the rest, the owner's
    }

    /// <summary>
    /// The bank risk (Economy/OwnBank/Risk, BankRisk): credit risk that follows the economy, a kind of borrower in trouble now and then, a concentration
    /// charge, savers who leave faster than they come and the city's loss of heart, the tools to meet a run (a sale of loans, money from other banks, the
    /// owner's capital), the regulator's ladder and the waterfall when a bank is taken. Game-free. Everything is a function of the bank, the city and the
    /// bank's seed, so it is the same on every machine and every run; with the level Off every function answers as the bank did before it existed.
    /// A few sums a call and nothing allocated.
    /// </summary>
    public static partial class OwnBankRules
    {
        public static int RiskOf(OwnBank b) => b == null ? BankRisk.Off : BankRisk.Level(b.Risk);

        /// <summary>The ratio under which the bank lends and buys no further: its warning line.</summary>
        public static int LendRatioOf(OwnBank b) => BankRisk.LendRatio[RiskOf(b)];

        /// <summary>Reserves kept against the deposits, thousandths.</summary>
        public static int ReserveShareOf(OwnBank b) => BankRisk.ReserveShare[RiskOf(b)];

        /// <summary>True while the bank is watched and the risk is on: no dividend, no pay and no lending past a part of it.</summary>
        public static bool WatchHeld(OwnBank b) => b.Running && RiskOf(b) > BankRisk.Off && b.Stage == Regulator.Watch;

        /// <summary>True while a bank that is clear of the regulator has a ratio under its own warning line (the risk on).</summary>
        public static bool Warned(OwnBank b) => b.Running && RiskOf(b) > BankRisk.Off && b.Stage == Regulator.Clear && Ratio(b) < LendRatioOf(b);

        // ------------------------------------------------------------------------------------------ credit risk

        /// <summary>The kind of borrower in trouble in a month, drawn from the bank's seed; -1 for none. The same for the same bank, month and level.</summary>
        public static int ShockSegment(int seed, int month, int risk)
        {
            int lv = BankRisk.Level(risk);
            if (BankRisk.ShockChance[lv] <= 0)
            {
                return -1;
            }
            unchecked
            {
                uint h = (uint)seed * 2246822519u ^ (uint)(month * 69069 + BankRisk.ShockSalt);
                h ^= h >> 15;
                h *= 2654435761u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h % 1000u < (uint)BankRisk.ShockChance[lv] ? (int)((h >> 12) % (uint)OwnBankCatalogue.Segments) : -1;
            }
        }

        /// <summary>The kind of borrower that will be in trouble next month, once the month before it is two thirds gone: the warning; -1 for none yet.</summary>
        public static int ShockAhead(OwnBank b, int day, int monthDays)
        {
            int m = Math.Max(1, monthDays);
            int into = day - b.MonthFrom;
            return RiskOf(b) > BankRisk.Off && into * 1000 >= m * (1000 - BankRisk.ShockWarnShare) ? ShockSegment(b.Seed, b.Month + 1, b.Risk) : -1;
        }

        /// <summary>The share of the book (loans, arrears and cards) one kind of borrower is, thousandths; 0 for an empty book.</summary>
        public static int Concentration(OwnBank b, int segment)
        {
            long total = BookTotal(b);
            return total > 0 ? (int)(KindBook(b, segment) * 1000 / total) : 0;
        }

        /// <summary>How far a kind is over the share one kind may be, thousandths of the book; 0 while the risk is off or the book is smaller than the capital.</summary>
        public static int ConcExcess(OwnBank b, int segment)
        {
            int lv = RiskOf(b);
            long total = BookTotal(b);
            if (lv == BankRisk.Off || total <= 0 || total < Math.Max(0, CapitalNow(b)) * BankRisk.ConcFloorShare / 1000)
            {
                return 0;
            }
            return Math.Max(0, (int)(KindBook(b, segment) * 1000 / total) - BankRisk.ConcLimit[lv]);
        }

        /// <summary>The risk weight the regulator adds for the part of the book that is more than one kind of borrower may be.</summary>
        public static long ConcentrationCharge(OwnBank b)
        {
            int lv = RiskOf(b);
            if (lv == BankRisk.Off)
            {
                return 0;
            }
            long total = BookTotal(b), sum = 0;
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                sum += total * ConcExcess(b, j) / 1000;
            }
            return sum * BankRisk.ConcWeight[lv] / 1000;
        }

        private static long KindBook(OwnBank b, int j) => b.Loans[j] + b.Arrears[j] + b.CardBal[j] + b.CardArr[j];

        private static long BookTotal(OwnBank b)
        {
            long sum = 0;
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                sum += KindBook(b, j);
            }
            return sum;
        }

        /// <summary>
        /// What the economy, a kind in trouble and a lopsided book do to a kind's defaults, thousandths (1000: nothing); 1000 with the risk off. Applied by
        /// <see cref="DefaultPerMille"/>, so loans, cards and the page all read one figure.
        /// </summary>
        public static int StressFactor(BankCity c, OwnBank b, int segment)
        {
            int lv = BankRisk.Level(c.Risk);
            if (lv == BankRisk.Off)
            {
                return 1000;
            }
            int cycle = Cycle(c);
            double f = (OwnBankCatalogue.IsBusiness(segment) ? BankRisk.FirmCycle[lv][cycle] : BankRisk.PeopleStress[lv][cycle]) / 1000.0;
            if (ShockSegment(b.Seed, b.Month, c.Risk) == segment)
            {
                f *= BankRisk.ShockDefault[lv] / 1000.0;
            }
            f *= 1.0 + ConcExcess(b, segment) * (double)BankRisk.ConcDefault[lv] / 1000000.0;
            return (int)Math.Round(f * 1000.0);
        }

        /// <summary>What a written-off loan comes back, thousandths of the usual, in the cycle: security fetches less in a slump.</summary>
        public static int RecoveryFactor(BankCity c) => BankRisk.RecoveryCycle[BankRisk.Level(c.Risk)][Cycle(c)];

        // ------------------------------------------------------------------------------------------ liquidity

        /// <summary>Cash and what the fund and the own book would fetch in a hurry (a haircut off their value).</summary>
        public static long Liquid(OwnBank b)
        {
            return b.Reserves + Math.Max(0, b.FundValue + b.BookValue) * (1000 - BankRisk.HaircutLiquid) / 1000;
        }

        /// <summary>A day's withdrawals in a run, at its usual shares (the risk Hard makes a run sharper).</summary>
        public static long RunNeedDay(OwnBank b)
        {
            long sum = 0;
            for (int k = 0; k < OwnBankCatalogue.Savers; k++)
            {
                sum += b.Deposits[k] * RunShareOf(b, k) / 1000;
            }
            return sum;
        }

        /// <summary>A run's day's share of a kind's balance, thousandths.</summary>
        public static int RunShareOf(OwnBank b, int kind) => OwnBankCatalogue.RunShare[kind] * BankRisk.RunMult[RiskOf(b)] / 1000;

        /// <summary>How long the cash would last a run, in tenths of a day; 990 at most, and for a bank with nothing to lose.</summary>
        public static int CoverDays(OwnBank b)
        {
            long need = RunNeedDay(b);
            return need <= 0 ? 990 : (int)Math.Max(0L, Math.Min(990L, Liquid(b) * 10 / need));
        }

        /// <summary>What the city's confidence keeps of itself for the bank's cash and losses, thousandths (the risk on): cash for a few days and a month's loss both cost it.</summary>
        public static int RiskCushion(OwnBank b)
        {
            int cushion = 1000;
            int cover = CoverDays(b);
            if (cover < BankRisk.CoverRunDays)
            {
                cushion = BankRisk.CoverRunTrust;
            }
            else if (cover < BankRisk.CoverWarnDays)
            {
                cushion = BankRisk.CoverWarnTrust;
            }
            if (b.HistoryCount > 0 && b.History[b.HistoryCount - 1] < -Math.Max(0, CapitalNow(b)) * BankRisk.LossShare / 1000)
            {
                cushion = cushion * BankRisk.LossTrust / 1000;
            }
            return cushion;
        }

        /// <summary>A day's money a bank loses of its own accord when the city's confidence is low (the risk on), by kind; businesses leave first.</summary>
        public static long Flight(OwnBank b, int kind, int confidence)
        {
            int lv = RiskOf(b);
            if (lv == BankRisk.Off || confidence >= BankRisk.FlightConfidence)
            {
                return 0;
            }
            long f = b.Deposits[kind] * BankRisk.FlightShare[lv] * (BankRisk.FlightConfidence - Math.Max(0, confidence)) / (BankRisk.FlightConfidence * 1000L);
            return kind == OwnBankCatalogue.Firms ? f * BankRisk.FlightFirms / 1000 : f;
        }

        /// <summary>The part of a run's withdrawals that paying savers well holds back, thousandths: so many points over the rivals' rate.</summary>
        public static int RunHold(BankCity c, OwnBank b, int kind)
        {
            if (RiskOf(b) == BankRisk.Off)
            {
                return 0;
            }
            int over = SaverBp(c, b, kind) - RivalSaverBp(c, b, kind);
            return over <= 0 ? 0 : (int)Math.Min(BankRisk.RunRateMost, (long)over * BankRisk.RunRatePer / 100);
        }

        /// <summary>How many of the new lending a bank may do now, thousandths: all of it, less while the regulator is watching it (the risk on).</summary>
        public static int LendScale(OwnBank b) => WatchHeld(b) ? BankRisk.WatchLend[RiskOf(b)] : 1000;

        // ------------------------------------------------------------------------------------------ meeting a run

        /// <summary>The discount other banks take loans at now, thousandths of their face: by the cycle, more in a run. Nothing while the risk is off.</summary>
        public static int SaleDiscountOf(BankCity c, OwnBank b)
        {
            int lv = RiskOf(b);
            return lv == BankRisk.Off ? 0 : BankRisk.SaleDiscount[lv][Cycle(c)] + (b.Run ? BankRisk.SaleRun : 0);
        }

        /// <summary>The most of a kind's performing loans one sale may take, at their face.</summary>
        public static long SaleMostFace(OwnBank b, int segment)
        {
            return RiskOf(b) == BankRisk.Off || !b.Running || segment < 0 || segment >= OwnBankCatalogue.Segments ? 0 : b.Loans[segment] * BankRisk.SaleMost / 1000;
        }

        /// <summary>
        /// A sale of performing loans of a kind to other banks, for their face less the discount: the cash into the reserves, the discount out of the capital
        /// at once (the next day's books carry it). False with nothing done if the sum is over what one sale may take.
        /// </summary>
        public static bool SellLoans(OwnBank b, BankCity c, int segment, long face, out long cash, out long loss)
        {
            cash = loss = 0;
            if (face <= 0 || face > SaleMostFace(b, segment))
            {
                return false;
            }
            loss = face * SaleDiscountOf(c, b) / 1000;
            cash = face - loss;
            b.Loans[segment] -= face;
            b.Reserves += cash;
            b.Capital -= loss;
            b.SoldFace += face;
            b.SoldLoss += loss;
            b.SaleLossPend += loss;
            return true;
        }

        /// <summary>What the bank may still borrow from other banks: a share of its capital less what it owes them.</summary>
        public static long BorrowRoom(OwnBank b)
        {
            int lv = RiskOf(b);
            return lv == BankRisk.Off || !b.Running || b.Test ? 0 : Math.Max(0, Math.Max(0, CapitalNow(b)) * BankRisk.InterbankMost[lv] / 1000 - b.Interbank);
        }

        /// <summary>What other banks charge now, hundredths of a per cent a year: the bank rate, their margin, more in a run and in a slump.</summary>
        public static int BorrowBp(BankCity c, OwnBank b)
        {
            int lv = RiskOf(b);
            return Math.Max(0, c.CityRateBp + BankRisk.InterbankSpread[lv] + (b.Run ? BankRisk.InterbankRun : 0) + BankRisk.InterbankCycle[Cycle(c)]);
        }

        /// <summary>The fee for arranging a loan from other banks.</summary>
        public static long BorrowFee(long cents) => cents <= 0 ? 0 : (cents * BankRisk.InterbankFeeBp + 9999) / 10000;

        /// <summary>
        /// Money borrowed from other banks into the reserves, for a month, at today's price; the fee out of the capital at once. An earlier loan is taken
        /// in at the mean of the two rates. False with nothing done if it is over <see cref="BorrowRoom"/> or the risk is off.
        /// </summary>
        public static bool Borrow(OwnBank b, BankCity c, long cents, int day, out long fee)
        {
            fee = 0;
            if (cents <= 0 || cents > BorrowRoom(b))
            {
                return false;
            }
            fee = BorrowFee(cents);
            int bp = BorrowBp(c, b);
            b.InterbankBp = b.Interbank > 0 ? (int)((b.InterbankBp * b.Interbank + (long)bp * cents) / (b.Interbank + cents)) : bp;
            b.Interbank += cents;
            b.InterbankDue = Math.Max(b.InterbankDue, day + BankRisk.InterbankMonths * Math.Max(1, c.MonthDays));
            b.Reserves += cents - fee;
            b.Capital -= fee;
            b.FundingPend += fee;
            return true;
        }

        /// <summary>Money repaid to other banks early, out of the cash over the reserve; returns what was repaid.</summary>
        public static long Repay(OwnBank b, long cents)
        {
            long pay = Math.Max(0, Math.Min(cents, Math.Min(b.Interbank, FreeCash(b))));
            b.Interbank -= pay;
            b.Reserves -= pay;
            if (b.Interbank <= 0)
            {
                b.Interbank = 0;
                b.InterbankDue = b.InterbankBp = 0;
            }
            return pay;
        }

        /// <summary>
        /// A day of the money borrowed: its interest out of the cash and the capital, and on its due day repaid out of the cash over the reserve, else rolled
        /// over for another month at a penalty rate.
        /// </summary>
        public static void InterbankDay(OwnBank b, int day, int yearDays, int monthDays, BankDay into)
        {
            into.Funding += b.FundingPend;
            into.SaleLoss += b.SaleLossPend;
            b.FundingPend = b.SaleLossPend = 0;
            if (b.Interbank <= 0)
            {
                return;
            }
            long interest = b.Interbank * b.InterbankBp / (10000L * Math.Max(1, yearDays));
            b.Reserves -= interest;
            b.Capital -= interest;
            into.Funding += interest;
            if (day >= b.InterbankDue)
            {
                if (FreeCash(b) >= b.Interbank)
                {
                    b.Reserves -= b.Interbank;
                    b.Interbank = 0;
                    b.InterbankDue = b.InterbankBp = 0;
                }
                else
                {
                    b.InterbankBp += BankRisk.InterbankRoll;
                    b.InterbankDue = day + BankRisk.InterbankMonths * Math.Max(1, monthDays);
                    into.Rolled = true;
                }
            }
        }

        // ------------------------------------------------------------------------------------------ taken by the regulator

        /// <summary>What a taken bank's performing loans fetch in the cycle, thousandths of the usual (1000 with the risk off).</summary>
        public static int WindFactor(OwnBank b, BankCity c) => BankRisk.WindCycle[RiskOf(b)][Cycle(c)];

        /// <summary>
        /// How the bank's assets would be shared out if the regulator took it now (or are, when it does): its assets sold for what they fetch, its savers paid
        /// first, the banks it borrowed from next, the regulator's charge on what is left and the rest the owner's (nothing for a test's bank). The parts add up:
        /// the savers' and banks' payments, their losses' counterparts, the fee and the owner's share are the assets and what was owed.
        /// </summary>
        public static Waterfall Resolve(OwnBank b, int cycle = 1000)
        {
            long assets = b.Reserves + Math.Max(0, b.CounterFees) + b.Lent * OwnBankCatalogue.WindPerforming / 1000 * cycle / 1000 + b.InArrears * OwnBankCatalogue.WindArrears / 1000
                          + (b.FundValue + b.BookValue) * OwnBankCatalogue.WindFund / 1000 + b.CardBook * CardTerms.WindCards / 1000 * cycle / 1000
                          + b.CardArrears * CardTerms.WindCardArrears / 1000;
            var w = new Waterfall { Assets = assets };
            long rest = Math.Max(0, assets);
            w.Savers = Math.Min(rest, b.Savings);
            w.SaversLoss = b.Savings - w.Savers;
            rest -= w.Savers;
            w.Banks = Math.Min(rest, b.Interbank);
            w.BanksLoss = b.Interbank - w.Banks;
            rest -= w.Banks;
            w.Owner = b.Test ? 0 : rest * (1000 - OwnBankCatalogue.WindFee) / 1000;
            w.Fee = rest - w.Owner;
            return w;
        }

        // ------------------------------------------------------------------------------------------ the warning signs

        /// <summary>The warning signs the page names (BankSign flags): none while the risk is off.</summary>
        public static int Signs(OwnBank b, BankCity c, int day, int confidence)
        {
            if (RiskOf(b) == BankRisk.Off || !b.Running)
            {
                return 0;
            }
            int signs = 0;
            if (confidence < BankRisk.SignConfidence)
            {
                signs |= BankSign.Confidence;
            }
            if (CoverDays(b) < BankRisk.CoverWarnDays)
            {
                signs |= BankSign.Cover;
            }
            int ratio = Ratio(b);
            if (ratio < RatioMost && ratio < LendRatioOf(b) + BankRisk.SignRatioRoom)
            {
                signs |= BankSign.Ratio;
            }
            if (ShockAhead(b, day, c.MonthDays) >= 0)
            {
                signs |= BankSign.Shock;
            }
            if (confidence < BankRisk.FlightConfidence)
            {
                signs |= BankSign.Flight;
            }
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                if (ConcExcess(b, j) > 0)
                {
                    signs |= BankSign.Concentrated;
                }
            }
            return signs;
        }

        /// <summary>The capital that would put the ratio at the warning line (the risk on), or at the law's watch line while it is off: what the owner would put in.</summary>
        public static long CapitalToClear(OwnBank b)
        {
            long line = RiskOf(b) > BankRisk.Off ? LendRatioOf(b) : OwnBankCatalogue.WatchRatio;
            return Math.Max(0, RiskWeighted(b) * line / 1000 - CapitalNow(b));
        }

        /// <summary>The bank's risk level written for a new bank, and for one a test founded: none borrowed, no sale made, no sign told.</summary>
        internal static void FoundNew(OwnBank b)
        {
            b.Risk = BankRisk.Off;
            b.Interbank = 0;
            b.InterbankDue = b.InterbankBp = 0;
            b.SoldFace = b.SoldLoss = 0;
            b.SaleLossPend = b.FundingPend = 0;
            b.SignsTold = b.ShockTold = int.MinValue;
            b.CardsOn = false;
            b.CardAuto = CardTerms.AutoDefault;
            b.CardDecline = CardTerms.DeclineDefault;
            b.CardRate = 0;
            b.CardFee = CardTerms.FeeDefault;
            for (int j = 0; j < CardTerms.Kinds; j++)
            {
                b.CardIssue[j] = j != OwnBankCatalogue.Poor;
                b.CardLimit[j] = CardTerms.LimitDefault;
                b.CardHolders[j] = 0;
                b.CardBal[j] = b.CardArr[j] = 0;
                b.CardQueue[j] = 0;
                b.CardRisk[j] = 1000;
            }
            Array.Clear(b.CardFig, 0, b.CardFig.Length);
            Array.Clear(b.CardLast, 0, b.CardLast.Length);
            b.CardLostAll = 0;
            b.OwnCard = false;
            b.OwnOwed = b.OwnLostAll = b.OwnLimit = 0;
            b.OwnRateBp = 0;
            b.OwnDrawn = b.OwnPaid = b.OwnInterest = b.OwnFees = b.OwnLost = 0;
            b.DeskOn = false;
            b.DeskMandate = BankDesk.None;
            b.DeskShare = BankDesk.ShareDefault;
            b.DeskLimit = BankDesk.LimitDefault;
            b.DeskPeak = 0;
            b.DeskCutDay = b.DeskCuts = 0;
            b.DeskBrokerage = b.DeskDerivs = b.DeskCommissionAll = b.DeskLastBrokerage = b.DeskLastDerivs = 0;
            b.Book.Clear();
        }
    }
}
