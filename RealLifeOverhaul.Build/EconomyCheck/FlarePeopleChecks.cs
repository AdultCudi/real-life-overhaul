using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;

// Flare's people (the owner, 4 Nov, day 5: "In Flare there are very few people. Why is that?"). A fresh city of the game's real size
// (about 450 named businesses over 13 districts, the 400 people of the city, the cast of influencers) is played day by day in the order the
// director plays it (the cast, the city's day, then the days before it a step at a time), and read the way Home reads it: how many posts the
// For you feed holds from how many different accounts on the first morning and on day 5, what Stories, Who to follow, Explore and Trending
// draw from, what a day's making costs against the 2 ms slice, and that a save from before grows to the new size in a day or two.
//
// The bar (plans/flare-rework/PLAN.md, "the city's pulse"; the mockup's Home: a feed of different accounts, eight story rings, Who to follow
// and Trending filled): a normal day posts at least 70 times from at least 60 different accounts, and the first morning of a new city, day 1
// at 08:00, already holds at least 40 posts from 40 accounts. Before: a day made 28 to 33 posts from as many accounts, Home at 10:00 on day 1
// held 4 posts from 4 accounts, on day 5 60 posts from 54 accounts; Stories showed no circle for a new account (only accounts followed), Who to
// follow 2 and Trending 3.
internal static partial class Program
{
    private const int PeopleDayPostsLeast = 70, PeopleDayAccountsLeast = 60;   // a normal day
    private const int PeopleFirstPostsLeast = 40, PeopleFirstAccountsLeast = 40;   // day 1, 08:00

    private static readonly string[] PeopleDistricts =
    {
        "ba:neighborhood_midtown", "ba:neighborhood_garment", "ba:neighborhood_hamptons", "ba:neighborhood_hellskitchen", "ba:neighborhood_murrayhill",
        "ba:neighborhood_downtown", "ba:neighborhood_queens", "ba:neighborhood_brooklyn", "ba:neighborhood_harbor", "reallife_neighborhood_uptown",
        "reallife_neighborhood_cedarheights", "ba:neighborhood_industrycity", "ba:neighborhood_thehamptons"
    };

    private static readonly string[] PeopleBizNames =
    {
        "Brewed Beans", "Pump Up", "The Way of the Wordsmith", "Copper Kettle", "Speedy Bites", "Thrifty Threads", "Glow Cosmetics", "Midtown Deli", "Bloom Cafe",
        "Iron Works Gym", "Blue Note Club", "Pixel Repair", "Harbor Grocers", "Linen and Lace", "Corner Pharmacy", "Night Owl Cinema", "Sunrise Bakery", "Metro Books",
        "Golden Fork", "Quick Cuts", "Fresh Fields", "Oak Street Florist", "Daily Grind", "Atlas Hardware", "Velvet Lounge"
    };

    /// <summary>The city's named businesses as the registrations list them: about 450, the way a real save has them. Half keep office hours.</summary>
    private static List<FlareBiz> PeopleCity(int count)
    {
        var all = new List<FlareBiz>(count);
        string[] types = { "coffeeshop", "clothing", "gym", "nightclub", "electronics", "fastfood", "supermarket", "hairdresser", "bookshop", "cinema" };
        for (int i = 0; i < count; i++)
        {
            string name = PeopleBizNames[i % PeopleBizNames.Length] + (i >= PeopleBizNames.Length ? " " + (i / PeopleBizNames.Length + 1) : string.Empty);
            int hours = 0;
            for (int h = i % 2 == 0 ? 9 : 0; h < (i % 2 == 0 ? 17 : 24); h++)
            {
                hours |= 1 << h;
            }
            all.Add(new FlareBiz
            {
                Place = "street_" + (i % 40) + "~" + (i + 1), Name = name, District = PeopleDistricts[i % PeopleDistricts.Length], Niche = FlareCast.NicheOf(types[i % types.Length]),
                Stars = 30 + i % 60, Open = hours
            });
        }
        return all;
    }

    /// <summary>The day's facts as the director hands them over: the sample of the city's businesses the game's pass takes (every step-th named one), the player's own.</summary>
    private static FlareCityFacts PeopleFacts(int day, List<FlareBiz> city, int sample, int busy)
    {
        var f = new FlareCityFacts { Day = day, Busy = busy, Rain = day % 3 == 0, PriceTrend = 1, RealFaults = true };
        for (int i = 0; i < PeopleDistricts.Length; i++)
        {
            f.Districts.Add(PeopleDistricts[i]);
        }
        int step = Math.Max(1, city.Count / sample);
        for (int i = 0, k = 0; i < city.Count && f.Shops.Count < sample; i++)
        {
            if (k++ % step == 0)
            {
                f.Shops.Add(city[i]);
            }
        }
        f.Mine.Add(new FlareBiz { Place = "elm~1", Name = "Pioneer Cafe", District = PeopleDistricts[0], Niche = FlareNiche.Food, Stars = 70, Mine = true, Open = FlareLiveRules.AllHours });
        return f;
    }

