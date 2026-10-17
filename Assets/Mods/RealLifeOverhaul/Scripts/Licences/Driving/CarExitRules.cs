using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Getting out of the driving school's car in plain numbers: where the player may be put down, in order, and when
    /// they are stuck half out.
    /// </summary>
    public static class CarExitRules
    {
        public const float DoorOut = 0.8f;       // metres out from the car's side to where the feet go
        public const float DoorBack = 0.3f;      // ...and this far behind the car's middle (the front door)
        public const float DoorForward = 1.1f;   // the second try on a side: this far ahead of the middle
        public const float EndOut = 1.0f;        // metres beyond the tail or the nose
        public const float Margin = 0.3f;        // metres: a place nearer the body than this is still in the car
        public const float SampleRadius = 1.2f;  // metres around a place to look for walkable ground
        public const float WatchFor = 30f;       // seconds after getting out in which a stuck player is looked for
        public const float StuckNear = 8f;       // metres from the car's middle: further off, the player has walked away
        public const float SettleSeconds = 1.5f; // the game's exit finishes a frame later: never judged before this
        public const float KeepAfterExit = 2f;   // seconds the car stays after the player got out
        public const int Places = 6;

        /// <summary>The i-th place to try, in the car's frame (across to its right, along to its nose); the driver's door first.</summary>
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

        /// <summary>True if a point in the car's frame is inside its body or within the margin.</summary>
        public static bool InBody(float across, float along, float halfWidth, float halfLength)
        {
            return Math.Abs(across) < halfWidth + Margin && Math.Abs(along) < halfLength + Margin;
        }

        /// <summary>Whether a player near the car, a little after getting out, is stuck half out of it.</summary>
        public static bool Stuck(bool atWheel, float secondsOut, float distance, bool underCar, bool markedInVehicle, bool hidden, bool onMesh, bool inBody)
        {
            if (atWheel || secondsOut < SettleSeconds || secondsOut > WatchFor || distance > StuckNear)
            {
                return false;
            }
            return underCar || markedInVehicle || hidden || !onMesh || inBody;
        }

        /// <summary>Never in the moment after the player got out, so the game's exit is over first.</summary>
        public static bool MayTakeBack(bool atWheel, float secondsOut, bool stuck)
        {
            return !atWheel && !stuck && secondsOut >= KeepAfterExit;
        }
    }
}
