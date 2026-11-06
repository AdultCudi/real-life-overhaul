using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-021: what the mod runs in the frame the game turns the day in is listed, and why.
// GameManager.NewDay calls the game's day event in the middle of its own day (after the wages, the rent, the books and the businesses'
// and rivals' step; before the tax, the daily summary, the employees' and rivals' daily run and the hour's work), so everything the mod
// puts on that event, or on the economy's AfterDay that is shared out from it, runs in that one frame. A player's log (27 Oct 2026) put the
// mod's scripts there at 61 to 151 ms. Each handler is listed with its verdict: "kept" (it books money into yesterday's statement, writes
// something the game reads in that frame, or is a few comparisons) or "spread" (it only queues its work, which is played in the frames after:
// Core/DaySpread, Core/DayPump, and the Info lines of the diagnostics, Core/DayLines). A handler on the day event that is not in the
// list fails the check: decide it, list it with its reason, and if it can move, queue it. The bodies of the listed handlers hold no search
// of the scene, no rebuilt layout and no disk read.
internal static partial class Program
{
    // The file that puts the handler on the game's day (or on the economy's AfterDay), the file and method that is the handler, the verdict
    // ("kept" or "spread") and why. The number of subscriptions in a file must be the number of entries naming it.
    private static readonly (string SubscribedIn, string File, string Method, string Verdict, string Why)[] DayHandlers =
    {
        ("Social/Flare/FlareDirector.cs", "Social/Flare/FlareDirector.cs", "OnDay", "spread",
            "Flare's round (32 to 46 ms) is queued and played a step a frame (DaySteps); only the queueing is in the frame"),
        ("Economy/Ventures/VenturesDirector.cs", "Economy/Ventures/VenturesDirector.Day.cs", "OnDay", "spread",
            "the deals' shares and payouts are money booked into yesterday's statement before the game's tax reads it, so they stay; the weekly look (22 ms) only begins here and is made a business a frame"),
        ("Careers/CareersDirector.cs", "Careers/CareersDirector.cs", "OnDay", "spread",
            "an instalment is taken from the player's money, and a day's pay and benefits are paid, at midnight as the game's own are; the job board's renewal (13 ms on a save's first day) is queued in three stages"),
        ("Economy/Banking/EconomyDirector.cs", "Economy/Banking/EconomyDirector.Day.cs", "OnDay", "kept",
            "settles the game's loans against the rating after the game's daily run, takes the mod's accounts' payments, brings yesterday's statement up to date before the game's tax reads it, and shares the day out to the features below"),
        ("Economy/Company/CompanyDirector.cs", "Economy/Company/CompanyDirector.Day.cs", "OnDay", "kept",
            "premiums, agreed raises, sick pay, overtime and allowances are money booked into yesterday's statement before the game's tax and daily summary read it; the staff's absences are set before the game's employee run; the day's footfall figures are written for its customers"),
        ("Economy/City/CityEconomyDirector.cs", "Economy/City/CityEconomyDirector.cs", "OnDay", "kept",
            "the city's change to the day's rent, wages and takings is money taken and paid into yesterday's statement; its prices are written for the day's trade"),
        ("Economy/Brands/BrandsDirector.cs", "Economy/Brands/BrandsDirector.cs", "OnDay", "kept",
            "the brands' kept prices and footfall figures are written for the day's customers (about 1.5 ms)"),
        ("Economy/Estate/EstateDirector.cs", "Economy/Estate/EstateDirector.cs", "OnDay", "kept",
            "rents made good, a hotel's takings and costs and the year's tax are money booked into yesterday's statement (about 1.3 ms)"),
        ("Economy/Dealer/DealerDirector.cs", "Economy/Dealer/DealerDirector.Clock.cs", "OnDay", "kept",
            "finance plans' payments, warranty claims and the dealerships' takings are money booked into yesterday's statement; nothing at all without a dealership"),
        ("Economy/Online/OnlineDirector.cs", "Economy/Online/OnlineDirector.Day.cs", "OnDay", "kept",
            "the online shops' takings and refunds are money booked into yesterday's statement; nothing at all without an online shop"),
        ("Economy/OwnBank/OwnBankDirector.cs", "Economy/OwnBank/OwnBankDirector.cs", "OnDay", "kept",
            "the player's own bank's interest and instalments are money; nothing at all without one"),
        ("Economy/Services/ServicesDirector.cs", "Economy/Services/ServicesDirector.cs", "OnDay", "kept",
            "the services' takings and costs are money booked into yesterday's statement"),
        ("Economy/Banking/BankApps/BanksDirector.cs", "Economy/Banking/BankApps/BanksDirector.cs", "Badges", "kept",
            "the banks' apps' badge counts: a few comparisons, drawn nothing"),
        ("Outlook/OutlookDirector.cs", "Outlook/OutlookDirector.cs", "OnDay", "kept",
            "writes one row of the city's figures into the Economy app's history (33 figures, well under a tenth of a millisecond)"),
        ("Tech/TechDirector.cs", "Tech/TechDirector.cs", "OnDay", "kept",
            "the labour market is read by the professionals' day (its shortages), which sets the game's employees' moods and absences before the game's employee run; the firms and data centres book takings and costs into yesterday's statement; an accepted applicant starts as the game's employee"),
        ("Fame/FameDirector.cs", "Fame/FameDirector.cs", "OnDay", "kept",
            "the standing is worked from yesterday's statements and read the same frame by the company's and the tech sector's esteem; each business's customers are written for the day (a second or less after the first day)"),
        ("Core/HeldUnits.cs", "Core/HeldUnits.cs", "OnDay", "kept",
            "puts the game's ignore lists back as they were before the game's rivals' step, which follows the day event in the same frame"),
        ("Core/DayRollGuard.cs", "Core/DayRollGuard.cs", "OnDay", "kept",
            "notes the day seen: one assignment"),
        ("Districts/DistrictView.cs", "Districts/DistrictView.cs", "OnDay", "kept",
            "prunes its log and redraws only while the district map is showing"),
        ("Diagnostics/SessionWatch.Midnight.cs", "Diagnostics/SessionWatch.Midnight.cs", "NewDay", "kept",
            "diagnostics: one frame number written")
    };

