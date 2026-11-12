using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.InputSystem;
using Extensions;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UI.Elements;
using UI.Overlays;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The world prompt: whatever the game's OverlayUI shows (its building entrance, its lift, its fuel stop, the mod's
    /// garage bay and every WorldPanel and DoorPanel of the mod's, which all stand in it) drawn as the kit's prompt card
    /// (UiPromptCard) where the game put its own box. The game projects its target each frame and moves its box there; the
    /// card reads that place, so a shown prompt costs no projection of its own. Its words are read from the game's own
    /// labels after the game and other mods wrote them (Going Public's "Shared" too), its actions from the buttons the game
    /// made, each with the key its ButtonInfo names; the game reads the keys, the mouse presses the game's own button. The
    /// game's header and buttons are hidden by leaf while the card shows. Read again only when a label's words or the
    /// buttons change, a frame later, once the buttons it replaced are gone. Cost a frame: four reference comparisons and a
    /// move; nothing allocated.
    /// </summary>
    internal sealed class WorldPrompt
    {
        private static readonly FieldInfo Header = GameContract.WorldOverlayHeader.Field;
        private static readonly FieldInfo First = GameContract.WorldOverlayFirst.Field;
        private static readonly FieldInfo Left = GameContract.WorldOverlayLeft.Field;
        private static readonly FieldInfo Right = GameContract.WorldOverlayRight.Field;
        private static readonly FieldInfo Buttons = GameContract.WorldOverlayButtons.Field;
        private static readonly FieldInfo Template = GameContract.WorldOverlayTemplate.Field;
        private static readonly FieldInfo Current = GameContract.WorldOverlayCurrent.Field;
        private static readonly FieldInfo DoorBuilding = GameContract.WorldDoorBuilding.Field;
        private static readonly FieldInfo Group = GameContract.WorldOverlayGroup.Field;

        private readonly UiScene _scene = new UiScene();
        private readonly UiConceal _conceal = new UiConceal();
        private readonly UiPromptCardModel _model = new UiPromptCardModel { Actions = new UiPromptAction[UiPromptCardModel.MostActions] };
        private readonly Button[] _pressed = new Button[UiPromptCardModel.MostActions];
        private readonly List<Button> _found = new List<Button>(8);
        private UiView _view;
        private float _scale;
        private OverlayUI _overlay;
        private GameObject _header, _buttons;
        private Button _template;
        private CanvasGroup _group;
        private TextLocalizationComponent _first, _left, _right;
        private string _seenFirst, _seenLeft, _seenRight;
        private int _seenCount = -1, _seenLast;
        private bool _readDue, _shown, _off;
        private Vector2 _at = new Vector2(float.NaN, float.NaN);
        private string _state = "not looked at yet";

        public string State => _off ? _state : _shown ? "drawn over the game's box" : _state;

        /// <summary>The model last read, for the tests.</summary>
        public UiPromptCardModel Model => _model;

        /// <summary>True while the card stands in the game's box's place.</summary>
        public bool Showing => _shown;

        public void Build(RectTransform rect, float scale)
        {
            _scale = scale;
            _view = new UiView(rect, "Prompt") { Scale = scale, Ring = false };
            _view.PointerPressed = Guard.Wrap<int>(Pressed);
            _view.PointerEntered = Guard.Wrap<int>(node => _view.SetHover(node));
            _view.PointerExited = Guard.Wrap<int>(node => _view.SetHover(-1));
            _view.Root.gameObject.SetActive(false);
        }

        public void Rescale(float scale)
        {
            _scale = scale;
            if (_view != null)
            {
                _view.Scale = scale;
            }
            _seenCount = -1;   // laid out again at the new scale
        }

        /// <summary>A frame: the card in the game's box's place while the game shows one and the switch is on.</summary>
        public void Frame(UiFrame frame, bool on)
        {
            if (_off || !Find())
            {
                return;
            }
            bool up = on && OverlayUI.IsVisible && _overlay.gameObject.activeInHierarchy && (_group == null || _group.alpha > 0.5f);
            if (!up)
            {
                Hide();
                return;
            }
            if (Changed())
            {
                _readDue = true;   // read a frame later: the buttons the game replaced go at the end of this one
                return;
            }
            if (_readDue)
            {
                _readDue = false;
                Read();
                Lay();
            }
            if (!_shown)
            {
                if (_seenCount < 0)
                {
                    return;   // nothing read yet
                }
                _conceal.Hide();
                _view.Root.gameObject.SetActive(true);
                _shown = true;
            }
            Place(frame);
        }

        /// <summary>The game's box back as it was, the card gone.</summary>
        public void Give()
        {
            Hide();
            _conceal.Clear();
        }

        private void Hide()
        {
            if (!_shown)
            {
                return;
            }
            _shown = false;
            _conceal.Show();
            _view.Root.gameObject.SetActive(false);
            _seenCount = -1;   // read afresh the next time it shows
        }

        // The game's box and its pieces, found again with each city's interface.
        private bool Find()
        {
            if (_overlay != null)
            {
                return true;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            _overlay = uis != null ? uis.overlayUI : null;
            if (_overlay == null)
            {
                return false;
            }
            _header = Header?.GetValue(_overlay) as GameObject;
            _buttons = Buttons?.GetValue(_overlay) as GameObject;
            _template = Template?.GetValue(_overlay) as Button;
            _group = Group?.GetValue(_overlay) as CanvasGroup;
            _first = First?.GetValue(_overlay) as TextLocalizationComponent;
            _left = Left?.GetValue(_overlay) as TextLocalizationComponent;
            _right = Right?.GetValue(_overlay) as TextLocalizationComponent;
            if (_header == null || _buttons == null || _template == null || _first == null || _left == null || _right == null || Current == null)
            {
                _off = true;
                _state = "the game's own (its box is not built as this version expects)";
                GameUi.UiClone.Fallback("the game's box over the world is not built as expected, so it stays the game's own");
                return false;
            }
            _conceal.Add(_header.transform);
            _conceal.Add(_buttons.transform);
            _state = "waiting for a box";
            return true;
        }

        // The game wrote its box again: a label's words, or its buttons made anew.
        private bool Changed()
        {
            string first = Words(_first), left = Words(_left), right = Words(_right);
            Transform parent = _template.transform.parent;
            int count = parent != null ? parent.childCount : 0;
            int last = count > 0 ? parent.GetChild(count - 1).GetInstanceID() : 0;
            if (ReferenceEquals(first, _seenFirst) && ReferenceEquals(left, _seenLeft) && ReferenceEquals(right, _seenRight) && count == _seenCount && last == _seenLast)
            {
                return false;
            }
            _seenFirst = first;
            _seenLeft = left;
            _seenRight = right;
            _seenCount = count;
            _seenLast = last;
            return true;
        }

        // A label's words as the game holds them now (the same string until it writes them again), null while it is off.
        private static string Words(TextLocalizationComponent label) =>
            label != null && label.gameObject.activeInHierarchy && label.TextContainer != null ? label.TextContainer.text : null;

        // The box's words and its buttons into the card's model.
        private void Read()
        {
            TMP_Text right = _right.TextContainer;
            _model.Title = WorldHost.Plain(Words(_first));
            _model.Sub = WorldHost.Plain(Words(_left));
            _model.State = WorldHost.Plain(Words(_right));
            _model.StateTone = right != null ? WorldHost.Tone(right.color) : UiRole.Text2;
            if (string.IsNullOrEmpty(_model.Title))
            {
                _model.Title = _model.Sub;   // a box with one line: that line is its name
                _model.Sub = null;
            }
            // The game's own buttons in its order, each made from the ButtonInfo of the same place (OverlayUI.SetButton, which
            // the game asks again at each key press too); another's button put beside them after, with no key of the game's.
            object current = Current.GetValue(_overlay);
            ButtonInfo[] infos = (current as IOverlay)?.GetButtons() ?? new ButtonInfo[0];
            _model.Icon = WorldKits.IconFor(current, infos.Length > 0 && infos[0] != null ? infos[0].key : null);
            Door(current);
            _found.Clear();
            Transform parent = _template.transform.parent;
            int made = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; parent != null && i < parent.childCount; i++)
                {
                    Transform child = parent.GetChild(i);
                    bool clone = child.name.StartsWith(_template.name, StringComparison.Ordinal);
                    Button b = child != _template.transform && child.gameObject.activeSelf && clone == (pass == 0) ? child.GetComponent<Button>() : null;
                    if (b != null)
                    {
                        _found.Add(b);
                        made += pass == 0 ? 1 : 0;
                    }
                }
            }
            int n = Math.Min(UiPromptCardModel.MostActions, _found.Count);
            for (int i = 0; i < n; i++)
            {
                Button b = _found[i];
                ButtonInfo info = i < made && i < infos.Length ? infos[i] : null;
                _pressed[i] = b;
                _model.Actions[i] = new UiPromptAction
                {
                    Label = Label(b, info), Key = info != null ? WorldHost.Key(info.playerAction) : default(UiGameKey),
                    Main = info != null && info.playerAction == PlayerAction.Interact, Disabled = !b.interactable,
                };
            }
            for (int i = n; i < _pressed.Length; i++)
            {
                _pressed[i] = null;
            }
            _model.ActionCount = n;
        }

        // A business's door: what it is and until when it is open ("Noodle house, open until 23:00") on the line under its name,
        // where the game writes the address; the game's own open or closed word beside it is then said by that line. A home, a
        // warehouse, an empty building and every other box keep the game's lines, as do words another mod wrote there.
        private void Door(object current)
        {
            var cbc = current is BuildingEntranceOverlay && DoorBuilding != null ? DoorBuilding.GetValue(current) as CityBuildingController : null;
            BuildingRegistration reg = cbc != null && cbc.building != null ? cbc.buildingRegistration : null;
            string type = reg != null ? cbc.building.BuildingType : null;
            if (reg == null || reg.AvailableForRent || string.IsNullOrEmpty(reg.BusinessName) || type == "ba:buildingtype_residential" || type == "ba:buildingtype_warehouse")
            {
                return;
            }
            string line = OpenLine(reg);
            if (line == null || !GameAddress(cbc))
            {
                return;   // no hours to read, or another mod's words where the game writes the address: the game's lines stay
            }
            _model.Sub = line;
            if (_model.State == UiText.T("common_open") || _model.State == UiText.T("common_closed"))
            {
                _model.State = null;
            }
        }

        // "Noodle house, open until 23:00"; "..., open" through the night; "..., opens at 09:00" later today; "..., closed". By
        // the business's opening hours as the game's building box reads them (BuildingResume.UpdateOpenStateLabel). Null when
        // they cannot be read: the game's lines stay.
        internal static string OpenLine(BuildingRegistration reg)
        {
            string kind = reg.businessTypeName.GetLocalization();
            int hour = TimeHelper.CurrentHour;
            try
            {
                ScheduleDay today = Day(reg, false);
                if (BusinessHelper.IsBusinessOpen(reg))
                {
                    OpeningHourSlot slot = null;
                    for (int i = 0; today != null && i < today.openingHourSlots.Count && slot == null; i++)
                    {
                        OpeningHourSlot s = today.openingHourSlots[i];
                        slot = s.startingHour <= hour && s.endingHour > hour ? s : null;
                    }
                    ScheduleDay next = Day(reg, true);
                    bool night = slot == null || slot.endingHour >= 23 && next != null && next.isOpen && Earliest(next) == 0;
                    return night ? Line("reallife_ui_world_door_open", kind, null) : Line("reallife_ui_world_door_until", kind, slot.endingHour.GetFormattedTime());
                }
                int opens = -1;
                for (int i = 0; !reg.temporarilyClosed && today != null && today.isOpen && i < today.openingHourSlots.Count; i++)
                {
                    int start = today.openingHourSlots[i].startingHour;
                    opens = start > hour && (opens < 0 || start < opens) ? start : opens;
                }
                return opens >= 0 ? Line("reallife_ui_world_door_opens", kind, opens.GetFormattedTime()) : Line("reallife_ui_world_door_closed", kind, null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // True while the box's second line is the address as the game writes it there (BuildingEntranceOverlay.AddressLabel).
        private bool GameAddress(CityBuildingController cbc)
        {
            try
            {
                return string.Equals(_model.Sub, WorldHost.Plain(Streets.AddressHelper.ToFormattedString(cbc.building.Address)), StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static ScheduleDay Day(BuildingRegistration reg, bool tomorrow)
        {
            var want = tomorrow ? TimeHelper.GetNextDayOfWeek() : TimeHelper.GetDayOfWeek();
            for (int i = 0; reg.scheduleDays != null && i < reg.scheduleDays.Count; i++)
            {
                if (reg.scheduleDays[i] != null && reg.scheduleDays[i].day == want)
                {
                    return reg.scheduleDays[i];
                }
            }
            return null;
        }

        private static int Earliest(ScheduleDay day)
        {
            int first = -1;
            for (int i = 0; i < day.openingHourSlots.Count; i++)
            {
                int start = day.openingHourSlots[i].startingHour;
                first = first < 0 || start < first ? start : first;
            }
            return first;
        }

        private static string Line(string key, string kind, string time) => UiText.T(key).Replace("{kind}", kind ?? string.Empty).Replace("{time}", time ?? string.Empty);

        // A button's words without the key the game writes after them (" [F]"), which the card shows on its cap.
        private static string Label(Button b, ButtonInfo info)
        {
            var text = b.GetComponentInChildren<TextLocalizationComponent>(true);
            string words = text != null && text.TextContainer != null ? text.TextContainer.text : null;
            if (words == null)
            {
                TMP_Text plain = b.GetComponentInChildren<TMP_Text>(true);   // another's button with words of its own
                words = plain != null ? plain.text : null;
            }
            string suffix = info != null ? info.playerAction.AsSuffix() : null;   // as the game wrote it after the words
            if (words != null && !string.IsNullOrEmpty(suffix) && words.EndsWith(suffix, StringComparison.Ordinal))
            {
                words = words.Substring(0, words.Length - suffix.Length);
            }
            return WorldHost.Plain(words) ?? string.Empty;
        }

        private void Lay()
        {
            float w = UiPromptCard.Width(_model), h = UiPromptCard.Height(_model);
            _scene.Begin(WorldKits.PromptId, w, h, true);
            _scene.Floats = true;
            _scene.Pointer = true;
            UiPromptCard.Lay(_scene, UiAt.Screen, _model, 0f, 0f);
            _view.Write(_scene, false);
            _at = new Vector2(float.NaN, float.NaN);
        }

        // The card where the game's box stands: beside the game's own projected point, clamped above the status bar.
        private void Place(UiFrame frame)
        {
            Vector3 screen = _overlay.transform.position;
            Vector2 p = WorldHost.ToLayout(screen, frame);
            float w = UiPromptCard.Width(_model), h = UiPromptCard.Height(_model);
            UiHudRules.Beside(p.x, p.y, screen.z >= 0f, w, h, frame.Width, frame.BandY, 0f, 0f, 0f, 0f, out float x, out float y);
            var at = new Vector2(Mathf.Round(x) * _scale, -Mathf.Round(y) * _scale);
            if (at != _at)
            {
                _at = at;
                _view.Root.anchoredPosition = at;
            }
        }

        // A row pressed with the mouse: the game's own button for it, which keeps its rules.
        private void Pressed(int node)
        {
            if (node < 0 || node >= _scene.NodeCount)
            {
                return;
            }
            ref UiNode n = ref _scene.Nodes[node];
            Button b = n.Arg >= 0 && n.Arg < _pressed.Length ? _pressed[n.Arg] : null;
            if (n.Disabled || b == null || !UiWidgets.Press(b))
            {
                UiGame.Instance.Play(UiCue.Error);
                return;
            }
            _view.Press(Time.unscaledTime);
            UiGame.Instance.Play(UiCue.Confirm);
        }
    }
}
