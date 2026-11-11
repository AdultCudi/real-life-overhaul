using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.GameUi;

// A player late in a game (8 Nov 2026): so many of the mod's notifications pop up that they clutter the screen and each one
// is a hitch, and asked to switch kinds of them off, never the ones that matter. Every one of them went through the game's
// one toast maker (NotificationsUI.ShowNotification: a copy of its template, the text laid out, a sound, two fades and a
// coroutine), and nothing let a kind be kept from it. Now each kind but the police's and health's has a switch (Options,
// "Notifications that pop up"); one switched off goes straight into the notification list as the game would write it, and
// is never made (Core/GameUi/NoticeMuteRules, Core/GameUi/NoticeMute). Warnings, errors, what is owed, the end of a life and
// the tests' answers always pop up. First the rules on every mask of kinds; then every notification the sources raise by
// its own key; the cost; the sources (the gate, the list, the morning line, the options page); the texts. Run with --notice-mute.
internal static partial class Program
{
    private static void NoticeMuting()
    {
        NoticeMuteRuleChecks();
        NoticeMuteKeyChecks();
        NoticeMutePace();
        NoticeMuteSources();
        NoticeMuteWords();
    }

    // One key of each kind with a switch, in NoticeMuteRules.Switchable's order (as CatalogueCheck's NoticeCheck has them).
    private static readonly string[] NoticeMuteSamples =
    {
        "reallife_tow_coming", "reallife_econ_note_lent", "reallife_estate_note_pipe", "reallife_career_signed", "reallife_co_note_offer",
        "reallife_tech_note_breach", "reallife_ledger_note_delivered", "reallife_social_met", "reallife_planner_reminder"
    };

    // Kinds that have no switch: the police's (a scheme's inquiry among them) and health's; and keys that are not the mod's.
    private static readonly string[] NoticeMuteNever =
    {
        "reallife_law_wanted", "reallife_justice_toast_warrant", "reallife_crime_stop_fleeing", "reallife_scheme_note_caution", "reallife_scheme_note_letter",
        "reallife_biz_racket_start", "reallife_biz_visit_police", "reallife_health_caught", "reallife_pd_ill_1", "reallife_event_ambulance",
        "reallife_family_checkup_due", "reallife_travel_burnout_doctor", "notificationui_notification_insufficient_money",
        "buildingmanager_notification_business_closed", "reallife_", "Reallife_tow_coming", null, string.Empty
    };

    private const int NoticeMuteMasks = 1 << NoticeKinds.Count;   // every set of kinds, those without a switch among them

    private static int NoticeMuteAll()
    {
        int mask = 0;
        foreach (NoticeKind kind in NoticeMuteRules.Switchable)
        {
            mask |= NoticeMuteRules.Bit(kind);
        }
        return mask;
    }

    // ---------------------------------------------------------------- the rules

