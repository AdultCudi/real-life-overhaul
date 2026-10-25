using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Where the mod's options are kept (Scripts/Core/OptionKeysRules, OptionKeys, Settings). The game gives each copy of a mod its
// own id (the Workshop copy its item number, a ModsLocal copy its folder's full path) and the options were kept under that id,
// so moving between the Workshop and the installer, or having both, lost every choice. Now every option is kept under the
// mod's own id, and one start per computer brings over what older versions kept, the running copy's first, the old keys left
// in place. First the game-free rules against a pretend PlayerPrefs, then the sources (no key built from the copy's id
// anywhere, the order at start, a save under the fixed id), then the texts and What's new.
internal static partial class Program
{
    private const string LocalBack = @"C:\Users\someone\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\RealLifeOverhaul";
    private const string LocalForward = "C:/Users/someone/AppData/LocalLow/Hovgaard Games/Big Ambitions/ModsLocal/RealLifeOverhaul";

    private static void OptionKeysFix()
    {
        OptionKeysWhere();
        OptionKeysOrder();
        OptionKeysMove();
        OptionKeysSources();
        OptionKeysTexts();
    }

    // A pretend PlayerPrefs: what is kept under each key, and every write.
    private sealed class PretendPrefs : IOptionStore
    {
        public readonly Dictionary<string, string> Kept = new Dictionary<string, string>(StringComparer.Ordinal);
        public int Writes;

        public bool Has(string key) => Kept.ContainsKey(key);

        public void Copy(string from, string to)
        {
            Kept[to] = Kept[from];
            Writes++;
        }

        public void Mark(string key)
        {
            Kept[key] = "1";
            Writes++;
        }

        // A save as the options page makes it: under the key Settings.PrefsKey gives, which is OptionKeysRules.Key.
        public void Save(string id, string value)
        {
            Kept[OptionKeysRules.Key(id)] = value;
            Writes++;
        }
    }

    private static void OptionKeysWhere()
    {
        Check(OptionKeysRules.FixedId == "RealLifeOverhaul" && OptionKeysRules.Key("texts_service") == "m:RealLifeOverhaul:texts_service",
            "options kept: under the mod's own id in the game's own format, m:RealLifeOverhaul:<option>, whatever the game calls the copy");
        Check(OptionKeysRules.WorkshopId == "3816536560" && OptionKeysRules.OldKey(OptionKeysRules.WorkshopId, "texts_service") == "m:3816536560:texts_service"
              && OptionKeysRules.OldKey(LocalBack, "texts_device") == "m:" + LocalBack + ":texts_device",
            "options kept: an older version's keys are read as the game wrote them, under the Workshop item's number or the ModsLocal folder's path");
        bool apart = true;
        foreach (string source in OptionKeysRules.Sources(LocalBack, LocalBack))
        {
            apart &= OptionKeysRules.Key("x") != OptionKeysRules.OldKey(source, "x");
        }
        Check(apart, "options kept: the fixed id's key is none of the older ones, so a save never lands where an older RLO reads");
    }

    private static void OptionKeysOrder()
    {
        Check(Same(OptionKeysRules.Sources(OptionKeysRules.WorkshopId, LocalBack), OptionKeysRules.WorkshopId, LocalBack, LocalForward),
            "options kept: the Workshop copy looks under its own number first, then the ModsLocal folder as the game writes it and with forward slashes");
        Check(Same(OptionKeysRules.Sources(LocalBack, LocalForward), LocalBack, OptionKeysRules.WorkshopId, LocalForward),
            "options kept: a ModsLocal copy looks under its own path first, then the Workshop's number, then the folder with forward slashes, none twice");
        Check(Same(OptionKeysRules.Sources(@"D:\Games\Mods\RLO", LocalBack + "\\"), @"D:\Games\Mods\RLO", OptionKeysRules.WorkshopId, LocalBack, LocalForward),
            "options kept: a copy in a folder of another name looks under its own path first; a trailing slash on the data folder is no other place");
        Check(Same(OptionKeysRules.Sources(null, LocalBack), OptionKeysRules.WorkshopId, LocalBack, LocalForward)
              && Same(OptionKeysRules.Sources(string.Empty, null), OptionKeysRules.WorkshopId)
              && Same(OptionKeysRules.Sources("realLifeOverhaul", null), OptionKeysRules.WorkshopId)
              && Same(OptionKeysRules.Sources(LocalBack.ToUpperInvariant(), LocalBack), LocalBack.ToUpperInvariant(), OptionKeysRules.WorkshopId, LocalForward),
            "options kept: no running id, no data folder, the fixed id itself and a path in other letters each give no extra or doubled place");
    }

