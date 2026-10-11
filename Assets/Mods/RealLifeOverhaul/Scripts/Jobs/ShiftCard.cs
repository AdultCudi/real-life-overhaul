using System;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The shift's figures on the HUD, in the game's own job panel: a copy of
    /// <c>Canvases/PlayerHUD/CurrentJobUI/Panel</c> (the dark box with a pale headline that the game shows at the
    /// top right while the player has its own employer job), made while it sleeps (GameUi/UiClone), with the
    /// game's script and its two buttons taken off. The headline says the job; under it are five lines in the
    /// panel's own lettering, written by whoever owns the card and only when a line's words change.
    ///
    /// It stands where the game's panel stands, and under it while the game's own is showing (the player also has
    /// the game's job). It is looked at, never clicked: nothing on it takes the pointer from the world.
    ///
    /// Made once per city the first time a shift needs it, kept switched off between shifts, and destroyed with
    /// the HUD it hangs on. If the game's panel is not where it was, <see cref="Make"/> says so in one
    /// "Game UI:" line and returns null: the shift is then worked with the panel over the cart alone.
    /// </summary>
    public sealed class ShiftCard
    {
        public const int Lines = 5;

        private const float FirstLine = -132.1f;    // the game's own first line under the headline...
        private const float LineStep = 42f;         // ...and the step between its lines
        private const float Foot = 36f;             // under the last line
        private const float GameHeight = 460f;      // the game's own panel, when it is showing above this one
        private const float Gap = 16f;

        private readonly GameObject _panel;
        private readonly RectTransform _rect;
        private readonly TMP_Text _title;
        private readonly TMP_Text[] _line = new TMP_Text[Lines];
        private readonly string[] _words = new string[Lines];
        private readonly Color32[] _colour = new Color32[Lines];
        private readonly GameObject _gamePanel;
        private readonly Vector2 _home;
        private string _titleWords;
        private bool _shown;
        private bool _under;
        private GameBar _bar;
        private bool _barTried;

        private ShiftCard(GameObject panel, TMP_Text title, TMP_Text[] lines, GameObject gamePanel)
        {
            _panel = panel;
            _rect = (RectTransform)panel.transform;
            _title = title;
            _gamePanel = gamePanel;
            _home = _rect.anchoredPosition;
            for (int i = 0; i < Lines; i++)
            {
                _line[i] = lines[i];
                _colour[i] = new Color32(255, 255, 255, 255);
            }
        }

        /// <summary>True while the copy exists (the HUD is rebuilt with the city).</summary>
        public bool Alive => _panel != null;

        /// <summary>Copies the game's job panel. Null, with one log line, if it cannot be done.</summary>
        public static ShiftCard Make(IModLogger logger)
        {
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                UI.CurrentJob.CurrentJob game = uis != null && uis.playerHUD != null ? uis.playerHUD.currentJobUI : null;
                if (game == null || game.panel == null)
                {
                    UiClone.Fallback("the game's job panel was not found: a shift's figures are shown on the panel over the place of work only");
                    return null;
                }
                SessionWatch.Mark("jobs: the shift's panel (a copy of the game's job panel)");
                Transform dormant = UiClone.Dormant(game.transform, "RealLifeOverhaul Shift Card (asleep)");
                GameObject panel = UiClone.Asleep(game.panel, dormant, "RealLifeOverhaul Shift Card");
                if (panel == null)
                {
                    UnityEngine.Object.Destroy(dormant.gameObject);
                    return null;
                }
                Transform root = panel.transform;
                Transform headline = UiClone.Need(root, "Headline/Text (TMP)", "the job panel's headline");
                Transform first = UiClone.Need(root, "BusinessName", "the job panel's first line");
                Transform plain = UiClone.Need(root, "Address", "the job panel's second line");
                if (headline == null || first == null || plain == null)
                {
                    UnityEngine.Object.Destroy(dormant.gameObject);
                    return null;
                }
                UiClone.KeepOnly(root, "Headline", "BusinessName", "Address");
                UiClone.StripGameScripts(panel, null);
                var lines = new TMP_Text[Lines];
                lines[0] = first.GetComponent<TMP_Text>();
                lines[1] = plain.GetComponent<TMP_Text>();
                for (int i = 2; i < Lines; i++)
                {
                    GameObject more = UnityEngine.Object.Instantiate(plain.gameObject, root, false);
                    more.name = "Line " + (i + 1);
                    lines[i] = more.GetComponent<TMP_Text>();
                }
                TMP_Text title = headline.GetComponent<TMP_Text>();
                if (title == null || lines[0] == null || lines[1] == null)
                {
                    UiClone.Fallback("the game's job panel has no texts where they were: a shift's figures are shown on the panel over the place of work only");
                    UnityEngine.Object.Destroy(dormant.gameObject);
                    return null;
                }
                for (int i = 0; i < Lines; i++)
                {
                    var rect = (RectTransform)lines[i].transform;
                    Vector2 at = rect.anchoredPosition;
                    at.y = FirstLine - LineStep * i;
                    rect.anchoredPosition = at;
                    lines[i].enableAutoSizing = true;      // a long line is set smaller, never cut off or wrapped
                    lines[i].fontSizeMax = lines[i].fontSize;
                    lines[i].fontSizeMin = 18f;
                    UiClone.Words(lines[i], string.Empty);
                }
                UiClone.Words(title, string.Empty);
                // Looked at, never clicked: nothing on it may take the pointer from the world under it.
                foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
                var box = (RectTransform)root;
                Vector2 size = box.sizeDelta;
                size.y = -FirstLine + LineStep * (Lines - 1) + Foot;
                box.sizeDelta = size;
                panel.AddComponent<Canvas>();   // batched by itself: a line written here does not rebuild the HUD's canvas
                var card = new ShiftCard(panel, title, lines, game.panel);
                panel.SetActive(false);
                root.SetParent(game.transform, false);
                root.SetAsFirstSibling();
                UnityEngine.Object.Destroy(dormant.gameObject);
                return card;
            }
            catch (Exception e)
            {
                logger?.Error(e);
                UiClone.Fallback("the game's job panel could not be copied: a shift's figures are shown on the panel over the place of work only");
                return null;
            }
        }

        /// <summary>The headline: the job, as final words. Written only when it differs.</summary>
        public void Title(string words)
        {
            if (_panel != null && !Same(words, _titleWords))
            {
                _titleWords = words;
                UiClone.Words(_title, words);
            }
        }

        /// <summary>One of the five lines, as final words (null or empty leaves it blank). Written only when the words or the colour differ.</summary>
        public void Line(int line, string words, Color32 colour)
        {
            if (_panel == null || line < 0 || line >= Lines)
            {
                return;
            }
            if (!Same(words, _words[line]))
            {
                _words[line] = words;
                UiClone.Words(_line[line], words);
            }
            Color32 was = _colour[line];
            if (was.r != colour.r || was.g != colour.g || was.b != colour.b || was.a != colour.a)
            {
                _colour[line] = colour;
                _line[line].color = colour;
            }
        }

        /// <summary>
        /// One of the game's bars (<see cref="GameBar"/>) on the card, in the place of its last two lines, which the
        /// owner then leaves blank. Made the first time it is asked for and kept, switched off, with the card; null
        /// (one "Game UI:" line, once) if the game's top bar could not be copied. The owner shows, hides and fills it.
        /// </summary>
        public GameBar Bar()
        {
            if (_bar == null && !_barTried && _panel != null)
            {
                _barTried = true;
                _bar = GameBar.Make(_rect, "RealLifeOverhaul Card Bar");
                if (_bar != null)
                {
                    // Midway between the last two lines, in the middle of the card: measured from the lines themselves.
                    var upper = (RectTransform)_line[Lines - 2].transform;
                    var lower = (RectTransform)_line[Lines - 1].transform;
                    float y = (upper.localPosition.y + upper.rect.center.y + lower.localPosition.y + lower.rect.center.y) * 0.5f;
                    _bar.Rect.localPosition = new Vector3(_rect.rect.center.x, y, 0f);
                }
            }
            return _bar != null && _bar.Alive ? _bar : null;
        }

        /// <summary>Shows or hides the card. Two comparisons when nothing changes: fit for every frame.</summary>
        public void Show(bool on)
        {
            if (_panel == null)
            {
                return;
            }
            if (on)
            {
                // Under the game's own job panel while that is showing.
                bool under = _gamePanel != null && _gamePanel.activeSelf;
                if (under != _under || !_shown)
                {
                    _under = under;
                    _rect.anchoredPosition = under ? new Vector2(_home.x, _home.y - GameHeight - Gap) : _home;
                }
            }
            if (on != _shown)
            {
                _shown = on;
                _panel.SetActive(on);
            }
        }

        public void Destroy()
        {
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
            }
        }

        private static bool Same(string a, string b)
        {
            return ReferenceEquals(a, b) || string.Equals(a, b, StringComparison.Ordinal);
        }
    }
}
