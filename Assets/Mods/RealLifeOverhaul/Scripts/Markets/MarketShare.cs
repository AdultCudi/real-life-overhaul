using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The stock market in a Going Public session (Core/SharedCity, part M): the host's market for the city, every player's
    /// holdings and account their own. Game-free. No price is sent: a market is exact, so a guest is given the host's market
    /// once, whole, and then only the host's new tape lines (each day's secret and city, events, the host's furthest moment
    /// and every player's trades), and plays the very moments the host played. Each set of lines ends with a checksum of the
    /// host's prices (<see cref="Checksum"/>); a guest that comes out different asks for the whole market again.
    ///     the snapshot   the city's line of the market, a new line, the tape's lines from its day, a K line
    ///     an update      the tape's new lines, a K line
    ///     K day phase n  the checksum of the market at that moment
    /// </summary>
    public static class MarketShare
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(MarketState s, MarketTape tape)
        {
            var t = new StringBuilder(128 * 1024);
            t.Append(MarketStore.Write(s, true)).Append('\n');
            t.Append(tape != null ? tape.From(s.Day) : "F " + s.Day.ToString(Inv) + " " + s.Phase.ToString(Inv) + "\n");
            Check(t, s);
            return t.ToString();
        }

        /// <summary>The host's new tape lines and a check of where the market stands; null if nothing moved.</summary>
        public static string Changes(string lines, MarketState s, ref long checkedAt)
        {
            if (string.IsNullOrEmpty(lines) && s.Now == checkedAt)
            {
                return null;
            }
            checkedAt = s.Now;
            var t = new StringBuilder((lines?.Length ?? 0) + 40);
            t.Append(lines);
            Check(t, s);
            return t.ToString();
        }

        private static void Check(StringBuilder t, MarketState s)
        {
            t.Append("K ").Append(s.Day.ToString(Inv)).Append(' ').Append(s.Phase.ToString(Inv)).Append(' ').Append(Checksum(s).ToString(Inv)).Append('\n');
        }

        /// <summary>Every listed company's last price and volume, and the index, at the market's moment.</summary>
        public static long Checksum(MarketState s)
        {
            unchecked
            {
                long sum = s.IndexLast;
                for (int i = 0; i < s.Stocks.Count; i++)
                {
                    Stock k = s.Stocks[i];
                    if (k.Listed)
                    {
                        sum += (long)k.Last * (2L * k.Id + 1L) + k.DayVolume;
                    }
                }
                return sum;
            }
        }

        /// <summary>
        /// The host's market for a guest with the guest's own holdings, account and bots, and the host's tape on
        /// <paramref name="into"/> if it is a copy of the host's, else a new one. Null, nothing changed, for a text that cannot
        /// be read.
        /// </summary>
        public static MarketState Adopt(MarketState mine, string text, List<long> checks, MarketTape into, out MarketTape tape, out long frontier)
        {
            tape = null;
            frontier = 0;
            int cut = text != null ? text.IndexOf('\n') : -1;
            if (mine == null || cut <= 0)
            {
                return null;
            }
            MarketState host;
            try
            {
                host = MarketStore.Read(text.Substring(0, cut), out _);
            }
            catch (Exception)
            {
                return null;
            }
            if (host == null || host.Stocks.Count == 0)
            {
                return null;
            }
            var lines = new MarketTape(host.Id);
            if (!Follow(lines, text.Substring(cut + 1), checks, ref frontier))
            {
                return null;   // read on a tape of its own first: a damaged text leaves the guest's tape as it was
            }
            MarketTape follow = lines;
            if (into != null && into.Id == host.Id)
            {
                follow = into;
                follow.Forget(host.Now);
                Follow(follow, text.Substring(cut + 1), null, ref frontier);
            }
            host.Me = mine.Me;
            host.Listing = mine.Listing ?? new ListingFile();   // the player's own way to the market and books, never the host's
            // The player's own listed company is not in the host's city: set aside whole until they play alone again.
            for (int i = 0; i < mine.Stocks.Count; i++)
            {
                Stock own = mine.Stocks[i];
                if (own.Listed && own.Own != null && mine.Me != 0UL && own.Own.Owner == mine.Me)
                {
                    host.Listing.Suspended = MarketStore.StockLine(mine, own);
                    host.Listing.SetAsideDay = Math.Max(1, mine.BusinessDay);
                }
            }
            host.NextOrderId = Math.Max(1, mine.NextOrderId);
            host.CarriedOver = mine.CarriedOver;
            host.HeldDay = host.HeldPhase = 0;   // what the guest has seen is what the host has played
            host.Positions.AddRange(mine.Positions);
            host.Orders.AddRange(mine.Orders);
            host.Trades.AddRange(mine.Trades);
            host.Watch.AddRange(mine.Watch);
            host.Dues.AddRange(mine.Dues);
            host.Bots.AddRange(mine.Bots);
            host.Bonds.Take(mine.Bonds);
            host.Derivs.Take(mine.Derivs);   // the player's own futures and options, on the host's exchange
            if (host.Materials.Count == 0)
            {
                host.Materials.AddRange(mine.Materials);   // a host without the exchange: the guest's own goes on with the host's days
            }
            Account a = host.Account, m = mine.Account;
            a.Margin = m.Margin;
            a.Cash = m.Cash;
            a.CallDay = m.CallDay;
            a.CallAmount = m.CallAmount;
            a.Commission = m.Commission;
            a.Interest = m.Interest;
            a.BorrowFees = m.BorrowFees;
            a.Dividends = m.Dividends;
            a.DividendsPaid = m.DividendsPaid;
            a.Realised = m.Realised;
            a.Payouts = m.Payouts;
            a.OpenedDay = m.OpenedDay;
            for (int i = 0; i < mine.Stocks.Count; i++)
            {
                Stock own = mine.Stocks[i];
                if (own.Own != null && own.Own.Owner == mine.Me && mine.Me != 0UL)
                {
                    continue;   // set aside above
                }
                Stock city = host.Stock(own.Id);
                if (city != null)
                {
                    city.Impact = own.Impact;   // the player's own recent trading
                    continue;
                }
                if (mine.Held(own.Id) != 0 || MarketBots.Held(mine, own.Id) > 0)
                {
                    own.Status = StockStatus.Gone;
                    own.Reason = GoneReason.Delisted;
                    own.GoneDay = host.Day;
                    own.GoneCents = Math.Max(1, own.Last);
                    host.Stocks.Add(own);
                }
            }
            tape = follow;
            return host;
        }

        /// <summary>
        /// The host's tape lines put on a guest's tape, replacing any day it had; the furthest moment into
        /// <paramref name="frontier"/>, which only grows, and the checks into the list. False for lines that cannot be read.
        /// </summary>
        public static bool Follow(MarketTape tape, string lines, List<long> checks, ref long frontier)
        {
            if (tape == null || lines == null)
            {
                return false;
            }
            string[] all = lines.Split('\n');
            for (int i = 0; i < all.Length; i++)
            {
                string line = all[i];
                if (line.Length < 3)
                {
                    continue;
                }
                string[] f = line.Split(' ');
                switch (f[0])
                {
                    case "D":
                        if (f.Length < 3 || !int.TryParse(f[1], NumberStyles.Integer, Inv, out int day) || !ulong.TryParse(f[2], NumberStyles.HexNumber, Inv, out ulong secret))
                        {
                            return false;
                        }
                        tape.SetSecret(day, secret);
                        break;
                    case "V":
                        if (f.Length < 3 || !int.TryParse(f[1], NumberStyles.Integer, Inv, out int envDay) || !MarketTape.ReadEnv(f[2], out MarketEnv env))
                        {
                            return false;
                        }
                        tape.SetEnv(envDay, env);
                        break;
                    case "E":
                        if (f.Length < 6 || !Ints(f, 1, 5, out int[] e))
                        {
                            return false;
                        }
                        tape.AddEvent(new MarketEvent { Day = e[0], Phase = e[1], Kind = e[2], A = e[3], B = e[4] });
                        break;
                    case "F":
                        if (f.Length < 3 || !Ints(f, 1, 2, out int[] at) || at[1] < 0 || at[1] > MarketCatalogue.PhaseClose)
                        {
                            return false;
                        }
                        tape.Reach(at[0], at[1], true);
                        frontier = Math.Max(frontier, MarketState.Time(at[0], at[1]));
                        break;
                    case "K":
                        if (f.Length < 4 || !Ints(f, 1, 2, out int[] moment) || !long.TryParse(f[3], NumberStyles.Integer, Inv, out long sum))
                        {
                            return false;
                        }
                        checks?.Add(MarketState.Time(moment[0], moment[1]));
                        checks?.Add(sum);
                        break;
                }
            }
            return true;
        }

        private static bool Ints(string[] f, int from, int count, out int[] values)
        {
            values = new int[count];
            for (int i = 0; i < count; i++)
            {
                if (from + i >= f.Length || !int.TryParse(f[from + i], NumberStyles.Integer, Inv, out values[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }

        /// <summary>
        /// A Going Public guest's trades on their way to the host, added up by company until the next request and sent as
        /// "i|stock|micros|shares" lines; a larger sum goes over several lines, so the host's tape takes the same total as if
        /// every fill had been sent alone. A line's text once a second.
        /// </summary>
    public sealed class GuestTrades
    {
        public const int LinesMost = 64;          // a request (MarketDirector.HostRequest)
        public const int MicrosMost = 200000;     // a line, either way (MarketEngine.GuestTrade)

        private readonly List<int> _stocks = new List<int>(8);
        private readonly List<long> _micros = new List<long>(8);
        private readonly List<long> _shares = new List<long>(8);

        public bool Empty => _stocks.Count == 0;

        /// <summary>A fill's lasting move (micros) and its shares, added to its company's since the last request.</summary>
        public void Add(int stock, int micros, long shares)
        {
            if (shares <= 0)
            {
                return;
            }
            int at = _stocks.IndexOf(stock);
            if (at < 0)
            {
                _stocks.Add(stock);
                _micros.Add(0L);
                _shares.Add(0L);
                at = _stocks.Count - 1;
            }
            _micros[at] += micros;
            _shares[at] += shares;
        }

        public int Lines
        {
            get
            {
                int lines = 0;
                for (int i = 0; i < _stocks.Count; i++)
                {
                    lines += LinesOf(_micros[i], _shares[i]);
                }
                return lines;
            }
        }

        /// <summary>Up to <paramref name="most"/> lines sent, the company that traded first first; the rest waits. How many were sent.</summary>
        public int Send(Action<string> send, int most)
        {
            int sent = 0;
            while (sent < most && _stocks.Count > 0)
            {
                long micros = _micros[0], shares = _shares[0];
                int lines = LinesOf(micros, shares);
                long m = lines <= 1 ? micros : Math.Sign(micros) * Math.Min(Math.Abs(micros), MicrosMost);
                long n = shares;
                if (lines > 1)
                {
                    long part = micros != 0 ? (long)((double)shares * Math.Abs(m) / Math.Abs(micros)) : shares / lines;
                    n = Math.Max(1L, Math.Min(Math.Min(part, shares - (lines - 1)), int.MaxValue));
                }
                send(Line(_stocks[0], m, Math.Max(1L, n)));
                sent++;
                micros -= m;
                shares -= n;
                if (lines <= 1)
                {
                    _stocks.RemoveAt(0);
                    _micros.RemoveAt(0);
                    _shares.RemoveAt(0);
                }
                else
                {
                    _micros[0] = micros;
                    _shares[0] = Math.Max(1L, shares);
                }
            }
            return sent;
        }

        public void Clear()
        {
            _stocks.Clear();
            _micros.Clear();
            _shares.Clear();
        }

        /// <summary>One trade line for the host, its numbers culture-free: a machine set to another language writes its minus sign as every other does.</summary>
        public static string Line(int stock, long micros, long shares)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            return "i|" + stock.ToString(inv) + "|" + micros.ToString(inv) + "|" + shares.ToString(inv);
        }

        /// <summary>A trade line read on the host, culture-free as <see cref="Line"/> writes it; false for anything else.</summary>
        public static bool Read(string line, out int stock, out int micros, out long shares)
        {
            stock = micros = 0;
            shares = 0L;
            string[] f = line != null ? line.Split('|') : null;
            CultureInfo inv = CultureInfo.InvariantCulture;
            return f != null && f.Length >= 4 && f[0] == "i" && int.TryParse(f[1], NumberStyles.Integer, inv, out stock)
                   && int.TryParse(f[2], NumberStyles.Integer, inv, out micros) && long.TryParse(f[3], NumberStyles.Integer, inv, out shares);
        }

        // The lines a company's sum takes: its move in parts of MicrosMost, its shares in parts of int.MaxValue; one at least.
        private static int LinesOf(long micros, long shares)
        {
            long byMove = (Math.Abs(micros) + MicrosMost - 1) / MicrosMost;
            long byShares = (Math.Max(1L, shares) + int.MaxValue - 1) / int.MaxValue;
            return (int)Math.Max(1L, Math.Min(int.MaxValue, Math.Max(byMove, byShares)));
        }
    }
}
