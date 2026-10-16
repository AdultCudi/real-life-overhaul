using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Flare;

// Checks for Flare, the city's social media app (Scripts/Social/Flare): the tiers, the worked example of the follower
// curve at the top of FlareRules, how long a new account takes to be Known, the limits on posting, repeats, the viral
// chance and its gap, a bad post, the friends' answers, the daily round, drafts, what the hour and the day draft, the
// bound on what is kept, the one notification a day, the save's two lines and the time a long game takes.
internal static partial class Program
{
    private static void Flare()
    {
        FlareTiers();
        FlareWorkedExample();
        FlareToKnown();
        FlareLimits();
        FlareRepeats();
        FlareViral();
        FlareBadPost();
        FlareFriends();
        FlareDay();
        FlareDrafts();
        FlareKept();
        FlareNotices();
        FlareSave();
        FlareTiming();
        FlareInfluences();
        FlareInfluencersChecks();
        FlareCityChecks();
        FlareBusyChecks();
        FlareReplyLikeChecks();
        FlareCommentLikeChecks();
        FlarePictureChecks();
    }

    private static List<FlarePerson> FlarePeople()
    {
        return new List<FlarePerson>
        {
            new FlarePerson { Index = 0, First = "Mom", Stage = 3, Taste = FlareTaste.Cheers, Closeness = 80 },
            new FlarePerson { Index = 1, First = "Alex", Stage = 2, Taste = FlareTaste.Cool, Closeness = 50 },
            new FlarePerson { Index = 2, First = "Sam", Stage = 1, Taste = FlareTaste.Flaky | FlareTaste.Food, Closeness = 20 },
            new FlarePerson { Index = 3, First = "Kim", Stage = 0, Taste = FlareTaste.Sport, Closeness = 5 },
            new FlarePerson { Index = 4, First = "Lee", Stage = 4, Taste = FlareTaste.None, Closeness = 70, Partner = true }
        };
    }

    private static void FlareTiers()
    {
        Check(FlareRules.TierOf(0) == FlareTier.Newcomer && FlareRules.TierOf(499) == FlareTier.Newcomer && FlareRules.TierOf(500) == FlareTier.Local, "Flare: Local from 500");
        Check(FlareRules.TierOf(4999) == FlareTier.Local && FlareRules.TierOf(5000) == FlareTier.Known && FlareRules.TierOf(25000) == FlareTier.Influencer
              && FlareRules.TierOf(100000) == FlareTier.Celebrity && FlareRules.TierOf(int.MaxValue) == FlareTier.Celebrity, "Flare: Known 5,000, Influencer 25,000, Celebrity 100,000");
        Check(FlareRules.NextTierAt(0) == 500 && FlareRules.NextTierAt(5000) == 25000 && FlareRules.NextTierAt(100000) == -1, "Flare: the next tier, and none past the top");
        FlareFile f = FlareRules.Open(7, 10, 3, 55);
        Check(f.Followers == 3 + FlareRules.StartExtra && f.Reputation == 55 && f.StartDay == 10 && f.History[0] == f.Followers && f.Tier == FlareTier.Newcomer,
            "Flare: a new account has the people met and a few dozen more (" + f.Followers + ")");
        Check(FlareRules.SourceQuality.Length == (int)FlareSource.Answer + 1, "Flare: a quality for every source");
        Check((int)FlareSource.Arrested == 10 && (int)FlareSource.Life == 11 && (int)FlareTone.Cheeky == 3 && (int)FlareAuthor.Friend == 1, "Flare: the kinds keep their numbers (they are in saves)");
    }

