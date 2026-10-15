using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Flare;

// A player, 15 Oct 2026: three gyms run as a brand, store rating over 80, "my brand recognition still isn't increasing",
// and an influencer hired for a campaign "isn't posting anything". Reproduced with the mod's own rules:
//   recognition  three gyms in one district for twenty days, by BrandRules.Day: it does grow, about half a point a day,
//                but the Brands app showed whole points (two mornings at 0) and a city-wide figure that stays near 0 for
//                a brand in one district; the app now shows tenths, says on how many mornings it has moved
//                (BrandRules.Missed) and its "tomorrow" is the step's own sum (BrandRules.GainOn)
//   influencers  a hired influencer who stops posting (the cast's month) made none of the posts paid for
//                (FlareCast.Day skipped every inactive one); the Business tab counted only the player's own posts, so a
//                deal's posts never showed there. Now the paid posts are made to the deal's end and counted on the deal
//                (FlareHire.Posted, LastPost, saved), and the tab and the deal's page say so (FlareCast.PostsAbout, HireStage)
internal static partial class Program
{
    private const string GymType = "ba:businesstype_gym";
    private const string GymDistrict = "ba:neighborhood_midtown";

    private static void BrandRec()
    {
        BrandRecGyms();
        BrandRecTomorrow();
        BrandRecMissed();
        FlareHireQuit();
        FlareHireCounted();
        FlareHireSaved();
        BrandRecWords();
    }

    // Three gyms of a brand in one district, each with standards of 85 (the player's store rating over 80).
    private static BrandsState Gyms(int customers, out Brand brand)
    {
        var s = new BrandsState { Seed = 1015, FirstDay = 1, LastDay = 1 };
        brand = BrandRules.Create(s, "Iron Works", GymType, "Gym~1", GymDistrict, 1);
        BrandRules.Join(s, brand, "Gym~2", GymType, GymDistrict, 1);
        BrandRules.Join(s, brand, "Gym~3", GymType, GymDistrict, 1);
        foreach (Member m in s.Members)
        {
            m.Standards = 85;
            m.Customers = customers;
            m.District = GymDistrict;
        }
        brand.Standards = BrandRules.Standards(s, brand);   // (as the morning's read of the shops leaves it)
        return s;
    }

    private static void BrandRecGyms()
    {
        foreach (int customers in new[] { 0, 120 })   // (a gym's visits counted as customers or not: it grows either way)
        {
            Brand b;
            BrandsState s = Gyms(customers, out b);
            DistrictFile d = b.District(GymDistrict);
            bool rising = true, seenDaily = true;
            int wholeFlat = 0;
            for (int day = 2; day <= 21; day++)
            {
                int before = d.Rec;
                BrandRules.Day(s, b);
                rising &= d.Rec > before;
                seenDaily &= BrandRules.Shown(d.Rec) > BrandRules.Shown(before);
                wholeFlat += d.Rec / 100 == before / 100 ? 1 : 0;
            }
            Check(rising && d.Rec >= 700 && d.Rec <= 1200,
                "brand recognition: three gyms with standards of 85 and " + customers + " customers a day grow every morning, to " + d.Rec + " hundredths after 20 days (7 to 12 points)");
            Check(seenDaily && wholeFlat >= 5,
                "brand recognition: in tenths the app shows a change every morning; in whole points, as it was shown, " + wholeFlat + " of the 20 mornings looked flat");
            Check(b.City * BrandCatalogue.CityDistricts <= d.Rec && BrandRules.Shown(b.City) > 0 && b.City / 100 == 0,
                "brand recognition: city-wide a brand in one district heads for its district's figure divided by " + BrandCatalogue.CityDistricts + " (" + b.City + " hundredths: 0 in whole points, shown now)");
            Check(b.Age == 20 && BrandRules.Missed(b, 21) == 0, "brand recognition: twenty mornings, twenty steps, none missed");
        }
        int quoted = BrandRules.After(3, 100, 75, 20);
        Check(quoted >= 700 && quoted < 1000 && BrandRules.Gain(3, 100, 75, 0) == 47,
            "brand recognition: the Guide's example (three shops, 100 customers, standards of 75, 20 days) is " + quoted + " hundredths, from 0.47 a day");
        Check(BrandRules.Shown(-5) == 0 && BrandRules.Shown(45) == 4 && BrandRules.Shown(BrandCatalogue.RecMost + 50) == 1000, "brand recognition: tenths shown stay within 0 to 100");
    }