    private static void NoticeMuteRuleChecks()
    {
        int all = NoticeMuteAll();
        NoticeKind[] kinds = NoticeMuteRules.Switchable;
        var distinct = new HashSet<NoticeKind>(kinds);
        Check(kinds.Length == 9 && distinct.Count == 9 && !distinct.Contains(NoticeKind.None) && !distinct.Contains(NoticeKind.Police) && !distinct.Contains(NoticeKind.Health),
            "notice mute: nine kinds have a switch, each once; the police, health and what is not the mod's have none");
        bool switches = true;
        foreach (NoticeKind kind in Enum.GetValues(typeof(NoticeKind)))
        {
            switches &= NoticeMuteRules.HasSwitch(kind) == distinct.Contains(kind);
        }
        Check(switches, "notice mute: a kind has a switch exactly when the options page lists one for it (a new kind needs its own switch, or none)");
        Check(NoticeMuteRules.With(0, NoticeKind.Road, false) == NoticeMuteRules.Bit(NoticeKind.Road) && NoticeMuteRules.With(NoticeMuteRules.Bit(NoticeKind.Road), NoticeKind.Road, true) == 0
              && NoticeMuteRules.With(NoticeMuteRules.Bit(NoticeKind.Road), NoticeKind.Road, false) == NoticeMuteRules.Bit(NoticeKind.Road)
              && NoticeMuteRules.With(0, NoticeKind.Police, false) == 0 && NoticeMuteRules.With(0, NoticeKind.Health, false) == 0 && NoticeMuteRules.With(0, NoticeKind.None, false) == 0,
            "notice mute: a switch off sets its kind's bit and on clears it; the police's and health's bits are never set");

        bool own = true, others = true, urgent = true, samples = true;
        for (int i = 0; i < kinds.Length; i++)
        {
            string key = NoticeMuteSamples[i];
            own &= NoticeKinds.Of(key) == kinds[i] && NoticeMuteRules.Muted(key, false, NoticeMuteRules.Bit(kinds[i])) && NoticeMuteRules.Muted(key, false, all);
            others &= !NoticeMuteRules.Muted(key, false, all & ~NoticeMuteRules.Bit(kinds[i])) && !NoticeMuteRules.Muted(key, false, 0);
            urgent &= !NoticeMuteRules.Muted(key, true, all);
            string sample = "reallife_notice_test_" + kinds[i].ToString().ToLowerInvariant();
            samples &= NoticeMuteRules.Muted(sample, false, NoticeMuteRules.Bit(kinds[i])) && !NoticeMuteRules.Muted(sample, false, all & ~NoticeMuteRules.Bit(kinds[i]));
        }
        Check(own, "notice mute: a notification of each kind is kept back when its own kind is off");
        Check(others, "notice mute: a kind's notification pops up while its own kind is on, whatever the others say");
        Check(urgent, "notice mute: a warning or an error always pops up, every kind off");
        Check(samples && !NoticeMuteRules.Muted("reallife_notice_test_police", false, all) && !NoticeMuteRules.Muted("reallife_notice_test_health", false, all),
            "notice mute: the options page's samples go as their kind goes, the police's and health's always pop up");

        bool never = true;
        string caught = "none";
        for (int mask = 0; mask < NoticeMuteMasks; mask++)
        {
            foreach (string key in NoticeMuteNever)
            {
                if (NoticeMuteRules.Muted(key, false, mask))
                {
                    never = false;
                    caught = (key ?? "null") + " under mask " + mask;
                }
            }
        }
        Check(never, "notice mute: the police's, health's and anything not the mod's pop up under every one of " + NoticeMuteMasks + " sets of kinds off (" + caught + ")");

        bool always = true;
        foreach (string prefix in NoticeMuteRules.AlwaysPrefixes())
        {
            string key = NoticeKinds.ModPrefix + prefix + (prefix.EndsWith("_", StringComparison.Ordinal) ? "x" : string.Empty);
            always &= NoticeMuteRules.Always(key) && !NoticeMuteRules.Muted(key, false, all) && NoticeMuteRules.HasSwitch(NoticeKinds.Of(key));
        }
        Check(always && NoticeMuteRules.AlwaysPrefixes().Length >= 8,
            "notice mute: what is owed, the end of a life and the morning line's news pop up with every kind off (each of a kind with a switch, so each is needed)");
        Check(!NoticeMuteRules.Always("reallife_econ_note_lent") && !NoticeMuteRules.Always("reallife_util") && !NoticeMuteRules.Always("util_toast") && !NoticeMuteRules.Always(null)
              && !NoticeMuteRules.Always("xreallife_util_toast") && NoticeMuteRules.Always("reallife_util_toast"),
            "notice mute: only the mod's own keys that begin with what always pops up are kept from the switches");
        Check(!NoticeMuteRules.Muted("reallife_notice_mute_test_done", false, all) && !NoticeMuteRules.Muted("reallife_career_test_promoted", false, all)
              && !NoticeMuteRules.Muted("reallife_lic_test_granted", false, all) && NoticeMuteRules.TestAnswer("reallife_lic_test_granted") && !NoticeMuteRules.TestAnswer("reallife_notice_test_road"),
            "notice mute: a test's answer and the driving test's words always pop up; the samples are no answers");
        Check(!NoticeMuteRules.Muted("reallife_home_morning_news", false, all) && !NoticeMuteRules.Muted("reallife_home_morning_more", false, all)
              && NoticeMuteRules.Muted("reallife_home_morning", false, NoticeMuteRules.Bit(NoticeKind.Other)),
            "notice mute: the morning line with the night's news pops up (it holds only kinds that pop up); the greeting alone goes as the rest of the mod's");
    }

    // ---------------------------------------------------------------- every notification the sources raise by its own key

