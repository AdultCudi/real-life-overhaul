using System;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// How near a police station's door or front desk the player must stand for its panel to take the interaction
    /// key. At the door it is the game's own reach: the game lets the player through an always-open entrance door
    /// (a precinct's are) from under 2 m of the door's point (CityBuildingController.IsPlayerNearEntranceDoor, a
    /// squared distance under 4). It is measured here on the flat, with the height apart, so that a door point set a
    /// little above the pavement does not shorten it. Every reach is in metres and is compared with a distance in
    /// metres (JobSpots.Flat is one, not its square). Game-free, so NavCheck runs it (StationReachCheck.cs). A few
    /// multiplications a call.
    /// </summary>
    public static class StationReach
    {
        public const float Door = 2f;     // metres on the flat from the door's own point: the game's own reach at a door
        public const float Rise = 2f;     // metres up or down from the door's point: a floor above or below is not at the door
        public const float Desk = 2.3f;   // metres on the flat from where the officer at the front desk serves

        /// <summary>True if the player, so far from the door's point on each axis (metres), is at the door.</summary>
        public static bool AtDoor(float dx, float dy, float dz) => dx * dx + dz * dz < Door * Door && dy > -Rise && dy < Rise;

        /// <summary>True if the player is at the front desk; <paramref name="flatMetres"/> is a distance on the flat in metres.</summary>
        public static bool AtDesk(float flatMetres) => flatMetres < Desk;

        /// <summary>The distance on the flat in metres from so far on the two level axes: for the self-check line.</summary>
        public static float Metres(float dx, float dz) => (float)Math.Sqrt(dx * dx + dz * dz);
    }
}
