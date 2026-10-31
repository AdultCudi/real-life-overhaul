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

    /// <summary>
    /// Runs one kit screen: lays it out when its view model, the frame, the look or the pad changes; reads the game's
    /// bindings and a pad through its input (arrows or d-pad move the one focus, held directions repeat as a game's do,
    /// Confirm presses, Back closes what is open, the tab keys step tabs); plays the game's own sounds; keeps the focused
    /// row of a list in view; and opens a real text field for typing, so the game holds its shortcuts while words are
    /// typed. Runs before the game's own Update (as the mod's other Escape-takers do), so Back a screen takes is not also
    /// the game's. Cost a frame: nothing while its screen is hidden (disabled); while shown, a few input reads and
    /// comparisons, a layout only on a change.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class UiHost : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiHost", typeof(UiHost));   // its Update (Core/Guard)
        private static readonly int HotUpdate = HotSpots.Slot("Ui: UiHost.Update");

        private const float RepeatDelay = 0.35f;   // seconds a direction is held before it repeats
        private const float RepeatEvery = 0.08f;   // seconds between repeats

        private readonly UiScene _scene = new UiScene();
        private UiView _view;
        private IUiScreen _screen;
        private IUiInput _input;
        private IUiSound _sound;
        private Func<bool> _dark;
        private Func<UiMotionSetting> _motion;
        private float _band;
        private Vector2 _laidSize;
        private bool _laidDark, _arrive, _typing;
        private UiPad _laidPad;
        private int _heldX, _heldY;
        private float _nextRepeat;
        private UiInputState _state;
        private TMP_InputField _field;
        private UnityEngine.Events.UnityAction<string> _onTyped, _onTypingEnded;

        /// <summary>The scene last laid out, for checks and the test that reports a screen.</summary>
        public UiScene Scene => _scene;

        /// <summary>The screen this host runs.</summary>
        public IUiScreen Screen => _screen;

        /// <summary>The view, for the host's owner to set the canvas scale on (2 in the game).</summary>
        public UiView View => _view;

        /// <summary>Sets the screen and what it reads; the prompt band is the height under the host's own rect (the game's bottom bar).</summary>
        public void Bind(IUiScreen screen, IUiInput input, IUiSound sound, Func<bool> dark, Func<UiMotionSetting> motion, float band)
        {
            _screen = screen;
            _input = input;
            _sound = sound;
            _dark = dark;
            _motion = motion;
            _band = band;
            if (_view == null)
            {
                _view = new UiView(transform, "Screen");
                _view.PointerEntered = Guard.Wrap<int>(PointerEntered);
                _view.PointerPressed = Guard.Wrap<int>(PointerPressed);
                _view.Wheeled = Guard.Wrap<int, float>(Wheeled);
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
            Layout();
            _view.Open();
            _view.SetFocus(UiFocusRules.First(_scene), false, Time.unscaledTime);
        }

        /// <summary>The screen is hidden: typing ends, motion stops where it ends, the host sleeps.</summary>
        public void Hide()
        {
            EndTyping();
            _view?.Still();
            enabled = false;
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
            _input.Read(ref _state);
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
            if (step)
            {
                Move(dx != 0 ? dx : 0, dx != 0 ? 0 : dy, now);
            }
            if (_state.Confirm)
            {
                PressNode(_view.Focus, now);
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
            if (_state.TabPrev || _state.TabNext)
            {
                _screen.Tab(_state.TabPrev ? -1 : 1);
                _sound?.Play(UiCue.Tab);
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

        // A target pressed by its own key: the focus goes to it first, so the press is seen where it lands.
        private void PressKeyed(int node, float now)
        {
            FocusNode(node, now, true);
            PressNode(node, now);
        }

        private void Move(int dx, int dy, float now)
        {
            int from = _view.Focus;
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
            _view.SetFocus(node, glide, now);
            _sound?.Play(UiCue.Move);
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
            var frame = new UiFrame { Width = size.x / scale, Height = size.y / scale, Band = _band };
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
            int again = command >= 0 ? _scene.FindNode(command, arg) : -1;
            int top = UiFocusRules.TopLayer(_scene);
            if (again < 0 || _scene.Nodes[again].Layer != top)
            {
                again = UiFocusRules.First(_scene);
            }
            _view.SetFocus(again, false, Time.unscaledTime);
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

        private void PointerEntered(int node)
        {
            if (!_typing && node >= 0 && node < _scene.NodeCount && node != _view.Focus && _scene.Nodes[node].Layer == UiFocusRules.TopLayer(_scene))
            {
                FocusNode(node, Time.unscaledTime, true);
            }
        }

        private void PointerPressed(int node)
        {
            if (_typing)
            {
                EndTyping();
            }
            if (node >= 0 && node < _scene.NodeCount && _scene.Nodes[node].Layer == UiFocusRules.TopLayer(_scene))
            {
                FocusNode(node, Time.unscaledTime, false);
                PressNode(node, Time.unscaledTime);
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
                _field.onValueChanged.AddListener(_onTyped);
                _field.onEndEdit.AddListener(_onTypingEnded);
            }
            _typing = true;
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
            field.characterLimit = 64;
            field.richText = false;
            r.gameObject.SetActive(false);
            return field;
        }
    }
}
