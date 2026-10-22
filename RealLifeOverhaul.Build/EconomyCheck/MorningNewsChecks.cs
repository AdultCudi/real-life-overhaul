using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealLifeOverhaul;

// A player's morning line came at 00:40 and 23:33, each time "You slept 23.8 hours", in the same minute as the weather, a
// city policy ended and the works next door finished (#dev-chat, 19 Oct 2026). The line comes only from the game's own sleep
// (or a hotel night), seen to begin and ended by the game: the bed's slider runs to 24 hours, fixed when Start is pressed,
// so a full slider less a twelve-minute walk to the bed is 23.8 hours. Its hours are now counted from the last look before
// the player lay down to the minute the game ended the sleep (its energy event from SleepActivity.Finish), not as the sum of
// one-second looks, which lost the last look's minutes when a time skip was cancelled at speed. The pile was the city's parts
// on game time (Civic, Events, the Ledger's round) catching up at once when the game's time skip, which pauses the game, ends:
// their notices are now held while the player sleeps and go out with the morning line, packed into notifications of at most
// 240 characters (Core/MorningNews, Core/MorningNewsRules, Home/WakeRules). Four notifications have switches of their own:
// the city's requests, protests, contract offers (own work and the tech firms') and a better credit rating.
// First the wake rule, on a model of the game's sleep, the old count against the new; then the packing, with the player's
// own texts in every language; the moment after the wake, the duplicate and the colour; the sources; the switches; the cost;
// the texts.
internal static partial class Program
{
    private static void MorningNotices()
    {
        WakeHoursChecks();
        WakeDueChecks();
        WakeGreetChecks();
        WakeModelChecks();
        NewsPackChecks();
        NewsPackLanguages();
        NewsMomentChecks();
        NewsSources();
        NewsSwitches();
        NewsPace();
        NewsWords();
    }

    private static float Clock(int day, int hour, int minute) => (day * 24 + hour) * 60 + minute;   // as the game's Timestamp.GetTotalMinutes

    // ---------------------------------------------------------------- the wake rule

    private static void WakeHoursChecks()
    {
        Check(Math.Abs(WakeRules.Hours(Clock(10, 23, 0), Clock(11, 7, 0)) - 8f) < 0.001f, "wake: from 23:00 to 07:00 is 8 hours");
        Check(WakeRules.Hours(-1f, Clock(11, 7, 0)) == 0f, "wake: no lying down seen is no sleep at all (0 hours)");
        Check(WakeRules.Hours(Clock(11, 7, 0), Clock(11, 7, 0)) == 0f && WakeRules.Hours(Clock(11, 8, 0), Clock(11, 7, 0)) == 0f,
            "wake: a wake at or before the lying down is 0 hours, never negative");
        float player = WakeRules.Hours(Clock(185, 0, 52), Clock(186, 0, 40));
        Check(player.ToString("F1", CultureInfo.InvariantCulture) == "23.8",
            "wake: the player's first case, lying down at 00:52 and the game's 24-hour sleep ending at 00:40, reads 23.8 hours (" + player + ")");
        float second = WakeRules.Hours(Clock(192, 23, 45), Clock(193, 23, 33));
        Check(second.ToString("F1", CultureInfo.InvariantCulture) == "23.8", "wake: the second, 23:45 to 23:33 the next day, reads 23.8 too (" + second + ")");
        Check(Math.Abs(WakeRules.Hours(Clock(500, 0, 5), Clock(501, 0, 5)) - 24f) < 0.001f, "wake: the slider's whole 24 hours, late in a long game, reads 24");
        Check(WakeRules.RoseAt(Clock(11, 7, 0), Clock(10, 23, 0), Clock(11, 7, 1)) == Clock(11, 7, 0),
            "wake: the minute the game ended the sleep is the wake, not the look that found the player up");
        Check(WakeRules.RoseAt(-1f, Clock(10, 23, 0), Clock(11, 7, 1)) == Clock(11, 7, 1),
            "wake: with no end from the game (a hotel night) the wake is the look that finds the player up");
        Check(WakeRules.RoseAt(Clock(10, 20, 0), Clock(10, 23, 0), Clock(11, 7, 1)) == Clock(11, 7, 1),
            "wake: an end from before this lying down (an earlier sleep's) is not this sleep's");
        Check(WakeRules.RoseAt(Clock(11, 9, 0), Clock(10, 23, 0), Clock(11, 7, 1)) == Clock(11, 7, 1), "wake: an end after now is not taken");
    }

