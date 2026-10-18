using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Ledger;

// Checks for Flare as a living network (Scripts/Social/Flare: FlareLiveRules, FlareLiveModel, FlareLiveStore,
// FlareNetShare, FlareSnap, FlareFilter; Scripts/Ledger/LedgerShare): the day's feed made from a city made up here (its
// kinds, its counts by the option, the same for the same city and day, never empty), the reactions over the hours and a
// pile-on, the City Office (when it answers, what it does, its crews, the watches, the tally that reaches the paper), what
// a review does to a business's customers and where it is applied, Trending, the tags and the typed text, the word list,
// the small copy of a picture and its pieces, the Going Public lines (a round trip, the bounds, the outbox's carry-over and
// one piece a second, nothing to an older RLO, another player's business left alone), the save's lines, every new text in
// three languages, and the cost of a day.
internal static partial class Program
{
    private static void FlareLive()
    {
        FlareLiveFeed();
        FlareLiveReactions();
        FlareLiveOffice();
        FlareLiveBusiness();
        FlareLiveTags();
        FlareLiveFilter();
        FlareLiveSnap();
        FlareLiveShare();
        FlareLiveSaves();
        FlareLiveLedger();
        FlareLiveTexts();
        FlareLiveTiming();
    }

    // ---------------------------------------------------------------- a city made up for the checks

    private static FlareCityFacts FlareFakeCity(int day, int busy, bool news)
    {
        var f = new FlareCityFacts { Day = day, Busy = busy, Rain = day % 3 == 0, News = news ? FlareCast.NewsCrime | FlareCast.NewsAccident : 0, NewsDistrict = news ? "ba:neighborhood_midtown" : null,
            PriceTrend = 1, EventToday = "reallife_event_name_market", EventTodayFrom = 9, EventTomorrow = "reallife_event_name_parade", EventTomorrowFrom = 11, Election = true,
            LateSites = 1, SiteDistrict = "ba:neighborhood_garment", Mover = "Fake Corp", MoverBp = 420, RateMove = 1 };
        f.Candidates.Add("Ada Barlow");
        f.Candidates.Add("Gus Hollis");
        f.Districts.Add("ba:neighborhood_midtown");
        f.Districts.Add("ba:neighborhood_garment");
        f.Districts.Add("ba:neighborhood_hamptons");
        for (int i = 0; i < 12; i++)
        {
            f.Shops.Add(new FlareBiz { Place = "Main~" + (10 + i), Name = "Shop " + i, District = f.Districts[i % 3], Niche = (FlareNiche)(i % 7), Stars = 30 + i * 5 });
        }
        f.Mine.Add(new FlareBiz { Place = "Elm~1", Name = "My Cafe", District = "ba:neighborhood_midtown", Niche = FlareNiche.Food, Stars = 80, Mine = true });
        return f;
    }

    private static FlareFile FlareLiveFile(int seed)
    {
        FlareFile f = FlareRules.Open(seed, 1, 3, 50);
        f.CastSeed = seed * 31 + 7;
        f.Net.CitySeed = f.CastSeed;
        return f;
    }

    private static List<FlareCitizen> FlareCitizensOf(FlareFile f, FlareCityFacts facts)
    {
        var c = new List<FlareCitizen>();
        FlareLiveRules.CitizensOf(FlareLiveRules.CitySeed(f), facts.Districts, c);
        return c;
    }

    private static string FlarePostsLine(FlareFile f)
    {
        return FlareStore.WritePosts(f);
    }

    // ---------------------------------------------------------------- the day's feed

    private static void FlareLiveFeed()
    {
        FlareCityFacts facts = FlareFakeCity(10, 1, true);
        FlareFile a = FlareLiveFile(42), b = FlareLiveFile(42);
        List<FlareCitizen> ca = FlareCitizensOf(a, facts), cb = FlareCitizensOf(b, facts);
        Check(ca.Count == FlareLiveRules.Citizens && ca[3].Handle == cb[3].Handle && ca[3].Handle.StartsWith("@", StringComparison.Ordinal), "Flare live: the city's people are the same for the same seed (" + ca[3].Handle + ")");
        var made = new List<FlarePost>();
        int n = FlareLiveRules.CityDay(a, facts, ca, made);
        FlareLiveRules.CityDay(b, FlareFakeCity(10, 1, true), cb, null);
        Check(n > 0 && FlarePostsLine(a) == FlarePostsLine(b), "Flare live: the same city on the same day makes the very same posts (" + n + ")");
        Check(FlareLiveRules.CityDay(a, facts, ca, made) == 0, "Flare live: the city's posts are made once a day");
        var kinds = new HashSet<FlareSource>();
        int talk = 0, official = 0, business = 0, invites = 0, reports = 0, reviews = 0;
        bool timesOk = true, inDay = true;
        foreach (FlarePost p in a.Posts)
        {
            kinds.Add(p.Source);
            talk += p.Author == FlareAuthor.Citizen ? 1 : 0;
            official += p.Author == FlareAuthor.Official ? 1 : 0;
            business += p.Author == FlareAuthor.Business ? 1 : 0;
            invites += p.Source == FlareSource.Invite ? 1 : 0;
            reports += p.Source == FlareSource.Report ? 1 : 0;
            reviews += p.Source == FlareSource.Review ? 1 : 0;
            timesOk &= p.Hour >= 7 && p.Hour <= 23 && p.Minute >= 0 && p.Minute < 60;
            inDay &= p.Day == 10;
        }
        Check(kinds.Contains(FlareSource.CityTalk) && kinds.Contains(FlareSource.Official) && kinds.Contains(FlareSource.BusinessPost) && kinds.Contains(FlareSource.Invite),
            "Flare live: the day has the city's people, official accounts, businesses and invitations (" + string.Join(", ", kinds) + ")");
        Check(talk == FlareLiveRules.CityTalkPerDay[1] && official >= 2 && official <= FlareLiveRules.OfficialMost[1] && business <= FlareLiveRules.BusinessPerDay[1] && invites == 2,
            "Flare live: so many of each at Normal (people " + talk + ", official " + official + ", businesses " + business + ", invitations " + invites + ")");
        Check(timesOk && inDay, "Flare live: every post of the day is dated within its waking hours, at its own minute");
        int quiet = 0, busy = 0;
        for (int day = 20; day < 40; day++)
        {
            FlareFile q = FlareLiveFile(7), k = FlareLiveFile(7);
            quiet += FlareLiveRules.CityDay(q, FlareFakeCity(day, 0, false), FlareCitizensOf(q, FlareFakeCity(day, 0, false)), null);
            busy += FlareLiveRules.CityDay(k, FlareFakeCity(day, 2, false), FlareCitizensOf(k, FlareFakeCity(day, 2, false)), null);
        }
        Check(quiet > 0 && busy > quiet * 2, "Flare live: Busy makes well over twice what Quiet does (" + quiet + " and " + busy + " over twenty days)");
        var empty = new FlareCityFacts { Day = 5, Busy = 0 };
        FlareFile e = FlareLiveFile(9);
        var cast = new List<FlareCitizen>();
        FlareLiveRules.CitizensOf(FlareLiveRules.CitySeed(e), null, cast);
        Check(FlareLiveRules.CityDay(e, empty, cast, null) >= FlareLiveRules.CityTalkPerDay[0] - 2, "Flare live: a city with no news, no shops and no districts still has its people posting (never empty)");
        FlareFile g = FlareLiveFile(9);
        var follow = FlareFakeCity(5, 1, true);
        follow.Following = true;
        Check(FlareLiveRules.CityDay(g, follow, FlareCitizensOf(g, follow), null) == 0 && g.Posts.Count == 0, "Flare live: a guest that follows the host's feed makes none of the city's posts");
        Check(FlareLiveRules.TopicOf(FlarePersona.Foodie, new FlareCityFacts { Rain = true }, 50) == RealLifeOverhaul.Flare.FlareTalk.Weather
              && FlareLiveRules.TopicOf(FlarePersona.Sporty, new FlareCityFacts(), 900) == RealLifeOverhaul.Flare.FlareTalk.Sport
              && FlareLiveRules.TopicOf(FlarePersona.Commuter, new FlareCityFacts { News = FlareCast.NewsFire }, 200) == RealLifeOverhaul.Flare.FlareTalk.Fire,
            "Flare live: a topic by what the day has first, then by what the person cares about");
        Check(FlareLiveRules.Stars(95, 500) == 5 && FlareLiveRules.Stars(10, 500) == 1 && FlareLiveRules.Stars(-1, 500) == 3 && FlareLiveRules.Stars(100, 999) == 5 && FlareLiveRules.Stars(0, 0) == 1,
            "Flare live: a review's stars follow the game's satisfaction, within one to five");
    }

