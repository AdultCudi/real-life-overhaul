using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's own lettering, for the mod's lettering in the world (the signs on a station's frontage, a hotel's name, a bus stop's
    /// bay). It is the font of the first text the game's interface has, looked up once and kept; looked up again only if the
    /// interface was rebuilt (a new city) and the old font asset went with it. Nothing is made and nothing runs per frame.
    /// </summary>
    public static class GameFont
    {
        private static TMP_FontAsset _font;

        /// <summary>The game's font asset; null if the interface has no text yet (callers then keep TextMeshPro's default).</summary>
        public static TMP_FontAsset Asset
        {
            get
            {
                if (_font == null)
                {
                    var sample = Object.FindObjectOfType<TextMeshProUGUI>();
                    _font = sample != null ? sample.font : null;
                }
                return _font;
            }
        }
    }
}
