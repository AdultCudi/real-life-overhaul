using Helpers;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The game's side of standing a handed-over building in play: where the player is, whether any of a ground is in the
    /// camera's picture, whether the screen is black. Nothing here runs a frame: asked once a second while a building waits,
    /// and at each black screen.
    /// </summary>
    internal static partial class LifeGame
    {
        /// <summary>True when any of the ground (x and z from, to) is in the main camera's picture, or the city map is open (it shows the whole city).</summary>
        public static bool InView(float x1, float z1, float x2, float z2)
        {
            var box = new Bounds(new Vector3((x1 + x2) * 0.5f, UptownLots.GroundY + StandRules.BoxHeight * 0.5f, (z1 + z2) * 0.5f),
                new Vector3(Mathf.Abs(x2 - x1) + 2f * NewBuildingsRules.Gap, StandRules.BoxHeight, Mathf.Abs(z2 - z1) + 2f * NewBuildingsRules.Gap));
            return CopyGame.InView(box);
        }

        /// <summary>True when the player stands on the ground (x and z from, to), a margin round it included.</summary>
        public static bool PlayerOn(float x1, float z1, float x2, float z2)
        {
            if (PlayerHelper.PlayerController == null)
            {
                return false;
            }
            Vector3 at = PlayerHelper.GetCityPosition();
            return StandRules.OnIt(x1, z1, x2, z2, at.x, at.z);
        }

        /// <summary>True while the screen is black: the loading screen, or the game's black screen at full strength (the fade at a door, its middle).</summary>
        public static bool Black => SceneFinds.Black;
    }
}
