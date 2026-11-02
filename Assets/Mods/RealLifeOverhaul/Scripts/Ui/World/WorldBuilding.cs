using System;
using System.Collections.Generic;
using System.Reflection;
using Localizor;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UI.InGameUI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's building box (BuildingResume) on the RLO interface. While the pointer is over a building, the kit's card
    /// stands where the game put its box at the door: the business's name, its kind and neighbourhood, whether it is open,
    /// and the game's own hints (left click to enter, right click for options). Once the building is picked (the game shows
    /// its buttons), it is the info panel docked at the top left, after Cities: Skylines II's building panel: icon tabs for
    /// the building, the business in it and its owner, its address as the strip's title, the business's initials, name and
    /// state, the building's and the business's rows, and the game's buttons in its footer (BizMan or the taxi, set
    /// destination, the sign, and any another mod put beside them, Voogle Route's own). In the HUD's left column when the
    /// kit's HUD shows, so the objectives stand under it; on the world's canvas otherwise. Every word is read from the game's
    /// own labels after the game and other mods wrote them; every press is the game's own button. Cost a frame while it
    /// shows: a dozen reference comparisons and a move; nothing allocated.
    /// </summary>
    internal sealed class WorldBuilding
    {
        private static readonly FieldInfo Panel = GameContract.ResumePanel.Field;
        private const int Words = 14;   // the labels whose words the panel shows

        private readonly UiScene _cardScene = new UiScene();
        private readonly UiScene _dockScene = new UiScene();
        private readonly UiConceal _conceal = new UiConceal();
        private readonly UiPromptCardModel _card = new UiPromptCardModel { Actions = new UiPromptAction[2] };
        private readonly UiInfoModel _info = new UiInfoModel();
        private readonly string[] _seen = new string[Words];
        private readonly bool[] _seenOn = new bool[Words + 6];
        private readonly Component[] _labels = new Component[Words];
        private readonly List<Transform> _foreign = new List<Transform>(4);
        private readonly List<Button> _footer = new List<Button>(6);
        private UiView _cardView, _dockView;
        private float _scale;
        private BuildingResume _resume;
        private RectTransform _panel;
        private readonly Vector3[] _corners = new Vector3[4];
        private Canvas _canvas;
        private Button _main;                 // the footer's wide button: the taxi in taxi mode, else BizMan
        private bool _shown, _docked, _inHud, _off, _dirty = true;
        private int _seenKids = -1;
        private int _tab;
        private Func<int, int, bool> _chain, _was;
        private Vector2 _at = new Vector2(float.NaN, float.NaN);
        private string _state = "not looked at yet";

        public string State => _off ? _state : _docked ? (_inHud ? "docked in the HUD's left column" : "docked at the top left") : _shown ? "at the door" : _state;

        /// <summary>True while the panel stands docked at the top left (a building picked): a world panel then has the door to itself.</summary>
        public bool Docked => _docked;

        /// <summary>The models last read, for the tests.</summary>
        public UiPromptCardModel Card => _card;
        public UiInfoModel Info => _info;
        public bool Showing => _shown;

        public void Build(RectTransform rect, float scale)
        {
            _scale = scale;
            _cardView = new UiView(rect, "Building") { Scale = scale, Ring = false };
            _cardView.Root.gameObject.SetActive(false);
            _dockView = new UiView(rect, "Building panel") { Scale = scale, Ring = false };
            _dockView.PointerPressed = Guard.Wrap<int>(DockPressed);
            _dockView.PointerEntered = Guard.Wrap<int>(node => _dockView.SetHover(node));
            _dockView.PointerExited = Guard.Wrap<int>(node => _dockView.SetHover(-1));
            _dockView.Root.gameObject.SetActive(false);
            _chain = Press;
            GameUi.WorldPanel.ResumeElsewhere = () => Instance() != null && Instance()._docked;
        }

        private static WorldBuilding Instance() => WorldHost.Instance != null ? WorldHost.Instance.Building : null;

        public void Rescale(float scale)
        {
            _scale = scale;
            if (_cardView != null)
            {
                _cardView.Scale = scale;
                _dockView.Scale = scale;
            }
            _dirty = true;
        }

        /// <summary>A frame: the card at the door or the docked panel while the game shows its box and the switch is on.</summary>
        public void Frame(UiFrame frame, bool on)
        {
            if (_off || !Find())
            {
                return;
            }
            // A building in the street; the city map's own picks (a station, a taxi's stop, a building on the map) stay the game's box.
            bool up = on && !CityMap.IsOpen && _panel.gameObject.activeInHierarchy && _resume.CityBuildingController != null;
            if (!up)
            {
                Hide();
                return;
            }
            bool picked = _resume.options != null && _resume.options.gameObject.activeInHierarchy;
            if (Changed() || picked != _docked || _dirty)
            {
                _dirty = false;
                if (picked != _docked)
                {
                    _tab = 0;
                }
                _docked = picked;
                Read();
                Lay(frame);
            }
            if (!_shown)
            {
                _shown = true;
                _conceal.Hide();
            }
            if (_docked)
            {
                _cardView.Root.gameObject.SetActive(false);
                Dock(frame);
            }
            else
            {
                Undock();
                _cardView.Root.gameObject.SetActive(true);
                Place(frame);
            }
        }

        /// <summary>The game's box back as it was, the kit's gone.</summary>
        public void Give()
        {
            Hide();
            _conceal.Clear();
            GameUi.WorldPanel.ResumeElsewhere = null;
        }

        private void Hide()
        {
            if (!_shown)
            {
                return;
            }
            _shown = false;
            _docked = false;
            _conceal.Show();
            _cardView.Root.gameObject.SetActive(false);
            Undock();
            Array.Clear(_seen, 0, _seen.Length);
            _dirty = true;
        }

        private bool Find()
        {
            if (_resume != null && _panel != null)
            {
                return true;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            _resume = uis != null ? uis.buildingResume : null;
            if (_resume == null)
            {
                return false;
            }
            _panel = Panel != null ? Panel.GetValue(_resume) as RectTransform : null;
            if (_panel == null && _resume.data != null)
            {
                _panel = _resume.data.parent as RectTransform;   // the box is the parent of its two halves, as WorldPanel.Beside finds it
            }
            if (_panel == null || _resume.addressLabel == null || _resume.businessNameLabel == null || _resume.options == null)
            {
                _off = true;
                _state = "the game's own (its building box is not built as this version expects)";
                GameUi.UiClone.Fallback("the game's building box is not built as expected, so it stays the game's own");
                return false;
            }
            _canvas = _panel.GetComponentInParent<Canvas>(true);
            _labels[0] = Text(_resume.addressLabel);
            _labels[1] = _resume.businessNameLabel;
            _labels[2] = Text(_resume.neighborhoodLabel);
            _labels[3] = Text(_resume.businessTypeLabel);
            _labels[4] = Text(_resume.openStateLabel);
            _labels[5] = _resume.trafficIndexLabel;
            _labels[6] = _resume.customerCapacityLabel;
            _labels[7] = Text(_resume.buildingAreaLabel);
            _labels[8] = Text(_resume.rivalBuildingOwner);
            _labels[9] = Text(_resume.rivalBusinessOwner);
            _labels[10] = _resume.clickToEnterLabel;
            _labels[11] = _resume.clickToOptionsLabel;
            _labels[12] = Text(_resume.taxiTravelButtonText);
            _labels[13] = null;
            _conceal.Add(_panel);
            _state = "waiting for a building";
            return true;
        }

        private static TMP_Text Text(TextLocalizationComponent label) => label != null ? label.TextContainer : null;

        // The game wrote its box again: a label's words, a label or a button shown or hidden.
        private bool Changed()
        {
            bool changed = false;
            for (int i = 0; i < Words; i++)
            {
                var t = _labels[i] as TMP_Text;
                bool on = t != null && t.gameObject.activeSelf;   // its own switch, as the game sets it: its half of the box may be hidden while picked
                string words = on ? t.text : null;
                if (!ReferenceEquals(words, _seen[i]) || on != _seenOn[i])
                {
                    _seen[i] = words;
                    _seenOn[i] = on;
                    changed = true;
                }
            }
            changed |= Seen(Words, _resume.bizManButton);
            changed |= Seen(Words + 1, _resume.taxiTravelButton);
            changed |= Seen(Words + 2, _resume.setDestinationButton);
            changed |= Seen(Words + 3, _resume.changeSignButton);
            int kids = Kids(_resume.options) + Kids(_resume.setDestinationButton != null ? _resume.setDestinationButton.transform.parent : null) * 64;
            if (kids != _seenKids)
            {
                _seenKids = kids;   // another mod's button put in or taken out (Voogle Route's beside set destination)
                changed = true;
            }
            return changed;
        }

        // The pieces a container shows now.
        private static int Kids(Transform t)
        {
            int n = 0;
            for (int i = 0; t != null && i < t.childCount; i++)
            {
                n += t.GetChild(i).gameObject.activeSelf ? 1 : 0;
            }
            return n;
        }

        private bool Seen(int slot, Button b) => Seen(slot, b != null && b.gameObject.activeInHierarchy && b.interactable);

        private bool Seen(int slot, bool on)
        {
            if (_seenOn[slot] == on)
            {
                return false;
            }
            _seenOn[slot] = on;
            return true;
        }

        private string Seen(int i) => WorldHost.Plain(_seen[i]);

        // The box's words into the card's and the panel's models.
        private void Read()
        {
            string address = Seen(0), name = Seen(1), place = After(Seen(2)), kind = Seen(3), open = Seen(4);
            string traffic = Seen(5), capacity = Seen(6), area = Seen(7), owner = Seen(8), business = Seen(9);
            var openText = _labels[4] as TMP_Text;
            UiRole tone = openText != null && !string.IsNullOrEmpty(open) ? WorldHost.Tone(openText.color) : UiRole.Text2;
            string firstLine = FirstLine(open), state = Join(firstLine, Rest(open));
            string facts = Join(kind, place);
            bool business_ = !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(kind);
            // At the door: the business by its name, what it is and where, whether it is open, and the game's hints.
            _card.Title = business_ ? name : address;
            _card.Sub = facts;
            _card.State = firstLine;
            _card.StateTone = tone;
            _card.Icon = business_ ? UiIcon.Store : UiIcon.Building;
            int hints = 0;
            if (!string.IsNullOrEmpty(Seen(10)))
            {
                _card.Actions[hints++] = new UiPromptAction { Label = Seen(10), Hint = true };
            }
            if (!string.IsNullOrEmpty(Seen(11)))
            {
                _card.Actions[hints++] = new UiPromptAction { Label = Seen(11), Hint = true };
            }
            _card.ActionCount = hints;
            // Picked: the info panel, its strip the address, its head the business, its rows the building's and the business's.
            _info.Title = address ?? name;
            _info.Icon = UiIcon.Building;
            _info.Tabs = business_ ? new[] { UiIcon.Building, UiIcon.Store, UiIcon.Person } : new[] { UiIcon.Building, UiIcon.Person };
            _info.TabTints = new UiRole[0];
            _tab = Math.Min(_tab, _info.Tabs.Length - 1);
            _info.Tab = _tab;
            _info.Lead = business_ ? name : null;
            _info.Mark = business_ ? Initials(name) : null;
            _info.MarkTone = UiTokens.Chart((int)(UiColour.HueOf(name ?? string.Empty) / 45f));
            _info.Name = null;
            _info.Facts = facts;
            _info.Status = state;
            _info.StatusIcon = UiIcon.Clock;
            _info.StatusTone = tone == UiRole.OkText ? UiRole.Ok : tone == UiRole.DangerText ? UiRole.Danger : tone == UiRole.WarnText ? UiRole.Warn : UiRole.None;
            // The building's rows, then the business's; an empty building's kind and traffic are the building's own.
            var building = new List<UiInfoRow>(6);
            Row(building, null, owner, true);   // "Building owned by Hudson Realty": the game's own sentence
            Row(building, "reallife_ui_world_neighbourhood", place, false);
            Row(building, "reallife_ui_world_area", area, false);
            Row(building, "reallife_ui_world_capacity", capacity, false);
            var trade = business_ ? new List<UiInfoRow>(3) : building;
            Row(trade, "reallife_ui_world_kind", kind, false);
            if (business_)
            {
                Row(trade, "reallife_ui_world_open", state, false);
            }
            Row(trade, "reallife_ui_world_traffic", traffic, false);
            // The tabs: the building with the business under it, the business alone, its owners.
            var sections = new List<UiInfoSection>(2);
            int tab = business_ ? _tab : (_tab == 1 ? 2 : 0);
            if (tab == 0)
            {
                sections.Add(new UiInfoSection { Label = UiText.T("reallife_ui_world_building"), Rows = building.ToArray() });
            }
            if (business_ && tab < 2)
            {
                sections.Add(new UiInfoSection { Label = UiText.T("reallife_ui_world_business"), Value = Short(business), Rows = trade.ToArray() });
            }
            if (tab == 2)
            {
                sections.Add(new UiInfoSection { Label = UiText.T("reallife_ui_world_owner"), Rows = OwnerRows(owner, business) });
            }
            _info.Sections = sections.ToArray();
            Footer();
        }

        // The owners' own tab: who holds the building and who runs the business in it, as the game says each.
        private static UiInfoRow[] OwnerRows(string owner, string business)
        {
            var rows = new List<UiInfoRow>(2);
            Row(rows, null, owner, true);
            Row(rows, null, business, true);
            return rows.ToArray();
        }

        // A row when the game shows it; a line that is its own sentence ("Owned by Hudson Realty") is the row's words alone.
        private static void Row(List<UiInfoRow> rows, string key, string value, bool sentence)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            rows.Add(sentence ? new UiInfoRow { Label = value } : new UiInfoRow { Label = UiText.T(key), Value = value });
        }

        // The game's buttons in the footer, in its order: the taxi in taxi mode or BizMan as the wide one, then the rest as icons.
        private void Footer()
        {
            _footer.Clear();
            var icons = new List<UiIcon>(5);
            _main = Shows(_resume.taxiTravelButton) ? _resume.taxiTravelButton : Shows(_resume.bizManButton) ? _resume.bizManButton : null;
            _info.FooterLabel = _main == _resume.taxiTravelButton && _main != null ? Seen(12) : _main != null ? UiWidgets.Label(_main) : null;
            _info.FooterLabelOff = false;
            Add(icons, _resume.bizManButton, UiIcon.Briefcase);
            Add(icons, _resume.setDestinationButton, UiIcon.Navigation);
            Add(icons, _resume.changeSignButton, UiIcon.PaintBrush);
            // Another mod's buttons beside the game's (Voogle Route clones set destination beside it): each pressed as it is.
            _foreign.Clear();
            Foreign(_resume.setDestinationButton != null ? _resume.setDestinationButton.transform.parent : null);
            Foreign(_resume.options);
            for (int i = 0; i < _foreign.Count && icons.Count < 5; i++)
            {
                Button b = _foreign[i].GetComponentInChildren<Button>(false);
                if (b != null && b.gameObject.activeInHierarchy && !_footer.Contains(b))
                {
                    _footer.Add(b);
                    icons.Add(UiIcon.MapTrifold);
                }
            }
            _info.Footer = icons.ToArray();
            _info.FooterTints = new UiRole[0];
        }

        // A container's pieces that hold none of the game's four buttons and are not the mod's own: another mod's.
        private void Foreign(Transform container)
        {
            for (int i = 0; container != null && i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (!child.gameObject.activeSelf || child.name.StartsWith(UiHostRules.ModPrefix, StringComparison.Ordinal) || Holds(child, _resume.bizManButton)
                    || Holds(child, _resume.taxiTravelButton) || Holds(child, _resume.setDestinationButton) || Holds(child, _resume.changeSignButton) || _foreign.Contains(child))
                {
                    continue;
                }
                if (child.GetComponentInChildren<Button>(false) != null)
                {
                    _foreign.Add(child);
                }
            }
        }

        private static bool Holds(Transform t, Button b) => b != null && (b.transform == t || b.transform.IsChildOf(t));

        private void Add(List<UiIcon> icons, Button b, UiIcon icon)
        {
            if (b != null && b != _main && b.gameObject.activeInHierarchy)
            {
                _footer.Add(b);
                icons.Add(icon);
            }
        }

        private static bool Shows(Button b) => b != null && b.gameObject.activeInHierarchy;

        private void Lay(UiFrame frame)
        {
            float w = UiPromptCard.Width(_card), h = UiPromptCard.Height(_card);
            _cardScene.Begin(WorldKits.BuildingId, w, h, true);
            _cardScene.Floats = true;
            _cardScene.Pointer = true;
            UiPromptCard.Lay(_cardScene, UiAt.Screen, _card, 0f, 0f);
            _cardView.Write(_cardScene, false);
            _at = new Vector2(float.NaN, float.NaN);
            if (!_docked)
            {
                return;
            }
            HudHost hud = HudHost.Instance;
            if (hud != null && HudStatusGame.Shows)
            {
                return;   // the HUD lays it out in its left column (Dock)
            }
            float dw = UiInfoPanel.OuterWidth(_info), dh = UiInfoPanel.Height(_info);
            _dockScene.Begin(WorldKits.BuildingId, dw + UiTokens.S2, dh, true);
            _dockScene.Floats = true;
            _dockScene.Pointer = true;
            UiInfoPanel.Draw(_dockScene, _info, 0f, 0f);
            _dockView.Write(_dockScene, false);
        }

        // Picked: in the HUD's left column while the kit's HUD shows (its objectives go under it), else at the top left here.
        private void Dock(UiFrame frame)
        {
            HudHost hud = HudHost.Instance;
            if (hud != null && HudStatusGame.Shows)
            {
                _dockView.Root.gameObject.SetActive(false);
                HudScreen screen = hud.Screen;
                if (!ReferenceEquals(screen.Model.Info, _info) || !_inHud)
                {
                    screen.Model.Info = _info;
                    screen.Dirty = true;
                    _inHud = true;
                }
                if (!ReferenceEquals(screen.PiecePressed, _chain))
                {
                    _was = screen.PiecePressed;   // whoever put a piece up before keeps their presses
                    screen.PiecePressed = _chain;
                }
                return;
            }
            if (_inHud)
            {
                Undock();
                Lay(frame);
            }
            _dockView.Root.gameObject.SetActive(true);
            float y = UiDraw.Snap(TopBarFoot(frame)) + UiTokens.S4;
            _dockView.Root.anchoredPosition = new Vector2(UiTokens.S4 * _scale, -y * _scale);
        }

        private void Undock()
        {
            _dockView.Root.gameObject.SetActive(false);
            if (!_inHud)
            {
                return;
            }
            _inHud = false;
            HudHost hud = HudHost.Instance;
            if (hud == null)
            {
                return;
            }
            HudScreen screen = hud.Screen;
            if (ReferenceEquals(screen.Model.Info, _info))
            {
                screen.Model.Info = null;
                screen.Dirty = true;
            }
            if (ReferenceEquals(screen.PiecePressed, _chain))
            {
                screen.PiecePressed = _was;
            }
            _was = null;
        }

        // Where the game's top bar ends, px of the layout, so the docked panel stands under it while the game's HUD shows.
        private float TopBarFoot(UiFrame frame)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            var bar = uis != null && uis.topBar != null ? uis.topBar.transform as RectTransform : null;
            if (bar == null || !bar.gameObject.activeInHierarchy)
            {
                return 0f;
            }
            Canvas canvas = bar.GetComponentInParent<Canvas>(false);
            bar.GetWorldCorners(_corners);
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 low = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
            return Mathf.Max(0f, WorldHost.ToLayout(low, frame).y);
        }

        // At the door: the card where the game's box stands, its top left at the box's, kept inside the screen.
        private void Place(UiFrame frame)
        {
            _panel.GetWorldCorners(_corners);
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            Vector2 topLeft = WorldHost.ToLayout(RectTransformUtility.WorldToScreenPoint(cam, _corners[1]), frame);
            float w = UiPromptCard.Width(_card), h = UiPromptCard.Height(_card), x = topLeft.x, y = topLeft.y;
            UiHudRules.Clamp(ref x, ref y, w, h, frame.Width, frame.BandY);
            var at = new Vector2(Mathf.Round(x) * _scale, -Mathf.Round(y) * _scale);
            if (at != _at)
            {
                _at = at;
                _cardView.Root.anchoredPosition = at;
            }
        }

        private void DockPressed(int node)
        {
            if (node < 0 || node >= _dockScene.NodeCount)
            {
                return;
            }
            ref UiNode n = ref _dockScene.Nodes[node];
            bool done = !n.Disabled && Press(n.Command, n.Arg);
            if (done)
            {
                _dockView.Press(Time.unscaledTime);
            }
            UiGame.Instance.Play(done ? UiCue.Confirm : UiCue.Error);
        }

        // The panel's own presses, from the HUD or the world's canvas; another piece's go to whoever put it up.
        private bool Press(int command, int arg)
        {
            if (!_docked || _resume == null)
            {
                return _was != null && _was(command, arg);
            }
            switch (command)
            {
                case UiInfoPanel.TabCommand:
                    if (arg < 0 || arg >= _info.Tabs.Length)
                    {
                        return false;
                    }
                    _tab = arg;
                    _dirty = true;
                    return true;
                case UiInfoPanel.FooterCommand:
                    Button b = arg < 0 ? _main : arg < _footer.Count ? _footer[arg] : null;
                    return b != null && UiWidgets.Press(b);
                case UiDraw.CloseCommand:
                    _resume.Close();
                    return true;
            }
            return _was != null && _was(command, arg);
        }

        // ------------------------------------------------------------------------------------------------- words

        // The words after a label's own name ("Neighborhood: Murray Hill" is Murray Hill), in any language's colon.
        private static string After(string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return null;
            }
            int at = Math.Max(words.LastIndexOf(':'), words.LastIndexOf('：'));
            return at >= 0 && at + 1 < words.Length ? words.Substring(at + 1).Trim() : words;
        }

        private static string FirstLine(string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return null;
            }
            int at = words.IndexOf('\n');
            return at < 0 ? words : words.Substring(0, at).Trim();
        }

        private static string Rest(string words)
        {
            int at = string.IsNullOrEmpty(words) ? -1 : words.IndexOf('\n');
            return at < 0 ? null : words.Substring(at + 1).Replace('\n', ' ').Trim();
        }

        private static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + ", " + b;

        // The business owner's line cut to its state, for a section's header ("Rented by you").
        private static string Short(string words) => string.IsNullOrEmpty(words) ? null : FirstLine(words);

        // A business's initials, as its sign would read: the first letters of its first two words.
        internal static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            var sb = new System.Text.StringBuilder(2);
            bool start = true;
            for (int i = 0; i < name.Length && sb.Length < 2; i++)
            {
                char c = name[i];
                if (char.IsLetterOrDigit(c))
                {
                    if (start)
                    {
                        sb.Append(char.ToUpperInvariant(c));
                    }
                    start = false;
                }
                else
                {
                    start = true;
                }
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }
    }
}
