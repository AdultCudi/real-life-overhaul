using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// A firm of the player's that listed and the stock market (Tech/Venture/ExitStockRules, Markets/Listing/ListingFirm): the tech sector's bridges to the market,
        /// the firm's own statements as the stock's daily figures on the market's tape (MarketEnv.FirmStock), and the firm's takings kept out of the player's whole
        /// company once they are a stock's, so one set of takings never feeds two stocks. Asked on a game day, a market morning or a click; nothing per frame.
        /// </summary>
        public static class ExitStockLinks
        {
            private static readonly List<Stock> Listed = new List<Stock>(MarketEnv.FirmsMost);
            private static readonly HashSet<string> Places = new HashSet<string>();
            private static readonly List<BuildingRegistration> Mine = new List<BuildingRegistration>(16);
            private static readonly long[] QuarterRev = new long[10], QuarterProfit = new long[10];
            private static readonly int[] QuarterDays = new int[10];

            internal static void Connect()
            {
                StockBridges.Open = Open;
                StockBridges.Make = Make;
                StockBridges.Look = Look;
                StockBridges.End = End;
                StockBridges.Grow = Grow;
                StockBridges.Cancel = Cancel;
                StockBridges.Sweep = Sweep;
                StockBridges.Startup = Startup;
                StockBridges.Show = Show;
                MarketBridges.FirmDealt = Dealt;
            }

            internal static void Disconnect()
            {
                StockBridges.Reset();
                MarketBridges.FirmDealt = null;
                Places.Clear();
            }

            // ------------------------------------------------------------------------------------------------- the tech sector asks the market

            private static bool Open()
            {
                MarketDirector m = MarketDirector.Instance;
                return m != null && m.FirmsOpen;
            }

            private static StockInfo Info(Stock s)
            {
                return new StockInfo
                {
                    Live = s.Listed, Id = s.Id, Ticker = s.Ticker, Name = s.Name, Shares = s.Shares, Founder = s.Own != null ? s.Own.Founder : 0L, Float = s.Own != null ? s.Own.Float : 0L,
                    Last = s.Last, LockDay = s.Own != null ? s.Own.LockDay : 0
                };
            }

            private static StockWhy Why(FirmWhy why)
            {
                switch (why)
                {
                    case FirmWhy.None: return StockWhy.None;
                    case FirmWhy.Already: return StockWhy.Already;
                    case FirmWhy.TooMany: return StockWhy.TooMany;
                    case FirmWhy.Full: return StockWhy.Full;
                    default: return StockWhy.Off;
                }
            }

            private static StockInfo Look(string place)
            {
                try
                {
                    MarketDirector m = MarketDirector.Instance;
                    if (m == null || !m.FirmsOpen)
                    {
                        return new StockInfo { Why = StockWhy.Off };   // the market is off or another player's: the player's own is set aside, not gone
                    }
                    Stock s = m.FirmStock(place, true);
                    return s != null ? Info(s) : default(StockInfo);
                }
                catch (Exception e)
                {
                    Fault("ExitStockLinks.Look", e);
                    return default(StockInfo);
                }
            }

            private static StockInfo Make(StockAsk a)
            {
                try
                {
                    MarketDirector m = MarketDirector.Instance;
                    GameInstance save = SaveGameManager.Current;
                    if (m == null || !m.FirmsOpen || save == null)
                    {
                        return new StockInfo { Why = StockWhy.Off };
                    }
                    BuildingRegistration reg = TalentGame.Find(a.Place);
                    int trade = reg != null ? MarketInputs.TradeOf(reg.businessTypeName) : -1;
                    int year = Math.Max(8, m.State.YearDays);
                    var r = new FirmListing
                    {
                        Place = a.Place, Name = a.Name, Sector = trade >= 0 ? (int)ListingSector.SectorOf(trade) : (int)MarketSector.Software, Trade = trade, Shares = a.Shares,
                        Founder = a.Founder, Float = a.Float, PriceCents = a.PriceCents, LockDay = a.LockDay, ListedDay = a.ListedDay, VestBase = a.VestBase, VestDay = a.VestDay,
                        VestCliff = a.VestCliff, VestSpan = a.VestSpan, Debt = a.Debt / 100
                    };
                    r.Books = Quarters(reg != null ? reg.Address : null, save.Day, year, out r.RevDay, out r.ProfitDay);
                    Stock s = m.ListFirm(r, out FirmWhy why);
                    if (s == null)
                    {
                        return new StockInfo { Why = Why(why) };
                    }
                    return Info(s);
                }
                catch (Exception e)
                {
                    Fault("ExitStockLinks.Make", e);
                    return new StockInfo { Why = StockWhy.Off };
                }
            }

            private static bool End(int id, int reason, int cents)
            {
                try
                {
                    MarketDirector m = MarketDirector.Instance;
                    GoneReason why = reason == 0 ? GoneReason.Failed : reason == 1 ? GoneReason.TakenOver : GoneReason.Delisted;
                    return m != null && m.EndFirm(id, why, cents);
                }
                catch (Exception e)
                {
                    Fault("ExitStockLinks.End", e);
                    return false;
                }
            }

            private static bool Grow(int id, long shares)
            {
                try
                {
                    MarketDirector m = MarketDirector.Instance;
                    return m != null && m.GrowFirm(id, shares);
                }
                catch (Exception e)
                {
                    Fault("ExitStockLinks.Grow", e);
                    return false;
                }
            }

            private static long Cancel(int id, int day)
            {
                try
                {
                    MarketDirector m = MarketDirector.Instance;
                    return m != null ? m.CancelUnvested(id, day) : 0L;
                }
                catch (Exception e)
                {
                    Fault("ExitStockLinks.Cancel", e);
                    return 0L;
                }
            }

            private static int Sweep(IList<string> places)
            {
                try
                {
                    MarketDirector m = MarketDirector.Instance;
                    return m != null ? m.SweepFirms(places) : 0;
                }
                catch (Exception e)
                {
                    Fault("ExitStockLinks.Sweep", e);
                    return 0;
                }
            }

            private static void Show(int id)
            {
                try
                {
                    MarketDirector.Instance?.OpenCompany(id);
                }
                catch (Exception e)
                {
                    Fault("ExitStockLinks.Show", e);
                }
            }

            // The sector a startup does is the market's sector it competes in.
            private static MarketSector SectorOf(int venture)
            {
                switch ((Venture)venture)
                {
                    case Venture.Fintech: return MarketSector.Finance;
                    case Venture.Health: return MarketSector.Healthcare;
                    case Venture.Games: return MarketSector.Leisure;
                    case Venture.Marketplace: return MarketSector.Stores;
                    case Venture.Climate: return MarketSector.Power;
                    default: return MarketSector.Software;   // software, AI, security and developer tools
                }
            }

            private static StockWhy Startup(StartupAsk a)
            {
                try
                {
                    MarketDirector m = MarketDirector.Instance;
                    if (m == null)
                    {
                        return StockWhy.Off;
                    }
                    var r = new StartupListing
                    {
                        Name = a.Name, Sector = (int)SectorOf(a.Venture), RevenueM = (int)Math.Max(1L, Math.Min(100000L, a.ArrCents / 100000000L)), Shares = a.Shares, PriceCents = a.PriceCents
                    };
                    m.ListStartup(r, out FirmWhy why);
                    return Why(why);
                }
                catch (Exception e)
                {
                    Fault("ExitStockLinks.Startup", e);
                    return StockWhy.Off;
                }
            }

            // The player dealt in a firm's shares on the market: the tech sector follows its cap table.
            private static void Dealt(int stock, long shares, int cents, long money, long fee)
            {
                try
                {
                    TechDirector.Instance?.FirmStockDealt(stock, shares, cents, money, fee);
                }
                catch (Exception e)
                {
                    Fault("ExitStockLinks.Dealt", e);
                }
            }

            // ------------------------------------------------------------------------------------------------- the firm's days

            /// <summary>True while the player has a firm on the market on its own, so the market's morning asks for its figures.</summary>
            internal static bool Any(MarketDirector market)
            {
                return market != null && market.FirmsOpen && market.ListedFirms(Listed) > 0;
            }

            private static void Refresh()
            {
                Places.Clear();
                MarketDirector m = MarketDirector.Instance;
                if (m == null || !m.FirmsOpen)
                {
                    return;
                }
                m.ListedFirms(Listed);
                for (int i = 0; i < Listed.Count; i++)
                {
                    Places.Add(Listed[i].Own.Place);
                }
            }

            /// <summary>
            /// True for a business of the player's whose takings are a stock's on their own: they are kept out of the player's whole company's books, places and trades, so the
            /// same takings never feed two stocks and a take over of the company does not hand the firm over.
            /// </summary>
            internal static bool IsListed(BuildingRegistration reg)
            {
                if (reg == null)
                {
                    return false;
                }
                if (Places.Count == 0)
                {
                    return false;
                }
                return Places.Contains(MarketStore.Clean(TalentGame.Place(reg)));
            }

            /// <summary>The set of listed firms' places made again, for a look at the player's businesses (the books, the places, the trades).</summary>
            internal static void Remember()
            {
                Refresh();
            }

            /// <summary>
            /// The day's figures of every firm the player has on the market, from the game's statements at each firm's address over the days the market counts, into the env
            /// the tape keeps: the same on every machine of a session, the founder's alone being the one that writes them.
            /// </summary>
            internal static void Env(ref MarketEnv e, int from, int day)
            {
                try
                {
                    MarketDirector m = MarketDirector.Instance;
                    TechDirector tech = TechDirector.Instance;
                    GameInstance save = SaveGameManager.Current;
                    if (m == null || save == null || !m.FirmsOpen || m.ListedFirms(Listed) == 0)
                    {
                        return;
                    }
                    int n = Math.Min(Listed.Count, MarketEnv.FirmsMost);
                    e.FirmStock = new int[n];
                    e.FirmRev = new long[n];
                    e.FirmProfit = new long[n];
                    e.FirmDebt = new long[n];
                    e.FirmFrom = from;
                    e.FirmDays = Math.Max(0, day - from);
                    int found = 0;
                    List<BuildingRegistration> all = save.BuildingRegistrations;
                    for (int i = 0; i < n; i++)
                    {
                        Stock s = Listed[i];
                        BuildingRegistration reg = null;
                        for (int k = 0; all != null && k < all.Count && reg == null; k++)
                        {
                            reg = all[k] != null && GoingPublic.Mine(all[k]) && all[k].HasValidAddress && MarketStore.Clean(TalentGame.Place(all[k])) == s.Own.Place ? all[k] : null;
                        }
                        e.FirmStock[found] = s.Id;
                        if (reg != null && e.FirmDays > 0)
                        {
                            Statements(reg.Address, from, day, out long revenue, out long profit);
                            e.FirmRev[found] = revenue;
                            e.FirmProfit[found] = profit;
                        }
                        e.FirmDebt[found] = tech != null && reg != null ? tech.StockDebt(TalentGame.Place(reg)) : 0L;
                        found++;
                    }
                    e.Firms = found;
                }
                catch (Exception ex)
                {
                    Fault("ExitStockLinks.Env", ex);
                    e.Firms = 0;
                }
            }

            // A business's takings and profit over the statement days from one day up to, not with, another.
            private static void Statements(Address address, int from, int to, out long revenue, out long profit)
            {
                revenue = profit = 0;
                GameInstance save = SaveGameManager.Current;
                if (save == null || save.financialSummaries == null || address == null || to <= from)
                {
                    return;
                }
                string key = Books.Key(address);
                double rev = 0.0, gain = 0.0;
                List<FinancialSummary> all = save.financialSummaries;
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    FinancialSummary day = all[i];
                    if (day == null || day.businessIncomeStatements == null)
                    {
                        continue;
                    }
                    if (day.dayNumber < from - 7)
                    {
                        break;   // the statements are in order of days: none older is wanted
                    }
                    if (day.dayNumber < from || day.dayNumber >= to)
                    {
                        continue;
                    }
                    for (int k = 0; k < day.businessIncomeStatements.Count; k++)
                    {
                        FinancialSummary.BusinessIncomeStatement s = day.businessIncomeStatements[k];
                        if (s != null && s.Address != null && Books.Key(s.Address) == key)
                        {
                            rev += Math.Max(0f, s.TotalSales);
                            gain += s.TotalProfit;
                        }
                    }
                }
                revenue = (long)Math.Round(rev);
                profit = (long)Math.Round(gain);
            }

            // A firm's own quarters from the game's statements, the last eight and the one under way, oldest first; and its last month's takings and profit a day.
            private static BookQuarter[] Quarters(Address address, int today, int year, out double revDay, out double profitDay)
            {
                revDay = profitDay = 0.0;
                GameInstance save = SaveGameManager.Current;
                if (save == null || save.financialSummaries == null || address == null)
                {
                    return new BookQuarter[0];
                }
                int now = MarketCatalogue.QuarterOf(today, year), first = now - 8;
                Array.Clear(QuarterRev, 0, QuarterRev.Length);
                Array.Clear(QuarterProfit, 0, QuarterProfit.Length);
                Array.Clear(QuarterDays, 0, QuarterDays.Length);
                string key = Books.Key(address);
                double monthRev = 0.0, monthProfit = 0.0;
                int monthDays = 0, month = Math.Max(7, year / 2);
                List<FinancialSummary> all = save.financialSummaries;
                int least = MarketCatalogue.QuarterStart(first, year);
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    FinancialSummary day = all[i];
                    if (day == null || day.businessIncomeStatements == null)
                    {
                        continue;
                    }
                    if (day.dayNumber < least - 7)
                    {
                        break;
                    }
                    if (day.dayNumber < least || day.dayNumber >= today)
                    {
                        continue;
                    }
                    int q = MarketCatalogue.QuarterOf(day.dayNumber, year) - first;
                    for (int k = 0; k < day.businessIncomeStatements.Count && q >= 0 && q < QuarterRev.Length; k++)
                    {
                        FinancialSummary.BusinessIncomeStatement s = day.businessIncomeStatements[k];
                        if (s != null && s.Address != null && Books.Key(s.Address) == key)
                        {
                            QuarterRev[q] += (long)Math.Round(Math.Max(0f, s.TotalSales));
                            QuarterProfit[q] += (long)Math.Round(s.TotalProfit);
                            QuarterDays[q]++;
                            if (today - day.dayNumber <= month)
                            {
                                monthRev += Math.Max(0f, s.TotalSales);
                                monthProfit += s.TotalProfit;
                                monthDays++;
                            }
                            break;
                        }
                    }
                }
                var books = new List<BookQuarter>(10);
                for (int q = 0; q < QuarterRev.Length; q++)
                {
                    if (QuarterDays[q] > 0)
                    {
                        books.Add(new BookQuarter { Quarter = first + q, Revenue = QuarterRev[q], Profit = QuarterProfit[q], Days = QuarterDays[q] });
                    }
                }
                if (monthDays > 0)
                {
                    revDay = monthRev / monthDays;
                    profitDay = monthProfit / monthDays;
                }
                return books.ToArray();
            }
        }
    }
}
