using System;
using System.Collections;
using BigAmbitions.Characters;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
using BigAmbitions.SoundSystem;
using Helpers;
using PlayerActivity;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// An online class, homework, a working day or a video interview at the computer at home, as the game's computer passes the time: one press
    /// walks the player there and sits them; the minutes count once (<see cref="ClassTally"/>). Nothing per frame of its own.
    /// </summary>
    internal sealed class ComputerSitting : IPlayerActivity
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("careers_study", "ComputerSitting", typeof(ComputerSitting));   // the game's calls of it (Core/Guard)

        private readonly CareersDirector _director;
        private readonly ComputerController _computer;
        private readonly ItemController _chair;
        private readonly ClassTally _tally;
        private readonly LabelInfo _title;
        private readonly string _typing;          // the computer's own using animation, as the game's computer has its user type
        private readonly bool _work;              // a working day, overtime or a video interview (else a class or homework)
        private Sitting _sitting;
        private LabelInfo[] _labels;
        private ButtonInfo[] _stop;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private float _nextType;
        private int _orderFrame = -1;             // the frame the walk order to the computer was given in (DeskGame.SetOffSeen)

        public ComputerSitting(CareersDirector director, ComputerController computer, Sitting sitting)
        {
            _director = director;
            _computer = computer;
            _sitting = sitting;
            _tally = new ClassTally(sitting.SkipMinutes);
            _title = DeskGame.Label(sitting.SkipTitle, 0);
            _work = sitting.Kind == SittingKind.Work || sitting.Kind == SittingKind.Overtime || sitting.Kind == SittingKind.Interview;
            _chair = computer.FindChair();
            AnimationType typing = AnimationType.UsingDesktopComputer;
            try
            {
                Item item = ItemsGetter.GetByName(computer.itemName);
                if (item != null && item.usingAnimation != AnimationType.None)
                {
                    typing = item.usingAnimation;
                }
            }
            catch (Exception)
            {
                // the computer's own animation is a nicety: the desk computer's typing otherwise
            }
            _typing = typing.ToStringFast();
        }

        /// <summary>True from the press until it is over, whichever way.</summary>
        public bool Open => _state != PlayerActivityState.Finished;

        /// <summary>The computer it is at.</summary>
        public ComputerController Computer => _computer;

        /// <summary>On the game's panel now: a frame later on its way, a frame after that the walk.</summary>
        public void Go()
        {
            _director.StartCoroutine(Guard.Routine(Walk()));
        }

        private IEnumerator Walk()
        {
            yield return null;   // the game's panel has seen it not started
            if (_state != PlayerActivityState.NotStarted)
            {
                yield break;
            }
            _state = PlayerActivityState.MovingTowardsActivity;
            yield return null;   // as the game's computer gives its walk order: a frame after its Start
            if (_state != PlayerActivityState.MovingTowardsActivity)
            {
                yield break;
            }
            PlayerController player = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.playerController : null;
            EntityController to = _chair != null ? _chair : (EntityController)_computer;
            if (player == null || _computer == null || !player.ExistsRoute(to, showErrorNotification: true))
            {
                End(true, false);   // the game has said it cannot be reached
                yield break;
            }
            Vector3 target = to.GetClosestNavMeshTargetPosition(PlayerHelper.GetPosition());
            if (target == Vector3.zero)
            {
                player.ResetWalkingAnimation();
                Arrived();
            }
            else
            {
                _orderFrame = Time.frameCount;
                player.SetGoal(target, Guard.Listener(Arrived));
            }
        }

        // There, as the game's computer seats its user: the chair, the hands on the computer, the screen, the energy.
        private void Arrived()
        {
            if (_state != PlayerActivityState.MovingTowardsActivity)
            {
                return;
            }
            try
            {
                PlayerController player = InstanceBehavior<GameManager>.Instance.playerController;
                ThirdPersonCharacter character = player.Character;
                _state = PlayerActivityState.Started;   // the game's panel makes it Running on its next frame
                player.SetNavigationBlocker(NavigationBlocker.StudyActivity);
                if (_chair != null)
                {
                    character.SitOnChair(_chair.transform);
                    _chair.Occupied = true;
                }
                _computer.Occupied = true;
                character.SetItemIKTargets(_computer);
                _computer.PlayVideoOnScreen(VideoClipData.VideoType.Work);
                _nextType = Time.time + UnityEngine.Random.Range(0f, 6f);
                _tally.Begin(CareersGame.Clock);   // the minutes are the clock's from here, a skip's included
                _sitting?.SkipStarted();
                DeskGame.SetOffSeen("the computer", true, _orderFrame);
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
        }

        /// <summary>The careers are stopping for a reason of their own (switched off, a fault): over, and nothing more is said.</summary>
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
            PlayerController player = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.playerController : null;
            try
            {
                if (player != null && was == PlayerActivityState.MovingTowardsActivity && walking)
                {
                    player.ResetNavigation();
                }
                else if (player != null && (was == PlayerActivityState.Started || was == PlayerActivityState.Running))
                {
                    StandUp(player);
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            Sitting sitting = _sitting;
            _sitting = null;
            TallyEnd end = _tally.Settle(CareersGame.Clock, out int minutes, out bool whole);
            if (sitting == null || !settle)
            {
                return;
            }
            if (end == TallyEnd.Counted)
            {
                sitting.SkipEnded(minutes, whole);
            }
            else if (end == TallyEnd.CalledOff)
            {
                sitting.CalledOff();
            }
        }

        // Up from the chair as the game's computer stands its user up; the study blocker goes in a finally, whatever fails before it.
        private void StandUp(PlayerController player)
        {
            try
            {
                ThirdPersonCharacter character = player.Character;
                if (_chair != null)
                {
                    character.Reset();
                    InstanceBehavior<SfxManager>.Instance?.PlayAudio(SoundType.ChairStandUp, player.transform.position, 1f, isPlayerCreatedSound: true);
                    _chair.Occupied = false;
                }
                if (_computer != null)
                {
                    _computer.Occupied = false;
                    _computer.StopVideoOnScreen();
                }
                character.SetItemIKTargets(null);
                PlayerHelper.GetAnimator().ResetTrigger(_typing);
            }
            finally
            {
                player.UnsetNavigationBlocker(NavigationBlocker.StudyActivity);
            }
        }

        // A class or homework ends when the energy runs out, as the game's studying does; a working day or an interview does not, as at the desk.
        public bool RequiresEnergy() => !_work;

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
                    Finish();   // however the panel ends it: the minutes counted once, and the player stood up
                    return;
                }
                if (_state == PlayerActivityState.MovingTowardsActivity && state == PlayerActivityState.NotStarted)
                {
                    DeskGame.SetOffSeen("the computer", false, _orderFrame);
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
                else if (Time.time >= _nextType)
                {
                    try
                    {
                        PlayerHelper.GetAnimator().SetTrigger(_typing);
                    }
                    catch (Exception)
                    {
                        // the animation is a nicety
                    }
                    _nextType = Time.time + UnityEngine.Random.Range(5f, 10f);
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        /// <summary>Over: by the end of the time, Stop, the game calling it off. The minutes sat are settled, once.</summary>
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

        public LabelInfo GetHeadlineLabel() => _title;

        public LabelInfo[] GetLabels()
        {
            if (FrameGuard.Off)
            {
                return (LabelInfo[])null;
            }
            try
            {
                if (_state != PlayerActivityState.Running)
                {
                    return null;
                }
                if (_labels == null && _sitting != null)
                {
                    _labels = _sitting.SkipLabels();   // made once
                }
                return _labels;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (LabelInfo[])null);
            }
        }

        public ButtonInfo[] GetButtons()
        {
            if (FrameGuard.Off)
            {
                return (ButtonInfo[])null;
            }
            try
            {
                return _state == PlayerActivityState.Running
                    ? _stop ?? (_stop = new[] { new ButtonInfo(ClassPlayRules.DeskStopName, "reallife_job_skip_stop", "gray", Guard.Way(Finish), PlayerAction.Cancel) })
                    : null;
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
