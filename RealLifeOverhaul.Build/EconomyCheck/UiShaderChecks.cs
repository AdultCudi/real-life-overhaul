using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// A player on a Mac (12 Nov 2026, version 1.0.0): every word of the RLO interface was a pink block, while the game's own
// text and the minimap's drew. Their Player.log names it: "Desired shader compiler platform 14 is not available in shader
// blob" twice, then "Shader TextMeshPro/Mobile/Distance Field shader is not supported on this GPU". Platform 14 is Metal.
// The font bundle (reallife_ui) is built for Windows, and its three faces' materials brought TextMeshPro's two distance
// field shaders along, compiled for Direct3D alone. Now a face never keeps the shader it came with: as the bundle is read
// each face's material is given a text shader of the running game's, on every system alike, a face whose shader does not
// draw on the graphics device is not written with, and one line of Player.log says what was found (Ui/UiShaderRules.cs,
// Ui/UiFonts.cs, Ui/UiDirector.cs). Held here: which shader is taken, the line, and the sources that carry it out.
internal static partial class Program
{
    private static void UiShaders()
    {
        UiShaderPickChecks();
        UiShaderLineChecks();
        UiShaderSourceChecks();
        UiShaderTextChecks();
    }

    private const string UiShaderUnlit = "TextMeshPro/SRP/TMP_SDF-HDRP UNLIT";   // the game's own shader for text in the world: never for the interface
    private const string UiShaderError = "Hidden/InternalErrorShader";           // what a material reads as once its shader has gone: the pink one

    private static void UiShaderPickChecks()
    {
        const string T = UiShaderRules.Text, S = UiShaderRules.TextSmall;
        Check(UiShaderRules.ForText(T) && UiShaderRules.ForText(S) && !UiShaderRules.ForText(UiShaderUnlit) && !UiShaderRules.ForText(UiShaderError)
              && !UiShaderRules.ForText("TextMeshPro/Sprite") && !UiShaderRules.ForText("UI/Default") && !UiShaderRules.ForText(T.ToLowerInvariant())
              && !UiShaderRules.ForText(null) && !UiShaderRules.ForText(string.Empty),
            "ui shaders: only TextMeshPro's two distance field shaders for an interface draw the kit's letters");
        Check(UiShaderRules.FromDefaultFont == 0 && UiShaderRules.FromGameFont == 1 && UiShaderRules.ByName == 2 && UiShaderRules.ByNameSmall == 3 && UiShaderRules.Places == 4,
            "ui shaders: the places a text shader is taken from are asked in one order: TextMeshPro's default font, the game's interface font, by name, by the lighter name");
        Check(UiShaderRules.Pick(new[] { T, T, T, S }, new[] { true, true, true, true }) == UiShaderRules.FromDefaultFont
              && UiShaderRules.Pick(new[] { S, T, T, S }, new[] { true, true, true, true }) == UiShaderRules.FromDefaultFont,
            "ui shaders: with every place offering one, the default font's is taken: the game holds it all session, so it never goes with a scene");
        // The Mac of the report as the fonts are read. The game's default font is NotoSans-Regular SDF, whose material draws with the game's own
        // TextMeshPro/Distance Field (its resources.assets and globalgamemanagers.assets, Build 3682); the interface font is not found yet, and by
        // name the engine may hand back the bundle's own copies, which do not draw there.
        Check(UiShaderRules.Pick(new[] { T, null, T, S }, new[] { true, false, false, false }) == UiShaderRules.FromDefaultFont,
            "ui shaders: on the Mac of the report the faces are given the game's shader at once, though the bundle's own copies are what the names find");
        Check(UiShaderRules.Pick(new[] { null, T, T, S }, new[] { false, true, false, false }) == UiShaderRules.FromGameFont,
            "ui shaders: without a default font the game's interface font's shader is taken");
        Check(UiShaderRules.Pick(new[] { UiShaderUnlit, UiShaderUnlit, T, S }, new[] { true, true, true, true }) == UiShaderRules.ByName,
            "ui shaders: a font whose shader is the game's one for text in the world is passed over");
        Check(UiShaderRules.Pick(new[] { null, null, T, S }, new[] { false, false, false, true }) == UiShaderRules.ByNameSmall,
            "ui shaders: a shader that does not draw on this graphics device is never taken, the next place is");
        Check(UiShaderRules.Pick(new[] { null, null, T, S }, new[] { false, false, false, false }) == -1
              && UiShaderRules.Pick(new[] { UiShaderError, UiShaderError, UiShaderError, UiShaderError }, new[] { true, true, true, true }) == -1
              && UiShaderRules.Pick(null, null) == -1 && UiShaderRules.Pick(new string[0], new bool[0]) == -1 && UiShaderRules.Pick(new[] { T }, new bool[0]) == -1,
            "ui shaders: with no text shader that draws, and for the engine's error shader, none is taken: the faces are left out");
        bool only = true;
        string[] names = { null, T, S, UiShaderUnlit, UiShaderError };
        for (int a = 0; a < names.Length; a++)
        {
            for (int b = 0; b < names.Length; b++)
            {
                for (int draws = 0; draws < 4; draws++)
                {
                    var given = new[] { names[a], names[b] };
                    var drawn = new[] { (draws & 1) != 0, (draws & 2) != 0 };
                    int pick = UiShaderRules.Pick(given, drawn);
                    only &= pick < 0 ? !(drawn[0] && UiShaderRules.ForText(given[0])) && !(drawn[1] && UiShaderRules.ForText(given[1]))
                        : drawn[pick] && UiShaderRules.ForText(given[pick]) && (pick == 0 || !(drawn[0] && UiShaderRules.ForText(given[0])));
                }
            }
        }
        Check(only, "ui shaders: over every pair of places, what is taken is a text shader that draws, the first such, and none is taken only when there is none");
    }

