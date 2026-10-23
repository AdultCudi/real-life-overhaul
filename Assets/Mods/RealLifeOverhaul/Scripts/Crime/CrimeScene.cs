using System;
using BigAmbitions.Characters;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>What the director hands a scene when it asks it to start.</summary>
    internal struct CrimeSetup
    {
        public Vector3 Player;
        public Vector3 Spot;        // where to build the scene: pavement out of immediate view, or a place indoors
        public CrimePlace Place;
        public bool Night;
        public bool Forced;         // asked for with a test button
        public float Amount;        // money at stake, for a scene about a known sum
    }

    /// <summary>
    /// One crime played out near the player, run by the director four times a second. Begin leaves a running scene or
    /// nothing; End may be called at any time, any number of times, and hands back everything borrowed.
    /// </summary>
    internal interface ICrimeScene
    {
        /// <summary>The id of this scene's entry in CrimeCatalogue.</summary>
        string Id { get; }
        bool Active { get; }
        /// <summary>Where the HUD marker points; follows a chase.</summary>
        Vector3 Focus { get; }
        /// <summary>False where the player should not be pointed at it (they are the victim, or it is beside them).</summary>
        bool ShowsMarker { get; }
        /// <summary>True for a scene inside a building; any other ends when the street goes.</summary>
        bool Indoors { get; }
        CrimeEntry Entry { get; }

        bool Begin(CrimeEntry entry, in CrimeSetup setup, float now);
        void Tick(float now, Vector3 player);
        /// <summary>The player called 911 about it. True if that changes anything.</summary>
        bool Report(float now);
        /// <summary>True while the interaction key near the scene would do something. Asked every frame: a field read.</summary>
        bool Interactive { get; }
        void Interact(float now, Vector3 player);
        void End();
    }

    /// <summary>
    /// What every scene shares: a cast of four, timed steps, the ending rolled from the catalogue, an officer, the arrest,
    /// the player's catch and the clearing up. A scene puts its people out in Stage and moves them on in Run.
    /// </summary>
    internal abstract partial class CrimeScene : ICrimeScene
    {
        protected const float RepathInterval = 0.4f;    // seconds between a chaser's new routes
        protected const float NearDistance = 9f;        // metres
        /// <summary>The player is this far away (metres): nobody is watching, and the scene ends.</summary>
        protected virtual float AbandonDistance => 200f;

        // The game has no hands-up, shouting or shoving animation: these stand in.
        protected const AnimationType Arguing = AnimationType.IDontThinkSo;
        protected const AnimationType Punch = AnimationType.BoxingPunch;
        protected const AnimationType Despair = AnimationType.DefeatStanding;
        protected const AnimationType Reach = AnimationType.PressAction;
        protected const PermanentAnimationType PhonePose = PermanentAnimationType.TalkingPhone;
        protected const PermanentAnimationType TextPose = PermanentAnimationType.TextingPhone;
        protected const PermanentAnimationType DrunkPose = PermanentAnimationType.Drunk;
        protected const string PhoneProp = "PhoneOff";

        // Steps every scene can use; a scene numbers its own from 0.
        protected const int StepArrest = 100;
        protected const int StepLeaving = 110;

        protected readonly ActorRole First = new ActorRole();    // the suspect, by convention
        protected readonly ActorRole Second = new ActorRole();   // the victim, or the other party
        protected readonly ActorRole Third = new ActorRole();    // a bystander, a lookout
        protected readonly ActorRole Cop = new ActorRole();

        protected int Step;
        protected float StepEnd;
        protected Vector3 Scene;
        protected CrimeEnding Ending;
        protected bool Reported;
        protected float NextRepath;

        private float _deadline;
        private bool _reacted;
        private string _lastEnding;
        private string _lastStart;
        private string _lastLine;

        public abstract string Id { get; }
        public bool Active { get; private set; }
        public CrimeEntry Entry { get; private set; }
        public virtual Vector3 Focus => Scene;
        public virtual bool ShowsMarker => true;
        public virtual bool Indoors => false;
        public virtual bool Interactive => false;

        public virtual void Interact(float now, Vector3 player)
        {
        }

        /// <summary>Hard cap in seconds.</summary>
        protected virtual float MaxTime => 115f;

        /// <summary>Puts the cast out and sets the first step. False if it cannot; whoever did turn up is sent home.</summary>
        protected abstract bool Stage(in CrimeSetup setup, float now);

        /// <summary>Moves the scene along, four times a second, after the cast has been ticked.</summary>
        protected abstract void Run(float now, Vector3 player);

        /// <summary>Anything of the scene's own to give back (a held car, a borrowed guard). Called on every way out.</summary>
        protected virtual void Release()
        {
        }

        /// <summary>The player has walked up to it. Called once.</summary>
        protected virtual void OnNear(Vector3 player)
        {
            Say(NotificationType.Info, Entry.Near, 7f);
        }

        public bool Begin(CrimeEntry entry, in CrimeSetup setup, float now)
        {
            End();
            Entry = entry;
            Ending = CrimeDeck.DrawEnding(entry.Endings, _lastEnding, Crime.Random01);
            _lastEnding = Ending != null ? Ending.Name : null;
            Scene = setup.Spot;
            Step = 0;
            Reported = false;
            _reacted = false;
            _arrested = null;
            _held = null;
            _citizen = false;
            _brokeAway = false;
            _caughtSince = 0f;
            _fastRunner = Random.value < 0.25f;
            Catchable = null;
            NextRepath = 0f;
            if (!Stage(setup, now))
            {
                Clear();
                return false;
            }
            Active = true;
            _deadline = now + MaxTime;
            string start = CrimeDeck.DrawText(entry.Starts, _lastStart, Crime.Random01);
            _lastStart = start;
            if (start != null)
            {
                Notifications.Show(NotificationType.Warning, start, null, 8f);
            }
            return true;
        }

        public void Tick(float now, Vector3 player)
        {
            if (!Active)
            {
                return;
            }
            First.Tick(now);
            Second.Tick(now);
            Third.Tick(now);
            Cop.Tick(now);
            if (now >= _deadline || (player - Focus).sqrMagnitude > AbandonDistance * AbandonDistance)
            {
                End();
                return;
            }
            if (!_reacted && Step < StepArrest && Entry.Near.Length > 0 && (player - Focus).sqrMagnitude < NearDistance * NearDistance
                && !Helpers.PlayerHelper.IsUsingVehicle)
            {
                _reacted = true;
                OnNear(player);
            }

            if (Catchable != null && Step < StepArrest && TryCatch(now, player))
            {
                return;
            }
            if (Step >= StepArrest)
            {
                RunShared(now, player);
            }
            else
            {
                Run(now, player);
            }
        }

        /// <summary>By default a call brings an officer on foot if none is there yet.</summary>
        public virtual bool Report(float now)
        {
            if (!Active || Reported)
            {
                return Active;
            }
            Reported = true;
            if (!Cop.Present && !Indoors)
            {
                SendOfficer(Scene, 16f, 26f);
            }
            return true;
        }

        public void End()
        {
            if (!Active && Entry == null)
            {
                return;
            }
            Clear();
        }

        // Over before anything is touched, so a failure here cannot run the scene again; each part goes back even if another fails.
        private void Clear()
        {
            Active = false;
            Entry = null;
            try
            {
                Catchable = null;
                _held = null;
                _arrest.Clear();   // cuffs off before the people go back to the pool
                First.Exit();
                Second.Exit();
                Third.Exit();
                Cop.Exit();
            }
            catch (Exception e)
            {
                Crime.Fault(e);
            }
            try
            {
                Release();
            }
            catch (Exception e)
            {
                Crime.Fault(e);
            }
            try
            {
                CrimeDirector.ReleaseCars();   // whatever the scene did not let go itself
            }
            catch (Exception e)
            {
                Crime.Fault(e);
            }
        }

        /// <summary>One of several lines as a toast, never the one this scene showed last.</summary>
        protected void Say(NotificationType type, string[] lines, float seconds = 7f)
        {
            string line = CrimeDeck.DrawText(lines, _lastLine, Crime.Random01);
            if (line != null)
            {
                _lastLine = line;
                Notifications.Show(type, line, null, seconds);
            }
        }

        /// <summary>Announces the ending rolled at the start, or another one, and thanks a caller for an arrest.</summary>
        protected void Conclude(CrimeEnding ending = null)
        {
            ending = ending ?? Ending;
            if (ending == null)
            {
                return;
            }
            if (ending.Arrest && Entry != null)
            {
                News.Report(NewsKind.Arrest, Scene, Entry.Id);
            }
            Say(NotificationType.Info, ending.Texts, 8f);
            if (ending.Arrest && Reported)
            {
                Crime.ThankCaller();
            }
        }

        /// <summary>Announces a named ending; one this crime does not have is not announced.</summary>
        protected void Conclude(string endingName)
        {
            CrimeEnding ending = Entry != null ? Entry.Ending(endingName) : null;
            if (ending != null)
            {
                Conclude(ending);
            }
        }

        /// <summary>Announces a named ending whose lines carry an {amount}.</summary>
        protected void ConcludeAmount(string endingName, float amount, NotificationType type = NotificationType.Info)
        {
            CrimeEnding ending = Entry != null ? Entry.Ending(endingName) : null;
            string line = ending != null ? CrimeDeck.DrawText(ending.Texts, _lastLine, Crime.Random01) : null;
            if (line != null)
            {
                _lastLine = line;
                Crime.ShowAmount(type, line, amount);
            }
        }

        protected bool Is(string endingName)
        {
            return Ending != null && Ending.Name == endingName;
        }

        protected void Go(int step, float now, float seconds)
        {
            Step = step;
            StepEnd = now + seconds;
        }

        protected void Go(int step, float now, float minSeconds, float maxSeconds)
        {
            Go(step, now, Random.Range(minSeconds, maxSeconds));
        }

        /// <summary>Everyone still out walks off; the scene ends when they are gone or the time is up.</summary>
        protected void Leave(float now, Vector3 player, float within = 28f)
        {
            Catchable = null;
            First.Quiet();
            Second.Quiet();
            Third.Quiet();
            First.Drop();
            Second.Drop();
            Third.Drop();
            Cop.Drop();
            First.WalkAwayFrom(player, Pace.Walk, 30f, within);
            Second.WalkAwayFrom(First.Position, Pace.Walk, 30f, within);
            Third.WalkAwayFrom(player, Pace.Walk, 30f, within);
            Cop.WalkAwayFrom(player, Pace.Walk, 30f, within);
            Go(StepLeaving, now, within + 2f);
        }

        /// <summary>A chaser heads for where its quarry is now, at most a few times a second.</summary>
        protected void Pursue(ActorRole chaser, ActorRole quarry, Pace pace, float now, float shortBy = 1.4f)
        {
            if (now < NextRepath)
            {
                return;
            }
            NextRepath = now + RepathInterval;
            Vector3 target = quarry.Position;
            chaser.Go(Crime.Short(target, chaser.Position, shortBy), pace, target, 10f);
        }

        protected static bool Within(ActorRole a, ActorRole b, float metres)
        {
            return (a.Position - b.Position).sqrMagnitude < metres * metres;
        }

        /// <summary>Somebody runs for it: to a far stretch of pavement, gone as soon as nobody can see them.</summary>
        protected static void Flee(ActorRole who, Vector3 from, float distance = 60f)
        {
            if (!who.Present)
            {
                return;
            }
            if (StreetSpots.AwayFrom(who.Position, from, distance, onRoad: false, out Vector3 far))
            {
                who.WalkOff(far, Pace.Run, 30f);
            }
            else
            {
                who.Exit();
            }
        }

        /// <summary>An officer on foot from down the street, jogging to a point. False if none could be had.</summary>
        protected bool SendOfficer(Vector3 to, float minDistance, float maxDistance)
        {
            if (Cop.Present)
            {
                return true;
            }
            if (!StreetSpots.PavementAround(to, minDistance, maxDistance, true, out Vector3 beat)
                || !Cop.Enter(beat, ActorLook.Officer, 3f, onRoad: false))
            {
                return false;
            }
            Cop.Go(Crime.Short(to, Cop.Position, 2f), Pace.Jog, to, 36f);
            return true;
        }
    }
}
