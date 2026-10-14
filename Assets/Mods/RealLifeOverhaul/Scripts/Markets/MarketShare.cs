using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The stock market in a Going Public session (Core/SharedCity, part M): one market for the city, the host's, with
    /// every player's own holdings, orders and account their own. Game-free (RealLifeOverhaul.Build/EconomyCheck).
    ///
    /// Nothing of the market's prices is sent as prices. A market is exact: the same state, the same day's secret, the
    /// same city of the day and the same events give the same moments, to the cent (that is what makes it proof against
    /// a reload, see <see cref="MarketTape"/>). So a guest is given the host's market once, whole (the city's part of its
    /// state, <see cref="MarketStore.Write(MarketState, bool)"/>, with the host's tape from that day), and from then on
    /// only the new lines of the host's tape: each day's secret and city, what happened and when, how far the host has
    /// played, and every player's trades (MarketInputs.EventTrade). The guest's market follows that tape, never past
    /// the host's furthest moment, and plays the very moments the host played; its own orders fill on the way as they
    /// would alone, and their moves go to the host to be put on the tape for everybody.
    ///
    /// The check: with each set of lines the host says what its prices came to at the moment it sent them
    /// (<see cref="Checksum"/>); a guest whose market comes out different at that moment asks for the whole market again.
    ///
    ///     the snapshot   the city's line of the market (MarketStore), a new line, the tape's lines from its day, a K line
    ///     an update      the tape's new lines, a K line
    ///     K day phase n  the checksum of the market at that moment
    /// </summary>
    public static class MarketShare
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The host's market, whole, for a guest.</summary>
        public static string Write(MarketState s, MarketTape tape)
        {
            var t = new StringBuilder(128 * 1024);
            t.Append(MarketStore.Write(s, true)).Append('\n');
            t.Append(tape != null ? tape.From(s.Day) : "F " + s.Day.ToString(Inv) + " " + s.Phase.ToString(Inv) + "\n");
            Check(t, s);
            return t.ToString();
        }

        /// <summary>The host's update: the tape's new lines and the check of where the market stands; null if nothing has moved.</summary>
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

        /// <summary>What the city's market comes to at its moment: every listed company's last price, by company, and the index.</summary>
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
        /// The host's market for a guest: a new state, the host's city with the guest's own holdings, orders, trades,
        /// watch list, account, dividends owed and trading bots, and the host's tape from that day, put on <paramref name="into"/> if it
        /// is a tape of the host's market (a guest's own copy of it from an earlier session: its past is kept, what it
        /// played alone since is forgotten) or on a new one. <paramref name="frontier"/> is the furthest moment the host has
        /// played. Null (and nothing changed) for a text that cannot be read. A holding in a company the host's market does
        /// not have (it should not happen: the companies are the catalogue's) is kept, its company standing still at its
        /// last price.
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
            int version;
            MarketState host;
            try
            {
                host = MarketStore.Read(text.Substring(0, cut), out version);
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
                return null;   // (read once on a tape of its own first: a damaged text leaves the guest's tape as it was)
            }
            MarketTape follow = lines;
            if (into != null && into.Id == host.Id)
            {
                follow = into;
                follow.Forget(host.Now);
                Follow(follow, text.Substring(cut + 1), null, ref frontier);
            }
            host.Me = mine.Me;
            host.Listing = mine.Listing ?? new ListingFile();   // (the player's own way to the market and books: theirs, never the host's)
            // The player's own listed company is not in the host's city: it is set aside, whole, until they play alone again
            // (MarketEngine.Alone), and nothing of it moves meanwhile.
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
            host.HeldDay = host.HeldPhase = 0;   // (what the guest has seen is what the host has played: nothing to hold back)
            host.Positions.AddRange(mine.Positions);
            host.Orders.AddRange(mine.Orders);
            host.Trades.AddRange(mine.Trades);
            host.Watch.AddRange(mine.Watch);
            host.Dues.AddRange(mine.Dues);
            host.Bots.AddRange(mine.Bots);   // (the player's bots: theirs, trading in the city's market as the player does)
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
                    continue;   // (set aside above)
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
        /// The host's tape lines put on a guest's tape: the host's secret and city of a day in place of any it had, what
        /// happened, the host's furthest moment (into <paramref name="frontier"/>, which only grows); the checks (moment,
        /// sum) into the list. False for lines that cannot be read.
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
}
