using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Traffic accidents: staging a collision, the wrecks taken out of traffic, placed, dressed and given back.</summary>
    public sealed partial class TrafficAccidents : MonoBehaviour
    {
        private const float PartnerDistance = 40f;  // metres: the second car of a collision is looked for this near the first
        private const int OpenRoadPoints = 7;       // road points each way that must be plain lane for a stretch to count as open road
        private const float TumbleChance = 0.4f;        // of severe accidents
        private const float HitAndRunChance = 0.25f;    // of accidents with two or more cars that did not roll

        private sealed class Wreck
        {
            public VehicleComponent Car;
            public AiCarRescueCheck RescueCheck;
            public bool WasKinematic;
            public RigidbodyConstraints Constraints;
            public bool Towed;                      // removed when the scene clears, instead of driving off
            public readonly List<Renderer> Seat = new List<Renderer>(4);   // the driver shown in its cab, hidden while that driver is out
            public readonly List<GameObject> Effects = new List<GameObject>();
            public readonly List<int> Preset = new List<int>();   // marks the car as ours; see Seize
        }

        private static Severity RollSeverity()
        {
            float roll = Random.value;
            return roll < 0.40f ? Severity.Minor : roll < 0.78f ? Severity.Moderate : Severity.Severe;
        }

        private static readonly int HotBegin = HotSpots.Slot("Traffic: TrafficAccidents.BeginAt");
        /// <summary>A collision just happened on the road: the scene starts where the cars came to rest. False if none could start: the caller keeps the car.</summary>
        /// <param name="first">The car that caused it: any car still on the road.</param>
        /// <param name="second">The ordinary traffic car it met; null for a crash on its own.</param>
        /// <param name="noticeKey">What is said about it, by its cause; null for the usual sentence of its severity.</param>
        /// <param name="firstVelocity">The first car's velocity just after it struck; zero leaves it where it is.</param>
        public bool BeginAt(VehicleComponent first, VehicleComponent second, float closingSpeed, string noticeKey, Vector3 firstVelocity = default, Vector3 secondVelocity = default)
        {
            using var hotSpot = HotSpots.Time(HotBegin);
            try
            {
                if (_lacking || _phase != Phase.None || first == null || !first.gameObject.activeInHierarchy || !ModState.Loaded || !TrafficTools.Ready
                    || Wiring.Scenes.AnyBut(Wiring.Scene.Accident))
                {
                    return false;
                }
                if (second != null && (second == first || !IsUsable(second)))
                {
                    second = null;   // a taxi, an ambulance, a car somebody else of the mod holds: it was hit, but it is nobody's wreck
                }
                if (_recheck)
                {
                    Recheck();
                }
                Vector3 player = PlayerHelper.GetPosition();
                _severity = (Severity)StreetRules.Severity(closingSpeed);
                _inPlace = true;
                _causeKey = noticeKey;
                StartScene(first.transform);
                _involved.Clear();
                _involved.Add(first.rb);
                if (second != null)
                {
                    _involved.Add(second.rb);
                }
                bool tumble = _severity == Severity.Severe && Random.value < TumbleChance && Rolls(first) && CanTumble(first, player);

                _leadWreck = Seize(first);
                Wreck other = second != null ? Seize(second) : null;
                Logger?.Info($"Accident: a collision on the road, closing at {closingSpeed * 3.6f:F0} km/h: {_severity}, {_wrecks.Count} car(s), "
                             + $"{Vector3.Distance(player, _scene):F0} m away{(tumble ? ", lead car rolling" : "")}.");
                if (tumble)
                {
                    if (other != null)
                    {
                        Pin(other, second.transform.position, second.transform.rotation);
                    }
                    BeginTumble(first);
                    return true;
                }
                if (BeginShunt(first, second, firstVelocity, secondVelocity))
                {
                    return true;
                }
                if (other != null)
                {
                    Pin(other, second.transform.position, second.transform.rotation);
                }
                Pin(_leadWreck, _origin, _heading);
                CompleteStaging(tumbled: false);
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Clear(resumeTraffic: true, announce: false);
                return false;
            }
        }

        private static readonly int HotTry = HotSpots.Slot("Traffic: TrafficAccidents.TryStageAccident");
        private void TryStageAccident()
        {
            using var hotSpot = HotSpots.Time(HotTry);
            _inPlace = false;
            _causeKey = null;
            Vector3 player = PlayerHelper.GetPosition();
            List<VehicleComponent> pool = Manager.GetVehicleList();
            VehicleComponent first = FindLeadCar(player);
            if (first == null)
            {
                return;
            }

            _severity = _forcedSeverity ?? RollSeverity();
            // A collision takes two cars whenever a second can be found; only a severe one may take three.
            int wanted = _severity == Severity.Severe ? Random.Range(2, 4) : 2;

            _others.Clear();
            while (_others.Count < wanted - 1)
            {
                VehicleComponent nearest = null;
                float bestSqr = PartnerDistance * PartnerDistance;
                foreach (VehicleComponent car in pool)
                {
                    if (car == first || _others.Contains(car) || !IsUsable(car))
                    {
                        continue;
                    }
                    float sqr = (car.transform.position - first.transform.position).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        nearest = car;
                    }
                }
                if (nearest == null)
                {
                    break;
                }
                _others.Add(nearest);
            }

            StartScene(first.transform);

            // Bodies that belong to this accident; everything else must not be overlapped.
            _involved.Clear();
            _involved.Add(first.rb);
            foreach (VehicleComponent other in _others)
            {
                _involved.Add(other.rb);
            }

            bool tumble = _severity == Severity.Severe && Random.value < TumbleChance && Rolls(first) && CanTumble(first, player);

            // They stop being traffic now; the others are moved into the crash once the lead car's pose is known.
            _leadWreck = Seize(first);
            foreach (VehicleComponent other in _others)
            {
                Pin(Seize(other), other.transform.position, other.transform.rotation);
            }
            _others.Clear();

            Logger?.Info($"Accident: {_severity}, {_wrecks.Count} car(s), {Vector3.Distance(player, _scene):F0} m away{(tumble ? ", lead car rolling" : "")}.");

            if (tumble)
            {
                BeginTumble(first);
                return;
            }

            float skew = _severity == Severity.Minor ? Random.Range(6f, 14f)
                : _severity == Severity.Moderate ? Random.Range(18f, 36f)
                : Random.Range(38f, 75f);

            // Spin the lead car as far as there is room for: full, half, the other way, or not at all.
            Quaternion leadRotation = _heading;
            for (int i = 0; i < 4; i++)
            {
                float angle = (i < 2 ? skew : -skew) * (i % 2 == 0 ? 1f : 0.5f) * _side;
                Quaternion candidate = _heading * Quaternion.Euler(0f, angle, 0f);
                if (IsPoseFree(first, _origin, candidate))
                {
                    leadRotation = candidate;
                    break;
                }
            }
            Pin(_leadWreck, _origin, leadRotation);
            CompleteStaging(tumbled: false);
        }

        private void StartScene(Transform lead)
        {
            _origin = lead.position;
            _heading = lead.rotation;
            _scene = _origin;
            _side = Random.value < 0.5f ? -1f : 1f;
            _playerHelped = false;
            _attended = false;
            _fled = false;
            _lingerUntil = 0f;
            _chase = Chase.None;
        }

        /// <summary>An ordinary traffic car on the move 45 to 140 m from the player, or null. The search starts at a random car.</summary>
        public VehicleComponent FindMovingCar(Vector3 player)
        {
            return FindMovingCar(player, openRoadOnly: false);
        }

        // One on a plain stretch if there is any: lanes to close, room for the units, no crossing through it.
        private VehicleComponent FindLeadCar(Vector3 player)
        {
            return FindMovingCar(player, openRoadOnly: true) ?? FindMovingCar(player, openRoadOnly: false);
        }

        private VehicleComponent FindMovingCar(Vector3 player, bool openRoadOnly)
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            int start = Random.Range(0, pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[(start + i) % pool.Count];
                if (!IsUsable(car) || car.GetCurrentSpeed() < _searchSpeed)
                {
                    continue;
                }
                float sqr = (car.transform.position - player).sqrMagnitude;
                if (sqr > _searchFrom * _searchFrom && sqr < _searchTo * _searchTo && (!openRoadOnly || IsOnOpenRoad(car)))
                {
                    return car;
                }
            }
            return null;
        }

        // True if the lane the car is in runs plain for a few road points both ways: no junction, no fork, no merge.
        private static bool IsOnOpenRoad(VehicleComponent car)
        {
            TrafficManager traffic = TrafficManager.Instance;
            Waypoint target = traffic.GetTargetWaypoint(car.GetIndex());
            if (target == null)
            {
                return false;
            }
            for (int way = 0; way < 2; way++)
            {
                Waypoint point = target;
                for (int step = 0; step < OpenRoadPoints && point != null; step++)
                {
                    if (point.IsInIntersection() || point.neighbors == null || point.neighbors.Count != 1 || point.prev == null || point.prev.Count != 1)
                    {
                        return false;
                    }
                    point = traffic.GetWaypoint(way == 0 ? point.neighbors[0] : point.prev[0]);
                }
            }
            return true;
        }

        private void CompleteStaging(bool tumbled)
        {
            Vector3 forward = _heading * Vector3.forward;
            Vector3 right = _heading * Vector3.right;
            VehicleComponent first = _leadWreck.Car;

            // None of this for a collision that happened on the road: those cars are where they met.
            // The third car first (dropped if there is no room): it swerved and ended up alongside, further out past a rolled car.
            if (_wrecks.Count > 2 && !_inPlace)
            {
                float gap = tumbled ? Random.Range(3.6f, 4.4f) : Random.Range(2.3f, 2.9f);
                Vector3 position = _scene - right * _side * gap - forward * Random.Range(0.5f, 3f);
                Quaternion rotation = _heading * Quaternion.Euler(0f, Random.Range(15f, 40f) * -_side, 0f);
                if (!MoveIfFree(_wrecks[2], position, rotation))
                {
                    LetGo(_wrecks[2]);
                }
            }
            if (_wrecks.Count > 1 && !_inPlace)
            {
                // Rear-end shunt: the second car into the back of the first, or the other way if there is no room; else it was not in it.
                VehicleComponent second = _wrecks[1].Car;
                float reach = first.length * 0.5f + second.length * 0.5f - 0.3f;
                Vector3 behind = _scene - forward * reach;
                if (!MoveIfFree(_wrecks[1], behind + right * Random.Range(-0.7f, 0.7f), _heading * Quaternion.Euler(0f, Random.Range(-18f, 18f), 0f))
                    && !MoveIfFree(_wrecks[1], behind, _heading)
                    && !MoveIfFree(_wrecks[1], _scene + forward * reach, _heading))
                {
                    LetGo(_wrecks[1]);
                }
            }
            bool alone = _wrecks.Count == 1;
            if (alone && !tumbled && !_inPlace)
            {
                // One car on its own left the lane: over towards the side it is skewed to, if there is room.
                Transform body = first.transform;
                Vector3 aside = body.position + right * (_side * Random.Range(1f, 1.5f));
                if (IsPoseFree(first, aside, body.rotation))
                {
                    Pin(_leadWreck, aside, body.rotation);
                    _scene = new Vector3(aside.x, _scene.y, aside.z);
                }
            }
            _single = alone;
            _seatsEmptied = false;

            // Said only now, when it is known how many cars are really in it.
            string key = _causeKey != null ? _causeKey : alone
                ? (_severity == Severity.Minor ? "reallife_accident_single_minor"
                    : _severity == Severity.Moderate ? "reallife_accident_single_moderate"
                    : "reallife_accident_single_severe")
                : (_severity == Severity.Minor ? "reallife_accident_minor"
                    : _severity == Severity.Moderate ? "reallife_accident_moderate"
                    : "reallife_accident_severe");
            Notice(_severity == Severity.Severe ? NotificationType.Error : NotificationType.Warning, key, 8f);

            // Some are hit-and-runs: the lead driver bolts as the police turn up (in place, only if the other car is not ahead).
            if (Settings.Police && !tumbled && _wrecks.Count > 1 && Random.value < HitAndRunChance
                && (!_inPlace || Vector3.Dot(_wrecks[1].Car.transform.position - first.transform.position, forward) < 0f))
            {
                PlanChase(first);
            }
            bool planned = _chase == Chase.Planned;

            // The damage goes on the lead car, unless that one is about to drive off.
            Wreck worst = planned ? _wrecks[1] : _leadWreck;
            for (int i = 0; i < _wrecks.Count; i++)
            {
                AddHazards(_wrecks[i]);
            }
            Dress(worst, alone);
            if (_severity == Severity.Severe && !planned && _wrecks.Count > 1)
            {
                AddEffect(_wrecks[1], TrafficEffects.CreateSmoke(_wrecks[1].Car, heavy: false));
            }

            for (int i = 0; i < _wrecks.Count; i++)
            {
                _wrecks[i].Towed = _wrecks[i] == worst ? _severity != Severity.Minor : _severity == Severity.Severe;
            }
            if (planned)
            {
                _leadWreck.Towed = false;
            }

            // The timetable. With police attending, the smaller accidents run a little longer so the visit fits.
            float now = Time.time;
            bool police = Settings.Police;
            float total = _severity == Severity.Minor ? (police ? Random.Range(70f, 100f) : Random.Range(35f, 65f))
                : _severity == Severity.Moderate ? (police ? Random.Range(90f, 130f) : Random.Range(80f, 125f))
                : Random.Range(125f, 170f);
            if (planned)
            {
                total = Mathf.Max(total, Random.Range(150f, 170f));
            }
            _deadline = now + Mathf.Min(total, MaxSceneTime);

            _worstWreck = worst;
            _otherWreck = planned ? _leadWreck : _wrecks.Count > 1 ? _wrecks[1] : null;
            VehicleComponent otherCar = _otherWreck != null ? _otherWreck.Car : null;
            _people.Begin(_severity, _scene, worst.Car, otherCar, planned, now);
            if (!planned)
            {
                // The lead driver caused it and stayed: questioned and ticketed, not arrested; only a hit-and-run's runner is arrested.
                _people.AtFault(worst: true);
            }

            // Each unit is sent after its own delay, so who gets there first varies. A minor bump closes nothing.
            PlanResponse(now, _severity != Severity.Minor, police, now + Random.Range(5f, 13f),
                now + (planned ? Random.Range(3f, 10f) : Random.Range(3f, 22f)), null, shut: _severity != Severity.Minor);

            // Glass and plastic where the cars met (or where one met whatever stopped it), for the officers to mark.
            if (_severity == Severity.Severe)
            {
                _cordon.Litter(Impact(), forward);
            }

            News.Report(NewsKind.Accident, _scene, null, (int)_severity);
            using var piece = SessionWatch.Piece("traffic: an accident being staged");
            Logger?.Info($"Accident {(_inPlace ? "scene begun where it happened" : "staged")}: {_severity}, {_wrecks.Count} car(s)"
                         + $"{(alone ? " (single vehicle)" : "")}, clears within {total:F0} s{(planned ? ", hit-and-run planned" : "")}.");
        }

        // Where the damage was done: between the first two cars, or just ahead of the nose of one on its own.
        private Vector3 Impact()
        {
            TrafficTools.Footprint(_wrecks[0].Car, out Vector3 centre, out Vector3 forward, out float halfLength, out _);
            Vector3 impact = centre + forward * (halfLength + 0.5f);
            if (_wrecks.Count > 1)
            {
                TrafficTools.Footprint(_wrecks[1].Car, out Vector3 second, out _, out _, out _);
                impact = (centre + second) * 0.5f;
            }
            impact.y = _scene.y;
            return impact;
        }

        // A vehicle that shows its driver in the cab (the delivery truck) has that figure hidden while the driver is out.
        // Only what was showing is hidden, and Unpin shows exactly that again: the figure belongs to a pooled car.
        private static void EmptySeat(Wreck wreck, bool onRoad)
        {
            if (wreck == null || wreck.Car == null || wreck.Seat.Count > 0)
            {
                return;
            }
            var cab = wreck.Car.GetComponentInChildren<DeliveryTruckDriverController>(true);
            if (cab == null || cab.driverAppearanceSetter == null)
            {
                return;
            }
            cab.driverAppearanceSetter.GetComponentsInChildren(true, SeatScratch);
            // The drawn figure is the merged mesh, on an object of its own: taken under both names the game keeps it by.
            Renderer merged = cab.driverAppearanceSetter.lastMergedMeshRenderer;
            if (merged != null && !SeatScratch.Contains(merged))
            {
                SeatScratch.Add(merged);
            }
            var combiner = cab.driverAppearanceSetter.meshCombiner;
            Renderer combined = combiner != null && combiner.resultMergeGameObject != null ? combiner.resultMergeGameObject.GetComponent<Renderer>() : null;
            if (combined != null && !SeatScratch.Contains(combined))
            {
                SeatScratch.Add(combined);
            }
            for (int i = 0; i < SeatScratch.Count; i++)
            {
                Renderer renderer = SeatScratch[i];
                if (renderer != null && !renderer.forceRenderingOff)
                {
                    renderer.forceRenderingOff = true;
                    wreck.Seat.Add(renderer);
                }
            }
            SeatScratch.Clear();
            if (wreck.Seat.Count > 0)
            {
                Logger?.Info($"Accident: the driver of '{TrafficTools.Name(wreck.Car)}' "
                             + (onRoad ? "is out on the road" : "is played by nobody at this scene, and does not sit at the wheel of a wreck through it")
                             + $"; the figure in its cab ({wreck.Seat.Count} renderer(s)) is hidden until the car is let go.");
            }
        }

        private static readonly List<Renderer> SeatScratch = new List<Renderer>(8);

        private static void FillSeat(Wreck wreck)
        {
            for (int i = 0; i < wreck.Seat.Count; i++)
            {
                if (wreck.Seat[i] != null)
                {
                    wreck.Seat[i].forceRenderingOff = false;
                }
            }
            wreck.Seat.Clear();
        }

        // Measures the road round everything lying in it and shuts it: traffic stops short and new traffic turns off.
        private void CloseRoad(ICasualty casualty, bool shut)
        {
            _footprints.Clear();
            for (int i = 0; i < _wrecks.Count; i++)
            {
                // Where the wreck really lies, skew and all (its colliders), so that no end of it crosses the tape.
                TrafficTools.Footprint(_wrecks[i].Car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
                _footprints.Add(new Footprint { Centre = centre, Forward = forward, HalfLength = halfLength + 0.15f, HalfWidth = halfWidth + 0.15f, Vehicle = true });
            }
            if (casualty != null)
            {
                _footprints.Add(new Footprint { Centre = casualty.Position, Forward = Vector3.forward, HalfLength = 1.9f, HalfWidth = 1.9f });   // room for the crew and the stretcher beside them
            }
            else if (_people.WillLie(out Vector3 where, out Vector3 along))
            {
                _footprints.Add(new Footprint { Centre = where, Forward = along, HalfLength = 1.6f, HalfWidth = 1.9f });
            }
            _cordon.Close(_scene, _heading * Vector3.forward, _footprints, shut);
        }

        private bool IsPoseFree(VehicleComponent car, Vector3 position, Quaternion rotation)
        {
            return SceneParking.IsFree(car, position, rotation, _involved);
        }

        // Taxis, private drivers and scripted routes are left alone, and an ambulance never crashes or runs.
        private static bool IsUsable(VehicleComponent car)
        {
            return car != null && car.gameObject.activeInHierarchy && car.presetPath == null
                   && !TrafficTools.IsAmbulance(car) && !TrafficTools.SpokenFor(car)
                   && !TrafficManager.Instance.IsVehicleIgnored(car.GetIndex());
        }

        // Takes the car away from the traffic system. It is not pinned yet.
        private Wreck Seize(VehicleComponent car)
        {
            Manager.SetVehicleIgnored(car.GetIndex(), true);
            Rigidbody body = car.rb;
            var wreck = new Wreck
            {
                Car = car,
                WasKinematic = body.isKinematic,
                Constraints = body.constraints,
                // The game deletes AI cars that sit still for 20-40 seconds.
                RescueCheck = TrafficTools.SuspendRescue(car)
            };
            // A scripted route, even an empty one, stops the game removing the car for being far from the player or sweeping it
            // away with traffic: a wreck staged at the edge of the traffic range could otherwise vanish once announced.
            car.presetPath = wreck.Preset;
            TrafficTools.Touch(car, "taken out of traffic as a wreck");
            _wrecks.Add(wreck);
            return wreck;
        }

        // A car that was drawn in but has no room in the crash: it was never in it, and drives on.
        private void LetGo(Wreck wreck)
        {
            _wrecks.Remove(wreck);
            Unpin(wreck);
            if (wreck.Car != null)
            {
                _involved.Remove(wreck.Car.rb);
                TrafficTools.ReturnToTraffic(wreck.Car, "the accident scene: no room for it in the crash");
            }
        }

        private static void Pin(Wreck wreck, Vector3 position, Quaternion rotation)
        {
            Rigidbody body = wreck.Car.rb;
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            wreck.Car.transform.SetPositionAndRotation(position, rotation);
        }

        private bool MoveIfFree(Wreck wreck, Vector3 position, Quaternion rotation)
        {
            if (!IsPoseFree(wreck.Car, position, rotation))
            {
                return false;
            }
            wreck.Car.transform.SetPositionAndRotation(position, rotation);
            return true;
        }

        // Takes everything off a wreck and gives the car its own physics back. It stays out of traffic until released.
        private static void Unpin(Wreck wreck)
        {
            FillSeat(wreck);
            foreach (GameObject effect in wreck.Effects)
            {
                if (effect != null)
                {
                    Destroy(effect);
                }
            }
            wreck.Effects.Clear();
            if (wreck.RescueCheck != null)
            {
                wreck.RescueCheck.enabled = true;
            }
            if (wreck.Car != null)
            {
                SceneObstacles.Remove(wreck.Car);
                if (wreck.Car.presetPath == wreck.Preset)
                {
                    wreck.Car.presetPath = null;
                }
                wreck.Car.rb.constraints = wreck.Constraints;
                wreck.Car.rb.isKinematic = wreck.WasKinematic;
                TrafficTools.Touch(wreck.Car, "unpinned by the accident scene");
            }
        }

        private static void AddEffect(Wreck wreck, GameObject effect)
        {
            if (effect != null)
            {
                wreck.Effects.Add(effect);
            }
        }

        private static void AddHazards(Wreck wreck)
        {
            Color amber = new Color(1f, 0.55f, 0.05f);
            AddEffect(wreck, TrafficEffects.CreateFlasher(wreck.Car, "RealLifeOverhaul Hazard", TrafficEffects.BeaconHeight(wreck.Car), amber, amber, 0.5f));
        }

        private void Dress(Wreck wreck, bool alone)
        {
            VehicleComponent car = wreck.Car;
            if (_severity == Severity.Minor)
            {
                // A car on its own must show why it has stopped: bits on the road and a wisp from the bonnet.
                if (alone || Random.value < 0.5f)
                {
                    AddEffect(wreck, TrafficEffects.CreateDebris(car, _scene, Random.Range(3, 6)));
                }
                if (alone)
                {
                    AddEffect(wreck, TrafficEffects.CreateSmoke(car, heavy: false));
                }
                var horn = car.GetComponentInChildren<AiCarHorn>();
                if (horn != null)
                {
                    TrafficTools.Hoot(horn);
                }
                return;
            }

            AddEffect(wreck, TrafficEffects.CreateDebris(car, _scene, _severity == Severity.Severe ? Random.Range(11, 18) : Random.Range(6, 10)));
            AddEffect(wreck, TrafficEffects.CreateSmoke(car, heavy: _severity == Severity.Severe));
            if (_severity == Severity.Severe && Random.value < 0.6f)
            {
                AddEffect(wreck, TrafficEffects.CreateFire(car));
            }
        }
    }
}
