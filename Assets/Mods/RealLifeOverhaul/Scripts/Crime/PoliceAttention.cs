using System;
using BAModAPI;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the police do about the player's own offences, when an officer nearby or, by chance, a patrol car saw: a ticket
    /// at the window for stopping, a pursuit for driving on, a car known for the day for getting away, a fine and custody
    /// for being caught; on foot a warning or a small fine. Idle it costs one time comparison a frame plus PlayerOffences'
    /// timed looks, and nothing indoors.
    /// </summary>
    public partial class PoliceAttention : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("crime", "PoliceAttention", typeof(PoliceAttention));

        private const float TickInterval = 0.1f;       // seconds
        private const float LookupInterval = 0.5f;     // seconds
        private const float OffenceCooldown = 25f;     // seconds before another offence can start a new stop
        private const float FootCooldown = 120f;       // seconds
        internal const float OfficerSight = 60f;        // metres within which an officer on foot can notice the player's driving
        private const float OfficerOnWatch = 0.55f;    // the chance one on patrol does, close and with a clear view
        private const float OfficerAtWork = 0.30f;     // and one busy at a scene

        private enum State { Idle, Summoned, Signalled, Fleeing, Ticketed, Custody }

        public static IModLogger Logger;
        public static PoliceAttention Instance { get; private set; }

        /// <summary>True while the police have business with the player, here or in PolicePursuit. No crime scene starts meanwhile.</summary>
        public static bool IsBusy =>
            (Instance != null && Instance._state != State.Idle) || (PolicePursuit.Instance != null && PolicePursuit.Instance.Busy)
            || Law.Custody.Active;

        /// <summary>True from the patrol car's lights behind the player until they stop, get away or are caught.</summary>
        public static bool PulledOver =>
            Instance != null && (Instance._state == State.Signalled || Instance._state == State.Fleeing);

        /// <summary>
        /// The player was stopped for traffic offences: a bit per offence on the bill (1 &lt;&lt; (int)Offence), and true when
        /// caught after fleeing.
        /// </summary>
        public static event Action<int, bool> Stopped;

        /// <summary>Hours in custody for an arrest made by another feature (Licences). False if custody is running or this is off.</summary>
        public static bool TakeIntoCustody(int hours)
        {
            PoliceAttention attention = Instance;
            if (attention == null || attention._custodyRunning || !attention.isActiveAndEnabled)
            {
                return false;
            }
            attention.BeginCustody(Mathf.Clamp(hours, 1, 48));
            return true;
        }

        private readonly PlayerOffences _offences = new PlayerOffences();

        private ResponseUnit _patrol;
        private float _nextSpeeding;       // speeding is looked at less often than anything else
        private State _state = State.Idle;
        private Rigidbody _body;           // the player's car, looked up twice a second
        private bool _driving;
        private float _nextLookup;
        private float _nextTick;
        private float _nextOffence;
        private float _nextFootOffence;
        private int _knownDay = -1;        // the day on which the player's car is known to the police
        private int _faults;

        /// <summary>The player's car is known to the police for the rest of the day (they got away from something).</summary>
        internal static void KnowCar()
        {
            if (Instance != null)
            {
                Instance._knownDay = TimeHelper.CurrentDay;
            }
        }

        private void Awake()
        {
            Instance = this;
            _patrol = new ResponseUnit(gameObject);
            _backup = new PursuitBackup(gameObject);
            PolicePursuit.CaughtAfterFleeing += OnHitAndRunCaught;
        }

        private void OnDestroy()
        {
            PolicePursuit.CaughtAfterFleeing -= OnHitAndRunCaught;
            if (_custodyRunning && _custodyResting)
            {
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);   // the coroutine that would have done it dies with us
            }
            Clear();
            RoadMap.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Crime: PoliceAttention.Update");
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
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.time;
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
                Logger?.Error(e);
                Clear();
                if (++_faults >= 3)
                {
                    Logger?.Warn("Police attention: switched off for this session after repeated errors (see above).");
                    enabled = false;
                }
            }
        }

        private void Tick(float now)
        {
            if (_state == State.Custody)
            {
                if (_lawHolds && !Law.Custody.OnScene)
                {
                    Clear();   // the screen is dark, or the arrest came to nothing: the patrol car goes
                }
                return;   // the coroutine has it, or Law/Custody does
            }
            if (now >= _nextLookup)
            {
                _nextLookup = now + LookupInterval;
                Lookup();
            }
            if (_state == State.Idle)
            {
                if (Settings.PlayerOffences && ModState.Loaded && TrafficTools.Ready)
                {
                    Detect(now);
                }
                return;
            }
            Pursue(now);
        }

        private void Lookup()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = gm != null ? gm.selectedVehicle : null;
            bool driving = vehicle != null && PlayerHelper.IsUsingVehicle && vehicle.vehicleType != null && vehicle.vehicleType.IsMotorVehicle;
            if (!driving)
            {
                _body = null;
            }
            else if (_body == null || _body.gameObject != vehicle.gameObject)
            {
                _body = vehicle.GetComponent<Rigidbody>();
                _offences.Reset();
            }
            _driving = driving && _body != null;
        }

        private void Detect(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null)
            {
                return;
            }
            if (_driving)
            {
                if (!RoadMap.Ensure())
                {
                    return;
                }
                Offence offence = _offences.Driving(_body, now);
                // Speeding, possible on every straight, is looked at once and then not for a while, seen or not.
                if (offence != Offence.None && now >= _nextOffence && (offence != Offence.Speeding || now >= _nextSpeeding)
                    && !IsBusy && LifeEvents.IsSafeForEvent())
                {
                    _nextOffence = now + OffenceCooldown;
                    if (offence == Offence.Speeding)
                    {
                        _nextSpeeding = now + CrimeLaw.SpeedingCooldown;
                    }
                    if (Seen(offence, _body.position))
                    {
                        Summon(offence, now);
                    }
                }
                return;
            }

            if (PlayerHelper.IsUsingVehicle || now < _nextFootOffence)
            {
                return;
            }
            Offence onFoot = _offences.OnFoot(player.transform.position, now);
            if (onFoot != Offence.None)
            {
                _nextFootOffence = now + FootCooldown;
                Shout(onFoot);
            }
        }

        // A patrol car by a chance per offence, or an officer on foot with a clear view, likelier the nearer; never certain,
        // with officers on most streets.
        private bool Seen(Offence offence, Vector3 at)
        {
            if (!Settings.Police)
            {
                return false;   // no patrol cars: nobody to pull anyone over
            }
            bool speeding = offence == Offence.Speeding;
            bool known = _knownDay == TimeHelper.CurrentDay;
            if (known && !speeding)
            {
                return true;
            }
            float chance = offence == Offence.RedLight ? CrimeLaw.RedLightSeen
                : speeding ? CrimeLaw.SpeedingSeen
                : CrimeLaw.RecklessSeen;
            if (speeding && _body != null && _body.velocity.sqrMagnitude > CrimeLaw.SpeedingGrossKmh * CrimeLaw.SpeedingGrossKmh / (3.6f * 3.6f))
            {
                chance *= CrimeLaw.SpeedingGrossTimes;
            }
            chance *= Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(1f, 10f, Settings.CrimeFrequency));
            float sight = OfficerSight * Events.Weather.SightFactor;   // less far in rain, mist and the dark
            StreetActor officer = PlayerOffences.OfficerNear(at, sight);
            if (officer != null && !PursuitSight.Blocked(officer.Position, at))
            {
                // Someone on foot is a poor judge of a passing car's speed.
                float near = Mathf.InverseLerp(sight, sight * 0.4f, Vector3.Distance(officer.Position, at));
                chance = Mathf.Max(chance, (officer.Yields ? OfficerOnWatch : OfficerAtWork) * Mathf.Lerp(0.4f, 1f, near) * (speeding ? CrimeLaw.SpeedingOnFoot : 1f));
            }
            if (known)
            {
                chance = Mathf.Max(chance, CrimeLaw.SpeedingSeenKnown);
            }
            if (Settings.LawReputation)
            {
                chance *= Law.Reputation.Scrutiny;   // up to 1.5 for a record that has grown lately
            }
            return Random.value < chance * CivicPolicy.EnforcementScale * CityWatch.SeenScale;
        }

        /// <summary>
        /// An offence the road data cannot show (drink: Leisure), seen or not as any other. Safe to call every second: false
        /// during a stop or within 25 s of the last look.
        /// </summary>
        internal static bool Report(Offence offence)
        {
            PoliceAttention attention = Instance;
            float now = Time.time;
            if (attention == null || !attention.isActiveAndEnabled || !attention._driving || attention._body == null || now < attention._nextOffence
                || IsBusy || !LifeEvents.IsSafeForEvent())
            {
                return false;
            }
            attention._nextOffence = now + OffenceCooldown;
            if (!attention.Seen(offence, attention._body.position))
            {
                return false;
            }
            attention.Summon(offence, now);
            return true;
        }

        private void Shout(Offence offence)
        {
            StreetActor officer = _offences.Witness;
            ActorPool pool = ActorPool.Instance;
            if (officer != null && pool != null)
            {
                pool.ShowEmoji(officer, CharacterEmojiName.CommonRedAngry, 3f);
            }
            if (Random.value < CrimeLaw.JaywalkFineChance || (Settings.LawReputation && Law.Reputation.NoWarnings))
            {
                Crime.Fine(CrimeLaw.JaywalkFine, offence == Offence.CrossOnRed ? "reallife_crime_jaywalk_fine_red" : "reallife_crime_jaywalk_fine");
            }
            else
            {
                Crime.Show(NotificationType.Warning, offence == Offence.CrossOnRed
                    ? (Random.value < 0.5f ? "reallife_crime_jaywalk_warn_red_1" : "reallife_crime_jaywalk_warn_red_2")
                    : (Random.value < 0.5f ? "reallife_crime_jaywalk_warn_1" : "reallife_crime_jaywalk_warn_2"), 6f);
            }
        }
    }
}
