using System;
using System.Collections.Generic;
using RealLifeOverhaul.Uptown;

namespace RealLifeOverhaul.Schools
{
    /// <summary>What a piece of the campus is: the game's own model it is copied from, or the building sites' bundle's pitch.</summary>
    public enum CampusPieceKind
    {
        Fence = 0,     // the game's playground fence module (Industry City's), 1.4 m long
        Post = 1,      // its end post, at a gate
        Court = 2,     // the game's basketball court plane with its markings (Factory3's yard)
        Hoop = 3,      // the game's basketball hoop
        Bench = 4,     // the game's sittable bench (OutsideBenchController)
        Tree = 5,      // the game's street tree in its pavement hole
        Pitch = 6,     // the building sites' bundle's pitch: grass, lines and a track
        Oak = 7,       // the game's free trees of the Vegetation root (the parks take them too): broadleaf large, birch, broadleaf medium and maple
        Birch = 8,
        Beech = 9,
        Maple = 10,
        Bush = 11,     // the game's broadleaf bush: a planter's, and along the fence
        Bin = 12,      // the game's litter bin
        Rack = 13,     // the game's bicycle stand (the street furniture's docks)
        Table = 14,    // the game's table group of Madison Square Park: a round table and four chairs people sit on
        Climb = 15,    // the game's climbing frame of the Industry City playground, the one piece of its playground not drawn in a static batch
        Pavement = 16  // the ground the game's pavement material is taken from (a template only: never laid)
    }

    /// <summary>One piece stood on the campus: its kind, its middle on the ground, its turn and its stretch along its own x and z.</summary>
    public struct CampusPiece
    {
        public CampusPieceKind Kind;
        public float X, Z, Yaw;
        public float StretchX, StretchZ;
    }

    /// <summary>A straight run of fence between two gates or corners: the ground it carves out of the walking meshes.</summary>
    public struct CampusFence
    {
        public float X0, Z0, X1, Z1;
    }

    /// <summary>A building of the campus that is the city's look alone (no door of its own): the ground a design is fitted to.</summary>
    public struct CampusBlockPlot
    {
        public string Name;          // its name in the log: "the hall", "the junior block"
        public float X, Z;           // its plot's middle on the ground
        public int Yaw;              // the plot's turn, as CivicWorks.StartPlot takes one: its front at -Z turned by it
        public float Width, Depth;   // m along its street and back from it
    }

    /// <summary>A square of pavement the game's pedestrians gather in: at a gate for the school run.</summary>
    public struct CampusCrowd
    {
        public string Name;
        public float X, Z, Yaw, Width, Depth;
        public CrowdWhen When;     // the moments of the school day people are there (SchoolRules.CrowdMoments)
        public bool Inside;        // on the campus's own ground (its paths and lawns), not on the pavement outside a gate
    }

    /// <summary>
    /// Birchwood School's campus on the plan's west square (block 202: Birch to Elm Street, Uptown to Summit Avenue): where
    /// the main school's lot is, the hall and the junior block, the basketball court and the green between them, the playing
    /// field, the fence with its three gates, and the squares of pavement where the families gather. Everything comes from
    /// the plan's block and fixed figures, so the campus stands the same on every machine. Game-free (EconomyCheck's
    /// SchoolChecks). COST: lists filled once a load.
    /// </summary>
    public static partial class SchoolGrounds
    {
        public const int Row = LayoutRules.CampusBlock / 100;
        public const int Column = LayoutRules.CampusBlock % 100;

        public const string Street = "reallife:street_birchwoodgreen";   // the campus's own way, a street of the mod's own (NewBuildings' MadeStreets)
        public const int MainNumber = 1;                                   // the main school: 1 Birchwood Green
        public const string MainSize = "ba:buildingsize_k";                // a K1 office's footprint and look: the city's own designs of it
        public const int MainVersion = 1;
        public const string MainType = "ba:buildingtype_office";
        public const int MainStoreys = 3;                                  // a floor each for the juniors, the middle school and the seniors

