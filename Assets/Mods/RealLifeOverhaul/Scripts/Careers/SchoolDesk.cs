using System;
using System.Collections;
using System.Reflection;
using BigAmbitions.Characters;
using BigAmbitions.DayNightCycle;
using Helpers;
using PlayerActivity;
using UI;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A class or exam of the mod's at a school desk or a university classroom, through the game's study flow: the game's
    /// activity for the desk is handed to this one, which adds the class's button (<see cref="DeskOffer.Only"/>: the class's
    /// panel alone). In a Going Public session made with <see cref="Own"/>, never a StudyActivity. One object per press of Study.
    /// </summary>
    internal sealed class SchoolDesk : IPlayerActivity
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("careers_study", "SchoolDesk", typeof(SchoolDesk));   // the game's calls of it (Core/Guard)

        private static FieldInfo _doorField, _placeField;
        private static MethodInfo _setActivity;
        private static bool _failed;

        private readonly StudyActivity _inner;
        private readonly EntityController _door;
        private readonly StudyDiploma _place;
        private readonly CareersDirector _director;
        private readonly DeskOffer _offer;
        private ButtonInfo[] _buttons;
        private LabelInfo[] _labels;
        private LabelInfo[] _gameLabels;   // the game's own course's labels last seen, and with the class's lines after them...
        private LabelInfo[] _withLines;    // ...made again only when the game gives new ones

        private bool _mine;
        private Sitting _sitting;
        private ClassTally _tally;
        private LabelInfo[] _running;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private float _nextType;
        private ButtonInfo[] _stop;
        private int _orderFrame = -1;   // the frame the walk order to the desk was given in (DeskGame.SetOffSeen)

        /// <summary>What the desk offers: a course's class or exam, whether it can be begun now, its button's words, and why not.</summary>
        internal sealed class DeskOffer
        {
            public int Course;
            public SittingKind Kind;   // a class, homework or an exam
            public bool Can;           // it can be begun now: only then has the panel its button, as the game's own panels do
            public string Key;         // the button's words, a key with no {placeholder}: the game's panel writes a button's key alone
            public string Why;         // the reason key while it cannot be begun (said in Lines too)
            public bool Only;          // the class's panel alone (a classroom of the university): no course of the game's
            public bool Due;           // a class or exam of the player's at the school is in its window: the class's button on the Start key (ClassPlayRules.ClassOnStart)
            public string Title;       // ...its headline
            public string[] Lines;     // ...and its lines
            public Sitting Test;       // a test's class, begun as it is outside its course's hours (Options > Tests), or null
        }

        private SchoolDesk(StudyActivity inner, EntityController door, StudyDiploma place, CareersDirector director, DeskOffer offer)
        {
            _inner = inner;
            _door = door;
            _place = place;
            _director = director;
            _offer = offer;
        }

        /// <summary>True while it runs the mod's class (from the press of its button until it is over).</summary>
        public bool Open => _mine && _state != PlayerActivityState.Finished;

        /// <summary>True if the game's study activity and panel have the three members a desk of ours needs (looked up once).</summary>
        public static bool Works => Resolve();

        // The game's three members, looked up together the first time any is wanted.
        private static bool Resolve()
        {
            if (_failed)
            {
                return false;
            }
            if (_doorField != null && _placeField != null && _setActivity != null)
            {
                return true;
            }
            _doorField = GameContract.StudyDoor.Field;   // the game's private pieces, resolved once (Core/GameContract)
            _placeField = GameContract.StudyPlace.Field;
            _setActivity = GameContract.CurrentActivity.Setter;
            if (_doorField == null || _placeField == null || _setActivity == null)
            {
                _failed = true;
                CareersDirector.Logger?.Warn("Careers: the game's study activity is not as this version expects ("
                                             + (_doorField == null ? "its door" : _placeField == null ? "its desk" : "the panel's activity")
                                             + " is not there); classes at the school and the university are attended with the Attend button where the player stands instead.");
                return false;
            }
            return true;
        }

        /// <summary>The classroom door the game's study activity was opened at, or null.</summary>
        public static EntityController DoorOf(StudyActivity game)
        {
            if (game == null || !Resolve())
            {
                return null;
            }
            try
            {
                return _doorField.GetValue(game) as EntityController;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's study panel just opened at a school desk: handed to a desk of ours with the offer; null if not as expected (nothing changed).</summary>
        public static SchoolDesk Take(PlayerActivityUI ui, StudyActivity game, CareersDirector director, DeskOffer offer)
        {
            if (ui == null || game == null || offer == null || !Resolve())
            {
                return null;
            }
            try
            {
                var door = _doorField.GetValue(game) as EntityController;
                var place = _placeField.GetValue(game) as StudyDiploma;
                if (door == null || place == null || place.chair == null || place.exitPosition == null)
                {
                    CareersDirector.Logger?.Warn("Careers: a study desk of the game's has " + (door == null ? "no door" : place == null ? "no desk" : place.chair == null ? "no chair" : "no way out")
                                                 + " (" + (door != null ? door.name : "?") + "); it is left to the game.");
                    return null;
                }
                var desk = new SchoolDesk(game, door, place, director, offer);
                _setActivity.Invoke(ui, new object[] { desk });
                ui.UpdateActivityDisplay();
                return desk;
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
                return null;
            }
        }

        /// <summary>A desk of ours from a study door without the game's panel (a Going Public session); null if it lacks its chair or way out.</summary>
        public static SchoolDesk Own(EducationDoorController door, CareersDirector director, DeskOffer offer)
        {
            StudyDiploma place = door != null ? door.StudyDiploma : null;
            if (offer == null || place == null || place.chair == null || place.exitPosition == null)
            {
                CareersDirector.Logger?.Warn("Careers: a study desk of the game's has " + (place == null ? "no desk" : place.chair == null ? "no chair" : "no way out")
                                             + " (" + (door != null ? door.name : "?") + "); its class is not offered there.");
                return null;
            }
            try
            {
                ItemController seat = place.chair.GetComponent<ItemController>();
                place.computer = seat != null ? seat.parentItemController : null;   // as the game's door sets it before its panel: the screen's video
                if (!(place.CreateActivity(door) is StudyActivity game))
                {
                    return null;
                }
                offer.Only = true;
                return new SchoolDesk(game, door, place, director, offer);
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
                return null;
            }
        }

        /// <summary>The class's button pressed for the player (the Campus app sent them here), a frame after the game's panel has seen the desk not started.</summary>
        public void Begin()
        {
            _director.StartCoroutine(Guard.Routine(BeginSoon()));
        }

        private IEnumerator BeginSoon()
        {
            yield return null;
            UIs uis = InstanceBehavior<UIs>.Instance;
            bool shown = uis != null && uis.playerActivityUI != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, this);
            if (shown && !_mine && _offer.Can && _inner.GetState() == PlayerActivityState.NotStarted)
            {
                Press();
            }
        }

        private void Press()
        {
            if (_mine || _inner.GetState() != PlayerActivityState.NotStarted)
            {
                return;
            }
            Sitting sitting = _offer.Test;
            string why = sitting != null ? _director.HoldTest(sitting, this) : _director.DeskSitting(_offer.Course, _offer.Kind, this, out sitting);
            if (why != null)
            {
                Notifications.Show(NotificationType.Warning, why, null, 8f);
                return;
            }
            _mine = true;
            _sitting = sitting;
            _tally = new ClassTally(sitting.SkipMinutes);
            _director.StartCoroutine(Guard.Routine(Walk()));
        }

        // Set off as the game's activities are ("Setting off" in docs/features/careers.md), then the chair, the screen and the energy.
        private IEnumerator Walk()
        {
            yield return null;
            if (_state != PlayerActivityState.NotStarted)
            {
                yield break;   // called off meanwhile
            }
            _state = PlayerActivityState.MovingTowardsActivity;
            yield return null;
            if (_state != PlayerActivityState.MovingTowardsActivity)
            {
                yield break;
            }
            PlayerController player = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.playerController : null;
            if (player == null || !player.ExistsRoute(_door, showErrorNotification: true))
            {
                Off();
                yield break;
            }
            _orderFrame = Time.frameCount;
            player.SetGoal(_door, Guard.Listener(() =>
            {
                if (_state != PlayerActivityState.MovingTowardsActivity)
                {
                    return;
                }
                try
                {
                    _state = PlayerActivityState.Started;   // the game's panel makes it Running on its next frame
                    _place.computer?.PlayVideoOnScreen(VideoClipData.VideoType.Work);
                    player.SetNavigationBlocker(NavigationBlocker.StudyActivity);
                    player.Character.SitOnChair(_place.chair);
                    _nextType = Time.time + UnityEngine.Random.Range(0f, 6f);
                    _tally.Begin(CareersGame.Clock);   // the minutes are the clock's from here, a skip's included
                    _sitting.SkipStarted();
                    DeskGame.SetOffSeen("the class's desk", true, _orderFrame);
                }
                catch (Exception e)
                {
                    CareersDirector.Logger?.Error(e);
                }
            }));
        }

        // Called off before the class began: the sitting goes with nothing counted and nothing said.
        private void Off()
        {
            _before = _state;
            _state = PlayerActivityState.Finished;
            Sitting sitting = _sitting;
            _sitting = null;
            _tally?.Settle(out _, out _);
            sitting?.CalledOff();
        }

        // IPlayerActivity: the game's study activity answers until the class's button is pressed.

        public bool RequiresEnergy() => true;

        public bool BlocksAutoSave() => true;

        public PlayerActivityState GetState()
        {
            if (FrameGuard.Off)
            {
                return Guard.Ended(this);   // its feature is off: ended as its Finish ends it, and the panel closes
            }
            try
            {
                return _mine ? _state : _inner.GetState();
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
                return Guard.Ended(this);
            }
        }

        public PlayerActivityState GetStateBeforeFinishing()
        {
            if (FrameGuard.Off)
            {
                return PlayerActivityState.NotStarted;
            }
            try
            {
                return _mine ? _before : _inner.GetStateBeforeFinishing();
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, PlayerActivityState.NotStarted);
            }
        }

        public void ChangeState(PlayerActivityState state)
        {
            try
            {
                if (_mine)
                {
                    if (state == PlayerActivityState.Finished)
                    {
                        Finish();   // however the panel ends it: the minutes counted once, and the player stood up
                        return;
                    }
                    if (_state == PlayerActivityState.MovingTowardsActivity && state == PlayerActivityState.NotStarted)
                    {
                        DeskGame.SetOffSeen("the class's desk", false, _orderFrame);
                        Off();   // the game's panel called the walk off for a click elsewhere and let it go: the player walks there
                        return;
                    }
                    if (_state != PlayerActivityState.Finished)
                    {
                        _state = state;
                    }
                }
                else
                {
                    _inner.ChangeState(state);
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
                if (!_mine)
                {
                    _inner.Perform(minutes);
                    return;
                }
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
                    DeskGame.Type();
                    _nextType = Time.time + UnityEngine.Random.Range(5f, 10f);
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
                if (!_mine)
                {
                    _inner.Finish();
                    return;
                }
                if (_state == PlayerActivityState.Finished)
                {
                    return;
                }
                PlayerActivityState was = _state;
                if (was != PlayerActivityState.Started && was != PlayerActivityState.Running)
                {
                    // Not sat down yet: the walk stopped, nothing to stand up from, nothing counted.
                    try
                    {
                        if (was == PlayerActivityState.MovingTowardsActivity)
                        {
                            InstanceBehavior<GameManager>.Instance.playerController.ResetNavigation();
                        }
                    }
                    catch (Exception e)
                    {
                        CareersDirector.Logger?.Error(e);
                    }
                    Off();
                    return;
                }
                _before = was;
                _state = PlayerActivityState.Finished;   // first: whatever the game's calls below do, nothing here runs twice
                StandUp();
                Sitting sitting = _sitting;
                _sitting = null;
                if (_tally.Settle(CareersGame.Clock, out int minutes, out bool whole) == TallyEnd.Counted)
                {
                    sitting?.SkipEnded(minutes, whole);   // once: a second end finds the tally settled
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        // Up at the desk's exit, as the game's studying stands its player up; the study blocker goes in a finally, whatever fails before it.
        private void StandUp()
        {
            GameManager game = InstanceBehavior<GameManager>.Instance;
            PlayerController player = game != null ? game.playerController : null;
            try
            {
                if (player != null)
                {
                    player.Character.Reset();
                    player.Character.navmeshAgent.Warp(_place.exitPosition.position);
                }
                PlayerHelper.GetAnimator().ResetTrigger(AnimationType.UsingDesktopComputer.ToStringFast());
                _place.computer?.StopVideoOnScreen();
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            finally
            {
                if (player != null)
                {
                    player.UnsetNavigationBlocker(NavigationBlocker.StudyActivity);
                }
            }
        }

        // The class's panel alone closed: before its button, nothing of the game's; after, called off or ended with its minutes counted.
        private void Close()
        {
            if (_mine)
            {
                Finish();
                return;
            }
            _inner.ChangeState(PlayerActivityState.Finished);
        }

        public LabelInfo GetHeadlineLabel()
        {
            if (FrameGuard.Off)
            {
                return Guard.Blank();
            }
            try
            {
                if (_mine && _sitting != null)
                {
                    return new LabelInfo(_sitting.SkipTitle, GameUi.WorldPanel.White(), false);
                }
                return _offer.Only && !_mine ? new LabelInfo(_offer.Title ?? string.Empty, GameUi.WorldPanel.White(), false) : _inner.GetHeadlineLabel();
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, Guard.Blank());
            }
        }

        // The game's own course's labels with the class's course and lines after them (a desk of the school), made again only
        // when the game gives new ones: it makes its labels as its panel is drawn, never per frame.
        private LabelInfo[] WithLines(LabelInfo[] game)
        {
            int n = _offer.Lines != null ? _offer.Lines.Length : 0;
            if (n == 0)
            {
                return game;
            }
            if (_withLines != null && ReferenceEquals(game, _gameLabels))
            {
                return _withLines;
            }
            int g = game != null ? game.Length : 0;
            var all = new LabelInfo[g + 1 + n];
            for (int i = 0; i < g; i++)
            {
                all[i] = game[i];
            }
            all[g] = new LabelInfo(_offer.Title ?? string.Empty, GameUi.WorldPanel.White(), false);
            for (int i = 0; i < n; i++)
            {
                all[g + 1 + i] = new LabelInfo(_offer.Lines[i] ?? string.Empty, GameUi.WorldPanel.White(), false);
            }
            _gameLabels = game;
            _withLines = all;
            return all;
        }

        public LabelInfo[] GetLabels()
        {
            if (FrameGuard.Off)
            {
                return (LabelInfo[])null;
            }
            try
            {
                if (!_mine && _offer.Only)
                {
                    if (_labels == null)
                    {
                        int n = _offer.Lines != null ? _offer.Lines.Length : 0;
                        _labels = new LabelInfo[n];
                        for (int i = 0; i < n; i++)
                        {
                            _labels[i] = new LabelInfo(_offer.Lines[i] ?? string.Empty, GameUi.WorldPanel.White(), false);
                        }
                    }
                    return _labels;
                }
                if (!_mine)
                {
                    return WithLines(_inner.GetLabels());
                }
                if (_sitting == null || _state != PlayerActivityState.Running)
                {
                    return null;
                }
                return _running ?? (_running = _sitting.SkipLabels());   // made once
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
                if (_mine)
                {
                    return _state == PlayerActivityState.Running
                        ? _stop ?? (_stop = new[] { new ButtonInfo(ClassPlayRules.DeskStopName, "reallife_job_skip_stop", "gray", Guard.Way(Finish), PlayerAction.Cancel) })
                        : null;
                }
                if (_offer.Only)
                {
                    // on the game's Start key: no course of the game's here, and its secondary key is the panel's fast forward. With
                    // nothing to begin, Cancel alone, as the game's own course panel once its course is done: the game's panel
                    // neither dims a button nor stops its key, so a button that cannot begin is not shown at all
                    if (_buttons == null)
                    {
                        var cancel = new ButtonInfo(ClassPlayRules.DeskCancelName, "common_cancel", "gray", Guard.Way(Close), PlayerAction.Cancel);
                        _buttons = _offer.Can
                            ? new[] { cancel, new ButtonInfo(ClassPlayRules.DeskStartName, _offer.Key, "blue", Guard.Wrap(Press), PlayerAction.Confirm, _offer.Can) }
                            : new[] { cancel };
                    }
                    return _buttons;
                }
                ButtonInfo[] game = _inner.GetButtons();
                if (_inner.GetState() != PlayerActivityState.NotStarted)
                {
                    return game;
                }
                if (_buttons == null)
                {
                    // While a class or exam is due, the class on the game's Start key and the game's own course on the secondary key,
                    // as a classroom of the university; else the class's button on the secondary key beside the game's Start.
                    bool first = _offer.Due;
                    int n = game != null ? game.Length : 0;
                    _buttons = new ButtonInfo[n + (_offer.Can ? 1 : 0)];
                    for (int i = 0; i < n; i++)
                    {
                        ButtonInfo b = game[i];
                        _buttons[i] = first && b != null && b.playerAction == PlayerAction.Confirm
                            ? new ButtonInfo(b.name, b.key, b.arguments, b.color, b.onClick, PlayerAction.SecondaryInteract, b.interactable)
                            : b;
                    }
                    if (_offer.Can)
                    {
                        _buttons[n] = new ButtonInfo(ClassPlayRules.DeskStartName, _offer.Key, "blue", Guard.Wrap(Press), first ? PlayerAction.Confirm : PlayerAction.SecondaryInteract, _offer.Can);
                    }
                }
                return _buttons;
            }
            catch (Exception fault)
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
                return _mine ? _state == PlayerActivityState.Running : !_offer.Only && _inner.HasFastForward();
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
                return _mine ? _state == PlayerActivityState.Running : !_offer.Only && _inner.HasTimeMachine();
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
                return _mine ? (_tally != null ? _tally.Left : 0) : _inner.GetRemainingMinutesForTimeMachine();
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
                return _mine ? TimeHelper.Now().AddMinutes(GetRemainingMinutesForTimeMachine()) : _inner.GetTimeMachineGoal();
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, TimeHelper.Now());
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
                return _mine ? _state == PlayerActivityState.Running : !_offer.Only && _inner.HasProgressBar();
            }
            catch (Exception fault)
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
                return _mine ? (_tally != null ? 100f * _tally.Done / _tally.Length : 0f) : _inner.GetProgressBarPercentageValue();
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
                if (!_mine)
                {
                    return _inner.GetProgressBarLabel();
                }
                int left = _tally != null ? _tally.Left : 0;
                return ("playeractivityui_time_remaining", new { hours = left / 60, minutes = left % 60 });
            }
            catch (Exception fault)
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
                return !_mine && !_offer.Only && _inner.HasSlider();
            }
            catch (Exception fault)
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
                return _inner.GetMinSliderValue();
            }
            catch (Exception fault)
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
                return _inner.GetMaxSliderValue();
            }
            catch (Exception fault)
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
                return _inner.GetCurrentSliderValue();
            }
            catch (Exception fault)
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
                if (!_mine)
                {
                    _inner.OnSliderValueChanged(value);
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
                return (string.Empty, (object)null);
            }
            try
            {
                return _inner.GetSliderInfo();
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, (string.Empty, (object)null));
            }
        }
    }
}
