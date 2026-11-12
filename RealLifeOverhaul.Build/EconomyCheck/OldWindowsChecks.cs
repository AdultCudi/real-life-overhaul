using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// Windows still drawn the old way (--old-windows, and the audit part in --sweep-ui): a pitch, an interview or any other scene window opened for
// the first time in a session must be on the kit at once (the owner, 6 Nov 2026: "First time it is opening old UI"); a question on the scene
// card shows who asks, what was heard, the progress, each answer whole and numbered, and no key cap but the number; and every window an
// action of the phone apps can open is traced to the runner of the kit that draws it (SweepAudit), by the callers' own source.
internal static partial class Program
{
    // The window mechanisms by the way their callers write them (comments taken out first); a file that matches is a caller.
    private static readonly KeyValuePair<AuditWindow, Regex>[] AuditCallers =
    {
        new KeyValuePair<AuditWindow, Regex>(AuditWindow.Scene, new Regex(@"new\s+SceneDesk\s*\(", RegexOptions.Compiled)),
        new KeyValuePair<AuditWindow, Regex>(AuditWindow.Picker, new Regex(@"new\s+(?:GameUi\.)?DeskList\s*\(|TravelList\.Make\s*\(", RegexOptions.Compiled)),
        new KeyValuePair<AuditWindow, Regex>(AuditWindow.ModBox, new Regex(@"MetaUi\.(?:Message|Confirm)\(|(?<![\w.])Confirm\(""", RegexOptions.Compiled)),
        new KeyValuePair<AuditWindow, Regex>(AuditWindow.Question, new Regex(@"GameConfirm\.", RegexOptions.Compiled)),
        new KeyValuePair<AuditWindow, Regex>(AuditWindow.TextInput, new Regex(@"TextBox\.Ask\(", RegexOptions.Compiled)),
        new KeyValuePair<AuditWindow, Regex>(AuditWindow.Prompt, new Regex(@"new\s+(?:GameUi\.)?(?:WorldPanel|DoorPanel)\b|\bJobPanel\b", RegexOptions.Compiled)),
        new KeyValuePair<AuditWindow, Regex>(AuditWindow.Dialog, new Regex(@"new\s+\w+Dialog\s*\(|\bDialogBuilder\b", RegexOptions.Compiled)),
        new KeyValuePair<AuditWindow, Regex>(AuditWindow.Notice, new Regex(@"Notifications\.Show", RegexOptions.Compiled)),
    };

    private static bool _oldWindowsChecked;

    private static void OldWindowsChecks()
    {
        OldWindowsColdOpen();
        OldWindowsCards();
        OldWindowsAudit();
    }

    // ---------------------------------------------------------------- the first opening

