using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What competition does to the game: a bake off's contract booked as the firm's takings at its own address, through the mod's booking path.</summary>
    internal static class BakeGame
    {
        public const string BakeKey = "reallife_transaction_tech_bakeoff";

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>A bake off's contract, cents: takings of the winning firm, dated, at its address. Nothing leaves a firm through it.</summary>
        public static void Takings(long cents, BuildingRegistration at, string customer, int? day)
        {
            if (cents > 0 && Save != null && at != null)
            {
                RealLifeOverhaul.Economy.BizBooks.Book(cents / 100f, new TransactionInfo(BakeKey, new Dictionary<string, string>(2) { { "name", customer ?? string.Empty } }), day, at.Address, true);
            }
        }
    }
}
