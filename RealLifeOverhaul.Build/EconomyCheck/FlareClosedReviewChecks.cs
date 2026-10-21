using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Flare;

// Checks for the city's reviews coming of visits (Scripts/Social/Flare/FlareLiveRules.Reviews): a business the game has
// temporarily closed, or closed at the review's hour, is never reviewed; one open a few hours a day only in those hours;
// with every business open the roll picks the one it always picked; the hour's reviews asked about once each, a skipped
// stretch of hours too; a review taken back with what is under it; the game's own closed state read, the effect and the
// owner's notice at the review's hour and not at the day's start, a guest leaving it to the host, the business card
// counting only reviews that went up, the Tests entry, its texts and the What's new line in every language, and the cost.
internal static partial class Program
{
    private const string ClosedReviewMine = "Elm~1";   // the player's business of FlareFakeCity

    private static void FlareClosedReviews()
    {
        FlareClosedReviewDays();
        FlareClosedReviewPick();
        FlareClosedReviewHour();
        FlareClosedReviewSources();
        FlareClosedReviewTexts();
        FlareClosedReviewTiming();
    }

    // Many days of the city made up here, the player's business and the others open at the hours given: the reviews of the
    // player's business, every review, those dated at an hour their business was closed, and every post of the days.
    private static int FlareClosedReviewRun(int mineOpen, int shopsOpen, out int all, out int outside, out int posts)
    {
        int mine = 0;
        all = outside = posts = 0;
        for (int day = 30; day < 90; day++)
        {
            FlareCityFacts facts = FlareFakeCity(day, 2, false);
            for (int i = 0; i < facts.Shops.Count; i++)
            {
                FlareBiz s = facts.Shops[i];
                s.Open = shopsOpen;
                facts.Shops[i] = s;
            }
            FlareBiz m = facts.Mine[0];
            m.Open = mineOpen;
            facts.Mine[0] = m;
            FlareFile f = FlareLiveFile(77);
            posts += FlareLiveRules.CityDay(f, facts, FlareCitizensOf(f, facts), null);
            foreach (FlarePost p in f.Posts)
            {
                if (p.Source != FlareSource.Review)
                {
                    continue;
                }
                bool own = p.Place == ClosedReviewMine;
                all++;
                mine += own ? 1 : 0;
                outside += FlareLiveRules.OpenAt(own ? mineOpen : shopsOpen, p.Hour) ? 0 : 1;
            }
        }
        return mine;
    }

    private static int Hours(int from, int to)
    {
        int mask = 0;
        for (int h = from; h <= to; h++)
        {
            mask |= 1 << (h % 24);
        }
        return mask;
    }

    // ---------------------------------------------------------------- the day's reviews

    private static void FlareClosedReviewDays()
    {
        int all, outside, posts;
        int open = FlareClosedReviewRun(FlareLiveRules.AllHours, FlareLiveRules.AllHours, out all, out outside, out posts);
        Check(open > 10 && all > open && outside == 0, "Flare closed reviews: open all day, the player's business is reviewed as before (" + open + " of " + all + " reviews in sixty days)");
        int closed = FlareClosedReviewRun(0, FlareLiveRules.AllHours, out int allClosed, out int outsideClosed, out posts);
        Check(closed == 0 && allClosed > 0 && outsideClosed == 0,
            "Flare closed reviews: temporarily closed (no open hour), the player's business gets no review in sixty days (" + closed + "), the city's open businesses still do (" + allClosed + ")");
        int few = FlareClosedReviewRun(Hours(12, 14), FlareLiveRules.AllHours, out int allFew, out int outsideFew, out posts);
        Check(few > 0 && outsideFew == 0, "Flare closed reviews: open from 12:00 to 15:00, the player's business is reviewed only for visits in those hours (" + few + " reviews, "
                                          + outsideFew + " outside them)");
        int night = FlareClosedReviewRun(Hours(20, 27), Hours(7, 17), out int allNight, out int outsideNight, out posts);
        Check(night > 0 && outsideNight == 0, "Flare closed reviews: a night business (20:00 to 04:00) among day shops: every review is dated at an hour its own business is open ("
                                              + allNight + " reviews, " + outsideNight + " outside)");
        int none = FlareClosedReviewRun(0, 0, out int allNone, out int outsideNone, out int postsNone);
        Check(none == 0 && allNone == 0 && postsNone > 60 * FlareLiveRules.CityTalkPerDay[2] / 2,
            "Flare closed reviews: with every business closed nobody writes a review, and the city's people still post (" + postsNone + " posts in sixty days)");
    }

