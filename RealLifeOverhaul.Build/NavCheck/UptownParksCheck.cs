using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Uptown;

// Uptown's parks (Uptown/ParksRules.cs, PieceRules.cs and the files that lay them): the plan's two park squares laid inside
// their blocks, off the streets, every piece on the grass beside the paths and none on a path's corridor, the paths walked
// from end to end round every piece (a grid of the park flooded from its south gate), the pedestrians' ground and zones on
// them, the same twice; the search for the game's pieces (which name is the piece asked for, the nearest within its reach,
// switched on or off alike) and the sources that make it so (one finder for the campus, the streets and the parks, looking
// through all the children of a root, a copy made switched on); "What is this ground?" calling them parks; and the texts in
// three languages with What's new's line at the end of the next row in all nine.
internal static partial class Program
{
    private static void UptownParks(bool print)
    {
        if (print)
        {
            PrintUptownParks();
        }
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        UptownParkLayoutChecks();
        UptownParkWalkChecks();
        UptownPieceSearchChecks();
        UptownParkSourceChecks(scripts);
        UptownParkTextChecks(scripts);
    }

    // With --parks: each park's pieces and paving, in figures.
    private static void PrintUptownParks()
    {
        foreach (int block in ParkBlocks())
        {
            var pieces = new List<ParkPiece>();
            var paths = new List<PlanBox>();
            ParksRules.Pieces(block, pieces);
            ParksRules.Paths(block, paths);
            ParksRules.Ground(block, out float x0, out float z0, out float x1, out float z1);
            Console.WriteLine("uptown parks: block " + block + " (" + (x1 - x0).ToString("F1") + " by " + (z1 - z0).ToString("F1") + " m, " + (ParksRules.HasFountain(block) ? "fountain" : "statue") + "): "
                              + string.Join(", ", Enum.GetValues(typeof(ParkKind)).Cast<ParkKind>().Select(k => k + " " + pieces.Count(p => p.Kind == k))) + "; paths " + paths.Count + " pieces, "
                              + paths.Sum(r => r.HalfX * r.HalfZ * 4.0).ToString("F0") + " m2");
        }
    }

    // ---------------------------------------------------------------- the layout

    private static List<int> ParkBlocks()
    {
        var blocks = new List<int>(4);
        ParksRules.Blocks(blocks);
        blocks.Sort();
        return blocks;
    }

    // A piece's footprint as a turned rectangle: half its width across its front, half its depth, and its middle.
    private static (float Wide, float Deep) ParkFootprint(ParkKind kind)
    {
        switch (kind)
        {
            case ParkKind.Tree: return (0.45f, 0.45f);
            case ParkKind.Bush: return (0.7f, 0.7f);
            case ParkKind.Bench: return (1.2f, 0.4f);
            case ParkKind.Bin: return (0.35f, 0.35f);
            case ParkKind.Lamp: return (0.3f, 0.3f);
            case ParkKind.Fountain: return (4.9f, 4.57f);
            default: return (1.92f, 2.81f);
        }
    }

    // True when a spot lies within the margin of a piece's turned footprint (the margin a walker's body takes).
    private static bool ParkHits(ParkPiece p, float x, float z, float margin)
    {
        (float wide, float deep) = ParkFootprint(p.Kind);
        double yaw = p.Yaw * Math.PI / 180.0, c = Math.Cos(yaw), s = Math.Sin(yaw);
        double dx = x - p.X, dz = z - p.Z;
        double across = dx * c - dz * s, along = dx * s + dz * c;   // along the piece's own front (+Z at yaw 0) and across it
        return Math.Abs(across) <= wide + margin && Math.Abs(along) <= deep + margin;
    }

    private static bool OnRects(List<PlanBox> rects, float x, float z)
    {
        return rects.Any(r => r.Holds(x, z, 0.001f));
    }

