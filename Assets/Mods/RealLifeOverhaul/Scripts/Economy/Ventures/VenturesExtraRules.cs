using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// Investors beyond the first deal, game-free and seeded by the save: who each has backed and where, the buy-back term a deal carries, the promises checked each month, what
    /// the other investors hear when one is left unpaid, the player's valuation and a rival raising money from the same investors. Everything that is not a decision of the player
    /// is a hash of the save's seed and what it is about, so the same save gives the same investors on every machine.
    /// </summary>
    public static class VenturesExtraRules
    {
        public const int WaryDays = 45;          // other investors who ask around keep away this long after one was left unpaid
        public const int WaryDiligence = 40;     // the diligence at which an investor asks around
        public const int BrokenWaryDays = 15;    // and a broken promise keeps the most careful away this long
        public const int BrokenDiligence = 65;
        public const int BuyBackYears = 3;
        public const int RivalRaiseChancePct = 22;    // a month, for each rival
        public const int RaiseLeastPct = 25, RaiseMostPct = 60;   // of the investor's largest sum, what a rival's round takes of it
        public const int CommittedMonths = 3;    // a round takes that sum from the investor for this many months

        // ------------------------------------------------------------------------------------------------- who they are

        /// <summary>The firms an investor has put money into: a figure made once from the seed, and one more for each deal with the player.</summary>
        public static int Portfolio(VenturesFile file, Investor investor)
        {
            int k = (int)investor.Kind;
            int least = k == 0 ? 3 : k == 1 ? 7 : 14, most = k == 0 ? 9 : k == 1 ? 22 : 38;
            int roll = (int)(VenturesRandom.Hash(file.Seed, investor.Id, 9101) % (uint)(most - least + 1));
            int deals = 0;
            for (int i = 0; i < file.Deals.Count; i++)
            {
                deals += file.Deals[i].InvestorId == investor.Id ? 1 : 0;
            }
            return least + roll + deals;
        }

        /// <summary>The names of the firms an investor also backs, at most three, with their stake in per cent: the rival studios and listed companies of the save.</summary>
        public static int AlsoBacks(VenturesFile file, Investor investor, string[] names, int[] stakes)
        {
            int found = 0;
            int pool = file.Companies.Count;
            if (pool == 0)
            {
                return 0;
            }
            int start = (int)(VenturesRandom.Hash(file.Seed, investor.Id, 9102) % (uint)pool);
            int step = 1 + (int)(VenturesRandom.Hash(file.Seed, investor.Id, 9103) % (uint)Math.Max(1, pool - 1));
            for (int i = 0; i < pool && found < names.Length; i++)
            {
                int at = (start + i * step) % pool;
                bool seen = false;
                for (int f = 0; f < found; f++)
                {
                    seen |= names[f] == file.Companies[at].Name;
                }
                if (seen)
                {
                    continue;
                }
                // A rival of the game's is the more likely to be on the list: the investor lent it money to grow.
                bool keep = file.Companies[at].Rival || VenturesRandom.Percent(file.Seed, investor.Id * 31 + at, 9104) < 45;
                if (keep)
                {
                    names[found] = file.Companies[at].Name;
                    stakes[found] = 2 + (int)(VenturesRandom.Hash(file.Seed, investor.Id * 31 + at, 9105) % 17u);
                    found++;
                }
            }
            return found;
        }

        /// <summary>What a deal values the business at: the money put in over the share it buys. The deal is a share of profit, so this is an implied figure.</summary>
        public static long Implied(int amount, int shareBp) => shareBp <= 0 ? 0L : (long)amount * 10000L / shareBp;

        // ------------------------------------------------------------------------------------------------- the buy-back term

        /// <summary>
        /// The term a deal will carry, known from the offer on (the key is the offer's number, then the deal's): patient investors ask none, an even one may allow it, a demanding
        /// one may call it. Its day is counted from the deal's start, so the term here holds the years in days. Null for none.
        /// </summary>
        public static DealTerm TermFor(VenturesFile file, Investor investor, int key, int yearDays)
        {
            int roll = VenturesRandom.Percent(file.Seed, key, 9201);
            BuyBackMode mode = investor.Temper == Temperament.Patient ? BuyBackMode.None
                : investor.Temper == Temperament.Even ? (roll < 50 ? BuyBackMode.Optional : BuyBackMode.None) : (roll < 70 ? BuyBackMode.Callable : BuyBackMode.Optional);
            if (mode == BuyBackMode.None)
            {
                return null;
            }
            int times10 = investor.Kind == InvestorKind.Firm ? 20 : 15;
            return new DealTerm { DealId = 0, Day = BuyBackYears * yearDays, Times10 = times10, Mode = mode };
        }

        /// <summary>What the term costs to settle: the multiple of the money put in, less what was paid out, and a quarter of the money at least.</summary>
        public static int BuyBackPrice(Deal deal, DealTerm term)
        {
            long gross = (long)deal.Amount * term.Times10 / 10L - deal.PaidOut;
            return (int)Math.Max(Math.Max(1L, (long)deal.Amount * VenturesCatalogue.CallLeastPct / 100L), gross);
        }

        /// <summary>Settles a deal at its term: the price is paid and the investor leaves. False, and nothing changed, when the cash is not there or the term is not an option.</summary>
        public static bool BuyBack(VenturesFile file, VenturesExtra extra, Deal deal, DealTerm term, IWallet wallet)
        {
            int price = BuyBackPrice(deal, term);
            Investor investor = file.Investor(deal.InvestorId);
            if (wallet.Cash < price || !wallet.Take(price, VenturesMoney.BuyOut, investor != null ? investor.Name : string.Empty))
            {
                return false;
            }
            file.MoveReputation(VenturesCatalogue.ReputationBoughtOut, deal.Test);
            file.Deals.Remove(deal);
            extra.Terms.Remove(term);
            extra.Prune(file);
            return true;
        }

        // ------------------------------------------------------------------------------------------------- the promises

        /// <summary>The month a day is in.</summary>
        public static int MonthOf(int day, int monthDays) => day / Math.Max(1, monthDays);

        /// <summary>The promises a deal will carry beyond its target, known from the offer on: how many (none to two) and of which kind, by how the investor is and a roll of the key.</summary>
        public static int PlannedPromises(VenturesFile file, Investor investor, int key, PromiseKind[] kinds)
        {
            int chance = investor.Temper == Temperament.Patient ? 20 : investor.Temper == Temperament.Even ? 50 : 100;
            if (VenturesRandom.Percent(file.Seed, key, 9301) >= chance)
            {
                return 0;
            }
            int first = (int)(VenturesRandom.Hash(file.Seed, key, 9302) % (uint)PromiseKind.Count);
            int count = investor.Temper == Temperament.Demanding && VenturesRandom.Percent(file.Seed, key, 9303) < 40 ? 2 : 1;
            count = Math.Min(count, kinds.Length);
            for (int n = 0; n < count; n++)
            {
                kinds[n] = (PromiseKind)((first + n) % (int)PromiseKind.Count);
            }
            return count;
        }

        /// <summary>The months a promise of a kind gives, for the page to say them: firms by six months, morale for four, a report each quarter.</summary>
        public static int MonthsOf(PromiseKind kind) => kind == PromiseKind.Firms ? 6 : kind == PromiseKind.Morale ? 4 : 3;

        /// <summary>The promises of a deal made at its start, from the plan of its offer.</summary>
        public static int MakePromises(VenturesExtra extra, Deal deal, PromiseKind[] kinds, int count, int day, int monthDays, int firmsNow)
        {
            int made = 0;
            for (int n = 0; n < count; n++)
            {
                var p = new Promise { Id = extra.NextId++, DealId = deal.Id, Kind = kinds[n], Checked = MonthOf(day, monthDays), Due = day + MonthsOf(kinds[n]) * monthDays };
                p.Target = kinds[n] == PromiseKind.Firms ? firmsNow + 2 : kinds[n] == PromiseKind.Morale ? 60 : 0;
                if (extra.Promises.Count >= VenturesExtra.PromisesMost)
                {
                    RemoveResolved(extra);
                }
                if (extra.Promises.Count < VenturesExtra.PromisesMost)
                {
                    extra.Promises.Add(p);
                    made++;
                }
            }
            return made;
        }

        private static void RemoveResolved(VenturesExtra extra)
        {
            for (int i = 0; i < extra.Promises.Count; i++)
            {
                if (extra.Promises[i].State != 0)
                {
                    extra.Promises.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>A promise as the deal's page reads it: how many of the deal's promises are kept, how many it made, and whether any is broken.</summary>
        public static void Kept(VenturesExtra extra, int dealId, out int kept, out int total, out bool broken)
        {
            kept = total = 0;
            broken = false;
            for (int i = 0; i < extra.Promises.Count; i++)
            {
                Promise p = extra.Promises[i];
                if (p.DealId != dealId)
                {
                    continue;
                }
                if (p.Kind == PromiseKind.Report)
                {
                    kept += p.Kept;
                    total += p.Kept + p.Missed;
                    broken |= p.Missed > 0;
                }
                else
                {
                    total++;
                    kept += p.State == 1 ? 1 : 0;
                    broken |= p.State == 2;
                }
            }
        }

        /// <summary>Sending the quarter's report: it counts for the report promise of the deal that is judged next. False when the deal has none.</summary>
        public static bool SendReport(VenturesExtra extra, int dealId, int day)
        {
            bool sent = false;
            for (int i = 0; i < extra.Promises.Count; i++)
            {
                Promise p = extra.Promises[i];
                if (p.DealId == dealId && p.Kind == PromiseKind.Report && p.State == 0 && p.ReportDay != day)
                {
                    p.ReportDay = day;
                    sent = true;
                }
            }
            return sent;
        }

        /// <summary>
        /// The monthly look, once for each new month: every promise of a live deal is judged on its kind, a report promise at the end of each quarter, a term called when its day
        /// has come, and the month's valuation is put in the ring. 'firms' is how many businesses the player runs, 'morale' the staff's standing on average or -1 with no staff.
        /// </summary>
        public static bool Month(VenturesFile file, VenturesExtra extra, int day, int monthDays, int firms, int morale, int level, List<DealEvent> said)
        {
            int month = MonthOf(day, monthDays);
            if (extra.MonthDone == month)
            {
                return false;
            }
            extra.MonthDone = month;
            int quarter = 3 * monthDays;
            extra.Prune(file);
            for (int i = 0; i < extra.Promises.Count; i++)
            {
                Promise p = extra.Promises[i];
                Deal deal = file.Deal(p.DealId);
                if (deal == null || p.State != 0 || p.Checked == month)
                {
                    continue;
                }
                p.Checked = month;
                if (deal.Stage == DealStage.Called || deal.Stage == DealStage.Cold)
                {
                    continue;
                }
                if (p.Kind == PromiseKind.Firms && day >= p.Due)
                {
                    p.State = firms >= p.Target ? 1 : 2;
                }
                else if (p.Kind == PromiseKind.Morale)
                {
                    p.Missed += morale >= 0 && morale < p.Target ? 1 : 0;
                    p.State = p.Missed > 0 ? 2 : day >= p.Due ? 1 : 0;
                }
                else if (p.Kind == PromiseKind.Report && day >= p.Due)
                {
                    bool sent = p.ReportDay > p.Due - quarter;
                    p.Kept += sent ? 1 : 0;
                    p.Missed += sent ? 0 : 1;
                    p.Due += quarter;
                    if (sent)
                    {
                        DealRules.KeepPromise(deal, said);
                    }
                    else
                    {
                        DealRules.BreakPromise(file, deal, day, level, said);
                    }
                    continue;
                }
                if (p.State == 1)
                {
                    DealRules.KeepPromise(deal, said);
                }
                else if (p.State == 2)
                {
                    DealRules.BreakPromise(file, deal, day, level, said);
                }
            }
            for (int i = extra.Terms.Count - 1; i >= 0; i--)
            {
                DealTerm term = extra.Terms[i];
                Deal deal = file.Deal(term.DealId);
                if (deal == null)
                {
                    extra.Terms.RemoveAt(i);
                }
                else if (term.Mode == BuyBackMode.Callable && day >= term.Day && deal.Stage != DealStage.Called)
                {
                    int owed = BuyBackPrice(deal, term);
                    DealRules.StartCall(deal, owed, level);
                    said?.Add(new DealEvent { What = DealNews.Called, DealId = deal.Id, InvestorId = deal.InvestorId, Amount = owed, Business = deal.Business });
                    extra.Terms.RemoveAt(i);
                }
            }
            extra.Remember((int)Math.Min(int.MaxValue, Valuation(file)));
            Committed(file, month);
            return true;
        }

        // ------------------------------------------------------------------------------------------------- what other investors hear

        /// <summary>One investor was left unpaid: the others who ask around keep away for a while, and it is written down that the word got round.</summary>
        public static int SpreadCold(VenturesFile file, VenturesExtra extra, int coldInvestorId, int day)
        {
            int heard = 0;
            for (int i = 0; i < file.Investors.Count; i++)
            {
                Investor v = file.Investors[i];
                if (v.Id != coldInvestorId && v.Diligence >= WaryDiligence)
                {
                    v.ClosedUntil = Math.Max(v.ClosedUntil, day + WaryDays);
                    heard++;
                }
            }
            extra.Hear(coldInvestorId, day, 0);
            return heard;
        }

        /// <summary>A promise was broken: the most careful of the other investors keep away for a short while.</summary>
        public static int SpreadBroken(VenturesFile file, VenturesExtra extra, int investorId, int day)
        {
            int heard = 0;
            for (int i = 0; i < file.Investors.Count; i++)
            {
                Investor v = file.Investors[i];
                if (v.Id != investorId && v.Diligence >= BrokenDiligence)
                {
                    v.ClosedUntil = Math.Max(v.ClosedUntil, day + BrokenWaryDays);
                    heard++;
                }
            }
            extra.Hear(investorId, day, 1);
            return heard;
        }

        // ------------------------------------------------------------------------------------------------- the valuation and a rival raising

        /// <summary>What the deals together value the player's firm at: the money put in over the share it bought, across the deals that are running. 0 with no deal.</summary>
        public static long Valuation(VenturesFile file)
        {
            long money = 0, share = 0;
            for (int i = 0; i < file.Deals.Count; i++)
            {
                Deal d = file.Deals[i];
                if (d.Stage != DealStage.Called)
                {
                    money += d.Amount;
                    share += d.ShareBp;
                }
            }
            return share <= 0 ? 0L : money * 10000L / share;
        }

        /// <summary>The rivals that raise money, at most three: the listed companies of the game's rivals, else the first listed ones.</summary>
        public static int Rivals(VenturesFile file, string[] names)
        {
            int found = 0;
            for (int pass = 0; pass < 2 && found < names.Length; pass++)
            {
                for (int i = 0; i < file.Companies.Count && found < names.Length; i++)
                {
                    bool take = pass == 0 ? file.Companies[i].Rival : !file.Companies[i].Rival && file.Companies[i].Sector == MarketSector.Entertainment;
                    if (take)
                    {
                        names[found++] = file.Companies[i].Name;
                    }
                }
            }
            for (int i = 0; found == 0 && i < file.Companies.Count && i < names.Length; i++)
            {
                names[found++] = file.Companies[i].Name;
            }
            return found;
        }

        /// <summary>A rival's valuation in a month: its own start, growth and a swing, plus a quarter more for the quarter after it raised. The same for the same save.</summary>
        public static long RivalValue(VenturesFile file, int rival, int month)
        {
            long start = Prices.At(300000) + (long)(VenturesRandom.Hash(file.Seed, rival, 9401) % 2000000u);
            double swing = ((int)(VenturesRandom.Hash(file.Seed, rival * 977 + month, 9402) % 1001u) - 500) / 5000.0;
            double grown = start * (1.0 + 0.012 * month) * (1.0 + swing);
            double lift = Raised(file, rival, month - 1) || Raised(file, rival, month - 2) ? 1.25 : 1.0;
            return (long)(grown * lift);
        }

        /// <summary>True when a rival raised money in a month.</summary>
        public static bool Raised(VenturesFile file, int rival, int month)
        {
            return month >= 0 && VenturesRandom.Percent(file.Seed, rival * 977 + month, 9403) < RivalRaiseChancePct;
        }

        /// <summary>The investor a rival's round in a month came from, by index into the file's investors, and the share of their largest sum it took.</summary>
        public static int RaisedFrom(VenturesFile file, int rival, int month, out int takenPct)
        {
            takenPct = RaiseLeastPct + (int)(VenturesRandom.Hash(file.Seed, rival * 977 + month, 9404) % (uint)(RaiseMostPct - RaiseLeastPct + 1));
            int rich = 0;
            for (int i = 0; i < file.Investors.Count; i++)
            {
                rich += file.Investors[i].Kind == InvestorKind.Angel ? 0 : 1;
            }
            if (rich == 0)
            {
                return file.Investors.Count == 0 ? -1 : 0;
            }
            int pick = (int)(VenturesRandom.Hash(file.Seed, rival * 977 + month, 9405) % (uint)rich);
            for (int i = 0; i < file.Investors.Count; i++)
            {
                if (file.Investors[i].Kind != InvestorKind.Angel && pick-- == 0)
                {
                    return i;
                }
            }
            return 0;
        }

        /// <summary>Sets what each investor has committed to rivals' rounds of the last few months: it comes off the sum they can put into the player's firm.</summary>
        public static void Committed(VenturesFile file, int month)
        {
            for (int i = 0; i < file.Investors.Count; i++)
            {
                file.Investors[i].Committed = 0;
            }
            string[] names = new string[3];
            int rivals = Rivals(file, names);
            for (int r = 0; r < rivals; r++)
            {
                for (int m = month - CommittedMonths + 1; m <= month; m++)
                {
                    int pct;
                    int at = Raised(file, r, m) ? RaisedFrom(file, r, m, out pct) : -1;
                    if (at >= 0)
                    {
                        RaisedFrom(file, r, m, out pct);
                        Investor v = file.Investors[at];
                        v.Committed += (int)((long)Prices.At(v.ChequeMost) * pct / 100L);
                    }
                }
            }
        }

        /// <summary>The month's rounds that rivals raised, for the page: the rival's index, the investor's index in the file and the sum.</summary>
        public static int RoundsOf(VenturesFile file, int month, int[] rival, int[] investor, int[] sum)
        {
            int n = 0;
            string[] names = new string[3];
            int rivals = Rivals(file, names);
            for (int r = 0; r < rivals && n < rival.Length; r++)
            {
                if (Raised(file, r, month))
                {
                    int pct;
                    int at = RaisedFrom(file, r, month, out pct);
                    if (at >= 0)
                    {
                        rival[n] = r;
                        investor[n] = at;
                        sum[n] = (int)((long)Prices.At(file.Investors[at].ChequeMost) * pct / 100L);
                        n++;
                    }
                }
            }
            return n;
        }
    }
}
