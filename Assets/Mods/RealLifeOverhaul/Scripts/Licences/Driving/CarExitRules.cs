using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Getting out of the driving school's car, in plain numbers: the places around the car where the
    /// player may be put on their feet, in the order they are tried, what counts as still inside the
    /// car's body, and when a player who is not at the wheel any more is taken to be stuck half out of
    /// it. Nothing of the game or the engine in it, so every rule is also run in a console test
    /// (RealLifeOverhaul.Build/NavCheck/CarExitCheck.cs). Licences/Driving/TestCar holds no number of
    /// its own for any of this.
    ///
    /// The game's own exit (CarController.ExitVehicle) puts the player beside the driver's door a frame
    /// later. When that one point is not walkable it puts them back at the wheel with a warning, and the
    /// school, which sees its car out of the player's hands once the lesson is over, would only ask
    /// again: so the school names a point the game will take, found from this list. And should anything
    /// stop the exit half way (the player shown again but still inside the car, off the navigation mesh),
    /// the school finishes it.
    /// </summary>
    public static class CarExitRules
    {
        public const float DoorOut = 0.8f;       // metres out from the car's side to where the feet go
        public const float DoorBack = 0.3f;      // ...and this far behind the car's middle (the front door)
        public const float DoorForward = 1.1f;   // the second try on a side: this far ahead of the middle
        public const float EndOut = 1.0f;        // metres beyond the tail or the nose
        public const float Margin = 0.3f;        // a standing place nearer the body than this is still in the car
        public const float SampleRadius = 1.2f;  // metres around a place within which walkable ground is looked for
        public const float WatchFor = 30f;       // seconds after getting out in which a stuck player is looked for
        public const float StuckNear = 8f;       // metres from the car's middle: further off, the player has walked away
        public const float SettleSeconds = 1.5f; // the game's own exit finishes a frame later: never judged before this
        public const float KeepAfterExit = 2f;   // seconds the car stays after the player got out, whatever else is due
        public const int Places = 6;

        /// <summary>
        /// The i-th place to try, in the car's own frame: across is to its right (the driver's door is on
        /// the left, as in the game's cars), along is towards its nose. The driver's door first, then the
        /// passenger's, then the tail, the two sides further forward, and the nose last.
        /// </summary>
        public static void Place(int i, float halfWidth, float halfLength, out float across, out float along)
        {
            float side = halfWidth + DoorOut;
            switch (i)
            {
                case 0: across = -side; along = -DoorBack; break;
                case 1: across = side; along = -DoorBack; break;
                case 2: across = 0f; along = -(halfLength + EndOut); break;
                case 3: across = -side; along = DoorForward; break;
                case 4: across = side; along = DoorForward; break;
                case 5: across = 0f; along = halfLength + EndOut; break;
                default: throw new ArgumentOutOfRangeException(nameof(i));
            }
        }

        /// <summary>True if a point, in the car's own frame, is inside its body or nearer it than the margin.</summary>
        public static bool InBody(float across, float along, float halfWidth, float halfLength)
        {
            return Math.Abs(across) < halfWidth + Margin && Math.Abs(along) < halfLength + Margin;
        }

        /// <summary>
        /// Whether a player who is not at the wheel is stuck half out of the school's car: still under the car
        /// in the scene, still marked as in a vehicle, hidden, off the navigation mesh or inside the car's body.
        /// Never while at the wheel, never before the game's own exit has had time to finish, and not once they
        /// are well away from the car or a while has passed.
        /// </summary>
        public static bool Stuck(bool atWheel, float secondsOut, float distance, bool underCar, bool markedInVehicle, bool hidden, bool onMesh, bool inBody)
        {
            if (atWheel || secondsOut < SettleSeconds || secondsOut > WatchFor || distance > StuckNear)
            {
                return false;
            }
            return underCar || markedInVehicle || hidden || !onMesh || inBody;
        }

        /// <summary>Whether the car may be taken back now: never within a moment of the player getting out, so the game's exit is over first.</summary>
        public static bool MayTakeBack(bool atWheel, float secondsOut, bool stuck)
        {
            return !atWheel && !stuck && secondsOut >= KeepAfterExit;
        }
    }
}
