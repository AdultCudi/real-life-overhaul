using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using IoPath = System.IO.Path;

// The mod's moods that grew to thousands of days (a player's report: "Did the right thing" 14833 days left, "A nice
// moment" 891, "A home-cooked meal" 71). The mod registered its moods with no cap (maxHoursDuration -1) and put every
// saved mood back on load through the game's HappinessHelper.AddModifier. Going Public scales a positive mood's hours on
// every AddModifier (Patch_AddModifier_PositiveDurationScale: times 100 / its morale tempo, ten by default), counting on
// each modifier's own cap, so each load in a session multiplied them again. Now every mood has a cap from its own
// catalogue entry (Core/MoodRules.Cap), a saved mood is written back with its saved hours (ModState.RestoreModifiers),
// and one past its cap is trimmed once on load. First the game-free rules, then the game's add and Going Public's scale
// as a model, the stash, the three catalogues and every caller's hours, the source, then What's new.
internal static partial class Program
{
    private static void MoodBuffs()
    {
        MoodBuffCaps();
        MoodBuffGoingPublic();
        MoodBuffStash();
        MoodBuffCatalogue();
        MoodBuffSources();
        Check(FixLineEverywhere("no longer grow to hundreds of days each time a save is loaded", "não crescem mais para centenas de dias",
                                "不再在每次加载存档时增长到几百天", "wachsen nicht mehr bei jedem Laden eines Spielstands auf Hunderte Tage an",
                                "ne grandissent plus jusqu'à des centaines de jours", "ya no crecen hasta cientos de días",
                                "больше не вырастают до сотен дней", "groeien niet meer tot honderden dagen",
                                "her kayıt yüklendiğinde yüzlerce güne çıkmıyor"),
            "mood buffs: What's new has the moods' line, with its fix mark, in some row of every language");
    }

    // ---------------------------------------------------------------- the cap

    private static void MoodBuffCaps()
    {
        int[,] expected =
        {
            { 2, 6 }, { 3, 9 }, { 6, 18 }, { 8, 24 }, { 10, 24 }, { 12, 24 }, { 23, 24 },
            { 24, 72 }, { 36, 108 }, { 48, 144 }, { 72, 168 }, { 96, 168 }, { 168, 168 }, { 200, 200 }
        };
        bool table = true;
        for (int i = 0; i < expected.GetLength(0); i++)
        {
            table &= MoodRules.Cap(expected[i, 0]) == expected[i, 1];
        }
        Check(table, "mood buffs: the cap is three times a mood's length, at most a day for a mood of hours and a week for one of days (8 h to 24, 24 h to 72, 72 h to 168)");
        Check(MoodRules.Cap(0) == -1 && MoodRules.Cap(-1) == -1, "mood buffs: a mood with no length of its own (until taken off) has no cap, as the game marks one");

        bool atLeast = true, hours = true, days = true;
        for (int h = 1; h <= 400; h++)
        {
            int cap = MoodRules.Cap(h);
            atLeast &= cap >= h;
            if (h < MoodRules.DayHours)
            {
                hours &= cap <= MoodRules.DayHours;
            }
            else
            {
                days &= cap >= MoodRules.DayHours && cap <= Math.Max(MoodRules.WeekHours, h);
            }
        }
        Check(atLeast, "mood buffs: a cap is never under the mood's own length, so a mood added as the catalogue has it is never cut");
        Check(hours, "mood buffs: a mood of hours stays hours: its cap is never more than a day");
        Check(days, "mood buffs: a mood of days stays days: its cap is a day or more and never more than a week (or its own length)");
        Check(MoodRules.Cap(int.MaxValue / 2) == int.MaxValue / 2, "mood buffs: the cap of a very long mood does not overflow");
    }

    // ---------------------------------------------------------------- the game's add and Going Public's scale, as a model

