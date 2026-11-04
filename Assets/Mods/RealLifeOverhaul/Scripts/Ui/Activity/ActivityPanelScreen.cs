using System;
using System.Collections.Generic;
using BigAmbitions.InputSystem;
using PlayerActivity;
using UI;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's activity panel (PlayerActivityUI) on the kit: before it starts, the side card over the dimmed world with
    /// the panel's headline, lines, slider and buttons; while the player walks there, waits, or it runs, the strip above
    /// the status bar with the meter, the clock, the time machine, fast forward and the panel's own buttons. The game's
    /// panel keeps running underneath and reads its own keys (Interact, Secondary, Special, the slider's keys with their
    /// repeat, Escape); the kit answers the pointer by pressing what the game's own button would, and shows the game's key
    /// on each cap. It shows what the game shows: nothing while the game's panel is faded out (the city map, a Going Public
    /// session's own panel) or a desk session's pages stand over it (SessionKit). Looks at the panel four times a second.
    /// Game side. Cost: a few reads a look; a layout only when what it shows changed.
    /// </summary>
    public sealed class ActivityPanelScreen : IUiScreen, IUiDragging
    {
        private const float Every = 0.25f;   // seconds between looks at the panel

        private readonly PlayerActivityUI _ui;
        private readonly CanvasGroup _fade;
        private readonly UiActivityModel _m = new UiActivityModel();
        private readonly List<ButtonInfo> _card = new List<ButtonInfo>(4);    // the card's buttons by index; null for the time machine
        private readonly List<ButtonInfo> _strip = new List<ButtonInfo>(4);   // the strip's; null for the way out while walking there
        private bool _dirty = true;
        private float _next;
        private int _seen;
        private bool _shown;                 // the last layout drew the panel (not an empty scene)
        private float _frameW, _frameH;      // the layout's size, last laid: the minimap's card is read into it

        public ActivityPanelScreen(PlayerActivityUI ui)
        {
            _ui = ui;
            _fade = GameContract.ActivityFade.Field != null ? GameContract.ActivityFade.Field.GetValue(ui) as CanvasGroup : null;
        }

        public string Id => "activity_panel";

        /// <summary>The strip's model while the strip shows, for the HUD to keep clear of it; null while it does not.</summary>
        public UiActivityModel Strip => _shown && UiActivity.Strip(_m.State) ? _m : null;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    int now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _dirty = false;
            IPlayerActivity a = _ui != null ? _ui.GetCurrentActivity : null;
            _shown = a != null && Visible(a);
            if (!_shown)
            {
                Empty(s, frame, dark);
                return;
            }
            Read(a);
            if (_m.State == UiActivityState.NotStarted)
            {
                UiActivity.LayCard(s, _m, frame, dark, keys);
                return;
            }
            s.Begin("activity", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            _frameW = frame.Width;
            _frameH = frame.Height;
            UiMinimapBox.Read(_frameW, _frameH, out _m.AvoidX, out _m.AvoidY, out _m.AvoidW, out _m.AvoidH);   // the strip keeps off the minimap's card
            UiActivity.StripPlace(frame, _m, out float x, out float y);
            UiActivity.DrawStrip(s, UiAt.Screen, _m, x, y);
        }

        private static void Empty(UiScene s, UiFrame frame, bool dark)
        {
            s.Begin("activity", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
        }

        // What the game shows of its panel now: faded out over the map or by a Going Public session it shows nothing (but
        // the walk there, which the game shows nothing of, has its strip while alone), and a desk session's pages stand
        // over it while they are chosen.
        private bool Visible(IPlayerActivity a)
        {
            if (SessionKit.Up)
            {
                return false;
            }
            bool faded = _fade != null && _fade.alpha < 0.5f;
            if (!faded)
            {
                return true;
            }
            PlayerActivityState state = a.GetState();
            return !GoingPublic.InSession && (state == PlayerActivityState.MovingTowardsActivity || state == PlayerActivityState.Waiting);
        }

        // The panel now as a number: what it is, its state, its buttons, its slider, its bar, the clock's minute and the speed.
        private int Signature()
        {
            IPlayerActivity a = _ui != null ? _ui.GetCurrentActivity : null;
            if (a == null)
            {
                return 0;
            }
            unchecked
            {
                int sig = a.GetHashCode() * 31 + (int)a.GetState();
                sig = sig * 31 + (Visible(a) ? 1 : 0);
                ButtonInfo[] buttons = a.GetButtons();
                for (int i = 0; buttons != null && i < buttons.Length; i++)
                {
                    sig = sig * 31 + (buttons[i] != null && buttons[i].name != null ? buttons[i].name.GetHashCode() : 0) + (buttons[i] != null && buttons[i].interactable ? 1 : 0);
                }
                LabelInfo[] labels = a.GetLabels();
                sig = sig * 31 + (labels != null ? labels.Length : -1);
                if (a.HasSlider())
                {
                    sig = sig * 31 + Mathf.RoundToInt(a.GetCurrentSliderValue());
                }
                if (a.HasProgressBar())
                {
                    sig = sig * 31 + Mathf.RoundToInt(a.GetProgressBarPercentageValue() * 4f);
                }
                sig = sig * 31 + (int)TimeHelper.NowInMinutes();
                GameSpeedController speed = InstanceBehavior<UIs>.Instance != null ? InstanceBehavior<UIs>.Instance.gameSpeed : null;
                sig = sig * 31 + (speed != null && speed.isFastForwarding ? 1 : 0) + (BuildingManager.IsInsideBuilding ? 2 : 0);
                if (_shown && UiActivity.Strip(_m.State))
                {
                    UiMinimapBox.Read(_frameW, _frameH, out float cx, out float cy, out float cw, out float ch);   // the card moved: the strip is laid again beside it
                    sig = sig * 31 + UiMinimapBox.Stamp(cx, cy, cw, ch);
                }
                return sig;
            }
        }

        // ------------------------------------------------------------------------------------------------- the model

        private void Read(IPlayerActivity a)
        {
            UiActivityState state = ActivityRead.State(a.GetState());
            _m.State = state;
            _m.Bar = false;   // the game's own HUD (or the kit's) has the bar under it
            _m.Headline = ActivityRead.Words(a.GetHeadlineLabel());
            _m.Title = null;
            _m.Icon = ActivityRead.Icon(a);
            _m.IconTint = UiRole.Gold;
            _m.Lines = ActivityRead.Rows(a.GetLabels());
            _m.Clock = TimeHelper.GetCurrentFormattedTime();
            UiStatusModel bar = UiChrome.Bar != null ? UiChrome.Bar() : null;
            _m.Place = bar != null && !string.IsNullOrEmpty(bar.Place) ? bar.Place : null;
            _m.PlaceLines = bar != null && !string.IsNullOrEmpty(bar.District) ? new[] { bar.District } : new string[0];
            _m.Kicker = null;
            _m.Body = null;
            _m.Person = _m.PersonLine = null;
            _m.Finish = _m.Sum = null;
            _m.MeterLeft = _m.MeterRight = null;
            _m.HasDuration = state == UiActivityState.NotStarted && a.HasSlider();
            if (_m.HasDuration)
            {
                int min = a.GetMinSliderValue(), max = a.GetMaxSliderValue();
                float now = a.GetCurrentSliderValue();
                (string key, object args) info = a.GetSliderInfo();
                _m.DurationLabel = UiText.T("reallife_ui_activity_how_long");
                _m.DurationValue = ActivityRead.Words(info.key, info.args);
                _m.DurationShare = max > min ? (now - min) / (max - min) : 0f;
                _m.DurationFrom = _m.DurationTo = null;
            }
            _m.HasProgress = a.HasProgressBar() && state != UiActivityState.NotStarted;
            if (_m.HasProgress)
            {
                (string key, object args) label = a.GetProgressBarLabel();
                _m.Progress = new UiProgressModel { Label = ActivityRead.Words(label.key, label.args), Share = Mathf.Clamp01(a.GetProgressBarPercentageValue() / 100f) };
            }
            _m.StateWords = state == UiActivityState.Moving ? UiText.T("reallife_ui_activity_moving") : state == UiActivityState.Waiting ? UiText.T("reallife_ui_activity_waiting")
                : state == UiActivityState.Finished ? UiText.T("reallife_ui_activity_done") : null;
            _m.CanSkip = a.HasTimeMachine();
            _m.SkipLabel = UiText.T("playeractivityui_timemachine");
            _m.SkipKey = ActivityRead.Key(PlayerAction.SpecialInteract);
            GameSpeedController speed = InstanceBehavior<UIs>.Instance != null ? InstanceBehavior<UIs>.Instance.gameSpeed : null;
            _m.CanFast = a.HasFastForward() && BuildingManager.IsInsideBuilding;
            _m.Fast = speed != null && speed.isFastForwarding;
            // The game's fast forward runs time at its one speed (TimeSpeed.FastForward, x6): the button says how fast.
            _m.FastLabel = UiText.Fill("reallife_ui_activity_fast_speed", "speed", UiText.Whole((int)TimeSpeed.FastForward));
            _m.FastKey = ActivityRead.Key(PlayerAction.SecondaryInteract);
            _m.CanCancel = state == UiActivityState.Moving;
            _m.CancelLabel = UiText.T("common_cancel");
            Buttons(a, state);
            ActivityFacts.Fill(a, _m);   // what the mod knows of its own activities beyond the game's panel (a working day's place and sums)
        }

        // The panel's buttons on the card (the way back at the left, the blue one the main one, the time machine beside it)
        // and on the strip (its own, then the way out while walking there).
        private void Buttons(IPlayerActivity a, UiActivityState state)
        {
            ButtonInfo[] buttons = a.GetButtons() ?? new ButtonInfo[0];
            _card.Clear();
            _strip.Clear();
            var card = new List<UiSideButton>(buttons.Length + 1);
            var strip = new List<UiSideButton>(buttons.Length + 1);
            foreach (ButtonInfo b in buttons)
            {
                if (b == null)
                {
                    continue;
                }
                bool back = b.playerAction == PlayerAction.Cancel;
                var kind = b.color == "blue" ? UiButtonKind.Primary : b.color == "red" ? UiButtonKind.Destructive : UiButtonKind.Secondary;
                var button = new UiSideButton { Label = ActivityRead.Words(b), Kind = back ? UiButtonKind.Secondary : kind, Key = UiAction.Count, GameKey = ActivityRead.Key(b.playerAction),
                    Disabled = !b.interactable, Left = back };
                card.Add(button);
                _card.Add(b);
                button.Kind = kind == UiButtonKind.Primary ? UiButtonKind.Primary : UiButtonKind.Secondary;
                strip.Add(button);
                _strip.Add(b);
            }
            if (state == UiActivityState.NotStarted && a.HasTimeMachine())
            {
                card.Add(new UiSideButton
                {
                    Label = UiText.T("playeractivityui_timemachine"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, GameKey = ActivityRead.Key(PlayerAction.SpecialInteract),
                });
                _card.Add(null);
            }
            if (state == UiActivityState.Moving && strip.Count == 0)
            {
                strip.Add(new UiSideButton { Label = UiText.T("common_cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, GameKey = ActivityRead.Key(PlayerAction.Cancel) });
                _strip.Add(null);
            }
            _m.Buttons = card.ToArray();
            _m.StripButtons = strip.ToArray();
        }

        // ------------------------------------------------------------------------------------------------- presses

        public bool Press(int command, int arg)
        {
            IPlayerActivity a = _ui != null ? _ui.GetCurrentActivity : null;
            if (a == null)
            {
                return false;
            }
            _dirty = true;
            switch (command)
            {
                case UiActivity.ButtonCommand:
                    if (arg < 0 || arg >= _card.Count)
                    {
                        return false;
                    }
                    if (_card[arg] == null)
                    {
                        _ui.ForceTimeMachine();   // as the panel's own time machine button presses it
                        return true;
                    }
                    return Click(_card[arg]);
                case UiActivity.CancelCommand:
                    if (arg < 0 || arg >= _strip.Count)
                    {
                        return false;
                    }
                    if (_strip[arg] == null)
                    {
                        _ui.CancelActivityMovement();   // as the game's Escape stops the walk there
                        return true;
                    }
                    return Click(_strip[arg]);
                case UiActivity.SkipCommand:
                    if (!a.HasTimeMachine())
                    {
                        return false;
                    }
                    _ui.ForceTimeMachine();
                    return true;
                case UiActivity.FastCommand:
                    if (!a.HasFastForward() || !BuildingManager.IsInsideBuilding)
                    {
                        return false;
                    }
                    _ui.OnPlayerActionPressed(PlayerAction.SecondaryInteract);   // the panel's own toggle, as its key flips it
                    return true;
                case UiActivity.StepCommand:
                    return a.HasSlider();
                default:
                    return false;
            }
        }

        private static bool Click(ButtonInfo b)
        {
            if (b == null || !b.interactable || b.onClick == null)
            {
                return false;
            }
            b.onClick();
            return true;
        }

        // The slider to a share of its run: the panel's own slider moved by the difference, as its keys move it.
        private void Slide(float share)
        {
            IPlayerActivity a = _ui != null ? _ui.GetCurrentActivity : null;
            if (a == null || !a.HasSlider() || a.GetState() != PlayerActivityState.NotStarted)
            {
                return;
            }
            int min = a.GetMinSliderValue(), max = a.GetMaxSliderValue();
            float target = Mathf.Round(min + Mathf.Clamp01(share) * (max - min));
            float now = a.GetCurrentSliderValue();
            if (Mathf.Abs(target - now) >= 0.5f)
            {
                _ui.ChangeSliderValue(target - now, true);
                _dirty = true;
            }
        }

        public void PointerDown(int command, int arg, float x, float y)
        {
            if (command == UiActivity.StepCommand)
            {
                Slide(UiActivity.SliderShare(_m, x));
            }
        }

        public bool DragBegin(int command, int arg, float x, float y)
        {
            if (command != UiActivity.StepCommand)
            {
                return false;
            }
            Slide(UiActivity.SliderShare(_m, x));
            return true;
        }

        public void DragMove(float x, float y) => Slide(UiActivity.SliderShare(_m, x));

        public void DragEnd(bool cancelled)
        {
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;   // the game reads its own Escape

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }
    }
}
