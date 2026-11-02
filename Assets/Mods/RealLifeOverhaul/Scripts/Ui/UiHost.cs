using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A screen that takes typed words on one of its targets (a search): the host opens a text field for it.</summary>
    public interface IUiTyping
    {
        /// <summary>The command whose target, pressed, starts typing.</summary>
        int TypingCommand { get; }

        /// <summary>The words already there when typing starts.</summary>
        string TypingText { get; }

        /// <summary>The words as they are typed.</summary>
        void Typed(string text);
    }

    /// <summary>A typing screen that sets its own limit and hears Enter (a text box of the game's, whose Enter confirms).</summary>
    public interface IUiTypingSubmit : IUiTyping
    {
        /// <summary>The most characters the field takes.</summary>
        int TypingLimit { get; }

        /// <summary>Enter pressed in the field: the screen confirms what was typed.</summary>
        void Submitted(string text);
    }

    /// <summary>
    /// Runs one kit screen: lays it out when its view model, the frame, the look, the pad or the input's lead changes,
    /// reads the game's bindings and a pad through its input (arrows or d-pad move the one focus, held directions repeat
    /// as a game's do, Confirm presses, Back closes what is open, the tab keys step tabs), plays the game's own sounds,
    /// keeps the focused row of a list in view, and opens a real text field for typing, so the game holds its shortcuts
    /// while words are typed. The mouse and the keys each lead in turn (UiFocusMode): the ring is the keys' alone, so a
    /// hover never moves the focus or makes a sound. Runs before the game's own Update (as the mod's other Escape-takers
    /// do), so Back a screen takes is not also the game's. Cost a frame: nothing while its screen is hidden, and while it
    /// shows a few input reads and comparisons, a layout only on a change.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class UiHost : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiHost", typeof(UiHost));   // its Update (Core/Guard)
        private static readonly int HotUpdate = HotSpots.Slot("Ui: UiHost.Update");

        private const float RepeatDelay = 0.35f;   // seconds a direction is held before it repeats
        private const float RepeatEvery = 0.08f;   // seconds between repeats
        private const int SearchLimit = 64;        // characters a search field takes

        private readonly UiScene _scene = new UiScene();
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
        private UiPad _laidPad;
        private int _heldX, _heldY;
        private float _nextRepeat;
        private UiInputState _state;
        private bool _dragging;      // a target is held by the pointer (IUiDragging)
        private Vector2 _dragAt;     // where, in px of the layout
        private TMP_InputField _field;
        private UnityEngine.Events.UnityAction<string> _onTyped, _onTypingEnded, _onSubmit;

        /// <summary>The screen stands over a panel of the game's that answers the same keys: the host takes them from the game each frame.</summary>
        public bool Exclusive;

        /// <summary>The screen takes no key, only the pointer (the notices over the world: a toast never steals the player's walk).</summary>
        public bool PointerOnly;

        /// <summary>Told once when the host stops after a fault, so its owner gives the game's own screen back.</summary>
        public Action<Exception> Failed;

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
                _view.PointerPressed = Guard.Wrap<int>(PointerPressed);
                _view.Wheeled = Guard.Wrap<int, float>(Wheeled);
                _view.PointerDragged = Guard.Wrap<int, UiDragPoint>(PointerDragged);
            }
            _laidSize = Vector2.zero;
        }

        /// <summary>The screen is shown: laid out fresh, arriving, focus on its first target; the host runs while it shows.</summary>
        public void Show()
        {
            if (_screen == null)
            {
                return;
            }
            _arrive = true;
            _laidSize = Vector2.zero;
            enabled = true;
            _mode.Start(_input != null && _input.Keys != null && _input.Keys.Pad != UiPad.None);
            _view.Ring = _mode.Ring;
            Layout();
            _view.Open();
            _view.SetFocus(UiFocusRules.First(_scene), false, Time.unscaledTime);
            _typeSoon = _screen is IUiTypingSubmit;   // a box for words takes the keys as it opens, once the game has filled it (next frame)
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
            EndTyping();
            EndDrag(true);
            _view?.Still();
            enabled = false;
        }

        /// <summary>What the frame draws round the screen changed (the status bar's app row): it is laid out again next frame.</summary>
        public void Again()
        {
            _laidSize = Vector2.zero;
        }

        /// <summary>
        /// Something of the kit's stands over the screen (a box, All apps): its keys rest and it stays drawn as it is, with no
        /// arrival when it gets them back, since it never left.
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
                Layout();   // a held screen drops its key caps and prompts: the keys are the layer's over it, one key one thing
            }
            enabled = !hold;
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults (Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
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
            float now = Time.unscaledTime;
            _state = default(UiInputState);
            if (PointerOnly)
            {
                if (_screen.Dirty || ((RectTransform)transform).rect.size != _laidSize)
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
                    BeginTyping(box);
                }
            }
            _input.Read(ref _state);
            if (Exclusive)
            {
                _input.TakeAll();
            }
            if (_state.Pointed && _mode.PointerMoved())
            {
                LeadChanged();
            }
            Vector2 size = ((RectTransform)transform).rect.size;
            bool dark = _dark != null && _dark();
            if (_screen.Dirty || size != _laidSize || dark != _laidDark || (_state.Pad != _laidPad && !_typing))
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
                PressNode(_view.Focus, now);
            }
            if (_state.Back && _dragging)
            {
                _input.TakeBack();
                EndDrag(true);   // Back lets go of a drag, and what it moved goes back
                return;
            }
            if (_state.Back)
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
                return;
            }
            ref UiNode n = ref _scene.Nodes[node];
            if (n.Disabled)
            {
                _sound?.Play(UiCue.Error);
                return;
            }
            _view.Press(now);
            if (_screen is IUiTyping typing && n.Command == typing.TypingCommand)
            {
                BeginTyping(typing);
                _sound?.Play(UiCue.Confirm);
                return;
            }
            if (n.Command == UiDraw.CloseCommand)
            {
                // A window's close button does what Back does, so a screen with pages steps back before it closes; at its
                // top level the frame closes the full menu, as the game's own close does.
                bool back = _screen.Back() || (Chrome != null && Chrome(n.Command, n.Arg));
                _sound?.Play(back ? UiCue.Back : UiCue.Error);
                if (_screen.Dirty)
                {
                    Layout();
                }
                return;
            }
            if (UiDraw.IsChrome(n.Command))
            {
                _sound?.Play(Chrome != null && Chrome(n.Command, n.Arg) ? UiCue.Confirm : UiCue.Error);
                return;
            }
            bool done = _screen.Press(n.Command, n.Arg);
            _sound?.Play(done ? UiCue.Confirm : UiCue.Error);
            if (_screen.Dirty)
            {
                Layout();
            }
        }

        // Lays the screen out and draws it; focus stays on the same target (by its command and argument) where it still is.
        private void Layout()
        {
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
            _screen.Lay(_scene, frame, dark, keys);
            _laidSize = size;
            _laidDark = dark;
            _laidPad = keys.Pad;
            bool arrive = _arrive;
            _arrive = false;
            _view.Write(_scene, arrive);
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
        private void PointerEntered(int node)
        {
            if (!_typing && node >= 0 && node < _scene.NodeCount && _scene.Nodes[node].Layer == UiFocusRules.TopLayer(_scene) && _mode.Entered(node))
            {
                _view.SetHover(node);
                Pointed(_scene.Nodes[node].Command, _scene.Nodes[node].Arg);
            }
        }

        private void PointerExited(int node)
        {
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
            if (_typing)
            {
                EndTyping();
            }
            if (node >= 0 && node < _scene.NodeCount && _scene.Nodes[node].Layer == UiFocusRules.TopLayer(_scene))
            {
                if (_mode.PointerMoved())
                {
                    _view.Ring = false;
                }
                FocusNode(node, Time.unscaledTime, false);
                PressNode(node, Time.unscaledTime);
            }
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

        // The pointer went down on a target or drags from it: a screen that takes hold of its targets hears where.
        private void PointerDragged(int node, UiDragPoint at)
        {
            var px = new Vector2(at.X, at.Y);
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

        private void BeginTyping(IUiTyping typing)
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
            _field.characterLimit = typing is IUiTypingSubmit limited && limited.TypingLimit > 0 ? limited.TypingLimit : SearchLimit;
            _field.gameObject.SetActive(true);
            _field.SetTextWithoutNotify(typing.TypingText ?? string.Empty);
            _field.ActivateInputField();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_field.gameObject);   // the game holds its key shortcuts while a field is selected
            }
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
