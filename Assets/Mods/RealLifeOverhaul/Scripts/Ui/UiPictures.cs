using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Pictures the kit shows but never draws: another mod's own app icon, read from its button by the game file. A screen
    /// names one by a handle (an item's Value), and the renderer asks <see cref="Source"/> for it, so the layout and its
    /// checks stay game-free. A handle of 0 or less is none. Cost: a delegate call when a picture's slot is bound.
    /// </summary>
    public static class UiPictures
    {
        /// <summary>The picture of a handle; set by whoever holds the pictures (the phone's game file, an offline render).</summary>
        public static Func<int, Sprite> Source;

        /// <summary>The picture of a handle, or null for none (the slot then shows nothing over its tile).</summary>
        public static Sprite Of(int handle) => handle > 0 && Source != null ? Source(handle) : null;
    }
}
