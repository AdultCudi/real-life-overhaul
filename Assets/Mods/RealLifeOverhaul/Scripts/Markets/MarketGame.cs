using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
    /// </summary>
    public static class MarketTapeFile
    {
        public static IModLogger Logger;
        private static bool _warned;

        public static string PathOf(ulong id)
        {
            return Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Market", id.ToString("X16") + ".tape");
        }

        public static MarketTape Read(ulong id)
        {
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
            try
            {
                string path = PathOf(id);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, "T1 " + id.ToString("X16") + "\n");
                }
                File.AppendAllText(path, lines);
            }
            catch (Exception e)
            {
                Warn("add to", e);
            }
        }

        public static void Rewrite(ulong id, string whole)
        {
            try
            {
                string path = PathOf(id);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".new";
                File.WriteAllText(temp, whole);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Warn("write", e);
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
