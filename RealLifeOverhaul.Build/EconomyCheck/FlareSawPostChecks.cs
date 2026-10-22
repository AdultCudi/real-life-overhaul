using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Flare;

// A city person's message on Flare that says "saw your post earlier" comes only after a real post of the player's: the
// account's posts are read, not a day number that stood for "never posted" (-1) and so passed the three days' check on
// a new game's first two days. A reply under another's post, a sponsored post, a business's, a friend's or another
// player's post is no post of the player's; one made after the message's minute or more than three days before it is
// not "earlier". With no such post the city writes no chat, and the tests' messages open with a hello that claims none.
internal static partial class Program
{
    private static void FlareSawPost()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        SawPostNewGame();
        SawPostNeverPosted();
        SawPostRealPost();
        SawPostNotPosts();
        SawPostOpener();
        SawPostTexts();
        SawPostSources();
        SawPostTiming();
        Console.WriteLine("  flare saw your post: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A new game starts on day 1 (the game's SaveGameManager), the account opened that day with no post.
    private static FlareFile SawPostFile(int seed)
    {
        FlareFile f = FlareRules.Open(seed, 1, 3, 50);
        f.CastSeed = seed * 31 + 7;
        f.Net.CitySeed = f.CastSeed;
        return f;
    }

    private static FlarePost SawPostOwn(int id, int day, int hour, FlareSource source)
    {
        return new FlarePost { Id = id, Day = day, Hour = hour, Minute = 0, Author = FlareAuthor.Player, Source = source };
    }

    // 1. The owner's case: a new game, nothing posted, a city person writes on day 1 or 2.
    private static void SawPostNewGame()
    {
        int chats = 0, written = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            for (int day = 1; day <= 2; day++)
            {
                FlareFile f = SawPostFile(seed);
                if (FlareLiveRules.CityDm(f, day, false, out string with, out string what, out long at))
                {
                    written++;
                    chats += what == "chat" ? 1 : 0;
                }
            }
        }
        Check(chats == 0, "flare saw your post: on a new game's first two days with nothing posted, no city person writes that they saw a post (" + chats + " of 800 accounts' days did; "
                          + written + " messages of other kinds)");
        FlareFile never = SawPostFile(7);
        Check(never.LastPostDay == -1 && never.Posts.Count == 0 && !FlareLiveRules.PostedBefore(never, FlareLiveRules.Now(1, 12, 0))
              && !FlareLiveRules.PostedBefore(never, FlareLiveRules.Now(2, 19, 59)), "flare saw your post: an account that never posted has no post before any minute of its first days");
    }