    // The district page's "tomorrow" is what the step does, a campaign's first and last day included.
    private static void BrandRecTomorrow()
    {
        Brand b;
        BrandsState s = Gyms(120, out b);
        DistrictFile d = b.District(GymDistrict);
        BrandRules.BookPush(b, BrandCatalogue.PushLocal, GymDistrict);
        bool same = true;
        for (int day = 0; day < BrandRules.PushDays(BrandCatalogue.PushLocal) + 3; day++)
        {
            int said = BrandRules.Step(d.Rec, BrandRules.GainOn(s, b, d, b.Age + 1), true);
            BrandRules.Day(s, b);
            same &= said == d.Rec;
        }
        Check(same, "brand recognition: the district page's tomorrow is the next morning's figure, through a campaign's first and last day");
    }

    private static void BrandRecMissed()
    {
        Brand b;
        BrandsState s = Gyms(120, out b);
        b.Founded = 10;
        for (int i = 0; i < 5; i++)
        {
            BrandRules.Day(s, b);
        }
        Check(BrandRules.Missed(b, 15) == 0 && BrandRules.Missed(b, 20) == 5 && BrandRules.Missed(b, 10) == 0 && BrandRules.Missed(b, 3) == 0,
            "brand recognition: mornings missed are those since the brand began with no step (Brands off), never under nothing");
        Check(BrandRules.StepsKey(0, 0) == "reallife_brand_fig_steps_first" && BrandRules.StepsKey(1, 1) == "reallife_brand_fig_steps_one"
              && BrandRules.StepsKey(1, 0) == "reallife_brand_fig_steps_one_still" && BrandRules.StepsKey(2, 1) == "reallife_brand_fig_steps",
            "brand recognition: the page says it starts tomorrow on the founding day, one morning in the singular, and so many of so many after");
        Check(BrandRules.MissedKey(1) == "reallife_brand_fig_missed_one" && BrandRules.MissedKey(5) == "reallife_brand_fig_missed",
            "brand recognition: one morning missed in the singular, several in the plural");
        b.Age = 400;   // (a test tool's year)
        Check(BrandRules.Missed(b, 20) == 0, "brand recognition: a brand whose clock ran ahead misses nothing");
    }

    // The case reported: the influencer hired stops posting (the month) before or during the deal.
    private static void FlareHireQuit()
    {
        FlareFile f = FlareCastFile(1015);
        FlareInfluencer fit = f.Influencers.Find(n => n.Niche == FlareNiche.Fitness);
        var shops = new List<FlareShop> { new FlareShop { Place = "Gym~1", Name = "Iron Works", Niche = FlareNiche.Fitness, District = fit.District } };
        var hire = new FlareHire { Id = 900, Kind = 6, Place = "Gym~1", Name = "Iron Works", From = 10, To = 16 };
        hire.Who.Add(fit.Id);
        f.Hires.Add(hire);
        fit.Quit = true;
        var visits = new List<FlarePost>();
        for (int day = 10; day <= 18; day++)
        {
            FlareCast.Day(f, day, shops, null, null, null, visits);
        }
        int paid = f.Posts.FindAll(p => p.Who == fit.Id && p.Source == FlareSource.Hired && p.Place == "Gym~1").Count;
        Check(paid == 7 && hire.Posted == 7 && hire.LastPost == 16,
            "Flare: an influencer who stopped posting still makes the 7 posts of a week's deal paid for (" + paid + " made, " + hire.Posted + " counted, the last on day " + hire.LastPost + ")");
        Check(!f.Posts.Exists(p => p.Who == fit.Id && p.Day > 16), "Flare: and nothing more once the deal is over");

        var late = new FlareHire { Id = 901, Kind = 5, Place = "Gym~1", Name = "Iron Works", From = 30, To = 31, Late = true };
        late.Who.Add(fit.Id);
        f.Hires.Add(late);
        FlareCast.Day(f, 30, shops, null, null, null, visits);
        Check(late.Posted == 0 && FlareCast.HireStage(late, 30) == FlareCast.HireWaiting, "Flare: a late one has no post on the deal's first day, and the page says none yet");
        FlareCast.Day(f, 31, shops, null, null, null, visits);
        Check(late.Posted == 1 && late.LastPost == 31 && FlareCast.HireStage(late, 31) == FlareCast.HirePosting, "Flare: then the post comes, counted on the deal");
        Check(FlareCast.HireStage(late, 29) == FlareCast.HireBefore && FlareCast.HireStage(late, 32) == FlareCast.HireOver, "Flare: before its first day and after its last the page says so");
        var ended = new FlareHire { Id = 902, Kind = 6, Place = "Gym~1", Name = "Iron Works", From = 40, To = 46, Ended = true };
        ended.Who.Add(fit.Id);
        f.Hires.Add(ended);
        FlareCast.Day(f, 41, shops, null, null, null, visits);
        Check(ended.Posted == 0 && !f.Posts.Exists(p => p.Who == fit.Id && p.Day == 41), "Flare: a deal the player ended makes no post, from one who has quit or anyone");
    }

