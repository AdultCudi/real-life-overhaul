using System;
using System.Collections;
using PlayerActivity;
using UI;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What a job tells <see cref="ShiftSkip"/> so that the rest of its shift can be left to the clock.</summary>
    public interface ISkippableShift
    {
        /// <summary>The panel's headline, as final words.</summary>
        string SkipTitle { get; }

        /// <summary>Game minutes of work the shift still has; zero when there is nothing left to pass.</summary>
        int SkipMinutes { get; }

        /// <summary>What the panel says before the start: how long, what it pays. Made once, when the panel opens.</summary>
        LabelInfo[] SkipLabels();

        /// <summary>The clock starts: whatever is on the street for the hand-played shift is put away.</summary>
        void SkipStarted();

        /// <summary>Minutes passed so far (the whole count, not the step). Called as the game's clock moves; cheap.</summary>
        void SkipWorked(int minutes);

        /// <summary>It is over, by the end of the time, the stop button, or the game calling it off: settle for these minutes.</summary>
        void SkipEnded(int minutes, bool whole);
    }

    /// <summary>
    /// The rest of a shift left to the clock, exactly as the game's own employer job does it: one of the game's
    /// activities (<c>PlayerActivity.IPlayerActivity</c>) shown on the game's own panel
    /// (<c>PlayerActivityUI</c>). The game's job (<c>PlayerActivity.WorkActivity</c>) works like this: its panel
    /// offers Start and Cancel with the game paused; started, the player is kept at the till, the panel shows a
    /// progress bar with the time remaining, the game's "skip time" button on its special key, and Stop; the
    /// button runs the game's time machine to the end of the shift, and whichever way it ends the pay for the
    /// minutes worked is settled in <c>Finish</c>. This class is the same thing for one of the mod's jobs: the
    /// same panel, buttons, keys, progress bar and time machine, with the job saying how long is left and
    /// settling the minutes that passed.
    ///
    /// The player is already at their place and held there, so no walk order is ever given (the game's panel
    /// calls an activity off if one arrives after it has allowed cancelling: see Home/HomeActivity). The game's
    /// panel must still see the activity "on its way" before it sees it started, because that is the step in
    /// which it lets the clock run again; it is given one frame of it.
    ///
    /// One object per offer, made when the player asks. Nothing here runs unless the game's panel is open.
    /// </summary>
    public sealed class ShiftSkip : IPlayerActivity
    {
        private readonly MonoBehaviour _host;
        private ISkippableShift _shift;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private LabelInfo[] _labels;
        private ButtonInfo[] _offer;
        private ButtonInfo[] _running;
        private int _total;
        private int _done;
        private bool _begun;

        private ShiftSkip(MonoBehaviour host, ISkippableShift shift)
        {
            _host = host;
            _shift = shift;
            _total = Mathf.Max(1, shift.SkipMinutes);
        }

        /// <summary>True from the offer until it is over, whichever way.</summary>
        public bool Open => _state != PlayerActivityState.Finished;

        /// <summary>
        /// True while the game's panel still has this activity. The game can drop one without a word (its panel takes
        /// a walk order for a click elsewhere while an activity is on its way): whoever offered it then lets it go.
        /// </summary>
        public bool Shown
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.playerActivityUI != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, this);
            }
        }

        /// <summary>True while the clock is doing the work (the game's panel shows its progress bar).</summary>
        public bool Working => _begun && _state != PlayerActivityState.Finished;

        /// <summary>
        /// Shows the offer on the game's panel. Null if the panel is busy with something else or the shift has
        /// nothing left to pass.
        /// </summary>
        public static ShiftSkip Offer(MonoBehaviour host, ISkippableShift shift)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (host == null || shift == null || uis == null || uis.playerActivityUI == null || PlayerActivityUI.IsPanelOpen || shift.SkipMinutes <= 0
                || (uis.timeMachine != null && uis.timeMachine.isRunning))
            {
                return null;
            }
            var skip = new ShiftSkip(host, shift);
            skip._labels = shift.SkipLabels();
            PlayerActivityUI.Show(skip);
            return ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, skip) ? skip : null;
        }

        /// <summary>
        /// The job is stopping for a reason of its own: the panel is closed and nothing more is said to the job.
        /// Safe at any time.
        /// </summary>
        public void Close()
        {
            _shift = null;
            if (_state == PlayerActivityState.Finished)
            {
                return;
            }
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (_begun && uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
                {
                    uis.timeMachine.CancelTimeMachine();   // the game's panel then finishes the activity itself
                }
            }
            catch (Exception e)
            {
                JobDirector.Logger?.Error(e);
            }
            Finish();
        }

        // ---------------------------------------------------------------- the game's contract

        public bool RequiresEnergy() => false;
        public PlayerActivityState GetState() => _state;
        public PlayerActivityState GetStateBeforeFinishing() => _before;
        public void ChangeState(PlayerActivityState state) { _state = state; }

        public void Perform(int minutes)
        {
            if (_state == PlayerActivityState.Finished || !_begun)
            {
                return;
            }
            _done += minutes;
            try
            {
                _shift?.SkipWorked(_done < _total ? _done : _total);
            }
            catch (Exception e)
            {
                JobDirector.Logger?.Error(e);
            }
            if (_done >= _total)
            {
                Finish();
            }
        }

        public void Finish()
        {
            if (_state == PlayerActivityState.Finished)
            {
                return;
            }
            _before = _state;
            _state = PlayerActivityState.Finished;
            if (!_begun)
            {
                return;
            }
            _begun = false;
            ISkippableShift shift = _shift;
            _shift = null;
            try
            {
                shift?.SkipEnded(_done < _total ? _done : _total, _done >= _total);
            }
            catch (Exception e)
            {
                JobDirector.Logger?.Error(e);
            }
        }

        private void Cancel()
        {
            if (_state == PlayerActivityState.NotStarted)
            {
                _before = _state;
                _state = PlayerActivityState.Finished;
                _shift = null;
            }
        }

        private void Start()
        {
            if (_state != PlayerActivityState.NotStarted || _shift == null)
            {
                return;
            }
            if (SaveGameManager.Current == null || SaveGameManager.Current.Energy <= 1f)
            {
                Notifications.ShowInsufficientEnergy();
                return;
            }
            if (_host == null || !_host.isActiveAndEnabled)
            {
                Cancel();
                return;
            }
            _state = PlayerActivityState.MovingTowardsActivity;
            _host.StartCoroutine(Guard.Routine(Begin()));
        }

        // One frame "on its way" (the game's panel hides itself and lets the clock run), then started, with no walk order.
        private IEnumerator Begin()
        {
            yield return null;
            if (_state != PlayerActivityState.MovingTowardsActivity || _shift == null)
            {
                yield break;   // called off meanwhile
            }
            _total = Mathf.Max(1, _shift.SkipMinutes);
            _done = 0;
            _begun = true;
            _state = PlayerActivityState.Started;   // the game's panel makes it Running on its next frame
            try
            {
                _shift.SkipStarted();
            }
            catch (Exception e)
            {
                JobDirector.Logger?.Error(e);
            }
        }

        public LabelInfo GetHeadlineLabel()
        {
            return new LabelInfo(_shift != null ? _shift.SkipTitle : string.Empty, WorldPanelWhite(), false);
        }

        public LabelInfo[] GetLabels()
        {
            return _state == PlayerActivityState.NotStarted ? _labels : null;
        }

        public ButtonInfo[] GetButtons()
        {
            switch (_state)
            {
                case PlayerActivityState.NotStarted:
                    return _offer ?? (_offer = new[]
                    {
                        new ButtonInfo("CancelShiftSkip", "common_cancel", "gray", Cancel, PlayerAction.Cancel),
                        new ButtonInfo("StartShiftSkip", "reallife_job_skip_start", "blue", Start, PlayerAction.Confirm)
                    });
                case PlayerActivityState.Running:
                    return _running ?? (_running = new[] { new ButtonInfo("StopShiftSkip", "reallife_job_skip_stop", "gray", Finish, PlayerAction.Cancel) });
                default:
                    return null;
            }
        }

        public bool HasFastForward() => _state == PlayerActivityState.Running;
        public bool HasTimeMachine() => _state == PlayerActivityState.Running;
        public int GetRemainingMinutesForTimeMachine() => Mathf.Max(0, _total - _done);
        public bool HasProgressBar() => _state == PlayerActivityState.Running;
        public float GetProgressBarPercentageValue() => 100f * _done / Mathf.Max(1, _total);

        public (string key, object arguments) GetProgressBarLabel()
        {
            int left = Mathf.Max(0, _total - _done);
            return ("playeractivityui_time_remaining", new { hours = left / 60, minutes = left % 60 });
        }

        public bool HasSlider() => false;
        public int GetMinSliderValue() => 0;
        public int GetMaxSliderValue() => 0;
        public float GetCurrentSliderValue() => 0f;
        public void OnSliderValueChanged(int value) { }
        public (string key, object arguments) GetSliderInfo() => (null, null);

        private static Color32 WorldPanelWhite()
        {
            return GameUi.WorldPanel.White();
        }
    }
}