    private static void WakeDueChecks()
    {
        float laid = Clock(10, 23, 0);
        Check(WakeRules.Due(true, laid, true, 8f, 11, 10), "wake due: a night of 8 hours the game ended, not yet told today: the line goes out");
        Check(!WakeRules.Due(false, laid, true, 8f, 11, 10), "wake due: the morning summary switched off: no line");
        Check(!WakeRules.Due(true, -1f, true, 8f, 11, 10),
            "wake due: no lying down seen (the day rolling over, a load, another time skip): no line");
        Check(!WakeRules.Due(true, laid, false, 8f, 11, 10), "wake due: a sleep the game never ended: no line");
        Check(!WakeRules.Due(true, laid, true, WakeRules.NightAtLeast - 0.01f, 11, 10) && WakeRules.Due(true, laid, true, WakeRules.NightAtLeast, 11, 10),
            "wake due: a nap under " + WakeRules.NightAtLeast + " hours says nothing; a sleep of exactly that does");
        Check(!WakeRules.Due(true, laid, true, 8f, 11, 11), "wake due: told already today: no second line");
        Check(WakeRules.Ended(true, -1f, laid), "wake ended: a hotel night is ended by its room");
        Check(!WakeRules.Ended(false, -1f, laid), "wake ended: no end from the game: not ended");
        Check(!WakeRules.Ended(false, laid - 30f, laid), "wake ended: an end from before the lying down is not this sleep's");
        Check(WakeRules.Ended(false, laid + 480f, laid) && WakeRules.Ended(false, laid, laid), "wake ended: an end at or after the lying down ends it");
        Check(!WakeRules.Ended(false, laid, -1f), "wake ended: no lying down seen: nothing to end");
        Check(WakeRules.Due(true, laid, true, 23.8f, 186, 185) && WakeRules.Due(true, Clock(192, 23, 45), true, 23.8f, 193, 192),
            "wake due: the player's two 24-hour sleeps each bring their line, a day apart");
    }

    private static void WakeGreetChecks()
    {
        int greeted = 0;
        for (int hour = 0; hour < 24; hour++)
        {
            greeted += WakeRules.Greets(hour) ? 1 : 0;
        }
        Check(greeted == WakeRules.MorningTo - WakeRules.MorningFrom && WakeRules.Greets(4) && WakeRules.Greets(7) && WakeRules.Greets(11),
            "wake greeting: good morning from 04:00 to 11:59, " + greeted + " hours of the day");
        Check(!WakeRules.Greets(0) && !WakeRules.Greets(23) && !WakeRules.Greets(3) && !WakeRules.Greets(12) && !WakeRules.Greets(18),
            "wake greeting: none at 00:40 or 23:33 (the player's wakes), at 03:00, noon or the evening");
    }

    // The game's sleep as the mod looks at it, once a second: clocks[i] is the game's clock at look i and asleep[i] whether the
    // panel showed the sleep under way. The old count added each look's minutes while asleep and dropped the look that found
    // the player up; the new is the span from the look before the first asleep one to the game's own end.
    private static float OldCount(float[] clocks, bool[] asleep)
    {
        float slept = 0f;
        for (int i = 1; i < clocks.Length; i++)
        {
            if (asleep[i])
            {
                slept += clocks[i] - clocks[i - 1];
            }
            else if (asleep[i - 1])
            {
                return slept / 60f;
            }
        }
        return slept / 60f;
    }

    private static float NewCount(float[] clocks, bool[] asleep, float endedAt)
    {
        float laid = -1f;
        for (int i = 1; i < clocks.Length; i++)
        {
            if (asleep[i] && !asleep[i - 1])
            {
                laid = clocks[i - 1];
            }
            else if (!asleep[i] && asleep[i - 1])
            {
                return WakeRules.Hours(laid, WakeRules.RoseAt(endedAt, laid, clocks[i]));
            }
        }
        return 0f;
    }