    // Helpers.HappinessHelper.AddModifier, as the mod calls it (no additiveHours): a mood already running is set to the new
    // length, capped (GetCappedHoursDuration); never added on top. 0 or less as a cap means none.
    private static int GameAdd(int hours, int cap)
    {
        return hours < 0 || cap <= 0 ? hours : Math.Min(hours, cap);
    }

    // Going Public's prefix (MPNeedsTuning.Patch_AddModifier_PositiveDurationScale): a positive mood's hours times 100 / tempo.
    private static int GoingPublicScaled(int amount, int hours, int moralePercent)
    {
        return amount > 0 && hours > 0 ? Math.Max(1, (int)Math.Round(hours * (100.0 / Math.Max(1, moralePercent)))) : hours;
    }

    private static void MoodBuffGoingPublic()
    {
        // The report, the old way: "Did the right thing" (+8, 24 h), no cap, put back through AddModifier on every load.
        int old = 24;
        for (int load = 0; load < 4; load++)
        {
            old = GameAdd(GoingPublicScaled(8, old, 10), -1);
        }
        Check(old == 240000, "mood buffs: the report reproduced: a day's mood with no cap, put back through Going Public's scaled add on four loads, has 240,000 hours (" + old + ")");

        // Now: put back as saved, every load; each add capped however Going Public scales it.
        int cap = MoodRules.Cap(24);
        int now = GameAdd(GoingPublicScaled(8, 24, 10), cap);
        bool steady = now == cap;
        for (int load = 0; load < 50; load++)
        {
            now = MoodRules.Kept(now, cap);
            steady &= now == cap;
        }
        Check(steady, "mood buffs: in Going Public a day's good mood is scaled to its cap of 72 h, and fifty loads keep it at 72 h");

        bool capped = true, refreshed = true;
        foreach (int tempo in new[] { 1, 5, 10, 50, 99, 100, 300 })
        {
            foreach (int length in new[] { 2, 3, 8, 12, 24, 48, 72, 96, 144 })
            {
                int moodCap = MoodRules.Cap(length);
                int left = 0;
                for (int add = 0; add < 20; add++)
                {
                    left = GameAdd(GoingPublicScaled(5, length, tempo), moodCap);
                    capped &= left <= moodCap;
                }
                refreshed &= left == Math.Min(GoingPublicScaled(5, length, tempo), moodCap);
            }
        }
        Check(capped, "mood buffs: a good mood added twenty times at any morale tempo never has more hours left than its cap");
        Check(refreshed, "mood buffs: a mood added again is refreshed to its length (scaled, capped), never piled on what is left");
        Check(GameAdd(GoingPublicScaled(-8, 12, 10), MoodRules.Cap(12)) == 12, "mood buffs: a bad mood is not scaled by Going Public and keeps its own length");
    }

    // ---------------------------------------------------------------- the stash and the trim on load

