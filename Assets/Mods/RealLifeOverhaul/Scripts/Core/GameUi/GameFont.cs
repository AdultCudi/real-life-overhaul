using TMPro;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's own lettering for the mod's lettering in the world (a station's frontage, a hotel's name, a bus stop's
    /// bay) and its panels (Meta/MetaUi): the font of a text of the game's interface, looked up once behind the loading
    /// screen and kept; looked up again through SceneFinds (so a look in play is counted and said) only if the interface
    /// was rebuilt and the font went with it. Costs nothing a frame.
    /// </summary>
    public static class GameFont
    {
        private static TMP_FontAsset _font;

        /// <summary>The game's font asset; null while the interface has no text (callers keep TextMeshPro's default).</summary>
        public static TMP_FontAsset Asset
        {
            get
            {
                if (_font == null)
                {
                    var sample = SceneFinds.Any(typeof(TextMeshProUGUI), "game UI: a text of the game's interface being looked for (its font)") as TextMeshProUGUI;
                    _font = sample != null ? sample.font : null;
                }
                return _font;
            }
        }
    }
}