    private static void WakeModelChecks()
    {
        // A time skip cancelled at full speed: lying down at 1001.5, looks at 1005 .. 1600, the cancel at 1850, the next look at 1851.
        float[] cancel = { 1000f, 1005f, 1100f, 1300f, 1600f, 1851f };
        bool[] cancelAsleep = { false, true, true, true, true, false };
        float truth = (1850f - 1001.5f) / 60f;
        float old = OldCount(cancel, cancelAsleep), now = NewCount(cancel, cancelAsleep, 1850f);
        Check(truth - old > 4f, "wake model: the old count drops the cancelled skip's last second of game time, " + old.ToString("F1", CultureInfo.InvariantCulture)
                                + " hours against " + truth.ToString("F1", CultureInfo.InvariantCulture));
        Check(Math.Abs(now - truth) <= 2f / 60f, "wake model: the new count is the sleep to within the look before it ("
                                                  + now.ToString("F2", CultureInfo.InvariantCulture) + " against " + truth.ToString("F2", CultureInfo.InvariantCulture) + ")");
        // A sleep that runs to its goal: the game holds the clock at the goal for a second before it ends the sleep.
        float[] full = { 2000f, 2004f, 2200f, 2900f, 3420f, 3428f, 3428f, 3429f };
        bool[] fullAsleep = { false, true, true, true, true, true, true, false };
        Check(Math.Abs(NewCount(full, fullAsleep, 3428f) - (3428f - 2000f) / 60f) < 0.001f && Math.Abs(OldCount(full, fullAsleep) - (3428f - 2000f) / 60f) < 0.001f,
            "wake model: a sleep run to its goal counts the same both ways, the game's pause at its end kept by the old count too");
        // The player's night: a 24-hour slider pressed at 00:40, a twelve-minute walk, the goal 00:40 the next day.
        float pressed = Clock(185, 0, 40);
        float[] night = { pressed + 11f, pressed + 13f, pressed + 300f, pressed + 1200f, pressed + 1440f, pressed + 1440f, pressed + 1441f };
        bool[] nightAsleep = { false, true, true, true, true, true, false };
        float hours = NewCount(night, nightAsleep, pressed + 1440f);
        Check(hours.ToString("F1", CultureInfo.InvariantCulture) == "23.8", "wake model: the player's night reads 23.8 hours, the slider's 24 less the walk ("
                                                                              + hours.ToString("F2", CultureInfo.InvariantCulture) + ")");
        // Things that are not a sleep: no look ever finds the panel's sleep under way.
        float[] skip = { 5000f, 5600f, 6200f, 6201f };
        bool[] awake = { false, false, false, false };
        Check(NewCount(skip, awake, -1f) == 0f && OldCount(skip, awake) == 0f, "wake model: a time skip that is not a sleep (custody, a ride, an outing, another mod's) counts no sleep");
        Check(NewCount(night, nightAsleep, pressed + 1440f) == NewCount(night, nightAsleep, pressed + 1440f), "wake model: the same looks give the same hours");
    }

    // ---------------------------------------------------------------- the packing

    private static List<NewsPart> PackOf(int morning, int headExtra, int moreExtra, params int[] lengths)
    {
        var parts = new List<NewsPart>();
        MorningNewsRules.Pack(morning, headExtra, moreExtra, lengths, lengths.Length, parts);
        return parts;
    }

    // Every notice once, in order, in parts that each fit; true when so.
    private static bool PacksWhole(List<NewsPart> parts, int morning, int headExtra, int moreExtra, int[] lengths)
    {
        bool ok = parts.Count > 0 && parts[0].Head && parts[0].From == 0;
        int next = 0;
        for (int p = 0; p < parts.Count; p++)
        {
            NewsPart part = parts[p];
            ok &= part.From == next && (p == 0) == part.Head;
            ok &= part.Alone ? part.Count == 1 : MorningNewsRules.Length(part, morning, headExtra, moreExtra, lengths) <= MorningNewsRules.Most || (part.Head && part.Count == 0);
            ok &= part.Count >= 1 || part.Head;
            next += part.Count;
        }
        return ok && next == lengths.Length;
    }

    // The fewest notifications in-order packing can have (by trying every cut), for the greedy fill to be measured against.
    private static int FewestParts(int morning, int headExtra, int moreExtra, int[] lengths)
    {
        int n = lengths.Length;
        var best = new int[n + 1];
        for (int i = 0; i <= n; i++)
        {
            best[i] = int.MaxValue / 2;
        }
        for (int head = 0; head <= n; head++)
        {
            int used = morning + headExtra;
            bool fits = true;
            for (int i = 0; i < head; i++)
            {
                used += lengths[i] + (i > 0 ? MorningNewsRules.Gap : 0);
                fits &= used <= MorningNewsRules.Most;
            }
            if (fits)
            {
                best[head] = Math.Min(best[head], 1);
            }
        }
        for (int from = 0; from < n; from++)
        {
            if (best[from] >= int.MaxValue / 2)
            {
                continue;
            }
            if (moreExtra + lengths[from] > MorningNewsRules.Most)
            {
                best[from + 1] = Math.Min(best[from + 1], best[from] + 1);   // goes out as it came
                continue;
            }
            int used = moreExtra;
            for (int to = from; to < n; to++)
            {
                used += lengths[to] + (to > from ? MorningNewsRules.Gap : 0);
                if (used > MorningNewsRules.Most)
                {
                    break;
                }
                best[to + 1] = Math.Min(best[to + 1], best[from] + 1);
            }
        }
        return best[n];
    }

