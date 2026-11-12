using RealLifeOverhaul.Construction;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Build mode's panels on the RLO interface, through its one switch (Construction's section, "build_mode"): Build mode asks <see cref="BuildMode.KitOn"/> when it opens
    /// and gets the kit's face while the interface, the HUD and the switch are all on, the old panels otherwise. A change of the switch takes hold at the next opening, so
    /// a player who flips it back mid-build is never left with half a face. Once a second while the interface is on (UiDirector).
    /// </summary>
    internal static class BuildKits
    {
        /// <summary>Puts the hooks in place (they cost nothing until Build mode opens).</summary>
        public static void Install()
        {
            BuildMode.KitOn = On;
            BuildMode.KitMake = BuildFace.Make;
        }

        /// <summary>
        /// The hooks taken away: Build mode opens on its old panels from now on; an open face is given back by closing Build mode (its map and time go back as on any leave).
        /// </summary>
        public static void Remove(string why)
        {
            BuildMode.KitOn = null;
            BuildMode.KitMake = null;
            if (BuildFace.Showing)
            {
                BuildFace.Leave();
            }
        }

        private static bool On() => UiDirector.On && UiScreenSwitches.On(BuildFace.ScreenId) && UiScreenSwitches.On("hud") && HudHost.Instance != null;
    }
}
