using System.Collections.Generic;
using System.Reflection;
using GleyTrafficSystem;
using GleyUrbanAssets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the traffic mend (<see cref="TrafficMend"/>) touches the game's traffic. A new density manager is made the
    /// way TrafficManager.Initialize makes its own, from the traffic's own parts (its pool, its lane points, its road grid,
    /// its position check, its camera positions and its level of active squares), asked to put no car on the road at
    /// first, told how many cars are on the road already and how many the game last wanted, and handed to the traffic in
    /// place of the one it holds. The one it held is never read, called or destroyed: what failed in it may be the object
    /// itself, so only the reference to it is compared and then written over. Every car on the road drives on as it was.
    /// Called on a mend or a test only, never per frame.
    /// </summary>
    internal static class TrafficMendGame
    {
        private static readonly List<PositionValidator> Validators = new List<PositionValidator>(2);

        /// <summary>The game's traffic as this machine's own runs here: out of doors, not a guest's mirrored traffic, and the contract whole.</summary>
        public static bool Running => !GameContract.TrafficMend.Off && TrafficTools.Ready;

        /// <summary>The city's traffic manager, to tell one city's traffic from the next; null with none. A field read.</summary>
        public static object Current => TrafficManager.HasInstance ? TrafficManager.Instance : null;

        /// <summary>
        /// Gives the running traffic a new density manager. False, with why, when one of the parts it is made from is
        /// missing; the reason is for the log. Throws when the game's own start of it does (a camera off the road grid).
        /// </summary>
        public static bool Mend(out int carsKept, out int wanted, out bool heldMissing, out string why)
        {
            carsKept = 0;
            wanted = 0;
            why = null;
            TrafficManager traffic = TrafficManager.Instance;
            heldMissing = ReferenceEquals(GameContract.TrafficDensity.Field.GetValue(traffic), null);
            object vehicles = GameContract.TrafficVehicles.Field.GetValue(traffic);
            object points = GameContract.TrafficPoints.Field.GetValue(traffic);
            GridManager grid = GameContract.TrafficGrid.Field.GetValue(traffic) as GridManager;
            object cameras = GameContract.TrafficCameras.Field.GetValue(traffic);   // the camera positions, boxed once
            object squares = GameContract.TrafficSquares.Field.GetValue(traffic);
            PositionValidator validator = LastValidator(traffic);
            if (vehicles == null || points == null || grid == null || validator == null || cameras == null || squares == null)
            {
                why = "the traffic lacks " + (vehicles == null ? "its pool" : points == null ? "its lane points" : grid == null ? "its road grid"
                    : validator == null ? "its position check" : "its camera positions");
                return false;
            }

            DensityManager fresh = traffic.gameObject.AddComponent<DensityManager>();
            try
            {
                // The traffic's own call, with no car to put on the road: those on it now stay and are counted below.
                GameContract.DensityStart.Method.Invoke(fresh, new object[] { vehicles, points, grid, validator, cameras, 0, Vector3.zero, Vector3.forward, squares });
            }
            catch
            {
                Object.Destroy(fresh);
                throw;
            }
            carsKept = OnTheRoad();
            GameContract.DensityCount.Field.SetValue(fresh, carsKept);
            wanted = LastWanted(carsKept);
            fresh.UpdateMaxCars(wanted);
            GameContract.TrafficDensity.Field.SetValue(traffic, fresh);
            return true;
        }

        /// <summary>
        /// The test's break, as a player's log showed it: the density manager's link to the road grid cut, and its count of
        /// cars set below what it wants, so that its next pass fails at once. False with no density manager to break.
        /// </summary>
        public static bool Break()
        {
            DensityManager held = GameContract.TrafficDensity.Field.GetValue(TrafficManager.Instance) as DensityManager;
            if (held == null)
            {
                return false;
            }
            GameContract.DensityGrid.Field.SetValue(held, null);
            GameContract.DensityCount.Field.SetValue(held, 0);
            return true;
        }

        // The last one TrafficManager.Initialize added: the one its density manager was given.
        private static PositionValidator LastValidator(TrafficManager traffic)
        {
            traffic.GetComponents(Validators);
            PositionValidator last = Validators.Count > 0 ? Validators[Validators.Count - 1] : null;
            Validators.Clear();
            return last;
        }

        // Every car of the pool on the road: the density manager counts each one it puts there and each one taken off.
        private static int OnTheRoad()
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            int count = 0;
            for (int i = 0; pool != null && i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car != null && car.gameObject.activeSelf)
                {
                    count++;
                }
            }
            return count;
        }

        // The number of cars the game last asked of its traffic (TimeOfDayController, by hour and district); the cars on the
        // road while it has not asked yet, or cannot be read, until it next does.
        private static int LastWanted(int carsKept)
        {
            FieldInfo last = GameContract.LastTrafficDensity.Field;
            GameManager game = InstanceBehavior<GameManager>.Instance;
            TimeOfDayController day = game != null ? game.timeOfDayController : null;
            if (last == null || day == null)
            {
                return carsKept;
            }
            int wanted = (int)last.GetValue(day);
            return wanted >= 0 ? wanted : carsKept;
        }
    }
}
