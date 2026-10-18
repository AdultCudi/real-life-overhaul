using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Rivals;

// Checks for the review round of Flare as a living network: only the city's reports go from a guest to the host's paper,
// a guest's post is answered by the host alone, session numbers kept to their host and session, what another player
// wrote filtered whole (its tags too), a report always reaches the City Office, an escalation keeps to the week's crews,
// no escalation with the office off or for what it only answers, the patrols stop with Flare, the feed made on the first
// day, the camera let go whatever happens, pictures counted as pictures, a picture on its way never cleared for another
// player's, a share once a player, no path of the player's machine sent, nothing dropped for want of room, today's
// reviews alone on joining, the guest's own city of the day giving way, posts on their way kept when the host goes, the
// Ledger's pieces per guest and filtered and the reprint not put off, the guest's own edition kept, the author's Steam
// account, and the rival tycoons posting in their own voice.
internal static partial class Program
{
    private static void FlareReview()
    {
        FlareReviewLedgerKinds();
        FlareReviewAnswers();
        FlareReviewHost();
        FlareReviewFilter();
        FlareReviewOffice();
        FlareReviewStart();
        FlareReviewPictures();
        FlareReviewShares();
        FlareReviewJoin();
        FlareReviewLedgerPieces();
        FlareReviewTycoons();
    }

