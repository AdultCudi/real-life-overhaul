using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>What the map check saw of the player's camera, set against the one the map opened over.</summary>
    public enum MapCameraSeen
    {
        NotKnown = 0,   // no camera kept as the map opened (the record came on while it was open), or another camera now
        Same = 1,       // its layers, far plane, draw distances by layer and their shape as before the map
        Differs = 2     // the same camera, not as before the map
    }

    /// <summary>
    /// The game-free part of the map check (Diagnostics/SessionWatch.Map; docs/standards/performance.md, RLO-PERF-022),
    /// checked by NavCheck (MapCheckCheck.cs): which camera an opening of the map is set against, and the verdict.
    /// The player's camera is part of the verdict: a camera not as before the map fails it. An opening of the map before
    /// the last one's camera was back down keeps the camera kept at the first opening: the game shuts the map in a
    /// coroutine that an opening stops (its word that the camera is back, onCityMapClosed, then never comes), and until
    /// that word the draw distances are the ones set for the way back down, not the street's.
    /// </summary>
    public static class MapCheckRules
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Whether an opening of the map takes the player's camera as it is now: not when the map is opened again while the
        /// last opening is still shutting (its camera on its way back down, or play not back yet), when the camera kept at
        /// the first opening stands.
        /// </summary>
        public static bool TakesCamera(bool stillShutting) => !stillShutting;

        /// <summary>True when the check passes: every part back within the time wanted, none holding anything, the camera not changed.</summary>
        public static bool Passes(bool back, int lingering, float within, float wanted, MapCameraSeen camera)
        {
            return back && lingering == 0 && within <= wanted && camera != MapCameraSeen.Differs;
        }

        /// <summary>
        /// The verdict at the end of the check's line. cut: the map was opened again before the check was over, so a part
        /// still on its way back is not judged.
        /// </summary>
        public static void Verdict(StringBuilder line, bool cut, bool back, int lingering, float within, float wanted, MapCameraSeen camera)
        {
            if (cut && !back)
            {
                line.Append("not judged, the map was opened again first");
                return;
            }
            if (Passes(back, lingering, within, wanted, camera))
            {
                line.Append("restored, 0 lingering: PASS");
                return;
            }
            line.Append("FAIL");
            if (!back || lingering > 0)
            {
                line.Append(", ").Append(lingering).Append(" lingering");
            }
            else if (within > wanted)
            {
                line.Append(", restored only after ").Append(within.ToString("F1", Inv)).Append(" s");
            }
            if (camera == MapCameraSeen.Differs)
            {
                line.Append(", the player's camera not as before the map");
            }
        }
    }
}
