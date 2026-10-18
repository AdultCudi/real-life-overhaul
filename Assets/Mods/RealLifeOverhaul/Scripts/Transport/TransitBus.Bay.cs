using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus's part for its stop's bay: the route into it, the kerb told to the driver, and waiting for a gap to pull out.</summary>
    public sealed partial class TransitBus
    {
        private const float FinalShort = 1.2f;       // the driver stops this far short of the last point of a route
        private const float BayLeast = 0.6f;         // metres aside of the lane's line from which a standing bus pulls out along a path of its own
        private const float GapBack = 30f;           // metres of lane behind its tail that must be clear for it to pull out
        private const float GapBackLate = 12f;       // once it has waited GapPatience: it edges out as a bus does
        private const float GapAhead = 5f;           // and ahead of its nose, always
        private const float GapHalf = 1.2f;          // metres, half the width of the lane looked at
        private const float GapMost = 45f;           // seconds after which a bus that still cannot pull out is let go where it stands
        private const float KerbNear = 20f;          // metres along the lane either side of a stop's lane point within which its driver knows where its kerb is
        private float _gapSince;

        // Whether it can use its stop's bay now, else why (BayNote); with sendAway the game's parked cars in it go first.
        private bool BayOpen(TransitStop stop, bool sendAway)
        {
            BayNote = null;
            if (!stop.HasBay)
            {
                return false;
            }
            if (!BusBay.Lay(stop.KerbAside, out _, out _curveIn, out _, out float behind, out float ahead))
            {
                BayNote = "the bus is " + (_widthHalf * 2f).ToString("F2", CultureInfo.InvariantCulture) + " m wide, too wide for the bay";
                return false;
            }
            _bayBehind = Mathf.Max(stop.BayBehind, behind);
            _bayAhead = Mathf.Max(stop.BayAhead, ahead);
            if (_bayBehind > stop.BayRoom + 0.01f || _bayAhead > stop.BayAhead + 0.01f)
            {
                BayNote = "the bus reaches " + _noseAhead.ToString("F1", CultureInfo.InvariantCulture) + " m ahead of its front axle and "
                          + _tailBehind.ToString("F1", CultureInfo.InvariantCulture) + " m behind it and needs " + _bayBehind.ToString("F0") + " m of bay to come in and "
                          + _bayAhead.ToString("F0") + " to pull out, where the stop's has " + stop.BayRoom.ToString("F0") + " and " + stop.BayAhead.ToString("F0");
                return false;
            }
            bool free = TransitBays.Free(stop.At, _bayBehind, _bayAhead, Vehicle != null ? Vehicle.Body : null, sendAway, out string taken, out int sent);   // the place's bay: its host's zone
            SentAway += sent;
            if (!free)
            {
                BayNote = "its bay is taken by " + taken;
            }
            return free;
        }

        // The end of a route on the lane's line: where the front axle rests for the bus's middle to stand Stand on from the pole.
        private void RouteToLane(TransitLine line, TransitStop stop)
        {
            for (int i = _laneNext; i < stop.PathIndex; i++)
            {
                Add(line, i);
            }
            _laneNext = stop.PathIndex;
            _route.Add(stop.Lane + stop.Along * (stop.Stand + FinalShort - _middleAhead));
            _routePoints.Add(-1);
        }

        // The end of a route in the stop's bay: the lane's points to where the bay's path leaves them, then the path (BusBay.In).
        private void RouteToBay(TransitLine line, TransitStop stop, int from)
        {
            BusBay.Curve curve = _curveIn;
            float offset = curve.Offset;
            int points = curve.Points;
            float settle = BusBay.Settle;
            float leaves = points * BusBay.Step + settle + _middleAhead + 0.5f - stop.Stand;   // metres of line before the stop where the path leaves the lane
            int i = from;
            for (; i < stop.PathIndex && stop.Metres - line.Metres[i] > leaves; i++)
            {
                Add(line, i);
            }
            _laneNext = i;
            _bayFrom = _route.Count;
            Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
            Vector3 rest = stop.Lane + stop.Along * (stop.Stand - _middleAhead) + right * offset;
            for (int k = points; k >= 1; k--)
            {
                float before = k * BusBay.Step;
                _route.Add(rest - stop.Along * (before + settle) - right * curve.Aside(before));
                _routePoints.Add(-1);
            }
            // Along the bay's line while its rear comes in behind it.
            for (float along = settle; along > 0.01f; along -= BusBay.Step)
            {
                _route.Add(rest - stop.Along * along);
                _routePoints.Add(-1);
            }
            _route.Add(rest);
            _routePoints.Add(-1);
            _route.Add(rest + stop.Along * FinalShort);
            _routePoints.Add(-1);
            _bayRun = points * BusBay.Step + settle + FinalShort;
        }

        // The bay was free when it set out and is not now: the route ends on the lane's line instead.
        private void BackToLane(string why)
        {
            int from = Mathf.Clamp(_bayFrom, 0, _route.Count);
            _route.RemoveRange(from, _route.Count - from);
            _routePoints.RemoveRange(from, _routePoints.Count - from);
            _bay = false;
            BayMode(false);
            BayNote = why;
            RouteToLane(_line, Stop);
        }

        // Its front axle within KerbNear of the stop's lane point, on the lane.
        private bool ByStop(Vector3 front)
        {
            if (Stop == null)
            {
                return false;
            }
            Vector3 off = front - Stop.Lane;
            float along = off.x * Stop.Along.x + off.z * Stop.Along.z;
            float aside = off.x * Stop.Along.z - off.z * Stop.Along.x;   // towards the kerb positive
            return along > -KerbNear && along < KerbNear && aside > -2f && aside < Stop.KerbAside;
        }

        // By its stop the driver knows where the kerb is, so nobody on the pavement is taken to be in its way.
        private void BayMode(bool on)
        {
            if (on == _bayMode || Stop == null)
            {
                return;
            }
            _bayMode = on;
            if (on)
            {
                Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
                _driver.Kerb(Stop.Lane + right * Stop.KerbAside, right);
            }
            else
            {
                _driver.NoKerb();
            }
        }

        /// <summary>Shuts the doors: on the lane's line it pulls away at once; in its bay it waits for the lane to clear (<see cref="TakeDeparted"/>).</summary>
        public void Depart()
        {
            if (State != Phase.Standing || !Held)
            {
                return;
            }
            Vehicle.Doors(false);
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            if (Vector3.Dot(Vehicle.transform.position - Stop.Lane, right) < BayLeast)
            {
                Onward(_line, Stop.PathIndex);
                return;
            }
            _awaitGap = true;
            _gapSince = Time.time;
            State = Phase.Leaving;
            _since = Time.time;
            Vehicle.Indicate(-1);
        }

        // In the bay with the doors shut it gives way to everything in the lane, edging out after GapPatience; no gap in GapMost
        // and it is let go where it stands.
        private void AwaitGap(float now)
        {
            if (!Vehicle.DoorsShut)
            {
                return;   // its doors first
            }
            float waited = now - _gapSince;
            if (LaneClear(waited >= GapPatience ? GapBackLate : GapBack))
            {
                _gapWaited = waited;
                _awaitGap = false;
                _departed = true;
                _pulledOut = true;
                PullOut(now);
            }
            else if (waited > GapMost)
            {
                _gapWaited = waited;
                _awaitGap = false;
                _departed = true;
                _pulledOut = false;
                Release(remove: false);
            }
        }

        // Nothing but itself in the lane beside it, from so far behind its tail to a little beyond its nose.
        private bool LaneClear(float back)
        {
            Vector3 middle = Vehicle.transform.position - Stop.Lane;
            middle.y = 0f;
            float at = Vector3.Dot(middle, Stop.Along);
            float halfLength = BodyLength * 0.5f;
            Vector3 from = Stop.Lane + Stop.Along * (at - halfLength - back);
            Vector3 to = Stop.Lane + Stop.Along * (at + halfLength + GapAhead);
            return SceneParking.InStrip(from, to, GapHalf, Vehicle.Body, TrafficTools.VehicleMask) == null;
        }

        // Out of the bay by a path of its own (BusBay.Out), then the lane's own points.
        private void PullOut(float now)
        {
            TransitLine line = _line;
            Vector3 right = Vector3.Cross(Vector3.up, Stop.Along);
            Vector3 stands = _driver.Front - Stop.Lane;
            stands.y = 0f;
            float along = Vector3.Dot(stands, Stop.Along);
            float aside = Vector3.Dot(stands, right);
            BusBay.Curve curve = BusBay.Out(aside, BusBay.BusLeave, BusBay.RunMost - Mathf.Max(0f, along));
            int points = curve.Points;
            Unpark();
            _route.Clear();
            _routePoints.Clear();
            for (int k = 1; k <= points; k++)
            {
                float gone = k * BusBay.Step;
                _route.Add(Stop.Lane + Stop.Along * (along + gone) + right * (aside - curve.Aside(gone)));
                _routePoints.Add(-1);
            }
            _bayFrom = 0;
            _bayTo = _route.Count;
            float rejoins = along + points * BusBay.Step + 0.5f;
            int i = Stop.PathIndex + 1;
            for (; i < line.Path.Length && line.Metres[i] - Stop.Metres < OnwardMetres; i++)
            {
                if (line.Metres[i] - Stop.Metres > rejoins)
                {
                    Add(line, i);
                }
            }
            _onwardTo = i;
            if (i >= line.Path.Length)
            {
                Extend(OnwardMetres - RouteMetres());   // the end of its line: on along the road
            }
            _since = now;
            if (_route.Count < 2)
            {
                Release(remove: false);   // nowhere to go: let go where it stands
                return;
            }
            // At the end of its line it still pulls out onto the lane, and is let go there.
            Begin();
            BayMode(true);
            _driver.CruiseSpeed = 1.5f;
            Remaining = _driver.RouteLeft;
        }
    }
}
