using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Flare;

// Posting on Flare without a limit (however much the player posts): the two game hours between
// posts and the three a day are gone, and so are the three reports and twelve replies a day; many posts in a row are all
// made and shown; what they could move stays bounded (the account by the day's first three, a business or the City Office
// by the first of the same thing a day, the council by the day's first three reports, the paper's pile-on once a day);
// in Going Public every post is queued and sent a second apart, none dropped, inside the host's bound, and a picture past
// the day's four goes without it, the player told once; the save and the time stay small with 500 posts a day; the texts.
internal static partial class Program
{
    private static void FlareNoLimit()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        NoLimitPostsInARow();
        NoLimitAccountBounded();
        NoLimitBusinessOnce();
        NoLimitReportsAndPaper();
        NoLimitOutbox();
        NoLimitPictures();
        NoLimitSaveSize();
        NoLimitTiming();
        NoLimitSources();
        NoLimitWords();
        NoLimitReviewFixes();
        Console.WriteLine("  flare no limit: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static FlareDraft NoLimitDraft(FlareFile f, FlareSource source, int day, int arg)
    {
        return new FlareDraft { Id = -1, Source = source, Day = day, Hour = 10, Person = -1, Arg = arg };
    }

    // A day of 'count' posts in a row, a minute apart from 10:00, of the kinds a player makes by hand; the posts made.
    private static List<FlarePost> NoLimitDay(FlareFile f, int day, int count)
    {
        var made = new List<FlarePost>(count);
        FlareSource[] kinds = { FlareSource.Life, FlareSource.Photo, FlareSource.Selfie, FlareSource.Outing };
        for (int i = 0; i < count; i++)
        {
            int minute = Math.Min(1439, 600 + i);
            FlarePost p = FlareRules.Post(f, NoLimitDraft(f, kinds[i % kinds.Length], day, i), FlareTone.Cheerful, -1, null, day, minute / 60, minute);
            p.Minute = minute % 60;
            made.Add(p);
        }
        return made;
    }

    // ---------------------------------------------------------------- many posts, all made and shown

    private static void NoLimitPostsInARow()
    {
        FlareFile f = FlareRules.Open(7, 1, 10, 50);
        List<FlarePost> made = NoLimitDay(f, 5, 500);
        Check(made.All(p => p != null) && made.Select(p => p.Id).Distinct().Count() == 500 && f.TotalPosts == 500 && FlareRules.PostsToday(f, 5) == 500,
            "Flare no limit: 500 posts in a row on one day are all made, each its own (a minute apart, nothing refused)");
        FlareFile quick = FlareRules.Open(7, 1, 10, 50);
        int sameMinute = 0;
        for (int i = 0; i < 20; i++)
        {
            sameMinute += FlareRules.Post(quick, NoLimitDraft(quick, FlareSource.Life, 5, i), FlareTone.Cheerful, -1, null, 5, 10, 600) != null ? 1 : 0;
        }
        Check(sameMinute == 20 && quick.TotalPosts == 20, "Flare no limit: 20 posts in the same minute are all made (no gap between posts)");
        long now = FlareLiveRules.Now(5, 23, 59);
        int own = f.OwnPosts;
        bool newestKept = true;
        for (int i = made.Count - own; i < made.Count; i++)
        {
            newestKept &= f.Post(made[i].Id) != null && FlareLiveRules.Visible(made[i], now);
        }
        Check(own == FlareRules.OwnMost && newestKept && f.Posts[f.Posts.Count - 1].Id == made[made.Count - 1].Id,
            "Flare no limit: the newest " + FlareRules.OwnMost + " of them are in the feed and shown, the latest last; older ones are folded into the totals as the feed always did");
        Check(f.Posts.Count <= FlareRules.PostsMost, "Flare no limit: the feed stays within its " + FlareRules.PostsMost + " posts with 500 of the player's in a day (" + f.Posts.Count + ")");
        FlareFile next = FlareRules.Open(7, 1, 10, 50);
        NoLimitDay(next, 5, 10);
        Check(FlareRules.Counts(next, 6) && FlareRules.PostsToday(next, 6) == 0 && !FlareRules.Counts(next, 5), "Flare no limit: a new day's first posts count again");
    }

    // ---------------------------------------------------------------- the account: the day's first three

    private static void NoLimitAccountBounded()
    {
        FlareFile three = FlareRules.Open(7, 1, 10, 50), many = FlareRules.Open(7, 1, 10, 50);
        three.LastViralDay = many.LastViralDay = 5;   // (no luck in either: the gain alone)
        NoLimitDay(three, 5, FlareRules.PostsCounted);
        List<FlarePost> made = NoLimitDay(many, 5, 500);
        Check(three.Followers > 46 && many.Followers == three.Followers && many.Reputation == three.Reputation,
            "Flare no limit: 500 posts in a day move followers and reputation exactly as the first " + FlareRules.PostsCounted + " do (" + three.Followers + " and " + many.Followers + ")");
        bool after = true;
        for (int i = FlareRules.PostsCounted; i < made.Count; i++)
        {
            after &= made[i].Gained == 0 && !made[i].Viral && made[i].Reach >= 1;
        }
        Check(after && made[0].Gained > 0, "Flare no limit: a post past the day's first three gains nothing and cannot go viral, and is still seen and liked");
        FlareFile lucky = FlareRules.Open(7, 1, 10, 50);
        int virals = 0;
        for (int i = 0; i < 300; i++)
        {
            virals += FlareRules.Post(lucky, NoLimitDraft(lucky, FlareSource.Life, 5, i), FlareTone.Cheerful, -1, null, 5, 10, 600).Viral ? 1 : 0;
        }
        Check(virals <= 1, "Flare no limit: however many posts, at most one viral moment a week (" + virals + ")");
        FlareFile bad = FlareRules.Open(7, 1, 10, 50);
        bad.Followers = 10000;
        for (int i = 0; i < 200; i++)
        {
            FlareRules.Post(bad, NoLimitDraft(bad, FlareSource.Arrested, 5, i), FlareTone.Cheerful, -1, null, 5, 10, 600);
        }
        int lost = 10000 - bad.Followers;
        Check(lost > 0 && lost <= FlareRules.PostsCounted * (FlareRules.BadLoss(10000) + 1) && bad.Reputation >= 50 - FlareRules.PostsCounted * FlareRules.BadReputationKnown,
            "Flare no limit: posts that go badly cost the account only for the day's first three, however many (" + lost + " followers)");
        FlareFile test = FlareRules.Open(7, 1, 10, 50);
        NoLimitDay(test, 5, 10);
        Check(FlareRules.Post(test, NoLimitDraft(test, FlareSource.Life, 5, 99), FlareTone.Cheerful, -1, null, 5, 12, 720, true).Viral,
            "Flare no limit: the viral test still goes viral past the day's first three");
    }

    // ---------------------------------------------------------------- a business: once a day for the same thing

    private static void NoLimitBusinessOnce()
    {
        var n = new FlareNet();
        string review = FlareLiveRules.MoveKey("me", FlareSource.Review, 1, null, "Elm~1");
        bool first = FlareLiveRules.FirstToday(n, 5, review);
        int again = 0;
        for (int i = 0; i < 50; i++)
        {
            again += FlareLiveRules.FirstToday(n, 5, FlareLiveRules.MoveKey("me", FlareSource.Review, 1 + i % 5, null, "Elm~1")) ? 1 : 0;
        }
        Check(first && again == 0 && FlareLiveRules.MovedToday(n, 5, review), "Flare no limit: the player's reviews of one business move its customers once a day, however many and whatever the stars");
        Check(FlareLiveRules.FirstToday(n, 5, FlareLiveRules.MoveKey("me", FlareSource.Review, 5, null, "Oak~2"))
              && FlareLiveRules.FirstToday(n, 5, FlareLiveRules.MoveKey("p:guest", FlareSource.Review, 1, null, "Elm~1"))
              && FlareLiveRules.FirstToday(n, 6, review) && !FlareLiveRules.MovedToday(n, 6, FlareLiveRules.MoveKey("p:guest", FlareSource.Review, 1, null, "Elm~1")),
            "Flare no limit: another business, another player of the session and the next day each move it again");
        Check(FlareLiveRules.FirstToday(n, 6, FlareLiveRules.MoveKey("me", FlareSource.Announce, 0, null, "Elm~1")) && !FlareLiveRules.FirstToday(n, 6, FlareLiveRules.MoveKey("me", FlareSource.Announce, 0, null, "Elm~1"))
              && FlareLiveRules.FirstToday(n, 6, FlareLiveRules.MoveKey("me", FlareSource.Announce, 2, null, "Elm~1")),
            "Flare no limit: an announcement of a kind moves its business once a day; another kind is its own");
        Check(FlareLiveRules.MoveKey(null, FlareSource.Review, 1, null, "Elm~1") == null && FlareLiveRules.FirstToday(n, 6, null) && FlareLiveRules.FirstToday(n, 6, null)
              && FlareLiveRules.MoveKey("me", FlareSource.Life, 0, null, "Elm~1") == null,
            "Flare no limit: the city's own reviews are not held to it (the city's day sets how many), nor a post that moves no business");
        Check(FlareLiveRules.Mover(new FlarePost { Author = FlareAuthor.Player }) == "me" && FlareLiveRules.Mover(new FlarePost { Author = FlareAuthor.Network, AuthorId = "765" }) == "p:765"
              && FlareLiveRules.Mover(new FlarePost { Author = FlareAuthor.Citizen }) == null && FlareLiveRules.Mover(new FlarePost { Author = FlareAuthor.Network }) == null,
            "Flare no limit: the player is 'me', a player of the session is theirs, the city's people are nobody");
        var full = new FlareNet();
        for (int i = 0; i < FlareLiveRules.MovedMost * 3; i++)
        {
            FlareLiveRules.FirstToday(full, 9, FlareLiveRules.MoveKey("me", FlareSource.Review, 3, null, "Shop~" + i));
        }
        Check(full.Moved.Count == FlareLiveRules.MovedMost && full.MovedDay == 9, "Flare no limit: what moved today is kept to " + FlareLiveRules.MovedMost + " at most");
    }

    // ---------------------------------------------------------------- reports, the council and the paper

    private static void NoLimitReportsAndPaper()
    {
        var n = new FlareNet();
        int weighed = 0;
        for (int i = 0; i < 40; i++)
        {
            weighed += FlareLiveRules.Weighs(n, 5) ? 1 : 0;
        }
        Check(weighed == FlareLiveRules.ReportsWeighed && FlareLiveRules.Weighs(n, 6), "Flare no limit: 40 reports in a day add to the council's weight as the first " + FlareLiveRules.ReportsWeighed + " do, and a new day again");
        string road = FlareLiveRules.MoveKey("me", FlareSource.Report, (int)FlareReport.Road, "d1", null);
        Check(FlareLiveRules.FirstToday(n, 5, road) && !FlareLiveRules.FirstToday(n, 5, road) && FlareLiveRules.FirstToday(n, 5, FlareLiveRules.MoveKey("me", FlareSource.Report, (int)FlareReport.Road, "d2", null))
              && FlareLiveRules.FirstToday(n, 5, FlareLiveRules.MoveKey("me", FlareSource.Report, (int)FlareReport.Crime, "d1", null)),
            "Flare no limit: the same report again the same day files nothing more; another district or another kind does");
        bool again;
        FlareCase c = FlareLiveRules.File(n, FlareReport.Business, "d1", "Elm~1", "Elm", 0f, 0f, 100, "me", 1, 3, out again);
        FlareCase open = FlareLiveRules.OpenCase(n, FlareReport.Business, "d1", "Elm~1");
        Check(!again && open == c && c.Repeats == 0 && FlareLiveRules.OpenCase(n, FlareReport.Business, "d1", "Oak~2") == null && FlareLiveRules.OpenCase(n, FlareReport.Road, "d1", null) == null,
            "Flare no limit: the case a report again joins is found without a repeat counted (the tally and the paper stay as they were)");
        FlareLiveRules.File(n, FlareReport.Business, "d1", "Elm~1", "Elm", 0f, 0f, 120, "c3", 2, 3, out again);
        Check(again && c.Repeats == 1, "Flare no limit: a report of the city's people about the same thing still counts as a repeat, as before");
        int stories = 0;
        for (int i = 0; i < 30; i++)
        {
            stories += FlareLiveRules.StoryOnce(n, 6, "reallife_flare_news_pileon", "name", "A", "handle", "@a") ? 1 : 0;
        }
        Check(stories == 1 && n.Stories.Count(s => s.Key == "reallife_flare_news_pileon") == 1 && FlareLiveRules.StoryOnce(n, 7, "reallife_flare_news_pileon", "name", "A", "handle", "@a"),
            "Flare no limit: 30 posts gone badly put one pile-on in the next morning's paper, not thirty");
    }

    // ---------------------------------------------------------------- Going Public: queued, a second apart, none dropped

    private static void NoLimitOutbox()
    {
        var box = new FlareOutbox();
        for (int i = 0; i < 500; i++)
        {
            box.Add("P|" + (i + 1) + "|11|0|0");
            box.Add("L|" + (i + 1) + "|1");
        }
        int posts = box.Count - FlareOutbox.Most;
        Check(posts == 500, "Flare no limit: 500 posts asked at once with 500 likes: every post is kept, only the oldest likes past " + FlareOutbox.Most + " go (" + box.Count + " waiting)");
        var host = new FlareRateBook();
        var got = new List<int>();
        int seconds = 0, refused = 0, mostInASecond = 0, mostLines = 0;
        while (box.Count > 0 && seconds < 5000)
        {
            int inSecond = 0, lines = 0;
            float now = 100f + seconds;
            box.Send(line =>
            {
                lines++;
                if (line[0] == 'P')
                {
                    inSecond++;
                    if (!host.Allow("guest", 'P', now, 5))
                    {
                        refused++;
                    }
                    got.Add(int.Parse(line.Split('|')[1]));
                }
                return true;
            }, FlareNetShare.LinesMost);
            mostInASecond = Math.Max(mostInASecond, inSecond);
            mostLines = Math.Max(mostLines, lines);
            seconds++;
        }
        bool inOrder = got.Count == 500;
        for (int i = 0; inOrder && i < got.Count; i++)
        {
            inOrder &= got[i] == i + 1;
        }
        Check(inOrder && refused == 0, "Flare no limit: all 500 posts reach the host, in order, none refused by its bounds (" + got.Count + " sent, " + refused + " refused)");
        Check(mostInASecond == FlareOutbox.PostsEach && mostLines <= FlareNetShare.LinesMost && seconds >= 500 && seconds <= 520,
            "Flare no limit: a post a second, within the request's " + FlareNetShare.LinesMost + " lines; 500 posts take about 500 seconds (" + seconds + ")");
        var flood = new FlareRateBook();
        int allowed = 0;
        for (int i = 0; i < FlareRateBook.PostsWindow + 50; i++)
        {
            allowed += flood.Allow("flood", 'P', 10f + i * 0.1f, 5) ? 1 : 0;
        }
        Check(allowed == FlareRateBook.PostsWindow, "Flare no limit: a guest sending far faster than a post a second is still bounded at the host (" + FlareRateBook.PostsWindow + " in ten minutes)");
        var day = new FlareRateBook();
        int allDay = 0;
        for (int i = 0; i < 2000; i++)
        {
            allDay += day.Allow("guest", 'P', i * 1f, 5) ? 1 : 0;
        }
        Check(allDay == 2000, "Flare no limit: a post a second all day is never refused (no posts a day bound; 2,000 taken)");
        var held = new FlareOutbox();
        held.Add("P|1|11");
        held.Add("P|2|11");
        held.Add("L|3|1");
        var first = new List<string>();
        held.Send(l => { first.Add(l); return true; }, 16);
        held.Send(l => false, 16);
        Check(first.Count == 2 && first[0] == "P|1|11" && first[1] == "L|3|1" && held.Count == 1, "Flare no limit: the second post waits for the next second while a like behind it goes; a full request keeps it");
    }

    private static void NoLimitPictures()
    {
        var n = new FlareNet();
        var ways = new List<FlareRateBook.PictureWay>();
        for (int i = 0; i < 9; i++)
        {
            ways.Add(FlareRateBook.Picture(n, 5));
        }
        int send = ways.Count(w => w == FlareRateBook.PictureWay.Send), told = ways.Count(w => w == FlareRateBook.PictureWay.TellWithout);
        Check(send == FlareRateBook.PicturesDay && told == 1 && ways[FlareRateBook.PicturesDay] == FlareRateBook.PictureWay.TellWithout && ways[8] == FlareRateBook.PictureWay.Without,
            "Flare no limit: the day's four pictures go; the fifth post goes without its picture and the player is told, once; later ones quietly");
        Check(FlareRateBook.Picture(n, 6) == FlareRateBook.PictureWay.Send, "Flare no limit: a new day's pictures go again");
    }

    // ---------------------------------------------------------------- the save with 500 posts a day

    private static void NoLimitSaveSize()
    {
        FlareFile few = FlareRules.Open(7, 1, 10, 50), many = FlareRules.Open(7, 1, 10, 50);
        for (int day = 2; day < 32; day++)
        {
            NoLimitDay(few, day, FlareRules.OwnMost);
            NoLimitDay(many, day, 500);
            FlareRules.Day(few, day + 1, 50, null);
            FlareRules.Day(many, day + 1, 50, null);
            for (int k = 0; k < 500; k++)
            {
                FlareLiveRules.FirstToday(many.Net, day, FlareLiveRules.MoveKey("me", FlareSource.Review, 3, null, "Shop~" + k));
            }
        }
        int a = FlareStore.WritePosts(few).Length + FlareStore.WriteAccount(few).Length;
        int b = FlareStore.WritePosts(many).Length + FlareStore.WriteAccount(many).Length;
        string net = FlareLiveStore.Write(many.Net);
        Check(many.OwnPosts <= FlareRules.OwnMost && many.Posts.Count <= FlareRules.PostsMost && b <= a + 512,
            "Flare no limit: 30 days of 500 posts a day save no more than days of " + FlareRules.OwnMost + " (" + b + " characters against " + a + ")");
        Check(net.Length < 16 * 1024 && many.Net.Moved.Count == FlareLiveRules.MovedMost, "Flare no limit: what moved today stays a small part of the save (" + net.Length + " characters)");
        var back = new FlareNet();
        FlareLiveStore.Read(back, net);
        Check(back.MovedDay == many.Net.MovedDay && back.Moved.Count == many.Net.Moved.Count && back.Moved[0] == many.Net.Moved[0]
              && !FlareLiveRules.FirstToday(back, back.MovedDay, many.Net.Moved[5]),
            "Flare no limit: what moved today reads back from the save, so a reload does not move a business twice");
        string older = net.Substring(0, net.IndexOf('\n') < 0 ? net.Length : net.IndexOf('\n'));
        older = older.Substring(0, older.LastIndexOf('|'));
        var old = new FlareNet();
        FlareLiveStore.Read(old, older);
        Check(old.MovedDay == -1 && old.Moved.Count == 0 && old.NextCase == many.Net.NextCase, "Flare no limit: a save from before (no moved day) still loads");
        FlareFile round = FlareStore.Read(FlareStore.WriteAccount(many), FlareStore.WritePosts(many));
        Check(round != null && round.Posts.Count == many.Posts.Count && round.TotalPosts == many.TotalPosts && round.PostsToday == many.PostsToday,
            "Flare no limit: the account and its posts read back after 15,000 posts (" + (round != null ? round.TotalPosts : -1) + " made)");
    }

    // ---------------------------------------------------------------- the time

    private static void NoLimitTiming()
    {
        double day = NoLimitDayCost();
        Check(day < 60.0, "Flare no limit: a day of 500 posts costs " + day.ToString("0.0") + " ms in all (each post on its press, nothing per frame)");
        Console.WriteLine("  Flare no limit: 500 posts in a day " + day.ToString("0.0") + " ms in all");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double NoLimitDayCost()
    {
        const int rounds = 5;
        var watch = new Stopwatch();
        for (int r = 0; r < rounds; r++)
        {
            FlareFile f = FlareRules.Open(30 + r, 1, 10, 50);
            watch.Start();
            NoLimitDay(f, 5, 500);
            watch.Stop();
        }
        return watch.Elapsed.TotalMilliseconds / rounds;
    }

    // ---------------------------------------------------------------- the sources

    private static void NoLimitSources()
    {
        string flare = ReviewSource("Social/Flare/FlareRules.cs") + ReviewSource("Social/Flare/FlareDirector.cs") + ReviewSource("Social/Flare/FlareDirector.Live.cs")
                       + ReviewSource("Social/Flare/FlareDirector.Share.cs") + ReviewSource("Social/Flare/FlareApp.Sheet.cs") + ReviewSource("Social/Flare/FlareApp.Side.cs")
                       + ReviewSource("Social/Flare/FlareLiveRules.cs") + ReviewSource("Social/Flare/FlareLiveRules.CityOffice.cs")
                       + ReviewSource("Social/Flare/FlareLiveRules.Feed.cs") + ReviewSource("Social/Flare/FlareNetShare.cs") + ReviewSource("Social/Flare/FlareOutbox.cs")
                       + ReviewSource("Social/Flare/FlareRateBook.cs") + ReviewSource("Social/Flare/FlarePieceBook.cs") + ReviewSource("Meta/FeatureList.cs");
        string[] gone = { "CanPost(", "NextPostMinute(", "PostGapMinutes", "PostsPerDay", "RepliesPerDay", "ReportsPerDay", "ReportedToday(", "Wait.DayFull", "Wait.TooSoon", "PostsDay = 40" };
        string left = gone.FirstOrDefault(flare.Contains);
        Check(flare.Length > 100000 && left == null, "Flare no limit: nothing in Flare waits two hours, counts three a day or refuses a report or a reply" + (left != null ? " (" + left + ")" : string.Empty));
        string live = ReviewSource("Social/Flare/FlareDirector.Live.cs");
        string compose = FlareMethod(live, "private FlarePost Built(") + FlareMethod(live, "private FlarePost Report(") + FlareMethod(live, "private FlarePost Reply(");
        Check(compose.Length > 1000 && !compose.Contains("reallife_flare_wait") && !compose.Contains("compose_reported") && !compose.Contains("compose_replies"),
            "Flare no limit: a post, a report and a reply are refused only when they are not whole");
        string effect = FlareMethod(live, "private void BusinessEffect(");
        string file = FlareMethod(live, "private FlareCase FileCase(");
        string report = FlareMethod(live, "private FlarePost Report(");
        Check(effect.IndexOf("FlareLiveRules.FirstToday(", StringComparison.Ordinal) >= 0 && effect.IndexOf("FirstToday(", StringComparison.Ordinal) < effect.IndexOf("Buzz(p.Place", StringComparison.Ordinal)
              && file.Contains("FlareLiveRules.FirstToday(") && file.IndexOf("FirstToday(", StringComparison.Ordinal) < file.IndexOf("FlareLiveRules.File(", StringComparison.Ordinal)
              && report.Contains("FlareLiveRules.Weighs("),
            "Flare no limit: a review's or an announcement's customers, a report's case and its weight in the council each go through their once-a-day bound before they move");
        string repeat = FlareMethod(live, "private void RepeatAnswer(");
        Check(file.Contains("RepeatAnswer(p, kind)") && repeat.Contains("FlareAnswer.Received") && repeat.Contains("ReplyTo = report.Id")
              && !repeat.Contains("Complained(") && !repeat.Contains("Repeats") && !repeat.Contains("Act(") && !repeat.Contains("c.Answer"),
            "Flare no limit: the same report again the same day is answered as noted, and moves no case, tally or crew");
        string postLive = FlareMethod(live, "private void PostLive(");
        Check(postLive.Contains("FlareLiveRules.StoryOnce(") && !postLive.Contains("FlareLiveRules.Story(_file"), "Flare no limit: a pile-on reaches the paper once a day");
        string share = ReviewSource("Social/Flare/FlareDirector.Share.cs");
        Check(FlareMethod(share, "private bool PictureAllowed(").Contains("FlareRateBook.Picture(") && FlareMethod(share, "private bool PictureAllowed(").Contains("reallife_flare_note_pictures_day"),
            "Flare no limit: a picture past the day's four is left off its post, and the player told");
        string side = ReviewSource("Social/Flare/FlareApp.Side.cs");
        Check(side.Contains("reallife_flare_side_standing") && !side.Contains("PostsToday"), "Flare no limit: the account's side shows no count of today's posts");
    }

    // ---------------------------------------------------------------- the review's fixes: the mood, the cases kept, friends, talk, posts on their way

    private static void NoLimitReviewFixes()
    {
        var n = new FlareNet();
        int counted = 0;
        for (int i = 0; i < 25; i++)
        {
            counted += FlareLiveRules.Counted(n, 5, "me") ? 1 : 0;
        }
        Check(counted == FlareLiveRules.ReportsWeighed && FlareLiveRules.Counted(n, 5, "p:guest") && FlareLiveRules.Counted(n, 5, null) && FlareLiveRules.Counted(n, 6, "me"),
            "Flare no limit: of 25 reports in a day only the first three move the mood, the tally and the paper; each player alone, the city's always, a new day again");
        bool again;
        FlareCase quiet = FlareLiveRules.File(n, FlareReport.Litter, "d3", null, "x", 0f, 0f, 100, "me", 1, 3, out again, false);
        bool wasQuiet = !quiet.Counted;
        FlareLiveRules.File(n, FlareReport.Litter, "d3", null, "x", 0f, 0f, 110, "c4", 2, 3, out again);
        Check(wasQuiet && again && quiet.Counted, "Flare no limit: a case filed past the three moves nothing, until a counted report says the same");
        var s = new FlareNet();
        FlareLiveRules.File(s, FlareReport.Road, "d1", null, "x", 0f, 0f, 100, "me", 1, 3, out again, false);
        string text = FlareLiveStore.Write(s);
        var back = new FlareNet();
        FlareLiveStore.Read(back, text);
        string line = text.Split('\n').First(l => l.StartsWith("C|", StringComparison.Ordinal));
        var old = new FlareNet();
        string older = line;
        for (int k = 0; k < 3; k++)
        {
            older = older.Substring(0, older.LastIndexOf('|'));   // (the case's last fields: Counted, and the city's faults' verdict and fault after it)
        }
        FlareLiveStore.Read(old, text.Replace(line, older));
        Check(back.Cases.Count == 1 && !back.Cases[0].Counted && old.Cases.Count == 1 && old.Cases[0].Counted,
            "Flare no limit: a case's counted mark reads back from the save; a case from an older save reads as counted");
        var host = new FlareNet();
        for (int i = 0; i < FlareNet.CasesMost; i++)
        {
            FlareLiveRules.File(host, FlareReport.Business, "d1", "Shop~" + i, "x", 0f, 0f, 100 + i, "p:a", i, 3, out again);
        }
        FlareLiveRules.File(host, FlareReport.Business, "d1", "New~1", "x", 0f, 0f, 500, "p:b", 99, 3, out again);
        bool aKept = host.Cases.Count(c => c.Reporter == "p:a") == FlareNet.CasesMost;
        var mixed = new FlareNet();
        for (int i = 0; i < FlareNet.CasesMost; i++)
        {
            FlareLiveRules.File(mixed, FlareReport.Business, "d1", "Shop~" + i, "x", 0f, 0f, 100 + i, i == 7 ? "me" : "p:a", i, 3, out again);
        }
        FlareLiveRules.File(mixed, FlareReport.Business, "d1", "New~1", "x", 0f, 0f, 500, "p:b", 99, 3, out again);
        Check(aKept && host.Cases.Count == FlareNet.CasesMost && mixed.Cases.Count(c => c.Reporter == "p:a") == FlareNet.CasesMost - 1 && mixed.Cases.All(c => c.Reporter != "me")
              && mixed.Cases.Any(c => c.Reporter == "p:b"),
            "Flare no limit: with forty cases open the host never lets another player's open case go for somebody else's: the host's own goes first, else the new one");
        var f = new FlareNet();
        Check(FlareLiveRules.FirstToday(f, 5, FlareLiveRules.FriendKey('T', 3)) && !FlareLiveRules.FirstToday(f, 5, FlareLiveRules.FriendKey('T', 3)) && FlareLiveRules.FirstToday(f, 5, FlareLiveRules.FriendKey('P', 3))
              && FlareLiveRules.FirstToday(f, 5, FlareLiveRules.TalkKey("Elm~1")) && !FlareLiveRules.FirstToday(f, 5, FlareLiveRules.TalkKey("Elm~1")) && FlareLiveRules.TalkKey(null) == null,
            "Flare no limit: a tagged friend, the partner seeing it, and a business's talk each once a day");
        string live = ReviewSource("Social/Flare/FlareDirector.Live.cs");
        string links = FlareMethod(ReviewSource("Social/Flare/FlareDirector.Links.cs"), "private void Influence(");
        string city = FlareMethod(ReviewSource("Social/Flare/FlareDirector.City.cs"), "private void CityInfluence(");
        string answer = FlareMethod(live, "private void Answer(");
        Check(answer.Contains("if (c.Counted)") && answer.IndexOf("if (c.Counted)", StringComparison.Ordinal) < answer.IndexOf("MayorMood", StringComparison.Ordinal)
              && FlareMethod(live, "private void Escalation(").Contains("!c.Counted") && FlareMethod(live, "private FlareCase FileCase(").Contains("FlareLiveRules.Counted(")
              && links.IndexOf("TalkKey(", StringComparison.Ordinal) >= 0 && links.IndexOf("TalkKey(", StringComparison.Ordinal) < links.IndexOf("AddBuzz(", StringComparison.Ordinal)
              && links.Contains("FriendKey('T'") && city.Contains("FriendKey('P'"),
            "Flare no limit: the mood, the tally and the paper wait on a counted case; the talk, a tagged friend and the partner's look go through their once a day");
        FlareFile g = FlareRules.Open(7, 1, 10, 50);
        var mine = new List<FlarePost>();
        for (int i = 0; i < 60; i++)
        {
            FlarePost p = FlareRules.Post(g, NoLimitDraft(g, FlareSource.Life, 5, i), FlareTone.Cheerful, -1, null, 5, 10, 600);
            p.Pending = i >= 10;   // the host has the first ten back; fifty still on their way
            mine.Add(p);
            FlareRules.Prune(g);
        }
        bool newest = mine.Skip(10).All(p => g.Post(p.Id) != null);
        foreach (FlarePost p in mine)
        {
            p.Pending = false;
        }
        FlareRules.Prune(g);
        Check(newest && g.OwnPosts == FlareRules.OwnMost && mine.Skip(20).All(p => g.Post(p.Id) != null),
            "Flare no limit: a guest posting fast keeps its newest posts while they are on their way, and the forty kept are the newest");
    }

    // ---------------------------------------------------------------- the texts

    private static readonly string[] NoLimitNewKeys =
    {
        "reallife_flare_side_standing", "reallife_flare_compose_counted", "reallife_flare_compose_report_again", "reallife_flare_compose_review_again", "reallife_flare_note_pictures_day"
    };

    private static readonly string[] NoLimitGoneKeys =
    {
        "reallife_flare_post_limits", "reallife_flare_wait_soon", "reallife_flare_wait_day", "reallife_flare_side_can_post", "reallife_flare_side_line",
        "reallife_flare_compose_reported", "reallife_flare_compose_replies"
    };

    private static void NoLimitWords()
    {
        string folder = LocalesFolder();
        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru" };
        var words = codes.Select(c => LoadWords(System.IO.Path.Combine(folder, c + ".json"))).ToArray();
        if (words.Take(3).Any(w => w == null))
        {
            Check(false, "Flare no limit words: the three locales are where the checks look for them");
            return;
        }
        var holes = new Regex(@"\{[a-z_]+\}");
        var bad = new List<string>();
        foreach (string key in NoLimitNewKeys)
        {
            for (int l = 0; l < 3; l++)
            {
                if (!words[l].TryGetValue(key, out string t) || t.Length == 0 || Holes(holes, t) != Holes(holes, words[0][key]) || (l == 2 && !Regex.IsMatch(t, @"\p{IsCJKUnifiedIdeographs}")))
                {
                    bad.Add(codes[l] + ":" + key);
                }
            }
        }
        Check(bad.Count == 0, "Flare no limit words: the new texts are in en, pt and zh-cn with the same {names}" + (bad.Count > 0 ? " (" + string.Join(", ", bad) + ")" : string.Empty));
        string scripts = string.Join("\n", new[] { "Social/Flare/FlareApp.Sheet.cs", "Social/Flare/FlareApp.Side.cs", "Social/Flare/FlareDirector.Share.cs" }.Select(ReviewSource));
        string unread = NoLimitNewKeys.FirstOrDefault(k => !scripts.Contains("\"" + k + "\""));
        Check(unread == null, "Flare no limit words: every new text is read" + (unread != null ? " (" + unread + ")" : string.Empty));
        var still = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            foreach (string key in NoLimitGoneKeys)
            {
                if (words[l] != null && words[l].ContainsKey(key))
                {
                    still.Add(codes[l] + ":" + key);
                }
            }
        }
        string all = string.Join("\n", System.IO.Directory.GetFiles(System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, "..", "Scripts")), "*.cs", System.IO.SearchOption.AllDirectories).Select(System.IO.File.ReadAllText));
        string read = NoLimitGoneKeys.FirstOrDefault(k => all.Contains("\"" + k + "\""));
        Check(still.Count == 0 && read == null, "Flare no limit words: the limit's texts are gone from all seven languages and nothing reads them" + (still.Count > 0 ? " (" + string.Join(", ", still) + ")" : read != null ? " (" + read + " read)" : string.Empty));
        bool guide = true;
        for (int l = 0; l < codes.Length; l++)
        {
            if (words[l] == null || !words[l].TryGetValue("reallife_guide_flare", out string g))
            {
                guide &= l >= 3;
                continue;
            }
            guide &= g.Contains("{flare_posts_counted}") && g.Contains("{flare_reports_day}") && g.Contains("{flare_pictures_day}")
                     && !g.Contains("{flare_posts_day}") && !g.Contains("{flare_gap_hours}") && !g.Contains("{flare_replies_day}");
        }
        Check(guide, "Flare no limit words: the Guide's Flare page says posting has no limit and what counts, in every language that has it");
        Check(words[0]["reallife_flare_note_pictures_day"].Length <= 240, "Flare no limit words: the pictures notice fits a toast");
        Check(WhatsNewHas(words[0], "Fixed: Flare no longer limits your posting") && WhatsNewHas(words[1], "Corrigido: o Flare não limita mais suas publicações") && WhatsNewHas(words[2], "已修复：Flare不再限制发帖"),
            "Flare no limit words: What's new has the fix in en, pt and zh-cn");
        var dash = new Regex(@"[–—]| - |!");
        string dashed = NoLimitNewKeys.FirstOrDefault(k => dash.IsMatch(words[0][k]) || dash.IsMatch(words[1][k]));
        Check(dashed == null, "Flare no limit words: no dash and no exclamation mark" + (dashed != null ? " (" + dashed + ")" : string.Empty));
    }
}
