namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>One bid on a lot: who (0 the player, 1 and more a dealer of the room by <see cref="DealerCatalogue.RivalNames"/>), the hour and the amount.</summary>
    public struct BidEntry
    {
        public int Bidder, Hour, Amount;
    }
}
