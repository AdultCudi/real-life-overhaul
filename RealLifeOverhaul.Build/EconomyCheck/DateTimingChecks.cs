using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RealLifeOverhaul;

// Keeping a date (Scripts/Social, OutingScene): a greeting never speaks of time apart the days do not make, the player is never late
// because the other came late or took long to be put out, and a walking companion keeps the player's pace and starts at a sight.
internal static partial class Program
{
    private static void DateTiming()
    {
        DateGreetings();
        DateLateness();
        DatePace();
        DateSights();
    }

    // What each greeting line claims, by the days it needs since the two last met. A line of the mod's with no entry here claims nothing.
    private static readonly Dictionary<string, int> GreetClaims = new Dictionary<string, int>
    {
        { "reallife_social_greet_close_1", 10 },   // "It's been too long."
        { "reallife_social_greet_close_2", 5 },    // "Finally! I've got so much to tell you."
        { "reallife_social_greet_love_3", 1 },     // "I missed you today."
    };

    private static void DateGreetings()
    {
        // Coffee at 17:00 then the cinema at 19:00: the second greeting is the same day as the first outing.
        Check(SocialDateRules.GreetSpanOf(100, 100, 40) == GreetSpan.Same && SocialDateRules.GreetSpanOf(100, 99, 40) == GreetSpan.Days
              && SocialDateRules.GreetSpanOf(100, 96, 40) == GreetSpan.Days && SocialDateRules.GreetSpanOf(100, 95, 40) == GreetSpan.Week
              && SocialDateRules.GreetSpanOf(100, 91, 40) == GreetSpan.Week && SocialDateRules.GreetSpanOf(100, 90, 40) == GreetSpan.Long,
            "dates, greeting: the days since the last outing give Same, Days (1 to 4), Week (5 to 9) and Long (10 or more); a second outing on the day is Same");
        Check(SocialDateRules.GreetSpanOf(100, 0, 85) == GreetSpan.Long && SocialDateRules.GreetSpanOf(100, 0, 99) == GreetSpan.Days && SocialDateRules.GreetSpanOf(100, 98, 60) == GreetSpan.Days
              && SocialDateRules.GreetSpanOf(100, 0, 0) == GreetSpan.Same,
            "dates, greeting: with no outing yet the day they met counts, the later of the two counts, and with neither known nothing is claimed");

        bool fits = true, spans = true;
        string wrong = string.Empty;
        foreach (string[][] pools in new[] { SocialCatalogue.GreetClose, SocialCatalogue.GreetLove })
        {
            spans &= pools.Length == Enum.GetValues(typeof(GreetSpan)).Length;
            for (int s = 0; s < pools.Length && spans; s++)
            {
                spans &= pools[s].Length >= 3;   // (a pool of one would say the same line every time)
                foreach (string key in pools[s])
                {
                    if (GreetClaims.TryGetValue(key, out int needs) && needs > SocialDateRules.SpanDays((GreetSpan)s))
                    {
                        fits = false;
                        wrong += " " + key + " in " + (GreetSpan)s;
                    }
                }
            }
        }
        Check(fits && spans, "dates, greeting: no pool for a span holds a line that claims more days than the span stands for, and each holds three or more lines (" + wrong.Trim() + ")");

        string[][] close = SocialCatalogue.GreetClose, love = SocialCatalogue.GreetLove;
        Check(Array.IndexOf(close[(int)GreetSpan.Same], "reallife_social_greet_close_1") < 0 && Array.IndexOf(close[(int)GreetSpan.Days], "reallife_social_greet_close_1") < 0
              && Array.IndexOf(close[(int)GreetSpan.Week], "reallife_social_greet_close_1") < 0 && Array.IndexOf(close[(int)GreetSpan.Long], "reallife_social_greet_close_1") >= 0
              && Array.IndexOf(close[(int)GreetSpan.Week], "reallife_social_greet_close_2") >= 0 && Array.IndexOf(close[(int)GreetSpan.Days], "reallife_social_greet_close_2") < 0
              && Array.IndexOf(love[(int)GreetSpan.Same], "reallife_social_greet_love_3") < 0 && Array.IndexOf(love[(int)GreetSpan.Days], "reallife_social_greet_love_3") >= 0,
            "dates, greeting: \"it's been too long\" is said only after ten days, \"so much to tell you\" after five and \"I missed you today\" not on the day of the last meeting");

        // Every written greeting of the mod is sorted: a new one has to be put in GreetClaims (or claim nothing by being listed here as it is) and in a pool.
        string en = File.ReadAllText(System.IO.Path.Combine(DateLocales(), "en.json")).TrimStart('﻿');
        Dictionary<string, string> words = JsonSerializer.Deserialize<Dictionary<string, string>>(en);
        var inPools = new HashSet<string>(StringComparer.Ordinal);
        foreach (string[] pool in new[] { SocialCatalogue.GreetAcquaintance, SocialCatalogue.GreetFriend, SocialCatalogue.GreetCloseLines, SocialCatalogue.GreetLoveLines })
        {
            inPools.UnionWith(pool);
        }
        bool sorted = true;
        string loose = string.Empty;
        foreach (KeyValuePair<string, string> pair in words)
        {
            bool greeting = pair.Key.StartsWith("reallife_social_greet_", StringComparison.Ordinal) && !pair.Key.StartsWith("reallife_social_greet_late", StringComparison.Ordinal);
            if (greeting && !inPools.Contains(pair.Key))
            {
                sorted = false;
                loose += " " + pair.Key;
            }
        }
        // The words themselves: none of the lines that fit any gap speaks of how long it has been.
        bool plain = true;
        foreach (string key in inPools)
        {
            if (GreetClaims.ContainsKey(key) || !words.TryGetValue(key, out string text))
            {
                continue;
            }
            string lower = text.ToLowerInvariant();
            foreach (string claim in new[] { "too long", "been ages", "so long", "missed you", "finally", "so much to tell" })
            {
                if (lower.Contains(claim))
                {
                    plain = false;
                    loose += " " + key + ":" + claim;
                }
            }
        }
        Check(sorted && plain, "dates, greeting: every greeting line sits in a pool, and a line that speaks of time apart is one of those that need days (" + loose.Trim() + ")");

        string meeting = ModSource(@"Social\OutingScene.Meeting.cs");
        string line = MemberText(meeting, "private string GreetingLine()");
        Check(line.Contains("SocialDateRules.GreetSpanOf(SocialLife.Day, _who.LastOutingDay, _who.MetDay)") && line.Contains("SocialCatalogue.GreetLove[(int)span]")
              && line.Contains("SocialCatalogue.GreetClose[(int)span]"),
            "dates, greeting: the line is picked from the pool of the days since the last outing");
    }