    // ---------------------------------------------------------------- the reactions over the hours

    private static void FlareLiveReactions()
    {
        FlareFile f = FlareLiveFile(11);
        var people = new List<FlarePerson>
        {
            new FlarePerson { Index = 0, First = "Mom", Stage = 3, Taste = FlareTaste.Cheers, Closeness = 80 },
            new FlarePerson { Index = 4, First = "Lee", Stage = 4, Closeness = 70, Partner = true }
        };
        var d = new FlareDraft { Id = 5, Source = FlareSource.Opened, Day = 3, Hour = 10, Place = "Elm~1", Name = "My Cafe" };
        f.Drafts.Add(d);
        FlarePost p = FlareRules.Post(f, d, FlareTone.Proud, -1, people, 3, 10, 600);
        var b = new StringBuilder();
        p.Reactors = FlareLiveRules.Reactors(p, people, new List<string> { "Ann Staff", "Bob;Bad|Name" }, true, b);
        var r = new List<FlareReaction>();
        FlareLiveRules.ReadReactors(p.Reactors, r);
        bool partner = false, boss = false, citizens = false, staffClean = true;
        int partnerDelay = 9999;
        foreach (FlareReaction x in r)
        {
            partner |= x.Who == "p4";
            boss |= x.Who == "b";
            citizens |= x.Who.StartsWith("c", StringComparison.Ordinal);
            staffClean &= x.Who.IndexOf(';') < 0 && x.Who.IndexOf('|') < 0;
            if (x.Who == "p4")
            {
                partnerDelay = x.Delay;
            }
        }
        Check(partner && boss && citizens && staffClean && partnerDelay <= 33, "Flare live: the partner (within half an hour), the boss, staff and the city's people react, and a name never breaks the line (" + p.Reactors + ")");
        Check(FlareLiveRules.Arrived(p, p.At) == 0 && FlareLiveRules.Arrived(p, p.At + 1440) == 1000 && FlareLiveRules.Arrived(p, p.At + 60) < FlareLiveRules.Arrived(p, p.At + 300)
              && FlareLiveRules.Arrived(p, p.At + 150) > 500, "Flare live: likes come in on a curve: none at first, most in a few hours, all within the day");
        Check(FlareLiveRules.Shown(1000, p, p.At + 1440) == 1000 && FlareLiveRules.Shown(1000, p, p.At - 5) == 0, "Flare live: the figures shown follow the curve");
        int before = f.Posts.Count;
        int replies = FlareLiveRules.CityReplies(f, p, null);
        bool future = true, toIt = true;
        for (int i = before; i < f.Posts.Count; i++)
        {
            future &= f.Posts[i].At > p.At;
            toIt &= f.Posts[i].ReplyTo == p.Id && f.Posts[i].Author == FlareAuthor.Citizen;
        }
        Check(f.Posts.Count == before + replies && future && toIt, "Flare live: the city's replies are posts of their own, each at a minute after the post");
        // A post that went badly and was seen widely: a pile-on of angry replies; tone matters.
        var bad = new FlarePost { Id = 900, Author = FlareAuthor.Player, Source = FlareSource.Arrested, Tone = FlareTone.Cheerful, Bad = true, Reach = 5000, Seed = 77, Day = 3, Hour = 12 };
        f.Posts.Add(bad);
        int angryAt = f.Posts.Count;
        int pile = FlareLiveRules.CityReplies(f, bad, null), angry = 0;
        for (int i = angryAt; i < f.Posts.Count; i++)
        {
            angry += f.Posts[i].Arg == (int)FlareReplyKind.Angry ? 1 : 0;
        }
        Check(FlareLiveRules.PileOn(bad) && pile >= FlareLiveRules.PileOnLeast && pile <= FlareLiveRules.PileOnMost && angry * 2 > pile, "Flare live: a bad post seen by thousands draws a pile-on, mostly angry (" + angry + " of " + pile + ")");
        var honest = new FlarePost { Source = FlareSource.Arrested, Tone = FlareTone.Honest };
        var cheeky = new FlarePost { Source = FlareSource.Selfie, Tone = FlareTone.Cheeky };
        Check(FlareLiveRules.ReplyKindOf(honest, 10) == FlareReplyKind.Kind && FlareLiveRules.ReplyKindOf(cheeky, 10) == FlareReplyKind.Funny && !FlareLiveRules.PileOn(new FlarePost { Bad = true, Reach = 10, Author = FlareAuthor.Player }),
            "Flare live: an honest word about an arrest is met kindly, a cheeky one with laughs, and a bad post few saw draws no pile-on");
    }

    // ---------------------------------------------------------------- the City Office