    private static string ReviewSource(string file)
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string path = System.IO.Path.Combine(scripts, file.Replace('/', System.IO.Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    // The body of a method in a source, from its signature to the next blank-line-and-comment or member (enough for a look inside).
    private static string FlareMethod(string source, string signature)
    {
        int at = source.IndexOf(signature, StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int open = source.IndexOf('{', at);
        int depth = 0;
        for (int i = open; i >= 0 && i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return source.Substring(at, i - at + 1);
            }
        }
        return string.Empty;
    }

    // ---------------------------------------------------------------- 1. the city's reports only

    private static void FlareReviewLedgerKinds()
    {
        bool city = true, own = true;
        for (int k = 0; k < LedgerShare.CityKinds; k++)
        {
            city &= LedgerShare.CityKind(k) && LedgerShare.AskReport(k, 1f, 2f, null, 0) != null;
            int kind, amount;
            float x, z;
            string detail;
            city &= LedgerShare.ReadReport(LedgerShare.AskReport(k, 1f, 2f, null, 0).Split('|'), 11, out kind, out x, out z, out detail, out amount) && kind == k;
        }
        for (int k = LedgerShare.CityKinds; k < 11; k++)
        {
            int kind, amount;
            float x, z;
            string detail;
            own &= !LedgerShare.CityKind(k) && LedgerShare.AskReport(k, 1f, 2f, null, 0) == null
                   && !LedgerShare.ReadReport(("N|" + k + "|1|2||0").Split('|'), 11, out kind, out x, out z, out detail, out amount);
        }
        Check(city && own, "Flare review: a guest's crash, arrest or knockdown never goes to the host's paper, nor a story; the city's seven kinds do, and the host takes no other");
        // The kinds are News's own, in its order: the seventh is the last of the city's.
        string news = ReviewSource("Social/News.cs");
        Match e = Regex.Match(news, @"public enum NewsKind\s*\{([^}]*)\}");
        var names = new List<string>();
        foreach (string line in (e.Success ? e.Groups[1].Value : string.Empty).Split('\n'))
        {
            Match n = Regex.Match(line, @"^\s*([A-Z][A-Za-z]*)\s*,?");
            if (n.Success)
            {
                names.Add(n.Groups[1].Value);
            }
        }
        Check(names.Count == 11 && names.IndexOf("CarBreakIn") == LedgerShare.CityKinds - 1 && names.IndexOf("PlayerCrash") == LedgerShare.CityKinds
              && FlareMethod(news, "public static void Report(NewsKind kind").Contains("LedgerShare.CityKind((int)kind)"),
              "Flare review: News hands on only the city's kinds (Accident to CarBreakIn, as LedgerShare counts them)");
    }

    // ---------------------------------------------------------------- 2 and 5. who answers a post, and a report always goes

    private static void FlareReviewAnswers()
    {
        Check(FlareLiveRules.HostAnswers(true, true, FlareSource.Life) && FlareLiveRules.HostAnswers(true, false, FlareSource.Report)
              && !FlareLiveRules.HostAnswers(true, false, FlareSource.Life) && !FlareLiveRules.HostAnswers(false, true, FlareSource.Report),
              "Flare review: a following guest's post that goes to the host is answered by the host alone; one that stays is answered here");
        string live = FlareMethod((ReviewSource("Social/Flare/FlareDirector.Live.cs") + ReviewSource("Social/Flare/FlareDirector.Cases.cs") + ReviewSource("Social/Flare/FlareDirector.Posting.cs")), "private void PostLive(FlarePost p)");
        Check(live.Contains("FlareLiveRules.HostAnswers(Following, Settings.FlareSharePosts, p.Source)") && Regex.Matches(live, @"if \(!hostAnswers\)").Count == 2
              && live.IndexOf("CityReplies", StringComparison.Ordinal) > live.IndexOf("if (!hostAnswers)", StringComparison.Ordinal)
              && live.IndexOf("MentionAnswer(p)", StringComparison.Ordinal) > live.LastIndexOf("if (!hostAnswers)", StringComparison.Ordinal),
              "Flare review: the guest makes no city replies and no account's answer to a post the host answers (none come twice)");
        Check(FlareLiveRules.ToSession(false, FlareSource.Report) && !FlareLiveRules.ToSession(false, FlareSource.Life) && FlareLiveRules.ToSession(true, FlareSource.Life)
              && FlareMethod((ReviewSource("Social/Flare/FlareDirector.Share.cs") + ReviewSource("Social/Flare/FlareDirector.Following.cs")), "private void ShareOwn(FlarePost p)").Contains("FlareLiveRules.ToSession(Settings.FlareSharePosts, p.Source)"),
              "Flare review: with posts not shared, a following guest's report still goes to the host's City Office (and its other posts do not)");
    }

    // ---------------------------------------------------------------- 3. session numbers, the host leaving, Flare off on the host

    private static void FlareReviewHost()
    {
        FlareFile f = FlareLiveFile(41);
        f.Posts.Add(new FlarePost { Id = f.NextId++, Author = FlareAuthor.Player, Source = FlareSource.Life, NetId = 12, Day = 3 });
        f.Posts.Add(new FlarePost { Id = f.NextId++, Author = FlareAuthor.Player, Source = FlareSource.Life, NetId = 0, Pending = true, Day = 3 });
        f.Posts.Add(new FlarePost { Id = f.NextId++, Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk, NetId = 13, Shared = true, Day = 3 });
        f.Net.Applied.Add(13);
        string whole = FlareNetShare.WriteWhole(f, 3, "Host A", "A", true, null, 77);
        int day, seed;
        string key;
        bool head = FlareNetShare.ReadHead(whole.Split('\n')[0], out day, out seed, out key);
        bool first = FlareNetShare.NewHost(f, key);
        bool cleared = f.Posts.Count == 2 && f.Posts[0].NetId == 0 && !f.Posts[1].Pending && f.Net.Applied.Count == 0;
        f.Posts[0].NetId = 20;
        f.Net.Applied.Add(20);
        bool same = !FlareNetShare.NewHost(f, key) && f.Posts[0].NetId == 20 && f.Net.Applied.Count == 1;
        string other = FlareNetShare.WriteWhole(f, 3, "Host A", "A", true, null, 78);
        FlareNetShare.ReadHead(other.Split('\n')[0], out day, out seed, out string key2);
        bool again = FlareNetShare.NewHost(f, key2) && f.Posts[0].NetId == 0 && f.Net.Applied.Count == 0;
        var n = new FlareNet();
        FlareLiveStore.Read(n, FlareLiveStore.Write(f.Net));
        Check(head && day == 3 && key == "Host A/77" && first && cleared && same && again && n.HostKey == f.Net.HostKey,
              "Flare review: session numbers belong to a host and its session: another one clears the last's posts, numbers and applied effects; the same one keeps them; the key is saved");
        bool older = FlareNetShare.ReadHead("FN1|3|9", out day, out seed, out key) && key == null;
        Check(older, "Flare review: a head without a host (an older host) still reads, and clears nothing");
        FlareFile g = FlareLiveFile(42);
        g.Posts.Add(new FlarePost { Id = g.NextId++, Author = FlareAuthor.Player, Source = FlareSource.Life, Pending = true });
        Check(FlareNetShare.HostLeft(g) == 1 && !g.Posts[0].Pending && FlareNetShare.HostLeft(g) == 0,
              "Flare review: when the host goes, the player's posts on their way stay their own, no longer waiting");
        string share = (ReviewSource("Social/Flare/FlareDirector.Share.cs") + ReviewSource("Social/Flare/FlareDirector.Following.cs"));
        string flare = ReviewSource("Social/Flare/FlareDirector.cs");
        Check(share.Contains("Ready = () => (Running && Save != null) || _offToSend") && share.Contains("Left = HostGone")
              && FlareMethod(share, "private string HostWhole()").Contains("_offToSend = false")
              && FlareMethod(flare, "private void StandDown(bool keep)").Contains("ShareStop();") && FlareMethod(flare, "private void StartUp()").Contains("ShareStart();"),
              "Flare review: a host that stops Flare in a session sends its guests an empty whole once (they run their own city again), and its feed whole when it starts again");
        var box = new FlareOutbox();
        for (int i = 0; i < 5; i++)
        {
            box.Add("L|" + (i + 1) + "|1");
        }
        int room = 2;
        var went = new List<string>();
        int sent = box.Send(line =>
        {
            if (room-- <= 0)
            {
                return false;
            }
            went.Add(line);
            return true;
        }, 16);
        var rest = new List<string>();
        box.Send(line =>
        {
            rest.Add(line);
            return true;
        }, 16);
        string city = ReviewSource("Core/SharedCity.cs");
        Check(sent == 2 && went.Count == 2 && rest.Count == 3 && rest[0] == "L|3|1" && city.Contains("public static bool Ask(char id, string line)")
              && FlareMethod(city, "public static bool Ask(char id, string line)").Contains("return false;"),
              "Flare review: a line the shared city has no room for stays in the outbox, in order, for the next second (none dropped)");
    }

    // ---------------------------------------------------------------- 4. what another player wrote, filtered whole

    private static void FlareReviewFilter()
    {
        Func<string, string> filter = s => FlareFilter.Clean(s, FlareFilter.Standard);
        string bad = new string(new[] { 's', 'h', 'i', 't' });
        var p = new FlarePost { Source = FlareSource.Life, Text = "hello #" + bad + " #potholes", Topic = bad + " potholes", Name = bad + " corp", Mention = "t:" + bad, Place = "Main~10", AuthorName = bad };
        FlareNetShare.CleanPost(p, filter);
        Check(p.Topic == "potholes" && p.Text.IndexOf(bad, StringComparison.Ordinal) < 0 && p.Name.IndexOf(bad, StringComparison.Ordinal) < 0 && p.Mention == null
              && p.Place == "Main~10" && p.AuthorName.IndexOf(bad, StringComparison.Ordinal) < 0,
              "Flare review: another player's post is filtered whole: its words, its names, the account it calls on, and a starred tag left out of its tags");
        Check(FlareNetShare.CleanTopic("roads mainst <b> " + new string('x', 40), null) == "roads mainst",
              "Flare review: what a guest sends as tags is tags only (letters, figures; not too long)");
        string share = (ReviewSource("Social/Flare/FlareDirector.Share.cs") + ReviewSource("Social/Flare/FlareDirector.Following.cs"));
        Check(FlareMethod(share, "private void GuestPost(FlareAsk ask, string who)").Contains("FlareNetShare.CleanPost(p,")
              && FlareMethod(share, "private int HostPost(string[] f, out bool added)").Contains("FlareNetShare.CleanPost(p,")
              && share.Contains("Filter(goer, who)") && share.Contains("Filter(text, from)"),
              "Flare review: both ways (a guest's post at the host, the host's feed at a guest) and the names of a message and an invitation go through the filter");
        Check(FlareFilter.SteamIdOf("76561198000000001") == 76561198000000001UL && FlareFilter.SteamIdOf("Somebody") == 0UL && FlareFilter.SteamIdOf("12345") == 0UL
              && FlareFilter.SteamIdOf("99999999999999999") == 0UL,
              "Flare review: Steam's filter is given the author's Steam account when the session's id is one, nobody otherwise");
    }

    // ---------------------------------------------------------------- 6 and 7. the City Office under the paper's eye

    private static void FlareReviewOffice()
    {
        var net = new FlareNet();
        long now = FlareLiveRules.Now(30, 12, 0);
        for (int i = 0; i < FlareLiveRules.CrewsPerWeek[(int)FlareOrder.Patrol]; i++)
        {
            var acted = new FlareCase { Id = 100 + i, Kind = FlareReport.Crime, District = "d" + i, Filed = FlareLiveRules.Now(18, 9, 0) };
            FlareLiveRules.Act(net, acted, FlareOrder.Patrol, 29, -1);   // filed twelve days ago, a crew sent yesterday
            net.Cases.Add(acted);
        }
        var c = new FlareCase { Id = 1, Kind = FlareReport.Crime, District = "d9", Filed = now - 600, State = FlareCaseState.Queued, Order = FlareOrder.Queued };
        net.Cases.Add(c);
        FlareOrder full = FlareLiveRules.EscalationOrder(net, c, now, true);
        net.Cases.RemoveAt(0);
        FlareOrder free = FlareLiveRules.EscalationOrder(net, c, now, true);
        Check(full == FlareOrder.None && free == FlareOrder.Patrol && net.Cases[0].ActedDay == 29,
              "Flare review: an escalation sends a crew only if one of the week's is free, and the week's crews are counted by the day they were sent");
        Check(FlareLiveRules.EscalationOrder(net, c, now, false) == FlareOrder.None
              && FlareLiveRules.EscalationOrder(net, new FlareCase { Kind = FlareReport.Transit, District = "d9" }, now, true) == FlareOrder.None,
              "Flare review: no crew under the paper's eye with the office's acting off, nor for the buses or the power");
        var tally = new FlareNet();
        for (int i = 0; i < 4; i++)
        {
            FlareLiveRules.Complained(tally, FlareReport.Litter, "d7", 20, false);
            FlareLiveRules.Complained(tally, FlareReport.Transit, "d7", 20, false);
        }
        Check(!FlareLiveRules.Escalate(tally, FlareReport.Litter, "d7", 21, false) && !FlareLiveRules.Escalate(tally, FlareReport.Transit, "d7", 21, true)
              && !FlareLiveRules.Escalate(tally, FlareReport.Utilities, "d7", 21, true) && FlareLiveRules.Escalate(tally, FlareReport.Litter, "d7", 21, true),
              "Flare review: ignored complaints reach the paper and pull on approval only while the office acts, and never about the buses or the power");
        string live = (ReviewSource("Social/Flare/FlareDirector.Live.cs") + ReviewSource("Social/Flare/FlareDirector.Cases.cs") + ReviewSource("Social/Flare/FlareDirector.Posting.cs"));
        Check(FlareMethod(live, "private void Escalation(FlareCase c, int day)").Contains("FlareLiveRules.EscalationOrder(_file.Net, c, Now, Settings.FlareCityOffice)")
              && FlareMethod(live, "private void Escalation(FlareCase c, int day)").Contains("FlareLiveRules.Escalate(_file.Net, c.Kind, c.District, day, Settings.FlareCityOffice)"),
              "Flare review: the director's escalation goes by those rules");
        var store = new FlareNet();
        FlareLiveStore.Read(store, FlareLiveStore.Write(net));
        Check(store.Cases.Count == net.Cases.Count && store.Cases[0].ActedDay == 29 && store.Cases[store.Cases.Count - 1].ActedDay == -1,
              "Flare review: the day a crew was sent is saved with its case");
    }

    // ---------------------------------------------------------------- 8, 9 and the camera

    private static void FlareReviewStart()
    {
        var net = new FlareNet { CityDay = 4 };
        Check(FlareLiveRules.StartDay(net, 5, false) && !FlareLiveRules.StartDay(net, 4, false) && !FlareLiveRules.StartDay(net, 5, true),
              "Flare review: when Flare starts on a day the city has not posted yet, it posts now (not on a following guest)");
        string flare = ReviewSource("Social/Flare/FlareDirector.cs");
        string start = FlareMethod(flare, "private void StartUp()");
        Check(start.Contains("if (FlareLiveRules.StartDay(file.Net, day, Following))") && start.IndexOf("LiveDay(day)", StringComparison.Ordinal) > start.IndexOf("_running = true", StringComparison.Ordinal),
              "Flare review: the director makes the day's feed as it starts (after the account is in place)");
        string links = ReviewSource("Wiring/FlareLiveLinks.cs");
        Check(FlareMethod(flare, "private void StandDown(bool keep)").Contains("FlareBridges.Watched?.Invoke(false, false)")
              && FlareMethod(links, "private static void Watched(bool patrol, bool clean)").Contains("CityWatch.Set(patrol)"),
              "Flare review: the patrols Flare asked for stop when Flare stops (Crime/CityWatch back to as it was)");
        string camera = ReviewSource("Social/Flare/FlareDirector.Camera.cs");
        string shoot = FlareMethod(camera, "private IEnumerator Shoot()");
        int fin = shoot.IndexOf("finally", StringComparison.Ordinal);
        Check(fin > shoot.IndexOf("FlareLiveGame.Capture()", StringComparison.Ordinal) && shoot.IndexOf("_shooting = false", StringComparison.Ordinal) > fin
              && shoot.IndexOf("FlareLiveGame.Drop(left)", StringComparison.Ordinal) > fin && Regex.Matches(shoot, "_shooting = false").Count == 1
              && FlareMethod(camera, "private void TakeShot()").Contains("GameConfirm.IsOpen"),
              "Flare review: the camera lets the screen's copy go and is free again whatever happens after the read, and takes no shot with the game's question box up");
    }

    // ---------------------------------------------------------------- pictures

    private static void FlareReviewPictures()
    {
        var rates = new FlareRateBook();
        int pictures = 0;
        for (int i = 0; i < 100; i++)
        {
            pictures += rates.Allow("guest", 'X', i, 3) ? 1 : 0;
        }
        var big = new FlareRateBook();
        int pieces = 0;
        for (int pic = 0; pic < 6; pic++)
        {
            for (int k = 0; k < FlareSnap.PartsMost; k++)
            {
                pieces += big.Allow("guest", 'X', pic * 10 + k, 3, k) ? 1 : 0;
            }
        }
        Check(pictures == FlareRateBook.PicturesDay && pieces == FlareRateBook.PicturesDay * FlareSnap.PartsMost && rates.Allow("other", 'X', 1f, 3) && rates.Allow("guest", 'X', 1f, 4),
              "Flare review: the host counts a guest's pictures, not their pieces: four a day however few pieces each has; each player alone; a new day again");
        var book = new FlarePieceBook();
        for (int g = 0; g < FlarePieceBook.Most; g++)
        {
            book.Add("g" + g, new FlareAsk { Kind = 'X', PicId = "g" + g + "/1", Index = 0, Count = 2, Text = "QUJD" }, 10f);
        }
        // Eight on their way, one of each guest's: a ninth guest's is refused, and the eight are all still there.
        book.Add("late", new FlareAsk { Kind = 'X', PicId = "late/1", Index = 0, Count = 2, Text = "QUJD" }, 20f);
        string lateLater = book.Add("late", new FlareAsk { Kind = 'X', PicId = "late/1", Index = 1, Count = 2, Text = "REVG" }, 20.5f);
        bool full = book.Count == FlarePieceBook.Most;
        string finished = book.Add("g3", new FlareAsk { Kind = 'X', PicId = "g3/1", Index = 1, Count = 2, Text = "REVG" }, 21f);
        // A guest's new one, the book full again: its own oldest gives way, nobody else's.
        book.Add("h", new FlareAsk { Kind = 'X', PicId = "h/1", Index = 0, Count = 2, Text = "QUJD" }, 21.5f);
        book.Add("g5", new FlareAsk { Kind = 'X', PicId = "g5/2", Index = 0, Count = 2, Text = "QUJD" }, 22f);
        string g0 = book.Add("g0", new FlareAsk { Kind = 'X', PicId = "g0/1", Index = 1, Count = 2, Text = "REVG" }, 22.5f);
        string g5new = book.Add("g5", new FlareAsk { Kind = 'X', PicId = "g5/2", Index = 1, Count = 2, Text = "REVG" }, 23f);
        string stranger = book.Add("g6", new FlareAsk { Kind = 'X', PicId = "g7/1", Index = 1, Count = 2, Text = "REVG" }, 23f);
        // Two minutes on, a picture nobody sends more of gives way.
        book.Add("i", new FlareAsk { Kind = 'X', PicId = "i/1", Index = 0, Count = 2, Text = "QUJD" }, 23.5f);
        book.Add("j", new FlareAsk { Kind = 'X', PicId = "j/1", Index = 0, Count = 2, Text = "QUJD" }, 24f);
        bool fresh = book.Add("k", new FlareAsk { Kind = 'X', PicId = "k/1", Index = 0, Count = 2, Text = "QUJD" }, 25f) == null && book.Count == FlarePieceBook.Most;
        book.Add("k", new FlareAsk { Kind = 'X', PicId = "k/1", Index = 0, Count = 2, Text = "QUJD" }, 300f);
        string kDone = book.Add("k", new FlareAsk { Kind = 'X', PicId = "k/1", Index = 1, Count = 2, Text = "REVG" }, 301f);
        Check(lateLater == null && full && finished == "QUJDREVG" && g0 == "QUJDREVG" && g5new == "QUJDREVG" && stranger == null && fresh && kDone == "QUJDREVG",
              "Flare review: a ninth picture on its way never clears another player's: refused while they are fresh; a player's new one takes the place of its own oldest; one gone stale gives way");
        var p = new FlarePost { Source = FlareSource.Photo, Name = @"C:\Users\Somebody\AppData\LocalLow\Hovgaard Games\Big Ambitions\Photos\shot_01.png", Token = 3 };
        string ask = FlareNetShare.AskPost(p, -1, "Guest");
        var shop = new FlarePost { Source = FlareSource.Review, Arg = 4, Place = "Main~3", Name = "24/7 Shop" };
        Check(ask.IndexOf("Users", StringComparison.Ordinal) < 0 && ask.Contains("shot_01.png") && FlareNetShare.NameOut(shop) == "24/7 Shop"
              && FlareNetShare.NameOut(new FlarePost { Source = FlareSource.Life, Name = @"D:\Games\x.png" }) == "x.png",
              "Flare review: a photo's path never leaves this machine (only the file's name: the Windows user's name stays home); a business's name as it is");
    }

    // ---------------------------------------------------------------- shares and likes once a player

    private static void FlareReviewShares()
    {
        var liked = new HashSet<string>();
        var shared = new HashSet<string>();
        var p = new FlarePost { Id = 5, NetId = 5 };
        int changes = 0;
        for (int i = 0; i < 200; i++)
        {
            changes += FlareNetShare.Count(p, new FlareAsk { Kind = 'S', NetId = 5 }, "guest", liked, shared) ? 1 : 0;
        }
        changes += FlareNetShare.Count(p, new FlareAsk { Kind = 'S', NetId = 5 }, "other", liked, shared) ? 1 : 0;
        bool likes = FlareNetShare.Count(p, new FlareAsk { Kind = 'L', NetId = 5, On = true }, "guest", liked, shared) && !FlareNetShare.Count(p, new FlareAsk { Kind = 'L', NetId = 5, On = true }, "guest", liked, shared)
                     && FlareNetShare.Count(p, new FlareAsk { Kind = 'L', NetId = 5, On = false }, "guest", liked, shared) && !FlareNetShare.Count(p, new FlareAsk { Kind = 'L', NetId = 5, On = false }, "guest", liked, shared);
        Check(changes == 2 && p.Reposts == 2 && likes && p.PlayerLikes == 0,
              "Flare review: a player shares a post once and likes it once (and takes it back once), however often they ask");
    }

    // ---------------------------------------------------------------- joining

    private static void FlareReviewJoin()
    {
        Check(FlareNetShare.AppliesOnArrival(new FlarePost { Source = FlareSource.Review, Day = 9 }, 9) && !FlareNetShare.AppliesOnArrival(new FlarePost { Source = FlareSource.Review, Day = 8 }, 9)
              && !FlareNetShare.AppliesOnArrival(new FlarePost { Source = FlareSource.Announce, Day = 9 }, 9),
              "Flare review: on joining, only today's reviews move a business here (the earlier ones have had their days)");
        FlareFile f = FlareLiveFile(43);
        var mine = new FlarePost { Id = f.NextId++, Author = FlareAuthor.Player, Source = FlareSource.Life, Day = 9 };
        f.Posts.Add(mine);
        f.Posts.Add(new FlarePost { Id = f.NextId++, Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk, Day = 9, ReplyTo = -1 });
        f.Posts.Add(new FlarePost { Id = f.NextId++, Author = FlareAuthor.Official, Source = FlareSource.Official, Day = 9, ReplyTo = -1 });
        f.Posts.Add(new FlarePost { Id = f.NextId++, Author = FlareAuthor.Citizen, Source = FlareSource.Reply, Day = 9, ReplyTo = mine.Id });
        f.Posts.Add(new FlarePost { Id = f.NextId++, Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk, Day = 8, ReplyTo = -1 });
        f.Posts.Add(new FlarePost { Id = f.NextId++, Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk, Day = 9, Shared = true, NetId = 4, ReplyTo = -1 });
        Check(FlareNetShare.DropOwnCity(f, 9) == 2 && f.Posts.Count == 4 && FlareNetShare.DropOwnCity(f, 9) == 0,
              "Flare review: a guest that starts following drops its own city's posts of today (a reply to its player's post stays, and the host's and earlier days' too)");
    }

    // ---------------------------------------------------------------- the Ledger's pieces

    private static void FlareReviewLedgerPieces()
    {
        var pieces = new List<KeyValuePair<string, LedgerPiece>>();
        Func<string, string> filter = s => FlareFilter.Clean(s, FlareFilter.Standard);
        string bad = new string(new[] { 's', 'h', 'i', 't' });
        int a = 0;
        for (int i = 0; i < 10; i++)
        {
            a += LedgerShare.Admit(pieces, "a", new LedgerPiece((LedgerSection)0, 50, "reallife_flare_news_x", null, new[] { "name", "A" + i }), filter) ? 1 : 0;
        }
        bool b = LedgerShare.Admit(pieces, "b", new LedgerPiece((LedgerSection)0, 50, "reallife_flare_news_x", null, new[] { "name", bad + " B" }), filter);
        bool twice = LedgerShare.Admit(pieces, "c", new LedgerPiece((LedgerSection)0, 50, "reallife_flare_news_y", null, new[] { "name", "C" }), filter)
                     && !LedgerShare.Admit(pieces, "c", new LedgerPiece((LedgerSection)0, 50, "reallife_flare_news_y", null, new[] { "name", "C" }), filter);
        Check(a == LedgerShare.PiecesPerGuestDay && b && twice && pieces[LedgerShare.PiecesPerGuestDay].Value.Data[1].IndexOf(bad, StringComparison.Ordinal) < 0,
              "Flare review: the host takes a few of each guest's pieces a day, each once, so one guest never fills the paper for the others, and their names go through the filter");
        for (int g = 0; g < 20; g++)
        {
            for (int i = 0; i < 4; i++)
            {
                LedgerShare.Admit(pieces, "g" + g, new LedgerPiece((LedgerSection)0, 50, "reallife_flare_news_z", null, new[] { "n", g + "_" + i }), null);
            }
        }
        Check(pieces.Count == LedgerShare.GuestPiecesMost, "Flare review: the guests' pieces of a day are bounded in all");
        float at = LedgerShare.ReprintAt(-1f, 100f, 20f);
        Check(at == 120f && LedgerShare.ReprintAt(at, 115f, 20f) == 120f, "Flare review: today's edition is printed again a while after the first guest's piece, not put off by each that follows");
        string share = ReviewSource("Ledger/LedgerDirector.Share.cs");
        string ledger = ReviewSource("Ledger/LedgerDirector.cs");
        Check(FlareMethod(share, "private bool GuestApply(string text, bool whole)").Contains("_ownToday = _state.Edition")
              && FlareMethod(share, "private void OwnAgain(int day)").Contains("_state.Edition = own") && ledger.Contains("OwnAgain(day);")
              && FlareMethod(share, "private void HostRequest(string text, string who)").Contains("LedgerShare.Admit(") && FlareMethod(share, "private void HostRequest(string text, string who)").Contains("LedgerShare.ReprintAt("),
              "Flare review: a guest's own edition of the day is kept when the host's takes its place, and back if it stops following that day; the host admits pieces by those rules");
    }

    // ---------------------------------------------------------------- the rival tycoons

    private static void FlareReviewTycoons()
    {
        var ts = new List<FlareTycoon> { new FlareTycoon { Index = 0, Name = RivalCatalogue.Tycoons[0].Name }, new FlareTycoon { Index = 2, Name = RivalCatalogue.Tycoons[2].Name } };
        int posts = 0, voiced = 0;
        bool same = true;
        for (int day = 1; day <= 60; day++)
        {
            FlareFile f = FlareLiveFile(44), g = FlareLiveFile(44);
            FlarePost p = FlareLiveRules.TycoonPost(f, ts, day), q = FlareLiveRules.TycoonPost(g, ts, day);
            same &= (p == null) == (q == null) && (p == null || (p.Name == q.Name && p.Arg == q.Arg && p.At == q.At));
            if (p != null)
            {
                posts++;
                voiced += p.Author == FlareAuthor.Tycoon && p.Arg < FlareLiveRules.TycoonLines && (p.Who == 0 || p.Who == 2) && p.Name == RivalCatalogue.Tycoons[p.Who].Name ? 1 : 0;
            }
        }
        ts[1].Move = (int)RivalMoveKind.Lobby;
        FlarePost news = null;
        for (int day = 1; day <= 60 && news == null; day++)
        {
            news = FlareLiveRules.TycoonPost(FlareLiveFile(44), ts, day);
        }
        Check(posts >= 10 && posts <= 35 && voiced == posts && same && news != null && news.Who == 2 && news.Arg == FlareLiveRules.TycoonMoveArg + (int)RivalMoveKind.Lobby
              && FlareLiveRules.TycoonPost(FlareLiveFile(44), new List<FlareTycoon>(), 5) == null,
              "Flare review: the rival tycoons post on about a third of days, each in their own voice, about their move first when they have just announced one; none without rivals");
        Check(RivalCatalogue.TycoonCount == FlareLiveRules.TycoonVoices && (int)RivalMoveKind.Count == FlareLiveRules.TycoonMoves,
              "Flare review: every tycoon has a voice on Flare and every kind of move a line");
        string links = ReviewSource("Wiring/FlareLiveLinks.cs");
        Check(links.Contains("FlareBridges.Tycoons = Tycoons;") && FlareMethod(links, "private static void Tycoons(List<FlareTycoon> into)").Contains("!rivals.Resting")
              && FlareMethod(links, "private static void Tycoons(List<FlareTycoon> into)").Contains("t.Active"),
              "Flare review: the tycoons in play come from the rivals (not while they rest, not once taken over)");
    }
}
