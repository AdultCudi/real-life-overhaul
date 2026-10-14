using System;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// Sightseeing's selfie, in plain numbers: which way the player turns so that the landmark is behind them in the
    /// picture, which of the mod's clips is played and how long it all lasts. Nothing of the game or the engine in it, so
    /// every rule is also run in a console test (RealLifeOverhaul.Build/NavCheck/SightseeingCheck.cs).
    ///
    /// A landmark is a point (a subway station's, a building's door) the player stands within 35 to 70 m of. Their back
    /// is turned to that point: they face along the line from it through where they stand. Standing on the point itself
    /// (nearer than <see cref="TooNear"/>) there is no such line, and they stay as they stand.
    /// </summary>
    public static class SightseeingRules
    {
        public const string SelfieClip = "selfie";       // CityAssets: the phone at arm's length in the right hand, the screen to the face
        public const string PhotoClip = "take_photo";    // ...and the picture of what is ahead, both hands: if the bundle has no selfie
        public const float TooNear = 1f;                 // metres from the landmark's point, on the flat, inside which there is no way to turn from it
        public const float ClipTime = 5.2f;              // seconds the activity lasts with a clip: the clip's 5 s and the fade back
        public const float NoClipTime = 2.5f;            // ...and without one (the bundle not loaded): a moment's look

        /// <summary>
        /// The way to face, on the flat, to have the landmark behind: a unit direction (x, z) from the landmark's point
        /// through the player's. False, and (0, 0), when the player stands within <see cref="TooNear"/> of it.
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

        /// <summary>How long the activity lasts: with one of the clips playing, or without.</summary>
        public static float Lasts(bool clip)
        {
            return clip ? ClipTime : NoClipTime;
        }
    }
}
