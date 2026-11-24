namespace RealLifeOverhaul
{
    /// <summary>
    /// When a rented scooter's clock is held while the player is indoors, in plain numbers so that EconomyCheck runs the very
    /// rules the mod runs. The game hires out its electric scooters at their stands and takes one away again once it has not been
    /// seen for the vehicle type's own number of minutes (AutoDestroyVehicle, from VehicleType.autoDestroyAfterMinutes): the
    /// clock starts when the scooter leaves the camera, and a building the player goes into is out of its sight, so a class or
    /// a film longer than the limit comes out to no scooter. A few comparisons a second.
    /// </summary>
    public static class ScooterHoldRules
    {
        /// <summary>
        /// Whether the game's clock on a vehicle of the player's is put back to now. It is when the player is indoors and the vehicle is a
        /// scooter the game takes away by that clock (a limit of 0 or more), counting now (seen last at a time that is set), standing on the
        /// street, not ridden and not one of the mod's own bicycles or motorbikes (those have their own rules, Garage/TwoWheelers).
        /// </summary>
        public static bool Holds(bool optionOn, bool indoors, bool scooter, int limit, bool counting, bool onStreet, bool ridden, bool ourBike)
        {
            return optionOn && indoors && scooter && limit >= 0 && counting && onStreet && !ridden && !ourBike;
        }

        /// <summary>
        /// The game's own test of a vehicle it takes away (AutoDestroyVehicle): a limit of 0 or more, a time last seen that is set and not
        /// zero, and at least that many minutes gone since. Whole minutes of the save's clock.
        /// </summary>
        public static bool GameTakes(int limit, bool counting, float now, float lastSeen)
        {
            return limit >= 0 && counting && now - lastSeen >= limit;
        }

        /// <summary>
        /// A stay indoors of the given length, as the game's clock sees it: the scooter leaves sight as the player goes in, and every
        /// <paramref name="tick"/> minutes (the director's second at the game's speed) the hold may put the clock back. True if the game
        /// would have taken the scooter at any moment of the stay, with the hold on or off.
        /// </summary>
        public static bool TakenDuring(int limit, float stay, float tick, bool hold)
        {
            const float Step = 0.25f;   // minutes between two looks of the game's clock
            float lastSeen = 0f;
            float held = 0f;
            for (float now = 0f; now <= stay; now += Step)
            {
                if (GameTakes(limit, true, now, lastSeen))
                {
                    return true;
                }
                if (hold && now - held >= tick)
                {
                    held = now;
                    lastSeen = now;
                }
            }
            return false;
        }
    }
}
