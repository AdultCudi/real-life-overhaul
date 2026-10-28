using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-019: what the mod does when BizMan opens a business, switches business or changes tab stays small.
// A player's report (27 Oct 2026) was that BizMan got slower with every release. The mod's part in a step is a handful of handlers: the
// pieces on BizMan's pages (the Interior tab, the presentation's refusal, the start screen's cards) and the listeners on the game's
// registration change event, which BizMan raises each time a business view or the Schedule tab is left. Each is listed here, and none of
// them subscribes to anything, searches the scene, walks every registration, rebuilds a layout, reads the disk or uses LINQ; the ones that
// run on every step allocate nothing; each opens BizPaceWatch.Time so the session record and the test in Options name it. A new listener
// on the registration change event, or a new component on a BizMan page, that is not in these lists fails the check: add it, time it, keep it light.
internal static partial class Program
{
    // The methods that run in a BizMan step, by file under Scripts. Timed: opens BizPaceWatch.Time. Cheap: allocates nothing and does no more than a lookup.
    private static readonly (string File, string Method, bool Timed, bool Cheap)[] BizHandlers =
    {
        ("Interiors/InteriorTab.cs", "Fix", true, true),
        ("Interiors/InteriorTab.cs", "OnLateUpdate", false, true),
        ("Interiors/InteriorTab.cs", "OnEnable", false, true),
        ("Interiors/InteriorTab.cs", "Clicked", false, false),
        ("Interiors/InteriorTab.Pages.cs", "Write", true, false),
        ("Interiors/InteriorTab.Pages.cs", "Written", false, true),
        ("Interiors/InteriorTab.Pages.cs", "Rewrite", false, false),
        ("Interiors/InteriorTab.Pages.cs", "WriteRight", false, false),
        ("Interiors/InteriorTab.Pages.cs", "WriteItems", false, false),
        ("Interiors/InteriorTab.Pages.cs", "WriteCustomers", false, false),
        ("Interiors/InteriorTab.Pages.cs", "WriteRules", false, false),
        ("Interiors/InteriorTab.Pages.cs", "WriteScores", false, false),
        ("Interiors/InteriorTab.Pages.cs", "WriteNow", false, false),
        ("Interiors/InteriorTab.Pages.cs", "WriteEffects", false, false),
        ("Core/ClaimedOffers.cs", "OnLateUpdate", false, true),
        ("Core/ClaimedOffers.cs", "Look", true, false),
        ("Core/ClaimedOffers.cs", "Hold", false, true),
        ("Tech/Firms/StartCards.cs", "Write", true, false),
        ("Tech/Firms/StartCards.cs", "OnTransformChildrenChanged", false, true),
        ("Tech/Firms/StartCards.cs", "OnLateUpdate", false, true),
        ("Core/SpecialNames.cs", "OnRegistration", true, true),
        ("Corners/CornerSigns.cs", "OnChange", true, true),
        ("Offices/Conversions.cs", "OnChange", true, true),
        ("Offices/Conversions.cs", "Facts", false, true),
        ("Tech/TechDirector.Infra.cs", "OnRegistrationChange", true, true),
        ("Tech/Infra/InfraGame.cs", "IsWarehouse", false, true),
        ("BizPace/BizPaceGame.cs", "ViewShown", false, true),
        ("BizPace/BizPaceGame.cs", "TabShown", false, true),
        ("BizPace/BizPaceGame.cs", "OnEnable", false, true),
        ("BizPace/BizPaceWatch.cs", "Time", false, true),
        ("BizPace/BizPaceWatch.cs", "Add", false, true),
        ("BizPace/BizPaceWatch.cs", "Mark", false, true),
        ("BizPace/BizPaceWatch.cs", "Frame", false, true)
    };