    private static void NoticeMuteKeyChecks()
    {
        Dictionary<string, string> en = LocaleFile("en");
        foreach (string prefix in NoticeMuteRules.AlwaysPrefixes())
        {
            int matched = 0;
            foreach (string key in en.Keys)
            {
                matched += key.StartsWith(NoticeKinds.ModPrefix + prefix, StringComparison.Ordinal) ? 1 : 0;
            }
            Check(matched > 0, "notice mute: what always pops up names keys the mod has (" + prefix + " matches " + matched + ")");
        }

        List<(string Type, string Key)> pairs = NoticeMutePairs();
        Check(pairs.Count >= 1000, "notice mute: the notifications raised in the sources by a key of the mod's were read (" + pairs.Count + " type and key pairs)");
        int all = NoticeMuteAll();
        int noSwitch = 0, wrong = 0, calm = 0, calmKept = 0, missing = 0;
        string wrongOne = "none", noSwitchOne = "none", missingOne = "none";
        foreach ((string type, string key) in pairs)
        {
            bool urgent = type == "Warning" || type == "Error";
            NoticeKind kind = NoticeKinds.Of(key);
            bool kept = NoticeMuteRules.Muted(key, urgent, all);
            if (!NoticeMuteRules.HasSwitch(kind) && kept)
            {
                noSwitch++;
                noSwitchOne = key;
            }
            foreach (NoticeKind other in NoticeMuteRules.Switchable)
            {
                if (other != kind && NoticeMuteRules.Muted(key, urgent, NoticeMuteRules.Bit(other)))
                {
                    wrong++;
                    wrongOne = key + " by " + other;
                }
            }
            if (!urgent && NoticeMuteRules.HasSwitch(kind))
            {
                calm++;
                calmKept += kept ? 1 : 0;
            }
            if (urgent && kept)
            {
                missing++;
                missingOne = key;
            }
        }
        Check(noSwitch == 0, "notice mute: no notification of the police, health or the game's own is kept back by any switch (" + noSwitchOne + ")");
        Check(wrong == 0, "notice mute: no notification is kept back by another kind's switch (" + wrong + ": " + wrongOne + ")");
        Check(missing == 0, "notice mute: no warning or error in the sources is kept back (" + missingOne + ")");
        Check(calm > 500 && calmKept * 2 > calm, "notice mute: most of the calm notifications of the kinds with a switch can be kept back ("
              + calmKept + " of " + calm + "): what always pops up is the exception");
    }

    private static List<(string Type, string Key)> _noticeMutePairs;   // read once for these checks