    private static void FlareLiveOffice()
    {
        var net = new FlareNet();
        long now = FlareLiveRules.Now(10, 9, 0);
        bool again;
        FlareCase c = FlareLiveRules.File(net, FlareReport.Road, "d1", null, "Main St", 1f, 2f, now, "me", 5, 3, out again);
        Check(!again && c.State == FlareCaseState.Open && c.ReplyAt >= now + FlareLiveRules.ReplyLeast && c.ReplyAt <= now + FlareLiveRules.ReplyMost,
            "Flare live: a report is a case, answered within hours (" + (c.ReplyAt - now) + " minutes)");
        FlareCase same = FlareLiveRules.File(net, FlareReport.Road, "d1", null, null, 0f, 0f, now + 10, "c3", 6, 3, out again);
        Check(again && same == c && c.Repeats == 1 && net.Cases.Count == 1, "Flare live: the same thing in the same district while it is open is the same case, its repeats counted");
        FlareCase other = FlareLiveRules.File(net, FlareReport.Business, "d1", "Main~5", "Shop", 0f, 0f, now, "me", 7, 3, out again);
        FlareCase other2 = FlareLiveRules.File(net, FlareReport.Business, "d1", "Main~6", "Shop 2", 0f, 0f, now, "me", 8, 3, out again);
        Check(other != other2, "Flare live: complaints about two businesses are two cases");
        var due = new List<FlareCase>();
        FlareLiveRules.Due(net, now, due);
        int dueNow = due.Count;
        FlareLiveRules.Due(net, now + FlareLiveRules.ReplyMost, due);
        Check(dueNow == 0 && due.Count == 3, "Flare live: a case is due at its time, not before");
        FlareAnswer answer;
        Check(FlareLiveRules.Decide(net, c, now, true, false, out answer) == FlareOrder.Works && answer == FlareAnswer.Works, "Flare live: a broken road gets a crew");
        Check(FlareLiveRules.Decide(net, c, now, false, false, out answer) == FlareOrder.Answered && answer == FlareAnswer.Received, "Flare live: with the City Office's acting off, an answer only");
        var crime = new FlareCase { Kind = FlareReport.Crime, District = "d2" };
        var litter = new FlareCase { Kind = FlareReport.Litter, District = "d2" };
        var bus = new FlareCase { Kind = FlareReport.Transit, District = "d2" };
        var unsafeSite = new FlareCase { Kind = FlareReport.Unsafe, District = "d2" };
        Check(FlareLiveRules.Decide(net, crime, now, true, false, out answer) == FlareOrder.Patrol && FlareLiveRules.Decide(net, litter, now, true, false, out answer) == FlareOrder.Cleanup
              && FlareLiveRules.Decide(net, bus, now, true, false, out answer) == FlareOrder.Answered && answer == FlareAnswer.Transit
              && FlareLiveRules.Decide(net, unsafeSite, now, true, true, out answer) == FlareOrder.SiteCheck && FlareLiveRules.Decide(net, other, now, true, false, out answer) == FlareOrder.Inspection,
            "Flare live: crime brings patrols, litter a cleaning round, an unsafe site the inspector, a business an inspection, the buses an answer");
        // The week's crews: once they are used, "not now".
        var full = new FlareNet();
        for (int i = 0; i < FlareLiveRules.CrewsPerWeek[(int)FlareOrder.Works]; i++)
        {
            FlareCase w = FlareLiveRules.File(full, FlareReport.Road, "w" + i, null, null, 0f, 0f, now, "me", i, 1, out again);
            FlareLiveRules.Act(full, w, FlareOrder.Works, 10, i);
        }
        FlareCase next = FlareLiveRules.File(full, FlareReport.Lamp, "w9", null, null, 0f, 0f, now, "me", 99, 1, out again);
        FlareOrder queued = FlareLiveRules.Decide(full, next, now, true, false, out answer);
        FlareCase weekLater = FlareLiveRules.File(full, FlareReport.Lamp, "w8", null, null, 0f, 0f, now + 8 * 1440, "me", 98, 1, out again);
        Check(queued == FlareOrder.Queued && answer == FlareAnswer.Busy && FlareLiveRules.Decide(full, weekLater, now + 8 * 1440, true, false, out answer) == FlareOrder.Works,
            "Flare live: " + FlareLiveRules.CrewsPerWeek[(int)FlareOrder.Works] + " works crews a week; the next waits, and a week on there is a crew again");
        // Watches: patrols for three days, cleaning for five; the day's turn takes them off.
        var watch = new FlareNet();
        FlareCase pc = FlareLiveRules.File(watch, FlareReport.Crime, "d5", null, null, 0f, 0f, now, "me", 1, 1, out again);
        FlareLiveRules.Act(watch, pc, FlareOrder.Patrol, 10, -1);
        Check(watch.WatchIn("d5", 10, FlareOrder.Patrol) == FlareOrder.Patrol && watch.WatchIn("d5", 13, FlareOrder.Patrol) == FlareOrder.None && pc.Until == 10 + FlareLiveRules.PatrolDays,
            "Flare live: patrols in the district for " + FlareLiveRules.PatrolDays + " days");
        Check(FlareLiveRules.WatchDay(watch, 13) && watch.Watches.Count == 0 && pc.State == FlareCaseState.Done, "Flare live: the day's turn ends the patrols and closes the case");
        // The tally: three ignored complaints of a kind in a district in a week reach the paper, once a week.
        var tally = new FlareNet();
        FlareLiveRules.Complained(tally, FlareReport.Litter, "d7", 20, false);
        FlareLiveRules.Complained(tally, FlareReport.Litter, "d7", 21, true);
        FlareLiveRules.Complained(tally, FlareReport.Litter, "d7", 21, false);
        bool early = FlareLiveRules.Escalate(tally, FlareReport.Litter, "d7", 21, true);
        FlareLiveRules.Complained(tally, FlareReport.Litter, "d7", 22, false);
        bool third = FlareLiveRules.Escalate(tally, FlareReport.Litter, "d7", 22, true);
        bool again2 = FlareLiveRules.Escalate(tally, FlareReport.Litter, "d7", 23, true);
        Check(!early && third && !again2 && FlareLiveRules.Tally(tally, FlareReport.Litter, "d7", 22) == FlareLiveRules.TallyToPaper,
            "Flare live: " + FlareLiveRules.TallyToPaper + " complaints nothing was done about reach the paper, an acted one does not count, and once a week");
        FlareLiveRules.WatchDay(tally, 30);
        Check(tally.Complaints.Count == 0 && tally.Escalated.Count == 0, "Flare live: a week on, the tally starts again");
        FlareLiveRules.Answered(tally, FlareReport.Road, "d7");
        FlareLiveRules.Story(tally, 23, "reallife_flare_news_ignored", "problem", "litter", "district", "d7", "count", "3");
        Check(tally.Stories.Count == 1 && tally.Stories[0].Data.Length == 6, "Flare live: the paper's piece keeps its names");
    }

    // ---------------------------------------------------------------- what a post does to a business

