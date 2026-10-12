using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Markets;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The City Ledger with the rest of the mod (Ledger/LedgerBridges). The paper calls no other feature and none calls
        /// it: what it prints is read here from what each feature keeps in public, once a morning.
        ///
        ///   Paper     the city's paper as Social/News writes it every morning: the street, the courts, City Hall's stories,
        ///             the weather, the player's own shops, people, and what the city's economy and the company add to it
        ///   Market    the market's last close (Markets): the city index and its last weeks, every sector, every company's
        ///             close and move, and the market's news since the last edition (results, dividends, listings, bids,
        ///             failures, crises, a crash), in the market's own words (MarketText)
        ///   Economy   the bank rate and inflation as the Stocks app shows them, and the state of the cycle (Economy/City)
        ///   Extra     what is on in the street today (Events/EventCalendar) and a spell of weather (Events/Weather)
        ///   Council   the council's policies (Civic) against how they stood at the last edition: one piece for each that
        ///             came in or ended
        ///   Taken     the bus stops, the help-wanted boards and the bike docks, which a kiosk keeps clear of
        ///   Say       a message from the City Ledger's contact (Social/SocialPhone), whose call is the paper's desk
        /// </summary>
        public static class LedgerLinks
        {
            private static readonly StringBuilder Positions = new StringBuilder(32);

            internal static void Connect()
            {
                LedgerBridges.PaperDay = () => News.PaperDay;
                LedgerBridges.Paper = Paper;
                LedgerBridges.Market = Market;
                LedgerBridges.Economy = EconomyFigures;
                LedgerBridges.Extra = Extra;
                LedgerBridges.Council = Council;
                LedgerBridges.Taken = Taken;
                LedgerBridges.Say = SocialPhone.LedgerMessage;
                SocialPhone.LedgerDesk = LedgerDesk.First;
            }

            internal static void Disconnect()
            {
                LedgerBridges.Reset();
                SocialPhone.LedgerDesk = null;
                LedgerCta.Remove();
            }

            // ---------------------------------------------------------------- the city's paper

            private static void Paper(List<LedgerPiece> into)
            {
                try
                {
                    IReadOnlyList<NewsItem> today = News.Today;
                    for (int i = 0; i < today.Count; i++)
                    {
                        NewsItem item = today[i];
                        LedgerPiece piece = LedgerRules.Piece(item.Key, Pairs(item.Data));
                        if (piece != null)
                        {
                            into.Add(piece);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("LedgerLinks.Paper", e);
                }
            }

            private static string[] Pairs(Dictionary<string, string> data)
            {
                if (data == null || data.Count == 0)
                {
                    return LedgerPiece.NoData;
                }
                var pairs = new string[data.Count * 2];
                int at = 0;
                foreach (KeyValuePair<string, string> pair in data)
                {
                    pairs[at++] = pair.Key;
                    pairs[at++] = pair.Value ?? string.Empty;
                }
                return pairs;
            }

            // ---------------------------------------------------------------- the market's last close

            private static void Market(LedgerEdition e, int since)
            {
                try
                {
                    MarketDirector market = MarketDirector.Instance;
                    MarketState s = market != null ? market.State : null;
                    if (s == null || s.IndexDaily.Count == 0)
                    {
                        return;
                    }
                    Candle last = s.IndexDaily[s.IndexDaily.Count - 1];
                    e.HasMarket = true;
                    e.MarketDay = last.Day;
                    e.IndexLevel = last.C;
                    e.IndexChangeBp = Move(last.C, s.IndexDaily.Count >= 2 ? s.IndexDaily[s.IndexDaily.Count - 2].C : last.C);
                    e.IndexWeekBp = Move(last.C, s.IndexDaily.Count >= 6 ? s.IndexDaily[s.IndexDaily.Count - 6].C : s.IndexDaily[0].C);
                    for (int i = Math.Max(0, s.IndexDaily.Count - LedgerCatalogue.ChartDays); i < s.IndexDaily.Count; i++)
                    {
                        e.IndexCloses.Add(s.IndexDaily[i].C);
                    }
                    for (int sec = 0; sec < (int)MarketSector.Count; sec++)
                    {
                        List<int> closes = s.SectorDaily[sec];
                        int close = closes.Count > 0 ? closes[closes.Count - 1] : s.SectorLast[sec];
                        e.Sectors.Add(new LedgerSector
                        {
                            Key = MarketCatalogue.SectorKeys[sec], Level = close, ChangeBp = Move(close, closes.Count >= 2 ? closes[closes.Count - 2] : close)
                        });
                    }
                    for (int i = 0; i < s.Stocks.Count; i++)
                    {
                        Stock k = s.Stocks[i];
                        if (!k.Listed || k.Daily.Count == 0)
                        {
                            continue;
                        }
                        Candle c = k.Daily[k.Daily.Count - 1];
                        int flags = (k.BidCents > 0 && k.BidEndDay >= e.Day ? LedgerQuote.Bid : 0) | (k.DeclaredCents > 0 && k.RecordDay == c.Day ? LedgerQuote.ExDividend : 0)
                                    | (k.ListedDay >= c.Day - 5 ? LedgerQuote.New : 0) | (k.SplitDay == e.Day && k.SplitRatio > 1 ? LedgerQuote.Split : 0);
                        e.Quotes.Add(new LedgerQuote
                        {
                            Name = k.Name, Ticker = k.Ticker, Sector = (int)k.Sector, Close = c.C, ChangeBp = Move(c.C, k.Daily.Count >= 2 ? k.Daily[k.Daily.Count - 2].C : c.O),
                            Volume = c.V, Flags = flags
                        });
                    }
                    for (int i = 0; i < s.News.Count; i++)
                    {
                        MarketNews n = s.News[i];
                        if (n.Day < since || n.Day >= e.Day)
                        {
                            continue;
                        }
                        string key = MarketText.NewsKey(n, s, out string[] data);
                        if (key.Length == 0)
                        {
                            continue;
                        }
                        LedgerPiece piece = LedgerRules.Piece(key, data, Heavier(n));
                        if (piece != null)
                        {
                            e.Pieces.Add(piece);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Fault("LedgerLinks.Market", ex);
                }
            }

            // What makes one piece of the market's news weigh more than another of its kind: the size of the move.
            private static int Heavier(in MarketNews n)
            {
                switch (n.Kind)
                {
                    case MarketNewsKind.Results: return Math.Min(30, Math.Abs(n.C) / 40);
                    case MarketNewsKind.Mover: return Math.Min(25, Math.Abs(n.A) / 40);
                    case MarketNewsKind.Crisis:
                    case MarketNewsKind.Windfall: return Math.Min(20, Math.Abs(n.A) / 50);
                    case MarketNewsKind.Crash: return Math.Min(8, Math.Abs(n.A) / 100);
                    default: return 0;
                }
            }

            private static int Move(int now, int before)
            {
                return before > 0 ? (int)Math.Round((now - before) * 10000.0 / before) : 0;
            }

            // ---------------------------------------------------------------- the economy

            private static void EconomyFigures(LedgerEdition e)
            {
                try
                {
                    if (!CityEconomy.Running)
                    {
                        return;
                    }
                    e.RateBp = 300 + (int)Math.Round(CityEconomy.BasePoints * 100f);   // as the market reads it (MarketLinks.City), so the paper and the Stocks app agree
                    e.InflationBp = (int)Math.Round(CityEconomy.InflationPercent * 100f);
                    e.CycleKey = CityText.StateKeys[MacroCatalogue.Clamp((int)CityEconomy.State, 0, CityText.StateKeys.Length - 1)];
                }
                catch (Exception ex)
                {
                    Fault("LedgerLinks.Economy", ex);
                }
            }

            // ---------------------------------------------------------------- what is on, and the weather's spell

            private static readonly string SpellPrefix = "reallife" + "_ledger_spell_";

            private static void Extra(int day, List<LedgerPiece> into)
            {
                try
                {
                    if (Settings.StreetEvents)
                    {
                        PlannedEvent plan = EventCalendar.For(day);
                        if (plan.Kind != EventKind.None)
                        {
                            LedgerPiece piece = LedgerRules.Piece(EventCalendar.Today(plan.Kind), new[] { "from", plan.From.GetFormattedTime(0f, true), "to", plan.To.GetFormattedTime(0f, true) });
                            if (piece != null)
                            {
                                into.Add(piece);
                            }
                        }
                    }
                    if (Settings.WeatherMatters)
                    {
                        Events.Spell spell = Weather.SpellOf(day);
                        string name = spell == Events.Spell.HeatWave ? "heat" : spell == Events.Spell.ColdSnap ? "cold" : spell == Events.Spell.Ice ? "ice" : null;
                        if (name != null)
                        {
                            LedgerPiece piece = LedgerRules.Piece(SpellPrefix + name, null);
                            if (piece != null)
                            {
                                into.Add(piece);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Fault("LedgerLinks.Extra", ex);
                }
            }

            // ---------------------------------------------------------------- the council

            private static readonly string PolicyOn = "reallife" + "_ledger_policy_on", PolicyOff = "reallife" + "_ledger_policy_off";

            // The council's levers written as "-1,0,1,...": one piece for each that stands elsewhere than at the last edition.
            private static string Council(string before, List<LedgerPiece> into)
            {
                try
                {
                    if (!Settings.Civic)
                    {
                        return before;
                    }
                    Positions.Length = 0;
                    for (int i = 0; i < CivicCatalogue.Levers; i++)
                    {
                        Positions.Append(i > 0 ? "," : string.Empty).Append(CivicPolicy.Position((Lever)i).ToString(CultureInfo.InvariantCulture));
                    }
                    string now = Positions.ToString();
                    if (before == null || before == now)
                    {
                        return now;
                    }
                    string[] was = before.Split(',');
                    for (int i = 0; i < CivicCatalogue.Levers && i < was.Length; i++)
                    {
                        int old = int.TryParse(was[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int o) ? o : 0;
                        int position = CivicPolicy.Position((Lever)i);
                        if (old == position)
                        {
                            continue;
                        }
                        PolicyDef policy = CivicCatalogue.Find((Lever)i, position);
                        PolicyDef ended = CivicCatalogue.Find((Lever)i, old);
                        LedgerPiece piece = policy != null
                            ? LedgerRules.Piece(PolicyOn, new[] { "policy", CivicText.T(policy.Name), "effect", CivicText.Effect(policy) })
                            : ended != null ? LedgerRules.Piece(PolicyOff, new[] { "policy", CivicText.T(ended.Name) }) : null;
                        if (piece != null)
                        {
                            into.Add(piece);
                        }
                    }
                    return now;
                }
                catch (Exception ex)
                {
                    Fault("LedgerLinks.Council", ex);
                    return before;
                }
            }

            // ---------------------------------------------------------------- the street's other fixtures

            private static bool Taken(Vector3 at, float within)
            {
                float sqr = within * within;
                try
                {
                    List<TransitStop> stops = TransitMap.Stops;
                    for (int i = 0; i < stops.Count; i++)
                    {
                        if (stops[i] != null && CitySpots.Flat(stops[i].Sign - at) < sqr)
                        {
                            return true;
                        }
                    }
                    JobDirector jobs = JobDirector.Instance;
                    JobBoards boards = jobs != null ? jobs.HelpBoards : null;
                    for (int i = 0; boards != null && i < boards.Count; i++)
                    {
                        if (CitySpots.Flat(boards.At(i) - at) < sqr)
                        {
                            return true;
                        }
                    }
                    GarageDirector garage = GarageDirector.Instance;
                    BikePlaces bikes = garage != null && garage.Bikes != null ? garage.Bikes.Places : null;
                    IReadOnlyList<BikePlace> all = bikes != null ? bikes.All : null;
                    for (int i = 0; all != null && i < all.Count; i++)
                    {
                        if (all[i] != null && CitySpots.Flat(all[i].Position - at) < sqr)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Fault("LedgerLinks.Taken", ex);
                }
                return false;
            }
        }
    }
}
