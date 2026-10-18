using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The player's cash as the market moves it: the game's own transaction call, so every trade, commission, dividend and
    /// fee is in the game's history under its own name. No address goes with it: shares are not a business's cost.
    /// </summary>
    public sealed class MarketWallet : IMarketWallet
    {
        public long Cash => SaveGameManager.Current != null ? (long)Math.Floor(Math.Max(0.0, SaveGameManager.Current.Money)) : 0L;

        // A sum larger than the game moves exactly in one transaction goes in pieces a float holds exactly (InvestRules.Piece).
        public bool Take(long amount, string why, string name)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (SaveGameManager.Current == null || Cash < amount)
            {
                return false;   // the cash is not there: nothing taken
            }
            Move(-1, amount, why, name);   // all of it is there: no piece can be refused
            return true;
        }

        public bool Ready(long amount)
        {
            return amount <= 0 || Cash >= amount;   // the game's own cash; the broker's money is MarketEngine.Wallet's
        }

        public void Force(long amount, string why, string name)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                Move(-1, amount, why, name);
            }
        }

        public void Give(long amount, string why, string name)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                Move(1, amount, why, name);
            }
        }

        private static void Move(int sign, long amount, string why, string name)
        {
            for (long left = amount; left > 0;)
            {
                GameManager.ChangeMoneySafe(sign * InvestRules.Piece(ref left), Info(why, name), null, null, true);
            }
        }

        // A dictionary each: the game keeps a transaction's data for as long as it lists the transaction.
        private static TransactionInfo Info(string why, string name)
        {
            return new TransactionInfo(why, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } });
        }

        /// <summary>
        /// What the game counts the player worth in money, as the Persona app shows it; their cash alone if it cannot be read.
        /// Read when a bond page is written or a bond bought, never per frame.
        /// </summary>
        public static long Wealth()
        {
            if (SaveGameManager.Current == null)
            {
                return 0L;
            }
            try
            {
                return (long)Math.Floor(Helpers.PlayerHelper.GetPersonalWealth().CurrentWealth);
            }
            catch (Exception)
            {
                return (long)Math.Floor(Math.Max(0.0, SaveGameManager.Current.Money));
            }
        }
    }
}
