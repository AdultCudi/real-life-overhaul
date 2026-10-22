using System;
using System.Collections;
using System.Globalization;
using BigAmbitions.Characters;
using BigAmbitions.DayNightCycle;
using Helpers;
using PlayerActivity;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A job interview at the employer's place on the game's activity panel, as the game's own door has it: Start walks the player
    /// to the entrance door and in, the screen goes black as the door's does, the interview's minutes pass behind it (the game's own
    /// skip alone, the shared clock's pace in a Going Public session) and the player comes back out at the same door, facing the
    /// street. Inside the employer's building already, the black screen comes where the player stands. The minutes count once
    /// (<see cref="ClassTally"/>); every end gives back the black screen, the player's place and the navigation it held
    /// (<see cref="InterviewRules.Out"/>). Nothing per frame of its own: the panel calls it as its own activities.
    /// </summary>
    internal sealed class InterviewVisit : IPlayerActivity
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("careers_jobs", "InterviewVisit", typeof(InterviewVisit));   // the game's calls of it (Core/Guard)

        private const NavigationBlocker Blocker = NavigationBlocker.StudyActivity;   // as at the home computer: the player stays in while the panel runs
        private const int RunningFrames = 120;   // frames the black screen waits for the game's panel to make the interview Running before its skip is pressed

        private readonly CareersDirector _director;
        private readonly InterviewWay _way;
        private readonly InterviewDoor _door;      // the door gone in at; null held where the player stands
        private readonly ClassTally _tally;
        private readonly LabelInfo _title;
        private readonly string _employer;         // for the log and the self-check line
        private Sitting _sitting;
        private LabelInfo[] _labels;
        private ButtonInfo[] _offer;
        private ButtonInfo[] _stop;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private PlayerController _player;          // the player of the city it began in: another (or none) at its end is a load
        private bool _blocked;                     // the player's navigation held for it
        private bool _faded;                       // the game's black screen was put up for it...
        private UnityEngine.Object _overlay;       // ...this one, the city's it began in
        private bool _fading;                      // ...and is still fading in: its end gives it back once it is black
        private bool _outDue;                      // ended while fading in
        private bool _skipped;                     // the game's skip was pressed behind the black screen
        private InterviewEnd _end;
        private int _orderFrame = -1;              // the frame the walk order to the door was given in (DeskGame.SetOffSeen)

        public InterviewVisit(CareersDirector director, Sitting sitting, InterviewWay way, InterviewDoor door, string employer)
        {
            _director = director;
            _sitting = sitting;
            _way = door != null ? way : InterviewWay.InPlace;
            _door = door;
            _employer = employer ?? string.Empty;
            _tally = new ClassTally(sitting.SkipMinutes);
            _title = DeskGame.Label(sitting.SkipTitle, 0);
            _labels = sitting.SkipLabels();
        }

        /// <summary>True from the offer until it is over, whichever way.</summary>
        public bool Open => _state != PlayerActivityState.Finished;

        /// <summary>The way it is held: in at the door, or where the player stands.</summary>
        public InterviewWay Way => _way;

        // Start on the game's panel: the walk to the door a frame later, as the game's own activities give their walk order.
        private void Start()
        {
            if (_state != PlayerActivityState.NotStarted || _sitting == null)
            {
                return;
            }
            if (SaveGameManager.Current == null || SaveGameManager.Current.Energy <= 1f)
            {
                UI.Notification.Notifications.ShowInsufficientEnergy();
                return;
            }
            if (_director == null || !_director.isActiveAndEnabled)
            {
                End(true, false);
                return;
            }
            _state = PlayerActivityState.MovingTowardsActivity;
            _director.StartCoroutine(Guard.Routine(Walk()));
        }

        private IEnumerator Walk()
        {
            yield return null;   // the game's panel has seen it on its way: it hides itself and lets the clock run
            if (_state != PlayerActivityState.MovingTowardsActivity)
            {
                yield break;
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null)
            {
                End(true, false);
                yield break;
            }
            _player = player;
            if (_door == null || !InterviewGame.Reachable(player, _door.Step))
            {
                player.ResetWalkingAnimation();
                Arrived();   // no walk to be had: in where the player stands, and out at the door all the same
                yield break;
            }
            _orderFrame = Time.frameCount;
            player.SetGoal(_door.Step, Guard.Listener(Arrived));
        }

        // At the door (or where the player stands): in, the screen black as the door's, the minutes the clock's from here.
        private void Arrived()
        {
            if (_state != PlayerActivityState.MovingTowardsActivity)
            {
                return;
            }
            try
            {
                PlayerController player = _player != null ? _player : PlayerHelper.PlayerController;
                _player = player;
                _state = PlayerActivityState.Started;   // the game's panel makes it Running on its next frame
                if (player != null)
                {
                    player.SetNavigationBlocker(Blocker);
                    _blocked = true;
                    if (_door != null)
                    {
                        InterviewGame.FaceIn(player, _door);
                    }
                }
                _tally.Begin(CareersGame.Clock);
                _sitting?.SkipStarted();
                if (_orderFrame >= 0)
                {
                    DeskGame.SetOffSeen("the employer's door", true, _orderFrame);
                }
                _faded = true;
                _fading = true;
                _overlay = InterviewGame.Overlay();
                _director.StartCoroutine(Guard.Routine(Inside()));
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
        }

        // The black screen, then the game's own skip behind it while the clock is this player's.
        private IEnumerator Inside()
        {
            if (_door != null)
            {
                InterviewGame.DoorSound(_door, false);
            }
            bool shared = GoingPublic.ClockShared;
            yield return InterviewGame.Fade(shared ? "reallife_work_interview_fade" : null);
            _fading = false;
            if (_outDue || _state == PlayerActivityState.Finished)
            {
                _outDue = false;
                GiveBack();
                yield break;
            }
            if (!InterviewRules.SkipBehind(shared))
            {
                yield break;   // the shared clock passes at its pace; the panel's Stop is on its key behind the black screen
            }
            for (int i = 0; i < RunningFrames && _state == PlayerActivityState.Started; i++)
            {
                yield return null;
            }
            if (_state == PlayerActivityState.Running)
            {
                _skipped = InterviewGame.Skip(this);   // as its own skip button: the panel performs the minutes and finishes it at the skip's end
            }
        }

        /// <summary>The careers are stopping for a reason of their own (switched off, a fault, the city going): over, nothing counted, everything given back.</summary>
        public void Close()
        {
            try
            {
                if (_state == PlayerActivityState.Running)
                {
                    InterviewGame.CancelSkip();   // the game's panel then finishes the activity itself
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
            try
            {
                if (was == PlayerActivityState.MovingTowardsActivity && walking && _player != null)
                {
                    _player.ResetNavigation();
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            Unblock();
            Sitting sitting = _sitting;
            _sitting = null;
            TallyEnd tally = _tally.Settle(CareersGame.Clock, out int minutes, out bool whole);
            _end = InterviewRules.End(settle, tally == TallyEnd.Counted, whole);
            if (_fading && _director != null && _director.isActiveAndEnabled)
            {
                _outDue = true;   // the fade in finishes first: then it is given back
            }
            else
            {
                GiveBack();
            }
            if (sitting == null || !settle)
            {
                return;
            }
            switch (InterviewRules.Count(_end))
            {
                case InterviewCount.Whole:
                case InterviewCount.Left:
                    sitting.SkipEnded(minutes, whole);
                    break;
                default:
                    if (tally == TallyEnd.CalledOff)
                    {
                        sitting.CalledOff();
                    }
                    break;
            }
        }

        private void Unblock()
        {
            if (!_blocked)
            {
                return;
            }
            _blocked = false;
            try
            {
                if (_player != null)
                {
                    _player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
        }

        // The black screen and the player's place given back, by InterviewRules.Out: out at the door and the fade back, or at once.
        private void GiveBack()
        {
            bool sameCity = _player != null && ReferenceEquals(PlayerHelper.PlayerController, _player) && !InterviewGame.Loading;
            InterviewOut how = InterviewRules.Out(_way, _faded, sameCity);
            _faded = false;
            switch (how)
            {
                case InterviewOut.Nothing:
                    return;
                case InterviewOut.Cleared:
                    InterviewGame.Clear(false, _overlay);
                    Check(how, false, false);
                    return;
            }
            if (_director != null && _director.isActiveAndEnabled)
            {
                _director.StartCoroutine(Guard.Routine(Out(how)));
                return;
            }
            try
            {
                if (how == InterviewOut.AtDoor && _door != null)
                {
                    InterviewGame.PutOut(_player, _door);
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            InterviewGame.Clear(true, _overlay);   // no host of the careers' to fade back on: the game's own fade back, on its interface
            Check(how, how == InterviewOut.AtDoor, true);
        }

        // Out at the door facing the street under the black screen, its sound, then the fade back as the door's.
        private IEnumerator Out(InterviewOut how)
        {
            bool put = false;
            try
            {
                if (how == InterviewOut.AtDoor && _player != null && _door != null)
                {
                    InterviewGame.PutOut(_player, _door);
                    InterviewGame.DoorSound(_door, true);
                    put = true;
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            yield return null;   // a frame for the camera to follow the player out
            if (InterviewGame.Black)
            {
                yield return InterviewGame.Unfade();
            }
            Check(how, put, false);
        }

        // With Developer diagnostics on, one line an interview at a place: where the player came out against the door's step and
        // heading, and the black screen and the navigation given back. fadingBack: the game's own fade back is still under way.
        private void Check(InterviewOut how, bool put, bool fadingBack)
        {
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            string fail = null;
            string where;
            if (how == InterviewOut.AtDoor)
            {
                float metres = float.MaxValue, facing = -1f;
                if (_player != null && _door != null)
                {
                    Vector3 off = _player.transform.position - _door.Step;
                    off.y = 0f;
                    metres = off.magnitude;
                    Vector3 heading = _player.transform.forward;
                    heading.y = 0f;
                    facing = heading.sqrMagnitude > 0.0001f ? Vector3.Dot(heading.normalized, _door.Outward) : -1f;
                }
                where = "out " + metres.ToString("0.00", CultureInfo.InvariantCulture) + " m from the door's step, facing " + facing.ToString("0.00", CultureInfo.InvariantCulture)
                        + " of the street's way (wanted " + InterviewRules.OutReach.ToString("0.0", CultureInfo.InvariantCulture) + " m and "
                        + InterviewRules.OutFacing.ToString("0.0", CultureInfo.InvariantCulture) + ")";
                fail = !put ? "the player could not be put out at the door" : !InterviewRules.OutAtDoor(metres, facing) ? "not out at the door facing the street" : null;
            }
            else
            {
                where = how == InterviewOut.Cleared ? "the city went from under it, the player left where the load put them" : "back where the player stood";
            }
            if (fail == null && InterviewGame.Black && how != InterviewOut.Cleared && !fadingBack)
            {
                fail = "the black screen is still up";
            }
            if (fail == null && _blocked)
            {
                fail = "the player's navigation is still held";
            }
            CareersDirector.Logger?.Info("Careers check (interview at a place): " + _employer + ", " + (_way == InterviewWay.Door ? "in at the door" : "where the player stood")
                                         + ", " + _tally.Done + " of " + _tally.Length + " minutes behind the black screen ("
                                         + (_skipped ? "the game's skip" : GoingPublic.ClockShared ? "the shared clock's pace" : "the clock") + "), ended " + _end + "; " + where
                                         + ", the black screen given back: " + (fail == null ? "PASS" : "FAIL, " + fail));
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
                    Finish();   // however the panel ends it: the minutes counted once, and everything given back
                    return;
                }
                if (_state == PlayerActivityState.MovingTowardsActivity && state == PlayerActivityState.NotStarted)
                {
                    DeskGame.SetOffSeen("the employer's door", false, _orderFrame);
                    End(true, false);   // the game's panel called the walk off for a click elsewhere, and let it go
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
                bool over = _tally.Add(minutes, CareersGame.Clock);
                _sitting?.SkipWorked(_tally.Done);
                if (over)
                {
                    Finish();
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        /// <summary>Over: by the end of the time, Stop, the skip called off, the game calling it off. The minutes sat are settled, once.</summary>
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

        // Cancel before it began: nothing counted, the panel closes.
        private void Cancel()
        {
            if (_state == PlayerActivityState.NotStarted)
            {
                End(true, false);
            }
        }

        public LabelInfo GetHeadlineLabel() => _title;

        public LabelInfo[] GetLabels()
        {
            return _state == PlayerActivityState.NotStarted || _state == PlayerActivityState.Running ? _labels : null;
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
                            new ButtonInfo("CancelRealLifeInterview", "common_cancel", "gray", Guard.Way(Cancel), PlayerAction.Cancel),
                            new ButtonInfo("StartRealLifeInterview", "reallife_job_skip_start", "blue", Guard.Wrap(Start), PlayerAction.Confirm)
                        });
                    case PlayerActivityState.Running:
                        return _stop ?? (_stop = new[] { new ButtonInfo("StopRealLifeInterview", "reallife_job_skip_stop", "gray", Guard.Way(Finish), PlayerAction.Cancel) });
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

        public int GetRemainingMinutesForTimeMachine()
        {
            if (FrameGuard.Off)
            {
                return 0;
            }
            try
            {
                return _tally.Left;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 0);
            }
        }

        public Timestamp GetTimeMachineGoal()
        {
            if (FrameGuard.Off)
            {
                return TimeHelper.Now();
            }
            try
            {
                return TimeHelper.Now().AddMinutes(_tally.Left);
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, TimeHelper.Now());
            }
        }

        public bool HasProgressBar() => _state == PlayerActivityState.Running;

        public float GetProgressBarPercentageValue()
        {
            if (FrameGuard.Off)
            {
                return 0f;
            }
            try
            {
                return 100f * _tally.Done / _tally.Length;
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
                int left = _tally.Left;
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
