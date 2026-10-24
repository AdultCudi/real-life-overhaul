using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Careers;

// A class or exam at a desk begins in a Going Public session as it does alone (Scripts/Careers: Study/ClassPlayRules.SessionPick
// and ShowsDesk, SchoolDesk's buttons, CareersDirector.OpenDesk). In a session Going Public hides the game's activity panel
// (a prefix on PlayerActivityUI.HidePanel) and, every half second, presses for the player the first button of the activity
// whose name holds "start" (MPRestSync.UpdateSeated, "sit immediately, no Start button"); its own stand up takes one holding
// "stop" or "cancel". The desk's begin button was named RealLifeClass, so the walk to the classroom ended at a hidden panel
// nobody could press. First the rule against Going Public's own reading, then the source, then the test and the texts.
internal static partial class Program
{
    private static void HiddenPanels()
    {
        HiddenPanelRule();
        HiddenPanelSources();
        HiddenPanelTexts();
    }

    // ---------------------------------------------------------------- the rule

    private static void HiddenPanelRule()
    {
        ClassPlayRules.SessionPick(new[] { "CancelStudy", "StartStudy" }, null, out int start, out int cancel);
        Check(start == 1 && cancel == 0, "hidden panel: Going Public presses the game's own study Start and stands up on its Cancel");
        ClassPlayRules.SessionPick(new[] { "CancelRealLifeClass", "RealLifeClass" }, null, out start, out cancel);
        Check(start < 0 && cancel == 0, "hidden panel: the desk's begin button as it was named (RealLifeClass) is never pressed in a session: the class that never began");
        ClassPlayRules.SessionPick(new[] { ClassPlayRules.DeskCancelName, ClassPlayRules.DeskStartName }, new[] { true, true }, out start, out cancel);
        Check(start == 1 && cancel == 0, "hidden panel: the desk's Cancel and Start, as it shows them, are Going Public's stand up and press");
        ClassPlayRules.SessionPick(new[] { ClassPlayRules.DeskStopName }, null, out start, out cancel);
        Check(start < 0 && cancel == 0, "hidden panel: while the class runs only its Stop is there, Going Public's stand up, and nothing is pressed");
        ClassPlayRules.SessionPick(new[] { "CancelRealLifeWork", "StartRealLifeWork", "StartLateRealLifeWork" }, null, out start, out cancel);
        ClassPlayRules.SessionPick(new[] { "CancelRealLifeWork", "StartLateRealLifeWork" }, null, out int late, out _);
        Check(start == 1 && cancel == 0 && late == 1, "hidden panel: a job's desk is begun on its Start, and on Work late when that is its only way to begin");
        ClassPlayRules.SessionPick(new[] { "CancelX", "StartX" }, new[] { true, false }, out start, out _);
        Check(start < 0, "hidden panel: a button that cannot be pressed is left out, as Going Public leaves it out");
        ClassPlayRules.SessionPick(new[] { "A", "B", "C", "D", "StartX" }, null, out start, out _);
        Check(start < 0, "hidden panel: Going Public reads four buttons at most, so a begin button comes within the first four");
        ClassPlayRules.SessionPick(new[] { "cancelx", "STARTX" }, null, out start, out cancel);
        Check(start == 1 && cancel == 0, "hidden panel: names are read whatever their case");
        ClassPlayRules.SessionPick(null, null, out start, out cancel);
        ClassPlayRules.SessionPick(new string[] { null, "StartX" }, null, out int afterNull, out _);
        Check(start < 0 && cancel < 0 && afterNull == 1, "hidden panel: no buttons press nothing, and a button with no name is passed over");
        Check(ClassPlayRules.ShowsDesk(false, true) && ClassPlayRules.ShowsDesk(false, false) && ClassPlayRules.ShowsDesk(true, true) && !ClassPlayRules.ShowsDesk(true, false),
            "hidden panel: alone a desk always shows its panel; in a session only when it can begin, else a notice says why, as the hidden panel cannot");
        ClassPlayRules.SessionPick(new[] { ClassPlayRules.DeskCancelName, ClassPlayRules.DeskStartName }, null, out int again, out int againCancel);
        Check(again == 1 && againCancel == 0, "hidden panel: the same answer on every ask, on the host and on a guest alike");
    }

    // ---------------------------------------------------------------- the source