    /// <summary>One fresh city played day by day, as the director plays it.</summary>
    private sealed class PeopleRun
    {
        public FlareFile File;
        public List<FlareCitizen> Citizens = new List<FlareCitizen>();
        public List<FlareBiz> City;
        public List<string> Districts = new List<string>(PeopleDistricts);
        public int Busy = 1, Sample = FlareLiveRules.ShopsSample;
        public double DayWorst, DayTotal, StepWorst;
        public int DayCount, Steps;
        public readonly Stopwatch Watch = new Stopwatch();
        public readonly List<FlarePost> Made = new List<FlarePost>();

        public PeopleRun(int seed, int busy, List<FlareBiz> city)
        {
            City = city;
            Busy = busy;
            File = FlareRules.Open(seed, 1, 3, 50);
            File.CastSeed = File.Net.CitySeed = seed * 7 + 1;
            FlareCast.Make(File, 1, Districts);
            FlareLiveRules.CitizensOf(FlareLiveRules.CitySeed(File), Districts, Citizens);
        }

        /// <summary>The cast's day, the city's day, then the days before it a step at a time (FlareDirector.DayStep, steps 1 to 4).</summary>
        public void Day(int day, bool timed)
        {
            FlareCast.TopUp(File, day, Districts);
            FlareCast.Day(File, day, new List<FlareShop>(), new List<string> { "Space Karen" }, new List<string>(), new List<string> { "Zuma Foods", "Nova Motors" }, null);
            FlareCityFacts facts = PeopleFacts(day, City, Sample, Busy);
            Watch.Restart();
            FlareLiveRules.CityDay(File, facts, Citizens, Made);
            Watch.Stop();
            if (timed)
            {
                DayWorst = Math.Max(DayWorst, Watch.Elapsed.TotalMilliseconds);
                DayTotal += Watch.Elapsed.TotalMilliseconds;
                DayCount++;
            }
            bool more = true;
            while (more)
            {
                Watch.Restart();
                more = FlareLiveRules.PeopleBefore(File, facts, Citizens, Made, out int n);
                Watch.Stop();
                if (timed && n > 0)
                {
                    StepWorst = Math.Max(StepWorst, Watch.Elapsed.TotalMilliseconds);
                    Steps++;
                }
            }
        }
    }

