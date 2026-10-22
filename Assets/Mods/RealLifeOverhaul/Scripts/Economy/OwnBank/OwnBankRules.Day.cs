using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The bank's day and its crews at scale. A day is a few dozen sums; nothing is allocated but in the fund's weekly rebalance.</summary>
    public static partial class OwnBankRules
    {
        private static readonly long[] Want = new long[OwnBankCatalogue.Segments];   // the day's new lending by segment, worked in here
        private static readonly long[] Moves = new long[OwnBankCatalogue.Savers];    // the day's savers' money in or out by kind, worked in here

        /// <summary>
        /// One day of a running bank: confidence, savers (no new money past its level's deposits), loans, lending (no further
        /// than its level's book), the fund, the staff, the profit into the capital, a fire sale if the savers cannot be paid,
        /// the regulator, then the owner's pay out of the profit. A bank closed is wound up at once (into.WoundUp).
        /// </summary>
        public static void Day(OwnBank b, BankCity c, int day, IList<FundQuote> quotes, BankDay into)
        {
            into.Clear();
            if (!b.Running)
            {
                return;
            }
            int y = Math.Max(1, c.YearDays), m = Math.Max(1, c.MonthDays);
            long level = Level(c.Level);
            BankCaps caps = Caps(b, c.Level, c.Scale);
            int tier = TierOf(b);

            // Confidence from the books as yesterday left them.
            int confidence = Confidence(c, b, Ratio(b));
            if (!b.Run && confidence < OwnBankCatalogue.RunStart)
            {
                b.Run = true;
                b.RunFrom = day;
                b.Scare = Math.Min(OwnBankCatalogue.ScareMost, b.Scare + OwnBankCatalogue.ScareRun);
                into.RunStarted = true;
            }
            else if (b.Run && confidence >= OwnBankCatalogue.RunEnd && b.Stage != Regulator.Breach)
            {
                b.Run = false;
                into.RunEnded = true;
            }
            int trust = Trust(c, b, day, confidence);

            // Short of its crews at scale, the bank takes savings and lends more slowly.
            int working = EndScaleRules.Working(Hire(b, c.Scale));

            // Savers: their interest, then each kind's money in or out; what comes in is shared out within the room its
            // level's deposits leave (a bank at or over them takes none, and keeps what it holds).
            long coming = 0;
            for (int k = 0; k < OwnBankCatalogue.Savers; k++)
            {
                long interest = b.Deposits[k] * SaverBp(c, b, k) / (10000L * y);
                b.Deposits[k] += interest;
                into.Savers += interest;
                long move;
                if (b.Run)
                {
                    move = -(b.Deposits[k] * OwnBankCatalogue.RunShare[k] / 1000);
                }
                else
                {
                    long gap = SaversWanted(c, b, k, trust) - b.Deposits[k];
                    move = gap * OwnBankCatalogue.SaverFollow[k] / (1000L * m) * working / 1000;
                }
                Moves[k] = Math.Max(-b.Deposits[k], move);
                coming += Math.Max(0, Moves[k]);
            }
            long room = DepositRoom(b, caps);
            for (int k = 0; k < OwnBankCatalogue.Savers; k++)
            {
                long move = Moves[k];
                if (move > 0 && coming > room)
                {
                    move = (long)((decimal)move * room / coming);   // (decimal: two balances of the city's size multiplied)
                }
                b.Deposits[k] += move;
                b.Reserves += move;
                if (move < 0)
                {
                    into.Withdrawn -= move;
                }
                else
                {
                    into.Deposited += move;
                }
            }

            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                long interest = b.Loans[j] * LoanBp(c, b, j) / (10000L * y) * EndScaleRules.Mix(1000, OwnBankCatalogue.IncomeAtScale, c.Scale) / 1000;
                b.Reserves += interest;
                into.Interest += interest;
                long defaulted = (long)Math.Round(b.Loans[j] * (double)DefaultPerMille(c, b, j) / (1000.0 * y));
                defaulted = Math.Min(b.Loans[j], defaulted);
                b.Loans[j] -= defaulted;
                b.Arrears[j] += defaulted;
                into.Defaulted += defaulted;
                long settled = b.Arrears[j] < 100 * m ? b.Arrears[j] : b.Arrears[j] / m;   // a few dollars left are settled at once
                long cured = settled * OwnBankCatalogue.CureShare[j] / 1000, written = settled - cured;
                b.Arrears[j] -= settled;
                b.Loans[j] += cured;
                into.Cured += cured;
                into.WrittenOff += written;
                into.Lost += written;
                if (written > 0)
                {
                    int share = OwnBankCatalogue.RecoverShare[j] + (c.Courts ? OwnBankCatalogue.CourtExtra : 0);
                    b.Recovering[j] += written * share / 1000;
                    b.Recover[j] = (OwnBankCatalogue.RecoverMonths + (c.Courts ? OwnBankCatalogue.CourtMonths : 0)) * m;
                    long court = c.Courts ? written * OwnBankCatalogue.CourtCost / 1000 : 0;
                    b.Reserves -= court;
                    into.Court += court;
                }
                if (b.Recovering[j] > 0)
                {
                    long back = b.Recovering[j] < 100 * m ? b.Recovering[j] : b.Recovering[j] / Math.Max(1, b.Recover[j]);
                    b.Recovering[j] -= back;
                    b.Recover[j] = Math.Max(1, b.Recover[j] - 1);   // the days left: the last write-off is all back by the end of its months
                    b.Reserves += back;
                    into.Recovered += back;
                }
                long repaid = b.Loans[j] / ((long)OwnBankCatalogue.TermYears[j] * y);
                b.Loans[j] -= repaid;
                b.Reserves += repaid;
                into.Repaid += repaid;
            }
            Lend(b, c, m, into, working, LoanRoom(b, caps));

            if (b.Reserves > 0)
            {
                long reserve = b.Reserves * Math.Max(0, c.CityRateBp - OwnBankCatalogue.ReserveSpread) / (10000L * y);
                b.Reserves += reserve;
                into.Reserve += reserve;
            }

            // What the branches' counters charged since the last day: cash in, and income (OwnBankRules.Visit).
            long counter = Math.Max(0, b.CounterFees);
            b.CounterFees = 0;
            b.Reserves += counter;
            into.Counter = counter;

            OwnBankFund.Day(b, c, day, quotes, into);

            // Staff: the hall of its level and a share of what it holds, giving way at scale to its crews' wages, its level's
            // systems and the deposit insurance. What the scale adds never takes the capital below nothing in one day.
            int s = c.Scale;
            long before = BankTiers.HallDayCents[tier] * level / PriceRules.Start
                          + (b.Savings * OwnBankCatalogue.StaffOnSavings + (b.Lent + b.InArrears) * OwnBankCatalogue.StaffOnLoans) / (1000L * y);
            long staff = s <= 0 ? before : before * (EndScaleRules.Full - s) / EndScaleRules.Full + Crews(b) * OwnBankCatalogue.CostYearDays / y * level / PriceRules.Start;
            long systems = s <= 0 ? 0 : EndScaleRules.Mix(0L, BankTiers.SystemsDayCents[tier] * OwnBankCatalogue.CostYearDays / y * level / PriceRules.Start
                                                           + b.Savings * OwnBankCatalogue.InsuranceBp / (10000L * y), s);
            long spare = b.Capital + into.Interest + into.Reserve + into.Recovered + into.Counter - into.Savers - into.Lost - into.Court - Math.Min(before, staff);
            staff = Math.Min(before, staff) + EndScaleRules.Take(ref spare, Math.Max(0, staff - before));
            systems = EndScaleRules.Take(ref spare, systems);
            b.Reserves -= staff + systems;
            into.Staff += staff;
            into.Systems += systems;

            // The fund's gains, losses and fees are in the capital already (OwnBankFund).
            b.Capital += into.Interest + into.Reserve + into.Recovered + into.Counter - into.Savers - into.Lost - into.Court - into.Staff - into.Systems;

            if (b.Reserves < 0)
            {
                OwnBankFund.FireSale(b, -b.Reserves, into);
            }
            if (b.Reserves < 0)
            {
                Close(b, day, ClosedWhy.Run, into);
                return;
            }

            Regulate(b, c, day, m, into);
            if (!b.Running)
            {
                return;
            }

            // The owner's pay out of the day's profit; the rest stays in the capital (OwnerPay).
            long pay = OwnerPay(b, into.Profit, c.Level);
            if (pay > 0)
            {
                b.Capital -= pay;
                b.Reserves -= pay;
                into.Pay = pay;
                b.PaidMonth += pay;
                b.PaidAll += pay;
            }
            if (b.Scare > 0)
            {
                b.Scare = Math.Max(0, b.Scare - Math.Max(1, b.Scare * OwnBankCatalogue.ScareFade / (1000 * m)));
            }
            b.MonthIncome += into.Income;
            b.MonthCosts += into.Costs;
            b.MonthLost += into.WrittenOff;
            b.LostAll += into.WrittenOff;
            b.RecoveredAll += into.Recovered;
            b.LastDay = day;
            if (day + 1 - b.MonthFrom >= m)
            {
                CloseMonth(b, day + 1);
            }
        }

        // Each segment goes LendFollow of a month's way to the loans its borrowers want, as far as the reserves over what
        // the deposits need, the capital over the lending line and the room its level's book leaves allow; nothing in a run
        // or a breach, nor while the book is at its level's most.
        private static void Lend(OwnBank b, BankCity c, int m, BankDay into, int working, long book)
        {
            if (b.Run || b.Stage == Regulator.Breach || book <= 0)
            {
                return;
            }
            long need = 0, weighted = 0;
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                long gap = LoansWanted(c, b, j) - b.Loans[j];
                Want[j] = gap > 0 ? gap * OwnBankCatalogue.LendFollow / (1000L * m) * working / 1000 : 0;
                need += Want[j];
                weighted += Want[j] * OwnBankCatalogue.LoanWeight[j] / 1000;
            }
            if (need <= 0)
            {
                return;
            }
            long cash = b.Reserves - ReserveNeed(b);
            long room = CapitalNow(b) * 1000 / OwnBankCatalogue.LendRatio - RiskWeighted(b);
            if (cash <= 0 || room <= 0)
            {
                return;
            }
            double scale = Math.Min(1.0, Math.Min(cash / (double)need, weighted > 0 ? room / (double)weighted : 1.0));
            if (book != BankTiers.NoLimit)
            {
                scale = Math.Min(scale, book / (double)need);
            }
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                long lent = (long)(Want[j] * scale);
                b.Loans[j] += lent;
                b.Reserves -= lent;
                into.Lent += lent;
            }
        }

        // Closed with no capital or a ratio under half the law's; under the law's, a breach with a deadline, fined for each
        // deadline missed and closed at the StrikesClose-th, and its record at its level begun again; above it, cleared or
        // watched.
        private static void Regulate(OwnBank b, BankCity c, int day, int m, BankDay into)
        {
            long capital = CapitalNow(b);
            int ratio = Ratio(b);
            if (capital <= 0)
            {
                Close(b, day, ClosedWhy.Insolvent, into);
                return;
            }
            if (ratio < OwnBankCatalogue.MinRatio / 2)
            {
                Close(b, day, ClosedWhy.Collapse, into);
                return;
            }
            if (ratio < OwnBankCatalogue.MinRatio)
            {
                b.TierFrom = day;   // a move up asks for its months without a breach
                if (b.Stage != Regulator.Breach)
                {
                    b.Stage = Regulator.Breach;
                    b.Deadline = day + OwnBankCatalogue.CureMonths * m;
                    b.Strikes = 0;
                    b.Scare = Math.Min(OwnBankCatalogue.ScareMost, b.Scare + OwnBankCatalogue.ScareBreach);
                    into.Breached = true;
                }
                else if (day >= b.Deadline)
                {
                    b.Strikes++;
                    if (b.Strikes >= OwnBankCatalogue.StrikesClose)
                    {
                        Close(b, day, ClosedWhy.Strikes, into);
                        return;
                    }
                    long fine = Fine(b, c.Level);
                    b.Capital -= fine;
                    b.Reserves -= fine;
                    b.FinedCents += fine;
                    into.Fine += fine;
                    into.Fined = true;
                    b.Deadline = day + m;
                    b.Scare = Math.Min(OwnBankCatalogue.ScareMost, b.Scare + OwnBankCatalogue.ScareFine);
                }
                return;
            }
            Regulator was = b.Stage;
            b.Stage = ratio < OwnBankCatalogue.WatchRatio ? Regulator.Watch : Regulator.Clear;
            b.Strikes = 0;
            into.Cured2 = was == Regulator.Breach;
            into.Watched = was == Regulator.Clear && b.Stage == Regulator.Watch;
        }

        /// <summary>The people of a crew the bank needs at the full scale, by what it holds today.</summary>
        public static int CrewAtScale(OwnBank b, int kind)
        {
            if (!b.Running)
            {
                return 0;
            }
            switch (kind)
            {
                case OwnBankCatalogue.Tellers: return EndScaleRules.Per(b.Deposits[OwnBankCatalogue.People], OwnBankCatalogue.TellerCents);
                case OwnBankCatalogue.Managers: return EndScaleRules.Per(b.Deposits[OwnBankCatalogue.Firms], OwnBankCatalogue.ManagerCents);
                case OwnBankCatalogue.Officers: return EndScaleRules.Per(b.Lent + b.InArrears, OwnBankCatalogue.OfficerCents);
                default:
                    return BankTiers.ComplianceLeast[TierOf(b)] + (int)Math.Min(int.MaxValue / 4, Math.Max(0L, b.Savings + b.Capital) / OwnBankCatalogue.ComplianceCents);
            }
        }

        public static int CrewNeed(OwnBank b, int kind, int share) => EndScaleRules.Need(CrewAtScale(b, kind), share);

        /// <summary>A day's hiring towards each crew's need. Returns how far the shortest crew covers its need, thousandths.</summary>
        public static int Hire(OwnBank b, int share)
        {
            int cover = EndScaleRules.Full;
            for (int k = 0; k < OwnBankCatalogue.CrewKinds; k++)
            {
                int need = CrewNeed(b, k, share);
                b.Crew[k] = EndScaleRules.Hire(b.Crew[k], need, OwnBankCatalogue.CrewPace);
                cover = Math.Min(cover, EndScaleRules.Cover(b.Crew[k], need));
            }
            return cover;
        }

        /// <summary>The crews' wages a day, cents at the start's prices.</summary>
        public static long Crews(OwnBank b)
        {
            long sum = 0;
            for (int k = 0; k < OwnBankCatalogue.CrewKinds; k++)
            {
                sum += (long)Math.Max(0, b.Crew[k]) * OwnBankCatalogue.CrewWage[k];
            }
            return sum;
        }

        /// <summary>The fine for a deadline missed: a share of the capital the bank is short of, never under the least.</summary>
        public static long Fine(OwnBank b, int level)
        {
            long short_ = RiskWeighted(b) * OwnBankCatalogue.MinRatio / 1000 - CapitalNow(b);
            return Math.Max(At(OwnBankCatalogue.FineLeastCents, level), Math.Max(0, short_) * OwnBankCatalogue.FineShare / 1000);
        }

        private static void Close(OwnBank b, int day, ClosedWhy why, BankDay into)
        {
            into.Closed = true;
            into.Why = why;
            into.WoundUp = WindUp(b, day, why);
        }

        /// <summary>A month's books closed on the first day of the next: its profit recorded, its figures kept for the page, a new month's luck.</summary>
        public static void CloseMonth(OwnBank b, int nextDay)
        {
            long profit = b.MonthIncome - b.MonthCosts;
            if (b.HistoryCount < b.History.Length)
            {
                b.History[b.HistoryCount++] = profit;
            }
            else
            {
                Array.Copy(b.History, 1, b.History, 0, b.History.Length - 1);
                b.History[b.History.Length - 1] = profit;
            }
            b.LastIncome = b.MonthIncome;
            b.LastCosts = b.MonthCosts;
            b.LastLost = b.MonthLost;
            b.PaidLast = b.PaidMonth;
            b.MonthIncome = b.MonthCosts = b.MonthLost = b.PaidMonth = 0;
            b.MonthFrom = nextDay;
            b.Month++;
        }

        /// <summary>The bank's clock set back by days a test ran at once, so its next day is the game's and its deadline and month have moved on.</summary>
        public static void Rewind(OwnBank b, int days)
        {
            if (days <= 0)
            {
                return;
            }
            b.FoundedDay -= days;
            b.TierFrom -= days;
            b.MonthFrom -= days;
            b.LastDay -= days;
            b.Deadline = b.Deadline > 0 ? b.Deadline - days : 0;
            b.RunFrom = b.RunFrom > 0 ? b.RunFrom - days : 0;
            b.WatchTold = b.WatchTold == int.MinValue ? int.MinValue : b.WatchTold - days;
        }
    }
}