    private static void MoodBuffStash()
    {
        Check(MoodRules.Kept(10, MoodRules.Cap(24)) == 10 && MoodRules.Kept(72, MoodRules.Cap(24)) == 72 && MoodRules.Kept(1, MoodRules.Cap(8)) == 1,
            "mood buffs: a saved mood within its cap comes back with exactly its saved hours");
        Check(MoodRules.Kept(14833 * 24, MoodRules.Cap(24)) == 72 && MoodRules.Kept(891 * 24, MoodRules.Cap(8)) == 24
              && MoodRules.Kept(71 * 24, MoodRules.Cap(8)) == 24,
            "mood buffs: the report's save trimmed once: 14833 days of a day's mood to 72 h, 891 and 71 days of an 8 h mood to 24 h");
        Check(MoodRules.Kept(500, -1) == 500 && MoodRules.Kept(500, 0) == 500, "mood buffs: a mood with no cap is never trimmed");

        bool read = MoodRules.TryRead("reallife_happinessmodifier_helped=355992", out string type, out int hours)
                    && type == "reallife_happinessmodifier_helped" && hours == 355992;
        Check(read, "mood buffs: a stash entry reads as its type and hours");
        bool refused = true;
        foreach (string bad in new[] { null, "", "x", "=5", "x=", "x=0", "x=-3", "x=a", "x=5=6", "x=99999999999" })
        {
            refused &= !MoodRules.TryRead(bad, out string t, out int h) && t == null && h == 0;
        }
        Check(refused, "mood buffs: an entry with no type, no hours, none left or not a number is not put back");

        // The stash as BeforeSave writes it, read back: each mood once, as saved.
        var back = new Dictionary<string, int>();
        foreach (string entry in "a=5;b=0;c=72;;d=x;a=9".Split(';'))
        {
            if (MoodRules.TryRead(entry, out string t, out int h) && !back.ContainsKey(t))
            {
                back[t] = h;
            }
        }
        Check(back.Count == 2 && back.TryGetValue("a", out int a) && a == 5 && back.TryGetValue("c", out int c) && c == 72, "mood buffs: a stash read back gives each mood once, with the hours it was saved with");
    }

    // ---------------------------------------------------------------- the catalogues and every caller's hours

    private static string MoodScripts()
    {
        return IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
    }

    // A catalogue's moods: (class, constant) to (type, hours), read from its source.
    private static void MoodTable(string file, string className, string tableStart, Dictionary<string, string> types, Dictionary<string, int> lengths, List<string> order)
    {
        string code = ModSource(file);
        foreach (Match m in Regex.Matches(code, "public const string (\\w+) = \"(reallife_happinessmodifier_\\w+)\";"))
        {
            types[className + "." + m.Groups[1].Value] = m.Groups[2].Value;
        }
        int at = code.IndexOf(tableStart, StringComparison.Ordinal);
        int end = at < 0 ? -1 : code.IndexOf("};", at, StringComparison.Ordinal);
        if (end < 0)
        {
            return;
        }
        foreach (Match m in Regex.Matches(code.Substring(at, end - at), @"\((\w+),\s*[^,()]+,\s*(\d+)\)"))
        {
            string key = className + "." + m.Groups[1].Value;
            if (types.TryGetValue(key, out string type))
            {
                lengths[type] = int.Parse(m.Groups[2].Value);
                order.Add(type);
            }
        }
    }

    private static void MoodBuffCatalogue()
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        var lengths = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();
        MoodTable(@"Core\ModState.cs", "ModState", "ModifierDefs =", types, lengths, order);
        MoodTable(@"Family\FamilyModifiers.cs", "FamilyModifiers", "Defs =", types, lengths, order);
        MoodTable(@"Travel\TravelEffects.cs", "TravelEffects", "Modifiers =", types, lengths, order);
        Check(order.Count == 52 && lengths.Count == 52, "mood buffs: the three catalogues (the mod's, family's, travel's) are read: 52 moods, each once (" + order.Count + ", " + lengths.Count + ")");

