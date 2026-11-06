using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Uptown;

// Uptown's road and ground surfaces at one height over one place (Uptown/SurfaceRules.cs, GroundGame.Art.cs, GroundGame.Surfaces.cs
// and GroundDirector.Surfaces.cs): a player's #general post of 26 Oct 2026 said the road flickered on 7th Street (at 1016, x -133.0
// z 722.5, and at 1001, x -134.1 z 515.2). The game's block grounds lie at 0.05518 m and its street and avenue pavements at 0.05516
// and 0.055 m, so a ground over a pavement fights it for the pixels. The plan as the load lays it (each kind at its own height, the
// backdrop's block grounds put away) has no two overlapping surfaces within 1.5 mm of each other, nor within 2 mm; the plan at the
// game's own heights has them, and so does a backdrop block ground left at its own height over 7th Street (the rule sees what
// the post showed); the first block's ground is laid like every block's; the heights, the sources that set them and hide the
// backdrop's grounds, the check's line and its test, and the texts in three languages with What's new's line in all nine.
internal static partial class Program
{
    // The backdrop's block grounds beside 7th Street (level6, Build 3682, navlab band_bounds at five decimals): their rectangle and top.
    private static readonly float[][] BackdropGrounds =
    {
        new[] { -205.721f, 478.149f, -135.579f, 598.151f, 0.05518f },    // (11), the first block west of 7th Street (the post's second place)
        new[] { -205.671f, 624.899f, -135.529f, 744.901f, 0.05518f },    // (19), row 2 west of 7th Street (the post's first)
        new[] { -118.471f, 478.109f, -48.329f, 598.231f, 0.06518f },     // (10), the first block east of it: 1 cm up, as a tenth of them are
    };

    private static readonly float[] ReportedX = { -133.0f, -134.1f };
    private static readonly float[] ReportedZ = { 722.5f, 515.2f };

    private static void UptownSurfaceChecks(string scripts)
    {
        UptownSurfacePlan();
        UptownSurfaceOld();
        UptownSurfaceSources(scripts);
        UptownSurfaceTexts(scripts);
    }

    private static int SurfaceOverlaps(List<SurfacePiece> pieces, float tolerance, List<SurfaceSpot> worst)
    {
        return SurfaceRules.Overlaps(pieces, tolerance, worst, int.MaxValue, out _);
    }

