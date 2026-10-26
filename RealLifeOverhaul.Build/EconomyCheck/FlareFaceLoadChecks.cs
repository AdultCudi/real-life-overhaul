using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;

// Flare's faces drawn ahead of need, and kept on disk to be loaded from there after: behind the loading screen the faces
// likely to be seen first, within two seconds; the rest behind the black screens and, for a card in view, with Flare
// open. Held here: which faces are likely and in what order, each once and only faces; a face on disk is never drawn
// again; the budget never starts a face it cannot finish in time; the textures kept hold a whole city and the oldest goes
// past the bound; the folder's bound and its clean-up, oldest first, never a face the load wants; every face of the
// city's people has the person's own sex and age; the save is read without being changed; and the sources draw only
// behind the loading screen, a black screen or with Flare open, hold the load for two seconds at most and send nothing.
internal static partial class Program
{
    private static void FlareFaceLoad()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        FaceLoadLikely();
        FaceLoadOnce();
        FaceLoadBudget();
        FaceLoadCache();
        FaceLoadDisk();
        FaceLoadLooks();
        FaceLoadSave();
        FaceLoadSources();
        FaceLoadCost();
        Console.WriteLine("  flare faces at load: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A city's account with friends (Mom, Alex, a partner), influencers, the city's posts with comments, a post of the
    // player's with reactions, and messages.
    private static FlareAvatarFacts FaceLoadFacts(int posts)
    {
        FlareFile f = FlareRules.Open(9, 30, 3, 50);
        f.Net.CitySeed = 5151;
        f.Influencers.Add(new FlareInfluencer { Id = 1, Handle = "@one", Seed = 301 });
        f.Influencers.Add(new FlareInfluencer { Id = 2, Handle = "@gone", Seed = 302, Quit = true });
        f.Influencers.Add(new FlareInfluencer { Id = 3, Handle = "@three", Seed = 303 });
        for (int i = 0; i < posts; i++)
        {
            f.Posts.Add(new FlarePost { Id = 100 + i, Day = 30, Author = FlareAuthor.Citizen, Who = (i * 37) % FlareLiveRules.Citizens, Source = FlareSource.CityTalk, Replies = 6, Reach = 900, Likes = 40, Seed = 7 * i + 1 });
        }
        f.Posts.Add(new FlarePost { Id = 900, Day = 30, Author = FlareAuthor.Player, Source = FlareSource.Photo, Reactors = "L:f0:12;R:c77:40;L:s:Robin:90;L:b:100", Seed = 99 });
        f.Net.Dms.Add(new FlareDm { Id = 1, With = "c:123", Text = "hi" });
        f.Net.Dms.Add(new FlareDm { Id = 2, With = "c:124", Mine = true, Text = "hello" });
        var people = new List<FlarePerson>
        {
            new FlarePerson { Index = 0, First = "Mom", Female = FixedPeople.MomFemale, Age = FixedPeople.MomAge, LookSeed = FixedPeople.MomSeed },
            new FlarePerson { Index = 1, First = "Alex", Female = FixedPeople.AlexFemale, Age = FixedPeople.AlexAge, LookSeed = FixedPeople.AlexSeed },
            new FlarePerson { Index = 5, First = "Sam", Female = true, Age = 29, LookSeed = 4455, Partner = true }
        };
        return new FlareAvatarFacts { File = f, People = people, CitySeed = FlareLiveRules.CitySeed(f), MyBusinesses = new List<string>() };
    }

    private static bool HasFace(List<FlareAvatarPlan> plans, FlareAvatarPlan face)
    {
        for (int i = 0; i < plans.Count; i++)
        {
            if (plans[i].Picture == face.Picture)
            {
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- which faces, in what order

    private static void FaceLoadLikely()
    {
        FlareAvatarFacts facts = FaceLoadFacts(30);
        var plans = new List<FlareAvatarPlan>();
        int n = FlareFaceAheadRules.Likely(facts, 30, FlareFaceAheadRules.LoadMost, plans);
        FlareAvatarPlan partner = FlareAvatarRules.Plan("p5", facts), mom = FlareAvatarRules.Plan("f0", facts), alex = FlareAvatarRules.Plan("f1", facts);
        Check(n == plans.Count && n > 0 && plans[0].Picture == partner.Picture && plans[1].Picture == mom.Picture && plans[2].Picture == alex.Picture
              && mom.Seed == FixedPeople.MomSeed && alex.Seed == FixedPeople.AlexSeed,
            "Flare faces at load: the partner first, then the people met (Mom and Alex among them, each with their one look)");
        FlareAvatarPlan one = FlareAvatarRules.Plan("i:1", facts), gone = FlareAvatarRules.Plan("i:2", facts), three = FlareAvatarRules.Plan("i:3", facts);
        Check(plans[3].Picture == one.Picture && plans[4].Picture == three.Picture && !HasFace(plans, gone),
            "Flare faces at load: then the active influencers; one who has quit is not drawn ahead");
        FlareAvatarPlan newest = FlareAvatarRules.Plan("c" + facts.File.Posts[facts.File.Posts.Count - 2].Who, facts);
        FlareAvatarPlan staff = FlareAvatarRules.Plan("s:Robin", facts), reactor = FlareAvatarRules.Plan("c77", facts), boss = FlareAvatarRules.Plan("b", facts);
        int at = -1, atOld = -1;
        FlareAvatarPlan oldest = FlareAvatarRules.Plan("c" + facts.File.Posts[0].Who, facts);
        for (int i = 0; i < plans.Count; i++)
        {
            at = plans[i].Picture == newest.Picture && at < 0 ? i : at;
            atOld = plans[i].Picture == oldest.Picture && atOld < 0 ? i : atOld;
        }
        Check(HasFace(plans, staff) && HasFace(plans, reactor) && HasFace(plans, boss) && staff.Kind == FlareAvatarKind.Staff,
            "Flare faces at load: the people with names who reacted to the player's post (a friend, one of the city's, one of the staff, the boss)");
        var comments = new List<FlareComment>();
        FlarePost last = facts.File.Posts[facts.File.Posts.Count - 2];
        FlareCommentRules.Plan(last, facts.CitySeed, FlareLiveRules.Citizens, null, FlareTaste.None, null, comments);
        bool commenters = comments.Count > 0;
        for (int i = 0; i < comments.Count; i++)
        {
            commenters &= HasFace(plans, FlareAvatarRules.Plan("c" + comments[i].Who, facts));
        }
        Check(at >= 0 && commenters && (atOld < 0 || atOld > at), "Flare faces at load: the newest post's author and the people who comment under it, before the older posts'");
        bool once = true, faces = true, cards = true;
        var pictures = new HashSet<string>();
        for (int i = 0; i < plans.Count; i++)
        {
            once &= pictures.Add(plans[i].Picture);
            faces &= plans[i].Kind == FlareAvatarKind.Face || plans[i].Kind == FlareAvatarKind.Staff;
        }
        Check(once && faces && n <= FlareFaceAheadRules.LoadMost, "Flare faces at load: each face once, only faces (no logo, emblem or selfie), at most " + FlareFaceAheadRules.LoadMost);
        var few = new List<FlareAvatarPlan>();
        FlareAvatarFacts small = FaceLoadFacts(3);
        FlareFaceAheadRules.Likely(small, 30, FlareFaceAheadRules.LoadMost, few);
        Check(HasFace(few, FlareAvatarRules.Plan("c123", small)) && !HasFace(few, FlareAvatarRules.Plan("c124", small)) && FlareFaceAheadRules.ThreadKey("c:123") == "c123" && FlareFaceAheadRules.ThreadKey("c:Ada Barlow") == "c:Ada Barlow"
              && FlareFaceAheadRules.ThreadKey(null) == null, "Flare faces at load: the city's people in the message threads, by the key their card shows (not one the player alone wrote in)");
        // Tomorrow's posters: with no posts at all, the people who write tomorrow's posts.
        FlareAvatarFacts quiet = FaceLoadFacts(0);
        quiet.File.Posts.Clear();
        quiet.File.Net.Dms.Clear();
        quiet.People = null;
        quiet.File.Influencers.Clear();
        var next = new List<FlareAvatarPlan>();
        FlareFaceAheadRules.Likely(quiet, 30, FlareFaceAheadRules.AheadMost, next);
        bool posters = next.Count > 0;
        for (int k = 0; k < FlareLiveRules.CityTalkPerDay[FlareLiveRules.CityTalkPerDay.Length - 1]; k++)
        {
            posters &= HasFace(next, FlareAvatarRules.Plan("c" + FlareLiveRules.TalkCitizen(quiet.CitySeed, 31, k), quiet));
        }
        Check(posters, "Flare faces ahead: the city's people who write tomorrow's posts");
        // TalkCitizen is the very pick the day's posts make.
        bool same = true;
        int talked = 0;
        for (int day = 20; day < 40; day++)
        {
            FlareFile f = FlareLiveFile(31);
            FlareCityFacts city = FlareFakeCity(day, 2, false);
            List<FlareCitizen> all = FlareCitizensOf(f, city);
            FlareLiveRules.CityDay(f, city, all, null);
            var picks = new HashSet<int>();
            for (int k = 0; k < FlareLiveRules.CityTalkPerDay[2]; k++)
            {
                picks.Add(FlareLiveRules.TalkCitizen(FlareLiveRules.CitySeed(f), day, k));
            }
            foreach (FlarePost p in f.Posts)
            {
                if (p.Author == FlareAuthor.Citizen && p.Day == day)
                {
                    same &= picks.Contains(p.Who);
                    talked++;
                }
            }
        }
        Check(same && talked > 100, "Flare faces ahead: the people predicted for a day are the ones the day's posts are written by (" + talked + " posts over 20 days)");
        var again = new List<FlareAvatarPlan>();
        FlareFaceAheadRules.Likely(FaceLoadFacts(30), 30, FlareFaceAheadRules.LoadMost, again);
        bool alike = again.Count == plans.Count;
        for (int i = 0; alike && i < plans.Count; i++)
        {
            alike = again[i].Picture == plans[i].Picture;
        }
        var many = new List<FlareAvatarPlan>();
        Check(alike && FlareFaceAheadRules.Likely(FaceLoadFacts(200), 30, FlareFaceAheadRules.LoadMost, many) == FlareFaceAheadRules.LoadMost
              && FlareFaceAheadRules.Likely(FaceLoadFacts(200), 30, FlareFaceAheadRules.AheadMost, many) == FlareFaceAheadRules.AheadMost
              && FlareFaceAheadRules.Likely(null, 30, 10, many) == 0 && FlareFaceAheadRules.Likely(FaceLoadFacts(5), 30, 0, many) == 0
              && FlareFaceAheadRules.LoadMost <= FlareFaceAheadRules.AheadMost,
            "Flare faces at load: the same list every time; a busy feed fills the most and no more; none for nothing; the load's faces fit among those waiting ahead");
    }

    // ---------------------------------------------------------------- once per look

    private static void FaceLoadOnce()
    {
        FlareAvatarFacts facts = FaceLoadFacts(40);
        var plans = new List<FlareAvatarPlan>();
        FlareFaceAheadRules.Likely(facts, 30, FlareFaceAheadRules.LoadMost, plans);
        var disk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var draw = new List<FlareAvatarPlan>();
        int there = FlareFaceAheadRules.Split(plans, disk, "save1", draw);
        Check(there == 0 && draw.Count == plans.Count, "Flare faces at load: with nothing on disk every likely face is drawn");
        // The load draws the first 25 (its budget), the rest wait; the next load draws only what it did not.
        for (int i = 0; i < 25; i++)
        {
            disk.Add(FlareFaceAheadRules.FileName(draw[i], "save1"));
        }
        there = FlareFaceAheadRules.Split(plans, disk, "save1", draw);
        bool none = true;
        for (int i = 0; i < draw.Count; i++)
        {
            none &= !disk.Contains(FlareFaceAheadRules.FileName(draw[i], "save1"));
        }
        Check(there == 25 && draw.Count == plans.Count - 25 && none, "Flare faces at load: a face on disk is never drawn again; the next load takes up where the last stopped");
        for (int i = 0; i < plans.Count; i++)
        {
            disk.Add(FlareFaceAheadRules.FileName(plans[i], "save1"));
        }
        Check(FlareFaceAheadRules.Split(plans, disk, "save1", draw) == plans.Count && draw.Count == 0, "Flare faces at load: once every face is on disk, a load draws none (once per look)");
        // Another save of the same city shares the city's faces; one of the staff is the save's own.
        FlareAvatarPlan staff = FlareAvatarRules.Plan("s:Robin", facts);
        disk.Add(FlareFaceAheadRules.FileName(staff, "save1"));
        disk.Add(FlareFaceAheadRules.FileName(FlareAvatarRules.Plan("c5", facts), "save1"));
        var only = new List<FlareAvatarPlan> { staff, FlareAvatarRules.Plan("c5", facts) };
        Check(FlareFaceAheadRules.Split(only, disk, "save2", draw) == 1 && draw.Count == 1 && draw[0].Kind == FlareAvatarKind.Staff
              && FlareFaceAheadRules.FileName(staff, "save1") != FlareFaceAheadRules.FileName(staff, "save2")
              && FlareFaceAheadRules.FileName(FlareAvatarRules.Plan("c5", facts), "save1") == FlareFaceAheadRules.FileName(FlareAvatarRules.Plan("c5", facts), "save2"),
            "Flare faces at load: another save of the same city shares the city's faces on disk; one of the staff is the save's own");
        Check(FlareFaceAheadRules.Split(null, disk, "s", draw) == 0 && draw.Count == 0 && FlareFaceAheadRules.Split(plans, null, "s", draw) == 0 && draw.Count == plans.Count,
            "Flare faces at load: no list draws none; no listing of the folder draws them all (each file looked at when its turn comes)");
    }

    // ---------------------------------------------------------------- the time budget

    private static void FaceLoadBudget()
    {
        int[] costs = { 1, 20, 50, 80, 150, 400, 1999, 2500 };
        bool within = true, used = true;
        string said = string.Empty;
        foreach (int cost in costs)
        {
            double spent = 0, drawnMs = 0;
            int drawn = 0;
            while (drawn < 10000 && FlareFaceAheadRules.Another(spent, drawnMs, drawn, FlareFaceAheadRules.LoadBudgetMs))
            {
                spent += cost;   // (a face finished: the hold's time and the face's own)
                drawnMs += cost;
                drawn++;
            }
            // Started only while one more fits at the cost seen so far (the first on the guess): never past the budget once the cost is known.
            within &= drawn <= 1 || drawn * (double)cost <= FlareFaceAheadRules.LoadBudgetMs;
            used &= FlareFaceAheadRules.LoadBudgetMs - spent < cost;
            said += " " + cost + " ms: " + drawn;
        }
        Check(within && used, "Flare faces at load: faces start only while the time left holds one more, and the time is used (" + said.Trim() + ")");
        Check(FlareFaceAheadRules.Another(0, 0, 0, FlareFaceAheadRules.LoadBudgetMs) && !FlareFaceAheadRules.Another(FlareFaceAheadRules.LoadBudgetMs - FlareFaceAheadRules.FirstFaceMs + 1, 0, 0, FlareFaceAheadRules.LoadBudgetMs)
              && !FlareFaceAheadRules.Another(-1, 0, 0, FlareFaceAheadRules.LoadBudgetMs) && !FlareFaceAheadRules.Another(0, 0, 0, 0)
              && FlareFaceAheadRules.Each(300, 3) == 100 && FlareFaceAheadRules.Each(5, 0) == 0,
            "Flare faces at load: the first face is guessed at " + FlareFaceAheadRules.FirstFaceMs + " ms; none with no time; the cost a face is the mean of those drawn");
        string hold = ReviewSource("Core/LoadHold.cs");
        Check(hold.Contains("public const float MostSeconds = 2f;") && Math.Abs(FlareFaceAheadRules.LoadBudgetMs - 2000.0) < 0.001,
            "Flare faces at load: the faces' budget is the load hold's own two seconds");
    }

    // ---------------------------------------------------------------- the textures kept

    private static void FaceLoadCache()
    {
        int city = FlareLiveRules.Citizens + 60 + 12;   // the city's people, the active influencers at their most, the friends
        Check(FlareAvatarRules.Most >= city && (long)FlareAvatarRules.Most * FlareAvatarRules.Size * FlareAvatarRules.Size * 4 <= 20L * 1024 * 1024,
            "Flare faces: the textures kept hold a whole city's faces (" + city + " of at most " + FlareAvatarRules.Most + ") within 20 MB");
        var lru = new FlareAvatarLru(FlareAvatarRules.Most);
        int gone = 0;
        for (int round = 0; round < 3; round++)
        {
            for (int i = 0; i < city; i++)
            {
                gone += lru.Use("face:" + i) != null ? 1 : 0;
            }
        }
        Check(gone == 0 && lru.Count == city, "Flare faces: a city's faces read once stay for the session, however often they are shown");
        var order = new List<string>();
        for (int i = city; i < FlareAvatarRules.Most + 40; i++)
        {
            string out1 = lru.Use("face:" + i);
            if (out1 != null)
            {
                order.Add(out1);
            }
        }
        bool oldest = order.Count == 40;
        for (int i = 0; i < order.Count; i++)
        {
            oldest &= order[i] == "face:" + i;
        }
        Check(oldest && lru.Count == FlareAvatarRules.Most, "Flare faces: past the bound the faces used longest ago go first, one for one");
        lru.Use("face:40");   // (used again: no longer the oldest)
        string next = lru.Use("new:1");
        Check(next == "face:41" && lru.Contains("face:40") && !lru.Contains("face:41"), "Flare faces: a face used again is kept over an older one");
        lru.Remove("face:42");
        lru.Remove("absent");
        lru.Remove(null);
        Check(lru.Count == FlareAvatarRules.Most - 1 && lru.Use("new:2") == null, "Flare faces: one taken out leaves room for the next");
        lru.Clear();
        Check(lru.Count == 0 && !lru.Contains("face:40") && lru.Use("x") == null && lru.Count == 1, "Flare faces: all go when the city unloads, and the keeping starts again");
        var keys = new string[FlareAvatarRules.Most * 2];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = "k" + i;
        }
        var warm = new FlareAvatarLru(FlareAvatarRules.Most);
        LruMany(warm, keys, keys.Length * 2);
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        double ms = LruTime(warm, keys, 200000);
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Check(bytes < 1024 && ms < 200.0, "Flare faces: 200,000 uses of the kept textures, half of them past the bound, in " + ms.ToString("0.0") + " ms, " + bytes + " bytes allocated (none once the bound is reached)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void LruMany(FlareAvatarLru lru, string[] keys, int n)
    {
        for (int i = 0; i < n; i++)
        {
            lru.Use(keys[(i * 7) % keys.Length]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double LruTime(FlareAvatarLru lru, string[] keys, int n)
    {
        var watch = Stopwatch.StartNew();
        LruMany(lru, keys, n);
        return watch.Elapsed.TotalMilliseconds;
    }

    // ---------------------------------------------------------------- the folder's bound

    private static void FaceLoadDisk()
    {
        var files = new List<FlareFaceFile>();
        var gone = new List<string>();
        for (int i = 0; i < FlareFaceAheadRules.DiskMost; i++)
        {
            files.Add(new FlareFaceFile { Name = "flare96_" + i + ".thumb", Touched = 1000 + i });
        }
        Check(FlareFaceAheadRules.Evict(files, null, gone) == 0 && gone.Count == 0 && FlareFaceAheadRules.Evict(null, null, gone) == 0,
            "Flare faces on disk: nothing is deleted up to the most of " + FlareFaceAheadRules.DiskMost);
        for (int i = 0; i < 300; i++)
        {
            files.Add(new FlareFaceFile { Name = "flare96_new" + i + ".thumb", Touched = 5000 + (i * 13) % 300 });
        }
        var keep = new HashSet<string> { "flare96_0.thumb", "flare96_5.thumb" };
        int n = FlareFaceAheadRules.Evict(files, keep, gone);
        bool oldestFirst = !gone.Contains("flare96_0.thumb") && !gone.Contains("flare96_5.thumb") && gone.Contains("flare96_1.thumb") && !gone.Contains("flare96_new0.thumb");
        var left = new HashSet<string>();
        foreach (FlareFaceFile f in files)
        {
            left.Add(f.Name);
        }
        foreach (string g in gone)
        {
            left.Remove(g);
        }
        Check(n == files.Count - FlareFaceAheadRules.DiskKeep && left.Count == FlareFaceAheadRules.DiskKeep && oldestFirst,
            "Flare faces on disk: past the most, the faces used longest ago are deleted down to " + FlareFaceAheadRules.DiskKeep + ", never one the load wants (" + n + " of " + files.Count + ")");
        var shuffled = new List<FlareFaceFile>(files);
        shuffled.Reverse();
        var gone2 = new List<string>();
        FlareFaceAheadRules.Evict(shuffled, keep, gone2);
        var ties = new List<FlareFaceFile>();
        for (int i = 0; i < FlareFaceAheadRules.DiskMost + 10; i++)
        {
            ties.Add(new FlareFaceFile { Name = "flare96_t" + (FlareFaceAheadRules.DiskMost + 10 - i).ToString("D5") + ".thumb", Touched = 7 });
        }
        var gone3 = new List<string>();
        FlareFaceAheadRules.Evict(ties, null, gone3);
        Check(string.Join(",", gone) == string.Join(",", gone2) && gone3.Count == FlareFaceAheadRules.DiskMost + 10 - FlareFaceAheadRules.DiskKeep && gone3[0] == "flare96_t00001.thumb",
            "Flare faces on disk: the same files deleted whatever the folder's order; for the same time, by name");
        var all = new HashSet<string>();
        foreach (FlareFaceFile f in files)
        {
            all.Add(f.Name);
        }
        Check(FlareFaceAheadRules.Evict(files, all, gone) == 0 && (long)FlareFaceAheadRules.DiskMost * (FlareAvatarRules.Size * FlareAvatarRules.Size * 3 + 12) <= 60L * 1024 * 1024
              && FlareFaceAheadRules.DiskKeep < FlareFaceAheadRules.DiskMost && FlareFaceAheadRules.DiskKeep >= 3 * (FlareFaceAheadRules.AheadMost + FlareLiveRules.Citizens),
            "Flare faces on disk: the faces wanted are never deleted; the folder holds at most 60 MB, and what is kept holds three cities");
        long day = FlareFaceAheadRules.TouchTicks;
        Check(!FlareFaceAheadRules.Stale(100, 100 + day) && FlareFaceAheadRules.Stale(100, 101 + day) && !FlareFaceAheadRules.Stale(100, 50),
            "Flare faces on disk: a face read more than a day after its file was written has its time brought forward, so the faces in use are never the oldest");
    }

    // ---------------------------------------------------------------- the age and sex of the city's faces

    private static void FaceLoadLooks()
    {
        bool match = true;
        int women = 0;
        foreach (int seed in new[] { 1, 4242, 5151, -77, int.MaxValue })
        {
            var citizens = new List<FlareCitizen>();
            FlareLiveRules.CitizensOf(seed, null, citizens);
            for (int i = 0; i < citizens.Count; i++)
            {
                FlareAvatarPlan p = FlareAvatarRules.Citizen(seed, i);
                match &= p.Kind == FlareAvatarKind.Face && p.Female == citizens[i].Female && p.Age == citizens[i].Age
                         && p.Female == FlareLiveRules.CitizenFemale(seed, i) && p.Age == FlareLiveRules.CitizenAge(seed, i);
                women += p.Female ? 1 : 0;
            }
        }
        Check(match && women > 600 && women < 1400,
            "Flare faces: every one of the city's " + FlareLiveRules.Citizens + " people is drawn of their own sex and age, as their name and their card say, for any city");
        var facts = FaceLoadFacts(0);
        FlareAvatarPlan card = FlareAvatarRules.Plan("c250", facts), rule = FlareAvatarRules.Citizen(facts.CitySeed, 250);
        Check(card.Picture == rule.Picture && card.Age == FlareLiveRules.CitizenAge(facts.CitySeed, 250), "Flare faces: a card of one of the city's people asks for the face the load drew");
    }

    // ---------------------------------------------------------------- the save read as it is

    private static void FaceLoadSave()
    {
        var data = new Dictionary<string, string>();
        SocialState.Load(data, 0, 112, 9, 777);
        SocialState.Get(4).Met = true;
        SocialState.Get(4).Stage = SocialStage.Partner;
        SocialState.Save(data);
        var before = new Dictionary<string, string>(data);
        var met = new List<SocialPerson>();
        SocialState.PeekMet(data, met);
        bool same = met.Count == SocialState.MetCount && met.Count >= 3;
        for (int i = 0; i < met.Count; i++)
        {
            SocialPerson live = SocialState.Get(met[i].Index);
            same &= live != null && live.Met && live.Female == met[i].Female && live.Age == met[i].Age && live.Seed == met[i].Seed && live.Together == met[i].Together;
        }
        bool unchanged = data.Count == before.Count;
        foreach (KeyValuePair<string, string> pair in before)
        {
            unchanged &= data.TryGetValue(pair.Key, out string v) && v == pair.Value;
        }
        Check(same && unchanged && met[0].Seed == FixedPeople.MomSeed && met[1].Seed == FixedPeople.AlexSeed,
            "Flare faces at load: the people met are read from the save as the social life will read them (Mom and Alex with their one look), the save untouched");
        data["reallife:social_people"] = data["reallife:social_people"].Substring(0, data["reallife:social_people"].Length / 2);
        SocialState.PeekMet(data, met);
        Check(met.Count == 0, "Flare faces at load: a cast the social life would make anew gives nobody met");
        SocialState.PeekMet(null, met);
        Check(met.Count == 0, "Flare faces at load: no save data, nobody");

        var flare = new Dictionary<string, string>();
        FlareAvatarFacts facts = FaceLoadFacts(12);
        FlareStore.Save(flare, facts.File);
        var flareBefore = new Dictionary<string, string>(flare);
        FlareFile peeked = FlareStore.Peek(flare), loaded = FlareStore.Load(new Dictionary<string, string>(flare));
        Check(peeked != null && loaded != null && peeked.Posts.Count == facts.File.Posts.Count && loaded.Posts.Count == facts.File.Posts.Count && FlareLiveRules.CitySeed(peeked) == facts.CitySeed
              && peeked.Influencers.Count == facts.File.Influencers.Count && peeked.Net.Dms.Count == facts.File.Net.Dms.Count && flare.Count == flareBefore.Count,
            "Flare faces at load: the account is read from the save as Flare will read it");
        flare[FlareStore.AccountKey] = "FL9|damaged";
        var damaged = new Dictionary<string, string>(flare);
        Check(FlareStore.Peek(damaged) == null && damaged.Count == flare.Count && damaged[FlareStore.AccountKey] == "FL9|damaged" && FlareStore.Peek(null) == null,
            "Flare faces at load: an account that does not read is left exactly as it is (Flare itself keeps it when it starts)");
    }

    // ---------------------------------------------------------------- the sources

    private static void FaceLoadSources()
    {
        string painter = ReviewSource("Social/Flare/FlareFacePainter.cs"), hold = ReviewSource("Core/LoadHold.cs"), mod = ReviewSource("Core/RealLifeOverhaulMod.cs"),
            director = ReviewSource("Social/Flare/FlareDirector.cs"), photos = ReviewSource("Social/Flare/FlareDirector.Photos.cs"), avatars = ReviewSource("Social/Flare/FlareAvatars.cs"),
            rules = ReviewSource("Social/Flare/FlareFaceAheadRules.cs"), turn = ReviewSource("Phone/Contacts/PortraitTurn.cs");
        string now = FlareMethod(painter, "private static FaceMoment Now()");
        Check(now.Contains("FlareFaceMoment.Of(LoadHold.Holding, _loadOn && !_loadStop, LoadScene.isLoading, fading, fading && SceneFinds.Covered, AppShowing, _wanted > 0)")
              && !now.Contains("timeScale") && !now.Contains("SceneFinds.Hidden"),
            "Flare faces: a face is drawn only behind the loading screen held for it, behind the game's black screen at full strength, or with Flare open (never in a pause, a fade or a load not held for it; the moments are NavCheck's face pace)");
        string on = FlareMethod(painter, "private void OnUpdate()");
        int moment = on.IndexOf("FaceMoment moment = Now()", StringComparison.Ordinal), none = on.IndexOf("moment == FaceMoment.None", StringComparison.Ordinal),
            budget = on.IndexOf("FlareFaceAheadRules.Another(", StringComparison.Ordinal), draw = on.IndexOf("Draw(now, moment)", StringComparison.Ordinal),
            appOnly = on.IndexOf("at < 0 && moment == FaceMoment.App", StringComparison.Ordinal);
        Check(moment >= 0 && none > moment && budget > none && appOnly > budget && draw > appOnly && Count(painter, "Draw(now, moment)") == 1
              && Count(painter, "PortraitTurn.Face(") == 1 && Count(painter, "PortraitTurn.Character(") == 1 && FlareMethod(painter, "private void Draw(").Contains("PortraitTurn.Face("),
            "Flare faces: the camera is asked in one place, only after the moment is known, behind the load's budget, and with the app open only for a face in view");
        Check(hold.Contains("public static bool Holding => _hold != null && UI.Load.LoadScene.isLoading;") && FlareMethod(hold, "public static Task Wait()").Contains("Task.WhenAny(hold.Task, Task.Delay(Backstop))")
              && FlareMethod(hold, "private void OnUpdate()").Contains("Spent >= MostSeconds * 1000.0") && FlareMethod(hold, "private void Update()").Contains("HotSpots.Time(HotUpdate)")
              && FlareMethod(hold, "private void Update()").Contains("FrameGuard.Off") && FlareMethod(hold, "private void OnDestroy()").Contains("End()")
              && FlareMethod(hold, "private static void End()").Contains("hold?.TrySetResult(true)"),
            "Flare faces: the load is held only while its loading screen is up, two seconds at most, ended by a timer that needs no frame, by a fault and when the city goes");
        Check(mod.Contains("return LoadHold.Wait();") && mod.Contains("Guard.Add<LoadHold>(_root)") && FlareMethod(director, "private void Awake()").Contains("FlareFacePainter.HoldAtLoad()")
              && FlareMethod(painter, "public static void HoldAtLoad()").Contains("LoadHold.Add("),
            "Flare faces: the mod's start hands the hold to the game, and Flare's faces are its piece");
        string ask = FlareMethod(painter, "private static bool LoadAsk()"), ahead = FlareMethod(painter, "public static void Ahead(");
        Check(ask.Contains("FlareStore.Peek(") && !ask.Contains("FlareStore.Load(") && ask.Contains("FlareBridges.SavedPeople") && ask.Contains("FlareFaceAheadRules.LoadMost")
              && ask.Contains("FlareFaceAheadRules.Split(") && ahead.Contains("FlareFaceAheadRules.Split(") && ahead.Contains("OnDisk") && FlareMethod(painter, "private IEnumerator Finish(").Contains("OnDisk.Add(")
              && FlareMethod(painter, "public static bool LoadStep()").Contains("Task.Run(") && ask.Contains("Task.Run(() => Clean("),
            "Flare faces: the save is only read at load, the folder listed and cleaned on a worker, and a face known on disk is never asked for again");
        Check(FlareMethod(photos, "private void FacesAhead(").Contains("FlareFacePainter.Ahead(") && director.Contains("FacesAhead(day);") && !painter.Contains("FlareLiveRules.Citizens; i++"),
            "Flare faces: the faces ahead are made again when Flare starts and each day, never a loop over all the city's people");
        Check(FlareMethod(painter, "public static void LoadEnd(").Contains("Flare check (faces at load): ") && FlareMethod(painter, "public static void LoadEnd(").Contains(" from disk, ")
              && FlareMethod(painter, "public static void LoadEnd(").Contains(" left for later"),
            "Flare faces: the load says how many were drawn, in what time, how many were on disk and how many are left");
        Check(!painter.Contains("GoingPublic") && !rules.Contains("GoingPublic") && !painter.Contains("FlareNetShare") && !painter.Contains("Send(") && !hold.Contains("GoingPublic"),
            "Flare faces: nothing of the faces is sent in Going Public (each machine draws its own)");
        Check(FlareMethod(avatars, "private IEnumerator Read(").Contains("Touch(file)") && FlareMethod(avatars, "private static void Touch(").Contains("FlareFaceAheadRules.Stale(")
              && FlareMethod(turn, "public static void Give(object who, float now, float rest)").Contains("Math.Max(0f, rest)") && turn.Contains("LoadPaceSeconds = 0f"),
            "Flare faces: a face read brings its file's time forward; behind the load's screen the camera does not rest between faces");
        Check(!rules.Contains("UnityEngine") && !rules.Contains("System.IO"), "Flare faces: the rules are game-free and touch no file");
    }

    private static int Count(string text, string what)
    {
        int n = 0;
        for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0; at = text.IndexOf(what, at + 1, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- the cost

    private static void FaceLoadCost()
    {
        FlareAvatarFacts facts = FaceLoadFacts(200);
        var plans = new List<FlareAvatarPlan>(FlareFaceAheadRules.AheadMost);
        LikelyMany(facts, plans, 3);
        double ms = LikelyTime(facts, plans, 20) / 20.0;
        Check(ms < 10.0, "Flare faces ahead: the likely faces of 200 posts made in " + ms.ToString("0.00") + " ms (at a load, when Flare starts and once a game day)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void LikelyMany(FlareAvatarFacts facts, List<FlareAvatarPlan> plans, int n)
    {
        for (int i = 0; i < n; i++)
        {
            FlareFaceAheadRules.Likely(facts, 30, FlareFaceAheadRules.AheadMost, plans);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double LikelyTime(FlareAvatarFacts facts, List<FlareAvatarPlan> plans, int n)
    {
        var watch = Stopwatch.StartNew();
        LikelyMany(facts, plans, n);
        return watch.Elapsed.TotalMilliseconds;
    }
}
