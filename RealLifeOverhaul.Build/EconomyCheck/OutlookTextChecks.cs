using System;
using System.Collections.Generic;
using System.IO;
using IoPath = System.IO.Path;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Outlook;

// The Economy app's texts, places and sources (Scripts/Outlook): every key its code names in every language with the English's names
// in curly brackets, its English plain (no dashes, no exclamation marks), the notices short, each sentence of the advice written from
// the real English with no name left unfilled, the Guide's page and the figures it names from the code, What's new in all eleven
// languages; the lists of the player's businesses and buildings; and the sources: nothing per frame, one place that reads the game,
// the guard on the mesh, and every registration.
internal static partial class Program
{
    private static string OutlookScripts()
    {
        return IoPath.GetFullPath(IoPath.Combine(LocalesFolder(), "..", "Scripts"));
    }

    private static string OutlookCode(string text)
    {
        return Regex.Replace(Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline), @"//[^\n]*", string.Empty);
    }

    // ---------------------------------------------------------------- the texts

    private static void OutlookTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("outlook texts", words, codes))
        {
            return;
        }
        string scripts = OutlookScripts();
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(IoPath.Combine(scripts, "Outlook"), "*.cs"))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        for (int rule = 0; rule < OutlookInsights.Count; rule++)
        {
            keys.Add(OutlookInsights.HeadKeys[rule]);
            keys.Add(OutlookInsights.MeanKeys[rule]);
            if (OutlookInsights.HasTwo(rule))
            {
                keys.Add(OutlookInsights.MeanTwoKeys[rule]);
            }
        }
        // The keys the code makes up: a shortage or glut announced for tomorrow, and a steady economy that grows or slows.
        keys.Add(OutlookInsights.HeadKeys[OutlookInsights.Shortage] + "_soon");
        keys.Add(OutlookInsights.HeadKeys[OutlookInsights.Glut] + "_soon");
        keys.Add(OutlookRules.VerdictLineKeys[(int)OutlookVerdict.Growing] + "_steady");
        keys.Add(OutlookRules.VerdictLineKeys[(int)OutlookVerdict.Slowing] + "_steady");
        // The keys the options and the Guide make from an option's id and a feature's id.
        foreach (string key in new[] { "reallife_opt_sec_econapp", "reallife_opt_tip_economy_app", "reallife_opt_tip_economy_app_look", "reallife_meta_title_economy_app" })
        {
            keys.Add(key);
        }
        var lacking = new List<string>();
        foreach (string key in keys)
        {
            string missing = LanguagesLacking(words, codes, key);
            if (missing.Length > 0)
            {
                lacking.Add(key + " (" + missing + ")");
            }
        }
        Dictionary<string, string> en = words[0];
        Check(keys.Count > 200 && lacking.Count == 0, "outlook texts: each of its " + keys.Count + " texts is in every language with the English's names in curly brackets (" + string.Join(", ", lacking.Take(5)) + ")");

        // Portuguese and Chinese are written for it: none of its own texts is the English left as it was.
        var left = new List<string>();
        foreach (string key in keys.Where(k => k.StartsWith("reallife_econapp_", StringComparison.Ordinal) || k.StartsWith("reallife_guide_economy_app", StringComparison.Ordinal)))
        {
            for (int l = 1; l < 3 && l < words.Length; l++)
            {
                if (words[l][key] == en[key] && en[key].Length > 12 && !en[key].StartsWith("{", StringComparison.Ordinal))
                {
                    left.Add(key + " (" + codes[l] + ")");
                }
            }
        }
        Check(left.Count == 0, "outlook texts: Portuguese and Chinese are translated, none is the English left as it was (" + string.Join(", ", left.Take(6)) + ")");

        // The player's report (jiwalanpa, 27 Oct): index numbers in the stock market with no names beside them. Every index row of the
        // Economy app, the city's own and each of the market's sectors, has a label key whose text is there, not the key and not
        // empty, in English, Portuguese and Chinese; no two sectors are called the same; and a row the market gives no name for
        // reads "Sector 7", never a bare number or nothing.
        var indexKeys = new List<string> { "reallife_mkt_index", "reallife_mkt_col_index", "reallife_econapp_chart_stocks", "reallife_econapp_sectors", "reallife_econapp_sector_n" };
        indexKeys.Add(OutlookCatalogue.Tiles[OutlookCatalogue.TileStocks].LabelKey);
        indexKeys.AddRange(RealLifeOverhaul.Markets.MarketCatalogue.SectorKeys);
        var unnamed = new List<string>();
        var sameName = new List<string>();
        for (int l = 0; l < 3 && l < words.Length; l++)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in indexKeys)
            {
                string text = words[l].TryGetValue(key, out string t) ? t : null;
                if (string.IsNullOrWhiteSpace(text) || text == key)
                {
                    unnamed.Add(key + " (" + codes[l] + ")");
                }
                else if (key.StartsWith("reallife_mkt_sector_", StringComparison.Ordinal) && !seen.Add(text))
                {
                    sameName.Add(text + " (" + codes[l] + ")");
                }
            }
        }
        Check(RealLifeOverhaul.Markets.MarketCatalogue.SectorKeys.Length == (int)RealLifeOverhaul.Markets.MarketSector.Count && indexKeys.Count > 28 && unnamed.Count == 0 && sameName.Count == 0,
            "outlook texts: each of the " + indexKeys.Count + " index rows (the city's and " + RealLifeOverhaul.Markets.MarketCatalogue.SectorKeys.Length + " sectors) has a label key with a text in English, Portuguese and Chinese, no two sectors alike ("
            + string.Join(", ", unnamed.Concat(sameName).Take(4)) + ")");
        var labelled = new List<string>();
        for (int l = 0; l < 3 && l < words.Length; l++)
        {
            OutlookWords sayer = OutlookEnglish(words[l]);
            string none = sayer.SectorLabel(6, null), blank = sayer.SectorLabel(6, "  "), empty = sayer.SectorLabel(0, string.Empty), kept = sayer.SectorLabel(6, "Banks");
            bool ok = kept == "Banks" && none.Contains("7") && blank == none && empty.Contains("1") && none != words[l]["reallife_econapp_sector_n"] && none.IndexOf('{') < 0 && none.Trim().Length > 2;
            if (!ok)
            {
                labelled.Add(codes[l] + ": " + none);
            }
        }
        Check(labelled.Count == 0, "outlook texts: a sector the market gives no name for reads Sector and its number in English, Portuguese and Chinese, a named one keeps its name (" + string.Join("; ", labelled) + ")");

        // Plain English: no dashes, no exclamation marks. The other languages are held to the same.
        var rough = new List<string>();
        for (int l = 0; l < words.Length; l++)
        {
            foreach (string key in keys)
            {
                string text = words[l][key];
                if (text.Contains("—") || text.Contains("–") || text.Contains(" - ") || text.Contains("!") || text.Contains("！"))
                {
                    rough.Add(key + " (" + codes[l] + ")");
                }
            }
        }
        Check(rough.Count == 0, "outlook texts: none of its texts has a dash or an exclamation mark in any language (" + string.Join(", ", rough.Take(4)) + ")");

        // A notice is at most 240 characters, in every language.
        var longest = new List<string>();
        foreach (string key in keys.Where(k => k.StartsWith("reallife_econapp_test_", StringComparison.Ordinal)))
        {
            for (int l = 0; l < words.Length; l++)
            {
                if (words[l][key].Length > 240)
                {
                    longest.Add(key + " (" + codes[l] + ")");
                }
            }
        }
        Check(longest.Count == 0, "outlook texts: its tests' notices are at most 240 characters (" + string.Join(", ", longest) + ")");

        // Short labels stay short: the tabs, the tiles and the verdict's words must fit their places in the languages written for it.
        var fit = new List<string>();
        foreach (string key in keys.Where(k => k.StartsWith("reallife_econapp_tab_", StringComparison.Ordinal) || k.StartsWith("reallife_econapp_tile_", StringComparison.Ordinal)
                                               || (k.StartsWith("reallife_econapp_verdict_", StringComparison.Ordinal) && !k.Contains("_line"))))
        {
            for (int l = 0; l < 3 && l < words.Length; l++)
            {
                if (words[l][key].Length > (l == 2 ? 10 : 24))
                {
                    fit.Add(key + " (" + codes[l] + ")");
                }
            }
        }
        Check(fit.Count == 0, "outlook texts: the tabs, the tiles' names and the verdict's words are short enough for their places (" + string.Join(", ", fit) + ")");

        // Every rule's sentences, written from each language's real texts with figures, leave no name unfilled and no text missing.
        var spoilt = new List<string>();
        for (int l = 0; l < 3 && l < words.Length; l++)
        {
            Dictionary<string, string> text = words[l];
            OutlookWords said = OutlookEnglish(text);
            for (int rule = 0; rule < OutlookInsights.Count; rule++)
            {
                for (int variant = 0; variant < (OutlookInsights.HasTwo(rule) ? 2 : 1); variant++)
                {
                    for (int soon = 0; soon < (rule == OutlookInsights.Shortage || rule == OutlookInsights.Glut ? 2 : 1); soon++)
                    {
                        var i = new OutlookInsight { Rule = rule, Variant = variant, A = 3, B = 12, C = 4, D = soon, Name = "d1" };
                        string head = said.Head(i), mean = said.Mean(i);
                        string both = head + " " + mean;
                        bool whole = both.IndexOf('{') < 0 && both.IndexOf('}') < 0 && both.IndexOf('[') < 0 && both.IndexOf("  ", StringComparison.Ordinal) < 0
                                     && head.Length > 12 && mean.Length > 12 && head.Length <= 200 && mean.Length <= 200;
                        if (!whole)
                        {
                            spoilt.Add(OutlookInsights.Names[rule] + " (" + codes[l] + ")");
                        }
                    }
                }
            }
        }
        Check(spoilt.Count == 0, "outlook texts: each of the " + OutlookInsights.Count + " rules writes whole sentences in English, Portuguese and Chinese, every name filled (" + string.Join(", ", spoilt.Take(4)) + ")");
        var enSaid = OutlookEnglish(en);
        bool stops = true;
        for (int rule = 0; rule < OutlookInsights.Count; rule++)
        {
            var i = new OutlookInsight { Rule = rule, A = 3, B = 12, C = 4, D = 0, Name = "d1" };
            stops &= enSaid.Head(i).EndsWith(".", StringComparison.Ordinal) && enSaid.Mean(i).EndsWith(".", StringComparison.Ordinal);
        }
        Check(stops, "outlook texts: every English sentence of the advice ends with a full stop");
        var verdictWords = new List<string>();
        var verdictBad = new List<string>();
        foreach (OutlookVerdict v in Enum.GetValues(typeof(OutlookVerdict)))
        {
            OutlookReadings state = OutlookQuiet();
            state.Cycle = v == OutlookVerdict.Recession ? 3 : v == OutlookVerdict.Slowing ? 2 : v == OutlookVerdict.Growing ? 0 : 1;
            state.Footfall = -5;
            state.Spend = 970;
            for (int l = 0; l < 3 && l < words.Length; l++)
            {
                string line = OutlookEnglish(words[l]).VerdictLine(v, state);
                if (line.IndexOf('{') >= 0 || line.IndexOf('[') >= 0 || line.Length < 12)
                {
                    verdictBad.Add(v + " (" + codes[l] + "): " + line);
                }
            }
            verdictWords.Add(enSaid.VerdictWord(v));
        }
        Check(verdictBad.Count == 0, "outlook texts: each verdict has a sentence with every name filled, in three languages (" + string.Join("; ", verdictBad.Take(2)) + ")");
        Check(verdictWords.Distinct().Count() == 5 && verdictWords.Contains("In recession") && verdictWords.Contains("Overheating"), "outlook texts: the five words of the verdict are five different words");

        // The Guide's page and the figures it names.
        string guide = en["reallife_guide_economy_app"];
        string guideSource = File.ReadAllText(IoPath.Combine(scripts, "Outlook", "OutlookGuide.cs"));
        var named = new SortedSet<string>(Regex.Matches(guide, @"\{(econapp_[a-z_]+)\}").Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.Ordinal);
        var declared = new SortedSet<string>(Regex.Matches(guideSource, "\\.Fact\\(\"(econapp_[a-z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.Ordinal);
        Check(named.Count >= 15 && named.All(declared.Contains), "outlook texts: every figure the Guide's page names comes from the code (" + string.Join(", ", named.Where(n => !declared.Contains(n))) + " not declared)");
        int headings = guide.Split('\n').Count(x => x.StartsWith("# ", StringComparison.Ordinal));
        int bullets = guide.Split('\n').Count(x => x.StartsWith("- ", StringComparison.Ordinal));
        bool same = true;
        for (int l = 1; l < 3 && l < words.Length; l++)
        {
            string[] other = words[l]["reallife_guide_economy_app"].Split('\n');
            same &= other.Count(x => x.StartsWith("# ", StringComparison.Ordinal)) == headings && other.Count(x => x.StartsWith("- ", StringComparison.Ordinal)) == bullets;
        }
        Check(headings == 6 && same, "outlook texts: the Guide's page has " + headings + " headings and " + bullets + " bullets, and the same in Portuguese and Chinese");
        foreach (string tab in new[] { "Prices and rates", "Jobs and people", "Districts", "Markets", "City finances", "Insights" })
        {
            Check(guide.Contains("<b>" + tab + "</b>"), "outlook texts: the Guide names the tab " + tab);
        }
        Check(guide.Contains("rival tycoons") && guide.Contains("Going Public") && guide.Contains("businesses") && guide.Contains("buildings"),
            "outlook texts: the Guide says what the rivals read, that nothing is sent in Going Public, and that the businesses and buildings are listed");
        Check(!en["reallife_guide_economy_city"].Contains("Read <b>The city</b>") && en["reallife_guide_economy_city"].Contains("<b>Economy</b> app"),
            "outlook texts: the city economy's guide sends the player to the Economy app, not to the page the Finance app had");

        // What's new, in all eleven languages: the line is looked for in any row, so that a release numbering the row does not lose it.
        var marks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "A new Economy app on the phone" }, { "pt", "Um novo app Economia" }, { "zh-cn", "手机上的新经济应用" }, { "de", "Eine neue Wirtschafts-App" },
            { "fr", "Une nouvelle application Économie" }, { "es", "Una nueva app Economía" }, { "ru", "Новое приложение «Экономика»" },
            { "nl", "Een nieuwe Economie-app" }, { "tr", "Telefondaki yeni Ekonomi uygulaması" }, { "pl", "Nowa aplikacja Gospodarka" }, { "ko", "휴대폰의 새 경제 앱" }
        };
        var missingNews = new List<string>();
        foreach (string code in ModLanguages.All)
        {
            int at = Array.IndexOf(codes, code);
            Dictionary<string, string> row = at >= 0 ? words[at] : LoadWords(IoPath.Combine(LocalesFolder(), code + ".json"));   // (Polish and Korean are read here: the shared helper holds the first nine)
            if (!marks.TryGetValue(code, out string mark) || !AnyChangesRow(row, mark))
            {
                missingNews.Add(code);
            }
        }
        Check(ModLanguages.All.Length == 11 && missingNews.Count == 0, "outlook texts: What's new says so in all " + ModLanguages.All.Length + " languages, in any of its rows (not: " + string.Join(", ", missingNews) + ")");
    }

    // ---------------------------------------------------------------- the player's businesses and buildings

    private static void OutlookPlaceChecks()
    {
        var r = new OutlookReadings();
        r.Begin();
        OutlookPlace shop = r.NextBusiness();
        shop.Name = "Cafe";
        shop.District = "Uptown";
        shop.Colour = 0x112233;
        shop.Customers = 5;
        shop.Prices = 2;
        shop.Known = false;
        OutlookPlace house = r.NextEstate();
        house.Name = "1 High St";
        house.Value = 100;
        house.Today = 110;
        house.Factor = 1100;
        r.Carried = 12;
        r.MoreBusinesses = 3;
        r.MoreEstates = 4;
        r.Done();
        Check(r.Businesses.Count == 1 && r.Estates.Count == 1 && r.Businesses[0].Name == "Cafe" && r.Estates[0].Factor == 1100, "outlook places: a look keeps the businesses and the buildings it filled");
        r.Begin();
        OutlookPlace again = r.NextBusiness();
        Check(ReferenceEquals(again, shop) && again.Name == null && again.District == null && again.Colour == 0 && again.Customers == 0 && again.Prices == 0 && again.Known
              && r.Carried == 0 && r.MoreBusinesses == 0 && r.MoreEstates == 0,
            "outlook places: a record is used again from look to look and comes back clean, and the carried amount and the counts of the rest start again");
        r.Done();
        Check(r.Businesses.Count == 1 && r.Estates.Count == 0, "outlook places: what a look did not fill is dropped, so the page never shows a place the player no longer has");
        r.Begin();
        for (int i = 0; i < 40; i++)
        {
            OutlookPlace p = r.NextEstate();
            p.Name = "e" + i;
        }
        r.Done();
        Check(r.Estates.Count == 40 && r.Estates[39].Name == "e39", "outlook places: the readings hold as many as they are given (the game's reader lists a dozen and counts the rest)");
    }

    // ---------------------------------------------------------------- the sources

    private static void OutlookSources()
    {
        string scripts = OutlookScripts();
        string[] files = Directory.GetFiles(IoPath.Combine(scripts, "Outlook"), "*.cs");
        string All(string pattern) => string.Join("\n", files.Where(f => Regex.IsMatch(IoPath.GetFileName(f), pattern)).Select(File.ReadAllText));
        string Core(string relative) => File.ReadAllText(IoPath.Combine(scripts, relative));
        string stripped = OutlookCode(string.Join("\n", files.Select(File.ReadAllText)));

        Check(!Regex.IsMatch(stripped, @"\b(void|IEnumerator)\s+(Update|LateUpdate|FixedUpdate|OnGUI)\s*\("), "outlook sources: no Update, LateUpdate, FixedUpdate or OnGUI, so nothing runs per frame");
        Check(!Regex.IsMatch(stripped, @"FindObjectsOfType|FindObjectOfType|FindObjectsByType|GameObject\.Find|FindWithTag|Physics\.|Camera\.main"), "outlook sources: no search of the scene, no physics query");
        Check(!Regex.IsMatch(stripped, @"Harmony|\.Patch\(|\[HarmonyPatch"), "outlook sources: no Harmony patch");
        Check(!Regex.IsMatch(stripped, @"using System\.Linq;"), "outlook sources: no LINQ in the app's folder");
        string mesh = All(@"^OutlookMesh\.cs$");
        Check(mesh.Contains("Guard.For(\"economy_app\", \"OutlookMesh\"") && mesh.Contains("FrameGuard.Off") && mesh.Contains("Guard.FailMesh") && mesh.Contains("Guard.NoMesh"),
            "outlook sources: the mesh's draw is behind the guard of its feature, and a fault draws nothing");
        var readers = files.Where(f => Regex.IsMatch(File.ReadAllText(f), @"\b(NeighborhoodHelper|TaxHelper)\b")).Select(IoPath.GetFileName).OrderBy(n => n).ToList();
        Check(readers.SequenceEqual(new[] { "OutlookGame.cs" }), "outlook sources: the game's district data and its tax are read in one file, OutlookGame.cs (" + string.Join(", ", readers) + ")");
        string game = All(@"^OutlookGame\.cs$");
        Check(!Regex.IsMatch(OutlookCode(game), @"\.AddTransaction\(|\bMoney\s*[-+]?=[^=]|\.Pay\(|\.Charge\("), "outlook sources: the game's reader writes nothing to the game");
        Check(game.Contains("OutlookReadings.MostPlaces") && game.Contains("r.MoreBusinesses++") && game.Contains("r.MoreEstates++"), "outlook sources: the game's reader lists a dozen businesses and a dozen buildings and counts the rest");
        string links = Core(@"Wiring\Wiring.OutlookLinks.cs");
        Check(!Regex.IsMatch(OutlookCode(links), @"Harmony|\.Patch\(") && links.Contains("OutlookBridges.Reset()") && links.Contains("EconomyBridges.OpenEconomyApp = null") && links.Contains("EconomyBridges.EconomyVerdict = null"),
            "outlook sources: the wiring wraps nothing and puts every bridge back");
        Check(All(@"^OutlookPage\.cs$").Contains("class Pool<T>") && !Regex.IsMatch(OutlookCode(All(@"^OutlookPage\.cs$")), @"\.ToList\(|\.Where\(|\.Select\(|\bOrderBy\("),
            "outlook sources: the page's parts are made once and used again, with no list built and no LINQ in the page");
        string kit = All(@"^OutlookKit\.cs$");
        Check(kit.Contains("HideFlags.DontUnloadUnusedAsset") && kit.Contains("private static Sprite _round, _circle, _soft"), "outlook sources: the three shapes are drawn once into textures the game keeps");

        // The registrations a new app and feature need.
        Check(Core(@"Core\GameUi\PagerRules.cs").Contains("\"RealLifeStocks\", \"RealLifeEconomy\""), "outlook sources: the app is in the phone's order, after the Stocks app");
        Check(Core(@"Core\GameUi\LookRules.cs").Contains("\"RealLifeEconomy\""), "outlook sources: the app has an accent colour of its own");
        string seats = Core(@"Core\StartRules.cs");
        Check(seats.Contains("public const int Outlook = 22;") && seats.Contains("new[] { CityEconomy, Markets },               // Outlook"), "outlook sources: its seat on the start line comes after the city's economy and the stock market, which it reads");
        Check(Core(@"Core\FeatureOwners.cs").Contains("{ \"RealLifeOverhaul.Outlook\", \"economy_app\" }"), "outlook sources: its classes belong to the feature economy_app");
        string settings = Core(@"Core\Settings.cs");
        Check(settings.Contains("EconomyAppId = \"economy_app\"") && settings.Contains("EconomyAppLookId = \"economy_app_look\"") && settings.Contains("EconomyApp = Bool(modId, EconomyAppId, EconomyApp)")
              && settings.Contains("EconomyAppLook = Int(modId, EconomyAppLookId, EconomyAppLook)") && settings.Contains("public static bool EconomyApp = true;") && settings.Contains("public static int EconomyAppLook = 0;"),
            "outlook sources: its two options are declared, on and dark to begin with, and loaded");
        string mod = Core(@"Core\RealLifeOverhaulMod.cs");
        Check(mod.Contains("Outlook.OutlookOptions.AddOutlook(o)") && mod.Contains("Guard.Add<Outlook.OutlookDirector>(_root)") && mod.Contains("Outlook.OutlookApp.Logger = context.Logger"), "outlook sources: the mod puts its options and its director on");
        string wiring = Core(@"Wiring\Wiring.cs");
        Check(wiring.Contains("OutlookLinks.Connect") && wiring.Contains("OutlookLinks.Disconnect"), "outlook sources: the wiring connects it and puts it back");
        Check(Core(@"Meta\FeatureCities.cs").Contains("new Line(\"economy_app\", CityScope.NewYork") && Core(@"Meta\OptionsTree.cs").Contains("S(\"econapp\").For(\"economy_app\")") && Core(@"Meta\FeatureList.cs").Contains("Outlook.OutlookGuide.Register()"),
            "outlook sources: it has its line in the city table, a section on the options page and its entry in the feature list");

        // The Finance app: the page is gone, the button leads here.
        string finance = string.Join("\n", Directory.GetFiles(IoPath.Combine(scripts, "Economy", "Finance"), "*.cs").Select(File.ReadAllText));
        Check(!finance.Contains("WriteCity") && !finance.Contains("ShowCity") && !finance.Contains("HideCity") && finance.Contains("reallife_econapp_finance_open") && finance.Contains("EconomyBridges.OpenEconomyApp"),
            "outlook sources: the Finance app's page for the city is gone and its button opens the Economy app");
        string bridges = Core(@"Economy\EconomyBridges.cs");
        Check(bridges.Contains("EconomyAppOn = null") && bridges.Contains("OpenEconomyApp = null") && bridges.Contains("app(1)"), "outlook sources: the city's notices open the Economy app on its Prices and rates tab, and the bridges are put back");

        // The tests of Options > Tests are on the options page and in the feature list.
        string guideSource = File.ReadAllText(IoPath.Combine(scripts, "Outlook", "OutlookGuide.cs"));
        foreach (string test in new[] { "reallife_econapp_test_tabs", "reallife_econapp_test_sample", "reallife_econapp_test_advice" })
        {
            Check(Regex.Matches(guideSource, "\"" + test + "\"").Count == 2, "outlook sources: the test " + test + " is on the options page and in the feature list");
        }
        string tests = All(@"^OutlookDirector\.Tests\.cs$");
        Check(tests.Contains("TabsRoutine") && tests.Contains("AdviceMistakes") && tests.Contains("\"PASS\"") && tests.Contains("FAIL"), "outlook sources: the tabs test draws every tab, checks the advice against the game's own figures and says PASS or FAIL in the log");
        string director = All(@"^OutlookDirector\.cs$");
        Check(director.Contains("StartLine.Holds(StartSeats.Outlook)") && director.Contains("HotSpots.Time(HotTick)") && director.Contains("AfterDay += _onDay") && director.Contains("AfterHour += _onHour"),
            "outlook sources: the director waits for its seat, opens a scope for the session record, and follows the game's day and hour");
    }
}