    private static void FlareWorkedExample()
    {
        // The table at the top of FlareRules.cs, row by row.
        int[,] rows =
        {
            // followers, quality, fit, reputation, gain, reach
            { 50, 1000, 1100, 50, 56, 16 },
            { 1000, 750, 1100, 50, 86, 248 },
            { 5000, 1000, 1150, 50, 219, 1725 },
            { 25000, 850, 1150, 50, 374, 7331 },
            { 100000, 1000, 1150, 80, 966, 34500 },
            { 5000, 750, 1100, 10, 126, 1238 }
        };
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            int gain = FlareRules.Gain(rows[i, 0], rows[i, 1], rows[i, 2], rows[i, 3], 1000);
            int reach = FlareRules.Reach(rows[i, 0], rows[i, 1], rows[i, 2]);
            Check(gain == rows[i, 4] && reach == rows[i, 5], "Flare: the worked example's row " + (i + 1) + " (gain " + gain + ", reach " + reach + ")");
        }
        Check(FlareRules.Quality(FlareSource.Selfie, true, false) == 1000 && FlareRules.Quality(FlareSource.Selfie, false, true) == 850 && FlareRules.Quality(FlareSource.Outing, false, true) == 700,
            "Flare: a tag adds a quarter, a picture in the shop a tenth, and only to a picture");
        Check(FlareRules.ToneFit(FlareSource.Outing, FlareTone.Cheerful) == 1100 + 50 && FlareRules.BestTone(FlareSource.Arrested) == FlareTone.Honest && FlareRules.BestTone(FlareSource.Opened) == FlareTone.Proud,
            "Flare: the tone that suits a source best");
        int previous = 0;
        bool slower = true;
        for (int followers = 0; followers <= 200000; followers += 5000)
        {
            int gain = FlareRules.Gain(followers, 1000, 1000, 50, 1000);
            slower &= gain >= previous;
            previous = gain;
        }
        Check(slower && FlareRules.Gain(100000, 1000, 1000, 50, 1000) < 100000 / 100, "Flare: a post brings more to a bigger account, but ever less of its size");
    }

    // Three ordinary posts a day from a new account: Known in about a month.
    private static void FlareToKnown()
    {
        FlareFile f = FlareRules.Open(11, 1, 14, 50);
        var people = FlarePeople();
        FlareSource[] sources = { FlareSource.Selfie, FlareSource.Outing, FlareSource.Photo };
        int day = 1;
        for (; day < 200 && f.Followers < FlareRules.TierFrom[2]; day++)
        {
            for (int i = 0; i < 3; i++)
            {
                FlareDraft d = FlareRules.Draft(f, sources[i], day, 9 + i * 3, -1, null, null, 0);
                f.LastViralDay = day;   // the curve without luck
                FlareRules.Post(f, d, FlareRules.BestTone(d.Source), -1, people, day, 9 + i * 3, (9 + i * 3) * 60);
            }
            FlareRules.Day(f, day + 1, 50, people);
        }
        Check(day >= 20 && day <= 45, "Flare: three ordinary posts a day make a new account Known in about a month (" + day + " days)");
    }

    private static void FlareLimits()
    {
        FlareFile f = FlareRules.Open(3, 1, 0, 50);
        var people = new List<FlarePerson>();
        Check(FlareRules.Counts(f, 5) && FlareRules.PostsToday(f, 5) == 0, "Flare: a new account's first post counts");
        FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Photo, 5, 10, -1, null, "a.png", 0), FlareTone.Cheeky, -1, people, 5, 10, 600);
        FlarePost second = FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Selfie, 5, 10, -1, null, "b.png", 0), FlareTone.Cheerful, -1, people, 5, 10, 601);
        Check(second != null && second.Gained > 0 && FlareRules.PostsToday(f, 5) == 2, "Flare: a second post a minute after the first is made and counts (no gap between posts)");
        FlareRules.Post(f, FlareRules.Draft(f, FlareSource.GoodDeed, 5, 14, -1, null, null, 0), FlareTone.Honest, -1, people, 5, 14, 840);
        int followers = f.Followers;
        FlarePost fourth = FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Outing, 5, 15, -1, null, null, 1), FlareTone.Cheerful, -1, people, 5, 15, 900);
        Check(fourth != null && fourth.Gained == 0 && f.Followers == followers && f.PostsToday == 4 && !FlareRules.Counts(f, 5),
            "Flare: a fourth post in a day is made and seen, and leaves the account as it is");
        Check(FlareRules.Counts(f, 6) && FlareRules.PostsToday(f, 6) == 0, "Flare: a new day counts afresh");
    }

    private static void FlareRepeats()
    {
        Check(FlareRules.Repeat(-1, 9) == 1000 && FlareRules.Repeat(9, 9) == 500 && FlareRules.Repeat(8, 9) == 550 && FlareRules.Repeat(0, 10) == 1000 && FlareRules.Repeat(1, 10) == 950,
            "Flare: the same kind again is half the same day, a little more each day, whole after ten");
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        var people = new List<FlarePerson>();
        FlarePost first = FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Selfie, 3, 9, -1, null, "a.png", 0), FlareTone.Cheerful, -1, people, 3, 9, 540);
        f.LastViralDay = 3;
        FlarePost second = FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Selfie, 3, 12, -1, null, "b.png", 0), FlareTone.Cheerful, -1, people, 3, 12, 720);
        Check(second.Quality == first.Quality / 2, "Flare: a second selfie the same day is worth half (" + first.Quality + ", " + second.Quality + ")");
    }

    private static void FlareViral()
    {
        Check(FlareRules.ViralChance(0, 1000, 1000) == 3 && FlareRules.ViralChance(int.MaxValue / 2, 1250, 1200) == FlareRules.ViralMost && FlareRules.ViralChance(100, 0, 1000) == 0,
            "Flare: the viral chance rises with the account and never passes " + FlareRules.ViralMost + " per mille");
        // A thousand posts from a big account: viral moments a week apart at least, each a jump.
        FlareFile f = FlareRules.Open(99, 1, 0, 80);
        f.Followers = 200000;
        var people = new List<FlarePerson>();
        int virals = 0, lastViral = -100;
        bool apart = true, jumps = true;
        for (int i = 0; i < 1000; i++)
        {
            int day = 1 + i / 3;
            int before = f.Followers;
            FlarePost p = FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Opened, day, 10, -1, "Main~" + i, "Shop", 0), FlareTone.Proud, -1, people, day, 10, 600 + (i % 3) * 150);
            if (p.Viral)
            {
                virals++;
                apart &= day - lastViral >= FlareRules.ViralGapDays;
                lastViral = day;
                jumps &= f.Followers - before >= before * FlareRules.ViralShare / 1000;
            }
        }
        Check(virals > 0 && apart && jumps, "Flare: viral moments come, a week apart at least, each 3% of the followers and more (" + virals + " in 334 days)");
        Check(virals <= 334 / FlareRules.ViralGapDays + 1, "Flare: never more than one a week");
    }

    private static void FlareBadPost()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        f.Followers = 10000;
        FlarePost p = FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Arrested, 4, 10, -1, null, null, 0), FlareTone.Cheerful, -1, new List<FlarePerson>(), 4, 10, 600);
        Check(p.Bad && !p.Viral && p.Gained == -(10000 * FlareRules.BadShare / 1000 + FlareRules.BadLeast) && f.Followers == 10000 + p.Gained && f.Reputation == 50 - FlareRules.BadReputationKnown,
            "Flare: cheerful about an arrest goes badly: 2% and 5 followers, and reputation (" + p.Gained + ", " + f.Reputation + ")");
        FlareFile g = FlareRules.Open(5, 1, 0, 50);
        FlarePost honest = FlareRules.Post(g, FlareRules.Draft(g, FlareSource.Arrested, 4, 10, -1, null, null, 0), FlareTone.Honest, -1, new List<FlarePerson>(), 4, 10, 600);
        Check(!honest.Bad && g.Reputation >= 50 + FlareRules.HonestReputation, "Flare: honest about an arrest softens it (" + g.Reputation + ")");
        FlareFile small = FlareRules.Open(5, 1, 0, 50);
        small.Followers = 3;
        FlareRules.Post(small, FlareRules.Draft(small, FlareSource.Arrested, 4, 10, -1, null, null, 0), FlareTone.Proud, -1, new List<FlarePerson>(), 4, 10, 600);
        Check(small.Followers == 0, "Flare: never under no followers");
    }

    private static void FlareFriends()
    {
        var people = FlarePeople();
        var opened = new FlarePost { Source = FlareSource.Opened, Tag = -1 };
        Check(FlareRules.Reaction(people[1], opened, 999) == 0 && FlareRules.Reaction(people[1], opened, 100) == 2 && FlareRules.ReplyOf(people[1], opened) == FlareRules.ReplyKind.Cool,
            "Flare: an envious friend does not like a money post, and is cool when they answer");
        Check(FlareRules.Reaction(people[0], opened, 999) == 1 && FlareRules.ReplyOf(people[0], opened) == FlareRules.ReplyKind.Proud, "Flare: a supportive friend always likes it");
        var walk = new FlarePost { Source = FlareSource.Outing, Arg = 1, Tag = 2 };
        Check(FlareRules.Reaction(people[2], walk, 0) == 2, "Flare: the friend tagged always answers, flaky or not");
        Check(FlareRules.Reaction(people[4], opened, 999) == 2 && FlareRules.ReplyOf(people[4], opened) == FlareRules.ReplyKind.Love, "Flare: the partner always answers, with love");
        var arrest = new FlarePost { Source = FlareSource.Arrested, Tag = -1 };
        Check(FlareRules.ReplyOf(people[0], arrest) == FlareRules.ReplyKind.Kind && FlareRules.ReplyOf(people[1], arrest) == FlareRules.ReplyKind.Kind, "Flare: everybody is kind about an arrest");
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        FlarePost p = FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Outing, 2, 10, 2, null, null, 1), FlareTone.Cheerful, 2, people, 2, 10, 600);
        Check((p.Likers & (1 << 3)) == 0 && (p.Repliers & (1 << 2)) != 0 && (p.Repliers & (1 << 4)) != 0, "Flare: a stranger does not follow; the tagged friend and the partner answer");
        Check(f.Unseen == FlareRules.Count(p.Likers) + FlareRules.Count(p.Repliers), "Flare: the friends' likes and replies are the badge");
    }

    private static void FlareDay()
    {
        var people = FlarePeople();
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        FlareDayReport r = FlareRules.Day(f, 2, 90, people);
        Check(f.Reputation == 54 && r.ReputationBefore == 50 && f.SettledDay == 2, "Flare: the reputation moves a tenth of the way to the city's opinion");
        FlareRules.Day(f, 3, 55, people);
        FlareRules.Day(f, 4, 55, people);
        Check(f.Reputation == 55, "Flare: a gap of under ten still moves by one, and stops when it is reached (" + f.Reputation + ")");
        Check(r.Following == 4 && (f.Following & (1 << 3)) == 0, "Flare: acquaintances and closer follow, a stranger does not");
        f.Followers = 1000;
        f.LastPostDay = 1;
        r = FlareRules.Day(f, 7, 55, people);
        Check(r.Lost == 0 && f.Followers == 1000, "Flare: six days of silence cost nothing");
        r = FlareRules.Day(f, 8, 55, people);
        Check(r.Lost == 10 && f.Followers == 990 && f.History[FlareFile.HistoryDays - 1] == 990, "Flare: after a week, 1% a day drift away, and the chart follows");
        FlareFile tiny = FlareRules.Open(5, 1, 0, 50);
        FlareRules.Day(tiny, 30, 55, people);
        Check(tiny.Followers == FlareRules.StartExtra, "Flare: a new account never drifts below its first followers");
        bool someone = false;
        for (int d = 10; d < 40; d++)
        {
            someone |= FlareRules.Day(f, d, 55, people).FriendPosts > 0;
        }
        Check(someone, "Flare: friends post their own now and then");
        bool onlyFollowers = true;
        foreach (FlarePost p in f.Posts)
        {
            onlyFollowers &= p.Author == FlareAuthor.Friend && p.Who != 3 && p.Source == FlareSource.Life;
        }
        Check(onlyFollowers, "Flare: only friends who follow post in the Feed");
    }

    private static void FlareDrafts()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        Check(FlareRules.Draft(f, FlareSource.Voted, 3, 9, -1, null, "X", 0) != null && FlareRules.Draft(f, FlareSource.Voted, 3, 10, -1, null, "X", 0) == null, "Flare: the same thing is drafted once");
        for (int i = 0; i < 12; i++)
        {
            FlareRules.Draft(f, FlareSource.Photo, 3, 9, -1, null, "p" + i + ".png", 0);
        }
        Check(f.Drafts.Count == FlareRules.DraftsMost && f.Drafts[f.Drafts.Count - 1].Name == "p11.png", "Flare: eight drafts at most, the oldest goes");
        FlareRules.Day(f, 4, 50, null);
        Check(f.Drafts.Count == FlareRules.DraftsMost, "Flare: a draft can be posted the next day");
        FlareRules.Day(f, 5, 50, null);
        Check(f.Drafts.Count == 0, "Flare: and is gone after two days");
        Check(FlareRules.CivicNew(f, 1) == 1 && FlareRules.CivicNew(f, 3) == 2 && FlareRules.CivicNew(f, 3) == 0 && FlareRules.CivicNew(f, 0) == 0 && FlareRules.CivicNew(f, 1) == 1,
            "Flare: a ballot and an endorsement are drafted once an election");
        f.CareerKey = -1;
        Check(!FlareRules.Promoted(f, 300) && FlareRules.Promoted(f, 301) && !FlareRules.Promoted(f, 301) && !FlareRules.Promoted(f, 502) && !FlareRules.Promoted(f, -1),
            "Flare: a rank up with the same employer is a promotion; a new job is not");
        FlareFile g = FlareRules.Open(5, 10, 0, 50);
        Check(FlareRules.ShopDay(g, "A~1", 0, 10) == FlareSource.None && FlareRules.ShopDay(g, "B~2", 0, 11) == FlareSource.Opened, "Flare: a business there from the start is no news; one opened later is");
        FlareSource news = FlareSource.None;
        for (int d = 12; d < 16; d++)
        {
            news = FlareRules.ShopDay(g, "B~2", 1000, d);
        }
        Check(news == FlareSource.None && FlareRules.ShopDay(g, "B~2", 1199, 16) == FlareSource.None && FlareRules.ShopDay(g, "B~2", 1500, 17) == FlareSource.RecordDay,
            "Flare: a record day is a fifth better than the best before, after three days of figures");
    }

    private static void FlareKept()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        var people = FlarePeople();
        for (int i = 0; i < 100; i++)
        {
            FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Photo, i, 10, -1, null, "p.png", 0), FlareTone.Cheeky, -1, people, i, 10, 600);
            FlareRules.Day(f, i + 1, 50, people);
        }
        Check(f.OwnPosts == FlareRules.OwnMost && f.Posts.Count <= FlareRules.PostsMost && f.TotalPosts == 100, "Flare: thirty posts of the player's kept, sixty in all, the rest in the totals ("
              + f.OwnPosts + ", " + f.Posts.Count + ")");
    }

    private static void FlareNotices()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        Check(FlareRules.MayNotify(f, 3, true) && !FlareRules.MayNotify(f, 3, true) && FlareRules.MayNotify(f, 4, true), "Flare: one notification a day at most");
        Check(!FlareRules.MayNotify(f, 5, false) && FlareRules.MayNotify(f, 5, true), "Flare: none at all with the option off, and the day's one is not spent");
    }

    private static void FlareSave()
    {
        var people = FlarePeople();
        FlareFile f = FlareRules.Open(424242, 3, 2, 61);
        FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Opened, 5, 10, -1, "Main%20St~12", "Copper | Kettle", 0), FlareTone.Proud, -1, people, 5, 10, 600);
        FlareRules.Draft(f, FlareSource.Outing, 5, 14, 2, null, null, 3);
        FlareRules.ShopDay(f, "Main%20St~12", 900, 5);
        FlareRules.Day(f, 6, 61, people);
        f.CivicSeen = 3;
        f.CareerKey = 402;
        f.NoticeDay = 6;
        string account = FlareStore.WriteAccount(f), posts = FlareStore.WritePosts(f);
        FlareFile b = FlareStore.Read(account, posts);
        Check(b != null && FlareStore.WriteAccount(b) == account && FlareStore.WritePosts(b) == posts, "Flare: the save's two lines read back exactly");
        Check(b != null && b.Followers == f.Followers && b.Reputation == f.Reputation && b.Posts.Count == f.Posts.Count && b.Drafts.Count == 1 && b.Drafts[0].Person == 2 && b.Shops.Count == 1
              && b.CivicSeen == 3 && b.CareerKey == 402 && b.NoticeDay == 6 && b.History[6] == f.History[6] && b.Seed == 424242,
            "Flare: every figure survives the save");
        Check(b != null && b.Posts.Count > 0 && b.Posts[0].Name == "Copper / Kettle", "Flare: a name with the separator in it is kept readable");
        string later = posts + "\nP|900|6|10|0|-1|99|0|-1|||0|1|1|1|1|1|0|0|0|0" + "\nP|901|6|10|99|-1|1|0|-1|||0|1|1|1|1|1|0|0|0|0";
        FlareFile c = FlareStore.Read(account + "\nD|950|99|6|10|-1|||0\nX|whatever", later);
        Check(c != null && c.Posts.Count == f.Posts.Count && c.Drafts.Count == 1, "Flare: a post, an author or a draft of a later version is left out, never read as something else");
        Check(FlareStore.Read("FL9|1|2|3", null) == null && FlareStore.Read("rubbish", "rubbish") == null && FlareStore.Read(null, null) == null && FlareStore.Load(new Dictionary<string, string>()) == null,
            "Flare: another version's line, a damaged one or none is a new account");
        var data = new Dictionary<string, string>();
        FlareStore.Save(data, f);
        Check(data.ContainsKey(FlareStore.AccountKey) && data.ContainsKey(FlareStore.PostsKey) && FlareStore.AccountKey.StartsWith("reallife:flare_") && FlareStore.PostsKey.StartsWith("reallife:flare_"),
            "Flare: the save's keys are reallife:flare_*");
    }

    private static void FlareTiming()
    {
        var people = FlarePeople();
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        var clock = Stopwatch.StartNew();
        for (int day = 2; day < 2000; day++)
        {
            FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Selfie, day, 10, 2, null, "s.png", 0), FlareTone.Cheerful, 2, people, day, 10, 600);
            FlareRules.Day(f, day + 1, 50, people);
        }
        string a = FlareStore.WriteAccount(f), p = FlareStore.WritePosts(f);
        FlareStore.Read(a, p);
        clock.Stop();
        Check(clock.ElapsedMilliseconds < 1500, "Flare: two thousand days of posts and rounds, and a save, in under 1.5 s (" + clock.ElapsedMilliseconds + " ms)");
    }
}
