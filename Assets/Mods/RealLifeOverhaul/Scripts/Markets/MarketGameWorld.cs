using System;
using System.Security.Cryptography;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The world the market lives in, in the game: a fresh secret for each new market day from the system's random source,
    /// never the game's or the save's, the city as the wiring reads it, the player's cash, and the director to tell.
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
