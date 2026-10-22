using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>The fit-out planner's working state: the grid's marks, the bare floor, and laying pieces out.</summary>
    public static partial class FitPlanner
    {
        /// <summary>The working state of one plan, its marks kept in arrays made once a plan.</summary>
        private sealed partial class Work
        {
            public readonly RoomGrid G;
            public readonly FitSite Site;
            public readonly FitPlan Into;
            public readonly bool[] Usable;
            private readonly bool[] _fixedClosed, _base, _closed, _after, _seen, _narrow, _trial;
            private readonly bool[] _keepValid;
            private readonly bool[] _sides;              // the floor beside the player's furniture, where it is used from
            private bool _hasSides;
            private readonly int[] _stack;
            private readonly float _margin;

            public Work(RoomGrid g, FitSite site, FitPlan into)
            {
                G = g;
                Site = site;
                Into = into;
                int n = g.Count;
                Usable = new bool[n];
                _fixedClosed = new bool[n];
                _base = new bool[n];
                _closed = new bool[n];
                _after = new bool[n];
                _seen = new bool[n];
                _stack = new int[n];
                _narrow = new bool[n];
                _trial = new bool[n];
                _sides = new bool[n];
                _keepValid = new bool[site.Keep.Count];
                _margin = Math.Max(0.1f, site.Erosion);   // what stands is cut out of the walkable floor by a walker's width, as the game's is
            }

            /// <summary>The floor reached from the door round what is already there, and which of the site's places it reaches.</summary>
            public bool Begin()
            {
                for (int i = 0; i < Site.Fixed.Count; i++)
                {
                    FitRect r = Site.Fixed[i];
                    if (r.Solid)
                    {
                        CloseRect(r.X0 - _margin, r.Z0 - _margin, r.X1 + _margin, r.Z1 + _margin, _fixedClosed);
                    }
                }
                if (G.Reach(Site.DoorX, Site.DoorZ, _fixedClosed, _base) == 0)
                {
                    return false;
                }
                Narrow();
                _hasSides = G.FurnitureSides(_base, _sides) > 0;
                // a place the building could not reach before is not the plan's to keep
                Array.Copy(_base, _after, _after.Length);
                for (int i = 0; i < _keepValid.Length; i++)
                {
                    FitSpot s = Site.Keep[i];
                    _keepValid[i] = ReachedNear(s.X, s.Z, Math.Max(Reach, s.R));
                }
                return true;
            }

            // The unit's doorways and narrow ways: open floor less than 1.8 m across between walls, running on for 2.4 m or
            // more the other way, and the floor within 0.9 m of it. Nothing of the plan's stands there.
            private void Narrow()
            {
                int cols = G.Cols, rows = G.Rows;
                var across = new int[cols * rows];
                var along = new int[cols * rows];
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols;)
                    {
                        if (!G.IsOpen(c, r))
                        {
                            c++;
                            continue;
                        }
                        int start = c;
                        while (c < cols && G.IsOpen(c, r))
                        {
                            c++;
                        }
                        for (int k = start; k < c; k++)
                        {
                            across[r * cols + k] = c - start;
                        }
                    }
                }
                for (int c = 0; c < cols; c++)
                {
                    for (int r = 0; r < rows;)
                    {
                        if (!G.IsOpen(c, r))
                        {
                            r++;
                            continue;
                        }
                        int start = r;
                        while (r < rows && G.IsOpen(c, r))
                        {
                            r++;
                        }
                        for (int k = start; k < r; k++)
                        {
                            along[k * cols + c] = r - start;
                        }
                    }
                }
                float cell = G.Cell;
                int reach = (int)Math.Ceiling(0.9f / cell);
                for (int i = 0; i < _narrow.Length; i++)
                {
                    float x = across[i] * cell, z = along[i] * cell;
                    if (across[i] == 0 || !((x <= 1.8f && z >= 2.4f) || (z <= 1.8f && x >= 2.4f)))
                    {
                        continue;
                    }
                    int c0 = i % cols, r0 = i / cols;
                    for (int dr = -reach; dr <= reach; dr++)
                    {
                        for (int dc = -reach; dc <= reach; dc++)
                        {
                            int c = c0 + dc, r = r0 + dr;
                            if (c >= 0 && r >= 0 && c < cols && r < rows)
                            {
                                _narrow[r * cols + c] = true;
                            }
                        }
                    }
                }
            }

            /// <summary>True if a rectangle covers a doorway or a narrow way of the unit.</summary>
            public bool InNarrow(float x0, float z0, float x1, float z1)
            {
                G.CellOf(x0, z0, out int c0, out int r0);
                G.CellOf(x1, z1, out int c1, out int r1);
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        G.Centre(c, r, out float x, out float z);
                        if (x >= x0 && x <= x1 && z >= z0 && z <= z1 && _narrow[r * G.Cols + c])
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            /// <summary>
            /// The largest rectangle of bare floor with no side under <paramref name="least"/>, by the histogram method, so a
            /// long strip a room cannot use never beats a smaller square it can; false if none.
            /// </summary>
            public bool Largest(float least, out float x0, out float z0, out float x1, out float z1)
            {
                x0 = z0 = x1 = z1 = 0f;
                int cols = G.Cols, rows = G.Rows;
                int minCols = (int)Math.Ceiling(least / G.Cell - 1e-4f), minRows = minCols;
                var height = new int[cols];
                var stack = new int[cols + 1];
                int bestArea = 0, bestC0 = 0, bestC1 = 0, bestR0 = 0, bestR1 = 0;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        height[c] = Usable[r * cols + c] ? height[c] + 1 : 0;
                    }
                    int top = 0;
                    for (int c = 0; c <= cols; c++)
                    {
                        int h = c < cols ? height[c] : 0;
                        while (top > 0 && height[stack[top - 1]] >= h)
                        {
                            int tall = height[stack[--top]];
                            int left = top > 0 ? stack[top - 1] + 1 : 0;
                            int wide = c - left;
                            if (tall >= minRows && wide >= minCols && tall * wide > bestArea)
                            {
                                bestArea = tall * wide;
                                bestC0 = left;
                                bestC1 = c - 1;
                                bestR1 = r;
                                bestR0 = r - tall + 1;
                            }
                        }
                        stack[top++] = c;
                    }
                }
                if (bestArea == 0)
                {
                    return false;
                }
                x0 = G.MinX + bestC0 * G.Cell;
                x1 = G.MinX + (bestC1 + 1) * G.Cell;
                z0 = G.MinZ + bestR0 * G.Cell;
                z1 = G.MinZ + (bestR1 + 1) * G.Cell;
                return true;
            }

            /// <summary>The floor the standing pieces from an index on take, in the room's axes.</summary>
            public void Footprint(int first, out float x0, out float z0, out float x1, out float z1)
            {
                x0 = z0 = float.MaxValue;
                x1 = z1 = float.MinValue;
                for (int i = first; i < Into.Pieces.Count; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (!p.Kept || (p.Role != FitRole.Floor && p.Role != FitRole.Seat && p.Role != FitRole.Hung))
                    {
                        continue;
                    }
                    x0 = Math.Min(x0, p.CX - p.HalfX);
                    z0 = Math.Min(z0, p.CZ - p.HalfZ);
                    x1 = Math.Max(x1, p.CX + p.HalfX);
                    z1 = Math.Max(z1, p.CZ + p.HalfZ);
                }
                if (x0 > x1)
                {
                    x0 = x1 = z0 = z1 = 0f;
                }
            }

            private void CloseRect(float x0, float z0, float x1, float z1, bool[] into)
            {
                G.CellOf(x0, z0, out int c0, out int r0);
                G.CellOf(x1, z1, out int c1, out int r1);
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        G.Centre(c, r, out float x, out float z);
                        if (x >= x0 && x <= x1 && z >= z0 && z <= z1)
                        {
                            into[r * G.Cols + c] = true;
                        }
                    }
                }
            }

            public int CountBase()
            {
                int n = 0;
                for (int i = 0; i < _base.Length; i++)
                {
                    n += _base[i] ? 1 : 0;
                }
                return n;
            }

            /// <summary>The reached cells further than <see cref="BareReach"/> from everything that stands, already there or planned, but for the ways in and the doorways.</summary>
            public int Bare()
            {
                Array.Clear(_seen, 0, _seen.Length);
                for (int i = 0; i < Site.Fixed.Count; i++)
                {
                    FitRect r = Site.Fixed[i];
                    CloseRect(r.X0 - BareReach, r.Z0 - BareReach, r.X1 + BareReach, r.Z1 + BareReach, _seen);
                }
                for (int i = 0; i < Into.Pieces.Count; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (p.Kept && (p.Role == FitRole.Floor || p.Role == FitRole.Seat || p.Role == FitRole.Flat))
                    {
                        CloseRect(p.CX - p.HalfX - BareReach, p.CZ - p.HalfZ - BareReach, p.CX + p.HalfX + BareReach, p.CZ + p.HalfZ + BareReach, _seen);
                    }
                }
                int bare = 0;
                float h = G.Cell * 0.5f;
                for (int i = 0; i < _base.Length; i++)
                {
                    if (!_base[i] || _seen[i] || _narrow[i])
                    {
                        continue;
                    }
                    G.Centre(i, out float x, out float z);
                    bare += AtADoor(x - h, z - h, x + h, z + h) ? 0 : 1;   // kept open on purpose
                }
                return bare;
            }

            /// <summary>The bare floor rooms may be laid out on: reached, a walkway from everything, clear of the doors and of the floor kept free.</summary>
            public void MarkUsable()
            {
                Array.Copy(_base, Usable, Usable.Length);
                bool[] near = _seen;
                Array.Clear(near, 0, near.Length);
                for (int i = 0; i < Site.Fixed.Count; i++)
                {
                    FitRect r = Site.Fixed[i];
                    CloseRect(r.X0 - Aisle, r.Z0 - Aisle, r.X1 + Aisle, r.Z1 + Aisle, near);
                }
                for (int i = 0; i < Site.KeepFree.Count; i++)
                {
                    FitRect r = Site.KeepFree[i];
                    CloseRect(r.X0 - 0.3f, r.Z0 - 0.3f, r.X1 + 0.3f, r.Z1 + 0.3f, near);
                }
                if (_hasSides)
                {
                    var furniture = new bool[near.Length];
                    G.NearFurniture(FromFurniture, furniture);
                    for (int i = 0; i < near.Length; i++)
                    {
                        near[i] |= furniture[i];
                    }
                }
                for (int i = 0; i < Into.Pieces.Count; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (p.Kept && (p.Role == FitRole.Floor || p.Role == FitRole.Seat))
                    {
                        CloseRect(p.CX - p.HalfX - Aisle, p.CZ - p.HalfZ - Aisle, p.CX + p.HalfX + Aisle, p.CZ + p.HalfZ + Aisle, near);
                    }
                }
                float h = G.Cell * 0.5f;
                for (int i = 0; i < Usable.Length; i++)
                {
                    if (!Usable[i])
                    {
                        continue;
                    }
                    G.Centre(i, out float x, out float z);
                    Usable[i] = !near[i] && !_narrow[i] && !AtADoor(x - h, z - h, x + h, z + h);
                }
            }

            /// <summary>A rectangle of the bare floor taken: no other room is laid out on it.</summary>
            public void Unmark(float x0, float z0, float x1, float z1)
            {
                bool[] off = _seen;
                Array.Clear(off, 0, off.Length);
                CloseRect(x0, z0, x1, z1, off);
                for (int i = 0; i < Usable.Length; i++)
                {
                    Usable[i] &= !off[i];
                }
            }

            /// <summary>The places somebody walking about goes to: in front of whatever is walked up to, and beside a chair of each group of desks.</summary>
            public void Stops()
            {
                Into.Stops.Clear();
                var groups = new HashSet<long>();
                for (int i = 0; i < Into.Pieces.Count && Into.Stops.Count < 24; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (!p.Kept || p.Role == FitRole.Lamp || p.Role == FitRole.Hung)
                    {
                        continue;
                    }
                    if (p.Use == FitUse.Stop)
                    {
                        float ahead = p.HalfAlongFront + 0.55f;
                        Into.Stops.Add(new FitSpot(p.CX + p.FrontX * ahead, p.CZ + p.FrontZ * ahead, 0f));
                        continue;
                    }
                    if (p.Role != FitRole.Seat || (p.Use != FitUse.Desk && p.Use != FitUse.Meeting))
                    {
                        continue;
                    }
                    // one for each room of the plan's, and each six metres square of the design's: a word with a colleague
                    long group = p.Design ? 1000000L + (long)Math.Floor(p.CX / 6f) * 1000L + (long)Math.Floor(p.CZ / 6f) : p.Zone * 8L + (long)p.Use;
                    if (groups.Add(group))
                    {
                        float sideX = p.FrontZ, sideZ = -p.FrontX;
                        Into.Stops.Add(new FitSpot(p.CX + sideX * 0.75f - p.FrontX * 0.2f, p.CZ + sideZ * 0.75f - p.FrontZ * 0.2f, 0f));
                    }
                }
            }

            /// <summary>
            /// A piece at (u, v) in a box, its front turned to (fu, fv), placed by its middle (centred) or its pivot. Its index,
            /// or -1 at a door or, being tall, in the street wall's windows.
            /// </summary>
            public int Put(Box b, string prefab, FitRole role, FitUse use, float u, float v, float fu, float fv, int anchor, int zone, bool centred)
            {
                FitSize s = Site.Size(prefab);
                b.Point(u, v, out float px, out float pz);
                b.Dir(fu, fv, out float fx, out float fz);
                float yaw = SitePlanBase.YawOf(fx, fz);
                float sin = (float)Math.Sin(yaw * Math.PI / 180.0), cos = (float)Math.Cos(yaw * Math.PI / 180.0);
                // the drawn middle from the pivot, turned: (x, z) goes to (x cos + z sin, -x sin + z cos)
                float mx = s.CX * cos + s.CZ * sin, mz = -s.CX * sin + s.CZ * cos;
                float cx = px, cz = pz;
                if (centred)
                {
                    px -= mx;
                    pz -= mz;
                }
                else
                {
                    cx += mx;
                    cz += mz;
                }
                float halfX = Math.Abs(cos) * s.W * 0.5f + Math.Abs(sin) * s.D * 0.5f, halfZ = Math.Abs(sin) * s.W * 0.5f + Math.Abs(cos) * s.D * 0.5f;
                bool tall = s.H > 1.3f;
                if (AtADoor(cx - halfX, cz - halfZ, cx + halfX, cz + halfZ) || InNarrow(cx - halfX, cz - halfZ, cx + halfX, cz + halfZ)
                    || (tall && Site.Windows && Facade(cx - halfX, cz - halfZ, cx + halfX, cz + halfZ)))
                {
                    return -1;
                }
                Into.Pieces.Add(new FitPiece
                {
                    Prefab = prefab, Role = role, Use = use, X = px, Z = pz, Yaw = yaw, CX = cx, CZ = cz, HalfX = halfX, HalfZ = halfZ,
                    Height = s.H, Anchor = anchor, Zone = zone, Kept = true, Tall = tall
                });
                return Into.Pieces.Count - 1;
            }

            /// <summary>A piece backing onto the box's wall at u, facing into the box.</summary>
            public int AgainstWall(Box b, string prefab, float u, FitUse use, int zone)
            {
                FitSize s = Site.Size(prefab);
                return Put(b, prefab, FitRole.Floor, use, u, b.WallBack + s.D * 0.5f, 0f, 1f, -1, zone, centred: true);
            }

            /// <summary>Something small on a piece: at a place in that piece's own frame, turned a little more if asked.</summary>
            public int OnTop(int under, string prefab, float localX, float localZ, float turn, float top, FitUse use = FitUse.None)
            {
                if (under < 0)
                {
                    return -1;
                }
                FitPiece d = Into.Pieces[under];
                float sin = (float)Math.Sin(d.Yaw * Math.PI / 180.0), cos = (float)Math.Cos(d.Yaw * Math.PI / 180.0);
                float x = d.X + localX * cos + localZ * sin, z = d.Z - localX * sin + localZ * cos;
                FitSize s = Site.Size(prefab);
                Into.Pieces.Add(new FitPiece
                {
                    Prefab = prefab, Role = FitRole.OnTop, Use = use, X = x, Z = z, Y = top, Yaw = d.Yaw + turn, CX = x, CZ = z, HalfX = s.W * 0.5f, HalfZ = s.D * 0.5f,
                    Height = s.H, Anchor = under, Zone = d.Zone, Kept = true
                });
                return Into.Pieces.Count - 1;
            }

            /// <summary>Something hung on the box's wall at u, at a height; the game side finds the wall's real surface behind it.</summary>
            public void Hang(Box b, string prefab, float u, float height, int zone)
            {
                if (!b.WallV0)
                {
                    return;
                }
                FitSize s = Site.Size(prefab);
                b.Point(u, 0.05f, out float x, out float z);
                b.Dir(0f, 1f, out float fx, out float fz);
                Into.Pieces.Add(new FitPiece
                {
                    Prefab = prefab, Role = FitRole.Hung, X = x, Z = z, Y = height, Yaw = SitePlanBase.YawOf(fx, fz), CX = x, CZ = z,
                    HalfX = Math.Abs(fz) * s.W * 0.5f + 0.05f, HalfZ = Math.Abs(fx) * s.W * 0.5f + 0.05f, Height = s.H, Anchor = -1, Zone = zone, Kept = true
                });
            }

            /// <summary>Floor in a box that no piece of the room being laid out stands on, and not at a door.</summary>
            public bool Free(Box b, float u, float v, float halfU, float halfV, int zone)
            {
                b.Point(u, v, out float x, out float z);
                b.Dir(1f, 0f, out float ax, out float az);
                float hx = Math.Abs(ax) * halfU + Math.Abs(az) * halfV, hz = Math.Abs(az) * halfU + Math.Abs(ax) * halfV;
                for (int i = 0; i < Into.Pieces.Count; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (p.Kept && p.Zone == zone && (p.Role == FitRole.Floor || p.Role == FitRole.Seat)
                        && Math.Abs(p.CX - x) < p.HalfX + hx && Math.Abs(p.CZ - z) < p.HalfZ + hz)
                    {
                        return false;
                    }
                }
                return !AtADoor(x - hx, z - hz, x + hx, z + hz) && !InNarrow(x - hx, z - hz, x + hx, z + hz);
            }
        }
    }
}