    // The plan as the load lays it.
    private static void UptownSurfacePlan()
    {
        var places = new List<ModulePlace>();
        LayoutPlans.Modules(places);
        Check(Near(SurfaceRules.Lift(ModuleKind.Street), 0f, 1e-6f) && Near(SurfaceRules.Lift(ModuleKind.Crossing), 0f, 1e-6f) && Near(SurfaceRules.Lift(ModuleKind.Avenue), 0.003f, 1e-6f)
              && Near(SurfaceRules.Lift(ModuleKind.Ground), -0.006f, 1e-6f) && SurfaceRules.Coplanar > 0.001f && SurfaceRules.Coplanar < SurfaceRules.AvenueLift,
            "uptown surfaces: streets and crossings stay as the game has them, avenues 3 mm up, block grounds 6 mm down, and a millimetre and a half is what counts as one height");
        var lifted = new List<SurfacePiece>();
        SurfaceRules.PlanPieces(lifted, true);
        var worst = new List<SurfaceSpot>();
        int near = SurfaceOverlaps(lifted, SurfaceRules.Coplanar, worst);
        Check(lifted.Count == places.Count + 17 && near == 0,
            "uptown surfaces: the plan's " + lifted.Count + " pieces (every module and the game's own at 1st Avenue) have no two surfaces within 1.5 mm of each other over one place ("
            + near + " pairs" + (near > 0 ? ", the worst " + worst[0].A + " with " + worst[0].B + " at " + worst[0].X.ToString("F1") + ", " + worst[0].Z.ToString("F1") : string.Empty) + ")");
        Check(SurfaceOverlaps(lifted, 0.002f, worst) == 0, "uptown surfaces: ... nor within 2 mm (the least is the game's own street against its crossing, 2.04 mm)");
        // Every ground and every pavement of the plan meet some other surface: the check looks at real overlaps (the avenue's 0.25 m over its crossing, the ground's 5 cm under an avenue's pavement).
        var grid = new List<SurfacePiece>();
        SurfaceRules.PlanPieces(grid, false);
        var found = new List<SurfaceSpot>();
        int old = SurfaceOverlaps(grid, SurfaceRules.Coplanar, found);
        Check(old > 100 && found.Count > 0, "uptown surfaces: made to fail once: the plan at the game's own heights has " + old + " pairs of surfaces at one height (the rule finds what the game's own layout leaves)");
        Check(found.Any(s => s.A.StartsWith("avenue", StringComparison.Ordinal) && s.B.StartsWith("crossing", StringComparison.Ordinal) || s.B.StartsWith("avenue", StringComparison.Ordinal) && s.A.StartsWith("crossing", StringComparison.Ordinal)),
            "uptown surfaces: ... among them an avenue's length over its crossing's asphalt, 0.25 m each side");
        Check(found.Any(s => s.A.StartsWith("ground", StringComparison.Ordinal) || s.B.StartsWith("ground", StringComparison.Ordinal)),
            "uptown surfaces: ... and a block's ground over a pavement it reaches onto by 5 cm");
        // The first block's ground is laid as every block's, one to a block of the grid, none off it.
        var grounds = places.Where(m => m.Kind == ModuleKind.Ground && !m.Scenery).ToList();
        Check(grounds.Count == LayoutRules.Rows * LayoutRules.Columns && grounds.Count(m => LayoutRules.IsFirst(m.Row, m.Street + LayoutRules.FirstColumn)) == LayoutRules.FirstColumns,
            "uptown surfaces: a ground to each of the band's " + grounds.Count + " blocks, the first block's eight among them, each on the grid's own place");
        // The plates lie inside their module's own size, each kind's pivot its middle.
        bool inside = true;
        foreach (ModuleKind kind in new[] { ModuleKind.Street, ModuleKind.Avenue, ModuleKind.Crossing, ModuleKind.Ground })
        {
            SurfacePiece piece = SurfaceRules.Place(kind, "k", 0f, 0f, 0f);
            float halfX = kind == ModuleKind.Street ? LayoutRules.StreetHalf : kind == ModuleKind.Avenue ? LayoutRules.AvenueModule * 0.5f + 0.05f
                : kind == ModuleKind.Crossing ? LayoutRules.CrossingHalfX + 0.3f : LayoutRules.StreetPitch * 0.5f - LayoutRules.StreetHalf + 0.01f;
            float halfZ = kind == ModuleKind.Street ? LayoutRules.StreetModule * 0.5f + 0.02f : kind == ModuleKind.Avenue ? LayoutRules.AvenueHalf
                : kind == ModuleKind.Crossing ? LayoutRules.CrossingHalfZ + 0.02f : LayoutRules.AvenuePitch * 0.5f - LayoutRules.AvenueHalf + 0.1f;
            inside &= piece.Plates.Count > 0 && -halfX <= piece.X0 && piece.X1 <= halfX && -halfZ <= piece.Z0 && piece.Z1 <= halfZ;
        }
        Check(inside, "uptown surfaces: each kind's flat faces lie inside its module's own size");
    }

