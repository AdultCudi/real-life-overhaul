using System;
using System.Collections;
using BigAmbitions.DayNightCycle;
using PlayerActivity;
using RealLifeOverhaul.Careers;
using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul.Sessions
{
    /// <summary>
    /// A desk session on the game's own activity panel, as the game's work is (WorkActivity): first a page of the sessions
    /// held at this firm (what each is, whether it is due, who takes it), then a page a step with its choices and their
    /// trade-offs on the game's buttons, then the time it takes in the chair at the owner's desk with the game's own skip
    /// and fast forward; the result is the director's to apply, once. In a Going Public session, which hides this panel
    /// and presses the button named Start, the sound choices are taken and a notice says them. Nothing per frame of its
    /// own: the game's panel asks it as it draws.
    /// </summary>
    internal sealed class SessionPanel : IPlayerActivity
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("living", "SessionPanel", typeof(SessionPanel));   // the game's calls of it (Core/Guard)

        private readonly SessionDirector _director;
        private readonly EmployeeStationController _station;
        private readonly SessionDef[] _defs;
        private readonly int[] _choices;
        private int _at;                 // the session picked on the first page
        private int _step = -1;          // -1 the first page; then the step being chosen
        private LabelInfo _title;
        private LabelInfo[] _labels, _running;
        private ButtonInfo[] _buttons, _stop;
        private PlayerActivityState _state = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        private int _length, _done;
        private bool _pressed, _seated;
        private Vector3 _stood;
        private float _nextType;

        public SessionPanel(SessionDirector director, EmployeeStationController station, SessionDef[] defs, int first)
        {
            _director = director;
            _station = station;
            _defs = defs;
            int steps = 1;
            for (int i = 0; i < defs.Length; i++)
            {
                steps = Math.Max(steps, defs[i].Steps.Length);
            }
            _choices = new int[steps];
            _at = Math.Max(0, Math.Min(defs.Length - 1, first));
            Page();
        }

        /// <summary>True from the click until it is over, whichever way.</summary>
        public bool Open => _state != PlayerActivityState.Finished;

        /// <summary>The desk it is at.</summary>
        public EmployeeStationController Station => _station;

        private SessionDef Def => _defs[_at];

        // Every change of the state goes through here: once it is no longer the pages' (the walk, the time in the chair, over) the kit's card
        // goes with them. Without it the card stood on, with its title and nothing under it, over a panel that had no page to show.
        private void Move(PlayerActivityState to)
        {
            PlayerActivityState from = _state;
            _state = to;
            if (to != PlayerActivityState.NotStarted && Ui.SessionKit.Up)
            {
                SessionDirector.Note("the card is put away: the panel went from " + from + " to " + to);
                Ui.SessionKit.Hide(this);
            }
        }

        /// <summary>True while the page being shown has words and a way out: what the card and the game's panel both draw. Every page has Cancel.</summary>
        public bool HasPage => _state == PlayerActivityState.NotStarted && _labels != null && _labels.Length > 0 && _buttons != null && _buttons.Length > 0;

        // The labels and buttons of the page being shown, made again only on a press.
        private void Page()
        {
            SessionDef def = Def;
            _title = DeskGame.Label(SessionText.T(def.TitleKey), 0);
            if (_step < 0)
            {
                string status = _director.Status(def, out int colour, out bool can, out int hand);
                _labels = _defs.Length > 1
                    ? new[]
                    {
                        DeskGame.Label(SessionText.T(def.AboutKey), 0), DeskGame.Label(status, colour),
                        DeskGame.Label(SessionText.Fill("reallife_session_label_of", "at", SessionText.Whole(_at + 1), "count", SessionText.Whole(_defs.Length)), 0)
                    }
                    : new[] { DeskGame.Label(SessionText.T(def.AboutKey), 0), DeskGame.Label(status, colour) };
                bool work = SessionBridges.WorkHereNow(_station);   // the owner's own work at this desk, when it is the firm's only free one (Tech's own desk)
                SessionRules.PageButtons pb = SessionRules.Buttons(can, _defs.Length, hand, work);
                int n = 1 + (pb.Begin ? 1 : 0) + (pb.Next ? 1 : 0) + (pb.Hand ? 1 : 0) + (pb.Work ? 1 : 0), k = 0;
                _buttons = new ButtonInfo[n];
                _buttons[k++] = new ButtonInfo("CancelRealLifeSession", "common_cancel", "gray", Guard.Way(Cancel), PlayerAction.Cancel);
                SessionDirector.Note("page " + (_at + 1) + " of " + _defs.Length + ": " + def.Id + ", "
                                     + (can ? "can be held now" : "cannot be held: " + status) + (pb.Next ? ", skip offered" : string.Empty));
                if (pb.Begin)
                {
                    // named Start: in a Going Public session the panel is hidden and only a button so named is pressed (Careers/Study/ClassPlayRules.SessionPick)
                    _buttons[k++] = new ButtonInfo("StartRealLifeSession", "reallife_session_button_begin", "blue", Guard.Wrap(Begin), PlayerAction.Confirm);
                }
                if (pb.Next)
                {
                    _buttons[k++] = new ButtonInfo("NextRealLifeSession", "reallife_session_button_next", "gray", Guard.Wrap(Next), PlayerAction.SecondaryInteract);
                }
                if (pb.Hand)
                {
                    _buttons[k++] = new ButtonInfo("HandRealLifeSession", hand > 0 ? "reallife_session_button_hand" : "reallife_session_button_take", "gray", Guard.Wrap(Hand), PlayerAction.Interact);
                }
                if (pb.Work)
                {
                    _buttons[k] = new ButtonInfo("WorkRealLifeSessionDesk", "reallife_session_button_work", "gray", Guard.Wrap(Work), PlayerAction.SpecialInteract);
                }
                return;
            }
            SessionStep step = def.Steps[_step];
            _labels = new LabelInfo[1 + step.Choices.Length];
            _labels[0] = DeskGame.Label(SessionText.T(step.TitleKey), 1);
            _buttons = new ButtonInfo[1 + step.Choices.Length];
            _buttons[0] = new ButtonInfo("CancelRealLifeSession", "common_cancel", "gray", Guard.Way(Cancel), PlayerAction.Cancel);
            for (int i = 0; i < step.Choices.Length; i++)
            {
                SessionChoice c = step.Choices[i];
                _labels[1 + i] = DeskGame.Label(SessionText.Fill("reallife_session_label_choice", "choice", SessionText.T(c.NameKey), "trade", SessionText.T(c.TradeKey)), 0);
                int pick = i;
                PlayerAction key = i == 0 ? PlayerAction.Confirm : i == 1 ? PlayerAction.SecondaryInteract : PlayerAction.Interact;
                _buttons[1 + i] = new ButtonInfo("StartRealLifeSessionChoice" + i, c.NameKey, i == 0 ? "blue" : "gray", Guard.Wrap(() => Choose(pick)), key);
            }
        }

        // A page changed while the panel is up: built again, and drawn again by the game's panel, which reads a page only when it opens.
        private void Repage()
        {
            Page();
            SessionGame.Redraw(this);
        }

        private void Next()
        {
            if (_state != PlayerActivityState.NotStarted || _pressed || _defs.Length < 2)
            {
                return;
            }
            int from = _at;
            _at = SessionRules.NextPage(_at, _defs.Length);
            SessionDirector.Note("skip: from session " + (from + 1) + " to " + (_at + 1) + " of " + _defs.Length);
            Repage();
        }

        private void Hand()
        {
            if (_state != PlayerActivityState.NotStarted || _pressed)
            {
                return;
            }
            _director.Hand(Def);
            Repage();
        }

        // The owner's own work at this desk instead: nothing of a session begun, the panel let go, and the work offered once it has gone.
        private void Work()
        {
            if (_state != PlayerActivityState.NotStarted || _pressed || _step >= 0)
            {
                return;
            }
            SessionDirector.Note("work here pressed on page " + (_at + 1) + " of " + _defs.Length + ": the session panel is let go, the card "
                                 + (Ui.SessionKit.Up ? "is put away" : "was not up") + ", then the work is offered");
            End(false, false);
            _director.WorkAfter(_station);
        }

        private void Begin()
        {
            if (_state != PlayerActivityState.NotStarted || _pressed || _step >= 0)
            {
                return;
            }
            if (Def.Steps.Length == 0 || GoingPublic.InSession)
            {
                _director.Sound(Def, _choices);   // the panel is hidden in a session: the sound choices, said in a notice when it begins
                Start();
                return;
            }
            _step = 0;
            Repage();
        }

        private void Choose(int pick)
        {
            if (_state != PlayerActivityState.NotStarted || _pressed || _step < 0 || _step >= Def.Steps.Length)
            {
                return;
            }
            _choices[_step] = pick;
            if (_step + 1 < Def.Steps.Length)
            {
                _step++;
                Repage();
                return;
            }
            Start();
        }

        // The choices made: the director checks it can still be held, then the walk to the chair and the time.
        private void Start()
        {
            string why = _director.Begins(Def, _choices, out int minutes);
            if (why != null)
            {
                SessionGame.Tell(UI.Notification.NotificationType.Warning, why, null, 8f);
                SessionDirector.Note("refused to begin " + Def.Id + ": " + why);
                _step = -1;
                Repage();
                return;
            }
            if (SaveGameManager.Current == null || SaveGameManager.Current.Energy <= 0f)
            {
                UI.Notification.Notifications.ShowInsufficientEnergy();
                return;
            }
            _pressed = true;
            _length = Math.Max(1, minutes);
            _done = 0;
            _running = _director.Summary(Def, _choices);
            SessionDirector.Note("begin " + Def.Id + " with the choices " + string.Join(",", new ArraySegment<int>(_choices, 0, Def.Steps.Length)) + ": " + _length + " game minutes at the desk");
            _director.StartCoroutine(Guard.Routine(Walk()));
        }

        private const float WalkSeconds = 40f;   // real seconds the walk to the chair may take before it is called off: the game's goal call says nothing when the way is gone

        // Set off as the game's activities are: a frame for the panel to see it, a frame more, then the walk to the chair.
        private IEnumerator Walk()
        {
            yield return null;
            if (_state != PlayerActivityState.NotStarted)
            {
                yield break;
            }
            if (!DeskGame.Reaches(_station))
            {
                // the game would say it cannot reach the desk and never call back: the page comes back, nothing spent
                SessionGame.Tell(UI.Notification.NotificationType.Warning, "reallife_session_note_no_way", null, 10f);
                SessionDirector.Note("the desk cannot be walked to from here: back to the page");
                _pressed = false;
                _step = -1;
                Repage();
                yield break;
            }
            Move(PlayerActivityState.MovingTowardsActivity);
            yield return null;
            if (_state != PlayerActivityState.MovingTowardsActivity)
            {
                yield break;
            }
            if (!DeskGame.WalkTo(_station, Arrived))
            {
                SessionDirector.Note("the walk to the desk could not be set off");
                End(false, true);
                yield break;
            }
            float until = Time.unscaledTime + WalkSeconds;
            while (_state == PlayerActivityState.MovingTowardsActivity && Time.unscaledTime < until)
            {
                yield return null;
            }
            if (_state == PlayerActivityState.MovingTowardsActivity)
            {
                SessionGame.Tell(UI.Notification.NotificationType.Warning, "reallife_session_note_no_way", null, 10f);
                SessionDirector.Note("the walk to the desk took over " + WalkSeconds + " s and is called off: nothing held, the session stays due");
                End(false, true);
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
                Transform seat = DeskGame.SeatOf(_station);
                if (seat != null)
                {
                    _seated = true;   // how End stands them up, once begun
                    if (!DeskGame.SitOn(seat, _station, out _stood))
                    {
                        End(false, false);   // never held: DeskGame gave the player back free to walk and said why, and nothing of the session is spent
                        return;
                    }
                    _nextType = Time.time + UnityEngine.Random.Range(0f, 6f);
                }
                Move(PlayerActivityState.Started);   // only once seated: every way out from here stands the player up (End); the game's panel makes it Running on its next frame
                _director.Sat(Def);
            }
            catch (Exception e)
            {
                SessionDirector.Logger?.Error(e);
                if (_state == PlayerActivityState.MovingTowardsActivity)
                {
                    End(false, false);   // a fault before the seat: nothing held, nothing left on its way
                }
            }
        }

        private void Cancel()
        {
            if (_state == PlayerActivityState.NotStarted)
            {
                End(false, false);
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
                    End(true, true);   // however the panel ends it: the minutes sat settled once, and the player stood up
                    return;
                }
                if (_state == PlayerActivityState.MovingTowardsActivity && state == PlayerActivityState.NotStarted)
                {
                    End(false, false);   // the game's panel called the walk off for a click elsewhere and let it go
                    return;
                }
                if (_state != PlayerActivityState.Finished)
                {
                    Move(state);
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

        /// <summary>Over: by the end of the time, Stop, the game calling it off. Held whole only if the time was sat.</summary>
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

        /// <summary>The sessions are stopping for a reason of their own (switched off, the desk gone, a load): over, nothing held.</summary>
        public void Close()
        {
            try
            {
                Ui.SessionKit.Hide(this);
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                if (_state == PlayerActivityState.Running && uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
                {
                    uis.timeMachine.CancelTimeMachine();   // the game's panel then finishes the activity itself
                }
            }
            catch (Exception e)
            {
                SessionDirector.Logger?.Error(e);
            }
            End(false, true);
        }

        // Every way out. settle: the director is told how it ended; walking: the walk is called off here.
        private void End(bool settle, bool walking)
        {
            if (_state == PlayerActivityState.Finished)
            {
                return;
            }
            PlayerActivityState was = _state;
            _before = was;
            Move(PlayerActivityState.Finished);   // first: whatever the game's calls below do, nothing here runs twice
            bool begun = was == PlayerActivityState.Started || was == PlayerActivityState.Running;
            try
            {
                if (was == PlayerActivityState.MovingTowardsActivity && walking)
                {
                    DeskGame.StopWalking();
                }
                else if (begun && _seated)
                {
                    DeskGame.StandUp(_stood, _station);
                }
            }
            catch (Exception e)
            {
                SessionDirector.Logger?.Error(e);
            }
            if (!settle || !begun)
            {
                return;   // never sat: nothing was held and nothing is said
            }
            if (_done >= _length)
            {
                _director.Held(Def, _choices);
            }
            else
            {
                _director.Stopped(Def, _done, _length);
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
                return _title;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, Guard.Blank());
            }
        }

        public LabelInfo[] GetLabels()
        {
            return _state == PlayerActivityState.NotStarted ? _labels : _state == PlayerActivityState.Running ? _running : null;
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
                        return _pressed ? null : _buttons;
                    case PlayerActivityState.Running:
                        return _stop ?? (_stop = new[] { new ButtonInfo("StopRealLifeSession", "workui_stop", "gray", Guard.Way(Finish), PlayerAction.Cancel) });
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
