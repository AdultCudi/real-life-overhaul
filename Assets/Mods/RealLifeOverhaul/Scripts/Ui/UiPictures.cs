using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where a picture item's key finds its sprite: the game's own pictures of goods, furniture, vehicles and buildings,
    /// which the game file that shows a catalogue sets. The layout keeps only the key and the picture's shape, so it stays
    /// game-free; the renderer asks here once per bind. Cost: one call a picture shown.
    /// </summary>
    public static class UiPictures
    {
        /// <summary>A key's sprite, or null when the game has none: the item then shows nothing.</summary>
        public static Func<string, Sprite> Find;
    }
}
