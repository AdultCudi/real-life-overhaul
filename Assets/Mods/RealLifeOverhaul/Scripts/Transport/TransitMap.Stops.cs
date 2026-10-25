using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus network's part for the stops: where along a line each goes, its pole, its boarding strip and where the bus stands.</summary>
    public static partial class TransitMap
    {
        private const float FirstStopAt = 90f;      // metres from the start of a path
        private const float StopWindow = 140f;      // metres after the place wanted in which a stop is looked for
        private const float JunctionClear = 26f;    // metres of plain lane either side of a stop
        private const float KerbMost = 5.6f;        // metres from the lane's middle to the pavement's edge, at most
        private const float KerbLeast = 1.3f;       // and at least
        private const float BayOn = 60f;            // metres beyond a stop's window in which a place with a bay is still taken
        private const float BayBack = 50f;          // and before the place wanted: two stops are never nearer than StopSpacing less this
        private static Func<int, int> _hostAt;
        private static float _stopWanted;       // metres along it at which its next stop is wanted
        private static int _stopFrom;           // and the point of its path the look goes on from

        /// <summary>About as many places as the routes' stops will be laid at, from the routes alone; the places themselves once laid.</summary>
        public static int PlacesLikely()
        {
            if (Ready)
            {
                return Places.Count;
            }
            int likely = 0;
            for (int i = 0; i < Lines.Count; i++)
            {
                float[] metres = Lines[i].Metres;
                likely += metres != null && metres.Length > 0 ? TransitPlan.StopsLikely(metres[metres.Length - 1], FirstStopAt, StopSpacing) : 0;
            }
            return likely;
        }

        // The next stop of a line, or the finding that the next stretch has none. True once the line has all its stops.
        private static bool PlaceStop(TransitLine line)
        {
            if (line.Wanted != null)
            {
                return PlaceWanted(line);   // a district's own route: its stops where they are wanted (TransitMap.Routes)
            }
            if (!_lineBegun)
            {
                _lineBegun = true;
                _stopWanted = FirstStopAt;
                _stopFrom = 0;
            }
            float total = line.Metres[line.Metres.Length - 1];
            float wanted = _stopWanted;
            int i = _stopFrom;
            if (i >= line.Path.Length || wanted >= total - 40f)
            {
                Terminus(line);
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    line.Stops[s].Name = line.Stops[s].IsHost ? NameOf(line.Stops[s].Wait) : line.Stops[s].At.Name;
                }
                _lineBegun = false;
                return true;
            }
            // A stop another line has already laid on this stretch: this line stops there too.
            int hostId = TransitPlan.HostAlong(line.Path, line.Metres, Mathf.Max(wanted - BayBack, FirstStopAt), wanted + StopWindow + BayOn, wanted, 3,
                _hostAt ?? (_hostAt = HostAt), out int hostIndex);
            if (hostId >= 0)
            {
                TransitStop shared = Share(line, hostIndex, Hosts[hostId]);
                if (shared != null)
                {
                    line.Stops.Add(shared);
                    _shared++;
                    CallsAt(Hosts[hostId]);
                    _stopWanted = shared.Metres + StopSpacing;
                    _stopFrom = shared.PathIndex;
                    return false;
                }
            }
            while (i < line.Path.Length && line.Metres[i] < wanted)
            {
                i++;
            }
            TransitStop plain = null;
            TransitStop bay = null;
            int j = i;
            for (; bay == null && j < line.Path.Length && line.Metres[j] <= wanted + StopWindow; j++)
            {
                TransitStop stop = Try(line, j, plain != null);
                if (stop != null && stop.HasBay)
                {
                    bay = stop;
                }
                else if (stop != null && plain == null)
                {
                    plain = stop;
                }
            }
            int windowEnd = j;
            // None in the window: a little further on, then a little before, nearest first.
            for (; bay == null && j < line.Path.Length && line.Metres[j] <= wanted + StopWindow + BayOn; j++)
            {
                bay = Try(line, j, true);
            }
            // Never nearer the start of the line than a first stop: a bus has to be put on the road before it.
            for (int k = i - 1; bay == null && k >= 0 && line.Metres[k] >= Mathf.Max(wanted - BayBack, FirstStopAt); k--)
            {
                bay = Try(line, k, true);
            }
            TransitStop placed = bay ?? plain;
            if (placed != null)
            {
                placed.LanePoint = line.Path[placed.PathIndex];
                placed.Host = placed;
                if (!HostIndex.ContainsKey(placed.LanePoint))
                {
                    HostIndex[placed.LanePoint] = Hosts.Count;
                    Hosts.Add(placed);
                }
                line.Stops.Add(placed);
                _stopWanted = placed.Metres + StopSpacing;
                _stopFrom = placed.PathIndex;
            }
            else
            {
                _stopWanted = wanted + StopWindow;
                _stopFrom = windowEnd;
            }
            return false;
        }

        // The line's last stop as near its station as the street allows, where its riders change.
        private const float TerminusGap = 120f;     // metres

        private static void Terminus(TransitLine line)
        {
            float total = line.Metres[line.Metres.Length - 1];
            float last = line.Stops.Count > 0 ? line.Stops[line.Stops.Count - 1].Metres : FirstStopAt - TerminusGap;
            if (total - last <= TerminusGap)
            {
                return;
            }
            int hostId = TransitPlan.HostAlong(line.Path, line.Metres, last + TerminusGap, total, total, 3, _hostAt ?? (_hostAt = HostAt), out int hostIndex);
            if (hostId >= 0)
            {
                TransitStop shared = Share(line, hostIndex, Hosts[hostId]);
                if (shared != null)
                {
                    line.Stops.Add(shared);
                    _shared++;
                    CallsAt(Hosts[hostId]);
                    return;
                }
            }
            for (int j = line.Path.Length - 4; j >= 3 && line.Metres[j] >= last + TerminusGap; j--)
            {
                TransitStop stop = Try(line, j, false);
                if (stop == null)
                {
                    continue;
                }
                stop.LanePoint = line.Path[stop.PathIndex];
                stop.Host = stop;
                if (!HostIndex.ContainsKey(stop.LanePoint))
                {
                    HostIndex[stop.LanePoint] = Hosts.Count;
                    Hosts.Add(stop);
                }
                line.Stops.Add(stop);
                return;
            }
        }

        public const int MostLinesAt = 8;           // lines that may stop at one place, the host's own counted
        private static readonly Dictionary<TransitStop, int> SharedBy = new Dictionary<TransitStop, int>(256);   // lines stopping at each place so far

        private static void CallsAt(TransitStop host)
        {
            SharedBy[host] = (SharedBy.TryGetValue(host, out int lines) ? lines : 1) + 1;
        }

        private static int HostAt(int lanePoint)
        {
            return HostIndex.TryGetValue(lanePoint, out int index) ? index : -1;
        }

        // This line's own stop at a place another line laid out; null if the lane is not plain either side on this line's way.
        // The bay is the place's only if this line's own way into it is as straight as the bay needs.
        private static TransitStop Share(TransitLine line, int index, TransitStop host, float clear = JunctionClear)
        {
            if (SharedBy.TryGetValue(host, out int lines) && lines >= MostLinesAt)
            {
                return null;   // as many lines as one place is timetabled for
            }
            int[] path = line.Path;
            for (int k = index - 1; k >= 0 && line.Metres[index] - line.Metres[k] < clear; k--)
            {
                if (!Plain(_points[path[k]]))
                {
                    return null;
                }
            }
            for (int k = index + 1; k < path.Length && line.Metres[k] - line.Metres[index] < clear; k++)
            {
                if (!Plain(_points[path[k]]))
                {
                    return null;
                }
            }
            var stop = new TransitStop
            {
                PathIndex = index, Lane = host.Lane, Along = host.Along, Kerb = host.Kerb, Sign = host.Sign, Wait = host.Wait, ShelterAt = host.ShelterAt,
                Shelter = host.Shelter, Parking = host.Parking, Metres = line.Metres[index], KerbAside = host.KerbAside, HasBay = host.HasBay, Bay = host.Bay,
                BayAlong = host.BayAlong, BayOffset = host.BayOffset, BayBehind = host.BayBehind, BayAhead = host.BayAhead, BayRoom = host.BayRoom, NoBay = host.NoBay,
                LanePoint = host.LanePoint, Host = host, Stand = host.Stand, KerbThings = host.KerbThings
            };
            if (stop.HasBay && BusBay.Lay(stop.KerbAside, out _, out BusBay.Curve curveIn, out BusBay.Curve curveOut, out _, out _))
            {
                float leaves = curveIn.Length + BusBay.Settle + BusBay.BusMiddleAhead + 1f;
                float rejoins = curveOut.Length - BusBay.BusMiddleAhead + 1f;
                string bends = LaneBends(stop, line, Vector3.Cross(Vector3.up, stop.Along), leaves, rejoins);
                if (bends != null)
                {
                    stop.HasBay = false;
                    stop.NoBay = "on the " + line.Number + "'s way into the bay " + bends;
                }
            }
            return stop;
        }

        // A stop at one lane point of a line, or null if it will not do (or has no bay, with bayOnly), with a clear stretch of
        // plain lane either side: the paired lines' own, or a shorter one where a district wants a stop between near crossings.
        private static TransitStop Try(TransitLine line, int index, bool bayOnly, float clear = JunctionClear)
        {
            int[] path = line.Path;
            if (index < 3 || index >= path.Length - 3 || HostIndex.ContainsKey(path[index]))
            {
                return null;   // a lane point another line has laid a place at is that place's: shared, never laid twice
            }
            Waypoint point = _points[path[index]];
            if (!Plain(point))
            {
                return null;
            }
            for (int k = index - 1; k >= 0 && line.Metres[index] - line.Metres[k] < clear; k--)
            {
                if (!Plain(_points[path[k]]))
                {
                    return null;
                }
            }
            for (int k = index + 1; k < path.Length && line.Metres[k] - line.Metres[index] < clear; k++)
            {
                if (!Plain(_points[path[k]]))
                {
                    return null;
                }
            }
            Vector3 before = Flat(point.position - _points[path[index - 3]].position);
            Vector3 after = Flat(_points[path[index + 3]].position - point.position);
            if (before.sqrMagnitude < 1f || after.sqrMagnitude < 1f || Vector3.Angle(before, after) > 9f)
            {
                return null;
            }
            Vector3 along = (before + after).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, along);
            Vector3 lane = point.position;
            if (!CitySpots.Ground(lane, out float ground) || Mathf.Abs(ground - lane.y) > 1f)
            {
                return null;   // a bridge, a ramp, a tunnel
            }
            lane.y = ground;

            // The pavement's edge to the right: the first point that is pavement, with road before it.
            float edge = -1f;
            Vector3 kerb = default;
            for (float d = KerbLeast; d <= KerbMost; d += 0.3f)
            {
                if (IsPavement(lane + right * d, out Vector3 found))
                {
                    edge = d;
                    kerb = found;
                    break;
                }
            }
            if (edge < 0f || edge <= KerbLeast + 0.01f)
            {
                return null;   // none, or the lane point itself is walkable: not a kerb understood
            }
            // Another lane between this one and the kerb would run its traffic between the bus and the people.
            if (LaneToRight(point, right))
            {
                return null;
            }
            var stop = new TransitStop { PathIndex = index, Lane = lane, Along = along, Kerb = kerb, Metres = line.Metres[index] };
            if (bayOnly)
            {
                LayBay(stop, line, right);
                if (!stop.HasBay)
                {
                    return null;
                }
            }
            Vector3 sign = lane + right * (edge + 0.5f);
            Vector3 wait = lane + right * (edge + 1.25f) - along * 1.6f;
            if (!IsPavement(sign, out sign) || !IsPavement(wait, out wait) || !IsPavement(lane + right * (edge + 1.5f) + along * 1.5f, out _)
                || !Open(sign, 0.3f) || !Open(wait, 0.45f) || !Open(wait - along * 1.1f, 0.4f))
            {
                return null;
            }
            stop.Sign = sign;
            stop.Wait = wait;
            FindBoarding(stop, right, edge);
            stop.Parking = _parkingMask != 0 && Physics.CheckSphere(lane + right * (edge * 0.5f + 0.6f) + Vector3.up * 0.5f, 0.6f, _parkingMask, QueryTriggerInteraction.Collide);
            if (!bayOnly)
            {
                LayBay(stop, line, right);
            }
            // A shelter stands back from the kerb ahead of the pole, only where the pavement is deep and bare enough.
            Vector3 shelter = lane + right * (edge + 2.35f) + along * 3.2f;
            if (IsPavement(shelter, out Vector3 floor) && IsPavement(shelter + right * 0.85f + along * 1.7f, out _) && IsPavement(shelter + right * 0.85f - along * 1.7f, out _)
                && IsPavement(shelter - right * 1.6f + along * 1.7f, out _) && IsPavement(shelter - right * 1.6f - along * 1.7f, out _)
                && !Physics.CheckBox(floor + Vector3.up * 1.3f, new Vector3(1.05f, 1.1f, 1.95f), Quaternion.LookRotation(along), _staticMask | _doorMask, QueryTriggerInteraction.Collide)
                && !Physics.CheckSphere(floor + Vector3.up, 3.2f, _doorMask, QueryTriggerInteraction.Collide))
            {
                stop.Shelter = true;
                stop.ShelterAt = floor;
            }
            // The pole clear of the doors; then the bus stands where neither door lines up with what stays on the kerb.
            if (!PlaceSign(stop, right, edge))
            {
                return null;
            }
            Stand(stop, right, edge);
            if (Mathf.Abs(stop.Stand) > 0.01f)
            {
                FindBoarding(stop, right, edge);
            }
            return stop;
        }

        private static readonly Collider[] BoardHits = new Collider[24];
        private const float BoardingPieceMost = 2.6f;   // metres across a piece of furniture may be at the most to be switched off

        // The game's plain street furniture on the boarding strip (BusZones.PropOf), never a tree or anything of the mod's.
        private static void FindBoarding(TransitStop stop, Vector3 right, float kerb)
        {
            stop.Boarding.Clear();
            stop.BoardingMade = false;
            if (_propsLayer < 0)
            {
                return;
            }
            Vector3 toWait = stop.Wait - stop.Lane;
            BusInside.Strip[] strips = BusInside.BoardingStrips(kerb, Vector3.Dot(Flat(toWait), stop.Along), Vector3.Dot(Flat(toWait), right), stop.Stand);
            for (int s = 0; s < strips.Length; s++)
            {
                BusInside.Strip strip = strips[s];
                Vector3 a = stop.Lane + stop.Along * strip.Along0 + right * strip.Aside0;
                Vector3 b = stop.Lane + stop.Along * strip.Along1 + right * strip.Aside1;
                Vector3 way = Flat(b - a);
                if (way.sqrMagnitude < 0.01f)
                {
                    continue;
                }
                Vector3 centre = (a + b) * 0.5f;
                centre.y = stop.Wait.y + 1.2f;
                int found = Physics.OverlapBoxNonAlloc(centre, new Vector3(strip.Half, 1.1f, strip.Length * 0.5f), BoardHits, Quaternion.LookRotation(way.normalized), 1 << _propsLayer,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < found; i++)
                {
                    Collider hit = BoardHits[i];
                    BoardHits[i] = null;
                    GameObject prop = hit != null ? BoardingPiece(hit) : null;
                    if (prop != null && !stop.Boarding.Contains(prop) && (stop.Zone == null || !stop.Zone.Clear.Contains(prop)))
                    {
                        stop.Boarding.Add(prop);
                    }
                }
            }
        }

        private static GameObject BoardingPiece(Collider hit)
        {
            if (hit.attachedRigidbody != null || hit.gameObject.layer != _propsLayer || hit.GetComponentInParent<TreeController>() != null)
            {
                return null;   // a thing that moves, not street furniture, or a street tree
            }
            Transform root = hit.transform.root;
            if (root != null && root.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal))
            {
                return null;   // the mod's own: the shelter, the stop's sign
            }
            Vector3 size = hit.bounds.size;
            if (size.x > BoardingPieceMost || size.z > BoardingPieceMost)
            {
                return null;   // a kiosk, a wall: not a piece of furniture to take away
            }
            GameObject piece = BusZones.PropOf(hit);
            return piece != null && !BusZones.MustStay(piece) ? piece : null;
        }

        // The pole at the first of BusInside.SignPlaces that is free pavement clear of the doors; false if none is. The
        // pavement's spot found for the pole may lie up to 0.3 m off the place asked, and the kerb found up to 0.3 m off the
        // step that found it, so the pole is judged again where it stands against the kerb as found: the figures the
        // poles' check (TransitMap.Log) reads, so every pole laid passes it.
        private static bool PlaceSign(TransitStop stop, Vector3 right, float kerb)
        {
            Vector3 toWait = Flat(stop.Wait - stop.Lane);
            float waitAlong = Vector3.Dot(toWait, stop.Along), waitAside = Vector3.Dot(toWait, right);
            float found = Vector3.Dot(Flat(stop.Kerb - stop.Lane), right);
            for (int i = 0; i < BusInside.SignPlaces.Length; i++)
            {
                if (i == BusInside.SignByShelter && !stop.Shelter)
                {
                    continue;
                }
                BusInside.Spot place = BusInside.SignPlaces[i];
                float aside = kerb + place.X;
                if (!BusInside.SignClearAlways(place.Z, aside, kerb, waitAlong, waitAside))
                {
                    continue;
                }
                if (IsPavement(stop.Lane + right * aside + stop.Along * place.Z, out Vector3 at) && Open(at, 0.3f))
                {
                    Vector3 toPole = Flat(at - stop.Lane);
                    if (!BusInside.SignClearAlways(Vector3.Dot(toPole, stop.Along), Vector3.Dot(toPole, right), found, waitAlong, waitAside))
                    {
                        continue;
                    }
                    stop.Sign = at;
                    return true;
                }
            }
            return false;
        }

        private static readonly Collider[] KerbHits = new Collider[24];
        private static readonly float[] KerbAt = new float[26];
        private static readonly float[] KerbHalfs = new float[26];

        // Where the bus stands by the pole (BusInside.StandShift), from what stays on the kerb by its doors: one box.
        private static void Stand(TransitStop stop, Vector3 right, float kerb)
        {
            stop.Stand = 0f;
            stop.KerbThings = 0;
            int count = 0;
            float from = BusInside.MiddleDoor - BusInside.DoorHalf - 2.5f;
            float to = BusInside.FrontDoor + BusInside.DoorHalf + 2.5f;
            float inner = kerb - 0.3f;
            float outer = kerb + BusInside.DoorStep;
            Vector3 centre = stop.Lane + stop.Along * ((from + to) * 0.5f) + right * ((inner + outer) * 0.5f);
            centre.y = stop.Wait.y + 1.2f;
            int found = _propsLayer < 0 ? 0 : Physics.OverlapBoxNonAlloc(centre, new Vector3((outer - inner) * 0.5f, 1.0f, (to - from) * 0.5f), KerbHits,
                Quaternion.LookRotation(stop.Along), 1 << _propsLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found; i++)
            {
                Collider hit = KerbHits[i];
                KerbHits[i] = null;
                if (hit == null || hit.attachedRigidbody != null || count >= KerbAt.Length - 1)
                {
                    continue;
                }
                Transform root = hit.transform.root;
                if (root != null && root.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal))
                {
                    continue;   // the mod's own: the pole is counted below
                }
                GameObject piece = BoardingPiece(hit);
                if (piece != null && stop.Boarding.Contains(piece))
                {
                    continue;   // switched off while the buses run
                }
                Bounds bounds = hit.bounds;
                bool tree = hit.GetComponentInParent<TreeController>() != null;
                KerbAt[count] = Vector3.Dot(Flat(bounds.center - stop.Lane), stop.Along);
                KerbHalfs[count] = tree ? TrunkHalf : Mathf.Abs(bounds.extents.x * stop.Along.x) + Mathf.Abs(bounds.extents.z * stop.Along.z);
                count++;
            }
            Vector3 pole = Flat(stop.Sign - stop.Lane);
            if (Vector3.Dot(pole, right) <= outer + BusInside.PoleReach)
            {
                KerbAt[count] = Vector3.Dot(pole, stop.Along);
                KerbHalfs[count] = BusInside.PoleReach;
                count++;
            }
            stop.KerbThings = count;
            stop.Stand = stop.HasBay ? BusInside.StandShift(KerbAt, KerbHalfs, count, 0f, BusInside.BayStandMost)
                                     : BusInside.StandShift(KerbAt, KerbHalfs, count, -1f, 1f);
        }

        private static bool LaneToRight(Waypoint point, Vector3 right)
        {
            for (int i = 0; point.otherLanes != null && i < point.otherLanes.Count; i++)
            {
                Waypoint beside = Point(point.otherLanes[i]);
                if (beside != null && Vector3.Dot(beside.position - point.position, right) > 1f)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Plain(Waypoint point)
        {
            return RoadGraph.Plain(point);
        }

        private static bool IsPavement(Vector3 place, out Vector3 spot)
        {
            return CitySpots.Pavement(place, 0.3f, out spot) && CitySpots.Flat(spot - place) < 0.3f * 0.3f;
        }

        private static bool Open(Vector3 spot, float radius)
        {
            return !Physics.CheckCapsule(spot + Vector3.up * (0.35f + radius), spot + Vector3.up * 2.4f, radius, _staticMask, QueryTriggerInteraction.Ignore)
                   && !Physics.CheckSphere(spot + Vector3.up, 2.4f, _doorMask, QueryTriggerInteraction.Collide);
        }

        // A stop is called by the address of the nearest building.
        private static string NameOf(Vector3 place)
        {
            CityBuildingController nearest = null;
            float best = 150f * 150f;
            for (int i = 0; _buildings != null && i < _buildings.Length; i++)
            {
                CityBuildingController building = _buildings[i];
                if (building == null || building.building == null)
                {
                    continue;
                }
                Vector3 offset = building.transform.position - place;
                float sqr = offset.x * offset.x + offset.z * offset.z;
                if (sqr < best)
                {
                    best = sqr;
                    nearest = building;
                }
            }
            try
            {
                if (nearest != null && nearest.building.Address != null && !nearest.building.Address.IsUndefined())
                {
                    return nearest.building.Address.ToFormattedString();
                }
            }
            catch (Exception)
            {
                // an address that cannot be written out: the stop goes by its number
            }
            return null;
        }
    }
}