    private static void NewsPackChecks()
    {
        Check(MorningNewsRules.Extra("{morning} While you slept: {news}", "{morning}", "{news}") == 18 && MorningNewsRules.Extra("Also while you slept: {news}", "{news}", null) == 22,
            "morning news: the words around the news are counted without their names in curly brackets (18 and 22)");
        Check(MorningNewsRules.Extra(null, "{a}", "{b}") == 0 && MorningNewsRules.Extra("plain", "{a}", "{b}") == 5 && MorningNewsRules.Extra("{a}", "{a}", "{a}") == 0,
            "morning news: no template adds nothing, one without names adds itself, a name is taken once and never below nothing");
        List<NewsPart> none = PackOf(115, 18, 22);
        Check(none.Count == 1 && none[0].Head && none[0].Count == 0, "morning news: nothing held is the morning line alone");
        List<NewsPart> one = PackOf(115, 18, 22, 76);
        Check(one.Count == 1 && one[0].Head && one[0].Count == 1 && MorningNewsRules.Length(one[0], 115, 18, 22, new[] { 76 }) == 209,
            "morning news: the storm warning fits with the morning line: one notification of 209 characters");
        int[] player = { 76, 100, 82 };
        List<NewsPart> two = PackOf(115, 18, 22, player);
        Check(two.Count == 2 && two[0].Count == 1 && two[1].From == 1 && two[1].Count == 2 && PacksWhole(two, 115, 18, 22, player),
            "morning news: the player's morning (the line, a storm, a policy ended, the works next door) is two notifications, the storm with the line");
        List<NewsPart> later = PackOf(200, 18, 22, 60);
        Check(later.Count == 2 && later[0].Count == 0 && later[1].Count == 1 && !later[1].Alone,
            "morning news: news too long for the line's notification goes into the next, and the line stands alone");
        List<NewsPart> alone = PackOf(115, 18, 22, 230, 40);
        Check(alone.Count == 3 && alone[1].Alone && alone[1].From == 0 && alone[2].From == 1 && !alone[2].Alone && PacksWhole(alone, 115, 18, 22, new[] { 230, 40 }),
            "morning news: a notice too long to share one goes out as it came, and what follows packs on");
        Check(PackOf(115, 18, 22, 107)[0].Count == 1 && PackOf(115, 18, 22, 108)[0].Count == 0,
            "morning news: the edge: a notice that makes the first exactly 240 characters is taken, one more character is not");
        var many = new int[MorningNewsRules.MostHeld];
        for (int i = 0; i < many.Length; i++)
        {
            many[i] = 100;
        }
        List<NewsPart> full = PackOf(115, 18, 22, many);
        Check(PacksWhole(full, 115, 18, 22, many) && full.Count == FewestParts(115, 18, 22, many),
            "morning news: the most held (" + MorningNewsRules.MostHeld + " notices of 100 characters) packs whole, in order, in the fewest notifications (" + full.Count + ")");
        var random = new Random(4242);
        bool whole = true, fewest = true;
        for (int round = 0; round < 3000; round++)
        {
            var lengths = new int[random.Next(0, MorningNewsRules.MostHeld + 1)];
            for (int i = 0; i < lengths.Length; i++)
            {
                lengths[i] = random.Next(20, 245);
            }
            int morning = random.Next(40, 200);
            List<NewsPart> parts = PackOf(morning, 18, 22, lengths);
            whole &= PacksWhole(parts, morning, 18, 22, lengths);
            fewest &= parts.Count == FewestParts(morning, 18, 22, lengths);
        }
        Check(whole, "morning news: 3,000 mornings of up to a dozen notices each pack whole, in order, each part within " + MorningNewsRules.Most + " characters");
        Check(fewest, "morning news: and the greedy fill is never more notifications than the fewest possible");
        List<NewsPart> again = PackOf(115, 18, 22, player);
        Check(again.Count == two.Count && again[1].From == two[1].From && again[1].Count == two[1].Count, "morning news: the same notices pack the same way every time");
        var listed = new List<int> { 76, 100, 82 };
        var parts2 = new List<NewsPart>();
        MorningNewsRules.Pack(115, 18, 22, listed, 7, parts2);
        Check(parts2.Count == 2 && PacksWhole(parts2, 115, 18, 22, player), "morning news: a count past the list is the list");
    }

