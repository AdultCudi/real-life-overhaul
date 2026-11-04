using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// The game's neighbourhood zones on the 2.5D map. The game shows a zone as a plane over the city (and the district view tints it with the colour
    /// it chooses); the picture covers the game's own city, so each zone that shows is drawn into the picture, flat on the ground, in the colour the zone has.
    /// A zone's rectangle on the ground is the world box of its mesh's bounds (the district view reads it the same way). Game-free.
    /// </summary>
    public static class Map25Zones
    {
        public const int MostZones = 24;       // zones at the most drawn: the game has some ten

        /// <summary>
        /// The ground rectangle of a zone: the eight corners of the mesh's local box (<paramref name="cx"/> etc. its centre, <paramref name="ex"/> etc. its half size) through
        /// <paramref name="matrix"/> (sixteen floats, local to world, column by column as the engine keeps them), as the smallest rectangle round them.
        /// </summary>
        public static void Rectangle(float[] matrix, float cx, float cy, float cz, float ex, float ey, float ez, out float minX, out float minZ, out float maxX, out float maxZ)
        {
            minX = minZ = float.MaxValue;
            maxX = maxZ = float.MinValue;
            for (int c = 0; c < 8; c++)
            {
                float lx = cx + ((c & 1) == 0 ? -ex : ex), ly = cy + ((c & 2) == 0 ? -ey : ey), lz = cz + ((c & 4) == 0 ? -ez : ez);
                float wx = matrix[0] * lx + matrix[4] * ly + matrix[8] * lz + matrix[12];
                float wz = matrix[2] * lx + matrix[6] * ly + matrix[10] * lz + matrix[14];
                minX = Math.Min(minX, wx);
                maxX = Math.Max(maxX, wx);
                minZ = Math.Min(minZ, wz);
                maxZ = Math.Max(maxZ, wz);
            }
        }

        /// <summary>Writes the four corners of a rectangle, going round, as eight numbers into <paramref name="corners"/> from the zone's place <paramref name="at"/>.</summary>
        public static void Corners(float minX, float minZ, float maxX, float maxZ, float[] corners, int at)
        {
            int o = at * 8;
            corners[o] = minX; corners[o + 1] = minZ;
            corners[o + 2] = maxX; corners[o + 3] = minZ;
            corners[o + 4] = maxX; corners[o + 5] = maxZ;
            corners[o + 6] = minX; corners[o + 7] = maxZ;
        }
    }

    /// <summary>
    /// The zones the picture was last given, so that reading the game's again says whether anything about them changed: one that shows or goes, a colour or an opacity,
    /// a rectangle. The picture is drawn again on a still camera only then. The first look of a book (and the first after <see cref="Forget"/>) is a change whatever the
    /// zones are, because the scene may still hold what an earlier picture drew. Nothing is allocated after it is made. Game-free.
    /// </summary>
    public sealed class Map25ZoneBook
    {
        private readonly float[] _corners = new float[Map25Zones.MostZones * 8], _alpha = new float[Map25Zones.MostZones];
        private readonly Map25Colour[] _colour = new Map25Colour[Map25Zones.MostZones];
        private int _count;
        private bool _fresh = true;

        /// <summary>Zones the book holds.</summary>
        public int Count => _count;

        /// <summary>The next look at the game's zones is a change, whatever they are.</summary>
        public void Forget()
        {
            _fresh = true;
        }

        /// <summary>True, and the book holds these zones from now on, when they are not the ones it held; false when they are the same.</summary>
        public bool Take(float[] corners, Map25Colour[] colours, float[] alphas, int count)
        {
            count = Math.Max(0, Math.Min(count, Map25Zones.MostZones));
            bool same = !_fresh && count == _count;
            for (int i = 0; same && i < count; i++)
            {
                same = _alpha[i] == alphas[i] && _colour[i].R == colours[i].R && _colour[i].G == colours[i].G && _colour[i].B == colours[i].B;
                for (int k = 0; same && k < 8; k++)
                {
                    same = _corners[i * 8 + k] == corners[i * 8 + k];
                }
            }
            if (same)
            {
                return false;
            }
            Array.Copy(corners, _corners, count * 8);
            Array.Copy(colours, _colour, count);
            Array.Copy(alphas, _alpha, count);
            _count = count;
            _fresh = false;
            return true;
        }
    }
}