        public const float FenceIn = 0.3f;        // m inside the block's edge the fence stands, off the pavement
        public const float FenceModule = 1.4f;    // m: the game's playground fence module, end to end (Industry City's, Build 3682)
        public const float FenceCut = 0.4f;       // m across the carving box along a run: the walker's way is shut, the pavement kept
        public const float MainWidth = 40f;       // m: the main school's lot along Uptown Avenue
        public const float MainDepth = 34f;       // m back from the block's edge
        public const float MainInset = 4f;        // m its front stands back from the block's edge: a forecourt inside the gate
        public const float GateWidth = 12f;       // m: the main gate on Uptown Avenue, before the main school's door
        public const float SideGate = 6f;         // m: the gate on Elm Street to the green, and the field's on Summit Avenue
        // The game's basketball court is a HALF court: its plane (SM_BasketballCourt, Factory3's yard and the Skatepark's two, which stand back to back) is 7.73 m
        // across and 5.13 m deep at the scale it is placed at (its mesh 13.64 by 8.52 along its own x and z, local scale 0.567 and 0.602, Build 3682), with the hoop
        // 0.6 m in from its baseline, at the middle of it. The campus stands two, back to back as the Skatepark does, each stretched evenly by CourtScale.
        public const float CourtScale = 1.55f;    // the game's half court, stretched evenly: 11.98 m across and 7.95 m deep, so two stand in the 12 m the courtyard gives the court
        public const float HalfCourtAcross = 7.73f;   // m: the game's half court, across (along its baseline), as the game places it
        public const float HalfCourtDeep = 5.13f;     // m: ... and from its baseline to its middle line
        public const float HoopToCentre = 1.96f;      // m from the game's hoop to the middle of the half court it stands at, along the way the hoop faces
        public const float HoopSide = 0.10f;          // m the court's middle lies to the hoop's right of the line it faces along (the game's own offset)
        public const float CourtWidth = 12f;          // m: the court across, the hoops' baselines (a half court's width at CourtScale: 11.98)
        public const float CourtLength = 15.9f;       // m: two half courts, baseline to baseline (2 x 7.95)
        public const float CourtFrom = 38.5f;         // m north of the block's south edge the court's south baseline is: 4.1 m of the courtyard behind the main school's back wall
        public const float CourtGap = 3.6f;           // m: the opening in the middle of the court fence's north baseline, to the plaza
        public const float PitchScale = 0.76f;    // the pitch prop stretched evenly (its lines are 46 by 30 m in the middle of it), so the field keeps its shape and its margins
        public const float PitchBaseWidth = 52f;  // the bundle's pitch's own size (LandmarkRules.PitchWidth, PitchDepth)
        public const float PitchBaseDepth = 48f;
        public const float PitchWidth = PitchBaseWidth * PitchScale;   // m: the playing field west to east (39.5) ...
        public const float PitchDepth = PitchBaseDepth * PitchScale;   // ... and south to north (36.5)
        public const float PitchNorth = 6f;       // m the playing field's north end keeps from the block's north edge (and the fence): a margin to walk and plant in
        public const float DoorBay = 1.55f;       // m the main school's front wall is behind its door (CampusLookRules.MainModel's DoorOut)
        public const int NetSides = 12;           // the hoops' net: a cone of twelve sides ...
        public const float NetTop = 1.30f;        // m: the hoop prefab's rim height in its own frame (its mesh: the rim ring is at 1.30, 0.54 out from the pole, Build 3682)
        public const float NetOut = 0.54f;        // m: the rim's middle in front of the pole, the way the hoop faces
        public const float NetTopRadius = 0.20f;  // m: ... as wide as the rim ...
        public const float NetFootRadius = 0.11f; // m: ... narrowing to this
        public const float NetLength = 0.36f;     // m: and this long
        public const float PlanterBack = 1.2f;    // m in front of the main school's front wall its four planters stand
        public const float PavementIn = 1.85f;    // m from a block's edge to the middle of the pavement round it (half a 3.7 m pavement)

