using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The player's bank as one line of the save's modData, under <see cref="Key"/>: "v=1", then parts divided by '^',
    /// each a letter and a colon, its records divided by ';' and fields by '|'.
    ///   s  state|name|seed|founded|end day|end cents|why|last day|capital|reserves|strategy|fund limit|rebalance|scare|
    ///      run|run from|stage|deadline|strikes|fined|month from|month|month income|month costs|last income|last costs|
    ///      month lost|last lost|lost in all|recovered in all|history count|test|closures|watch told|stood still
    ///   d  each kind of saver: deposits|points over the rivals
    ///   l  each kind of borrower: loans|arrears|recovering|its days|points over the rivals|lending
    ///   f  the fund's holdings: stock|ticker|name|shares|cost|last price|dividend
    ///   m  the months' profit, the oldest first
    ///   e  the end game's scale: the day it began|tellers|managers|loan officers|compliance officers|the least capital founded under
    ///   k  the branches' counters: fees not yet booked|the last hour worked
    ///   b  each branch: place|name|carry|month from|the week's seven days|their visits seen to|this month's figures|last month's
    /// A later version adds fields at the end of a record or a part with a new letter; an older line's missing fields read as none.
    /// </summary>
    public static class OwnBankStore
    {
        public const string Key = "reallife:economy_ownbank";
        public const int Version = 1;
        public const int HoldingsMost = 40;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(OwnBank b)
        {
            var t = new StringBuilder(1024);
            t.Append("v=").Append(Version).Append("^s:");
            long[] head =
            {
                (int)b.State, 0, b.Seed, b.FoundedDay, b.EndDay, b.EndCents, (int)b.Why, b.LastDay, b.Capital, b.Reserves, b.Strategy, b.FundLimit, b.Rebalance ? 1 : 0, b.Scare,
                b.Run ? 1 : 0, b.RunFrom, (int)b.Stage, b.Deadline, b.Strikes, b.FinedCents, b.MonthFrom, b.Month, b.MonthIncome, b.MonthCosts, b.LastIncome, b.LastCosts,
                b.MonthLost, b.LastLost, b.LostAll, b.RecoveredAll, b.HistoryCount, b.Test ? 1 : 0, b.Closures, b.WatchTold, b.Still ? 1 : 0
            };
            for (int f = 0; f < head.Length; f++)
            {
                if (f > 0)
                {
                    t.Append('|');
                }
                if (f == 1)
                {
                    t.Append(Escape(b.Name));
                }
                else
                {
                    t.Append(head[f].ToString(Inv));
                }
            }
            t.Append("^d:");
            for (int k = 0; k < OwnBankCatalogue.Savers; k++)
            {
                t.Append(k > 0 ? ";" : string.Empty).Append(b.Deposits[k].ToString(Inv)).Append('|').Append(b.SaverOffset[k].ToString(Inv));
            }
            t.Append("^l:");
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                t.Append(j > 0 ? ";" : string.Empty).Append(b.Loans[j].ToString(Inv)).Append('|').Append(b.Arrears[j].ToString(Inv)).Append('|').Append(b.Recovering[j].ToString(Inv))
                    .Append('|').Append(b.Recover[j].ToString(Inv)).Append('|').Append(b.LoanOffset[j].ToString(Inv)).Append('|').Append(b.Lending[j] ? '1' : '0');
            }
            t.Append("^f:");
            for (int i = 0; i < b.Holdings.Count && i < HoldingsMost; i++)
            {
                FundHolding h = b.Holdings[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(h.StockId.ToString(Inv)).Append('|').Append(Escape(h.Ticker)).Append('|').Append(Escape(h.Name)).Append('|')
                    .Append(h.Qty.ToString(Inv)).Append('|').Append(h.CostCents.ToString(Inv)).Append('|').Append(h.LastCents.ToString(Inv)).Append('|').Append(h.DpsCents.ToString(Inv));
            }
            t.Append("^m:");
            for (int i = 0; i < b.History.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(b.History[i].ToString(Inv));
            }
            t.Append("^e:").Append(b.ScaleFrom.ToString(Inv));
            for (int k = 0; k < OwnBankCatalogue.CrewKinds; k++)
            {
                t.Append('|').Append(b.Crew[k].ToString(Inv));
            }
            t.Append('|').Append(b.FoundedLeast.ToString(Inv));
            t.Append("^k:").Append(b.CounterFees.ToString(Inv)).Append('|').Append(b.CounterHour.ToString(Inv));
            if (b.Branches.Count > 0)
            {
                t.Append("^b:");
                for (int i = 0; i < b.Branches.Count && i < BranchCatalogue.BranchesMost; i++)
                {
                    WriteBranch(t.Append(i > 0 ? ";" : string.Empty), b.Branches[i]);
                }
            }
            return t.ToString();
        }

        /// <summary>The bank of a save's line; null if the save has none or it cannot be read.</summary>
        public static OwnBank Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            string[] parts = text.Split('^');
            if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var b = new OwnBank();
            bool head = false;
            try
            {
                for (int p = 1; p < parts.Length; p++)
                {
                    string part = parts[p];
                    if (part.Length < 2 || part[1] != ':')
                    {
                        continue;
                    }
                    string body = part.Substring(2);
                    switch (part[0])
                    {
                        case 's':
                            head = ReadHead(body.Split('|'), b);
                            break;
                        case 'd':
                            string[] savers = body.Split(';');
                            for (int k = 0; k < OwnBankCatalogue.Savers && k < savers.Length; k++)
                            {
                                string[] c = savers[k].Split('|');
                                b.Deposits[k] = Math.Max(0, L(c, 0));
                                b.SaverOffset[k] = Step(I(c, 1), OwnBankCatalogue.SaverOffsetLeast, OwnBankCatalogue.SaverOffsetMost, OwnBankCatalogue.SaverOffsetStep);
                            }
                            break;
                        case 'l':
                            string[] books = body.Split(';');
                            for (int j = 0; j < OwnBankCatalogue.Segments && j < books.Length; j++)
                            {
                                string[] c = books[j].Split('|');
                                b.Loans[j] = Math.Max(0, L(c, 0));
                                b.Arrears[j] = Math.Max(0, L(c, 1));
                                b.Recovering[j] = Math.Max(0, L(c, 2));
                                b.Recover[j] = Math.Max(0, I(c, 3));
                                b.LoanOffset[j] = Step(I(c, 4), OwnBankCatalogue.LoanOffsetLeast, OwnBankCatalogue.LoanOffsetMost, OwnBankCatalogue.LoanOffsetStep);
                                b.Lending[j] = I(c, 5) != 0;
                            }
                            break;
                        case 'f':
                            foreach (string record in body.Split(';'))
                            {
                                string[] c = record.Split('|');
                                if (c.Length < 7 || b.Holdings.Count >= HoldingsMost)
                                {
                                    continue;
                                }
                                var h = new FundHolding
                                {
                                    StockId = I(c, 0), Ticker = Unescape(c[1]), Name = Unescape(c[2]), Qty = Math.Max(0, L(c, 3)), CostCents = Math.Max(0, L(c, 4)),
                                    LastCents = Math.Max(0, I(c, 5)), DpsCents = Math.Max(0, I(c, 6))
                                };
                                if (h.Qty > 0 && b.Find(h.StockId) == null)
                                {
                                    b.Holdings.Add(h);
                                }
                            }
                            break;
                        case 'm':
                            string[] months = body.Split('|');
                            for (int i = 0; i < b.History.Length && i < months.Length; i++)
                            {
                                b.History[i] = L(months, i);
                            }
                            break;
                        case 'e':
                            string[] scale = body.Split('|');
                            b.ScaleFrom = scale.Length > 0 && scale[0].Length > 0 ? I(scale, 0) : EndScaleRules.Unset;
                            for (int k = 0; k < OwnBankCatalogue.CrewKinds; k++)
                            {
                                b.Crew[k] = Math.Max(0, I(scale, k + 1));
                            }
                            b.FoundedLeast = Math.Max(0, L(scale, OwnBankCatalogue.CrewKinds + 1));
                            break;
                        case 'k':
                            string[] counter = body.Split('|');
                            b.CounterFees = Math.Max(0, L(counter, 0));
                            b.CounterHour = counter.Length > 1 && counter[1].Length > 0 ? I(counter, 1) : int.MinValue;
                            break;
                        case 'b':
                            foreach (string record in body.Split(';'))
                            {
                                BankBranch br = ReadBranch(record.Split('|'));
                                if (br != null && b.Branches.Count < BranchCatalogue.BranchesMost && b.Branch(br.Place) == null)
                                {
                                    b.Branches.Add(br);
                                }
                            }
                            break;
                    }
                }
            }
            catch (FormatException)
            {
                return null;
            }
            return head ? b : null;
        }

        private static bool ReadHead(string[] c, OwnBank b)
        {
            if (c.Length < 31)
            {
                return false;
            }
            b.State = (OwnBankState)Clamp(I(c, 0), 0, (int)OwnBankState.Closed);
            b.Name = Unescape(c[1]);
            b.Seed = I(c, 2);
            b.FoundedDay = I(c, 3);   // the test tools' rewind can take a bank's days under nothing
            b.EndDay = Math.Max(0, I(c, 4));
            b.EndCents = Math.Max(0, L(c, 5));
            b.Why = (ClosedWhy)Clamp(I(c, 6), 0, (int)ClosedWhy.Run);
            b.LastDay = I(c, 7);
            b.Capital = L(c, 8);
            b.Reserves = L(c, 9);
            b.Strategy = Clamp(I(c, 10), 0, OwnBankCatalogue.Strategies - 1);
            b.FundLimit = Step(I(c, 11), 0, OwnBankCatalogue.FundLimitMost, OwnBankCatalogue.FundLimitStep);
            b.Rebalance = I(c, 12) != 0;
            b.Scare = Clamp(I(c, 13), 0, OwnBankCatalogue.ScareMost);
            b.Run = I(c, 14) != 0;
            b.RunFrom = Math.Max(0, I(c, 15));
            b.Stage = (Regulator)Clamp(I(c, 16), 0, (int)Regulator.Breach);
            b.Deadline = Math.Max(0, I(c, 17));
            b.Strikes = Clamp(I(c, 18), 0, OwnBankCatalogue.StrikesClose);
            b.FinedCents = Math.Max(0, L(c, 19));
            b.MonthFrom = I(c, 20);
            b.Month = Math.Max(0, I(c, 21));
            b.MonthIncome = Math.Max(0, L(c, 22));
            b.MonthCosts = Math.Max(0, L(c, 23));
            b.LastIncome = Math.Max(0, L(c, 24));
            b.LastCosts = Math.Max(0, L(c, 25));
            b.MonthLost = Math.Max(0, L(c, 26));
            b.LastLost = Math.Max(0, L(c, 27));
            b.LostAll = Math.Max(0, L(c, 28));
            b.RecoveredAll = Math.Max(0, L(c, 29));
            b.HistoryCount = Clamp(I(c, 30), 0, OwnBankCatalogue.HistoryMonths);
            b.Test = I(c, 31) != 0;
            b.Closures = Clamp(I(c, 32), 0, 1000);
            if (b.Closures == 0 && b.State == OwnBankState.Closed && !b.Test)
            {
                b.Closures = 1;   // a line written before closures were counted: its bank was closed once
            }
            b.WatchTold = c.Length > 33 ? I(c, 33) : int.MinValue;   // a line written before it was kept: never told
            b.Still = I(c, 34) != 0;
            return true;
        }

        private static void WriteBranch(StringBuilder t, BankBranch br)
        {
            t.Append(Escape(br.Place)).Append('|').Append(Escape(br.Name)).Append('|').Append(br.Carry.ToString(Inv)).Append('|').Append(br.MonthFrom.ToString(Inv));
            for (int i = 0; i < BranchCatalogue.WeekDays; i++)
            {
                t.Append('|').Append(br.Days[i].ToString(Inv));
            }
            for (int i = 0; i < BranchCatalogue.WeekDays; i++)
            {
                t.Append('|').Append(br.Week[i].ToString(Inv));
            }
            for (int i = 0; i < BranchFigure.Count; i++)
            {
                t.Append('|').Append(br.Month[i].ToString(Inv));
            }
            for (int i = 0; i < BranchFigure.Count; i++)
            {
                t.Append('|').Append(br.Last[i].ToString(Inv));
            }
        }

        // A branch's record; null without its place. Figures a later version adds read as none on an older line.
        private static BankBranch ReadBranch(string[] c)
        {
            if (c.Length < 4 || c[0].Length == 0)
            {
                return null;
            }
            var br = new BankBranch { Place = Unescape(c[0]), Name = Unescape(c[1]), Carry = Clamp(I(c, 2), 0, 999), MonthFrom = I(c, 3) };
            int at = 4;
            for (int i = 0; i < BranchCatalogue.WeekDays; i++, at++)
            {
                br.Days[i] = at < c.Length && c[at].Length > 0 ? I(c, at) : int.MinValue;
            }
            for (int i = 0; i < BranchCatalogue.WeekDays; i++, at++)
            {
                br.Week[i] = Math.Max(0, I(c, at));
            }
            for (int i = 0; i < BranchFigure.Count; i++, at++)
            {
                br.Month[i] = Math.Max(0, L(c, at));
            }
            for (int i = 0; i < BranchFigure.Count; i++, at++)
            {
                br.Last[i] = Math.Max(0, L(c, at));
            }
            return br;
        }

        /// <summary>A line read back that no longer balances has its reserves made what the books say. True if it had to be.</summary>
        public static bool Mend(OwnBank b)
        {
            if (!b.Running || OwnBankRules.Balanced(b))
            {
                return false;
            }
            b.Reserves = b.Savings + b.Capital - b.Lent - b.InArrears - b.FundCost;
            return true;
        }

        private static int I(string[] c, int i)
        {
            return i < c.Length && int.TryParse(c[i], NumberStyles.Integer, Inv, out int v) ? v : 0;
        }

        private static long L(string[] c, int i)
        {
            return i < c.Length && long.TryParse(c[i], NumberStyles.Integer, Inv, out long v) ? v : 0;
        }

        private static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        // A choice read back within its bounds and on its step.
        private static int Step(int value, int least, int most, int step)
        {
            int v = Clamp(value, least, most);
            return least + (v - least) / step * step;
        }

        // '%', '^', ';' and '|' written as %25, %5E, %3B and %7C.
        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            return s.Replace("%", "%25").Replace("^", "%5E").Replace(";", "%3B").Replace("|", "%7C");
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            return s.Replace("%7C", "|").Replace("%3B", ";").Replace("%5E", "^").Replace("%25", "%");
        }
    }
}