    private static void OldWindowsColdOpen()
    {
        string scripts = ModScripts();
        string kit = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "Dialog", "SceneKit.cs"));
        string host = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "Host", "UiGameHost.cs"));
        string desk = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "Dialog", "SceneDesk.cs"));
        string list = File.ReadAllText(System.IO.Path.Combine(scripts, "Core", "GameUi", "TravelList.cs"));

        // The scene window is switched on by its owner (TravelList.Open) before the kit binds to it: the first scene of a session makes the host in
        // that same call, so the watcher on the window never sees it open. The host shows the moment it binds over a window that is on.
        bool stands = Regex.IsMatch(kit, @"class\s+SceneHost\s*:[^{]*\bIUiGameStands\b");
        int open = list.IndexOf("public void Open(", StringComparison.Ordinal);
        int on = open >= 0 ? list.IndexOf("_root.SetActive(true);", open, StringComparison.Ordinal) : -1;
        int told = desk.IndexOf("SceneKit.Opened(this)", StringComparison.Ordinal);
        Check(on > open && open >= 0 && told > 0 && kit.Contains("h.Host.Install();"),
            "old windows: a scene window is switched on by its owner and only then bound by the kit, in the same call (the first opening of a session)");
        Check(UiHostRules.ShowsAtBind(stands, true), "old windows: the scene kit's host shows the moment it binds over a window already on, so the first opening is the kit's, not the game's list");
        Check(!UiHostRules.ShowsAtBind(false, true), "old windows: without that, a window opened before the bind shows the kit only from its second opening (the fault of 6 Nov 2026)");

        // Any other screen the game builds on its first use is found by the host's later looks already opened: it shows the kit at once too.
        Check(UiHostRules.ShowsLate(true, true, false) && !UiHostRules.ShowsLate(false, true, false) && !UiHostRules.ShowsLate(true, false, false)
              && !UiHostRules.ShowsLate(true, true, true),
            "old windows: a screen the game made after the host's first look and found switched on shows the kit at once; one found on the first look, one off, or an overlay, does not");
        Check(host.Contains("bool builtLate = _misses > 0 || _state == NotBuilt;")
              && host.Contains("UiHostRules.ShowsLate(builtLate, found.gameObject.activeInHierarchy, _screen.Kind == UiGameKind.Overlay)"),
            "old windows: the host reads whether the game made its screen after its first look, and shows the kit at the bind if so");

        // Every scene window names itself to the scene kit as its desk is made, and the picker leaves exactly those to it.
        string picker = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "World", "WorldPicker.cs"));
        Check(desk.Contains("SceneKit.Name(name);") && kit.Contains("internal static bool Owns(string name)") && picker.Contains("=> SceneKit.Owns(windowName)"),
            "old windows: a scene window is known to the scene kit by its desk, and the list picker leaves it there (no second list of names)");
        var names = new List<string>();
        var users = new List<string>();
        var at = new Regex(@"new\s+SceneDesk\s*\(\s*""([^""]+)""");
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string rel = System.IO.Path.GetRelativePath(scripts, file).Replace('\\', '/');
            if (rel.StartsWith("Ui/", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (Match m in at.Matches(File.ReadAllText(file)))
            {
                names.Add(m.Groups[1].Value);
                users.Add(rel);
            }
        }
        Check(names.Count == 6 && names.Distinct().Count() == 6, "old windows: six scene windows, each with a name of its own (" + string.Join(", ", users) + ")");
        string[] expected =
        {
            "Civic/OfficePanel.cs", "Economy/Ventures/PitchMeeting.cs", "Fame/FamePanel.cs", "Justice/JusticePanel.cs", "Tech/Hiring/InterviewMeeting.cs",
            "Tech/Venture/VentureMeeting.cs"
        };
        Check(expected.All(users.Contains), "old windows: the scene users are the court, the show, the office door, the investor's meeting, the interview and the founders' pitch");
        // The scene's own page goes through its list in one way: Begin, Talk or Moment, Add, Open.
        bool opens = true;
        foreach (string u in users)
        {
            string text = File.ReadAllText(System.IO.Path.Combine(scripts, u.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            opens &= text.Contains(".Open(") && text.Contains(".Begin(");
        }
        Check(opens, "old windows: every scene user fills its list with Begin and opens it with Open, the calls the kit's page is made from");

        // The check must be able to fail: a runner that does not exist, or a scene with no stand, is found.
        Check(OldWindowsUntaken(new[] { new AuditRow { App = "x", Action = "y", Window = "z", Kind = AuditWindow.Scene, Runner = "no_such_screen", Area = "Tech/" } }).Count == 1,
            "old windows: a window whose runner is not a registered screen is named by the audit");
    }

    // ---------------------------------------------------------------- the card

    private static void OldWindowsCards()
    {
        UiWords en = UiWords("en");
        SceneWords words = UiSamples.SceneWordsOf(en);
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Scenes");
        var s = new UiScene();
        bool pitchCards = true;
        string wrong = "none";
        foreach (string state in new[] { "venture_pitch", "venture_player", "interview_round", "pitch_question" })
        {
            var thread = new List<SceneSaid>();
            ScenePage page = UiSamples.ScenePageOf(en, state, thread);
            UiDecisionModel card = SceneViews.Decision(page, words);
            bool numbered = true, capless = true;
            for (int i = 0; i < card.Choices.Length; i++)
            {
                numbered &= card.Choices[i].Key == (UiAction)((int)UiAction.Pick1 + i);
                capless &= card.Choices[i].Cap == UiChoiceCap.None;
            }
            DialogRules.SplitInfo(page.Info, out string heard, out string chip);
            bool ok = SceneViews.LookOf(page) == SceneLook.Decision && card.Kicker == chip && !string.IsNullOrEmpty(chip) && card.Faces.Length == 1 && card.Faces[0] == page.Speaker
                      && card.EmblemLabel == page.Speaker && card.Body != null && card.Body.Contains(heard.Split('\n')[0]) && numbered && capless
                      && card.Choices[card.Choices.Length - 1].Name == page.Entries[page.Entries.Count - 1].Name && card.Buttons.Length == 0;
            if (!ok)
            {
                pitchCards = false;
                wrong = state + " (kicker '" + card.Kicker + "', chip '" + chip + "', faces " + card.Faces.Length + ", numbered " + numbered + ", capless " + capless + ")";
            }
            UiFrame f = frame;
            SceneLook look = SceneViews.Lay(s, page, thread, words, -1f, f, true, UiDefaultKeys.Keyboard);
            int panel = FirstPanel(s);
            float top = panel >= 0 ? s.Items[panel].Y : -1f, bottom = panel >= 0 ? s.Items[panel].Y + s.Items[panel].H : -1f;
            bool centred = panel >= 0 && Math.Abs(top - (frame.BandY - (bottom - top)) / 2f) <= 2f && top >= SceneViews.Clear - 1f && bottom <= frame.BandY - SceneViews.Clear + 1f;
            if (look != SceneLook.Decision || !centred)
            {
                pitchCards = false;
                wrong = state + " laid as " + look + (centred ? string.Empty : " off centre (top " + top + ", bottom " + bottom + " of " + frame.BandY + ")");
            }
        }
        Check(pitchCards, "old windows: a pitch, an interview round or an investor's question is one decision card, centred and clear of the hint "
                          + "bar: who asks, what was heard, the progress as its kicker, "
                          + "the answers numbered with no F or P cap, the way out last (" + wrong + ")");

        // The player's pitch says each answer whole, in every language the previews show.
        bool whole = true;
        string cut = "none";
        foreach (string lang in new[] { "en", "pt", "zh-cn", "de" })
        {
            UiWords t = UiWords(lang);
            foreach (string state in new[] { "venture_player", "pitch_question" })
            {
                ScenePage page = UiSamples.ScenePageOf(t, state, new List<SceneSaid>());
                UiDecisionModel card = SceneViews.Decision(page, UiSamples.SceneWordsOf(t));
                for (int i = 0; i < page.Entries.Count; i++)
                {
                    if (page.Entries[i].Said == null)
                    {
                        continue;
                    }
                    string all = card.Choices[i].Name + " " + string.Join(" ", (card.Choices[i].Lines ?? new UiConsequence[0]).Select(l => l.Text));
                    if (DialogSquash(all) != DialogSquash(ContactsRules.Plain(page.Entries[i].Said)) || (card.Choices[i].Lines?.Length ?? 0) > SceneViews.SaidMost)
                    {
                        whole = false;
                        cut = state + " answer " + i + " in " + lang;
                    }
                }
            }
        }
        Check(whole, "old windows: what the player says by an answer is on the card in full, none of it cut by an ellipsis (" + cut + ")");

        // The keys: 1 to 9 on the rows, F the first answer that can be taken, P the next, Esc the window's own cancel where the scene lets the player leave.
        ScenePage keys = UiSamples.ScenePageOf(en, "venture_pitch", new List<SceneSaid>());
        int main = SceneViews.Keyed(keys, out int second);
        SceneEntry first = keys.Entries[0];
        first.Enabled = false;
        keys.Entries[0] = first;
        int main2 = SceneViews.Keyed(keys, out int second2);
        Check(main == 0 && second == 1 && main2 == 1 && second2 == 2, "old windows: F takes the first answer that can be taken and P the next, a greyed one passed over");
        string screen = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Ui", "Dialog", "SceneKit.cs"));
        string deskSrc = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Ui", "Dialog", "SceneDesk.cs"));
        Check(screen.Contains("bool done = List.Pick(List.Page.Entries[arg].Id);") && deskSrc.Contains("UiClone.Safe(() => Picked(id));") && deskSrc.Contains("_onPick?.Invoke(id);")
              && screen.Contains("action == UiAction.Alt ? main : action == UiAction.Option ? second : -1"),
            "old windows: a press on a row, 1 to 9, F or P goes through the window's own pick to its owner's callback, which the meeting's rules answer");
        Check(screen.Contains("if (List.Locked || !List.IsOpen)") && screen.Contains("List.Close();") && screen.Contains("public bool Back() => Leave();"),
            "old windows: Esc is the window's own cancel where the scene lets the player leave, and the game's menu where it does not");
        var pad = new UiScene();
        UiFrame padFrame = UiFrame.FullMenu(16f / 9f, "Scenes");
        SceneViews.Lay(pad, UiSamples.ScenePageOf(en, "venture_player", new List<SceneSaid>()), new List<SceneSaid>(), words, -1f, padFrame, true, UiDefaultKeys.XboxPad);
        int reached = 0;
        for (int i = 0; i < 4; i++)
        {
            reached += pad.FindNode(UiDecision.ChoiceCommand, i) >= 0 ? 1 : 0;
        }
        Check(reached == 4 && UiFocusRules.Unreached(pad, out string _) == 0, "old windows: with a pad every answer of the player's pitch can be moved to and pressed");

        // The stray caps of the older cards are gone from the scene screens' source, so no scene shows F or P by a row.
        string views = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Ui", "SceneViews.cs"));
        Check(!views.Contains("DialogRules.Keys("), "old windows: no scene row carries an F or a P cap, only its number");
    }

    // ---------------------------------------------------------------- the audit

    private static void OldWindowsAudit()
    {
        string scripts = ModScripts();
        AuditRow[] rows = SweepAudit.Rows;
        HashSet<string> screens = new HashSet<string>(UiScreens.All.Select(d => d.Id), StringComparer.Ordinal);
        List<string> untaken = OldWindowsUntaken(rows);
        Check(untaken.Count == 0, "old windows: every window an action opens has a registered runner with a switch, or the owner's dated decision ("
                                    + (untaken.Count == 0 ? "all " + rows.Length + " rows" : string.Join("; ", untaken)) + ")");
        bool apps = true;
        string who = "all";
        foreach (SweepRow ledger in SweepLedger.Rows.Where(r => r.State == SweepState.Kit && r.Surface.EndsWith(" app", StringComparison.Ordinal)))
        {
            if (!rows.Any(r => r.Kind == AuditWindow.AppFace && r.Runner == ledger.Screen) && ledger.Screen != "studio")
            {
                apps = false;
                who = ledger.Surface;
            }
        }
        Check(apps, "old windows: every phone app of the ledger has an action row of its own naming its face (" + who + ")");

        // Every caller of a window mechanism in a folder is named by a row for that folder and kind: a new window cannot reach players undrawn.
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var stale = new List<string>();
        int callers = 0;
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string rel = System.IO.Path.GetRelativePath(scripts, file).Replace('\\', '/');
            if (rel.StartsWith("Ui/", StringComparison.Ordinal) || rel.StartsWith("Core/GameUi/", StringComparison.Ordinal) || rel.IndexOf("Test", StringComparison.Ordinal) >= 0
                || rel.StartsWith("Diagnostics/", StringComparison.Ordinal))
            {
                continue;
            }
            string code = Regex.Replace(File.ReadAllText(file), @"^\s*//.*$", string.Empty, RegexOptions.Multiline);
            foreach (KeyValuePair<AuditWindow, Regex> mech in AuditCallers)
            {
                if (mech.Value.IsMatch(code))
                {
                    callers++;
                    if (!rows.Any(r => r.Kind == mech.Key && SweepAudit.Holds(r.Area, rel)))
                    {
                        missing.Add(mech.Key + " in " + rel);
                    }
                }
            }
        }
        foreach (AuditRow r in rows)
        {
            if (r.Area.Length > 0 && !Directory.Exists(System.IO.Path.Combine(scripts, r.Area.TrimEnd('/').Replace('/', System.IO.Path.DirectorySeparatorChar))))
            {
                stale.Add(r.Area);
            }
        }
        Check(missing.Count == 0, "old windows: every caller of a scene, list, question, typed answer, prompt or dialogue is covered by a row of the audit for its folder and kind ("
                                  + callers + " callers"
                                  + (missing.Count == 0 ? string.Empty : "; add " + string.Join(", ", missing.Take(12))) + ")");
        Check(stale.Count == 0, "old windows: every folder the audit names exists (" + (stale.Count == 0 ? "all" : string.Join(", ", stale)) + ")");

        // The ledger says what is true: scenes are drawn by their own runner, and none of the surfaces claims the picker for them.
        SweepRow scene = SweepLedger.Rows.FirstOrDefault(r => r.Screen == "scenes");
        Check(scene.Surface != null && scene.State == SweepState.Kit && scene.Words.Length > 40, "old windows: the ledger names the scene windows as drawn by the scenes screen");
        bool listsHonest = SweepLedger.Rows.Where(r => r.Surface.StartsWith("A pick list window", StringComparison.Ordinal)).All(r => r.Words.Contains("scene") || r.Words.Contains("Scene"));
        Check(listsHonest, "old windows: the ledger's pick list row says which windows it does not take (the scenes)");
        Check(!SweepLedger.Rows.Any(r => r.Words != null && r.Words.Contains("the scene talks")), "old windows: the ledger does not claim the monologue panel draws the scenes");
    }

    // The rows whose window no registered runner (with a switch) draws; empty when each is taken.
    private static List<string> OldWindowsUntaken(AuditRow[] rows)
    {
        var found = new List<string>();
        var screens = UiScreens.All.ToDictionary(d => d.Id, d => d, StringComparer.Ordinal);
        foreach (AuditRow r in rows)
        {
            string runner = r.Kind == AuditWindow.AppFace ? r.Runner : SweepAudit.RunnerOf(r.Kind);
            if (r.Decision != null)
            {
                continue;
            }
            if (runner == null || !screens.TryGetValue(runner, out UiScreenDef def) || def.Runner == null && def.Id != "session_panel" && def.Template != UiTemplate.Hud)
            {
                found.Add(r.App + ": " + r.Action + " -> " + (runner ?? "nothing"));
            }
            else if (r.Kind != AuditWindow.AppFace && r.Runner != runner)
            {
                found.Add(r.App + ": " + r.Action + " is drawn by " + runner + ", not " + r.Runner);
            }
        }
        return found;
    }
}