    private static void UiShaderLineChecks()
    {
        string with = "the game's '" + UiShaderRules.Text + "', " + UiShaderRules.From(UiShaderRules.FromDefaultFont, "NotoSans-Regular SDF");
        string mac = UiShaderRules.Line(UiShaderRules.Bundle, 3, 1, 1, 1, 0, null, with, "Metal", "Apple M4 Pro", "Mac OS X 15.1.0", 3682);
        Check(mac.StartsWith("Shaders check (reallife_ui): 3 materials, 1 shaders, 1 not supported on Metal, 1 repointed to the game's own, 0 left: PASS. ", StringComparison.Ordinal),
            "ui shaders: the line of the report's Mac once mended says its three materials, their one shader that Metal does not run, the game's in its place, none left, PASS");
        Check(mac.Contains("Text shader in use: the game's 'TextMeshPro/Distance Field', from TextMeshPro's default font in the game, NotoSans-Regular SDF.")
              && mac.Contains("Graphics: Metal (Apple M4 Pro).") && mac.Contains("System: Mac OS X 15.1.0.") && mac.EndsWith("Game: Build 3682.", StringComparison.Ordinal),
            "ui shaders: the line names what the text is drawn with and where it came from, the graphics device, the system and the game's build");
        string pc = UiShaderRules.Line(UiShaderRules.Bundle, 3, 1, 0, 1, 0, null, with, "Direct3D11", "NVIDIA GeForce RTX 5070 Ti", "Windows 11  (10.0.26300) 64bit", 3682);
        Check(pc.StartsWith("Shaders check (reallife_ui): 3 materials, 1 shaders, 0 not supported on Direct3D11, 1 repointed to the game's own, 0 left: PASS. ", StringComparison.Ordinal),
            "ui shaders: on Windows the faces' shader is replaced by the game's own all the same, so every system goes one way");
        const string Names = "Overpass-Regular SDF Material, Overpass-SemiBold SDF Material, Overpass-Bold SDF Material";
        string fail = UiShaderRules.Line(UiShaderRules.Bundle, 3, 1, 1, 0, 3, Names, "none of the game's that draws here", "Metal", "Apple M4 Pro", "Mac OS X 15.1.0", 3682);
        Check(fail.StartsWith("Shaders check (reallife_ui): 3 materials, 1 shaders, 1 not supported on Metal, 0 repointed to the game's own, 3 left: FAIL (" + Names + ": ", StringComparison.Ordinal)
              && fail.Contains("the game's font stands in for these faces") && !fail.Contains("PASS"),
            "ui shaders: faces left without a shader that draws are a FAIL that names their materials and says the game's font stands in");
        string none = UiShaderRules.Line(UiShaderRules.Bundle, 0, 0, 0, 0, 0, null, "the game's own", "Metal", "Apple M4 Pro", "Mac OS X 15.1.0", 3682);
        Check(none.Contains("0 materials, 0 shaders, 0 not supported on Metal, 0 repointed to the game's own, 0 left: PASS"), "ui shaders: a bundle that was not read leaves nothing on a shader that does not draw");
        Check(mac.IndexOf('\n') < 0 && fail.IndexOf('\n') < 0 && mac.Length < 500 && fail.Length < 700, "ui shaders: the check is one line of the log");
        Check(UiShaderRules.From(UiShaderRules.FromGameFont, "Rubik-Medium SDF") == "from the game's interface font, Rubik-Medium SDF"
              && UiShaderRules.From(UiShaderRules.ByName, null) == "found in the game by name" && UiShaderRules.From(UiShaderRules.ByNameSmall, null) == "found in the game by name"
              && UiShaderRules.From(-1, null) == "none found" && UiShaderRules.From(UiShaderRules.FromDefaultFont, null).EndsWith("?", StringComparison.Ordinal),
            "ui shaders: each place the shader can come from has its words, a font without a name too");
    }

