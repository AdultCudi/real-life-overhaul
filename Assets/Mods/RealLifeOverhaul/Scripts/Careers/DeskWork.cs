using System;
using System.Collections;
using BigAmbitions.DayNightCycle;
using Helpers;
using PlayerActivity;
using RealLifeOverhaul.FitOut;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A working day of a professional job at the player's own desk, on the game's own work panel: what
    /// PlayerActivity.WorkActivity is for the game's job at El Gato Food Market, for the mod's job. The player has clicked
    /// their station (or desk, or home computer) and walked to it; the panel says, with the game's own labels where it has
    /// them, whether the business is open, a day off, the shift not yet started or starting soon, the day done, the desk
    /// taken, and what the day pays, with Cancel and Start working (or Work late, once the day is worked). Started, the
    /// player walks the last step to the station and is put to work there in the game's own pose (Careers/DeskGame); the
    /// panel shows the game's progress bar with the time remaining, fast forward, "skip time" and Stop working. Whichever
    /// way it ends, the minutes worked are the director's to settle (CareersDirector.Ended): pay, skills, the counted share
    /// of the day, lateness and overtime as before.
    ///
    /// One object per click. Nothing here runs unless the game's panel has it; the director looks once a second whether it
    /// still does (the panel can let an activity go without a word).
    /// </summary>
    internal sealed class DeskWork : IPlayerActivity
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("careers_study", "DeskWork", typeof(DeskWork));   // the game's calls of it (Core/Guard)

        private readonly CareersDirector _director;
        private readonly EmployeeStationController _station;   // the game's station or the home computer; or null, and
        private readonly FitDesk _desk;                         // ...a fitted desk of the mod's
        private readonly LabelInfo _title;
        private readonly LabelInfo[] _labels;
        private readonly bool _canStart, _canLate;
        private ButtonInfo[] _offer, _stop;
        private Sitting _sitting;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private int _length, _done;
        private float _nextType;

        public DeskWork(CareersDirector director, EmployeeStationController station, FitDesk desk, string title, LabelInfo[] labels, bool canStart, bool canLate)
        {
            _director = director;
            _station = station;
            _desk = desk;
            _title = DeskGame.Label(title, 0);
            _labels = labels;
            _canStart = canStart;
            _canLate = canLate;
        }

        /// <summary>True from the click until it is over, whichever way.</summary>
        public bool Open => _state != PlayerActivityState.Finished;

        /// <summary>The station it is at (null for a fitted desk).</summary>
        public EmployeeStationController Station => _station;

        /// <summary>The fitted desk it is at (null for a station).</summary>
        public FitDesk Desk => _desk;

        // ---------------------------------------------------------------- starting

        private void StartDay() => Begin(false);

        private void StartLate() => Begin(true);

        private void Begin(bool late)
        {
            if (_state != PlayerActivityState.NotStarted)
            {
                return;
            }
            if (SaveGameManager.Current == null || SaveGameManager.Current.Energy <= 0f)
            {
                Notifications.ShowInsufficientEnergy();
                return;
            }
            string why = _director.DeskBegin(late, out Sitting sitting);
            if (why != null)
            {
                Notifications.Show(NotificationType.Warning, why, null, 8f);
                return;
            }
            _sitting = sitting;
            _length = Math.Max(1, sitting.SkipMinutes);
            _done = 0;
            _state = PlayerActivityState.MovingTowardsActivity;
            _director.StartCoroutine(Guard.Routine(Walk()));
        }

        // As WorkActivity: a frame later the last step to the station; there, set to work.
        private IEnumerator Walk()
        {
            yield return null;
            if (_state != PlayerActivityState.MovingTowardsActivity)
            {
                yield break;
            }
            bool going = _station != null ? DeskGame.WalkTo(_station, Arrived) : _desk != null && DeskGame.WalkTo(_desk.Stand, Arrived);
            if (!going)
            {
                Off();
            }
        }

        private void Arrived()
        {
            if (_state != PlayerActivityState.MovingTowardsActivity)
            {
                return;
            }
            try
            {
                _state = PlayerActivityState.Started;   // the game's panel makes it Running on its next frame
                if (_station != null)
                {
                    DeskGame.SitAt(_station);
                }
                else
                {
                    DeskGame.SitAt(_desk);
                    _nextType = Time.time + UnityEngine.Random.Range(0f, 6f);
                }
                _sitting.SkipStarted();
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
        }

        // Called off before the day began: nothing counted.
        private void Off()
        {
            _before = _state;
            _state = PlayerActivityState.Finished;
            Sitting sitting = _sitting;
            _sitting = null;
            sitting?.SkipEnded(0, false);
        }

        private void Cancel()
        {
            if (_state == PlayerActivityState.NotStarted)
            {
                _before = _state;
                _state = PlayerActivityState.Finished;
            }
        }

        // ---------------------------------------------------------------- the game's contract

        public bool RequiresEnergy() => false;

        public bool BlocksAutoSave() => true;

        public PlayerActivityState GetState()
        {
            if (FrameGuard.Off)
            {
                return Guard.Ended(this);   // (its feature is off: the activity ends as its own Finish ends it, and the panel closes)
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
                if (_state != PlayerActivityState.Running)
                {
                    return;
                }
                _done += minutes;
                _sitting?.SkipWorked(Math.Min(_done, _length));
                if (_done >= _length)
                {
                    Finish();
                }
                else if (_desk != null && Time.time >= _nextType)
                {
                    DeskGame.Type();
                    _nextType = Time.time + UnityEngine.Random.Range(5f, 10f);
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        /// <summary>Over: by the end of the time, Stop working, the game calling it off. The minutes worked are settled.</summary>
        public void Finish()
        {
            try
            {
                End(true);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        /// <summary>The job is stopping for a reason of its own (it ended, the part was switched off): over, and nothing more is said to it.</summary>
        public void Close()
        {
            try
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                if (_state == PlayerActivityState.Running && uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
                {
                    uis.timeMachine.CancelTimeMachine();   // (the game's panel then finishes the activity itself)
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            End(false);
        }

        private void End(bool settle)
        {
            if (_state == PlayerActivityState.Finished)
            {
                return;
            }
            PlayerActivityState was = _state;
            try
            {
                if (was == PlayerActivityState.MovingTowardsActivity)
                {
                    DeskGame.StopWalking();
                }
                else if (was == PlayerActivityState.Started || was == PlayerActivityState.Running)
                {
                    if (_station != null)
                    {
                        DeskGame.StandFrom(_station);
                    }
                    else
                    {
                        DeskGame.StandFrom(_desk);
                    }
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            _before = was;
            _state = PlayerActivityState.Finished;
            Sitting sitting = _sitting;
            _sitting = null;
            if (sitting == null)
            {
                return;
            }
            bool begun = was == PlayerActivityState.Started || was == PlayerActivityState.Running;
            if (!settle)
            {
                if (begun)
                {
                    CareersGame.Tiring(false);
                }
                return;
            }
            sitting.SkipEnded(begun ? Math.Min(_done, _length) : 0, begun && _done >= _length);
        }

        public LabelInfo GetHeadlineLabel()
        {
            if (FrameGuard.Off)
            {
                return Guard.Blank();
            }
            try
            {
                return _sitting != null && _state != PlayerActivityState.NotStarted ? DeskGame.Label(_sitting.SkipTitle, 0) : _title;
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
                        if (_offer == null)
                        {
                            int n = 1 + (_canStart ? 1 : 0) + (_canLate ? 1 : 0), k = 0;
                            _offer = new ButtonInfo[n];
                            _offer[k++] = new ButtonInfo("CancelRealLifeWork", "common_cancel", "gray", Guard.Way(Cancel), PlayerAction.Cancel);
                            if (_canStart)
                            {
                                _offer[k++] = new ButtonInfo("StartRealLifeWork", "workui_start", "blue", Guard.Wrap(StartDay), PlayerAction.Confirm);
                            }
                            if (_canLate)
                            {
                                _offer[k] = new ButtonInfo("LateRealLifeWork", "reallife_life_button_late", "blue", Guard.Wrap(StartLate), _canStart ? PlayerAction.SecondaryInteract : PlayerAction.Confirm);
                            }
                        }
                        return _offer;
                    case PlayerActivityState.Running:
                        return _stop ?? (_stop = new[] { new ButtonInfo("StopRealLifeWork", "workui_stop", "gray", Guard.Way(Finish), PlayerAction.Cancel) });
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

        public int GetRemainingMinutesForTimeMachine() => Math.Max(0, _length - _done);

        public Timestamp GetTimeMachineGoal()
        {
            if (FrameGuard.Off)
            {
                return TimeHelper.Now();
            }
            try
            {
                return TimeHelper.Now().AddMinutes(GetRemainingMinutesForTimeMachine());
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, TimeHelper.Now());
            }
        }

        public bool HasProgressBar() => _state == PlayerActivityState.Running;

        public float GetProgressBarPercentageValue() => 100f * _done / Math.Max(1, _length);

        public (string key, object arguments) GetProgressBarLabel()
        {
            if (FrameGuard.Off)
            {
                return (string.Empty, (object)null);
            }
            try
            {
                int left = Math.Max(0, _length - _done);
                return ("playeractivityui_time_remaining", new { hours = left / 60, minutes = left % 60 });
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

        public void OnSliderValueChanged(int value)
        {
        }

        public (string key, object arguments) GetSliderInfo() => (null, null);
    }
}
