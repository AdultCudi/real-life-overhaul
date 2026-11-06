using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The city's stock market (MarketBridges) with the city on a market day, read at its open (the economy, the
        /// council, the weather and the street, yesterday's crimes, fires and accidents, the player's takings by trade;
        /// the tape keeps it for a replay), what moves prices as it happens, the game's rivals, the old Ventures market,
        /// the banks for the bonds' deposits, the raw materials exchange and the Planner.
        /// </summary>
        public static class MarketLinks
        {
            private const int OldWaitSeconds = 15;   // how long the market waits for Ventures to open its file before it takes the save's copy

            private static readonly List<BuildingRegistration> Businesses = new List<BuildingRegistration>(16);
            private static int _day, _crimes, _breakIns, _fires, _accidents;               // today's reports so far
            private static int _crimesY, _breakInsY, _firesY, _accidentsY;                 // yesterday's
            private static int _leversSeen = -1, _electionsSeen = -1;
            private static bool _storming;
            private static int _oldAsked;

            internal static void Connect()
            {
                MarketBridges.City = City;
                MarketBridges.Rivals = Rivals;
                MarketBridges.Old = Old;
                MarketBridges.Poll = Poll;
                MarketBridges.Banks = Banks;
                MarketBridges.NetWorth = Books.NetWorth;
                MarketBridges.OwnWholesale = CityGame.OwnPrice;
                MarketBridges.CityPrices = CityPrices;
                MarketBridges.MaterialsFixed = MaterialsFixed;
                MarketBridges.BuilderBooks = MarketsBuildLinks.BuilderTakings;
                News.Reported += Reported;
                FireLosses.Billed += Fire;
                PlannerDirector.Instance?.AddSource(Source);
                _leversSeen = -1;
                _electionsSeen = -1;
                _storming = false;
                _oldAsked = 0;
                _day = 0;
            }

            internal static void Disconnect()
            {
                MarketBridges.Reset();
                News.Reported -= Reported;
                FireLosses.Billed -= Fire;
            }

            /// <summary>The bank rate in basis points, as the market and the City Ledger read it.</summary>
            internal static int RateBp() => 300 + (int)Math.Round(CityEconomy.BasePoints * 100f);

            private static bool CityPrices()
            {
                try
                {
                    CityEconomyDirector city = CityEconomyDirector.Instance;
                    return city != null && city.Running;
                }
                catch (Exception e)
                {
                    Fault("MarketLinks.CityPrices", e);
                    return false;
                }
            }

            private static void MaterialsFixed()
            {
                try
                {
                    CityEconomyDirector.RewriteWholesale();
                }
                catch (Exception e)
                {
                    Fault("MarketLinks.MaterialsFixed", e);
                }
            }

            private static MarketEnv City(int day)
            {
                MarketEnv e = MarketEnv.Quiet();
                e.PriceLevel = Prices.Level;
                try
                {
                    e.Cycle = (int)CityEconomy.State;
                    e.RateBp = RateBp();
                    e.InflationBp = (int)Math.Round(CityEconomy.InflationPercent * 100f);
                    CityState live = CityEconomy.Live;
                    if (live != null)
                    {
                        e.RateNextBp = live.Pending ? live.PendingMove * 100 / MacroCatalogue.QuartersPerPoint : 0;
                        long income = 0;
                        for (int i = 0; i < live.Districts.Count; i++)
                        {
                            income += live.Districts[i].Income;
                        }
                        e.DistrictIncome = live.Districts.Count > 0 ? (int)(income / live.Districts.Count) : 1000;
                        for (int k = 0; k < MarketEnv.SupplyKinds && k < live.Supply.Length; k++)
                        {
                            e.Supply[k] = live.Supply[k].Factor > 0 ? live.Supply[k].Factor : 1000;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Fault("MarketLinks.City (the city's economy)", ex);
                }
                try
                {
                    e.Levers = Levers();
                    CivicDirector civic = CivicDirector.Instance;
                    if (civic != null && civic.Running && civic.Government != null)
                    {
                        e.Campaign = civic.Government.Campaigning ? 1 : 0;
                        e.Elections = civic.Government.State.Number;
                    }
                }
                catch (Exception ex)
                {
                    Fault("MarketLinks.City (the council)", ex);
                }
                try
                {
                    e.Sky = Weather.Sky == Sky.Storm ? 2 : Weather.Sky == Sky.Rain || RainHelper.isRaining ? 1 : 0;
                    e.Spell = (int)Weather.SpellOf(day);
                    e.Event = (int)EventCalendar.For(day).Kind;
                }
                catch (Exception ex)
                {
                    Fault("MarketLinks.City (the weather and the street)", ex);
                }
                Count(day);
                e.Crimes = _crimesY;
                e.BreakIns = _breakInsY;
                e.Fires = _firesY;
                e.Accidents = _accidentsY;
                try
                {
                    Trades(ref e);
                }
                catch (Exception ex)
                {
                    Fault("MarketLinks.City (the player's businesses)", ex);
                }
                ListingLinks.Env(ref e, day);
                return e;
            }

            // The council's policies packed as the market keeps them: one base-3 digit each.
            private static int Levers()
            {
                var positions = new int[MarketEnv.LeverCount];
                for (int i = 0; i < MarketEnv.LeverCount && i < CivicCatalogue.Levers; i++)
                {
                    positions[i] = CivicPolicy.Position((Lever)i);
                }
                return MarketEnv.Packed(positions);
            }

            // Yesterday's takings of the player's businesses, by the trade a listed company shares with them.
            private static void Trades(ref MarketEnv e)
            {
                Books.Businesses(Businesses);
                e.Trades = 0;
                for (int i = 0; i < Businesses.Count; i++)
                {
                    BuildingRegistration r = Businesses[i];
                    int trade = MarketInputs.TradeOf(r != null ? r.businessTypeName : null);
                    if (trade < 0)
                    {
                        continue;
                    }
                    int sales = (int)Math.Min(int.MaxValue, Math.Max(0f, Books.SalesYesterday(r.Address)));
                    int at = Array.IndexOf(e.TradeOf, trade, 0, e.Trades);
                    if (at >= 0)
                    {
                        e.TradeSales[at] = (int)Math.Min(int.MaxValue, (long)e.TradeSales[at] + sales);
                    }
                    else if (e.Trades < MarketEnv.TradesMost)
                    {
                        e.TradeOf[e.Trades] = trade;
                        e.TradeSales[e.Trades] = sales;
                        e.Trades++;
                    }
                }
                Businesses.Clear();
            }

            // The day's reports become yesterday's when a new day is counted.
            private static void Count(int day)
            {
                if (day == _day)
                {
                    return;
                }
                if (day == _day + 1)
                {
                    _crimesY = _crimes;
                    _breakInsY = _breakIns;
                    _firesY = _fires;
                    _accidentsY = _accidents;
                }
                else
                {
                    _crimesY = _breakInsY = _firesY = _accidentsY = 0;
                }
                _crimes = _breakIns = _fires = _accidents = 0;
                _day = day;
            }

            private static void Reported(NewsKind kind, string detail, int amount)
            {
                try
                {
                    GameInstance save = SaveGameManager.Current;
                    if (save == null)
                    {
                        return;
                    }
                    Count(save.Day);
                    int what;
                    switch (kind)
                    {
                        case NewsKind.Crime: what = MarketInputs.EventCrime; _crimes++; break;
                        case NewsKind.BreakIn:
                        case NewsKind.CarBreakIn: what = MarketInputs.EventBreakIn; _breakIns++; break;
                        case NewsKind.Accident:
                        case NewsKind.PedestrianHit:
                        case NewsKind.HitAndRun: what = MarketInputs.EventAccident; _accidents++; break;
                        default: return;
                    }
                    MarketDirector.Instance?.Engine?.Event(what, 0, 0);
                }
                catch (Exception e)
                {
                    Fault("MarketLinks.Reported", e);
                }
            }

            private static void Fire(BuildingRegistration business, int amount)
            {
                try
                {
                    GameInstance save = SaveGameManager.Current;
                    if (save == null)
                    {
                        return;
                    }
                    Count(save.Day);
                    _fires++;
                    MarketDirector.Instance?.Engine?.Event(MarketInputs.EventFire, MarketInputs.TradeOf(business != null ? business.businessTypeName : null), 0);
                }
                catch (Exception e)
                {
                    Fault("MarketLinks.Fire", e);
                }
            }

            // Once a second, from the director: a policy moved, an election counted, a storm breaking.
            private static void Poll(Action<int, int, int> say)
            {
                try
                {
                    int levers = Levers();
                    if (_leversSeen >= 0 && levers != _leversSeen)
                    {
                        for (int i = 0, was = _leversSeen, now = levers; i < MarketEnv.LeverCount; i++, was /= 3, now /= 3)
                        {
                            int moved = now % 3 - was % 3;
                            if (moved != 0)
                            {
                                say(MarketInputs.EventPolicy, i, moved);
                            }
                        }
                    }
                    _leversSeen = levers;
                    CivicDirector civic = CivicDirector.Instance;
                    int elections = civic != null && civic.Running && civic.Government != null ? civic.Government.State.Number : -1;
                    if (_electionsSeen >= 0 && elections > _electionsSeen)
                    {
                        say(MarketInputs.EventElection, elections, 0);
                    }
                    _electionsSeen = elections;
                    bool storming = Weather.Sky == Sky.Storm;
                    if (storming && !_storming)
                    {
                        say(MarketInputs.EventStorm, 0, 0);
                    }
                    _storming = storming;
                }
                catch (Exception e)
                {
                    Fault("MarketLinks.Poll", e);
                }
            }

            // The city's three largest banks for the bonds' deposits, the largest first, as the bank apps rank them.
            private static void Banks(List<string> names)
            {
                names.Clear();
                try
                {
                    IReadOnlyList<Lender> banks = BankTerms.Banks;
                    for (int i = 0; banks != null && i < banks.Count && i < BondCatalogue.BanksMost; i++)
                    {
                        names.Add(banks[i] != null ? banks[i].Name : null);
                    }
                }
                catch (Exception e)
                {
                    Fault("MarketLinks.Banks", e);
                    names.Clear();
                }
            }

            private static void Rivals(List<string> names, List<string> ids)
            {
                names.Clear();
                ids.Clear();
                try
                {
                    VenturesGame.RivalNames(names);
                    for (int i = 0; i < names.Count; i++)
                    {
                        ids.Add(names[i]);   // the game names its rivals uniquely: the name is the id
                    }
                }
                catch (Exception e)
                {
                    Fault("MarketLinks.Rivals", e);
                    names.Clear();
                    ids.Clear();
                }
            }

            private static OldMarket Old()
            {
                try
                {
                    GameInstance save = SaveGameManager.Current;
                    if (save == null)
                    {
                        return null;
                    }
                    VenturesDirector ventures = VenturesDirector.Instance;
                    if (ventures != null && ventures.File != null)
                    {
                        return new OldMarket { File = ventures.File };   // the live file: Ventures writes it into the save
                    }
                    if (ventures != null && ventures.Opening && _oldAsked++ < OldWaitSeconds)
                    {
                        return null;   // Ventures may be opening its file for deals that carry on: ask again in a second
                    }
                    VenturesFile file = VenturesStore.Load(save.modData, out int _);
                    if (file == null)
                    {
                        return new OldMarket();
                    }
                    return new OldMarket { File = file, Done = () => VenturesStore.Save(save.modData, file) };
                }
                catch (Exception e)
                {
                    Fault("MarketLinks.Old", e);
                    return new OldMarket();   // nothing is brought over, and nothing of the old file is touched
                }
            }

            private static readonly MarketSource Source = new MarketSource();

            /// <summary>
            /// The stock market's rows in the Planner, read only. Ids: 1 results, 2 a dividend (both with the company's id),
            /// 3 the margin call, 4 an order (its id), 5 a bond (its number).
            /// </summary>
            private sealed class MarketSource : IPlannerSource
            {
                public string Id => "markets";

                public bool Available => Settings.Markets && MarketDirector.Instance != null && MarketDirector.Instance.State != null && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    MarketState s = MarketDirector.Instance.State;
                    for (int i = 0; i < s.Stocks.Count; i++)
                    {
                        Stock k = s.Stocks[i];
                        if (!k.Listed || k.ResultsDay <= 0 || (s.Position(k.Id) == null && !s.Watching(k.Id)))
                        {
                            continue;
                        }
                        PlannerEntry e = into.Add(Id, 1, k.Id, PlannerCategory.Money, k.ResultsDay);
                        if (e == null)
                        {
                            continue;
                        }
                        e.Start = MarketCatalogue.ResultsMinute;
                        if (into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_mkt_plan_results_title", "name", k.Name);
                            e.What = PlannerText.T("reallife_mkt_plan_results_what", "name", k.Name);
                        }
                    }
                    for (int i = 0; i < s.Dues.Count; i++)
                    {
                        DividendDue due = s.Dues[i];
                        Stock k = s.Stock(due.StockId);
                        PlannerEntry e = into.Add(Id, 2, due.StockId, PlannerCategory.Money, due.PayDay);
                        if (e == null || !into.WantText)
                        {
                            continue;
                        }
                        long money = due.Cents >= 0 ? due.Cents / 100 : (-due.Cents + 99) / 100;
                        e.Title = PlannerText.T("reallife_mkt_plan_dividend_title", "name", k != null ? k.Name : string.Empty);
                        e.What = PlannerText.T(due.Cents >= 0 ? "reallife_mkt_plan_dividend_what" : "reallife_mkt_plan_dividend_owed");
                        if (due.Cents >= 0)
                        {
                            e.Money = PlannerText.T("reallife_planner_pays", "amount", MarketText.Money(money));
                        }
                        else
                        {
                            PlannerText.Costs(e, money, MarketText.Money(money));
                        }
                    }
                    for (int i = 0; i < s.Bonds.Holdings.Count; i++)
                    {
                        BondHolding h = s.Bonds.Holdings[i];
                        BondDef d = BondCatalogue.Find(h.Def);
                        int day = BondRules.NextPayDay(h);
                        PlannerEntry e = d != null && day > 0 ? into.Add(Id, 5, h.Id, PlannerCategory.Money, day) : null;
                        if (e == null || !into.WantText)
                        {
                            continue;
                        }
                        bool end = h.Paid + 1 >= h.Payments, rolls = end && InvestRules.Rolls(h, Settings.MarketsBonds);   // rolling over: what it repays goes into a new one
                        long cents = BondRules.InterestCents(h, d) + (end && !rolls ? h.Face : 0);
                        e.Title = PlannerText.T(rolls ? "reallife_mkt_plan_bond_roll_title" : end ? "reallife_mkt_plan_bond_end_title" : "reallife_mkt_plan_bond_title", "name", BondText.Name(h));
                        e.What = PlannerText.T(rolls ? "reallife_mkt_plan_bond_roll_what" : end ? "reallife_mkt_plan_bond_end_what" : "reallife_mkt_plan_bond_what");
                        e.Money = PlannerText.T("reallife_planner_pays", "amount", BondText.Money(cents));
                    }
                    Account a = s.Account;
                    if (a.Margin && a.CallDay > 0)
                    {
                        PlannerEntry e = into.Add(Id, 3, 0, PlannerCategory.Money, a.CallDay);
                        if (e != null)
                        {
                            e.Deadline = true;
                            e.End = MarketCatalogue.CloseMinute;
                            if (into.WantText)
                            {
                                e.Title = PlannerText.T("reallife_mkt_plan_call_title");
                                e.What = PlannerText.T("reallife_mkt_plan_call_what", "amount", MarketText.Money(a.CallAmount));
                                PlannerText.Costs(e, a.CallAmount, MarketText.Money(a.CallAmount));
                                e.IfMissed = PlannerText.T("reallife_mkt_plan_call_missed");
                            }
                        }
                    }
                    for (int i = 0; i < s.Orders.Count; i++)
                    {
                        Markets.Order o = s.Orders[i];
                        if (!o.IsOpen || o.Life != OrderLife.UntilCancelled || o.Forced)
                        {
                            continue;
                        }
                        PlannerEntry e = into.Add(Id, 4, o.Id, PlannerCategory.Money, o.ExpireDay);
                        if (e == null)
                        {
                            continue;
                        }
                        e.Deadline = true;
                        e.End = MarketCatalogue.CloseMinute;
                        if (into.WantText)
                        {
                            Stock k = s.Stock(o.StockId);
                            e.Title = PlannerText.T("reallife_mkt_plan_order_title", "name", k != null ? k.Name : string.Empty);
                            e.What = PlannerText.T(o.Side == OrderSide.Buy ? "reallife_mkt_plan_order_buy" : "reallife_mkt_plan_order_sell", "count", o.Left.ToString("N0"));
                        }
                    }
                }
            }
        }
    }
}