    // 2. Never posted, a thousand days: never a chat.
    private static void SawPostNeverPosted()
    {
        int chats = 0, dms = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            FlareFile f = SawPostFile(seed);
            for (int day = 1; day <= 1000; day++)
            {
                if (FlareLiveRules.CityDm(f, day, day % 5 == 0, out _, out string what, out _))
                {
                    dms++;
                    chats += what == "chat" ? 1 : 0;
                }
            }
        }
        Check(chats == 0 && dms > 0, "flare saw your post: an account that never posts gets no chat in a thousand days on twenty accounts (" + chats + " chats, " + dms + " other messages)");
    }

    // 3. A real post: a chat may follow within three days, never before it and never after them.
    private static void SawPostRealPost()
    {
        int chats = 0, early = 0, late = 0, lateChats = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            for (int day = 6; day <= 8; day++)
            {
                FlareFile f = SawPostFile(seed);
                FlarePost p = SawPostOwn(1, 5, 9, FlareSource.Selfie);
                f.Posts.Add(p);
                f.LastPostDay = 5;
                if (FlareLiveRules.CityDm(f, day, false, out _, out string what, out long at) && what == "chat")
                {
                    chats++;
                    early += at < p.At ? 1 : 0;
                    late += at - p.At > FlareLiveRules.ChatPostMinutes ? 1 : 0;
                }
            }
            FlareFile old = SawPostFile(seed);
            old.Posts.Add(SawPostOwn(1, 5, 9, FlareSource.Selfie));
            old.LastPostDay = 5;
            if (FlareLiveRules.CityDm(old, 9, false, out _, out string oldWhat, out long oldAt) && oldWhat == "chat")
            {
                lateChats += oldAt - old.Posts[0].At > FlareLiveRules.ChatPostMinutes ? 1 : 0;
            }
        }
        Check(chats > 100 && early == 0 && late == 0, "flare saw your post: after a selfie on day 5 the city's chats of days 6 to 8 all come after it and within three days (" + chats + " chats)");
        Check(lateChats == 0, "flare saw your post: a post more than three days before the message's minute is not \"earlier\" (" + lateChats + " chats about it)");
        FlareFile f2 = SawPostFile(3);
        f2.Posts.Add(SawPostOwn(1, 5, 15, FlareSource.Photo));
        long morning = FlareLiveRules.Now(5, 10, 30), evening = FlareLiveRules.Now(5, 18, 0);
        Check(!FlareLiveRules.PostedBefore(f2, morning) && FlareLiveRules.PostedBefore(f2, evening), "flare saw your post: a post at 15:00 is not before a message at 10:30 the same day, and is before one at 18:00");
        Check(FlareLiveRules.PostedBefore(f2, FlareLiveRules.Now(5, 15, 0) + FlareLiveRules.ChatPostMinutes) && !FlareLiveRules.PostedBefore(f2, FlareLiveRules.Now(5, 15, 0) + FlareLiveRules.ChatPostMinutes + 1),
            "flare saw your post: a post counts up to three days to the minute, and not a minute more");
        FlareFile a = SawPostFile(11), b = SawPostFile(11);
        a.Posts.Add(SawPostOwn(1, 5, 9, FlareSource.Outing));
        b.Posts.Add(SawPostOwn(1, 5, 9, FlareSource.Outing));
        bool sameA = FlareLiveRules.CityDm(a, 6, false, out string withA, out string whatA, out long atA);
        bool sameB = FlareLiveRules.CityDm(b, 6, false, out string withB, out string whatB, out long atB);
        Check(sameA == sameB && withA == withB && whatA == whatB && atA == atB, "flare saw your post: the same account and day give the same message on every machine");
    }

    // 4. What is not a post of the player's.
    private static void SawPostNotPosts()
    {
        long at = FlareLiveRules.Now(6, 12, 0);
        var others = new List<FlarePost>
        {
            new FlarePost { Id = 1, Day = 6, Hour = 9, Author = FlareAuthor.Player, Source = FlareSource.Reply, ReplyTo = 40 },     // a reply under another's post
            new FlarePost { Id = 2, Day = 6, Hour = 9, Author = FlareAuthor.Player, Source = FlareSource.Sponsored },               // the company's campaign
            new FlarePost { Id = 3, Day = 6, Hour = 9, Author = FlareAuthor.Business, Source = FlareSource.BusinessPost },          // a business's own post
            new FlarePost { Id = 4, Day = 6, Hour = 9, Author = FlareAuthor.Friend, Source = FlareSource.Life, Tag = 0 },           // a friend's post, the player tagged
            new FlarePost { Id = 5, Day = 6, Hour = 9, Author = FlareAuthor.Network, Source = FlareSource.Photo },                  // another player's in Going Public
            new FlarePost { Id = 6, Day = 6, Hour = 9, Author = FlareAuthor.Citizen, Source = FlareSource.Review },                 // a city person's review
            new FlarePost { Id = 7, Day = 6, Hour = 9, Author = FlareAuthor.Influencer, Source = FlareSource.Visit },               // an influencer at the player's shop
            new FlarePost { Id = 8, Day = 6, Hour = 9, Author = FlareAuthor.Official, Source = FlareSource.Answer, ReplyTo = 41 }   // the City Office's answer
        };
        bool none = true;
        for (int i = 0; i < others.Count; i++)
        {
            FlareFile f = SawPostFile(5);
            f.Posts.Add(others[i]);
            none &= !FlareLiveRules.PostedBefore(f, at);
        }
        FlareFile all = SawPostFile(5);
        all.Posts.AddRange(others);
        Check(none && !FlareLiveRules.PostedBefore(all, at), "flare saw your post: a reply, a sponsored post, a business's, a friend's that tags the player, another player's, a review, an influencer's and an answer are not the player's posts");
        FlareFile report = SawPostFile(5);
        report.Posts.Add(SawPostOwn(1, 6, 9, FlareSource.Report));
        FlareFile own = SawPostFile(5);
        own.Posts.Add(SawPostOwn(1, 6, 9, FlareSource.Opened));
        Check(FlareLiveRules.PostedBefore(report, at) && FlareLiveRules.PostedBefore(own, at), "flare saw your post: a report the player posted and a post about their new business are their posts");
    }

    // 5. The tests' opener: a chat after a post, a hello without.
    private static void SawPostOpener()
    {
        FlareFile f = SawPostFile(9);
        long at = FlareLiveRules.Now(1, 12, 0);
        string none = FlareLiveRules.Opener(f, at);
        f.Posts.Add(SawPostOwn(1, 1, 11, FlareSource.Selfie));
        string after = FlareLiveRules.Opener(f, at);
        Check(none == "hello" && after == "chat", "flare saw your post: a message that must be written opens with a hello without a post (" + none + ") and a chat after one (" + after + ")");
    }

    // 6. The texts: the hello claims no post, in the three languages.
    private static void SawPostTexts()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        const string Key = "reallife_flare_dm_hello";
        bool there = en.ContainsKey(Key) && pt.ContainsKey(Key) && zh.ContainsKey(Key);
        Check(there && en[Key].IndexOf("post", StringComparison.OrdinalIgnoreCase) < 0 && pt[Key].IndexOf("post", StringComparison.OrdinalIgnoreCase) < 0 && zh[Key].IndexOf("帖", StringComparison.Ordinal) < 0,
            "flare saw your post: the hello is in en, pt and zh-cn and speaks of no post");
        Check(en.ContainsKey("reallife_flare_dm_chat") && en["reallife_flare_dm_chat"].Contains("saw your post"), "flare saw your post: the chat that names a post is kept for after a real one");
    }

    // 7. The sources: every opener of the city's people goes through the rule.
    private static void SawPostSources()
    {
        string feed = ModSource(@"Social\Flare\FlareLiveRules.Feed.cs");
        Check(feed.Contains("what = PostedBefore(f, at) ? \"chat\" : null;") && !feed.Contains("LastPostDay >= day - 3"),
            "flare saw your post: the city's chat is decided by the player's real posts, not by the last post's day");
        string text = ModSource(@"Social\Flare\FlareText.Live.cs");
        Check(text.Contains("case \"chat\": return \"reallife_flare_dm_chat\";") && text.Contains("default: return \"reallife_flare_dm_hello\";"),
            "flare saw your post: only a chat's key names a post; any other first message is the hello");
        string tests = ModSource(@"Social\Flare\FlareDirector.Tests.cs"), invites = ModSource(@"Social\Flare\FlareDirector.Invites.cs"), live = ModSource(@"Social\Flare\FlareDirector.Live.cs");
        Check(!tests.Contains("\"reallife_flare_dm_chat\"") && !invites.Contains("\"reallife_flare_dm_chat\"") && tests.Contains("FlareText.DmKey(FlareLiveRules.Opener(_file, Now))")
              && invites.Contains("FlareText.DmKey(FlareLiveRules.Opener(_file, Now))") && live.Contains("FlareLiveRules.CityDm(_file, day, hiring, out with, out what, out at)"),
            "flare saw your post: the tests' messages and the city's daily one take their opener from the rule");
    }

    // 8. The cost: once a day, a walk over the posts kept.
    private static void SawPostTiming()
    {
        FlareFile f = SawPostFile(13);
        for (int i = 0; i < FlareRules.PostsMost; i++)
        {
            f.Posts.Add(new FlarePost { Id = i + 1, Day = 1, Hour = 0, Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk });
        }
        long at = FlareLiveRules.Now(400, 12, 0);
        FlareLiveRules.PostedBefore(f, at);
        var watch = Stopwatch.StartNew();
        bool any = false;
        for (int i = 0; i < 1000; i++)
        {
            any |= FlareLiveRules.PostedBefore(f, at);
        }
        double each = watch.Elapsed.TotalMilliseconds / 1000;
        Check(!any && each < 0.2, "flare saw your post: the check over a full feed of " + FlareRules.PostsMost + " posts takes " + each.ToString("0.0000") + " ms, once a day (under 0.2 ms)");
    }
}
