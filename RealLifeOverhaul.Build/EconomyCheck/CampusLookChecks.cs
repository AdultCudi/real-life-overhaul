using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Schools;

// Birchwood School's own look (Scripts/Schools/CampusLook*.cs, docs/features/schools.md "The look"): the three models of the
// second props bundle against the facts the code stands them by (their size, their draw calls, the game's materials they name),
// where each stands (the main school on its building's door, the hall and the junior block on their plots, clear of the fence,
// the courtyard and each other), what is solid and carved, the lines for Player.log, the sources (the game's materials found in one
// search behind the loading screen, the engine's own path to hide the old look and give it back, nothing a frame, nothing sent or saved)
// and the texts: the tests' names in three languages, What's new in all nine.
internal static partial class Program
{
    private static string PropFile(string relative)
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\PropAssets", relative));
    }

    private static void CampusLookChecks()
    {
        CampusLookBundleChecks();
        CampusLookPlacementChecks();
        CampusLookSourceChecks();
        CampusLookTextChecks();
    }

    // ---------------------------------------------------------------- the models against the bundle

    private static void CampusLookBundleChecks()
    {
        string contentsPath = PropFile(@"contents\reallife_props_2.json");
        Check(File.Exists(contentsPath), "campus look bundle: the second bundle's list of contents is committed");
        if (!File.Exists(contentsPath))
        {
            return;
        }
        using JsonDocument contents = JsonDocument.Parse(File.ReadAllText(contentsPath));
        Dictionary<string, JsonElement> props = contents.RootElement.GetProperty("props").EnumerateArray().ToDictionary(p => p.GetProperty("id").GetString(), p => p.Clone());
        int[] budgets = { 20000, 8000, 8000 };
        for (int i = 0; i < CampusLookRules.Models.Length; i++)
        {
            LookModel m = CampusLookRules.Models[i];
            Check(props.TryGetValue(m.Id, out JsonElement prop), "campus look bundle: reallife_props_2 holds " + m.Id);
            if (!props.ContainsKey(m.Id))
            {
                continue;
            }
            float[] size = prop.GetProperty("size").EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
            CampusLookRules.Size(m, out float sx, out float sy, out float sz);
            Check(Math.Abs(size[0] - sx) < 0.06f && Math.Abs(size[1] - sy) < 0.06f && Math.Abs(size[2] - sz) < 0.06f,
                "campus look bundle: " + m.Id + " is " + string.Join(" x ", size) + " m, the facts say " + sx + " x " + sy + " x " + sz);
            int triangles = prop.GetProperty("triangles").GetInt32();
            Check(triangles > 1000 && triangles <= budgets[i], "campus look bundle: " + m.Id + " has " + triangles + " triangles (a budget of " + budgets[i] + ")");
            Check(prop.TryGetProperty("parts", out JsonElement parts) && parts.GetArrayLength() >= 2 && parts.GetArrayLength() <= CampusLookRules.MostDraws,
                "campus look bundle: " + m.Id + " is drawn in 2 to " + CampusLookRules.MostDraws + " calls: " + (prop.TryGetProperty("parts", out JsonElement p2) ? p2.GetArrayLength().ToString() : "no parts"));
            if (!prop.TryGetProperty("parts", out parts))
            {
                continue;
            }
            JsonElement[] list = parts.EnumerateArray().ToArray();
            Check(list[0].GetProperty("game").ValueKind == JsonValueKind.Null, "campus look bundle: " + m.Id + "'s first part is the bundle's atlas");
            Check(list.Skip(1).All(p => p.GetProperty("game").ValueKind == JsonValueKind.String && CampusLookRules.Kinds.Contains(p.GetProperty("game").GetString())),
                "campus look bundle: " + m.Id + "'s other parts are each a kind of the game's materials the code knows");
            Check(list.Skip(1).Select(p => p.GetProperty("game").GetString()).Distinct().Count() == list.Length - 1, "campus look bundle: " + m.Id + " names each kind once (one submesh a kind)");
            Check(list.Sum(p => p.GetProperty("triangles").GetInt32()) == triangles && list.All(p => p.GetProperty("triangles").GetInt32() > 0),
                "campus look bundle: " + m.Id + "'s parts add up to its triangles, none empty");
            Check(list.Any(p => p.GetProperty("game").ValueKind == JsonValueKind.String && p.GetProperty("game").GetString() == "brick"), "campus look bundle: " + m.Id + " is brick built");
        }
        Check(props.Values.Sum(p => p.GetProperty("triangles").GetInt32()) <= contents.RootElement.GetProperty("triangle_limit").GetInt32(), "campus look bundle: the second bundle is within its triangle limit");
        // The models' own settings and the build's list of kinds say what the code knows.
        foreach (string id in CampusLookRules.Models.Select(m => m.Id))
        {
            string json = File.ReadAllText(PropFile(@"models\" + id + @"\" + id + ".json"));
            using JsonDocument settings = JsonDocument.Parse(json);
            bool kindsKnown = true;
            foreach (JsonProperty slot in settings.RootElement.GetProperty("game").EnumerateObject())
            {
                kindsKnown &= CampusLookRules.Kinds.Contains(slot.Value.GetProperty("kind").GetString()) && slot.Value.GetProperty("tile").GetDouble() > 0;
            }
            if (settings.RootElement.TryGetProperty("split", out JsonElement splits))
            {
                foreach (JsonElement split in splits.EnumerateArray())
                {
                    kindsKnown &= CampusLookRules.Kinds.Contains(split.GetProperty("kind").GetString());
                }
            }
            Check(kindsKnown, "campus look bundle: " + id + ".json asks only for kinds of the game's materials the code finds, each with a tile size");
            Check(File.Exists(PropFile(@"models\" + id + @"\" + id + ".glb")), "campus look bundle: the model " + id + ".glb is committed");
        }
        Match kinds = Regex.Match(File.ReadAllText(PropFile(@"scripts\models.py")), @"GAME_KINDS = \(([^)]*)\)");
        Check(kinds.Success && Regex.Matches(kinds.Groups[1].Value, "\"([a-z]+)\"").Select(m => m.Groups[1].Value).SequenceEqual(CampusLookRules.Kinds),
            "campus look bundle: the build's list of the game's kinds (models.py) is the code's");
        foreach (string kind in CampusLookRules.Kinds)
        {
            string[] names = CampusLookRules.GameNames(kind);
            Check(names.Length >= 2 && names.All(n => n.StartsWith("M_", StringComparison.Ordinal)) && names.Distinct().Count() == names.Length, "campus look bundle: a kind '" + kind + "' has its game materials by name, best first");
        }
        Check(CampusLookRules.Kinds.SelectMany(CampusLookRules.GameNames).Distinct().Count() == CampusLookRules.Kinds.SelectMany(CampusLookRules.GameNames).Count() && CampusLookRules.GameNames("nothing").Length == 0,
            "campus look bundle: no game material serves two kinds, and a kind unknown has none");
        Check(CampusLookRules.StandsInFor("fence") == "glass" && CampusLookRules.StandsInFor("brick") == "brick", "campus look bundle: a missing chain-link is stood in for by the glass's colour");
        string sets = File.ReadAllText(PropFile("sets.json"));
        string library = ModSource(@"Core\PropLibrary.cs");
        Check(Regex.IsMatch(sets, "\"reallife_props_2\"") && Regex.IsMatch(sets, "\"reallife_props_site\"") && Regex.IsMatch(library, "\"Bundles/reallife_props\", \"Bundles/reallife_props_2\", \"Bundles/reallife_props_site\""),
            "campus look bundle: the loader reads the three bundles in the table's order");
    }

    // ---------------------------------------------------------------- where each stands

    private static (float x0, float z0, float x1, float z1) BodyOnGround(LookModel m, float rootX, float rootZ, float yaw)
    {
        // The model's body in its own frame is x across, z back from the front wall; turned by yaw it is a turned rectangle: its corners' extremes.
        double a = yaw * Math.PI / 180.0;
        float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (float lx in new[] { -m.HalfWidth, m.HalfWidth })
        {
            foreach (float lz in new[] { 0f, m.Depth })
            {
                float wx = rootX + lx * c + lz * s, wz = rootZ - lx * s + lz * c;
                minX = Math.Min(minX, wx);
                maxX = Math.Max(maxX, wx);
                minZ = Math.Min(minZ, wz);
                maxZ = Math.Max(maxZ, wz);
            }
        }
        return (minX, minZ, maxX, maxZ);
    }

    private static void CampusLookPlacementChecks()
    {
        LookModel main = CampusLookRules.Models[0];
        // The main school stands on its building's door: the model's door (1.55 m in front of its front wall) comes out where the door is, whichever way it faces.
        bool door = true, faces = true;
        foreach (float facing in new[] { 0f, 90f, 180f, 270f, 37.5f, -90f })
        {
            CampusLookRules.RootOnDoor(main, 100f, -40f, facing, out float rx, out float rz, out float yaw);
            double a = yaw * Math.PI / 180.0;
            float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
            float doorX = rx + 0f * c + -main.DoorOut * s, doorZ = rz - 0f * s + -main.DoorOut * c;      // the model's (0, -DoorOut) turned by yaw
            door &= Math.Abs(doorX - 100f) < 0.001f && Math.Abs(doorZ + 40f) < 0.001f;
            float frontX = -s, frontZ = -c;                                                                 // the model's front, its -Z turned
            double h = facing * Math.PI / 180.0;
            faces &= Math.Abs(frontX - (float)Math.Sin(h)) < 0.001f && Math.Abs(frontZ - (float)Math.Cos(h)) < 0.001f;
        }
        Check(door, "campus look placement: the main school's door comes out on the building's own door, however it faces");
        Check(faces, "campus look placement: the main school faces the way its door does");
        SchoolGrounds.MainLot(out float mx, out float mz, out float facingMain, out float width, out float depth, out float inset);
        // The copy engine's door on a K1 office stands about on the lot's middle: the look then stands in its lot, its forecourt on the block's edge.
        CampusLookRules.RootOnDoor(main, mx, mz - depth * 0.5f + inset - main.DoorOut, facingMain, out float x, out float z, out float yaw0);
        (float x0, float z0, float x1, float z1) body = BodyOnGround(main, x, z, yaw0);
        SchoolGrounds.Ground(out float gx0, out float gz0, out float gx1, out float gz1);
        Check(Math.Abs(yaw0) < 0.01f && Math.Abs(x - mx) < 0.01f && Math.Abs(z - (mz - depth * 0.5f + inset)) < 0.01f, "campus look placement: a door on the lot's middle puts the main school on its lot, its front wall the forecourt's depth in from the block's edge");
        Check(body.x0 >= mx - width * 0.5f - 0.5f && body.x1 <= mx + width * 0.5f + 0.5f && body.z1 <= mz + depth * 0.5f + 0.5f && body.z0 >= gz0, "campus look placement: the main school's body is inside its lot");
        Check(CampusLookRules.MainWhy(0f, 0f) == null && CampusLookRules.MainWhy(CampusLookRules.MostAside - 0.1f, -CampusLookRules.MostForward + 0.1f) == null
              && CampusLookRules.MainWhy(CampusLookRules.MostAside + 0.1f, 0f) != null && CampusLookRules.MainWhy(0f, CampusLookRules.MostForward + 0.1f) != null,
            "campus look placement: a door too far across the lot, or the look too far from the footprint, refuses the look and keeps the copy engine's");
        // The hall and the junior block on their plots.
        var plots = new List<CampusBlockPlot>();
        SchoolGrounds.Blocks(plots);
        var bodies = new List<(string, float, float, float, float)> { ("the main school", body.x0, body.z0, body.x1, body.z1) };
        for (int i = 0; i < 2; i++)
        {
            LookModel m = CampusLookRules.Models[i + 1];
            CampusBlockPlot plot = plots[i];
            CampusLookRules.RootOnPlot(m, plot, out float rx, out float rz, out float yaw);
            double a = yaw * Math.PI / 180.0;
            float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
            (float x0, float z0, float x1, float z1) b = BodyOnGround(m, rx, rz, yaw);
            bodies.Add((plot.Name, b.x0, b.z0, b.x1, b.z1));
            Check(Math.Abs(yaw - plot.Yaw) < 0.01f, "campus look placement: " + plot.Name + " faces the way its plot does (" + yaw + ")");
            // Its front looks out of the block: the hall west to Birch Street, the junior block east to Elm Street.
            bool west = plot.Yaw == 90;
            float frontX = -s;
            Check(west ? frontX < -0.99f : frontX > 0.99f, "campus look placement: " + plot.Name + "'s front looks " + (west ? "west to Birch Street" : "east to Elm Street"));
            // What stands out of the front wall stops inside the fence, the wall a fence's clearance behind it; the back wall stays on the campus.
            float edge = west ? gx0 : gx1;
            float tip = rx + frontX * m.Front;
            float fence = west ? gx0 + SchoolGrounds.FenceIn : gx1 - SchoolGrounds.FenceIn;
            Check((west ? tip > fence : tip < fence) && Math.Abs(Math.Abs(rx - edge) - CampusLookRules.WallFromEdge(m)) < 0.01f,
                "campus look placement: " + plot.Name + "'s porch ends inside the fence, its wall " + CampusLookRules.WallFromEdge(m) + " m from the block's edge");
            Check(b.x0 >= gx0 && b.x1 <= gx1 && b.z0 >= gz0 && b.z1 <= gz1, "campus look placement: " + plot.Name + "'s body is inside the campus");
            // Across the plot's width it stands in the middle (the plot's middle z is the model's).
            Check(Math.Abs((b.z0 + b.z1) * 0.5f - plot.Z) < 0.01f && Math.Abs((b.z1 - b.z0) - m.HalfWidth * 2f) < 0.01f, "campus look placement: " + plot.Name + " stands across the middle of its plot");
        }
        for (int a = 0; a < bodies.Count; a++)
        {
            for (int b = a + 1; b < bodies.Count; b++)
            {
                bool apart = bodies[a].Item4 <= bodies[b].Item2 || bodies[b].Item4 <= bodies[a].Item2 || bodies[a].Item5 <= bodies[b].Item3 || bodies[b].Item5 <= bodies[a].Item3;
                Check(apart, "campus look placement: " + bodies[a].Item1 + " and " + bodies[b].Item1 + " stand apart");
            }
        }
        // The courtyard between the two backs holds the court and the green; nothing of it stands in a building.
        SchoolGrounds.Courtyard(out float courtX, out float half);
        Check(half * 2f >= SchoolGrounds.CourtWidth + 2f * 1.2f, "campus look placement: the way between the hall's back and the junior block's is " + half * 2f + " m, the court " + SchoolGrounds.CourtWidth + " m and a bench each side fit");
        var pieces = new List<CampusPiece>();
        var runs = new List<CampusFence>();
        SchoolGrounds.Pieces(pieces, runs);
        Check(pieces.Where(p => p.Kind != CampusPieceKind.Fence && p.Kind != CampusPieceKind.Post && p.Kind != CampusPieceKind.Pitch).All(p => bodies.All(bb => !Within(p.X, p.Z, bb.Item2, bb.Item3, bb.Item4, bb.Item5, 0.3f))),
            "campus look placement: the court, hoops, benches and trees stand clear of the models' bodies");
        CampusPiece court = pieces.First(p => p.Kind == CampusPieceKind.Court);
        Check(Math.Abs(court.X - courtX) < 0.01f, "campus look placement: the court lies in the middle of the way between the backs");
        // What is solid and carved: the body behind the front wall, the main school's entrance bay before it, nothing of the porch or the forecourt.
        var boxes = new List<LookBox>();
        foreach (LookModel m in CampusLookRules.Models)
        {
            CampusLookRules.Boxes(m, boxes);
            Check(boxes.Count == (m.BayDepth > 0f ? 2 : 1) && boxes[0].Z - boxes[0].HalfZ >= -0.001f && Math.Abs(boxes[0].HalfZ * 2f - m.Depth) < 0.001f && boxes.All(b => b.Height > 2f && b.Height <= m.Height),
                "campus look placement: " + m.Id + "'s solid ground is its body behind the front wall" + (m.BayDepth > 0f ? " and its bay before it" : string.Empty));
            Check(boxes.All(b => { CampusLookRules.CarveSize(b, out float sx, out float sz); return sx > 0.4f && sz > 0.4f && sx <= b.HalfX * 2f && sz <= b.HalfZ * 2f; }), "campus look placement: " + m.Id + "'s carving boxes are inside its bodies");
            if (m.BayDepth > 0f)
            {
                Check(boxes[1].Z + boxes[1].HalfZ <= 0.001f && boxes[1].Z < 0f, "campus look placement: " + m.Id + "'s bay is in front of its front wall");
            }
        }
        // The lines for Player.log.
        Check(CampusLookRules.StoodLine("the hall", 2576, 4, 3, 0, 0, 0) == "the hall: 2576 triangles, 4 draw call(s) (3 of the game's own materials)"
              && CampusLookRules.StoodLine("the main school", 14786, 4, 2, 1, 9000, 12).Contains("1 plain stand-in(s)") && CampusLookRules.StoodLine("the main school", 14786, 4, 3, 0, 9000, 12).EndsWith("the design it replaces draws 9000 triangles in 12 draw call(s)")
              && CampusLookRules.FallbackLine("the hall", "the bundle is missing") == "the hall keeps the copy engine's design: the bundle is missing" && CampusLookRules.FallbackLine("the hall", null).EndsWith("no reason given"),
            "campus look placement: a load's lines say the figures, the stand-ins, what the design replaced cost, and why a model was not stood");
    }

    // ---------------------------------------------------------------- the sources

    private static void CampusLookSourceChecks()
    {
        string Bare(string text) => Regex.Replace(text, @"//[^\n]*", " ");
        string look = Bare(ModSource(@"Schools\CampusLook.cs")), rules = Bare(ModSource(@"Schools\CampusLookRules.cs")), director = Bare(ModSource(@"Schools\SchoolsDirector.Look.cs"));
        string tests = Bare(ModSource(@"Schools\SchoolsDirector.LookTests.cs")), campus = Bare(ModSource(@"Schools\SchoolsDirector.Campus.cs")), guide = ModSource(@"Schools\SchoolsGuide.cs");
        Check(look.Length > 0 && rules.Length > 0 && director.Length > 0 && tests.Length > 0, "campus look sources: its four files are there");
        Check(!Regex.IsMatch(rules, @"\bUnityEngine\b|\bRandom\.|\bnew Random\b|DateTime|TickCount|\bTime\.|GetHashCode"), "campus look sources: the rules are game-free and leave nothing to chance");
        string all = look + rules + director + tests;
        Check(!Regex.IsMatch(all, @"void (Update|LateUpdate|FixedUpdate|OnGUI)\(|using System\.Linq|\.Where\(|\.Select\(|\.ToList\(\)|\bTODO\b|NotImplementedException"), "campus look sources: nothing a frame, no LINQ, no stub");
        Check(!Regex.IsMatch(all, @"FindObjectsOfType|FindObjectsByType|GameObject\.Find\b|FindObjectOfType|OverlapSphere\(|RaycastAll\(|GoingPublic|MPClient|MPServer|SaveHook|modData"), "campus look sources: no scene search of its own, nothing sent over the network, nothing saved");
        Check(Regex.Matches(look, @"SceneFinds\.InMemory<Material>").Count == 1 && Regex.Matches(look, @"public static void FindMaterials\(\)[\s\S]*?SceneFinds\.InMemory<Material>").Count == 1,
            "campus look sources: the game's materials are found by name in one search of memory, in FindMaterials");
        string all2 = string.Join("\n", Directory.GetFiles(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Schools")), "*.cs").Select(f => Bare(File.ReadAllText(f))));
        Check(Regex.Matches(all2, @"CampusLook\.FindMaterials\(\)").Count == 1 && Regex.IsMatch(campus, @"private bool AtLoad\(\)[\s\S]*?CampusLook\.FindMaterials\(\);"),
            "campus look sources: only the load, behind the loading screen, looks for the game's materials");
        Check(Regex.IsMatch(look, @"private static LookHandle Stand\([\s\S]*?if \(!_searched\)[\s\S]*?Refused\(") && !Regex.IsMatch(look, @"private static LookHandle Stand\([\s\S]*?FindMaterials\(\);"),
            "campus look sources: a model is never stood by a search in play: without the load's search it is refused");
        Check(Regex.IsMatch(look, @"MainWhy\([\s\S]*?Measure\(c, ref[\s\S]*?CopyGame\.HideLook\(c\)") && look.Contains("CopyGame.ShowLook(h.Over)") && look.Contains("renderersToHide?.Remove(h.Drawn)") && look.Contains("renderersToHide.Add(h.Drawn)"),
            "campus look sources: the main school's own look is hidden by the engine's own path only once the model fits its door, its fade list given the model, and everything given back");
        Check(look.Contains("c.entranceDoors[0].doorTransform.position") && !Regex.IsMatch(look, @"doorTransform\.(position|rotation|localPosition)\s*="), "campus look sources: the building's door is read, never moved");
        Check(look.Contains("CopyLook look = root.AddComponent<CopyLook>();") && look.Contains("look.Fill(box,") && look.Contains("part.cityBuildingController = look;") && look.Contains("LayerHelper.BuildingLayerIndex"),
            "campus look sources: a model is the city's own kind of look: the see-through fade, the map's box, the buildings' layer");
        Check(look.Contains("renderer.renderingLayerMask = _layerMask") && look.Contains("renderer.lightProbeUsage = _probes") && look.Contains("building.GetComponentsInChildren(true, Scratch)"), "campus look sources: a model is lit as the copy engine's own renderers are");
        Check(look.Contains("carve.carveOnlyStationary = true;") && look.Contains("carve.carving = true;") && look.Contains("wall.size = ") && Regex.IsMatch(look, @"if \(solid\)[\s\S]*?AddComponent<BoxCollider>\(\)"),
            "campus look sources: every box is carved out of the walking meshes once, and a block's is solid");
        Check(director.Contains("CampusLookRules.FallbackLine(") && director.Contains("FarDistricts.Include(_lookRoot.transform)") && director.Contains("CampusLook.Forget()") && director.Contains("CampusLook.Take(ref _looks[i])"),
            "campus look sources: a model that is not stood says why in one line, the looks join the district's far squares and are all given back");
        Check(Regex.IsMatch(campus, @"_copies\.Add\(own \? null : SchoolsGame\.Plot\(_plots\[i\]\)\);") && campus.Contains("LookStands(i + 1)") && campus.Contains("cut = StandLooks(main) || SchoolsGame.Cut(main);")
              && campus.Contains("LeaveLook();"), "campus look sources: the copy engine's designs are only the fallback, and the main school keeps the old cut when its model does not stand");
        Check(director.Contains("Stopwatch") && director.Contains("triangles in \" + draws + \" draw call(s)") && look.Contains("h.Ms = (Stopwatch.GetTimestamp()") && look.Contains("Measure(c, ref replacedTriangles, ref replacedDraws)"),
            "campus look sources: the load measures and logs the triangles, the draw calls and the time, against what the design it replaces drew");
        foreach (string line in new[] { "look", "compare" })
        {
            Check(tests.Contains("\"Schools check (" + line + ")") && guide.Contains("\"Schools check (" + line + ")\""), "campus look sources: the test '" + line + "' writes its line and is in Options > Tests");
        }
        Check(tests.Contains("Done(\"Schools check (look): \" + t, pass, name)") && tests.Contains("Done(\"Schools check (compare): \"") && Regex.Matches(tests, @"CampusLook\.Judge\(").Count == 1,
            "campus look sources: each test ends in PASS or FAIL through the one Done, with the figures");
    }

    // ---------------------------------------------------------------- the texts

    private static void CampusLookTextChecks()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("campus look texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < 3 && l < words.Length; l++)
        {
            foreach (string key in new[] { "reallife_schools_test_look", "reallife_schools_test_compare" })
            {
                Check(words[l].TryGetValue(key, out string text) && text.Length > 10 && text.Length <= 240 && !text.Contains('!') && !text.Contains((char)0x2014) && !text.Contains((char)0x2013),
                    "campus look texts: " + key + " is in " + codes[l] + ", short, without an exclamation mark or dash");
            }
        }
        var fragments = new Dictionary<string, string>
        {
            { "en", "Birchwood School has its own look." }, { "pt", "A Escola Birchwood ganhou visual próprio." }, { "zh-cn", "伯奇伍德学校有了自己的外观。" }, { "de", "Die Birchwood School hat ihr eigenes Aussehen." },
            { "fr", "La Birchwood School a son propre aspect." }, { "es", "La Birchwood School tiene su propio aspecto." }, { "ru", "У школы Birchwood теперь собственный облик." },
            { "nl", "De Birchwood School heeft een eigen uiterlijk." }, { "tr", "Birchwood School'un kendi görünümü var." }
        };
        for (int l = 0; l < codes.Length; l++)
        {
            Check(fragments.TryGetValue(codes[l], out string wanted) && WhatsNewHas(words[l], wanted), "campus look texts: What's new has the campus's own look in " + codes[l] + " (any row)");
            int times = words[l].Where(r => r.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !r.Key.EndsWith("_title", StringComparison.Ordinal)).Sum(r => Regex.Matches(r.Value, Regex.Escape(fragments[codes[l]])).Count);
            Check(times == 1, "campus look texts: its What's new line is in " + codes[l] + " once, never repeated: " + times);
            string line = words[l].Where(r => r.Key.StartsWith("reallife_changes_", StringComparison.Ordinal)).Select(r => r.Value).FirstOrDefault(v => v.Contains(fragments[codes[l]])) ?? string.Empty;
            int at = line.IndexOf(fragments[codes[l]], StringComparison.Ordinal);
            string mine = at >= 0 ? line.Substring(at) : string.Empty;
            Check(!mine.Contains('!') && !mine.Contains((char)0x2014) && !mine.Contains((char)0x2013) && !mine.Contains(" - "), "campus look texts: its What's new line has no exclamation mark or dash in " + codes[l]);
        }
        Check(codes.Length == 9, "campus look texts: What's new is read in all nine languages: " + codes.Length);
    }
}
