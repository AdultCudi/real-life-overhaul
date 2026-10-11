using System;
using BAModAPI;
using BigAmbitions.Characters;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Police who are simply there. Near the player there is, most of the time, a patrol car
    /// parked at the kerb with one or two officers standing by it, and now and then an officer
    /// standing at a junction. Both are placed where the camera is not looking and taken away
    /// the same way once the player has left them behind or they have stood long enough.
    /// It costs next to nothing: the car is one copy of the game's own police car prop, built
    /// once and moved (a box collider and a cut in the navigation mesh make it solid; nothing
    /// on it moves), and the officers are standing people from the pool, who cost nothing
    /// while out of view. One car and one junction post at a time. The officers are people the
    /// pool may take back at any moment for a scene (ActorRole.EnterPresence); the car then
    /// simply stands empty.
    /// A place for the car must pass every test or there is none: beside a plain stretch of lane
    /// (never in a junction), far enough from the lane's middle that traffic clears it, in no
    /// other lane, on the road and not on a pavement or a crossing, and overlapping no vehicle
    /// and no street furniture, with room to spare.
    /// Idle, this is one time comparison per frame; a site is looked at twice a second.
    /// </summary>
    public class PolicePresence : MonoBehaviour
    {
        private const float CheckInterval = 4f;
        private const float TickInterval = 0.5f;
        private const float MinDistance = 45f;
        private const float MaxDistance = 95f;
        private const float LostDistance = 175f;     // the player has left a site this far behind: it goes
        private const float LaneGap = 2.0f;          // metres from a lane's middle to the near side of the parked car, at least
        private const float OtherLane = 2.2f;        // no other lane's road point may be this near the car's middle
        private const float Room = 0.45f;            // metres clear of every vehicle and all street furniture, all round
        private const float SecondOfficerChance = 0.6f;
        private const int Attempts = 2;              // places tried per look (each asks the whole road graph for its nearest point); the next look tries again

        private const PermanentAnimationType RadioPose = PermanentAnimationType.TalkingPhone;
        private const string RadioProp = "PhoneOff";

        private static readonly float[] KerbOffsets = { 3.0f, 3.3f, 3.6f };

        public static IModLogger Logger;
        public static PolicePresence Instance { get; private set; }

        private readonly ActorRole _driver = new ActorRole();
        private readonly ActorRole _partner = new ActorRole();
        private readonly ActorRole _post = new ActorRole();

        private GameObject _car;            // built once, switched off between sites
        private NavMeshObstacle _carBox;
        private float _halfWidth = 1f;
        private float _halfLength = 2.6f;
        private bool _carFailed;            // the game has no police car prop: no parked cars this session
        private bool _carOut;
        private bool _crewDue;              // the second officer is still to be put out (one new character per look)
        private Vector3 _carAt;
        private Vector3 _kerb;              // on the pavement beside the car
        private Vector3 _roadward;          // from the kerb towards the lane
        private float _carUntil;
        private bool _postOut;
        private float _postUntil;
        private float _nextRadio;
        private float _nextCheck;
        private float _nextTick;
        private int _faults;

        private void Awake()
        {
            Instance = this;
            _nextCheck = Time.time + CheckInterval * 2f;
        }

        private void OnDestroy()
        {
            Clear();
            if (_car != null)
            {
                Destroy(_car);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>True while a patrol car of ours is parked at the kerb.</summary>
        public bool CarOut => _carOut;

        private static bool Wanted => Settings.Police && Settings.People && Settings.PolicePresence > 0 && ModState.Loaded && TrafficTools.Ready;

        private void Update()
        {
            float now = Time.time;
            bool anything = _carOut || _postOut;
            if (now < (anything ? Mathf.Min(_nextTick, _nextCheck) : _nextCheck))
            {
                return;
            }
            try
            {
                if (anything && now >= _nextTick)
                {
                    _nextTick = now + TickInterval;
                    Tick(now);
                }
                if (now >= _nextCheck)
                {
                    _nextCheck = now + CheckInterval;
                    Look(now);
                }
            }
            catch (Exception e)
            {
                // Clear leaves nothing out, so a fault cannot repeat; after three there is no presence this session.
                Logger?.Error(e);
                Clear();
                if (++_faults >= 3)
                {
                    Logger?.Warn("Police presence: switched off for this session after repeated errors (see above).");
                    enabled = false;
                }
            }
        }

        // Every few seconds: put out what is missing, one thing (and one new character at most) at a time.
        private void Look(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (!Wanted || player == null || !LifeEvents.IsSafeForEvent())
            {
                return;
            }
            Vector3 origin = player.transform.position;
            float presence = Settings.PolicePresence;
            if (_carOut && _crewDue)
            {
                _crewDue = false;
                if (!StreetSpots.IsInView(_kerb) && StreetSpots.OnPavement(_kerb + Vector3.Cross(Vector3.up, _roadward) * 1.3f, 1.5f, out Vector3 beside)
                    && _partner.EnterPresence(beside, ActorLook.Officer, 1.5f, onRoad: false))
                {
                    _partner.Stand(beside + _roadward * 6f + Vector3.Cross(Vector3.up, _roadward) * 5f);
                }
                return;
            }
            if (!_carOut && !_carFailed && Random.value < presence * 0.06f)
            {
                PlaceCar(origin, now);
                return;
            }
            if (!_postOut && Random.value < presence * 0.02f)
            {
                PlacePost(origin, now);
            }
        }

        private void Tick(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (!Wanted || player == null)
            {
                Clear();   // indoors, a menu, or switched off: nothing is left standing in a street nobody is in
                return;
            }
            Vector3 origin = player.transform.position;
            if (_carOut)
            {
                _driver.Tick(now);
                _partner.Tick(now);
                bool far = (_carAt - origin).sqrMagnitude > LostDistance * LostDistance;
                if (far || (now >= _carUntil && !StreetSpots.IsInView(_carAt)))
                {
                    RemoveCar();
                }
                else if (now >= _nextRadio && _driver.Settled)
                {
                    // Now and then one of them is on the radio for a while.
                    _nextRadio = now + Random.Range(20f, 45f);
                    if (Random.value < 0.5f)
                    {
                        _driver.Work(SceneClips.RadioTalk, RadioPose, RadioProp);   // a hand to the radio on the collar; a phone to the ear where that clip cannot be had
                    }
                    else
                    {
                        _driver.Drop();
                    }
                }
            }
            if (_postOut)
            {
                _post.Tick(now);
                Vector3 at = _post.Position;
                if (!_post.Present || (at - origin).sqrMagnitude > LostDistance * LostDistance || (now >= _postUntil && !_post.Visible))
                {
                    _post.Exit();
                    _postOut = false;
                }
            }
        }

        // ---------------------------------------------------------------- the parked patrol car

        private void PlaceCar(Vector3 origin, float now)
        {
            if (_car == null && !BuildCar())
            {
                return;
            }
            TrafficManager traffic = TrafficManager.Instance;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(MinDistance, MaxDistance);
                Vector3 probe = origin + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                Waypoint point = traffic.GetClosestWaypoint(probe, 14f);
                if (point == null || !IsPlainLane(point) || !FindKerbPlace(traffic, point, out Vector3 at, out Quaternion facing, out Vector3 kerb))
                {
                    continue;
                }
                float sqr = (at - origin).sqrMagnitude;
                if (sqr < MinDistance * MinDistance * 0.7f || StreetSpots.IsInView(at))
                {
                    continue;
                }
                // The officers first: a patrol car with nobody near it is only put out as what is left of a site.
                if (!_driver.EnterPresence(kerb, ActorLook.Officer, 1.5f, onRoad: false))
                {
                    return;   // the pool has nobody to spare just now
                }
                _roadward = at - kerb;
                _roadward.y = 0f;
                _roadward = _roadward.sqrMagnitude < 0.01f ? facing * Vector3.left : _roadward.normalized;
                _driver.Stand(kerb + _roadward * 6f - Vector3.Cross(Vector3.up, _roadward) * 5f);
                _crewDue = Random.value < SecondOfficerChance;

                _car.SetActive(false);   // moved while off, so the navigation mesh is cut once, in the right place
                _car.transform.SetPositionAndRotation(at, facing);
                _car.SetActive(true);
                MeshCuts.Note("a patrol car parked at a junction");
                _carOut = true;
                _carAt = at;
                _kerb = kerb;
                _carUntil = now + Random.Range(240f, 420f);
                _nextRadio = now + Random.Range(10f, 30f);
                _nextTick = now;
                Logger?.Info($"Police presence: patrol car parked at the kerb {Mathf.Sqrt(sqr):F0} m away, {(_crewDue ? "two officers" : "one officer")} by it.");
                return;
            }
        }

        // A stretch of lane with nothing about it: one way in, one way on, open, not in a junction, and the same for its neighbours.
        private static bool IsPlainLane(Waypoint point)
        {
            TrafficManager traffic = TrafficManager.Instance;
            for (int way = 0; way < 2; way++)
            {
                Waypoint at = point;
                for (int step = 0; step < 3; step++)
                {
                    if (at == null || at.temporaryDisabled || at.IsInIntersection() || at.neighbors == null || at.neighbors.Count != 1
                        || at.prev == null || at.prev.Count != 1)
                    {
                        return false;
                    }
                    at = traffic.GetWaypoint(way == 0 ? at.neighbors[0] : at.prev[0]);
                }
            }
            return true;
        }

        // Beside a lane, on its kerb side: the nearest place out from the lane's middle that passes every test.
        private bool FindKerbPlace(TrafficManager traffic, Waypoint point, out Vector3 at, out Quaternion facing, out Vector3 kerb)
        {
            at = default;
            kerb = default;
            facing = Quaternion.identity;
            Waypoint next = traffic.GetWaypoint(point.neighbors[0]);
            Vector3 along = next != null ? next.position - point.position : Vector3.zero;
            along.y = 0f;
            if (along.sqrMagnitude < 0.25f)
            {
                return false;
            }
            along.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, along);
            facing = Quaternion.LookRotation(along, Vector3.up);

            for (int i = 0; i < KerbOffsets.Length; i++)
            {
                float outward = Mathf.Max(KerbOffsets[i], LaneGap + _halfWidth);
                Vector3 place = point.position + right * outward;
                if (!StreetSpots.Ground(place, out float ground))
                {
                    continue;
                }
                place.y = ground;
                // On the road, with no corner on a pavement or a crossing, touching nothing, and in no other lane
                // (asked last: it is the one test that walks the road graph).
                if (!StreetSpots.OnRoad(place, 0.5f, out _)
                    || OnPavement(place) || OnPavement(place + along * _halfLength + right * _halfWidth) || OnPavement(place + along * _halfLength - right * _halfWidth)
                    || OnPavement(place - along * _halfLength + right * _halfWidth) || OnPavement(place - along * _halfLength - right * _halfWidth)
                    || !SceneParking.IsFree(place, facing, _halfWidth, _halfLength, Room)
                    || traffic.GetClosestWaypoint(place, OtherLane) != null)
                {
                    continue;
                }
                // And a pavement beside it for the officers to stand on.
                if (!StreetSpots.OnPavement(place + right * (_halfWidth + 1.3f), 1.6f, out kerb))
                {
                    continue;
                }
                at = place;
                return true;
            }
            return false;
        }

        private static bool OnPavement(Vector3 point)
        {
            return NavMesh.SamplePosition(point, out _, 0.3f, StreetSpots.Pavement);
        }

        // Once: the game's police car prop with a box round it, kept switched off between sites.
        private bool BuildCar()
        {
            _car = new GameObject("RealLifeOverhaul Parked Patrol");
            _car.transform.SetParent(transform, false);
            _car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            GameObject body = TrafficEffects.CreateParkedPatrol(_car.transform, out Vector3 size);
            if (body == null)
            {
                Destroy(_car);
                _car = null;
                _carFailed = true;
                Logger?.Info("Police presence: the game's police car prop could not be had; no patrol cars are parked. Officers still stand at junctions.");
                return false;
            }
            // The prop may be modelled along either axis: its long side is its length.
            _halfWidth = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.5f, 0.8f, 1.3f);
            _halfLength = Mathf.Clamp(Mathf.Max(size.x, size.z) * 0.5f, 1.8f, 3.2f);
            if (size.x > size.z)
            {
                body.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * body.transform.localRotation;
                body.transform.localPosition = Quaternion.Euler(0f, 90f, 0f) * body.transform.localPosition;
            }

            // Solid for the player's car and for anything that looks for parked vehicles, and for people on foot.
            int layer = LayerMask.NameToLayer("ParkedVehicles");
            if (layer >= 0)
            {
                _car.layer = layer;
                var box = _car.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, Mathf.Max(0.6f, size.y * 0.5f), 0f);
                box.size = new Vector3(_halfWidth * 2f, Mathf.Max(1.2f, size.y), _halfLength * 2f);
            }
            _carBox = _car.AddComponent<NavMeshObstacle>();
            _carBox.shape = NavMeshObstacleShape.Box;
            _carBox.center = new Vector3(0f, 1f, 0f);
            _carBox.size = new Vector3(_halfWidth * 2f + 0.3f, 2f, _halfLength * 2f + 0.3f);
            _carBox.carving = true;
            _carBox.carveOnlyStationary = true;
            _car.SetActive(false);
            return true;
        }

        private void RemoveCar()
        {
            _driver.Exit();
            _partner.Exit();
            _carOut = false;
            _crewDue = false;
            if (_car != null)
            {
                _car.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- the officer at a junction

        private void PlacePost(Vector3 origin, float now)
        {
            TrafficManager traffic = TrafficManager.Instance;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(MinDistance * 0.8f, MaxDistance * 0.85f);
                Vector3 probe = origin + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                Waypoint point = traffic.GetClosestWaypoint(probe, 25f, InJunction);
                // A corner of the junction: the pavement nearest a point a few metres out from its middle, towards the player's side.
                if (point == null)
                {
                    continue;
                }
                Vector3 towards = origin - point.position;
                towards.y = 0f;
                towards = towards.sqrMagnitude < 1f ? Vector3.forward : towards.normalized;
                Vector3 corner = point.position + Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * towards * 9f;
                if (!StreetSpots.OnPavement(corner, 7f, out Vector3 stand) || StreetSpots.IsInView(stand)
                    || (stand - origin).sqrMagnitude < MinDistance * MinDistance * 0.5f
                    || (_carOut && (stand - _carAt).sqrMagnitude < 30f * 30f))
                {
                    continue;
                }
                if (!_post.EnterPresence(stand, ActorLook.Officer, 1.5f, onRoad: false))
                {
                    return;
                }
                _post.Stand(point.position);
                // On watch at the corner, facing the junction. The lights are working and the traffic obeys them, so nobody
                // is waved on: directing traffic is for an accident scene (People/SceneOfficer). Some are on the radio.
                if (Random.value < 0.4f)
                {
                    _post.Work(SceneClips.RadioTalk, RadioPose, RadioProp);
                }
                _postOut = true;
                _postUntil = now + Random.Range(100f, 220f);
                _nextTick = Mathf.Min(_nextTick, now);
                Logger?.Info($"Police presence: an officer is standing at a junction {Vector3.Distance(stand, origin):F0} m away.");
                return;
            }
        }

        private static readonly Predicate<Waypoint> InJunction = IsInJunction;

        private static bool IsInJunction(Waypoint point)
        {
            return point.IsInIntersection();
        }

        // Everything off the street at once. Safe at any time, any number of times.
        private void Clear()
        {
            try
            {
                RemoveCar();
                _post.Exit();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _postOut = false;
        }
    }
}
