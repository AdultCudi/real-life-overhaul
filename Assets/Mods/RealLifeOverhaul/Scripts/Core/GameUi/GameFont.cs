using TMPro;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's own font, for the mod's lettering in the world and its panels: looked up once behind the loading screen
    /// (<see cref="Refind"/>), again through SceneFinds only if the font found has gone. A look that found no text is
    /// kept as "none" until the next load, so asking costs one test, never a search. Costs nothing a frame.
    /// </summary>
    public static class GameFont
    {
        private static TMP_FontAsset _font;
        private static bool _none;   // looked, and the interface had no text: not looked for again until a load

        /// <summary>The game's font asset; null while the interface has no text (callers keep TextMeshPro's default).</summary>
        public static TMP_FontAsset Asset
        {
            get
            {
                if (_font == null && !_none)
                {
                    var sample = SceneFinds.Any(typeof(TextMeshProUGUI), "game UI: a text of the game's interface being looked for (its font)") as TextMeshProUGUI;
                    _font = sample != null ? sample.font : null;
                    _none = _font == null;
                }
                return _font;
            }
        }

        /// <summary>
        /// Behind the loading screen: "none" forgotten and the font looked for if it is not held. False while the interface
        /// has no text, so the loading screen's work tries again behind the next black screen.
        /// </summary>
        public static bool Refind()
        {
            _none = false;
            return Asset != null;
        }
    }
}