    /// <summary>What the For you feed holds when Home is opened at a minute: the posts (as Home lists them, newest first, capped) and the accounts behind them.</summary>
    private static void PeopleFeed(FlareFile f, long now, int cap, out int posts, out int accounts)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        posts = 0;
        for (int i = f.Posts.Count - 1; i >= 0 && posts < cap; i--)
        {
            FlarePost p = f.Posts[i];
            if (p.At > now || p.ReplyTo >= 0)
            {
                continue;
            }
            posts++;
            seen.Add(FlareLiveRules.KeyOf(p));
        }
        accounts = seen.Count;
    }

    /// <summary>The posts one day made and the accounts behind them, and how many of each kind of account.</summary>
    private static void PeopleDayPosts(FlareFile f, int day, out int posts, out int accounts, out int residents, out int shops, out int influencers, out int visits)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        posts = residents = shops = influencers = visits = 0;
        for (int i = 0; i < f.Posts.Count; i++)
        {
            FlarePost p = f.Posts[i];
            if (p.Day != day || p.ReplyTo >= 0)
            {
                continue;
            }
            posts++;
            if (seen.Add(FlareLiveRules.KeyOf(p)))
            {
                residents += p.Author == FlareAuthor.Citizen ? 1 : 0;
                shops += p.Author == FlareAuthor.Business ? 1 : 0;
                influencers += p.Author == FlareAuthor.Influencer ? 1 : 0;
            }
            visits += p.Source == FlareSource.CityTalk && p.Arg / 10 == (int)RealLifeOverhaul.Flare.FlareTalk.Visit ? 1 : 0;
        }
        accounts = seen.Count;
    }

    private static void FlarePeopleChecks()
    {
        List<FlareBiz> city = PeopleCity(450);
        PeopleFresh(city);
        PeopleOptions(city);
        PeopleMix(city);
        PeopleShown(city);
        PeopleCost(city);
        PeopleOldSave(city);
        PeopleSaved(city);
        PeopleTexts();
    }

    // ---------------------------------------------------------------- a fresh city, day 1 to 5

    private static void PeopleFresh(List<FlareBiz> city)
    {
        var run = new PeopleRun(4242, 1, city);
        var lines = new List<string>();
        int shortDays = 0, firstPosts = -1, firstAccounts = -1;
        for (int day = 1; day <= 5; day++)
        {
            run.Day(day, false);
            if (day == 1)
            {
                PeopleFeed(run.File, FlareLiveRules.Now(1, 8, 0), FlareFeedRulesFeedMost(), out firstPosts, out firstAccounts);
            }
            PeopleDayPosts(run.File, day, out int dp, out int da, out int dr, out int ds, out int di, out int dv);
            shortDays += dp >= PeopleDayPostsLeast && da >= PeopleDayAccountsLeast ? 0 : 1;
            PeopleFeed(run.File, FlareLiveRules.Now(day, 10, 0), FlareFeedRulesFeedMost(), out int fp, out int fa);
            lines.Add("day " + day + ": made " + dp + " posts from " + da + " accounts (" + dr + " residents, " + ds + " businesses, " + di + " influencers, " + dv
                      + " visits), Home at 10:00 holds " + fp + " from " + fa);
        }
        PeopleFeed(run.File, FlareLiveRules.Now(5, 10, 0), FlareFeedRulesFeedMost(), out int posts5, out int accounts5);
        foreach (string line in lines)
        {
            Console.WriteLine("    flare people: " + line);
        }
        Console.WriteLine("    flare people: first morning (day 1, 08:00) Home holds " + firstPosts + " posts from " + firstAccounts + " accounts; " + run.File.Posts.Count + " posts kept, "
                          + run.File.ActiveInfluencers + " influencers");
        Check(firstPosts >= PeopleFirstPostsLeast && firstAccounts >= PeopleFirstAccountsLeast,
            "Flare people: a new city's first morning has a day behind it: Home at 08:00 on day 1 holds " + firstPosts + " posts from " + firstAccounts + " accounts (at least "
            + PeopleFirstPostsLeast + " from " + PeopleFirstAccountsLeast + "; before: 4 from 4 at 10:00)");
        Check(shortDays == 0, "Flare people: every day from 1 to 5 posts at least " + PeopleDayPostsLeast + " times from at least " + PeopleDayAccountsLeast
                              + " different accounts (before: 28 to 33 from as many); " + shortDays + " day(s) short");
        Check(posts5 >= 100 && accounts5 >= 80, "Flare people: on day 5 at 10:00 the For you feed holds " + posts5 + " posts from " + accounts5 + " accounts, at least 100 from 80 (before: 60 from 54)");
        Check(run.File.Posts.Count <= FlareRules.PostsMost && run.File.ActiveInfluencers >= FlareCast.Start,
            "Flare people: the posts kept stay under the cap of " + FlareRules.PostsMost + " (" + run.File.Posts.Count + ") and the cast has its " + FlareCast.Start + " influencers ("
            + run.File.ActiveInfluencers + "; before 16)");
        // The same seed, the same city on every machine.
        var again = new PeopleRun(4242, 1, city);
        for (int day = 1; day <= 5; day++)
        {
            again.Day(day, false);
        }
        Check(FlareStore.WritePosts(run.File) == FlareStore.WritePosts(again.File), "Flare people: the same city's seed makes the same posts on every machine (Going Public)");
    }

    // ---------------------------------------------------------------- the option, never twice in a day, real names and open hours

    private static void PeopleOptions(List<FlareBiz> city)
    {
        int[] posts = new int[3], accounts = new int[3];
        for (int busy = 0; busy < 3; busy++)
        {
            var run = new PeopleRun(77, busy, city);
            for (int day = 1; day <= 4; day++)
            {
                run.Day(day, false);
            }
            PeopleDayPosts(run.File, 4, out posts[busy], out accounts[busy], out _, out _, out _, out _);
        }
        Check(posts[0] >= 25 && posts[0] < posts[1] && posts[1] < posts[2] && accounts[0] >= 25,
            "Flare people: the option still scales the city: a day is " + posts[0] + " posts quiet, " + posts[1] + " normal, " + posts[2] + " busy (" + accounts[0] + "/" + accounts[1] + "/" + accounts[2]
            + " accounts)");
        // Nobody posts twice in a day of the city's people.
        bool once = true;
        for (int day = 1; day <= 30 && once; day++)
        {
            var seen = new HashSet<int>();
            for (int k = 0; k < FlareLiveRules.CityTalkPerDay[2]; k++)
            {
                once &= seen.Add(FlareLiveRules.TalkCitizen(99, day, k));
            }
        }
        Check(once, "Flare people: the city's people are dealt out, so nobody posts twice in a day, whatever the option (30 days of 72)");
        // Real names, real places, the hours the game has them open: a visit and an offer name a business of the city's, a visit at an hour it is open.
        var one = new PeopleRun(31, 1, city);
        for (int day = 1; day <= 3; day++)
        {
            one.Day(day, false);
        }
        var byPlace = new Dictionary<string, FlareBiz>(StringComparer.Ordinal);
        for (int i = 0; i < city.Count; i++)
        {
            byPlace[city[i].Place] = city[i];
        }
        byPlace["elm~1"] = new FlareBiz { Place = "elm~1", Name = "Pioneer Cafe", Open = FlareLiveRules.AllHours };
        int visits = 0, offers = 0, mine = 0;
        bool real = true, open = true;
        for (int i = 0; i < one.File.Posts.Count; i++)
        {
            FlarePost p = one.File.Posts[i];
            bool visit = p.Author == FlareAuthor.Citizen && p.Source == FlareSource.CityTalk && p.Arg / 10 == (int)RealLifeOverhaul.Flare.FlareTalk.Visit;
            bool offer = p.Author == FlareAuthor.Business && p.Source == FlareSource.BusinessPost && p.Arg >= FlareLiveRules.OfferFirst && p.Arg < FlareLiveRules.OfferFirst + FlareLiveRules.OfferKinds;
            if (!visit && !offer)
            {
                continue;
            }
            visits += visit ? 1 : 0;
            offers += offer ? 1 : 0;
            mine += visit && p.Place == "elm~1" ? 1 : 0;
            bool ok = p.Place != null && byPlace.TryGetValue(p.Place, out FlareBiz b) && b.Name == p.Name && p.District != null && !string.IsNullOrEmpty(p.Topic);
            if (!ok && real)
            {
                Console.WriteLine("    flare people: not real: " + (visit ? "visit" : "offer") + " place " + p.Place + " name " + p.Name + " district " + p.District + " topic " + p.Topic);
            }
            real &= ok;
            open &= !visit || (byPlace.TryGetValue(p.Place, out FlareBiz v) && FlareLiveRules.OpenAt(v.Open, p.Hour));
        }
        Check(visits >= 40 && offers >= 20 && real, "Flare people: residents at real businesses (" + visits + " visits) and shops' own offers (" + offers + "), each with the business's own name, place and district");
        Check(open, "Flare people: nobody posts from a business at an hour the game has it closed");
        Check(mine > 0, "Flare people: the same rules for every business: residents are at the player's own business too (" + mine + " visits), never a post of its own");
        bool noPlayerShop = true;
        for (int i = 0; i < one.File.Posts.Count; i++)
        {
            noPlayerShop &= !(one.File.Posts[i].Author == FlareAuthor.Business && one.File.Posts[i].Place == "elm~1");
        }
        Check(noPlayerShop, "Flare people: a business of the player's posts only when the player does, as before (parity: the rivals' and the city's shops post by the rules of the day)");
        // The economy does not grow with the feed: only the first posts of a day are heard by their followers, as before.
        Check(FlareLiveRules.BusinessHeardPerDay[0] == 2 && FlareLiveRules.BusinessHeardPerDay[1] == 4 && FlareLiveRules.BusinessHeardPerDay[2] == 7
              && FlareLiveRules.CaseTalkPerDay[0] == 7 && FlareLiveRules.CaseTalkPerDay[1] == 14 && FlareLiveRules.CaseTalkPerDay[2] == 24,
            "Flare people: reports, reviews and the shops' heard posts are as many as before (7, 14, 24 and 2, 4, 7): the city's life grows, not its cases or its customers' moves");
    }

    // ---------------------------------------------------------------- the kinds of account

    private static void PeopleMix(List<FlareBiz> city)
    {
        var run = new PeopleRun(4242, 1, city);
        for (int day = 1; day <= 3; day++)
        {
            run.Day(day, false);
        }
        PeopleDayPosts(run.File, 3, out int posts, out int accounts, out int residents, out int shops, out int influencers, out _);
        int officials = 0;
        for (int i = 0; i < run.File.Posts.Count; i++)
        {
            officials += run.File.Posts[i].Author == FlareAuthor.Official && run.File.Posts[i].Day == 3 ? 1 : 0;
        }
        Check(residents >= 30 && shops >= 12 && influencers >= 5 && officials >= 1,
            "Flare people: a day has residents (" + residents + "), shops (" + shops + "), influencers (" + influencers + ") and the city's officials (" + officials + ") posting, " + accounts + " accounts in all");
    }

    // ---------------------------------------------------------------- Stories, Who to follow, Explore and Trending

    private static void PeopleShown(List<FlareBiz> city)
    {
        var run = new PeopleRun(4242, 1, city);
        for (int day = 1; day <= 3; day++)
        {
            run.Day(day, false);
        }
        FlareFile f = run.File;
        long now = FlareLiveRules.Now(3, 14, 0);
        var keys = new List<string>();
        var at = new List<int>();
        FlareFeedRules.Stories(f, now, 8, keys, at);
        var kinds = new HashSet<char>();
        bool distinct = new HashSet<string>(keys).Count == keys.Count, fresh = true;
        for (int i = 0; i < keys.Count; i++)
        {
            kinds.Add(keys[i][0]);
            FlarePost p = f.Posts[at[i]];
            fresh &= p.At <= now && now - p.At <= 1440 && FlareLiveRules.KeyOf(p) == keys[i];
        }
        Check(keys.Count == 8 && distinct && fresh && kinds.Count >= 3,
            "Flare people: a new account that follows nobody still sees " + keys.Count + " story rings of the last day, every one a different account of " + kinds.Count + " kinds (before: none)");
        // What the player follows comes first.
        string followed = keys[keys.Count - 1];
        FlareCast.Follow(f, followed, true);
        FlareFeedRules.Stories(f, now, 8, keys, at);
        Check(keys.Count == 8 && keys[0] == followed, "Flare people: an account the player follows leads the stories (at " + keys.IndexOf(followed) + ")");
        FlareCast.Follow(f, followed, false);
        var who = new List<string>();
        FlareFeedRules.WhoToFollow(f, now, 5, who);
        var whoKinds = new HashSet<char>();
        bool followable = true;
        for (int i = 0; i < who.Count; i++)
        {
            whoKinds.Add(who[i][0]);
            followable &= FlareFeedRules.Followable(who[i]) && !FlareCast.Followed(f, who[i]);
        }
        Check(who.Count == 5 && followable && whoKinds.Count >= 3 && new HashSet<string>(who).Count == 5,
            "Flare people: Who to follow lists " + who.Count + " accounts of " + whoKinds.Count + " kinds (influencers, businesses, residents), none followed or unfollowable (before: 2, influencers first)");
        var explore = new List<string>();
        FlareFeedRules.WhoToFollow(f, now, 12, explore);
        Check(explore.Count == 12 && new HashSet<string>(explore).Count == 12, "Flare people: Explore offers " + explore.Count + " accounts to follow (before: 6)");
        var tags = new List<string>();
        var counts = new List<int>();
        FlareLiveRules.Trending(f.Posts, now, 8, tags, counts);
        Check(tags.Count >= 6, "Flare people: Trending in the city has " + tags.Count + " tags from the last day (before: the same few)");
        // Home opens quickly over the whole kept feed: the rules the page reads, once a change or ten game minutes.
        FlareFeedRules.Stories(f, now, 8, keys, at);
        int feedMost = FlareFeedRulesFeedMost();
        var sw = Stopwatch.StartNew();
        int rounds = 200;
        for (int r = 0; r < rounds; r++)
        {
            FlareFeedRules.Stories(f, now, 8, keys, at);
            FlareFeedRules.WhoToFollow(f, now, 12, explore);
            FlareLiveRules.Trending(f.Posts, now, 8, tags, counts);
            int shown = 0;
            for (int i = f.Posts.Count - 1; i >= 0 && shown < feedMost; i--)
            {
                shown += f.Posts[i].At <= now && f.Posts[i].ReplyTo < 0 ? 1 : 0;
            }
        }
        sw.Stop();
        double each = sw.Elapsed.TotalMilliseconds / rounds;
        Console.WriteLine("    flare people: Home's rules (stories, who to follow, trending, the feed's walk) over " + f.Posts.Count + " posts: " + each.ToString("0.000") + " ms");
        Check(each < 2.0, "Flare people: speed: the rules Home reads over " + f.Posts.Count + " kept posts take " + each.ToString("0.000") + " ms, under the 2 ms slice");
    }

    private static int FlareFeedRulesFeedMost()
    {
        Match cap = Regex.Match(File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Ui", "Flare", "FlareScreen.cs")), @"\bFeedMost = (\d+)");
        return cap.Success ? int.Parse(cap.Groups[1].Value) : 0;
    }

    // ---------------------------------------------------------------- the cost of a day

    private static void PeopleCost(List<FlareBiz> city)
    {
        // Warm up, then the days of many cities.
        var warm = new PeopleRun(1, 1, city);
        for (int day = 1; day <= 3; day++)
        {
            warm.Day(day, false);
        }
        double dayWorst = 0, dayTotal = 0, stepWorst = 0;
        int days = 0, steps = 0;
        double busyWorst = 0;
        for (int rep = 0; rep < 30; rep++)
        {
            var run = new PeopleRun(500 + rep, 1, city);
            for (int day = 1; day <= 6; day++)
            {
                run.Day(day, rep >= 3);
            }
            dayWorst = Math.Max(dayWorst, run.DayWorst);
            dayTotal += run.DayTotal;
            days += run.DayCount;
            stepWorst = Math.Max(stepWorst, run.StepWorst);
            steps += run.Steps;
            var busy = new PeopleRun(900 + rep, 2, city);
            for (int day = 1; day <= 6; day++)
            {
                busy.Day(day, rep >= 3);
            }
            busyWorst = Math.Max(busyWorst, Math.Max(busy.DayWorst, busy.StepWorst));
        }
        double slice = FlareCityCatalogue.SliceMs;
        Console.WriteLine("    flare people: a day's making (the city's day and its prune) " + (dayTotal / days).ToString("0.000") + " ms on average, " + dayWorst.ToString("0.000") + " ms at worst; a step of the days before "
                          + stepWorst.ToString("0.000") + " ms at worst (" + steps + " steps); the busiest option " + busyWorst.ToString("0.000") + " ms at worst; the slice is " + slice.ToString("0.0") + " ms (before: 0.006 ms a day)");
        Check(dayWorst < slice && stepWorst < slice && busyWorst < slice,
            "Flare people: speed: a day's making takes " + dayWorst.ToString("0.000") + " ms at worst, a step of the days before " + stepWorst.ToString("0.000") + " ms and the busiest option "
            + busyWorst.ToString("0.000") + " ms, each under the " + slice.ToString("0.0") + " ms slice (the director plays them a step a frame)");
        // The save: a full feed written and read back.
        var full = new PeopleRun(7, 2, city);
        for (int day = 1; day <= 6; day++)
        {
            full.Day(day, false);
        }
        var sw = Stopwatch.StartNew();
        string line = null;
        for (int r = 0; r < 20; r++)
        {
            line = FlareStore.WritePosts(full.File);
        }
        sw.Stop();
        double write = sw.Elapsed.TotalMilliseconds / 20;
        sw.Restart();
        FlareFile back = null;
        for (int r = 0; r < 20; r++)
        {
            back = FlareStore.Read(FlareStore.WriteAccount(full.File), line);
        }
        sw.Stop();
        double read = sw.Elapsed.TotalMilliseconds / 20;
        Console.WriteLine("    flare people: the posts line of " + full.File.Posts.Count + " posts is " + line.Length / 1024 + " KB, written in " + write.ToString("0.00") + " ms and read in " + read.ToString("0.00") + " ms");
        Check(back != null && back.Posts.Count == full.File.Posts.Count && write < 4.0 && read < 8.0,
            "Flare people: the busiest city's " + full.File.Posts.Count + " kept posts (" + line.Length / 1024 + " KB) round trip through the save, written in " + write.ToString("0.00") + " ms and read in " + read.ToString("0.00")
            + " ms (the save reads on a worker)");
        // Going Public: the host's whole part carries the newest posts only.
        string whole = FlareNetShare.WriteWhole(full.File, 6, "host", "Host", true, null, 1);
        int lines = whole.Split('\n').Length;
        Check(lines <= FlareNetShare.PostsWhole + 40 && whole.Length < 200 * 1024, "Flare people: the host's whole part to a session carries the newest " + FlareNetShare.PostsWhole + " shared posts at most (" + lines + " lines, " + whole.Length / 1024 + " KB)");
    }

    // ---------------------------------------------------------------- a save from before grows

    private static void PeopleOldSave(List<FlareBiz> city)
    {
        // A save from before: three days of the old size (a third of the people's posts, no people's extras made), a cast of 16, the city's day made.
        var run = new PeopleRun(2024, 1, city);
        for (int day = 1; day <= 3; day++)
        {
            run.Day(day, false);
        }
        FlareFile f = run.File;
        for (int i = f.Posts.Count - 1; i >= 0; i--)
        {
            FlarePost p = f.Posts[i];
            bool people = p.Author == FlareAuthor.Citizen && p.Source == FlareSource.CityTalk || p.Author == FlareAuthor.Business;
            if (people && p.Id % 3 != 0)
            {
                f.Posts.RemoveAt(i);
            }
        }
        for (int i = 16; i < f.Influencers.Count; i++)
        {
            f.Influencers[i].Quit = true;
        }
        f.Net.PeopleFrom = -1;
        PeopleDayPosts(f, 3, out int before, out _, out _, out _, out _, out _);
        int activeBefore = f.ActiveInfluencers;
        // The update lands on day 3: the city's day is made already; the days before are made a step a frame, and the cast grows four a day.
        FlareCityFacts facts = PeopleFacts(3, city, FlareLiveRules.ShopsSample, 1);
        var made = new List<FlarePost>();
        var sw = new Stopwatch();
        double worst = 0;
        int steps = 0;
        while (true)
        {
            sw.Restart();
            bool more = FlareLiveRules.PeopleBefore(f, facts, run.Citizens, made, out int n);
            sw.Stop();
            worst = Math.Max(worst, sw.Elapsed.TotalMilliseconds);
            steps += n > 0 ? 1 : 0;
            if (!more)
            {
                break;
            }
        }
        PeopleDayPosts(f, 3, out int after, out int accounts, out _, out _, out _, out _);
        PeopleDayPosts(f, 2, out int after2, out _, out _, out _, out _, out _);
        PeopleDayPosts(f, 1, out int after1, out _, out _, out _, out _, out _);
        Check(steps == 3 && after >= PeopleDayPostsLeast && accounts >= PeopleDayAccountsLeast && after2 > 40 && after1 > 40 && worst < FlareCityCatalogue.SliceMs,
            "Flare people: a save from before grows to the new size in " + steps + " steps of a frame each (day 3 " + before + " to " + after + " posts from " + accounts + " accounts, days 2 and 1 now " + after2 + " and "
            + after1 + "), the slowest step " + worst.ToString("0.000") + " ms, under the slice");
        int days = 0;
        while (f.ActiveInfluencers < FlareCast.Start && days < 10)
        {
            FlareCast.TopUp(f, 3 + days, run.Districts);
            days++;
        }
        Check(activeBefore == 16 && f.ActiveInfluencers == FlareCast.Start && days == (FlareCast.Start - 16 + FlareCast.TopUpPerDay - 1) / FlareCast.TopUpPerDay,
            "Flare people: a cast of 16 grows to " + f.ActiveInfluencers + " in " + days + " days, " + FlareCast.TopUpPerDay + " a day (and a cast at its size gets none: " + FlareCast.TopUp(f, 9, run.Districts) + ")");
        // A guest that follows the host makes none of the city's posts.
        FlareFile guest = FlareRules.Open(3, 1, 3, 50);
        guest.CastSeed = guest.Net.CitySeed = 12;
        FlareCityFacts follow = PeopleFacts(4, city, FlareLiveRules.ShopsSample, 1);
        follow.Following = true;
        Check(!FlareLiveRules.PeopleBefore(guest, follow, run.Citizens, made, out int none) && none == 0 && guest.Posts.Count == 0, "Flare people: a guest that follows the host's feed makes none of the people's posts");
        // A city with nothing in it never throws and never loops.
        FlareFile empty = FlareRules.Open(4, 1, 3, 50);
        empty.CastSeed = empty.Net.CitySeed = 13;
        var bare = new FlareCityFacts { Day = 0, Busy = 1 };
        int rounds = 0;
        while (FlareLiveRules.PeopleBefore(empty, bare, new List<FlareCitizen>(), made, out _) && rounds < 10)
        {
            rounds++;
        }
        Check(rounds < 10, "Flare people: a city with no people, shops or districts on its first day makes the days before without a fault and without end");
    }

    // ---------------------------------------------------------------- the save

    private static void PeopleSaved(List<FlareBiz> city)
    {
        var run = new PeopleRun(8, 1, city);
        for (int day = 1; day <= 4; day++)
        {
            run.Day(day, false);
        }
        FlareFile f = run.File;
        string line = FlareLiveStore.Write(f.Net);
        var back = new FlareNet();
        FlareLiveStore.Read(back, line);
        Check(back.PeopleFrom == f.Net.PeopleFrom && f.Net.PeopleFrom >= 0 && back.CityDay == f.Net.CityDay && back.DmDay == f.Net.DmDay,
            "Flare people: the day the people's posts are made from (" + f.Net.PeopleFrom + ") goes through the network's save line, at its end");
        // The line an older save wrote: fifteen fields, none of this: it reads, and the field is its default.
        string[] head = line.Split('\n')[0].Split('|');
        string old = string.Join("|", head, 0, 15);
        var older = new FlareNet();
        FlareLiveStore.Read(older, old);
        Check(older.PeopleFrom == -1 && older.CityDay == f.Net.CityDay && older.CitySeed == f.Net.CitySeed, "Flare people: a network line from before (no people's day) loads, and the people's posts are made on the next day's step");
        // The posts line keeps visits and offers (Place, Name, Arg) through the save.
        FlareFile reread = FlareStore.Read(FlareStore.WriteAccount(f), FlareStore.WritePosts(f));
        int a = 0, b = 0;
        for (int i = 0; i < f.Posts.Count; i++)
        {
            FlarePost p = f.Posts[i];
            FlarePost q = reread.Post(p.Id);
            a++;
            b += q != null && q.Arg == p.Arg && q.Place == p.Place && q.Name == p.Name && q.Topic == p.Topic && q.District == p.District && q.Who == p.Who ? 1 : 0;
        }
        Check(a == b && a > 0, "Flare people: every kept post (" + a + ") keeps its words' kind, business, district, tags and author through the save");
    }

    // ---------------------------------------------------------------- the words

    private static void PeopleTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("Flare people", words, codes))
        {
            return;
        }
        var bad = new List<string>();
        int keys = 0;
        for (int lang = 0; lang < 3; lang++)
        {
            for (int i = 0; i < 12; i++)
            {
                string[] ask = i < 10 ? new[] { "reallife_flare_talk_visit_" + i, "reallife_flare_biz_offer_" + i } : new[] { "reallife_flare_biz_offer_" + i };
                foreach (string k in ask)
                {
                    keys++;
                    if (!words[lang].TryGetValue(k, out string t))
                    {
                        bad.Add(codes[lang] + " lacks " + k);
                        continue;
                    }
                    bool named = t.Contains("{name}");
                    bool dash = t.IndexOf('-') >= 0 || t.IndexOf('–') >= 0 || t.IndexOf('—') >= 0;
                    if (!named || dash || t.Contains("!") || t.Contains("！") || t.Length > 120)
                    {
                        bad.Add(codes[lang] + " " + k + (named ? string.Empty : " has no business") + (dash ? " has a dash" : string.Empty) + (t.Contains("!") ? " has an exclamation mark" : string.Empty));
                    }
                }
            }
            foreach (string k in new[] { "reallife_flare_dm_hello_2", "reallife_flare_dm_hello_3" })
            {
                keys++;
                if (!words[lang].TryGetValue(k, out string t) || t.Contains("!") || t.IndexOf('-') >= 0)
                {
                    bad.Add(codes[lang] + " " + k);
                }
            }
        }
        words[0].TryGetValue("reallife_changes_next", out string en);
        Check(bad.Count == 0 && keys == 72 && en != null && en.Contains("Flare is alive from the first day"),
            "Flare people: the " + keys + " new lines of a visit, an offer and a hello are in en, pt and zh-cn, each naming its business, with no dash and no exclamation mark, and What's new says it"
            + (bad.Count > 0 ? " (" + string.Join("; ", bad) + ")" : string.Empty));
        // Each visit line fits the kind of business it is for: two lines to a kind, five kinds.
        bool groups = true;
        for (int g = 0; g < 5; g++)
        {
            for (int roll = 0; roll < 2; roll++)
            {
                FlareNiche niche = g == 0 ? FlareNiche.Food : g == 1 ? FlareNiche.Fashion : g == 2 ? FlareNiche.Fitness : g == 3 ? FlareNiche.Nightlife : FlareNiche.Tech;
                groups &= FlareLiveRules.VisitVariant(niche, roll) == g * 2 + roll;
            }
        }
        groups &= FlareLiveRules.VisitVariant(FlareNiche.Business, 1) == 9;
        Check(groups, "Flare people: a visit's words are chosen by the kind of business, two lines each for food, fashion, fitness, night life and the rest");
    }
}
