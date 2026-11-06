using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.NewBuildings;

// fix/crest-motors, run alone with --crest-motors. A guest of a Going Public session was told "there is no traffic on the roads" at the driving
// school and charged nothing, but never told the real reason (the school's car comes out of the host's traffic): the desk now says so in the list,
// before the fee, and the Guide says it. Standing up from a seat in one of the district's buildings (Crest Motors' dealer's desk, a lounge chair)
// with no walkable way to the door puts the player at the game's own entry point, whatever the floor's rules got wrong about a layout.
internal static partial class Program
{
    private static void CrestMotors()
    {
        // The school's answer: a guest first, whatever the traffic does; the others by their own street.
        Check(LessonRules.Refusal(true, true) == LessonRules.GuestKey && LessonRules.Refusal(true, false) == LessonRules.GuestKey,
            "crest motors: a guest is told the lesson is the host's to take, whether or not the traffic runs on their street");
        Check(LessonRules.Refusal(false, true) == null && LessonRules.Refusal(false, false) == LessonRules.NoRoadsKey,
            "crest motors: the host and a player alone can take a lesson while their own traffic runs, and are told there is none when it does not");
        Check(LessonRules.GuestKey != LessonRules.NoRoadsKey, "crest motors: the guest's refusal is not the one about the roads");

        // The stand-up: only a player who was seated, is standing now and cannot reach the door is put at it.
        Check(DeskWatchRules.StoodUpBoxed(true, false, false), "crest motors: a player who has just stood up with no way to the door is put at the door");
        Check(!DeskWatchRules.StoodUpBoxed(true, false, true) && !DeskWatchRules.StoodUpBoxed(true, true, false)
              && !DeskWatchRules.StoodUpBoxed(false, false, false) && !DeskWatchRules.StoodUpBoxed(false, true, false),
            "crest motors: a player with a way out, one still seated, or one who was never seated is left where they are");

        string school = Crest(@"Licences\Driving\DrivingSchool.cs"), practical = Crest(@"Licences\Driving\PracticalTest.cs"), director = Crest(@"Licences\LicenceDirector.cs"),
            dealer = Crest(@"Uptown\LifeGame.Dealer.cs"), desk = Crest(@"Construction\BuilderDeskGame.cs");
        string available = MemberText(school, "public static bool Available(SchoolService service, bool inPerson, out string reasonKey)");
        int guest = available.IndexOf("LessonRules.Refusal(GoingPublic.TrafficMirrored, true)", StringComparison.Ordinal), money = available.IndexOf("Licences.Affords(Fee(service))", StringComparison.Ordinal);
        Check(available.Contains("reasonKey == null && Drives(service)") && guest > 0 && money > guest,
            "crest motors: the school's list says a guest cannot be taken out, after the service's own reasons and before the fee is asked");
        Check(MemberText(school, "public static bool Drives(SchoolService service)").Contains("SchoolService.TestHeavy") && director.Contains("DrivingSchool.Drives(service)"),
            "crest motors: one list of the services that take the car out serves the list and the desk");
        Check(MemberText(practical, "public bool CanStage(").Contains("LessonRules.Refusal(GoingPublic.TrafficMirrored, TrafficTools.Ready && RoadMap.Ensure())")
              && MemberText(practical, "public bool Begin(").Contains("LessonRules.Refusal(GoingPublic.TrafficMirrored, TrafficTools.Ready && RoadMap.Ensure())")
              && !practical.Contains("\"reallife_lic_no_roads\""),
            "crest motors: staging a lesson before the fee, and starting it, ask the same rule, so a guest is never charged and told about roads");
        Check(director.Contains("LessonRules.GuestKey") && director.Contains("LessonRules.NoRoadsKey"), "crest motors: the desk counts both answers as nothing paid");

        string watch = MemberText(dealer, "public static string Watch(float now)");
        string unbox = MemberText(dealer, "private static string Unbox(ThirdPersonCharacter body)");
        Check(watch.Contains("_wasSeated = seated;") && watch.Contains("Unbox(body)") && dealer.Contains("_wasSeated = false;"),
            "crest motors: the once-a-second look in a district building asks where a player is on the look after they stand up");
        Check(unbox.Contains("NavMesh.CalculatePath(") && unbox.Contains("PathComplete") && unbox.Contains("agent.Warp(door)") && unbox.Contains("DeskWatchRules.StoodUpBoxed(")
              && desk.Contains("internal static bool Door(BuildingManager manager, out Vector3 door)"),
            "crest motors: a player with no complete way to the game's own door is warped to its spawn point");
        Check(!Regex.IsMatch(watch + unbox, @"FindObjectsByType|FindObjectOfType|Physics\.|new NavMeshPath|new List<"),
            "crest motors: the look allocates and searches nothing (one path, kept)");

        // Texts.
        string folder = LocalesFolder();
        string[] langs = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: ", "Naprawiono: ", "수정: " };
        string noGuest = null, noGuide = null, noFix = null, bad = null;
        for (int i = 0; i < langs.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, langs[i] + ".json")) ?? new Dictionary<string, string>();
            if (i < 3)
            {
                if (!words.TryGetValue(LessonRules.GuestKey, out string t) || t.Length < 30 || t.Length > 240 || t.Contains("!") || t.Contains("—") || t.Contains("–"))
                {
                    noGuest = noGuest ?? langs[i];
                }
            }
            if (!words.TryGetValue("reallife_guide_licences", out string guide) || !guide.Contains("Going Public"))
            {
                noGuide = noGuide ?? langs[i];
            }
            if (!words.TryGetValue("reallife_changes_next", out string next) || !next.Contains(marks[i]) || !next.Contains("Crest Motors") || !next.Contains("Going Public"))
            {
                noFix = noFix ?? langs[i];
            }
            else if (next.Contains("  ") || next.Contains("!") || next.Contains("—") || next.Contains("–"))
            {
                bad = bad ?? langs[i];
            }
        }
        Check(noGuest == null, "crest motors: the guest's refusal is a text of at most 240 characters, without dashes, in English, Portuguese and Chinese" + (noGuest != null ? " (" + noGuest + ")" : string.Empty));
        Check(noGuide == null, "crest motors: the Getting a licence page names Going Public in all eleven languages" + (noGuide != null ? " (" + noGuide + ")" : string.Empty));
        Check(noFix == null && bad == null, "crest motors: the next What's new row has the fix sentence in all eleven languages, tidy" + (noFix != null ? " (" + noFix + ")" : bad != null ? " (" + bad + ")" : string.Empty));
    }

    private static string Crest(string file)
    {
        return CodeAlone(ModSource(file)).Replace("\r\n", "\n");
    }
}