    // ---------------------------------------------------------------- the pick among the open

    private static void FlareClosedReviewPick()
    {
        var all = new List<FlareBiz>();
        for (int i = 0; i < 7; i++)
        {
            all.Add(new FlareBiz { Place = "Main~" + i, Open = FlareLiveRules.AllHours });
        }
        bool same = true;
        for (int roll = 0; roll < 1000; roll++)
        {
            same &= FlareLiveRules.OpenPick(all, 9 + roll % 14, roll) == roll % all.Count;
        }
        Check(same, "Flare closed reviews: with every business open, the roll picks the business it always picked (a day's feed unchanged where nothing is closed)");
        var one = new List<FlareBiz>();
        for (int i = 0; i < 6; i++)
        {
            one.Add(new FlareBiz { Place = "Main~" + i, Open = i == 3 ? Hours(9, 17) : i == 5 ? Hours(18, 22) : 0 });
        }
        bool only = true;
        for (int roll = 0; roll < 1000; roll++)
        {
            only &= FlareLiveRules.OpenPick(one, 10, roll) == 3 && FlareLiveRules.OpenPick(one, 20, roll) == 5 && FlareLiveRules.OpenPick(one, 8, roll) == -1;
        }
        Check(only && FlareLiveRules.OpenPick(null, 10, 5) == -1 && FlareLiveRules.OpenPick(new List<FlareBiz>(), 10, 5) == -1,
            "Flare closed reviews: the pick is only ever of a business open at the review's hour, and none when none is (or there are none)");
        Check(FlareLiveRules.OpenAt(FlareLiveRules.AllHours, 0) && FlareLiveRules.OpenAt(FlareLiveRules.AllHours, 23) && !FlareLiveRules.OpenAt(FlareLiveRules.AllHours, 24)
              && !FlareLiveRules.OpenAt(FlareLiveRules.AllHours, -1) && !FlareLiveRules.OpenAt(0, 12) && FlareLiveRules.OpenAt(Hours(20, 27), 2) && !FlareLiveRules.OpenAt(Hours(20, 27), 5),
            "Flare closed reviews: an hour is open by its bit of the day's hours, never outside 0 to 23");
        bool window = true;
        for (int k = 0; k < 400; k++)
        {
            int h = FlareLiveRules.ReviewHour(91, 40 + k % 30, k);
            window &= h >= FlareLiveRules.ReviewFrom && h <= FlareLiveRules.ReviewTo;
        }
        Check(window, "Flare closed reviews: a review's visit is between " + FlareLiveRules.ReviewFrom + ":00 and " + FlareLiveRules.ReviewTo + ":59, as the city's reviews always were");
    }

    // ---------------------------------------------------------------- the hour asks the game again

    private static FlarePost ClosedReviewPost(int id, int day, int hour, FlareAuthor author, FlareSource source, bool shared = false, int replyTo = -1)
    {
        return new FlarePost { Id = id, Day = day, Hour = hour, Minute = 20, Author = author, Source = source, Shared = shared, ReplyTo = replyTo, Place = ClosedReviewMine, Name = "My Cafe", Arg = 2 };
    }

    private static FlareFile ClosedReviewFile()
    {
        FlareFile f = FlareLiveFile(31);
        f.Posts.Add(ClosedReviewPost(1, 5, 10, FlareAuthor.Citizen, FlareSource.Review));
        f.Posts.Add(ClosedReviewPost(2, 5, 11, FlareAuthor.Citizen, FlareSource.Review));
        f.Posts.Add(ClosedReviewPost(3, 5, 10, FlareAuthor.Player, FlareSource.Review));
        f.Posts.Add(ClosedReviewPost(4, 5, 10, FlareAuthor.Citizen, FlareSource.Review, true));
        f.Posts.Add(ClosedReviewPost(5, 5, 10, FlareAuthor.Citizen, FlareSource.CityTalk));
        f.Posts.Add(ClosedReviewPost(6, 4, 22, FlareAuthor.Citizen, FlareSource.Review));
        f.Posts.Add(ClosedReviewPost(7, 5, 10, FlareAuthor.Citizen, FlareSource.Reply, false, 1));
        return f;
    }

    private static string Ids(List<FlarePost> posts)
    {
        var ids = new List<string>();
        foreach (FlarePost p in posts)
        {
            ids.Add(p.Id.ToString());
        }
        return string.Join(",", ids);
    }

