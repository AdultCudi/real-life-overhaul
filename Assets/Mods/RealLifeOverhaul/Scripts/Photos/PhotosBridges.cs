using System;
using UnityEngine;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// What the Camera and the Gallery ask of the rest of the mod (Wiring/PhotosLinks fills each in). Each is optional:
    /// without one the apps still work, and say so where the player would miss it.
    /// </summary>
    public static class PhotosBridges
    {
        /// <summary>
        /// A photo shared to Flare: its full path and its small copy's (Core/PhotoThumbs.FileOf). Flare opens on its compose
        /// sheet with the photo attached (FlareDirector.SharePhoto). False if Flare is not running. Default: none (the Gallery
        /// says Flare is off).
        /// </summary>
        public static Func<string, string, bool> ShareToFlare;

        /// <summary>Whether Flare is running now (the Gallery shows "Share to Flare" only then). Default: no.</summary>
        public static Func<bool> FlareRunning;

        /// <summary>The camera's shutter sound (Leisure/Sound, photo mode's own). Default: silence.</summary>
        public static Action Shutter;

        /// <summary>
        /// The phone's wallpaper set to a picture, or (null) put back to the phone's own. False if the phone has no wallpaper
        /// the mod can set (the modern phone look is off). Default: none (the Gallery says so and sets nothing).
        /// </summary>
        public static Func<Texture, bool> Wallpaper;

        /// <summary>Whether the phone has a wallpaper the mod can set now (Phone/PhoneSkin applied). Default: no.</summary>
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
