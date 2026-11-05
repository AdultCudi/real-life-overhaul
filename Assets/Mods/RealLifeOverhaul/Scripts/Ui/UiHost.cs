using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Runs one kit screen: lays it out when its view model, the frame, the look, the pad or the input's lead changes,
    /// reads the game's bindings and a pad through its input (arrows or d-pad move the one focus, held directions repeat
    /// as a game's do, Confirm presses, Back closes what is open, the tab keys step tabs), plays the game's own sounds,
    /// keeps the focused row of a list in view, and opens a real text field for typing, so the game holds its shortcuts
    /// while words are typed. The mouse and the keys each lead in turn (UiFocusMode): the ring is the keys' alone, so a
    /// hover never moves the focus or makes a sound. Runs before the game's own Update (as the mod's other Escape-takers
    /// do), so Back a screen takes is not also the game's. Cost a frame: nothing while its screen is hidden, and while it
    /// shows a few input reads and comparisons, a layout only on a change. While a field takes the keys the screen's caret
    /// (UiDraw.Caret) blinks on the clock: one comparison a frame and one item shown or hidden twice a second, never a layout,
    /// and nothing at all while no field types. What its screen costs while it shows is counted
    /// by name for the session record (UiCost, Developer diagnostics on), with one Verbose line as it closes.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class UiHost : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiHost", typeof(UiHost));   // its Update (Core/Guard)
        private static readonly int HotUpdate = HotSpots.Slot("Ui: UiHost.Update");
        private static readonly int HotShow = HotSpots.Slot("Ui: UiHost.Show");   // also run by the game's own enabling of a screen
        private static readonly int HotHide = HotSpots.Slot("Ui: UiHost.Hide");

        private const float RepeatDelay = 0.35f;   // seconds a direction is held before it repeats
        private const float RepeatEvery = 0.08f;   // seconds between repeats
        private const int SearchLimit = 64;        // characters a search field takes
        private const float BlinkEvery = 0.5f;     // seconds the caret shows, then hides, as a normal text box blinks
        private const float SolidFor = 0.6f;       // seconds the caret stays shown after a layout (a key typed lays the screen out): it never blinks mid-word

        private readonly UiScene _scene = new UiScene();
        private int _beforeFocus = -2, _beforeLaid = -1;   // the focus and layout count as a press began, for its trace
        private string _beforeNode, _confirmNote;
        private string _pressNote;
        private int _focusLaid;
        private int _beforeCommand = -1, _beforeArg;
        private readonly UiFocusMode _mode = new UiFocusMode(UiLead.Pointer);
        private UiView _view;
        private IUiScreen _screen;
        private IUiInput _input;
        private IUiSound _sound;
        private Func<bool> _dark;
        private Func<UiMotionSetting> _motion;
        private float _top, _band;
        private string _title;
        private UiIcon _icon;
        private Vector2 _laidSize;
        private bool _laidDark, _arrive, _typing, _typeSoon, _held;
        private int _typeCommand = -1, _typeArg;   // the target that takes the keys while _typing: the scene tells its field to draw the caret
        private bool _caretOn;                      // the caret shows now (the blink's state)
        private float _blinkAt;                     // the clock time of the blink's next turn
        private UiPad _laidPad;
        private int _laidFaces;   // UiFaces.Version at the last layout: a game picture that arrived since lays the screen out again
        private int _heldX, _heldY;
        private int _laid;   // layouts made, so a press can tell that its own focus laid the screen out again
        private float _nextRepeat;
        private UiInputState _state;
        private bool _dragging;      // a target is held by the pointer (IUiDragging)
        private readonly UiWindowGrip _grip = new UiWindowGrip();   // the window held by its title strip (UiWindowDrag)
        private int _placesSeen;
        private Vector2 _dragAt;     // where, in px of the layout
        private TMP_InputField _field;
        private UnityEngine.Events.UnityAction<string> _onTyped, _onTypingEnded, _onSubmit;
        private UiCost _cost;

        /// <summary>The screen stands over a panel of the game's that answers the same keys: the host takes them from the game each frame.</summary>
        public bool Exclusive;
        public bool TakesInteract;   // only Interact is taken from the game each frame, for a card that answers it and no other key

        /// <summary>The screen takes no key, only the pointer (the notices over the world: a toast never steals the player's walk).</summary>
        public bool PointerOnly;

        /// <summary>
        /// The window id the player may move this screen's window by (its title strip), as the game's own small windows are moved; null
        /// for a screen that stands still. <see cref="WindowSaved"/>: the place is kept on this computer for the next session.
        /// </summary>
        public string WindowId
        {
            get => _grip.Id;
            set => _grip.Id = value;
        }

        public bool WindowSaved
        {
            get => _grip.Saved;
            set => _grip.Saved = value;
        }

        /// <summary>Called every frame the window is held by its strip: the game file tells the game's own mouse and map camera that a window is being dragged.</summary>
        public Action WindowHeld;

        /// <summary>Told once when the host stops after a fault, so its owner gives the game's own screen back.</summary>
        public Action<Exception> Failed;

        /// <summary>The screen's id in UiScreens, which another mod's fault is blamed on and its breaker opened for; null for a piece of the frame.</summary>
        public string ScreenId;

        /// <summary>
        /// True while the screen is on its way out (the full menu closing, as View on map closes it on its way to the map):
        /// nothing is laid out or read for a screen leaving, and its owner hides it. Null for a screen that never leaves so.
        /// </summary>
        public Func<bool> Leaving;

        /// <summary>For the press line only (Developer diagnostics): what holds the screen's keys now, from its owner; null for a screen never held.</summary>
        public Func<string> HeldBy;

        /// <summary>What the screen costs while it shows, by name (UiCost); named by its owner through <see cref="Measure"/>.</summary>
        public UiCost Cost => _cost;

        /// <summary>
        /// The frame's answer to what is not the screen's: the status bar's app row and All apps (UiDraw.AppRowCommand and
        /// after), and the window's close at the screen's top level, which closes the full menu as the game's own close does.
        /// Null outside the full menu: those targets then do nothing.
        /// </summary>
        public Func<int, int, bool> Chrome;

        /// <summary>The scene last laid out, for checks and the test that reports a screen.</summary>
        public UiScene Scene => _scene;

        /// <summary>The screen this host runs.</summary>
        public IUiScreen Screen => _screen;

        /// <summary>The view, for the host's owner to set the canvas scale on (2 in the game).</summary>
        public UiView View => _view;

        /// <summary>Sets the screen and what it reads: the host's rect is the whole full menu, its masthead and band the kit's own, titled with the app's name.</summary>
        public void Bind(IUiScreen screen, IUiInput input, IUiSound sound, Func<bool> dark, Func<UiMotionSetting> motion, float top, float band, string title,
            UiIcon icon = UiIcon.None)
        {
            _icon = icon;
            _screen = screen;
            _input = input;
            _sound = sound;
            _dark = dark;
            _motion = motion;
            _top = top;
            _band = band;
            _title = title;
            if (_view == null)
            {
                _view = new UiView(transform, "Screen");
                _view.PointerEntered = Guard.Wrap<int>(PointerEntered);
                _view.PointerExited = Guard.Wrap<int>(PointerExited);
                _view.PointerPressed = Guard.Wrap<int>(PressedSafe);
                _view.PointerReport = PressTrace;
                _view.Wheeled = Guard.Wrap<int, float>(WheeledSafe);
                _view.PointerDragged = Guard.Wrap<int, UiDragPoint>(PointerDragged);
                _view.PointerMoved = Guard.Wrap<int, UiDragPoint>(PointerMoved);
                _view.Tracked = Tracked;
            }
            _laidSize = Vector2.zero;
            if (_cost == null)
            {
                string type = screen != null ? screen.GetType().Name : null;
                Measure(type != null && type.Length > 6 && type.EndsWith("Screen", StringComparison.Ordinal) ? type.Substring(0, type.Length - 6) : type, false);
            }
        }

        /// <summary>The name the screen's cost goes by in the session record, and whether it is a piece of the HUD (no line of its own).</summary>
        public void Measure(string name, bool hud)
        {
            bool showing = _cost != null && _cost.Showing;
            _cost?.Hidden(Time.unscaledTime);
            _cost = new UiCost(name, hud);
            if (showing)
            {
                _cost.Shown(Time.unscaledTime);
            }
        }

        private UiCost.Scope Measured(UiWork work) => _cost != null ? _cost.Time(work) : default;

        // Destroyed with its city or its owner: a screen that still showed stops counting as one.
        private void OnDestroy()
        {
            _cost?.Hidden(Time.unscaledTime);
        }

        /// <summary>The screen is shown: laid out fresh, arriving, focus on its first target; the host runs while it shows.</summary>
        public void Show()
        {
            using var hotSpot = HotSpots.Time(HotShow);
            if (_screen == null)
            {
                return;
            }
            _cost?.Shown(Time.unscaledTime);
            using var cost = Measured(UiWork.Open);
            _arrive = true;
            _laidSize = Vector2.zero;
            enabled = true;
            _mode.Start(_input != null && _input.Keys != null && _input.Keys.Pad != UiPad.None);
            _view.Ring = _mode.Ring;
            Layout();
            _view.Open();
            _view.SetFocus(UiFocusRules.First(_scene), false, Time.unscaledTime);
            _typeSoon = _screen is IUiTypingSubmit && !(_screen is IUiTypingQuiet);   // a box for words takes the keys as it opens, once the game has filled it (next frame)
        }

        /// <summary>
        /// The keys have gone back to the game (a screen that holds them only while asked, as the phone does): the ring goes
        /// and the mouse leads again, nothing else moves.
        /// </summary>
        public void PointerLeads()
        {
            if (_screen == null || _view == null)
            {
                return;
            }
            _mode.Start(false);
            LeadChanged();
        }

        /// <summary>The screen is hidden: typing ends, a drag held is let go of, motion stops where it ends, the host sleeps.</summary>
        public void Hide()
        {
            using var hotSpot = HotSpots.Time(HotHide);
            EndTyping();
            EndDrag(true);
            _grip.End();
            _view?.Still();
            enabled = false;
            string line = _cost?.Hidden(Time.unscaledTime);
            if (line != null && Meta.Dev.Verbose)
            {
                UiDirector.Logger?.Info(line);
            }
        }

        // The screen was handed away by what it just did (the game closed it, or the full menu is closing on the way to the
        // map): a layout now would be drawn for nobody, and its owner hides it. Null while it is in front, held or not.
        private string GoneWhy() => UiHostRules.Gone(gameObject.activeInHierarchy, enabled, Leaving != null && Leaving());

        private bool Gone() => GoneWhy() != null;

        /// <summary>What the frame draws round the screen changed (the status bar's app row): it is laid out again next frame.</summary>
        public void Again()
        {
            _laidSize = Vector2.zero;
        }

        /// <summary>
        /// Something stands over the screen (a box, All apps, another mod's window): its keys rest, and it stays in front and
        /// running, so the mouse still works on it and what it does is drawn at once. No arrival when it gets the keys back,
        /// since it never left.
        /// </summary>
        public void Hold(bool hold)
        {
            if (_screen == null)
            {
                return;
            }
            if (hold)
            {
                EndTyping();
                _view?.SetHover(-1);
            }
            if (_held != hold)
            {
                _held = hold;
                if (!Gone())
                {
                    Layout();   // a held screen drops its key caps and prompts: the keys are the layer's over it, one key one thing
                }
            }
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            using var cost = Measured(UiWork.Frame);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults (Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e) when (Foreign(e))
            {
                Failed?.Invoke(e);   // another mod's code threw while the screen read or pressed its piece: blamed by name, the screen the game's own, not the kit's fault
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                Failed?.Invoke(e);
            }
        }

        private void OnUpdate()
        {
            if (_screen == null || _input == null)
            {
                enabled = false;
                return;
            }
            if (Leaving != null && Leaving())
            {
                return;   // on its way out: nothing laid out or read, and its owner hides it this frame
            }
            float now = Time.unscaledTime;
            _state = default(UiInputState);
            if (_grip.Moving)
            {
                WindowHeld?.Invoke();
            }
            if (PointerOnly || _held)
            {
                // No key is read here (a held screen's are the layer's over it), but the screen is in front and takes the mouse,
                // so it is drawn as it changes.
                if (_screen.Dirty || UiFaces.Version != _laidFaces || ((RectTransform)transform).rect.size != _laidSize || (WindowId != null && UiWindowPlaces.Version != _placesSeen))
                {
                    Layout();
                }
                return;
            }
            if (_typeSoon)
            {
                _typeSoon = false;
                if (_screen is IUiTypingSubmit box)
                {
                    BeginTyping(box, box.TypingCommand, 0);
                }
            }
            _input.Read(ref _state);
            if (Exclusive)
            {
                _input.TakeAll();
            }
            else if (TakesInteract)
            {
                _input.TakeInteract();
            }
            if (_state.Pointed && _mode.PointerMoved())
            {
                LeadChanged();
            }
            Vector2 size = ((RectTransform)transform).rect.size;
            bool dark = _dark != null && _dark();
            if (_screen.Dirty || UiFaces.Version != _laidFaces || size != _laidSize || dark != _laidDark || (_state.Pad != _laidPad && !_typing)
                || (WindowId != null && UiWindowPlaces.Version != _placesSeen))
            {
                Layout();
            }
            if (_typing)
            {
                if (_state.Back)
                {
                    _input.TakeBack();
                    EndTyping();
                    _sound?.Play(UiCue.Back);
                }
                else if (now >= _blinkAt)
                {
                    Blink(now);
                }
                return;
            }
            if (_dragging && _screen is IUiDragging held)
            {
                // Every frame while it is held, so a board scrolls when the pointer rests at its edge.
                held.DragMove(_dragAt.x, _dragAt.y);
                if (_screen.Dirty)
                {
                    Layout();
                }
            }
            Navigate(now);
            if (_view.Moving)
            {
                _view.StepFocus(now);
            }
        }

        private void Navigate(float now)
        {
            int dx = _state.MoveX, dy = _state.MoveY;
            bool step = false;
            if (dx != _heldX || dy != _heldY)
            {
                _heldX = dx;
                _heldY = dy;
                step = dx != 0 || dy != 0;
                _nextRepeat = now + RepeatDelay;
            }
            else if ((dx != 0 || dy != 0) && now >= _nextRepeat)
            {
                step = true;
                _nextRepeat = now + RepeatEvery;
            }
            if ((step || _state.Confirm) && _mode.Woke())
            {
                if (_state.Confirm)
                {
                    _confirmNote = Time.frameCount + " woke the ring, pressed nothing";
                }
                LeadChanged();   // the press only shows where the focus is: nobody acts on a target they could not see
                return;
            }
            if (step && !Steered(dx != 0 ? dx : 0, dx != 0 ? 0 : dy))
            {
                Move(dx != 0 ? dx : 0, dx != 0 ? 0 : dy, now);
            }
            if (_state.Page != 0 && _screen is IUiPaging paging)
            {
                paging.Page(_state.Page);
                if (_screen.Dirty)
                {
                    Layout();
                }
            }
            if (_state.Confirm)
            {
                _confirmNote = Time.frameCount + " pressed " + NodeName(_view.Focus);
                PressNode(_view.Focus, now);
            }
            if (_state.Back && _dragging)
            {
                _input.TakeBack();
                EndDrag(true);   // Back lets go of a drag, and what it moved goes back
                return;
            }
            if (_state.Back && !(_screen is IUiGameBack back && !back.TakesBack))   // a screen that cannot answer Cancel now leaves it the game's
            {
                int cancel = UiFocusRules.Keyed(_scene, UiAction.Back);
                if (cancel >= 0)
                {
                    _input.TakeBack();
                    PressKeyed(cancel, now);   // the target that shows Back (a question's Keep, a form's Cancel) is what Back does
                }
                else if (_screen.Back())
                {
                    _input.TakeBack();
                    _sound?.Play(UiCue.Back);
                }
            }
            if (_state.View && Chrome != null && Chrome(UiDraw.AllAppsCommand, 0))
            {
                _sound?.Play(UiCue.Confirm);   // the pad's View: All apps, as the status bar's tile
            }
            if (_state.TabPrev || _state.TabNext)
            {
                int strip = _view.Focus >= 0 && _view.Focus < _scene.NodeCount ? _scene.Nodes[_view.Focus].Command : -1;
                _screen.Tab(_state.TabPrev ? -1 : 1);
                _sound?.Play(UiCue.Tab);
                if (_screen.Dirty)
                {
                    Layout();
                }
                FollowShown(strip);
            }
            UiAction key = _state.Alt ? UiAction.Alt : _state.Option ? UiAction.Option : _state.Pick > 0 && _state.Pick <= 9 ? (UiAction)((int)UiAction.Pick1 + _state.Pick - 1) : UiAction.Count;
            if (key != UiAction.Count)
            {
                int keyed = UiFocusRules.Keyed(_scene, key);
                if (keyed >= 0)
                {
                    PressKeyed(keyed, now);   // one key, one target: the cap drawn on it says so
                }
                else if (key == UiAction.Alt || key == UiAction.Option)
                {
                    bool done = _screen.Act(key);
                    _sound?.Play(done ? UiCue.Confirm : UiCue.Error);
                }
            }
        }

        // A target pressed by its own key: with the ring up the focus goes to it first, so the press is seen where it lands.
        private void PressKeyed(int node, float now)
        {
            FocusNode(node, now, _mode.Ring);
            PressNode(node, now);
        }

        // The tab keys moved what a strip shows (tabs, groups). A ring left on the old one would say two things at once,
        // so the focus goes with the shown one when it stood in that strip.
        private void FollowShown(int strip)
        {
            if (strip < 0 || _view.Focus < 0 || _view.Focus >= _scene.NodeCount || _scene.Nodes[_view.Focus].Command != strip)
            {
                return;
            }
            for (int i = 0; i < _scene.NodeCount; i++)
            {
                int face = _scene.Nodes[i].Face;
                if (_scene.Nodes[i].Command == strip && face >= 0 && (_scene.Items[face].Flags & UiFlags.Selected) != 0)
                {
                    _view.SetFocus(i, false, Time.unscaledTime);
                    return;
                }
            }
        }

        // The keys or the mouse took the lead: the ring comes or goes, the hover goes or comes back, and the band is laid
        // again, since it names Move and Select only for the keys.
        private void LeadChanged()
        {
            _view.Ring = _mode.Ring;
            _view.SetHover(_mode.Ring ? -1 : _mode.Hover);
            Layout();
        }

        private void Move(int dx, int dy, float now)
        {
            int from = _view.Focus;
            if (dx != 0 && Stepped(from, dx))
            {
                return;
            }
            int next = UiFocusRules.Next(_scene, from, dx, dy);
            if (next == UiFocusRules.ScrollAsked && from >= 0)
            {
                ref UiNode n = ref _scene.Nodes[from];
                int want = n.Index + (dy > 0 ? 1 : -1);
                int list = n.List;
                _screen.Reveal(list, want);
                Layout();
                next = FindRow(list, want);
            }
            if (next < 0)
            {
                return;
            }
            FocusNode(next, now, true);
        }

        private void FocusNode(int node, float now, bool glide)
        {
            if (node == _view.Focus)
            {
                return;
            }
            _view.SetFocus(node, glide && _mode.Ring, now);
            if (_mode.Ring)
            {
                _sound?.Play(UiCue.Move);   // the hover tick is the keys' step: a click lands silently on its own target
            }
            ref UiNode n = ref _scene.Nodes[node];
            int command = n.Command, arg = n.Arg;
            if (n.List >= 0 && n.Clip >= 0 && !InView(node))
            {
                _screen.Reveal(n.List, n.Index);
            }
            _screen.Focused(command, arg);
            if (_screen.Dirty)
            {
                Layout();
            }
        }

        private void PressNode(int node, float now)
        {
            if (node < 0 || node >= _scene.NodeCount)
            {
                _pressNote = "no target to press";
                return;
            }
            using var cost = Measured(UiWork.Press);
            ref UiNode n = ref _scene.Nodes[node];
            if (n.Disabled)
            {
                _sound?.Play(UiCue.Error);
                _pressNote = "target disabled";
                return;
            }
            _view.Press(now);
            if (_screen is IUiTyping typing && n.Command == typing.TypingCommand)
            {
                _pressNote = "opened typing";
                BeginTyping(typing, n.Command, n.Arg);
                _sound?.Play(UiCue.Confirm);
                return;
            }
            if (n.Command == UiDraw.CloseCommand)
            {
                _pressNote = "close";
                // A window's close button does what Back does, so a screen with pages steps back before it closes; at its
                // top level the frame closes the full menu, as the game's own close does.
                bool back = _screen.Back() || (Chrome != null && Chrome(n.Command, n.Arg));
                _sound?.Play(back ? UiCue.Back : UiCue.Error);
                if (_screen.Dirty && !Gone())
                {
                    Layout();
                }
                return;
            }
            if (UiDraw.IsChrome(n.Command))
            {
                _pressNote = "frame";
                _sound?.Play(Chrome != null && Chrome(n.Command, n.Arg) ? UiCue.Confirm : UiCue.Error);
                return;
            }
            bool done = _screen.Press(n.Command, n.Arg);
            if (Meta.Dev.Verbose)
            {
                // So a click drawn late reads from the press line alone: what kept it from being laid out, and what held the keys.
                _pressNote = "screen.Press " + (done ? "true" : "false") + ", dirty " + (_screen.Dirty ? "yes" : "no") + ", gone " + (GoneWhy() ?? "no")
                    + ", keys " + (_held ? "held by " + (HeldBy?.Invoke() ?? "what stands over it") : "its own");
            }
            _sound?.Play(done ? UiCue.Confirm : UiCue.Error);
            if (_screen.Dirty && !Gone())
            {
                Layout();   // a press that handed the screen away (View on map) leaves it dirty: it is laid out fresh as it shows again
            }
        }

        // Lays the screen out and draws it; focus stays on the same target (by its command and argument) where it still is.
        private void Layout()
        {
            _laid++;
            using var cost = Measured(UiWork.Layout);
            var rect = (RectTransform)transform;
            Vector2 size = rect.rect.size;
            float scale = _view.Scale;
            var frame = new UiFrame { Width = size.x / scale, Height = size.y / scale, Top = _top, Band = _band, Title = _title, Icon = _icon, Pointer = !_mode.Ring || _held, Held = _held };
            int command = -1, arg = 0;
            if (_view.Focus >= 0 && _view.Focus < _scene.NodeCount)
            {
                command = _scene.Nodes[_view.Focus].Command;
                arg = _scene.Nodes[_view.Focus].Arg;
            }
            bool dark = _dark != null && _dark();
            _view.Motion = _motion != null ? _motion() : UiMotionSetting.Full;
            IUiKeys keys = _input != null ? _input.Keys : UiDefaultKeys.Keyboard;
            _scene.TypingCommand = _typing ? _typeCommand : -1;   // the field that takes the keys draws its caret (UiDraw.Typing)
            _scene.TypingArg = _typeArg;
            _scene.Draggable = WindowId != null;
            _screen.Lay(_scene, frame, dark, keys);
            PlaceWindow(frame);
            _laidSize = size;
            _laidDark = dark;
            _laidPad = keys.Pad;
            _laidFaces = UiFaces.Version;
            bool arrive = _arrive;
            _arrive = false;
            _view.Write(_scene, arrive);
            _caretOn = true;
            _blinkAt = Time.unscaledTime + SolidFor;   // a layout is a key typed or a field just opened: the caret shows solid a moment
            int again = command >= 0 && !_scene.FocusReset ? _scene.FindNode(command, arg) : -1;
            int top = UiFocusRules.TopLayer(_scene);
            if (again < 0 || _scene.Nodes[again].Layer != top)
            {
                again = UiFocusRules.First(_scene);
            }
            _view.SetFocus(again, false, Time.unscaledTime);
            _mode.Rehover(_view.PointerNode());
            _view.SetHover(_mode.Ring ? -1 : _mode.Hover);
        }

        private int FindRow(int list, int index)
        {
            for (int i = 0; i < _scene.NodeCount; i++)
            {
                if (_scene.Nodes[i].List == list && _scene.Nodes[i].Index == index)
                {
                    return i;
                }
            }
            return -1;
        }

        private bool InView(int node)
        {
            ref UiNode n = ref _scene.Nodes[node];
            ref UiClip c = ref _scene.Clips[n.Clip];
            float top = n.Y - c.Offset, bottom = top + n.H;
            return top >= -0.5f && bottom <= c.H + 0.5f;
        }

        // ------------------------------------------------------------------------------------------------- pointer

        // A hover only lifts the target's face: the focus stays where the keys or a click left it, and nothing sounds.
        // The guard's catch counts a fault against the interface; a fault of another mod's code is caught here first (an exception filter: no
        // cost unless one is thrown), logged once with its mod, and the screen is the game's own: the breaker, not the guard.
        private bool Foreign(Exception e)
        {
            if (!UiForeignGame.Caught(e, ScreenId, _cost != null ? _cost.Name : null))
            {
                return false;
            }
            return true;
        }

        private void PressedSafe(int node)
        {
            try
            {
                PointerPressed(node);
            }
            catch (Exception e) when (Foreign(e))
            {
                Failed?.Invoke(e);
            }
        }

        private void WheeledSafe(int clip, float notches)
        {
            try
            {
                Wheeled(clip, notches);
            }
            catch (Exception e) when (Foreign(e))
            {
                Failed?.Invoke(e);
            }
        }

        private void PointerDragged(int node, UiDragPoint at)
        {
            try
            {
                DragCore(node, at);
            }
            catch (Exception e) when (Foreign(e))
            {
                Failed?.Invoke(e);
            }
        }

        private void PointerEntered(int node)
        {
            using var cost = Measured(UiWork.Pointer);
            if (!_typing && node >= 0 && node < _scene.NodeCount && _scene.Nodes[node].Layer == UiFocusRules.TopLayer(_scene) && _mode.Entered(node))
            {
                _view.SetHover(node);
                Pointed(_scene.Nodes[node].Command, _scene.Nodes[node].Arg);
            }
        }

        private void PointerExited(int node)
        {
            using var cost = Measured(UiWork.Pointer);
            if (_mode.Exited(node))
            {
                _view.SetHover(-1);
                Pointed(-1, -1);
            }
        }

        // A slider, a choice or an amount with focus takes Left and Right as its step (IUiStepping), so the d-pad stays in its column.
        private bool Stepped(int node, int dx)
        {
            if (!(_screen is IUiStepping stepping) || node < 0 || node >= _scene.NodeCount || _scene.Nodes[node].Disabled || !stepping.Steps(_scene.Nodes[node].Command))
            {
                return false;
            }
            stepping.Step(_scene.Nodes[node].Command, _scene.Nodes[node].Arg, dx);
            _sound?.Play(UiCue.Move);
            if (_screen.Dirty)
            {
                Layout();
            }
            return true;
        }

        // The screen hears what the mouse rests on (IUiPointing), for a reading the keys' focus would show.
        private void Pointed(int command, int arg)
        {
            if (_screen is IUiPointing pointing)
            {
                pointing.Pointed(command, arg);
                if (_screen.Dirty)
                {
                    Layout();
                }
            }
        }

        // A click is the mouse leading: the focus goes to what was clicked without a ring, so the keys pick up from there.
        private void PointerPressed(int node)
        {
            using var cost = Measured(UiWork.Press);
            _beforeFocus = _view.Focus;
            _beforeNode = NodeName(node);
            _beforeCommand = node >= 0 && node < _scene.NodeCount ? _scene.Nodes[node].Command : -1;
            _beforeArg = node >= 0 && node < _scene.NodeCount ? _scene.Nodes[node].Arg : 0;
            _beforeLaid = _laid;
            if (node < 0 || node >= _scene.NodeCount)
            {
                if (_typing)
                {
                    EndTyping();
                }
                return;
            }
            if (_scene.Nodes[node].Handle)
            {
                return;   // a click on a window's title strip does nothing: the strip is only a grip
            }
            // Ending a field or moving the focus can lay the screen out again (a screen that reads Focused), which renumbers the targets:
            // the press is found again by what it is, so it never lands on the target that took its number.
            int command = _scene.Nodes[node].Command, arg = _scene.Nodes[node].Arg, layer = _scene.Nodes[node].Layer, laid = _laid;
            if (_typing)
            {
                EndTyping();
            }
            if (_laid != laid)
            {
                node = UiPress.Find(_scene, command, arg, layer);
            }
            if (node >= 0 && _scene.Nodes[node].Layer == UiFocusRules.TopLayer(_scene))
            {
                if (_mode.PointerMoved())
                {
                    _view.Ring = false;
                }
                laid = _laid;
                FocusNode(node, Time.unscaledTime, false);
                _focusLaid = _laid - laid;
                _pressNote = "not reached";
                if (_laid != laid)
                {
                    node = UiPress.Find(_scene, command, arg, layer);
                }
                PressNode(node, Time.unscaledTime);
            }
        }

        // One line for a mouse press while a kit screen is up, written only on a press (Developer diagnostics on):
        // "UI press: <screen> frame F; down: ray <node>, here <node>, began on <node>; up: <pressed node or why none>; Confirm this frame: <what>;
        // other input this frame: <list or none>; focus A -> B; layouts N (M by the press)". A node reads "cmd/arg#number", or "none".
        private void PressTrace(UiPressReport r)
        {
            int beforeFocus = _beforeFocus == -2 ? _view.Focus : _beforeFocus;
            int laidBefore = _beforeLaid < 0 ? _laid : _beforeLaid;
            string pressed = r.Pressed >= 0 ? _beforeNode : r.Why;
            _beforeFocus = -2;
            _beforeLaid = -1;
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            string confirm = _confirmNote != null && _confirmNote.StartsWith(Time.frameCount + " ", StringComparison.Ordinal) ? _confirmNote.Substring(_confirmNote.IndexOf(' ') + 1) : "no";
            var others = new System.Text.StringBuilder();
            if (_state.Back) { others.Append(" Back"); }
            if (_state.Alt) { others.Append(" Alt"); }
            if (_state.Option) { others.Append(" Option"); }
            if (_state.TabPrev || _state.TabNext) { others.Append(" Tab"); }
            if (_state.View) { others.Append(" View"); }
            if (_state.Page != 0) { others.Append(" Page"); }
            if (_state.Pick != 0) { others.Append(" Pick").Append(_state.Pick); }
            if (_state.MoveX != 0 || _state.MoveY != 0) { others.Append(" Move"); }
            UiDirector.Logger?.Info("UI press: " + (_cost != null ? _cost.Name : "screen") + " frame " + Time.frameCount + "; down: ray " + NodeName(r.RayNode) + ", here " + NodeName(r.HereNode)
                + ", began on " + NodeName(r.DownNode) + "; up: " + pressed + "; Confirm this frame: " + confirm + "; other input this frame:" + (others.Length == 0 ? " none" : others.ToString())
                + "; steps: focusing laid out " + _focusLaid + ", press " + _pressNote + "; drawn after: " + Drawn(_beforeCommand, _beforeArg)
                + "; screen dirty after: " + (_screen != null && _screen.Dirty ? "yes" : "no")
                + "; focus " + NodeName(beforeFocus) + " -> " + NodeName(_view.Focus) + "; layouts " + _laid + " (" + (_laid - laidBefore) + " by the press)");
        }

        // What the layout now on show draws for the target just pressed: its face's fill and flags (a chosen chip or row reads Accent/Selected or RowSel/Selected).
        private string Drawn(int command, int arg)
        {
            int node = command >= 0 && _scene != null ? _scene.FindNode(command, arg) : -1;
            if (node < 0 || _scene.Nodes[node].Face < 0)
            {
                return "no face";
            }
            ref UiItem face = ref _scene.Items[_scene.Nodes[node].Face];
            return face.Fill + "/" + face.Flags;
        }

        private string NodeName(int node)
        {
            return _scene != null && node >= 0 && node < _scene.NodeCount ? _scene.Nodes[node].Command + "/" + _scene.Nodes[node].Arg + "#" + node : "none";
        }

        // A step the screen takes for what it holds chosen (a board's shift), the focus staying where it is.
        private bool Steered(int dx, int dy)
        {
            if (!(_screen is IUiSteering steering) || !steering.Steer(dx, dy, _state.Shift))
            {
                return false;
            }
            _sound?.Play(UiCue.Move);
            if (_screen.Dirty)
            {
                Layout();
            }
            return true;
        }

        // A target the screen follows the mouse across (IUiTracking): only on the layer that takes the pointer now.
        private bool Tracked(int node)
        {
            return _screen is IUiTracking tracking && node >= 0 && node < _scene.NodeCount && tracking.Tracks(_scene.Nodes[node].Command) && !_typing
                   && _scene.Nodes[node].Layer == UiFocusRules.TopLayer(_scene);
        }

        // The mouse moved over a tracked target: the screen reads where, and lays out again only if what it reads changed.
        private void PointerMoved(int node, UiDragPoint at)
        {
            using var cost = Measured(UiWork.Pointer);
            if (!(_screen is IUiTracking tracking) || node < 0 || node >= _scene.NodeCount)
            {
                return;
            }
            ref UiNode n = ref _scene.Nodes[node];
            tracking.Moved(n.Command, n.Arg, at.X, at.Y);
            if (_screen.Dirty)
            {
                Layout();
            }
        }

        // The pointer went down on a target or drags from it: a screen that takes hold of its targets hears where.
        private void DragCore(int node, UiDragPoint at)
        {
            using var cost = Measured(UiWork.Pointer);
            var px = new Vector2(at.X, at.Y);
            if (node >= 0 && node < _scene.NodeCount && _scene.Nodes[node].Handle)
            {
                if (_grip.Drag(at))
                {
                    Layout();
                }
                return;
            }
            if (!(_screen is IUiDragging dragging) || node < 0 || node >= _scene.NodeCount)
            {
                return;
            }
            ref UiNode n = ref _scene.Nodes[node];
            switch (at.Phase)
            {
                case UiDrag.Down:
                    dragging.PointerDown(n.Command, n.Arg, px.x, px.y);
                    break;
                case UiDrag.Begin:
                    _dragging = dragging.DragBegin(n.Command, n.Arg, px.x, px.y);
                    _dragAt = px;
                    break;
                case UiDrag.Move:
                    if (_dragging)
                    {
                        _dragAt = px;
                        dragging.DragMove(px.x, px.y);
                    }
                    break;
                default:
                    EndDrag(false);
                    break;
            }
            if (_screen.Dirty)
            {
                Layout();
            }
        }

        // The window's place is read from where the player left it unless it is held now.
        private void PlaceWindow(UiFrame frame)
        {
            _placesSeen = UiWindowPlaces.Version;
            _grip.Place(_scene, frame.Width, frame.BandY);
        }

        private void EndDrag(bool cancelled)
        {
            if (!_dragging)
            {
                return;
            }
            _dragging = false;
            if (_screen is IUiDragging dragging)
            {
                dragging.DragEnd(cancelled);
            }
        }

        private void Wheeled(int clip, float notches)
        {
            using var cost = Measured(UiWork.Pointer);
            if (notches != 0f)
            {
                _screen.Wheel(clip, notches);
                if (_screen.Dirty)
                {
                    Layout();
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- typing

        private void BeginTyping(IUiTyping typing, int command, int arg)
        {
            if (_field == null)
            {
                _field = MakeField(transform);
                _onTyped = Guard.Listener<string>(Typed);
                _onTypingEnded = Guard.Listener<string>(TypingEnded);
                _onSubmit = Guard.Listener<string>(Submitted);
                _field.onValueChanged.AddListener(_onTyped);
                _field.onEndEdit.AddListener(_onTypingEnded);
                _field.onSubmit.AddListener(_onSubmit);
            }
            _typing = true;
            _typeCommand = command;
            _typeArg = arg;
            _laidSize = Vector2.zero;   // laid out again next frame, with the caret in the field that takes the keys
            _field.characterLimit = typing is IUiTypingSubmit limited && limited.TypingLimit > 0 ? limited.TypingLimit : SearchLimit;
            _field.gameObject.SetActive(true);
            _field.SetTextWithoutNotify(typing.TypingText ?? string.Empty);
            _field.ActivateInputField();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_field.gameObject);   // the game holds its key shortcuts while a field is selected
            }
        }

        // The caret's blink: its one item shown or hidden on the clock, never a layout. Run only while a field takes the keys.
        private void Blink(float now)
        {
            _blinkAt = now + BlinkEvery;
            _caretOn = !_caretOn;
            _view.SetCaret(_caretOn);
        }

        private void Typed(string text)
        {
            if (_typing && _screen is IUiTyping typing)
            {
                typing.Typed(text);
            }
        }

        private void TypingEnded(string text)
        {
            EndTyping();
        }

        // Enter in the field: a screen that confirms on it (a text box) hears it, after the last letter has reached it.
        private void Submitted(string text)
        {
            if (_screen is IUiTypingSubmit submit)
            {
                submit.Typed(text);
                submit.Submitted(text);
            }
        }

        private void EndTyping()
        {
            if (!_typing)
            {
                return;
            }
            _typing = false;
            _typeCommand = -1;
            _laidSize = Vector2.zero;   // laid out again next frame, the caret gone
            if (_field != null)
            {
                _field.DeactivateInputField();
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _field.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
                _field.gameObject.SetActive(false);
            }
        }

        // A real text field, out of sight: the words show in the screen's own search box; the field carries the keyboard,
        // the system's input method (Chinese, Korean) and the game's rule that shortcuts wait while a field is selected.
        private static TMP_InputField MakeField(Transform parent)
        {
            RectTransform r = UiView.NewRect(parent, "Typing");
            TypingNow.OnGameLayer(r.gameObject);   // the layer the game's shortcuts look at: its letters stay out of them (GameManager.HasInputSelected)
            r.sizeDelta = new Vector2(1f, 1f);
            r.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            RectTransform area = UiView.NewRect(r, "Area");
            UiView.Stretch(area);
            area.gameObject.AddComponent<RectMask2D>();
            RectTransform tr = UiView.NewRect(area, "Text");
            UiView.Stretch(tr);
            var text = tr.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = UiFonts.For(UiStyle.Body);
            var field = r.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = SearchLimit;
            field.richText = false;
            r.gameObject.SetActive(false);
            return field;
        }
    }
}
