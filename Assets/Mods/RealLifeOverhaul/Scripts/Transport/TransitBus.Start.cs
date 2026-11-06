using System.Collections.Generic;
using System.Globalization;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus's part for putting it on the road: where on its line it may start, out of sight, and standing at a stop.</summary>
    public sealed partial class TransitBus
    {
        private const float StartBack = 125f;        // metres of line before the stop it starts from, if it can
        private const float StartBackMost = 330f;    // metres at most
        private const float StartBackLeast = 60f;    // and at least
        private const float StartApart = 14f;        // metres between two places tried to start from
        private readonly List<int> _starts = new List<int>(StartTries);
        private BusVehicle _made;        // the one bus this keeps, made the first time it is sent

        /// <summary>Puts a bus on its line some way before a stop, out of sight unless <paramref name="mayAppearInView"/>. False, with why.</summary>
        public bool Send(TransitStop stop, bool mayAppearInView, out string refusal, float mostBack = StartBackMost, float prefer = StartBack)
        {
            refusal = null;
            if (State != Phase.Idle)
            {
                refusal = "the bus is still out";
                return false;
            }
            if (!StreetReady || stop == null || stop.Line >= TransitMap.Lines.Count)
            {
                refusal = "traffic is not running";
                return false;
            }
            TransitLine line = TransitMap.Lines[stop.Line];
            float closedBack = CollectStarts(line, stop, mayAppearInView, mostBack, prefer);
            if (_starts.Count == 0)
            {
                refusal = closedBack >= 0f
                    ? "the road is closed " + closedBack.ToString("F0") + " m before the stop and no plain lane out of view lies between"
                    : mayAppearInView ? "its line has no plain stretch of lane within " + mostBack.ToString("F0") + " m before this stop"
                    : "every plain stretch of lane it could start from is in view";
                return false;
            }
            BusVehicle bus = Bus();
            if (bus == null)
            {
                refusal = "the bus cannot be drawn (the game has no lit shader)";
                return false;
            }
            int start = -1;
            for (int i = 0; i < _starts.Count && start < 0; i++)
            {
                if (RoomAt(line, _starts[i]))
                {
                    start = _starts[i];
                }
            }
            if (start < 0)
            {
                refusal = "traffic stands on all " + _starts.Count + " of the lane points it could start from";
                return false;
            }
            Vector3 from = TransitMap.Point(line.Path[start]).position;
            Take(bus, stop, line);
            bus.Place(from, TransitMap.Point(line.Path[start + 1]).position - from, line);
            _driver.Take(bus);
            _bay = BayOpen(stop, sendAway: true);
            if (_bay)
            {
                RouteToBay(line, stop, start + 1);
            }
            else
            {
                _laneNext = start + 1;
                RouteToLane(line, stop);
            }
            StartedBack = stop.Metres - line.Metres[start];
            State = Phase.Approaching;
            Lost = false;
            _since = Time.time;
            _startHold.Clear();
            _holdCutTold = false;
            Held0 = 0f;
            AtLights = 0f;
            _tickAt = Time.time;
            Begin();
            Remaining = _driver.RouteLeft;
            _driveSeconds = SecondsLeft;
            DriveAtStart = _driveSeconds;
            LightsAtStart = LightsAhead();
            return true;
        }

        /// <summary>Puts a bus on the lane just past a stop, already leaving: what a rider sees pull away after getting off.</summary>
        public bool SendLeaving(TransitStop stop)
        {
            if (State != Phase.Idle || !StreetReady || stop == null || stop.Line >= TransitMap.Lines.Count)
            {
                return false;
            }
            TransitLine line = TransitMap.Lines[stop.Line];
            // With a bay: where a bus that has just pulled out is, past the bay's end.
            float lead = stop.HasBay ? stop.BayAhead : 0f;
            int last = Mathf.Min(stop.PathIndex + (stop.HasBay ? 14 : 9), line.Path.Length - 3);
            BusVehicle bus = Bus();
            if (bus == null)
            {
                return false;
            }
            int at = -1;
            for (int i = stop.PathIndex + 2; i <= last && at < 0; i += 2)
            {
                Waypoint point = TransitMap.Point(line.Path[i]);
                if (point != null && !point.temporaryDisabled && TrafficManager.IsNotIntersection(point) && line.Metres[i] - stop.Metres >= lead && RoomAt(line, i))
                {
                    at = i;
                }
            }
            if (at < 0)
            {
                return false;
            }
            Take(bus, stop, line);
            bus.Place(TransitMap.Point(line.Path[at]).position, stop.Along, line);
            _driver.Take(bus);
            Onward(line, at);
            return true;
        }

        /// <summary>
        /// Puts a bus standing at a stop as one just pulled in, doors shut: the bus a rider arrives on, set while the screen is
        /// dark. False, with why, if it cannot stand there now.
        /// </summary>
        public bool PutStanding(TransitStop stop, out string refusal)
        {
            refusal = null;
            if (State != Phase.Idle || !StreetReady || stop == null || stop.Line >= TransitMap.Lines.Count)
            {
                refusal = State != Phase.Idle ? "the bus is still out" : "traffic is not running";
                return false;
            }
            BusVehicle bus = Bus();
            if (bus == null)
            {
                refusal = "the bus cannot be drawn (the game has no lit shader)";
                return false;
            }
            TransitLine line = TransitMap.Lines[stop.Line];
            Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
            bool bay = BayOpen(stop, sendAway: true);
            string bayNote = BayNote;
            Vector3 front;
            if (bay)
            {
                front = stop.Lane + stop.Along * (stop.Stand - _middleAhead) + right * _curveIn.Offset;   // where RouteToBay has it come to rest
            }
            else
            {
                front = stop.Lane + stop.Along * (stop.Stand - _middleAhead);            // and RouteToLane
                if (SceneParking.InStrip(stop.Lane - stop.Along * (BodyLength * 0.5f + 1f), stop.Lane + stop.Along * (BodyLength * 0.5f + 1f), BodyHalfWidth + 0.2f,
                        _made != null ? _made.Body : null, TrafficTools.VehicleMask) != null)
                {
                    refusal = "traffic stands on the lane at the stop" + (bayNote != null ? " and " + bayNote : "");
                    return false;
                }
            }
            Take(bus, stop, line);
            _bay = bay;
            BayNote = bayNote;
            bus.Place(front, stop.Along, line);
            _driver.Take(bus);
            Park();
            State = Phase.Standing;
            Lost = false;
            _since = Time.time;
            Took = 0f;
            StartedBack = 0f;
            Remaining = 0f;
            return true;
        }

        // Where it may start: plain lane points of its line before the stop, out of view, none before a closed road; about
        // preferBack first, then further, then nearer. Returns how far back the road is closed, or -1.
        private float CollectStarts(TransitLine line, TransitStop stop, bool mayAppearInView, float mostBack, float preferBack)
        {
            _starts.Clear();
            float closedBack = -1f;
            int floor = 1;
            for (int i = stop.PathIndex; i >= 1 && stop.Metres - line.Metres[i] <= Mathf.Max(mostBack, StartBackMost); i--)
            {
                Waypoint point = TransitMap.Point(line.Path[i]);
                if (point != null && point.temporaryDisabled)
                {
                    floor = i + 2;
                    closedBack = stop.Metres - line.Metres[i];
                    break;
                }
            }
            int ceiling = stop.PathIndex - 3;
            float prefer = Mathf.Clamp(preferBack, StartBackLeast, mostBack);
            int pivot = -1;
            for (int i = ceiling; i >= floor; i--)
            {
                if (stop.Metres - line.Metres[i] >= prefer)
                {
                    pivot = i;
                    break;
                }
            }
            for (int pass = 0; pass < (mayAppearInView ? 2 : 1); pass++)
            {
                bool unseen = pass == 0;
                float lastTaken = float.MinValue;
                for (int i = pivot; i >= floor && _starts.Count < StartTries; i--)   // from the preferred distance, further back
                {
                    float back = stop.Metres - line.Metres[i];
                    if (back > mostBack)
                    {
                        break;
                    }
                    if (back - lastTaken >= StartApart && CanStart(line, i, unseen))
                    {
                        _starts.Add(i);
                        lastTaken = back;
                    }
                }
                lastTaken = float.MaxValue;
                for (int i = Mathf.Max(pivot + 1, floor); i <= ceiling && _starts.Count < StartTries; i++)   // then nearer
                {
                    float back = stop.Metres - line.Metres[i];
                    if (back < StartBackLeast)
                    {
                        break;
                    }
                    if (back <= mostBack && lastTaken - back >= StartApart && CanStart(line, i, unseen))
                    {
                        _starts.Add(i);
                        lastTaken = back;
                    }
                }
                if (_starts.Count > 0)
                {
                    break;
                }
            }
            return closedBack;
        }

        private bool CanStart(TransitLine line, int index, bool unseen)
        {
            Waypoint point = TransitMap.Point(line.Path[index]);
            Waypoint next = index + 1 < line.Path.Length ? TransitMap.Point(line.Path[index + 1]) : null;
            if (point == null || next == null || point.temporaryDisabled || point.enter || point.exit || point.neighbors == null || point.neighbors.Count == 0
                || !TrafficManager.IsNotIntersection(point))
            {
                return false;
            }
            // Never within NearKeep of the player; in sight only as the test button's last resort, far off.
            if (TransitSight.Near(point.position, TransitSight.NearKeep))
            {
                return false;
            }
            Vector3 along = next.position - point.position;
            along.y = 0f;
            Vector3 middle = point.position + (along.sqrMagnitude > 0.0001f ? along.normalized : Vector3.forward) * _middleAhead;
            bool seen = TransitSight.Seen(middle, along, BodyLength * 0.5f + 0.5f);
            return unseen ? !seen : seen && !TransitSight.Near(point.position, 90f);
        }

        /// <summary>
        /// Makes the bus's body now, switched off: the loading screen pays for it (TransitDirector.PrewarmDressings), so that the first
        /// <see cref="Send"/> in play builds nothing. A bus built in a tick took some 20 ms in a player's log (27 Oct 2026). False if the game has no shader to draw it with.
        /// </summary>
        public bool Prepare()
        {
            return Bus() != null;
        }

        private BusVehicle Bus()
        {
            if (_made == null)
            {
                _made = BusVehicle.Make(_host.transform, "RealLifeOverhaul City Bus");
            }
            return _made;
        }

        // Nothing stands where its body would be with its front axle at this point: one box.
        private bool RoomAt(TransitLine line, int index)
        {
            Waypoint point = TransitMap.Point(line.Path[index]);
            Waypoint next = index + 1 < line.Path.Length ? TransitMap.Point(line.Path[index + 1]) : null;
            if (point == null || next == null)
            {
                return false;
            }
            Vector3 along = next.position - point.position;
            along.y = 0f;
            if (along.sqrMagnitude < 0.01f)
            {
                return false;
            }
            along.Normalize();
            Vector3 middle = point.position + along * _middleAhead;
            return SceneParking.InStrip(middle - along * (BodyLength * 0.5f + 1f), middle + along * (BodyLength * 0.5f + 1f), BodyHalfWidth + 0.2f, _made != null ? _made.Body : null,
                       TrafficTools.VehicleMask) == null;
        }

        private void Take(BusVehicle bus, TransitStop stop, TransitLine line)
        {
            Vehicle = bus;
            Stop = stop;
            _line = line;
            _parked = false;
            _gate = -1;
            _gateWhy = Hold.None;
            Waiting = Hold.None;
            _route.Clear();
            _routePoints.Clear();
            _bay = false;
            _bayMode = false;
            _bayLooked = false;
            _awaitGap = false;
            _departed = false;
            _pulledOut = false;
            _goingOut = false;
            _gapWaited = 0f;
            _bayFrom = 0;
            _bayTo = 0;
            _bayRun = 0f;
            BayNote = null;
            SentAway = 0;
            StoodShort = null;
            _hold.Clear();
            Chassis = "the mod's own city bus (" + BodyLength.ToString("F1", CultureInfo.InvariantCulture) + " m long, "
                      + (BodyHalfWidth * 2f).ToString("F2", CultureInfo.InvariantCulture) + " wide, wheelbase " + BusVehicle.Wheelbase.ToString("F1", CultureInfo.InvariantCulture)
                      + "; " + (bus.Modelled ? "modelled" : "in plain shapes: the prop bundle has no city_bus yet") + ")";
        }

        private void Add(TransitLine line, int pathIndex)
        {
            Waypoint point = TransitMap.Point(line.Path[pathIndex]);
            if (point != null)
            {
                _route.Add(point.position);
                _routePoints.Add(line.Path[pathIndex]);
            }
        }
    }
}
