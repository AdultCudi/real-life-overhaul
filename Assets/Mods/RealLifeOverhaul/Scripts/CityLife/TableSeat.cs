using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Somebody seated at a table for a clip made there (chess_sit: the stool's centre 0.62 m from the board's, the board
    /// straight ahead) faces the board's centre, whichever stool they have and whichever way the table stands: the way is
    /// from their own spot to the centre, never a stool's or the table's heading. Game-free, so NavCheck runs it
    /// (TableSeatCheck.cs). A square root and an arc cosine when asked, once a second for each person seated.
    /// </summary>
    public static class TableSeat
    {
        public const float FacedWithin = 6f;      // degrees off the board's centre that still read as facing it
        public const float TooNear = 0.05f;       // metres on the flat: no way to face a point this close

        /// <summary>The spot of the stool on one side of a table: <paramref name="side"/> +1 along the table's axis, -1 against it.</summary>
        public static void Stool(float tableX, float tableZ, float alongX, float alongZ, float apart, int side, out float x, out float z)
        {
            float length = (float)Math.Sqrt(alongX * alongX + alongZ * alongZ);
            float s = side >= 0 ? 1f : -1f;
            if (!(length > 1e-4f))
            {
                x = tableX;
                z = tableZ;
                return;
            }
            x = tableX + alongX / length * apart * s;
            z = tableZ + alongZ / length * apart * s;
        }

        /// <summary>The unit way (x, z) from a seat to the board's centre; false, and (0, 0), within <see cref="TooNear"/>.</summary>
        public static bool Way(float seatX, float seatZ, float centreX, float centreZ, out float x, out float z)
        {
            float dx = centreX - seatX, dz = centreZ - seatZ;
            float apart = (float)Math.Sqrt(dx * dx + dz * dz);
            if (!(apart >= TooNear))   // NaN too
            {
                x = z = 0f;
                return false;
            }
            x = dx / apart;
            z = dz / apart;
            return true;
        }

        /// <summary>Degrees between the way somebody faces (x, z, any length) and the way from their seat to the board's centre; 0 when either has no way.</summary>
        public static float Off(float forwardX, float forwardZ, float seatX, float seatZ, float centreX, float centreZ)
        {
            float length = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
            if (!(length > 1e-4f) || !Way(seatX, seatZ, centreX, centreZ, out float wx, out float wz))
            {
                return 0f;
            }
            double cos = (forwardX * wx + forwardZ * wz) / length;
            cos = Math.Max(-1.0, Math.Min(1.0, cos));
            return (float)(Math.Acos(cos) * 180.0 / Math.PI);
        }

        /// <summary>True when somebody that many degrees off the board's centre faces it.</summary>
        public static bool Faces(float off) => off <= FacedWithin;
    }
}