        // Every call that names a mood and its hours as a number: never past the mood's cap, so no cap cuts what a feature meant.
        int calls = 0;
        var over = new List<string>();
        var call = new Regex(@"\b(?:AddModifier|Add|Keep)\(\s*(ModState|TravelEffects|FamilyModifiers)\.(\w+)\s*,\s*(\d+)\s*[,)]");
        foreach (string path in Directory.GetFiles(MoodScripts(), "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in call.Matches(File.ReadAllText(path)))
            {
                if (!types.TryGetValue(m.Groups[1].Value + "." + m.Groups[2].Value, out string type) || !lengths.TryGetValue(type, out int length))
                {
                    continue;
                }
                calls++;
                int asked = int.Parse(m.Groups[3].Value);
                if (asked > MoodRules.Cap(length))
                {
                    over.Add(IoPath.GetFileName(path) + ": " + m.Value + " over " + MoodRules.Cap(length));
                }
            }
        }
        Check(calls >= 12 && over.Count == 0, "mood buffs: every mood a feature adds with hours of its own is within its cap (" + calls + " calls" + (over.Count > 0 ? "; " + string.Join("; ", over) : "") + ")");

        // Lengths worked out at run time: the longest of each within its mood's cap.
        int Cap(string name) => types.TryGetValue(name, out string t) && lengths.TryGetValue(t, out int l) ? MoodRules.Cap(l) : -2;
        string balance = ModSource(@"Home\HomeBalance.cs");
        var longest = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(balance, @"case Illness\.(\w+): return (\d+) \+ \(int\)\(roll \* (\d+)f\);"))
        {
            longest[m.Groups[1].Value] = int.Parse(m.Groups[2].Value) + int.Parse(m.Groups[3].Value);
        }
        bool illness = longest.Count == 5;
        foreach (string infectious in new[] { "Cold", "Flu", "FoodPoisoning" })
        {
            illness &= longest.TryGetValue(infectious, out int h) && h <= Cap("ModState.Sick");
        }
        illness &= longest.TryGetValue("BackPain", out int back) && back <= Cap("ModState.BackPain")
                   && longest.TryGetValue("Burnout", out int burnout) && burnout <= Cap("ModState.Burnout");
        Check(illness, "mood buffs: the longest cold, flu, food poisoning, bad back and burnout (Home/HomeBalance.Hours) are within their moods' caps");

