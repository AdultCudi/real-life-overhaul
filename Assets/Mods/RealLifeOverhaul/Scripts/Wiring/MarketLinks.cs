using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Helpers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The city's stock market with the rest of the mod (Markets/MarketBridges). The market calls no other feature
        /// and none calls it: what they have to do with each other is here, and every answer is read from what a
        /// feature already keeps in public.
        ///
        ///   City     the city on a market day, read at its open (the tape keeps it for a replay):
        ///              the cycle, the bank rate and its next move, inflation   Economy/City (CityEconomy)
        ///              the price level                                         Core/Prices
        ///              the districts' income, the supply of each kind of goods the city's economy, as it stands
        ///              the council's policies, a campaign, elections counted   Civic (CivicPolicy, the government)
        ///              the sky, the spell, the street event                    Events/Weather, Events/EventCalendar
        ///              crimes, break-ins, fires and accidents reported         counted here from the paper's reports
        ///              yesterday                                               (Social/News.Reported) and the fires' bills
        ///              the player's own takings by trade                       Economy/Finance (Books): yesterday's
        ///   Events   what moves prices the moment it happens: a crime, a break-in, an accident (the paper's reports), a
        ///            fire at a business (Events/FireLosses), a policy moved (Civic), an election counted, a storm
        ///            breaking (polled once a second by the director)
        ///   Rivals   the game's named rivals (Ventures' reader of them)
        ///   Old      the old market's file (Ventures'): the live one while Ventures runs, else the save's copy
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
                News.Reported += Reported;
                FireLosses.Billed += Fire;
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

            // ---------------------------------------------------------------- the city on a market day

            private static MarketEnv City(int day)
            {
                MarketEnv e = MarketEnv.Quiet();
                e.PriceLevel = Prices.Level;
                try
                {
                    e.Cycle = (int)CityEconomy.State;
                    e.RateBp = 300 + (int)Math.Round(CityEconomy.BasePoints * 100f);
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

            // ---------------------------------------------------------------- what happens during the day

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

            // ---------------------------------------------------------------- the rivals and the old market

            private static void Rivals(List<string> names, List<string> ids)
            {
                names.Clear();
                ids.Clear();
                try
                {
                    VenturesGame.RivalNames(names);
                    for (int i = 0; i < names.Count; i++)
                    {
                        ids.Add(names[i]);   // (the game names its rivals uniquely: the name is the id)
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
                    if (Settings.VenturesInvestors && ventures != null && _oldAsked++ < OldWaitSeconds)
                    {
                        return null;   // Ventures is opening its file: ask again in a second
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
        }
    }
}