    private static string DateLocales()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
    }

    private static void DateLateness()
    {
        const float agreed = 17f * 60f;
        int late = SocialRules.LateMinutes;
        // The fault: the player stood at the place at the agreed time, the other came 35 minutes later by their own time, and the hello was
        // said then. Judged at the hello (the old rule) that is 36 minutes late; judged on the player it is none.
        float hello = agreed + 36f;
        Check(hello - agreed > late && SocialDateRules.LateBy(hello, agreed, agreed) <= 0f,
            "dates, late: the player at the place on the minute and the other there 35 minutes on is " + SocialDateRules.LateBy(hello, agreed, agreed) + " late, not 36");

        bool never = true;
        for (int delay = 0; delay <= 120; delay++)
        {
            for (int early = 0; early <= 15; early += 5)
            {
                never &= SocialDateRules.LateBy(agreed + delay, agreed, agreed - early) <= 0f;   // however long the other (or their putting out) took
            }
        }
        Check(never, "dates, late: a player at the place by the agreed time is never late, whenever the other turns up or the hello is said");

        Check(SocialDateRules.LateBy(agreed + 40f, agreed, agreed + 25f) > late && SocialDateRules.LateBy(agreed + 40f, agreed, agreed + 20f) <= late
              && SocialDateRules.LateBy(agreed + 10f, agreed, -1f) == 10f && SocialDateRules.LateBy(agreed - 4f, agreed, -1f) < 0f,
            "dates, late: a player who got there 25 minutes late is late, 20 is not yet, and one never noted at the place is judged at the hello as before");

        Check(SocialDateRules.KeptWaiting(agreed - 5f, agreed + 30f) && !SocialDateRules.KeptWaiting(agreed - 5f, agreed + 2f) && !SocialDateRules.KeptWaiting(-1f, agreed + 30f)
              && !SocialDateRules.KeptWaiting(agreed - 5f, -1f) && !SocialDateRules.KeptWaiting(agreed + 30f, agreed - 5f),
            "dates, late: the other says sorry for keeping the player waiting only when the player was there and they came more than 8 minutes after");

        Check(!SocialDateRules.StoodUpByPlayer(agreed, agreed, -1f) && !SocialDateRules.StoodUpByPlayer(agreed, agreed + 60f, -1f)
              && SocialDateRules.StoodUpByPlayer(agreed, -1f, -1f) && SocialDateRules.StoodUpByPlayer(agreed, agreed + SocialRules.StoodUpMinutes + 1f, -1f)
              && SocialDateRules.StoodUpByPlayer(agreed, agreed, agreed + 10f),
            "dates, late: nobody is stood up when the player was at the place in time and the other never came; the player is when they never came, or came after the window");

        string meeting = ModSource(@"Social\OutingScene.Meeting.cs");
        string arrived = MemberText(meeting, "private void Arrived()");
        string gone = MemberText(meeting, "private bool PlanGone(float now, float nowMinutes)");
        string nudge = MemberText(meeting, "private void Nudge(float nowMinutes)");
        string travel = MemberText(meeting, "private void Travel(float now)");
        Check(arrived.Contains("_lateBy = SocialDateRules.LateBy(SocialLife.Now, _time, _playerAt);") && !meeting.Contains("SocialLife.Now - _time")
              && gone.Contains("SocialDateRules.StoodUpByPlayer(_time, _playerAt, _partnerAt)") && nudge.Contains("_playerAt >= 0f")
              && travel.Contains("NotePlayer(nowMinutes,") && Occurs(meeting, "PartnerPlaced();") == 2 && meeting.Contains("if (_playerAt < 0f && nearest < WaveWithin)"),
            "dates, late: the scene notes the minute the player stood at the place, judges lateness and the stood-up on it, and notes when the other was put out");
    }

    private static void DatePace()
    {
        const float walk = 1.2f, jog = 4f, run = 6f;   // the game's GlobalReferences figures
        Check(Math.Abs(SocialDateRules.PartnerPace(jog, 1f, walk, jog, run) - jog) < 0.001f && Math.Abs(SocialDateRules.PartnerPace(run, 1f, walk, jog, run) - run) < 0.001f
              && Math.Abs(SocialDateRules.PartnerPace(walk, 1f, walk, jog, run) - walk) < 0.001f && SocialDateRules.PartnerPace(jog, 1f, walk, jog, run) > walk * 3f - 0.001f,
            "dates, pace: a companion near the player goes at the player's own speed (jog 4, run 6, the walk key 1.2), not the game's walk that was a third of it");
        Check(Math.Abs(SocialDateRules.PartnerPace(jog, 5f, walk, jog, run) - jog * SocialDateRules.CatchUpFactor) < 0.001f && SocialDateRules.PartnerPace(jog, 7f, walk, jog, run) == run
              && SocialDateRules.PartnerPace(run, 5f, walk, jog, run) == run,
            "dates, pace: behind the player they hurry a little, far behind they go at the game's run, and never past it");
        Check(SocialDateRules.PartnerPace(0f, 1f, walk, jog, run) == jog && SocialDateRules.PartnerPace(40f, 1f, walk, jog, run) == run && SocialDateRules.PartnerPace(0.3f, 1f, walk, jog, run) == walk,
            "dates, pace: an unknown speed is the jog, a figure outside the game's walk and run is kept to them");
        bool keeps = true;
        for (float player = walk; player <= run; player += 0.1f)
        {
            for (float gap = 0f; gap <= 12f; gap += 0.5f)
            {
                float pace = SocialDateRules.PartnerPace(player, gap, walk, jog, run);
                keeps &= pace >= player - 0.0001f && pace <= run + 0.0001f;
            }
        }
        Check(keeps, "dates, pace: at every pace of the player and every gap the companion is at least as fast as the player and no faster than the game's run");

        string walkCode = ModSource(@"Social\OutingScene.Walk.cs");
        string follow = MemberText(walkCode, "private void Follow(float now, Vector3 player, float gap)");
        string person = ModSource(@"CityLife\CityPerson.cs");
        string walkTo = MemberText(person, "public bool WalkTo(Vector3 destination, bool jog = false, float pace = 0f)");
        string figures = MemberText(person, "public static void PaceFigures(out float walk, out float jog, out float run)");
        Check(follow.Contains("SocialDateRules.PartnerPace(D.PlayerPace, gap, walk, jog, run)") && follow.Contains("_friend.WalkTo(goal, false, ") && !follow.Contains("gap > 7f")
              && walkTo.Contains("if (pace > 0f)") && walkTo.Contains("agent.speed = pace;") && figures.Contains("walkingSpeedWalk") && figures.Contains("walkingSpeedJog") && figures.Contains("walkingSpeedRun")
              && ModSource(@"Social\SocialDirector.Scene.cs").Contains("PlayerPace = player.Character != null && player.Character.navmeshAgent != null ? player.Character.navmeshAgent.speed : 0f;"),
            "dates, pace: a walk's companion is sent at the pace of the speed set on the player's agent, with the game's own figures from GlobalReferences");
    }

    private static void DateSights()
    {
        Check(!SocialDateRules.PleasantSight("hospital") && !SocialDateRules.PleasantSight(null) && !SocialDateRules.PleasantSight(string.Empty)
              && SocialDateRules.PleasantSight("times_square") && SocialDateRules.PleasantSight("st_patrick") && SocialDateRules.PleasantSight("madison_park") && SocialDateRules.PleasantSight("sunset_park"),
            "dates, walk: Leisure's parks, squares and waterfronts are places to walk, the hospital is not");
        float reach = 45f + SocialDateRules.SightSlack;
        Check(SocialDateRules.NearSight(0f, 0f, 45f) && SocialDateRules.NearSight(reach, 0f, 45f) && !SocialDateRules.NearSight(reach + 0.1f, 0f, 45f)
              && SocialDateRules.NearSight(reach * 0.7f, -reach * 0.7f, 45f) && !SocialDateRules.NearSight(reach * 0.75f, reach * 0.75f, 45f),
            "dates, walk: a place counts as at a sight within its radius and " + SocialDateRules.SightSlack + " m on the flat");

        string places = ModSource(@"Social\SocialPlaces.cs");
        string find = MemberText(places, "public static bool Find(PlanKind kind, Vector3 around, float minDistance, float maxDistance, out Venue venue, CityBuildingController not = null, float openAt = -1f)");
        string search = MemberText(places, "private static bool Search(PlanKind kind, Vector3 around, float minDistance, float maxDistance, out Venue venue, CityBuildingController not, float openAt, bool atSight)");
        int first = find.IndexOf("openAt, true)", StringComparison.Ordinal), second = find.IndexOf("openAt, false)", StringComparison.Ordinal);
        Check(find.Contains("kind == PlanKind.Walk") && find.Contains("SocialLinks.SightCount() > 0") && first > 0 && second > first && search.Contains("(atSight && !SocialLinks.NearSight(door.position))"),
            "dates, walk: a walk's place is looked for near a sight first and anywhere when none is known or none is near, and only a walk is");
        string links = ModSource(@"Wiring\LifeLinks.cs");
        Check(links.Contains("SocialLinks.SightCount = SightCount;") && links.Contains("SocialLinks.NearSight = NearSight;") && links.Contains("SocialLinks.SightCount = null;")
              && links.Contains("SocialLinks.NearSight = null;") && links.Contains("Leisure.LeisureVenues.Landmarks") && links.Contains("SocialDateRules.PleasantSight(sight.Id)"),
            "dates, walk: Leisure's sights reach Social through the wiring only, and let go at disconnect");
    }
}
