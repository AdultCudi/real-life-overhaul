using System;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A stop's bay as it is now: whether its length of the strip is free. The bus zone (BusZones) keeps the game from
    /// parking there; what this finds came since. One of the game's parked cars in the bay, where nobody is looking, is
    /// handed back to the game's store the way the game takes them in itself (ParkingLaneGenerator.CleanupParkedVehicles);
    /// anything else (the player's car, a delivery van, a wreck) leaves the bay taken and the bus stops in the lane. The box
    /// runs from the lane's edge to the bus's door side, so a sign on the kerb is not counted. One box a time, when a stop
    /// is dressed or a bus comes up to it.
    /// </summary>
    public static class TransitBays
    {
        private const float BoxFrom = 0.35f;      // metres above the road the box starts: over the kerb, under every bumper
        private const float BoxHalf = 0.75f;      // metres, half the box's height
        private const float LaneSide = 0.10f;     // the box starts this far beyond the lane's edge: what drives in the lane is not in it

        private static readonly Collider[] Found = new Collider[32];
        private static int _parkedLayer = -2;

        /// <summary>
        /// True if nothing stands in the stop's bay between <paramref name="behind"/> and <paramref name="ahead"/> metres of
        /// the pole, <paramref name="except"/> aside. With <paramref name="sendAway"/>, the game's parked cars in it that
        /// nobody is looking at are taken in first. <paramref name="taken"/> names what stands in it.
        /// </summary>
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
                taken = "more than can be counted";   // the buffer is full
            }
            for (int i = 0; i < count; i++)
            {
                Collider collider = Found[i];
                Found[i] = null;
                if (collider == null || !collider.gameObject.activeInHierarchy || (except != null && collider.attachedRigidbody == except))
                {
                    continue;   // a parked car just taken in is switched off, its other pieces with it
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
                // Only the lane's own children on that layer are what the lane takes in and puts out.
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
