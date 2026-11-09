using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A capital gain the player made by disposing of a stake in a startup, noted for the tax return: the day it completed and the cents, at the prices of that day.</summary>
    public sealed class GainLine
    {
        public int Day;
        public long Cents;
        public string Name = string.Empty;
    }

    /// <summary>How much of a firm's cost basis the player's earlier sales of shares have already used, so slices of one stake add up to the whole basis and no more.</summary>
    public sealed class BasisLine
    {
        public string Place = string.Empty;
        public long Sold;     // shares sold so far
        public long Used;     // basis used so far, cents at the start's prices
    }

    /// <summary>What exits note for the tax return. Kept in a line of its own, never in the exits' or the ventures' lines. Game-free.</summary>
    public sealed class GainFile
    {
        public readonly List<GainLine> Lines = new List<GainLine>(8);
        public readonly List<BasisLine> Basis = new List<BasisLine>(4);

        public bool Empty => Lines.Count == 0 && Basis.Count == 0;

        public BasisLine BasisOf(string place, bool make)
        {
            for (int i = 0; i < Basis.Count; i++)
            {
                if (Basis[i].Place == place)
                {
                    return Basis[i];
                }
            }
            if (!make)
            {
                return null;
            }
            var b = new BasisLine { Place = place };
            Basis.Add(b);
            return b;
        }
    }

    /// <summary>
    /// Capital gains of a startup stake sold, for the quarter's return (CompanyBridges.StartupGains). The gain is what the player received less their cost basis, never under
    /// nothing. The basis is what the mod holds: what the player paid for shares (the player's holder's Invested) used in proportion to the shares sold, and, for a firm
    /// sold with its business, the business's stock and furniture at the game's resale price (the figure a sale of a business takes) in the player's share of the firm. The
    /// incorporation fee and the running costs were deductible in the returns when paid, so they are not in the basis a second time. Game-free.
    /// </summary>
    public static class ExitGains
    {
        public const int KeptDays = 400;     // a note older than this has been in a closed return for a long time
        public const int LinesMost = 500;

        public static long Invested(Cap c)
        {
            long sum = 0;
            for (int i = 0; c != null && i < c.Holders.Count; i++)
            {
                sum += c.Holders[i].Kind == (int)HolderKind.Player ? Math.Max(0L, c.Holders[i].Invested) : 0;
            }
            return sum;
        }

        /// <summary>The cents (at today's prices) of an amount written at the start's prices.</summary>
        public static long Level(long cents, int level) => cents * level / 1000;

        public static long Gain(long received, long basis) => Math.Max(0L, received - Math.Max(0L, basis));

        /// <summary>
        /// The part of what the player paid for their shares that goes with selling <paramref name="shares"/> of the <paramref name="held"/> they hold now (cents at the start's
        /// prices). The slices of one stake add up to the whole, whatever their size. Nothing is changed.
        /// </summary>
        public static long Slice(GainFile f, Cap c, string place, long held, long shares)
        {
            long invested = Invested(c);
            BasisLine b = f.BasisOf(place, false);
            long sold = b != null ? b.Sold : 0, used = b != null ? b.Used : 0;
            long original = Math.Max(0L, held) + sold;
            if (invested <= 0 || original <= 0 || shares <= 0)
            {
                return 0;
            }
            long target = shares >= held ? invested : (long)((double)invested * (sold + shares) / original);
            return Math.Max(0L, Math.Min(invested, target) - Math.Min(invested, used));
        }

        private static void Take(GainFile f, string place, long shares, long slice)
        {
            BasisLine b = f.BasisOf(place, true);
            b.Sold += Math.Max(0L, shares);
            b.Used += Math.Max(0L, slice);
        }

        /// <summary>
        /// The player's own shares sold (a round's window, the listing's lock-up over, the plain sale of a tenth): what they fetched, the shares held before, the shares sold.
        /// Notes the gain and returns it, cents at today's prices.
        /// </summary>
        public static long ForShares(GainFile f, Cap c, string place, string name, long paid, long held, long shares, int day, int level)
        {
            if (f == null || paid <= 0)
            {
                return 0;
            }
            long slice = Slice(f, c, place, held, shares);
            Take(f, place, shares, slice);
            return Note(f, day, name, Gain(Level(paid, level), Level(slice, level)));
        }

        /// <summary>
        /// The player's shares of a firm that trades on the market, sold there (the founder's deal in the Stocks app). The market pays the wallet in today's money, so
        /// what it fetched is <paramref name="money"/> less the <paramref name="fee"/> (what the wallet gained, as every other sale counts it) and is not levelled again;
        /// the basis is the one a sale of the same shares in <see cref="ForShares"/> uses. A purchase (shares not under nothing) notes nothing. Returns the cents noted.
        /// </summary>
        public static long ForMarketSale(GainFile f, Cap c, string place, string name, long money, long fee, long held, long shares, int day, int level)
        {
            long net = money - Math.Max(0L, fee);
            if (f == null || shares >= 0 || net <= 0)
            {
                return 0;
            }
            long sold = -shares;
            long slice = Slice(f, c, place, held, sold);
            Take(f, place, sold, slice);
            return Note(f, day, name, Gain(net, Level(slice, level)));
        }

        /// <summary>
        /// A firm sold with its business (a buyer's offer, the team taken for the hire): the wallet's gain by the sale, the business's stock and furniture at the game's
        /// resale price in today's cents (0 when it cannot be read), and the firm's cap table. The player's part of the fixtures is their share of the firm; what the
        /// investors' share of it cost was the investors' money, which the sale already paid back to them before the player had anything.
        /// </summary>
        public static long ForSale(GainFile f, Cap c, string place, string name, long player, long fixtures, int day, int level)
        {
            if (f == null)
            {
                return 0;
            }
            long held = PlayerShares(c), total = c != null ? c.Total : 0;
            long own = c == null || total <= 0 ? Math.Max(0L, fixtures) : (long)((double)Math.Max(0L, fixtures) * held / total);
            long slice = Slice(f, c, place, held, held);
            BasisLine b = f.BasisOf(place, false);
            if (b != null)
            {
                f.Basis.Remove(b);   // the firm is gone: nothing of it is left to slice
            }
            return Note(f, day, name, Gain(Level(Math.Max(0L, player), level), own + Level(slice, level)));
        }

        /// <summary>A stake the player bought in a startup of the city (an angel's line) sold with the startup: what it fetched against what the player paid for it.</summary>
        public static long ForStake(GainFile f, string name, long paid, long invested, int day, int level)
        {
            return f == null || paid <= 0 ? 0 : Note(f, day, name, Gain(Level(paid, level), Level(invested, level)));
        }

        public static long PlayerShares(Cap c)
        {
            long mine = 0;
            for (int i = 0; c != null && i < c.Holders.Count; i++)
            {
                mine += c.Holders[i].Kind == (int)HolderKind.Player ? c.Holders[i].Shares : 0;
            }
            return mine;
        }

        /// <summary>Notes a gain on its day (one line a day, so sales in tiny slices add up in cents and lose nothing to rounding); nothing for no gain. Returns the cents noted.</summary>
        public static long Note(GainFile f, int day, string name, long gain)
        {
            if (f == null || gain <= 0)
            {
                return 0;
            }
            for (int i = f.Lines.Count - 1; i >= 0; i--)
            {
                if (day - f.Lines[i].Day > KeptDays)
                {
                    f.Lines.RemoveAt(i);
                }
            }
            GainLine line = null;
            for (int i = 0; i < f.Lines.Count && line == null; i++)
            {
                line = f.Lines[i].Day == day ? f.Lines[i] : null;
            }
            if (line == null)
            {
                if (f.Lines.Count >= LinesMost)
                {
                    f.Lines.RemoveAt(0);
                }
                line = new GainLine { Day = day, Name = name ?? string.Empty };
                f.Lines.Add(line);
            }
            line.Cents += gain;
            return gain;
        }

        /// <summary>The gains of stakes sold between two days, both counted, in the game's dollars as the return counts its takings.</summary>
        public static int Between(GainFile f, int fromDay, int toDay)
        {
            long sum = 0;
            for (int i = 0; f != null && i < f.Lines.Count; i++)
            {
                sum += f.Lines[i].Day >= fromDay && f.Lines[i].Day <= toDay ? f.Lines[i].Cents : 0;
            }
            return (int)Math.Min(int.MaxValue / 2, sum / 100);
        }
    }

    /// <summary>
    /// The save line of the gains noted: <c>reallife:tech_exit_gains</c>, version 1, in <see cref="TechWriter"/>'s format. A save from before has no line and starts with nothing noted,
    /// so a firm sold earlier is taxed for nothing; an older RLO never reads the line. Game-free.
    /// </summary>
    public static class ExitGainStore
    {
        public const string Key = "reallife:tech_exit_gains";
        public const int Version = 1;

        public static GainFile Load(Dictionary<string, string> data) => TechLines.Load(data, Key, Version, Read, out _);

        /// <summary>The line, or null when there is nothing to keep.</summary>
        public static string Write(GainFile f)
        {
            if (f == null || f.Empty)
            {
                return null;
            }
            var w = new TechWriter(Version);
            w.Part('g');
            foreach (GainLine l in f.Lines)
            {
                w.Record().Add(l.Day).Add(l.Cents).Add(l.Name);
            }
            w.Part('b');
            foreach (BasisLine b in f.Basis)
            {
                w.Record().Place(b.Place).Add(b.Sold).Add(b.Used);
            }
            return w.ToString();
        }

        public static GainFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new GainFile();
            foreach (Fields x in r.Records('g'))
            {
                if (f.Lines.Count >= ExitGains.LinesMost)
                {
                    break;
                }
                int day = x.Int();
                long cents = x.Long();
                if (cents > 0)
                {
                    f.Lines.Add(new GainLine { Day = day, Cents = cents, Name = x.Text() });
                }
            }
            foreach (Fields x in r.Records('b'))
            {
                if (f.Basis.Count >= 32)
                {
                    break;
                }
                f.Basis.Add(new BasisLine { Place = x.Place(), Sold = Math.Max(0L, x.Long()), Used = Math.Max(0L, x.Long()) });
            }
            return f;
        }
    }
}
