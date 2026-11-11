using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// A player's "top left needs panel invisible at a new game and after a shower" (0.63.11, build 3682, 10 Nov 2026). The three bars of hygiene,
// stress and home under the game's top bar stood only at home, while hygiene was under 35, while stress was over 65 and while ill
// (HomeDirector.Strip), so a new game away from home (hygiene 80, stress 20, no home yet) showed nothing; they came when hygiene fell under 35,
// and a shower put it back over 35 and took them away again. They now stand whenever the game's top bar does (HomeBarRules); the home bar
// waits for a home of the player's own. Offline: run with --needs-panel.
internal static partial class Program
{
    private static void NeedsPanel()
    {
        NeedsPanelRules();
        NeedsPanelSources();
        NeedsPanelTexts();
    }

    private static void NeedsPanelRules()
    {
        // The rule takes the two options and nothing about the player: there is no hygiene, stress, illness or place in it to hide the bars on.
        Check(HomeBarRules.Shows(true, true), "needs panel: the bars stand when their option and the needs are on");
        Check(!HomeBarRules.Shows(false, true) && !HomeBarRules.Shows(true, false) && !HomeBarRules.Shows(false, false),
            "needs panel: the bars stand aside when their option or the needs are off");
        Check(HomeBarRules.Most == 3 && HomeBarRules.Count(true) == 3 && HomeBarRules.Count(false) == 2,
            "needs panel: three bars with a home of the player's own, hygiene and stress without");
    }

    private static void NeedsPanelSources()
    {
        string offers = ModSource(@"Home\HomeDirector.Offers.cs"), director = ModSource(@"Home\HomeDirector.cs"), bar = ModSource(@"Home\HomeNeedsBar.cs");

        // The decision: the rule's answer, and only that, between the bars and the Hide that takes them down. The player met "at home, hygiene
        // under 35, stress over 65 or ill" here, so none of those may stand in front of the Hide again.
        string strip = MethodText(offers, "private void Strip()");
        int hide = strip.IndexOf("HomeNeedsBar.Hide()", StringComparison.Ordinal);
        string decision = hide > 0 ? strip.Substring(0, hide) : string.Empty;
        Check(strip.Length > 0 && decision.Contains("if (!HomeBarRules.Shows(Settings.HomeStrip, Settings.HomeNeeds))"),
            "needs panel source: Strip takes the bars down only when HomeBarRules.Shows says no, with no place asked about");
        Check(decision.Length > 0 && !Regex.IsMatch(decision, @"atHome|Hygiene|Stress|\bIll\b|\.Ill\b|\|\|"),
            "needs panel source: nothing of where the player is or how they are doing stands in front of the Hide");
        Check(Occurs(strip, "HomeNeedsBar.Hide()") == 1 && Occurs(strip, "return;") == 1 && Occurs(strip, "HomeNeedsBar.Show(") == 1
              && strip.Contains("\n            HomeNeedsBar.Show(HomeState.Hygiene, HomeState.Stress, HomeState.Tidy, HomeTidy.HasHome, note);"),
            "needs panel source: past that one test Strip always shows the bars, with the player's three figures and whether there is a home");
        Check(director.Contains("\n            Strip();") && !director.Contains("Strip(atHome)"), "needs panel source: the director asks once a second, without the place");

        // The bars: how many stand follows the home, and the tab is switched on and off in two places only.
        Check(bar.Contains("int count = HomeBarRules.Count(hasHome);") && Regex.IsMatch(bar, @"if \(count == HomeBarRules\.Most\)\s*\{\s*Bar\(2, home\);\s*\}")
              && !bar.Contains("Bar(2, hasHome ? home : 0f)"),
            "needs panel source: the home bar is written only while it stands, never as an empty bar of a player with no home");
        string place = MethodText(bar, "private static void Place()");
        Check(place.Contains("Reach[_count - 1]") && place.Contains("_home.SetActive(homeOn)") && place.Contains("_noteRect.anchoredPosition = new Vector2(Pad + bars + Pad,"),
            "needs panel source: the tab is as wide as the bars that stand, the home bar is switched off with no home, and the illness line follows the last bar");
        Check(bar.Contains("Reach[i] = game.localPosition.x + game.rect.xMax - firstLeft;") && bar.Contains("_home = copy.transform.GetChild(HomeBarRules.Most - 1).gameObject;"),
            "needs panel source: where each bar ends is read from the game's own bars when the tab is built");
        Check(Occurs(bar, "_tab.SetActive(") == 2 && MethodText(bar, "public static void Hide()").Contains("_tab.SetActive(false);")
              && MethodText(bar, "public static void Show(").Contains("_tab.SetActive(true);"),
            "needs panel source: the tab is switched on in Show and off in Hide and nowhere else, so a screen that hides the game's top bar never leaves it off");

        // Every way the bars are taken down or put up: the director's Strip and its TakeBack, and the interface HUD's hold. Nothing new may hide them
        // on a condition of its own.
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var hiders = new List<string>();
        var showers = new List<string>();
        int files = 0;
        if (Directory.Exists(scripts))
        {
            foreach (string path in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                files++;
                string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", string.Empty);   // a comment may name them
                if (code.Contains("HomeNeedsBar.Hide("))
                {
                    hiders.Add(System.IO.Path.GetFileName(path));
                }
                if (code.Contains("HomeNeedsBar.Show("))
                {
                    showers.Add(System.IO.Path.GetFileName(path));
                }
            }
        }
        hiders.Sort(StringComparer.Ordinal);
        Check(files > 500, "needs panel source: the mod's scripts were read (" + files + " files)");
        Check(string.Join(",", hiders) == "HomeDirector.Offers.cs,HomeDirector.cs" && string.Join(",", showers) == "HomeDirector.Offers.cs",
            "needs panel source: only the home director takes the bars down (its Strip and its TakeBack) and only Strip puts them up ("
            + string.Join(",", hiders) + " / " + string.Join(",", showers) + ")");
    }

