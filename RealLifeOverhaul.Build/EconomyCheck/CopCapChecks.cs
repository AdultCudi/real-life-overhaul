using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Meta;

// A player found the police cap oversized and floating above the officers' heads, and asked to be able to leave it off
// (Scripts/People/OfficerCapRules.cs): "Police officers wear caps" (police_caps, on by default) leaves every officer bare
// headed when it is off, whichever cap they would have worn: the modelled one, or the game's own hat where the props are
// missing or the hair refused the modelled one. The cap and the hair are not touched. First the rules, then the one place
// that dresses an officer and the pool's shelf reading the option, then the option and its texts.
internal static partial class Program
{
    private static void CopCaps()
    {
        CopCapRules();
        CopCapDressing();
        CopCapShelf();
        CopCapOption();
        CopCapWords();
    }

    // Whether an officer wears these, as the three heads the rules know them: the one the kit's cap and the game's hat slot make.
    private static bool CopCapWears(OfficerHead head, bool modelledFitted, bool hatOn)
    {
        return head == OfficerHead.Bare ? !modelledFitted && !hatOn
            : head == OfficerHead.ModelledCap ? modelledFitted && !hatOn
            : !modelledFitted && hatOn;
    }

    private static void CopCapRules()
    {
        Check(OfficerCapRules.First(false, true) == OfficerHead.Bare && OfficerCapRules.First(false, false) == OfficerHead.Bare,
            "cop caps: with the option off an officer is dressed bare headed, whether or not the modelled cap's props are there");
        Check(OfficerCapRules.First(true, true) == OfficerHead.ModelledCap, "cop caps: with the option on and the props there, the modelled cap");
        Check(OfficerCapRules.First(true, false) == OfficerHead.GameHat, "cop caps: with the option on and no props, the game's own hat");
        Check(OfficerCapRules.First(true, true) == OfficerCapRules.First(true, true) && OfficerCapRules.First(false, true) == OfficerCapRules.First(false, true),
            "cop caps: the same inputs give the same head");
        Check(OfficerCapRules.AfterRefusal(OfficerHead.ModelledCap) == OfficerHead.GameHat, "cop caps: a modelled cap the kit refused is the game's hat");
        Check(OfficerCapRules.AfterRefusal(OfficerHead.Bare) == OfficerHead.Bare && OfficerCapRules.AfterRefusal(OfficerHead.GameHat) == OfficerHead.GameHat,
            "cop caps: a refusal never puts a hat on a bare head, and leaves the game's hat as it is");
        Check(!OfficerCapRules.Redress(true, true) && !OfficerCapRules.Redress(false, false) && OfficerCapRules.Redress(true, false) && OfficerCapRules.Redress(false, true),
            "cop caps: an officer is dressed again only when the option is not what they were dressed for");

        // The self-check's verdict, over every way the option, the props, the kit and the hat slot can stand: an officer matches
        // when what they wear is the head the rules dressed or the one a refusal leaves, and in no other case.
        int wrong = 0;
        int passes = 0;
        for (int mask = 0; mask < 16; mask++)
        {
            bool on = (mask & 1) != 0, props = (mask & 2) != 0, fitted = (mask & 4) != 0, hat = (mask & 8) != 0;
            OfficerHead first = OfficerCapRules.First(on, props);
            bool expected = CopCapWears(first, fitted, hat) || CopCapWears(OfficerCapRules.AfterRefusal(first), fitted, hat);
            bool got = OfficerCapRules.Matches(on, props, fitted, hat);
            wrong += got == expected ? 0 : 1;
            passes += got ? 1 : 0;
        }
        Check(wrong == 0, "cop caps: the self-check passes exactly the heads the rules allow, in all 16 ways the pieces can stand (" + wrong + " wrong)");
        Check(passes == 5, "cop caps: five of the sixteen ways pass: a bare head with the option off, props or none (2), the cap or the hat with it on and the props there (2), "
              + "the hat with it on and no props (1), not " + passes);
        Check(!OfficerCapRules.Matches(false, true, true, false) && !OfficerCapRules.Matches(false, true, false, true) && !OfficerCapRules.Matches(false, false, false, true),
            "cop caps: with the option off, a modelled cap or a game's hat on an officer fails the check, props or none");
        Check(OfficerCapRules.Matches(false, true, false, false) && OfficerCapRules.Matches(false, false, false, false),
            "cop caps: with the option off, a bare head is what passes");
        Check(!OfficerCapRules.Matches(true, true, false, false) && !OfficerCapRules.Matches(true, true, true, true) && !OfficerCapRules.Matches(true, false, false, false),
            "cop caps: with the option on, an officer with neither cap nor hat, or with both, fails the check");

        var watch = System.Diagnostics.Stopwatch.StartNew();
        int due = 0;
        for (int i = 0; i < 200000; i++)
        {
            due += OfficerCapRules.Redress((i & 1) != 0, (i & 2) != 0) ? 1 : 0;
            due += OfficerCapRules.Matches((i & 1) != 0, (i & 2) != 0, (i & 4) != 0, (i & 8) != 0) ? 1 : 0;
            due += OfficerCapRules.First((i & 1) != 0, (i & 2) != 0) == OfficerHead.Bare ? 1 : 0;
        }
        watch.Stop();
        Check(due > 0 && watch.Elapsed.TotalMilliseconds < 50.0, "cop caps: 600,000 looks at an officer's head take " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms (under 50)");
    }

    private static string CopCapScripts()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));   // (Path alone is this class's own method)
    }

    // The one place that dresses an officer, and what it reads.
    private static void CopCapDressing()
    {
        string dress = ModSource(@"People\ActorPool.Dress.cs");
        string kit = ModSource(@"People\ServiceKit.cs");
        string dressing = MemberText(dress, "private static void Dress(StreetActor actor, ActorLook look)");
        int caps = dressing.IndexOf("actor.CapsOn = Settings.PoliceCaps;", StringComparison.Ordinal);
        Check(caps >= 0 && caps < dressing.IndexOf("Looks.Count(", StringComparison.Ordinal),
            "cop caps: an officer is dressed for the option's value, written down before anything is built, so that a look that fails is not tried again for it");
        Check(dressing.Contains("OfficerCapRules.First(actor.CapsOn, modelled)") && dressing.Contains("bool modelled = ServiceKit.CapToBeHad;")
              && dressing.Contains("bool cap = head == OfficerHead.ModelledCap;") && !dressing.Contains("look == ActorLook.Officer && ServiceKit.CapToBeHad"),
            "cop caps: the kit is asked for the modelled cap only when the rules say the modelled cap, never by the props alone");
        Check(Occurs(dressing, "Uniforms.Wear(") == 2 && dressing.Contains("Uniforms.Wear(appearance, look, actor.Dyes, head != OfficerHead.GameHat, ref actor.Shirt);"),
            "cop caps: the game's hat is put on at the first wearing only when the rules say the game's hat");
        int refused = dressing.IndexOf("actor.Kit.CapRefused", StringComparison.Ordinal);
        const string again = "Uniforms.Wear(appearance, look, actor.Dyes, OfficerCapRules.AfterRefusal(head) != OfficerHead.GameHat, ref actor.Shirt);";
        int second = dressing.IndexOf(again, StringComparison.Ordinal);
        Check(refused > 0 && second > refused && Occurs(dressing, again) == 1,
            "cop caps: the game's hat goes on after the first wearing only where the kit refused the modelled cap, by the rules' answer to a refusal");
        Check(kit.Contains("CapRefused = cap && (head == null || !FitCap(animator, head, layer));") && Occurs(kit, "FitCap(animator, head, layer)") == 1
              && kit.Contains("CapFitted = cap && !CapRefused;"),
            "cop caps: the kit fits the modelled cap only where it is asked for it, so an officer dressed bare gets none (the cap's own fit is as it was)");
        string headCheck = MemberText(dress, "private static void HeadCheck(");
        Check(dressing.Contains("if (look == ActorLook.Officer && Meta.Dev.Verbose)") && dressing.Contains("HeadCheck(actor, appearance, modelled);")
              && headCheck.Contains("OfficerCapRules.Matches(") && headCheck.Contains("Uniforms.HatOn(appearance, out string slot)") && headCheck.Contains("actor.Kit.CapFitted")
              && headCheck.Contains("\"People check (an officer's head)") && headCheck.Contains("\"PASS\" : \"FAIL\""),
            "cop caps: each officer dressed writes one self-check line with diagnostics on: the option against the cap and the hat slot, PASS or FAIL");

        // Fixed once for every path: nobody else dresses an officer or hangs the modelled cap, so no path can leave it on.
        string scripts = CopCapScripts();
        var elsewhere = new List<string>();
        foreach (string path in Directory.Exists(scripts) ? Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories) : new string[0])
        {
            string relative = path.Substring(scripts.Length + 1).Replace('\\', '/');
            string text = File.ReadAllText(path);
            if (relative != "People/ActorPool.Dress.cs" && relative != "People/Uniforms.cs" && (text.Contains("Uniforms.Wear(") || text.Contains("new ServiceKit(")))
            {
                elsewhere.Add(relative + " dresses a uniform or fits a kit");
            }
            if (relative != "People/ServiceKit.cs" && (text.Contains("\"police_cap\"") || text.Contains("ServiceKit.CapId")))
            {
                elsewhere.Add(relative + " names the modelled police cap");
            }
        }
        Check(Directory.Exists(scripts) && elsewhere.Count == 0, "cop caps: officers are dressed in one place and the modelled cap is named in one file, so the option rules every path:"
              + (elsewhere.Count > 0 ? " " + string.Join("; ", elsewhere) : string.Empty));
    }

    // The pool's shelf: an idle officer dressed for the other value is made again, and nobody is handed out in the wrong cap.
    private static void CopCapShelf()
    {
        string pool = ModSource(@"People\ActorPool.cs");
        string actor = ModSource(@"People\StreetActor.cs");
        Check(actor.Contains("internal bool CapsOn = true;"), "cop caps: each person of the pool remembers the option's value they were dressed for, on to begin with");
        string outOfDate = MemberText(pool, "private static bool CapsOutOfDate(StreetActor actor)");
        Check(outOfDate.Contains("actor.IsValid") && outOfDate.Contains("actor.Look == ActorLook.Officer") && outOfDate.Contains("OfficerCapRules.Redress(actor.CapsOn, Settings.PoliceCaps)"),
            "cop caps: an officer, and only an officer, is out of date when the value they were dressed for is not the option's now (a destroyed one never is)");
        Check(MemberText(pool, "private static bool Due(StreetActor actor)").Contains("actor.Stale || CapsOutOfDate(actor)")
              && MemberText(pool, "private bool AnyStale()").Contains("!_actors[i].InUse && Due(_actors[i])"),
            "cop caps: the shelf counts an out of date officer as due a new look, so the tending that makes civilians' looks again makes theirs");
        string tend = MemberText(pool, "private void TendOne()");
        Check(tend.Contains("if (!actor.InUse && CapsOutOfDate(actor))") && tend.Contains("Redress(actor);   // the officer's look is made again for the option's value now"),
            "cop caps: one look at a time, when Looks says one may be built, an idle out of date officer is dressed again");
        string take = MemberText(pool, "private StreetActor Take(ActorLook look, Vector3 position, bool mayReclaim)");
        int handed = take.IndexOf("CapsUpToDate(yielding);", StringComparison.Ordinal);
        Check(take.Contains("(Due(ready) && !Due(actor)) || (Due(ready) == Due(actor)") && take.Contains("CapsUpToDate(ready);")
              && handed > 0 && handed < take.IndexOf("return yielding;", StringComparison.Ordinal),
            "cop caps: a scene is handed an up to date officer when there is one, and anyone out of date is dressed again before they are handed over");
        string up = MemberText(pool, "private static void CapsUpToDate(StreetActor actor)");
        Check(up.Contains("if (CapsOutOfDate(actor))") && up.Contains("Dress(actor, actor.Look);"),
            "cop caps: dressing again before a hand over happens only for an out of date officer");
        Check(MemberText(pool, "public void CapsChanged()").Contains("Tend();"), "cop caps: a change of the option wakes the shelf's tending, which starts only if one is due");
        Check(pool.Contains("_onSettings = Guard.Wrap(CapsChanged);") && pool.Contains("Settings.Changed += _onSettings;") && pool.Contains("Settings.Changed -= _onSettings;"),
            "cop caps: a preset, an import or a reset, which read the options again without the page's switch, wake it too (one handler, made once, taken off with the pool)");
    }

    private static void CopCapOption()
    {
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string list = ModSource(@"Meta\FeatureList.cs");
        Check(settings.Contains("PoliceCapsId = \"police_caps\"") && settings.Contains("public static bool PoliceCaps = true;")
              && settings.Contains("PoliceCaps = Bool(modId, PoliceCapsId, PoliceCaps);"),
            "cop caps: the option is saved as police_caps, on by default, and read at every load");
        Check(mod.Contains("AddToggle(Settings.PoliceCapsId, \"reallife_police_caps\", Settings.PoliceCaps,") && mod.Contains("Settings.PoliceCaps = v;")
              && mod.Contains("ActorPool.Instance?.CapsChanged();"),
            "cop caps: the options page has the switch, and it tells the pool at once");
        Check(list.Contains(".Owns(Settings.PeopleId, Settings.OfficersOnPatrolId, Settings.PoliceCapsId)") && !list.Contains("Preset(Settings.PoliceCapsId"),
            "cop caps: the people's page owns the option, and no preset touches it (a look, not a difficulty)");

        TreeSection section = OptionsTree.SectionOf("police_caps");
        TreeRow row = OptionsTree.Row("police_caps");
        Check(section != null && section.Id == "police" && row != null && !row.IsButton, "cop caps: the switch stands in the Police section of the options page");
        Check(row != null && row.Free && row.Needs.Length == 0 && section != null && section.Rows.Count <= OptionsTree.MostRows,
            "cop caps: it works whatever the Police main switch says, as officers stand at stations and scenes without it, and the section is no more than " + OptionsTree.MostRows + " rows");
        Check(row != null && row.TipKey == "reallife_opt_tip_police_caps" && row.Words != null && row.Words.Contains("cap") && row.Words.Contains("hat"),
            "cop caps: it has its sentence under the pointer and the search finds it by cap and by hat");
    }

    private static void CopCapWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("cop caps", langs, codes))
        {
            return;
        }
        string lacking = string.Empty;
        foreach (string key in new[] { "reallife_police_caps", "reallife_opt_tip_police_caps" })
        {
            string l = LanguagesLacking(langs, codes, key);
            lacking += l.Length > 0 ? " " + key + " (" + l + ")" : string.Empty;
        }
        Check(lacking.Length == 0, "cop caps: every text is in every language:" + lacking);
        Check(langs[0].TryGetValue("reallife_police_caps", out string label) && label == "Police officers wear caps"
              && langs[0].TryGetValue("reallife_opt_tip_police_caps", out string tip) && tip.EndsWith(".", StringComparison.Ordinal)
              && tip.Contains("cap") && tip.Contains("Off,"),
            "cop caps: the option is named as a player would look for it and its sentence says what off does");
        Check(WhatsNewHas(langs[0], "Police officers can now go without their caps"), "cop caps: What's new has the line in English (any of its rows)");
    }
}