        /// <summary>The campus's ground: the square's west, south, east and north edges (m).</summary>
        public static void Ground(out float x0, out float z0, out float x1, out float z1)
        {
            LayoutRules.Block(Row, Column, out x0, out z0, out x1, out z1);
        }

        /// <summary>The main school's lot: on Uptown Avenue in the middle of the block, its front a forecourt back from the gate.</summary>
        public static void MainLot(out float x, out float z, out float facing, out float width, out float depth, out float inset)
        {
            Ground(out float x0, out float z0, out float x1, out _);
            x = (x0 + x1) * 0.5f;
            z = z0 + MainDepth * 0.5f;
            facing = 180f;   // Uptown Avenue lies south of it
            width = MainWidth;
            depth = MainDepth;
            inset = MainInset;
        }

        /// <summary>The spot on Uptown Avenue's pavement before the main gate, and the way into the campus from it (degrees).</summary>
        public static void Gate(out float x, out float z, out float heading)
        {
            Ground(out float x0, out float z0, out float x1, out _);
            x = (x0 + x1) * 0.5f;
            z = z0 - PavementIn;
            heading = 0f;
        }

        /// <summary>The hall facing Birch Street and the junior block facing Elm Street, each beside the courtyard.</summary>
        public static void Blocks(List<CampusBlockPlot> into)
        {
            into.Clear();
            Ground(out float x0, out float z0, out float x1, out _);
            float middle = z0 + MainDepth + 2f + 15f;
            into.Add(new CampusBlockPlot { Name = "the hall", X = x0 + 12f, Z = middle, Yaw = 90, Width = 30f, Depth = 24f });
            into.Add(new CampusBlockPlot { Name = "the junior block", X = x1 - 12f, Z = middle, Yaw = 270, Width = 30f, Depth = 24f });
        }

        /// <summary>
        /// The one line for Player.log about a building of the campus that is the city's look alone, said once it is known:
        /// the design it wears, or why it was not made, or why the copy engine was never asked.
        /// </summary>
        public static string BlockLine(string name, bool asked, bool stands, string design, string why)
        {
            string reason = string.IsNullOrEmpty(why) ? "no reason given" : why;
            return !asked ? name + " was not asked of the copy engine: " + reason
                : stands ? name + " stands as the city's design " + (design ?? "?")
                : name + " was not made: " + reason;
        }

        /// <summary>
        /// The way between the hall's back and the junior block's back, where the court lies: its middle x and its half width (m). The
        /// models' fronts stand clear of the fence and their backs reach 24.4 m in from it (CampusLookRules.WallFromEdge, Depth).
        /// </summary>
        public static void Courtyard(out float x, out float halfWidth)
        {
            Ground(out float x0, out _, out float x1, out _);
            LookModel hall = CampusLookRules.Models[1], junior = CampusLookRules.Models[2];
            float west = x0 + CampusLookRules.WallFromEdge(hall) + hall.Depth;
            float east = x1 - CampusLookRules.WallFromEdge(junior) - junior.Depth;
            x = (west + east) * 0.5f;
            halfWidth = (east - west) * 0.5f;
        }

        // A run of fence from one end to the other, its modules laid end to end, a post at each end; its carving box kept.
        private static void Run(List<CampusPiece> into, List<CampusFence> runs, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            float length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (length < FenceModule)
            {
                return;
            }
            int modules = (int)Math.Floor(length / FenceModule);
            float ux = dx / length, uz = dz / length;
            // The game's module reaches back 1.4 m along its own -Z from where it stands (its mesh, Build 3682), so each stands at
            // its far end turned with +Z along the run: end to end from the run's start, with no gap and no overlap.
            float yaw = (float)(Math.Atan2(ux, uz) * 180.0 / Math.PI);
            yaw = (yaw % 360f + 360f) % 360f;
            float lead = (length - modules * FenceModule) * 0.5f;
            for (int i = 0; i < modules; i++)
            {
                float along = lead + (i + 1) * FenceModule;
                into.Add(new CampusPiece { Kind = CampusPieceKind.Fence, X = ax + ux * along, Z = az + uz * along, Yaw = yaw, StretchX = 1f, StretchZ = 1f });
            }
            into.Add(new CampusPiece { Kind = CampusPieceKind.Post, X = ax, Z = az, Yaw = yaw, StretchX = 1f, StretchZ = 1f });
            into.Add(new CampusPiece { Kind = CampusPieceKind.Post, X = bx, Z = bz, Yaw = yaw, StretchX = 1f, StretchZ = 1f });
            runs.Add(new CampusFence { X0 = ax, Z0 = az, X1 = bx, Z1 = bz });
        }

