using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>Which ends of a lot along its strip nothing stands against: the end at its lower index (its strip's west or south) and the one at its higher.</summary>
    public struct LotEnds
    {
        public bool Low;      // nothing of the plan stands against its lower end
        public bool High;     // nor against its higher end
        public bool LowRoad;  // and a road (a street's or an avenue's pavement) lies straight beyond the lower end
        public bool HighRoad; // ... the higher

        /// <summary>True when either end is open.</summary>
        public bool Any => Low || High;
    }

    /// <summary>What one side wall of a design has: windows of its own, a plain wall a window overlay can dress, or a wall that cannot be dressed.</summary>
    public enum SideKind
    {
        Bare = 0,       // a low or stepped wall, or a design the table does not know: nothing can be done with it
        Dressable = 1,  // a plain wall, flat to a good height: windows are laid over it (SideRules)
        Windowed = 2    // windows of its own
    }

    /// <summary>
    /// How the sides of the game's own designs look, and which ends of Uptown's lots show them (the owner, 27 Oct 2026, after a
    /// player walked the district: "A lot of buildings have missing textures, there are gaps between buildings and buildings
    /// do not face the road properly"), game-free (EconomyCheck's UptownLookChecks). The game's buildings stand side by side
    /// in Manhattan, so 85 in 100 of its designs have blank side walls, a brick or plaster with no window, which nobody ever
    /// sees there. Uptown's buildings stand side by side too, but the end of every strip, the sides beside an open lot and the
    /// corners of the first row's blocks on 1st Avenue's continuations show a side to the street. A design's sides are the
    /// game's own, measured from its files (LookSides, Build 3682); a plain one is dressed with windows (the game's own
    /// backside material, SideRules) and a lot with an open end takes a design whose side there can be dressed.
    /// Every answer comes from the arguments and the table alone: the same on every machine.
    /// COST: nothing per frame; a table look-up for each lot at a load.
    /// </summary>
    public static class LookRules
    {
        public const float OnRoadReach = 3f;   // m beyond a lot's end a road is looked for: its pavement begins there or at the end itself
        public const float TouchReach = 1f;    // m: a piece within this of an end closes it (FrontageRules.SliverMost: no gap to the eye)

        private struct Sides
        {
            public SideKind Left, Right;
            public float LeftTop, RightTop;   // m: how high a plain wall can be dressed
            public bool LeftFlat, RightFlat;  // a plain wall of flat colour, no texture of its own: dressed, but not the wall to show a street
        }

        private static Dictionary<string, Sides> _sides;   // "name|size's letter" to its sides

        /// <summary>How many designs the table knows.</summary>
        public static int Known => Table().Count;

        /// <summary>The designs the table knows, as "name|size's letter" keys, for the checks.</summary>
        public static IEnumerable<string> Keys => Table().Keys;

        /// <summary>What a design's side (<paramref name="right"/>: its right looking out from its door's side, else its left) has.</summary>
        public static SideKind Kind(DesignEntry d, bool right)
        {
            return d != null && Table().TryGetValue(KeyOf(d), out Sides s) ? (right ? s.Right : s.Left) : SideKind.Bare;
        }

        /// <summary>
        /// True for a plain side of flat colour (no texture of its own, a grey or plain-coloured wall): windows can be laid on it, but a
        /// street should not be shown it where another design will do.
        /// </summary>
        public static bool Flat(DesignEntry d, bool right)
        {
            return d != null && Table().TryGetValue(KeyOf(d), out Sides s) && (right ? s.RightFlat : s.LeftFlat);
        }

        /// <summary>m: how high a plain side can be dressed with windows, 0 for a side that cannot (or has windows already).</summary>
        public static float Top(DesignEntry d, bool right)
        {
            return d != null && Table().TryGetValue(KeyOf(d), out Sides s) && (right ? s.Right : s.Left) == SideKind.Dressable ? (right ? s.RightTop : s.LeftTop) : 0f;
        }

        /// <summary>
        /// True when no open end of the lot would show a side that cannot be dressed, nor a wall of flat colour to a road:
        /// <paramref name="ends"/> against the side each end is (<see cref="RightIsHigh"/>), so a building stands with a side that has
        /// windows or can be given them toward the street, an open lot or the corner, and a textured one toward a street.
        /// </summary>
        public static bool Finished(DesignEntry d, LotEnds ends, float facing)
        {
            bool rightHigh = RightIsHigh(facing);
            return Shows(d, !rightHigh, ends.Low, ends.LowRoad) && Shows(d, rightHigh, ends.High, ends.HighRoad);
        }

        // An end that is shut, or whose side can be dressed and, toward a road, is not flat colour.
        private static bool Shows(DesignEntry d, bool right, bool open, bool road)
        {
            return !open || (Kind(d, right) != SideKind.Bare && !(road && Flat(d, right)));
        }

        /// <summary>
        /// True when the lot's higher end (along its strip, which runs from the west or the south) is on the right looking out at its
        /// street: an avenue's lots facing north and a street's facing west have their east or north end on the right, the others on
        /// the left.
        /// </summary>
        public static bool RightIsHigh(float facing)
        {
            int f = ((int)Math.Round(facing) % 360 + 360) % 360;
            return f == 0 || f == 270;
        }

        private static string KeyOf(DesignEntry d)
        {
            int cut = d.Size != null ? d.Size.LastIndexOf('_') : -1;
            return d.Name + "|" + (cut >= 0 ? d.Size.Substring(cut + 1) : d.Size);
        }

        private static Dictionary<string, Sides> Table()
        {
            if (_sides != null)
            {
                return _sides;
            }
            string[] rows = LookSides.Rows;
            var made = new Dictionary<string, Sides>(rows.Length, StringComparer.Ordinal);
            for (int i = 0; i < rows.Length; i++)
            {
                int right = rows[i].LastIndexOf('|'), left = rows[i].LastIndexOf('|', right - 1);
                var s = new Sides();
                Read(rows[i].Substring(left + 1, right - left - 1), out s.Left, out s.LeftTop, out s.LeftFlat);
                Read(rows[i].Substring(right + 1), out s.Right, out s.RightTop, out s.RightFlat);
                made[rows[i].Substring(0, left)] = s;
            }
            _sides = made;
            return made;
        }

        private static void Read(string text, out SideKind kind, out float top, out bool flat)
        {
            top = 0f;
            flat = text.EndsWith("f", StringComparison.Ordinal);
            if (flat)
            {
                text = text.Substring(0, text.Length - 1);
            }
            if (text == "w")
            {
                kind = SideKind.Windowed;
            }
            else if (text == "-" || !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out top))
            {
                kind = SideKind.Bare;
                top = 0f;
            }
            else
            {
                kind = SideKind.Dressable;
            }
        }

        /// <summary>
        /// True when a point lies on a street's or an avenue's ground of the band (the road and its pavements, kerb to back): the
        /// street's width at any of the nineteen streets between 1st Avenue and Crest Avenue, the avenue's along the whole band and its
        /// continuations.
        /// </summary>
        public static bool OnRoad(float x, float z)
        {
            float west = LayoutRules.StreetX(LayoutStreets.WestMost) - LayoutRules.StreetHalf, east = LayoutRules.StreetX(LayoutStreets.EastMost) + LayoutRules.StreetHalf;
            float south = LayoutRules.AvenueZ(0) - LayoutRules.AvenueHalf, north = LayoutRules.AvenueZ(LayoutRules.Rows) + LayoutRules.AvenueHalf;
            if (x < west || x > east || z < south || z > north)
            {
                return false;
            }
            for (int row = 0; row <= LayoutRules.Rows; row++)
            {
                if (Math.Abs(z - LayoutRules.AvenueZ(row)) <= LayoutRules.AvenueHalf)
                {
                    return true;
                }
            }
            for (int s = LayoutStreets.WestMost; s <= LayoutStreets.EastMost; s++)
            {
                if (Math.Abs(x - LayoutRules.StreetX(s)) <= LayoutRules.StreetHalf)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Which ends of every lot of the plan nothing of the plan stands against (into the caller's dictionary, cleared, by lot id):
        /// an end is open when the lot next along its strip is not one that <paramref name="stands"/> says is built, or where it
        /// has no next lot (the strip's end or the ground of a big place) when a road lies straight beyond it. The lot beside a
        /// street strip's far end, the back of an avenue strip's lots, is a closed end: nothing is seen between them.
        /// </summary>
        public static void Open(IList<BuildingsLot> lots, Func<string, bool> stands, Dictionary<string, LotEnds> into)
        {
            into.Clear();
            if (lots == null || stands == null)
            {
                return;
            }
            var next = new Dictionary<long, BuildingsLot>(lots.Count * 2);
            for (int i = 0; i < lots.Count; i++)
            {
                next[((long)lots[i].Strip << 20) + lots[i].Index] = lots[i];
            }
            for (int i = 0; i < lots.Count; i++)
            {
                BuildingsLot lot = lots[i];
                var ends = new LotEnds();
                bool alongX = Math.Abs(Math.Cos(lot.Facing * Math.PI / 180.0)) > 0.5;
                float along = alongX ? lot.X : lot.Z, across = alongX ? lot.Z : lot.X, half = lot.Width * 0.5f;
                bool lowRoad = Beyond(alongX, along - half - OnRoadReach, across), highRoad = Beyond(alongX, along + half + OnRoadReach, across);
                if (FindBefore(next, lot, out BuildingsLot low))
                {
                    ends.Low = !stands(low.Id);
                }
                else
                {
                    ends.Low = lowRoad;
                }
                if (next.TryGetValue(((long)lot.Strip << 20) + (lot.Index + lot.Spaces), out BuildingsLot high))
                {
                    ends.High = !stands(high.Id);
                }
                else
                {
                    ends.High = highRoad;
                }
                ends.LowRoad = ends.Low && lowRoad;
                ends.HighRoad = ends.High && highRoad;
                into[lot.Id] = ends;
            }
        }

        // The lot whose spaces end where this one's begin (the one whose Index plus Spaces is this Index): a short walk back along the strip.
        private static bool FindBefore(Dictionary<long, BuildingsLot> next, BuildingsLot lot, out BuildingsLot found)
        {
            for (int back = 1; back <= 8 && lot.Index - back >= 0; back++)
            {
                if (next.TryGetValue(((long)lot.Strip << 20) + (lot.Index - back), out BuildingsLot candidate) && candidate.Index + candidate.Spaces == lot.Index)
                {
                    found = candidate;
                    return true;
                }
            }
            found = default;
            return false;
        }

        private static bool Beyond(bool alongX, float along, float across)
        {
            return alongX ? OnRoad(along, across) : OnRoad(across, along);
        }
    }
}
