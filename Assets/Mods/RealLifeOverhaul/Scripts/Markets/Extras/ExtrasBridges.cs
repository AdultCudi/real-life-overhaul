using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>A rival tycoon as the market's extras need them: true while they are forward and may act.</summary>
    public delegate bool RivalLook(int index, out long worth, out int stockId, out string holding, out string name, out bool listed);

    /// <summary>
    /// What the stock market's extras ask of the rest of the mod, set by Wiring/StockExtrasLinks and put back by
    /// <see cref="Reset"/>. Each is optional: without it its part stands down and the rest works. Asked on a day, a
    /// market morning or a press, never per frame.
    /// </summary>
    public static class ExtrasBridges
    {
        /// <summary>The player's own bank as it stands (Economy/OwnBank). Default: no bank.</summary>
        public static Func<BankFacts> Bank;

        /// <summary>The offer's money, in cents, put into the bank's capital: the public's, never the player's cash. True if the bank took it.</summary>
        public static Func<long, bool> BankCapital;

        /// <summary>The player's Standing, 0 to 100; under nothing where it is not kept.</summary>
        public static Func<int> Standing;

        /// <summary>
        /// The founder dealt in their bank's shares: the stake's place, its name, the shares (under nothing: sold), the money, the commission, the shares held
        /// before and what the stake cost to found, cents at the start's prices. The tax return follows (Tech/Venture/ExitGains).
        /// </summary>
        public static Action<string, string, long, long, long, long, long> Dealt;

        /// <summary>What the bank was founded with, cents at the start's prices: the founder's cost of their shares for the tax return.</summary>
        public static Func<long> BankPaidIn;

        /// <summary>The bank's stock is no more: the offer's money the bank was keeping for the public is its own again (Economy/OwnBank, <c>OwnBank.Public</c>).</summary>
        public static Action BankRelease;

        public static RivalLook Rival;

        /// <summary>A tycoon's worth moved by a gain or a loss they took on the market, in money.</summary>
        public static Action<int, long> RivalGain;

        public static void Reset()
        {
            Bank = null;
            BankCapital = null;
            Standing = null;
            Dealt = null;
            BankPaidIn = null;
            BankRelease = null;
            Rival = null;
            RivalGain = null;
        }
    }
}
