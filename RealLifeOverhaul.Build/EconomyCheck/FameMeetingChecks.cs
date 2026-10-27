using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Fame;

// Checks that every meeting Standing puts in the Planner has its place (Scripts/Fame/FamePress, Wiring/StandingLinks): each kind
// of invitation is held by a video call, the Planner entry says so, the Standing page offers the call to join while it
// runs, and nothing starts by itself where the player stands. A player once could not find where an evening with investors was.
internal static partial class Program
{
    private static readonly string[] MeetingWords =
    {
        "reallife_fame_where_call", "reallife_fame_label_where", "reallife_fame_plan_call_what", "reallife_fame_invite_call", "reallife_fame_join",
        "reallife_fame_join_busy", "reallife_fame_join_driving", "reallife_fame_join_talking", "reallife_fame_join_activity", "reallife_fame_join_scene", "reallife_fame_join_window",
        "reallife_fame_join_subway", "reallife_fame_join_boat", "reallife_fame_note_call_on", "reallife_fame_call_place_0", "reallife_fame_call_place_1", "reallife_fame_call_open_0",
        "reallife_fame_call_open_1", "reallife_fame_test_meetings", "reallife_fame_test_meetings_ok", "reallife_fame_test_meetings_bad"
    };

    private static void FameMeetings()
    {
        // ---- every kind has a place or a call
        var unplaced = new List<string>();
        foreach (FameInviteKind kind in Enum.GetValues(typeof(FameInviteKind)))
        {
            if (FamePress.Held(kind) == FameHeld.Nowhere)
            {
                unplaced.Add(kind.ToString());
            }
        }
        Check(unplaced.Count == 0, "Standing: every kind of invitation is held by a call or at a place" + (unplaced.Count > 0 ? "; not: " + string.Join(", ", unplaced) : string.Empty));
        Check(FamePress.Held((FameInviteKind)99) == FameHeld.Nowhere, "Standing: a kind nobody gave a place is told as having none");

        // ---- the call can be joined from its hour for the late hours, and only when accepted
        var invite = new FameInvite { Id = 1, Kind = FameInviteKind.Investors, Day = 10, Hour = 19, State = FameInviteState.Accepted };
        Check(!FamePress.Joinable(invite, 10, 18) && FamePress.Joinable(invite, 10, 19) && FamePress.Joinable(invite, 10, 19 + FamePress.LateHours)
              && !FamePress.Joinable(invite, 10, 19 + FamePress.LateHours + 1) && !FamePress.Joinable(invite, 9, 23) && !FamePress.Joinable(invite, 11, 19),
            "Standing: a call is joined from its hour until the late hours are over");
        invite.State = FameInviteState.Offered;
        Check(!FamePress.Joinable(invite, 10, 19), "Standing: an invitation not yet accepted cannot be joined");
        invite.State = FameInviteState.Accepted;
        FameFile f = FameKnown(40);
        Check(FamePress.Due(f, invite, 10, 19, true) && !FamePress.Due(f, invite, 10, 19, false), "Standing: Due still asks whether the player is free, as before");

        // ---- the Planner says where; nothing starts by itself
        string links = Read(@"Wiring\StandingLinks.cs"), press = Read(@"Fame\FameDirector.Press.cs");
        Check(links.Contains("e.Where = PlannerText.T(\"reallife_fame_where_call\")"), "Standing: an accepted invitation's Planner entry says where it is held");
        string hour = FlareMethod(press, "private void PressHour(int day, int hour)"), join = FlareMethod(press, "public void JoinInvite(int id)");
        // ---- joining from the phone: the phone and the pause never block; each real blocker is named
        var idle = new FameJoinState();   // standing or sitting idle, phone open, game paused: neither is a field of the state
        Check(FameJoin.Decide(idle) == FameJoinBlock.None, "Standing: idle with the phone open and the game paused, a call can be joined");
        var each = new (string Name, FameJoinState State, FameJoinBlock Want)[]
        {
            ("driving", new FameJoinState { Driving = true }, FameJoinBlock.Driving),
            ("a conversation or question", new FameJoinState { Talking = true }, FameJoinBlock.Talking),
            ("the work or study panel", new FameJoinState { Activity = true }, FameJoinBlock.Activity),
            ("a scene of the mod's", new FameJoinState { Scene = true }, FameJoinBlock.Scene),
            ("a trip window", new FameJoinState { TripWindow = true }, FameJoinBlock.TripWindow),
            ("the subway", new FameJoinState { Subway = true }, FameJoinBlock.Subway),
            ("the casino boat", new FameJoinState { Boat = true }, FameJoinBlock.Boat),
            ("loading or another screen", new FameJoinState { Other = true }, FameJoinBlock.Other)
        };
        var wrong = new List<string>();
        var keys = new HashSet<string>();
        foreach (var e in each)
        {
            if (FameJoin.Decide(e.State) != e.Want)
            {
                wrong.Add(e.Name);
            }
            keys.Add(FameJoin.Key(e.Want));
        }
        Check(wrong.Count == 0, "Standing: each of driving, a conversation, work, a scene, a trip window, the subway, the boat and another screen blocks the join" + (wrong.Count > 0 ? "; not: " + string.Join(", ", wrong) : string.Empty));
        Check(keys.Count == each.Length && FameJoin.Key(FameJoinBlock.Driving) == "reallife_fame_join_driving",
            "Standing: every blocker has a refusal of its own, the generic one only for the rest");
        Check(FameJoin.Decide(new FameJoinState { Driving = true, Scene = true, Other = true }) == FameJoinBlock.Driving,
            "Standing: with several blockers the most specific is named, driving first");
        string game = Read(@"Fame\FameGame.cs"), joinBlock = FlareMethod(game, "public static FameJoinBlock JoinBlock()");
        Check(join.Contains("FameGame.JoinBlock()") && join.Contains("FameJoin.Key(") && join.Contains("Logger?.Info(") && !join.Contains("Free()") && join.Contains("FameGame.Unpause()"),
            "Standing: joining asks the join decision, logs and names the blocker on a refusal, and takes the game off pause when the call begins");
        Check(joinBlock.Length > 0 && !joinBlock.Contains("IsSafeForEvent") && !joinBlock.Contains(".Paused") && !joinBlock.Contains("FullMenu") && !joinBlock.Contains("Scenes.PlayerHeld")
              && !joinBlock.Contains("AnyWindow(true)") && joinBlock.Contains("PlayerInScene"),
            "Standing: the join decision does not read the pause or the phone's full menu, which are up whenever Join is pressed");

        Check(hour.Length > 0 && !hour.Contains("StartScene(") && join.Contains("FamePress.Joinable(") && join.Contains("StartScene(invite)"),
            "Standing: a call begins only when the player joins it from the Standing page, never by itself in the hour");
        Check(Read(@"Fame\FamePage.Press.cs").Contains("director.JoinInvite(") && Read(@"Fame\FamePage.Press.cs").Contains("FamePress.Joinable("),
            "Standing: the Standing page offers the call to join while it runs");
        Check(Read(@"Fame\FameDirector.Tests.cs").Contains("public void TestMeetings()") && Read(@"Core\RealLifeOverhaulMod.cs").Contains("reallife_fame_test_meetings"),
            "Standing: a test in the options makes every kind of invitation and checks the Planner and the page");

        // ---- the words, in the three languages with the same {names}
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(PhotoScripts, @"..\Locales"));
        var langs = new[] { "en", "pt", "zh-cn" };
        var words = new Dictionary<string, string>[3];
        for (int i = 0; i < 3; i++)
        {
            words[i] = LoadWords(System.IO.Path.Combine(folder, langs[i] + ".json"));
        }
        Check(words[0] != null && words[1] != null && words[2] != null, "Standing meetings: the three locales are where the checks look for them");
        if (words[0] == null || words[1] == null || words[2] == null)
        {
            return;
        }
        var holes = new Regex(@"\{[a-z_]+\}");
        var missing = new List<string>();
        foreach (string key in MeetingWords)
        {
            string names = null;
            for (int i = 0; i < 3; i++)
            {
                if (!words[i].TryGetValue(key, out string text) || text.Length == 0 || text.Contains("  ") || text.Contains("!"))
                {
                    missing.Add(langs[i] + ":" + key);
                    continue;
                }
                var set = new List<string>();
                foreach (Match m in holes.Matches(text))
                {
                    set.Add(m.Value);
                }
                set.Sort(StringComparer.Ordinal);
                string joined = string.Join(",", set);
                names = names ?? joined;
                if (joined != names)
                {
                    missing.Add(langs[i] + ":" + key + " {names}");
                }
            }
        }
        Check(missing.Count == 0, "Standing meetings: every text is in en, pt and zh-cn with the same {names}, no double space, no exclamation mark (" + MeetingWords.Length + " keys"
                                  + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty) + ")");
    }
}