    // What no handler of a step does. (Arrays and strings are the Cheap rule's: RLO-PERF-003's patterns.)
    private static readonly (string What, Regex Pattern)[] BizBanned =
    {
        ("a subscription made", new Regex(@"\b(?:GlobalEvents|GameEvent|LocalizorManager|SceneManager|Canvas)\.\w+\s*\+=|\.AddListener\s*\(|\bEventHold\.Keep\s*\(|\bDelegate\.Combine\s*\(|\bSaveHook\.Keep\s*\(|\.onClick\s*\+=", RegexOptions.Compiled)),
        ("a search of the scene", new Regex(@"\b(?:FindObjectOfType|FindObjectsOfType|FindObjectsByType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsOfTypeAll)\b|\bGameObject\.Find\w*\s*\(|\bSceneFinds\.|\bGetComponentsInChildren\b|\bGetComponentInChildren\b", RegexOptions.Compiled)),
        ("every registration walked", new Regex(@"\bBuildingRegistrations\b|\bforeach\s*\(\s*(?:var|BuildingRegistration)\s+\w+\s+in\s+\w*(?:regs|registrations)\b|\b(?:InfraGame\.Warehouses|FirmsGame\.Seen|InteriorGame\.Places|GameDlc\.Open)\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("a layout rebuilt", new Regex(@"\bForceRebuildLayoutImmediate\b|\bForceUpdateCanvases\b|\.Fit\s*\(\s*\)", RegexOptions.Compiled)),
        ("the disk read", new Regex(@"\bFile\.\w+\s*\(|\bDirectory\.\w+\s*\(|\bResources\.Load\w*\s*\(|\bLoadAsset\w*\s*\(|\bStreamReader\b|\bWWW\b|\bUnityWebRequest\b", RegexOptions.Compiled)),
        ("LINQ", new Regex(@"\.(?:Where|Select|OrderBy|OrderByDescending|ToList|ToArray|Any|First|FirstOrDefault|Last|Sum|Max|Min|Count|Contains)\s*\(\s*(?:\w+\s*=>|\)|\()|\busing\s+System\.Linq\b", RegexOptions.Compiled))
    };

    // The game's registration change event raised by BizMan, and the components put on BizMan's pages: each file that does either is named.
    private static readonly string[] BizEventFiles =
    {
        "Core/SpecialNames.cs", "Corners/CornerSigns.cs", "Offices/Conversions.cs", "Tech/TechDirector.Infra.cs"
    };

    // The first four put components on BizMan's pages. The last two only name BizMan (to open it, to read its unsaved changes) and put components on
    // objects of their own: the city map's plots, the phone's apps.
    private static readonly string[] BizPageFiles =
    {
        "Core/ClaimedOffers.cs", "Interiors/InteriorTab.cs", "Tech/Firms/StartCards.cs", "BizPace/BizPaceGame.cs",
        "Construction/BuildModeGame.Plots.cs", "Core/GameUi/PhoneApps.cs"
    };

    private static readonly Regex BizEventListener = new Regex(@"onBuildingRegistrationChange\s*\+=|Delegate\.Combine\s*\(\s*GlobalEvents\.onBuildingRegistrationChange|EventHold\.Keep\s*\(\s*ref\s+GlobalEvents\.onBuildingRegistrationChange", RegexOptions.Compiled);
    private static readonly Regex BizPageHook = new Regex(@"AddComponent<", RegexOptions.Compiled);
    private static readonly Regex BizPageSubject = new Regex(@"\bfullMenu\.bizMan\b|\bBizManBusiness\b|\bStartBusinessUI\b|\bBizManPresentation\b|\bBizManInsight\b|\bBizManSettings\b|\bBizManSchedule\b", RegexOptions.Compiled);

    private static void BizPaceChecks()
    {
        const string rule = "RLO-PERF-019";
        var files = new Dictionary<string, SourceFile>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            files[f.Rel] = f;
        }
        foreach ((string file, string method, bool timed, bool cheap) in BizHandlers)
        {
            string rel = ScriptsDir + "/" + file;
            Examine(rule, rel);
            if (!files.TryGetValue(rel, out SourceFile f))
            {
                Hit(rule, rel, 0, "the file is gone: put the list of BizMan's handlers right");
                continue;
            }
            Match m = Regex.Match(f.CodeText, @"[\w>\]]\s+" + method + @"\s*\([^;{}]*\)\s*\{");
            if (!m.Success || !f.Block(m.Index, out int open, out int close))
            {
                Hit(rule, rel, 0, method + " is gone: put the list of BizMan's handlers right");
                continue;
            }
            int bodyLine = f.LineAt(open);
            string body = f.CodeText.Substring(open + 1, close - open - 1);
            if (timed && !body.Contains("BizPaceWatch.Time("))
            {
                Hit(rule, rel, bodyLine, method + " is a step of BizMan and does not open BizPaceWatch.Time(...) on its work");
            }
            string[] lines = body.Split('\n');
            for (int k = 0; k < lines.Length; k++)
            {
                foreach ((string what, Regex pattern) in BizBanned)
                {
                    if (pattern.IsMatch(lines[k]))
                    {
                        Hit(rule, rel, bodyLine + k + 1, method + " (a step of BizMan): " + what + ": " + lines[k]);
                    }
                }
                if (!cheap)
                {
                    continue;
                }
                foreach ((string what, Regex pattern) in Allocations)
                {
                    if (what != "a lambda or closure" && pattern.IsMatch(lines[k]) && !lines[k].Contains("Meta.Dev.Verbose") && !lines[k].Contains("Logger"))
                    {
                        Hit(rule, rel, bodyLine + k + 1, method + " (every BizMan step): " + what + ": " + lines[k]);
                    }
                }
            }
        }

        // A listener on the registration change event, or a component on BizMan's pages, in a file the lists do not name.
        foreach (SourceFile f in Scripts)
        {
            string name = f.Rel.Substring(ScriptsDir.Length + 1);
            bool eventFile = Array.IndexOf(BizEventFiles, name) >= 0, pageFile = Array.IndexOf(BizPageFiles, name) >= 0;
            if (!eventFile && BizEventListener.IsMatch(f.CodeText))
            {
                Hit(rule, f.Rel, 0, "listens to the game's registration change event, which BizMan raises each time a business view or the Schedule tab is left: name its handler in BizHandlers (StandardCheck/BizPaceChecks.cs), time it with BizPaceWatch.Time and keep it light");
            }
            if (!pageFile && BizPageSubject.IsMatch(f.CodeText) && BizPageHook.IsMatch(f.CodeText))
            {
                Hit(rule, f.Rel, 0, "puts a component on BizMan's pages: name its handlers in BizHandlers (StandardCheck/BizPaceChecks.cs), time them with BizPaceWatch.Time and keep them light");
            }
        }
    }
}
