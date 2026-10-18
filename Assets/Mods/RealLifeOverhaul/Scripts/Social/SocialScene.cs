using System.Collections.Generic;
using System.Globalization;
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

    /// <summary>What took the player away from a scene. A door, the street not running and a time skip only hold it up, if it can wait.</summary>
    public enum SceneBreak
    {
        None,
        NoGame,           // no save, no player, no HUD
        Loading,
        PlayerDead,
        Hospital,         // the player is being taken to hospital
        TimeSkip,         // the game's time machine, not started by the scene itself
        JobShift,         // one of the mod's jobs is on
        Indoors,
        StreetNotReady,   // out of doors, but the street has not been running for six seconds (an activity fast-forwarding)
        OutingsOff,
        SocialOff,
        GaveUp,           // held up for longer than whoever was waiting would wait
        TestButton,       // a test button took the street, or "stop" was pressed
        Shutdown          // the city is being closed
    }

    /// <summary>
    /// Something played on the street with one or two people of the cast, ticked by the director five times a second. Its
    /// properties say what to show; <see cref="End"/>, called once on every way out, takes back whatever it put out.
    /// </summary>
    public abstract class SocialScene
    {
        private const float TalkTurn = 6.2f;       // seconds one of the two talks before the other does
        private const float GoneBeyond = 70f;      // metres from the player at which somebody walking off is gone
        private const float LeaveMost = 90f;       // seconds anybody is given to walk off

        protected SocialDirector D;
        protected SceneTalk Talk;

        private bool _talking;
        private bool _talkClosed;
        private float _talkTurnAt;
        private bool _theirTurn;
        private bool _clipsMissing;
        private CityPerson _talkWith;

        private readonly CityPerson[] _leaving = new CityPerson[SocialCast.Size];
        private readonly float[] _leftAt = new float[SocialCast.Size];
        private readonly int[] _legs = new int[SocialCast.Size];

        public string Title { get; protected set; }
        public string Line { get; protected set; }
        public string Status { get; protected set; }
        public string Prompt { get; protected set; }

        /// <summary>The locale key of the word on the prompt's button (the game writes the player's key after it).</summary>
        public string PromptButton { get; protected set; }

        /// <summary>Null: over the player.</summary>
        public Transform PromptOver { get; protected set; }

        public bool HasMarker { get; protected set; }
        public Vector3 MarkerAt { get; protected set; }
        public string MarkerLabel { get; protected set; }

        public bool Over { get; protected set; }

        /// <summary>Why it could not be staged; set by <see cref="Begin"/> when it returns false.</summary>
        public TestRefusal Refusal { get; protected set; } = TestRefusal.NothingFits;
        public string RefusalDetail { get; protected set; }

        /// <summary>True while the scene itself passes the time with the game's time machine: not an interruption.</summary>
        public virtual bool SkippingTime => false;

        /// <summary>True while it only shows where to go: nobody is out yet, and other things may still happen.</summary>
        public virtual bool Waiting => false;

        public bool Start(SocialDirector director, float now)
        {
            D = director;
            return Begin(now);
        }

        protected abstract bool Begin(float now);

        public abstract void Tick(float now);

        /// <summary>The prompt's button was pressed.</summary>
        public virtual void Interact(float now)
        {
        }

        /// <summary>The car the player drives hit something, at this closing speed in m/s.</summary>
        public virtual void OnCrash(float speed)
        {
        }

        /// <summary>Every way out. Must not throw and must be safe in any state.</summary>
        public abstract void End(SceneEnd why);

        /// <summary>True if it can wait: <see cref="Holding"/> is asked in place of the tick until <see cref="Resume"/>. False ends it.</summary>
        public virtual bool Hold(SceneBreak why, float now)
        {
            return false;
        }

        /// <summary>False once whoever was waiting has given up and said so.</summary>
        public virtual bool Holding(SceneBreak why, float now, float held)
        {
            return true;
        }

        public virtual void Resume(SceneBreak why, float now, float held)
        {
        }

        /// <summary>About to be ended from outside: the player is told why, and what becomes of the plan. Must not throw.</summary>
        public virtual void Cut(SceneBreak why)
        {
        }

        /// <summary>What the scene is and how far it got, for the log.</summary>
        public virtual string Describe()
        {
            return GetType().Name;
        }

        /// <summary>The prompt's words have no key in them; the button's word is a locale key.</summary>
        protected void Offer(string words, string button, CityPerson over)
        {
            Prompt = words;
            PromptButton = button;
            PromptOver = over != null && !over.Lost ? over.Character.transform : null;
        }

        /// <summary>The sentence that says what happened, for a notification; null for what is not shown (a load, the game going).</summary>
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
            MarkerAt = at;
            MarkerLabel = label;
        }

        /// <summary>Moves with them on every tick it is called.</summary>
        protected void MarkPerson(CityPerson person, string label)
        {
            if (person == null || person.Lost)
            {
                return;
            }
            HasMarker = true;
            MarkerAt = person.Position;
            MarkerLabel = label;
        }

        protected void Unmark()
        {
            HasMarker = false;
        }

        protected void LogPut(SocialPerson who, CityPerson person, Vector3 goal, string how)
        {
            if (SocialDirector.Logger == null || person == null || person.Lost)
            {
                return;
            }
            Vector3 at = person.Position;
            float off = SocialPlaces.OffMesh(at);
            bool route = SocialCast.Walk(person, goal, out float length, out bool whole);
            SocialDirector.Logger.Info("Social: " + who.Name + " " + how + " at (" + at.x.ToString("F0") + ", " + at.y.ToString("F1", CultureInfo.InvariantCulture) + ", "
                                       + at.z.ToString("F0") + "), " + JobSpots.Flat(at, SocialDirector.Where()).ToString("F0") + " m from the player; navigation mesh "
                                       + (off < 0f ? "NOT within 1 m" : off.ToString("F2", CultureInfo.InvariantCulture) + " m off") + "; walk to the place: "
                                       + (!route ? "no route" : length.ToString("F0") + " m" + (whole ? string.Empty : " (ends short of it)")) + ", "
                                       + JobSpots.Flat(at, goal).ToString("F0") + " m in a straight line.");
        }

        /// <summary>The conversation in the game's own dialogue panel; while it is open they talk in turn (<see cref="TickTalk"/>).</summary>
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
                // An introduction: the one who spoke before listens.
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
                _talkTurnAt = now + TalkTurn;
                bool theirs = _theirTurn;
                _theirTurn = !_theirTurn;
                bool played = with != null && !with.Lost && with.Work(theirs ? "talk_a" : "talk_b", D.Props, 0.4f);
                bool mine = D.Body.Clip(theirs ? "talk_b" : "talk_a", 0.4f);
                if (!played && !mine)
                {
                    _clipsMissing = true;   // no clips to be had: they stand and talk
                }
            }
            return false;
        }

        /// <summary>They walk off out of view and are taken in there. Safe with null.</summary>
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
                    // Still in view (the player came along): they walk on.
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

        /// <summary>The upper body only, so it can be made by somebody about to walk.</summary>
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
}
