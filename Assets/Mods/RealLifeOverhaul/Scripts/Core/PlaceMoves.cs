using System.Collections.Generic;
using BAModAPI;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Tells the player once when a place of the mod's (the courthouse, City Utilities) has moved since the save was
    /// made: the save's modData keeps "street|number" under the place's key (<see cref="CityReserves.TellMoved"/>).
    /// Once per choice; nothing per frame.
    /// </summary>
    internal static class PlaceMoves
    {
        /// <summary>
        /// The place <paramref name="key"/> is now at a street and number: tells <paramref name="noticeKey"/> if it moved
        /// and keeps the new place. <paramref name="featureKey"/> is the feature's save line (present: it ran before).
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
