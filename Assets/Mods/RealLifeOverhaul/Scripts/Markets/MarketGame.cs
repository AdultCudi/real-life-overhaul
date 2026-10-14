using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The player's cash as the market moves it: the game's own transaction call, so every purchase, sale, commission,
    /// dividend and fee is in the game's transaction history under its own name (Finance puts "shares_" keys under
    /// investments). No address goes with it: shares are not a business's cost.
    /// </summary>
    public sealed class MarketWallet : IMarketWallet
    {
        public long Cash => SaveGameManager.Current != null ? (long)Math.Floor(Math.Max(0.0, SaveGameManager.Current.Money)) : 0L;

        public bool Take(long amount, string why, string name)
        {
            return amount <= 0 || (SaveGameManager.Current != null && amount <= int.MaxValue && GameManager.ChangeMoneySafe(-amount, Info(why, name), null, null, false));
        }

        public void Force(long amount, string why, string name)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                GameManager.ChangeMoneySafe(-Math.Min(amount, int.MaxValue), Info(why, name), null, null, true);
            }
        }

        public void Give(long amount, string why, string name)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                GameManager.ChangeMoneySafe(Math.Min(amount, int.MaxValue), Info(why, name), null, null, true);
            }
        }

        // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
        private static TransactionInfo Info(string why, string name)
        {
            return new TransactionInfo(why, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } });
        }
    }

    /// <summary>
    /// The market's tape on disk: a file of the game's own data folder for each playthrough
    /// (RealLifeOverhaul/Market/&lt;id&gt;.tape), outside every save. New lines are added to it as the market goes (a new
    /// day's secret the moment it is drawn, the furthest moment played at least once a second), and the whole of it is
    /// written again, compacted, when the game saves. A file that cannot be read or written leaves the market running on
    /// what it has, with one line in the log.
    ///
    /// NOT ON THE GAME'S OWN THREAD. Each write is a few calls of the file system (and, where the machine's virus guard
    /// looks at every file written, a look of its own), which on the game's thread would stall a frame every few seconds
    /// of a trading day. So the writes go to one writer on the thread pool, in the order they are given: a day's secret is
    /// on disk within a few milliseconds of being drawn, still long before a load could ask for it again. Before the file
    /// is read, and as the market stops, whatever is on its way is written first (Drain).
    /// </summary>
    public static class MarketTapeFile
    {
        public static IModLogger Logger;
        private static bool _warned;

        private struct Job
        {
            public string Path;
            public string Text;
            public string First;    // what a new file begins with
            public bool Whole;      // the whole tape again, in place of what is there
        }

        private static readonly object Gate = new object();
        private static readonly Queue<Job> Jobs = new Queue<Job>(8);
        private static bool _writing;

        // The last tape's file and first line, made once for it (on the game's thread) rather than for every append, about
        // once a second while the market trades: the data folder is a call into the engine and a new string each time.
        private static ulong _namedId;
        private static string _namedPath, _namedFirst;

        public static string PathOf(ulong id)
        {
            Name(id);
            return _namedPath;
        }

        private static void Name(ulong id)
        {
            if (_namedPath == null || _namedId != id)
            {
                _namedPath = Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Market", id.ToString("X16") + ".tape");
                _namedFirst = "T1 " + id.ToString("X16") + "\n";
                _namedId = id;
            }
        }

        public static MarketTape Read(ulong id)
        {
            Drain();
            try
            {
                string path = PathOf(id);
                return MarketTape.Read(id, File.Exists(path) ? File.ReadAllText(path) : null);
            }
            catch (Exception e)
            {
                Warn("read", e);
                return new MarketTape(id);
            }
        }

        public static void Append(ulong id, string lines)
        {
            if (string.IsNullOrEmpty(lines))
            {
                return;
            }
            Name(id);
            Enqueue(new Job { Path = _namedPath, Text = lines, First = _namedFirst });
        }

        public static void Rewrite(ulong id, string whole)
        {
            Enqueue(new Job { Path = PathOf(id), Text = whole, Whole = true });
        }

        /// <summary>Waits (a couple of seconds at the most) until every write given so far is on disk.</summary>
        public static void Drain()
        {
            lock (Gate)
            {
                int waits = 0;
                while ((_writing || Jobs.Count > 0) && waits++ < 20)
                {
                    Monitor.Wait(Gate, 100);
                }
            }
        }

        // (The path is made here, on the game's thread: the data folder is the engine's to say.)
        private static void Enqueue(Job job)
        {
            lock (Gate)
            {
                Jobs.Enqueue(job);
                if (_writing)
                {
                    return;
                }
                _writing = true;
            }
            ThreadPool.QueueUserWorkItem(_ => Write());
        }

        // The one writer: the jobs in the order given, until there are none.
        private static void Write()
        {
            while (true)
            {
                Job job;
                lock (Gate)
                {
                    if (Jobs.Count == 0)
                    {
                        _writing = false;
                        Monitor.PulseAll(Gate);
                        return;
                    }
                    job = Jobs.Dequeue();
                }
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(job.Path));
                    if (job.Whole)
                    {
                        string temp = job.Path + ".new";
                        File.WriteAllText(temp, job.Text);
                        if (File.Exists(job.Path))
                        {
                            File.Delete(job.Path);
                        }
                        File.Move(temp, job.Path);
                    }
                    else
                    {
                        if (!File.Exists(job.Path))
                        {
                            File.WriteAllText(job.Path, job.First);
                        }
                        File.AppendAllText(job.Path, job.Text);
                    }
                }
                catch (Exception e)
                {
                    Warn(job.Whole ? "write" : "add to", e);
                }
            }
        }

        private static void Warn(string what, Exception e)
        {
            if (!_warned)
            {
                _warned = true;
                Logger?.Warn("Stock market: could not " + what + " the market's tape (" + e.Message + "). The market goes on; a load may replay a day differently until the file can be written again.");
            }
        }
    }

    /// <summary>
    /// The world the market lives in, in the game: a fresh secret nobody can guess for each new market day (the system's
    /// own random source, never the game's or the save's), the city as the wiring reads it, the player's cash, and the
    /// director, which is told of the news, the fills, the money and the calls.
    /// </summary>
    public sealed class MarketGameWorld : IMarketWorld
    {
        private readonly MarketDirector _director;
        private readonly MarketWallet _wallet = new MarketWallet();
        private readonly byte[] _bytes = new byte[8];
        private readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();

        public MarketGameWorld(MarketDirector director)
        {
            _director = director;
        }

        public ulong NewSecret()
        {
            _random.GetBytes(_bytes);
            return BitConverter.ToUInt64(_bytes, 0);
        }

        public MarketEnv Env(int day)
        {
            Func<int, MarketEnv> city = MarketBridges.City;
            if (city != null)
            {
                try
                {
                    return city(day);
                }
                catch (Exception e)
                {
                    MarketDirector.Logger?.Error(e);
                }
            }
            MarketEnv quiet = MarketEnv.Quiet();
            quiet.PriceLevel = Prices.Level;
            return quiet;
        }

        public IMarketWallet Wallet => _wallet;

        public int CommissionLeast => Prices.At(MarketCatalogue.CommissionLeastAtStart);

        public void Said(in MarketNews news)
        {
            _director.Heard(news);
        }

        public void Filled(Order order, Stock stock, long qty, int cents)
        {
            _director.OrderFilled(order, stock, qty, cents);
        }

        public void Paid(TradeKind kind, Stock stock, long amount)
        {
            _director.MoneyMoved(kind, stock, amount);
        }

        public void Called(long amount, int byDay)
        {
            _director.MarginCalled(amount, byDay);
        }
    }
}