    private static void FlareClosedReviewHour()
    {
        FlareFile f = ClosedReviewFile();
        var due = new List<FlarePost>();
        FlareLiveRules.ReviewsDue(f, 5 * 24 + 9, 5 * 24 + 10, due);
        string ten = Ids(due);
        FlareLiveRules.ReviewsDue(f, 4 * 24 + 21, 5 * 24 + 11, due);
        string stretch = Ids(due);
        Check(ten == "1" && stretch == "1,2,6", "Flare closed reviews: the hour asks about the city's own reviews of its visits alone, not the player's, a host's copy or other posts ("
                                               + ten + "), and a skipped stretch of hours about each of its visits (" + stretch + ")");
        var asked = new Dictionary<int, int>();
        long upTo = 4 * 24 + 23;
        for (int hour = 0; hour < 24; hour += hour == 3 ? 5 : 1)   // the hours as the game calls them, four of the night slept through at once
        {
            long now = 5 * 24 + hour;
            FlareLiveRules.ReviewsDue(f, upTo, now, due);
            foreach (FlarePost p in due)
            {
                asked[p.Id] = (asked.TryGetValue(p.Id, out int n) ? n : 0) + 1;
            }
            upTo = now;
        }
        Check(asked.Count == 2 && asked[1] == 1 && asked[2] == 1, "Flare closed reviews: over a day's hours each review is asked about once, at its visit's hour");
        bool back = FlareLiveRules.TakeBack(f, 1);
        Check(back && f.Post(1) == null && f.Post(7) == null && f.Post(2) != null && f.Post(3) != null && f.Posts.Count == 5 && !FlareLiveRules.TakeBack(f, 1),
            "Flare closed reviews: a review taken back goes with the replies under it, and nothing else; once only");
    }

    // ---------------------------------------------------------------- the game's closed state, read at the hour

    private static void FlareClosedReviewSources()
    {
        string live = ReviewSource("Social/Flare/FlareDirector.Live.cs");
        string city = ReviewSource("Social/Flare/FlareDirector.City.cs");
        string game = ReviewSource("Social/Flare/FlareLiveGame.cs");
        string rules = ReviewSource("Social/Flare/FlareLiveRules.cs");
        string card = ReviewSource("Social/Flare/FlareApp.Business.cs");
        string tests = ReviewSource("Social/Flare/FlareDirector.Tests.cs");
        string page = ReviewSource("Core/RealLifeOverhaulMod.cs");
        string hours = FlareMethod(game, "private static int OpenHours(BuildingRegistration reg)");
        string openAt = FlareMethod(game, "public static bool OpenAt(string place, int hour, bool today)");
        Check(FlareMethod(game, "public static FlareBiz Biz(BuildingRegistration reg, bool mine)").Contains("Open = OpenHours(reg)")
              && hours.Contains("!reg.temporarilyClosed") && hours.Contains("BusinessHelper.IsBusinessOpen(reg, h)")
              && openAt.Contains("!reg.temporarilyClosed") && openAt.Contains("BusinessHelper.IsBusinessOpen(reg, hour)") && openAt.Contains("AnyBusiness(place)"),
            "Flare closed reviews: a business's open hours are the game's own (BusinessHelper.IsBusinessOpen), none while temporarily closed, read for the day and again at the hour");
        string talk = FlareMethod(rules, "private static FlarePost Talk(");
        Check(talk.Contains("ReviewHour(seed, day, k)") && talk.Contains("OpenPick(from, hour, pick)") && talk.Contains("New(f, day, seed, k, ReviewFrom, ReviewTo")
              && !talk.Contains("% from.Count]"), "Flare closed reviews: the day's reviews pick among the businesses open at the review's hour, never any business of the list");
        string made = FlareMethod(live, "private void CityMade(FlarePost p)");
        string up = FlareMethod(live, "private void ReviewUp(FlarePost p)");
        string at = FlareMethod(live, "private void ReviewsAt(int day, int hour)");
        Check(made.Contains("ReviewUp(p)") && made.Contains("<= _reviewsTo") && !made.Contains("BusinessEffect("),
            "Flare closed reviews: a city review moves no customers and tells nobody at the day's start; only one whose hour has come is asked about at once");
        Check(up.Contains("FlareLiveGame.OpenAt(p.Place, p.Hour, Save != null && Save.Day == p.Day)") && up.IndexOf("FlareLiveGame.OpenAt", StringComparison.Ordinal) < up.IndexOf("BusinessEffect(p, false)", StringComparison.Ordinal)
              && up.Contains("FlareLiveRules.TakeBack(_file, p.Id)") && up.Contains("GoingPublic.IsHost") && up.Contains("SharedCity.Changed(FlareNetShare.Part)"),
            "Flare closed reviews: at its hour a review moves customers (and tells the owner) only with the game's business open; closed, it is taken back, and a host's guests get the feed whole");
        Check(at.Contains("if (!Following)") && at.Contains("FlareLiveRules.ReviewsDue(_file, _reviewsTo, now, _reviewsDue)") && at.Contains("_reviewsTo = now")
              && FlareMethod(city, "private void CityHour(int day, int hour)").Contains("ReviewsAt(day, hour);")
              && FlareMethod(live, "private void LiveStart(FlareFile file)").Contains("_reviewsTo = (long)Save.Day * 24 + Save.Hour"),
            "Flare closed reviews: the hours' reviews are asked about on the game's hour (a step of the hour, never a frame), from the save's hour on; a guest leaves them to the host");
        int effects = 0;
        for (int i = live.IndexOf("BusinessEffect(", StringComparison.Ordinal); i >= 0; i = live.IndexOf("BusinessEffect(", i + 1, StringComparison.Ordinal))
        {
            effects++;
        }
        Check(effects == 1, "Flare closed reviews: the city's reviews reach a business's customers by one way alone, the hour's (" + effects + ")");
        Check(FlareMethod(card, "private string ShopLines(FlareRow row)").Contains("p.Source == FlareSource.Review && p.At <= now"),
            "Flare closed reviews: the business card counts the reviews that went up, not those of visits still to come");
        Check(page.Contains("\"reallife_flare_test_review_now\", () => Flare.FlareDirector.Instance?.TestReviewNow()")
              && FlareMethod(tests, "public void TestReviewNow()").Contains("ReviewUp(p);") && FlareMethod(tests, "public void TestReviewNow()").Contains("TestReady(\"Flare review now\")"),
            "Flare closed reviews: Options > Tests has a review of the player's business now, asked about as the hour asks");
    }