    private static bool Same(List<string> got, params string[] wanted)
    {
        if (got.Count != wanted.Length)
        {
            return false;
        }
        for (int i = 0; i < wanted.Length; i++)
        {
            if (got[i] != wanted[i])
            {
                return false;
            }
        }
        return true;
    }

    private static void OptionKeysMove()
    {
        // The owner's case: the Workshop copy kept the service, the ModsLocal copy the device and an older service; the fixed id
        // has the camera already (chosen in a build with this fix).
        var prefs = new PretendPrefs();
        prefs.Kept["m:3816536560:texts_service"] = "1";
        prefs.Kept["m:" + LocalBack + ":texts_service"] = "0";
        prefs.Kept["m:" + LocalBack + ":texts_device"] = "2";
        prefs.Kept["m:" + LocalForward + ":meta_seen_release"] = "0.51.0";
        prefs.Kept["m:3816536560:camera_enabled"] = "0";
        prefs.Kept[OptionKeysRules.Key("camera_enabled")] = "1";
        var ids = new List<string> { "texts_service", "texts_device", "meta_seen_release", "camera_enabled", "never_saved", null, "", "texts_service" };

        Check(OptionKeysRules.Due(prefs), "options kept: the move is due on a computer it never ran on");
        var move = new OptionMove();
        move.Begin(OptionKeysRules.Sources(OptionKeysRules.WorkshopId, LocalBack));
        int taken = OptionKeysRules.Move(prefs, ids, move);
        Check(taken == 3 && move.Total == 3 && prefs.Kept[OptionKeysRules.Key("texts_service")] == "1",
            "options kept: started from the Workshop, its own choice of service (ChatGPT) is the one taken, not the ModsLocal copy's older one");
        Check(prefs.Kept[OptionKeysRules.Key("texts_device")] == "2" && prefs.Kept[OptionKeysRules.Key("meta_seen_release")] == "0.51.0",
            "options kept: what only the other copy kept is taken from it, under either way of writing its folder, a text as a text");
        Check(prefs.Kept[OptionKeysRules.Key("camera_enabled")] == "1" && !prefs.Has(OptionKeysRules.Key("never_saved")),
            "options kept: a value under the fixed id already is never overwritten, and an option no copy saved stays at its default");
        Check(prefs.Kept["m:3816536560:texts_service"] == "1" && prefs.Kept["m:" + LocalBack + ":texts_service"] == "0" && prefs.Kept["m:3816536560:camera_enabled"] == "0",
            "options kept: the old keys stay as they were, for an older RLO that still reads them");
        Check(move.Taken[0] == 1 && move.Taken[1] == 1 && move.Taken[2] == 1 && OptionKeysRules.From(move) == "3816536560 (1), " + LocalBack + " (1), " + LocalForward + " (1)",
            "options kept: the test says how many came from which older place (" + OptionKeysRules.From(move) + ")");

        // The page's rows looked at after the start's list: nothing counted twice, a row only the page has is still taken.
        prefs.Kept["m:3816536560:only_on_page"] = "7";
        int late = OptionKeysRules.Move(prefs, new[] { "texts_service", "texts_device", "only_on_page" }, move);
        Check(late == 1 && move.Total == 4 && prefs.Kept[OptionKeysRules.Key("only_on_page")] == "7",
            "options kept: the second pass over the options page's rows takes only what the first missed and counts nothing twice");

        // The move is done for good: a reset afterwards (a key deleted under the fixed id) is never undone at a later start.
        OptionKeysRules.Finish(prefs, move);
        prefs.Kept.Remove(OptionKeysRules.Key("texts_device"));
        Check(move.Finished && !OptionKeysRules.Due(prefs), "options kept: once finished the move is not due again, so a reset to defaults stands");

        // A save after the move lands under the fixed id, and the next start of the other copy reads it there.
        prefs.Save("texts_service", "3");
        Check(prefs.Kept[OptionKeysRules.Key("texts_service")] == "3" && prefs.Kept["m:3816536560:texts_service"] == "1" && prefs.Kept["m:" + LocalBack + ":texts_service"] == "0",
            "options kept: a save of an option lands under the fixed id only, where the Workshop copy and a ModsLocal copy both read it");

        // Started from the ModsLocal copy instead, on a fresh computer: its own value of the service wins.
        var other = new PretendPrefs();
        other.Kept["m:3816536560:texts_service"] = "1";
        other.Kept["m:" + LocalBack + ":texts_service"] = "0";
        var local = new OptionMove();
        local.Begin(OptionKeysRules.Sources(LocalBack, LocalForward));
        OptionKeysRules.Move(other, new[] { "texts_service" }, local);
        Check(other.Kept[OptionKeysRules.Key("texts_service")] == "0" && local.Taken[0] == 1 && local.Taken[1] == 0,
            "options kept: started from the ModsLocal copy, its own value is taken before the Workshop's");

        // Nothing kept anywhere (a new player): nothing written but the marker.
        var fresh = new PretendPrefs();
        var none = new OptionMove();
        none.Begin(OptionKeysRules.Sources(OptionKeysRules.WorkshopId, LocalBack));
        OptionKeysRules.Move(fresh, ids, none);
        OptionKeysRules.Finish(fresh, none);
        Check(none.Total == 0 && fresh.Writes == 1 && OptionKeysRules.From(none) == string.Empty, "options kept: a new player's start writes nothing but the marker");
    }

