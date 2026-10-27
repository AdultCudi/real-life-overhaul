using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A rectangle on a site's ground, in the site's own axes (metres from its middle, x along the street, z from the street's side to the back).</summary>
    public struct ParkRect
    {
        public float X0, Z0, X1, Z1;

        public ParkRect(float x0, float z0, float x1, float z1)
        {
            X0 = Math.Min(x0, x1);
            X1 = Math.Max(x0, x1);
            Z0 = Math.Min(z0, z1);
            Z1 = Math.Max(z0, z1);
        }

        public float Width => X1 - X0;
        public float Length => Z1 - Z0;
        public float MidX => (X0 + X1) * 0.5f;
        public float MidZ => (Z0 + Z1) * 0.5f;
        public float Area => Width * Length;
    }

    /// <summary>A thing stood on a site's ground: its place and the way its front looks (degrees about the vertical, 0 along +z).</summary>
    public struct ParkPoint
    {
        public float X, Z, Yaw;

        public ParkPoint(float x, float z, float yaw)
        {
            X = x;
            Z = z;
            Yaw = yaw;
        }
    }

    /// <summary>One row of bays side by side: a lane of the game's parking, the box its cars stand in.</summary>
    public struct ParkRow
    {
        public float X, Z;      // the middle of the row's box
        public bool Along;      // the row runs along z (its bays side by side down the lot); false: along x
        public float Length;    // m: the bays side by side
        public int Bays;        // bays in the row
        public bool Booth;      // the booth stands in the bay at the row's end by the way in, which the row's box leaves out
        public float Yaw;       // the way a car noses in, away from the aisle the row's bays open on (degrees about the vertical, 0 along +z)
        public bool NearHigh;   // the end of the row by the way in is its high end (in x for a row along x; a row along z has it at its low end)

        /// <summary>The row's box in the site's axes.</summary>
        public ParkRect Box
        {
            get
            {
                float half = Length * 0.5f, depth = CarParkRules.BayDepth * 0.5f;
                return Along ? new ParkRect(X - depth, Z - half, X + depth, Z + half) : new ParkRect(X - half, Z - depth, X + half, Z + depth);
            }
        }
    }

    /// <summary>
    /// The plan of a car park on a site (docs/features/construction.md, "Parking lots"): the bays in rows 2.5 by 5 m, the aisles
    /// between them, the connector from the mouth to the aisles, the mouth in the street's side, the lines, the kerbs, the lamps, the
    /// booth and the barrier. Aisles run along the street (<see cref="Across"/>) or across it, whichever holds more bays. Made of
    /// the site's whole metres alone, so it is the same on every machine; game-free.
    /// </summary>
    public sealed class CarParkLayout
    {
        public int Width, Depth;                    // the site, whole metres
        public bool Across;                         // the aisles run along the street (x); false: from the street back (z)
        public bool MouthHigh;                      // the mouth at the high-x end of the frontage
        public int Code;                            // the way it was made, 1 to 4 (CarParkRules.Codes): what a project and a car park keep to lay it out again
        public bool Ok;
        public string Why = string.Empty;           // English, for the log: why there is no plan
        public int Bays;                            // parking bays, the booth's bay not counted
        public int Accessible;                      // bays marked for the disabled (a blue square each)
        public ParkRect Connector;                  // the strip from the mouth to the aisles
        public ParkRect Mouth;                      // the gap in the street's kerb, on the site's front line
        public ParkPoint Booth;                     // the booth, in the bay beside the throat
        public readonly List<ParkRow> Rows = new List<ParkRow>(8);
        public readonly List<ParkRect> Aisles = new List<ParkRect>(4);
        public readonly List<ParkRect> Lines = new List<ParkRect>(96);     // the bays' dividers
        public readonly List<ParkRect> Dashes = new List<ParkRect>(48);    // the aisles' and the connector's middle dashes
        public readonly List<ParkRect> Blue = new List<ParkRect>(4);       // the bays for the disabled
        public readonly List<ParkRect> Kerbs = new List<ParkRect>(8);      // the low kerb round the site, broken at the mouth
        public readonly List<ParkPoint> Barriers = new List<ParkPoint>(2);
        public readonly List<ParkPoint> Lamps = new List<ParkPoint>(8);

        /// <summary>The metres of surface laid: the whole site.</summary>
        public int Area => Width * Depth;

        /// <summary>The apron over the pavement: the mouth's width, reaching out <paramref name="pavement"/> m from the site's front line.</summary>
        public ParkRect Apron(float pavement)
        {
            return new ParkRect(Mouth.X0, -Depth * 0.5f - pavement, Mouth.X1, -Depth * 0.5f);
        }
    }

    public static partial class CarParkRules
    {
        // ------------------------------------------------------------------ the layout

        /// <summary>
        /// The plan of a car park on a site of this many whole metres, the mouth at one end of the frontage: the better of the two ways
        /// the aisles can run (along the street, or across it), by the bays each holds; the first when they tie. Not Ok, with why, when
        /// the site cannot hold a row of three bays and an aisle.
        /// </summary>
        public static CarParkLayout Layout(int width, int depth, bool mouthHigh)
        {
            CarParkLayout across = Build(width, depth, true, mouthHigh);
            CarParkLayout along = Build(width, depth, false, mouthHigh);
            if (!across.Ok && !along.Ok)
            {
                return across.Why.Length >= along.Why.Length ? across : along;
            }
            if (!along.Ok)
            {
                return across;
            }
            return !across.Ok || along.Bays > across.Bays ? along : across;
        }

        /// <summary>
        /// The way a plan is made as a number: 1 aisles along the street with the mouth at the low end, 2 at the high end, 3 aisles from the
        /// street back with it at the low end, 4 at the high end.
        /// </summary>
        public static int Codes(bool across, bool mouthHigh) => (across ? 1 : 3) + (mouthHigh ? 1 : 0);

        /// <summary>The plan made the way a code says (1 to 4); not Ok for a code that is none of them.</summary>
        public static CarParkLayout LayoutOf(int width, int depth, int code)
        {
            if (code < 1 || code > 4)
            {
                return new CarParkLayout { Width = width, Depth = depth, Why = "no such way to lay it out" };
            }
            return Build(width, depth, code < 3, code % 2 == 0);
        }

        /// <summary>
        /// The four ways a site can be laid out (the aisles along the street or from it back, the mouth at either end of the frontage)
        /// that hold a car park, the one with most bays first (the first made first when they tie), into <paramref name="into"/> (cleared).
        /// </summary>
        public static void Candidates(int width, int depth, List<CarParkLayout> into)
        {
            into.Clear();
            for (int code = 1; code <= 4; code++)
            {
                CarParkLayout l = LayoutOf(width, depth, code);
                if (!l.Ok)
                {
                    continue;
                }
                int at = into.Count;
                while (at > 0 && into[at - 1].Bays < l.Bays)
                {
                    at--;
                }
                into.Insert(at, l);
            }
        }

        /// <summary>The bays the site holds with the mouth at either end of its frontage: the better of the two, 0 when it holds none.</summary>
        public static int BaysOf(int width, int depth)
        {
            CarParkLayout low = Layout(width, depth, false);
            CarParkLayout high = Layout(width, depth, true);
            return Math.Max(low.Ok ? low.Bays : 0, high.Ok ? high.Bays : 0);
        }

        // One orientation, in (u, v): u runs the way the aisles run, v across them; the connector is the strip at u from the edge to
        // the edge and the connector's width, the whole length of v; the rows and aisles stack along v after it, in u from there on.
        private static CarParkLayout Build(int width, int depth, bool across, bool mouthHigh)
        {
            var plan = new CarParkLayout { Width = width, Depth = depth, Across = across, MouthHigh = mouthHigh, Code = Codes(across, mouthHigh) };
            float uTotal = across ? width : depth, vTotal = across ? depth : width;
            float u0 = Edge, u1 = uTotal - Edge, v0 = Edge, v1 = vTotal - Edge;
            float lanes = u0 + ConnectorWidth;
            int perRow = (int)Math.Floor((u1 - lanes) / BayWidth + 1e-4);
            if (perRow < 3)
            {
                plan.Why = "the site is " + width + " by " + depth + " m: too short for a row of three bays beside the way in";
                return plan;
            }
            float rowEnd = lanes + perRow * BayWidth;
            var stack = new List<Band>(8);
            Stack(v1 - v0, stack);
            if (stack.Count == 0)
            {
                plan.Why = "the site is " + width + " by " + depth + " m: too shallow for an aisle and a row of bays";
                return plan;
            }
            // The connector, and the mouth at the street's end of it.
            plan.Connector = Map(plan, u0, v0, lanes, v1);
            plan.Mouth = across ? Map(plan, u0, v0 - Edge, lanes, v0) : Map(plan, u0 - Edge, v0, u0, v0 + ConnectorWidth);
            float at = v0;
            bool first = true;
            for (int i = 0; i < stack.Count; i++)
            {
                Band band = stack[i];
                float b0 = at, b1 = at + band.Size;
                at = b1;
                if (band.Aisle)
                {
                    plan.Aisles.Add(Map(plan, lanes, b0, rowEnd, b1));
                    DashAlong(plan, lanes + 1f, rowEnd - 0.5f, (b0 + b1) * 0.5f, true);
                    LampsAlong(plan, lanes, rowEnd, b0);
                    continue;
                }
                // The first row's first bay, beside the throat, is the booth's: the row's box and its lines begin after it.
                int skipped = first ? 1 : 0;
                float from = lanes + skipped * BayWidth;
                ParkRect box = Map(plan, from, b0, rowEnd, b1);
                bool aisleAfter = i + 1 < stack.Count && stack[i + 1].Aisle;
                float nose = (across ? 0f : mouthHigh ? 270f : 90f) + (aisleAfter ? 180f : 0f);   // away from the aisle: the way +v points, turned about when the aisle lies that way
                var row = new ParkRow
                {
                    Bays = perRow - skipped, Length = (perRow - skipped) * BayWidth, X = box.MidX, Z = box.MidZ, Along = !across, Booth = first, Yaw = nose % 360f, NearHigh = across && mouthHigh
                };
                plan.Rows.Add(row);
                plan.Bays += row.Bays;
                for (int k = skipped; k <= perRow; k++)
                {
                    float line = lanes + k * BayWidth;
                    plan.Lines.Add(Map(plan, line - LineWidth * 0.5f, b0, line + LineWidth * 0.5f, b1));
                }
                if (first)
                {
                    ParkRect bay = Map(plan, lanes, b0, lanes + BayWidth, b1);
                    // The booth faces the throat: toward the connector, which lies the other way from the aisles' length.
                    float yaw = across ? (mouthHigh ? 90f : 270f) : 180f;
                    plan.Booth = new ParkPoint(bay.MidX, bay.MidZ, yaw);
                }
                first = false;
            }
            DashAlong(plan, v0 + 1.5f, v1 - 0.5f, u0 + ConnectorWidth * 0.5f, false);
            // The barrier's posts flank the throat, a little way in from the street.
            if (across)
            {
                plan.Barriers.Add(PointAt(plan, u0 + 0.45f, v0 + 6f, 0f));
                plan.Barriers.Add(PointAt(plan, lanes - 0.45f, v0 + 6f, 0f));
            }
            else
            {
                plan.Barriers.Add(PointAt(plan, u0 + 6f, v0 + 0.45f, 0f));
                plan.Barriers.Add(PointAt(plan, u0 + 6f, v0 + ConnectorWidth - 0.45f, 0f));
            }
            // The bays for the disabled: a blue square for every twenty, in the first row's bays beside the way in (counted from its end by the throat).
            ParkRow near = plan.Rows[0];
            ParkRect nearBox = near.Box;
            plan.Accessible = Math.Max(1, Math.Min(near.Bays, plan.Bays / AccessibleEvery));
            for (int k = 0; k < plan.Accessible; k++)
            {
                float along = near.NearHigh ? nearBox.X1 - (k + 0.5f) * BayWidth : (near.Along ? nearBox.Z0 : nearBox.X0) + (k + 0.5f) * BayWidth;
                float m = BlueSide * 0.5f;
                plan.Blue.Add(near.Along ? new ParkRect(nearBox.MidX - m, along - m, nearBox.MidX + m, along + m)
                                         : new ParkRect(along - m, nearBox.MidZ - m, along + m, nearBox.MidZ + m));
            }
            Kerb(plan);
            plan.Ok = true;
            return plan;
        }

        private struct Band
        {
            public bool Aisle;
            public float Size;
        }

        // The rows and aisles in order along v: a row, an aisle, two rows, an aisle... a row, as many modules of 16 m as fit, and where
        // 11 m are left over one more aisle and row; a site with room for one aisle and one row has just that.
        private static void Stack(float room, List<Band> into)
        {
            int modules = (int)Math.Floor(room / ModuleDepth + 1e-4);
            if (modules >= 1)
            {
                into.Add(new Band { Size = BayDepth });
                for (int m = 0; m < modules; m++)
                {
                    into.Add(new Band { Aisle = true, Size = AisleWidth });
                    into.Add(new Band { Size = BayDepth });
                    if (m < modules - 1)
                    {
                        into.Add(new Band { Size = BayDepth });
                    }
                }
                if (room - modules * ModuleDepth >= AisleWidth + BayDepth - 1e-4)
                {
                    into.Add(new Band { Aisle = true, Size = AisleWidth });
                    into.Add(new Band { Size = BayDepth });
                }
                return;
            }
            if (room >= AisleWidth + BayDepth - 1e-4)
            {
                into.Add(new Band { Size = BayDepth });
                into.Add(new Band { Aisle = true, Size = AisleWidth });
            }
        }

        // A rectangle in (u, v) as a rectangle of the site: u along the street (across) or from the street back, the way in at the low or the high end.
        private static ParkRect Map(CarParkLayout plan, float u0, float v0, float u1, float v1)
        {
            ParkPoint a = PointAt(plan, u0, v0, 0f), b = PointAt(plan, u1, v1, 0f);
            return new ParkRect(a.X, a.Z, b.X, b.Z);
        }

        private static ParkPoint PointAt(CarParkLayout plan, float u, float v, float yaw)
        {
            float x, z;
            if (plan.Across)
            {
                x = plan.MouthHigh ? plan.Width * 0.5f - u : -plan.Width * 0.5f + u;
                z = -plan.Depth * 0.5f + v;
            }
            else
            {
                x = plan.MouthHigh ? plan.Width * 0.5f - v : -plan.Width * 0.5f + v;
                z = -plan.Depth * 0.5f + u;
            }
            return new ParkPoint(x, z, yaw);
        }

        // Dashes down the middle of a strip: along u at a given v (an aisle), or along v at a given u (the connector).
        private static void DashAlong(CarParkLayout plan, float from, float to, float fixedAt, bool alongU)
        {
            for (float at = from; at + DashLength <= to; at += DashLength * 2f)
            {
                plan.Dashes.Add(alongU ? Map(plan, at, fixedAt - DashWidth * 0.5f, at + DashLength, fixedAt + DashWidth * 0.5f)
                                       : Map(plan, fixedAt - DashWidth * 0.5f, at, fixedAt + DashWidth * 0.5f, at + DashLength));
            }
        }

        // A lamp on the divider nearest each LampEvery of an aisle's length, on the aisle's edge.
        private static void LampsAlong(CarParkLayout plan, float lanes, float rowEnd, float aisleEdge)
        {
            for (float at = lanes + LampEvery * 0.5f; at < rowEnd - 1f; at += LampEvery)
            {
                float snapped = lanes + (float)Math.Round((at - lanes) / BayWidth) * BayWidth;
                plan.Lamps.Add(PointAt(plan, snapped, aisleEdge - 0.12f, 0f));
            }
        }

        // The kerb round the site: its three other sides at the edge, the street's side broken at the mouth.
        private static void Kerb(CarParkLayout plan)
        {
            float hx = plan.Width * 0.5f, hz = plan.Depth * 0.5f, k = KerbWidth;
            plan.Kerbs.Add(new ParkRect(-hx, hz - k, hx, hz));                 // the back
            plan.Kerbs.Add(new ParkRect(-hx, -hz, -hx + k, hz));               // the low-x side
            plan.Kerbs.Add(new ParkRect(hx - k, -hz, hx, hz));                 // the high-x side
            ParkRect gap = plan.Mouth;
            if (gap.X0 > -hx + k + 0.01f)
            {
                plan.Kerbs.Add(new ParkRect(-hx + k, -hz, gap.X0, -hz + k));
            }
            if (gap.X1 < hx - k - 0.01f)
            {
                plan.Kerbs.Add(new ParkRect(gap.X1, -hz, hx - k, -hz + k));
            }
        }
    }
}