    private static void NeedsPanelTexts()
    {
        // What the player reads: the option's sentence, the Guide's Home page and What's new. English only here; the other languages are filled in after.
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en != null, "needs panel texts: en.json was read");
        if (en == null)
        {
            return;
        }
        string tip = en.TryGetValue("reallife_opt_tip_home_strip", out string t) ? t : string.Empty;
        string guide = en.TryGetValue("reallife_guide_home", out string g) ? g : string.Empty;
        Check(tip.Contains("all the time") && tip.Contains("the home bar waits until you have a home") && !tip.Contains("or when a need runs low"),
            "needs panel texts: the option's sentence says the bars show all the time and the home bar waits for a home");
        Check(guide.Contains("bars all the time, and the home bar waits until you have a home.") && !guide.Contains("at home, or when one runs low"),
            "needs panel texts: the Guide's Home page says the same");

        // Found by its English line in any row, so that it is found before a release and after the version script numbers the row.
        const string fixedLine = "Fixed: The bars for hygiene, stress and home now show all the time under the game's top bar, where they used to vanish unless you were at home"
                                 + " or a need was running low, and the home bar waits until you have a home.";
        int rows = 0, times = 0;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(fixedLine))
            {
                rows++;
                times += Occurs(row.Value, fixedLine);
            }
        }
        Check(rows == 1 && times == 1, "needs panel texts: one English What's new row has the fix once (" + rows + " row(s), " + times + " time(s))");
        Check(fixedLine.IndexOf('!') < 0 && fixedLine.IndexOf((char)0x2014) < 0 && fixedLine.IndexOf((char)0x2013) < 0 && !fixedLine.Contains("  ") && !fixedLine.Contains(" - "),
            "needs panel texts: the fix reads plainly, with no dash, double space or exclamation mark");

        // The page that describes the feature says the same.
        string page = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\home.md"));
        string docs = File.Exists(page) ? File.ReadAllText(page) : string.Empty;
        Check(docs.Contains("The three are shown all the time the game's top bar is") && docs.Contains("HomeBarRules.Shows(Settings.HomeStrip, Settings.HomeNeeds)")
              && !docs.Contains("The three are shown at home, while hygiene is under 35"),
            "needs panel texts: the Home page of docs/features says when the bars show");
    }
}