    // The player's own morning, in every language: the line, the storm, the policy that ended and the works next door, each
    // as that language words it; every part within the limit, every notice whole, never more than three notifications.
    private static void NewsPackLanguages()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("morning news", langs, codes))
        {
            return;
        }
        bool fits = true;
        string counts = string.Empty;
        for (int l = 0; l < langs.Length; l++)
        {
            Dictionary<string, string> w = langs[l];
            string morning = w["reallife_home_morning"].Replace("{slept}", "23.8").Replace("{quality}", w["reallife_health_word_groggy"]).Replace("{tip}", w["reallife_health_tip_eat"]);
            string[] news =
            {
                w["reallife_weather_warning_storm"],
                w["reallife_civic_policy_ended"].Replace("{policy}", "Dearer parking, stiffer tickets"),
                w["reallife_civic_site_done_near"].Replace("{project}", "a new pavement").Replace("{address}", "12 4th Avenue")
            };
            var lengths = new int[news.Length];
            for (int i = 0; i < news.Length; i++)
            {
                lengths[i] = news[i].Length;
            }
            int head = MorningNewsRules.Extra(w["reallife_home_morning_news"], "{morning}", "{news}");
            int more = MorningNewsRules.Extra(w["reallife_home_morning_more"], "{news}", null);
            List<NewsPart> parts = PackOf(morning.Length, head, more, lengths);
            fits &= PacksWhole(parts, morning.Length, head, more, lengths) && parts.Count <= 3;
            counts += (l > 0 ? ", " : string.Empty) + codes[l] + " " + parts.Count;
        }
        Check(fits, "morning news: the player's morning packs whole in every language, within " + MorningNewsRules.Most + " characters a notification (" + counts + ")");
    }

    private static void NewsMomentChecks()
    {
        Check(!MorningNewsRules.CaughtUp(1.49f, 0f, 2f, 0f) && MorningNewsRules.CaughtUp(1.5f, 0f, 2f, 0f),
            "morning news: the line waits until " + MorningNewsRules.CatchUpGame + " seconds of game time after the wake, when every part on game time has looked again");
        Check(!MorningNewsRules.CaughtUp(0f, 0f, 5.9f, 0f) && MorningNewsRules.CaughtUp(0f, 0f, 6f, 0f),
            "morning news: a game paused at once keeps the line waiting no more than " + MorningNewsRules.CatchUpReal + " seconds of real time");
        var ids = new List<string> { null, "rlo-weather", "other", "rlo-weather" };
        Check(MorningNewsRules.Same(ids, ids.Count, "rlo-weather") == 1 && MorningNewsRules.Same(ids, ids.Count, null) == -1 && MorningNewsRules.Same(ids, ids.Count, "none") == -1
              && MorningNewsRules.Same(ids, 1, "rlo-weather") == -1 && MorningNewsRules.Same(null, 3, "x") == -1,
            "morning news: a newer weather notice takes the held one's place (the game's duplicate id); none without an id, or beyond the count");
        var weights = new List<int> { 1, 2, 0, 3 };
        Check(MorningNewsRules.Strongest(weights, 0, 2, -1) == 2 && MorningNewsRules.Strongest(weights, 2, 2, 0) == 3 && MorningNewsRules.Strongest(weights, 0, 0, -1) == -1
              && MorningNewsRules.Strongest(weights, 3, 5, 0) == 3 && MorningNewsRules.Strongest(weights, 2, 1, 1) == 1,
            "morning news: a part takes the strongest kind of its notices, within the list, and never less than it starts from");
        Check(MorningNewsRules.HeadWeight(0, 1) == 0 && MorningNewsRules.HeadWeight(1, 0) == 1 && MorningNewsRules.HeadWeight(0, 2) == 2 && MorningNewsRules.HeadWeight(1, 3) == 3
              && MorningNewsRules.HeadWeight(3, 2) == 3 && MorningNewsRules.HeadWeight(2, -1) == 2,
            "morning news: the line keeps the colour of the night's sleep unless the news it carries holds a warning or an error");
    }

    // ---------------------------------------------------------------- the sources

    private static void NewsSources()
    {
        string director = ModSource(@"Home\HomeDirector.cs");
        string sleep = MemberText(director, "private void Sleep(Doing doing, float minutesNow, float minutesBefore, bool atHome, int awayBed)");
        Check(CodeAt(sleep, "_laidAt = minutesBefore;") > 0 && CodeAt(sleep, "MorningNews.Hold();") > CodeAt(sleep, "_laidAt = minutesBefore;")
              && CodeAt(sleep, "WakeRules.Ended(_awayNight, _endedAt, laidAt)") > 0 && CodeAt(sleep, "WakeRules.RoseAt(_endedAt, laidAt, minutesNow)") > 0
              && CodeAt(sleep, "WakeRules.Hours(laidAt, roseAt)") > 0 && CodeAt(sleep, "WakeRules.Due(Settings.HomeMorning, laidAt, ended, hours, day, HomeState.MorningDay)") > 0
              && CodeAt(sleep, "_slept") < 0,
            "wake, as code: the sleep is counted from the look before it to the game's end by Home/WakeRules, and the line is due only by its rule");
        Check(CodeAt(sleep, "MorningNews.Release();") > 0 && CodeAt(sleep, "_wokeReal = Time.unscaledTime;") > CodeAt(sleep, "HomeState.MorningDay = day;"),
            "wake, as code: a wake with no line lets the held news go; one with a line waits for the city's news after telling the day");
        string ended = MemberText(director, "private void SleepEnded()");
        Check(CodeAt(ended, "GetCurrentActivity is SleepActivity") > 0 && CodeAt(ended, "_endedAt = TimeHelper.NowInMinutes();") > 0 && CodeAt(ended, "_laidAt < 0f") > 0
              && director.Contains("private const string EnergyIncreased = \"ba:gameevent_playerenergyincreased\";")
              && CodeAt(MemberText(director, "private void OnGameEvent(string gameEvent)"), "SleepEnded();") > 0,
            "wake, as code: the game's own end of a sleep (its energy event while the sleep is the panel's activity, not a rest's) is the wake's minute");
        string line = MemberText(director, "private void MorningLine(float now)");
        Check(CodeAt(line, "MorningNewsRules.CaughtUp(Time.time, _wokeGame, now, _wokeReal)") > 0 && CodeAt(line, "HomeReport.Morning(_wokeHours, _wokeHour);") > 0
              && CodeAt(line, "MorningNews.Holding && !MorningNews.Wanted") > 0,
            "wake, as code: the line goes out once the city's news has caught up, and held news is let go when its option goes off");
        string destroy = MemberText(director, "private void OnDestroy()");
        Check(CodeAt(destroy, "MorningNews.Drop();") > 0 && CodeAt(destroy, "MorningNews.Drop();") < CodeAt(destroy, "TakeBack();")
              && CodeAt(MemberText(director, "private void TakeBack()"), "MorningNews.Release();") > 0,
            "morning news, as code: the city unloading drops what was held unshown; home life switched off shows it as it came");
        string report = ModSource(@"Home\HomeReport.cs");
        string morning = MemberText(report, "public static void Morning(float hours, int hour)");
        Check(CodeAt(morning, "WakeRules.Greets(hour) ? \"reallife_home_morning\" : \"reallife_home_woke\"") > 0 && CodeAt(morning, "MorningNews.Take(News)") > 0
              && CodeAt(morning, "MorningNewsRules.Pack(morning.Length, headExtra, moreExtra, NewsLengths, count, Parts);") > 0
              && CodeAt(morning, "MorningNews.ShowAsCame(News[part.From]);") > 0,
            "morning news, as code: the line greets by the hour, takes what was held, packs it by the rule and shows a notice too long to share as it came");
        int lines = 0;
        foreach (string file in System.IO.Directory.GetFiles(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts")),
                     "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string code = System.IO.File.ReadAllText(file);
            if (CodeAt(code, "\"reallife_home_morning\"") >= 0 || CodeAt(code, "HomeReport.Morning(") >= 0)
            {
                lines += System.IO.Path.GetFileName(file) == "HomeReport.cs" || System.IO.Path.GetFileName(file) == "HomeDirector.cs"
                         || System.IO.Path.GetFileName(file) == "HomeDirector.Tests.cs" ? 0 : 1;
            }
        }
        Check(lines == 0, "wake: nothing outside home life's director, report and test sends the morning line (" + lines + " other files)");

        string hold = ModSource(@"Core\MorningNews.cs");
        string show = MemberText(hold, "public static void Show(NotificationType type, string key, Dictionary<string, string> data, float seconds, string same = null, bool track = true)");
        Check(CodeAt(show, "if (!Holding || !Wanted)") > 0 && CodeAt(show, "MorningNewsRules.Same(HeldSame, HeldSame.Count, same)") > 0
              && CodeAt(show, "Held.Count >= MorningNewsRules.MostHeld") > 0
              && hold.Contains("Settings.HomeLife && Settings.HomeSleep && Settings.HomeMorning && Settings.HomeMorningNews"),
            "morning news, as code: a notice is held only while the player sleeps with the summary and its news on, a newer duplicate in place of the older, a dozen at most");
        Check(!hold.Contains("SharedCity") && !hold.Contains("GoingPublicLink") && !hold.Contains("Random"),
            "morning news, Going Public: only this machine's notifications, nothing sent, nothing rolled");
        string weather = ModSource(@"Events\Weather.cs");
        string announce = MemberText(weather, "private static void Announce()");
        string forecast = MemberText(weather, "private static void Forecast(EventDirector director, int day)");
        Check(CodeAt(announce, "MorningNews.Show(") > 0 && CodeAt(forecast, "MorningNews.Show(") > 0 && CodeAt(weather, "Notifications.Show(") < 0
              && CodeAt(weather, "MorningNews.Show(NotificationType.Warning, Random.value < 0.5f ? \"reallife_weather_power_cut_a\"") > 0,
            "morning news, as code: every notice of the weather's is held while the player sleeps");
        string events = ModSource(@"Events\EventDirector.cs");
        string tell = MemberText(events, "private void Tell(string key, PlannedEvent plan)");
        Check(CodeAt(tell, "MorningNews.Show(NotificationType.Info, key, data, 10f);") > 0 && CodeAt(tell, "Bulletin(key,") > CodeAt(tell, "MorningNews.Show(")
              && CodeAt(tell, "Notifications.Show(") < 0,
            "morning news, as code: what is on today and tomorrow is held while the player sleeps; the City Bulletin's line goes either way");
        string civic = ModSource(@"Civic\CivicText.cs");
        Check(CodeAt(civic, "Notifications.Show(") < 0 && Occurs(civic, "MorningNews.Show(type, key,") == 5 && Occurs(civic, "Heard") >= 4,
            "morning news, as code: every city toast (the four toasts and a guest's copy of the host's) is held while the player sleeps, and still heard by Going Public's guests");
        string ledger = ModSource(@"Ledger\LedgerDirector.Online.cs");
        Check(CodeAt(MemberText(ledger, "private void Told(int day)"), "MorningNews.Show(NotificationType.Info, key,") > 0,
            "morning news, as code: the morning's papers are held while the player sleeps");
        string links = ModSource(@"Wiring\LifeLinks.cs");
        Check(links.Contains("HomeBridges.SleepingAway = () => HotelStay.SleepingBed;"), "wake: a hotel night still counts as sleep, through its bridge");
    }

    // ---------------------------------------------------------------- the switches

    private static void NewsSwitches()
    {
        string settings = ModSource(@"Core\Settings.cs");
        string[,] options =
        {
            { "HomeMorningNews", "home_morning_news" }, { "StandingRequestNotes", "standing_request_notes" }, { "StreetProtestNotes", "street_protest_notes" },
            { "CareersOwnOfferNotes", "careers_own_offer_notes" }, { "TechOfferNotes", "tech_offer_notes" }, { "EconomyGradeUpNotes", "economy_grade_up_notes" }
        };
        bool declared = true;
        string tree = ModSource(@"Meta\OptionsTree.cs"), page = ModSource(@"Core\RealLifeOverhaulMod.cs");
        for (int i = 0; i < options.GetLength(0); i++)
        {
            string field = options[i, 0], id = options[i, 1];
            declared &= settings.Contains("public const string " + field + "Id = \"" + id + "\";") && settings.Contains("public static bool " + field + " = true;")
                        && settings.Contains(field + " = Bool(modId, " + field + "Id, " + field + ");")
                        && page.Contains(".AddToggle(Settings." + field + "Id, ") && tree.Contains("\"" + id + "\"");
        }
        Check(declared, "switches: the six options are declared, on by default, read at load, on the options page and in its tree");
        Check(tree.Contains(".Opt(\"home_morning_news\", \"home_morning\")") && tree.Contains("\"standing_corruption\", \"standing_request_notes\")")
              && tree.Contains(".Opt(\"street_protest_notes\", \"street_events\")") && tree.Contains(".Opt(\"careers_own_offer_notes\", \"careers_own\")")
              && tree.Contains(".Opt(\"tech_offer_notes\", \"tech_sector\")") && tree.Contains(".Opt(\"economy_grade_up_notes\", \"economy\")"),
            "switches: each stands in its feature's section, needing what makes its notices");

        string fame = MemberText(ModSource(@"Fame\FameDirector.City.cs"), "private void Requested(FameRequest r)");
        Check(CodeAt(fame, "if (Settings.StandingRequestNotes)") > 0 && CodeAt(fame, "\"reallife_fame_note_request\"") > CodeAt(fame, "if (Settings.StandingRequestNotes)"),
            "switches: the city's request is told only with its switch on; it is on the Standing page either way (made before it is told)");
        string own = MemberText(ModSource(@"Careers\CareersDirector.Own.cs"), "private void OfferContract(int day, int salt, bool force)");
        Check(CodeAt(own, "w.Contracts.Add(c2);") > 0 && CodeAt(own, "if (Settings.CareersOwnOfferNotes)") > CodeAt(own, "w.Contracts.Add(c2);")
              && CodeAt(own, "\"reallife_own_note_offer\"") > CodeAt(own, "if (Settings.CareersOwnOfferNotes)"),
            "switches: a client's contract is offered on the Own work tab first, and told only with its switch on");
        string tech = MemberText(ModSource(@"Tech\TechDirector.Firms.cs"), "private void Worked(WorkNote note)");
        Check(Occurs(tech, "if (Settings.TechOfferNotes)") == 2 && CodeAt(tech, "\"reallife_tech_note_offer\"") > CodeAt(tech, "if (Settings.TechOfferNotes)")
              && CodeAt(tech, "\"reallife_tech_note_tender_won\"") > 0,
            "switches: the tech firms' offers and tenders are told only with their switch on; a tender won, lost or work done always is");
        string grade = MemberText(ModSource(@"Economy\Banking\EconomyDirector.Day.cs"), "private void OnGrade(CreditGrade was, CreditGrade now)");
        Check(CodeAt(grade, "bool told = !up || Settings.EconomyGradeUpNotes;") > 0 && CodeAt(grade, "if (told)") > 0 && CodeAt(grade, "Badge();") > CodeAt(grade, "if (told)"),
            "switches: a better credit grade may go untold, a worse one is always told, and the app's badge follows either way");
        string calendar = ModSource(@"Events\EventCalendar.cs");
        string director = ModSource(@"Events\EventDirector.cs") + ModSource(@"Events\EventDirector.Tests.cs");
        string scene = ModSource(@"Events\StreetEventScene.cs");
        Check(calendar.Contains("public static bool Quiet(EventKind kind) => kind == EventKind.Protest && !Settings.StreetProtestNotes;")
              && Occurs(director, "EventCalendar.Quiet(") == 3 && Occurs(scene, "if (!EventCalendar.Quiet(_kind))") == 2,
            "switches: a protest's notices (what is on, the staged one, the test's, its start and its end) go untold with their switch off; other events are told");
    }

    // ---------------------------------------------------------------- the cost

    private static void NewsPace()
    {
        var lengths = new List<int>(MorningNewsRules.MostHeld);
        for (int i = 0; i < MorningNewsRules.MostHeld; i++)
        {
            lengths.Add(40 + 17 * i);
        }
        var parts = new List<NewsPart>(8);
        int sum = 0;
        Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < 100000; i++)
        {
            MorningNewsRules.Pack(100 + (i & 63), 18, 22, lengths, lengths.Count, parts);
            sum += parts.Count + (WakeRules.Due(true, i, true, 8f, 2, 1) ? 1 : 0) + (MorningNewsRules.CaughtUp(i & 3, 0f, 1f, 0f) ? 1 : 0);
        }
        watch.Stop();
        string took = watch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);
        Check(sum > 0 && watch.Elapsed.TotalMilliseconds < 400.0,
            "morning news: 100,000 mornings of a dozen notices packed and judged take " + took + " ms (under 400; one a day in play)");
    }

    // ---------------------------------------------------------------- the texts

    private static void NewsWords()
    {
        Check(FixLineEverywhere("waking up no longer brings a pile of notifications at once", "acordar não traz mais uma pilha de notificações de uma vez",
                                "醒来时不再一下子涌来一大堆通知", "Beim Aufwachen kommt kein Stapel Benachrichtigungen mehr auf einmal",
                                "le réveil n'apporte plus une pile de notifications d'un coup", "despertar ya no trae un montón de avisos de golpe",
                                "пробуждение больше не приносит груду уведомлений разом", "wakker worden brengt niet meer een stapel meldingen tegelijk",
                                "uyanmak artık bir anda bir yığın bildirim getirmiyor"),
            "morning news: What's new has the fix's line, with its fix mark, in some row of every language");
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("morning news", langs, codes))
        {
            return;
        }
        string[] keys =
        {
            "reallife_home_woke", "reallife_home_morning_news", "reallife_home_morning_more", "reallife_home_morning_news_option", "reallife_opt_tip_home_morning_news",
            "reallife_fame_request_notes", "reallife_opt_tip_standing_request_notes", "reallife_events_protest_notes", "reallife_opt_tip_street_protest_notes",
            "reallife_own_opt_offer_notes", "reallife_opt_tip_careers_own_offer_notes", "reallife_tech_opt_offer_notes", "reallife_opt_tip_tech_offer_notes",
            "reallife_econ_opt_grade_up_notes", "reallife_opt_tip_economy_grade_up_notes", "reallife_test_home_morning"
        };
        foreach (string key in keys)
        {
            bool all = LanguagesLacking(langs, codes, key).Length == 0;
            for (int l = 0; all && l < langs.Length; l++)
            {
                string text = langs[l][key];
                all &= text.Length > 5 && (l == 0 || text != langs[0][key]) && !text.Contains("!") && !text.Contains("—") && !text.Contains("–") && !text.Contains(" - ");
            }
            Check(all && codes.Length == ModLanguages.All.Length,
                "morning news: " + key + " is in every language, each its own, with the English names in curly brackets, no dash and no exclamation mark");
        }
        Dictionary<string, string> en = langs[0];
        Check(en["reallife_home_morning_news"].Contains("{morning}") && en["reallife_home_morning_news"].Contains("{news}") && en["reallife_home_morning_more"].Contains("{news}")
              && en["reallife_home_woke"].Contains("{slept}") && en["reallife_home_woke"].Contains("{quality}") && en["reallife_home_woke"].Contains("{tip}")
              && !en["reallife_home_woke"].Contains("morning"),
            "morning news: the summary's words carry the line and the news, and the line for a wake at another hour has no morning in it");
        int longest = Math.Max(en["reallife_home_woke"].Length, Math.Max(en["reallife_home_morning_news"].Length, en["reallife_home_morning_more"].Length));
        Check(longest <= MorningNewsRules.Most, "morning news: each of the summary's own words is within " + MorningNewsRules.Most + " characters (" + longest + ")");
        bool guide = true;
        for (int l = 0; l < langs.Length; l++)
        {
            guide &= langs[l]["reallife_guide_phone"].Contains(langs[l]["reallife_home_morning_news_option"]);
        }
        Check(guide, "morning news: the Guide's phone page names the option by its own label in every language");
    }
}
