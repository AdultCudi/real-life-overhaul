using BigAmbitions.Tags;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps a scooter the player rented from the game's stands while they are indoors. The game takes a scooter nobody is
    /// on away once it has not been seen for its type's number of minutes (AutoDestroyVehicle, every frame, never asking where
    /// the player is), and a building is out of its sight, so a long class or a film came out to no scooter and no word. While the
    /// player is inside, each such scooter on the street is marked as seen now, once a second, so the game's clock starts again
    /// the moment they step out. Nothing per frame: the director's second, a few comparisons for each vehicle of the player's, no
    /// allocation. Only the save's own list is read, so in a Going Public session another player's vehicle is never touched.
    /// </summary>
    public static class ScooterHold
    {
        /// <summary>Marks the player's rented scooters seen now while they are indoors; returns how many it held.</summary>
        public static int Tick(GameInstance save)
        {
            bool indoors = BuildingManager.IsInsideBuilding;
            if (!Settings.GarageScooterHold || !indoors || save == null || save.VehicleInstances == null)
            {
                return 0;
            }
            int held = 0;
            for (int i = 0; i < save.VehicleInstances.Count; i++)
            {
                VehicleInstance vehicle = save.VehicleInstances[i];
                VehicleType type = vehicle != null && vehicle.lastSeen != null ? vehicle.VehicleType : null;
                if (type == null)
                {
                    continue;
                }
                bool counting = vehicle.lastSeen.GetTotalMinutes() != 0f;
                if (ScooterHoldRules.Holds(true, indoors, type.HasTag(TagRef.Vehicletag.isscooter), type.autoDestroyAfterMinutes, counting,
                        string.IsNullOrEmpty(vehicle.streetName), vehicle.id == save.ActiveVehicleId, GarageState.Bike(vehicle.id) != null))
                {
                    vehicle.lastSeen.SetCurrentTime();
                    held++;
                }
            }
            return held;
        }
    }
}
