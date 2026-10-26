using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// People hit by vehicles: the hit is rolled from the speed (knocked down under 15 km/h, injured to 35, then killed
    /// more often the faster), the person thrown down as a ragdoll and looked after until off the street. Someone who
    /// stays down becomes an accident scene with an ambulance and a patrol car; what it costs the player is
    /// <see cref="PolicePursuit"/>'s business.
    /// Cost: with nobody down, one time comparison per frame; otherwise a tick four times a second.
    /// </summary>
    public partial class PedestrianIncidents : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ragdoll", "PedestrianIncidents", typeof(PedestrianIncidents));

        private const float CheckInterval = 20f;         // seconds between rolls for a staged incident
        private const float TickInterval = 0.25f;        // seconds
        internal const float KnockSpeed = 15f / 3.6f;     // m/s: below this nobody is hurt
        internal const float InjurySpeed = 35f / 3.6f;    // ...below this nobody dies
        internal const float DeadlySpeed = 60f / 3.6f;    // ...above this most do
        internal const float KnockedBelow = KnockSpeed * 0.9f;   // m/s: the most a two-wheeler's strike counts for
        private const float StagedShare = 0.4f;          // staged incidents per staged car accident
        private const float StagedDeathChance = 0.35f;   // of a staged incident
        private const float SameSceneDistance = 60f;     // metres: casualties this close together share an ambulance

        private static readonly string[] StagedStarts =
        {
            "reallife_pedstruck_start_1", "reallife_pedstruck_start_2", "reallife_pedstruck_start_3"
        };

        public static IModLogger Logger;
        public static PedestrianIncidents Instance { get; private set; }

        private readonly Casualty[] _casualties = new Casualty[RagdollSystem.MaxBodies];
        private float _nextCheck;
        private float _nextTick;
        private bool _anyone;
        private int _figureFailures;   // standing figures in a row that could not be thrown down
        private bool _figuresOff;

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < _casualties.Length; i++)
            {
                _casualties[i] = new Casualty(this);
            }
            _nextCheck = Time.time + CheckInterval;
            RagdollSystem.MakeRoom = GiveUpOne;
        }

        private void OnDestroy()
        {
            ClearAll();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Ragdoll: PedestrianIncidents.Update");

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.time;
            if (!_anyone)
            {
                if (now < _nextCheck)
                {
                    return;
                }
                _nextCheck = now + CheckInterval;
                try
                {
                    if (Settings.PedestrianIncidents && !RecklessDrivers.IsSource && CanStage() && LifeEvents.IsSafeForEvent()
                        && !Wiring.Scenes.AnyLargeSceneActive
                        && Random.value < StreetRules.Due(Settings.AccidentFrequency, Events.Weather.AccidentFactor, TimeHelper.CurrentHour) * StagedShare)
                    {
                        TryStage(now, TrafficAccidents.Instance.FindMovingCar(PlayerHelper.GetPosition()));
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    ClearAll();
                    if (Guard.StandsDown(e, "people hit by traffic"))
                    {
                        enabled = false;   // a game that lacks what a staging names would fail at every look
                    }
                }
                return;
            }

            if (now < _nextTick)
            {
                return;
            }
            _nextTick = now + TickInterval;
            try
            {
                Tick(now);
            }
            catch (Exception e)
            {
                // ClearAll leaves nobody down, so a fault cannot repeat.
                Logger?.Error(e);
                ClearAll();
                if (Guard.StandsDown(e, "people hit by traffic"))
                {
                    enabled = false;
                }
            }
        }

        private void Tick(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            // Indoors or fast-forwarding, the street is gone and the game has taken its pedestrians back.
            if (!Settings.Ragdolls || !TrafficTools.Street || player == null)
            {
                ClearAll();
                return;
            }
            Vector3 avoid = player.transform.position;
            bool anyone = false;
            for (int i = 0; i < _casualties.Length; i++)
            {
                Casualty casualty = _casualties[i];
                if (!casualty.InUse)
                {
                    continue;
                }
                casualty.Tick(now, avoid);
                if (!casualty.InUse)
                {
                    continue;
                }
                anyone = true;
                // Help is called once they lie still; a staged incident has asked for it already.
                if (!casualty.SceneAsked && casualty.Result != Casualty.Outcome.Knocked && casualty.Resting)
                {
                    casualty.SceneAsked = true;
                    if (casualty.ByPlayer)
                    {
                        CallHelp(casualty);
                    }
                    else
                    {
                        CallHelpStruck(casualty);
                    }
                }
            }
            if (!anyone)
            {
                _anyone = false;
                _nextCheck = now + CheckInterval * 3f;
            }
        }

        private const float TripForward = 1.4f, TripUp = 0.8f;   // m/s: a trip throws a walker forward and a little up
        private static readonly List<Transform> TripRemotes = new List<Transform>(4);

        // True if a walker is, holds or is held by another player of a Going Public session.
        private static bool IsRemote(Transform walker)
        {
            for (int i = 0; i < TripRemotes.Count; i++)
            {
                Transform r = TripRemotes[i];
                if (r != null && (walker == r || walker.IsChildOf(r) || r.IsChildOf(walker)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The game pedestrian nearest a point, within <paramref name="within"/> metres, trips at a city fault: knocked down,
        /// never hurt, nobody to blame. False if nobody is that near or no ragdoll can be had. One pass over the walkers.
        /// </summary>
        public bool Trip(Vector3 at, float within, Vector3 forward)
        {
            try
            {
                List<Pedestrian> walkers = PerfGame.Pedestrians;
                Pedestrian nearest = null;
                float best = within * within;
                TripRemotes.Clear();
                GoingPublic.RemotePlayers(TripRemotes);   // they are not the game's walkers, but never throw one down even so
                for (int i = 0; walkers != null && i < walkers.Count; i++)
                {
                    Pedestrian p = walkers[i];
                    if (p == null || p.tpc == null || IsRemote(p.tpc.transform))
                    {
                        continue;
                    }
                    Vector3 offset = p.tpc.transform.position - at;
                    float sqr = offset.x * offset.x + offset.z * offset.z;
                    if (sqr < best)
                    {
                        best = sqr;
                        nearest = p;
                    }
                }
                if (nearest == null || CanStrike(nearest.tpc) != Strike.Down)
                {
                    return false;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return false;
                }
                HornReactions.LetGo(nearest);
                if (!casualty.BeginPedestrian(nearest, forward * TripForward + Vector3.up * TripUp, Casualty.Outcome.Knocked, false, Time.time))
                {
                    return false;
                }
                _anyone = true;
                _nextTick = Time.time + TickInterval;
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return false;
            }
        }

        /// <summary>The player's vehicle has run into one of the game's pedestrians at <paramref name="speed"/> (m/s).</summary>
        public Strike StrikePedestrian(Pedestrian pedestrian, Vector3 carVelocity, float speed, Rigidbody playerCar)
        {
            try
            {
                // Asked first and cheaply: called again and again while the car is over someone who cannot be thrown.
                Strike why = CanStrike(pedestrian != null ? pedestrian.tpc : null);
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                HornReactions.LetGo(pedestrian);   // a pace or stop for a horn is undone before they are taken over
                if (casualty.BeginPedestrian(pedestrian, Launch(carVelocity, speed), Roll(speed), true, Time.time))
                {
                    OnPlayerHit(casualty, speed, playerCar);
                    return Strike.Down;
                }
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        public Strike StrikeFigure(BaseHuman figure, Vector3 carVelocity, float speed, Rigidbody playerCar)
        {
            try
            {
                Strike why = _figuresOff ? Strike.Failed : CanStrike(figure, seatedToo: true);
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                HornReactions.LetGo(figure);   // turned to look at a horn: turned back first
                if (casualty.BeginFigure(figure, Launch(carVelocity, speed), Roll(speed), true, Time.time))
                {
                    _figureFailures = 0;
                    OnPlayerHit(casualty, speed, playerCar);
                    return Strike.Down;
                }
                FigureRefused();
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        // Not built like the characters the ragdoll was written for: given up after three in a row, so a car standing over
        // one does not try (and reset its fidgeting) every other physics step.
        private void FigureRefused()
        {
            if (++_figureFailures >= 3)
            {
                _figuresOff = true;
                Logger?.Warn("Pedestrian collisions: the game's standing figures cannot be thrown down (three in a row refused); they are left alone for this session.");
            }
        }

        public Strike StrikeActor(StreetActor actor, Vector3 carVelocity, float speed, Rigidbody playerCar)
        {
            try
            {
                if (actor == null || !actor.IsValid)
                {
                    return Strike.NotCounted;
                }
                Strike why = CanStrike(actor.Character, posedByHand: actor.PosedByHand);   // nobody the pool is not drawing is run over
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                if (casualty.BeginActor(actor, Launch(carVelocity, speed), Roll(speed), true, Time.time))
                {
                    OnPlayerHit(casualty, speed, playerCar);
                    return Strike.Down;
                }
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        /// <summary>What became of someone inside the car's sweep.</summary>
        public enum Strike
        {
            Down,         // thrown down
            NotCounted,   // nobody there to hit: gone, already lying there, or the save is not loaded
            Seated,       // sitting: wired to the seat, left alone
            NoRagdoll,    // every ragdoll is still falling, ragdolls are off or failed, or the person is not being drawn
            Failed        // could not be taken over or thrown after all
        }

        // Down if this person can be thrown right now; otherwise why not. posedByHand: one of the mod's people kneeling by
        // hand, drawn, whose animator is off only for the pose (StreetActor.PosedByHand), which Casualty.BeginActor clears.
        private static Strike CanStrike(BaseHuman person, bool seatedToo = false, bool posedByHand = false)
        {
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (!ModState.Loaded || ragdolls == null || person == null || RagdollSystem.Holds(person))
            {
                return Strike.NotCounted;
            }
            if (person.isSittingOn != null && !seatedToo)
            {
                // A plain figure is let go of its seat and thrown (Casualty.BeginFigure); a full street character has more
                // tied to the seat than that.
                return Strike.Seated;
            }
            return ragdolls.CanBegin(person, posedByHand) ? Strike.Down : Strike.NoRagdoll;
        }

        private void OnPlayerHit(Casualty casualty, float speed, Rigidbody playerCar)
        {
            _anyone = true;
            _nextTick = Time.time + TickInterval;
            Logger?.Info($"Pedestrian hit by the player at {speed * 3.6f:F0} km/h: {casualty.Result}.");
            if (casualty.Result == Casualty.Outcome.Knocked)
            {
                Notifications.Show(NotificationType.Warning, "reallife_pedhit_knocked", null, 6f);
                return;
            }

            // The player gets the chance to stop; with police pursuits off it is settled as an accident on the spot.
            PolicePursuit pursuit = PolicePursuit.Instance;
            bool deferred = pursuit != null && playerCar != null && pursuit.ReportPedestrian(playerCar, casualty.Dead, casualty.Position);
            if (!deferred)
            {
                PolicePursuit.ChargeForPeople(casualty.Dead ? 0 : 1, casualty.Dead ? 1 : 0, stopped: true);
            }
        }

        // Between the injury and deadly speeds the odds of dying climb from 10% to 45%, then on to 95% at 90 km/h.
        private static Casualty.Outcome Roll(float speed)
        {
            if (speed < KnockSpeed)
            {
                return Casualty.Outcome.Knocked;
            }
            if (speed < InjurySpeed)
            {
                return Casualty.Outcome.Injured;
            }
            float death = speed < DeadlySpeed
                ? Mathf.Lerp(0.10f, 0.45f, Mathf.InverseLerp(InjurySpeed, DeadlySpeed, speed))
                : Mathf.Lerp(0.75f, 0.95f, Mathf.InverseLerp(DeadlySpeed, 90f / 3.6f, speed));
            return Random.value < death ? Casualty.Outcome.Dead : Casualty.Outcome.Injured;
        }

        // The body leaves at about the car's speed and a little upwards: a metre at walking pace, a long way at 60.
        private static Vector3 Launch(Vector3 carVelocity, float speed)
        {
            Vector3 along = carVelocity;
            along.y = 0f;
            along = along.sqrMagnitude > 0.01f ? along.normalized : Vector3.forward;
            return along * Mathf.Clamp(speed, 2.5f, 26f) + Vector3.up * Mathf.Clamp(1.5f + speed * 0.18f, 1.5f, 5.5f);
        }

        // An ambulance and a patrol car for someone the player has put on the ground. Any other incident gives way.
        private void CallHelp(Casualty casualty)
        {
            TrafficAccidents accidents = TrafficAccidents.Instance;
            if (accidents == null || accidents.HasCasualty)
            {
                return;   // crews are already attending someone here; this one goes with the same ambulance
            }
            StreetCrime.EndActive();
            TrafficAccidents.EndActive();
            if (accidents.StageCasualty(casualty, casualty.Position, null, casualty.Dead, byPlayer: true))
            {
                Notifications.Show(NotificationType.Warning, "reallife_pedhit_scene", null, 8f, "reallife_accident");
            }
        }

        /// <summary>A casualty has gone with the ambulance or the scene ended: anyone else lying hurt nearby goes too.</summary>
        internal void OnCollected(Casualty collected)
        {
            for (int i = 0; i < _casualties.Length; i++)
            {
                Casualty other = _casualties[i];
                if (other != collected && other.Present && other.Result != Casualty.Outcome.Knocked && other.Resting
                    && (other.Position - collected.Position).sqrMagnitude < SameSceneDistance * SameSceneDistance)
                {
                    other.Remove();
                }
            }
        }

        private Casualty FreeCasualty()
        {
            if (!Settings.Ragdolls)
            {
                return null;
            }
            for (int i = 0; i < _casualties.Length; i++)
            {
                if (!_casualties[i].InUse)
                {
                    // A new object each time, on a hit only: a scene still holding the last person in this place must
                    // not find a different one behind it.
                    _casualties[i] = new Casualty(this);
                    return _casualties[i];
                }
            }
            return GiveUpOne(false) ? FreeCasualty() : null;
        }

        // RagdollSystem.MakeRoom too. Removes the one at rest who has lain longest, out of view if anyone is; with
        // unseenOnly (too many still bodies: nobody need vanish for that), only a still body out of view.
        private bool GiveUpOne(bool unseenOnly)
        {
            Casualty oldest = null;
            bool oldestSeen = true;
            for (int i = 0; i < _casualties.Length; i++)
            {
                Casualty casualty = _casualties[i];
                if (!casualty.Resting || (unseenOnly && !casualty.Still))
                {
                    continue;
                }
                bool seen = RagdollSystem.InView(casualty.Position);
                if (seen && unseenOnly)
                {
                    continue;
                }
                if (oldest == null || (oldestSeen && !seen) || (seen == oldestSeen && casualty.StruckAt < oldest.StruckAt))
                {
                    oldest = casualty;
                    oldestSeen = seen;
                }
            }
            if (oldest == null)
            {
                return false;
            }
            oldest.Remove();
            return true;
        }

        // Everybody off the street and whole again; marked over first, so no failure here can make it run again.
        private void ClearAll()
        {
            _anyone = false;
            _nextCheck = Time.time + CheckInterval * 3f;
            for (int i = 0; i < _casualties.Length; i++)
            {
                if (_casualties[i] != null)
                {
                    _casualties[i].Remove();   // catches its own faults
                }
            }
        }
    }
}
