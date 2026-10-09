using System;
using BigAmbitions.Characters;
using GleyTrafficSystem;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>What the director hands a scene when it asks it to start.</summary>
    internal struct CrimeSetup
    {
        public Vector3 Player;
        public Vector3 Spot;        // a point the scene can be built around: pavement out of immediate view, or a place indoors
        public CrimePlace Place;
        public bool Night;
        public bool Forced;         // asked for with a test button
        public float Amount;        // money at stake, for the scenes that are about a known sum
    }

    /// <summary>
    /// One crime played out near the player. The director starts at most one at a time, ticks it
    /// four times a second and ends it; a scene never runs by itself. Begin either leaves a
    /// running scene behind or nothing at all, and End may be called at any moment, any number
    /// of times, and always hands back every person, car and borrowed game object.
    /// </summary>
    internal interface ICrimeScene
    {
        /// <summary>The id of this scene's entry in CrimeCatalogue.</summary>
        string Id { get; }
        bool Active { get; }
        /// <summary>Where the HUD marker points; follows a chase.</summary>
        Vector3 Focus { get; }
        /// <summary>False for scenes the player should not be pointed at (they are the victim, or it is right beside them).</summary>
        bool ShowsMarker { get; }
        /// <summary>True for scenes played inside a building; all others end when the street goes away.</summary>
        bool Indoors { get; }
        CrimeEntry Entry { get; }

        bool Begin(CrimeEntry entry, in CrimeSetup setup, float now);
        void Tick(float now, Vector3 player);
        /// <summary>The player called 911 about it. True if that changes anything (police come, or come sooner).</summary>
        bool Report(float now);
        /// <summary>True while pressing the interaction key near the scene would do something. Asked every frame; must be a field read.</summary>
        bool Interactive { get; }
        /// <summary>The player pressed the interaction key while the scene was interactive.</summary>
        void Interact(float now, Vector3 player);
        void End();
    }

    /// <summary>
    /// What every scene shares: a cast of up to four people, a step counter with a timer, the
    /// ending rolled from the catalogue, the lines that announce it, an officer who can be sent
    /// for, an arrest that ends in a patrol car or a walk round the corner, and the clearing up.
    /// A scene is a class with a Stage method (put the people out) and a Run method (a switch
    /// over its own steps).
    /// </summary>
    internal abstract class CrimeScene : ICrimeScene
    {
        protected const float RepathInterval = 0.4f;    // how often a chaser asks for a new route
        protected const float NearDistance = 9f;
        /// <summary>The player is this far away: nobody is watching, and the scene ends.</summary>
        protected virtual float AbandonDistance => 200f;
        private const float CatchReach = 1.7f;          // metres: the player has hold of a runner this close
        private const float LetGoDistance = 7f;         // ...and has let them go by walking this far away
        private const float BreakAwayChance = 0.4f;     // of people the player is holding: one try each

        // The game has no hands-up, shouting or shoving animation. These are the stand-ins.
        protected const AnimationType Arguing = AnimationType.IDontThinkSo;
        protected const AnimationType Punch = AnimationType.BoxingPunch;
        protected const AnimationType Despair = AnimationType.DefeatStanding;
        protected const AnimationType Reach = AnimationType.PressAction;
        protected const PermanentAnimationType PhonePose = PermanentAnimationType.TalkingPhone;
        protected const PermanentAnimationType TextPose = PermanentAnimationType.TextingPhone;
        protected const PermanentAnimationType DrunkPose = PermanentAnimationType.Drunk;
        protected const string PhoneProp = "PhoneOff";

        // Steps every scene can use; a scene numbers its own from 0 upwards.
        protected const int StepArrest = 100;
        private const int StepHold = 104;
        private const int StepBreak = 105;
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
        private ActorRole _arrested;
        private ActorRole _held;
        private readonly ArrestSequence _arrest = new ArrestSequence();
        private bool _arrestTold;
        private bool _citizen;
        private bool _brokeAway;
        private bool _fastRunner;
        private float _breakAt;
        private float _caughtSince;

        /// <summary>Somebody running whom the player could stop by getting to them; null when nobody is. Set with RunFor.</summary>
        protected ActorRole Catchable;
        private string _arrestEnding;

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

        /// <summary>Hard cap in seconds: every scene is gone inside two minutes unless it says otherwise.</summary>
        protected virtual float MaxTime => 115f;

        /// <summary>Puts the cast out and sets the first step. False if it cannot be done; whoever did turn up is sent home by the caller.</summary>
        protected abstract bool Stage(in CrimeSetup setup, float now);

        /// <summary>Moves the scene along. Called four times a second, after the cast has been ticked.</summary>
        protected abstract void Run(float now, Vector3 player);

        /// <summary>Anything of the scene's own to give back (a held car, a borrowed customer). Called on every way out.</summary>
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

        /// <summary>
        /// By default a call brings an officer on foot if none is in the scene yet. Scenes with
        /// their own idea of what police do (a pursuit, an indoor theft) override this.
        /// </summary>
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

        // Marked over before anything is touched, so that no failure in here can make the scene run again.
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

        // ---------------------------------------------------------------- telling the player

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

        /// <summary>Announces the ending rolled at the start (or another one the scene turned into) and thanks a caller for an arrest.</summary>
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

        protected void Conclude(string endingName)
        {
            CrimeEnding ending = Entry != null ? Entry.Ending(endingName) : null;
            if (ending != null)
            {
                Conclude(ending);   // an ending this crime does not have is simply not announced
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

        // ---------------------------------------------------------------- steps

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
            if (First.Present) First.WalkAwayFrom(player, Pace.Walk, 30f, within);
            if (Second.Present) Second.WalkAwayFrom(First.Position, Pace.Walk, 30f, within);
            if (Third.Present) Third.WalkAwayFrom(player, Pace.Walk, 30f, within);
            if (Cop.Present) Cop.WalkAwayFrom(player, Pace.Walk, 30f, within);
            Go(StepLeaving, now, within + 2f);
        }

        // ---------------------------------------------------------------- moving people

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

        // ---------------------------------------------------------------- arrests

        /// <summary>
        /// The officer has caught up with someone. From here the scene runs by itself
        /// (ArrestSequence): hands up, cuffed, walked to the kerb, into a patrol car and away,
        /// or walked off out of sight if no car can be had. The ending is announced as the cuffs
        /// go on; pass an empty name if the scene has already said what there is to say.
        /// With no officer on the street there is nobody to make an arrest: the suspect runs.
        /// </summary>
        /// <param name="byPlayer">The player stopped them: they are thanked when the cuffs go on.</param>
        protected void Arrest(ActorRole suspect, float now, string endingName = null, bool byPlayer = false)
        {
            Catchable = null;
            _held = null;
            if (!Cop.Present)
            {
                Flee(suspect, Scene);
                Go(StepLeaving, now, 20f);
                return;
            }
            _arrested = suspect;
            _arrestEnding = endingName;
            _arrestTold = false;
            _citizen |= byPlayer;
            if (Indoors)
            {
                _arrest.BeginIndoors(suspect, Cop, IndoorExit, now);
            }
            else
            {
                _arrest.Begin(suspect, Cop, now);
            }
            Go(StepArrest, now, 110f);
            _deadline = Mathf.Max(_deadline, now + 115f);   // an arrest once begun is played to the end
        }

        /// <summary>Indoors, where an arrested suspect is led out to.</summary>
        protected virtual Vector3 IndoorExit => Scene;

        private void RunShared(float now, Vector3 player)
        {
            switch (Step)
            {
                case StepArrest:
                    _arrest.Tick(now, player);
                    if (_arrest.Cuffed && !_arrestTold)
                    {
                        _arrestTold = true;
                        if (_citizen)
                        {
                            RewardCatch();
                        }
                        else if (_arrestEnding == null)
                        {
                            Conclude();
                        }
                        else if (_arrestEnding.Length > 0)
                        {
                            Conclude(_arrestEnding);
                        }
                        DismissOthers(_arrested.Position);
                    }
                    if (_arrest.Finished || now >= StepEnd)
                    {
                        _arrest.Clear();
                        Go(StepLeaving, now, 14f);
                    }
                    break;

                case StepHold:
                    RunHold(now, player);
                    break;

                case StepBreak:
                    RunBreak(now, player);
                    break;

                case StepLeaving:
                    if ((!First.Present && !Second.Present && !Third.Present && !Cop.Present) || now >= StepEnd)
                    {
                        End();
                    }
                    break;

                default:
                    End();
                    break;
            }
        }

        // Whoever is left (a victim, a bystander) watches them go, then leaves too.
        private void DismissOthers(Vector3 from)
        {
            if (Dismissable(First)) First.WalkAwayFrom(from, Pace.Walk);
            if (Dismissable(Second)) Second.WalkAwayFrom(from, Pace.Walk);
            if (Dismissable(Third)) Third.WalkAwayFrom(from, Pace.Walk);
        }

        private bool Dismissable(ActorRole role)
        {
            if (role == _arrested || role == _held || !role.Present)
            {
                return false;
            }
            role.Quiet();
            role.Drop();
            return true;
        }

        // ---------------------------------------------------------------- the player stops someone

        /// <summary>
        /// Somebody runs for it in a way the player can do something about. Most runners only
        /// jog (they are looking over their shoulder), so a player on foot who goes after them
        /// catches up; about one in four really runs. Getting within arm's reach of them for a
        /// moment, from behind or by standing in their way, stops them: see HoldForPolice.
        /// </summary>
        protected void RunFor(ActorRole who, Vector3 from, float distance = 60f)
        {
            if (!who.Present)
            {
                return;
            }
            if (StreetSpots.AwayFrom(who.Position, from, distance, onRoad: false, out Vector3 far))
            {
                who.WalkOff(far, RunnerPace, 30f);
                Catchable = who;
            }
            else
            {
                who.Exit();
            }
        }

        /// <summary>How fast this scene's runner goes: rolled once per scene.</summary>
        protected Pace RunnerPace => _fastRunner ? Pace.Run : Pace.Jog;

        private bool TryCatch(float now, Vector3 player)
        {
            if (!Catchable.Present)
            {
                Catchable = null;
                return false;
            }
            if (Helpers.PlayerHelper.IsUsingVehicle || (Catchable.Position - player).sqrMagnitude > CatchReach * CatchReach)
            {
                _caughtSince = 0f;
                return false;
            }
            if (_caughtSince <= 0f)
            {
                _caughtSince = now;   // within reach: still there on the next look counts as caught
                return false;
            }
            HoldForPolice(Catchable, now, player);
            return true;
        }

        /// <summary>
        /// A citizen's arrest: the player has hold of someone. They stand with their hands up
        /// while an officer comes at a run; the player has to stay with them. Some try to break
        /// away once (catch them again and they give up for good); walking off lets them go, at
        /// no cost. When the officer arrives it becomes an ordinary arrest and the player is
        /// thanked, sometimes with a reward.
        /// </summary>
        protected void HoldForPolice(ActorRole who, float now, Vector3 player)
        {
            Catchable = null;
            _held = who;
            _citizen = true;
            _caughtSince = 0f;
            who.Quiet();
            who.Drop();
            who.Stand(player);
            who.Surrender();
            // A victim who was giving chase catches up and stands by.
            if (Second != who && Second.Present)
            {
                Second.Quiet();
                Second.Go(Crime.Short(who.Position, Second.Position, 2.4f), Pace.Jog, who.Position, 12f);
            }
            if (!Cop.Present && !Indoors)
            {
                SendOfficer(who.Position, 14f, 24f);
            }
            _breakAt = !_brokeAway && Random.value < BreakAwayChance ? now + Random.Range(3f, 6f) : 0f;
            Crime.Show(NotificationType.Success, Pick(HoldLines), 7f);
            Go(StepHold, now, 45f);
            _deadline = Mathf.Max(_deadline, now + 60f);
        }

        private void RunHold(float now, Vector3 player)
        {
            if (!_held.Present)
            {
                End();
                return;
            }
            if (Cop.Present && Within(Cop, _held, 3f))
            {
                Arrest(_held, now, null, byPlayer: true);
                return;
            }
            if (Cop.Present)
            {
                Pursue(Cop, _held, Pace.Run, now);
            }
            if ((player - _held.Position).sqrMagnitude > LetGoDistance * LetGoDistance)
            {
                // The player walked away: no harm done, and no thanks either.
                Crime.Show(NotificationType.Info, Pick(LetGoLines), 6f);
                LetGo(now);
            }
            else if (_breakAt > 0f && now >= _breakAt)
            {
                _breakAt = 0f;
                _brokeAway = true;
                _held.Quiet();
                if (StreetSpots.AwayFrom(_held.Position, player, 45f, onRoad: false, out Vector3 far))
                {
                    _held.Go(far, Pace.Jog, far, 16f);
                    Crime.Show(NotificationType.Warning, Pick(BreakLines), 6f);
                    Go(StepBreak, now, 14f);
                }
            }
            else if (now >= StepEnd)
            {
                // No officer could be had. They are let go with a flea in their ear.
                Crime.Show(NotificationType.Info, "reallife_crime_citizen_nobody", 7f);
                ModState.AddModifier(ModState.Helped);
                LetGo(now);
            }
        }

        private void RunBreak(float now, Vector3 player)
        {
            if (!_held.Present)
            {
                End();
                return;
            }
            if (Cop.Present)
            {
                Pursue(Cop, _held, Pace.Run, now);
                if (Within(Cop, _held, 2.5f))
                {
                    Arrest(_held, now, null, byPlayer: true);
                    return;
                }
            }
            if (!Helpers.PlayerHelper.IsUsingVehicle && (player - _held.Position).sqrMagnitude < CatchReach * CatchReach)
            {
                // Caught again: that was their one try.
                _held.Stand(player);
                _held.Surrender();
                Crime.Show(NotificationType.Success, "reallife_crime_citizen_recaught", 6f);
                Go(StepHold, now, 35f);
            }
            else if (now >= StepEnd || _held.Settled)
            {
                Crime.Show(NotificationType.Info, "reallife_crime_citizen_slipped", 6f);
                LetGo(now);
            }
        }

        // The held person runs off and everybody else goes home.
        private void LetGo(float now)
        {
            ActorRole who = _held;
            Flee(who, Helpers.PlayerHelper.GetPosition());
            DismissOthers(who.Position);
            if (Cop.Present)
            {
                Cop.WalkAwayFrom(who.Position, Pace.Walk);
            }
            _held = null;
            _citizen = false;
            Go(StepLeaving, now, 22f);
        }

        /// <summary>
        /// The player's catch has ended in handcuffs: thanks, a lift in mood, and now and then
        /// something more. A scene with something of its own to give back overrides this.
        /// </summary>
        protected virtual void RewardCatch()
        {
            ModState.AddModifier(ModState.Helped);
            if (Second.Present && Second != _arrested)
            {
                Second.Emote(CharacterEmojiName.CommonGreenHappy, 3f);
            }
            float roll = Random.value;
            if (roll < 0.35f)
            {
                int reward = Mathf.RoundToInt(Random.Range(20f, 80f) / 5f) * 5;
                Crime.Give(reward, "reallife_transaction_reward");
                Crime.ShowAmount(NotificationType.Success, Pick(RewardLines), reward, 9f);
            }
            else if (roll < 0.5f)
            {
                ModState.AddModifier(ModState.MoodUpBig);
                Crime.Show(NotificationType.Success, Pick(CommendLines), 9f);
            }
            else
            {
                Crime.Show(NotificationType.Success, Pick(ThanksLines), 9f);
            }
        }

        private string Pick(string[] lines)
        {
            string line = CrimeDeck.DrawText(lines, _lastLine, Crime.Random01);
            _lastLine = line;
            return line;
        }

        private static readonly string[] HoldLines =
        {
            "reallife_crime_citizen_hold_1", "reallife_crime_citizen_hold_2", "reallife_crime_citizen_hold_3"
        };
        private static readonly string[] BreakLines =
        {
            "reallife_crime_citizen_break_1", "reallife_crime_citizen_break_2"
        };
        private static readonly string[] LetGoLines =
        {
            "reallife_crime_citizen_letgo_1", "reallife_crime_citizen_letgo_2"
        };
        private static readonly string[] ThanksLines =
        {
            "reallife_crime_citizen_thanks_1", "reallife_crime_citizen_thanks_2", "reallife_crime_citizen_thanks_3"
        };
        private static readonly string[] RewardLines =
        {
            "reallife_crime_citizen_reward_1", "reallife_crime_citizen_reward_2"
        };
        private static readonly string[] CommendLines =
        {
            "reallife_crime_citizen_commend_1", "reallife_crime_citizen_commend_2"
        };
    }
}
