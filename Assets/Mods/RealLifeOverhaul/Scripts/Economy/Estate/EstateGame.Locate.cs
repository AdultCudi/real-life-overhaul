using System.Collections.Generic;
using Streets;
using UI.Guiders;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The one place a building of the property business is shown to the player: the game's own guide to it (the destination), as the Planner sets one.</summary>
    internal static partial class EstateGame
    {
        /// <summary>Sets the game's destination to a building by its place; false with no save, no guide or no such address.</summary>
        public static bool Locate(string place)
        {
            Address address = AddressOf(place);
            if (address == null || SaveGameManager.Current == null || InstanceBehavior<GuidersManager>.Instance == null)
            {
                return false;
            }
            SaveGameManager.Current.customDestination = address;
            GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination);
            Notifications.Show(NotificationType.Success, "notification_destination_set", new Dictionary<string, string> { { "address", address.ToFormattedString() } });
            return true;
        }
    }
}
