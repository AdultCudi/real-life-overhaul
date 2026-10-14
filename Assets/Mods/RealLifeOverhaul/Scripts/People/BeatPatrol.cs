using System;
using BAModAPI;
using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Officers on the beat: most of the time, while the player is outdoors, a pair of officers
    /// (now and then one alone) is walking a stretch of pavement nearby. A patrol lasts a few
    /// minutes and a new one starts soon after the last has gone or been left behind. They
    /// appear out of immediate view and leave the same way. One patrol at a time.
    /// They are people the pool may take back at any moment (ActorRole.EnterPresence): when an
    /// accident, a crime or an arrest needs somebody and nobody else is free, one of them is
    /// gone that instant, and the patrol carries on with whoever is left or ends.
    /// Idle, this is one time comparison per frame; a patrol is moved along twice a second.
    /// </summary>
    public class BeatPatrol : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("people", "BeatPatrol", typeof(BeatPatrol));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float CheckInterval = 8f;
        private const float TickInterval = 0.5f;
        private const float MinDistance = 35f;
        private const float MaxDistance = 70f;
        private const float LostDistance = 150f;     // the player has left them far behind
        private const float PartnerChance = 0.85f;
        private const int MaxStops = 4;

        private enum Step { None, Walking, Pausing, Leaving }

        public static IModLogger Logger;
        public static BeatPatrol Instance { get; private set; }

        private readonly ActorRole _lead = new ActorRole();
        private readonly ActorRole _partner = new ActorRole();
        private readonly Vector3[] _beat = new Vector3[MaxStops];

        private Step _step = Step.None;
        private int _stops;
        private int _next;
        private float _stepEnd;
        private float _until;
        private float _nextCheck;
        private float _nextTick;

        private void Awake()
        {
            Instance = this;
            _nextCheck = Time.time + CheckInterval;
        }

        private void OnDestroy()
        {
            End();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("People: BeatPatrol.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.time;
            if (_step == Step.None)
            {
                if (now < _nextCheck)
                {
                    return;
                }
                _nextCheck = now + CheckInterval;
                try
                {
                    // At the default settings a new patrol starts about half a minute after the last one ended.
                    if (Settings.OfficersOnPatrol && Settings.Police && Settings.People && ModState.Loaded && TrafficTools.Ready && LifeEvents.IsSafeForEvent()
                        && Random.value < Settings.PatrolFrequency * 0.016f * Settings.PolicePresence)
                    {
                        TryStart(now);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    End();
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
                // End leaves no patrol behind, so a fault cannot repeat.
                Logger?.Error(e);
                End();
            }
        }

        private void TryStart(float now)
        {
            ActorPool pool = ActorPool.Instance;
            PlayerController player = PlayerHelper.PlayerController;
            if (pool == null || player == null)
            {
                return;
            }
            Vector3 origin = player.transform.position;
            if (!StreetSpots.PavementAround(origin, MinDistance, MaxDistance, true, out Vector3 start))
            {
                return;
            }

            // The beat: a few stretches of pavement, each a short walk on from the last.
            _stops = 0;
            Vector3 from = start;
            for (int i = 0; i < MaxStops; i++)
            {
                if (!StreetSpots.PavementAround(from, 14f, 28f, false, out Vector3 stop))
                {
                    break;
                }
                _beat[_stops++] = stop;
                from = stop;
            }
            if (_stops < 2 || !_lead.EnterPresence(start, ActorLook.Officer, 3f, onRoad: false))
            {
                End();
                _nextCheck = now + CheckInterval;
                return;
            }
            if (Random.value < PartnerChance)
            {
                _partner.EnterPresence(start + Vector3.right * 1.2f, ActorLook.Officer, 2f, onRoad: false);
            }

            _next = 0;
            _until = now + Random.Range(120f, 240f);
            _nextTick = now + TickInterval;
            WalkOn(now);
            Logger?.Info($"Patrol: {(_partner.Present ? "two officers" : "one officer")} on the beat, {Vector3.Distance(origin, start):F0} m away, {_stops} stops.");
        }

        private void Tick(float now)
        {
            _lead.Tick(now);
            _partner.Tick(now);

            PlayerController player = PlayerHelper.PlayerController;
            if (!TrafficTools.Ready || player == null || (!_lead.Present && !_partner.Present))
            {
                End();   // the street was unloaded, or they have gone
                return;
            }
            Vector3 origin = player.transform.position;
            if (_step != Step.Leaving)
            {
                if ((_lead.Position - origin).sqrMagnitude > LostDistance * LostDistance)
                {
                    End();
                    return;
                }
                if (now >= _until || !Settings.OfficersOnPatrol || !Settings.Police)
                {
                    Dismiss(now, origin, 25f);
                    return;
                }
            }

            switch (_step)
            {
                case Step.Walking:
                    if (_lead.Settled || !_lead.Present || now >= _stepEnd)
                    {
                        // A look around before moving on.
                        _step = Step.Pausing;
                        _stepEnd = now + Random.Range(2f, 6f);
                    }
                    break;

                case Step.Pausing:
                    if (now >= _stepEnd)
                    {
                        WalkOn(now);
                    }
                    break;

                case Step.Leaving:
                    if (now >= _stepEnd)
                    {
                        End();
                    }
                    break;
            }
        }

        // On to the next stop, and round again after the last.
        private void WalkOn(float now)
        {
            Vector3 stop = _beat[_next];
            Vector3 after = _beat[(_next + 1) % _stops];
            _next = (_next + 1) % _stops;
            _lead.Go(stop, Pace.Walk, after, 45f);
            _partner.Go(stop + Vector3.right * 1.1f, Pace.Walk, after, 45f);
            _step = Step.Walking;
            _stepEnd = now + 46f;
        }

        // They walk off away from the player and are gone when they get there, when nobody can see them, or when time is up.
        private void Dismiss(float now, Vector3 player, float within)
        {
            _lead.WalkAwayFrom(player, Pace.Walk, 30f, within);
            _partner.WalkAwayFrom(player, Pace.Walk, 30f, within);
            _step = Step.Leaving;
            _stepEnd = now + within + 1f;
        }

        // Both off the street. The patrol is marked over first, so no failure here can make it run again.
        private void End()
        {
            _step = Step.None;
            _nextCheck = Time.time + CheckInterval;
            try
            {
                _lead.Exit();
                _partner.Exit();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
