using System;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// Where the player stands to sit at a desk, and which desk of a floor is theirs when the seed's own cannot be walked
    /// to. Game-free: the callers try the places on the navigation mesh (FitReach). The places go out from the chair away
    /// from its desk, then to its sides: in front of the seat is under the desk, cut out of the walkable floor.
    /// </summary>
    public static class DeskReach
    {
        public const int Spots = 7;              // places by a chair tried, at most
        public const float Out = 0.55f;          // metres out from the seat, away from the desk: where somebody stands to sit down
        public const float Side = 0.6f;          // metres to the chair's side
        public const float Near = 2.5f;          // metres from the seat within which a player with nowhere nearer to walk is at the desk
        public const float SampleRadius = 0.35f; // metres a place may be moved to meet the walkable floor
        public const float EndWithin = 0.25f;    // metres a path's last corner may stop short of the place and still reach it
        public const float SeatRadius = 0.6f;    // metres the floor by a seat may be from it (the chair is not cut out; its desk is)
        public const float BesideMost = 1.6f;    // metres walked at most from a place to the seat's floor: further, something stands between

        /// <summary>
        /// The <paramref name="n"/>th place to stand by a seat, away from its desk's middle; false past the last. With no desk
        /// (or one right over the seat) away is behind the seat, against its facing.
        /// </summary>
        public static bool Spot(int n, float seatX, float seatZ, float deskX, float deskZ, float faceX, float faceZ, out float x, out float z)
        {
            Away(seatX, seatZ, deskX, deskZ, faceX, faceZ, out float ax, out float az);
            float sx = az, sz = -ax;   // the chair's side
            float u, v;                // metres away and to the side
            switch (n)
            {
                case 0: u = Out; v = 0f; break;
                case 1: u = Out + 0.3f; v = 0f; break;
                case 2: u = 0f; v = Side; break;
                case 3: u = 0f; v = -Side; break;
                case 4: u = Out; v = Side * 0.75f; break;
                case 5: u = Out; v = -Side * 0.75f; break;
                case 6: u = 0f; v = 0f; break;   // the seat itself: a chair standing free
                default: x = seatX; z = seatZ; return false;
            }
            x = seatX + ax * u + sx * v;
            z = seatZ + az * u + sz * v;
            return true;
        }

        /// <summary>The flat direction out of a chair, away from its desk; a unit vector.</summary>
        public static void Away(float seatX, float seatZ, float deskX, float deskZ, float faceX, float faceZ, out float ax, out float az)
        {
            ax = seatX - deskX;
            az = seatZ - deskZ;
            float length = (float)Math.Sqrt(ax * ax + az * az);
            if (length < 0.05f)
            {
                ax = -faceX;
                az = -faceZ;
                length = (float)Math.Sqrt(ax * ax + az * az);
                if (length < 1e-4f)
                {
                    ax = 0f;
                    az = -1f;
                    return;
                }
            }
            ax /= length;
            az /= length;
        }

        /// <summary>
        /// The seed's <paramref name="pick"/> if it can be walked to, else the next one round that can; -1 if none. A fixed
        /// order, so the same floor gives the same desk every visit and on every machine.
        /// </summary>
        public static int First(int pick, int count, Func<int, bool> reachable)
        {
            if (count <= 0 || pick < 0 || reachable == null)
            {
                return -1;
            }
            for (int i = 0; i < count; i++)
            {
                int at = (pick % count + i) % count;
                if (reachable(at))
                {
                    return at;
                }
            }
            return -1;
        }

        /// <summary>True if a path's end (its last corner) is the place asked for: the game's own test of a partial path.</summary>
        public static bool Ends(float endX, float endZ, float x, float z) => (endX - x) * (endX - x) + (endZ - z) * (endZ - z) <= EndWithin * EndWithin;

        /// <summary>True if the walk from a place to the seat's floor is short enough not to go round a partition.</summary>
        public static bool Beside(float walkedMetres) => walkedMetres >= 0f && walkedMetres <= BesideMost;

        /// <summary>True if a player at a flat distance from the seat is at the desk already, with nowhere nearer to walk.</summary>
        public static bool AtDesk(float flatMetres) => flatMetres <= Near;
    }
}
