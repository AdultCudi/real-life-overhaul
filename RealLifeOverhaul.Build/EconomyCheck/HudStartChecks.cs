using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

// The owner's play of 3 Nov 2026 (build 00988a180): the game's own top bar all session and no line from the HUD. The
// interface's fonts are read behind the loading screen by Core/LoadHold, which holds it at most two seconds and then
// forgets its pieces. That load took 2051 ms with the fonts still reading, so UiFonts.Step was never called again, the
// fonts were never ready, and HudLive.Begin returned at its fonts test every second without a word (the load before
// took 1993 ms, just inside the limit). Now the director steps the fonts on each second after the hold, and a HUD that
// has not stood up ten seconds into a city says in Player.log what it waits for.
internal static partial class Program
{
    private static void HudStart()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            sources[file.Substring(scripts.Length + 1)] = File.ReadAllText(file);
        }
        const string Director = @"Ui\UiDirector.cs";
        int pieces = HoldPieces(sources, out string lost);
        Check(pieces >= 2 && lost == null && sources.ContainsKey(Director),
            "load hold: each of the " + pieces + " pieces read behind the loading screen is finished after it when the hold ends first (its end goes on, or its step is called elsewhere)"
            + (lost != null ? "; not: " + lost : string.Empty));
        Check(MemberText(sources.TryGetValue(Director, out string director) ? director : string.Empty, "private void Slow()").Contains("UiFonts.Step()"),
            "load hold: the director's second steps the interface's fonts on while they are not in (UiDirector.Slow)");
        // The rule must be able to fail: the director as it was, the fonts stepped by the hold alone.
        var before = new Dictionary<string, string>(sources, StringComparer.OrdinalIgnoreCase);
        before[Director] = (director ?? string.Empty).Replace("UiFonts.Step())", "false)");
        HoldPieces(before, out string lostBefore);
        Check(lostBefore != null && lostBefore.Contains("fonts"), "load hold: the sources as they were (the fonts stepped by the hold alone, its end empty) are found");

        string live = sources.TryGetValue(@"Ui\HudScreens\HudLive.cs", out string text) ? text : string.Empty;
        int waits = BeginWaits(live, out string silent);
        Check(waits >= 4 && silent == null, "HUD start: each of the " + waits + " ways HudLive.Begin waits in a loaded city is named (Waits)" + (silent != null ? "; not: " + silent : string.Empty));
        Check(!(BeginWaits(live.Replace("Waits(\"the interface's fonts are not in\");", string.Empty), out string a) > 0 && a == null)
              && !(BeginWaits(live.Replace("Waits(\"the game's top bar has no left bar or buttons yet\");", string.Empty), out string b) > 0 && b == null),
            "HUD start: a wait without its name (the fonts, the game's top bar) is found");
        string said = MemberText(live, "private static void Waits(string what)");
        Match after = Regex.Match(live, @"LateAfter = (\d+)f");
        Check(said.Contains("Logger?.Warn(") && said.Contains("+ what") && said.Contains("_lateSaid = true") && after.Success && int.Parse(after.Groups[1].Value) <= 15,
            "HUD start: a HUD that has not stood up " + (after.Success ? after.Groups[1].Value : "?") + " s into a city says once in Player.log what it waits for");
    }

    // Each LoadHold.Add of the sources: its end must do something, or its step must be called outside the hold. The pieces
    // counted; the first that would be lost when the hold ends first, or null.
    private static int HoldPieces(Dictionary<string, string> sources, out string lost)
    {
        lost = null;
        int pieces = 0;
        foreach (KeyValuePair<string, string> source in sources)
        {
            foreach (Match m in Regex.Matches(source.Value, @"LoadHold\.Add\(""([^""]+)"",\s*([\w\.]+),\s*([^;]+)\);"))
            {
                pieces++;
                if (!Regex.IsMatch(m.Groups[3].Value.Trim(), @"^\w+\s*=>\s*\{\s*\}\)?$"))
                {
                    continue;   // its end is told the hold ended and finishes the piece itself
                }
                string step = m.Groups[2].Value;
                bool called = false;
                foreach (string code in sources.Values)
                {
                    foreach (Match call in Regex.Matches(code, @"(?<![\w\.])" + Regex.Escape(step) + @"\(\)"))
                    {
                        called |= !Regex.IsMatch(code.Substring(Math.Max(0, call.Index - 12), Math.Min(12, call.Index)), @"\b(bool|void)\s+$");
                    }
                }
                if (!called && lost == null)
                {
                    lost = m.Groups[1].Value + " (" + source.Key + ")";
                }
            }
        }
        return pieces;
    }

    // HudLive.Begin's returns once the city is loaded: each must say what it waits for (Waits) in its own block. The ways
    // counted; the first silent one (the text before it), or null.
    private static int BeginWaits(string live, out string silent)
    {
        silent = null;
        string begin = MemberText(live, "private static void Begin()");
        int from = begin.IndexOf("UI.Load.LoadScene.isLoading)", StringComparison.Ordinal);
        from = from >= 0 ? begin.IndexOf("return;", from, StringComparison.Ordinal) : -1;
        if (from < 0)
        {
            silent = "the city test is not there";
            return 0;
        }
        int ways = 0;
        for (int at = begin.IndexOf("return;", from + 7, StringComparison.Ordinal); at >= 0; at = begin.IndexOf("return;", at + 7, StringComparison.Ordinal))
        {
            int open = begin.LastIndexOf('{', at);
            ways++;
            if (silent == null && begin.IndexOf("Waits(", open, at - open, StringComparison.Ordinal) < 0)
            {
                string head = begin.Substring(Math.Max(0, open - 90), Math.Min(90, open)).Trim();
                silent = head.Substring(head.LastIndexOf('\n') + 1).Trim();
            }
        }
        return ways;
    }
}
