using System;
using System.Collections.Generic;

internal static partial class Program
{
    // The home routines' test buttons were pressed in the options menu, where the game's check for starting anything cannot
    // pass, so "Not now" was all they ever said; and nothing in the Guide said where a player starts a run or an exercise.
    private static void ExerciseFixes()
    {
        string tests = ModSource(@"Home\HomeDirector.Tests.cs");
        string routine = MemberText(tests, "public void TestRoutine(HomeWish wish)");
        Check(routine.Contains("TestWait.Run(Logger,") && routine.Contains("wish != HomeWish.StopRun") && !routine.Contains("HomeWish.CookFirst")
              && routine.Contains("() => Act(wish, true)"),
            "exercise fix: every routine test button waits for the menus to close, not only the cooking ones");
        Check(MemberText(tests, "public void TestPage()").Contains("TestWait.Run(Logger, \"Home page\"")
              && !tests.Contains("Refuse(\"reallife_home_refused_busy\""),
            "exercise fix: the wellbeing page test waits too, and no test refuses with Not now while its own menu is up");

        string wait = ModSource(@"Core\TestWait.cs");
        Check(wait.Contains("WaitSeconds = 20f") && wait.Contains("WaitForSecondsRealtime(0.25f)") && wait.Contains("Time.unscaledTime")
              && wait.Contains("Cancel();") && wait.Contains("Destroy(gameObject)") && wait.Contains("TestRefusal.ScreenBusy"),
            "exercise fix: a waiting test polls four times a second on real time, is replaced by the next press, gives up after 20 s and leaves nothing behind");

        Check(ModSource(@"Fame\FameDirector.Tests.cs").Contains("TestWait.Run(Logger, \"Standing night show\"")
              && ModSource(@"Fame\FameDirector.Tests.cs").Contains("TestWait.Run(Logger, \"Standing evening\"")
              && ModSource(@"Civic\CivicDirector.Tests.cs").Contains("TestWait.Run(Logger, \"Civic office scene\"")
              && ModSource(@"Floors\FloorsDirector.Tests.cs").Contains("TestWait.Run(Logger, TestName, FloorsGame.Free"),
            "exercise fix: the scene tests of Fame, Civic and Floors that needed the game free wait for the menu as well");

        string director = ModSource(@"Home\HomeDirector.cs");
        string routines = ModSource(@"Home\HomeDirector.Routines.cs");
        string busy = MemberText(director, "private static string BusyKey()");
        Check(busy.Contains("reallife_home_refused_busy_talk") && busy.Contains("reallife_home_refused_busy_activity") && busy.Contains("reallife_home_refused_busy_skip")
              && busy.Contains("reallife_home_refused_busy_menu") && busy.Contains("reallife_home_refused_busy_paused")
              && busy.IndexOf("IsUiOpen", StringComparison.Ordinal) < busy.IndexOf("Paused", StringComparison.Ordinal),
            "exercise fix: the Not now text names a conversation, an activity, a time skip, a menu or the pause, in that order");
        Check(Occurs(routines, "Refuse(BusyKey(), what)") == 1 && routines.Contains("BusyKey(), what, \"waited 20 s\"") && director.Contains("Refuse(BusyKey(), _wish.ToString()")
              && !routines.Contains("Refuse(\"reallife_home_refused_busy\"") && !director.Contains("Refuse(\"reallife_home_refused_busy\""),
            "exercise fix: every refusal for a player who is not free says what is in the way");

        string phone = ModSource(@"Home\HomePhone.cs");
        Check(phone.Contains("HomeWish.Run") && phone.Contains("HomeWish.Stretch") && phone.Contains("HomeWish.PushUps") && phone.Contains("HomeWish.SitUps"),
            "exercise fix: the Wellbeing contact is the way to start a run and the three floor exercises in play");

        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("exercise texts", words, codes))
        {
            return;
        }
        foreach (string key in new[] { "menu", "talk", "activity", "skip", "paused" })
        {
            string id = "reallife_home_refused_busy_" + key;
            bool all = true;
            for (int l = 0; l < codes.Length; l++)
            {
                // The other languages fall back to English until they are translated.
                all &= (codes[l] != "en" && codes[l] != "pt" && codes[l] != "zh-cn") || (words[l].TryGetValue(id, out string v) && v.Length > 0 && v.Length <= 240);
            }
            Check(all, "exercise texts: " + key + " is in English, Portuguese and Chinese and fits a notification");
        }
        for (int l = 0; l < codes.Length; l++)
        {
            string guide = words[l].TryGetValue("reallife_guide_home", out string g) ? g : string.Empty;
            string contact = words[l].TryGetValue("reallife-health", out string c) ? c : "?";
            string run = words[l].TryGetValue("reallife_health_menu_run", out string r) ? r : "?";
            string exercise = words[l].TryGetValue("reallife_health_menu_exercise", out string e) ? e : "?";
            Check(guide.Contains(contact) && guide.Contains(run) && guide.Contains(exercise),
                "exercise texts: the Guide's Home page names the " + contact + " contact, " + run + " and " + exercise + " in " + codes[l]);
        }
    }
}
