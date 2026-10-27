using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The south side of 1st Avenue's two continuations (the owner, 27 Oct 2026; a player walking the avenue at 563, 457: plain grey
    /// walls on both sides): the band's avenue runs along the game's backdrop there, whose buildings are boxes of 26 triangles in a
    /// gradient colour (MapBorder/Buildings Blockouts, SM_BorderV*) standing a metre from the south pavement, 20 to 80 m tall. The
    /// backdrop's pieces within <see cref="Depth"/> of the pavement are put away (LayoutPlans.Hides) and a row of the game's own
    /// buildings is copied for their looks alone along it, facing the avenue, on the backdrop's block grounds, as the ring stands on
    /// the band's other three sides. Game-free, the same on every machine (EconomyCheck's UptownLookChecks).
    /// COST: nothing per frame; a table over the catalogue's widths for each block once a load, behind the loading screen.
    /// </summary>
    public static class SouthRules
    {
        public const float Depth = 35f;          // m from the avenue's south pavement the row's buildings may reach back: as a street's strip
        public const float HideDepth = 42f;      // m: the backdrop's pieces whose middle lies this near the pavement are put away
        public const float HideOver = 2.5f;      // m past the pavement's back: the backdrop's buildings there stand to z 452.6
        public const float HideSide = 8f;        // m past a block's ends: the backdrop's pieces between two blocks' buildings go too
        public const float Unit = 0.5f;          // m: the widths are tried in these steps
        public const float Slack = 1f;           // m of a block's frontage left over at most: no gap to the eye
        public const float WidestPiece = 40f;    // m: a building of the row is no wider than this
        public const float LeastPiece = 7f;      // m: nor narrower

        /// <summary>The band's columns whose blocks have backdrop ground south of the avenue: three west of 9th Street, four east of 1st (the game's block grounds there, Build 3682).</summary>
        public static readonly int[] Columns = { 3, 4, 5, 14, 15, 16, 17 };

        /// <summary>The south pavement's back, z, where the row's fronts stand.</summary>
        public static float Front => LayoutRules.AvenueZ(0) - LayoutRules.AvenueHalf;

        /// <summary>
        /// Where the backdrop's pieces are put away (a piece whose middle lies in one): along the avenue's south pavement over the blocks of
        /// <see cref="Columns"/>, west of 9th Street and east of 1st Street, from the pavement back <see cref="HideDepth"/> and a few metres
        /// over it (the backdrop's buildings there stand to 2 m past it). Appended to the caller's list (not cleared).
        /// </summary>
        public static void Hides(List<GroundRect> into)
        {
            int west = 0, east = 0;
            float westFrom = float.MaxValue, westTo = float.MinValue, eastFrom = float.MaxValue, eastTo = float.MinValue;
            for (int k = 0; k < Columns.Length; k++)
            {
                Strip(Columns[k], out float x0, out float x1);
                bool onWest = Columns[k] < LayoutRules.FirstColumn;
                west += onWest ? 1 : 0;
                east += onWest ? 0 : 1;
                westFrom = onWest ? Math.Min(westFrom, x0) : westFrom;
                westTo = onWest ? Math.Max(westTo, x1) : westTo;
                eastFrom = onWest ? eastFrom : Math.Min(eastFrom, x0);
                eastTo = onWest ? eastTo : Math.Max(eastTo, x1);
            }
            float front = Front;
            if (west > 0)
            {
                into.Add(new GroundRect(westFrom - HideSide, front - HideDepth, westTo + HideSide, front + HideOver));
            }
            if (east > 0)
            {
                into.Add(new GroundRect(eastFrom - HideSide, front - HideDepth, eastTo + HideSide, front + HideOver));
            }
        }

        /// <summary>A block's frontage along the avenue: its west and east x.</summary>
        public static void Strip(int column, out float x0, out float x1)
        {
            LayoutRules.Block(1, column, out x0, out _, out x1, out _);
        }

        /// <summary>
        /// The row of buildings along every block's south edge, into the caller's list of fillers (not cleared): of the catalogue's designs
        /// that can be copied, each block's frontage cut into buildings whose widths add up to it within <see cref="Slack"/>, one design
        /// picked by the block's and the building's place among those that leave the rest fillable. Returns how many.
        /// </summary>
        public static int Plan(IList<DesignEntry> designs, List<FrontFill> into)
        {
            if (designs == null)
            {
                return 0;
            }
            int before = into.Count;
            var slots = new List<int>(designs.Count);
            var which = new List<int>(designs.Count);
            for (int i = 0; i < designs.Count; i++)
            {
                float across = FrontageRules.Across(designs[i]);
                if (FrontageRules.Deep(designs[i]) <= Depth && across >= LeastPiece && across <= WidestPiece && NewBuildingsRules.Copyable(designs[i]) && !designs[i].Whole)
                {
                    slots.Add((int)Math.Ceiling((across + FrontageRules.FillerSpare) / Unit));
                    which.Add(i);
                }
            }
            for (int k = 0; k < Columns.Length && slots.Count > 0; k++)
            {
                Strip(Columns[k], out float x0, out float x1);
                Fill(designs, slots, which, Columns[k], x0, x1, into);
            }
            return into.Count - before;
        }

        private static void Fill(IList<DesignEntry> designs, List<int> slots, List<int> which, int column, float x0, float x1, List<FrontFill> into)
        {
            int units = (int)Math.Floor((x1 - x0 + 0.01f) / Unit);
            int slack = (int)Math.Round(Slack / Unit);
            var fillable = new bool[units + 1];   // fillable[r]: r units of frontage can be filled, with at most the slack left over
            for (int r = 0; r <= units; r++)
            {
                fillable[r] = r <= slack;
                for (int s = 0; !fillable[r] && s < slots.Count; s++)
                {
                    fillable[r] = slots[s] <= r && fillable[r - slots[s]];
                }
            }
            if (!fillable[units])
            {
                return;
            }
            float z = Front - Depth * 0.5f;
            int left = units, piece = 0;
            float at = x0;
            var chosen = new List<int>(16);
            while (left > slack)
            {
                int count = 0;
                for (int s = 0; s < slots.Count; s++)
                {
                    count += slots[s] <= left && fillable[left - slots[s]] ? 1 : 0;
                }
                if (count == 0)
                {
                    break;
                }
                string key = "south|" + column.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + piece.ToString(System.Globalization.CultureInfo.InvariantCulture);
                int wanted = (int)(NewBuildingsRules.Hash(key) % (uint)count);
                for (int s = 0; s < slots.Count; s++)
                {
                    if (slots[s] <= left && fillable[left - slots[s]] && wanted-- == 0)
                    {
                        chosen.Add(s);
                        left -= slots[s];
                        break;
                    }
                }
                piece++;
            }
            at += left * Unit * 0.5f;   // what is left over is shared between the two ends
            for (int i = 0; i < chosen.Count; i++)
            {
                float width = slots[chosen[i]] * Unit;
                into.Add(new FrontFill { Design = which[chosen[i]], Lot = new MadeLot(at + width * 0.5f, z, 0f, width, Depth) });
                at += width;
            }
        }
    }
}
