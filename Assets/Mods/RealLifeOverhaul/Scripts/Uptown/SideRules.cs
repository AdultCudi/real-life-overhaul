using System;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>One window overlay on a side wall, in its building's own frame (its root's, the frame the design was measured in).</summary>
    public struct SidePanel
    {
        public float AX, AZ;       // m: the wall's left end, looking at it from outside, at the panel's foot
        public float BX, BZ;       // m: its right end
        public float NX, NZ;       // the wall's outward way, a unit vector
        public float Y0, Y1;       // m above the root: the panel's foot and top
        public int Columns, Rows;  // window cells across and up: one window in each
        public int Style;          // which of the game's window materials (SideRules.Styles), by the piece's id
    }

    /// <summary>
    /// The window overlays on the sides of Uptown's buildings that show a plain wall to a street or an open lot (the owner, 27 Oct
    /// 2026: "buildings do not face the road properly", the screenshots of a plain brick or grey wall as long as a block): the game's
    /// own building backsides are a quad over the wall with a window picture on it, one in each cell of a grid its texture
    /// coordinates count (Shader Graphs/SH_BuildingBackside, M_BuildBack_V1 to V6: the window's cut-out alpha leaves the wall
    /// showing), so a plain side is dressed the game's way: one such quad a few centimetres off the wall, from a floor above the
    /// street to a metre under where the wall stops being flat (LookSides). Game-free, the same on every machine.
    /// COST: arithmetic, once for each open end of a building at a load.
    /// </summary>
    public static class SideRules
    {
        public const float Lift = 0.04f;         // m the quad stands off the wall: no flicker against it
        public const float FromGround = 3.2f;    // m above the street the first floor of windows begins: the ground floor's wall is left alone
        public const float BelowTop = 1.0f;      // m under the wall's flat top the panel stops
        public const float CellWide = 3.0f;      // m of wall one window cell takes across
        public const float CellHigh = 3.6f;      // m, a floor
        public const float EndInset = 0.6f;      // m kept clear at each end of the wall: a corner's trim stands there
        public const float Least = 6f;           // m of wall across the least panel is laid on

        /// <summary>The names of the game's window materials the panels wear, in the order <see cref="SidePanel.Style"/> counts them: casement, sash, stone-framed and a dark twelve-pane sash.</summary>
        public static readonly string[] Styles = { "M_BuildBack_V1", "M_BuildBack_V2", "M_BuildBack_V4", "M_BuildBack_V5" };

        /// <summary>
        /// The overlay for one end of a building: <paramref name="d"/> on <paramref name="ground"/>, <paramref name="high"/> for the lot's
        /// higher end. False when the side has windows already, cannot be dressed, or is too short or too low to take one.
        /// </summary>
        public static bool Panel(DesignEntry d, MadeLot ground, bool high, string id, out SidePanel panel)
        {
            panel = default;
            if (d == null)
            {
                return false;
            }
            bool right = high == LookRules.RightIsHigh(ground.Facing);
            float top = LookRules.Top(d, right);
            if (top <= 0f)
            {
                return false;
            }
            int dir = ((int)Math.Round(NewBuildingsRules.Turn(NewBuildingsRules.FrontOf(d) + (right ? 90f : 270f))) % 360 + 360) % 360;
            float plane, lo, hi, sx, sz;
            switch (dir)
            {
                case 90:
                    plane = d.BoxMaxX; lo = d.BoxMinZ; hi = d.BoxMaxZ; sx = 1f; sz = 0f;
                    break;
                case 270:
                    plane = d.BoxMinX; lo = d.BoxMinZ; hi = d.BoxMaxZ; sx = -1f; sz = 0f;
                    break;
                case 0:
                    plane = d.BoxMaxZ; lo = d.BoxMinX; hi = d.BoxMaxX; sx = 0f; sz = 1f;
                    break;
                default:
                    plane = d.BoxMinZ; lo = d.BoxMinX; hi = d.BoxMaxX; sx = 0f; sz = -1f;
                    break;
            }
            float from = lo + EndInset, to = hi - EndInset;
            float y0 = d.DoorY + FromGround, y1 = top - BelowTop;
            int columns = (int)Math.Round((to - from) / CellWide), rows = (int)Math.Floor((y1 - y0) / CellHigh + 0.25f);
            if (to - from < Least || columns < 1 || rows < 1)
            {
                return false;
            }
            // The viewer outside looks against the wall's way out: their right hand is (-nz, nx), so the panel runs from the left end to the right.
            float rx = -sz, rz = sx, mid = (from + to) * 0.5f, half = (to - from) * 0.5f;
            bool alongZ = dir == 90 || dir == 270;
            float cx = alongZ ? plane + sx * Lift : mid, cz = alongZ ? mid : plane + sz * Lift;
            panel.AX = cx - rx * half; panel.AZ = cz - rz * half;
            panel.BX = cx + rx * half; panel.BZ = cz + rz * half;
            if (alongZ)
            {
                panel.AX = panel.BX = plane + sx * Lift;
            }
            else
            {
                panel.AZ = panel.BZ = plane + sz * Lift;
            }
            panel.NX = sx; panel.NZ = sz;
            panel.Y0 = y0; panel.Y1 = y0 + rows * ((y1 - y0) / rows);
            panel.Columns = columns; panel.Rows = rows;
            panel.Style = (int)(NewBuildingsRules.Hash(id + "|windows") % (uint)Styles.Length);
            return true;
        }

        /// <summary>
        /// The panel's four corners (foot left, foot right, top right, top left, looking at it from outside) as x, y, z, with their texture
        /// coordinates (the cells counted across and up) and the two triangles that face outward: a front face is clockwise from the front.
        /// </summary>
        public static void Quad(SidePanel p, float[] positions, float[] uvs, int[] triangles)
        {
            positions[0] = p.AX; positions[1] = p.Y0; positions[2] = p.AZ;
            positions[3] = p.BX; positions[4] = p.Y0; positions[5] = p.BZ;
            positions[6] = p.BX; positions[7] = p.Y1; positions[8] = p.BZ;
            positions[9] = p.AX; positions[10] = p.Y1; positions[11] = p.AZ;
            uvs[0] = 0f; uvs[1] = 0f;
            uvs[2] = p.Columns; uvs[3] = 0f;
            uvs[4] = p.Columns; uvs[5] = p.Rows;
            uvs[6] = 0f; uvs[7] = p.Rows;
            // foot left, top left, top right; foot left, top right, foot right: clockwise from the front
            triangles[0] = 0; triangles[1] = 3; triangles[2] = 2;
            triangles[3] = 0; triangles[4] = 2; triangles[5] = 1;
        }

        /// <summary>True when a triangle (three corners as x, y, z from <paramref name="at"/>) faces the way <paramref name="nx"/>, <paramref name="nz"/>: Unity's front face is Cross(b - a, c - a).</summary>
        public static bool Faces(float[] positions, int[] triangles, int at, float nx, float nz)
        {
            int a = triangles[at] * 3, b = triangles[at + 1] * 3, c = triangles[at + 2] * 3;
            float ux = positions[b] - positions[a], uy = positions[b + 1] - positions[a + 1], uz = positions[b + 2] - positions[a + 2];
            float vx = positions[c] - positions[a], vy = positions[c + 1] - positions[a + 1], vz = positions[c + 2] - positions[a + 2];
            float cx = uy * vz - uz * vy, cz = ux * vy - uy * vx;
            return cx * nx + cz * nz > 0f;
        }
    }
}
