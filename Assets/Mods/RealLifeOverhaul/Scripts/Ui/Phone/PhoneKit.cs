using System;
using System.Collections.Generic;
using BigAmbitions.InputSystem;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Planner;
using TMPro;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's phone drawn on the RLO interface (PhoneScreen on the Phone template), the phone the player sees whenever
    /// they walk the city. The kit's phone stands inside the game's phone container, so it folds down and comes up with the
    /// game's collapse and hides whenever the game hides its phone (the full menu, the map, a mini game); it keeps its own
    /// size, the game's phone's, taller only with the radio row and never taller than the screen, whatever other mods do
    /// to the game's container. The game's grid of buttons stays alive under it, each button unseen and untouchable through
    /// the host's UiConceal (the grid is a parent other mods fill, so never the grid itself), and every tile presses
    /// its button's own onClick, so the game's, the mod's and other mods' apps open as before. The collapse, the radio and the private driver press the game's own buttons. The phone
    /// takes the keys and the pad only when asked (the Phone key, the pad's View) and gives them back on Back at home.
    /// Another pager of the phone (one the classic pager stepped aside for) or another mod's screen on it has the kit
    /// stand down to the classic phone for the session, with one line in Player.log; a fault of the kit gives the classic
    /// phone back for the session too. Cost: while it shows, the host's input reads and a few comparisons a frame, a look
    /// at the buttons four times a second; nothing while the phone is hidden or the switch is off.
    /// </summary>
    public static class PhoneKit
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "PhoneKit", typeof(PhoneKit));

        internal const float Look = 0.25f;          // seconds between looks at the buttons and the game's readings
        private const float PressDip = 0.08f;       // seconds the pressed tile dips before its app opens
        private const float VolumeStep = 0.1f;      // of the game's volume slider, a press
        private const string OtherScreen = "VoogleRoute_PhoneGps";   // another mod's screen drawn on the phone
        private const int UiLayer = 5;              // Unity's interface layer, the game's LayerHelper.UiLayerIndex, when the name is not found

        private static readonly PhoneInput Gate = new PhoneInput();
        private static readonly PhoneActions Actions = new PhoneActions();
        private static readonly UiInfoRow[] NextRows = new UiInfoRow[8];

        private static GameObject _root;
        private static GameObject _keys;             // selected while the phone holds the keys: the game then leaves them alone
        private static bool _suspendedBefore;        // the mod's own keys as they were when the phone took the keys
        private static GameObject _selectedBefore;   // what the event system had selected then (a button last clicked), given back
        private static UiHost _host;
        private static PhoneScreen _screen;
        private static Canvas _canvas;
        private static RectTransform _container;
        private static CollapsibleWindow _window;
        private static SmartphoneUI _phone;
        private static readonly UiConceal Buttons = new UiConceal();   // the grid's buttons, hidden and given back as found
        private static Transform _grid;
        private static RectTransform _scaler;        // the phone's root canvas, whose height is the screen's in canvas units
        private static int _gridSeen;                // the grid's children as last handed to Buttons
        private static Button _collapse, _driver, _skip;
        private static Slider _volume;
        private static TMP_Text _song, _title, _driverLabel;
        private static CanvasGroup _radio;
        private static UnityEngine.Events.UnityAction<bool> _onCollapse;
        private static bool _gaveUp, _stopped;
        private static string _state = "not looked at yet";
        private static float _nextLook;
        private static int _pressItem = -1;
        private static float _pressAt;
        private static int _minute = -1, _day = -1;
        private static string _songSeen, _titleSeen;
        private static bool _radioSeen, _driverSeen;

        /// <summary>Where the kit phone stands, for the interface's line at load.</summary>
        public static string State => _root != null ? "drawn over the game's phone" : _state;

        /// <summary>True while the kit phone shows.</summary>
        public static bool Showing => _root != null && _root.activeInHierarchy;

        /// <summary>The kit phone's screen while it stands, for the tests and the checks.</summary>
        internal static PhoneScreen Screen => _screen;

        /// <summary>Puts the kit phone over the game's once per city; asked again once a second while the switches are on.</summary>
        public static void Install()
        {
            if (!UiScreenSwitches.On(PhoneScreen.ScreenId))
            {
                Remove("its switch is off");
                return;
            }
            if (_gaveUp || _stopped || FrameGuard.Off || GameContract.PhoneKitPart.Off)
            {
                return;
            }
            if (_root != null)
            {
                if (_phone == null)
                {
                    Remove("the city was unloaded");
                    return;
                }
                StandDownIfTaken();
                return;
            }
            if (Time.unscaledTime < _nextLook)
            {
                return;
            }
            _nextLook = Time.unscaledTime + 1f;
            UIs uis = InstanceBehavior<UIs>.Instance;
            SmartphoneUI phone = uis != null ? uis.smartphoneUI : null;
            CollapsibleWindow window = uis != null ? uis.smartphoneCollapsibleWindow : null;
            PhoneGridPager pager = PhoneGridPager.Hud;
            if (phone == null || window == null || pager == null)
            {
                _state = "waiting for the phone and its pages";
                return;   // the phone's pages come with the mod's apps a moment after the city: asked again next second
            }
            if (Taken(pager, phone.transform, out string why))
            {
                StandDown(why);
                return;
            }
            Build(phone, window, pager);
        }

        private static void Build(SmartphoneUI phone, CollapsibleWindow window, PhoneGridPager pager)
        {
            _phone = phone;
            _window = window;
            _container = (RectTransform)window.transform;
            _grid = pager.transform;
            _gridSeen = 0;
            _scaler = RootCanvas(_container);
            _collapse = (GameContract.PhoneCollapse.Field?.GetValue(window) as GameObject)?.GetComponent<Button>();
            _driver = GameContract.PhoneDriver.Field?.GetValue(phone) as Button;
            _driverLabel = _driver != null ? _driver.GetComponentInChildren<TMP_Text>(true) : null;
            RadioControls radio = phone.radioControls;
            _skip = radio != null ? GameContract.PhoneRadioSkip.Field?.GetValue(radio) as Button : null;
            _volume = radio != null ? GameContract.PhoneRadioVolume.Field?.GetValue(radio) as Slider : null;
            _song = radio != null ? GameContract.PhoneRadioSong.Field?.GetValue(radio) as TMP_Text : null;
            _radio = radio != null ? radio.GetComponent<CanvasGroup>() : null;
            var frame = phone.GetComponent<Player.HUD.SmartphoneUI.SmartPhoneFrame>();
            var titleKey = frame != null ? GameContract.PhoneTitle.Field?.GetValue(frame) as Localizor.LanguageChangeEvent.TextLocalizationComponent : null;
            _title = titleKey != null ? titleKey.GetComponent<TMP_Text>() : null;

            _root = new GameObject("RealLife Interface Phone", typeof(RectTransform));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(_container, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.SetAsLastSibling();
            // Over the whole device: a click on the phone's rim or a gap between tiles is the kit's, never the game's
            // phone under it (its head would fold the phone away).
            var blocker = _root.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            // What the event system selects while the phone holds the keys, on the interface's layer: the game's own way of
            // keeping its keys away from the player and the camera while its interface has them (GameManager.HasInputSelected).
            _keys = new GameObject("RealLife Phone Keys", typeof(RectTransform));
            _keys.transform.SetParent(_root.transform, false);
            int ui = LayerMask.NameToLayer("UI");
            _keys.layer = ui >= 0 ? ui : UiLayer;
            _canvas = _root.AddComponent<Canvas>();
            _root.AddComponent<GraphicRaycaster>();
            PhoneItemsGame.Read(pager);
            PhoneItems.Current = PhoneItemsGame.Items;
            HookPictures();
            _screen = new PhoneScreen(PhoneItemsGame.Items, Actions, UiText.Words) { Docked = true };
            _host = _root.AddComponent<UiHost>();
            _host.Bind(_screen, Gate, UiGame.Instance, UiDirector.Dark, UiDirector.Motion, 0f, 0f, null, UiIcon.None);
            _host.View.Scale = UiTokens.CanvasPerPx;
            _host.Failed = Fallback;
            _onCollapse = Guard.Listener<bool>(Collapsed);
            window.onStateChange.AddListener(_onCollapse);
            Size();
            Readings(true);
            _state = "drawn over the game's phone";
            _root.AddComponent<PhoneKitWatch>();   // its OnEnable shows the kit at once while the phone shows
            UiDirector.Logger?.Info("UI: the phone is drawn on the RLO interface (" + PhoneItemsGame.Items.Count + " apps, the Phone key " + Keys.Label(KeyAction.Phone) + ").");
        }

        private static System.Func<string, Sprite> _picturesBefore;
        private static System.Func<string, Sprite> _pictures;

        // The phone answers its own pictures' keys (other mods' app icons) and hands every other key to whoever answered
        // before (a catalogue's goods), so the kit has one way to find a picture. Hooked once a session.
        private static void HookPictures()
        {
            if (_pictures != null && UiPictures.Find == _pictures)
            {
                return;
            }
            _picturesBefore = UiPictures.Find;
            _pictures = key =>
            {
                int handle = UiDraw.PictureHandle(key);
                return handle > 0 ? PhoneItemsGame.Picture(handle) : _picturesBefore != null ? _picturesBefore(key) : null;
            };
            UiPictures.Find = _pictures;
        }

        // Another pager of the phone, or another mod's screen drawn on it: theirs, and the classic phone with them.
        private static bool Taken(PhoneGridPager pager, Transform phone, out string why)
        {
            why = pager.SteppedAside ? "another mod pages the phone's apps" : Named(phone, OtherScreen, 0) ? "another mod draws a screen of its own on the phone" : null;
            return why != null;
        }

        // A child of that name's beginning anywhere under the phone, without a list made for the search.
        private static bool Named(Transform under, string start, int depth)
        {
            for (int i = 0; i < under.childCount; i++)
            {
                Transform child = under.GetChild(i);
                if (child.name.StartsWith(start, StringComparison.Ordinal) || depth < 6 && Named(child, start, depth + 1))
                {
                    return true;
                }
            }
            return false;
        }

        private static void StandDownIfTaken()
        {
            PhoneGridPager pager = PhoneGridPager.Hud;
            if (pager == null)
            {
                Remove("the phone's pages went");
                return;
            }
            if (Taken(pager, _phone.transform, out string why))
            {
                StandDown(why);
            }
        }

        private static void StandDown(string why)
        {
            Remove(null);
            _stopped = true;
            _state = "the classic phone for this session (" + why + ")";
            UiClone.Fallback("the RLO phone stands down to the classic phone for this session: " + why);
        }

        /// <summary>A new city: a phone that stood down in the last one is looked at again.</summary>
        public static void Rearm()
        {
            _stopped = false;
            _nextLook = 0f;
            if (!_gaveUp)
            {
                _state = "not looked at yet";
            }
        }

        /// <summary>The game's phone as it was: the kit gone, the grid seen and touchable again.</summary>
        public static void Remove(string why)
        {
            if (_root == null && _phone == null)
            {
                return;
            }
            try
            {
                Leave();
                Buttons.Clear();
                if (_window != null && _onCollapse != null)
                {
                    _window.onStateChange.RemoveListener(_onCollapse);
                }
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root);
                }
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Error(e);
            }
            _root = null;
            _keys = null;
            _host = null;
            _screen = null;
            _canvas = null;
            _phone = null;
            _window = null;
            _container = null;
            _grid = null;
            _gridSeen = 0;
            _scaler = null;
            _onCollapse = null;
            _pressItem = -1;
            _minute = _day = -1;
            PhoneItems.Current = null;
            PhoneItemsGame.Forget();
            if (UiDirector.Logger != null && why != null && Meta.Dev.Verbose)
            {
                UiDirector.Logger.Info("UI: the phone is the game's own again (" + why + ").");
            }
        }

        /// <summary>A fault in the kit's phone (its host has counted it): the classic phone for the rest of the session.</summary>
        public static void Fallback(Exception e)
        {
            UiDirector.Logger?.Warn("UI: " + UiText.T("reallife_ui_note_fallback") + " (the phone, " + (e != null ? e.GetType().Name : "?") + ")");
            Remove("a fault");
            _gaveUp = true;
            _state = "the classic phone after a fault of the kit's phone";
        }

        // ------------------------------------------------------------------------------------------------- showing

        internal static void Shown()
        {
            if (_root == null)
            {
                return;
            }
            try
            {
                Conceal();
                Buttons.Hide();
                Sort();
                Size();
                Readings(true);
                Next();
                _host.Show();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, null);
                Fallback(e);
            }
        }

        internal static void Hidden()
        {
            Leave();
            _pressItem = -1;
            _host?.Hide();
        }

        // The phone's frame by the game's own: as wide as its phone, as tall as its phone with or without the radio row,
        // never taller than the screen, whatever another mod has made of the container.
        private static void Size()
        {
            bool radio = _radio != null && _radio.blocksRaycasts;
            float screen = _scaler != null && _scaler.rect.height > 1f ? _scaler.rect.height / UiTokens.CanvasPerPx : UiFrame.ScreenHeight;
            float h = UiPhone.DeviceHeight(radio, screen);
            var rect = (RectTransform)_root.transform;
            var want = new Vector2(UiPhone.Width * UiTokens.CanvasPerPx, h * UiTokens.CanvasPerPx);
            if (rect.sizeDelta != want)
            {
                rect.sizeDelta = want;
            }
        }

        // Walked by hand: the game builds the phone switched off, and the engine keeps rootCanvas up only while it is on.
        private static RectTransform RootCanvas(Transform from)
        {
            RectTransform top = null;
            for (Transform t = from; t != null; t = t.parent)
            {
                if (t.GetComponent<Canvas>() != null)
                {
                    top = t as RectTransform;
                }
            }
            return top;
        }

        // The grid is a parent other mods fill, so its buttons are hidden one by one, each given back as found. Asked every
        // frame the phone shows: the pager and other mods add and drop buttons at any time, and a new one hides at once.
        private static void Conceal()
        {
            if (_grid == null)
            {
                return;
            }
            int seen = _grid.childCount;
            for (int i = 0; i < _grid.childCount; i++)
            {
                seen = unchecked(seen * 31 + _grid.GetChild(i).GetInstanceID());
            }
            if (seen == _gridSeen)
            {
                return;
            }
            _gridSeen = seen;
            Buttons.Prune();
            for (int i = 0; i < _grid.childCount; i++)
            {
                Buttons.Add(_grid.GetChild(i));
            }
        }

        // Just over the phone's own canvas: the game's question boxes, notices and other mods' canvases stay above it.
        private static void Sort()
        {
            if (_canvas == null)
            {
                return;
            }
            Canvas phone = _root.transform.parent.GetComponentInParent<Canvas>(true);
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = (phone != null ? phone.sortingOrder : 0) + 1;
        }

        private static void Collapsed(bool collapsed)
        {
            if (_screen != null)
            {
                _screen.Readings.Collapsed = collapsed;
                _screen.Touch();
                if (collapsed)
                {
                    Leave();   // folded away: the keys are the game's
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- each frame

        /// <summary>The phone's frame while it shows: its key, the wheel, a pressed tile's dip, Esc in a folder, and its look.</summary>
        internal static void Frame(float now, ref float nextLook)
        {
            if (_screen == null)
            {
                return;
            }
            if (_pressItem >= 0 && now >= _pressAt)
            {
                int item = _pressItem;
                _pressItem = -1;
                if (!PhoneItemsGame.Press(item))
                {
                    UiGame.Instance.Play(UiCue.Error);
                }
            }
            if (Gate.Focused && !Held())
            {
                Leave();   // a click away, or the game selected something of its own: the keys are back with the game
            }
            if (PhoneKey() || PadView())
            {
                if (Gate.Focused)
                {
                    Leave();
                }
                else if (!_window.IsCollapsed)
                {
                    Take();
                }
            }
            Conceal();
            Wheel();
            if (!Gate.Focused && _screen.Folder >= 0 && FolderKey())
            {
                _screen.CloseFolder();
                UiGame.Instance.Play(UiCue.Back);
            }
            if (now >= nextLook)
            {
                nextLook = now + Look;
                Size();
                Readings(false);
                PhoneItemsGame.Poll(PhoneGridPager.Hud);
            }
        }

        // The game's cancel key closes an open folder while the phone does not hold the keys, as the classic phone's does.
        private static bool FolderKey()
        {
            if (!PlayerAction.Cancel.Pressed() || !PhoneFolderKeys.KeyIsFree())
            {
                return false;
            }
            PlayerAction.Cancel.Reset();
            return true;
        }

        // While the phone holds the keys the mod's own keys are suspended, its own key too, so it is read here as Keys binds it.
        private static bool PhoneKey()
        {
            if (!Gate.Focused)
            {
                return Keys.Down(KeyAction.Phone);
            }
            if (!UnityEngine.Input.GetKeyDown(Keys.Key(KeyAction.Phone)))
            {
                return false;
            }
            switch (Keys.Modifier(KeyAction.Phone))
            {
                case KeyModifier.Shift: return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
                case KeyModifier.Ctrl: return UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
                case KeyModifier.Alt: return UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt);
                default: return true;
            }
        }

        // The phone's keys object is what the event system has selected.
        private static bool Held()
        {
            EventSystem events = EventSystem.current;
            return _keys != null && events != null && events.currentSelectedGameObject == _keys;
        }

        private static bool PadView()
        {
            Gamepad pad = Gamepad.current;
            return pad != null && pad.selectButton.wasPressedThisFrame;
        }

        // The wheel over the phone turns its pages.
        private static void Wheel()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }
            float notches = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(notches) < 0.01f)
            {
                return;
            }
            var rect = (RectTransform)_root.transform;
            Camera eye = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(rect, mouse.position.ReadValue(), eye) && _screen.Turn(notches > 0f ? -1 : 1))
            {
                UiGame.Instance.Play(UiCue.Tab);
            }
        }

        // The keys and the pad to the phone, by the game's own path: the phone's keys object selected on the interface's
        // layer, so the game's walking, camera, car and shortcuts leave them alone (GameManager.ShouldBlockKeyboardShortcuts),
        // the mod's own keys suspended, and the host taking the kit's keys from the game each frame. Never over a field
        // the player is typing in.
        private static void Take()
        {
            EventSystem events = EventSystem.current;
            if (_keys == null || events == null || Typing(events))
            {
                UiGame.Instance.Play(UiCue.Error);
                return;
            }
            _selectedBefore = events.currentSelectedGameObject;
            events.SetSelectedGameObject(_keys);
            _suspendedBefore = Keys.Suspended;
            Keys.Suspended = true;
            Gate.Focused = true;
            _host.Exclusive = true;
            UiGame.Instance.Play(UiCue.Confirm);
        }

        // Every way out (the key again, Back at home, a click away, the phone folding or hidden, a fault, the switch off)
        // gives back exactly what Take took: the selection only if it is still the phone's, the mod's keys as they were.
        private static void Leave()
        {
            if (!Gate.Focused)
            {
                return;
            }
            Gate.Focused = false;
            Keys.Suspended = _suspendedBefore;
            EventSystem events = EventSystem.current;
            if (_keys != null && events != null && events.currentSelectedGameObject == _keys)
            {
                events.SetSelectedGameObject(_selectedBefore != null && _selectedBefore.activeInHierarchy ? _selectedBefore : null);
            }
            _selectedBefore = null;
            if (_host != null)
            {
                _host.Exclusive = false;
                _host.PointerLeads();
            }
        }

        // A text field is being typed in: the keys are its own.
        private static bool Typing(EventSystem events)
        {
            GameObject now = events.currentSelectedGameObject;
            return now != null && now.activeInHierarchy && (now.GetComponent<TMP_InputField>() != null || now.GetComponent<InputField>() != null);
        }

        // The game's readings the phone shows: its name, the time, the radio, the private driver, the fold. Written into the
        // screen only when one changed.
        private static void Readings(bool all)
        {
            UiPhoneModel m = _screen.Readings;
            bool changed = all;
            string title = _title != null ? _title.text : null;
            if (all || !ReferenceEquals(title, _titleSeen))
            {
                _titleSeen = title;
                m.Name = string.IsNullOrEmpty(title) ? "BizPhone" : title.Trim();
                changed = true;
            }
            int minute = PlannerDirector.Now();
            if (minute != _minute)
            {
                _minute = minute;
                m.Time = PlannerText.Clock(PlannerTime.OfDay(minute));
                changed = true;
                int day = PlannerTime.DayOf(minute);
                if (day != _day)
                {
                    bool first = _day < 0;
                    _day = day;
                    if (!all && !first)
                    {
                        Next();   // a new day: what comes next moved on
                    }
                }
            }
            bool radio = _radio != null && _radio.blocksRaycasts;
            string song = _song != null ? _song.text : null;
            if (all || radio != _radioSeen || !ReferenceEquals(song, _songSeen))
            {
                _radioSeen = radio;
                _songSeen = song;
                m.Radio = radio;
                int cut = song != null ? song.IndexOf('\n') : -1;
                m.RadioTitle = cut > 0 ? song.Substring(0, cut) : song;
                m.RadioTrack = cut > 0 ? song.Substring(cut + 1) : null;
                changed = true;
            }
            if (_volume != null)
            {
                float share = Mathf.InverseLerp(_volume.minValue, _volume.maxValue, _volume.value);
                bool canSkip = _skip != null && _skip.interactable;
                if (Mathf.Abs(share - m.RadioVolume) > 0.005f || canSkip != m.RadioCanSkip)
                {
                    m.RadioVolume = share;
                    m.RadioCanSkip = canSkip;
                    changed = true;
                }
            }
            bool driver = _driver != null && _driver.gameObject.activeSelf;
            if (all || driver != _driverSeen)
            {
                _driverSeen = driver;
                m.Driver = driver;
                m.DriverLabel = _driverLabel != null && !string.IsNullOrEmpty(_driverLabel.text) ? _driverLabel.text : UiText.T("reallife_phone_driver");
                changed = true;
            }
            bool collapsed = _window != null && _window.IsCollapsed;
            if (collapsed != m.Collapsed)
            {
                m.Collapsed = collapsed;
                changed = true;
            }
            if (changed)
            {
                _screen.Touch();
            }
        }

        // What comes next, read through the Planner's and the Calendar's directors.
        private static void Next()
        {
            if (_screen == null)
            {
                return;
            }
            UiPhoneModel m = _screen.Readings;
            int n = PhoneNextGame.Read(NextRows);
            m.Next = NextRows;
            m.NextCount = n;
            _screen.Touch();
        }

        // ------------------------------------------------------------------------------------------------- presses

        private sealed class PhoneActions : IPhoneActions
        {
            public bool Focused => Gate.Focused;

            public void Open(int item)
            {
                _pressItem = item;
                _pressAt = Time.unscaledTime + PressDip;   // the tile's dip is seen before the app takes the screen
            }

            public void Collapse()
            {
                if (_collapse != null && _collapse.interactable)
                {
                    _collapse.onClick.Invoke();
                }
            }

            public void RadioSkip()
            {
                if (_skip != null && _skip.interactable)
                {
                    _skip.onClick.Invoke();
                }
            }

            public void RadioVolume(int step)
            {
                if (_volume != null)
                {
                    _volume.value = Mathf.Clamp(_volume.value + step * VolumeStep * (_volume.maxValue - _volume.minValue), _volume.minValue, _volume.maxValue);   // its own listener keeps it
                }
            }

            public void Driver()
            {
                if (_driver != null && _driver.interactable)
                {
                    _driver.onClick.Invoke();
                }
            }

            public void Leave()
            {
                PhoneKit.Leave();
            }
        }

        // The kit's input: the game's keys and pad only while the phone holds them; the mouse always.
        private sealed class PhoneInput : IUiInput
        {
            public bool Focused;

            public void Read(ref UiInputState state)
            {
                var real = default(UiInputState);
                UiGame.Instance.Read(ref real);
                if (Focused)
                {
                    state = real;
                    return;
                }
                state.Pointed = real.Pointed;
                state.Pad = real.Pad;
            }

            public void TakeBack()
            {
                if (Focused)
                {
                    UiGame.Instance.TakeBack();
                }
            }

            public void TakeAll()
            {
                if (Focused)
                {
                    UiGame.Instance.TakeAll();
                }
            }

            public IUiKeys Keys => UiGame.Instance.Keys;
        }

        // ------------------------------------------------------------------------------------------------- tests

        /// <summary>Test: the kit phone's second page, or why not.</summary>
        public static void TestSecondPage()
        {
            if (!Ready("second"))
            {
                return;
            }
            _screen.CloseFolder();
            bool turned = _screen.Pages > 1 && _screen.Turn(1);
            Report("second", turned, "page " + (_screen.Page + 1) + " of " + _screen.Pages);
        }

        /// <summary>Test: the kit phone's first folder with an app, or why not.</summary>
        public static void TestFolder()
        {
            if (!Ready("folder"))
            {
                return;
            }
            for (int f = 0; f < PhoneLayoutRules.FolderCount; f++)
            {
                if (PhoneItemsGame.Items.Members(f) > 0)
                {
                    _screen.OpenFolder(f);
                    Report("folder", true, "the folder " + PhoneLayoutRules.FolderIds[f] + " open, " + PhoneItemsGame.Items.Members(f) + " apps");
                    return;
                }
            }
            Report("folder", false, "no folder has an app (App folders on the phone may be off)");
        }

        /// <summary>Test: the phone switched between the classic phone and the RLO phone for this session.</summary>
        public static void TestClassic()
        {
            UiScreenSwitches.Set(UiScreenSwitches.OptionOf(PhoneScreen.ScreenId), !UiScreenSwitches.On(PhoneScreen.ScreenId));
            bool on = UiScreenSwitches.On(PhoneScreen.ScreenId);
            if (!on)
            {
                Remove("the test switched it off");
            }
            else
            {
                _gaveUp = false;
                Rearm();
                Install();
            }
            Notifications.Show(NotificationType.Info, on ? "reallife_phone_kit_test_on" : "reallife_phone_kit_test_off", null, 8f, null, null, true, false);
            string now = on ? "the RLO phone again (" + State + ")" : "the classic one, the grid seen and touchable again";
            UiDirector.Logger?.Info("Phone check (switch): the phone is " + now + ": PASS.");
        }

        private static bool Ready(string test)
        {
            if (Showing && _screen != null)
            {
                return true;
            }
            Notifications.Show(NotificationType.Error, "reallife_phone_kit_test_none", null, 8f, null, null, true, false);
            UiDirector.Logger?.Info("Phone check (" + test + "): the RLO phone is not showing (" + State + ").");
            return false;
        }

        private static void Report(string test, bool ok, string said)
        {
            _screen.Touch();
            UiDirector.Logger?.Info("Phone check (" + test + "): " + said + "; " + PhoneItemsGame.Items.Count + " apps, the nav bar at the screen's foot on every view: "
                                    + (ok ? "PASS" : "FAIL") + ".");
            Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_phone_kit_test_pass" : "reallife_phone_kit_test_fail", null, 8f, null, null, true, false);
        }
    }

    /// <summary>
    /// On the kit phone: tells it the moment the game shows or hides its phone, and runs its frame while it shows. Runs
    /// before the game's own key handling, so Esc in a folder and the Phone key are the phone's first. Cost a frame: a few
    /// comparisons and input reads while the phone shows; nothing while it is hidden.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class PhoneKitWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "PhoneKitWatch", typeof(PhoneKitWatch));
        private static readonly int HotUpdate = HotSpots.Slot("Ui: PhoneKitWatch.Update");

        private float _nextLook;

        private void OnEnable()
        {
            if (!FrameGuard.Off)
            {
                PhoneKit.Shown();
            }
        }

        private void OnDisable()
        {
            if (!FrameGuard.Off)
            {
                PhoneKit.Hidden();
            }
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                PhoneKit.Frame(Time.unscaledTime, ref _nextLook);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                PhoneKit.Fallback(e);
            }
        }
    }
}
