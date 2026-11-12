using System;
using RealLifeOverhaul.Nav;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The minimap and the GPS (NavBridges): HudHeld hides the minimap while a scene of the mod's has the screen;
        /// GuideToStation is Law's route to the nearest police station, for the GPS's test button; KitHud is where the kit's
        /// HUD stands, for the minimap to keep clear of it and to be drawn over it.
        /// </summary>
        public static class NavLinks
        {
            internal static void Connect()
            {
                NavBridges.HudHeld = () => Law.Custody.Active || TransitDirector.Riding || Leisure.PhotoMode.Active || Photos.CameraMode.Active || Photos.CineDirector.Active;
                NavBridges.GuideToStation = () => Law.LawDirector.Instance != null && Settings.LawPrecincts && Law.LawDirector.Instance.GuideToNearest() != null;
                NavBridges.KitHud = KitHud;
            }

            // The kit's HUD for the minimap: its bar and left column, and the order its canvas takes over the HUD's.
            private static NavKitSpace KitHud()
            {
                try
                {
                    Ui.HudHost host = Ui.HudHost.Instance;
                    if (host == null || !host.Space(out float barTop, out bool hasColumn, out float columnRight, out float columnBottom, out float chipsLeft, out float chipsTop))
                    {
                        return default;
                    }
                    return new NavKitSpace
                    {
                        Up = true, BarTop = barTop, HasColumn = hasColumn, ColumnRight = columnRight, ColumnBottom = columnBottom, ChipsLeft = chipsLeft, ChipsTop = chipsTop,
                        Order = Ui.UiHostRules.MinimapOrder(host.CanvasOrder)
                    };
                }
                catch (Exception e)
                {
                    Fault("NavLinks.KitHud", e);
                    return default;
                }
            }

            internal static void Disconnect()
            {
                NavBridges.HudHeld = null;
                NavBridges.GuideToStation = null;
                NavBridges.KitHud = null;
            }
        }
    }
}
