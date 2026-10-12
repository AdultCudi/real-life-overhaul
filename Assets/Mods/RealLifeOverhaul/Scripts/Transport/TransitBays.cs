using System;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A stop's bay on the street as it is now: whether its length of the strip is free, and the
    /// one thing done to keep it so. A bay is a bus zone (BusZones): once the zone is made the game
    /// parks no more cars there and its furniture there is switched off, so what this finds is
    /// what came since or could not be taken in view: one of the game's parked cars standing in the
    /// bay, where nobody is looking, is handed back to the game's own store of them, the way the
    /// game itself takes them in (<c>ParkingLaneGenerator.CleanupParkedVehicles</c>: the lane's own
    /// release call, then <c>ParkingSimulator.ReleaseParkedVehicle</c>). Nothing is ever taken in
    /// view, and nothing else is touched: a vehicle of the player's, a van on a delivery job, a
    /// wreck, a cart or whatever else stands there leaves the bay taken, and the bus stops in the
    /// lane as it used to. The box looked in is the one the bay was laid out by: from the lane's
    /// edge to where the bus's door side will be, so that a sign or a bin on the kerb's edge is
    /// not taken for something in the bay, and what was found at the kerb then and judged no
    /// hindrance (a street tree's broad collider) is not counted now.
    ///
    /// Asked when a stop is dressed, when a bus is put on the road for it and once as that bus
    /// comes up to it: one box each time, never on a timer.
    /// </summary>
    public static class TransitBays
    {
        private const float BoxFrom = 0.35f;      // metres above the road the box starts: over the kerb, under every bumper
        private const float BoxHalf = 0.75f;
        private const float LaneSide = 0.10f;     // the box starts this far beyond the lane's edge: what drives in the lane is not in it

        private static readonly Collider[] Found = new Collider[32];
        private static int _parkedLayer = -2;

        /// <summary>
        /// True if nothing stands in the stop's bay. With <paramref name="sendAway"/>, the game's parked cars in it that
        /// nobody is looking at are taken in first.
        /// </summary>
        /// <param name="behind">Metres of the strip back from the pole that are looked at (the stop's own BayBehind, or what the vehicle asking needs).</param>
        /// <param name="ahead">...and on from it.</param>
        /// <param name="except">A vehicle that does not count (the bus that is asking), or null.</param>
        /// <param name="taken">What stands in it, when it is not free.</param>
        /// <param name="sentAway">How many parked cars were taken in.</param>
        public static bool Free(TransitStop stop, float behind, float ahead, Rigidbody except, bool sendAway, out string taken, out int sentAway)
        {
            taken = null;
            sentAway = 0;
            if (stop == null || !stop.HasBay)
            {
                taken = "no bay";
                return false;
            }
            int mask = SceneParking.RoadsideMask;
            if (mask == 0)
            {
                return true;
            }
            Vector3 right = Vector3.Cross(Vector3.up, stop.BayAlong);
            float from = BusBay.LaneHalf + LaneSide;
            float width = stop.KerbAside - BusBay.KerbGap - from;   // to where the bus's door side will be (TransitMap.StripBad)
            Vector3 centre = stop.Lane + stop.BayAlong * ((ahead - behind) * 0.5f) + right * (from + width * 0.5f);
            centre.y = stop.Bay.y + BoxFrom + BoxHalf;
            int count = Physics.OverlapBoxNonAlloc(centre, new Vector3(width * 0.5f, BoxHalf, (behind + ahead) * 0.5f), Found, Quaternion.LookRotation(stop.BayAlong, Vector3.up),
                mask, QueryTriggerInteraction.Ignore);
            if (count >= Found.Length)
            {
                taken = "more than can be counted";   // (the buffer is full: not a bay to drive into)
            }
            for (int i = 0; i < count; i++)
            {
                Collider collider = Found[i];
                Found[i] = null;
                if (collider == null || !collider.gameObject.activeInHierarchy || (except != null && collider.attachedRigidbody == except))
                {
                    continue;   // (a parked car taken in a moment ago is switched off: its other pieces are no longer there)
                }
                if (stop.Zone != null && stop.Zone.Known.Contains(collider))
                {
                    continue;   // found at the kerb when the bay was laid out, and no hindrance
                }
                if (sendAway && SendAway(collider))
                {
                    sentAway++;
                    continue;
                }
                if (taken == null)
                {
                    Rigidbody body = collider.attachedRigidbody;
                    taken = "'" + (body != null ? body.name : collider.transform.root.name) + "'";
                }
            }
            return taken == null;
        }

        // One of the game's parked cars for show, standing where nobody is looking: back into the game's store of them.
        private static bool SendAway(Collider collider)
        {
            try
            {
                if (_parkedLayer == -2)
                {
                    _parkedLayer = LayerMask.NameToLayer("ParkedVehicles");
                }
                Transform car = collider.transform;
                ParkingLaneGenerator lane = car.GetComponentInParent<ParkingLaneGenerator>();
                if (lane == null || _parkedLayer < 0)
                {
                    return false;
                }
                while (car.parent != null && car.parent != lane.transform)
                {
                    car = car.parent;
                }
                // (The lane's own children on that layer are what the lane itself takes in and puts out: nothing else is.)
                if (car.parent != lane.transform || car.gameObject.layer != _parkedLayer || CitySpots.InView(car.position))
                {
                    return false;
                }
                lane.onReleaseVehicle?.Invoke(car.gameObject);
                ParkingSimulator.ReleaseParkedVehicle(car.gameObject);
                return true;
            }
            catch (Exception e)
            {
                TransitDirector.Logger?.Error(e);
                return false;
            }
        }
    }
}
