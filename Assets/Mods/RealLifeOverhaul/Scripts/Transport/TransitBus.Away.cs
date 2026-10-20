using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus's part for leaving: on along its line or the road, and let go once nobody sees it.</summary>
    public sealed partial class TransitBus
    {
        private const float OnwardMore = 160f;       // metres of line more for a bus let go where somebody is looking: it drives on out of sight

        private void Onward(TransitLine line, int fromIndex)
        {
            Unpark();
            _route.Clear();
            _routePoints.Clear();
            int i = fromIndex + 1;
            for (; i < line.Path.Length && line.Metres[i] - line.Metres[fromIndex] < OnwardMetres; i++)
            {
                Add(line, i);
            }
            _onwardTo = i;
            if (i >= line.Path.Length)
            {
                Extend(OnwardMetres - RouteMetres());   // the end of its line: on along the road, not left standing in it
            }
            State = Phase.Leaving;
            _since = Time.time;
            if (_route.Count < 2)
            {
                Release(remove: false);   // the end of its line: nothing to follow
                return;
            }
            Begin();
            Remaining = _driver.RouteLeft;
        }

        /// <summary>
        /// Every way out. With <paramref name="remove"/>, or when nobody is looking, the bus is taken off the street at once;
        /// otherwise it drives on along its line and goes once nobody is looking.
        /// </summary>
        public void Release(bool remove)
        {
            if (State == Phase.Idle)
            {
                return;
            }
            // Seen, or near the player, it never just goes: it drives on and is taken off once nobody can see it.
            if (!remove && Held && StreetReady && (InView || Near) && GoOut())
            {
                return;
            }
            Abandon();
        }

        // Let go where somebody is looking: on along its line from the point after the last on its route, out of sight.
        private bool GoOut()
        {
            if (_line == null || _goingOut)
            {
                return _goingOut;
            }
            _goingOut = true;
            Unpark();
            Vehicle.Doors(false);
            Vehicle.Indicate(0);
            _route.Clear();
            _routePoints.Clear();
            // On along its line from the next point ahead of it (on its way to its stop that is not the point after the stop: it
            // would cut across to it), and on along the road where the line ends.
            int from = Mathf.Clamp(Mathf.Max(_onwardTo, Stop != null ? Stop.PathIndex + 1 : 0), 0, _line.Path.Length);
            int ahead = LineIndexFromFront(_line);
            if (ahead >= 0 && (State == Phase.Approaching || ahead > from))
            {
                from = ahead;
            }
            for (int i = from; i < _line.Path.Length && _line.Metres[i] - _line.Metres[Mathf.Max(0, from - 1)] < OnwardMore; i++)
            {
                if (Closed(_line.Path[i]))
                {
                    break;   // its line is closed ahead: on along whatever road is open from here (Extend), not into the closure
                }
                Add(_line, i);
            }
            Extend(OnwardMore - RouteMetres());
            State = Phase.Leaving;
            _since = Time.time;
            _awaitGap = false;
            if (_route.Count < 2)
            {
                _gate = -1;        // pulled up at a light, say: it is no longer waiting for one
                _gateWhy = Hold.None;
                _driver.Stop();
                return true;   // nowhere at all to drive to: it stands there until nobody can see it
            }
            Begin();
            Remaining = _driver.RouteLeft;
            return true;
        }

        // Driving off at the end of its route, still seen or near: on along the road, out of sight.
        private void DriveOn()
        {
            int had = _route.Count;
            if (had > 0)
            {
                Vector3 last = _route[had - 1];
                int lane = _routePoints[had - 1];
                _route.Clear();
                _routePoints.Clear();
                _route.Add(last);
                _routePoints.Add(lane);
            }
            Extend(OnwardMore);
            if (_route.Count >= 2)
            {
                _route.RemoveAt(0);
                _routePoints.RemoveAt(0);
                Begin();
                Remaining = _driver.RouteLeft;
            }
        }

        // The first point of its line ahead of where its front axle is, or -1.
        private int LineIndexFromFront(TransitLine line)
        {
            Vector3 front = _driver.Front;
            int best = -1;
            float nearest = 30f * 30f;
            for (int i = 0; i + 1 < line.Path.Length; i++)
            {
                Waypoint point = TransitMap.Point(line.Path[i]);
                Waypoint after = TransitMap.Point(line.Path[i + 1]);
                if (point == null || after == null)
                {
                    continue;
                }
                Vector3 to = point.position - front;
                to.y = 0f;
                Vector3 way = after.position - point.position;
                way.y = 0f;
                if (Vector3.Dot(to, way) > 0.5f && to.sqrMagnitude > 4f && to.sqrMagnitude < nearest)
                {
                    nearest = to.sqrMagnitude;
                    best = i;
                }
            }
            return best;
        }

        private float RouteMetres()
        {
            float metres = 0f;
            for (int i = 1; i < _route.Count; i++)
            {
                metres += Vector3.Distance(_route[i], _route[i - 1]);
            }
            return metres;
        }

        // On along the road graph from the last lane point of its route, as straight on as the road goes, for so many metres: where
        // its line ends the bus drives on out of sight, as a bus to its depot does, not left standing in the road.
        private void Extend(float metres)
        {
            if (metres <= 1f)
            {
                return;
            }
            int lane = -1;
            for (int i = _routePoints.Count - 1; i >= 0 && lane < 0; i--)
            {
                lane = _routePoints[i];
            }
            if (lane < 0 && _route.Count == 0 && _line != null && Stop != null && Stop.PathIndex < _line.Path.Length)
            {
                lane = _line.Path[Mathf.Min(_line.Path.Length - 1, Stop.PathIndex)];   // nothing of its line after the stop: on from the stop's own lane point
            }
            Waypoint point = TransitMap.Point(lane);
            if (point == null)
            {
                return;
            }
            Vector3 heading = _route.Count >= 2 ? _route[_route.Count - 1] - _route[_route.Count - 2] : Vehicle != null ? Vehicle.transform.forward : Vector3.forward;
            heading.y = 0f;
            heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;
            float done = 0f;
            for (int guard = 0; done < metres && guard < 300; guard++)
            {
                int best = -1;
                float straightest = -0.2f;   // never back the way it came
                for (int k = 0; point.neighbors != null && k < point.neighbors.Count; k++)
                {
                    Waypoint next = TransitMap.Point(point.neighbors[k]);
                    if (next == null || next.temporaryDisabled || !RoadGraph.Drivable(next))
                    {
                        continue;
                    }
                    Vector3 way = next.position - point.position;
                    way.y = 0f;
                    if (way.sqrMagnitude < 0.01f)
                    {
                        continue;
                    }
                    float dot = Vector3.Dot(way.normalized, heading);
                    if (dot > straightest)
                    {
                        straightest = dot;
                        best = point.neighbors[k];
                    }
                }
                if (best < 0)
                {
                    break;
                }
                Waypoint chosen = TransitMap.Point(best);
                Vector3 step = chosen.position - point.position;
                step.y = 0f;
                done += step.magnitude;
                heading = step.normalized;
                _route.Add(chosen.position);
                _routePoints.Add(best);
                point = chosen;
            }
        }

        // Taken off the street, or under it: nothing to undo but the bus itself.
        private void Abandon()
        {
            _driver.Forget();
            if (Vehicle != null)
            {
                Vehicle.Hide();
            }
            Forget();
        }

        private void Forget()
        {
            _line = null;
            _parked = false;
            _gate = -1;
            _gateWhy = Hold.None;
            Waiting = Hold.None;
            _route.Clear();
            _routePoints.Clear();
            _bay = false;
            _bayMode = false;
            _awaitGap = false;
            _goingOut = false;
            _bayTo = 0;
            _onwardTo = 0;
            Vehicle = null;
            Stop = null;
            Remaining = 0f;
            State = Phase.Idle;
        }
    }
}
