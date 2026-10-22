using RealLifeOverhaul.Nav;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The minimap and the GPS (NavBridges): HudHeld hides the minimap while a scene of the mod's has the screen;
        /// GuideToStation is Law's route to the nearest police station, for the GPS's test button.
        /// </summary>
        public static class NavLinks
        {
            internal static void Connect()
            {
                NavBridges.HudHeld = () => Law.Custody.Active || TransitDirector.Riding || Leisure.PhotoMode.Active || Photos.CameraMode.Active;
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
