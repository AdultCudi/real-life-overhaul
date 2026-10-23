using System;
using UnityEngine;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// What the Camera and the Gallery ask of the rest of the mod, filled in by Wiring/PhotosLinks. Each is optional: without
    /// one the apps still work and say so where it matters.
    /// </summary>
    public static class PhotosBridges
    {
        /// <summary>Opens Flare's compose sheet with a photo and its small copy attached; false if Flare is not running.</summary>
        public static Func<string, string, bool> ShareToFlare;

        public static Func<bool> FlareRunning;

        public static Action Shutter;

        /// <summary>Sets the phone's wallpaper, or with null puts back the phone's own; false if the phone has none the mod can set.</summary>
        public static Func<Texture, bool> Wallpaper;

        public static Func<bool> HasWallpaper;

        internal static void Reset()
        {
            ShareToFlare = null;
            FlareRunning = null;
            Shutter = null;
            Wallpaper = null;
            HasWallpaper = null;
        }
    }
}
