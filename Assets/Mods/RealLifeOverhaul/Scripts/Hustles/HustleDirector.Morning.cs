using System;

namespace RealLifeOverhaul.Hustles
{
    public sealed partial class HustleDirector
    {
        // A day's listings from the sellers of the book, and from the rivals that own a business that sells used vehicles or machines (read once a day).
        // Game-free but for three reads (ReadDealers, ShopLevel, MarketPeople): EconomyCheck plays it day by day as it stands.
        private const int PoolWaitSeconds = 90;
        private long _waitSince;   // when the morning first found the people pool not up yet

        private void MorningOf(int day)
        {
            if (RealLifeOverhaul.Careers.PeoplePool.WaitingFor(ref _waitSince, PoolWaitSeconds))
            {
                return;   // the sellers are people of the city's pool, named for good: the morning waits for the population to come up (seconds after the save loads)
            }
            SellerRules.Ensure(_sellers, _state.Seed, SellerPerson);
            ReadDealers();
            HustleRules.Morning(_state, day, _homes, _cars, _colours, ShopLevel, MarketPeople, _sellers, _dealers);
        }
    }
}
