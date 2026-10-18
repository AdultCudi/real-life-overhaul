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
    /// A class or an exam of the mod's at Manhattan Business School, through the game's own study flow: the player goes in,
    /// walks to one of the school's study desks and presses Study, as for one of the game's seven courses. The game opens
    /// its own panel with its own study activity for that desk (PlayerActivity.StudyActivity, made by the desk's
    /// EducationDoorController). While the player is on a course of the mod's at the school, that activity is handed to
    /// this one, which leaves every part of it to the game's activity (its course, price, slider, Start and Cancel, and all
    /// that follows if the game's course is studied) and adds one button, on the game's secondary key beside its Start:
    /// "Attend {course}" during the class's window, "Sit the {course} exam" while it is due, or, greyed, when the next
    /// class is.
    ///
    /// Pressed, the class is this activity's own, done the way the game does its studying: the player walks to the desk,
    /// sits on its chair at its computer (the screen shows its video), types now and then, and spends energy as studying
    /// does; the game's panel shows the progress, the time left, fast forward and "skip time", and Stop. It sets off as
    /// the game's own activities do (<see cref="Walk"/>), so that the panel never takes its walk for a click elsewhere.
    /// When it is over, whichever way, the minutes are counted to the mod's enrolment (CareersDirector.Ended, as a class
    /// from the Campus app is), never to a diploma of the game's: nothing of the game's courses or of the save's diplomas
    /// is touched, and the player stands up at the desk's exit as the game's studying leaves them.
    ///
    /// One object per press of Study, made only while the player is inside the school and on a course there (Careers
    /// director, one reference comparison a frame then). If the game's activity is not as expected, it is left alone.
    ///
    /// At Hudson City University (Careers/CampusInterior) the classrooms are the school's own, with one of the university's
    /// courses in each: there the panel is the class's alone (<see cref="DeskOffer.Only"/>): the room's course as its
    /// headline, where it is and when its class is, Cancel and the class's button on the game's Start key, and none of
    /// the game's courses.
    ///
    /// From the Campus app (Attend, Sit, Homework while at the school or the university) the director opens the desk's
    /// panel itself, as a click on the door does, and presses the button for the player (<see cref="Begin"/>): one press,
    /// and the player walks to the desk and sits. A walk called off (a click elsewhere, no way there) counts nothing and
    /// says nothing; once sat, the minutes are counted once whichever way it ends (<see cref="ClassTally"/>).
    ///
    /// In a Going Public session the game's study panel is never asked for (ClassPlayRules.WayToDesk): Going Public takes
    /// every StudyActivity the panel is asked to show and puts up its own training window for the game's course instead.
    /// There the desk is made with <see cref="Own"/>, from the door's own study place, and the game's panel is asked to
    /// show this desk, which is not a StudyActivity: the class's panel alone, as at a classroom of the university.
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

        private bool _mine;
        private Sitting _sitting;
        private ClassTally _tally;
        private LabelInfo[] _running;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private float _nextType;
        private ButtonInfo[] _stop;
        private int _orderFrame = -1;   // the frame the walk order to the desk was given in (DeskGame.SetOffSeen)

        /// <summary>What the desk offers: a course's class or exam, whether it can be done now, and the words of the button.</summary>
        internal sealed class DeskOffer
        {
            public int Course;
            public SittingKind Kind;   // a class, a piece of homework or an exam
            public bool Can;
            public string Key;
            public object Arguments;
            public bool Only;          // the class's panel alone (a classroom of the university): no course of the game's
            public string Title;       // ...its headline
            public string[] Lines;     // ...and its lines
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

        // The game's members, all three looked up together the first time any is wanted. (Once the door's alone was looked
        // up first, when a classroom of the university was opened; the other two were then never looked up, the desk was
        // taken for broken, and every classroom door after it gave the game's own course, "Office Businesses" at room 4.)
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
            _doorField = GameContract.StudyDoor.Field;   // (the game's private pieces, resolved once as the mod starts: Core/GameContract)
            _placeField = GameContract.StudyPlace.Field;
            _setActivity = GameContract.CurrentActivity.Setter;
            if (_doorField == null || _placeField == null || _setActivity == null)
            {
                _failed = true;
                CareersDirector.Logger?.Warn("Careers: the game's study activity is not as this version expects (" + (_doorField == null ? "its door" : _placeField == null ? "its desk" : "the panel's activity")
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

        /// <summary>
        /// The game's panel has just opened its own study activity at a desk of the school: it is handed to a desk of ours
        /// with the offer. Returns it, or null if the game's activity is not as expected (then nothing is changed). A desk
        /// without its chair or its way out (a layout the game changed) is refused alone; the others still work.
        /// </summary>
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

        /// <summary>
        /// A desk of ours made from a study door without the game's panel (a Going Public session): the door's study
        /// activity made as the game's door makes it (StudyDiploma.CreateActivity) but never shown, so the game's panel can
        /// be asked to show this desk, the class's panel alone (<see cref="DeskOffer.Only"/>). Null if the desk lacks its
        /// chair or its way out, or the game's activity is not a study activity (nothing is then changed).
        /// </summary>
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
                place.computer = seat != null ? seat.parentItemController : null;   // (as the game's door sets it before its panel: the screen's video)
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

        // ---------------------------------------------------------------- the mod's class

        /// <summary>
        /// The class's button pressed for the player: the Campus app's Attend (Sit, Homework) sent them to this desk, so
        /// the one press goes the whole way, as a press of the button would, a frame after the game's panel has seen the
        /// desk not started. The same checks as the button (busy, tired, the class's window) say why not, if not.
        /// </summary>
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
            string why = _director.DeskSitting(_offer.Course, _offer.Kind, out Sitting sitting);
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

        // Set off as the game's own activities are (docs/features/careers.md, "Setting off"): on its way only once every
        // script of the press's frame has run, and the walk order in the next frame, the first in which the game's panel
        // sees it on its way; a walk order after that frame's end is one the panel takes for a click elsewhere. Then, at
        // the desk, the chair, the screen and the energy, as the game's studying.
        private IEnumerator Walk()
        {
            yield return null;
            if (_state != PlayerActivityState.NotStarted)
            {
                yield break;   // (called off meanwhile)
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
                    _tally.Begin();
                    _sitting.SkipStarted();
                    DeskGame.SetOffSeen("the class's desk", true, _orderFrame);
                }
                catch (Exception e)
                {
                    CareersDirector.Logger?.Error(e);
                }
            }));
        }

        // Called off before the class began (no way to the desk, or the walk stopped by a click elsewhere): the sitting goes
        // with nothing counted and nothing said.
        private void Off()
        {
            _before = _state;
            _state = PlayerActivityState.Finished;
            Sitting sitting = _sitting;
            _sitting = null;
            _tally?.Settle(out _, out _);
            sitting?.CalledOff();
        }

        // ---------------------------------------------------------------- IPlayerActivity: the game's, until the button is pressed

        public bool RequiresEnergy() => true;

        public bool BlocksAutoSave() => true;

        public PlayerActivityState GetState()
        {
            if (FrameGuard.Off)
            {
                return Guard.Ended(this);   // (its feature is off: the activity ends as its own Finish ends it, and the panel closes)
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
                bool over = _tally.Add(minutes);
                _sitting?.SkipWorked(_tally.Done);
                if (over)
                {
                    Finish();
                }
                else if (Time.time >= _nextType)
                {
                    try
                    {
                        PlayerHelper.GetAnimator().SetTrigger(AnimationType.UsingDesktopComputer.ToStringFast());
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
                _state = PlayerActivityState.Finished;   // (first: whatever the game's calls below do, nothing here runs twice)
                StandUp();
                Sitting sitting = _sitting;
                _sitting = null;
                if (_tally.Settle(out int minutes, out bool whole) == TallyEnd.Counted)
                {
                    sitting?.SkipEnded(minutes, whole);   // (once: a second end finds the tally settled)
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        // Up at the desk's exit, as the game's studying stands its player up. The study blocker goes whatever the game's
        // calls before it do, so that nothing can leave the player held at the desk.
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

        // The class's panel alone, closed: before its button, with nothing of the game's; after, the class is called off,
        // or ended with its minutes counted.
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
                    return _inner.GetLabels();
                }
                if (_sitting == null || _state != PlayerActivityState.Running)
                {
                    return null;
                }
                return _running ?? (_running = _sitting.SkipLabels());   // (made once)
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
                        ? _stop ?? (_stop = new[] { new ButtonInfo("StopRealLifeClass", "reallife_job_skip_stop", "gray", Guard.Way(Finish), PlayerAction.Cancel) })
                        : null;
                }
                if (_offer.Only)
                {
                    // (on the game's Start key: no course of the game's is offered here, and its secondary key is the panel's fast forward)
                    return _buttons ?? (_buttons = new[]
                    {
                        new ButtonInfo("CancelRealLifeClass", "common_cancel", "gray", Guard.Way(Close), PlayerAction.Cancel),
                        new ButtonInfo("RealLifeClass", _offer.Key, _offer.Arguments, "blue", Guard.Wrap(Press), PlayerAction.Confirm, _offer.Can)
                    });
                }
                ButtonInfo[] game = _inner.GetButtons();
                if (_inner.GetState() != PlayerActivityState.NotStarted)
                {
                    return game;
                }
                if (_buttons == null)
                {
                    int n = game != null ? game.Length : 0;
                    _buttons = new ButtonInfo[n + 1];
                    for (int i = 0; i < n; i++)
                    {
                        _buttons[i] = game[i];
                    }
                    _buttons[n] = new ButtonInfo("RealLifeClass", _offer.Key, _offer.Arguments, "blue", Guard.Wrap(Press), PlayerAction.SecondaryInteract, _offer.Can);
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