    // ---------------------------------------------------------------- the texts

    private static void FlareClosedReviewTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("Flare closed reviews", words, codes))
        {
            return;
        }
        var missing = new List<string>();
        foreach (string key in new[] { "reallife_flare_test_review_now", "reallife_flare_test_review_up", "reallife_flare_test_review_back", "reallife_flare_test_review_none" })
        {
            string lacking = LanguagesLacking(words, codes, key);
            if (lacking.Length > 0 || !words[0].ContainsKey(key) || words[0][key].Length > 240 || (words[2].TryGetValue(key, out string zh) && !HasHan(zh)))
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "Flare closed reviews: the Tests entry's texts are in every language, short enough for a toast" + (missing.Count > 0 ? "; not: " + string.Join(", ", missing) : string.Empty));
        var lines = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            string next = words[l].TryGetValue("reallife_changes_next", out string t) ? t : string.Empty;
            if (!FixMarks.TryGetValue(codes[l], out string mark) || !next.StartsWith(mark, StringComparison.Ordinal) || !next.Contains("Flare"))
            {
                lines.Add(codes[l]);
            }
        }
        Check(lines.Count == 0 && words[0]["reallife_changes_next"].Contains("temporarily closed") && words[0]["reallife_changes_next"].Contains("opening hours"),
            "Flare closed reviews: What's new's next row has the fix in every language (" + codes.Length + (lines.Count > 0 ? "; not in " + string.Join(", ", lines) : string.Empty) + ")");
    }

    // ---------------------------------------------------------------- the cost

    private static void FlareClosedReviewTiming()
    {
        double hour = ClosedReviewHourCost(out int posts);
        Check(hour < 0.2, "Flare closed reviews: the hour's look for its reviews over a full feed (" + posts + " posts) costs " + hour.ToString("0.0000") + " ms, once a game hour");
        Console.WriteLine("  Flare closed reviews: the hour's look over " + posts + " posts " + hour.ToString("0.0000") + " ms");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double ClosedReviewHourCost(out int posts)
    {
        FlareFile f = FlareLiveFile(13);
        for (int day = 20; day < 24; day++)
        {
            FlareCityFacts facts = FlareFakeCity(day, 2, true);
            FlareLiveRules.CityDay(f, facts, FlareCitizensOf(f, facts), null);
        }
        posts = f.Posts.Count;
        var due = new List<FlarePost>(8);
        const int rounds = 240;
        FlareLiveRules.ReviewsDue(f, 23 * 24 + 9, 23 * 24 + 10, due);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < rounds; i++)
        {
            FlareLiveRules.ReviewsDue(f, 23 * 24 + i % 24 - 1, 23 * 24 + i % 24, due);
        }
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds / rounds;
    }
}