        private static void Tree(List<CampusPiece> into, float x, float z)
        {
            into.Add(new CampusPiece { Kind = CampusPieceKind.Tree, X = x, Z = z, Yaw = 0f, StretchX = 1f, StretchZ = 1f });
        }

        /// <summary>
        /// The squares of ground the game's pedestrians gather in, and when (<see cref="SchoolRules.CrowdMoments"/>): the pavements before the main gate on Uptown
        /// Avenue and the side gate on Elm Street for the school run (the game's pedestrians walk the pavements alone, never the campus's ground), then the
        /// campus's own: the forecourt before the main door, the courtyard's plaza, the court, the green's tables, the field and the two yards. The gates are
        /// the ways in and out: the walking meshes join the campus to the pavements there, and the fence is cut out of them everywhere else.
        /// </summary>
        public static void Crowds(List<CampusCrowd> into)
        {
            into.Clear();
            Ground(out float x0, out float z0, out float x1, out _);
            Courtyard(out float courtX, out _);
            float mid = (x0 + x1) * 0.5f;
            float sideGate = z0 + SideGateV;
            into.Add(new CampusCrowd { Name = "the main gate", X = mid, Z = z0 - PavementIn, Yaw = 0f, Width = GateWidth + 6f, Depth = 2.6f, When = CrowdWhen.Run | CrowdWhen.OpenDay });
            into.Add(new CampusCrowd { Name = "the side gate", X = x1 + PavementIn, Z = sideGate, Yaw = 90f, Width = SideGate + 6f, Depth = 2.6f, When = CrowdWhen.Run | CrowdWhen.OpenDay });
            into.Add(Square("the forecourt", mid, z0 + 2.4f, 22f, 2.2f, CrowdWhen.Run | CrowdWhen.OpenDay | CrowdWhen.Lunch));
            into.Add(Square("the plaza", courtX, z0 + 60.8f, 13f, 9f, CrowdWhen.Break | CrowdWhen.Lunch));
            into.Add(Square("the court", courtX, z0 + CourtFrom + CourtLength * 0.5f, 10.5f, 14f, CrowdWhen.Break | CrowdWhen.Lunch | CrowdWhen.After));
            into.Add(Square("the green", mid, z0 + 75.4f, 36f, 3f, CrowdWhen.Lunch));
            into.Add(Square("the field", mid, z0 + PitchMiddleV, PitchWidth - 6f, PitchDepth - 6f, CrowdWhen.After));
            into.Add(Square("the west yard", x0 + 11.3f, z0 + 17f, 5f, 18f, CrowdWhen.Break | CrowdWhen.Lunch));
            into.Add(Square("the east yard", x1 - 11.3f, z0 + 17f, 5f, 18f, CrowdWhen.Break | CrowdWhen.Lunch));
        }

        private static CampusCrowd Square(string name, float x, float z, float width, float depth, CrowdWhen when)
        {
            return new CampusCrowd { Name = name, X = x, Z = z, Yaw = 0f, Width = width, Depth = depth, When = when, Inside = true };
        }

        /// <summary>True when the spot is on the campus's own ground (inside the fence's line).</summary>
        public static bool On(float x, float z)
        {
            Ground(out float x0, out float z0, out float x1, out float z1);
            return x >= x0 && x <= x1 && z >= z0 && z <= z1;
        }
    }
}
