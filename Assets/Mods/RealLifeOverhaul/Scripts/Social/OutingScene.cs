using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A plan being kept: they meet at the place agreed, greet, talk, do the thing and part. One class for the eight outings
    /// and the two favours, which differ only in the middle: a business (the time machine passes the time inside), a walk,
    /// a drive or a lift, a move. Everybody it puts out is given back in <see cref="End"/>.
    /// </summary>
    public sealed partial class OutingScene : SocialScene
    {
        private enum Step { Travel, Greeting, Talk, ToDoor, Inside, Walking, Boarding, Riding, PullOver, Boxes, Wrap, Parting, Bench }

        private readonly SocialPerson _who;
        private readonly bool _test;
        private readonly float _time;           // game minutes agreed
        private readonly float _arrives;        // game minutes they get there
        private readonly bool _theirIdea;
        private readonly Venue _venue;
        private readonly Venue _to;
        private PlanKind _kind;
        private OutingDef _def;
        private CityPerson _friend;
        private Step _step;
        private float _stepFrom;
        private float _stepEnd;
        private float _lastTick;
        private float _nextTry;
        private Vector3 _spot;                  // where they wait
        private string _first;                  // what the marker on them says
        private bool _greeted;
        private bool _settled;
        private bool _closed;                   // the place was shut: a walk instead
        private bool _covered;                  // they paid because the player could not
        private bool _onFootSaid;
        private int _paid;
        private float _lateBy;                  // game minutes
        private bool _heldWarned;
        private string _heldLine;
        private SceneBreak _cut;

        private bool _skipping;
        private float _skipFrom;
        private int _backTries;
        private bool _cups;
        private float _sipAt = -1f;
        private float _cupBackAt = -1f;
        private float _outUntil;
        private float _turnAt;

        private float _metres;
        private int _shownTens = -1;
        private int _beats;
        private Vector3 _lastPlayer;
        private Vector3 _followGoal;
        private bool _stood;
        private float _apartSince = -1f;
        private bool _apartSaid;
        private float _talkUntil = -1f;         // walk_talk is on their upper body until then

        private CarController _car;
        private Vector3 _lastCar;
        private Vector3 _lastVelocity;
        private float _enterUntil = -1f;
        private float _stoppedSince = -1f;
        private float _awaySince = -1f;
        private int _jolts;
        private int _crashes;

        public OutingScene(SocialPerson who, PlanKind kind, in Venue venue, float time, float arrives, bool theirIdea, in Venue to, bool test)
        {
            _who = who;
            _kind = kind;
            _def = SocialCatalogue.Outing(kind);
            _venue = venue;
            _to = to;
            _time = time;
            _arrives = arrives;
            _theirIdea = theirIdea;
            _test = test;
        }

        public SocialPerson Who => _who;
        public PlanKind Kind => _kind;
        public bool Greeted => _greeted;
        public override bool SkippingTime => _skipping;
        public override bool Waiting => _step == Step.Travel && _friend == null;

        private bool Rides => _kind == PlanKind.Drive || _kind == PlanKind.Lift;

        /// <summary>True while the other person is met, out, drawn and within <paramref name="within"/> metres of <paramref name="where"/>.</summary>
        public bool Beside(Vector3 where, float within)
        {
            return _greeted && _friend != null && _friend.Valid && _friend.Character.visible && _friend.Character.gameObject.activeInHierarchy
                   && (_friend.Position - where).sqrMagnitude <= within * within;
        }

        protected override bool Begin(float now)
        {
            if (!_venue.Valid)
            {
                Refusal = TestRefusal.NoVenue;
                return false;
            }
            _spot = _venue.Front;
            _first = SocialLife.First(_who);
            Dictionary<string, string> data = SocialLife.Data(_who, "outing", SocialLife.T(_def.Name));
            data["place"] = _venue.Name;
            Title = SocialLife.Capital(SocialLife.T("reallife_social_title", data));
            Line = SocialLife.T(Rides ? "reallife_social_obj_meet_car" : "reallife_social_obj_meet", data);
            Status = SocialLife.Clock(_time);
            Mark(_spot, _venue.Name);
            _lastPlayer = D.PlayerPosition;
            _pickPrompt = SocialLife.Say("reallife_social_moving_pick", null);
            _dropPrompt = SocialLife.Say("reallife_social_moving_drop", null);
            Go(Step.Travel, now, 0f);
            return true;
        }

        private void Go(Step step, float now, float seconds)
        {
            if (_talkUntil > 0f)
            {
                _talkUntil = -1f;
                if (_friend != null && !_friend.Lost)
                {
                    _friend.StopOver(0.3f);
                }
            }
            _step = step;
            _stepFrom = now;
            _stepEnd = now + seconds;
        }

        public override void Tick(float now)
        {
            float dt = Mathf.Max(0.05f, now - _lastTick);
            _lastTick = now;
            if (_friend != null && _friend.Lost && _step != Step.Parting)
            {
                // Knocked down: the outing is over and nobody is blamed.
                _friend = null;
                Say(_who, "reallife_social_plan_lost", null, NotificationType.Warning);
                ClearPlan();
                Over = true;
                return;
            }
            switch (_step)
            {
                case Step.Travel:
                    Travel(now);
                    break;
                case Step.Greeting:
                    Greeting(now);
                    break;
                case Step.Talk:
                    if (TickTalk(now, _friend))
                    {
                        StartActivity(now);
                    }
                    break;
                case Step.ToDoor:
                    if ((now - _stepFrom > 0.8f && _friend.Arrived) || now >= _stepEnd)
                    {
                        GoInside(now);
                    }
                    break;
                case Step.Inside:
                    Inside(now);
                    break;
                case Step.Walking:
                    Walking(now);
                    break;
                case Step.Bench:
                    OnBench(now);
                    break;
                case Step.Boarding:
                    Boarding(now);
                    break;
                case Step.Riding:
                    Riding(now, dt);
                    break;
                case Step.PullOver:
                    PullOver(now);
                    break;
                case Step.Boxes:
                    Boxes(now);
                    break;
                case Step.Wrap:
                    Wrapping(now);
                    break;
                case Step.Parting:
                    if (!TickLeaving(now))
                    {
                        Over = true;
                    }
                    break;
            }
            _lastVelocity = D.CarVelocity;
        }

        public override void Interact(float now)
        {
            if (Prompt == null)
            {
                return;
            }
            if (_step == Step.Travel && _friend != null && !_friend.Lost && !Rides)
            {
                Greet(now);
            }
            else if (_step == Step.Boxes)
            {
                BoxKey(now);
            }
            else if (_step == Step.Walking && _bench != null)
            {
                SitOnBench(now);
            }
            else if (_step == Step.Bench)
            {
                LeaveBench("the player got up");
                BackToWalk(now);
            }
        }

        public override bool Hold(SceneBreak why, float now)
        {
            if (Over || _step == Step.Parting || _settled)
            {
                return false;   // only the goodbye was left
            }
            if (_greeted && why == SceneBreak.TimeSkip)
            {
                return false;   // nobody stands on the pavement while hours go by
            }
            Prompt = null;
            _heldWarned = false;
            _toldThere = false;
            if (!_greeted)
            {
                return true;
            }
            PutBack();
            if (_friend != null && !_friend.Lost && !D.Cast.Seated(_friend))
            {
                _friend.Stand();
            }
            _heldLine = Line;
            Line = SocialLife.T("reallife_social_obj_held", SocialLife.Data(_who));
            return true;
        }

        public override bool Holding(SceneBreak why, float now, float held)
        {
            if (!_greeted)
            {
                float nowMinutes = SocialLife.Now;
                if (PlanGone(now, nowMinutes))
                {
                    return false;   // called off, or stood up: they have said so
                }
                Nudge(nowMinutes);
                if (why == SceneBreak.Indoors)
                {
                    WhileAway(now, nowMinutes);
                }
                return true;
            }
            if (_friend == null || _friend.Lost)
            {
                return true;
            }
            if (!_heldWarned && held > HoldNudge)
            {
                _heldWarned = true;
                SocialLife.SendText(_who, "reallife_social_text_outside", SocialLife.Data(_who), true);
            }
            if (held > HoldMost)
            {
                Say(_who, SocialLines.Pick(SocialCatalogue.LeftBehind), null, NotificationType.Warning);
                if (!_test)
                {
                    SocialLife.Change(_who, 0f, -4f, -3f);
                }
                _settled = true;
                SocialDirector.Logger?.Info("Social: " + _who.Name + " gave up waiting outside after " + held.ToString("F0") + " s (" + why + ").");
                return false;
            }
            return true;
        }

        public override void Resume(SceneBreak why, float now, float held)
        {
            _lastTick = now;
            _lastPlayer = D.PlayerPosition;
            _nextTry = now + 1f;
            if (!_greeted)
            {
                return;   // Travel picks it up
            }
            Line = _heldLine;
            _heldLine = null;
            // Nothing ran out while the player was away.
            _stepFrom += held;
            _stepEnd += held;
            _turnAt += held;
            _outUntil += held;
            if (_sipAt > 0f)
            {
                _sipAt += held;
            }
            if (_cupBackAt > 0f)
            {
                _cupBackAt += held;
            }
            if (_enterUntil >= 0f)
            {
                _enterUntil += held;
            }
            if (_awaySince >= 0f)
            {
                _awaySince += held;
            }
            _apartSince = -1f;
            _apartSaid = false;
            _stoppedSince = -1f;
            _stood = false;
            _followGoal = default;
            if (_car != null)
            {
                _lastCar = _car.transform.position;
            }
        }

        public override void Cut(SceneBreak why)
        {
            _cut = why;
            if (_step == Step.Bench)
            {
                LeaveBench("the outing was cut");
                BackToWalk(Time.time);
            }
            string reason = WhyText(why);
            if (reason == null || Over || _settled || _step == Step.Parting || _greeted)
            {
                return;   // after hello the ending says it, with this reason (End)
            }
            Dictionary<string, string> data = SocialLife.Data(_who, "why", reason);
            data["outing"] = SocialLife.T(_def.Name);
            data["place"] = _venue.Name;
            data["time"] = SocialLife.Clock(_time);
            bool kept = !_test && SocialState.Plan.Active && why != SceneBreak.OutingsOff && why != SceneBreak.SocialOff;
            SocialLife.Toast(kept ? NotificationType.Info : NotificationType.Warning,
                _test ? "reallife_social_break_test" : kept ? "reallife_social_break_kept" : "reallife_social_break_dropped", data, 10f);
        }

        public override string Describe()
        {
            return _def.Kind + " with " + _who.Name + " at " + _venue.Name + ", step " + _step + (_greeted ? ", after hello" : ", before hello")
                   + (_friend == null ? ", nobody out" : _walkingIn ? ", they were walking in" : D.Cast.Seated(_friend) ? ", they were in the car or inside" : ", they were out")
                   + (_test ? ", a test" : string.Empty);
        }

        private void StartActivity(float now)
        {
            if (_kind == PlanKind.Walk)
            {
                StartWalk(now);
                return;
            }
            if (_kind == PlanKind.Moving)
            {
                StartBoxes(now);
                return;
            }
            if (_kind != PlanKind.Business && !SocialPlaces.Open(_venue))
            {
                Say(_who, SocialLines.Pick(SocialCatalogue.Closed), SocialLife.Data(_who, "place", _venue.Name));
                _closed = true;
                _kind = PlanKind.Walk;
                _def = SocialCatalogue.Outing(PlanKind.Walk);
                StartWalk(now);
                return;
            }
            // Who pays was settled in the conversation; a bill the player cannot meet is quietly met by the other.
            int pay = Talk != null ? Talk.Pay : SceneTalk.Split;
            int amount = pay == SceneTalk.Treat ? _def.Cost * 2 : pay == SceneTalk.Theirs ? 0 : _def.Cost;
            if (_def.Cost > 0 && pay == 0)
            {
                amount = _def.Cost;   // the panel was closed without an answer: halves
            }
            if (amount > 0 && !SocialLife.Pay(amount, _def.Transaction))
            {
                if (pay == SceneTalk.Treat && SocialLife.Pay(_def.Cost, _def.Transaction))
                {
                    amount = _def.Cost;
                    pay = SceneTalk.Split;
                }
                else
                {
                    amount = 0;
                    _covered = true;
                    Say(_who, "reallife_social_pay_covered");
                }
            }
            _paid = amount;
            if (Talk != null)
            {
                Talk.Pay = pay;
            }
            Line = SocialLife.T("reallife_social_obj_going_in", SocialLife.Data(_who, "place", _venue.Name));
            if (CitySpots.Pavement(_venue.Door, 3f, out Vector3 door) && _friend.WalkTo(door))
            {
                Go(Step.ToDoor, now, Mathf.Clamp(JobSpots.Flat(_friend.Position, door) / 1.2f + 3f, 7f, 30f));
            }
            else
            {
                GoInside(now);
            }
        }

        private void GoInside(float now)
        {
            D.Cast.Seat(_friend);
            Line = null;
            var uis = InstanceBehavior<UI.UIs>.Instance;
            // In a Going Public session the clock is everyone's: no time passes.
            if (_def.Minutes > 0 && !GoingPublic.ClockShared && uis != null && uis.timeMachine != null && !uis.timeMachine.isRunning)
            {
                D.Body.Hold();
                _skipping = true;
                _skipFrom = now;
                BeginRestEffects();
                uis.timeMachine.StartTimeMachine(new Timestamp(SocialLife.Now + _def.Minutes), true, _def.Caption, true, false);
                SocialDirector.Logger?.Info("Social: " + _def.Kind + " with " + _who.Name + " at " + _venue.Name + ": " + _def.Minutes + " game minutes pass.");
            }
            _backTries = 0;
            Go(Step.Inside, now, 0f);
        }

        private void Inside(float now)
        {
            var uis = InstanceBehavior<UI.UIs>.Instance;
            if (_skipping)
            {
                if (uis == null || uis.timeMachine == null || !uis.timeMachine.isRunning)
                {
                    EndRestEffects();   // the film is over: no resting while the street comes back
                }
                if (uis != null && uis.timeMachine != null && (uis.timeMachine.isRunning || uis.timeMachine.isBlockingUi || now - _skipFrom < 1.5f))
                {
                    return;
                }
                _skipping = false;
                D.Body.Free();
                _nextTry = now;
            }
            if (now < _nextTry)
            {
                return;
            }
            _nextTry = now + 1f;
            // Out where there is room now: an hour on, the street is not as it was.
            Vector3 player = D.PlayerPosition;
            if (SocialPlaces.Standing(_venue.Front, out Vector3 spot) && JobSpots.Flat(spot, player) > 0.9f && D.Cast.Unseat(_friend, spot, player))
            {
                _friend.Face(player);
                if (_kind == PlanKind.Coffee && ClipLibrary.Has("cup_hold"))
                {
                    _cups = _friend.WorkOver("cup_hold", D.Props, 0.3f, ClipEnd.Return, BodyMask.RightArm);
                    D.Body.Over("cup_hold", BodyMask.RightArm);
                    _sipAt = now + 2.2f;
                }
                Wrap(now, true);
                return;
            }
            if (++_backTries >= 6)
            {
                D.Cast.Release(_friend);
                _friend = null;
                Wrap(now, true);
            }
        }

        private static float Length(string clip, float fallback)
        {
            return ClipLibrary.Has(clip) && ClipLibrary.TryGet(clip, out CityClip found) ? found.Length : fallback;
        }

        private void ClearPlan()
        {
            if (!_test)
            {
                SocialState.Plan.Clear();
            }
        }

        public override void End(SceneEnd why)
        {
            EndRestEffects();
            LeaveBench("the outing ended");
            SocialDirector.Talk = null;
            string reason = WhyText(_cut);
            if (_greeted && !_settled && !_test && why != SceneEnd.Fault)
            {
                // Walking out on it costs; being taken away from it does not.
                _settled = true;
                if (why == SceneEnd.Left)
                {
                    SocialLife.Change(_who, 0f, -4f, -3f);
                    SocialLife.Toast(NotificationType.Warning, "reallife_social_cut_short_left", SocialLife.Data(_who), 8f);
                }
                else
                {
                    SocialLife.Change(_who, _def.Familiarity * 0.4f, _def.Affection * 0.3f, 0f);
                    SocialLife.Toast(NotificationType.Info, reason != null ? "reallife_social_break_cut_short" : "reallife_social_cut_short",
                        SocialLife.Data(_who, "why", reason), 9f);
                }
            }
            else if (_greeted && !_settled && _test && reason != null)
            {
                _settled = true;
                SocialLife.Toast(NotificationType.Warning, "reallife_social_break_test", SocialLife.Data(_who, "why", reason), 9f);
            }
            ReturnBoxes();
            D.Cast.Release(_friend);
            ReleaseLeaving();
            _friend = null;
            _car = null;
            _skipping = false;
        }
    }
}
