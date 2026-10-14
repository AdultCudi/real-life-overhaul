using System;
using System.Collections;
using System.Reflection;
using BigAmbitions.DayNightCycle;
using BigAmbitions.InputSystem;
using PlayerActivity;
using UI;
using UI.Elements;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A bus ride through the city on the game's own activity panel (<c>PlayerActivity.PlayerActivityUI</c>), as the game shows
    /// its own actions (studying, working, sleeping): one of the game's activities (<c>PlayerActivity.IPlayerActivity</c>) with
    /// the panel's headline, progress bar, fast-forward toggle and time-skip button, on their keys.
    ///
    /// - LEFT TO RUN, the bus drives its line to the rider's stop in real time (TransitDirector.Journey) and the bar fills with
    ///   the metres of line ridden; its label says how many stops are to come and the next one.
    /// - FAST-FORWARD is the game's own toggle (<c>GameSpeedController</c> at <c>TimeSpeed.FastForward</c>): everything six times as
    ///   fast, the bus too. The game shows its toggle only inside a building; here it is shown on the street as well, and its key
    ///   (the game's secondary interact) is read for it while the ride is on, since the game's panel reads it only indoors.
    /// - SKIP is the game's time-skip button: rather than the game's time machine at once, the panel is closed and the rest of the
    ///   ride passes under the ride's own fade (TransitRide, as before), to the bus standing at the stop.
    /// - GET OFF (the panel's cancel key, or a move) asks for the next stop; the button then offers to stay on.
    ///
    /// The game's panel must see the activity "on its way" for a frame before it is started (that is the step in which it lets
    /// the clock run again: Jobs/ShiftSkip says why); no walk order is given. Nothing here runs unless the ride is on.
    /// </summary>
    public sealed class BusRidePanel : IPlayerActivity
    {
        private static readonly FieldInfo ToggleField = GameContract.FastForward.Field;   // (resolved once: Core/GameContract)
        private static bool _toggleTold;

        private readonly TransitDirector _host;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private ButtonInfo[] _riding;
        private ButtonInfo[] _asked;
        private bool _skip;
        private bool _closing;
        private bool _began;

        private BusRidePanel(TransitDirector host)
        {
            _host = host;
        }

        /// <summary>True while the game's panel still has this ride.</summary>
        public bool Shown
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.playerActivityUI != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, this);
            }
        }

        /// <summary>True from when the panel shows its bar until the ride is over.</summary>
        public bool Running => _began && _state != PlayerActivityState.Finished;

        /// <summary>The panel for a ride, shown and started. Null if the game's panel is busy with something else or a time skip runs.</summary>
        public static BusRidePanel Show(TransitDirector host)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (host == null || !host.isActiveAndEnabled || uis == null || uis.playerActivityUI == null || PlayerActivityUI.IsPanelOpen
                || (uis.timeMachine != null && uis.timeMachine.isRunning))
            {
                return null;
            }
            var panel = new BusRidePanel(host);
            PlayerActivityUI.Show(panel);
            if (!ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, panel))
            {
                return null;
            }
            panel._state = PlayerActivityState.MovingTowardsActivity;
            host.StartCoroutine(panel.Begin());
            return panel;
        }

        // One frame "on its way" (the game's panel lets the clock run), then started; a frame later its toggle is shown on the street.
        private IEnumerator Begin()
        {
            yield return null;
            if (_state != PlayerActivityState.MovingTowardsActivity)
            {
                yield break;
            }
            _began = true;
            _state = PlayerActivityState.Started;   // the game's panel makes it Running on its next frame
            yield return null;
            yield return null;
            ShowToggle();
        }

        /// <summary>The ride is over for a reason of the director's own (they get off, it is skipped, or it ends early): the panel goes. Safe at any time.</summary>
        public void Close()
        {
            if (_state == PlayerActivityState.Finished)
            {
                return;
            }
            _closing = true;
            Finish();
        }

        /// <summary>What the director has to show has changed (a stop to get off at asked for, or taken back): the buttons again.</summary>
        public void Refresh()
        {
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (!Running || !Shown)
                {
                    return;
                }
                Toggle toggle = FastToggle();
                bool fast = toggle != null && toggle.isOn;
                uis.playerActivityUI.UpdateActivityDisplay();   // (it puts the toggle off: put back as it was)
                ShowToggle();
                if (toggle != null && fast)
                {
                    toggle.isOn = true;
                }
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
        }

        /// <summary>The fast-forward key while the ride is on (the game's panel reads it only indoors). Called per frame only while a ride runs.</summary>
        public void Keys()
        {
            if (!Running || !HasFastForward() || ThirdPersonCamera.IsUiOpen() || GameManager.ShouldBlockKeyboardShortcuts() || !PlayerAction.SecondaryInteract.Pressed())
            {
                return;
            }
            Toggle toggle = FastToggle();
            if (toggle != null && toggle.gameObject.activeInHierarchy)
            {
                toggle.isOn = !toggle.isOn;
            }
        }

        /// <summary>Whether the game is fast-forwarding the ride now.</summary>
        public static bool FastForwarding
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.gameSpeed != null && uis.gameSpeed.isFastForwarding;
            }
        }

        private static Toggle FastToggle()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.playerActivityUI == null || ToggleField == null)
            {
                if (!_toggleTold)
                {
                    _toggleTold = true;
                    TransitDirector.Logger?.Info("Transit check (journey): the game's fast-forward toggle was not found on its activity panel: the ride offers its time skip only: FAIL.");
                }
                return null;
            }
            return ToggleField.GetValue(uis.playerActivityUI) as Toggle;
        }

        // The game shows its toggle only inside a building (PlayerActivityUI.SetUpSpeedOptions): shown on the street for the ride.
        private void ShowToggle()
        {
            try
            {
                if (!Running || !Shown || !HasFastForward())
                {
                    return;
                }
                Toggle toggle = FastToggle();
                if (toggle != null && toggle.transform.parent != null)
                {
                    toggle.transform.parent.gameObject.SetActive(true);
                }
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the game's contract

        public bool RequiresEnergy() => false;
        public PlayerActivityState GetState() => _state;
        public PlayerActivityState GetStateBeforeFinishing() => _before;
        public void ChangeState(PlayerActivityState state) { _state = state; }
        public void Perform(int minutes) { }

        public void Finish()
        {
            if (_state == PlayerActivityState.Finished)
            {
                return;
            }
            _before = _state;
            _state = PlayerActivityState.Finished;
            bool began = _began;
            _began = false;
            try
            {
                if (_closing)
                {
                    StopFastForward();
                }
                else if (_skip)
                {
                    _host.JourneySkip("the player skipped the rest on the game's panel");
                }
                else if (began)
                {
                    _host.JourneyPanelGone();   // (the game closed it: out of energy, say; the ride goes on, without the panel)
                }
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
            }
        }

        // Closed by the director: the game's fast forward goes off at once, as the game's panel puts it off when an activity ends.
        private static void StopFastForward()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.gameSpeed != null && uis.gameSpeed.isFastForwarding)
            {
                uis.gameSpeed.isFastForwarding = false;
                uis.gameSpeed.Reset();
            }
        }

        public LabelInfo GetHeadlineLabel()
        {
            (string key, object arguments) title = _host.JourneyHeadline();
            return new LabelInfo(title.key, title.arguments, GameUi.WorldPanel.White());
        }

        public LabelInfo[] GetLabels() => null;

        public ButtonInfo[] GetButtons()
        {
            if (_state != PlayerActivityState.Running && _state != PlayerActivityState.Started)
            {
                return null;
            }
            if (_host.JourneyOffAsked)
            {
                return _asked ?? (_asked = new[] { new ButtonInfo("BusRideStayOn", "reallife_bus_ride_stay_on", "gray", StayOn, PlayerAction.Cancel) });
            }
            return _riding ?? (_riding = new[] { new ButtonInfo("BusRideGetOff", "reallife_bus_ride_get_off", "gray", GetOff, PlayerAction.Cancel) });
        }

        private void GetOff()
        {
            _host.JourneyGetOff("the player asked on the game's panel");
        }

        private void StayOn()
        {
            _host.JourneyStayOn();
        }

        public bool HasFastForward() => RideRules.FastForwardOffered(_state == PlayerActivityState.Running || _state == PlayerActivityState.Started, GoingPublic.ClockShared);
        public bool HasTimeMachine() => RideRules.SkipOffered(_state == PlayerActivityState.Running || _state == PlayerActivityState.Started);
        public int GetRemainingMinutesForTimeMachine() => Mathf.Max(1, Mathf.RoundToInt(_host.JourneyMinutesLeft()));

        // The game's time-skip button: not its time machine at once, but the ride's own fade (the panel finishes this at once
        // for a goal already past, and the director takes the rest from there: Finish).
        public Timestamp GetTimeMachineGoal()
        {
            _skip = true;
            return new Timestamp(Mathf.Max(0f, TimeHelper.NowInMinutes() - 10f));
        }

        public bool HasProgressBar() => _state == PlayerActivityState.Running || _state == PlayerActivityState.Started;
        public float GetProgressBarPercentageValue() => 100f * _host.JourneyProgress;
        public (string key, object arguments) GetProgressBarLabel() => _host.JourneyBarLabel();

        public bool HasSlider() => false;
        public int GetMinSliderValue() => 0;
        public int GetMaxSliderValue() => 0;
        public float GetCurrentSliderValue() => 0f;
        public void OnSliderValueChanged(int value) { }
        public (string key, object arguments) GetSliderInfo() => (null, null);
    }
}
