namespace RealLifeOverhaul
{
    /// <summary>
    /// When the game's car wash has stopped without ending, in plain numbers so that NavCheck checks them. The wash holds
    /// the car and the player until its end, which a car without its own spray or sound (another mod's) never reaches.
    /// A few compares while a car is washing.
    /// </summary>
    public static class WashRules
    {
        public const float Longest = 4f;     // s of game time: the game's own wash lasts 3 to 4 (CarController.washDuration)
        public const float Grace = 4f;       // s over that: a slow frame or two never ends a living wash
        public const float Restart = 0.25f;  // of the dirt: a rise this large is a new wash begun (the game's starts at 1)

        /// <summary>True when a wash that began at <paramref name="since"/> is still under way longer than any wash lasts.</summary>
        public static bool Stopped(float since, float now)
        {
            return now - since > Longest + Grace;
        }
    }

    /// <summary>One car's wash as it is watched: which car, since when, and the dirt last seen.</summary>
    public struct WashWatch
    {
        public int Car;      // the car watched (its object's id), 0 for none
        public float Since;  // game time the wash was first seen, or seen to start again
        public float Dirt;   // the car's dirt when last seen

        /// <summary>The watch moved on (<paramref name="car"/> 0 for none); true once, when that car's wash has stopped.</summary>
        public bool Step(int car, bool washing, float dirt, float now)
        {
            if (car == 0 || !washing)
            {
                Car = 0;
                return false;
            }
            if (car != Car)
            {
                Car = car;
                Since = now;
                Dirt = dirt;
                return false;
            }
            if (dirt > Dirt + WashRules.Restart)
            {
                Since = now;   // a new wash: the game's starts at the top
            }
            Dirt = dirt;
            if (!WashRules.Stopped(Since, now))
            {
                return false;
            }
            Car = 0;   // once: a car let go is not let go again
            return true;
        }
    }
}