    // Each call in the sources whose arguments are a notification's type and then a literal key of the mod's.
    private static List<(string Type, string Key)> NoticeMutePairs()
    {
        if (_noticeMutePairs != null)
        {
            return _noticeMutePairs;
        }
        var pairs = new List<(string Type, string Key)>(2048);
        _noticeMutePairs = pairs;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var call = new Regex("NotificationType\\.(Info|Success|Warning|Error)\\s*,\\s*\"(reallife_[a-z0-9_]+)\"");
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!System.IO.Directory.Exists(scripts))
        {
            return pairs;
        }
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            foreach (Match m in call.Matches(System.IO.File.ReadAllText(file)))
            {
                if (seen.Add(m.Groups[1].Value + " " + m.Groups[2].Value))
                {
                    pairs.Add((m.Groups[1].Value, m.Groups[2].Value));
                }
            }
        }
        return pairs;
    }

    // ---------------------------------------------------------------- the cost

    private static void NoticeMutePace()
    {
        List<(string Type, string Key)> pairs = NoticeMutePairs();
        var keys = new string[pairs.Count];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = pairs[i].Key;
        }
        int all = NoticeMuteAll();
        NoticeMuteAsk(keys, all, 1, out _);   // warmed: the tables are made once
        long before = GC.GetAllocatedBytesForCurrentThread();
        int calmHits = NoticeMuteAsk(keys, 0, 1000000 / Math.Max(1, keys.Length), out double calmMs);
        long calmBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        int rounds = 200000 / Math.Max(1, keys.Length);
        int hits = NoticeMuteAsk(keys, all, rounds, out double ms);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double each = ms * 1000.0 / Math.Max(1.0, (double)rounds * keys.Length);
        Check(keys.Length > 0 && calmHits == 0 && calmBytes == 0 && calmMs < 100.0,
            "notice mute: with every kind on a notification costs one comparison and allocates nothing (" + calmMs.ToString("F1") + " ms and " + calmBytes + " bytes for a million)");
        Check(keys.Length > 0 && hits > 0 && bytes == 0 && each < 10.0,
            "notice mute: with every kind off a notification's kind and exceptions take " + each.ToString("F2") + " microseconds and " + bytes + " bytes (under 10, and none)");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization | System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int NoticeMuteAsk(string[] keys, int mask, int rounds, out double ms)
    {
        int hits = 0;
        long began = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                hits += NoticeMuteRules.Muted(keys[i], false, mask) ? 1 : 0;
            }
        }
        ms = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        return hits;
    }

    // ---------------------------------------------------------------- the sources

    private static void NoticeMuteSources()
    {
        string mute = ModSource(@"Core\GameUi\NoticeMute.cs");
        string pass = MemberText(mute, "private static void Pass(NotificationType type, string key, Dictionary<string, string> data, float seconds, string same, "
                                       + "Action onClick, bool sound, bool track)");
        string keep = MemberText(mute, "private static void Keep(NotificationsUI maker, NotificationType type, string key, Dictionary<string, string> data, float seconds, string same, bool track)");
        string stand = MemberText(mute, "private static void Stand()");
        string aside = MemberText(mute, "private static void StepAside()");
        Check(CodeAt(stand, "GameContract.NoticeMaker.Method") > 0 && CodeAt(stand, "Delegate.CreateDelegate(") > 0 && CodeAt(stand, "Array.IndexOf(list, own)") > 0
              && CodeAt(stand, "list[at] = Gate;") > CodeAt(stand, "_game = (Action<") && CodeAt(stand, "SetEvent(list);") > CodeAt(stand, "list[at] = Gate;"),
            "notice mute, as code: the gate takes the game's maker's own place on its event, found through the game contract, and keeps the maker's entry");
        Check(CodeAt(aside, "list[at] = game;") > 0 && CodeAt(aside, "Delegate.Remove(shown, Gate)") > 0 && CodeAt(aside, "maker != null") > 0 && CodeAt(aside, "_keptKey = null;") > 0,
            "notice mute, as code: stepping aside gives the maker its own place back, or takes the gate off alone when the maker went with its city");
        Check(CodeAt(pass, "catch (Exception fault)") > 0 && CodeAt(pass, "Guard.Fail(FrameGuard, fault);") > 0 && !pass.Contains("Guard.Wrap")
              && CodeAt(pass, "game(type, key, data, seconds, same, onClick, sound, track);") > CodeAt(pass, "catch (Exception fault)")
              && CodeAt(pass, "NoticeMuteRules.Muted(key, Urgent(type), _mask)") > 0 && CodeAt(pass, "StepAside();") > 0,
            "notice mute, as code: the gate guards itself and calls the game's maker outside its guard, so a fault or the guard standing it down never loses a notification");
        int cleared = CodeAt(pass, "_keptKey = null;");
        Check(cleared > 0 && cleared < CodeAt(pass, "try") && CodeAt(pass, "_keptKey = key;") > CodeAt(pass, "Keep(maker, type, key, data, seconds, same, track);")
              && CodeAt(pass, "_keptData = data;") > cleared && CodeAt(mute, "return key != null && ReferenceEquals(key, _keptKey) && ReferenceEquals(data, _keptData);") > 0,
            "notice mute, as code: the gate says of each notification whether it kept it back, so the listeners after the maker never take one made before it stood for one kept");
        Check(CodeAt(keep, "maker.container.Find(same)") > 0 && CodeAt(keep, "SameUp(same, now)") > 0
              && CodeAt(keep, "save.notifications.Enqueue(new Notification { type = type, key = key, notificationData = data, date = TimeHelper.Now() });") > CodeAt(keep, "maker.container.Find(same)")
              && CodeAt(keep, "if (track && save != null && save.notifications != null)") > 0 && !keep.Contains("Instantiate") && !keep.Contains("PlayNotificationSound"),
            "notice mute, as code: a kept notification goes into the save's list as the game writes it (its sender's choice, its duplicate dropped), with nothing made and no sound");
        Check(Occurs(mute, "NoticeMuteRules.With(") == 9 && CodeAt(mute, "Settings.Changed += Reread;") > 0 && CodeAt(mute, "Harmony") < 0 && CodeAt(mute, "void Update(") < 0
              && CodeAt(mute, "LateUpdate") < 0 && CodeAt(mute, "SharedCity") < 0 && CodeAt(mute, "GoingPublicLink") < 0 && mute.Length > 0,
            "notice mute, as code: the nine switches make the mask, read again with the options; no patch, nothing a frame, nothing sent");
        Check(CodeAt(mute, "TestRefusals.Tell(Logger, \"Notice mute\", TestRefusal.NotLoaded);") > 0 && CodeAt(mute, "NoticeLook.Samples") > 0
              && CodeAt(mute, "kept == expected ? \": PASS.\"") > 0 && CodeAt(mute, "Notifications.Show(NotificationType.Info, DoneKey, data, DoneSeconds);") > 0,
            "notice mute, as code: the test sends each kind's sample and sets what was kept back against what the switches say");

        string contract = ModSource(@"Core\GameContract.Notices.cs");
        Check(contract.Contains("ContractBook.Part(\"notice_mute\", \"phone\"") && contract.Contains("\"UI.Notification.NotificationsUI\", \"ShowNotification\", \"Void\"")
              && contract.Contains("\"NotificationType\", \"String\", \"Dictionary<String,String>\", \"Single\", \"String\", \"Action\", \"Boolean\", \"Boolean\""),
            "notice mute, as code: the game's private toast maker is a line of the game contract, with its eight parameters");

        string look = ModSource(@"Core\GameUi\NoticeLook.cs");
        string shown = MemberText(look, "private void Shown(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, "
                                        + "Action onClick, bool sound, bool track)");
        Check(CodeAt(look, "NoticeMute.Attached(_toasts);") > 0 && CodeAt(MemberText(look, "private void OnDestroy()"), "NoticeMute.Leave();") > 0
              && CodeAt(shown, "NoticeMute.Shown();") > 0 && CodeAt(shown, "if (NoticeMute.KeptBack(key, data))") > 0
              && CodeAt(shown, "_lastToast = made;") > CodeAt(shown, "if (NoticeMute.KeptBack(key, data))") && CodeAt(shown, "NoticeMute.Muting(") < 0,
            "notice mute, as code: the gate is placed with the city's maker and taken off with it; the look never draws on an older toast for one kept back");
        string shell = ModSource(@"Ui\Shell\ShellNotices.cs");
        string card = MemberText(shell, "private static void Shown(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, "
                                        + "Action click, bool sound, bool track)");
        Check(CodeAt(card, "if (NoticeMute.KeptBack(key, data))") > 0 && CodeAt(card, "stack.GetChild(stack.childCount - 1)") > CodeAt(card, "if (NoticeMute.KeptBack(key, data))"),
            "notice mute, as code: the kit's cards make no card of an older toast for one kept back");
        string hold = ModSource(@"Core\MorningNews.cs");
        string held = MemberText(hold, "public static void Show(NotificationType type, string key, Dictionary<string, string> data, float seconds, string same = null, bool track = true)");
        Check(CodeAt(held, "if (GameUi.NoticeMute.Muting(type, key))") > 0 && CodeAt(held, "Held.Add(notice);") > CodeAt(held, "if (GameUi.NoticeMute.Muting(type, key))"),
            "notice mute, as code: a kind switched off is never held for the morning line, which pops up");

        string settings = ModSource(@"Core\Settings.cs");
        string page = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string features = ModSource(@"Meta\FeatureList.cs");
        bool declared = true, placed = true, ordered = true;
        int last = -1;
        foreach (NoticeKind kind in NoticeMuteRules.Switchable)
        {
            string name = kind.ToString();
            string id = "notice_show_" + name.ToLowerInvariant();
            declared &= CodeAt(settings, "public const string NoticeShow" + name + "Id = \"" + id + "\";") > 0 && CodeAt(settings, "public static bool NoticeShow" + name + " = true;") > 0
                        && CodeAt(settings, "NoticeShow" + name + " = Bool(modId, NoticeShow" + name + "Id, NoticeShow" + name + ");") > 0
                        && CodeAt(mute, "Settings.NoticeShow" + name + ")") > 0;
            placed &= CodeAt(page, ".AddToggle(Settings.NoticeShow" + name + "Id, \"reallife_" + id + "\", Settings.NoticeShow" + name + ",") > 0
                      && CodeAt(page, "Settings.NoticeShow" + name + " = v; GameUi.NoticeMute.Place(); }") > 0 && CodeAt(features, "Settings.NoticeShow" + name + "Id") > 0;
            int at = CodeAt(tree, "\"" + id + "\"");
            ordered &= at > last;
            last = at;
        }
        Check(declared, "notice mute, as code: each kind's switch is an option, on by default, read at load and read by the gate");
        Check(placed && CodeAt(page, ".AddButton(\"reallife_test_notice_mute\", GameUi.NoticeMute.Test)") > 0 && CodeAt(page, "GameUi.NoticeMute.Logger = context.Logger") > 0
              && CodeAt(features, "\"reallife_test_notice_mute\"") > 0,
            "notice mute, as code: each switch is on the options page and places the gate at once; the test is the phone's");
        Check(ordered && CodeAt(tree, "S(\"notices\").Guide(\"phone\")") > 0, "notice mute, as code: the options page lists the switches in their own section, in the rules' order");
    }

    // ---------------------------------------------------------------- the texts

    private static void NoticeMuteWords()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string Text(string key) => en.TryGetValue(key, out string v) ? v : null;
        int Words(string text) => text == null ? 0 : text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
        bool Plain(string text) => text != null && text.IndexOf('!') < 0 && text.IndexOf('-') < 0 && text.IndexOf((char)0x2013) < 0 && text.IndexOf((char)0x2014) < 0;
        bool Searchable(string text) => text != null && !Regex.IsMatch(text.ToLowerInvariant(), "(^|[^a-z])(rent|fps)");

        string section = Text("reallife_opt_sec_notices");
        Check(section != null && Words(section) <= 6 && section.Length <= 40 && Plain(section), "notice mute, texts: the section's title is short and plain (" + section + ")");
        bool labels = true, tips = true, plain = true, search = true, list = true;
        foreach (NoticeKind kind in NoticeMuteRules.Switchable)
        {
            string id = "notice_show_" + kind.ToString().ToLowerInvariant();
            string label = Text("reallife_" + id);
            string tip = Text("reallife_opt_tip_" + id);
            labels &= label != null && Words(label) <= 9 && label.Length <= 60 && !label.EndsWith(".", StringComparison.Ordinal);
            tips &= tip != null && Words(tip) <= 32 && tip.Length <= 190 && tip.EndsWith(".", StringComparison.Ordinal);
            plain &= Plain(label) && Plain(tip);
            search &= Searchable(label) && Searchable(tip);
            list &= tip != null && tip.Contains("notification list") && tip.Contains("always pop up");
        }
        Check(labels, "notice mute, texts: each switch's label is at most nine words and 60 characters, with no full stop");
        Check(tips, "notice mute, texts: each switch's sentence is at most 32 words and 190 characters, ending in a full stop");
        Check(plain && search, "notice mute, texts: no exclamation mark or dash, and no word the search keeps for other rows (rent, fps)");
        Check(list, "notice mute, texts: each sentence says where a kind switched off goes and what always pops up");
        Check((Text("reallife_opt_tip_notice_show_money") ?? string.Empty).Contains("bills") && (Text("reallife_opt_tip_notice_show_other") ?? string.Empty).Contains("death"),
            "notice mute, texts: the money switch says bills always pop up, the rest of the mod's says a death does");
        string test = Text("reallife_test_notice_mute");
        Check(test != null && Words(test) <= 13 && test.Length <= 80 && char.IsUpper(test[0]) && !test.StartsWith("Test", StringComparison.OrdinalIgnoreCase) && Plain(test),
            "notice mute, texts: the test's button says what it does (" + test + ")");
        string done = Text("reallife_notice_mute_test_done");
        Check(Fits(en, "reallife_notice_mute_test_done") && done.Contains("{shown}") && done.Contains("{all}") && done.Contains("{kept}") && Plain(done),
            "notice mute, texts: the test's answer fits a toast and names what popped up and what went into the list");
        Check(Text("reallife_contract_part_notice_mute") != null, "notice mute, texts: the game contract's part has its name");
        Check(WhatsNewHas(en, "goes straight into the notification list without popping up"), "notice mute, texts: What's new says a kind switched off goes into the list");
    }
}
