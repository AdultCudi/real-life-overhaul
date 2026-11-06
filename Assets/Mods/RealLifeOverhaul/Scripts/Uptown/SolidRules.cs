using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewGround;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// What a car can run into in the districts where nothing is drawn, game-free (docs/features/uptown.md, "The edges"; the beta's
    /// "crashing into nothing while driving and dying", 27 Oct 2026). The mod stands two kinds of solid with no mesh of their own:
    /// the invisible walls round the districts (<see cref="LayoutPlans.NewWalls"/>, the game's own style, behind the ring's buildings
    /// and the backdrop's) and the boxes that close the street ends (<see cref="EdgeRules.Ends"/>). The rules: the first solid a car
    /// meets driving out of the band along any street or avenue is a barricade's box, whose first face is its fencing's drawn face
    /// (<see cref="FenceGap"/>); and no part of any invisible solid overlaps a road, a lane or the surface a car drives on, or touches
    /// one except behind a barricade's fencing. A new kind of solid with no mesh is listed in <see cref="Sites"/>, or the source check
    /// fails. COST: made once a load and by the checks; nothing per frame.
    /// </summary>
    public static class SolidRules
    {
        public const int Across = 7;               // rays across an end's width (the middle and both sides)
        public const float Inside = 1f;            // m inside the band a ray starts
        public const float Touch = 0.3f;           // m: an invisible solid this near a road's surface is the road's end at it
        public const float FenceMost = 0.15f;      // m: the most a box's first face may stand before its fencing's drawn face

        /// <summary>
        /// Every place in the mod's scripts that makes a collider, with how many and why it is not a solid with no mesh on a road: a
        /// trigger, a body drawn with it, a surface, or one of the two listed in the rules above. NavCheck reads the sources and fails on
        /// a file that makes more colliders than it is listed for, or one that is not listed (UptownSolidCheck).
        /// </summary>
        public static readonly string[] Sites =
        {
            "Careers/DeskPointer.cs|1|a trigger for the pointer",
            "Civic/SiteKit.cs|2|the posts and face of a site's fencing, each on the mesh it stands for",
            "Construction/BuilderDeskPointer.cs|1|a trigger for the pointer",
            "Construction/CarParkGame.cs|2|the car park's drawn pieces, and a box for a lamp or a machine drawn with it",
            "Construction/CarParkPointer.cs|1|a trigger for the pointer",
            "Corners/CornerLook.cs|1|a corner building's bodies, drawn",
            "Garage/BikeMount.cs|1|the bars of the player's bike, drawn",
            "Landmarks/LandmarksGame.cs|1|a landmark's solid pieces, each a drawn model or a plain shape",
            "Law/PrecinctFrontage.Kerb.cs|1|the police car at the station's kerb, drawn",
            "Ledger/LedgerPointer.cs|1|a trigger for the pointer",
            "NewGround/PeopleGame.cs|1|a crossing's trigger",
            "People/PolicePresence.cs|1|the police car, drawn",
            "Ragdoll/RagdollRig.cs|3|a falling body's bones, drawn with it",
            "Schools/CampusLook.cs|1|the campus buildings' bodies, drawn",
            "Traffic/Drivers/DriverGame.cs|1|a hold line, a trigger",
            "Traffic/Solid/SolidGame.cs|1|a person ahead, a trigger",
            "Traffic/Tow/TowTruck.cs|1|the tow truck, drawn",
            "Transport/BusVehicle.cs|1|the bus, drawn",
            "Transport/TransitPointer.cs|1|a trigger for the pointer",
            "Uptown/LifeGame.Doors.cs|1|the exit probe's stand-in for a car's body, switched off and destroyed in the same call",
            "Uptown/GroundGame.cs|1|the invisible walls (rules above), the ground and the road surfaces",
            "Uptown/GroundGame.Edges.cs|1|a barricade's box, its first face its fencing's drawn face",
            "Uptown/ParksGame.cs|1|a fountain's basin, sized to the drawn fountain"
        };

        /// <summary>
        /// What a car driving out of the band along the way out of each end first meets, across the end's width: null when it is a
        /// barricade's box on every ray, else words for the first end where it is an invisible wall (or nothing drawn).
        /// </summary>
        public static string FirstIsFencing(List<Barricade> ends, List<PlanBox> walls)
        {
            for (int e = 0; e < ends.Count; e++)
            {
                Barricade b = ends[e];
                float acrossX = -b.OutZ, acrossZ = b.OutX;
                for (int s = 0; s < Across; s++)
                {
                    float offset = (s - (Across - 1) * 0.5f) / ((Across - 1) * 0.5f) * b.Half * 0.9f;
                    float px = b.Cx + acrossX * offset - b.OutX * Inside, pz = b.Cz + acrossZ * offset - b.OutZ * Inside;
                    float box = float.PositiveInfinity, wall = float.PositiveInfinity;
                    bool inWall = false;
                    for (int i = 0; i < ends.Count; i++)
                    {
                        float t = Hit(px, pz, b.OutX, b.OutZ, ends[i].Wall);
                        box = Math.Min(box, t < 0f ? 0f : t);
                    }
                    for (int w = 0; w < walls.Count; w++)
                    {
                        float t = Hit(px, pz, b.OutX, b.OutZ, walls[w]);
                        inWall |= t < 0f;
                        wall = Math.Min(wall, t < 0f ? 0f : t);
                    }
                    if (inWall)
                    {
                        continue;   // a point inside a wall (the pavement's back edge against 1st Avenue's south wall): no car is there
                    }
                    if (!(box < wall))
                    {
                        return b.Name + ": a car driving out of the band there meets " + (float.IsInfinity(wall) ? "nothing" : "an invisible wall") + " "
                               + Fixed(float.IsInfinity(wall) ? float.PositiveInfinity : wall - Inside) + " m past the edge before any fencing";
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// How far a barricade's box stands before its fencing's drawn face (m): the box's first face is at <c>Fence - FenceFace - BoxAhead</c> past the
        /// edge and the panels' face at <c>Fence - FenceFace</c>, read from the pieces themselves. Zero or the little the rules leave.
        /// </summary>
        public static float FenceGap(Barricade end)
        {
            var pieces = new List<PiecePlace>(24);
            EdgeRules.Pieces(end, pieces);
            float face = float.PositiveInfinity;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Kind == PieceKind.Hoarding)
                {
                    face = Math.Min(face, (pieces[i].X - end.Cx) * end.OutX + (pieces[i].Z - end.Cz) * end.OutZ - EdgeRules.FenceFace);
                }
            }
            float front = float.PositiveInfinity;
            foreach (float x in new[] { end.Wall.X - end.Wall.HalfX, end.Wall.X + end.Wall.HalfX })
            {
                foreach (float z in new[] { end.Wall.Z - end.Wall.HalfZ, end.Wall.Z + end.Wall.HalfZ })
                {
                    front = Math.Min(front, (x - end.Cx) * end.OutX + (z - end.Cz) * end.OutZ);
                }
            }
            return face - front;
        }

        /// <summary>
        /// The first road, lane or ground a car drives on that an invisible solid overlaps or touches (within <see cref="Touch"/>) with no
        /// barricade's fencing in front of it, in words; null when none does. The invisible solids are the walls and the part of each
        /// barricade's box behind its fencing's back; the surfaces are the road colliders and the plan's lanes.
        /// </summary>
        public static string OnTheRoad(List<Barricade> ends, List<PlanBox> walls, List<PlanBox> roads, RoadPlan lanes)
        {
            var hidden = new List<PlanBox>(walls.Count + ends.Count);
            var names = new List<string>(walls.Count + ends.Count);
            for (int w = 0; w < walls.Count; w++)
            {
                hidden.Add(walls[w]);
                names.Add("the invisible wall " + w);
            }
            for (int e = 0; e < ends.Count; e++)
            {
                hidden.Add(EdgeRules.Behind(ends[e]));
                names.Add("the box behind the fencing at " + ends[e].Name);
            }
            for (int h = 0; h < hidden.Count; h++)
            {
                for (int r = 0; r < roads.Count; r++)
                {
                    float gap = Gap(hidden[h], roads[r]);
                    if (gap < -1e-4f)
                    {
                        return names[h] + " overlaps a road surface";
                    }
                    if (gap < Touch && !Shielded(roads[r], hidden[h], ends, walls))
                    {
                        return names[h] + " stands " + Fixed(Math.Max(gap, 0f)) + " m from the end of a road surface with no fencing in front of it";
                    }
                }
                for (int l = 0; l < lanes.Roads.Count; l++)
                {
                    if (EdgeRules.LaneTouches(lanes.Roads[l], hidden[h]))
                    {
                        return names[h] + " comes within a lane's width of road " + l + " of the plan";
                    }
                }
            }
            return null;
        }

        // True when a barricade's fencing stands between a road surface and an invisible solid that touches it: the road runs to an end's edge, the
        // fencing spans the road's whole width there and stands in front of any wall at the edge, and the solid lies in that end's way out.
        private static bool Shielded(PlanBox road, PlanBox hidden, List<Barricade> ends, List<PlanBox> walls)
        {
            for (int e = 0; e < ends.Count; e++)
            {
                Barricade b = ends[e];
                float across = b.Avenue ? road.Z : road.X, acrossHalf = b.Avenue ? road.HalfZ : road.HalfX, mid = b.Avenue ? b.Cz : b.Cx;
                if (Math.Abs(across - mid) + acrossHalf > b.Half + 1e-3f)
                {
                    continue;   // the fencing does not span the road's width here
                }
                float reach = float.NegativeInfinity;   // how far past the edge the road's surface reaches (negative: short of it)
                for (int c = 0; c < 4; c++)
                {
                    float x = road.X + ((c & 1) == 0 ? -road.HalfX : road.HalfX), z = road.Z + ((c & 2) == 0 ? -road.HalfZ : road.HalfZ);
                    reach = Math.Max(reach, (x - b.Cx) * b.OutX + (z - b.Cz) * b.OutZ);
                }
                float from = -Inside, to = EdgeRules.Reach;
                PlanBox way = b.Avenue
                    ? new PlanBox(b.Cx + b.OutX * from, b.Cz - b.Half, b.Cx + b.OutX * to, b.Cz + b.Half, 0f)
                    : new PlanBox(b.Cx - b.Half, b.Cz + b.OutZ * from, b.Cx + b.Half, b.Cz + b.OutZ * to, 0f);
                float face = EdgeRules.WallFace(b.Cx, b.Cz, b.OutX, b.OutZ, b.Half, walls);
                if (reach >= -Touch && Gap(way, hidden) < 0.01f && b.Fence + EdgeRules.FenceBack <= face + 1e-3f)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The gap between two boxes on the ground (m); below zero when they overlap.</summary>
        public static float Gap(PlanBox a, PlanBox b)
        {
            float gx = Math.Abs(a.X - b.X) - (a.HalfX + b.HalfX), gz = Math.Abs(a.Z - b.Z) - (a.HalfZ + b.HalfZ);
            if (gx < 0f && gz < 0f)
            {
                return Math.Max(gx, gz);
            }
            return (float)Math.Sqrt(Math.Max(gx, 0f) * Math.Max(gx, 0f) + Math.Max(gz, 0f) * Math.Max(gz, 0f));
        }

        // The distance along a ray (px, pz) + t (dx, dz) at which it first meets a box, or infinity; below zero when it starts inside.
        private static float Hit(float px, float pz, float dx, float dz, PlanBox box)
        {
            float t0 = float.NegativeInfinity, t1 = float.PositiveInfinity;
            if (!Slab(px, dx, box.X - box.HalfX, box.X + box.HalfX, ref t0, ref t1) || !Slab(pz, dz, box.Z - box.HalfZ, box.Z + box.HalfZ, ref t0, ref t1) || t1 < 0f)
            {
                return float.PositiveInfinity;
            }
            return t0 < 0f ? -1f : t0;
        }

        private static bool Slab(float p, float d, float lo, float hi, ref float t0, ref float t1)
        {
            if (Math.Abs(d) < 1e-6f)
            {
                return p >= lo && p <= hi;
            }
            float a = (lo - p) / d, b = (hi - p) / d;
            t0 = Math.Max(t0, Math.Min(a, b));
            t1 = Math.Min(t1, Math.Max(a, b));
            return t0 <= t1;
        }

        private static string Fixed(float value)
        {
            return value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
