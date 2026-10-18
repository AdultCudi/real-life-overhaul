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
    /// A bus ride through the city on the game's own activity panel, as the game shows studying or working: headline, bar,
    /// fast-forward toggle and time-skip button. The bar fills with the metres of line ridden (TransitDirector.Journey).
    /// Fast forward is the game's own toggle, shown here on the street too (the game shows it only indoors) and its key read
    /// while the ride is on. Skip closes the panel and passes the rest under the ride's fade (TransitRide). Get off asks for
    /// the next stop. The panel must see the activity "on its way" for a frame before it starts, the step in which it lets
    /// the clock run again (Jobs/ShiftSkip). Costs nothing a frame unless a ride is on.
    /// </summary>
    public sealed class BusRidePanel : IPlayerActivity
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("transport", "BusRidePanel", typeof(BusRidePanel));   // the game's calls of it

        private static readonly FieldInfo ToggleField = GameContract.FastForward.Field;
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
            host.StartCoroutine(Guard.Routine(panel.Begin()));
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

        /// <summary>The ride is over for the director's own reason (got off, skipped, ended early): the panel goes. Safe at any time.</summary>
        public void Close()
        {
            if (_state == PlayerActivityState.Finished)
            {
                return;
            }
            _closing = true;
            Finish();
        }

        /// <summary>Shows the buttons again after what the director offers has changed (a stop to get off at asked for, or taken back).</summary>
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
                uis.playerActivityUI.UpdateActivityDisplay();   // it puts the toggle off: put back as it was
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

        /// <summary>The fast-forward key, which the game's panel reads only indoors. Per frame only while a ride runs.</summary>
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

        // The game shows its toggle only inside a building (PlayerActivityUI.SetUpSpeedOptions).
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

        public bool RequiresEnergy() => false;
        public PlayerActivityState GetState()
        {
            return FrameGuard.Off ? Guard.Ended(this) : _state;   // off: the activity ends as its own Finish ends it
        }
        public PlayerActivityState GetStateBeforeFinishing() => _before;
        public void ChangeState(PlayerActivityState state)
        {
            _state = state;
        }
        public void Perform(int minutes) { }

        public void Finish()
        {
            try
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
                        _host.JourneyPanelGone();   // the game closed it (out of energy, say): the ride goes on without the panel
                    }
                }
                catch (Exception e)
                {
                    TransitDirector.Logger?.Error(e);
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        // Closed by the director: fast forward off at once, as the game's panel puts it off when an activity ends.
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
            if (FrameGuard.Off)
            {
                return Guard.Blank();
            }
            try
            {
                (string key, object arguments) title = _host.JourneyHeadline();
                return new LabelInfo(title.key, title.arguments, GameUi.WorldPanel.White());
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, Guard.Blank());
            }
        }

        public LabelInfo[] GetLabels() => null;

        public ButtonInfo[] GetButtons()
        {
            if (FrameGuard.Off)
            {
                return (ButtonInfo[])null;
            }
            try
            {
                if (_state != PlayerActivityState.Running && _state != PlayerActivityState.Started)
                {
                    return null;
                }
                if (_host.JourneyOffAsked)
                {
                    return _asked ?? (_asked = new[] { new ButtonInfo("BusRideStayOn", "reallife_bus_ride_stay_on", "gray", Guard.Way(StayOn), PlayerAction.Cancel) });
                }
                return _riding ?? (_riding = new[] { new ButtonInfo("BusRideGetOff", "reallife_bus_ride_get_off", "gray", Guard.Way(GetOff), PlayerAction.Cancel) });
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (ButtonInfo[])null);
            }
        }

        private void GetOff()
        {
            _host.JourneyGetOff("the player asked on the game's panel");
        }

        private void StayOn()
        {
            _host.JourneyStayOn();
        }

        public bool HasFastForward()
        {
            if (FrameGuard.Off)
            {
                return false;
            }
            try
            {
                return RideRules.FastForwardOffered(_state == PlayerActivityState.Running || _state == PlayerActivityState.Started, GoingPublic.ClockShared);
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, false);
            }
        }
        public bool HasTimeMachine()
        {
            if (FrameGuard.Off)
            {
                return false;
            }
            try
            {
                return RideRules.SkipOffered(_state == PlayerActivityState.Running || _state == PlayerActivityState.Started);
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, false);
            }
        }
        public int GetRemainingMinutesForTimeMachine()
        {
            if (FrameGuard.Off)
            {
                return 0;
            }
            try
            {
                return Mathf.Max(1, Mathf.RoundToInt(_host.JourneyMinutesLeft()));
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 0);
            }
        }

        // The time-skip button: not the game's time machine but the ride's own fade. A goal already past makes the panel finish
        // this at once, and Finish hands the rest to the director.
        public Timestamp GetTimeMachineGoal()
        {
            if (FrameGuard.Off)
            {
                return TimeHelper.Now();
            }
            try
            {
                _skip = true;
                return new Timestamp(Mathf.Max(0f, TimeHelper.NowInMinutes() - 10f));
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, TimeHelper.Now());
            }
        }

        public bool HasProgressBar() => _state == PlayerActivityState.Running || _state == PlayerActivityState.Started;
        public float GetProgressBarPercentageValue()
        {
            if (FrameGuard.Off)
            {
                return 0f;
            }
            try
            {
                return 100f * _host.JourneyProgress;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 0f);
            }
        }
        public (string key, object arguments) GetProgressBarLabel()
        {
            if (FrameGuard.Off)
            {
                return (string.Empty, (object)null);
            }
            try
            {
                return _host.JourneyBarLabel();
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (string.Empty, (object)null));
            }
        }

        public bool HasSlider() => false;
        public int GetMinSliderValue() => 0;
        public int GetMaxSliderValue() => 0;
        public float GetCurrentSliderValue() => 0f;
        public void OnSliderValueChanged(int value) { }
        public (string key, object arguments) GetSliderInfo() => (null, null);
    }
}