    private static void UptownParkLayoutChecks()
    {
        List<int> blocks = ParkBlocks();
        Check(string.Join(",", blocks) == "117,309", "uptown parks: the plan's parks are blocks 117 and 309, not the campus's 202: " + string.Join(",", blocks));
        Check(ParksRules.IsPark(309) && ParksRules.IsPark(117) && !ParksRules.IsPark(202) && !ParksRules.IsPark(110) && !ParksRules.IsPark(205), "uptown parks: IsPark says the two squares and no other block");
        Check(ParksRules.HasFountain(309) && !ParksRules.HasFountain(117), "uptown parks: a fountain at the 6th to 5th Street park and a statue at the other");
        foreach (int block in blocks)
        {
            var pieces = new List<ParkPiece>();
            var again = new List<ParkPiece>();
            var paths = new List<PlanBox>();
            ParksRules.Pieces(block, pieces);
            ParksRules.Pieces(block, again);
            ParksRules.Paths(block, paths);
            ParksRules.Ground(block, out float x0, out float z0, out float x1, out float z1);
            ParksRules.Centre(block, out float cx, out float cz);
            string name = "uptown parks: block " + block;
            Check(pieces.Count == again.Count && pieces.Zip(again, (a, b) => a.Kind == b.Kind && a.X == b.X && a.Z == b.Z && a.Yaw == b.Yaw && a.Variant == b.Variant).All(v => v),
                name + " is laid the same every time (" + pieces.Count + " pieces)");
            int Count(ParkKind kind) => pieces.Count(p => p.Kind == kind);
            bool fountain = ParksRules.HasFountain(block);
            Check(Count(ParkKind.Fountain) == (fountain ? 1 : 0) && Count(ParkKind.Statue) == (fountain ? 0 : 1) && pieces.Any(p => (p.Kind == ParkKind.Fountain || p.Kind == ParkKind.Statue) && Math.Abs(p.X - cx) < 0.01f && Math.Abs(p.Z - cz) < 0.01f),
                name + " has its fountain or its statue at the middle, one of them");
            Check(Count(ParkKind.Tree) >= 24 && Count(ParkKind.Bush) >= 8 && Count(ParkKind.Bench) >= 8 && Count(ParkKind.Lamp) == 8 && Count(ParkKind.Bin) == 4,
                name + " has trees, bushes, benches, lamps and bins: " + string.Join(", ", Enum.GetValues(typeof(ParkKind)).Cast<ParkKind>().Select(k => k + " " + Count(k))));
            Check(pieces.All(p => p.Variant >= 0 && (p.Kind != ParkKind.Tree || p.Variant <= ParksRules.Maple) && (p.Kind != ParkKind.Bush || p.Variant < ParksRules.BushKinds)),
                name + "'s tree and bush variants are kinds the game has");
            // Inside the square, off the streets: every piece on the park's block by the ground survey, and well inside its edge.
            int off = 0, outside = 0;
            foreach (ParkPiece p in pieces)
            {
                GroundAnswer a = GroundSurvey.At(p.X, p.Z);
                off += a.Kind == GroundKind.ParkSquare && a.Block == block ? 0 : 1;
                float edge = Math.Min(Math.Min(p.X - x0, x1 - p.X), Math.Min(p.Z - z0, z1 - p.Z));
                outside += edge >= (p.Kind == ParkKind.Tree ? 3.0f : 1.5f) ? 0 : 1;
            }
            Check(off == 0 && outside == 0, name + ": every piece stands inside the square (" + off + " off it by the ground survey), clear of its edge and so of the streets (" + outside + " near)");
            // The paths: inside the block, none over another, from the south gate to the north one and from the west to the east.
            double area = 0;
            bool overlap = false;
            for (int i = 0; i < paths.Count; i++)
            {
                area += paths[i].HalfX * paths[i].HalfZ * 4.0;
                for (int j = i + 1; j < paths.Count; j++)
                {
                    float ox = Math.Min(paths[i].X + paths[i].HalfX, paths[j].X + paths[j].HalfX) - Math.Max(paths[i].X - paths[i].HalfX, paths[j].X - paths[j].HalfX);
                    float oz = Math.Min(paths[i].Z + paths[i].HalfZ, paths[j].Z + paths[j].HalfZ) - Math.Max(paths[i].Z - paths[i].HalfZ, paths[j].Z - paths[j].HalfZ);
                    overlap |= ox > 0.001f && oz > 0.001f;
                }
            }
            bool inside = paths.All(r => r.X - r.HalfX >= x0 - 0.001f && r.X + r.HalfX <= x1 + 0.001f && r.Z - r.HalfZ >= z0 - 0.001f && r.Z + r.HalfZ <= z1 + 0.001f);
            bool spine = true, cross = true;
            for (float z = z0 + 0.1f; z < z1; z += 0.5f)
            {
                spine &= OnRects(paths, cx, z);
            }
            for (float x = x0 + 0.1f; x < x1; x += 0.5f)
            {
                cross &= OnRects(paths, x, cz);
            }
            Check(!overlap && inside && spine && cross && area > 600.0, name + "'s paths are disjoint rectangles inside the block, the spine and the cross path whole from edge to edge (" + paths.Count + " pieces, "
                                                                          + area.ToString("F0") + " m2)");
            // None on a path's corridor: no piece but the plaza's own stands over the middle two metres of a path outside the plaza.
            float plaza = ParksRules.PlazaHalf(block);
            int blocking = 0;
            foreach (ParkPiece p in pieces)
            {
                if (p.Kind == ParkKind.Fountain || p.Kind == ParkKind.Statue)
                {
                    continue;
                }
                float u = Math.Abs(p.X - cx), v = Math.Abs(p.Z - cz);
                bool inPlaza = u <= plaza + 0.3f && v <= plaza + 0.3f;
                if (!inPlaza && (ParkHitsCorridor(p, cx, cz)))
                {
                    blocking++;
                }
            }
            Check(blocking == 0, name + ": no tree, bush, bench, lamp or bin stands in a path's walking corridor outside the plaza (" + blocking + ")");
            // No two pieces on top of one another.
            int clash = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                for (int j = i + 1; j < pieces.Count; j++)
                {
                    ParkPiece a = pieces[i], b = pieces[j];
                    bool bush = a.Kind == ParkKind.Bush || b.Kind == ParkKind.Bush;
                    float d = (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
                    clash += d < (bush ? 0.9f : 1.5f) ? 1 : 0;
                }
            }
            Check(clash == 0, name + ": no two pieces stand on one another (" + clash + ")");
        }
    }

