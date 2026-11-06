using System.Collections.Generic;
using System.Globalization;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The bus's part for going round what stops it for good (BusDetour): a lane closed ahead (the works, an accident's cordon)
    /// or something standing still in its lane, as the city's traffic goes round them, by a lane beside it running the same way
    /// and back onto its route beyond. Asked from the hold's look four times a second, but tried once a hold: a few lanes beside
    /// its route walked, no physics, a list kept for it.
    /// </summary>
    public sealed partial class TransitBus
    {
        private const float PassBeyond = 8f;    // metres beyond what holds it its route is blocked for: the length of a car, and room
        private const float PassClear = 14f;    // metres more of its route on which what it goes round is not looked at

        private static readonly MapLanes Lanes = new MapLanes();
        private readonly List<int> _way = new List<int>(BusDetour.MostPoints);

        // The traffic system's lane points as the way round sees them.
        private sealed class MapLanes : IBusLanes
        {
            public bool Open(int point)
            {
                Waypoint p = TransitMap.Point(point);
                return p != null && !p.temporaryDisabled && RoadGraph.Drivable(p);
            }

            public float X(int point) => TransitMap.Point(point)?.position.x ?? 0f;
            public float Z(int point) => TransitMap.Point(point)?.position.z ?? 0f;
            public int NextCount(int point) => TransitMap.Point(point)?.neighbors?.Count ?? 0;
            public int Next(int point, int k) => TransitMap.Point(point).neighbors[k];
            public int SideCount(int point) => TransitMap.Point(point)?.otherLanes?.Count ?? 0;
            public int Side(int point, int k) => TransitMap.Point(point).otherLanes[k];
        }

        // Held up: by a closed lane ahead it stands before (a moment), or behind something standing still (half a minute), it
        // goes round by a lane beside it. True if it set off round. Tried once a hold; the log says what came of it.
        private bool Round(float now, float still, Collider by)
        {
            bool closed = _gate >= 0 && !_driver.Active && _gateWhy == Hold.Closure;
            bool traffic = !closed && _driver.Active && Waiting == Hold.Traffic && by != null && !IsPlayer(by);
            if (!closed && !traffic)
            {
                return false;
            }
            Rigidbody body = by != null ? by.attachedRigidbody : null;
            VehicleComponent car = body != null ? body.GetComponent<VehicleComponent>() : null;
            bool holderStill = body == null || body.velocity.sqrMagnitude < 0.25f;
            bool yields = car != null && WaitsForBus(car) && !OnCarsWay(car, body);   // that car is told to go on (YieldTo) instead
            bool unitBehind = !closed && UnitBehind();   // a lit emergency vehicle waits behind it: it makes way at once
            if (!RideRules.GoRound(closed, still, holderStill, yields, _hold.Rounded, unitBehind))
            {
                return false;
            }
            _hold.Rounded = true;
            string what = (closed ? "a lane ahead that a scene or the works has closed" : Describe(by)) + (unitBehind ? ", with a lit emergency vehicle coming up behind it" : "");
            if (_bayTo > 0 || _awaitGap || State == Phase.Standing)
            {
                return false;   // pulling out of its bay, or at its stop: nothing to go round
            }
            int next = Mathf.Max(0, _driver.NextPoint);
            int blockFrom, blockTo;
            if (closed)
            {
                blockFrom = -1;
                for (int i = Mathf.Max(next, _gate); i < _routePoints.Count && blockFrom < 0; i++)
                {
                    Waypoint point = _routePoints[i] >= 0 ? TransitMap.Point(_routePoints[i]) : null;
                    blockFrom = point != null && point.temporaryDisabled ? i : -1;
                }
                blockTo = blockFrom;
                while (blockTo + 1 < _routePoints.Count && _routePoints[blockTo + 1] >= 0 && Closed(_routePoints[blockTo + 1]))
                {
                    blockTo++;
                }
            }
            else
            {
                blockFrom = _driver.PointAhead(_driver.HeldAhead);
                blockTo = _driver.PointAhead(_driver.HeldAhead + PassBeyond);
            }
            bool bayAhead = State == Phase.Approaching && _bay;
            int leave = -1, rejoin = -1;
            bool found = blockFrom >= next && BusDetour.Find(Lanes, _routePoints, next, blockFrom, blockTo, _driver.Front.x, _driver.Front.z, _way, out leave, out rejoin)
                         && (!bayAhead || rejoin <= _bayFrom);
            if (!found)
            {
                Logger?.Info("Transit check (round): the " + (_line != null ? _line.Number : "bus") + " has stood " + still.ToString("F0") + " s for " + what
                             + "; no lane beside its route runs past it, so it waits as the traffic there does (the director moves it on out of sight): PASS.");
                return false;
            }
            // Its route from the next point: up to where it leaves, the way round, and on from where it rejoins.
            int dropped = rejoin - leave - 1;
            _route.RemoveRange(leave + 1, dropped);
            _routePoints.RemoveRange(leave + 1, dropped);
            for (int k = 0; k < _way.Count; k++)
            {
                Waypoint point = TransitMap.Point(_way[k]);
                _route.Insert(leave + 1 + k, point.position);
                _routePoints.Insert(leave + 1 + k, _way[k]);
            }
            _route.RemoveRange(0, next);
            _routePoints.RemoveRange(0, next);
            if (bayAhead)
            {
                _bayFrom = Mathf.Max(0, _bayFrom - dropped + _way.Count - next);
            }
            Unpark();
            float metres = 0f;
            for (int i = leave - next + 1; i < leave - next + 1 + _way.Count && i < _route.Count; i++)
            {
                metres += i > 0 ? Vector3.Distance(_route[i], _route[i - 1]) : 0f;
            }
            Begin();
            Waiting = Hold.None;
            if (!closed)
            {
                _driver.Pass(by, _driver.HeldAhead + PassBeyond + PassClear);
            }
            Remaining = _driver.RouteLeft;
            Logger?.Info("Transit check (round): the " + (_line != null ? _line.Number : "bus") + " had stood " + still.ToString("F0") + " s for " + what
                         + "; it goes round by the lane beside it, " + _way.Count + " lane point(s), " + metres.ToString("F0", CultureInfo.InvariantCulture)
                         + " m, back on its route beyond it: PASS.");
            return true;
        }

        private static bool Closed(int point)
        {
            Waypoint p = TransitMap.Point(point);
            return p == null || p.temporaryDisabled;
        }
    }
}
