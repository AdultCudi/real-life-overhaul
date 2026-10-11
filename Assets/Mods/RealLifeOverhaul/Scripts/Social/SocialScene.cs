using System.Collections.Generic;
using Characters.EmojiSystem;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Why a scene stopped.</summary>
    public enum SceneEnd
    {
        Done,          // played to its end
        Left,          // the player walked away from it, or stopped it
        Interrupted,   // something took the player off the street: a door, a time skip, a job, hospital
        Fault          // the code met something unexpected
    }

    /// <summary>
    /// What took the player (or the street) away from a scene: one value per condition, so the
    /// log and the player are told exactly which. Some end a scene; a door, the street not
    /// running and a time skip only hold it up if the scene says it can wait.
    /// </summary>
    public enum SceneBreak
    {
        None,
        NoGame,           // no save, no player, no HUD: the game is going or not there yet
        Loading,          // a load is under way
        PlayerDead,
        Hospital,         // the player is being taken to hospital
        TimeSkip,         // the game's time machine is running (not started by the scene itself)
        JobShift,         // one of the mod's jobs is on
        Indoors,          // the player is inside a building
        StreetNotReady,   // out of doors, but the street has not been running for six seconds (an activity fast-forwarding)
        OutingsOff,       // the option was switched off
        SocialOff,        // the whole social life was switched off
        GaveUp,           // held up for longer than whoever was waiting would wait
        TestButton,       // a test button took the street, or "stop" was pressed
        Shutdown          // the city is being closed
    }

    /// <summary>
    /// Something played on the street with one or two people of the cast: a list of steps the
    /// director ticks five times a second and does nothing between. It says what to show (who
    /// it is with, what to do next, where, which key) by setting the fields below, only when
    /// they change. Whatever it puts on the street or on the player it takes back in
    /// <see cref="End"/>, which the director calls exactly once on every way a scene can stop.
    /// </summary>
    public abstract class SocialScene
    {
        private const float TalkTurn = 6.2f;       // seconds one of the two talks before the other does
        private const float GoneBeyond = 70f;      // metres from the player at which somebody walking off is simply gone
        private const float LeaveMost = 90f;       // seconds anybody is given to walk off

        protected SocialDirector D;

        // ------------------------------------------------ what the director shows; set only when it changes
        public string Title { get; protected set; }
        public string Line { get; protected set; }
        public string Status { get; protected set; }
        public string Prompt { get; protected set; }

        /// <summary>The locale key of the word on the prompt's button (the game writes the player's key after it).</summary>
        public string PromptButton { get; protected set; }

        /// <summary>The person the prompt's panel stands over; null: over the player.</summary>
        public Transform PromptOver { get; protected set; }

        /// <summary>Offers the prompt: its words (no key in them), the button's word and the person it is about, if one is out.</summary>
        protected void Offer(string words, string button, CityPerson over)
        {
            Prompt = words;
            PromptButton = button;
            PromptOver = over != null && !over.Lost ? over.Character.transform : null;
        }
        public bool HasMarker { get; protected set; }
        public Vector3 MarkerAt { get; protected set; }
        public string MarkerLabel { get; protected set; }

        /// <summary>True while the marker is on somebody rather than on a place: it moves, and is shown by the mod's own marker only.</summary>
        public bool MarkerOnPerson { get; protected set; }

        public bool Over { get; protected set; }

        /// <summary>Why it could not be staged; set by <see cref="Begin"/> when it returns false.</summary>
        public TestRefusal Refusal { get; protected set; } = TestRefusal.NothingFits;
        public string RefusalDetail { get; protected set; }

        /// <summary>True while the scene itself is passing the time with the game's time machine: not an interruption.</summary>
        public virtual bool SkippingTime => false;

        /// <summary>True for a scene that goes on while the player is at the wheel.</summary>
        public virtual bool Drives => false;

        /// <summary>True while it only shows where to go: nobody is out yet, and other things may still happen.</summary>
        public virtual bool Waiting => false;

        public bool Start(SocialDirector director, float now)
        {
            D = director;
            return Begin(now);
        }

        protected abstract bool Begin(float now);

        /// <summary>Five times a second while the scene is on.</summary>
        public abstract void Tick(float now);

        /// <summary>The interaction key, pressed while <see cref="Prompt"/> is shown.</summary>
        public virtual void Interact(float now)
        {
        }

        /// <summary>The car the player is driving hit something, at this closing speed in m/s.</summary>
        public virtual void OnCrash(float speed)
        {
        }

        /// <summary>Every way out. Must not throw and must be safe in any state.</summary>
        public abstract void End(SceneEnd why);

        /// <summary>
        /// Something holds the scene up (a door, a time skip, the street not running). True if it
        /// can wait: it is then not ticked, <see cref="Holding"/> is asked instead, and
        /// <see cref="Resume"/> is called when the street is the player's again. False ends it
        /// (<see cref="Cut"/> is called first). While held the director does not know where the
        /// player is on the street: ask <see cref="SocialDirector.Where"/>.
        /// </summary>
        public virtual bool Hold(SceneBreak why, float now)
        {
            return false;
        }

        /// <summary>Five times a second while held. False once whoever is waiting has given up: the scene has told the player, and is ended.</summary>
        public virtual bool Holding(SceneBreak why, float now, float held)
        {
            return true;
        }

        public virtual void Resume(SceneBreak why, float now, float held)
        {
        }

        /// <summary>
        /// The scene is about to be ended by something that is not its own doing. If the player had
        /// been told about it, this is where they are told why and what becomes of the plan, in
        /// one notification. Must not throw.
        /// </summary>
        public virtual void Cut(SceneBreak why)
        {
        }

        /// <summary>What the scene is and how far it had got, for the log line that says why it stopped.</summary>
        public virtual string Describe()
        {
            return GetType().Name;
        }

        /// <summary>The sentence that says what happened, for a notification; null for what cannot be shown (a load, the game going).</summary>
        protected static string WhyText(SceneBreak why)
        {
            switch (why)
            {
                case SceneBreak.PlayerDead: return SocialLife.T("reallife_social_why_dead");
                case SceneBreak.Hospital: return SocialLife.T("reallife_social_why_hospital");
                case SceneBreak.TimeSkip: return SocialLife.T("reallife_social_why_time_skip");
                case SceneBreak.JobShift: return SocialLife.T("reallife_social_why_shift");
                case SceneBreak.Indoors: return SocialLife.T("reallife_social_why_indoors");
                case SceneBreak.StreetNotReady: return SocialLife.T("reallife_social_why_street");
                case SceneBreak.OutingsOff: return SocialLife.T("reallife_social_why_outings_off");
                case SceneBreak.SocialOff: return SocialLife.T("reallife_social_why_social_off");
                default: return null;   // a load, the game going, a test button, their own giving up (which they say themselves)
            }
        }

        protected void Mark(Vector3 at, string label)
        {
            HasMarker = true;
            MarkerOnPerson = false;
            MarkerAt = at;
            MarkerLabel = label;
        }

        /// <summary>The marker on somebody who is out: it goes with them, moved on every tick this is called.</summary>
        protected void MarkPerson(CityPerson person, string label)
        {
            if (person == null || person.Lost)
            {
                return;
            }
            HasMarker = true;
            MarkerOnPerson = true;
            MarkerAt = person.Position;
            MarkerLabel = label;
        }

        protected void Unmark()
        {
            HasMarker = false;
            MarkerOnPerson = false;
        }

        /// <summary>
        /// One line in the log for somebody put on the street: where, how far from the player, how
        /// far that point is from the navigation mesh, and the walk they have to make.
        /// </summary>
        protected void LogPut(SocialPerson who, CityPerson person, Vector3 goal, string how)
        {
            if (SocialDirector.Logger == null || person == null || person.Lost)
            {
                return;
            }
            Vector3 at = person.Position;
            float off = SocialPlaces.OffMesh(at);
            bool route = SocialCast.Walk(person, goal, out float length, out bool whole);
            SocialDirector.Logger.Info("Social: " + who.Name + " " + how + " at (" + at.x.ToString("F0") + ", " + at.y.ToString("F1") + ", " + at.z.ToString("F0") + "), "
                                       + JobSpots.Flat(at, SocialDirector.Where()).ToString("F0") + " m from the player; navigation mesh "
                                       + (off < 0f ? "NOT within 1 m" : off.ToString("F2") + " m off") + "; walk to the place: "
                                       + (!route ? "no route" : length.ToString("F0") + " m" + (whole ? string.Empty : " (ends short of it)")) + ", "
                                       + JobSpots.Flat(at, goal).ToString("F0") + " m in a straight line.");
        }

        // ---------------------------------------------------------------- talking, face to face

        protected SceneTalk Talk;
        private bool _talking;
        private bool _talkClosed;
        private float _talkTurnAt;
        private bool _theirTurn;
        private bool _clipsMissing;
        private CityPerson _talkWith;

        protected bool Conversing => _talking;

        /// <summary>
        /// Opens the conversation in the game's own dialogue panel. Both turn to each other first;
        /// while it is open they talk and listen in turn (<see cref="TickTalk"/>).
        /// </summary>
        protected void Converse(SceneTalk talk, CityPerson with, float now)
        {
            Talk = talk;
            SocialDirector.Talk = talk;
            _talking = true;
            _talkClosed = false;
            _theirTurn = true;
            _talkTurnAt = 0f;
            _talkWith = with;
            Prompt = null;
            if (with != null && !with.Lost)
            {
                with.Stand();
                with.Face(D.PlayerPosition);
                D.Body.Turn(D, with.Position);
            }
            D.Body.Hold();
            SocialPhone.OpenInPerson(with != null && !with.Lost ? with.Character : null, () => _talkClosed = true);
        }

        /// <summary>Keeps the two of them animated while the panel is open. True once, when it has closed.</summary>
        protected bool TickTalk(float now, CityPerson with)
        {
            if (!_talking)
            {
                return false;
            }
            if (!ReferenceEquals(with, _talkWith))
            {
                // Somebody else has taken up the conversation (an introduction): the one who spoke before stands and listens.
                if (_talkWith != null && !_talkWith.Lost)
                {
                    _talkWith.Idle();
                }
                _talkWith = with;
                _talkTurnAt = 0f;
                _theirTurn = true;
            }
            if (_talkClosed || !SocialPhone.DialogOpen)
            {
                _talking = false;
                SocialDirector.Talk = null;
                D.Body.Rest();
                D.Body.Free();
                if (with != null && !with.Lost)
                {
                    with.Idle();
                }
                _talkWith = null;
                return true;
            }
            if (now >= _talkTurnAt && !_clipsMissing)
            {
                // Whoever talks uses their hands; the other listens. Nobody gestures at nobody: both are here, facing.
                _talkTurnAt = now + TalkTurn;
                bool theirs = _theirTurn;
                _theirTurn = !_theirTurn;
                bool played = with != null && !with.Lost && with.Work(theirs ? "talk_a" : "talk_b", D.Props, 0.4f);
                bool mine = D.Body.Clip(theirs ? "talk_b" : "talk_a", 0.4f);
                if (!played && !mine)
                {
                    _clipsMissing = true;   // no clips to be had: they stand and talk, and nothing is faked
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- people walking off

        private readonly CityPerson[] _leaving = new CityPerson[SocialCast.Size];
        private readonly float[] _leftAt = new float[SocialCast.Size];
        private readonly int[] _legs = new int[SocialCast.Size];

        /// <summary>Somebody walks off to pavement out of view and is taken in there. Safe with null.</summary>
        protected void Depart(CityPerson person, float now)
        {
            if (person == null)
            {
                return;
            }
            if (person.Lost)
            {
                D.Cast.Release(person);
                return;
            }
            for (int i = 0; i < _leaving.Length; i++)
            {
                if (_leaving[i] == person)
                {
                    return;
                }
            }
            for (int i = 0; i < _leaving.Length; i++)
            {
                if (_leaving[i] == null)
                {
                    _leaving[i] = person;
                    _leftAt[i] = now;
                    _legs[i] = 0;
                    person.StopOver();
                    person.PutAway(D.Props);
                    Walk(person);
                    return;
                }
            }
            D.Cast.Release(person);
        }

        private static void Walk(CityPerson person)
        {
            if (SocialPlaces.WalkOff(person.Position, out Vector3 to))
            {
                person.WalkTo(to);
            }
        }

        /// <summary>True while somebody is still walking off.</summary>
        protected bool TickLeaving(float now)
        {
            bool any = false;
            for (int i = 0; i < _leaving.Length; i++)
            {
                CityPerson person = _leaving[i];
                if (person == null)
                {
                    continue;
                }
                if (person.Lost)
                {
                    D.Cast.Release(person);
                    _leaving[i] = null;
                    continue;
                }
                float away = JobSpots.Flat(person.Position, D.PlayerPosition);
                bool seen = person.Character.visible && CitySpots.InView(person.Position) && away < GoneBeyond;
                if (!seen || now - _leftAt[i] > LeaveMost)
                {
                    D.Cast.Release(person);
                    _leaving[i] = null;
                    continue;
                }
                any = true;
                if (now - _leftAt[i] > 2f + _legs[i] * 12f && person.Arrived && _legs[i] < 4)
                {
                    // Still in view where they got to (the player came along): they keep walking.
                    _legs[i]++;
                    Walk(person);
                }
            }
            return any;
        }

        protected void ReleaseLeaving()
        {
            for (int i = 0; i < _leaving.Length; i++)
            {
                D.Cast.Release(_leaving[i]);
                _leaving[i] = null;
            }
        }

        // ---------------------------------------------------------------- small things every scene does

        /// <summary>A wave, a thank-you: the upper body only, so it can be made by somebody about to walk.</summary>
        protected static void Gesture(CityPerson person, CityProps props, string clip)
        {
            if (person != null && !person.Lost && ClipLibrary.Has(clip))
            {
                person.WorkOver(clip, props, 0.2f, ClipEnd.Return, BodyMask.Upper, false);
            }
        }

        protected void Say(SocialPerson who, string key, Dictionary<string, string> data = null, NotificationType type = NotificationType.Info)
        {
            if (key == null)
            {
                return;
            }
            data = data ?? SocialLife.Data(who);
            SocialLife.Said(who, key, data);
            SocialLife.Toast(type, key, data, 8f);
        }
    }

    /// <summary>
    /// Somebody walks up to the player in the street: a stranger who would like a word (met at
    /// the gym, at work, at the coffee cart, next door, after being helped, in the shop), a
    /// friend bringing somebody to be introduced, or a friend who was passing. They come in on
    /// foot from out of view, stop in front of the player, wave, and wait to be spoken to. The
    /// conversation is the game's own dialogue panel, with the two of them facing and talking;
    /// swapping numbers puts the stranger in the phone. Then they walk off.
    /// </summary>
    public sealed class MeetScene : SocialScene
    {
        public enum Kind { Stranger, Introduction, DropBy }

        private enum Step { Approach, Offer, Talk, After, Leaving }

        private const float StopAt = 2.3f;         // metres from the player they stop at
        private const float TalkWithin = 3.4f;
        private const float ApproachMost = 75f;    // seconds they keep coming
        private const float Patience = 45f;        // seconds they wait to be spoken to (and outside a door the player went through)
        private const float WalkMost = 45f;        // metres of route they walk in by at most

        private readonly Kind _kind;
        private readonly SocialPerson _who;        // the one the conversation is with: the stranger, or the friend who dropped by
        private readonly SocialPerson _friend;     // who brings them, for an introduction
        private readonly bool _atShop;             // a drop-by outside the player's own shop
        private CityPerson _a;                     // _who
        private CityPerson _b;                     // _friend
        private Step _step;
        private float _stepFrom;
        private float _startedAt;
        private float _nextAim;
        private Vector3 _aim;
        private bool _waved;
        private string _label;                     // what the marker on them says
        private string _heldLine;

        public MeetScene(Kind kind, SocialPerson who, SocialPerson friend = null, bool atShop = false)
        {
            _kind = kind;
            _who = who;
            _friend = friend;
            _atShop = atShop;
        }

        public SocialPerson Who => _who;

        protected override bool Begin(float now)
        {
            if (!D.OnFoot)
            {
                Refusal = TestRefusal.OnFootOnly;
                return false;
            }
            Vector3 player = D.PlayerPosition;
            if (!CitySpots.Pavement(player, 5f, out Vector3 here))
            {
                Refusal = TestRefusal.NoPavement;
                return false;
            }
            if (!D.Cast.HasRoom)
            {
                Refusal = TestRefusal.PoolBusy;
                return false;
            }
            // From out of view, by a route that was measured: the whole way, and no more than WalkMost metres of it.
            if (!SocialPlaces.WalkIn(here, player, WalkMost, out Vector3 from, out _))
            {
                Refusal = TestRefusal.NoWayIn;
                return false;
            }
            _a = D.Cast.Put(_who, from, here);
            if (_a == null)
            {
                Refusal = TestRefusal.PoolBusy;
                return false;
            }
            if (!SocialCast.Walk(_a, here, out float length, out bool whole) || !whole || length > WalkMost * 1.3f)
            {
                // Their own agent does not get there by a short way after all: not staged from here (asked again later).
                D.Cast.Release(_a);
                _a = null;
                Refusal = TestRefusal.NoWayIn;
                RefusalDetail = "the walk from the place found is " + (whole ? length.ToString("F0") + " m" : "not possible all the way");
                return false;
            }
            if (_kind == Kind.Introduction && _friend != null)
            {
                // The friend comes with them, a step to one side; without room for two the stranger comes alone.
                Vector3 beside = from + Vector3.Cross(Vector3.up, (here - from).normalized) * 1.1f;
                if (CitySpots.Pavement(beside, 1.2f, out Vector3 second) && CitySpots.ClearQuietly(second, 0.35f) && CitySpots.Reachable(second, here))
                {
                    _b = D.Cast.Put(_friend, second, here);
                }
            }
            _startedAt = now;
            Go(Step.Approach, now);
            _label = _kind == Kind.Stranger ? SocialLife.T("reallife_social_marker_somebody") : SocialLife.First(_friend ?? _who);
            Title = _kind == Kind.Stranger ? SocialLife.T("reallife_social_meet_title") : SocialLife.T("reallife_social_with", SocialLife.Data(_friend ?? _who));
            Line = SocialLife.T(_kind == Kind.Stranger ? "reallife_social_meet_coming" : _kind == Kind.DropBy ? "reallife_social_dropby_coming" : "reallife_social_intro_coming",
                SocialLife.Data(_friend ?? _who));
            Aim(now, true);
            MarkPerson(_a, _label);
            LogPut(_who, _a, here, "is walking up to the player (" + _kind + (_b != null ? ", with " + _friend.Name : string.Empty) + "): put out of view");
            return true;
        }

        private void Go(Step step, float now)
        {
            _step = step;
            _stepFrom = now;
        }

        // Where they are heading: a point just short of the player, on the pavement. Asked again as the player moves.
        private void Aim(float now, bool force)
        {
            if (!force && now < _nextAim)
            {
                return;
            }
            _nextAim = now + 1f;
            Vector3 player = D.PlayerPosition;
            Vector3 towards = _a.Position - player;
            towards.y = 0f;
            Vector3 want = towards.sqrMagnitude > 0.5f ? player + towards.normalized * (StopAt - 0.3f) : player;
            if (!CitySpots.Pavement(want, 2.5f, out Vector3 goal))
            {
                return;   // the player is off the pavement: they keep to the last place they could get to
            }
            if (!force && JobSpots.Flat(goal, _aim) < 0.7f)
            {
                return;
            }
            _aim = goal;
            _a.WalkTo(goal);
            if (_b != null && !_b.Lost)
            {
                Vector3 side = Vector3.Cross(Vector3.up, towards.sqrMagnitude > 0.01f ? towards.normalized : Vector3.forward) * 1.0f;
                if (CitySpots.Pavement(goal + side + towards.normalized * 0.5f, 1.5f, out Vector3 second))
                {
                    _b.WalkTo(second);
                }
            }
        }

        public override void Tick(float now)
        {
            if (_step == Step.Leaving)
            {
                // Nobody is ours to ask about any more: only the walking off is left.
                if (!TickLeaving(now))
                {
                    Over = true;
                }
                return;
            }
            if (_a == null || _a.Lost)
            {
                Over = true;   // knocked down or gone: nothing more happens here
                return;
            }
            Vector3 player = D.PlayerPosition;
            float gap = JobSpots.Flat(_a.Position, player);
            if (_step == Step.Approach || _step == Step.Offer)
            {
                MarkPerson(_a, _label);   // the marker is on them from the moment they are out until they are spoken to
            }
            switch (_step)
            {
                case Step.Approach:
                    if (now - _startedAt > ApproachMost || !D.OnFoot)
                    {
                        GiveUp(now);
                        return;
                    }
                    if (gap <= StopAt + 0.4f || (_a.Arrived && gap < TalkWithin))
                    {
                        Arrive(now);
                        return;
                    }
                    // Somebody whose route has ended short of the player is sent on again, not left standing out of view.
                    Aim(now, _a.Arrived && now >= _nextAim);
                    break;
                case Step.Offer:
                    if (gap > TalkWithin + 2.5f)
                    {
                        // The player moved on: they come after, for as long as their patience lasts.
                        Prompt = null;
                        if (now - _stepFrom > Patience)
                        {
                            GiveUp(now);
                            return;
                        }
                        Aim(now, false);
                        if (_a.Arrived && gap < TalkWithin)
                        {
                            Arrive(now);
                        }
                        break;
                    }
                    if (now - _stepFrom > Patience || !D.OnFoot)
                    {
                        GiveUp(now);
                        return;
                    }
                    if (Prompt == null)
                    {
                        Offer(SocialLife.Say(_kind == Kind.Stranger ? "reallife_social_meet_prompt" : "reallife_social_talk_prompt", SocialLife.Data(_friend ?? _who)),
                            "reallife_social_button_talk", _a);
                    }
                    break;
                case Step.Talk:
                    if (TickTalk(now, _kind == Kind.Introduction && _b != null && !_b.Lost && Talk != null && !Talk.PersonSpeaks ? _b : _a))
                    {
                        Outcome(now);
                    }
                    break;
                case Step.After:
                    if (now - _stepFrom > 1.6f)
                    {
                        Depart(_a, now);
                        Depart(_b, now);
                        _a = null;
                        _b = null;
                        Line = null;
                        Title = null;
                        Go(Step.Leaving, now);
                    }
                    break;
            }
        }

        private void Arrive(float now)
        {
            _a.Stand();
            _a.Face(D.PlayerPosition);
            if (_b != null && !_b.Lost)
            {
                _b.Stand();
                _b.Face(D.PlayerPosition);
            }
            if (!_waved)
            {
                _waved = true;
                Gesture(_kind == Kind.Introduction && _b != null ? _b : _a, D.Props, "wave_hello");
                if (_kind == Kind.DropBy)
                {
                    Say(_who, SocialLines.Pick(SocialCatalogue.DropBy));
                }
            }
            Line = SocialLife.T(_kind == Kind.Stranger ? "reallife_social_meet_waiting" : "reallife_social_friend_waiting", SocialLife.Data(_friend ?? _who));
            if (_step != Step.Offer)
            {
                Go(Step.Offer, now);
            }
        }

        public override void Interact(float now)
        {
            if (_step != Step.Offer || Prompt == null || _a == null || _a.Lost)
            {
                return;
            }
            bool brought = _kind == Kind.Introduction && _b != null && !_b.Lost;   // the friend is here to make the introduction
            var talk = new SceneTalk { Person = _who, Introducer = brought ? _friend : null, Mode = _kind == Kind.DropBy ? SceneTalk.DropBy : brought ? SceneTalk.Introduction : SceneTalk.Meet };
            if (_kind == Kind.DropBy)
            {
                bool shop = SocialTalk.Shop(out string name, out float sales, out _);
                string[] pool = !_atShop ? SocialCatalogue.DropByStreet : shop && sales > 0f ? SocialCatalogue.DropByShopGood : SocialCatalogue.DropByShopQuiet;
                talk.Opener = SocialLines.Pick(pool);
                talk.OpenerData = SocialLife.Data(_who, "shop", name ?? SocialLife.T("reallife_social_your_shop"));
                talk.OpenerData["amount"] = SocialLife.Money(sales);
                talk.GoLabel = "reallife_social_btn_good_to_see";
            }
            else
            {
                string[] pool = _kind == Kind.Introduction ? null : SocialCatalogue.MeetOpen[(int)_who.Origin];
                talk.Opener = pool != null ? SocialLines.Pick(pool) : "reallife_social_intro_hello";
                talk.OpenerData = SocialLife.Data(_who);
            }
            if (_b != null && !_b.Lost)
            {
                _b.Face(D.PlayerPosition);
            }
            Unmark();
            Converse(talk, _kind == Kind.Introduction && _b != null && !_b.Lost ? _b : _a, now);
            Go(Step.Talk, now);
        }

        private void Outcome(float now)
        {
            SceneTalk talk = Talk;
            Line = null;
            if (_kind == Kind.DropBy)
            {
                SocialLife.Contacted(_who);
                SocialLife.Remember(_who, MemoryCode.DroppedBy);
                _who.Shared++;
                SocialLife.Change(_who, 2f, talk != null && talk.Chatted ? 1f : 2f, 1f);
                SocialState.LastDropByDay = SocialLife.Day;
                D.Cast.Emoji(_a, CharacterEmojiName.CommonGreenHappy, 3f);
                Say(_who, SocialLines.Pick(_who.Together ? SocialCatalogue.PartingLove : SocialCatalogue.Parting));
            }
            else if (talk != null && talk.Swapped)
            {
                SocialLife.Meet(_who, talk.Warm);
                D.Cast.Emoji(_a, CharacterEmojiName.CommonGreenHappy, 3f);
                if (_friend != null)
                {
                    SocialLife.Contacted(_friend);
                    SocialLife.Remember(_friend, MemoryCode.Introduced);
                    SocialLife.Change(_friend, 1f, 2f, 1f);
                }
            }
            else if (talk != null && talk.Brushed)
            {
                SocialState.LastMeetDay = SocialLife.Day;   // nobody else tries for a while either
                D.Cast.Emoji(_a, CharacterEmojiName.CommonYellowNeutral, 3f);
                SocialLife.Toast(NotificationType.Info, SocialLines.Pick(SocialCatalogue.MeetBrushed), null, 6f);
                if (_friend != null)
                {
                    SocialLife.Change(_friend, 0f, SocialRules.RudeAffection, 0f);
                    SocialLife.Remember(_friend, MemoryCode.Rude);
                }
            }
            else
            {
                SocialState.LastMeetDay = SocialLife.Day;
                SocialLife.Toast(NotificationType.Info, "reallife_social_meet_no_numbers", null, 6f);
            }
            Gesture(_a, D.Props, ClipLibrary.Has("job_thank") ? "job_thank" : "wave_hello");
            Go(Step.After, now);
        }

        private void GiveUp(float now)
        {
            Prompt = null;
            Line = null;
            Unmark();
            ToldGone();
            SocialDirector.Logger?.Info("Social: " + _who.Name + " gave up (" + _step + " for " + (now - _stepFrom).ToString("F0") + " s, " + (D.OnFoot ? "the player on foot" : "the player in a vehicle")
                                        + ", " + JobSpots.Flat(_a.Position, D.PlayerPosition).ToString("F0") + " m apart) and walked off.");
            Go(Step.After, now - 2f);
        }

        // The player had been told somebody was coming over: they are told that they have gone, too.
        private void ToldGone()
        {
            if (_kind == Kind.Stranger)
            {
                SocialLife.Toast(NotificationType.Info, SocialLines.Pick(SocialCatalogue.MeetGaveUp), null, 6f);
            }
            else
            {
                SocialLife.Toast(NotificationType.Info, "reallife_social_meet_missed", SocialLife.Data(_friend ?? _who), 7f);
            }
        }

        // ---------------------------------------------------------------- held up: a door, the street not running

        public override bool Hold(SceneBreak why, float now)
        {
            if ((_step != Step.Approach && _step != Step.Offer) || why == SceneBreak.TimeSkip || _a == null || _a.Lost)
            {
                return false;
            }
            // They wait outside the door for as long as they would have waited to be spoken to.
            Prompt = null;
            _a.Stand();
            if (_b != null && !_b.Lost)
            {
                _b.Stand();
            }
            _heldLine = Line;
            Line = SocialLife.T(_kind == Kind.Stranger ? "reallife_social_meet_held" : "reallife_social_obj_held", SocialLife.Data(_friend ?? _who));
            return true;
        }

        public override bool Holding(SceneBreak why, float now, float held)
        {
            if (held <= Patience)
            {
                return true;
            }
            ToldGone();
            SocialDirector.Logger?.Info("Social: " + _who.Name + " gave up waiting outside after " + held.ToString("F0") + " s (" + why + ").");
            return false;
        }

        public override void Resume(SceneBreak why, float now, float held)
        {
            Line = _heldLine;
            _heldLine = null;
            _startedAt += held;
            _stepFrom += held;
            if (_a != null && !_a.Lost && _step == Step.Approach)
            {
                Aim(now, true);
            }
        }

        public override void Cut(SceneBreak why)
        {
            string reason = WhyText(why);
            if (reason != null && (_step == Step.Approach || _step == Step.Offer))
            {
                SocialLife.Toast(NotificationType.Info, "reallife_social_meet_cut", SocialLife.Data(_friend ?? _who, "why", reason), 8f);
            }
        }

        public override string Describe()
        {
            return "meeting (" + _kind + ") with " + _who.Name + ", step " + _step;
        }

        public override void End(SceneEnd why)
        {
            if (D == null)
            {
                return;
            }
            SocialDirector.Talk = null;
            D.Cast.Release(_a);
            D.Cast.Release(_b);
            ReleaseLeaving();
            _a = null;
            _b = null;
        }
    }
}