    private static void HiddenPanelSources()
    {
        string school = ModSource(@"Careers\SchoolDesk.cs"), play = ModSource(@"Careers\CareersDirector.ClassPlay.cs");
        string tests = ModSource(@"Careers\CareersDirector.Tests.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), prompt = ModSource(@"Careers\CareersDirector.Prompt.cs");
        Check(school.Length > 0 && play.Length > 0 && tests.Length > 0 && mod.Length > 0 && prompt.Length > 0, "hidden panel: the desk, the director's desks and tests and the options page are read");
        string buttons = MethodText(school, "public ButtonInfo[] GetButtons()");
        Check(Occurrences(buttons, "new ButtonInfo(ClassPlayRules.DeskStartName,") == 2 && buttons.Contains("new ButtonInfo(ClassPlayRules.DeskCancelName,")
              && buttons.Contains("new ButtonInfo(ClassPlayRules.DeskStopName,") && !buttons.Contains("new ButtonInfo(\""),
            "hidden panel: the desk's Start, Cancel and Stop are named by the rule's names, at the university's rooms and at the school's desks alike");

        // Every activity of the mod's on the game's panel (an IPlayerActivity; an overlay's box is not the hidden panel): each
        // button that begins it (on the Confirm key, or Work late, which takes Confirm when it is alone) is named so Going
        // Public presses it in a session.
        var unnamed = new List<string>();
        int begins = 0;
        foreach (string file in System.IO.Directory.GetFiles(DesignScripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string text = System.IO.File.ReadAllText(file);
            if (!text.Contains(": IPlayerActivity"))
            {
                continue;
            }
            foreach (Match m in Regex.Matches(text, "new ButtonInfo\\(\"([A-Za-z]+)\"(?:(?!new ButtonInfo)[^;])*?(PlayerAction\\.Confirm|, key\\))"))
            {
                begins++;
                ClassPlayRules.SessionPick(new[] { m.Groups[1].Value }, null, out int found, out _);
                if (found < 0)
                {
                    unnamed.Add(System.IO.Path.GetFileName(file) + ": " + m.Groups[1].Value);
                }
            }
        }
        Check(begins >= 5 && unnamed.Count == 0, "hidden panel: every begin button of the mod's activities (" + begins + ") is named for Going Public's press in a session (not: "
                                                  + string.Join(", ", unnamed) + ")");

        string open = MethodText(play, "internal string OpenDesk(EducationDoorController door, int room, bool begin)");
        int shows = open.IndexOf("if (!ClassPlayRules.ShowsDesk(session, offer.Can))", StringComparison.Ordinal);
        int own = open.IndexOf("return OwnDesk(door, offer, begun, session, where, begin);", StringComparison.Ordinal);
        Check(shows > 0 && own > shows && open.Contains("return offer.Why ?? \"reallife_study_why_no_class\";"),
            "hidden panel: in a session a desk with nothing to begin opens no hidden panel and gives its reason for a notice, before the desk's own panel is opened");
        string press = MethodText(school, "private void Press()");
        string hold = MethodText(prompt, "internal string HoldTest(Sitting sitting, IPlayerActivity holder)");
        Check(press.Contains("Sitting sitting = _offer.Test;") && press.Contains("_director.HoldTest(sitting, this)")
              && press.Contains("_director.DeskSitting(_offer.Course, _offer.Kind, this, out sitting)")
              && hold.Contains("SeatWhy(false)") && hold.Contains("Hold(sitting, holder);"),
            "hidden panel: a desk's press takes the course's own checks, and only a test's class is held as it is, the player's seat still asked");
        string test = MethodText(tests, "public void TestSessionDesk()");
        string after = MethodText(tests, "private IEnumerator SessionPress(SchoolDesk desk, string course)");
        Check(mod.Contains(".AddButton(\"reallife_study_test_session_desk\", () => Careers.CareersDirector.Instance?.TestSessionDesk())")
              && test.Contains("OwnDesk(door, offer, false, true, ") && test.Contains("DeskGame.NearestStudyDoor()") && test.Contains("\"reallife_study_test_session_desk_none\"")
              && after.Contains("ClassPlayRules.SessionPick(names, on, out int start, out int cancel);") && after.Contains("buttons[start].onClick();")
              && after.Contains("\"Careers check (session desk): ") && after.Contains("\"PASS\"") && after.Contains("\"FAIL, \"")
              && after.Contains("\"reallife_study_test_session_desk_pass\"") && after.Contains("\"reallife_study_test_session_desk_fail\""),
            "hidden panel: Options > Tests opens the nearest desk the session's way, presses as Going Public does and writes one PASS or FAIL check line");
    }

    // ---------------------------------------------------------------- the texts

    private static void HiddenPanelTexts()
    {
        var keys = new[] { "reallife_study_test_session_desk", "reallife_study_test_session_desk_none", "reallife_study_test_session_desk_pass", "reallife_study_test_session_desk_fail" };
        var lacking = new List<string>();
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            foreach (string key in keys)
            {
                if (own == null || !own.TryGetValue(key, out string words) || words.Length == 0 || words.IndexOf('{') >= 0)
                {
                    lacking.Add(code + " " + key);
                }
            }
        }
        Check(lacking.Count == 0, "hidden panel: the test's button and notices are in en, pt and zh-cn (lacking: " + string.Join(", ", lacking) + ")");
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("hidden panel", langs, codes))
        {
            return;
        }
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Fixed: in a Going Public session, a class or exam now begins at its school desk or university classroom when you get there",
            ["pt"] = "Corrigido: numa sessão do Going Public, a aula ou a prova agora começa na mesa da escola ou na sala da universidade quando você chega",
            ["zh-cn"] = "已修复：在 Going Public 联机中，你到达学校的课桌或大学的教室时，课程或考试现在会开始",
            ["de"] = "Behoben: In einer Going-Public-Sitzung beginnt eine Stunde oder Prüfung jetzt an ihrem Tisch in der Schule",
            ["fr"] = "Corrigé : dans une session Going Public, une séance ou un examen commence désormais à son bureau de l'école",
            ["es"] = "Corregido: en una sesión de Going Public, una clase o un examen ahora empieza en su pupitre de la escuela",
            ["ru"] = "Исправлено: в сессии Going Public занятие или экзамен теперь начинается за партой школы",
            ["nl"] = "Opgelost: in een Going Public-sessie begint een les of examen nu aan de tafel op school",
            ["tr"] = "Düzeltildi: bir Going Public oturumunda ders veya sınav artık oraya vardığınızda"
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
            "hidden panel: What's new has the fix's line, with its language's fix mark, in some reallife_changes_ row of every language (lacking: " + string.Join(", ", missing) + ")");
    }
}
