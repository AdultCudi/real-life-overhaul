using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Law
{
    /// <summary>The kerb: bollards, the flag pole, and the patrol cars parked along it.</summary>
    internal sealed partial class PrecinctFrontage
    {
        private const float CarWithin = 48f;         // metres from the door a patrol car may stand
        private const float LaneGap = 2.0f;          // metres from the lane's middle to a parked car's side, at least
        private const float OtherLane = 2.2f;        // metres: no other lane's point this near a parked car
        private const float Room = 0.45f;            // metres kept clear round a parked car

        private static readonly float[] KerbOffsets = { 3.0f, 3.3f, 3.6f };   // metres from the lane's middle
        // Metres along the kerb from the door, tried in turn.
        private static readonly float[] BollardAside = { -1.4f, 1.4f, -2.8f, 2.8f, -4.2f, 4.2f };
        private static readonly float[] PoleAside = { 5.4f, -5.4f, 6.6f, -6.6f };

        private void PlaceKerb()
        {
            _bollardsOut = 0;
            string why = "no kerb found in front of the door";
            for (int i = 0; i < Bollards; i++)
            {
                if (_bollards[i] == null)
                {
                    _bollards[i] = StationProps.Holder("RealLifeOverhaul Precinct Bollard " + (i + 1), _root, StationProps.Bollard, Pivot.Floor,
                        new Vector3(0.16f, 0.9f, 0.16f), out _, out bool modelled);
                    if (!modelled)
                    {
                        StationProps.BollardStandIn(_bollards[i]);
                    }
                    StationProps.Solid(_bollards[i], new Vector3(0f, 0.5f, 0f), new Vector3(0.22f, 1f, 0.22f));
                }
                if (!Kerb(BollardAside[i], 0.9f, 0.12f, out Vector3 at, out string reason))
                {
                    why = reason;
                    continue;
                }
                _bollards[i].gameObject.SetActive(false);
                _bollards[i].SetPositionAndRotation(at, Quaternion.LookRotation(_out, Vector3.up));
                _bollards[i].gameObject.SetActive(true);
                MeshCuts.Note("a precinct's bollards and flag pole");
                _bollardsOut++;
            }
            if (_bollardsOut > 0)
            {
                Fit(Piece.Bollards);
            }
            else
            {
                Skip(Piece.Bollards, why);
            }

            if (_pole == null)
            {
                _pole = StationProps.Holder("RealLifeOverhaul Precinct Flag", _root, StationProps.FlagPole, Pivot.Own, new Vector3(1.3f, 5.1f, 0.3f), out _, out bool modelled);
                if (!modelled)
                {
                    StationProps.FlagPoleStandIn(_pole);
                }
                StationProps.Solid(_pole, new Vector3(0f, 1f, 0f), new Vector3(0.3f, 2f, 0.3f));
            }
            why = "no kerb found for it";
            for (int i = 0; i < PoleAside.Length; i++)
            {
                if (!Kerb(PoleAside[i], 5.3f, 0.16f, out Vector3 at, out string reason))
                {
                    why = reason;
                    continue;
                }
                // The flag flies along the kerb, away from the door; its whole sweep must be clear.
                Vector3 fly = _across * (PoleAside[i] > 0f ? 1f : -1f);
                if (Physics.CheckBox(at + fly * 0.65f + Vector3.up * 4.5f, new Vector3(0.7f, 0.5f, 0.12f), Quaternion.LookRotation(_out, Vector3.up), _solidMask, QueryTriggerInteraction.Ignore))
                {
                    why = "something is in the way of the flag";
                    continue;
                }
                _pole.gameObject.SetActive(false);
                _pole.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.Cross(fly, Vector3.up), Vector3.up));   // the flag flies along the holder's +X
                _pole.gameObject.SetActive(true);
                MeshCuts.Note("a precinct's bollards and flag pole");
                Fit(Piece.Pole);
                return;
            }
            Skip(Piece.Pole, why);
        }

        // The kerb strip at a place along the frontage: just past the pavement's edge, with the road beyond it.
        private bool Kerb(float aside, float height, float radius, out Vector3 at, out string why)
        {
            at = default;
            if (!StreetSpots.OnPavement(_front + _across * aside, 0.7f, out Vector3 start))
            {
                why = "no pavement there";
                return false;
            }
            if (!NavMesh.Raycast(start, start + _out * 9f, out NavMeshHit edge, StreetSpots.Pavement))
            {
                why = "the pavement runs on (a square, not a kerb)";
                return false;
            }
            Vector3 beyond = edge.position + _out * 1.1f;
            if (NavMesh.SamplePosition(beyond, out _, 0.35f, StreetSpots.Pavement) || !StreetSpots.OnRoad(beyond, 0.6f, out _))
            {
                why = "what the pavement ends at there is not the road";
                return false;
            }
            at = edge.position + _out * 0.06f;
            if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - _doorGround.y) > 0.4f)
            {
                why = "the ground steps there";
                return false;
            }
            at.y = ground;
            if (Physics.CheckCapsule(at + Vector3.up * (radius + 0.12f), at + Vector3.up * height, radius, _solidMask, QueryTriggerInteraction.Ignore))
            {
                why = "something stands there already";
                return false;
            }
            why = null;
            return true;
        }

        private int CarsOut()
        {
            int count = 0;
            for (int i = 0; i < Cars; i++)
            {
                count += _carOut[i] ? 1 : 0;
            }
            return count;
        }

        private int FreeCar()
        {
            for (int i = 0; i < Cars; i++)
            {
                if (!_carOut[i])
                {
                    return i;
                }
            }
            return -1;
        }

        private void PlaceCars(bool force)
        {
            TrafficManager traffic = TrafficManager.Instance;
            if (traffic == null)
            {
                return;
            }
            Waypoint start = TrafficTools.ClosestWaypoint(_front, 22f);
            if (start == null)
            {
                return;
            }
            for (int way = 0; way < 2 && FreeCar() >= 0; way++)
            {
                Waypoint at = start;
                for (int step = 0; step < 16 && at != null && FreeCar() >= 0; step++)
                {
                    if (CitySpots.Flat(at.position - _dressed.Door) > CarWithin * CarWithin)
                    {
                        break;
                    }
                    if ((way == 0 || step > 0) && IsPlainLane(traffic, at))
                    {
                        TryCar(traffic, at, force);
                    }
                    List<int> next = way == 0 ? at.neighbors : at.prev;
                    at = next != null && next.Count == 1 ? traffic.GetWaypoint(next[0]) : null;
                }
            }
        }

        private void TryCar(TrafficManager traffic, Waypoint point, bool force)
        {
            int slot = FreeCar();
            if (slot < 0 || (_cars[slot] == null && !BuildCar(slot)))
            {
                return;
            }
            if (!FindKerbPlace(traffic, point, out Vector3 at, out Quaternion facing))
            {
                return;
            }
            // Clear of the others by more than a car's length, and never while somebody looks at the spot.
            float apart = _halfLength * 2f + 1.4f;
            for (int other = 0; other < Cars; other++)
            {
                if (other != slot && _carOut[other] && CitySpots.Flat(at - _carAt[other]) < apart * apart)
                {
                    return;
                }
            }
            if (!force && StreetSpots.IsInView(at))
            {
                return;
            }
            GameObject car = _cars[slot];
            car.SetActive(false);   // moved while off, so the navigation mesh is cut once, in the right place
            car.transform.SetPositionAndRotation(at, facing);
            car.SetActive(true);
            MeshCuts.Note("a precinct's parked car");
            _carOut[slot] = true;
            _carAt[slot] = at;
            _logger?.Info($"Law: {_dressed.Name}: patrol car {slot + 1} parked at the kerb {Mathf.Sqrt(CitySpots.Flat(at - _dressed.Door)):F0} m from the door.");
        }

        // A plain stretch of lane: one way in, one way on, open, not in a junction, and the same for its neighbours.
        private static bool IsPlainLane(TrafficManager traffic, Waypoint point)
        {
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

        // Beside a lane on its kerb side, by the tests People/PolicePresence parks by, clear of the bollards.
        private bool FindKerbPlace(TrafficManager traffic, Waypoint point, out Vector3 at, out Quaternion facing)
        {
            at = default;
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
                // On the road with no corner on a pavement, touching nothing, in no other lane, with a pavement beside it.
                if (!StreetSpots.OnRoad(place, 0.5f, out _)
                    || OnPavement(place) || OnPavement(place + along * _halfLength + right * _halfWidth) || OnPavement(place + along * _halfLength - right * _halfWidth)
                    || OnPavement(place - along * _halfLength + right * _halfWidth) || OnPavement(place - along * _halfLength - right * _halfWidth)
                    || !SceneParking.IsFree(place, facing, _halfWidth, _halfLength, Room)
                    || TrafficTools.ClosestWaypoint(place, OtherLane) != null
                    || !StreetSpots.OnPavement(place + right * (_halfWidth + 1.3f), 1.6f, out _)
                    || NearKerbPiece(place, along, right))
                {
                    continue;
                }
                at = place;
                return true;
            }
            return false;
        }

        // The bollards and the flag pole have no collider: a car is kept clear of them by measuring.
        private bool NearKerbPiece(Vector3 place, Vector3 along, Vector3 right)
        {
            for (int i = 0; i <= Bollards; i++)
            {
                Transform piece = i < Bollards ? _bollards[i] : _pole;
                if (piece == null || !piece.gameObject.activeSelf)
                {
                    continue;
                }
                Vector3 offset = piece.position - place;
                if (Mathf.Abs(Vector3.Dot(offset, along)) < _halfLength + 0.4f && Mathf.Abs(Vector3.Dot(offset, right)) < _halfWidth + 0.4f)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool OnPavement(Vector3 point)
        {
            return NavMesh.SamplePosition(point, out _, 0.3f, StreetSpots.Pavement);
        }

        // Once a slot: the game's police car prop with a box round it, switched off between visits.
        private bool BuildCar(int slot)
        {
            var car = new GameObject("RealLifeOverhaul Precinct Patrol " + (slot + 1));
            car.transform.SetParent(_root, false);
            car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            GameObject body = TrafficEffects.CreateParkedPatrol(car.transform, out Vector3 size);
            if (body == null)
            {
                UnityEngine.Object.Destroy(car);
                _carsFailed = true;
                _logger?.Info("Law: the game's police car prop could not be had; the precincts go without parked patrol cars.");
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
            int layer = LayerMask.NameToLayer("ParkedVehicles");
            if (layer >= 0)
            {
                car.layer = layer;
                var box = car.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, Mathf.Max(0.6f, size.y * 0.5f), 0f);
                box.size = new Vector3(_halfWidth * 2f, Mathf.Max(1.2f, size.y), _halfLength * 2f);
            }
            var cut = car.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = new Vector3(0f, 1f, 0f);
            cut.size = new Vector3(_halfWidth * 2f + 0.3f, 2f, _halfLength * 2f + 0.3f);
            cut.carving = true;
            cut.carveOnlyStationary = true;
            car.SetActive(false);
            _cars[slot] = car;
            return true;
        }
    }
}