    private static readonly Regex DaySubscription = new Regex(
        @"EventHold\.Keep\s*\(\s*ref\s+GlobalEvents\.onNewDay\b|GlobalEvents\.onNewDay\s*\+=|Delegate\.Combine\s*\(\s*GlobalEvents\.onNewDay\b|EconomyDirector\.AfterDay\s*\+=", RegexOptions.Compiled);

    // What no handler of the day does (RLO-PERF-003's allocations are not asked of them: a day handler allocates what a day needs).
    private static readonly (string What, Regex Pattern)[] DayBanned =
    {
        ("a search of the scene", new Regex(@"\b(?:FindObjectOfType|FindObjectsOfType|FindObjectsByType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsOfTypeAll)\b|\bGameObject\.Find\w*\s*\(|\bSceneFinds\.", RegexOptions.Compiled)),
        ("a layout rebuilt", new Regex(@"\bForceRebuildLayoutImmediate\b|\bForceUpdateCanvases\b", RegexOptions.Compiled)),
        ("the disk read", new Regex(@"\bFile\.\w+\s*\(|\bDirectory\.\w+\s*\(|\bResources\.Load\w*\s*\(|\bLoadAsset\w*\s*\(|\bStreamReader\b|\bUnityWebRequest\b", RegexOptions.Compiled))
    };

    private static void DayChecks()
    {
        const string rule = "RLO-PERF-021";
        var files = new Dictionary<string, SourceFile>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            files[f.Rel] = f;
        }

        // Every subscription to the game's day is in a file the list names, as many as it lists.
        var listed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((string subscribed, string file, string method, string verdict, string why) in DayHandlers)
        {
            listed[subscribed] = listed.TryGetValue(subscribed, out int n) ? n + 1 : 1;
            Check(verdict == "kept" || verdict == "spread", rule + ": " + file + " " + method + " has a verdict of kept or spread");
            Check(why.Length >= 25, rule + ": " + file + " " + method + " says why it runs in the frame the day turns in");
        }
        foreach (SourceFile f in Scripts)
        {
            string name = f.Rel.Substring(ScriptsDir.Length + 1);
            int found = DaySubscription.Matches(f.CodeText).Count;
            listed.TryGetValue(name, out int expected);
            if (found == 0 && expected == 0)
            {
                continue;
            }
            Examine(rule, f.Rel);
            if (found > expected)
            {
                Hit(rule, f.Rel, 0, found + " handler(s) on the game's day, " + expected + " listed: a handler on the day runs in the frame the game turns the day in; "
                                    + "decide it (kept, with the reason, or queued through DaySpread) and list it in StandardCheck/DayChecks.cs");
            }
            else if (found < expected)
            {
                Hit(rule, f.Rel, 0, found + " handler(s) on the game's day, " + expected + " listed: put the list of the day's handlers right (StandardCheck/DayChecks.cs)");
            }
        }

        // Each listed handler is there, and holds nothing that does not belong in a frame of the game's.
        foreach ((string subscribed, string file, string method, string verdict, string why) in DayHandlers)
        {
            string rel = ScriptsDir + "/" + file;
            Examine(rule, rel);
            if (!files.TryGetValue(rel, out SourceFile f))
            {
                Hit(rule, rel, 0, "the file is gone: put the list of the day's handlers right (StandardCheck/DayChecks.cs)");
                continue;
            }
            Match m = Regex.Match(f.CodeText, @"[\w>\]]\s+" + method + @"\s*\([^;{}]*\)\s*\{");
            if (!m.Success || !f.Block(m.Index, out int open, out int close))
            {
                Hit(rule, rel, 0, method + " is gone: put the list of the day's handlers right (StandardCheck/DayChecks.cs)");
                continue;
            }
            int bodyLine = f.LineAt(open);
            string[] lines = f.CodeText.Substring(open + 1, close - open - 1).Split('\n');
            for (int k = 0; k < lines.Length; k++)
            {
                foreach ((string what, Regex pattern) in DayBanned)
                {
                    if (pattern.IsMatch(lines[k]))
                    {
                        Hit(rule, rel, bodyLine + k + 1, method + " (on the game's day): " + what + ": " + lines[k]);
                    }
                }
            }
        }
    }
}
