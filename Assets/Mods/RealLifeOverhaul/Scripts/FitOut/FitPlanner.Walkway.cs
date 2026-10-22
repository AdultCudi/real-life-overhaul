using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>The fit-out planner's walkway check, and which pieces of a design go so that every way stays open.</summary>
    public static partial class FitPlanner
    {
        private sealed partial class Work
        {
            /// <summary>
            /// Leaves out the kept design piece whose going opens a closed place again: of those touching it, the first that
            /// does, partitions last and the smallest first; else the nearest. Its index, or -1.
            /// </summary>
            public int Opener(float px, float pz, int first)
            {
                List<FitPiece> pieces = Into.Pieces;
                G.CellOf(px, pz, out int pc, out int pr);
                int start = pc >= 0 && pr >= 0 && pc < G.Cols && pr < G.Rows ? pr * G.Cols + pc : -1;
                bool pocket = start >= 0 && _base[start] && !_closed[start] && !_after[start];
                if (pocket)
                {
                    Array.Clear(_seen, 0, _seen.Length);
                    Pocket(start, out _, out _, out _, out _);   // its cells are marked in _seen
                }
                var candidates = new List<int>(16);
                float grow = _margin + G.Cell;
                for (int i = first; i < pieces.Count; i++)
                {
                    FitPiece p = pieces[i];
                    if (!p.Kept || !p.Design || p.Role != FitRole.Floor)
                    {
                        continue;
                    }
                    bool touches = pocket ? Touches(p, grow, _seen) : new FitRect(p.CX - p.HalfX, p.CZ - p.HalfZ, p.CX + p.HalfX, p.CZ + p.HalfZ, true).Distance(px, pz) < 1.5f;
                    if (touches)
                    {
                        candidates.Add(i);
                    }
                }
                candidates.Sort((a, b) =>
                {
                    int byKind = (FitRoles.IsPartition(pieces[a].Prefab) ? 1 : 0).CompareTo(FitRoles.IsPartition(pieces[b].Prefab) ? 1 : 0);
                    return byKind != 0 ? byKind : (pieces[a].HalfX * pieces[a].HalfZ).CompareTo(pieces[b].HalfX * pieces[b].HalfZ);
                });
                for (int k = 0; k < candidates.Count && k < 10; k++)
                {
                    int i = candidates[k];
                    FitPiece p = pieces[i];
                    p.Kept = false;
                    pieces[i] = p;
                    if (Opens(px, pz))
                    {
                        return i;
                    }
                    p.Kept = true;
                    pieces[i] = p;
                }
                int nearest = NearestDroppable(px, pz, first);
                if (nearest >= 0)
                {
                    FitPiece p = pieces[nearest];
                    p.Kept = false;
                    pieces[nearest] = p;
                }
                return nearest;
            }

            // A piece's footprint, grown, over any marked cell.
            private bool Touches(FitPiece p, float grow, bool[] marked)
            {
                G.CellOf(p.CX - p.HalfX - grow, p.CZ - p.HalfZ - grow, out int c0, out int r0);
                G.CellOf(p.CX + p.HalfX + grow, p.CZ + p.HalfZ + grow, out int c1, out int r1);
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        if (marked[r * G.Cols + c])
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // With the pieces kept as they are now, is a place reached from the door?
            private bool Opens(float x, float z)
            {
                Array.Copy(_fixedClosed, _closed, _closed.Length);
                List<FitPiece> pieces = Into.Pieces;
                for (int i = 0; i < pieces.Count; i++)
                {
                    FitPiece p = pieces[i];
                    if (p.Kept && p.Role == FitRole.Floor)
                    {
                        CloseRect(p.CX - p.HalfX - _margin, p.CZ - p.HalfZ - _margin, p.CX + p.HalfX + _margin, p.CZ + p.HalfZ + _margin, _closed);
                    }
                }
                G.Reach(Site.DoorX, Site.DoorZ, _closed, _trial);
                G.CellOf(x - Reach, z - Reach, out int c0, out int r0);
                G.CellOf(x + Reach, z + Reach, out int c1, out int r1);
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        G.Centre(c, r, out float cx, out float cz);
                        if (_trial[r * G.Cols + c] && (cx - x) * (cx - x) + (cz - z) * (cz - z) <= Reach * Reach)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            /// <summary>A piece of the design that would stand where it may not: on what is there, on floor kept free, at a door.</summary>
            public bool Clashes(FitPiece p)
            {
                if (!FitRoles.Fitted(p.Prefab))
                {
                    return true;
                }
                if (p.Role == FitRole.Lamp || p.Role == FitRole.OnTop)
                {
                    return false;   // on the ceiling, or going with what it stands on
                }
                if (p.Role == FitRole.Hung)
                {
                    for (int i = 0; i < Site.Fixed.Count; i++)
                    {
                        FitRect r = Site.Fixed[i];
                        if ((r.Wall && r.Distance(p.X, p.Z) < 1.0f) || (r.Solid && r.Distance(p.X, p.Z) < 0.05f))
                        {
                            return true;
                        }
                    }
                    return false;
                }
                float grow = p.Role == FitRole.Flat ? 0f : 0.2f;
                for (int i = 0; i < Site.Fixed.Count; i++)
                {
                    if (Overlaps(p, Site.Fixed[i], grow))
                    {
                        return true;
                    }
                }
                if (p.Role == FitRole.Flat)
                {
                    return false;   // a carpet may lie at the door and on the way in
                }
                for (int i = 0; i < Site.KeepFree.Count; i++)
                {
                    if (Overlaps(p, Site.KeepFree[i], 0f))
                    {
                        return true;
                    }
                }
                return AtADoor(p.CX - p.HalfX, p.CZ - p.HalfZ, p.CX + p.HalfX, p.CZ + p.HalfZ);
            }

            private static bool Overlaps(FitPiece p, FitRect r, float grow)
            {
                return p.CX + p.HalfX > r.X0 - grow && p.CX - p.HalfX < r.X1 + grow && p.CZ + p.HalfZ > r.Z0 - grow && p.CZ - p.HalfZ < r.Z1 + grow;
            }

            /// <summary>A rectangle within reach of the door, on the way straight in from it, or round another way out.</summary>
            public bool AtADoor(float x0, float z0, float x1, float z1)
            {
                var foot = new FitRect(x0, z0, x1, z1, true);
                if (foot.Distance(Site.DoorX, Site.DoorZ) < DoorClear)
                {
                    return true;
                }
                // the way in: a corridor straight in from the door, whose frame is square to the room's axes
                float ax = Site.InZ * WayInHalf, az = -Site.InX * WayInHalf;
                float ex = Site.DoorX + Site.InX * WayIn, ez = Site.DoorZ + Site.InZ * WayIn;
                float wx0 = Math.Min(Math.Min(Site.DoorX - ax, Site.DoorX + ax), Math.Min(ex - ax, ex + ax));
                float wx1 = Math.Max(Math.Max(Site.DoorX - ax, Site.DoorX + ax), Math.Max(ex - ax, ex + ax));
                float wz0 = Math.Min(Math.Min(Site.DoorZ - az, Site.DoorZ + az), Math.Min(ez - az, ez + az));
                float wz1 = Math.Max(Math.Max(Site.DoorZ - az, Site.DoorZ + az), Math.Max(ez - az, ez + az));
                if (x1 > wx0 && x0 < wx1 && z1 > wz0 && z0 < wz1)
                {
                    return true;
                }
                for (int i = 0; i < Site.Exits.Count; i++)
                {
                    FitSpot e = Site.Exits[i];
                    if (foot.Distance(e.X, e.Z) < e.R)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>In the band of floor along the street wall, where its windows are.</summary>
            public bool Facade(float x0, float z0, float x1, float z1)
            {
                float least = float.MaxValue;
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = (corner & 1) == 0 ? x0 : x1, z = (corner & 2) == 0 ? z0 : z1;
                    least = Math.Min(least, (x - Site.DoorX) * Site.InX + (z - Site.DoorZ) * Site.InZ);
                }
                return least < FacadeBand;
            }

            /// <summary>Whatever stands on a piece left out, or is drawn up to it, is left out with it.</summary>
            public void DropDependents(int first)
            {
                List<FitPiece> pieces = Into.Pieces;
                for (int pass = 0; pass < 3; pass++)
                {
                    bool any = false;
                    for (int i = first; i < pieces.Count; i++)
                    {
                        FitPiece p = pieces[i];
                        if (p.Kept && p.Anchor >= 0 && !pieces[p.Anchor].Kept)
                        {
                            p.Kept = false;
                            pieces[i] = p;
                            any = true;
                        }
                    }
                    if (!any)
                    {
                        return;
                    }
                }
            }

            /// <summary>The walkway check: true if from the door everything to be reached is and nothing reached before is closed off; else the counts and the first place wanting.</summary>
            public bool Check(bool stopAtFirst, out int unreached, out int pockets, out float px, out float pz)
            {
                unreached = pockets = 0;
                px = pz = 0f;
                bool found = false;
                Array.Copy(_fixedClosed, _closed, _closed.Length);
                List<FitPiece> pieces = Into.Pieces;
                for (int i = 0; i < pieces.Count; i++)
                {
                    FitPiece p = pieces[i];
                    if (p.Kept && p.Role == FitRole.Floor)
                    {
                        CloseRect(p.CX - p.HalfX - _margin, p.CZ - p.HalfZ - _margin, p.CX + p.HalfX + _margin, p.CZ + p.HalfZ + _margin, _closed);
                    }
                }
                G.Reach(Site.DoorX, Site.DoorZ, _closed, _after);
                // the places the site had to be reached
                for (int i = 0; i < _keepValid.Length; i++)
                {
                    FitSpot s = Site.Keep[i];
                    if (!_keepValid[i] || ReachedNear(s.X, s.Z, Math.Max(Reach, s.R)))
                    {
                        continue;
                    }
                    unreached++;
                    if (!found)
                    {
                        found = true;
                        px = s.X;
                        pz = s.Z;
                    }
                    if (stopAtFirst)
                    {
                        return false;
                    }
                }
                // where the player's own furniture is used from
                for (int i = 0; _hasSides && i < _sides.Length; i++)
                {
                    if (!_sides[i] || _after[i])
                    {
                        continue;
                    }
                    unreached++;
                    if (!found)
                    {
                        found = true;
                        G.Centre(i, out px, out pz);
                    }
                    if (stopAtFirst)
                    {
                        return false;
                    }
                }
                // every seat and every stop
                for (int i = 0; i < pieces.Count; i++)
                {
                    FitPiece p = pieces[i];
                    if (!p.Kept || p.Use == FitUse.None || p.Role == FitRole.Lamp || p.Role == FitRole.Hung
                        || ReachedNearRect(p.CX - p.HalfX, p.CZ - p.HalfZ, p.CX + p.HalfX, p.CZ + p.HalfZ, Reach))
                    {
                        continue;
                    }
                    unreached++;
                    if (!found)
                    {
                        found = true;
                        px = p.CX;
                        pz = p.CZ;
                    }
                    if (stopAtFirst)
                    {
                        return false;
                    }
                }
                // all the floor that could be walked to before
                Array.Clear(_seen, 0, _seen.Length);
                for (int i = 0; i < _base.Length; i++)
                {
                    if (!_base[i] || _closed[i] || _after[i] || _seen[i])
                    {
                        continue;
                    }
                    int cells = Pocket(i, out int c0, out int r0, out int c1, out int r1);
                    if (cells < PocketCells || c1 - c0 < 1 || r1 - r0 < 1)
                    {
                        continue;   // a gap behind something: nobody's way
                    }
                    pockets++;
                    if (!found)
                    {
                        found = true;
                        G.Centre(i, out px, out pz);
                    }
                    if (stopAtFirst)
                    {
                        return false;
                    }
                }
                return !found;
            }

            // The part of the floor closed off with a cell: how many cells, and its extent in cells.
            private int Pocket(int start, out int c0, out int r0, out int c1, out int r1)
            {
                int top = 0, count = 0;
                c0 = c1 = start % G.Cols;
                r0 = r1 = start / G.Cols;
                _stack[top++] = start;
                _seen[start] = true;
                while (top > 0)
                {
                    int i = _stack[--top];
                    count++;
                    int c = i % G.Cols, r = i / G.Cols;
                    c0 = Math.Min(c0, c);
                    c1 = Math.Max(c1, c);
                    r0 = Math.Min(r0, r);
                    r1 = Math.Max(r1, r);
                    for (int k = 0; k < 4; k++)
                    {
                        int nc = c + (k == 0 ? 1 : k == 1 ? -1 : 0), nr = r + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nc < 0 || nr < 0 || nc >= G.Cols || nr >= G.Rows)
                        {
                            continue;
                        }
                        int n = nr * G.Cols + nc;
                        if (_base[n] && !_closed[n] && !_after[n] && !_seen[n])
                        {
                            _seen[n] = true;
                            _stack[top++] = n;
                        }
                    }
                }
                return count;
            }

            private bool ReachedNear(float x, float z, float within)
            {
                G.CellOf(x - within, z - within, out int c0, out int r0);
                G.CellOf(x + within, z + within, out int c1, out int r1);
                float sqr = within * within;
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        if (!_after[r * G.Cols + c])
                        {
                            continue;
                        }
                        G.Centre(c, r, out float cx, out float cz);
                        if ((cx - x) * (cx - x) + (cz - z) * (cz - z) <= sqr)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // A reached cell within a distance of a rectangle.
            private bool ReachedNearRect(float x0, float z0, float x1, float z1, float within)
            {
                G.CellOf(x0 - within, z0 - within, out int c0, out int r0);
                G.CellOf(x1 + within, z1 + within, out int c1, out int r1);
                var rect = new FitRect(x0, z0, x1, z1, false);
                for (int r = Math.Max(0, r0); r <= Math.Min(G.Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(G.Cols - 1, c1); c++)
                    {
                        if (!_after[r * G.Cols + c])
                        {
                            continue;
                        }
                        G.Centre(c, r, out float cx, out float cz);
                        if (rect.Distance(cx, cz) <= within)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            /// <summary>The kept floor piece of the design that stands nearest a place, to be left out; -1 for none.</summary>
            public int NearestDroppable(float x, float z, int first)
            {
                int best = -1;
                float least = float.MaxValue;
                for (int i = first; i < Into.Pieces.Count; i++)
                {
                    FitPiece p = Into.Pieces[i];
                    if (!p.Kept || !p.Design || p.Role != FitRole.Floor)
                    {
                        continue;
                    }
                    float dx = Math.Max(0f, Math.Abs(x - p.CX) - p.HalfX), dz = Math.Max(0f, Math.Abs(z - p.CZ) - p.HalfZ);
                    float d = dx * dx + dz * dz;
                    if (d < least)
                    {
                        least = d;
                        best = i;
                    }
                }
                return best;
            }
        }
    }
}
