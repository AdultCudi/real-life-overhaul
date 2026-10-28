using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Extensions;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A save that was hit by the wrapped charge (a purse past an int once made a life event, a trip's mishap or a business's
    /// surcharge take int.MinValue as money, 2.1 billion, shown as "Vehicle costs" and the like) gets it paid back once, at the
    /// load: the game's history holds each such charge as exactly -2147483648 under one of the mod's transaction types. The
    /// number paid back is kept in the save, so a charge is never paid back twice, and a history that has since lost the
    /// charge pays back nothing. Only the recent history can be read (the game keeps about a thousand transactions). COST: one
    /// pass over at most a thousand transactions, at the load.
    /// </summary>
    internal static class LifeMoneyRepair
    {
        private const string Key = "reallife:wrapped_refunded";
        public const string RefundKey = "reallife_transaction_wrapped_refund";

        private static IModLogger Logger => LifeEvents.Logger;

        /// <summary>Pays back the wrapped charges the history holds that have not been paid back. Returns how many.</summary>
        public static int OnLoad()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.Transactions == null)
            {
                return 0;
            }
            int found = 0;
            foreach (Transaction entry in save.Transactions)
            {
                if (entry != null && LifeMoneyRules.IsWrapped(entry.amount, entry.transactionType))
                {
                    found++;
                }
            }
            if (found == 0)
            {
                return 0;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            int refunded = save.modData.TryGetValue(Key, out string text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
            int due = LifeMoneyRules.Due(found, refunded);
            if (due <= 0)
            {
                return 0;
            }
            RealLifeOverhaul.Economy.BizBooks.Book(LifeMoneyRules.RefundEach * due, new TransactionInfo(RefundKey), null, null, force: true);
            save.modData[Key] = (refunded + due).ToString(CultureInfo.InvariantCulture);
            Logger?.Info("Life events: " + due + " charge(s) of 2,147,483,648 that a purse past an int once made are in this save's history; each is paid back (" + found + " in the history, "
                         + refunded + " paid back before).");
            var data = new Dictionary<string, string>(2) { { "amount", (LifeMoneyRules.RefundEach * due).ToCurrencyFormat() }, { "count", due.ToString(CultureInfo.InvariantCulture) } };
            Notifications.Show(NotificationType.Success, "reallife_wrapped_refunded", data, 14f);
            return due;
        }
    }
}
