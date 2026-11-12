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
// The fix's texts in the ten other languages are held too (NeedsPanelLanguages): the first translation of them translated the Guide's whole Home
// page again, spoke to a French player as "tu", to a Polish player in masculine forms, and turned the old rule round in Turkish and Russian.
internal static partial class Program
{
    private static void NeedsPanel()
    {
        NeedsPanelRules();
        NeedsPanelSources();
        NeedsPanelTexts();
        NeedsPanelLanguages();
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
        // What the player reads: the option's sentence, the Guide's Home page and What's new. English here; the other languages in NeedsPanelLanguages.
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

    private const string HomePageKey = "reallife_guide_home", HomeTipKey = "reallife_opt_tip_home_strip";

    private static readonly Regex GuideBold = new Regex("<b>(.*?)</b>", RegexOptions.Singleline);

    // The Guide, the option tips and What's new speak to a French player as "vous" (translate/GUIDE.md rule 13); "tu", "toi", "ton", "ta", "tes", "te" and "t'" are the other register.
    private static readonly Regex FrenchTu = new Regex(@"\b(tu|toi|ton|ta|tes|te)\b|\bt['’]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex FrenchVous = new Regex(@"\b(vous|votre|vos)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Polish past tense and "ready" forms carry the player's gender (byłeś, spałeś, miał, gotów); GUIDE.md's Polish rule "No gender in the player" asks for the imperative, the infinitive or an impersonal sentence.
    private static readonly Regex PolishGender = new Regex(@"\b\w+(łeś|łaś)\b|\b(miał|miała|gotów|gotowa)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // The fix's sentence in each language of ModLanguages.All: how it begins (that finds it in What's new, in whichever row holds it) and the clause that says when the bars used to
    // vanish, which is where a translation turned the rule round: "unless you were at home or a need was running low" (read in Turkish as "while at home or as long as no need ran
    // low", in Russian as "if you were not at home or the need did not fall").
    private static readonly Dictionary<string, string[]> NeedsPanelFix = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        { "en", new[] { "Fixed: The bars for hygiene, stress and home now show all the time",
                         "unless you were at home or a need was running low" } },
        { "pt", new[] { "Corrigido: As barras de higiene, estresse e casa agora aparecem sempre",
                         "a menos que você estivesse em casa ou uma necessidade estivesse baixa" } },
        { "zh-cn", new[] { "已修复：卫生、压力和居家状态条现在会始终显示",
                         "以前只有在家或某项需求很低时它们才会出现" } },
        { "de", new[] { "Behoben: Die Balken für Hygiene, Stress und Zuhause werden jetzt immer",
                         "außer wenn du zu Hause warst oder ein Bedürfnis knapp wurde" } },
        { "fr", new[] { "Corrigé : Les barres d'hygiène, de stress et de logement s'affichent désormais en permanence",
                         "sauf si vous étiez chez vous ou si un besoin était bas" } },
        { "es", new[] { "Corregido: Las barras de higiene, estrés y casa ahora se muestran siempre",
                         "salvo que estuvieras en casa o una necesidad estuviera bajando mucho" } },
        { "ru", new[] { "Исправлено: Шкалы гигиены, стресса и дома теперь всегда показываются",
                         "если вы были не дома и ни одна потребность не была на исходе" } },
        { "nl", new[] { "Opgelost: De balken voor hygiëne, stress en thuis staan nu altijd",
                         "tenzij je thuis was of een behoefte laag stond" } },
        { "tr", new[] { "Düzeltildi: Hijyen, stres ve ev çubukları artık oyunun üst çubuğunun altında her zaman görünür",
                         "evde değilseniz ve hiçbir ihtiyaç azalmamışsa" } },
        { "pl", new[] { "Naprawiono: Paski higieny, stresu i domu pokazują się teraz stale",
                         "poza domem, gdy żadna potrzeba nie była nisko" } },
        { "ko", new[] { "수정: 위생, 스트레스, 집 막대가 이제 게임 상단 막대 아래에 항상 표시됩니다",
                         "집에 있거나 욕구가 낮아질 때가 아니면" } }
    };

    // What a player of each language reads of the fix, in all eleven files: the Guide's Home page, the option's sentence and the fix's sentence in What's new.
    private static void NeedsPanelLanguages()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        string pageEn = null;
        if (en == null || !en.TryGetValue(HomePageKey, out pageEn))
        {
            Check(false, "needs panel languages: en.json and the Guide's Home page were read from " + folder);
            return;
        }
        MatchCollection namesEn = GuideBold.Matches(pageEn);
        string[] linesEn = pageEn.Split('\n');

        // Each name in bold on the English page stands for the texts of the mod that read exactly so (an option, a button, a tab, a page). The page in another language names it by what
        // one of those says in that language (translate/GUIDE.md rule 12), so that the player finds on the options page the words the Guide gave.
        var standFor = new List<string>[namesEn.Count];
        bool every = namesEn.Count > 0;
        for (int i = 0; i < namesEn.Count; i++)
        {
            standFor[i] = new List<string>();
            foreach (KeyValuePair<string, string> kv in en)
            {
                if (kv.Value == namesEn[i].Groups[1].Value && !kv.Key.StartsWith("reallife_guide_", StringComparison.Ordinal) && !kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal))
                {
                    standFor[i].Add(kv.Key);
                }
            }
            every &= standFor[i].Count > 0;
        }
        Check(every, "needs panel languages: every name in bold on the English Home page is a text of the mod, which the page in each language is held to");

        foreach (string code in ModLanguages.All)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, code + ".json"));
            Check(words != null, "needs panel languages: " + code + ".json was read");
            if (words == null)
            {
                continue;
            }
            string page = words.TryGetValue(HomePageKey, out string pageText) ? pageText : string.Empty;
            string tip = words.TryGetValue(HomeTipKey, out string tipText) ? tipText : string.Empty;
            string fix = NeedsPanelSentence(words, code, out int times);

            // The page has the English page's lines: its headings, its points and the figures in curly brackets.
            string[] lines = page.Split('\n');
            bool shaped = lines.Length == linesEn.Length;
            for (int i = 0; shaped && i < lines.Length; i++)
            {
                shaped = lines[i].StartsWith("# ", StringComparison.Ordinal) == linesEn[i].StartsWith("# ", StringComparison.Ordinal)
                         && lines[i].StartsWith("- ", StringComparison.Ordinal) == linesEn[i].StartsWith("- ", StringComparison.Ordinal)
                         && NamesOf(lines[i]) == NamesOf(linesEn[i]);
            }
            Check(shaped, "needs panel languages: the Home page in " + code + " has the English page's " + linesEn.Length + " lines, with their headings, points and figures (" + lines.Length + " lines)");

            // It names its options and buttons as the language shows them, word for word: the first translation of the fix translated the whole page again, and in six languages
            // "Easy needs" and other names came out as words the options page does not use.
            MatchCollection names = GuideBold.Matches(page);
            var off = new List<string>();
            for (int i = 0; i < namesEn.Count; i++)
            {
                bool held = false;
                if (i < names.Count)
                {
                    foreach (string key in standFor[i])
                    {
                        held |= words.TryGetValue(key, out string label) && label == names[i].Groups[1].Value;
                    }
                }
                if (!held)
                {
                    off.Add("<b>" + (i < names.Count ? names[i].Groups[1].Value : "?") + "</b> for <b>" + namesEn[i].Groups[1].Value + "</b>");
                }
            }
            Check(names.Count == namesEn.Count && off.Count == 0,
                "needs panel languages: the Home page in " + code + " names its options and buttons as " + code + " shows them, word for word ("
                + names.Count + " names in bold, the English " + namesEn.Count + "; not the language's own: " + string.Join(", ", off) + ")");

            // The fix's sentence is in What's new once, and says when the bars used to vanish as the English does.
            Check(NeedsPanelFix.TryGetValue(code, out string[] sentence), "needs panel languages: " + code + " has its pieces of the fix's sentence in NeedsPanelFix");
            if (sentence != null)
            {
                Check(times == 1, "needs panel languages: What's new in " + code + " has the fix's sentence once, in one row (" + times + " time(s))");
                Check(fix.Contains(sentence[1]), "needs panel languages: the fix's sentence in " + code + " says when the bars used to vanish: \"" + sentence[1] + "\"");
            }

            // Register and gender, in the three texts of the fix.
            string[] texts = { tip, page, fix };
            string[] kinds = { "option's sentence", "Home page", "What's new sentence" };
            for (int i = 0; i < texts.Length; i++)
            {
                Check(texts[i].Length > 0, "needs panel languages: the " + kinds[i] + " in " + code + " is there");
                if (code == "fr")
                {
                    Check(!FrenchTu.IsMatch(texts[i]) && FrenchVous.IsMatch(texts[i]),
                        "needs panel languages: the French " + kinds[i] + " speaks to the player as \"vous\", with no \"tu\", \"ton\", \"te\" or \"toi\" (found: \"" + FrenchTu.Match(texts[i]).Value + "\")");
                }
                if (code == "pl")
                {
                    Check(!PolishGender.IsMatch(texts[i]),
                        "needs panel languages: the Polish " + kinds[i] + " has no form of the player's gender (found: \"" + PolishGender.Match(texts[i]).Value + "\")");
                }
            }
        }
    }

    // The fix's sentence as one language's What's new has it: from its first words, in whichever row holds them (the next row, or a numbered one after a release), to the language's
    // next fix mark or the end of the row. times is how often the first words stand in all the rows; the sentence is empty unless that is exactly once.
    private static string NeedsPanelSentence(Dictionary<string, string> words, string code, out int times)
    {
        times = 0;
        string found = string.Empty;
        if (!NeedsPanelFix.TryGetValue(code, out string[] sentence) || !FixMarks.TryGetValue(code, out string mark))
        {
            return found;
        }
        foreach (KeyValuePair<string, string> row in words)
        {
            if (!row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) || row.Key.EndsWith("_title", StringComparison.Ordinal))
            {
                continue;
            }
            int at = row.Value.IndexOf(sentence[0], StringComparison.Ordinal);
            if (at >= 0)
            {
                times += Occurs(row.Value, sentence[0]);
                int next = row.Value.IndexOf(mark, at + mark.Length, StringComparison.Ordinal);
                found = row.Value.Substring(at, (next < 0 ? row.Value.Length : next) - at);
            }
        }
        return times == 1 ? found : string.Empty;
    }
}