    private static void FlareLiveBusiness()
    {
        Check(FlareLiveRules.ReviewBuzz(5, 100) == 1 && FlareLiveRules.ReviewBuzz(5, 5000) == 2 && FlareLiveRules.ReviewBuzz(1, 100) == -1 && FlareLiveRules.ReviewBuzz(1, 5000) == -2
              && FlareLiveRules.ReviewBuzz(3, 9000) == 0, "Flare live: a good review brings customers, a poor one costs them, more for one many saw, a middling one nothing");
        FlareFile f = FlareLiveFile(3);
        for (int i = 0; i < 10; i++)
        {
            FlareRules.AddBuzz(f, "Elm~1", FlareLiveRules.ReviewBuzz(5, 9000), 4, FlareLiveRules.ReviewDays);
        }
        int up = f.BuzzAt("Elm~1", 4);
        for (int i = 0; i < 20; i++)
        {
            FlareRules.AddBuzz(f, "Elm~1", FlareLiveRules.ComplaintBuzz, 4, FlareLiveRules.InspectionDays);
        }
        int down = f.BuzzAt("Elm~1", 4);
        Check(up == FlareRules.BuzzMost && down == -FlareRules.BuzzMost && f.BuzzAt("Elm~1", 4 + FlareLiveRules.InspectionDays) == 0,
            "Flare live: reviews and complaints move customers within Flare's cap either way (" + up + ", " + down + "), and for their days only");
        Check(!FlareLiveRules.EffectHere(FlareSource.Review, true, false, false) && !FlareLiveRules.EffectHere(FlareSource.Report, true, false, false)
              && !FlareLiveRules.EffectHere(FlareSource.Announce, true, false, true),
            "Flare live: nothing is ever applied to another player's business here (their own game applies it)");
        Check(FlareLiveRules.EffectHere(FlareSource.Review, false, true, false) && FlareLiveRules.EffectHere(FlareSource.Review, false, false, false)
              && FlareLiveRules.EffectHere(FlareSource.Announce, false, true, true) && !FlareLiveRules.EffectHere(FlareSource.Announce, false, true, false)
              && !FlareLiveRules.EffectHere(FlareSource.Announce, false, false, true),
            "Flare live: a review applies to the player's own and the city's; an announcement only to the poster's own business, by the poster");
        Check(FlareLiveRules.AnnounceBuzz(100) == 1 && FlareLiveRules.AnnounceBuzz(9000) == 2, "Flare live: an announcement brings a little more custom, more for a big account");
    }

    // ---------------------------------------------------------------- Trending, the tags, the typed text

    private static void FlareLiveTags()
    {
        Check(FlareLiveRules.TagsOf("Pothole on #MainSt again #mainst #Roads, #a #b #c #d", "potholes") == "potholes mainst roads a b",
            "Flare live: tags are lower case, each once, the built one first, five at most (" + FlareLiveRules.TagsOf("Pothole on #MainSt again #mainst #Roads, #a #b #c #d", "potholes") + ")");
        Check(FlareLiveRules.TagsOf("no tags here", null) == null && FlareLiveRules.TagsOf(null, "rain") == "rain", "Flare live: no tags, none; the built tag alone");
        string longText = new string('a', 500);
        Check(FlareLiveRules.Clip(longText).Length == FlareLiveRules.TextMost && FlareLiveRules.Clip("  two\nlines \r\n here  ") == "two lines here" && FlareLiveRules.Clip("   ") == null
              && FlareLiveRules.Clip("<b>bold</b>").IndexOf('<') < 0, "Flare live: typed text is one line, at most " + FlareLiveRules.TextMost + " characters, never markup");
        string emoji = FlareLiveRules.Clip(new string('x', FlareLiveRules.TextMost - 1) + "😀");
        Check(emoji.Length == FlareLiveRules.TextMost - 1 && !char.IsHighSurrogate(emoji[emoji.Length - 1]), "Flare live: a character of two halves is never cut in two at the limit");
        var posts = new List<FlarePost>
        {
            new FlarePost { Day = 2, Hour = 9, Topic = "rain potholes", Reach = 1200 },
            new FlarePost { Day = 2, Hour = 10, Topic = "rain", Reach = 0 },
            new FlarePost { Day = 2, Hour = 11, Topic = "food", Reach = 0 },
            new FlarePost { Day = 2, Hour = 23, Topic = "future" },
            new FlarePost { Day = 0, Hour = 1, Topic = "old" }
        };
        var tags = new List<string>();
        var counts = new List<int>();
        FlareLiveRules.Trending(posts, FlareLiveRules.Now(2, 12, 0), 5, tags, counts);
        Check(tags.Count == 3 && tags[0] == "rain" && tags[1] == "potholes" && counts[0] > counts[1] && !tags.Contains("future") && !tags.Contains("old"),
            "Flare live: Trending counts the last day's shown posts by how far they went (" + string.Join(" ", tags) + ")");
    }

    // ---------------------------------------------------------------- the word list

    private static void FlareLiveFilter()
    {
        Check(FlareFilter.Count >= 30, "Flare live: the word list has its words");
        string s = FlareFilter.Clean("what the shit is this", FlareFilter.Standard);
        Check(s == "what the s*** is this", "Flare live: a listed word is starred but its first letter (" + s + ")");
        Check(FlareFilter.Clean("sh1t", FlareFilter.Standard) == "s***" && FlareFilter.Clean("s.h.i.t", FlareFilter.Standard).IndexOf('h') < 0 && FlareFilter.Clean("SHIIIT!", FlareFilter.Standard).StartsWith("S*", StringComparison.Ordinal),
            "Flare live: figures for letters, dots between them and doubled letters are seen through");
        Check(FlareFilter.Clean("Scunthorpe and Dickens", FlareFilter.Standard) == "Scunthorpe and Dickens" && FlareFilter.Clean("A peacock", FlareFilter.Strict) == "A peacock",
            "Flare live: a word that only holds a short listed one is left alone");
        Check(FlareFilter.Clean("you motherfuckers", FlareFilter.Standard) == "you motherfuckers" && FlareFilter.Clean("you motherfuckers", FlareFilter.Strict) != "you motherfuckers",
            "Flare live: Strict finds a long listed word inside another, Standard only whole words");
        Check(FlareFilter.Clean("visit www.example.com now", FlareFilter.Strict).IndexOf("example", StringComparison.Ordinal) < 0 && FlareFilter.Clean("visit www.example.com now", FlareFilter.Standard).Contains("example"),
            "Flare live: Strict stars web addresses");
        Check(FlareFilter.Clean(null, 0) == null && FlareFilter.Clean("a clean line", FlareFilter.Strict) == "a clean line", "Flare live: a clean text comes back the same");
    }

    // ---------------------------------------------------------------- the small copy of a picture

