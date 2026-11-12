using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>
    /// Everything the schemes read from or do to the game: the day, the cash, the save's own seed, money in and out
    /// through the mod's one booking call, the game's notices, and the phone put away before a page of theirs. Asked on a
    /// press or once a game day, never per frame.
    /// </summary>
    internal static class SchemeGame
    {
        public const string ExpenseKey = "reallife_transaction_scheme_expense";       // the tax an invented expense saved: money in
        public const string WashKey = "reallife_transaction_scheme_wash";             // cash sales through a till, after the cut and the tax: money in
        public const string RepayKey = SchemeCatalogue.RepayKey;                      // paid back by the player
        public const string FoundKey = SchemeCatalogue.FoundKey;                      // taken back after an inquiry's findings
        public const string SurchargeKey = SchemeCatalogue.SurchargeKey;              // the penalty on top of it
        public const string FavourKey = "reallife_transaction_scheme_favour";         // paid to somebody through the bank account
        public const string FundKey = "reallife_transaction_scheme_fund";             // placed in a rival's fund
        public const string FundBackKey = "reallife_transaction_scheme_fund_back";    // a fund's money back, with its return or by the court
        public const string FundFeeKey = "reallife_transaction_scheme_fund_fee";      // having a fund looked at

        private static GameInstance Save => SaveGameManager.Current;

        public static int Day => Save != null ? Save.Day : 0;

        /// <summary>The cash in the wallet, whole money, never under nothing.</summary>
        public static long Cash => Save != null ? (long)Mathf.Max(0f, Mathf.Floor(Save.Money)) : 0L;

        /// <summary>The game's own name for the playthrough (the city's seed and the playthrough's own id): what every draw of the schemes starts from, the same on every load.</summary>
        public static string SeedText => Save == null ? "city" : SchemeRules.PlaythroughOf(Save.seed, Save.characterId);

        public static int Level => Prices.Level;

        public static bool InSession => GoingPublic.InSession;

        /// <summary>Money out of the wallet; refused, with nothing booked, when the wallet does not hold it.</summary>
        public static bool Pay(long amount, string key)
        {
            return amount <= 0 || (Cash >= amount && BizBooks.Book(-amount, new TransactionInfo(key), null, null, false, false));
        }

        /// <summary>Money taken whatever the wallet holds, as the game takes its own wages: what an inquiry found is not left to the player's good will.</summary>
        public static void Take(long amount, string key)
        {
            if (amount > 0)
            {
                BizBooks.Book(-amount, new TransactionInfo(key), null, null, true, false);
            }
        }

        public static void Give(long amount, string key)
        {
            if (amount > 0)
            {
                BizBooks.Book(amount, new TransactionInfo(key), null, null, false, false);
            }
        }

        /// <summary>A notice of the game's, in its own words and colours; a click on it runs <paramref name="click"/> behind the guard.</summary>
        public static void Tell(NotificationType type, string key, Dictionary<string, string> data, float seconds = 12f, System.Action click = null)
        {
            Notifications.Show(type, key, data, seconds, null, click != null ? Guard.Wrap(click) : null, true, false);
        }

        /// <summary>The pairs of a notice as the game's data.</summary>
        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var data = new Dictionary<string, string>(4);
            for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return data;
        }

        /// <summary>The phone's full screen is put away: the game's list stands on the HUD, which it hides.</summary>
        public static void PutPhoneAway()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.fullMenu != null && FullMenu.IsOpen)
            {
                uis.fullMenu.Toggle(false);
            }
        }
    }
}
