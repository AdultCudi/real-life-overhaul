using System;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// Where the player stands to sit down at a desk, and which desk of a floor is theirs when the seed's own cannot be
    /// walked to (Careers: a desk of a fitted floor, or a station of the game's). Game-free: the places are tried on the
    /// game's navigation mesh by the callers (FitReach), once as a floor stands or a station is chosen, and once more when
    /// the desk is clicked. Nothing here runs per frame.
    ///
    /// The places by a chair go out from it away from the desk it is drawn up to, then to its sides: the floor in front of
    /// the seat is under the desk, and once the desk is cut out of the walkable floor it is no place to walk to (a desk at
    /// an angle hard against the end of a long table cannot be walked to at all, and the game refuses the click).
    /// </summary>
    public static class DeskReach
    {
        public const int Spots = 7;              // places by a chair tried, at most
        public const float Out = 0.55f;          // metres out from the seat, away from the desk: where somebody stands to sit down
        public const float Side = 0.6f;          // metres to the chair's side
        public const float Near = 2.5f;          // metres from the seat within which a player who cannot be walked anywhere nearer is at the desk
        public const float SampleRadius = 0.35f; // metres a place may be moved to meet the walkable floor
        public const float EndWithin = 0.25f;    // metres a path's last corner may stop short of the place and still reach it
        public const float SeatRadius = 0.6f;    // metres the floor by a seat may be from it (the chair is not cut out of the floor; its desk is)
        public const float BesideMost = 1.6f;    // metres walked at most from a place to the seat's floor: further, and a partition or a desk stands between them

        /// <summary>
        /// The <paramref name="n"/>th place to stand by a seat at (<paramref name="seatX"/>, <paramref name="seatZ"/>), whose
        /// desk's middle is at (<paramref name="deskX"/>, <paramref name="deskZ"/>); false past the last. With no desk (or a
        /// desk right over the seat) the seat's own back, the opposite of where it faces (<paramref name="faceX"/>, <paramref name="faceZ"/>), is away.
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
        /// The player's desk among <paramref name="count"/>: the seed's <paramref name="pick"/> if it can be walked to,
        /// otherwise the next one round that can; -1 if none can. Each is asked at most once, in a fixed order, so the same
        /// floor gives the same desk every visit and on every machine.
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

        /// <summary>True if a place is beside the seat: the walk from it to the seat's floor is no longer than <see cref="BesideMost"/>, not round a partition.</summary>
        public static bool Beside(float walkedMetres) => walkedMetres >= 0f && walkedMetres <= BesideMost;

        /// <summary>True if a player at a flat distance from the seat is at the desk already, with nowhere nearer to walk.</summary>
        public static bool AtDesk(float flatMetres) => flatMetres <= Near;
    }
}