    // The backdrop's own block grounds at their old heights, and the post's two places.
    private static void UptownSurfaceOld()
    {
        var lifted = new List<SurfacePiece>();
        SurfaceRules.PlanPieces(lifted, true);
        var worst = new List<SurfaceSpot>();
        var withOld = new List<SurfacePiece>(lifted);
        foreach (float[] g in BackdropGrounds.Take(2))
        {
            withOld.Add(SurfaceRules.Flat("backdrop ground", g[0], g[1], g[2], g[3], g[4]));
        }
        int pairs = SurfaceOverlaps(withOld, SurfaceRules.Coplanar, worst);
        Check(pairs > 0 && worst.All(s => s.A == "backdrop ground" || s.B == "backdrop ground"),
            "uptown surfaces: made to fail once: the backdrop's two block grounds beside 7th Street left at 0.05518 m fight the street's pavement (" + pairs + " pairs, as the post showed)");
        for (int i = 0; i < ReportedX.Length; i++)
        {
            int count = 0;
            for (int j = 0; j < worst.Count; j++)
            {
                count += SurfaceRules.Near(worst[j].X, worst[j].Z, ReportedX[i], ReportedZ[i]) ? 1 : 0;
            }
            var none = new List<SurfaceSpot>();
            SurfaceRules.Overlaps(lifted, SurfaceRules.Coplanar, none, int.MaxValue, out _);
            Check(count > 0 && none.Count(s => SurfaceRules.Near(s.X, s.Z, ReportedX[i], ReportedZ[i])) == 0,
                "uptown surfaces: the post's place " + (i + 1) + " (" + ReportedX[i].ToString("F1") + ", " + ReportedZ[i].ToString("F1") + "): " + count + " pairs within 60 m with the backdrop's ground left, none in the plan as laid");
        }
        // A backdrop ground 1 cm up (a tenth of them stand so) does not fight: the rule is about height, not about the grid.
        var up = new List<SurfacePiece>(lifted) { SurfaceRules.Flat("backdrop ground", BackdropGrounds[2][0], BackdropGrounds[2][1], BackdropGrounds[2][2], BackdropGrounds[2][3], BackdropGrounds[2][4]) };
        Check(SurfaceOverlaps(up, SurfaceRules.Coplanar, new List<SurfaceSpot>()) == 0, "uptown surfaces: a backdrop ground 1 cm up is no fight, the rule is about height");
        // The measured grounds sit where the plan's blocks are, so the load's hiding finds each by its middle.
        bool middles = true;
        foreach (float[] g in BackdropGrounds)
        {
            float cx = (g[0] + g[2]) * 0.5f, cz = (g[1] + g[3]) * 0.5f;
            middles &= LayoutRules.OnGround(cx, cz);
        }
        Check(middles, "uptown surfaces: each backdrop ground's middle is on the band's ground, so the load's hiding finds it");
    }

    // Over the sources: the load sets the heights, puts the backdrop's grounds away and checks, the test is on the page.
    private static void UptownSurfaceSources(string scripts)
    {
        string Read(string file)
        {
            string path = Path.Combine(scripts, "Uptown", file);
            return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
        }
        string art = Read("GroundGame.Art.cs"), surfaces = Read("GroundGame.Surfaces.cs"), director = Read("GroundDirector.cs"), check = Read("GroundDirector.Surfaces.cs"), guide = Read("GroundGuide.cs");
        string paving = Read("GroundGame.Paving.cs"), rules = Read("SurfaceRules.cs");
        string copy = Body(art, "public static int CopyArt(RoadBuild build)");
        Check(copy.Contains("SurfaceRules.Lift(place.Kind)") && !copy.Contains("template.transform.position.y") && !Regex.IsMatch(art, @"\bKept\(")
              && copy.Contains("HideKept(place.X, place.Z)") && copy.IndexOf("GroundsLaid++;", StringComparison.Ordinal) < copy.IndexOf("HideKept(", StringComparison.Ordinal),
            "uptown surfaces: every copy is set at its kind's height, never the template's, and each ground laid puts the backdrop's own away (no ground skipped for it)");
        string hide = Body(art, "private static void HideKept(float x, float z)");
        Check(hide.Contains("Hidden.Add(plane.gameObject)") && hide.Contains("SetActive(false)") && hide.Contains("BlockWide * 0.5f") && hide.Contains("BlockDeep * 0.5f")
              && Body(art, "private static void DropArt()").Contains("GroundsHidden = 0;"),
            "uptown surfaces: the backdrop's grounds put away are the ones whose middle lies on the block, shown again with the rest of the backdrop, counted");
        Check(Body(paving, "private static void UnpaveKept()").Contains("activeInHierarchy") && !rules.Contains("UnityEngine"),
            "uptown surfaces: the ring is laid as asphalt only on a backdrop ground still shown, and the rules know no Unity type");
        Check(surfaces.Contains("SurfaceRules.GamePieces(into)") && surfaces.Contains("KeptPlanes") && surfaces.Contains("copy.activeInHierarchy") && surfaces.Contains("drawn.bounds"),
            "uptown surfaces: the check reads the copies where they stand, the game's own at 1st Avenue and every backdrop ground still shown");
        Check(director.Contains("SurfacesAtLoad();") && director.IndexOf("if (Meta.Dev.Verbose)\n            {\n                SurfacesAtLoad();", StringComparison.Ordinal) > 0
              && check.Contains("\"Uptown check (road surfaces\"") && check.Contains("PASS") && check.Contains("FAIL") && check.Contains("WalkGame.Warp(") && check.Contains("TestRefusal.OnFootOnly")
              && check.Contains("-133.0f") && check.Contains("722.5f") && check.Contains("-134.1f") && check.Contains("515.2f"),
            "uptown surfaces: the load writes \"Uptown check (road surfaces)\" with Developer diagnostics on, and the test puts the player at both places on foot");
        Check(guide.Contains("\"reallife_uptown_ground_test_surfaces\", GroundDirector.TestSurfaces"), "uptown surfaces: the test is under Options > Tests");
        Check(!Regex.IsMatch(surfaces + check + rules, @"\b(Update|LateUpdate|FixedUpdate|FindObjectsByType|FindObjectOfType|GetComponentsInChildren)\b") && !Regex.IsMatch(surfaces + check + rules, @"\bLinq\b|\.Select\(|\.Where\("),
            "uptown surfaces: nothing per frame, no search of the scene, no LINQ");
    }