    // True when a piece's footprint reaches into the middle two metres of the spine, the cross path or a shorter path.
    private static bool ParkHitsCorridor(ParkPiece p, float cx, float cz)
    {
        const float half = 1.0f;
        (float wide, float deep) = ParkFootprint(p.Kind);
        double yaw = p.Yaw * Math.PI / 180.0, c = Math.Abs(Math.Cos(yaw)), s = Math.Abs(Math.Sin(yaw));
        double reachX = c * wide + s * deep, reachZ = s * wide + c * deep;   // the footprint's half extents along the ground's axes
        double u = Math.Abs(p.X - cx), v = Math.Abs(p.Z - cz);
        return u - reachX < half || v - reachZ < half || Math.Abs(v - ParksRules.SideAt) - reachZ < half;
    }

    // ---------------------------------------------------------------- walked

    private static void UptownParkWalkChecks()
    {
        const float cell = 0.5f, body = 0.4f;
        var player = LayoutPlans.PlayerGround();
        var npc = LayoutPlans.NpcGround();
        foreach (int block in ParkBlocks())
        {
            var pieces = new List<ParkPiece>();
            var paths = new List<PlanBox>();
            ParksRules.Pieces(block, pieces);
            ParksRules.Paths(block, paths);
            ParksRules.Ground(block, out float x0, out float z0, out float x1, out float z1);
            ParksRules.Centre(block, out float cx, out float cz);
            int columns = (int)Math.Ceiling((x1 - x0) / cell), rows = (int)Math.Ceiling((z1 - z0) / cell);
            var free = new bool[columns, rows];
            int onPath = 0, walkable = 0;
            for (int i = 0; i < columns; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    float x = x0 + (i + 0.5f) * cell, z = z0 + (j + 0.5f) * cell;
                    if (!OnRects(paths, x, z))
                    {
                        continue;
                    }
                    onPath++;
                    if (pieces.All(p => !ParkHits(p, x, z, body)))
                    {
                        free[i, j] = true;
                        walkable++;
                    }
                }
            }
            // Flood from the south gate: the spine's south end, the middle of the block's south edge.
            var seen = new bool[columns, rows];
            var open = new Stack<(int, int)>();
            int si = (int)((cx - x0) / cell), sj = 0;
            if (free[si, sj])
            {
                open.Push((si, sj));
                seen[si, sj] = true;
            }
            int reached = 0;
            while (open.Count > 0)
            {
                (int i, int j) = open.Pop();
                reached++;
                foreach ((int di, int dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int a = i + di, b = j + dj;
                    if (a >= 0 && b >= 0 && a < columns && b < rows && free[a, b] && !seen[a, b])
                    {
                        seen[a, b] = true;
                        open.Push((a, b));
                    }
                }
            }
            bool Reaches(float x, float z)
            {
                int i = Math.Min(columns - 1, Math.Max(0, (int)((x - x0) / cell))), j = Math.Min(rows - 1, Math.Max(0, (int)((z - z0) / cell)));
                return seen[i, j];
            }
            string name = "uptown parks: block " + block;
            float plaza = ParksRules.PlazaHalf(block);
            bool ends = Reaches(cx, z1 - 0.1f) && Reaches(x0 + 0.1f, cz) && Reaches(x1 - 0.1f, cz)
                        && Reaches(x0 + 0.1f, cz + ParksRules.SideAt) && Reaches(x1 - 0.1f, cz + ParksRules.SideAt) && Reaches(x0 + 0.1f, cz - ParksRules.SideAt) && Reaches(x1 - 0.1f, cz - ParksRules.SideAt)
                        && Reaches(cx + plaza - 0.5f, cz + plaza * 0.6f) && Reaches(cx - plaza + 0.5f, cz - plaza * 0.6f);
            Check(free[si, sj] && ends && reached == walkable && walkable >= onPath * 0.85, name + " is walked from its south gate to its north one, its west and east ends, the shorter paths and the plaza's edges, round every piece ("
                                                                                              + reached + " of " + onPath + " half-metre squares of path, a body of " + body + " m)");
            // The pedestrians' ground and the player's cover the paths and reach out onto the pavements at the block's edge.
            bool both = true;
            foreach (PlanBox r in paths)
            {
                both &= UptownCovered(npc.Boxes, r.X, r.Z) && UptownCovered(player.Boxes, r.X, r.Z) && UptownCovered(npc.Boxes, r.X - r.HalfX + 0.1f, r.Z - r.HalfZ + 0.1f)
                        && UptownCovered(npc.Boxes, r.X + r.HalfX - 0.1f, r.Z + r.HalfZ - 0.1f);
            }
            bool gates = UptownCovered(npc.Boxes, cx, z0 - 0.3f) && UptownCovered(npc.Boxes, cx, z1 + 0.3f) && UptownCovered(npc.Boxes, x0 - 0.3f, cz) && UptownCovered(npc.Boxes, x1 + 0.3f, cz);
            Check(both && gates, name + "'s paths are the pedestrians' ground too, and the spine and cross path run out onto the pavements at the block's edge");
            // The grass between is the player's, not the pedestrians'.
            Check(UptownCovered(player.Boxes, x0 + 3f, z0 + 3f) && !UptownCovered(npc.Boxes, x0 + 3f, z0 + 3f) && !UptownCovered(npc.Boxes, cx + 12f, cz + 15f),
                name + "'s lawns are walked by the player alone, the pedestrians keep to the paths");
            var zones = new List<PlanBox>();
            ParksRules.Zones(block, zones);
            Check(zones.Count == ParksRules.ZonesPerPark && zones.All(z => UptownCovered(npc.Boxes, z.X, z.Z) && UptownCovered(npc.Boxes, z.X - z.HalfX + 0.1f, z.Z - z.HalfZ + 0.1f)
                                                                           && UptownCovered(npc.Boxes, z.X + z.HalfX - 0.1f, z.Z + z.HalfZ - 0.1f) && OnRects(paths, z.X, z.Z)
                                                                           && !pieces.Any(p => p.Kind != ParkKind.Lamp && p.Kind != ParkKind.Bin && p.Kind != ParkKind.Bench && ParkHits(p, z.X, z.Z, 0f))),
                name + "'s two pedestrians' zones lie on the spine, on the pedestrians' ground, off every piece");
        }
        var all = new List<PlanBox>();
        LayoutPlans.ParkZones(all);
        var pavement = new List<PlanBox>();
        LayoutPlans.Zones(pavement);
        Check(all.Count == LayoutPlans.ParkZoneCount && all.Count == 4 && pavement.Count == 111 && pavement.Count == LayoutPlans.ZoneCount,
            "uptown parks: four zones in the parks, after the pavements' 111 (" + all.Count + ", " + pavement.Count + ")");
    }

