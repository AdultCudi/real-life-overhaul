using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>One piece of the district as it stands or is planned to: a building or a filler, its design and the ground it stands on.</summary>
    public struct LookPiece
    {
        public string Id;          // its lot's id for a building; the filler's own name
        public DesignEntry Design; // the design it wears
        public MadeLot Ground;     // the ground it stands on, its front on the ground's front line, facing as the lot does
        public bool Filler;        // scenery: a design copied for its look alone
        public bool Back;          // a filler's second row behind a building, not a piece of the street's front
    }

    /// <summary>One end of a piece on the street's front that nothing stands against.</summary>
    public struct LookEnd
    {
        public int Piece;       // its place in the pieces
        public bool High;       // the end at the higher end of the strip (east or north)
        public bool Road;       // a road lies straight beyond it
        public bool Right;      // it is the piece's right side looking out from its street
        public SideKind Kind;   // what its design has there
    }

    /// <summary>One thing the census found wrong, and where (m, the world's x and z).</summary>
    public struct LookFault
    {
        public string Id;
        public float X, Z;
        public string Text;
    }

    /// <summary>What <see cref="LookCensus.Count"/> found.</summary>
    public sealed class LookReport
    {
        public int Buildings;        // buildings that stand, each on its lot
        public int Fillers;          // fillers on the street's front, scenery beside buildings or along the district's edge
        public int Behind;           // fillers behind buildings, in second rows
        public int Lots;             // lots of the plan, the big places' ground not among them
        public int Land;             // lots with no building: open land for the construction game, a landmark's block, a lot something stands on
        public int OpenEnds;         // ends of pieces on the street's front that nothing stands against: a side shown to the street, an open lot or a corner
        public int RoadEnds;         // of those, the ones with a road straight beyond
        public int BlankEnds;        // of the open ends, those whose design's side there has no window of its own
        public int BlankRoadEnds;    // of those, the ones on a road
        public int BareEnds;         // of the blank ends, those that cannot be dressed with windows either: a low or stepped wall
        public int BareRoadEnds;     // of those, the ones on a road
        public int Dressed;          // of the blank ends, those given a window overlay (SideRules)
        public int DressedRoad;      // of those, the ones on a road
        public int Yards;            // buildings that leave a yard of BackLeast or more behind them where no second row stands
        public float YardArea;       // m2 of those yards
        public int YardsShown;       // of those, the yards a street or an open lot looks into: the building has an open end
        public int Gaps;             // gaps wider than a sliver between two neighbours of a street's front, inside the lots that are built
        public float WidestGap;      // m
        public int Facing;           // pieces whose door does not face the way its lot's street lies
        public readonly List<LookFault> Faults = new List<LookFault>(16);   // each thing found, at most the first <see cref="LookCensus.Listed"/>
        public int FaultCount;       // everything found, listed or not

        /// <summary>Open ends still showing a blank wall once the overlays are laid.</summary>
        public int LeftBlank => BlankEnds - Dressed;

        /// <summary>Of those, the ones a road lies beyond.</summary>
        public int LeftBlankRoad => BlankRoadEnds - DressedRoad;

        /// <summary>One line for the log: every count.</summary>
        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            return Buildings + " buildings and " + Fillers + " fillers on the front (" + Behind + " behind), " + Land + " of " + Lots + " lots with no building; "
                   + OpenEnds + " open ends (" + RoadEnds + " on a road), " + BlankEnds + " with no window of their own (" + BlankRoadEnds + " on a road), "
                   + Dressed + " given windows (" + DressedRoad + " on a road), " + LeftBlank + " left blank (" + LeftBlankRoad + " on a road), " + BareEnds + " that cannot be dressed; "
                   + Gaps + " gaps wider than " + FrontageRules.SliverMost.ToString("0", c) + " m inside built lots (the widest " + WidestGap.ToString("0.0", c) + " m), "
                   + Yards + " yards behind buildings (" + YardArea.ToString("0", c) + " m2, " + YardsShown + " seen from a street or an open lot); "
                   + Facing + " turned off their street";
        }
    }

    /// <summary>
    /// The census of Uptown's looks (the owner, 27 Oct 2026: "A lot of buildings have missing textures, there are gaps between
    /// buildings and buildings do not face the road properly"), game-free: from the pieces that stand (a load's, the plan's with
    /// the game's designs, or the test's) and the plan's lots, the ends of every piece nothing stands against and whether the design
    /// shows a bare side there, the gaps along each street's front inside the lots that are built, the lots with no building, and
    /// whether every piece's door faces the street its lot is on. The same rules run offline on the plan (EconomyCheck's
    /// UptownLookChecks) and in the game (Options > Tests, "Uptown: the look census"). COST: nothing per frame; one pass over the
    /// pieces, the neighbours of each found by their strip: a test's, never a frame's.
    /// </summary>
    public static class LookCensus
    {
        public const int Listed = 40;            // faults written out in the line at most
        public const float Closed = 0.8f;        // of an end's face the pieces against it cover for it to count as closed
        public const float TurnLeast = 1f;       // degrees a door may face off its lot's street

        private struct Box
        {
            public float X0, Z0, X1, Z1;   // m: the piece's footprint, along the world's axes (Uptown's grid lies along them)
            public float Along0, Along1;   // m: its extent along its strip
            public float Across0, Across1; // m: and across it
            public bool AlongX;
            public int Right;              // the way its right side runs: 1 when its higher end is on the right looking out
            public int At;                 // its place in the pieces
        }

        /// <summary>The census: <paramref name="pieces"/> against the plan's <paramref name="lots"/>, <paramref name="built"/> telling which lot has a building.</summary>
        public static LookReport Count(IList<LookPiece> pieces, IList<BuildingsLot> lots, Func<string, bool> built)
        {
            var r = new LookReport();
            if (pieces == null || lots == null)
            {
                return r;
            }
            var boxes = new List<Box>(pieces.Count);
            for (int i = 0; i < pieces.Count; i++)
            {
                LookPiece p = pieces[i];
                if (p.Design == null)
                {
                    continue;
                }
                r.Buildings += !p.Filler ? 1 : 0;
                r.Fillers += p.Filler && !p.Back ? 1 : 0;
                r.Behind += p.Filler && p.Back ? 1 : 0;
                Box b = BoxOf(p);
                b.At = i;
                boxes.Add(b);
                Facing(p, r);
            }
            Ends(pieces, boxes, r);
            Yards(pieces, lots, r, boxes);
            Land(lots, built, r);
            Gaps(pieces, boxes, lots, built, r);
            return r;
        }

        private static Box BoxOf(LookPiece p)
        {
            NewBuildingsRules.Footprint(p.Design, p.Ground, out float cx, out float cz, out float hw, out float hd);
            bool alongX = Math.Abs(Math.Cos(p.Ground.Facing * Math.PI / 180.0)) > 0.5;
            var b = new Box { AlongX = alongX, Right = LookRules.RightIsHigh(p.Ground.Facing) ? 1 : 0 };
            b.X0 = alongX ? cx - hw : cx - hd;
            b.X1 = alongX ? cx + hw : cx + hd;
            b.Z0 = alongX ? cz - hd : cz - hw;
            b.Z1 = alongX ? cz + hd : cz + hw;
            b.Along0 = alongX ? b.X0 : b.Z0;
            b.Along1 = alongX ? b.X1 : b.Z1;
            b.Across0 = alongX ? b.Z0 : b.X0;
            b.Across1 = alongX ? b.Z1 : b.X1;
            return b;
        }

        // The piece's door faces the way its lot's street lies: the turn the engine gives it plus its door's facing is the lot's facing.
        private static void Facing(LookPiece p, LookReport r)
        {
            NewBuildingsRules.Place(p.Design, p.Ground, out _, out _, out float yaw);
            float away = Math.Abs(NewBuildingsRules.Turn(yaw + NewBuildingsRules.FrontOf(p.Design) - p.Ground.Facing + 180f) - 180f);
            if (away > TurnLeast)
            {
                r.Facing++;
                float doorFaces = NewBuildingsRules.Turn(yaw + NewBuildingsRules.FrontOf(p.Design));
                Note(r, p.Id, p.Ground.X, p.Ground.Z, "its door faces " + doorFaces.ToString("0", CultureInfo.InvariantCulture) + ", its street lies "
                        + p.Ground.Facing.ToString("0", CultureInfo.InvariantCulture) + " (" + p.Design.Name + ")");
            }
        }

        // Each end of every piece on the street's front: how much of its face the other pieces stand against, a road beyond it, a bare side.
        private static void Ends(IList<LookPiece> pieces, List<Box> boxes, LookReport r)
        {
            var open = new List<LookEnd>(256);
            OpenEnds(pieces, boxes, open);
            for (int i = 0; i < open.Count; i++)
            {
                LookEnd e = open[i];
                r.OpenEnds++;
                r.RoadEnds += e.Road ? 1 : 0;
                if (e.Kind != SideKind.Windowed)
                {
                    r.BlankEnds++;
                    r.BlankRoadEnds += e.Road ? 1 : 0;
                    if (e.Kind == SideKind.Dressable && SideRules.Panel(pieces[e.Piece].Design, pieces[e.Piece].Ground, e.High, pieces[e.Piece].Id, out _))
                    {
                        r.Dressed++;
                        r.DressedRoad += e.Road ? 1 : 0;
                    }
                }
                if (e.Kind == SideKind.Bare)
                {
                    LookPiece p = pieces[e.Piece];
                    r.BareEnds++;
                    r.BareRoadEnds += e.Road ? 1 : 0;
                    Note(r, p.Id, p.Ground.X, p.Ground.Z, "its " + (e.Right ? "right" : "left") + " side is a wall nothing can be done with and open"
                                                          + (e.Road ? " to a road" : "") + " (" + p.Design.Name + ")");
                }
            }
        }

        /// <summary>
        /// The ends of the pieces on the street's front that nothing stands against, into the caller's list (cleared): a piece's end is open
        /// when the pieces against it cover less of its face than <see cref="Closed"/>. Second rows count too: a street shows the side of the one at a strip's end.
        /// </summary>
        public static void OpenEnds(IList<LookPiece> pieces, List<LookEnd> into)
        {
            into.Clear();
            var boxes = new List<Box>(pieces != null ? pieces.Count : 0);
            for (int i = 0; pieces != null && i < pieces.Count; i++)
            {
                if (pieces[i].Design == null)
                {
                    continue;
                }
                Box b = BoxOf(pieces[i]);
                b.At = i;
                boxes.Add(b);
            }
            OpenEnds(pieces, boxes, into);
        }

        private static void OpenEnds(IList<LookPiece> pieces, List<Box> boxes, List<LookEnd> into)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                LookPiece p = pieces[boxes[i].At];
                for (int end = 0; end < 2; end++)
                {
                    bool high = end == 1;
                    if (Covered(boxes, i, high) >= Closed)
                    {
                        continue;
                    }
                    Box b = boxes[i];
                    float beyond = high ? b.Along1 + LookRules.OnRoadReach : b.Along0 - LookRules.OnRoadReach;
                    float middle = (b.Across0 + b.Across1) * 0.5f;
                    bool road = b.AlongX ? LookRules.OnRoad(beyond, middle) : LookRules.OnRoad(middle, beyond);
                    bool right = high == (b.Right == 1);
                    into.Add(new LookEnd { Piece = boxes[i].At, High = high, Road = road, Right = right, Kind = LookRules.Kind(p.Design, right) });
                }
            }
        }

        // The share of one end's face the other pieces cover within a sliver of it (the pieces of other strips too: a street's first
        // building stands against the back of the avenue's).
        private static float Covered(List<Box> boxes, int i, bool high)
        {
            Box b = boxes[i];
            float edge = high ? b.Along1 : b.Along0;
            float lo = b.Across0, hi = b.Across1, total = hi - lo;
            if (total <= 0.01f)
            {
                return 1f;
            }
            float covered = 0f;
            float last = lo;
            var spans = new List<float[]>(4);
            for (int k = 0; k < boxes.Count; k++)
            {
                if (k == i)
                {
                    continue;
                }
                Box o = boxes[k];
                if (o.AlongX != b.AlongX)
                {
                    // another strip's piece: its own axes are swapped, so its extent along our strip is its across
                    float oa0 = b.AlongX ? o.X0 : o.Z0, oa1 = b.AlongX ? o.X1 : o.Z1, oc0 = b.AlongX ? o.Z0 : o.X0, oc1 = b.AlongX ? o.Z1 : o.X1;
                    if (Reaches(edge, high, oa0, oa1) && Math.Min(oc1, hi) > Math.Max(oc0, lo))
                    {
                        spans.Add(new[] { Math.Max(oc0, lo), Math.Min(oc1, hi) });
                    }
                    continue;
                }
                if (Reaches(edge, high, o.Along0, o.Along1) && Math.Min(o.Across1, hi) > Math.Max(o.Across0, lo))
                {
                    spans.Add(new[] { Math.Max(o.Across0, lo), Math.Min(o.Across1, hi) });
                }
            }
            spans.Sort((a, c) => a[0].CompareTo(c[0]));
            for (int s = 0; s < spans.Count; s++)
            {
                float from = Math.Max(spans[s][0], last);
                if (spans[s][1] > from)
                {
                    covered += spans[s][1] - from;
                    last = spans[s][1];
                }
            }
            return covered / total;
        }

        // True when a piece's extent [a0, a1] along our strip reaches the end at <edge>, within a sliver outside it or inside it.
        private static bool Reaches(float edge, bool high, float a0, float a1)
        {
            return high ? a0 <= edge + LookRules.TouchReach && a1 > edge : a1 >= edge - LookRules.TouchReach && a0 < edge;
        }

        // A building that leaves BackLeast or more of its lot behind it where no piece of a second row stands.
        private static void Yards(IList<LookPiece> pieces, IList<BuildingsLot> lots, LookReport r, List<Box> boxes)
        {
            var open = new List<LookEnd>(256);
            OpenEnds(pieces, boxes, open);
            var shown = new HashSet<int>();
            for (int i = 0; i < open.Count; i++)
            {
                shown.Add(open[i].Piece);
            }
            var byId = new Dictionary<string, BuildingsLot>(lots.Count, StringComparer.Ordinal);
            for (int i = 0; i < lots.Count; i++)
            {
                byId[lots[i].Id] = lots[i];
            }
            for (int i = 0; i < pieces.Count; i++)
            {
                LookPiece p = pieces[i];
                if (p.Filler || p.Design == null || !byId.TryGetValue(p.Id, out BuildingsLot lot))
                {
                    continue;
                }
                float behind = lot.Depth - FrontageRules.Deep(p.Design);
                if (behind < FrontageRules.BackLeast)
                {
                    continue;
                }
                bool backed = false;
                for (int k = 0; k < pieces.Count && !backed; k++)
                {
                    backed = pieces[k].Filler && pieces[k].Back && Inside(lot, pieces[k].Ground.X, pieces[k].Ground.Z);
                }
                if (!backed)
                {
                    r.Yards++;
                    r.YardsShown += shown.Contains(i) ? 1 : 0;
                    r.YardArea += behind * lot.Width;
                    Note(r, p.Id, lot.X, lot.Z, behind.ToString("0.0", CultureInfo.InvariantCulture) + " m of its lot left bare behind it");
                }
            }
        }

        // True for a point on a lot's ground.
        private static bool Inside(BuildingsLot lot, float x, float z)
        {
            bool alongX = Math.Abs(Math.Cos(lot.Facing * Math.PI / 180.0)) > 0.5;
            float halfX = (alongX ? lot.Width : lot.Depth) * 0.5f, halfZ = (alongX ? lot.Depth : lot.Width) * 0.5f;
            return Math.Abs(x - lot.X) <= halfX && Math.Abs(z - lot.Z) <= halfZ;
        }

        private static void Land(IList<BuildingsLot> lots, Func<string, bool> built, LookReport r)
        {
            for (int i = 0; i < lots.Count; i++)
            {
                BuildingsLot lot = lots[i];
                if (BigPlacesRules.Holds(lot.Id))
                {
                    continue;
                }
                r.Lots++;
                r.Land += built != null && built(lot.Id) ? 0 : 1;
            }
        }

        // Along every strip, consecutive pieces of the front more than a sliver apart where the stretch between them lies inside lots that are built.
        private static void Gaps(IList<LookPiece> pieces, List<Box> boxes, IList<BuildingsLot> lots, Func<string, bool> built, LookReport r)
        {
            var groups = new Dictionary<long, List<Box>>(64);
            for (int i = 0; i < boxes.Count; i++)
            {
                LookPiece p = pieces[boxes[i].At];
                if (p.Back)
                {
                    continue;
                }
                // The strip's front line, which every piece of the front shares: the largest z of a lot facing north, the smallest facing south, and so on.
                float front = p.Ground.Facing < 1f || Math.Abs(p.Ground.Facing - 90f) < 1f ? boxes[i].Across1 : boxes[i].Across0;
                long key = (long)Math.Round(p.Ground.Facing) * 1000000L + (long)Math.Round(front * 2f);
                if (!groups.TryGetValue(key, out List<Box> list))
                {
                    list = new List<Box>(16);
                    groups.Add(key, list);
                }
                list.Add(boxes[i]);
            }
            foreach (List<Box> list in groups.Values)
            {
                list.Sort((a, b) => a.Along0.CompareTo(b.Along0));
                for (int i = 1; i < list.Count; i++)
                {
                    float gap = list[i].Along0 - list[i - 1].Along1;
                    if (gap <= FrontageRules.SliverMost + 0.01f || !Within(lots, built, list[i], list[i - 1].Along1, list[i].Along0))
                    {
                        continue;
                    }
                    r.Gaps++;
                    r.WidestGap = Math.Max(r.WidestGap, gap);
                    Note(r, pieces[list[i].At].Id, pieces[list[i].At].Ground.X, pieces[list[i].At].Ground.Z, gap.ToString("0.0", CultureInfo.InvariantCulture) + " m from the piece before it");
                }
            }
        }

        // True when every metre of the stretch from a to b along the box's strip lies on lots that have a building.
        private static bool Within(IList<BuildingsLot> lots, Func<string, bool> built, Box on, float a, float b)
        {
            float lotsBuilt = 0f;
            for (int i = 0; i < lots.Count; i++)
            {
                BuildingsLot l = lots[i];
                bool alongX = Math.Abs(Math.Cos(l.Facing * Math.PI / 180.0)) > 0.5;
                if (alongX != on.AlongX)
                {
                    continue;
                }
                float across = alongX ? l.Z : l.X;
                if (Math.Abs(across - (on.Across0 + on.Across1) * 0.5f) > l.Depth * 0.5f + 0.5f)
                {
                    continue;
                }
                float start = (alongX ? l.X : l.Z) - l.Width * 0.5f, end = start + l.Width;
                if (built != null && built(l.Id))
                {
                    lotsBuilt += Math.Max(0f, Math.Min(b, end) - Math.Max(a, start));
                }
            }
            return lotsBuilt >= b - a - 0.05f;
        }

        private static void Note(LookReport r, string id, float x, float z, string text)
        {
            r.FaultCount++;
            if (r.Faults.Count < Listed)
            {
                r.Faults.Add(new LookFault { Id = id, X = x, Z = z, Text = text });
            }
        }

        /// <summary>The faults of a report as one text, one to a line, for a log.</summary>
        public static string List(LookReport r)
        {
            var text = new StringBuilder(512);
            for (int i = 0; r != null && i < r.Faults.Count; i++)
            {
                text.Append(i == 0 ? "" : "; ").Append(r.Faults[i].Id).Append(": ").Append(r.Faults[i].Text);
            }
            return text.ToString();
        }
    }
}
