using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RealLifeOverhaul.Markets;

// The city's stock market (Scripts/Markets): prices over thousands of days and many seeds, what a trade costs and what
// protects the market from being played, orders, dividends, splits, failures and takeovers, the candles and the analysis
// tools against figures worked out by hand, the tape (a reload replays, never re-rolls), the save's line, and the old
// Ventures market brought over once. Every expected figure is worked out from the catalogue or by hand.
internal static partial class Program
{
    /// <summary>The player's cash, standing in for the game's.</summary>
    private sealed class MarketWallet : IMarketWallet
    {
        public long Money;
        public long In, Out, Forced;

        public long Cash => Math.Max(0, Money);

        public bool Take(long amount, string why, string name)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (Money < amount)
            {
                return false;
            }
            Money -= amount;
            Out += amount;
            return true;
        }

        public void Force(long amount, string why, string name)
        {
            if (amount > 0)
            {
                Money -= amount;
                Forced += amount;
            }
        }

        public void Give(long amount, string why, string name)
        {
            if (amount > 0)
            {
                Money += amount;
                In += amount;
            }
        }
    }

    /// <summary>The city and the world, standing in for the game's: secrets from a seed, a city that can be set.</summary>
    private sealed class MarketWorld : IMarketWorld
    {
        public ulong Seed;
        public int Secrets;
        public MarketEnv City = MarketEnv.Quiet();
        public readonly MarketWallet Purse = new MarketWallet();
        public readonly List<MarketNews> Heard = new List<MarketNews>();
        public int Fills, Calls, Least = MarketCatalogue.CommissionLeastAtStart;
        public long FilledQty;
        public readonly List<long> Payments = new List<long>();

        public MarketWorld(ulong seed)
        {
            Seed = seed;
        }

        public ulong NewSecret()
        {
            Secrets++;
            return MarketRandom.Mix64(Seed * 0x9E3779B97F4A7C15UL + (ulong)Secrets * 0xD1B54A32D192ED03UL);
        }

        public MarketEnv Env(int day)
        {
            MarketEnv e = City;
            e.Supply = (int[])City.Supply.Clone();
            e.TradeOf = (int[])City.TradeOf.Clone();
            e.TradeSales = (int[])City.TradeSales.Clone();
            return e;
        }

        public IMarketWallet Wallet => Purse;
        public int CommissionLeast => Least;

        public void Said(in MarketNews news)
        {
            if (Heard.Count < 200000)
            {
                Heard.Add(news);
            }
        }

        public void Filled(Order order, Stock stock, long qty, int cents)
        {
            if (qty > 0)
            {
                Fills++;
                FilledQty += qty;
            }
        }

        public void Paid(TradeKind kind, Stock stock, long amount)
        {
            Payments.Add(amount);
        }

        public void Called(long amount, int byDay)
        {
            Calls++;
        }
    }

    private static MarketEngine NewMarket(ulong seed, int year, int day = 1, int minute = 0, MarketWorld world = null, MarketTape tape = null)
    {
        world = world ?? new MarketWorld(seed);
        MarketState state = MarketSetup.Open(seed, day, minute, year, world.Env(day), seed, null, null, null);
        return new MarketEngine(state, tape ?? new MarketTape(seed), world);
    }

    private static MarketWorld WorldOf(MarketEngine m)
    {
        return (MarketWorld)typeof(MarketEngine).GetField("_world", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(m);
    }

    private static void RunTo(MarketEngine engine, int day, int phase = MarketCatalogue.PhaseClose)
    {
        int work = int.MaxValue;
        engine.RunTo(MarketState.Time(day, phase), ref work);
    }

    /// <summary>The first market day on or after a day.</summary>
    private static int TradingOnOrAfter(int day)
    {
        while (!MarketCatalogue.TradingDay(day))
        {
            day++;
        }
        return day;
    }

    private static void Markets()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        MarketCatalogueChecks();
        MarketCalendarChecks();
        MarketRandomChecks();
        MarketPriceChecks();
        MarketLongRun();
        MarketTradeCosts();
        MarketImpact();
        MarketClosedAndHeld();
        MarketTicketPath();
        MarketSkippedTime();
        MarketLimitsAndStops();
        MarketDividends();
        MarketSplits();
        MarketFailuresAndTakeovers();
        MarketNewsAndMood();
        MarketCandles();
        MarketIndicatorChecks();
        MarketStoreChecks();
        MarketTapeChecks();
        MarketCarryOverChecks();
        MarketRounding();
        MarketCancelling();
        MarketMargin();
        MarketCaps();
        MarketCity();
        MarketNoFreeLunch();
        MarketCost();
        MarketTrading2();   // stop losses, take profits and trading bots (MarketChecks5.cs)
        MarketDayMoves();   // the day's move of every company, the index and the sectors, in a session and between (MarketChecks6.cs)
        Console.WriteLine("  the stock market: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }
}