        int sickMost = 0, hurtMost = 0;
        foreach (string path in Directory.GetFiles(IoPath.Combine(MoodScripts(), "Life"), "*.cs"))
        {
            string code = File.ReadAllText(path);
            foreach (Match m in Regex.Matches(code, @"\.Sick\((\d+),\s*(\d+)\)"))
            {
                sickMost = Math.Max(sickMost, int.Parse(m.Groups[2].Value));
            }
            foreach (Match m in Regex.Matches(code, @"\.Hurt\((\d+),"))
            {
                hurtMost = Math.Max(hurtMost, int.Parse(m.Groups[1].Value));
            }
        }
        foreach (string path in Directory.GetFiles(MoodScripts(), "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(path), @"\.Injure\((\d+),"))
            {
                hurtMost = Math.Max(hurtMost, int.Parse(m.Groups[1].Value));
            }
        }
        Match shark = Regex.Match(ModSource(@"Economy\Banking\Terms\ProductCatalogue.cs"), @"SharkInjuryHours = (\d+);");
        hurtMost = Math.Max(hurtMost, shark.Success ? int.Parse(shark.Groups[1].Value) : int.MaxValue);
        Check(sickMost > 0 && sickMost <= Cap("ModState.Sick") && hurtMost > 0 && hurtMost <= Cap("ModState.Injured"),
            "mood buffs: the longest sickness and injury a life event, a crash, a beating or the loan shark gives are within their moods' caps (" + sickMost + " h, " + hurtMost + " h)");

        Match holiday = Regex.Match(ModSource(@"Travel\Holidays.Trip.cs"), @"int hours = Mathf\.Min\([^;]*,\s*(\d+)\);");
        Check(holiday.Success && int.Parse(holiday.Groups[1].Value) <= Cap("TravelEffects.Rested"),
            "mood buffs: the longest rest a holiday leaves (Travel/Holidays.Trip) is within its mood's cap");
    }

    // ---------------------------------------------------------------- the source, read as the game cannot be run here

    private static void MoodBuffSources()
    {
        string state = ModSource(@"Core\ModState.cs");
        string load = MemberText(state, "public static void Load()");
        string restore = MemberText(state, "private static void RestoreModifiers(GameInstance save, string stashed)");
        string trim = MemberText(state, "private static int Trim(HappinessModifierData data, int hours, int cap, string where)");
        Check(load.Contains("RestoreModifiers(save,") && !load.Contains("AddModifier("),
            "mood buffs: a load puts the saved moods back through RestoreModifiers, never through AddModifier (which Going Public scales)");
        Check(restore.Contains("MoodRules.TryRead(entry, out string type, out int hours)") && restore.Contains("list.Add(data);")
              && restore.Contains("hideDuration = modifier.hideDuration") && restore.Contains("Holds(list, type)")
              && restore.Contains("HappinessHelper.UpdateHappiness();") && !restore.Contains("AddModifier(")
              && trim.Contains("data.hoursLeft = kept;") && trim.Contains("MoodRules.Kept(hours, cap)"),
            "mood buffs: a saved mood is written into the game's list with its saved hours, once a type, as the game writes a new one");
        Check(restore.Contains("trimmed += Trim(held, held.hoursLeft, known.maxHoursDuration") && restore.Contains("trimmed += Trim(data, hours, modifier.maxHoursDuration")
              && trim.Contains("\"Moods check (load): \"") && restore.Contains("\"Moods check (load): \""),
            "mood buffs: a mood past its cap, saved or in the save's list, is trimmed once on load, with a check line");
        Check(restore.Contains("save.gameVariables.disableHappiness") && restore.Contains("!ModifiersReady"),
            "mood buffs: no mood is put back with happiness switched off or the moods unregistered, as before");

        string enter = MemberText(state, "internal static void Enter(Dictionary<string, HappinessModifier> registry, (string type, int amount, int hours)[] defs)");
        Check(enter.Contains("modifier.maxHoursDuration = MoodRules.Cap(hours);"), "mood buffs: every mood is registered with the cap its own length gives");
        string register = MemberText(state, "private static void RegisterModifiers()");
        Check(register.Contains("Enter(registry, ModifierDefs);") && register.Contains("Enter(registry, RealLifeOverhaul.Family.FamilyModifiers.Defs);")
              && register.Contains("Enter(registry, TravelEffects.Modifiers);"),
            "mood buffs: the load registers family's and travel's moods with the mod's, so a saved one always finds its type");
        Check(MemberText(state, "public static bool KnowsModifier(string type)").Contains("InTable(RealLifeOverhaul.Family.FamilyModifiers.Defs, type) || InTable(TravelEffects.Modifiers, type)"),
            "mood buffs: the save check knows family's and travel's moods, so it no longer takes them out after a load");
        Check(MemberText(ModSource(@"Family\FamilyModifiers.cs"), "public static void Register()").Contains("ModState.Enter(registry, Defs);")
              && MemberText(ModSource(@"Travel\TravelEffects.cs"), "public static void Register(BAModAPI.IModLogger logger)").Contains("ModState.Enter(registry, Modifiers);"),
            "mood buffs: family and travel register their moods through the one place that sets the cap");

        int noCap = 0, created = 0, additive = 0, gameAdds = 0;
        foreach (string path in Directory.GetFiles(MoodScripts(), "*.cs", SearchOption.AllDirectories))
        {
            string code = File.ReadAllText(path);
            noCap += Regex.Matches(code, @"maxHoursDuration\s*=\s*-1").Count;
            created += Regex.Matches(code, @"CreateInstance<HappinessModifier>\(\)").Count;
            additive += Regex.Matches(code, @"additiveHours\s*:").Count;
            gameAdds += Regex.Matches(code, @"HappinessHelper\.AddModifier\(").Count;
        }
        Check(noCap == 0 && created == 1, "mood buffs: no mood of the mod is registered without a cap, and moods are made in one place (" + noCap + " uncapped, " + created + " made)");
        Check(additive == 0 && gameAdds == 1 && MemberText(state, "public static void AddModifier(string type, int hours = -1)").Contains("HappinessHelper.AddModifier(type, hours);"),
            "mood buffs: the mod adds moods only through ModState.AddModifier and never with additive hours, so a repeated mood refreshes instead of piling up");
    }
}
