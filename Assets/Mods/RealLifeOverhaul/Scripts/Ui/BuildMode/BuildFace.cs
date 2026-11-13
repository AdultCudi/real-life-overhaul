using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Build mode's kit face: the panels over the open city map as a screen of the kit (UiBuildPanels) on the HUD host's modal piece, pointer only, as the map's choice of
    /// a place is (MapKits). BuildMode stays the logic: the state machine, the facts and every action. This writes what BuildMode says (the same calls its old panels took,
    /// IBuildBar and the rest) into one model and gives each press back as the row ids the old panels used. The map and the camera stay the game's: the piece takes the
    /// mouse over its own targets and no key. Cost: a layout when BuildMode writes; nothing a frame.
    /// </summary>
    internal sealed partial class BuildFace : IUiScreen, IUiTyping, IUiPointing, IBuildFace
    {
        public const string ScreenId = "build_mode";

        private static readonly UiIcon[] ToolIcons = { UiIcon.MapPin, UiIcon.PaintBrush, UiIcon.Building, UiIcon.Trash, UiIcon.Handshake, UiIcon.Wrench };
        private static readonly UiIcon[] ViewIcons = { UiIcon.Eye, UiIcon.Grid, UiIcon.Coin, UiIcon.People, UiIcon.Hammer, UiIcon.Wallet, UiIcon.Car };

        private readonly BuildFaceCalls _calls;
        private readonly UiBuildModel _m = new UiBuildModel();
        private readonly string[] _toolWhy = new string[(int)BuildTool.Count];
        private readonly string[] _toolNames = new string[(int)BuildTool.Count];
        private readonly string[] _toolTips = new string[(int)BuildTool.Count];
        private readonly string[] _viewTips = new string[(int)BuildView.Count];
        private readonly SheetWriter _sheet;
        private readonly LegendWriter _legend;
        private string _hint, _hintMore, _pointed;
        private bool _dirty = true, _shown, _gone;
        private UiHost _host;
        private BuildTool _tool;
        private int[] _counts = new int[(int)BuildUse.Count];
        private int _use;
        private TourStep _tourStep = TourStep.Done;

        private BuildFace(BuildFaceCalls calls)
        {
            _calls = calls;
            _sheet = new SheetWriter(this);
            _legend = new LegendWriter(this);
            _m.Tools.Tools = new UiTool[(int)BuildTool.Count];
            for (int i = 0; i < ToolIcons.Length; i++)
            {
                _m.Tools.Tools[i].Icon = ToolIcons[i];
            }
            _m.ViewCount = (int)BuildView.Count;
            for (int i = 0; i < ViewIcons.Length && i < _m.ViewIcons.Length; i++)
            {
                _m.ViewIcons[i] = ViewIcons[i];
            }
            _m.Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") } };
            _m.PromptCount = 1;
        }

        /// <summary>The face Build mode asks for, or null where the kit cannot stand over the map.</summary>
        internal static IBuildBar Make(BuildFaceCalls calls) => new BuildFace(calls);

        /// <summary>The panel's box in the layout's px for what stands near the left to keep clear of, while it is up; false otherwise.</summary>
        internal static bool Showing => _current != null && _current._shown;

        private static BuildFace _current;

        /// <summary>The panels' model while the face is up, for what stands near them to keep clear.</summary>
        internal static UiBuildModel Current => _current?._m;

        /// <summary>Build mode left, as its leave button does (the interface going away under it).</summary>
        internal static void Leave() => _current?._calls.Leave?.Invoke();

        // The face's piece failed under it: Build mode is left as when the interface goes, and opens on its old panels until the next city.
        private static void Lost() => UiDirector.PieceLost(UiDirector.BuildPiece, "its face failed");

        public string Id => ScreenId;

        public bool Dirty => _dirty;

        // ------------------------------------------------------------------ the screen

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _m.Hint = _hint;
            _m.HintMore = !string.IsNullOrEmpty(_pointed) ? _pointed : _hintMore;
            UiBuildPanels.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (_gone)
            {
                return false;
            }
            switch (command)
            {
                case UiToolRow.ToolCommand:
                    if (arg >= 0 && arg < _toolWhy.Length && _toolWhy[arg] != null)
                    {
                        Notifications.Show(NotificationType.Info, "reallife_ui_build_tool_off", new Dictionary<string, string> { { "reason", _toolWhy[arg] } }, 5f, null, null, true, false);
                        return true;
                    }
                    _calls.Tool?.Invoke(arg);
                    return true;
                case UiBuildPanels.ViewCommand:
                    _calls.View?.Invoke(arg);
                    return true;
                case UiBuildPanels.RowCommand:
                    _calls.Row?.Invoke(arg);
                    return true;
                case UiBuildPanels.LeaveCommand:
                    _calls.Leave?.Invoke();
                    return true;
                case UiBuildPanels.TabCommand:
                    _calls.Use?.Invoke(arg);
                    return true;
                case UiBuildPanels.FavouritesCommand:
                    if (_m.Catalogue != null)
                    {
                        _m.Catalogue.FavouritesOnly = !_m.Catalogue.FavouritesOnly;
                        _m.Catalogue.Scroll = 0f;
                        _dirty = true;
                    }
                    return true;
                case UiBuildPanels.CardCommand:
                    _calls.Card?.Invoke(arg);
                    return true;
                case UiBuildPanels.StarCommand:
                    _calls.Favourite?.Invoke(arg);
                    return true;
                case UiBuildPanels.PlaceCommand:
                    _calls.Place?.Invoke();
                    return true;
                case UiBuildPanels.BackCommand:
                    _calls.Back?.Invoke();
                    return true;
                case UiBuildPanels.TourNextCommand:
                    Next();
                    return true;
                case UiBuildPanels.TourSkipCommand:
                    Skip();
                    return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        /// <summary>The mouse over a tool says what the tool is in the hint's second line, while it rests there.</summary>
        public void Pointed(int command, int arg)
        {
            string now = command == UiToolRow.ToolCommand && arg >= 0 && arg < _toolNames.Length ? _toolNames[arg] + ": " + (_toolWhy[arg] ?? _toolTips[arg]) : null;
            if (!string.Equals(now, _pointed, StringComparison.Ordinal))
            {
                _pointed = now;
                _dirty = true;
            }
        }

        // Esc is Build mode's own (it steps out of the tour, the window and the picks before the map closes): the piece takes no key.
        public bool Back() => false;

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        // Focus wants a row of cards beyond those in view: the window scrolls to it.
        public void Reveal(int list, int index)
        {
            if (_m.Catalogue == null || index < 0)
            {
                return;
            }
            float step = UiBuildPanels.CardHeight + UiTokens.S2, top = _m.Catalogue.Scroll, row = index * step;
            _m.Catalogue.Scroll = row < top ? row : row + step > top + UiBuildPanels.CardRows * step ? row - (UiBuildPanels.CardRows - 1) * step : top;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            if (_m.Catalogue == null)
            {
                return;
            }
            float step = UiBuildPanels.CardHeight + UiTokens.S2;
            _m.Catalogue.Scroll = Math.Max(0f, _m.Catalogue.Scroll - notches * step);
            _dirty = true;
        }

        // ------------------------------------------------------------------ typing: the catalogue's search

        public int TypingCommand => UiBuildPanels.SearchCommand;

        public string TypingText => _m.Catalogue != null ? _m.Catalogue.Search : string.Empty;

        public void Typed(string text)
        {
            if (_m.Catalogue != null)
            {
                _m.Catalogue.Search = text ?? string.Empty;
                _m.Catalogue.Scroll = 0f;
                _dirty = true;
            }
        }

        // ------------------------------------------------------------------ IBuildBar

        public bool Alive => !_gone;

        public bool Shown => _shown;

        IBuildSheet IBuildBar.Sheet => _sheet;

        IBuildLegend IBuildBar.Legend => _legend;

        public void Show(bool shown)
        {
            if (shown == _shown || _gone)
            {
                return;
            }
            HudHost hud = HudHost.Instance ?? HudHost.Ensure();
            if (shown)
            {
                if (hud == null)
                {
                    return;
                }
                _squaresUp = true;   // before the first layout, which the modal makes at once
                Pictures();
                _host = hud.Modal(this, UiText.T("reallife_bld_mode_title"), UiIcon.Building, Lost);
                _host.PointerOnly = true;
                HudStatusGame.ToolRow = _m.Tools;
                _current = this;
                _shown = true;
            }
            else
            {
                _shown = false;
                HudStatusGame.ToolRow = null;
                if (ReferenceEquals(_current, this))
                {
                    _current = null;
                    _squaresUp = false;
                }
                if (hud != null && hud.ModalHost != null && ReferenceEquals(hud.ModalHost.Screen, this))
                {
                    hud.EndModal();
                }
                _m.Catalogue = null;
                _m.Tour = null;
                _tourStep = TourStep.Done;
            }
            _dirty = true;
        }

        public void Destroy()
        {
            Show(false);
            _gone = true;
            DropSquares();
        }

        public void ReadHud()
        {
        }

        public void Tools(BuildTool inUse, string[] names, string[] lines, string[] why)
        {
            _tool = inUse;
            UiToolRowModel row = _m.Tools;
            row.Tool = (int)inUse;
            for (int t = 0; t < row.Tools.Length && t < names.Length; t++)
            {
                row.Tools[t].Name = names[t];
                _toolNames[t] = names[t];
                _toolTips[t] = lines[t];
                _toolWhy[t] = why[t];
            }
            _m.SheetIcon = ToolIcons[Math.Max(0, Math.Min(ToolIcons.Length - 1, (int)inUse))];
            _dirty = true;
        }

        public void Views(BuildView shown, string[] names, string[] lines)
        {
            _m.View = (int)shown;
            for (int v = 0; v < _m.ViewCount && v < names.Length; v++)
            {
                _m.ViewNames[v] = names[v];
                _viewTips[v] = lines[v];
            }
            _m.ViewTip = lines[(int)shown];
            _m.Title = UiText.T("reallife_bld_mode_title");
            _m.DemandTitle = UiText.T("reallife_ui_build_wants");
            _m.LeaveLabel = UiText.T("reallife_ui_build_leave");
            _dirty = true;
        }

        public void Demand(int use, int value, Color colour, string title, string line)
        {
            if (use < 0 || use >= UiBuildModel.DemandMost)
            {
                return;
            }
            string words = BuildModeHintRules.DemandWords(value);
            _m.Demand[use] = new UiBuildDemand
            {
                Name = ConstructionText.T(BuildCatalogue.UseKeys[use]), Value = value, Rgb = Rgb(colour),
                Words = UiText.T(words == "reallife_bld_tip_demand_high" ? "reallife_ui_build_dem_high" : words == "reallife_bld_tip_demand_some" ? "reallife_ui_build_dem_some"
                    : words == "reallife_bld_tip_demand_low" ? "reallife_ui_build_dem_low" : "reallife_ui_build_dem_unknown"),
                Tone = value < 0 ? UiRole.Track : words == "reallife_bld_tip_demand_high" ? UiRole.Ok : words == "reallife_bld_tip_demand_some" ? UiRole.Accent : UiRole.Warn
            };
            _m.DemandCount = Math.Max(_m.DemandCount, use + 1);
            _dirty = true;
        }

        public void Hint(int step, string next, string more)
        {
            _m.Step = step;
            _hint = next;
            _hintMore = more;
            _dirty = true;
        }

        public void Place() => _dirty = true;

        public string Overlap() => null;   // the kit's layouts are checked offline at every frame (UiScreenChecks)

        public bool CoversCentre(out string what)
        {
            what = null;
            return false;
        }

        public bool ToolGreyed(BuildTool tool, out string why)
        {
            why = _toolWhy[(int)tool];
            return why != null;
        }

        public float ToolbarTop() => float.NaN;

        public string HintWords => _hint;

        public string HintMoreWords => _hintMore;

        /// <summary>What the panels hold now, in a line, for the tests.</summary>
        public string Describe() => "tool " + _tool + ", view " + _m.View + ", " + _m.KeyCount + " legend entries, " + _m.DemandCount + " demand rows, " + _m.LineCount + " lines, "
            + (_m.Catalogue != null ? _m.Catalogue.CardCount + " designs" : "no design window");

        /// <summary>The model, for the tests.</summary>
        internal UiBuildModel Model => _m;

        private static int Rgb(Color c) => (Mathf.RoundToInt(c.r * 255f) << 16) | (Mathf.RoundToInt(c.g * 255f) << 8) | Mathf.RoundToInt(c.b * 255f);

        // ------------------------------------------------------------------ pictures: a colour's swatch and the game's picture of a use

        private static bool _picturesOn;
        private static bool _squaresUp;   // Build mode is up: its own squares answer the swatches of colour, and go with its face
        private static readonly Dictionary<int, Sprite> Squares = new Dictionary<int, Sprite>(16);
        private const string UsePicture = "build:use:";

        // The keys this face's pictures use, answered here: a use's picture always, a colour's swatch only while Build mode is up, since
        // its squares go with the face (the world's and the buying screens' answer the swatches otherwise); any other is another part's.
        private static void Pictures()
        {
            if (_picturesOn)
            {
                return;
            }
            _picturesOn = true;
            UiPictures.Answer(key => key != null && (key.StartsWith(UsePicture, StringComparison.Ordinal) || (_squaresUp && UiDraw.SwatchColour(key) >= 0)) ? Find(key) : null);
        }

        // The colour squares made for the legend and the zones, given back with the face: they are made again, a handful, the next time Build mode opens.
        private static void DropSquares()
        {
            foreach (Sprite sprite in Squares.Values)
            {
                if (sprite != null)
                {
                    Texture2D texture = sprite.texture;
                    UnityEngine.Object.Destroy(sprite);
                    UnityEngine.Object.Destroy(texture);
                }
            }
            Squares.Clear();
        }

        private static Sprite Find(string key)
        {
            if (key.StartsWith(UsePicture, StringComparison.Ordinal))
            {
                // A name of the kit's list, so the picture check traces every name asked for to the ones wanted behind the loading screen.
                int named = int.TryParse(key.Substring(UsePicture.Length), out int use) ? Array.IndexOf(BuildModeKit.Names, BuildModeCatalogue.Picture(use)) : -1;
                return named >= 0 ? GameSprites.Get(BuildModeKit.Names[named]) : null;
            }
            int rgb = UiDraw.SwatchColour(key);
            if (rgb < 0)
            {
                return null;
            }
            if (Squares.TryGetValue(rgb, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "RealLife build swatch", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            var c = new Color32((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255), 255);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Squares[rgb] = sprite;
            return sprite;
        }
    }
}
