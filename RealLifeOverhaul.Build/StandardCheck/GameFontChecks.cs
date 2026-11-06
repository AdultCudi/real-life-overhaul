using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-017: the game's font is never found by a scene search in play. A player's session
// (26 Oct 2026) logged "a text of the game's interface being looked for (its font): 1 found in 97.1 ms, while the game was
// being played: FAIL", a 139 ms frame, because GameFont.Asset searched the scene whenever the font it held had gone. The one
// scene search lives in GameFont.FromScene; the Asset getter reaches it only behind SceneFinds.Black, after the look among
// the assets in memory by the font's name; and no other file of Core/GameUi searches the scene for a text.
internal static partial class Program
{
    private const string FontFile = ScriptsDir + "/Core/GameUi/GameFont.cs";

    private static readonly Regex FontSceneSearch = new Regex(@"SceneFinds\s*\.\s*(?:Any|All)\b", RegexOptions.Compiled);

    private static void GameFontChecks()
    {
        const string rule = "RLO-PERF-017";
        Examine(rule, FontFile);
        SourceFile font = null;
        foreach (SourceFile f in Scripts)
        {
            if (f.Rel == FontFile)
            {
                font = f;
            }
        }
        if (font == null)
        {
            Hit(rule, FontFile, 0, "the file is gone: put this check right");
            return;
        }
        Match from = Regex.Match(font.CodeText, @"void\s+FromScene\s*\([^;{}]*\)\s*\{");
        Match asset = Regex.Match(font.CodeText, @"TMP_FontAsset\s+Asset\s*\{");
        if (!from.Success || !font.Block(from.Index, out int fromOpen, out int fromClose))
        {
            Hit(rule, FontFile, 0, "GameFont.FromScene (the one scene search) is gone: the font must be found by a search in one method only");
            return;
        }
        foreach (Match m in FontSceneSearch.Matches(font.CodeText))
        {
            if (m.Index < fromOpen || m.Index > fromClose)
            {
                Hit(rule, FontFile, font.LineAt(m.Index) + 1, "a scene search outside FromScene: the font is found again by name among the assets, not by a scene search in play");
            }
        }
        if (!asset.Success || !font.Block(asset.Index, out int open, out int close))
        {
            Hit(rule, FontFile, 0, "GameFont.Asset is gone: put this check right");
            return;
        }
        string getter = font.CodeText.Substring(open + 1, close - open - 1);
        int call = getter.IndexOf("FromScene()", StringComparison.Ordinal);
        int guard = getter.IndexOf("SceneFinds.Black", StringComparison.Ordinal);
        if (call >= 0 && (guard < 0 || guard > call))
        {
            Hit(rule, FontFile, font.LineAt(open + 1 + call) + 1, "the getter reaches the scene search without SceneFinds.Black first");
        }
        // No other file of the game's interface helpers searches the scene for a text or a font.
        foreach (SourceFile f in Scripts)
        {
            if (f.Rel.StartsWith(ScriptsDir + "/Core/GameUi/", StringComparison.Ordinal) && f.Rel != FontFile)
            {
                for (int i = 0; i < f.Code.Length; i++)
                {
                    if (FontSceneSearch.IsMatch(f.Code[i]) && Regex.IsMatch(f.Code[i], @"TextMeshPro|TMP_Text|TMP_FontAsset"))
                    {
                        Hit(rule, f.Rel, i + 1, "a scene search for a text or a font: " + f.Code[i].Trim());
                    }
                }
            }
        }
    }
}
