using System.Collections.Generic;
using BigAmbitions.Characters;
using Helpers;
using PlayerActivity;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of the mod's routines as one of the game's activities, on the game's own panel and with its own walk there. A
    /// routine says where to go and what <see cref="Begin"/>, <see cref="Minute"/> and <see cref="End"/> do; every way out
    /// goes through <see cref="Finish"/>.
    /// </summary>
    public abstract class HomeActivity : IPlayerActivity
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("home", "HomeActivity", typeof(HomeActivity));   // the game's calls of it (Core/Guard)

        private const float SkipAfter = 6f;      // seconds of a routine that are watched before the game's time skip takes the rest
        private const int WatchedWhole = 14;     // a routine no longer than this many game minutes is watched to the end

        protected readonly HomeDirector Host;
        protected PlayerActivityState State = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        protected int Total;
        protected int Done;
        private bool _begun;
        private bool _again;                     // offered a second time after the game's panel dropped it as it set off
        private Vector3 _goal;                   // where the walk is to end; zero when there was none

        /// <summary>When (unscaled time) the director starts the game's time skip for this routine; negative for never.</summary>
        public float SkipAt = -1f;

        protected HomeActivity(HomeDirector host)
        {
            Host = host;
        }

        public abstract Doing Kind { get; }
        protected abstract string Title { get; }
        protected abstract string StartKey { get; }

        /// <summary>The furniture to walk to, or null for a piece of floor (<see cref="Floor"/>).</summary>
        protected virtual EntityController Furniture => null;

        /// <summary>The clear floor wanted round a routine without furniture.</summary>
        protected virtual float Floor => 0.7f;

        /// <summary>Last look before setting off: false (having said why) keeps the panel open.</summary>
        protected virtual bool Ready(PlayerController player) => true;

        /// <summary>On arrival, before anything is seen: false (having said why) calls the routine off.</summary>
        protected virtual bool Prepare(PlayerController player) => true;

        protected abstract void Begin(PlayerController player);
        protected virtual void Minute(PlayerController player, int minutes) { }
        protected abstract void End(PlayerController player, bool completed, int minutes);

        protected virtual int SliderLow => 0;
        protected virtual int SliderHigh => 0;
        protected virtual int SliderNow => Total;
        protected virtual void Slide(int value) { Total = value; }
        protected virtual (string, object) SliderText() => (Title, null);
        protected virtual LabelInfo[] Labels() => null;

        public bool Running => State == PlayerActivityState.Running;
        public bool Over => State == PlayerActivityState.Finished;

        // IPlayerActivity: the game's calls, each behind the guard.

        public virtual bool RequiresEnergy() => false;
        public PlayerActivityState GetState()
        {
            if (FrameGuard.Off)
            {
                return Guard.Ended(this);   // (its feature is off: the activity ends as its own Finish ends it, and the panel closes)
            }
            try
            {
                return State;
            }
            catch (System.Exception fault)
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
                State = state;
            }
            catch (System.Exception fault)
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
                if (State == PlayerActivityState.Finished)
                {
                    return;
                }
                Done += minutes;
                if (Done >= Total)
                {
                    Finish();
                    return;
                }
                Minute(PlayerHelper.PlayerController, minutes);
            }
            catch (System.Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void Finish()
        {
            try
            {
                if (State == PlayerActivityState.Finished)
                {
                    return;
                }
                PlayerController player = PlayerHelper.PlayerController;
                if (State == PlayerActivityState.MovingTowardsActivity && player != null)
                {
                    player.ResetNavigation();
                }
                _before = State;
                State = PlayerActivityState.Finished;
                SkipAt = -1f;
                if (_begun)
                {
                    _begun = false;
                    Host.Body.Release();
                    try
                    {
                        End(player, Done >= Total, Mathf.Min(Done, Total));
                    }
                    catch (System.Exception e)
                    {
                        HomeDirector.Logger?.Error(e);
                    }
                }
                Done = 0;
            }
            catch (System.Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        private void Cancel()
        {
            _before = State;
            State = PlayerActivityState.Finished;
        }

        /// <summary>Starts the routine without the start button, for one chosen on the mod's own list; only once the game's panel has shown it for a frame or two.</summary>
        public void StartNow()
        {
            if (State == PlayerActivityState.NotStarted)
            {
                Start();
            }
        }

        public void CallOff()
        {
            if (State == PlayerActivityState.NotStarted)
            {
                Cancel();
            }
        }

        private void Start()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || !Ready(player))
            {
                return;
            }
            State = PlayerActivityState.MovingTowardsActivity;
            // The walk order is given at the end of this frame, as the game's own activities give theirs: from the end of the
            // frame in which its panel sees a routine on its way, the panel takes any new walk order for a click elsewhere and
            // drops the routine (PlayerActivityUI.CancelActivityMovement).
            if (_again)
            {
                // The second go, after the panel dropped the first: at once, before the panel has seen it on its way.
                SetOff();
                return;
            }
            Host.StartCoroutine(Guard.Routine(SetOffAtFrameEnd()));
        }

        private static readonly WaitForEndOfFrame FrameEnd = new WaitForEndOfFrame();

        private System.Collections.IEnumerator SetOffAtFrameEnd()
        {
            yield return FrameEnd;
            try
            {
                SetOff();
            }
            catch (System.Exception e)
            {
                HomeDirector.Logger?.Error(e);
                HomeDirector.Refuse("reallife_home_refused_called_off", Title, "error while setting off");
                if (State == PlayerActivityState.MovingTowardsActivity)
                {
                    Cancel();
                }
            }
        }

        private void SetOff()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (State != PlayerActivityState.MovingTowardsActivity || player == null)
            {
                return;
            }
            EntityController furniture = Furniture;
            Vector3 target;
            if (furniture != null)
            {
                if (!player.ExistsRoute(furniture, showErrorNotification: true))
                {
                    // The game has said so itself ("cannot reach").
                    HomeDirector.Logger?.Info("Home: '" + Title + "' not started (no way to the furniture on foot).");
                    Cancel();
                    return;
                }
                target = furniture.GetClosestNavMeshTargetPosition(player.transform.position);
            }
            else if (!HomePlace.ClearFloor(player, Floor, out target))
            {
                HomeDirector.Refuse("reallife_home_refused_no_room", Title);
                Cancel();
                return;
            }
            Vector3 gap = target - player.transform.position;
            gap.y = 0f;
            if (target == Vector3.zero || gap.sqrMagnitude < 0.2f * 0.2f)
            {
                _goal = Vector3.zero;
                player.ResetWalkingAnimation();
                Arrived();
                return;
            }
            _goal = target;
            player.SetGoal(target, Guard.Listener(Arrived));
            if (State == PlayerActivityState.NotStarted)
            {
                // The panel took the order for a click elsewhere after all and dropped the routine: offered once more, then the player is told.
                player.RemoveGoal();
                if (!_again)
                {
                    _again = true;
                    HomeDirector.Logger?.Info("Home: '" + Title + "' was called off by the game's panel as it set off; offered again.");
                    Host.Again(this);
                }
                else
                {
                    HomeDirector.Refuse("reallife_home_refused_called_off", Title);
                }
            }
            else if (State == PlayerActivityState.MovingTowardsActivity && !player.hasOnGoalReachedAction)
            {
                // The game would not take a walk order at all (something of its own holds the player where they stand).
                HomeDirector.Refuse("reallife_home_refused_cannot_walk", Title);
                Cancel();
            }
        }

        private void Arrived()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (State != PlayerActivityState.MovingTowardsActivity || player == null || player.Character == null)
            {
                return;
            }
            if (_goal != Vector3.zero)
            {
                // The game also says "arrived" when the walk never began (a movement key was held down).
                Vector3 off = _goal - player.transform.position;
                off.y = 0f;
                if (off.sqrMagnitude > 1.5f * 1.5f)
                {
                    HomeDirector.Refuse("reallife_home_refused_not_reached", Title);
                    Cancel();
                    return;
                }
            }
            EntityController furniture = Furniture;
            if (furniture != null)
            {
                if (furniture.Occupied)
                {
                    HomeDirector.Refuse("reallife_home_refused_in_use", Title);
                    Cancel();
                    return;
                }
                Vector3 look = furniture.transform.position - player.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                {
                    player.Character.ForceToRotation(Quaternion.LookRotation(look.normalized));
                }
            }
            if (!Prepare(player))
            {
                Cancel();   // it has said why
                return;
            }
            State = PlayerActivityState.Started;   // the game's panel makes it Running on its next frame
            Done = 0;
            _begun = true;
            Host.Body.Bind(player);
            Host.Body.Hold();
            Begin(player);
            // A short routine is watched through; a long one for a few seconds, then the game's time skip takes the rest. Never
            // out of doors: the time skip clears the street of traffic.
            SkipAt = Total > WatchedWhole && BuildingManager.IsInsideBuilding ? Time.unscaledTime + SkipAfter : -1f;
        }

        public LabelInfo GetHeadlineLabel()
        {
            if (FrameGuard.Off)
            {
                return Guard.Blank();
            }
            try
            {
                return Headline();
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, Guard.Blank());
            }
        }

        protected virtual LabelInfo Headline()
        {
            return new LabelInfo(Title, InstanceBehavior<GlobalReferences>.Instance.colors.white);
        }

        public LabelInfo[] GetLabels()
        {
            if (FrameGuard.Off)
            {
                return null;
            }
            try
            {
                return State == PlayerActivityState.NotStarted ? Labels() : null;
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (LabelInfo[])null);
            }
        }

        public ButtonInfo[] GetButtons()
        {
            if (FrameGuard.Off)
            {
                return null;
            }
            try
            {
                switch (State)
                {
                    case PlayerActivityState.NotStarted:
                        return new[]
                        {
                            new ButtonInfo("CancelHomeRoutine", "common_cancel", "gray", Guard.Way(Cancel), PlayerAction.Cancel),
                            new ButtonInfo("StartHomeRoutine", StartKey, "blue", Guard.Wrap(Start), PlayerAction.Confirm)
                        };
                    case PlayerActivityState.Running:
                        return new[] { new ButtonInfo("StopHomeRoutine", "reallife_home_stop", "gray", Guard.Way(Finish), PlayerAction.Cancel) };
                    default:
                        return null;
                }
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (ButtonInfo[])null);
            }
        }

        public bool HasFastForward()
        {
            if (FrameGuard.Off)
            {
                return false;
            }
            try
            {
                return State == PlayerActivityState.Running;
            }
            catch (System.Exception fault)
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
                return State == PlayerActivityState.Running;
            }
            catch (System.Exception fault)
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
                return Mathf.Max(0, Total - Done);
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 0);
            }
        }
        public bool HasProgressBar()
        {
            if (FrameGuard.Off)
            {
                return false;
            }
            try
            {
                return State == PlayerActivityState.Running;
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, false);
            }
        }
        public float GetProgressBarPercentageValue()
        {
            if (FrameGuard.Off)
            {
                return 0f;
            }
            try
            {
                return 100f * Done / Mathf.Max(1, Total);
            }
            catch (System.Exception fault)
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
                int left = Mathf.Max(0, Total - Done);
                return ("playeractivityui_time_remaining", new { hours = left / 60, minutes = left % 60 });
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (string.Empty, (object)null));
            }
        }

        public bool HasSlider()
        {
            if (FrameGuard.Off)
            {
                return false;
            }
            try
            {
                return State == PlayerActivityState.NotStarted && SliderHigh > SliderLow;
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, false);
            }
        }
        public int GetMinSliderValue()
        {
            if (FrameGuard.Off)
            {
                return 0;
            }
            try
            {
                return SliderLow;
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 0);
            }
        }
        public int GetMaxSliderValue()
        {
            if (FrameGuard.Off)
            {
                return 0;
            }
            try
            {
                return SliderHigh;
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 0);
            }
        }
        public float GetCurrentSliderValue()
        {
            if (FrameGuard.Off)
            {
                return 0f;
            }
            try
            {
                return SliderNow;
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 0f);
            }
        }
        public void OnSliderValueChanged(int value)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (SliderHigh > SliderLow)   // the panel sets its slider up for every activity, with or without one
                {
                    Slide(Mathf.Clamp(value, SliderLow, SliderHigh));
                }
            }
            catch (System.Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }
        public (string key, object arguments) GetSliderInfo()
        {
            if (FrameGuard.Off)
            {
                return (string.Empty, (object)null);
            }
            try
            {
                return SliderText();
            }
            catch (System.Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (string.Empty, (object)null));
            }
        }

        /// <summary>One of the mod's clips by id, if the bundle has it (asked quietly: most of these are still on the wish-list).</summary>
        protected bool Clip(string id)
        {
            return ClipLibrary.Has(id) && Host.Body.Clip(id);
        }

        protected static LabelInfo Line(string key, object arguments = null)
        {
            return new LabelInfo(key, arguments, InstanceBehavior<GlobalReferences>.Instance.colors.white);
        }

        /// <summary>One of the game's one-off gestures on the player (a trigger): harmless if their animator has none such.</summary>
        protected static void Gesture(PlayerController player, AnimationType gesture)
        {
            if (player != null && player.Character != null && player.Character.animator != null)
            {
                player.Character.animator.SetTrigger(gesture);
            }
        }

        protected static void Toast(NotificationType type, string key, string name = null, string value = null, string name2 = null, string value2 = null)
        {
            Dictionary<string, string> data = null;
            if (name != null)
            {
                data = new Dictionary<string, string>(2) { { name, value } };
                if (name2 != null)
                {
                    data[name2] = value2;
                }
            }
            Notifications.Show(type, key, data, 8f);
        }
    }
}
