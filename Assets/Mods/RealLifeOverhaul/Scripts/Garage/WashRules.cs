namespace RealLifeOverhaul
{
    /// <summary>
    /// When the game's car wash in a repair bay has stopped without ending, in plain numbers, so that NavCheck (WashCheck.cs)
    /// checks the very rules <see cref="GarageWash"/> watches a wash by.
    ///
    /// The game's wash (CarController.WashCoroutine, from "Wash" on the panel in a bay) first holds the car and the player:
    /// the vehicle navigation blocker WashingVehicle takes the car's pedals and the way out of it, and SetFreeze holds its
    /// body; then it plays its sound and shows its spray, and only at the end of its 3 to 4 seconds lets both go. A car
    /// that has no spray or no wash sound of its own (another mod's car built without them; a player's report, 20 Oct
    /// 2026: a Porsche of another mod) makes the coroutine fail at its first line after the hold, and nothing ever lets
    /// go: the car cannot drive, the player cannot get out, and only loading the game again helps. A car whose object is
    /// switched off mid-wash stops the coroutine the same way.
    ///
    /// So a wash is watched from the moment the car is seen washing: while it lives, the dirt the game writes each frame
    /// goes down, and a new wash starts it back at the top. A wash still under way <see cref="Longest"/> plus
    /// <see cref="Grace"/> seconds of game time after it began has stopped, and is ended as the game ends one.
    ///
    /// A few compares, four times a second, and only while a car is washing. Nothing is allocated.
    /// </summary>
    public static class WashRules
    {
        public const float Longest = 4f;     // s of game time: the game's own wash lasts 3 to 4 (CarController.washDuration)
        public const float Grace = 4f;       // s over that before a wash is taken to have stopped: a slow frame or two never ends a living one
        public const float Restart = 0.25f;  // of the dirt: a rise this large is a new wash begun (the game's starts at the top, 1)

        /// <summary>True when a wash that began at <paramref name="since"/> is still under way at <paramref name="now"/> longer than any wash lasts.</summary>
        public static bool Stopped(float since, float now)
        {
            return now - since > Longest + Grace;
        }
    }

    /// <summary>
    /// One car's wash as it is watched (<see cref="WashRules"/>): which car, since when, and the dirt last seen. A struct of
    /// plain numbers; <see cref="Step"/> is asked four times a second with what the game has now.
    /// </summary>
    public struct WashWatch
    {
        public int Car;      // the car watched (its object's id), 0 for none
        public float Since;  // game time the wash was first seen, or seen to start again
        public float Dirt;   // the car's dirt when last seen

        /// <summary>
        /// The watch moved on by what the game has now: <paramref name="car"/> the player's car (0 for none),
        /// <paramref name="washing"/> whether the game has it washing, its <paramref name="dirt"/> and the game time.
        /// True once, when that car's wash has stopped without ending; the watch then starts afresh.
        /// </summary>
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
            Car = 0;   // said once: a car let go is not let go again
            return true;
        }
    }
}