    private static void OptionKeysSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        var built = new List<string>();
        var prefsById = new List<string>();
        var copyId = new List<string>();
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string name = file.Substring(scripts.Length + 1).Replace('\\', '/');
            string[] lines = System.IO.File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (Regex.IsMatch(line, @"""m:""\s*\+\s*_?[mM]od[iI]d") && name != "Core/OptionKeysRules.cs"
                    && !(name == "Core/Settings.cs" && line.Contains("private static string Key(string modId, string id) => \"m:\" + modId + \":\" + id;")))
                {
                    built.Add(name + ":" + (i + 1));
                }
                if (name != "Core/Settings.cs" && line.Contains("PlayerPrefs.") && Regex.IsMatch(line, @"\b_?[mM]od[iI]d\b|ModId\b"))
                {
                    prefsById.Add(name + ":" + (i + 1));
                }
                if (line.Contains("context.ModId") && !CopyIdAllowed(name, line))
                {
                    copyId.Add(name + ":" + (i + 1));
                }
            }
        }
        Check(built.Count == 0, "options kept: no PlayerPrefs key is built from a mod id but the fixed one's and the older keys' readers (" + string.Join(", ", built) + ")");
        Check(prefsById.Count == 0, "options kept: no PlayerPrefs call outside Settings (whose one id is the fixed one, below) names a mod id (" + string.Join(", ", prefsById) + ")");
        Check(copyId.Count == 0, "options kept: the id the game gives the copy is used only to bring older options over and to tell two copies apart (" + string.Join(", ", copyId) + ")");

        string settings = ModSource(@"Core\Settings.cs");
        string start = MethodText(settings, "public static void Start(string running)");
        int bring = start.IndexOf("OptionKeys.Bring(running);", StringComparison.Ordinal), load = start.IndexOf("Load(OptionKeysRules.FixedId);", StringComparison.Ordinal);
        Check(bring > 0 && load > bring, "options kept: at start the older options are brought over before the first read, which is under the fixed id");
        Check(settings.Contains("private static void Load(string modId)") && !settings.Contains("public static void Load(")
              && Regex.Matches(settings, @"(?<![.\w])Load\((?!string)").Count == 2 && settings.Contains("                Load(_modId);"),
            "options kept: Settings reads only from Start (the fixed id) and from Reload (the id Start gave), never with the copy's id");
        string load2 = MethodText(settings, "private static void Load(string modId)");
        Check(load2.Contains("bool starting = _modId == null;") && load2.IndexOf("bool starting", StringComparison.Ordinal) < load2.IndexOf("_modId = modId;", StringComparison.Ordinal)
              && load2.Contains("UnityEngine.PlayerPrefs.SetInt(Key(modId, PermadeathId), 0);") && load2.Contains("Permadeath = Bool(modId, PermadeathId, false);"),
            "options kept: Permadeath is still written off at the first read of a run (the main menu), then read, as before");
        Check(Regex.IsMatch(load2, @"Keys\.Load\(\);[\s\S]*CharacterCatalogue\.Load\(\);[\s\S]*PerfTweaks\.Load\(\);[\s\S]*JusticeSettings\.Load\(\);[\s\S]*RoommateOptions\.Load\(\);"),
            "options kept: the key bindings, the looks, the performance tweaks, justice and the roommate are read in the same read, with no id of their own");
        foreach (string file in new[] { @"Core\Keys.cs", @"Character\CharacterCatalogue.cs", @"Performance\PerfTweaks.cs", @"Justice\JusticeSettings.cs", @"Home\Roommates\RoommateSettings.cs" })
        {
            string code = ModSource(file);
            Check(code.Contains("OptionKeysRules.Key(") && !code.Contains("Load(string modId)") && !code.Contains("\"m:\""),
                "options kept: " + file + " keeps its options under OptionKeysRules.Key, with no key format or mod id of its own");
        }

        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        int started = mod.IndexOf("Settings.Start(context.ModId);", StringComparison.Ordinal);
        int finish = mod.IndexOf("if (OptionKeys.Finish(options, log))", StringComparison.Ordinal);
        int register = mod.IndexOf("OptionsService.Register(OptionKeysRules.FixedId, Guard.Rows(Guard.Organised(context, options)));", StringComparison.Ordinal);
        Check(started > 0 && finish > started && register > finish && mod.Contains("OptionsService.RemoveModOptions(OptionKeysRules.FixedId);")
              && !mod.Contains("OptionsService.Register(context.ModId") && !mod.Contains("RemoveModOptions(_context.ModId)"),
            "options kept: the page is registered under the fixed id (the game's own controls save there too), after the page's rows were brought over");
        string layout = ModSource(@"Meta\OptionsLayout.cs");
        Check(layout.Contains("_modId = OptionKeysRules.FixedId;") && !layout.Contains("_modId = context.ModId;"),
            "options kept: the options page looks itself up under the id it was registered under");
        Check(layout.Contains("Features.Find(\"diagnostics\")?.AddTest(OptionKeys.TestKey, OptionKeys.Test);")
              && layout.Contains("test.LabelKey == LayoutTestKey || test.LabelKey == OptionKeys.TestKey"),
            "options kept: the test is under Diagnostics' tests and works in the main menu too");

        string keys = ModSource(@"Core\OptionKeys.cs");
        Check(keys.Contains("if (!OptionKeysRules.Due(Store))") && keys.Contains("Consts(typeof(Settings), into);") && keys.Contains("Keys.PrefIds(into);")
              && keys.Contains("PerfTweaks.PrefIds(into);") && keys.Contains("CharacterCatalogue.Slots") && keys.Contains("Consts(typeof(Meta.Welcome), into);")
              && keys.Contains("Path.Combine(UnityEngine.Application.persistentDataPath, \"ModsLocal\", OptionKeysRules.LocalFolder)"),
            "options kept: the start's list holds every holder's ids, and the ModsLocal folder is named as the game names it (its full path)");

        // RLO Texts' sign-in, keys and service file are kept by path in the game's data folder, the same for every copy.
        string game = ModSource(@"Texts\TextsGame.cs"), service = ModSource(@"Texts\TextsDirector.Service.cs");
        Check(game.Contains("public static string Home => Path.Combine(Application.persistentDataPath, TextPacks.Fixed);")
              && service.Contains("new TextKeys(TextsGame.Home)") && service.Contains("Path.Combine(TextsGame.Home, ConfigFile)"),
            "options kept: RLO Texts' keys (service-keys.bin) and service.txt are in the game's data folder, shared by every copy already");
    }

    // The copy's id may tell two copies apart and name where older versions kept the options; nothing else.
    private static bool CopyIdAllowed(string file, string line)
    {
        return line.Contains("Settings.Start(context.ModId);")
               || line.Contains("_started != context.ModId") || line.Contains("_started = context.ModId;")
               || line.Contains("_running != context.ModId") || line.Contains("_running = context.ModId;")
               || (file == "Meta/MetaCity.cs" && line.Contains("new ModContext(context.ModRootPath, context.ModId, new GatedLogger(context.Logger))"));
    }

    private static void OptionKeysTexts()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("options kept", langs, codes))
        {
            return;
        }
        var lacking = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (codes[l] != "en" && codes[l] != "pt" && codes[l] != "zh-cn")
            {
                continue;
            }
            foreach (string key in new[] { "reallife_opt_test_kept", "reallife_opt_test_kept_taken", "reallife_opt_test_kept_none", "reallife_opt_test_kept_before" })
            {
                if (!langs[l].TryGetValue(key, out string text) || text.Length == 0 || (key != "reallife_opt_test_kept" && !text.Contains("{id}"))
                    || (key == "reallife_opt_test_kept_taken" && !(text.Contains("{count}") && text.Contains("{from}"))))
                {
                    lacking.Add(codes[l] + ":" + key);
                }
            }
        }
        Check(lacking.Count == 0, "options kept: the test's button and its three answers, with their names in curly brackets, in en, pt and zh-cn (lacking: " + string.Join(", ", lacking) + ")");
        Check(langs[Array.IndexOf(codes, "en")]["reallife_opt_test_kept"] == "Options: where my options are kept", "options kept: the test's button reads 'Options: where my options are kept'");

        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Fixed: the Steam Workshop copy of the mod and a copy in ModsLocal",
            ["pt"] = "Corrigido: a cópia do mod na Workshop da Steam e uma cópia em ModsLocal",
            ["zh-cn"] = "已修复：Steam 创意工坊上的模组副本和 ModsLocal 中的副本",
            ["de"] = "Behoben: Die Kopie der Mod aus dem Steam Workshop und eine Kopie in ModsLocal",
            ["fr"] = "Corrigé : la copie du mod du Steam Workshop et une copie dans ModsLocal",
            ["es"] = "Corregido: la copia del mod del Workshop de Steam y una copia en ModsLocal",
            ["ru"] = "Исправлено: копия мода из Мастерской Steam и копия в ModsLocal",
            ["nl"] = "Opgelost: de kopie van de mod uit de Steam Workshop en een kopie in ModsLocal",
            ["tr"] = "Düzeltildi: Modun Steam Workshop kopyası ile ModsLocal içindeki bir kopya"
        };
        var missing = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (!lines.TryGetValue(codes[l], out string wanted) || !WhatsNewHas(langs[l], wanted))
            {
                missing.Add(codes[l]);
            }
        }
        Check(missing.Count == 0 && codes.Length == lines.Count,
            "options kept: What's new has the fix's line, with its language's fix mark, in some reallife_changes_ row of every language (lacking: " + string.Join(", ", missing) + ")");
    }
}