    private static void FlareHireCounted()
    {
        FlareFile f = FlareCastFile(1016);
        FlareInfluencer fit = f.Influencers.Find(n => n.Niche == FlareNiche.Fitness);
        var shops = new List<FlareShop> { new FlareShop { Place = "Gym~1", Name = "Iron Works", Niche = FlareNiche.Fitness, District = fit.District } };
        var hire = new FlareHire { Id = 910, Kind = 6, Place = "Gym~1", Name = "Iron Works", From = 5, To = 11 };
        hire.Who.Add(fit.Id);
        f.Hires.Add(hire);
        f.Posts.Add(new FlarePost { Id = 911, Day = 3, Hour = 9, Author = FlareAuthor.Player, Source = FlareSource.Sponsored, Place = "Gym~1" });
        f.Posts.Add(new FlarePost { Id = 912, Day = 4, Hour = 9, Author = FlareAuthor.Player, Source = FlareSource.Sponsored, Place = "Gym~2" });
        f.Posts.Add(new FlarePost { Id = 913, Day = 4, Hour = 10, Author = FlareAuthor.Player, Source = FlareSource.Review, Place = "Gym~1", Arg = 4 });   // (a review is counted apart on the card)
        var visits = new List<FlarePost>();
        for (int day = 5; day <= 8; day++)
        {
            FlareCast.Day(f, day, shops, null, null, null, visits);
        }
        int own, last;
        int all = FlareCast.PostsAbout(f, "Gym~1", out own, out last);
        Check(all == 5 && own == 1 && last == 8,
            "Flare: the Business tab counts a business's paid posts with the player's own (" + all + " in all, " + own + " own, the last on day " + last + "; the hire's 4 were not counted before)");
        for (int day = 9; day < 9 + 40; day++)
        {
            FlareCast.Day(f, day, shops, new[] { "Rival Corp" }, new[] { "A Candidate" }, new[] { "A Brand" }, visits);
        }
        all = FlareCast.PostsAbout(f, "Gym~1", out own, out last);
        Check(hire.Posted == 7 && all == 8 && last == 11, "Flare: the count holds after the Feed has let the deal's posts go (" + all + ", the last on day " + last + ")");
        Check(FlareCast.PostsAbout(f, "Nowhere~0", out own, out last) == 0 && own == 0 && last == -1, "Flare: a business with no post has none, and no last day");
        // The Business tab's card and a deal's card read these, not a count of their own (Social/Flare/FlareApp.Screens.cs).
        string screens = System.IO.File.ReadAllText(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Social\Flare\FlareApp.Screens.cs"));
        Check(screens.Contains("FlareCast.PostsAbout(file, row.Key, out own, out last)") && screens.Contains("FlareCast.HirePostsKey(h, day)") && screens.Contains("\"reallife_flare_shop_paid_posts\""),
            "Flare: the Business tab's shop card counts with PostsAbout and a deal's card says where its posts stand");
    }

    private static void FlareHireSaved()
    {
        FlareFile f = FlareCastFile(1017);
        var hire = new FlareHire { Id = 920, Kind = 6, Place = "Gym%20Street~1", Name = "Iron Works", From = 5, To = 11, Posted = 4, LastPost = 9 };
        hire.Who.Add(f.Influencers[0].Id);
        f.Hires.Add(hire);
        string line = FlareStore.WriteCast(f);
        var back = new FlareFile();
        FlareStore.ReadCast(back, line);
        Check(back.Hires.Count == 1 && back.Hires[0].Posted == 4 && back.Hires[0].LastPost == 9 && FlareStore.WriteCast(back) == line,
            "Flare: a deal's posts so far and its last post's day are kept in the save");
        int at = line.IndexOf("\nH|", StringComparison.Ordinal);
        int end = line.IndexOf('\n', at + 1);
        string hireLine = end < 0 ? line.Substring(at) : line.Substring(at, end - at);
        string[] fields = hireLine.Split('|');
        string older = line.Replace(hireLine, string.Join("|", fields, 0, 13));             // (as 0.26.0 and before wrote a deal)
        string counting = line.Replace(hireLine, string.Join("|", fields, 0, 15));          // (with the posts counted, before the mark was written)
        var old = new FlareFile();
        FlareStore.ReadCast(old, older);
        FlareHire o = old.Hires.Count == 1 ? old.Hires[0] : null;
        Check(o != null && o.Posted == 0 && o.LastPost == -1 && !o.Counted && o.To == 11,
            "Flare: a deal saved before the posts were counted loads as it was, its count marked as not known");
        var mid = new FlareFile();
        FlareStore.ReadCast(mid, counting);
        Check(mid.Hires.Count == 1 && mid.Hires[0].Counted && mid.Hires[0].Posted == 4, "Flare: a deal saved with its count but no mark is counted whole");

        // What a deal's card says from an older save, half run: not "No post yet", and then only the posts since the update, said as such.
        Check(o != null && FlareCast.HirePostsKey(o, 8) == null, "Flare: a deal from an older save, half run, says nothing of its posts until one comes (not \"No post yet\")");
        FlareInfluencer who = old.Influencers.Find(n => n.Id == o.Who[0]);
        var shops = new List<FlareShop> { new FlareShop { Place = o.Place, Name = o.Name, Niche = who.Niche, District = who.District } };
        var visits = new List<FlarePost>();
        FlareCast.Day(old, 8, shops, null, null, null, visits);
        Check(!o.Counted && o.Posted == 1 && FlareCast.HirePostsKey(o, 8) == "reallife_flare_hire_posts_since_one", "Flare: its first post after the update is said as one since the update");
        FlareCast.Day(old, 9, shops, null, null, null, visits);
        Check(!o.Counted && o.Posted == 2 && FlareCast.HirePostsKey(o, 9) == "reallife_flare_hire_posts_since", "Flare: then as so many since the update");

        // A deal from an older save that had not begun is counted whole from its first post.
        var fresh = new FlareFile();
        FlareStore.ReadCast(fresh, older);
        FlareHire n0 = fresh.Hires[0];
        Check(FlareCast.HirePostsKey(n0, 4) == "reallife_flare_hire_posts_from", "Flare: before its first day the card says when the posts begin");
        FlareCast.Day(fresh, 5, shops, null, null, null, visits);
        Check(n0.Counted && n0.Posted == 1 && FlareCast.HirePostsKey(n0, 5) == "reallife_flare_hire_posts_one", "Flare: one that begins after the update is counted whole: one paid post, said in the singular");
        FlareCast.Day(fresh, 6, shops, null, null, null, visits);
        Check(FlareCast.HirePostsKey(n0, 6) == "reallife_flare_hire_posts", "Flare: then so many paid posts");

        var counted = new FlareHire { From = 5, To = 11 };
        var waiting = new FlareHire { From = 5, To = 11, Late = true };
        Check(FlareCast.HirePostsKey(waiting, 5) == "reallife_flare_hire_posts_none" && FlareCast.HirePostsKey(counted, 12) == "reallife_flare_hire_posts_none_over",
            "Flare: a counted deal with no post yet says none yet, and one over with none says so");
    }

    private static void BrandRecWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("brand recognition", langs, codes))
        {
            return;
        }
        string[] keys =
        {
            "reallife_brand_fig_steps", "reallife_brand_fig_missed", "reallife_brand_fig_city", "reallife_flare_shop_paid_posts", "reallife_flare_hire_posts_from",
            "reallife_flare_hire_posts", "reallife_flare_hire_posts_none", "reallife_flare_hire_posts_none_over", "reallife_flare_hire_posts_where",
            "reallife_brand_fig_steps_first", "reallife_brand_fig_steps_one", "reallife_brand_fig_steps_one_still", "reallife_brand_fig_missed_one",
            "reallife_flare_hire_posts_one", "reallife_flare_hire_posts_since", "reallife_flare_hire_posts_since_one"
        };
        var missing = new List<string>();
        foreach (string key in keys)
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "brand recognition: every new text is in every language with the same {names} (" + string.Join(", ", missing) + ")");
        bool guide = true;
        for (int l = 0; l < langs.Length; l++)
        {
            guide &= langs[l].TryGetValue("reallife_guide_economy_brands", out string g) && g.Contains("{br_three_gain}") && g.Contains("{br_three_after}") && g.Contains("{br_city_one}");
        }
        Check(guide, "brand recognition: the Guide's Brands page says how fast recognition grows and why the city-wide figure stays low, in every language");
        // In What's new: the next row until the release, the version's own row after. Every row is looked in, the whole of it.
        bool line = false;
        foreach (KeyValuePair<string, string> row in langs[0])
        {
            line |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("Fixed: an influencer you hired on Flare who stopped posting");
        }
        Check(line, "brand recognition: What's new has the fix's line, in some reallife_changes_ row");
    }
}