    private static void UiShaderSourceChecks()
    {
        string fonts = ModSource(@"Ui\UiFonts.cs"), director = ModSource(@"Ui\UiDirector.cs"), slots = ModSource(@"Ui\UiView.Slots.cs");
        Check(fonts.Length > 0 && director.Length > 0 && slots.Length > 0, "ui shaders: the fonts', the director's and the view's sources are read");
        string take = MemberText(fonts, "private static void Take(UnityEngine.Object[] found)");
        Check(take.Contains("Read[at] = f;") && take.Contains("Built[at] = came != null ? came.shader : null;") && take.Contains("Mend();") && !take.Contains("Assets["),
            "ui shaders: a face read from the bundle is kept aside with what its material came with, and mended before it is written with");
        MatchCollection writes = Regex.Matches(fonts, @"\bAssets\[\w+\]\s*=(?!=)");
        string mend = MemberText(fonts, "private static void Mend()");
        Check(writes.Count == 1 && mend.Contains("Assets[i] = draws ? f : null;") && mend.Contains("bool draws = Draws(m);") && mend.Contains("Point(m, game);"),
            "ui shaders: the one place a face is given out is the mend, and only while its shader draws here (" + writes.Count + " place(s) write the faces)");
        string draws = MemberText(fonts, "private static bool Draws(Material m)");
        Check(draws.Contains("s.isSupported") && draws.Contains("UiShaderRules.ForText(s.name)"),
            "ui shaders: a material draws when its shader runs on this graphics device and is one of the text shaders, so the engine's error shader never passes");
        string point = MemberText(fonts, "private static void Point(Material m, Shader shader)");
        Check(point.Contains("m.shader = shader;") && point.Contains("string[] keywords = m.shaderKeywords;") && point.Contains("m.shaderKeywords = keywords;")
              && point.Contains("int queue = m.renderQueue;") && point.Contains("m.renderQueue = queue;") && !point.Contains("new Material"),
            "ui shaders: a material given another shader keeps its properties, its keywords and its render queue, and no material is made for it");
        string shader = MemberText(fonts, "private static Shader TextShader(out string from)");
        Check(shader.Contains("found[UiShaderRules.FromDefaultFont] = Of(standard);") && shader.Contains("found[UiShaderRules.FromGameFont] = Of(_fallback);")
              && shader.Contains("found[UiShaderRules.ByName] = Shader.Find(UiShaderRules.Text);") && shader.Contains("found[UiShaderRules.ByNameSmall] = Shader.Find(UiShaderRules.TextSmall);")
              && shader.Contains("draws[i] = found[i] != null && found[i].isSupported;") && shader.Contains("UiShaderRules.Pick(names, draws)"),
            "ui shaders: the shader is taken by the rules from the four places, each asked whether it draws on this graphics device");
        Check(Occurs(fonts, "Mend();") == 3 && MemberText(fonts, "public static void SetFallback(TMP_FontAsset gameFont)").Contains("Mend();")
              && MemberText(fonts, "public static bool Watch()").Contains("Mend();") && !Regex.IsMatch(fonts, @"\bvoid\s+(Update|LateUpdate|FixedUpdate)\s*\("),
            "ui shaders: the mend runs as the faces are read, when the game's font is found and when the shader in use has gone, never a frame");
        Check(!Regex.IsMatch(fonts, @"RuntimePlatform|Application\.platform|operatingSystemFamily|graphicsDeviceType\s*[!=]=") && !Regex.IsMatch(mend, @"\bif\s*\([^)]*(Windows|OSX|Metal|Direct3D)"),
            "ui shaders: nothing in the fonts asks which system this is, so Windows goes the way a Mac does");
        string watch = MemberText(fonts, "public static bool Watch()");
        Check(watch.Contains("(object)_with == null || _with != null") && !Regex.IsMatch(watch, @"\bnew\b|\.name\b|\+\s*"""),
            "ui shaders: the watch for a shader that went with its scene is two null tests and makes nothing");
        string forStyle = MemberText(fonts, "public static TMP_FontAsset For(UiStyle style)");
        Check(forStyle.Contains("return best >= 0 && Assets[best] != null ? Assets[best] : _fallback;"), "ui shaders: a style is written with a face given out by the mend, else with the game's font");
        Check(fonts.Contains("public const string BundlePath = \"Bundles/" + UiShaderRules.Bundle + "\";"), "ui shaders: the check's line names the bundle the fonts are read from");
        string slow = MemberText(director, "private void Slow()"), look = MemberText(director, "private static void ShadersLook()");
        Check(slow.Contains("ShadersLook();") && slow.IndexOf("ShadersLook();", StringComparison.Ordinal) < slow.IndexOf("bool city =", StringComparison.Ordinal),
            "ui shaders: the director looks at the check each second, in the main menu too");
        Check(look.Contains("UiFonts.Watch()") && look.Contains("if (!UiFonts.ShadersNews)") && look.Contains("UiFonts.ShadersLine(GameBuild.Running)")
              && look.Contains("Meta.Dev.Always(Logger, line);") && look.Contains("Logger?.Warn(line);"),
            "ui shaders: the line is written once, whatever Developer diagnostics says, and as a warning when a face is left out");
        string bind = MemberText(slots, "private void BindText(TextMeshProUGUI t, ref UiItem it)");
        Check(bind.Contains("TMP_FontAsset font = UiFonts.For(it.Style);") && bind.Contains("UiFonts.OnLight(t.font) : t.font != null ? t.font.material : null;") && !Regex.IsMatch(slots, @"Shader\.Find|new Material"),
            "ui shaders: the kit's texts take their face and its material from the fonts alone");
        // The rule must be able to fail: the sources as they were, a face given out as it was read.
        string before = fonts.Replace("Read[at] = f;", "Assets[at] = f;");
        Check(Regex.Matches(before, @"\bAssets\[\w+\]\s*=(?!=)").Count == 2, "ui shaders: a face given out as it was read, with the shader the bundle brought, is found");
    }

    private static void UiShaderTextChecks()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en != null && WhatsNewHas(en, "Fixed: on a Mac, the RLO interface's text was drawn as pink blocks."), "ui shaders texts: What's new tells of the fix in some row");
    }
}
