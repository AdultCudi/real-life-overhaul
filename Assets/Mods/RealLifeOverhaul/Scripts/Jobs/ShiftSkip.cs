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

        /// <summary>Minutes passed so far, the whole count; called as the game's clock moves, so cheap.</summary>
        void SkipWorked(int minutes);

        /// <summary>Over by the end of the time, the stop button, or the game calling it off: settle for these minutes.</summary>
        void SkipEnded(int minutes, bool whole);
    }

    /// <summary>
    /// A shift whose length the player picks on the panel's slider before it starts (whole values between two ends), as the game's
    /// own activities offer: <see cref="ISkippableShift.SkipMinutes"/> then follows the slider. Optional: a shift without it has no slider.
    /// </summary>
    public interface ISlidingShift : ISkippableShift
    {
        /// <summary>The slider's left end.</summary>
        int SlideLeast { get; }

        /// <summary>The slider's right end.</summary>
        int SlideMost { get; }

        /// <summary>Where the slider stands.</summary>
        int SlideNow { get; }

        /// <summary>The player moved the slider to this value: kept, held between the two ends.</summary>
        void Slide(int value);

        /// <summary>The words beside the slider for where it stands (a game text key and its arguments).</summary>
        (string key, object arguments) SlideInfo();
    }

    /// <summary>
    /// What a shift can tell the work panel beyond its own words, so the card says what the shift pays, trains and costs before it
    /// starts and the strip what it has earned while it runs. Optional: a shift without it shows only the lines it writes itself.
    /// The sums are the ones the shift settles with (<see cref="ShiftFactsRules"/>), so the card and the pay agree to the unit.
    /// </summary>
    public interface IShiftFacts
    {
        /// <summary>The whole money the first <paramref name="minutes"/> of the run settle to, before tax; the pay of the units done by then.</summary>
        int PayFor(int minutes);

        /// <summary>The game's skill the shift trains, or null for none.</summary>
        string SkillKey { get; }

        /// <summary>The skill points the first <paramref name="minutes"/> of the run train.</summary>
        float SkillFor(int minutes);

        /// <summary>The stress the shift adds a game hour; 0 when the hourly upkeep adds none for it.</summary>
        float StressRate { get; }
    }

    /// <summary>
    /// The rest of a shift left to the clock as the game's own employer job does it (<c>PlayerActivity.WorkActivity</c>): one of
    /// its activities on its panel, with Start, the skip-time button, a progress bar and Stop. No walk order is given (the panel
    /// would call it off); it is seen on its way for one frame, the step in which the panel lets the clock run.
    /// </summary>
    public sealed class ShiftSkip : IPlayerActivity
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("jobs", "ShiftSkip", typeof(ShiftSkip));

        private readonly MonoBehaviour _host;
        private ISkippableShift _shift;
        private readonly ISlidingShift _slide;   // the shift's own slider, if it has one: asked once
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
            _slide = shift as ISlidingShift;
            _total = Mathf.Max(1, shift.SkipMinutes);
        }

        /// <summary>True when the shift's length is the player's to pick on the panel's slider before it starts.</summary>
        public bool Slides => _slide != null;

        /// <summary>The minutes the shift would run as the slider stands (the whole shift's, once begun); 0 when it has let go.</summary>
        public int ChosenMinutes => _shift != null ? Mathf.Max(0, _shift.SkipMinutes) : 0;

        /// <summary>What the shift says of its pay, skill and stress, or null when it says none (or has let go).</summary>
        public IShiftFacts Facts => _shift as IShiftFacts;

        /// <summary>The game minutes of the run passed so far, and its length: what the strip's hour and sum are read from.</summary>
        public int DoneMinutes => _begun ? _done : 0;

        public int TotalMinutes => _total;

        /// <summary>True from the offer until it is over, whichever way.</summary>
        public bool Open => _state != PlayerActivityState.Finished;

        /// <summary>True while the game's panel still has this activity: the game can drop one without a word.</summary>
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

        /// <summary>Shows the offer on the game's panel. Null if the panel is busy or the shift has nothing left to pass.</summary>
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

        /// <summary>The job is stopping for a reason of its own: the panel closes and nothing more is said to the job. Safe at any time.</summary>
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

        public bool RequiresEnergy() => false;
        public PlayerActivityState GetState()
        {
            if (FrameGuard.Off)
            {
                return Guard.Ended(this);   // its feature is off: the activity ends as Finish would, and the panel closes
            }
            try
            {
                return _state;
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
                return Guard.Ended(this);
            }
        }
        public PlayerActivityState GetStateBeforeFinishing() => _before;
        public void ChangeState(PlayerActivityState state)
        {
            try
            {
                _state = state;
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void Perform(int minutes)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
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
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

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
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
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

        // One frame on its way (the game's panel hides itself and lets the clock run), then started, with no walk order.
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
                Finish();   // a start that failed half way is ended at once through the job's own end (SkipEnded), never left running with the player held
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
                return new LabelInfo(_shift != null ? _shift.SkipTitle : string.Empty, GameUi.WorldPanel.White(), false);
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, Guard.Blank());
            }
        }

        public LabelInfo[] GetLabels()
        {
            return _state == PlayerActivityState.NotStarted ? _labels : null;
        }

        public ButtonInfo[] GetButtons()
        {
            if (FrameGuard.Off)
            {
                return (ButtonInfo[])null;
            }
            try
            {
                switch (_state)
                {
                    case PlayerActivityState.NotStarted:
                        return _offer ?? (_offer = new[]
                        {
                            new ButtonInfo("CancelShiftSkip", "common_cancel", "gray", Guard.Way(Cancel), PlayerAction.Cancel),
                            new ButtonInfo("StartShiftSkip", "reallife_job_skip_start", "blue", Guard.Wrap(Start), PlayerAction.Confirm)
                        });
                    case PlayerActivityState.Running:
                        return _running ?? (_running = new[] { new ButtonInfo("StopShiftSkip", "reallife_job_skip_stop", "gray", Guard.Way(Finish), PlayerAction.Cancel) });
                    default:
                        return null;
                }
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (ButtonInfo[])null);
            }
        }

        public bool HasFastForward() => _state == PlayerActivityState.Running;
        public bool HasTimeMachine() => _state == PlayerActivityState.Running;
        public int GetRemainingMinutesForTimeMachine() => Mathf.Max(0, _total - _done);
        public bool HasProgressBar() => _state == PlayerActivityState.Running;
        public float GetProgressBarPercentageValue() => 100f * _done / Mathf.Max(1, _total);

        public (string key, object arguments) GetProgressBarLabel()
        {
            if (FrameGuard.Off)
            {
                return (string.Empty, (object)null);
            }
            try
            {
                int left = Mathf.Max(0, _total - _done);
                return ("playeractivityui_time_remaining", new { hours = left / 60, minutes = left % 60 });
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (string.Empty, (object)null));
            }
        }

        // The length of a shift that has a slider (ISlidingShift), picked before it starts; the rest have none.
        public bool HasSlider()
        {
            if (FrameGuard.Off)
            {
                return false;
            }
            try
            {
                return _slide != null && _shift != null && _state == PlayerActivityState.NotStarted && _slide.SlideMost > _slide.SlideLeast;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, false);
            }
        }

        public int GetMinSliderValue()
        {
            try
            {
                return _slide != null ? _slide.SlideLeast : 0;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 0);
            }
        }

        public int GetMaxSliderValue()
        {
            try
            {
                return _slide != null ? _slide.SlideMost : 0;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 0);
            }
        }

        public float GetCurrentSliderValue()
        {
            try
            {
                return _slide != null ? _slide.SlideNow : 0f;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 0f);
            }
        }

        public void OnSliderValueChanged(int value)
        {
            try
            {
                if (_slide != null && _shift != null && _state == PlayerActivityState.NotStarted)   // the panel sets its slider up for every activity, with or without one
                {
                    _slide.Slide(value);
                    _total = Mathf.Max(1, _shift.SkipMinutes);
                    _labels = _shift.SkipLabels();   // its pay and time lines follow the part the slider keeps
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public (string key, object arguments) GetSliderInfo()
        {
            if (FrameGuard.Off)
            {
                return (null, null);
            }
            try
            {
                return HasSlider() ? _slide.SlideInfo() : (null, null);
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (string.Empty, (object)null));
            }
        }
    }
}
