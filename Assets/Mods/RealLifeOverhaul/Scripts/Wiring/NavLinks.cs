using RealLifeOverhaul.Nav;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The minimap and the GPS with the rest of the mod (Navigation/NavBridges):
        ///
        ///   HudHeld          a scene of the mod's has the screen and the game's HUD is not what the player looks at:
        ///                    custody (Law), a bus ride (Transport), photo mode (Leisure). The minimap is hidden.
        ///   GuideToStation   Law's own "guide me to the nearest police station", for the GPS's test button
        ///
        /// Everything else the minimap shows of the mod it reads from Core/MapPlaces (the kinds of place whose row on
        /// the city map is ticked), and everything that sets a destination does so through the game's own marker, so
        /// no feature needs to know of the GPS.
        /// </summary>
        public static class NavLinks
        {
            internal static void Connect()
            {
                NavBridges.HudHeld = () => Law.Custody.Active || TransitDirector.Riding || Leisure.PhotoMode.Active;
                NavBridges.GuideToStation = () => Law.LawDirector.Instance != null && Settings.LawPrecincts && Law.LawDirector.Instance.GuideToNearest() != null;
            }

            internal static void Disconnect()
            {
                NavBridges.HudHeld = null;
                NavBridges.GuideToStation = null;
            }
        }
    }
}
