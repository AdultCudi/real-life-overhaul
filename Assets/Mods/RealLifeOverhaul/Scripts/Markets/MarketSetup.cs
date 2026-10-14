using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>A company's price brought from the old market: its new id, its last price and its last closes (oldest first).</summary>
    public struct CarriedPrice
    {
        public int Id;
        public int Cents;
        public int[] Closes;
        public int CloseCount;
        public string Name;          // a rival's holding: the name it was listed under
    }

    /// <summary>
    /// A new market for a save. Game-free. Every company of the catalogue that is listed at the start, and up to four
    /// holdings of the game's own named rivals, at the city's price level, each with a history of its last hundred
    /// market days and the two years before (made up from the moves its prices have; a company brought over from the
    /// old market keeps its price and its last closes). The city's index starts at 1,000 and so does each sector's.
    /// The same save opened twice on the same day gives the same market (its seed is the playthrough's and the day).
    /// </summary>
    public static class MarketSetup
    {
        public static MarketState Open(ulong id, int day, int minute, int year, in MarketEnv env, ulong seed, IList<string> rivalNames, IList<string> rivalIds,
            IList<CarriedPrice> carried)
        {
            year = Math.Max(8, year);
            var state = new MarketState
            {
                Id = id, CreatedDay = day, YearDays = year, Day = day, Env = env, BusinessDay = day, RateSeenBp = env.RateBp, ElectionsSeen = env.Elections
            };
            state.Phase = MarketCatalogue.TradingDay(day) ? MarketCatalogue.PhaseAt(minute) : MarketCatalogue.PhaseClose;
            for (int i = 0; i < state.SectorVar.Length; i++)
            {
                state.SectorVar[i] = 1.0;
            }
            for (int i = 0; i < MarketCatalogue.All.Length; i++)
            {
                CompanyDef d = MarketCatalogue.All[i];
                if (d.Pipeline)
                {
                    continue;
                }
                CarriedPrice c = Find(carried, d.Id);
                Add(state, MarketLife.Make(d, null, day, env, year, seed, c.Cents), c, day, year, seed);
            }
            // The game's rivals: the names the old market listed them under first, then the game's own.
            int r = 0;
            for (int i = 0; carried != null && i < carried.Count && r < MarketCatalogue.RivalTemplates.Length; i++)
            {
                if (MarketCatalogue.IsRival(carried[i].Id) && !string.IsNullOrEmpty(carried[i].Name) && Rival(state, carried[i].Name) == null)
                {
                    CompanyDef t = MarketCatalogue.RivalTemplates[r++];
                    Stock s = MarketLife.Make(t, carried[i].Name, day, env, year, seed, carried[i].Cents);
                    s.RivalId = RivalId(rivalNames, rivalIds, carried[i].Name);
                    Add(state, s, carried[i], day, year, seed);
                }
            }
            for (int i = 0; rivalNames != null && i < rivalNames.Count && r < MarketCatalogue.RivalTemplates.Length; i++)
            {
                if (string.IsNullOrEmpty(rivalNames[i]) || Rival(state, rivalNames[i]) != null)
                {
                    continue;
                }
                CompanyDef t = MarketCatalogue.RivalTemplates[r++];
                Stock s = MarketLife.Make(t, rivalNames[i], day, env, year, seed, 0);
                s.RivalId = rivalIds != null && i < rivalIds.Count ? rivalIds[i] : null;
                Add(state, s, default(CarriedPrice), day, year, seed);
            }
            Indices(state, year);
            MarketLife.DayBases(state, state.InSession);   // (as a save of this moment reads: MarketStore.Read)
            return state;
        }

        private static CarriedPrice Find(IList<CarriedPrice> carried, int id)
        {
            for (int i = 0; carried != null && i < carried.Count; i++)
            {
                if (carried[i].Id == id)
                {
                    return carried[i];
                }
            }
            return default(CarriedPrice);
        }

        /// <summary>The rival holding listed under a name; null if there is none.</summary>
        public static Stock Rival(MarketState state, string name)
        {
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                if (MarketCatalogue.IsRival(state.Stocks[i].Id) && state.Stocks[i].Name == name)
                {
                    return state.Stocks[i];
                }
            }
            return null;
        }

        private static string RivalId(IList<string> names, IList<string> ids, string name)
        {
            for (int i = 0; names != null && ids != null && i < names.Count && i < ids.Count; i++)
            {
                if (names[i] == name)
                {
                    return ids[i];
                }
            }
            return null;
        }

        private static void Add(MarketState state, Stock s, in CarriedPrice c, int day, int year, ulong seed)
        {
            double dayVol = MarketCatalogue.VolScale(year) * Math.Sqrt(Math.Pow(MarketCatalogue.IndexDayVol * s.Def.Beta / 100.0, 2) + Math.Pow(MarketCatalogue.Sectors[(int)s.Sector].SectorVolBp / 10000.0, 2)
                                      + Math.Pow(s.Def.IdioBp / 10000.0, 2));
            MarketLife.MakePast(s, day, year, seed, dayVol, c.Closes, c.Closes != null ? Math.Min(c.CloseCount, c.Closes.Length) : 0);
            s.ListedDay = ListedLongAgo(day, year);
            state.Stocks.Add(s);
        }

        /// <summary>The day a company of a new market counts as listed on: years before (it is no new listing).</summary>
        public static int ListedLongAgo(int day, int year)
        {
            return day - 5 * Math.Max(8, year);
        }

        // The index and each sector's at 1,000 today, and their histories from the companies' (the same day's closes added up).
        private static void Indices(MarketState state, int year)
        {
            double all = 0.0;
            var sectors = new double[(int)MarketSector.Count];
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                Stock s = state.Stocks[i];
                double value = s.Shares * s.Last / 100.0;
                all += value;
                sectors[(int)s.Sector] += value;
            }
            state.IndexDivisor = all / 1000.0;
            for (int sec = 0; sec < sectors.Length; sec++)
            {
                state.SectorDivisor[sec] = sectors[sec] > 0.0 ? sectors[sec] / 1000.0 : 0.0;
                state.SectorLast[sec] = state.SectorPrevClose[sec] = sectors[sec] > 0.0 ? 100000 : 0;
            }
            state.IndexLast = state.IndexOpen = state.IndexHigh = state.IndexLow = state.IndexPrevClose = 100000;
            // Each past day: the companies' candles of that day weighed by their shares.
            var days = new SortedDictionary<int, double[]>();
            var monthly = new SortedDictionary<int, double[]>();
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                Stock s = state.Stocks[i];
                AddUp(days, s.Daily, s.Shares, (int)s.Sector);
                AddUp(monthly, s.Monthly, s.Shares, (int)s.Sector);
            }
            foreach (KeyValuePair<int, double[]> m in monthly)
            {
                state.IndexMonthly.Add(IndexCandle(m.Key, m.Value, state.IndexDivisor));
            }
            foreach (KeyValuePair<int, double[]> d in days)
            {
                state.IndexDaily.Add(IndexCandle(d.Key, d.Value, state.IndexDivisor));
                for (int sec = 0; sec < sectors.Length; sec++)
                {
                    if (state.SectorDivisor[sec] > 0.0)
                    {
                        state.SectorDaily[sec].Add((int)Math.Round(d.Value[5 + sec] / state.SectorDivisor[sec]));
                    }
                }
            }
            if (state.IndexDaily.Count > 0)
            {
                state.IndexPrevClose = state.IndexDaily[state.IndexDaily.Count - 1].C;
            }
        }

        // Open, high, low and close of each day (money: shares times cents) and each sector's close.
        private static void AddUp(SortedDictionary<int, double[]> into, List<Candle> candles, long shares, int sector)
        {
            for (int i = 0; i < candles.Count; i++)
            {
                Candle c = candles[i];
                if (!into.TryGetValue(c.Day, out double[] sums))
                {
                    sums = new double[5 + (int)MarketSector.Count];
                    into[c.Day] = sums;
                }
                sums[0] += shares * (double)c.O;
                sums[1] += shares * (double)c.H;
                sums[2] += shares * (double)c.L;
                sums[3] += shares * (double)c.C;
                sums[4] += 1;
                sums[5 + sector] += shares * (double)c.C;
            }
        }

        private static Candle IndexCandle(int day, double[] sums, double divisor)
        {
            int o = (int)Math.Round(sums[0] / divisor), h = (int)Math.Round(sums[1] / divisor), l = (int)Math.Round(sums[2] / divisor), c = (int)Math.Round(sums[3] / divisor);
            return new Candle(day, o, Math.Max(h, Math.Max(o, c)), Math.Min(l, Math.Min(o, c)), c, 0);
        }
    }
}