    // ---------------------------------------------------------------- the search for the game's pieces

    private static void UptownPieceSearchChecks()
    {
        Check(PieceRules.Matches("SleepingBench", "SleepingBench") && PieceRules.Matches("SleepingBench (11)", "SleepingBench") && PieceRules.Matches("Tree Birch (61)", "Tree Birch")
              && !PieceRules.Matches("SM_Trash Can_LOD1", "SM_Trash Can") && !PieceRules.Matches("SM_FountainWater", "SM_Fountain") && !PieceRules.Matches("Playground_Fence_ModularFinal", "Playground_Fence_Modular")
              && !PieceRules.Matches("Sleeping", "SleepingBench") && !PieceRules.Matches(null, "x") && !PieceRules.Matches("x", string.Empty),
            "uptown pieces: a name is the piece asked for with the editor's numbers (SleepingBench (11)) and no other kind that begins the same");
        // The game's bench group stands switched off at a load: the search takes it as it takes one that is on (whether a piece is
        // on or off is not what a name is picked by), the nearest to its fixed place, a piece of another kind never.
        var names = new List<string> { "SleepingBench (3)", "SleepingBench_LOD1", "SleepingBench (11)", "SleepingBench", "SM_Bench" };
        var xs = new List<float> { 300f, 214.3f, 214.40f, 0f, 214.4f };
        var zs = new List<float> { 99f, 99.4f, 99.39f, 0f, 99.39f };
        Check(PieceRules.Pick(names, xs, zs, "SleepingBench", 214.40f, 99.39f, 40f) == 2, "uptown pieces: the nearest bench to its fixed place is taken, a piece named for another kind never");
        Check(PieceRules.Pick(names, xs, zs, "SleepingBench", 214.40f, 99.39f, 0.05f) == 2 && PieceRules.Pick(names, xs, zs, "SleepingBench", 800f, 800f, 40f) == -1,
            "uptown pieces: a piece farther than its reach from the fixed place is not taken (the game's scene has moved)");
        Check(PieceRules.Pick(new List<string> { "A (1)", "A (2)" }, new List<float> { 1f, 1f }, new List<float> { 1f, 1f }, "A", 0f, 0f, 10f) == 0, "uptown pieces: two at the same distance, the first is taken (every machine the same)");
        Check(PieceRules.Words(new[] { "SleepingBench", "SM_Trash Can", "Prefab_BikeRentalStand" }, new[] { true, true, false }) == "found: SleepingBench, SM_Trash Can; not found: Prefab_BikeRentalStand"
              && PieceRules.Words(new[] { "a" }, new[] { true }) == "found: a" && PieceRules.Words(new[] { "a" }, new[] { false }) == "not found: a",
            "uptown pieces: what was found and what was not is one line");
    }

