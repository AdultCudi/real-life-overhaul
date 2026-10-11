using System.Collections.Generic;
using System.Text;
using BigAmbitions.DayNightCycle;
using Characters.EmojiSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A plan being kept: the player goes to the place that was agreed, the other person is
    /// waiting there (or comes late, walking in), they greet, talk face to face, do the thing,
    /// and part. One class for the eight outings and the two favours that are played on the
    /// street, because they share everything but the middle:
    ///
    ///  - a business (coffee, a meal, drinks, the cinema, a workout, the player's own shop): they
    ///    walk to the door together, the time inside passes with the game's own time machine,
    ///    and they are back on the pavement outside. After a coffee both come out with a cup.
    ///    Found shut, it becomes a walk.
    ///  - a walk: the other person walks beside the player wherever they go, for 160 m.
    ///  - a drive, or a lift somewhere: the player pulls up, they walk to a rear door and get in
    ///    (the job passenger's clips and places), ride, and get out on the kerb.
    ///  - helping them move: three boxes carried from the old door to the new one.
    ///
    /// People only ever move by walking on the navigation mesh, are only put where a place was
    /// found and tested, and are all given back in <see cref="End"/>.
    /// </summary>
    public sealed class OutingScene : SocialScene
    {
        private enum Step { Travel, Greeting, Talk, ToDoor, Inside, Walking, Boarding, Riding, PullOver, Boxes, Wrap, Parting }
        private enum Hello { Closing, Turning, Playing }
        private enum Toast { None, Closing, Turning, Raised, Done }

        private const float Appear = 130f;           // metres from the place at which whoever waits there is put out
        private const float WalkMost = 45f;          // metres of route somebody walking in is given at most...
        private const float WalkSeconds = 40f;       // ...and seconds: after that they are put nearer, or met where they are
        private const float AtSpot = 2.5f;           // metres from where they wait that counts as being there
        private const float HoldNudge = 40f;         // seconds the player is indoors, after hello, before they ask where the player is
        private const float HoldMost = 100f;         // ...and before they give up and go
        private const float WaveWithin = 11f;
        private const float HelloWithin = 3.4f;
        private const float GreetGap = 0.80f;        // metres apart, facing, that every greeting clip is made for (CityAssets)
        private const float GreetSlack = 0.10f;      // ...and how far off that a whole-body one is still played
        private const float ToastGap = 1.05f;        // ...and `cheers`: two glasses meet bowl to bowl at this, and pass at 0.80
        private const float ToastSlack = 0.08f;
        private const float WalkGoal = 160f;         // metres walked together
        private const float DriveGoal = 650f;        // metres driven together
        private const float StopWithin = 17f;        // the car has "pulled up" when stopped this near
        private const float StoppedSpeed = 0.7f;
        private const float DoorExact = 0.35f;       // metres from where the getting-in clip is made for within which it is played
        private const float HarshG = 9.5f;           // m/s2 held over two ticks: an emergency stop
        private const float AwayAllow = 25f;         // seconds the driver may leave them sitting in the car
        private const int BoxCount = 3;
        // What is carried on a move: the printed moving box (0.45 x 0.35 x 0.35, "FRAGILE"; made to be carried exactly as
        // the parcel the carrying clip was made with, a hand under each end) when the bundle has it, else that parcel.
        private static string BoxProp => PropLibrary.Has("moving_box") ? "moving_box" : "parcel_m";
        private const string CarryClip = "carry_box_hold";

        private readonly SocialPerson _who;
        private readonly bool _test;
        private readonly float _time;
        private readonly float _arrives;
        private readonly bool _theirIdea;
        private readonly Venue _venue;
        private readonly Venue _to;
        private readonly StringBuilder _text = new StringBuilder(256);
        private PlanKind _kind;
        private OutingDef _def;
        private CityPerson _friend;
        private Step _step;
        private float _stepFrom;
        private float _stepEnd;
        private Vector3 _spot;                 // where they wait
        private bool _early;                   // the player was there first
        private bool _walkingIn;
        private bool _waved;
        private bool _greeted;
        private bool _settled;
        private bool _parted;
        private bool _closed;                 // the place was shut: a walk instead
        private bool _covered;                 // they paid because the player could not
        private int _placeTries;
        private float _nextTry;
        private float _lateBy;
        private int _paid;
        private string _greetMine, _greetTheirs;   // the greeting chosen: the player's half and theirs (null: a wave)
        private bool _greetWhole;              // whole-body clips (a real hug, a kiss on the cheek), not the upper body only
        private Hello _hello;
        private string _first;                 // what the marker on them says
        private bool _returning;               // walking back to the pavement after the car pulled away
        private bool _nudged;                  // "where are you?" went out
        private float _walkUntil;
        private int _walkTries;
        private bool _heldWarned;
        private bool _toldThere;               // told, while indoors, that they are waiting outside
        private string _heldLine;
        private SceneBreak _cut;

        // inside
        private bool _skipping;
        private float _skipFrom;
        private int _backTries;
        private float _sipAt = -1f;
        private float _cupBackAt = -1f;
        private float _outUntil;
        private float _turnAt;
        private bool _turned;
        private bool _wentWell;
        private bool _cups;
        private Toast _toast;
        private float _toastAt, _toastFrom;
        private bool _toastHeld;               // the player is kept on the spot for the toast

        // walking
        private float _metres;
        private int _shownTens = -1;
        private Vector3 _lastPlayer;
        private Vector3 _followGoal;
        private float _nextFollow;
        private float _side = 1f;
        private bool _stood;
        private float _apartSince = -1f;
        private bool _apartSaid;
        private int _beats;
        private float _talkUntil = -1f;        // walk_talk is on their upper body until then

        // driving
        private CarController _car;
        private Vector3 _boardSpot;
        private Vector3 _boardCarAt;
        private Vector3 _inwards;
        private bool _doorClip;
        private bool _doorRight;
        private float _enterUntil = -1f;
        private float _stoppedSince = -1f;
        private bool _tried;
        private Vector3 _triedAt;
        private float _lookingSince;
        private Vector3 _lastCar;
        private Vector3 _lastVelocity;
        private bool _wasHarsh;
        private float _lastJolt = -100f;
        private float _lastTick;
        private float _awaySince = -1f;
        private int _jolts;
        private int _crashes;
        private bool _onFootSaid;

        // boxes
        private readonly Transform[] _boxes = new Transform[BoxCount];
        private readonly Vector3[] _boxAt = new Vector3[BoxCount];
        private readonly Transform[] _delivered = new Transform[BoxCount];
        private Vector3 _drop;
        private int _boxCount;
        private int _boxesDone;
        private int _carrying = -1;
        private string _pickPrompt, _dropPrompt;

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
        public bool Greeted => _greeted;
        public override bool SkippingTime => _skipping;
        public override bool Waiting => _step == Step.Travel && _friend == null;

        private bool Rides => _kind == PlanKind.Drive || _kind == PlanKind.Lift;

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
                // The walk is over, however it ended: the talking arm with it.
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

        // ---------------------------------------------------------------- the tick

        public override void Tick(float now)
        {
            float dt = Mathf.Max(0.05f, now - _lastTick);
            _lastTick = now;
            if (_friend != null && _friend.Lost && _step != Step.Parting)
            {
                // Knocked down, or gone with the scene: the outing is over and nobody is blamed.
                _friend = null;
                Say(_who, "reallife_social_plan_lost", null, NotificationType.Warning);
                ClearPlan();
                Over = true;
                return;
            }
            switch (_step)
            {
                case Step.Travel: Travel(now); break;
                case Step.Greeting: Greeting(now); break;
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
                case Step.Inside: Inside(now); break;
                case Step.Walking: Walking(now); break;
                case Step.Boarding: Boarding(now); break;
                case Step.Riding: Riding(now, dt); break;
                case Step.PullOver: PullOver(now); break;
                case Step.Boxes: Boxes(now); break;
                case Step.Wrap: Wrapping(now); break;
                case Step.Parting:
                    if (!TickLeaving(now))
                    {
                        Over = true;
                    }
                    break;
            }
            _lastVelocity = D.CarVelocity;
        }

        // ---------------------------------------------------------------- getting there, and whoever waits

        private void Travel(float now)
        {
            float nowMinutes = SocialLife.Now;
            if (PlanGone(now, nowMinutes))
            {
                return;
            }
            Nudge(nowMinutes);
            Vector3 player = D.PlayerPosition;
            if (_friend == null)
            {
                if (JobSpots.Flat(player, _spot) > Appear || now < _nextTry)
                {
                    return;
                }
                if (nowMinutes < _arrives)
                {
                    ShowEarly();
                    return;
                }
                PutOut(now, player, false);
                return;
            }

            float gap = JobSpots.Flat(_friend.Position, player);
            if (_returning)
            {
                // Back to the pavement after the car pulled away: no boarding until they are there and standing.
                if ((_friend.Arrived && now - _stepFrom > 1f) || now > _walkUntil)
                {
                    _returning = false;
                    _friend.Stand();
                    WaitingPose();
                }
                else
                {
                    return;
                }
            }
            if (!_greeted)
            {
                MarkPerson(_friend, _first);   // the marker is on them, wherever they are, until hello is said
            }
            if (_walkingIn)
            {
                if (gap < WaveWithin)
                {
                    Stood(player);   // the player has come to meet them: that is where they meet
                }
                else if (WalkingIn(now, player))
                {
                    return;
                }
                gap = JobSpots.Flat(_friend.Position, player);
            }
            if (!_waved && gap < WaveWithin)
            {
                _waved = true;
                _friend.Idle(0.3f);
                _friend.Face(player);
                Gesture(_friend, D.Props, "wave_hello");
            }
            if (Rides)
            {
                Prompt = null;
                TryBoard(now, gap);
                return;
            }
            if (!D.OnFoot)
            {
                Prompt = null;
                if (gap < 40f && !_onFootSaid)
                {
                    _onFootSaid = true;
                    Line = SocialLife.T("reallife_social_obj_park", SocialLife.Data(_who));
                }
                return;
            }
            if (gap < HelloWithin)
            {
                if (Prompt == null)
                {
                    Offer(SocialLife.Say("reallife_social_hello_prompt", SocialLife.Data(_who)), "reallife_social_button_hello", _friend);
                }
            }
            else
            {
                Prompt = null;
            }
        }

        // True if there is no longer anything to keep: called off on the phone, or the player never came.
        private bool PlanGone(float now, float nowMinutes)
        {
            if (!_test && !_greeted && !SocialState.Plan.Active)
            {
                Abandon(now);   // called off on the phone while the way there was being shown
                return true;
            }
            if (nowMinutes > _time + SocialRules.StoodUpMinutes)
            {
                // The player never came (or came and never said a word).
                if (_test)
                {
                    SocialLife.Toast(NotificationType.Warning, "reallife_social_test_expired", null, 6f);
                }
                else
                {
                    SocialLife.StoodUp();
                }
                Abandon(now);
                return true;
            }
            return false;
        }

        private void ShowEarly()
        {
            if (!_early)
            {
                _early = true;
                Line = SocialLife.T("reallife_social_obj_early", SocialLife.Data(_who, "time", SocialLife.Clock(_arrives)));
            }
        }

        // Twenty minutes after the time (and after they got there themselves) they ask where the player is. Once; it is the
        // one warning before being stood up, so it goes out whatever the day's cap on texts says.
        private void Nudge(float nowMinutes)
        {
            if (_nudged || _test || _greeted || nowMinutes < Mathf.Max(_time + SocialRules.LateMinutes, _arrives + 10f))
            {
                return;
            }
            _nudged = true;
            SocialLife.SendText(_who, "reallife_social_text_where", SocialLife.Data(_who, "place", _venue.Name), true);
            SocialDirector.Logger?.Info("Social: " + _who.Name + " asked where the player is (" + (nowMinutes - _time).ToString("F0") + " min after the time agreed).");
        }

        // The plan is off before anybody said hello. Whoever was waiting walks off; nobody vanishes where they stood.
        private void Abandon(float now)
        {
            Prompt = null;
            Line = null;
            Title = null;
            Unmark();
            _settled = true;
            _walkingIn = false;
            _returning = false;
            if (_friend != null)
            {
                _friend.Idle(0.3f);
                Depart(_friend, now);
                _friend = null;
                Go(Step.Parting, now, 0f);
            }
            else
            {
                Over = true;
            }
        }

        // Puts them out. If the player would see them appear they walk in from close by, out of view, by a route that
        // was measured (never more than WalkMost metres); otherwise they are simply there, by the door. With "unseen"
        // the player is indoors: nobody can see the pavement, and they stand facing the street.
        private void PutOut(float now, Vector3 player, bool unseen)
        {
            _nextTry = now + 1.5f;
            _placeTries++;
            if (!SocialPlaces.Standing(_venue.Front, out _spot) && (_placeTries < 3 || !CitySpots.Pavement(_venue.Front, 6f, out _spot)))
            {
                // No room outside the place just now (a van on the pavement, roadworks). From the third try any pavement
                // within six metres of the door will do; with none at all it is called off, nobody's fault.
                _spot = _venue.Front;
                if (_placeTries >= 8)
                {
                    NoPlace("no pavement to stand on within 6 m of the door");
                }
                return;
            }
            bool watched = !unseen && (_early || (CitySpots.InView(_spot) && JobSpots.Flat(player, _spot) < 60f));
            if (watched && _placeTries <= 4)
            {
                if (!SocialPlaces.WalkIn(_spot, player, WalkMost, out Vector3 from, out _))
                {
                    return;   // no short way in from out of view this time: asked again in a moment
                }
                _friend = D.Cast.Put(_who, from, _spot);
                // Their own agent has the last word on the route: the whole way, and no longer than was asked for.
                if (_friend != null && SocialCast.Walk(_friend, _spot, out float length, out bool whole) && whole && length <= WalkMost * 1.3f && _friend.WalkTo(_spot))
                {
                    _walkingIn = true;
                    _walkTries = 0;
                    _stepFrom = now;
                    _walkUntil = now + WalkSeconds;
                    MarkPerson(_friend, _first);
                    Line = SocialLife.T("reallife_social_obj_coming", SocialLife.Data(_who));
                    LogPut(_who, _friend, _spot, "is walking in to " + _venue.Name + " (try " + _placeTries + "): put out of view");
                    return;
                }
                D.Cast.Release(_friend);
                _friend = null;
                return;
            }
            if (watched && _placeTries <= 8 && CitySpots.InView(_spot) && JobSpots.Flat(player, _spot) < 60f)
            {
                return;   // no way in was found: they appear by the door as soon as the player looks another way (or after six seconds more)
            }
            Vector3 outward = _spot + (_spot - _venue.Door);
            _friend = D.Cast.Put(_who, _spot, unseen && JobSpots.Flat(outward, _spot) > 0.3f ? outward : player);
            if (_friend == null)
            {
                if (_placeTries >= 12)
                {
                    NoPlace(D.Cast.HasRoom ? "the place found would not take them" : "no character free");
                }
                return;
            }
            _walkingIn = false;
            WaitingPose();
            MarkPerson(_friend, _first);
            Line = SocialLife.T(Rides ? "reallife_social_obj_meet_car" : "reallife_social_obj_there", SocialLife.Data(_who, "place", _venue.Name));
            LogPut(_who, _friend, _spot, "is waiting outside " + _venue.Name + (unseen ? ": put there while the player is indoors"
                : watched ? ": no short way in from out of view in " + (_placeTries - 1) + " tries, so put at the place" : ": put at the place, nobody looking that way"));
        }

        private void NoPlace(string why)
        {
            SocialDirector.Logger?.Info("Social: " + _who.Name + " could not be put outside " + _venue.Name + " after " + _placeTries + " tries (" + why + "); the plan is off.");
            Say(_who, "reallife_social_plan_lost", null, NotificationType.Warning);
            ClearPlan();
            Over = true;
        }

        // Somebody walking in. True while they still are. The walk is bounded: if their route ended short of the place, or
        // WalkSeconds have gone, they are put at the place (if nobody is looking at them or at it), or nearer and walk
        // again (twice at most), or they stop where they are: wherever they stand, that is where they are met.
        private bool WalkingIn(float now, Vector3 player)
        {
            float left = JobSpots.Flat(_friend.Position, _spot);
            bool ended = now - _stepFrom > 1f && _friend.Arrived;
            if (ended && left <= AtSpot)
            {
                Stood(player);
                return false;
            }
            if (!ended && now < _walkUntil)
            {
                return true;
            }
            string what = ended ? "their route ended " + left.ToString("F0") + " m short" : "still " + left.ToString("F0") + " m away after " + (now - _stepFrom).ToString("F0") + " s";
            bool looking = _friend.Character.visible && CitySpots.InView(_friend.Position);
            if (!looking)
            {
                if (!(CitySpots.InView(_spot) && JobSpots.Flat(player, _spot) < 60f) && D.Cast.Move(_friend, _spot, player))
                {
                    SocialDirector.Logger?.Info("Social: " + _who.Name + " (" + what + ") was put at the place: nobody was looking at them or at it.");
                    Stood(player);
                    return false;
                }
                if (_walkTries < 2 && SocialPlaces.WalkIn(_spot, player, 26f, out Vector3 nearer, out float length) && D.Cast.Move(_friend, nearer, _spot) && _friend.WalkTo(_spot))
                {
                    _walkTries++;
                    _stepFrom = now;
                    _walkUntil = now + 25f;
                    SocialDirector.Logger?.Info("Social: " + _who.Name + " (" + what + ") was put nearer, out of view: " + length.ToString("F0") + " m of route to walk.");
                    return true;
                }
            }
            SocialDirector.Logger?.Info("Social: " + _who.Name + " (" + what + ") stops where they are, " + JobSpots.Flat(_friend.Position, player).ToString("F0")
                                        + " m from the player: the marker is on them and that is where they are met.");
            Stood(player);
            return false;
        }

        // They are standing and waiting, at the place or wherever they got to: said on screen, with the marker on them.
        private void Stood(Vector3 player)
        {
            _walkingIn = false;
            _friend.Stand();
            _friend.Face(player);
            WaitingPose();
            MarkPerson(_friend, _first);
            bool atPlace = JobSpots.Flat(_friend.Position, _spot) <= AtSpot * 2f;
            Line = SocialLife.T(Rides ? "reallife_social_obj_meet_car" : atPlace ? "reallife_social_obj_there" : "reallife_social_obj_over_there",
                SocialLife.Data(_who, "place", _venue.Name));
        }

        private void WaitingPose()
        {
            string clip = Random.value < 0.5f ? "arms_crossed" : "queue_wait";
            if (ClipLibrary.Has(clip))
            {
                _friend.Work(clip, D.Props, 0.4f);
            }
        }

        // ---------------------------------------------------------------- held up: a door, a time skip, the street not running

        public override bool Hold(SceneBreak why, float now)
        {
            if (Over || _step == Step.Parting || _settled)
            {
                return false;   // only the goodbye was left: nothing to wait for, nothing to say
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
                // Before hello they are, or will be, where it was agreed, whichever door the player went through. If
                // they are out already the player is told at once that they are outside.
                return true;
            }
            // After hello: they wait outside, for a while (see Holding).
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
                    return false;   // called off, or stood up (they have said so): nothing is left to wait for
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
                // Left standing outside for too long: they have gone, and they mind.
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

        // Before hello, with the player through a door: nobody can see the pavement, so whoever is due is simply put by
        // the venue's door facing the street, and whoever was walking in is taken the rest of the way if they are slow.
        private void WhileAway(float now, float nowMinutes)
        {
            Vector3 outward = JobSpots.Flat(_venue.Door, _spot) > 0.3f ? _spot + (_spot - _venue.Door) : _spot + Vector3.forward;
            if (_friend == null)
            {
                if (now < _nextTry)
                {
                    return;
                }
                _nextTry = now + 1.5f;
                Vector3 street = SocialDirector.Where();   // the door of the building the player is in (asked every second and a half, not every tick)
                if (JobSpots.Flat(street, _spot) > Appear)
                {
                    return;
                }
                if (nowMinutes < _arrives)
                {
                    ShowEarly();
                    return;
                }
                PutOut(now, street, true);
            }
            else if (_walkingIn)
            {
                bool there = now - _stepFrom > 1f && _friend.Arrived && JobSpots.Flat(_friend.Position, _spot) <= AtSpot;
                if (!there && now < _walkUntil && !_friend.Arrived)
                {
                    return;
                }
                if (!there)
                {
                    D.Cast.Move(_friend, _spot, outward);
                }
                Stood(outward);
            }
            if (_friend != null && !_walkingIn && !_toldThere)
            {
                _toldThere = true;
                Line = SocialLife.T(Rides ? "reallife_social_obj_meet_car" : "reallife_social_obj_there", SocialLife.Data(_who, "place", _venue.Name));
                SocialLife.Toast(NotificationType.Info, "reallife_social_waiting_outside", SocialLife.Data(_who, "place", _venue.Name), 9f);
            }
        }

        public override void Resume(SceneBreak why, float now, float held)
        {
            _lastTick = now;
            _lastPlayer = D.PlayerPosition;
            _nextTry = now + 1f;
            if (!_greeted)
            {
                return;   // Travel picks it up: the marker is on them, and they wave when the player is near
            }
            Line = _heldLine;
            _heldLine = null;
            // Whatever was being timed is timed from now: nothing ran out while the player was away.
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
            string reason = WhyText(why);
            if (reason == null || Over || _settled || _step == Step.Parting || _greeted)
            {
                return;   // after hello the ending itself says it, with this reason (see End)
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
                   + (_friend == null ? ", nobody out" : _walkingIn ? ", they were walking in" : D != null && D.Cast.Seated(_friend) ? ", they were in the car or inside" : ", they were out")
                   + (_test ? ", a test" : string.Empty);
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
        }

        // ---------------------------------------------------------------- hello

        private void Greet(float now)
        {
            Prompt = null;
            Line = null;
            Unmark();
            _walkingIn = false;
            Arrived();
            Vector3 player = D.PlayerPosition;
            _friend.Idle(0.25f);
            _friend.Stand();
            _friend.Face(player);
            D.Body.Hold();
            D.Body.Turn(D, _friend.Position);

            // The greeting is made for two people FACING, GreetGap apart. The player stays where they are; whoever is met
            // comes the last steps up to them first (never further than hello is said from), then both turn, then it plays.
            ChooseGreeting();
            float gap = JobSpots.Flat(_friend.Position, player);
            if (_greetMine != null && gap > GreetGap + GreetSlack && _friend.StepTo(player, GreetGap))
            {
                _hello = Hello.Closing;
                Go(Step.Greeting, now, 4.5f);
                return;
            }
            _hello = Hello.Turning;
            Go(Step.Greeting, now, 0.3f);
        }

        // Which greeting, by how close they are: a kiss on the cheek for a partner, a real hug (one clip each, the whole
        // body) for close friends and family, a handshake for everybody else. Null: none of those can be had, a wave.
        private void ChooseGreeting()
        {
            bool close = _who.Stage >= SocialStage.CloseFriend || _who.IsFamily;
            _greetWhole = true;
            if (_who.Together && ClipLibrary.Has("social_cheek_kiss"))
            {
                _greetMine = _greetTheirs = "social_cheek_kiss";
            }
            else if (close && ClipLibrary.Has("social_hug_a") && ClipLibrary.Has("social_hug_b"))
            {
                _greetMine = "social_hug_a";      // the one who pats
                _greetTheirs = "social_hug_b";    // the one who is patted
            }
            else
            {
                _greetWhole = false;
                string upper = close ? "social_hug" : "social_handshake";
                _greetMine = _greetTheirs = ClipLibrary.Has(upper) ? upper : null;
            }
        }

        private void Greeting(float now)
        {
            Vector3 player = D.PlayerPosition;
            switch (_hello)
            {
                case Hello.Closing:
                    // Come to a stop in front of the player (an agent reads "arrived" a quarter of a metre early), or held up.
                    if ((now - _stepFrom > 0.4f && _friend.Arrived && _friend.Speed < 0.05f) || now >= _stepEnd)
                    {
                        _friend.Stand();
                        _friend.Face(player);
                        D.Body.Turn(D, _friend.Position);
                        _hello = Hello.Turning;
                        Go(Step.Greeting, now, 0.35f);
                    }
                    break;

                case Hello.Turning:
                    if (now >= _stepEnd)
                    {
                        _hello = Hello.Playing;
                        Go(Step.Greeting, now, PlayGreeting(JobSpots.Flat(_friend.Position, player)) + 0.1f);
                    }
                    break;

                default:
                    if (now >= _stepEnd)
                    {
                        D.Body.Rest(0.2f);
                        OpenTalk(now);
                    }
                    break;
            }
        }

        // Both at the same moment, each their own half; only standing as the clip was made for (they pass through each
        // other nearer, and reach at air further off). Returns how long it takes.
        private float PlayGreeting(float gap)
        {
            string mine = _greetMine, theirs = _greetTheirs;
            bool whole = _greetWhole;
            if (mine != null && whole && Mathf.Abs(gap - GreetGap) > GreetSlack)
            {
                // Not near enough for arms round each other: the upper body's hug if they are near enough for that.
                whole = false;
                mine = theirs = ClipLibrary.Has("social_hug") ? "social_hug" : null;
            }
            if (mine != null && !whole && (gap <= 0.65f || gap >= 1.0f))
            {
                mine = theirs = null;
            }
            if (mine != null && whole)
            {
                // Whole bodies: the player is held, they are standing. One without the other is not played.
                if (_friend.Work(theirs, D.Props, 0.2f, ClipEnd.Return, false))
                {
                    if (D.Body.Clip(mine, 0.2f))
                    {
                        return Length(mine, 2.6f);
                    }
                    _friend.Idle(0.2f);
                }
                mine = theirs = null;
            }
            if (mine == null)
            {
                mine = theirs = "wave_hello";
            }
            Gesture(_friend, D.Props, theirs);
            D.Body.Over(mine, BodyMask.Upper, 0.2f);
            return Length(mine, 1.8f);
        }

        private static float Length(string clip, float fallback)
        {
            return ClipLibrary.Has(clip) && ClipLibrary.TryGet(clip, out CityClip found) ? found.Length : fallback;
        }

        // What being there (and when) is worth. Once, whichever way the two of them come together.
        private void Arrived()
        {
            if (_greeted)
            {
                return;
            }
            _greeted = true;
            _lateBy = SocialLife.Now - _time;
            ClearPlan();
            SocialLife.Contacted(_who);
            if (_test)
            {
                return;
            }
            if (_lateBy > SocialRules.LateMinutes && !_who.Has(SocialTrait.Late))
            {
                float weight = _who.Has(SocialTrait.Punctual) ? 2f : 1f;
                SocialLife.Change(_who, 0f, SocialRules.LateAffection * weight, SocialRules.LateTrust * weight);
                SocialLife.Remember(_who, MemoryCode.Late);
            }
            else if (_lateBy <= SocialRules.LateMinutes)
            {
                SocialLife.Change(_who, 0f, 0f, SocialRules.OnTimeTrust);
            }
        }

        private string GreetingLine()
        {
            if (_lateBy > SocialRules.LateMinutes)
            {
                return SocialLines.Pick(_who.Has(SocialTrait.Late) ? SocialCatalogue.GreetLateEasy : SocialCatalogue.GreetLate);
            }
            if (_early && _arrives - _time > 8f)
            {
                return SocialLines.Pick(SocialCatalogue.TheyAreLate);
            }
            return SocialLines.Pick(_who.Together ? SocialCatalogue.GreetLove
                : _who.Stage >= SocialStage.CloseFriend || _who.IsFamily ? SocialCatalogue.GreetClose
                : _who.Stage == SocialStage.Friend ? SocialCatalogue.GreetFriend
                : SocialCatalogue.GreetAcquaintance);
        }

        private void OpenTalk(float now)
        {
            bool pays = _def.Cost > 0;
            var talk = new SceneTalk
            {
                Person = _who, Mode = SceneTalk.Outing, Opener = GreetingLine(), OpenerData = SocialLife.Data(_who, "place", _venue.Name),
                Cost = pays ? _def.Cost : 0,
                TheyOffer = pays && (_theirIdea || _who.Has(SocialTrait.Generous)) && !_who.Has(SocialTrait.Careful),
                GoLabel = _kind == PlanKind.Walk ? "reallife_social_btn_lets_walk" : _kind == PlanKind.Moving ? "reallife_social_btn_lets_start" : "reallife_social_btn_go_in"
            };
            Converse(talk, _friend, now);
            Go(Step.Talk, now, 0f);
        }

        // ---------------------------------------------------------------- the thing itself

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
                // Shut. They make the best of it: a walk instead.
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
                // (They may have been met a little way off: the walk to the door is given the time it takes, half a minute at most.)
                Go(Step.ToDoor, now, Mathf.Clamp(JobSpots.Flat(_friend.Position, door) / 1.2f + 3f, 7f, 30f));
            }
            else
            {
                GoInside(now);
            }
        }

        // They are through the door: off the street, and the time inside passes the way the game passes time.
        private void GoInside(float now)
        {
            D.Cast.Seat(_friend);
            Line = null;
            var uis = InstanceBehavior<UI.UIs>.Instance;
            if (_def.Minutes > 0 && uis != null && uis.timeMachine != null && !uis.timeMachine.isRunning)
            {
                D.Body.Hold();
                _skipping = true;
                _skipFrom = now;
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
            // Out on the pavement again, where there is room now (an hour on, the street is not as it was).
            Vector3 player = D.PlayerPosition;
            if (SocialPlaces.Standing(_venue.Front, out Vector3 spot) && JobSpots.Flat(spot, player) > 0.9f && D.Cast.Unseat(_friend, spot, player))
            {
                _friend.Face(player);
                if (_kind == PlanKind.Coffee && ClipLibrary.Has("cup_hold"))
                {
                    // Both come out with a cup: theirs in the hand it was made for, the player's on the arm only.
                    _cups = _friend.WorkOver("cup_hold", D.Props, 0.3f, ClipEnd.Return, BodyMask.RightArm);
                    D.Body.Over("cup_hold", BodyMask.RightArm);
                    _sipAt = now + 2.2f;
                }
                Wrap(now, true);
                return;
            }
            if (++_backTries >= 6)
            {
                // Nowhere for them to come out to: they have gone their own way from the door.
                D.Cast.Release(_friend);
                _friend = null;
                Wrap(now, true);
            }
        }

        // ---------------------------------------------------------------- a walk

        private void StartWalk(float now)
        {
            _metres = 0f;
            _shownTens = -1;
            _lastPlayer = D.PlayerPosition;
            _apartSince = -1f;
            _beats = 0;
            _friend.Idle(0.3f);
            ShowWalk();
            Go(Step.Walking, now, 420f);
        }

        private void ShowWalk()
        {
            int tens = Mathf.FloorToInt(_metres / 10f);
            if (tens != _shownTens)
            {
                _shownTens = tens;
                Dictionary<string, string> data = SocialLife.Data(_who, "metres", (tens * 10).ToString());
                data["goal"] = ((int)WalkGoal).ToString();
                Line = SocialLife.T("reallife_social_obj_walk", data);
            }
        }

        private void Walking(float now)
        {
            Vector3 player = D.PlayerPosition;
            float moved = JobSpots.Flat(player, _lastPlayer);
            _lastPlayer = player;
            float gap = JobSpots.Flat(_friend.Position, player);
            if (D.OnFoot && gap < 8f && moved < 3f)
            {
                _metres += moved;
            }
            ShowWalk();
            Follow(now, player, gap);

            // Something to say a third and two thirds of the way along: a gesture of the arms over the game's own walk.
            if (_beats < 2 && _metres >= WalkGoal * (_beats + 1) / 3f && gap < 6f)
            {
                _beats++;
                Say(_who, SocialLines.Pick(SocialCatalogue.WalkTalk));
                // Talking as they walk: head turned to the player and the outside hand making the point, over the game's
                // own walk (walk_talk is for whoever walks on the right, walk_talk_l on the left). Standing: talk_a.
                string talk = _side > 0f ? "walk_talk" : "walk_talk_l";
                if (!_stood && _friend.Speed > 0.3f && ClipLibrary.Has(talk)
                    && _friend.WorkOver(talk, D.Props, 0.3f, ClipEnd.Return, BodyMask.Upper, false))
                {
                    _talkUntil = now + 5f;   // one pass of the loop
                }
                else
                {
                    Gesture(_friend, D.Props, "talk_a");
                }
            }
            if (_talkUntil > 0f && now >= _talkUntil)
            {
                _talkUntil = -1f;
                _friend.StopOver(0.4f);
            }

            if (gap > 30f || !D.OnFoot)
            {
                if (_apartSince < 0f)
                {
                    _apartSince = now;
                }
                else if (now - _apartSince > 15f && !_apartSaid)
                {
                    _apartSaid = true;
                    Say(_who, SocialLines.Pick(SocialCatalogue.WalkBehind));
                }
                else if (now - _apartSince > 45f)
                {
                    LeftBehind(now);
                    return;
                }
            }
            else
            {
                _apartSince = -1f;
                _apartSaid = false;
            }
            if (_metres >= WalkGoal)
            {
                _friend.Stand();
                _friend.Face(player);
                Wrap(now, true);
            }
            else if (now >= _stepEnd)
            {
                // A long time standing about: they call it a day, kindly.
                Wrap(now, _metres > WalkGoal * 0.5f);
            }
        }

        // They keep beside the player: a new route only when the place beside the player has moved.
        private void Follow(float now, Vector3 player, float gap)
        {
            if (now < _nextFollow)
            {
                return;
            }
            _nextFollow = now + 0.4f;
            Vector3 forward = D.PlayerForward;
            if (gap < 1.9f && D.PlayerSpeed < 0.25f)
            {
                if (!_stood)
                {
                    _stood = true;
                    _friend.Stand();
                    _friend.Face(player);
                }
                return;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (!CitySpots.Pavement(player + right * (_side * 1.1f) - forward * 0.3f, 0.9f, out Vector3 goal))
            {
                _side = -_side;
                if (!CitySpots.Pavement(player + right * (_side * 1.1f) - forward * 0.3f, 0.9f, out goal)
                    && !CitySpots.Pavement(player - forward * 1.3f, 2.5f, out goal))
                {
                    return;   // the player is out on the road: they wait on the pavement
                }
            }
            if (!_stood && JobSpots.Flat(goal, _followGoal) < 0.6f)
            {
                return;
            }
            _followGoal = goal;
            _stood = false;
            _friend.WalkTo(goal, gap > 7f);
        }

        private void LeftBehind(float now)
        {
            Say(_who, SocialLines.Pick(SocialCatalogue.LeftBehind), null, NotificationType.Warning);
            if (!_test)
            {
                SocialLife.Change(_who, 0f, -4f, -3f);
            }
            _settled = true;
            Depart(_friend, now);
            _friend = null;
            Line = null;
            Go(Step.Parting, now, 0f);
        }

        // ---------------------------------------------------------------- a drive, a lift

        private void TryBoard(float now, float gap)
        {
            if (!D.Driving)
            {
                _stoppedSince = -1f;
                if (!_onFootSaid && gap < 30f)
                {
                    _onFootSaid = true;
                    Line = SocialLife.T("reallife_social_obj_bring_car", SocialLife.Data(_who));
                }
                return;
            }
            if (_onFootSaid)
            {
                _onFootSaid = false;
                Line = SocialLife.T("reallife_social_obj_meet_car", SocialLife.Data(_who, "place", _venue.Name));
            }
            if (D.CarSpeed > StoppedSpeed || JobSpots.Flat(D.Car.transform.position, _friend.Position) > StopWithin)
            {
                _stoppedSince = -1f;
                return;
            }
            if (_stoppedSince < 0f)
            {
                _stoppedSince = now;
            }
            if (now - _stoppedSince < 0.5f)
            {
                return;
            }
            // Stopped beside them: is there a rear door they can walk to? Asked again only once the car has moved.
            Vector3 carAt = D.Car.transform.position;
            if (_tried && JobSpots.Flat(carAt, _triedAt) < 1f)
            {
                return;
            }
            bool room = JobSpots.BesideCar(D.Car, _friend.Position, out _boardSpot);
            _doorClip = false;
            if (room)
            {
                JobSpots.AtDoor(D.Car, _boardSpot, out Vector3 door, out _inwards, out _doorRight);
                if (ClipJobAnimations.Has(_doorRight ? JobClip.CarEnter : JobClip.CarEnterLeft))
                {
                    _doorClip = true;
                    _boardSpot = door;
                }
            }
            if (room)
            {
                _friend.Idle(0.2f);
                SocialCast.OnRoad(_friend);
                room = _friend.WalkTo(_boardSpot);
            }
            if (!room)
            {
                _tried = true;
                _triedAt = carAt;
                Line = SocialLife.T("reallife_social_obj_no_room", SocialLife.Data(_who));
                return;
            }
            _tried = false;
            _car = D.Car;
            _boardCarAt = carAt;
            Unmark();
            if (!_greeted)
            {
                Arrived();
                Say(_who, GreetingLine(), SocialLife.Data(_who, "place", _venue.Name));
            }
            Line = SocialLife.T("reallife_social_obj_boarding", SocialLife.Data(_who));
            Go(Step.Boarding, now, 30f);
        }

        private void Boarding(float now)
        {
            bool moved = !D.Driving || D.Car != _car || D.CarSpeed > 1.5f || JobSpots.Flat(D.Car.transform.position, _boardCarAt) > 1.2f;
            float gap = JobSpots.Flat(_friend.Position, _boardSpot);
            if (_enterUntil >= 0f)
            {
                // At the door, getting in. If the car pulls away now they are left standing.
                if (!moved && now < _enterUntil)
                {
                    return;
                }
                _enterUntil = -1f;
                if (!moved)
                {
                    D.Cast.Seat(_friend);
                    StartRide(now);
                    return;
                }
                ActivityAnimators.Forget(_friend.Character.animator);   // the door is gone from under their hand
            }
            bool there = now - _stepFrom > 0.6f && _friend.Arrived && gap < 1.3f;
            if (!moved && (there || (now > _stepEnd && gap < 3f)))
            {
                SocialCast.DoorSound(_boardSpot);
                // With the clip, and only from the place it is made for: square to the door, open it, get in. Nobody is slid the last step.
                bool atDoor = _doorClip && gap < DoorExact;
                _friend.Stand();
                _friend.Face(atDoor ? _friend.Position + _inwards * 3f : _car.transform.position);
                float getIn = atDoor ? D.Animations.Play(_friend.Character, _doorRight ? JobClip.CarEnter : JobClip.CarEnterLeft) : 0f;
                SocialDirector.Logger?.Info(getIn > 0f
                    ? "Social: " + _who.Name + " gets in at the " + (_doorRight ? "right" : "left") + " rear door."
                    : "Social: " + _who.Name + " is in the car (no getting-in clip" + (_doorClip ? ": they stopped " + gap.ToString("F2") + " m from the door)." : " to be had)."));
                if (getIn > 0f)
                {
                    _enterUntil = now + getIn;
                    return;
                }
                D.Cast.Seat(_friend);
                StartRide(now);
                return;
            }
            if (moved || now > _stepEnd)
            {
                // The car pulled away (or they could not get to it): back to the pavement, and wait there again.
                if (CitySpots.Pavement(_friend.Position, 8f, out Vector3 back))
                {
                    _friend.WalkTo(back);
                }
                _stoppedSince = -1f;
                _tried = !moved;
                _triedAt = _boardCarAt;
                _returning = true;   // no boarding until they are back and standing (or twenty seconds have gone)
                _walkUntil = now + 20f;
                Line = SocialLife.T("reallife_social_obj_meet_car", SocialLife.Data(_who, "place", _venue.Name));
                Go(Step.Travel, now, 0f);
            }
        }

        private void StartRide(float now)
        {
            _metres = 0f;
            _shownTens = -1;
            _beats = 0;
            _jolts = 0;
            _awaySince = -1f;
            _wasHarsh = false;
            _stoppedSince = -1f;
            _tried = false;
            _lastCar = _car.transform.position;
            _lastVelocity = D.CarVelocity;
            if (_kind == PlanKind.Lift && _to.Valid)
            {
                Mark(_to.Front, _to.Name);
                Line = SocialLife.T("reallife_social_obj_lift", SocialLife.Data(_who, "to", _to.Name));
            }
            else
            {
                ShowDrive();
            }
            Go(Step.Riding, now, 0f);
        }

        private void ShowDrive()
        {
            int tens = Mathf.FloorToInt(_metres / 50f);
            if (tens != _shownTens)
            {
                _shownTens = tens;
                Dictionary<string, string> data = SocialLife.Data(_who, "metres", (tens * 50).ToString());
                data["goal"] = ((int)DriveGoal).ToString();
                Line = SocialLife.T("reallife_social_obj_drive", data);
            }
        }

        private void Riding(float now, float dt)
        {
            if (_car == null)
            {
                // The car is gone, and they with it.
                D.Cast.Release(_friend);
                _friend = null;
                Wrap(now, false);
                return;
            }
            if (!D.Driving || D.Car != _car)
            {
                if (_awaySince < 0f)
                {
                    _awaySince = now;
                    _shownTens = -1;
                    Line = SocialLife.T("reallife_social_obj_back_in", SocialLife.Data(_who));
                }
                else if (now - _awaySince > AwayAllow)
                {
                    // Left sitting in the car: they have got out and gone by the time the driver is back.
                    Say(_who, SocialLines.Pick(SocialCatalogue.LeftBehind), null, NotificationType.Warning);
                    if (!_test)
                    {
                        SocialLife.Change(_who, 0f, -4f, -4f);
                    }
                    _settled = true;
                    D.Cast.Release(_friend);
                    _friend = null;
                    Over = true;
                }
                _stoppedSince = -1f;
                return;
            }
            bool lift = _kind == PlanKind.Lift && _to.Valid;
            if (_awaySince >= 0f)
            {
                _awaySince = -1f;
                if (lift)
                {
                    Line = SocialLife.T("reallife_social_obj_lift", SocialLife.Data(_who, "to", _to.Name));
                }
            }
            Vector3 carAt = _car.transform.position;
            float moved = JobSpots.Flat(carAt, _lastCar);
            _lastCar = carAt;
            if (moved < 30f)
            {
                _metres += moved;
            }

            // How hard the car is being thrown about: braking, swerving, a kerb.
            float g = (D.CarVelocity - _lastVelocity).magnitude / dt;
            bool harsh = g > HarshG;
            if (((harsh && _wasHarsh) || g > HarshG + 5.5f) && now - _lastJolt > 3f)
            {
                _lastJolt = now;
                _jolts++;
                Say(_who, SocialLines.Pick(SocialCatalogue.RideJolt));
            }
            _wasHarsh = harsh;

            float goal = lift ? Mathf.Max(200f, JobSpots.Flat(_venue.Front, _to.Front)) : DriveGoal;
            if (_beats < 2 && _metres >= goal * (_beats + 1) / 3f)
            {
                _beats++;
                Say(_who, SocialLines.Pick(SocialCatalogue.RideTalk));
            }
            if (lift)
            {
                if (Stopped(now, _to.Front, 22f) && StepOut(now, _to.Front, carAt))
                {
                    Unmark();
                    Wrap(now, _jolts < 3 && _crashes == 0);
                }
                return;
            }
            ShowDrive();
            if (_metres >= goal)
            {
                Line = SocialLife.T("reallife_social_obj_pull_over", SocialLife.Data(_who));
                _stoppedSince = -1f;
                _tried = false;
                Go(Step.PullOver, now, 0f);
            }
        }

        private void PullOver(float now)
        {
            if (_car == null || !D.Driving || D.Car != _car)
            {
                Go(Step.Riding, now, 0f);   // out of the car: the ride's own rules for that apply
                return;
            }
            Vector3 carAt = _car.transform.position;
            if (!Stopped(now, carAt, 5f))
            {
                return;
            }
            // Out on the kerb side: towards the nearest pavement.
            Vector3 towards = CitySpots.Pavement(carAt, 9f, out Vector3 kerb) ? kerb : carAt + _car.transform.right * 3f;
            if (StepOut(now, towards, carAt))
            {
                Wrap(now, _jolts < 3 && _crashes == 0);
            }
        }

        // True once the car being driven has stood still near a place for a moment.
        private bool Stopped(float now, Vector3 place, float within)
        {
            if (D.CarSpeed > StoppedSpeed || JobSpots.Flat(_car.transform.position, place) > within)
            {
                _stoppedSince = -1f;
                return false;
            }
            if (_stoppedSince < 0f)
            {
                _stoppedSince = now;
            }
            return now - _stoppedSince >= 0.6f;
        }

        // They get out on the side nearer where they are going. False while there is no room on either side; after
        // twelve seconds of that they are let go unseen rather than kept in the car for good.
        private bool StepOut(float now, Vector3 towards, Vector3 carAt)
        {
            if (_tried && JobSpots.Flat(carAt, _triedAt) < 1f && now - _lookingSince < 12f)
            {
                return false;
            }
            if (!_tried)
            {
                _lookingSince = now;
            }
            if (JobSpots.BesideCar(_car, towards, out Vector3 spot))
            {
                JobSpots.AtDoor(_car, spot, out Vector3 door, out Vector3 inwards, out bool right);
                JobClip exit = right ? JobClip.CarExit : JobClip.CarExitLeft;
                bool atDoor = ClipJobAnimations.Has(exit) && D.Cast.Unseat(_friend, door, door + inwards * 3f);
                if (atDoor || D.Cast.Unseat(_friend, spot, spot + (spot - carAt)))
                {
                    SocialCast.DoorSound(spot);
                    if (atDoor)
                    {
                        _outUntil = now + D.Animations.Play(_friend.Character, exit);
                    }
                    SocialDirector.Logger?.Info("Social: " + _who.Name + " gets out " + (atDoor ? "at the " + (right ? "right" : "left") + " rear door (clip)." : "beside the car."));
                    _car = null;
                    return true;
                }
            }
            _tried = true;
            _triedAt = carAt;
            Line = SocialLife.T("reallife_social_obj_no_room_out", SocialLife.Data(_who));
            if (now - _lookingSince >= 12f)
            {
                SocialDirector.Logger?.Info("Social: no room beside the car for " + _who.Name + " after 12 s; they are let out unseen.");
                D.Cast.Release(_friend);
                _friend = null;
                _car = null;
                return true;
            }
            return false;
        }

        public override void OnCrash(float speed)
        {
            if ((_step == Step.Riding || _step == Step.PullOver) && _friend != null)
            {
                _crashes++;
                Say(_who, SocialLines.Pick(SocialCatalogue.RideCrash), null, NotificationType.Warning);
                if (_crashes == 1 && !_test)
                {
                    SocialLife.Remember(_who, MemoryCode.Crash);
                }
            }
        }

        // ---------------------------------------------------------------- helping them move

        private void StartBoxes(float now)
        {
            _boxCount = 0;
            _boxesDone = 0;
            _carrying = -1;
            bool carry = ClipLibrary.Has(CarryClip);
            bool there = _to.Valid && SocialPlaces.Standing(_to.Front, out _drop);
            if (carry && there && CitySpots.Along(_friend.Position, out Vector3 along))
            {
                // The boxes stand in a row along the pavement beside them, each where there is room for one.
                for (int side = 0; side < 2 && _boxCount < BoxCount; side++)
                {
                    for (int i = 0; i < BoxCount + 2 && _boxCount < BoxCount; i++)
                    {
                        Vector3 at = _friend.Position + along * ((side == 0 ? 1f : -1f) * (0.85f + 0.55f * i));
                        if (!CitySpots.OnPavement(at, out Vector3 spot) || !CitySpots.ClearQuietly(spot, 0.28f) || JobSpots.Flat(spot, D.PlayerPosition) < 0.6f)
                        {
                            continue;
                        }
                        Transform box = D.Props.Stand(BoxProp, spot, Random.Range(-12f, 12f));
                        if (box == null)
                        {
                            break;
                        }
                        _boxes[_boxCount] = box;
                        _boxAt[_boxCount] = spot;
                        _boxCount++;
                    }
                }
            }
            if (_boxCount == 0)
            {
                // No clip to carry with, no prop, or no room to stand a box: it is talked over instead, and counts for half.
                Say(_who, "reallife_social_moving_nothing");
                Wrap(now, false);
                return;
            }
            Mark(_drop, _to.Name);
            ShowBoxes();
            Go(Step.Boxes, now, 480f);
        }

        private void ShowBoxes()
        {
            Dictionary<string, string> data = SocialLife.Data(_who, "to", _to.Name);
            data["count"] = _boxesDone.ToString();
            data["goal"] = _boxCount.ToString();
            Line = SocialLife.T("reallife_social_obj_moving", data);
        }

        private void Boxes(float now)
        {
            Vector3 player = D.PlayerPosition;
            if (now >= _stepEnd)
            {
                PutBack();
                Wrap(now, _boxesDone > 0);
                return;
            }
            if (_carrying >= 0)
            {
                if (!D.OnFoot || D.Body.OverName != CarryClip)
                {
                    PutBack();   // into a car with a box in the arms, or the clip was lost: it is back where it was
                    Prompt = null;
                    return;
                }
                if (JobSpots.Flat(player, _drop) < 2.4f)
                {
                    Offer(_dropPrompt, "reallife_social_button_drop", null);
                }
                else
                {
                    Prompt = null;
                }
                return;
            }
            if (!D.OnFoot)
            {
                Prompt = null;
                return;
            }
            if (NearestBox(player) >= 0)
            {
                Offer(_pickPrompt, "reallife_social_button_pick", null);
            }
            else
            {
                Prompt = null;
            }
        }

        private int NearestBox(Vector3 player)
        {
            int best = -1;
            float bestGap = 1.8f;
            for (int i = 0; i < _boxCount; i++)
            {
                if (_boxes[i] == null)
                {
                    continue;
                }
                float gap = JobSpots.Flat(player, _boxAt[i]);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = i;
                }
            }
            return best;
        }

        private void BoxKey(float now)
        {
            Vector3 player = D.PlayerPosition;
            if (_carrying < 0)
            {
                int box = NearestBox(player);
                if (box < 0)
                {
                    return;
                }
                // The box on the ground goes back on the shelf in the same instant one like it is in the player's arms.
                D.Props.Return(_boxes[box]);
                _boxes[box] = null;
                if (!D.Body.Over(CarryClip, BodyMask.Arms, 0.25f, BoxProp))
                {
                    _boxes[box] = D.Props.Stand(BoxProp, _boxAt[box], 0f);
                    return;
                }
                _carrying = box;
                Prompt = null;
                if (_boxesDone == 0 && _friend != null && !_friend.Lost && SocialPlaces.Standing(_drop + (_drop - _spot).normalized * 1.6f, out Vector3 beside))
                {
                    _friend.WalkTo(beside);   // they go ahead to the new door and wait there
                }
                return;
            }
            if (JobSpots.Flat(player, _drop) >= 2.4f)
            {
                return;
            }
            D.Body.StopOver();
            // Set down in a row by the new door, wherever there is room for one more.
            Vector3 at = _drop;
            if (CitySpots.Along(_drop, out Vector3 along))
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 want = _drop + along * ((i % 2 == 0 ? 1f : -1f) * (0.5f + 0.5f * (i / 2)));
                    if (CitySpots.OnPavement(want, out Vector3 spot) && CitySpots.ClearQuietly(spot, 0.25f) && JobSpots.Flat(spot, player) > 0.5f)
                    {
                        at = spot;
                        break;
                    }
                }
            }
            _delivered[_boxesDone] = D.Props.Stand(BoxProp, at, Random.Range(-10f, 10f));
            _carrying = -1;
            _boxesDone++;
            Prompt = null;
            ShowBoxes();
            if (_boxesDone >= _boxCount)
            {
                Unmark();
                Wrap(now, true);
                return;
            }
            Say(_who, SocialLines.Pick(SocialCatalogue.MovingTalk));
        }

        // The box in the player's arms is on the ground at the old door again.
        private void PutBack()
        {
            if (_carrying < 0)
            {
                return;
            }
            D.Body.StopOver();
            _boxes[_carrying] = D.Props.Stand(BoxProp, _boxAt[_carrying], 0f);
            _carrying = -1;
        }

        // ---------------------------------------------------------------- afterwards

        private void Wrap(float now, bool wentWell)
        {
            Prompt = null;
            Line = null;
            Unmark();
            Settle(wentWell);
            // Somebody who is still getting out of the car finishes that first: nobody turns round half way out of a door.
            _wentWell = wentWell;
            _turned = false;
            _turnAt = Mathf.Max(now, _outUntil);
            Go(Step.Wrap, now, (_cups ? 9f : 5.5f) + (_turnAt - now));
        }

        private void Wrapping(float now)
        {
            if (!_turned && now >= _turnAt)
            {
                _turned = true;
                if (_friend != null && !_friend.Lost && !D.Cast.Seated(_friend))
                {
                    _friend.Stand();
                    _friend.Face(D.PlayerPosition);
                    D.Cast.Emoji(_friend, _wentWell ? CharacterEmojiName.CommonGreenHappy : CharacterEmojiName.CommonYellowNeutral, 3f);
                    if (!_cups && !RaiseToast(now) && ClipLibrary.Has("talk_a"))
                    {
                        _friend.Work("talk_a", D.Props, 0.4f);
                    }
                }
            }
            if (_toast != Toast.None && _toast != Toast.Done)
            {
                Toasting(now);
            }
            if (_cupBackAt > 0f && now >= _cupBackAt)
            {
                _cupBackAt = -1f;
                D.Body.Over("cup_hold", BodyMask.RightArm);   // the drink taken, the cup is held again
            }
            if (_cups && _sipAt > 0f && now >= _sipAt && _friend != null && !_friend.Lost)
            {
                // A drink from the cup: theirs with the whole body (they are standing), the player's with the arm.
                _sipAt = -1f;
                if (ClipLibrary.Has("sip"))
                {
                    _friend.Work("sip", D.Props, 0.3f);
                    D.Body.Over("sip", BodyMask.RightArm);
                    _cupBackAt = now + 3.5f;
                }
            }
            if (now < _stepEnd)
            {
                return;
            }
            if (!_parted)
            {
                // A word and a wave first; they turn to go when the wave is done.
                _parted = true;
                _stepEnd = now + 1.5f;
                D.Body.Rest();
                EndToast();
                if (_friend != null && !_friend.Lost)
                {
                    _friend.Idle(0.3f);
                    _friend.PutAway(D.Props);
                    Say(_who, SocialLines.Pick(_who.Together ? SocialCatalogue.PartingLove : SocialCatalogue.Parting));
                    Gesture(_friend, D.Props, ClipLibrary.Has("job_thank") ? "job_thank" : "wave_hello");
                }
                return;
            }
            Depart(_friend, now);
            if (_wentWell && _friend != null && !_friend.Lost && ClipLibrary.Has("goodbye_wave_walk"))
            {
                // A wave over the shoulder as they go: the right arm only, over the game's own walk.
                _friend.WorkOver("goodbye_wave_walk", D.Props, 0.25f, ClipEnd.Return, BodyMask.RightArm, false);
            }
            _friend = null;
            Title = null;
            Go(Step.Parting, now, 0f);
        }

        // ---------------------------------------------------------------- a toast, after drinks that went well

        // `cheers` is made for two FACING ToastGap apart, a wine glass each at the clip's grip: at that distance the two
        // bowls meet at its clink mark (0.8 s). So they come up to that distance first, the player is kept there, both
        // turn, and both start it at the same moment. True if it has been begun (it may still come to nothing).
        private bool RaiseToast(float now)
        {
            if (_kind != PlanKind.Drinks || !_wentWell || _closed || !D.OnFoot || D.PlayerSpeed > 0.25f || !ClipLibrary.Has("cheers")
                || !ClipLibrary.TryGet("cheers", out CityClip cheers) || !cheers.HasTool || !PropLibrary.TryGet(cheers.Prop, out _, out _))
            {
                return false;
            }
            Vector3 player = D.PlayerPosition;
            float gap = JobSpots.Flat(_friend.Position, player);
            if (gap > HelloWithin + 1.5f)
            {
                return false;
            }
            D.Body.Hold();
            _toastHeld = true;
            D.Body.Turn(D, _friend.Position);
            _toast = Toast.Turning;
            _toastAt = now + 0.35f;
            if (Mathf.Abs(gap - ToastGap) > ToastSlack && _friend.StepTo(player, ToastGap))
            {
                _toast = Toast.Closing;
                _toastAt = now + 3.5f;
            }
            _toastFrom = now;
            _stepEnd = Mathf.Max(_stepEnd, now + 3.5f + 0.35f + cheers.Length + 1.6f);
            return true;
        }

        private void Toasting(float now)
        {
            if (_friend == null || _friend.Lost)
            {
                EndToast();
                return;
            }
            Vector3 player = D.PlayerPosition;
            switch (_toast)
            {
                case Toast.Closing:
                    if ((now - _toastFrom > 0.4f && _friend.Arrived && _friend.Speed < 0.05f) || now >= _toastAt)
                    {
                        _friend.Stand();
                        _friend.Face(player);
                        D.Body.Turn(D, _friend.Position);
                        _toast = Toast.Turning;
                        _toastAt = now + 0.35f;
                    }
                    break;

                case Toast.Turning:
                    if (now < _toastAt)
                    {
                        break;
                    }
                    // Both or neither, and only standing as the clip was made for; the glasses stay in the hands (the
                    // clip ends holding one) until the goodbye.
                    if (Mathf.Abs(JobSpots.Flat(_friend.Position, player) - ToastGap) <= ToastSlack
                        && _friend.Work("cheers", D.Props, 0.25f, ClipEnd.Hold))
                    {
                        if (D.Body.Clip("cheers", 0.25f, ClipEnd.Hold))
                        {
                            _toast = Toast.Raised;
                            _stepEnd = Mathf.Max(_stepEnd, now + Length("cheers", 1.8f) + 1.6f);
                            break;
                        }
                        _friend.Idle(0.2f);
                        _friend.PutAway(D.Props);
                    }
                    EndToast();
                    if (ClipLibrary.Has("talk_a"))
                    {
                        _friend.Work("talk_a", D.Props, 0.4f);   // no toast after all: a word instead, as after anything else
                    }
                    break;
            }
        }

        // The player let go again (their glass goes with Body.Rest, theirs with PutAway at the goodbye). Safe at any time.
        private void EndToast()
        {
            if (_toastHeld)
            {
                _toastHeld = false;
                D.Body.Rest(0.3f);
                D.Body.Free();
            }
            if (_toast != Toast.None)
            {
                _toast = Toast.Done;
            }
        }

        // What it cost and what it did, applied once and shown on the card.
        private void Settle(bool wentWell)
        {
            if (_settled)
            {
                return;
            }
            _settled = true;
            float quality = wentWell ? 1f : 0.5f;
            if (_closed)
            {
                quality *= 0.9f;
            }
            float liking = SocialRules.Liking(_who, _kind);
            float familiarity = _def.Familiarity * quality;
            float affection = _def.Affection * liking * quality;
            float trust = _def.Trust * quality;
            int pay = Talk != null ? Talk.Pay : 0;
            if (pay == SceneTalk.Treat)
            {
                affection += 2f;
            }
            if (_covered)
            {
                trust -= 1f;
            }
            if (_crashes > 0)
            {
                trust -= 4f;
            }
            int gift = 0;
            if (wentWell && (_kind == PlanKind.Lift || _kind == PlanKind.Moving) && !_who.Has(SocialTrait.Careful))
            {
                // Petrol money, or something towards a pizza: a favour is thanked.
                gift = _kind == PlanKind.Lift ? 10 + Mathf.RoundToInt(_metres / 100f) : 25;
            }

            SocialStage before = _who.Stage;
            if (!_test)
            {
                SocialLife.Contacted(_who);
                _who.Shared++;
                _who.LastOutingDay = SocialLife.Day;
                if (_who.Together)
                {
                    _who.Dates++;
                }
                SocialLife.Remember(_who, _def.Memory);
                // Next time they fancy something else.
                _who.Wants = (PlanKind)(((int)_who.Wants + 1 + Random.Range(0, 3)) % ((int)PlanKind.Walk + 1));
                _who.WantsDay = SocialLife.Day;
                SocialLife.Change(_who, familiarity, affection, trust);
            }
            if (gift > 0)
            {
                SocialLife.Give(gift, _def.Transaction);
            }
            if (_def.Energy > 0f)
            {
                EnergyHelper.GenerateEnergy(_def.Energy);
            }
            else if (_def.Energy < 0f && SaveGameManager.Current != null && SaveGameManager.Current.Energy > 14f - _def.Energy)
            {
                EnergyHelper.SpentEnergyOnce(-_def.Energy);   // tiring, never dangerous
            }
            ModState.AddModifier(wentWell ? (_who.Together || liking > 1.2f ? ModState.MoodUpBig : ModState.Social) : ModState.MoodDown);
            if (SocialLinks.Wellbeing != null)
            {
                SocialLinks.Wellbeing.Stress(wentWell ? -12f : 4f);
                if (_kind == PlanKind.Meal)
                {
                    SocialLinks.Wellbeing.Ate();
                }
            }

            string after = wentWell ? SocialLines.Pick(_def.After) : SocialLines.Pick(SocialCatalogue.WentBadly);
            Dictionary<string, string> data = SocialLife.Data(_who, "place", _venue.Name);
            data["to"] = _to.Valid ? _to.Name : string.Empty;
            Say(_who, after, data, wentWell ? NotificationType.Success : NotificationType.Info);
            if (wentWell && !_test && _kind <= PlanKind.Business)
            {
                D.ThankLater(_who);
            }

            // The card: built once, here.
            _text.Length = 0;
            if (_paid > 0)
            {
                CardLine("reallife_social_card_cost", "<color=#FF8A70>-" + SocialLife.Money(_paid) + "</color>");
            }
            if (pay == SceneTalk.Theirs || _covered)
            {
                CardLine("reallife_social_card_cost", SocialLife.T("reallife_social_card_they_paid"));
            }
            if (gift > 0)
            {
                CardLine("reallife_social_card_thanks", "<color=#7CE08A>+" + SocialLife.Money(gift) + "</color>");
            }
            if (_def.Energy != 0f)
            {
                CardLine("reallife_social_card_energy", Signed(_def.Energy));
            }
            CardLine("reallife_social_card_mood", SocialLife.T(wentWell ? "reallife_social_card_mood_up" : "reallife_social_card_mood_down"));
            if (!_test)
            {
                CardLine("reallife_social_card_familiarity", Signed(familiarity));
                CardLine("reallife_social_card_affection", Signed(affection));
                CardLine("reallife_social_card_trust", Signed(trust));
                if (_lateBy > SocialRules.LateMinutes && !_who.Has(SocialTrait.Late))
                {
                    _text.Append(SocialLife.T("reallife_social_card_late")).Append('\n');
                }
                _text.Append('\n').Append(SocialLife.T(_who.Stage != before ? "reallife_social_card_now" : "reallife_social_card_still")).Append("   <b>")
                    .Append(SocialLife.StageName(_who)).Append("</b>");
            }
            else
            {
                _text.Append('\n').Append(SocialLife.T("reallife_social_card_test"));
            }
            Dictionary<string, string> title = SocialLife.Data(_who, "outing", SocialLife.T(_def.Name));
            D.ShowCard(SocialLife.Capital(SocialLife.T("reallife_social_title", title)), _text.ToString());
            SocialDirector.Logger?.Info("Social: " + _def.Kind + " with " + _who.Name + (wentWell ? " went well" : " went badly") + (_closed ? " (the place was shut: a walk)" : string.Empty)
                                        + ": paid " + _paid + ", familiarity " + familiarity.ToString("F1") + ", affection " + affection.ToString("F1") + ", trust "
                                        + trust.ToString("F1") + (_test ? " (test: nothing recorded)." : "."));
        }

        private void CardLine(string key, string value)
        {
            _text.Append(SocialLife.T(key)).Append("   ").Append(value).Append('\n');
        }

        private static string Signed(float value)
        {
            int rounded = Mathf.RoundToInt(value);
            return rounded >= 0 ? "<color=#7CE08A>+" + rounded + "</color>" : "<color=#FF8A70>" + rounded + "</color>";
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
            if (D == null)
            {
                return;
            }
            SocialDirector.Talk = null;
            string reason = WhyText(_cut);
            if (_greeted && !_settled && !_test && why != SceneEnd.Fault)
            {
                // Cut short after they had met: walking out on it costs; being taken away from it does not.
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
            for (int i = 0; i < BoxCount; i++)
            {
                D.Props.Return(_boxes[i]);
                D.Props.Return(_delivered[i]);
                _boxes[i] = null;
                _delivered[i] = null;
            }
            _carrying = -1;
            D.Cast.Release(_friend);
            ReleaseLeaving();
            _friend = null;
            _car = null;
            _skipping = false;
        }
    }
}
