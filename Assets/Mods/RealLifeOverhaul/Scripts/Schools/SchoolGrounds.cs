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
        Pitch = 6      // the building sites' bundle's pitch: grass, lines and a track
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
    }

    /// <summary>
    /// Birchwood School's campus on the plan's west square (block 202: Birch to Elm Street, Uptown to Summit Avenue): where
    /// the main school's lot is, the hall and the junior block, the basketball court and the green between them, the playing
    /// field, the fence with its three gates, and the squares of pavement where the families gather. Everything comes from
    /// the plan's block and fixed figures, so the campus stands the same on every machine. Game-free (EconomyCheck's
    /// SchoolChecks). COST: lists filled once a load.
    /// </summary>
    public static class SchoolGrounds
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
        public const float CourtLength = 24f;     // m: the court as stood, its long side running north
        public const float CourtWidth = 15f;
        public const float CourtBaseLength = 13.64f;   // m: the game's court plane's own size (its mesh, Build 3682)
        public const float CourtBaseWidth = 8.52f;
        public const float PitchWidth = 52f;      // m: the playing field west to east ...
        public const float PitchDepth = 40f;      // ... and south to north
        public const float PitchBaseWidth = 52f;  // the bundle's pitch's own size (LandmarkRules.PitchWidth, PitchDepth)
        public const float PitchBaseDepth = 48f;
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
        /// Every piece stood on the campus (into the list, cleared): the fence round the block in modules, broken by the main
        /// gate, the side gate and the field's gate, a post at each side of a gate; the court and its hoops in the courtyard;
        /// trees and benches round it; the playing field at the north end.
        /// </summary>
        public static void Pieces(List<CampusPiece> into, List<CampusFence> runs)
        {
            into.Clear();
            runs.Clear();
            Ground(out float x0, out float z0, out float x1, out float z1);
            float mid = (x0 + x1) * 0.5f;
            float s = z0 + FenceIn, n = z1 - FenceIn, w = x0 + FenceIn, e = x1 - FenceIn;
            float sideGate = z0 + MainDepth + 2f + 30f + 3f;   // between the junior block and the field
            // South, on Uptown Avenue: the main gate before the main school's door.
            Run(into, runs, w, s, mid - GateWidth * 0.5f, s);
            Run(into, runs, mid + GateWidth * 0.5f, s, e, s);
            // East, on Elm Street: the side gate to the green.
            Run(into, runs, e, s, e, sideGate - SideGate * 0.5f);
            Run(into, runs, e, sideGate + SideGate * 0.5f, e, n);
            // North, on Summit Avenue: the field's gate.
            Run(into, runs, e, n, mid + SideGate * 0.5f, n);
            Run(into, runs, mid - SideGate * 0.5f, n, w, n);
            // West, on Birch Street: whole.
            Run(into, runs, w, n, w, s);
            // The courtyard between the hall and the junior block: the court running north, a hoop at each end.
            float courtZ = z0 + MainDepth + 2f + CourtLength * 0.5f + 3f;
            into.Add(new CampusPiece { Kind = CampusPieceKind.Court, X = mid, Z = courtZ, Yaw = 90f, StretchX = CourtLength / CourtBaseLength, StretchZ = CourtWidth / CourtBaseWidth });
            into.Add(new CampusPiece { Kind = CampusPieceKind.Hoop, X = mid, Z = courtZ - CourtLength * 0.5f + 0.6f, Yaw = 0f, StretchX = 1f, StretchZ = 1f });
            into.Add(new CampusPiece { Kind = CampusPieceKind.Hoop, X = mid, Z = courtZ + CourtLength * 0.5f - 0.6f, Yaw = 180f, StretchX = 1f, StretchZ = 1f });
            // The green round it: trees at its corners, benches along its sides facing the court.
            float greenW = mid - CourtWidth * 0.5f - 3f, greenE = mid + CourtWidth * 0.5f + 3f;
            float greenS = courtZ - CourtLength * 0.5f, greenN = courtZ + CourtLength * 0.5f + 5f;
            Tree(into, greenW, greenS);
            Tree(into, greenE, greenS);
            Tree(into, greenW, greenN);
            Tree(into, greenE, greenN);
            into.Add(new CampusPiece { Kind = CampusPieceKind.Bench, X = greenW, Z = courtZ, Yaw = 90f, StretchX = 1f, StretchZ = 1f });
            into.Add(new CampusPiece { Kind = CampusPieceKind.Bench, X = greenE, Z = courtZ, Yaw = 270f, StretchX = 1f, StretchZ = 1f });
            into.Add(new CampusPiece { Kind = CampusPieceKind.Bench, X = mid - 4f, Z = greenN, Yaw = 180f, StretchX = 1f, StretchZ = 1f });
            into.Add(new CampusPiece { Kind = CampusPieceKind.Bench, X = mid + 4f, Z = greenN, Yaw = 180f, StretchX = 1f, StretchZ = 1f });
            // The playing field at the north end, behind the side gate, before the field's gate.
            into.Add(new CampusPiece
            {
                Kind = CampusPieceKind.Pitch, X = mid, Z = z1 - 2f - PitchDepth * 0.5f, Yaw = 0f, StretchX = PitchWidth / PitchBaseWidth, StretchZ = PitchDepth / PitchBaseDepth
            });
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
        /// The pavements where the families wait: before the main gate on Uptown Avenue, and before the side gate on Elm
        /// Street (the game's pedestrians walk the pavements alone, never the campus's ground).
        /// </summary>
        public static void Crowds(List<CampusCrowd> into)
        {
            into.Clear();
            Ground(out float x0, out float z0, out float x1, out _);
            float mid = (x0 + x1) * 0.5f;
            float sideGate = z0 + MainDepth + 2f + 30f + 3f;
            into.Add(new CampusCrowd { Name = "the main gate", X = mid, Z = z0 - PavementIn, Yaw = 0f, Width = GateWidth + 6f, Depth = 2.6f });
            into.Add(new CampusCrowd { Name = "the side gate", X = x1 + PavementIn, Z = sideGate, Yaw = 90f, Width = SideGate + 6f, Depth = 2.6f });
        }

        /// <summary>True when the spot is on the campus's own ground (inside the fence's line).</summary>
        public static bool On(float x, float z)
        {
            Ground(out float x0, out float z0, out float x1, out float z1);
            return x >= x0 && x <= x1 && z >= z0 && z <= z1;
        }
    }
}
