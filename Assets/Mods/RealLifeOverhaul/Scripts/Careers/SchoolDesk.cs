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
    /// that follows if the game's course is studied) and adds one button: "Attend {course}" during the class's window,
    /// "Sit the {course} exam" while it is due, or, greyed, when the next class is.
    ///
    /// Pressed, the class is this activity's own, done the way the game does its studying: the player walks to the desk,
    /// sits on its chair at its computer (the screen shows its video), types now and then, and spends energy as studying
    /// does; the game's panel shows the progress, the time left, fast forward and "skip time", and Stop. When it is over,
    /// whichever way, the minutes are counted to the mod's enrolment (CareersDirector.Ended, as a class from the Campus app
    /// is), never to a diploma of the game's: nothing of the game's courses or of the save's diplomas is touched, and the
    /// player stands up at the desk's exit as the game's studying leaves them.
    ///
    /// One object per press of Study, made only while the player is inside the school and on a course there (Careers
    /// director, one reference comparison a frame then). If the game's activity is not as expected, it is left alone.
    /// </summary>
    internal sealed class SchoolDesk : IPlayerActivity
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static FieldInfo _doorField, _placeField;
        private static MethodInfo _setActivity;
        private static bool _failed;

        private readonly StudyActivity _inner;
        private readonly EntityController _door;
        private readonly StudyDiploma _place;
        private readonly CareersDirector _director;
        private readonly DeskOffer _offer;
        private ButtonInfo[] _buttons;

        private bool _mine;
        private Sitting _sitting;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private int _length, _done;
        private float _nextType;
        private ButtonInfo[] _stop;

        /// <summary>What the desk offers: a course's class or exam, whether it can be done now, and the words of the button.</summary>
        internal sealed class DeskOffer
        {
            public int Course;
            public bool Exam;
            public bool Can;
            public string Key;
            public object Arguments;
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

        /// <summary>
        /// The game's panel has just opened its own study activity at a desk of the school: it is handed to a desk of ours
        /// with the offer. Returns it, or null if the game's activity is not as expected (then nothing is changed).
        /// </summary>
        public static SchoolDesk Take(PlayerActivityUI ui, StudyActivity game, CareersDirector director, DeskOffer offer)
        {
            if (_failed || ui == null || game == null || offer == null)
            {
                return null;
            }
            try
            {
                if (_doorField == null)
                {
                    _doorField = typeof(StudyActivity).GetField("_educationDoor", Hidden);
                    _placeField = typeof(StudyActivity).GetField("_studyDiploma", Hidden);
                    _setActivity = typeof(PlayerActivityUI).GetProperty("GetCurrentActivity", BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true);
                }
                var door = _doorField != null ? _doorField.GetValue(game) as EntityController : null;
                var place = _placeField != null ? _placeField.GetValue(game) as StudyDiploma : null;
                if (door == null || place == null || place.chair == null || place.exitPosition == null || _setActivity == null)
                {
                    _failed = true;
                    CareersDirector.Logger?.Warn("Careers: the game's study desks are not as this version expects; classes at the school are attended from the Campus app instead.");
                    return null;
                }
                var desk = new SchoolDesk(game, door, place, director, offer);
                _setActivity.Invoke(ui, new object[] { desk });
                ui.UpdateActivityDisplay();
                return desk;
            }
            catch (Exception e)
            {
                _failed = true;
                CareersDirector.Logger?.Error(e);
                return null;
            }
        }

        // ---------------------------------------------------------------- the mod's class

        private void Press()
        {
            if (_mine || _inner.GetState() != PlayerActivityState.NotStarted)
            {
                return;
            }
            string why = _director.DeskSitting(_offer.Course, _offer.Exam, out Sitting sitting);
            if (why != null)
            {
                Notifications.Show(NotificationType.Warning, why, null, 8f);
                return;
            }
            _mine = true;
            _sitting = sitting;
            _length = Math.Max(1, sitting.SkipMinutes);
            _done = 0;
            _state = PlayerActivityState.MovingTowardsActivity;
            _director.StartCoroutine(Walk());
        }

        // As the game's studying: a frame later, the walk to the desk; there, the chair, the screen and the energy.
        private IEnumerator Walk()
        {
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
            player.SetGoal(_door, () =>
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
                    _sitting.SkipStarted();
                }
                catch (Exception e)
                {
                    CareersDirector.Logger?.Error(e);
                }
            });
        }

        // Called off before the class began: the sitting goes with nothing counted.
        private void Off()
        {
            _before = _state;
            _state = PlayerActivityState.Finished;
            Sitting sitting = _sitting;
            _sitting = null;
            sitting?.SkipEnded(0, false);
        }

        // ---------------------------------------------------------------- IPlayerActivity: the game's, until the button is pressed

        public bool RequiresEnergy() => true;

        public bool BlocksAutoSave() => true;

        public PlayerActivityState GetState() => _mine ? _state : _inner.GetState();

        public PlayerActivityState GetStateBeforeFinishing() => _mine ? _before : _inner.GetStateBeforeFinishing();

        public void ChangeState(PlayerActivityState state)
        {
            if (_mine)
            {
                _state = state;
            }
            else
            {
                _inner.ChangeState(state);
            }
        }

        public void Perform(int minutes)
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
            _done += minutes;
            _sitting?.SkipWorked(Math.Min(_done, _length));
            if (_done >= _length)
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

        public void Finish()
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
            try
            {
                PlayerController player = InstanceBehavior<GameManager>.Instance.playerController;
                if (was == PlayerActivityState.MovingTowardsActivity)
                {
                    player.ResetNavigation();
                }
                else
                {
                    player.Character.Reset();
                    player.Character.navmeshAgent.Warp(_place.exitPosition.position);
                    player.UnsetNavigationBlocker(NavigationBlocker.StudyActivity);
                    PlayerHelper.GetAnimator().ResetTrigger(AnimationType.UsingDesktopComputer.ToStringFast());
                    _place.computer?.StopVideoOnScreen();
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
            if (was == PlayerActivityState.MovingTowardsActivity)
            {
                sitting?.SkipEnded(0, false);
            }
            else
            {
                sitting?.SkipEnded(Math.Min(_done, _length), _done >= _length);
            }
        }

        public LabelInfo GetHeadlineLabel()
        {
            return _mine && _sitting != null ? new LabelInfo(_sitting.SkipTitle, GameUi.WorldPanel.White(), false) : _inner.GetHeadlineLabel();
        }

        public LabelInfo[] GetLabels()
        {
            if (!_mine)
            {
                return _inner.GetLabels();
            }
            return _sitting != null && _state == PlayerActivityState.Running ? _sitting.SkipLabels() : null;
        }

        public ButtonInfo[] GetButtons()
        {
            if (_mine)
            {
                return _state == PlayerActivityState.Running
                    ? _stop ?? (_stop = new[] { new ButtonInfo("StopRealLifeClass", "reallife_job_skip_stop", "gray", Finish, PlayerAction.Cancel) })
                    : null;
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
                _buttons[n] = new ButtonInfo("RealLifeClass", _offer.Key, _offer.Arguments, "blue", Press, PlayerAction.SecondaryInteract, _offer.Can);
            }
            return _buttons;
        }

        public bool HasFastForward() => _mine ? _state == PlayerActivityState.Running : _inner.HasFastForward();

        public bool HasTimeMachine() => _mine ? _state == PlayerActivityState.Running : _inner.HasTimeMachine();

        public int GetRemainingMinutesForTimeMachine() => _mine ? Math.Max(0, _length - _done) : _inner.GetRemainingMinutesForTimeMachine();

        public Timestamp GetTimeMachineGoal() => _mine ? TimeHelper.Now().AddMinutes(GetRemainingMinutesForTimeMachine()) : _inner.GetTimeMachineGoal();

        public bool HasProgressBar() => _mine ? _state == PlayerActivityState.Running : _inner.HasProgressBar();

        public float GetProgressBarPercentageValue() => _mine ? 100f * _done / Math.Max(1, _length) : _inner.GetProgressBarPercentageValue();

        public (string key, object arguments) GetProgressBarLabel()
        {
            if (!_mine)
            {
                return _inner.GetProgressBarLabel();
            }
            int left = Math.Max(0, _length - _done);
            return ("playeractivityui_time_remaining", new { hours = left / 60, minutes = left % 60 });
        }

        public bool HasSlider() => !_mine && _inner.HasSlider();

        public int GetMinSliderValue() => _inner.GetMinSliderValue();

        public int GetMaxSliderValue() => _inner.GetMaxSliderValue();

        public float GetCurrentSliderValue() => _inner.GetCurrentSliderValue();

        public void OnSliderValueChanged(int value)
        {
            if (!_mine)
            {
                _inner.OnSliderValueChanged(value);
            }
        }

        public (string key, object arguments) GetSliderInfo() => _inner.GetSliderInfo();
    }
}