    // ---------------------------------------------------------------- sources

    private static void UptownParkSourceChecks(string scripts)
    {
        string uptown = Path.Combine(scripts, "Uptown"), schools = Path.Combine(scripts, "Schools");
        string Read(string folder, string file) => File.Exists(Path.Combine(folder, file)) ? Code(File.ReadAllText(Path.Combine(folder, file))) : string.Empty;
        string pieces = Read(uptown, "GamePieces.cs"), pieceRules = Read(uptown, "PieceRules.cs"), parksRules = Read(uptown, "ParksRules.cs"), parks = Read(uptown, "ParksGame.cs");
        string streets = Read(uptown, "StreetsGame.cs"), campus = Read(schools, "SchoolsGame.cs"), director = Read(uptown, "GroundDirector.Parks.cs"), furnish = Read(uptown, "GroundDirector.Streets.cs");
        string groundDirector = Read(uptown, "GroundDirector.cs"), art = Read(uptown, "GroundGame.Art.cs"), survey = Read(uptown, "GroundDirector.Survey.cs");
        Check(new[] { pieces, pieceRules, parksRules, parks, streets, campus, director }.All(s => s.Length > 0), "uptown parks sources: every file is there");
        foreach (var file in new[] { ("PieceRules.cs", pieceRules), ("ParksRules.cs", parksRules) })
        {
            Check(!Regex.IsMatch(file.Item2, @"\busing UnityEngine|\bVector3\b|\bMathf\b|\bGameObject\b|\bSystem\.Linq\b|using System\.Linq|\bUnityEngine\.Random\b|new System\.Random\(\)|DateTime\.Now"),
                "uptown parks sources: " + file.Item1 + " is game-free, with no LINQ and nothing left to chance or the clock");
        }
        // One finder for all three: a root's children are all looked through, switched off or on, and a copy is made switched on.
        Check(Regex.IsMatch(pieces, @"GetComponentsInChildren<Transform>\(true\)") && !Regex.IsMatch(pieces, @"GetComponentsInChildren<Transform>\(\s*(?:false)?\s*\)"),
            "uptown pieces sources: GamePieces looks through ALL the children of a scene root, the switched-off ones too (the pavement benches and Industry City's docks stand switched off)");
        foreach (var file in new[] { ("StreetsGame.cs", streets), ("SchoolsGame.cs", campus), ("ParksGame.cs", parks), ("GroundDirector.Parks.cs", director) })
        {
            Check(!Regex.IsMatch(file.Item2, @"GetComponentsInChildren<Transform>\(\s*(?:false)?\s*\)"), "uptown pieces sources: no search of the switched-on alone in " + file.Item1);
        }
        Check(streets.Contains("GamePieces.Find(Templates, Found)") && campus.Contains("GamePieces.Find(Templates, Found)") && parks.Contains("GamePieces.Find(Templates, Found)"),
            "uptown pieces sources: the street furniture, the campus and the parks find their pieces through GamePieces, one finder");
        Check(!Regex.IsMatch(streets + campus + parks, @"private static void Take\(|SceneManager\.GetSceneAt|GetRootGameObjects"), "uptown pieces sources: none of the three keeps a finder of its own");
        Check(Regex.IsMatch(pieces, @"Object\.Instantiate\(template, parent\);[\s\S]*copy\.SetActive\(true\);[\s\S]*forceRenderingOff = false"), "uptown pieces sources: a copy is made switched on and its renderers drawn");
        Check(streets.Contains("GamePieces.Stand(template, holder.transform,") && campus.Contains("GamePieces.Stand(template, parent,") && parks.Contains("GamePieces.Stand(Found[t], parent,"),
            "uptown pieces sources: every piece is laid through GamePieces.Stand");
        foreach (string template in new[] { "SleepingBench", "SM_Trash Can", "SM_PavementHoleforTrees_Broadleaf M1", "SM_PavementHoleforTrees_02 Birch", "SM_PavementHoleforTrees_02 Broadleaf L1", "Prefab_BikeRentalStand" })
        {
            Check(Regex.Matches(streets, "Name = \"" + Regex.Escape(template) + "\"").Count == 1, "uptown pieces sources: the street furniture asks for " + template + " once");
        }
        Check(streets.Contains("FoundWords = GamePieces.Words(Templates, Found)") && director.Contains("StreetsGame.FoundWords") && director.Contains("ParksGame.FoundWords"),
            "uptown pieces sources: what the street furniture and the parks found and missed is said once a load");
        // The parks: the scene touched in ParksGame alone, laid with the street furniture before the ground grows, asleep and woken together.
        var scene = new Regex(@"\b(?:Object\.Instantiate|Instantiate\(|SetActive\(|AddComponent<|GetComponentsInChildren<|SceneManager\.|DestroyImmediate|Physics\.)");
        Check(scene.IsMatch(parks) && !scene.IsMatch(director) && !scene.IsMatch(furnish) && !scene.IsMatch(parksRules), "uptown parks sources: the scene touched in ParksGame and GamePieces alone");
        Check(!Regex.IsMatch(parks + director, @"FindObjects|GameObject\.Find\(|SceneFinds\.|\bSystem\.Linq\b|\bRandom\.Range\b|\bvoid\s+(?:Update|LateUpdate|FixedUpdate)\s*\(|\bmodData\b|\bSaveHook\b|\bSend\("),
            "uptown parks sources: no search of the scene, no LINQ, no chance, no frame method, nothing saved or sent");
        Check(Regex.IsMatch(parks, @"holder\.SetActive\(false\);[\s\S]*holder\.transform\.SetParent\(art, false\);[\s\S]*holder\.SetActive\(true\);") && parks.Contains("HolderName = \"RealLifeOverhaul "),
            "uptown parks sources: the parks made asleep under the art's holder, named for the mod, and woken together");
        int furnishAt = furnish.IndexOf("StreetsGame.Furnish(GroundGame.ArtRoot)", StringComparison.Ordinal), parksAt = furnish.IndexOf("LayParks();", StringComparison.Ordinal);
        int copyArt = groundDirector.IndexOf("GroundGame.CopyArt(", StringComparison.Ordinal), laid = groundDirector.IndexOf("Furnish();", StringComparison.Ordinal),
            ground = groundDirector.IndexOf("GroundWork.Ground(LayoutPlans.PlayerGround())", StringComparison.Ordinal);
        Check(furnishAt >= 0 && parksAt > furnishAt && copyArt >= 0 && laid > copyArt && ground > laid && director.Contains("ParksGame.Lay(GroundGame.ArtRoot)"),
            "uptown parks sources: the parks laid with the street furniture, after the art and before the ground grows (walked round), behind the loading screen");
        Check(groundDirector.Contains("ParksGame.Forget();") && groundDirector.Contains("ParksWords()") && parks.Contains("Object.Destroy(mesh)"), "uptown parks sources: the parks forgotten with the city, their paving's meshes given up");
        Check(art.Contains("LayoutPlans.ParkZones(ZoneBoxes);") && Regex.IsMatch(art, @"LayoutPlans\.Zones\(ZoneBoxes\);\s*LayoutPlans\.ParkZones\(ZoneBoxes\);"), "uptown parks sources: the parks' zones made with the pavements', after them");
        Check(parks.Contains("lights[i].enabled = false") && parks.Contains("CapsuleCollider"), "uptown parks sources: a lamp copied dark and a fountain with a basin to be walked round");
        Check(parks.Contains("ParksRules.Paths(block, Rects)") && parks.Contains("M_ConcretePavement_02") && !parks.Contains("PropLibrary"), "uptown parks sources: the paths the game's pavement material on a flat surface, no prop of the mod's");
        Check(survey.Contains("ParksGame.Stands(a.Block)"), "uptown parks sources: What is this ground? says whether the park stands at this load");
        // The ground survey calls them parks on the plan and the text no longer says bare.
        foreach (int block in ParkBlocks())
        {
            ParksRules.Centre(block, out float cx, out float cz);
            GroundAnswer a = GroundSurvey.At(cx, cz);
            Check(a.Kind == GroundKind.ParkSquare && a.Block == block, "uptown parks: What is this ground? calls the middle of block " + block + " a park (" + a.Kind + ")");
        }
    }

