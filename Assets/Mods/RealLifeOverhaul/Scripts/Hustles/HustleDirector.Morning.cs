using System;

namespace RealLifeOverhaul.Hustles
{
    public sealed partial class HustleDirector
    {
        // A day's listings from the sellers of the book, and from the rivals that own a business that sells used vehicles or machines (read once a day).
        // Game-free but for four reads (SellerName, ReadDealers, ShopLevel, MarketPeople): EconomyCheck plays it day by day as it stands.
        private void MorningOf(int day)
        {
            SellerRules.Ensure(_sellers, _state.Seed, SellerName);
            ReadDealers();
            HustleRules.Morning(_state, day, _homes, _cars, _colours, ShopLevel, MarketPeople, _sellers, _dealers);
        }
    }
}
