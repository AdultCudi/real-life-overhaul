using RealLifeOverhaul.Map25;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// How the city map's tilt button works, in plain numbers: three states in a ring (by the camera's distance, flat, tilted) and the pitch each asks for. By
    /// distance is what the map did before the button: flat from afar and the game's own tilt from close. Nothing of the game in it, so the checks run it.
    /// </summary>
    public static class NavMapTilt
    {
        public const int Auto = 0, Flat = 1, Tilted = 2;

        /// <summary>The state the button's next press gives: by distance, flat, tilted, then by distance again. Anything else is the default.</summary>
        public static int Next(int mode) => Known(mode) == Auto ? Flat : mode == Flat ? Tilted : Auto;

        /// <summary>The state a saved number means: anything but flat or tilted is by distance.</summary>
        public static int Known(int mode) => mode == Flat || mode == Tilted ? mode : Auto;

        /// <summary>The camera's pitch in degrees for a state at a distance from its pivot in metres.</summary>
        public static float Pitch(int mode, float distance) => mode == Flat ? Map25View.FlatPitch : mode == Tilted ? Map25View.TiltPitch : Map25View.PitchFor(distance);

        /// <summary>The words the button's tile carries: the picture as it stands, so the player reads the state, not the action.</summary>
        public static string Words(int mode, float distance) => Pitch(mode, distance) >= Map25View.FlatPitch - 1f ? "2D" : "3D";
    }
}
