using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Schools
{
    /// <summary>One of the player's children as the campus needs to know them.</summary>
    public struct SchoolChild
    {
        public string Name;
        public float Age;      // years
        public int School;     // 0 not yet, 1 public, 2 private (the campus is a private school to family life)
        public bool Grown;     // left home
    }

    /// <summary>
    /// What the schools ask of the rest of the mod, each with a default so they work alone; Wiring/Wiring.SchoolLinks fills
    /// them in: the player's children and their schooling (Scripts/Family), the council's money for schools (Scripts/Civic). Each is asked on a page, a press or a day's pass,
    /// never per frame.
    /// </summary>
    public static class SchoolsBridges
    {
        /// <summary>The player's children into the list (cleared); how many. Default: none.</summary>
        public static Func<List<SchoolChild>, int> Children;

        /// <summary>A child's schooling set: true for the campus (a private school), false back to the public school. False when there is no such child. Default: no.</summary>
        public static Func<string, bool, bool> SetSchool;

        /// <summary>The council's money for schools (Civic's grants lever): +1 more, -1 less, 0 as the city came. Default: 0.</summary>
        public static Func<int> Council;

        public static void Reset()
        {
            Children = null;
            SetSchool = null;
            Council = null;
        }
    }
}
