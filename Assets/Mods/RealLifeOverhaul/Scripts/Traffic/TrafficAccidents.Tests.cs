using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Traffic accidents: the test buttons, and the wider search for a moving car they share.</summary>
    public sealed partial class TrafficAccidents : MonoBehaviour
    {
        /// <summary>Options-menu test buttons: stage an accident right away, of a chosen severity or a random one.</summary>
        public void StageAccidentNow(Severity? severity = null)
        {
            try
            {
                TestRefusal why = _lacking ? TestRefusal.FeatureOff : TestRefusals.Street();
                if (why == TestRefusal.None && _phase != Phase.None)
                {
                    why = TestRefusal.SceneActive;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Accident", why);
                    return;
                }
                if (_recheck)
                {
                    Recheck();
                }
                StreetCrime.EndActive();   // one incident at a time, and the tester asked for this one
                _forcedSeverity = severity;
                // As the mod itself stages one first; then nearer and further; then any car that is moving at all.
                for (int pass = 0; pass < TestSearch.Length && _phase == Phase.None; pass++)
                {
                    _searchFrom = TestSearch[pass].from;
                    _searchTo = TestSearch[pass].to;
                    _searchSpeed = TestSearch[pass].speed;
                    TryStageAccident();
                }
                ResetSearch();
                if (_phase == Phase.None)
                {
                    TestRefusals.Tell(Logger, "Accident", TestRefusal.NoMovingCar, (int)TestSearch[TestSearch.Length - 1].to, TrafficCensus(PlayerHelper.GetPosition()));
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ResetSearch();
                Clear(resumeTraffic: true, announce: false);
            }
        }

        private static readonly (float from, float to, float speed)[] TestSearch =
        {
            (MinDistance, MaxDistance, MovingSpeed), (30f, 200f, MovingSpeed), (18f, 250f, 4f)
        };

        /// <summary>
        /// A moving traffic car for the other test buttons, searched as widely as a test accident; null, with how far was
        /// looked and the pool's census for the log, if there is none.
        /// </summary>
        public VehicleComponent FindMovingCarWide(Vector3 player, out int within, out string census)
        {
            VehicleComponent car = null;
            for (int pass = 0; pass < TestSearch.Length && car == null; pass++)
            {
                _searchFrom = TestSearch[pass].from;
                _searchTo = TestSearch[pass].to;
                _searchSpeed = TestSearch[pass].speed;
                car = FindMovingCar(player, openRoadOnly: false);
            }
            Severity? forced = _forcedSeverity;
            ResetSearch();
            _forcedSeverity = forced;
            within = (int)TestSearch[TestSearch.Length - 1].to;
            census = car == null ? TrafficCensus(player) : null;
            return car;
        }

        private void ResetSearch()
        {
            _forcedSeverity = null;
            _searchFrom = MinDistance;
            _searchTo = MaxDistance;
            _searchSpeed = MovingSpeed;
        }

        // What the traffic pool looked like when no car could be found: for the log, on a failed test only.
        private static string TrafficCensus(Vector3 player)
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            int onRoad = 0, usable = 0, moving = 0;
            float nearest = float.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car == null || !car.gameObject.activeInHierarchy)
                {
                    continue;
                }
                onRoad++;
                if (!IsUsable(car))
                {
                    continue;
                }
                usable++;
                if (car.GetCurrentSpeed() >= 4f)
                {
                    moving++;
                    nearest = Mathf.Min(nearest, Vector3.Distance(car.transform.position, player));
                }
            }
            return "traffic pool " + pool.Count + ", on the road " + onRoad + ", ordinary cars free to use " + usable + ", of those moving " + moving
                   + (moving > 0 ? ", the nearest " + nearest.ToString("F0") + " m away" : string.Empty);
        }

        // Set only while a test button stages an accident of a chosen severity.
        private Severity? _forcedSeverity;
    }
}
