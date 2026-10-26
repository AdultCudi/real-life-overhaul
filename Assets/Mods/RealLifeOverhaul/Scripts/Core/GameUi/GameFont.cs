using BAModAPI;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's own font, for the mod's lettering in the world and its panels. It is found once by a scene search while
    /// the screen is black (<see cref="Refind"/>), and its name is kept. If the font held goes (Unity destroys it with the
    /// scene it came from), it is found again by that name among the assets in memory, which is not a scene search and
    /// costs under a millisecond, so no panel opening in play ever searches the scene (a player's session showed 97 ms,
    /// a 139 ms frame). Without a name yet, play gets null (callers keep TextMeshPro's default) until the next black
    /// screen. Costs nothing a frame: a null test, and at most one asset look until the next load.
    /// </summary>
    public static class GameFont
    {
        public static IModLogger Logger;

        private static TMP_FontAsset _font;
        private static string _name;        // the game's UI font by name, kept from the scene search so the asset look can find it again
        private static bool _none;          // searched behind a black screen, and the interface had no text: not looked for again until a load
        private static bool _lost;          // looked for by name in memory and not there: not looked for again until a load

        /// <summary>The game's font asset; null while it is not known (callers keep TextMeshPro's default).</summary>
        public static TMP_FontAsset Asset
        {
            get
            {
                if (_font == null && !_none)
                {
                    FromMemory();
                    if (_font == null && SceneFinds.Black)
                    {
                        FromScene();   // the last resort, and only behind a black screen
                    }
                }
                return _font;
            }
        }

        /// <summary>
        /// Behind the loading screen: the held "none" and "lost" forgotten and the font looked for if it is not held. False
        /// while the interface has no text, so the loading screen's work tries again behind the next black screen.
        /// </summary>
        public static bool Refind()
        {
            _none = false;
            _lost = false;
            if (_font == null)
            {
                FromMemory();
                if (_font == null)
                {
                    FromScene();
                }
            }
            return _font != null;
        }

        // The font of the name kept, among the assets in memory: no scene object is searched.
        private static void FromMemory()
        {
            if (_name == null || _lost)
            {
                return;
            }
            foreach (TMP_FontAsset font in SceneFinds.InMemory<TMP_FontAsset>("game UI: the game's font being looked for among the assets by its name"))
            {
                if (font != null && font.name == _name)
                {
                    _font = font;
                    return;
                }
            }
            _lost = true;
        }

        // The one scene search: a text of the game's interface, whose font is kept with its name. Callers: behind a black screen.
        private static void FromScene()
        {
            var sample = SceneFinds.Any(typeof(TextMeshProUGUI), "game UI: a text of the game's interface being looked for (its font)") as TextMeshProUGUI;
            _font = sample != null ? sample.font : null;
            _none = _font == null;
            if (_font != null && _font.name != _name)
            {
                _name = _font.name;
                _lost = false;
                Logger?.Info("Game UI: the game's interface font is '" + _name + "'; if it goes it is found again by that name among the assets, not by a scene search.");
            }
        }
    }
}
