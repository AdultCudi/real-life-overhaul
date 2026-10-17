using System;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// Sightseeing's selfie in plain numbers: which way the player turns to have the landmark behind them, and how long
    /// it lasts. Game-free, so NavCheck runs it (SightseeingCheck.cs).
    /// </summary>
    public static class SightseeingRules
    {
        public const string SelfieClip = "selfie";       // CityAssets: the phone at arm's length, screen to the face
        public const string PhotoClip = "take_photo";    // played when the bundle has no selfie
        public const float TooNear = 1f;                 // metres on the flat: no way to turn from a point this close
        public const float ClipTime = 5.2f;              // seconds: the clip's 5 s and the fade back
        public const float NoClipTime = 2.5f;            // seconds, without a clip

        /// <summary>
        /// The unit direction (x, z) from the landmark through the player, so facing it puts the landmark behind them;
        /// false, and (0, 0), within <see cref="TooNear"/>.
        /// </summary>
        public static bool BackTo(float playerX, float playerZ, float landmarkX, float landmarkZ, out float x, out float z)
        {
            float dx = playerX - landmarkX, dz = playerZ - landmarkZ;
            float apart = (float)Math.Sqrt(dx * dx + dz * dz);
            if (!(apart >= TooNear))   // NaN too
            {
                x = z = 0f;
                return false;
            }
            x = dx / apart;
            z = dz / apart;
            return true;
        }

        public static float Lasts(bool clip) => clip ? ClipTime : NoClipTime;
    }
}