    // The texts: the three languages' keys, and the one line of What's new in all nine, once each, with its fix mark and no double space.
    private static void UptownSurfaceTexts(string scripts)
    {
        string locales = Path.GetFullPath(Path.Combine(scripts, @"..\Locales"));
        string[] keys = { "reallife_uptown_ground_test_surfaces", "reallife_uptown_ground_surfaces_clear", "reallife_uptown_ground_surfaces_over" };
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language + ".json")));
            foreach (string key in keys)
            {
                Check(doc.RootElement.TryGetProperty(key, out JsonElement value) && value.GetString().Length > 0 && !value.GetString().Contains("  ")
                      && (key == keys[0] || value.GetString().Contains("{where}")) && (key != keys[2] || value.GetString().Contains("{count}")),
                    "uptown surfaces: " + key + " is in " + language + ".json with its placeholders and no double space");
            }
        }
        var line = new Dictionary<string, (string mark, string phrase)>
        {
            { "en", ("Fixed: ", "the road in Uptown flickered in places") }, { "pt", ("Corrigido: ", "a rua piscava em alguns pontos do Uptown") },
            { "zh-cn", ("已修复：", "上城有些地方的路面会闪烁") }, { "de", ("Behoben: ", "Die Straße in Uptown flackerte an einigen Stellen") },
            { "fr", ("Corrigé : ", "la route d'Uptown clignotait par endroits") }, { "es", ("Corregido: ", "la calle de Uptown parpadeaba en algunos puntos") },
            { "ru", ("Исправлено: ", "дорога в Uptown местами мерцала") }, { "nl", ("Opgelost: ", "de weg in Uptown flikkerde op sommige plekken") },
            { "tr", ("Düzeltildi: ", "Uptown'daki yol bazı yerlerde titriyordu") }
        };
        foreach (KeyValuePair<string, (string mark, string phrase)> l in line)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, l.Key + ".json")));
            // Any row of What's new, never the next row alone: the version step moves the next row's lines into the version's own.
            var rows = doc.RootElement.EnumerateObject().Where(p => p.Name.StartsWith("reallife_changes_", StringComparison.Ordinal) && !p.Name.EndsWith("_title", StringComparison.Ordinal))
                          .Select(p => p.Value.GetString()).ToList();
            string row = rows.FirstOrDefault(r => r.Contains(l.Value.phrase));
            int at = row != null ? row.IndexOf(l.Value.phrase, StringComparison.Ordinal) : -1;
            Check(rows.Count(r => r.Contains(l.Value.phrase)) == 1 && at >= l.Value.mark.Length && row.Substring(at - l.Value.mark.Length, l.Value.mark.Length) == l.Value.mark
                  && !row.Contains("  ") && Regex.Matches(row, Regex.Escape(l.Value.phrase)).Count == 1,
                "uptown surfaces: What's new says it once in " + l.Key + ", with " + l.Value.mark.Trim() + ", and no double space");
        }
    }
}
