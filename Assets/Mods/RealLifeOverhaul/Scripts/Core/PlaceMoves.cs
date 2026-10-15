using System.Collections.Generic;
using BAModAPI;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A place of the mod's that has moved since the save was made (the courthouse, City Utilities) is told once: the
    /// save's modData keeps where it is ("street|number", under the place's own key), and when the place is chosen at a
    /// load, somewhere else than that (or, in a save from before it kept one in which the feature ran, somewhere else than
    /// the old rule's building: Core/CityReserves.TellMoved), the game's notification says where it is now. Nothing else of
    /// the feature is touched: what it keeps (cases, hearings, accounts) is not by address. Once per choice; no frame cost.
    /// </summary>
    internal static class PlaceMoves
    {
        /// <summary>
        /// The place <paramref name="key"/> is now at a street and number. <paramref name="featureKey"/> is the feature's
        /// own save line (present: the feature ran in this save before). Tells <paramref name="noticeKey"/> with the
        /// address if it moved, and keeps the new place in the save.
        /// </summary>
        public static void Chosen(string key, string featureKey, string oldRule, string street, int number, string address, string noticeKey, string what, IModLogger logger)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || string.IsNullOrEmpty(street))
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData.TryGetValue(key, out string kept);
            bool ran = !string.IsNullOrEmpty(featureKey) && save.modData.ContainsKey(featureKey);
            string now = CityReserves.Mark(street, number);
            if (CityReserves.TellMoved(kept, now, ran, oldRule))
            {
                Meta.Dev.Always(logger, what + " has moved from " + (string.IsNullOrEmpty(kept) ? oldRule : kept).Replace('|', ' ') + " to " + address + " since the save was made; the player is told once.");
                Notifications.Show(NotificationType.Info, noticeKey, new Dictionary<string, string>(1) { { "address", address } }, 12f);
            }
            save.modData[key] = now;
        }
    }
}
