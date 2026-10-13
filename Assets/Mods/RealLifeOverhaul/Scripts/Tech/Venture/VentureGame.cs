using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the venture part does to the game: money only, as dated transactions of the mod's own (investments and their
    /// returns with no address, a firm's raise and its bridge at the firm), none of it deductible; and a founder's name, the
    /// game's own way. Nothing here runs by itself.
    /// </summary>
    internal static class VentureGame
    {
        public const string InvestKey = "reallife_transaction_tech_venture_investment";
        public const string ReturnKey = "reallife_transaction_tech_venture_return";
        public const string FundKey = "reallife_transaction_tech_venture_fund";
        public const string FundBackKey = "reallife_transaction_tech_venture_fund_back";
        public const string DiligenceKey = "reallife_transaction_tech_venture_diligence_fee";
        public const string RaiseKey = "reallife_transaction_tech_venture_raise";
        public const string BridgeKey = "reallife_transaction_tech_venture_bridge_loan";
        public const string InterestKey = "reallife_transaction_tech_venture_bridge_interest";
        public const string ShareSaleKey = "reallife_transaction_tech_venture_share_sale";

        private static GameInstance Save => SaveGameManager.Current;

        public static long Cash => Save != null ? (long)Mathf.Max(0f, Save.Money) * 100 : 0;

        /// <summary>Money out of the player's cash, cents; refused (false) if the cash does not cover it.</summary>
        public static bool Pay(long cents, string key, string name, BuildingRegistration at = null, bool force = false)
        {
            if (cents <= 0)
            {
                return true;
            }
            return Save != null && GameManager.ChangeMoneySafe(-cents / 100f, Info(key, name), null, at != null ? at.Address : null, force);
        }

        /// <summary>Money into the player's cash, cents.</summary>
        public static void Give(long cents, string key, string name, BuildingRegistration at = null)
        {
            if (cents > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(cents / 100f, Info(key, name), null, at != null ? at.Address : null, true);
            }
        }

        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
        }

        /// <summary>A founder's name, the game's own way.</summary>
        public static string Founder()
        {
            var s = new Seeker();
            HiringGame.Name(s);
            return s.P.Name;
        }
    }
}