    // ---------------------------------------------------------------- texts

    private static void UptownParkTextChecks(string scripts)
    {
        string locales = Path.GetFullPath(Path.Combine(scripts, @"..\Locales"));
        var named = new[] { "reallife_uptown_streets_test_parks", "reallife_uptown_streets_parks_done", "reallife_uptown_ground_what_park" };
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language + ".json")));
            foreach (string key in named)
            {
                Check(doc.RootElement.TryGetProperty(key, out JsonElement value) && value.GetString().Length > 0, "uptown parks texts: " + key + " is in " + language + ".json");
                if (language == "en" && doc.RootElement.TryGetProperty(key, out JsonElement en))
                {
                    Check(en.GetString().Length <= 240 && !Regex.IsMatch(en.GetString(), "[–—!]| - "), "uptown parks texts: " + key + " at most 240 characters, no dash, no exclamation mark");
                }
            }
            string park = doc.RootElement.GetProperty("reallife_uptown_ground_what_park").GetString();
            Check(language != "en" || !Regex.IsMatch(park, "bare|empty|never", RegexOptions.IgnoreCase), "uptown parks texts: What is this ground? no longer calls a park bare (" + language + ")");
            string done = doc.RootElement.GetProperty("reallife_uptown_streets_parks_done").GetString();
            Check(new[] { "{parks}", "{blocks}", "{trees}", "{bushes}", "{benches}", "{lamps}", "{bins}" }.All(done.Contains), "uptown parks texts: the parks' notice names its seven figures in " + language);
        }
        var line = new Dictionary<string, string>
        {
            { "en", "Uptown's two other squares are parks now" }, { "pt", "As outras duas praças do Uptown agora são parques" }, { "zh-cn", "上城另外两个广场现在是公园" },
            { "de", "Die beiden anderen Plätze von Uptown sind jetzt Parks" }, { "fr", "Les deux autres places d'Uptown sont désormais des parcs" },
            { "es", "Las otras dos plazas de Uptown son ahora parques" }, { "ru", "Две другие площади Uptown теперь парки" }, { "nl", "De twee andere pleinen van Uptown zijn nu parken" },
            { "tr", "Uptown'ın diğer iki meydanı artık park" }
        };
        foreach (var language in line)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language.Key + ".json")));
            string next = doc.RootElement.GetProperty("reallife_changes_next").GetString();
            int at = next.IndexOf(language.Value, StringComparison.Ordinal);
            Check(at > 0 && next.IndexOf(language.Value, at + 1, StringComparison.Ordinal) < 0,
                "uptown parks texts: What's new's next row has the parks' line, once, in " + language.Key);
            Check(at > 0 && next.Substring(at).Length < 700, "uptown parks texts: the parks' line is the last of the row in " + language.Key + " (" + (at > 0 ? next.Length - at : -1) + " characters)");
        }
    }
}