    private static void FlareLiveSnap()
    {
        int w = 256, h = 144;
        var rgb = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int o = (y * w + x) * 3;
                rgb[o] = (byte)(x * 255 / w);
                rgb[o + 1] = (byte)(y * 255 / h);
                rgb[o + 2] = (byte)((x + y) % 256);
            }
        }
        byte[] packed = FlareSnap.Encode(rgb, w, h);
        int dw, dh;
        byte[] back = FlareSnap.Decode(packed, out dw, out dh);
        Check(packed != null && packed.Length <= FlareSnap.MostBytes && back != null && dw <= FlareSnap.Width && dh * w == dw * h,
            "Flare live: a picture's small copy is at most " + FlareSnap.MostBytes / 1024 + " KB and keeps its shape (" + (packed?.Length ?? 0) + " bytes, " + dw + "x" + dh + ")");
        // The colours come back close: against the picture made small the same way.
        int sw, sh;
        byte[] small = FlareSnap.Shrink(rgb, w, h, 3, false, dw, FlareSnap.TallMost, out sw, out sh);
        long err = 0;
        for (int i = 0; back != null && small != null && i < Math.Min(back.Length, small.Length); i++)
        {
            err += Math.Abs(back[i] - small[i]);
        }
        double mean = back != null && small != null ? err / (double)small.Length : 999;
        Check(mean < 14.0, "Flare live: the colours of the small copy come back close (" + mean.ToString("0.0") + " steps of 255 on average)");
        var rng = new Random(5);
        var noise = new byte[w * h * 3];
        rng.NextBytes(noise);
        byte[] hard = FlareSnap.Encode(noise, w, h);
        Check(hard != null && hard.Length <= FlareSnap.MostBytes, "Flare live: even noise fits the bound, by rounding harder and growing smaller (" + (hard?.Length ?? 0) + " bytes)");
        byte[] broken = (byte[])packed.Clone();
        broken[2] = (byte)'X';
        int bw, bh;
        Check(FlareSnap.Decode(broken, out bw, out bh) == null && FlareSnap.Decode(new byte[3], out bw, out bh) == null && FlareSnap.Decode(null, out bw, out bh) == null,
            "Flare live: a damaged small copy is refused");
        var chunks = new List<string>();
        FlareSnap.Chunks(hard, chunks);
        var parts = new FlareSnapParts("p/1", chunks.Count);
        bool whole = false;
        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            whole = parts.Add(i, chunks[i]);
            parts.Add(i, "garbage");   // a piece twice is left out
        }
        byte[] joined = parts.Bytes();
        bool same = joined != null && joined.Length == hard.Length;
        for (int i = 0; same && i < joined.Length; i++)
        {
            same = joined[i] == hard[i];
        }
        Check(chunks.Count <= FlareSnap.PartsMost && whole && same && !new FlareSnapParts("p/2", 2).Add(0, chunks[0]), "Flare live: a picture goes in " + chunks.Count + " pieces, put back together in any order");
        var bgr = new byte[4 * 4 * 4];
        for (int i = 0; i < 16; i++)
        {
            bgr[i * 4] = 10;
            bgr[i * 4 + 1] = 20;
            bgr[i * 4 + 2] = 200;
        }
        int ow, oh;
        byte[] fromBgr = FlareSnap.Shrink(bgr, 4, 4, 4, true, 2, 10, out ow, out oh);
        Check(fromBgr != null && ow == 2 && oh == 2 && fromBgr[0] == 200 && fromBgr[2] == 10, "Flare live: a screen's blue-first pixels are read as they are");
    }

    // ---------------------------------------------------------------- Going Public

    private static void FlareLiveShare()
    {
        FlareFile host = FlareLiveFile(21);
        host.Posts.Add(new FlarePost { Id = 1, Day = 4, Hour = 9, Minute = 5, Author = FlareAuthor.Citizen, Who = 3, Source = FlareSource.CityTalk, Arg = 12, Reach = 100, Likes = 9, Topic = "city" });
        host.Posts.Add(new FlarePost { Id = 2, Day = 4, Hour = 10, Author = FlareAuthor.Player, Source = FlareSource.Report, Arg = 0, District = "d1", Text = "Hole | big\nand deep", NetPic = "n:Host/1", Reach = 50 });
        host.Posts.Add(new FlarePost { Id = 3, Day = 4, Hour = 11, Author = FlareAuthor.Friend, Who = 1, Source = FlareSource.Life });
        host.Posts.Add(new FlarePost { Id = 4, Day = 4, Hour = 12, Author = FlareAuthor.Official, Who = 0, Source = FlareSource.Answer, Arg = 1, ReplyTo = 2, When = 3, Name = "5 Main St" });
        host.Posts.Add(new FlarePost { Id = 5, Day = 4, Hour = 12, Author = FlareAuthor.Influencer, Who = 9, Source = FlareSource.Niche });
        host.Net.Cases.Add(new FlareCase { Id = 7, Kind = FlareReport.Road, District = "d1", State = FlareCaseState.Acting, Order = FlareOrder.Works, Post = 2, Answer = 4, Ref = 33, Filed = 9000, ReplyAt = 9100 });
        host.Net.Watches.Add(new FlareWatch { District = "d2", Kind = FlareOrder.Patrol, Until = 9 });
        var pics = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Host/1", "QUJD") };
        string whole = FlareNetShare.WriteWhole(host, 4, "Host", "Hana", true, pics, 1);
        string[] lines = whole.Split('\n');
        int posts = 0, cases = 0, watches = 0, pictures = 0;
        FlarePost own = null, answer = null;
        int ownReply = 0, answerReply = 0;
        foreach (string line in lines)
        {
            string[] f = line.Split('|');
            FlarePost p;
            int reply;
            if (FlareNetShare.ReadPost(f, out p, out reply))
            {
                posts++;
                if (p.Source == FlareSource.Report)
                {
                    own = p;
                    ownReply = reply;
                }
                if (p.Source == FlareSource.Answer)
                {
                    answer = p;
                    answerReply = reply;
                }
            }
            FlareCase c;
            int pn, an;
            cases += FlareNetShare.ReadCase(f, out c, out pn, out an) && pn == 2 && an == 4 && c.Ref == 33 ? 1 : 0;
            FlareWatch w;
            watches += FlareNetShare.ReadWatch(f, out w) && w.District == "d2" ? 1 : 0;
            pictures += line.StartsWith("X|Host/1|QUJD", StringComparison.Ordinal) ? 1 : 0;
        }
        Check(lines[0] == "FN1|4|" + host.Net.CitySeed + "|Host|1" && posts == 3 && cases == 1 && watches == 1 && pictures == 1,
            "Flare live: the whole part has the city's and the players' posts (not the friends' or the influencers'), the cases, the watches and the pictures (" + posts + " posts)");
        Check(own != null && own.Author == FlareAuthor.Network && own.AuthorId == "Host" && own.AuthorName == "Hana" && own.Text == "Hole | big and deep" && own.Pic == "n:Host/1" && own.District == "d1" && ownReply == -1,
            "Flare live: the host's own post goes under its name, its typed text whole (a line break as a space), its picture by the session's id");
        Check(answer != null && answerReply == 2 && answer.When == 3 && answer.Name == "5 Main St", "Flare live: an answer names the session's number of the post it answers");
        // A guest's post, as the host reads it, and its bounds.
        var g = new FlarePost { Token = 77, Source = FlareSource.Review, Arg = 4, Place = "Main~5", Name = "Shop", Text = "Nice | place", NetPic = "n:Gus/2", Reach = 300 };
        string ask = FlareNetShare.AskPost(g, -1, "Gus");
        FlareAsk a;
        Check(FlareNetShare.ReadAsk(ask, out a) && a.Kind == 'P' && a.Token == 77 && a.Post.Source == FlareSource.Review && a.Post.Arg == 4 && a.Post.Text == "Nice | place" && a.Name == "Gus" && a.Post.Pic == "n:Gus/2",
            "Flare live: a guest's post reaches the host as it was written");
        FlareAsk bad;
        Check(!FlareNetShare.ReadAsk(FlareNetShare.AskPost(new FlarePost { Source = FlareSource.Review, Arg = 9, Place = "x" }, -1, "Gus"), out bad)
              && !FlareNetShare.ReadAsk(FlareNetShare.AskPost(new FlarePost { Source = FlareSource.Report, Arg = 1 }, -1, "Gus"), out bad)
              && !FlareNetShare.ReadAsk(FlareNetShare.AskPost(new FlarePost { Source = FlareSource.CityTalk }, -1, "Gus"), out bad)
              && !FlareNetShare.ReadAsk(FlareNetShare.AskPost(new FlarePost { Source = FlareSource.Reply, ReplyTo = 3 }, 3, "Gus"), out bad)
              && !FlareNetShare.ReadAsk("P|1|" + new string('x', FlareNetShare.AskChars), out bad) && !FlareNetShare.ReadAsk("Q|1", out bad),
            "Flare live: the host takes no built part out of its bounds, no post of the city's kinds from a guest, no reply without words, no line too long, no kind it does not know");
        string longText = FlareNetShare.AskPost(new FlarePost { Source = FlareSource.Life, Text = new string('w', 900) }, -1, "Gus");
        Check(FlareNetShare.ReadAsk(longText, out a) && a.Post.Text.Length == FlareLiveRules.TextMost, "Flare live: typed text is clipped to its length at the host");
        Check(FlareNetShare.ReadAsk(FlareNetShare.AskLike(12, true), out a) && a.Kind == 'L' && a.On && a.NetId == 12 && FlareNetShare.ReadAsk(FlareNetShare.AskDm("Hana", "hi | there"), out a) && a.To == "Hana"
              && a.Text == "hi | there" && FlareNetShare.ReadAsk(FlareNetShare.AskGoing(5), out a) && a.Kind == 'G', "Flare live: a like, a message and going reach the host");
        string dm = FlareNetShare.WriteDm("Gus", "Gus", 9999, "see you\nthere");
        string from, name, words;
        long at;
        Check(FlareNetShare.ReadDm(dm.Split('|'), out from, out name, out at, out words) && from == "Gus" && at == 9999 && words == "see you there", "Flare live: a message to one player reads back");
        string going = FlareNetShare.WriteGoing("Gus", "Gus", 50, 12);
        string gw, gn;
        long gat;
        int gnet;
        Check(FlareNetShare.ReadGoing(going.Split('|'), out gw, out gn, out gat, out gnet) && gnet == 12, "Flare live: going to an invitation reads back");
        // The outbox: as many lines as a request may carry, one piece of a picture a second, the rest the next second.
        var box = new FlareOutbox();
        box.Add("X|p/1|0|3|aaa");
        box.Add("P|1|...");
        box.Add("X|p/1|1|3|bbb");
        box.Add("L|4|1");
        box.Add("X|p/1|2|3|ccc");
        var sent = new List<string>();
        int first = box.Send(l => { sent.Add(l); return true; }, 16);
        int pieces = 0;
        foreach (string l in sent)
        {
            pieces += l[0] == 'X' ? 1 : 0;
        }
        sent.Clear();
        int second = box.Send(l => { sent.Add(l); return true; }, 1);
        Check(first == 3 && pieces == 1 && second == 1 && sent[0] == "X|p/1|1|3|bbb" && box.Count == 1, "Flare live: one piece of a picture a second, the other lines at once, in order; what is left waits");
        for (int i = 0; i < FlareOutbox.Most + 10; i++)
        {
            box.Add("L|" + i + "|1");
        }
        Check(box.Count == FlareOutbox.Most, "Flare live: a guest offline for long keeps " + FlareOutbox.Most + " lines at most");
        // The host's bounds on one player.
        var rates = new FlareRateBook();
        int allowed = 0;
        for (int i = 0; i < FlareRateBook.PostsWindow + 20; i++)
        {
            allowed += rates.Allow("Gus", 'P', 100f + i * 0.5f, 4) ? 1 : 0;
        }
        bool later = rates.Allow("Gus", 'P', 100f + (FlareRateBook.PostsWindow + 20) * 0.5f + FlareRateBook.Window, 4);
        bool otherPlayer = rates.Allow("Ada", 'P', 101f, 4);
        Check(allowed == FlareRateBook.PostsWindow && later && otherPlayer, "Flare live: " + FlareRateBook.PostsWindow + " posts a player in ten minutes at most, then again; each player counted alone");
        int picDay = 0;
        for (int i = 0; i < 100; i++)
        {
            picDay += rates.Allow("Gus", 'X', 1000f + i, 5) ? 1 : 0;
        }
        Check(picDay == FlareRateBook.PicturesDay, "Flare live: a day's pictures from a player are bounded (" + FlareRateBook.PicturesDay + " pictures)");
        // Nothing to an older RLO: another version's message is read but nothing of it applies, and a part letter it does not know is left out.
        var m = new SharedMessage { Kind = SharedKind.Update, Version = "9.9.9", Sequence = 3 };
        m.Parts.Add(new SharedPart('f', "P|1"));
        m.Parts.Add(new SharedPart('l', "LS1|4"));
        byte[] bytes = SharedCityWire.Write(m);
        SharedMessage read;
        Check(SharedCityWire.Read(bytes, "0.24.0", out read) == SharedRead.OtherVersion, "Flare live: an RLO of another version reads the message as another version's and applies nothing of it");
        Check(SharedCityWire.Read(bytes, "9.9.9", out read) == SharedRead.Ok && read.Part('f') == "P|1" && read.Part('Z') == null,
            "Flare live: Flare's and the paper's letters travel in the shared city's own messages, and a letter no part has is simply not there");
        Check(FlareNetShare.Part != 'E' && FlareNetShare.Part != 'G' && FlareNetShare.Part != 'W' && FlareNetShare.Part != 'N' && FlareNetShare.Part != 'S' && FlareNetShare.Part != 'M'
              && LedgerShare.Part != FlareNetShare.Part && LedgerShare.Part != 'E' && LedgerShare.Part != 'M', "Flare live: Flare and the City Ledger have letters of their own");
        Check(FlareNetShare.Esc("a|b%c\nd") == "a%7Cb%25c%0Ad" && FlareNetShare.Unesc("a%7Cb%25c%0Ad") == "a|b%c d" && FlareNetShare.Unesc("100%") == "100%",
            "Flare live: a typed text's separators are escaped, and read back");
    }

    // ---------------------------------------------------------------- the save

    private static void FlareLiveSaves()
    {
        FlareFile f = FlareLiveFile(31);
        f.Posts.Add(new FlarePost
        {
            Id = 3, Day = 5, Hour = 9, Minute = 41, Author = FlareAuthor.Network, Source = FlareSource.Review, Arg = 5, Place = "Main~1", Name = "Shop", ReplyTo = 2, Text = "Great | place 100%",
            Pic = "n:Gus/1", NetId = 88, Token = 7, AuthorId = "Gus", AuthorName = "Gus %", District = "d1", Topic = "review food", Reactors = "L:c1:5", Mention = "o:0", Reposts = 4,
            PlayerLikes = 2, Case = 6, Liked = true, Reposted = true, Shared = true, Pending = true, When = 120, NetPic = "n:Me/3"
        });
        string posts = FlareStore.WritePosts(f);
        FlareFile b = FlareStore.Read(FlareStore.WriteAccount(f), posts);
        FlarePost p = b != null && b.Posts.Count == 1 ? b.Posts[0] : null;
        Check(p != null && p.Minute == 41 && p.ReplyTo == 2 && p.Text == "Great | place 100%" && p.Pic == "n:Gus/1" && p.NetId == 88 && p.Token == 7 && p.AuthorId == "Gus" && p.AuthorName == "Gus %"
              && p.District == "d1" && p.Topic == "review food" && p.Reactors == "L:c1:5" && p.Mention == "o:0" && p.Reposts == 4 && p.PlayerLikes == 2 && p.Case == 6 && p.Liked && p.Reposted
              && p.Shared && p.Pending && p.When == 120 && p.NetPic == "n:Me/3" && p.Author == FlareAuthor.Network && p.Source == FlareSource.Review,
            "Flare live: every new field of a post comes back from the save, typed text and all");
        Check(b != null && FlareStore.WritePosts(b) == posts, "Flare live: the posts' line reads back exactly");
        string old = "P|9|5|9|0|-1|2|0|-1|Elm~1|Photo.png|0|1|500|10|1|0|0|0|5|0";
        FlareFile o = FlareStore.Read(FlareStore.WriteAccount(f), old);
        FlarePost op = o != null && o.Posts.Count == 1 ? o.Posts[0] : null;
        Check(op != null && op.ReplyTo == -1 && op.Text == null && op.Case == -1 && op.When == -1 && !op.Shared && op.Minute == 0, "Flare live: a post from an older save reads, with none of the new fields");
        // The network's line.
        FlareNet n = f.Net;
        n.Cases.Add(new FlareCase { Id = 4, Kind = FlareReport.Litter, District = "d|2", Place = "Main~1", Name = "Main St", X = 1.5f, Z = -2.25f, Filed = 7000, ReplyAt = 7100, State = FlareCaseState.Acting,
            Order = FlareOrder.Cleanup, Ref = -1, Until = 9, Post = 3, Answer = 5, Reporter = "p:Gus", Repeats = 2 });
        n.Watches.Add(new FlareWatch { District = "d2", Kind = FlareOrder.Cleanup, Until = 9 });
        n.Complaints.Add(new FlareComplaint { Kind = FlareReport.Road, District = "d3", Day = 4, Acted = true });
        FlareLiveRules.Dm(n, "p:Gus", "Gus", 7200, false, "hi | you", null, null);
        FlareLiveRules.Dm(n, "c3", "Ada Barlow", 7300, false, null, "reallife_flare_dm_chat", "event=a~b=c");
        FlareLiveRules.Story(n, 6, "reallife_flare_news_pileon", "name", "A | B", "handle", "@ab");
        n.Muted.Add("Gus");
        n.Hidden.Add("Bob");
        n.Going.Add(3);
        n.Applied.Add(88);
        n.Escalated.Add("3|d3|4");
        n.NoticeSeen = 7150;
        n.CityDay = 5;
        n.PicDay = 5;
        n.PicsToday = 2;
        n.PicNext = 4;
        n.ReportsDay = 5;
        n.ReportsToday = 1;
        n.CitySeed = 1234;
        string line = FlareLiveStore.Write(n);
        var r = new FlareNet();
        FlareLiveStore.Read(r, line);
        Check(FlareLiveStore.Write(r) == line && r.Cases.Count == 1 && r.Cases[0].District == "d|2" && r.Cases[0].Z == -2.25f && r.Cases[0].Reporter == "p:Gus" && r.Dms.Count == 2
              && r.Dms[0].Text == "hi | you" && r.Dms[1].Data == "event=a~b=c" && r.Stories.Count == 1 && r.Stories[0].Data[1] == "A | B" && r.IsMuted("Gus") && r.IsHidden("Bob")
              && r.Going[0] == 3 && r.Applied[0] == 88 && r.NoticeSeen == 7150 && r.CitySeed == 1234 && r.PicsToday == 2 && r.Escalated[0] == "3|d3|4",
            "Flare live: the network's line reads back exactly, every list and figure");
        var junk = new FlareNet();
        FlareLiveStore.Read(junk, "NT9|1|2|3");
        FlareLiveStore.Read(r, line + "\nC|9|99|x\nM|1\nZ|what");
        Check(junk.Cases.Count == 0 && junk.NextCase == 1 && r.Cases.Count == 1, "Flare live: a later version's head is not read, and a line not understood is left out");
        var data = new Dictionary<string, string>();
        FlareStore.Save(data, f);
        FlareFile loaded = FlareStore.Load(data);
        Check(data.ContainsKey(FlareLiveStore.Key) && loaded != null && loaded.Net.CitySeed == 1234 && loaded.Net.Cases.Count == 1, "Flare live: the save keeps the network beside the account");
    }

    // ---------------------------------------------------------------- the City Ledger in a session

    private static void FlareLiveLedger()
    {
        var e = new LedgerEdition { Day = 12, HasMarket = true, MarketDay = 11, IndexLevel = 10500, IndexChangeBp = 30 };
        e.Pieces.Add(new LedgerPiece(LedgerSection.City, 70, "reallife_news_crime_1", null, new[] { "street", "Main | St" }));
        e.Sectors.Add(new LedgerSector { Key = "s1", Level = 1000, ChangeBp = 2 });
        e.Quotes.Add(new LedgerQuote { Name = "Fake", Ticker = "FK", Sector = 0, Close = 1234, ChangeBp = 5, Volume = 10 });
        string text = LedgerShare.Write(e);
        LedgerEdition back = LedgerShare.Read(text);
        Check(back != null && back.Day == 12 && back.Pieces.Count == 1 && back.Quotes.Count == 1 && back.Pieces[0].Value("street") == "Main / St" && LedgerShare.Read("XX|1") == null && LedgerShare.Write(null) == null,
            "Flare live: the host's edition is the guests' paper, read back whole");
        string piece = LedgerShare.AskPiece(new LedgerPiece(LedgerSection.People, 50, "reallife_study_news_graduated", null, new[] { "name", "Ada", "degree", "Law" }));
        LedgerPiece p = LedgerShare.ReadPiece(piece.Split('|'));
        Check(p != null && p.Key == "reallife_study_news_graduated" && p.Value("degree") == "Law" && LedgerShare.AskPiece(new LedgerPiece(LedgerSection.Markets, 50, "reallife_mkt_news_up", null, null)) == null
              && LedgerShare.ReadPiece("P|0|50|reallife_mkt_news_up|".Split('|')) == null, "Flare live: a guest sends only pieces about its player, and the host takes no other");
        string report = LedgerShare.AskReport(3, 12.5f, -4f, "mugging", 1);
        int kind, amount;
        float x, z;
        string detail;
        Check(LedgerShare.ReadReport(report.Split('|'), 11, out kind, out x, out z, out detail, out amount) && kind == 3 && x == 12.5f && detail == "mugging"
              && !LedgerShare.ReadReport("N|40|0|0||0".Split('|'), 11, out kind, out x, out z, out detail, out amount),
            "Flare live: a guest's report reaches the host's paper; a kind it does not know does not");
        var asked = new List<KeyValuePair<string, LedgerPiece>>();
        for (int i = 0; i < 10; i++)
        {
            asked.Add(new KeyValuePair<string, LedgerPiece>("Gus", new LedgerPiece(LedgerSection.People, 50, "reallife_life_news_top", null, new[] { "name", "Gus" + i })));
        }
        asked.Add(new KeyValuePair<string, LedgerPiece>("Ada", new LedgerPiece(LedgerSection.People, 50, "reallife_life_news_top", null, new[] { "name", "Ada" })));
        asked.Add(new KeyValuePair<string, LedgerPiece>("Ada", new LedgerPiece(LedgerSection.People, 50, "reallife_life_news_top", null, new[] { "name", "Ada" })));
        var taken = new List<LedgerPiece>();
        LedgerShare.Take(asked, taken);
        Check(taken.Count == LedgerShare.PiecesPerGuestDay + 1, "Flare live: a few pieces of each guest's a day, each once (" + taken.Count + ")");
    }

    // ---------------------------------------------------------------- the texts

    private static void FlareLiveTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("Flare live", words, codes))
        {
            return;
        }
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var key = new Regex("\"(reallife_(?:flare|opt_tip_flare)_[a-z0-9_]+)\"");
        foreach (string file in new[]
                 {
                     "Social/Flare/FlareText.Live.cs", "Social/Flare/FlareApp.cs", "Social/Flare/FlareApp.Frame.cs", "Social/Flare/FlareApp.Side.cs", "Social/Flare/FlareApp.Cells.cs",
                     "Social/Flare/FlareApp.Screens.cs", "Social/Flare/FlareApp.Sheet.cs", "Social/Flare/FlareDirector.Live.cs", "Social/Flare/FlareDirector.Cases.cs", "Social/Flare/FlareDirector.Posting.cs", "Social/Flare/FlareDirector.Share.cs", "Social/Flare/FlareDirector.Following.cs",
                     "Social/Flare/FlareDirector.Camera.cs", "Social/Flare/FlareDirector.Tests.cs", "Core/RealLifeOverhaulMod.cs"
                 })
        {
            string path = System.IO.Path.Combine(scripts, file.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                Check(false, "Flare live: the source " + file + " is where the checks look for it");
                continue;
            }
            foreach (Match m in key.Matches(File.ReadAllText(path)))
            {
                if (!m.Groups[1].Value.EndsWith("_", StringComparison.Ordinal))
                {
                    keys.Add(m.Groups[1].Value);
                }
            }
        }
        foreach (string id in new[] { "flare_busy", "flare_city_office", "flare_share_posts", "flare_share_pictures", "flare_filter", "flare_theme" })
        {
            keys.Add("reallife_opt_tip_" + id);
        }
        var missing = new List<string>();
        foreach (string k in keys)
        {
            string lacking = LanguagesLacking(words, codes, k);
            if (lacking.Length > 0 || (words[2].TryGetValue(k, out string zh) && !HasHan(zh) && zh != words[0][k] && Regex.IsMatch(Regex.Replace(zh, @"\{[a-z_]+\}", string.Empty), "[A-Za-z]")))
            {
                missing.Add(k + (lacking.Length > 0 ? " (" + lacking + ")" : " (zh-cn not Chinese)"));
            }
        }
        Check(keys.Count > 250 && missing.Count == 0, "Flare live: every text the network shows is in en, pt and zh-cn, with the same names in curly brackets (" + keys.Count + " keys"
                                                     + (missing.Count > 0 ? "; missing: " + string.Join(", ", missing) : string.Empty) + ")");
        Dictionary<string, string> en = words[0];
        bool notes = true;
        foreach (string k in keys)
        {
            if (k.StartsWith("reallife_flare_note_", StringComparison.Ordinal) && en.TryGetValue(k, out string t))
            {
                notes &= t.Length <= 240;
            }
        }
        Check(notes, "Flare live: every notification is short enough for a toast");
        string page;
        bool facts = en.TryGetValue("reallife_guide_flare", out page);
        foreach (string fact in new[] { "{flare_text_most}", "{flare_reports_day}", "{flare_reply_most_hours}", "{flare_crews_works}", "{flare_patrol_days}", "{flare_cleanup_days}",
                     "{flare_inspection_days}", "{flare_tally}", "{flare_posts_counted}", "{flare_picture_kb}", "{flare_pictures_day}" })
        {
            facts &= page != null && page.Contains(fact) && words[1]["reallife_guide_flare"].Contains(fact) && words[2]["reallife_guide_flare"].Contains(fact);
        }
        Check(facts, "Flare live: the Guide's page gives the network's figures from the code, in every language");
        bool news = false;
        for (int l = 0; l < 3; l++)
        {
            news = l == 0 ? FlareNewsLine(words[l]) : news && FlareNewsLine(words[l]);
        }
        Check(news, "Flare live: What's new has the line in en, pt and zh-cn");
    }

    private static bool FlareNewsLine(Dictionary<string, string> words)
    {
        foreach (KeyValuePair<string, string> pair in words)
        {
            if (pair.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !pair.Key.EndsWith("_title", StringComparison.Ordinal)
                && (pair.Value.Contains("Flare") && (pair.Value.Contains("City Ledger") || pair.Value.Contains("城市纪事报"))) && (pair.Value.Contains("Going Public")))
            {
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- the cost

    private static void FlareLiveTiming()
    {
        double day = FlareDayCost(out int posts);
        double notices = FlareNoticesCost();
        Check(day < 2.0, "Flare live: a day of the city's posts (Busy, " + posts + " posts) costs " + day.ToString("0.000") + " ms, on the day's turn only");
        Check(notices < 2.0, "Flare live: the notifications over a full feed cost " + notices.ToString("0.000") + " ms, on the hour and when the app opens");
        Console.WriteLine("  Flare live: a day of the feed " + day.ToString("0.000") + " ms (" + posts + " posts); notifications over 220 posts " + notices.ToString("0.000") + " ms");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double FlareDayCost(out int posts)
    {
        const int rounds = 30;
        posts = 0;
        var watch = new Stopwatch();
        for (int i = 0; i < rounds; i++)
        {
            FlareFile f = FlareLiveFile(100 + i);
            FlareCityFacts facts = FlareFakeCity(10 + i, 2, true);
            List<FlareCitizen> cast = FlareCitizensOf(f, facts);
            watch.Start();
            posts = FlareLiveRules.CityDay(f, facts, cast, null);
            watch.Stop();
        }
        return watch.Elapsed.TotalMilliseconds / rounds;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double FlareNoticesCost()
    {
        FlareFile f = FlareLiveFile(5);
        for (int i = 0; i < 220; i++)
        {
            f.Posts.Add(new FlarePost { Id = i + 1, Day = 3, Hour = 8 + i % 12, Author = i % 3 == 0 ? FlareAuthor.Player : FlareAuthor.Citizen, Reactors = "L:c1:5;R:f2:30;S:c3:90;L:b:200", ReplyTo = i % 3 == 1 ? i : -1, Seed = i });
        }
        var scratch = new List<FlareReaction>();
        var into = new List<FlareNotice>();
        var places = new List<string> { "Elm~1" };
        var watch = Stopwatch.StartNew();
        const int rounds = 20;
        for (int i = 0; i < rounds; i++)
        {
            FlareLiveRules.Notices(f, FlareLiveRules.Now(4, 0, 0), "me", places, scratch, into, 60);
        }
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds / rounds;
    }
}
