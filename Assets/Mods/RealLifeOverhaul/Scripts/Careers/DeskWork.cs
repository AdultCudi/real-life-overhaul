using System;
using System.Collections;
using BigAmbitions.DayNightCycle;
using PlayerActivity;
using RealLifeOverhaul.FitOut;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A working day of a professional job at the player's own desk on the game's work panel, as WorkActivity is to the game's
    /// own job: its labels, Start working or Work late, the walk and the pose; the minutes are the director's to settle.
    /// </summary>
    internal sealed class DeskWork : IPlayerActivity
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("careers_study", "DeskWork", typeof(DeskWork));   // the game's calls of it (Core/Guard)

        private readonly CareersDirector _director;
        private readonly EmployeeStationController _station;   // the game's station; or null, and
        private readonly FitDesk _desk;                         // ...a fitted desk of the mod's
        private readonly LabelInfo _title;
        private readonly LabelInfo[] _labels;
        private readonly bool _canStart, _canLate;
        private ButtonInfo[] _offer, _stop;
        private Sitting _sitting;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private int _length, _done;
        private bool _pressed;                                  // Start working or Work late pressed: on its way from the next frame
        private bool _seated;                                   // at a desk, on its chair (else on their feet at the station)...
        private Vector3 _stood;                                 // ...and where they stood before sitting, to stand up on
        private float _nextType;
        private int _orderFrame = -1;                           // the frame the walk order to the desk was given in (DeskGame.SetOffSeen)

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

        private void StartDay() => Begin(false);

        private void StartLate() => Begin(true);

        private void Begin(bool late)
        {
            if (_pressed || _state != PlayerActivityState.NotStarted)
            {
                return;
            }
            if (SaveGameManager.Current == null || SaveGameManager.Current.Energy <= 0f)
            {
                Notifications.ShowInsufficientEnergy();
                return;
            }
            string why = _director.DeskBegin(late, this, out Sitting sitting);
            if (why != null)
            {
                Notifications.Show(NotificationType.Warning, why, null, 8f);
                return;
            }
            _pressed = true;
            _sitting = sitting;
            _length = Math.Max(1, sitting.SkipMinutes);
            _done = 0;
            _director.StartCoroutine(Guard.Routine(Walk()));
        }

        // Set off as the game's activities are ("Setting off" in docs/features/careers.md), then the last step to the station.
        private IEnumerator Walk()
        {
            yield return null;
            if (_state != PlayerActivityState.NotStarted)
            {
                yield break;   // cancelled meanwhile
            }
            _state = PlayerActivityState.MovingTowardsActivity;
            yield return null;
            if (_state != PlayerActivityState.MovingTowardsActivity)
            {
                yield break;
            }
            _orderFrame = Time.frameCount;
            bool going = _station != null ? DeskGame.WalkTo(_station, Arrived) : _desk != null && DeskGame.WalkTo(_desk, Arrived);
            if (!going)
            {
                End(true, true);
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
                Transform seat = _desk != null ? _desk.Seat : DeskGame.SeatOf(_station);
                if (seat != null)
                {
                    _seated = true;
                    DeskGame.SitOn(seat, _station, out _stood);
                    _nextType = Time.time + UnityEngine.Random.Range(0f, 6f);
                }
                else
                {
                    DeskGame.SitAt(_station);   // a till, a counter: on their feet at it, as the game's own job
                }
                _sitting.SkipStarted();
                DeskGame.SetOffSeen("the work desk", true, _orderFrame);
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
        }

        private void Cancel()
        {
            if (_state == PlayerActivityState.NotStarted)
            {
                End(true, false);   // pressed already: the day's sitting is let go with nothing counted
            }
        }

        public bool RequiresEnergy() => false;

        public bool BlocksAutoSave() => true;

        public PlayerActivityState GetState()
        {
            if (FrameGuard.Off)
            {
                return Guard.Ended(this);   // its feature is off: ended as its Finish ends it, and the panel closes
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
                if (state == PlayerActivityState.Finished)
                {
                    End(true, true);   // however the panel ends it: the minutes worked settled once, and the player stood up
                    return;
                }
                if (_state == PlayerActivityState.MovingTowardsActivity && state == PlayerActivityState.NotStarted)
                {
                    DeskGame.SetOffSeen("the work desk", false, _orderFrame);
                    End(true, false);   // the game's panel called the walk off for a click elsewhere and let it go: the player walks there
                    return;
                }
                if (_state != PlayerActivityState.Finished)
                {
                    _state = state;
                }
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
                else if (_seated && Time.time >= _nextType)
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
                End(true, true);
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
                    uis.timeMachine.CancelTimeMachine();   // the game's panel then finishes the activity itself
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            End(false, true);
        }

        // Every way out. settle: the minutes go to the director (else it has let the sitting go); walking: the walk is called off here.
        private void End(bool settle, bool walking)
        {
            if (_state == PlayerActivityState.Finished)
            {
                return;
            }
            PlayerActivityState was = _state;
            _before = was;
            _state = PlayerActivityState.Finished;   // first: whatever the game's calls below do, nothing here runs twice
            bool begun = was == PlayerActivityState.Started || was == PlayerActivityState.Running;
            try
            {
                if (was == PlayerActivityState.MovingTowardsActivity && walking)
                {
                    DeskGame.StopWalking();
                }
                else if (begun && _seated)
                {
                    DeskGame.StandUp(_desk != null ? _desk.Stand : _stood, _station);
                }
                else if (begun)
                {
                    DeskGame.StandFrom(_station);
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            Sitting sitting = _sitting;
            _sitting = null;
            if (sitting == null)
            {
                return;
            }
            if (!settle)
            {
                if (begun)
                {
                    CareersGame.Tiring(false);
                }
                return;
            }
            if (!begun)
            {
                sitting.CalledOff();   // pressed, but never at work: nothing counted, nothing said
                return;
            }
            sitting.SkipEnded(Math.Min(_done, _length), _done >= _length);
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
                                PlayerAction key = _canStart ? PlayerAction.SecondaryInteract : PlayerAction.Confirm;   // beside Start, the other key
                                _offer[k] = new ButtonInfo("LateRealLifeWork", "reallife_life_button_late", "blue", Guard.Wrap(StartLate), key);
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
