using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Hustles;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The Marketplace with the jobs and the economy (Hustles/HustleBridges):
        ///
        ///   KerbSpot    a car bought is handed over at a free stretch of kerb near the seller's door, found as the
        ///               test tools' car is (Economy/EconomyBridges.KerbSpot, Wiring/DebtLinks).
        ///   HireVan     a junk run or a move with no van of the player's at the door hires the jobs' van for the day
        ///   ReturnVan   (Jobs/JobDirector.HireVan, ReturnVan): the same van, the same fee, taken back once the player is out.
        ///   OnShift     no visit or repair starts while one of the jobs is on (Jobs/JobDirector.OnShift).
        ///
        /// Each is asked once, on a press; a fault is said once (<see cref="Fault"/>) and the default answer given.
        /// </summary>
        public static class HustleLinks
        {
            internal static void Connect()
            {
                HustleBridges.KerbSpot = KerbSpot;
                HustleBridges.HireVan = HireVan;
                HustleBridges.ReturnVan = ReturnVan;
                HustleBridges.OnShift = OnShift;
            }

            internal static void Disconnect()
            {
                HustleBridges.Clear();
            }

            private static bool KerbSpot(Vector3 near, out Vector3 spot, out Quaternion heading)
            {
                spot = default(Vector3);
                heading = Quaternion.identity;
                try
                {
                    return EconomyBridges.KerbSpot != null && EconomyBridges.KerbSpot(near, out spot, out heading);
                }
                catch (System.Exception e)
                {
                    Fault("HustleLinks.KerbSpot", e);
                    return false;
                }
            }

            private static CarController HireVan(Vector3 near, out string reasonKey)
            {
                reasonKey = "reallife_job_no_car";
                try
                {
                    JobDirector jobs = JobDirector.Instance;
                    return jobs != null ? jobs.HireVan(near, out reasonKey) : null;
                }
                catch (System.Exception e)
                {
                    Fault("HustleLinks.HireVan", e);
                    return null;
                }
            }

            private static void ReturnVan()
            {
                try
                {
                    JobDirector.Instance?.ReturnVan();
                }
                catch (System.Exception e)
                {
                    Fault("HustleLinks.ReturnVan", e);
                }
            }

            private static bool OnShift()
            {
                try
                {
                    return JobDirector.OnShift;
                }
                catch (System.Exception e)
                {
                    Fault("HustleLinks.OnShift", e);
                    return false;
                }
            }
        }
    }
}
