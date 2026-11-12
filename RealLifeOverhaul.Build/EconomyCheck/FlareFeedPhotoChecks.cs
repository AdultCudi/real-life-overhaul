using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;

// The photos in Flare's feed (FlareCityPhotoRules, FlareDirector.Photos): a feed that looks like a social media app. A save is loaded with a bank
// of 18 photos and a feed of 60 mixed posts: at "some" at least four in ten of the posts that could carry a photo carry one (right after the load,
// the posts first seen then included), at "none" not one, never the same photo twice in a row, and the same picks for the same seed. Also the
// nearest fit (another district, the dusk for the day or night, never the day for the night), a small bank's photos used more, a bank still
// filling leaving a post to be tried again, and the catch-up in how fast photos are taken while the bank is small, with its cost.
internal static partial class Program
{
    private const string FeedCafe = "5th Avenue~3", FeedBar = "5th Avenue~8", FeedShop = "5th Avenue~5";

    // A bank as a player has after a short walk: 18 photos in two districts, three lights, a rainy street, fronts, a park, a sight and the day's event.
    private static FlareBank FeedBank()
    {
        var bank = new FlareBank();
        BankAdd(bank, FlarePhotoKind.Street, Mid, 10, false, 10);
        BankAdd(bank, FlarePhotoKind.Street, Mid, 10, false, 10);
        BankAdd(bank, FlarePhotoKind.Street, Mid, 18, false, 10);
        BankAdd(bank, FlarePhotoKind.Street, Mid, 22, false, 10);
        BankAdd(bank, FlarePhotoKind.Street, Gar, 11, false, 10);
        BankAdd(bank, FlarePhotoKind.Street, Gar, 22, false, 10);
        BankAdd(bank, FlarePhotoKind.Street, Mid, 9, true, 9);
        BankAdd(bank, FlarePhotoKind.Front, Mid, 12, false, 10, FeedCafe, (int)FlareNiche.Food);
        BankAdd(bank, FlarePhotoKind.Front, Mid, 20, false, 10, FeedBar, (int)FlareNiche.Nightlife);
        BankAdd(bank, FlarePhotoKind.Front, Gar, 14, false, 10, FeedShop, (int)FlareNiche.Fashion);
        BankAdd(bank, FlarePhotoKind.Front, Mid, 13, false, 10, "5th Avenue~4", (int)FlareNiche.Food);
        BankAdd(bank, FlarePhotoKind.Park, Mid, 11, false, 10, "madison_park");
        BankAdd(bank, FlarePhotoKind.Park, Gar, 16, false, 9, "sunset_park");
        BankAdd(bank, FlarePhotoKind.Landmark, Mid, 15, false, 10, "bridge");
        BankAdd(bank, FlarePhotoKind.Event, Mid, 10, false, 10, "reallife_event_name_market");
        BankAdd(bank, FlarePhotoKind.Front, Mid, 22, false, 10, "5th Avenue~9", (int)FlareNiche.Nightlife);
        BankAdd(bank, FlarePhotoKind.Street, Mid, 6, false, 10);
        BankAdd(bank, FlarePhotoKind.Front, Gar, 12, false, 10, "Main~2", (int)FlareNiche.Food);
        return bank;
    }

    private static FlarePost FeedPost(FlareFile f, FlareAuthor author, FlareSource source, int arg, int day, int hour, int district, string place, string topic, int who)
    {
        string[] districts = { Mid, Gar, "ba:neighborhood_hamptons" };
        return new FlarePost
        {
            Id = f.NextId++, Day = day, Hour = hour, Minute = (day * 7 + hour * 13) % 60, Author = author, Source = source, Arg = arg, Who = who, Place = place, Topic = topic,
            District = district >= 0 ? districts[district] : null, Seed = FlareRules.Roll(f.Seed, f.NextId, 3) * 1000 + FlareRules.Roll(f.Seed, f.NextId, 4)
        };
    }

    // A feed of 60 top-level posts, the newest last: the city's own days (talk, businesses, reports, reviews, notices) and the rest of a real feed.
    private static FlareFile FeedFile(int seed, out List<FlarePost> topLevel)
    {
        FlareFile f = FlareLiveFile(seed);
        var city = new List<FlarePost>();
        for (int day = 8; day <= 10; day++)
        {
            FlareCityFacts facts = FlareFakeCity(day, 1, day % 2 == 0);
            FlareLiveRules.CityDay(f, facts, FlareCitizensOf(f, facts), null);
        }
        for (int i = 0; i < f.Posts.Count; i++)
        {
            if (f.Posts[i].ReplyTo < 0)
            {
                city.Add(f.Posts[i]);
            }
        }
        var rnd = new Random(seed);
        var more = new List<FlarePost>();
        for (int i = 0; i < 10; i++)   // influencers: their niche, a visit, a paid post, the news, the election
        {
            FlareSource[] sources = { FlareSource.Niche, FlareSource.Niche, FlareSource.Visit, FlareSource.Hired, FlareSource.CityNews, FlareSource.Election, FlareSource.Niche, FlareSource.Rival, FlareSource.Niche, FlareSource.Apology };
            more.Add(FeedPost(f, FlareAuthor.Influencer, sources[i], 0, 9 + i % 2, 8 + rnd.Next(14), -1, sources[i] == FlareSource.Visit || sources[i] == FlareSource.Hired ? "5th Avenue~" + (3 + i) : null, null, i));
        }
        for (int i = 0; i < 6; i++)   // friends' everyday posts, by their topics
        {
            more.Add(FeedPost(f, FlareAuthor.Friend, FlareSource.Life, i % 7 * 10 + i % 2, 9 + i % 2, 9 + rnd.Next(12), -1, null, null, i));
        }
        for (int i = 0; i < 5; i++)    // a rival's buildings
        {
            more.Add(FeedPost(f, FlareAuthor.Tycoon, FlareSource.BusinessPost, 0, 9 + i % 2, 8 + rnd.Next(14), i % 3, "Rival~" + i, null, i));
        }
        FlareSource[] mine = { FlareSource.Opened, FlareSource.RecordDay, FlareSource.Interior, FlareSource.Announce };
        for (int i = 0; i < 4; i++)    // the player's own businesses
        {
            more.Add(FeedPost(f, FlareAuthor.Player, mine[i], 0, 10, 9 + i * 3, -1, i % 2 == 0 ? FeedCafe : "Elm~1", null, -1));
        }
        more.Add(FeedPost(f, FlareAuthor.Brand, FlareSource.BrandNews, 0, 10, 11, -1, null, null, 0));
        more.Add(FeedPost(f, FlareAuthor.Ledger, FlareSource.Headline, 0, 10, 7, -1, null, "cityledger", 0));
        more.Add(FeedPost(f, FlareAuthor.Company, FlareSource.Results, 0, 10, 8, -1, null, null, 0));
        var all = new List<FlarePost>();
        city.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Id.CompareTo(b.Id));
        all.AddRange(city.GetRange(Math.Max(0, city.Count - (60 - more.Count)), Math.Min(city.Count, 60 - more.Count)));
        all.AddRange(more);
        all.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Id.CompareTo(b.Id));
        f.Posts.Clear();
        f.Posts.AddRange(all);
        topLevel = all;
        return f;
    }

    // What the director's look does with the feed, as often as it looks (a game minute): the picks made, newest post first.
    private static void FeedSettle(FlareFile f, FlareBank bank, int option, long now, bool rainNow)
    {
        int citySeed = FlareLiveRules.CitySeed(f);
        Func<FlarePost, int> nicheOf = p => p.Author == FlareAuthor.Influencer ? p.Who % 7 : -1;
        string[] homes = { Mid, Gar, "ba:neighborhood_hamptons" };
        for (int look = 0; look < 40; look++)
        {
            int cursor = int.MaxValue, settled = 0;
            for (int k = 0; k < 24; k++)
            {
                FlarePost p = FlareCityPhotoRules.NextPick(f, bank, citySeed, option, now, nicheOf, ref cursor);
                if (p == null)
                {
                    break;
                }
                FlareBankPhoto photo = FlareCityPhotoRules.Wish(p, nicheOf(p), p.Author == FlareAuthor.Influencer ? homes[p.Who % 3] : null, rainNow && now - p.At <= FlareCityPhotoRules.WindowMinutes, out FlarePhotoWish w)
                    ? FlareCityPhotoRules.Pick(bank, w, FlareCommentRules.Seed(p, citySeed)) : null;
                if (FlareCityPhotoRules.Settled(bank, p, photo, now))
                {
                    FlareCityPhotoRules.Picked(bank, p.Id, photo);
                    settled++;
                }
            }
            if (settled == 0)
            {
                break;
            }
        }
    }

    private static string FeedPicks(FlareFile f, FlareBank bank)
    {
        var b = new System.Text.StringBuilder();
        for (int i = 0; i < f.Posts.Count; i++)
        {
            b.Append(bank.PickOf(f.Posts[i].Id)).Append(',');
        }
        return b.ToString();
    }

    private static void FlareFeedPhotoChecks()
    {
        FeedShare();
        FeedFit();
        FeedSmallBank();
        FeedPace();
    }

    // ---------------------------------------------------------------- 60 posts and 18 photos, loaded after a save

    private static void FeedShare()
    {
        FlareFile f = FeedFile(61, out List<FlarePost> feed);
        long now = feed[feed.Count - 1].At + 30;   // a load: everything is up already, and the oldest posts are a day and a half old
        int eligible = 0, mixed = 0;
        foreach (FlarePost p in feed)
        {
            int niche = p.Author == FlareAuthor.Influencer ? p.Who % 7 : -1;
            eligible += FlareCityPhotoRules.KindShare(p, niche) > 0 ? 1 : 0;
        }
        var kinds = new HashSet<string>();
        foreach (FlarePost p in feed)
        {
            kinds.Add(p.Author + "/" + p.Source);
        }
        mixed = kinds.Count;
        int[] carry = new int[FlareCityPhotoRules.Options];
        string[] picks = new string[FlareCityPhotoRules.Options];
        for (int option = 0; option < FlareCityPhotoRules.Options; option++)
        {
            FlareFile g = FeedFile(61, out _);
            FlareBank bank = FeedBank();
            FeedSettle(g, bank, option, now, false);
            for (int i = 0; i < g.Posts.Count; i++)
            {
                carry[option] += bank.PickOf(g.Posts[i].Id) >= 0 ? 1 : 0;
            }
            picks[option] = FeedPicks(g, bank);
        }
        int shareOfAll = carry[2] * 100 / feed.Count, shareOfEligible = carry[2] * 100 / Math.Max(1, eligible);
        if (Environment.GetEnvironmentVariable("RLO_FLARE_REPORT") == "1")
        {
            Console.WriteLine("  flare feed photos: 60 posts, " + eligible + " able; carrying a photo: none " + carry[0] + ", a few " + carry[1] + ", some " + carry[2] + " (" + shareOfEligible + "% of able, " + shareOfAll + "% of all), many " + carry[3]);
        }
        Check(feed.Count == 60 && mixed >= 14 && eligible >= 35, "Flare feed photos: the feed made for the checks is 60 posts of " + mixed + " kinds, " + eligible + " of them able to carry a photo");
        Check(carry[2] * 100 >= 40 * eligible,
            "Flare feed photos: a bank of 18 photos and 60 posts loaded after a save: at \"some\" " + carry[2] + " of the " + eligible + " posts that could carry a photo do (" + shareOfEligible + "%, at least 40%), "
            + shareOfAll + "% of all 60");
        int wantsNone = 0;
        foreach (FlarePost p in feed)
        {
            wantsNone += FlareCityPhotoRules.Wants(p, FlareLiveRules.CitySeed(f), 0, p.Author == FlareAuthor.Influencer ? p.Who % 7 : -1) || FlareCityPhotoRules.Share(p, p.Who % 7, 0) > 0 ? 1 : 0;
        }
        Check(carry[0] == 0 && wantsNone == 0 && picks[0].Replace("-2,", string.Empty).Length == 0, "Flare feed photos: at \"none\" not one post carries a photo, wants one or is picked for one");
        Check(carry[1] > 0 && carry[1] < carry[2] && carry[2] < carry[3] && carry[3] * 100 <= 95 * eligible,
            "Flare feed photos: the option means something: none " + carry[0] + ", a few " + carry[1] + ", some " + carry[2] + ", many " + carry[3] + " of " + eligible + " able");
        // a poorer bank, as a player who walks one district by day has: 18 photos of Midtown by day and at dusk, no night, no rain
        FlareFile poorFile = FeedFile(61, out _);
        var poor = new FlareBank();
        for (int i = 0; i < 6; i++)
        {
            BankAdd(poor, FlarePhotoKind.Street, Mid, i < 4 ? 9 + i : 18, false, 10);
        }
        for (int i = 0; i < 6; i++)
        {
            BankAdd(poor, FlarePhotoKind.Front, Mid, 11 + i % 3, false, 10, "5th Avenue~" + i, i % 3 == 0 ? (int)FlareNiche.Food : i % 3 == 1 ? (int)FlareNiche.Fashion : (int)FlareNiche.Nightlife);
        }
        BankAdd(poor, FlarePhotoKind.Park, Mid, 10, false, 10, "madison_park");
        BankAdd(poor, FlarePhotoKind.Park, Mid, 15, false, 10, "madison_park");
        BankAdd(poor, FlarePhotoKind.Landmark, Mid, 13, false, 10, "bridge");
        BankAdd(poor, FlarePhotoKind.Event, Mid, 10, false, 10, "reallife_event_name_market");
        BankAdd(poor, FlarePhotoKind.Event, Mid, 12, false, 10, "reallife_event_name_market");
        BankAdd(poor, FlarePhotoKind.Street, Mid, 17, false, 10);
        FeedSettle(poorFile, poor, 2, now, false);
        int poorCarry = 0;
        for (int i = 0; i < poorFile.Posts.Count; i++)
        {
            poorCarry += poor.PickOf(poorFile.Posts[i].Id) >= 0 ? 1 : 0;
        }
        if (Environment.GetEnvironmentVariable("RLO_FLARE_REPORT") == "1")
        {
            Console.WriteLine("  flare feed photos: a poorer bank (" + poor.Photos.Count + " photos of one district, day and dusk only): " + poorCarry + " of " + eligible + " able (" + poorCarry * 100 / eligible + "%)");
        }
        Check(poor.Photos.Count == 18 && poorCarry * 100 >= 35 * eligible,
            "Flare feed photos: even a bank of 18 photos of one district by day and at dusk gives " + poorCarry + " of the " + eligible + " able posts a photo (at least 35%): the night posts wait for a night photo");
        // never twice in a row: among the photos of the feed, newest first, a photo does not come back within the bank's recent window
        FlareFile h = FeedFile(61, out _);
        FlareBank hb = FeedBank();
        FeedSettle(h, hb, 2, now, false);
        var order = new List<int>();
        for (int i = h.Posts.Count - 1; i >= 0; i--)
        {
            int id = hb.PickOf(h.Posts[i].Id);
            if (id >= 0)
            {
                order.Add(id);
            }
        }
        int window = FlareCityPhotoRules.RecentAllowed(hb.Photos.Count);
        bool never = order.Count > 10;
        for (int i = 0; i < order.Count; i++)
        {
            for (int j = Math.Max(0, i - window); j < i; j++)
            {
                never &= order[i] != order[j];
            }
        }
        bool few = true;
        foreach (FlareBankPhoto photo in hb.Photos)
        {
            few &= photo.Uses <= FlareCityPhotoRules.UsesAllowed(hb.Photos.Count);
        }
        Check(never && few && window >= 2, "Flare feed photos: no photo twice in a row, nor within the " + window + " photos before it, in the " + order.Count + " photos of the feed (each on at most "
              + FlareCityPhotoRules.UsesAllowed(hb.Photos.Count) + " posts)");
        // the same picks for the same seed, on every machine; another city, other picks
        FlareFile again = FeedFile(61, out _);
        FlareBank bankAgain = FeedBank();
        FeedSettle(again, bankAgain, 2, now, false);
        FlareFile other = FeedFile(62, out _);
        FlareBank bankOther = FeedBank();
        FeedSettle(other, bankOther, 2, now, false);
        Check(FeedPicks(again, bankAgain) == picks[2] && FeedPicks(h, hb) == picks[2] && FeedPicks(other, bankOther) != picks[2],
            "Flare feed photos: the same city and bank pick the very same photos for the same posts, on every machine (nothing is sent); another city picks others");
        // a live feed too, each post picked as it goes up: the same count of pictures, spread over the day
        FlareFile live = FeedFile(61, out List<FlarePost> liveFeed);
        FlareBank liveBank = FeedBank();
        long start = liveFeed[0].At - 60;
        for (long minute = start; minute <= now; minute += 3)
        {
            FeedSettle(live, liveBank, 2, minute, false);
        }
        int liveCarry = 0;
        for (int i = 0; i < live.Posts.Count; i++)
        {
            liveCarry += liveBank.PickOf(live.Posts[i].Id) >= 0 ? 1 : 0;
        }
        if (Environment.GetEnvironmentVariable("RLO_FLARE_REPORT") == "1")
        {
            Console.WriteLine("  flare feed photos: picked as the posts go up: " + liveCarry + " of 60 (" + liveCarry * 100 / 60 + "%)");
        }
        Check(liveCarry * 100 >= 40 * eligible, "Flare feed photos: posts picked as they go up (not at a load) carry a photo as often: " + liveCarry + " of " + eligible + " able");
        // a rainy morning at the load: the oldest posts are taken as dry, the newest as wet
        FlareFile rain = FeedFile(61, out _);
        FlareBank rainBank = FeedBank();
        FeedSettle(rain, rainBank, 2, now, true);
        int rainCarry = 0;
        for (int i = 0; i < rain.Posts.Count; i++)
        {
            rainCarry += rainBank.PickOf(rain.Posts[i].Id) >= 0 ? 1 : 0;
        }
        Check(rainCarry * 100 >= 40 * eligible, "Flare feed photos: loaded in the rain a photo still comes with " + rainCarry + " of the " + eligible + " able posts");
        Check(FlareCityPhotoRules.Due(feed[0], now) && !FlareCityPhotoRules.Due(feed[feed.Count - 1], feed[feed.Count - 1].At - FlareCityPhotoRules.LeadMinutes - 1)
              && FlareCityPhotoRules.Due(feed[feed.Count - 1], feed[feed.Count - 1].At - FlareCityPhotoRules.LeadMinutes),
            "Flare feed photos: a post a day and a half old is still due its pick at a load; one not yet up (beyond the lead) is not");
        // the sources of the director: the cursor, the settling, the weather of an old post
        string photosSource = ReviewSource("Social/Flare/FlareDirector.Photos.cs");
        string look = FlareMethod(photosSource, "private void PicksLook(");
        Check(look.Contains("FlareCityPhotoRules.NextPick(") && look.Contains("ref cursor") && look.Contains("FlareCityPhotoRules.Settled(_bank, p, photo, now)") && look.Contains("now - p.At <= FlareCityPhotoRules.WindowMinutes")
              && !photosSource.Contains("_pickSkip"),
            "Flare feed photos sources: the director's look picks the whole feed from the newest, settles a pick by the bank's rule, and takes an old post's weather as dry");
    }

    // ---------------------------------------------------------------- the nearest fit

    private static void FeedFit()
    {
        var bank = new FlareBank();
        FlareBankPhoto midDay = BankAdd(bank, FlarePhotoKind.Street, Mid, 10, false, 10);
        FlareBankPhoto garDay = BankAdd(bank, FlarePhotoKind.Street, Gar, 10, false, 10);
        FlareBankPhoto midDusk = BankAdd(bank, FlarePhotoKind.Street, Mid, 18, false, 10);
        FlareBankPhoto midNight = BankAdd(bank, FlarePhotoKind.Street, Mid, 23, false, 10);
        FlareBankPhoto midRain = BankAdd(bank, FlarePhotoKind.Street, Mid, 10, true, 10);
        FlarePost morning = BankTalk(1, RealLifeOverhaul.Flare.FlareTalk.Commute, 10), hampton = BankTalk(2, RealLifeOverhaul.Flare.FlareTalk.Commute, 10, "ba:neighborhood_hamptons");
        FlarePost late = BankTalk(3, RealLifeOverhaul.Flare.FlareTalk.Commute, 23), eve = BankTalk(4, RealLifeOverhaul.Flare.FlareTalk.Commute, 19);
        FlareBankPhoto exact = BankFor(morning, bank, false);
        Check(exact == midDay && FlareCityPhotoRules.Penalty(midDay, WishOf(morning, false), 0) == 0 && FlareCityPhotoRules.Penalty(garDay, WishOf(morning, false), 0) == FlareCityPhotoRules.PenaltyDistrict
              && FlareCityPhotoRules.Penalty(midDusk, WishOf(morning, false), 0) == FlareCityPhotoRules.PenaltyLight && FlareCityPhotoRules.Penalty(midNight, WishOf(morning, false), 0) == -1
              && FlareCityPhotoRules.Penalty(midRain, WishOf(morning, false), 0) == FlareCityPhotoRules.PenaltyWet,
            "Flare feed photos: a photo's distance from a post: its own district, light and weather 0; another district " + FlareCityPhotoRules.PenaltyDistrict + ", the dusk for the day "
            + FlareCityPhotoRules.PenaltyLight + ", the rain for the sun " + FlareCityPhotoRules.PenaltyWet + "; the night for the day none at all");
        var onlyOther = new FlareBank();
        BankAdd(onlyOther, FlarePhotoKind.Street, Gar, 10, false, 10);
        FlareBankPhoto near = BankFor(hampton, onlyOther, false);
        var onlyNight = new FlareBank();
        BankAdd(onlyNight, FlarePhotoKind.Street, Mid, 23, false, 10);
        var onlyDusk = new FlareBank();
        BankAdd(onlyDusk, FlarePhotoKind.Street, Mid, 18, false, 10);
        Check(near != null && BankFor(hampton, bank, false) != null && BankFor(morning, onlyNight, false) == null && BankFor(late, onlyDusk, false) != null && BankFor(morning, onlyDusk, false) != null,
            "Flare feed photos: a post of a district the bank has no photo of takes the nearest, a street of another; a dusk photo stands in for a day or a night one; a night photo never for the day");
        var rainOnly = new FlareBank();
        BankAdd(rainOnly, FlarePhotoKind.Street, Mid, 10, true, 10);
        FlarePost saysSun = BankTalk(5, RealLifeOverhaul.Flare.FlareTalk.Weather, 10, Mid, "sunshine"), saysRain = BankTalk(6, RealLifeOverhaul.Flare.FlareTalk.Weather, 10, Mid, "rain");
        Check(BankFor(saysSun, rainOnly, false) == null && BankFor(saysRain, rainOnly, true) != null && BankFor(saysRain, bank, false) == midRain && BankFor(morning, rainOnly, false) != null,
            "Flare feed photos: a post about the weather takes only a photo of that weather; any other post takes the other weather rather than none");
        FlarePost evening = BankTalk(7, RealLifeOverhaul.Flare.FlareTalk.Commute, 18);
        Check(BankFor(evening, bank, false) == midDusk && (BankFor(eve, bank, false) == midDusk || BankFor(eve, bank, false) == midNight), "Flare feed photos: the exact light comes first (a dusk post the dusk photo)");
        // the kinds that carry a photo now: business and influencer posts, the player's own business posts, a rival's, a friend's, the city's weather and sport, never a report
        var kinds = new List<string>();
        var checks = new (FlarePost post, int niche, bool carries)[]
        {
            (BankPost(10, FlareAuthor.Business, FlareSource.BusinessPost, 0, 12, Mid, "Main~1"), -1, true),
            (BankPost(11, FlareAuthor.Influencer, FlareSource.Niche, 0, 12, null), (int)FlareNiche.Tech, true),
            (BankPost(12, FlareAuthor.Influencer, FlareSource.Hired, 0, 12, null, "5th Avenue~1"), (int)FlareNiche.Fashion, true),
            (BankPost(13, FlareAuthor.Influencer, FlareSource.CityNews, 0, 12, null), (int)FlareNiche.LocalNews, true),
            (BankPost(14, FlareAuthor.Influencer, FlareSource.Election, 0, 12, null), (int)FlareNiche.LocalNews, false),
            (BankPost(15, FlareAuthor.Player, FlareSource.Opened, 0, 12, Mid, "Elm~1"), -1, true),
            (BankPost(16, FlareAuthor.Player, FlareSource.Interior, 0, 12, Mid, "Elm~1"), -1, true),
            (BankPost(17, FlareAuthor.Player, FlareSource.Voted, 0, 12, Mid), -1, false),
            (BankPost(18, FlareAuthor.Tycoon, FlareSource.BusinessPost, 0, 12, Mid, "Rival~1"), -1, true),
            (BankPost(19, FlareAuthor.Friend, FlareSource.Life, (int)FlareRules.LifeTopic.Sport * 10, 12, null), -1, true),
            (BankPost(20, FlareAuthor.Friend, FlareSource.Outing, 0, 12, null), -1, false),
            (BankTalk(21, RealLifeOverhaul.Flare.FlareTalk.Weather, 12), -1, true),
            (BankTalk(22, RealLifeOverhaul.Flare.FlareTalk.Sport, 12), -1, true),
            (BankTalk(23, RealLifeOverhaul.Flare.FlareTalk.Crime, 12), -1, false),
            (BankTalk(24, RealLifeOverhaul.Flare.FlareTalk.Accident, 12), -1, false),
            (BankPost(25, FlareAuthor.Citizen, FlareSource.Report, 0, 12, Mid), -1, false),
            (BankPost(26, FlareAuthor.Citizen, FlareSource.Review, 4, 12, Mid, "Main~1"), -1, false),
            (BankPost(27, FlareAuthor.Official, FlareSource.Official, 0, 12, Mid), -1, false),
            (BankPost(28, FlareAuthor.Ledger, FlareSource.Headline, 0, 12, Mid), -1, false),
        };
        bool kindsOk = true;
        foreach (var c in checks)
        {
            bool does = FlareCityPhotoRules.KindShare(c.post, c.niche) > 0;
            if (does != c.carries)
            {
                kindsOk = false;
                kinds.Add(c.post.Author + "/" + c.post.Source);
            }
        }
        FlarePost reply = BankPost(30, FlareAuthor.Business, FlareSource.BusinessPost, 0, 12, Mid, "Main~1");
        reply.ReplyTo = 4;
        FlarePost own = BankPost(31, FlareAuthor.Business, FlareSource.BusinessPost, 0, 12, Mid, "Main~1");
        own.Pic = "f:C:/x.png";
        Check(kindsOk && FlareCityPhotoRules.KindShare(reply, -1) == 0 && FlareCityPhotoRules.KindShare(own, -1) == 0,
            "Flare feed photos: business, influencer (a tech one too), the player's own business posts, a rival's, a friend's and the city's weather and sport carry photos; complaints, reports, reviews, notices, replies and a post with its own picture do not"
            + (kinds.Count > 0 ? " (wrong: " + string.Join(", ", kinds) + ")" : string.Empty));
        // the player's and the rivals' posts go by the same rules
        FlarePost player = BankPost(40, FlareAuthor.Player, FlareSource.Opened, 0, 12, Mid, "Elm~1"), rival = BankPost(41, FlareAuthor.Tycoon, FlareSource.BusinessPost, 0, 12, Mid, "Elm~1"),
            shop = BankPost(42, FlareAuthor.Business, FlareSource.BusinessPost, 0, 12, Mid, "Elm~1");
        FlareCityPhotoRules.Wish(player, -1, null, false, out FlarePhotoWish a);
        FlareCityPhotoRules.Wish(rival, -1, null, false, out FlarePhotoWish b);
        FlareCityPhotoRules.Wish(shop, -1, null, false, out FlarePhotoWish c2);
        Check(a.Kind0 == b.Kind0 && b.Kind0 == c2.Kind0 && a.Kind1 == b.Kind1 && b.Kind1 == c2.Kind1 && a.Place0 == b.Place0 && FlareCityPhotoRules.KindShare(player, -1) == FlareCityPhotoRules.KindShare(rival, -1) + 100,
            "Flare feed photos: the player's business posts and a rival's, a shop's, wish for the same photo: the front of the place, else the street");
        // the best fit is picked, among a fuzz of banks: nothing free is nearer, and none only when nothing free fits
        var rnd = new Random(5);
        int checkedPicks = 0, notBest = 0;
        string[] districts = { Mid, Gar, "ba:neighborhood_hamptons" };
        for (int round = 0; round < 40; round++)
        {
            var fuzz = new FlareBank();
            int size = 3 + rnd.Next(60);
            for (int i = 0; i < size; i++)
            {
                var kind = (FlarePhotoKind)(1 + rnd.Next(5));
                BankAdd(fuzz, kind, districts[rnd.Next(3)], rnd.Next(24), rnd.Next(3) == 0, 8 + rnd.Next(4), kind == FlarePhotoKind.Front ? "Main~" + rnd.Next(5) : kind == FlarePhotoKind.Event ? "ev" : kind == FlarePhotoKind.Park ? "sunset_park" : kind == FlarePhotoKind.Landmark ? "bridge" : null,
                    kind == FlarePhotoKind.Front ? rnd.Next(4) : -1);
            }
            for (int k = 0; k < 60; k++)
            {
                FlarePost p = BankTalk(900 + k, (RealLifeOverhaul.Flare.FlareTalk)rnd.Next(16), rnd.Next(24), districts[rnd.Next(3)], rnd.Next(2) == 0 ? "rain" : "sunshine", 8 + rnd.Next(4));
                if (!FlareCityPhotoRules.Wish(p, -1, null, rnd.Next(4) == 0, out FlarePhotoWish w))
                {
                    continue;
                }
                FlareBankPhoto got = FlareCityPhotoRules.Pick(fuzz, w, p.Seed);
                int bestRank = int.MaxValue;
                int uses = FlareCityPhotoRules.UsesAllowed(fuzz.Photos.Count), recentN = FlareCityPhotoRules.RecentAllowed(fuzz.Photos.Count);
                var recent = new HashSet<int>();
                for (int i = fuzz.Picks.Count - 1, seen = 0; i >= 0 && seen < recentN; i--)
                {
                    if (fuzz.Picks[i].Photo >= 0)
                    {
                        recent.Add(fuzz.Picks[i].Photo);
                        seen++;
                    }
                }
                foreach (FlareBankPhoto ph in fuzz.Photos)
                {
                    if (ph.Uses >= uses || recent.Contains(ph.Id))
                    {
                        continue;
                    }
                    for (int way = 0; way < w.Ways; way++)
                    {
                        int pen = FlareCityPhotoRules.Penalty(ph, w, way);
                        if (pen >= 0)
                        {
                            bestRank = Math.Min(bestRank, pen + 2 * way);
                        }
                    }
                }
                int gotRank = int.MaxValue;
                for (int way = 0; got != null && way < w.Ways; way++)
                {
                    int pen = FlareCityPhotoRules.Penalty(got, w, way);
                    if (pen >= 0)
                    {
                        gotRank = Math.Min(gotRank, pen + 2 * way);
                    }
                }
                checkedPicks++;
                notBest += gotRank == bestRank ? 0 : 1;
                FlareCityPhotoRules.Picked(fuzz, p.Id, got);
            }
        }
        Check(notBest == 0 && checkedPicks > 800, "Flare feed photos: in " + checkedPicks + " picks over 40 banks the photo picked is always the nearest free fit, and none only when no free photo fits at all");
    }

    private static FlarePhotoWish WishOf(FlarePost p, bool wet)
    {
        FlareCityPhotoRules.Wish(p, -1, null, wet, out FlarePhotoWish w);
        return w;
    }

    // ---------------------------------------------------------------- a small bank, a bank still filling

    private static void FeedSmallBank()
    {
        Check(FlareCityPhotoRules.UsesAllowed(FlareCityPhotoRules.BankMost) == FlareCityPhotoRules.UsesMost && FlareCityPhotoRules.UsesAllowed(18) > FlareCityPhotoRules.UsesMost
              && FlareCityPhotoRules.UsesAllowed(4) > FlareCityPhotoRules.UsesAllowed(18) && FlareCityPhotoRules.UsesAllowed(18) > FlareCityPhotoRules.UsesAllowed(40)
              && FlareCityPhotoRules.RecentAllowed(FlareCityPhotoRules.BankMost) == FlareCityPhotoRules.RecentMost && FlareCityPhotoRules.RecentAllowed(1) >= 2
              && FlareCityPhotoRules.RecentAllowed(4) <= FlareCityPhotoRules.RecentAllowed(18) && FlareCityPhotoRules.RecentAllowed(18) <= FlareCityPhotoRules.RecentAllowed(60),
            "Flare feed photos: a full bank keeps " + FlareCityPhotoRules.UsesMost + " posts to a photo; a small bank's photos carry more (18 photos: " + FlareCityPhotoRules.UsesAllowed(18) + ", 4 photos: "
            + FlareCityPhotoRules.UsesAllowed(4) + "), and never twice in a row (the recent window, at least 2)");
        // four photos and a long feed: never twice in a row, however small
        var bank = new FlareBank();
        for (int i = 0; i < 4; i++)
        {
            BankAdd(bank, FlarePhotoKind.Street, Mid, 10, false, 10);
        }
        int last = -1, repeats = 0, shown = 0;
        for (int k = 0; k < 60; k++)
        {
            FlarePost p = BankTalk(700 + k, RealLifeOverhaul.Flare.FlareTalk.Commute, 10);
            FlareBankPhoto photo = BankFor(p, bank, false);
            FlareCityPhotoRules.Picked(bank, p.Id, photo);
            if (photo != null)
            {
                shown++;
                repeats += photo.Id == last ? 1 : 0;
                last = photo.Id;
            }
        }
        Check(repeats == 0 && shown >= 4 * FlareCityPhotoRules.UsesAllowed(4) - 2 && shown <= 4 * FlareCityPhotoRules.UsesAllowed(4),
            "Flare feed photos: four photos carry " + shown + " posts, never two in a row, before they are worn out");
        // a bank still filling: a post it has no photo for is not settled as bare until the bank holds enough
        var small = new FlareBank();
        for (int i = 0; i < FlareCityPhotoRules.ReadyPhotos - 1; i++)
        {
            BankAdd(small, FlarePhotoKind.Park, Mid, 10, false, 10, "madison_park");
        }
        FlarePost food = BankTalk(800, RealLifeOverhaul.Flare.FlareTalk.Food, 11);
        long up = food.At + 5;
        bool tryAgain = !FlareCityPhotoRules.Settled(small, food, null, up) && FlareCityPhotoRules.Settled(small, food, small.Photos[0], up);
        BankAdd(small, FlarePhotoKind.Park, Mid, 10, false, 10, "madison_park");
        bool settled = FlareCityPhotoRules.Settled(small, food, null, up) && !FlareCityPhotoRules.Settled(small, food, null, food.At - 1);
        Check(tryAgain && settled, "Flare feed photos: a post the bank has no photo for is left to be tried again while the bank is filling (under " + FlareCityPhotoRules.ReadyPhotos + " photos), then settled as bare once it is up");
        // those posts get their photo as soon as the bank has one that fits
        FlareFile f = FeedFile(61, out List<FlarePost> feed);
        long now = feed[feed.Count - 1].At + 30;
        var filling = new FlareBank();
        for (int i = 0; i < 4; i++)
        {
            BankAdd(filling, FlarePhotoKind.Street, Mid, 10, false, 10);
        }
        FeedSettle(f, filling, 2, now, false);
        int pending = 0, bare = 0;
        for (int i = 0; i < f.Posts.Count; i++)
        {
            int pick = filling.PickOf(f.Posts[i].Id);
            pending += pick == -2 ? 1 : 0;
            bare += pick == -1 ? 1 : 0;
        }
        FlareBank rich = FeedBank();
        foreach (FlareBankPhoto ph in rich.Photos)
        {
            ph.Id += 100;
            filling.Photos.Add(ph);
        }
        FeedSettle(f, filling, 2, now, false);
        int pendingAfter = 0, carried = 0;
        for (int i = 0; i < f.Posts.Count; i++)
        {
            int pick = filling.PickOf(f.Posts[i].Id);
            pendingAfter += pick == -2 ? 1 : 0;
            carried += pick >= 0 ? 1 : 0;
        }
        Check(bare == 0 && pending > 0 && pendingAfter < pending && carried > 0,
            "Flare feed photos: a bank of four photos leaves " + pending + " wanted posts to be tried again and settles none as bare; once the bank has 22 they are picked (" + pendingAfter + " left, " + carried + " in all with a photo)");
    }

    // ---------------------------------------------------------------- how fast photos are taken, and what it costs

    private static void FeedPace()
    {
        const double MinuteMs = 60_000.0;
        Check(FlareCityPhotoRules.NextPace(20f, 5.0, 4) == FlareCityPhotoRules.PaceFilling && FlareCityPhotoRules.NextPace(20f, 5.0, 100) == FlareCityPhotoRules.PaceLeast
              && FlareCityPhotoRules.NextPace(20f, 5.0) == FlareCityPhotoRules.PaceLeast && FlareCityPhotoRules.PaceFilling < FlareCityPhotoRules.PaceLeast && FlareCityPhotoRules.PaceFilling >= 10f,
            "Flare feed photos: while the bank holds under " + FlareCityPhotoRules.CatchUpPhotos + " photos they are taken every " + FlareCityPhotoRules.PaceFilling + " s (at most), otherwise every " + FlareCityPhotoRules.PaceLeast + " s");
        float slow = FlareCityPhotoRules.NextPace(FlareCityPhotoRules.PaceFilling, FlareCityPhotoRules.SlowMs + 1, 4);
        Check(slow >= FlareCityPhotoRules.PaceFilling * 2 && slow <= FlareCityPhotoRules.PaceMost && FlareCityPhotoRules.NextPace(FlareCityPhotoRules.PaceMost, 99.0, 4) == FlareCityPhotoRules.PaceMost,
            "Flare feed photos: a slow photo still doubles the wait (to " + FlareCityPhotoRules.PaceMost + " s at most), a filling bank or not");
        Check(FlareCityPhotoRules.DayLimit(0) == FlareCityPhotoRules.DayMostFilling && FlareCityPhotoRules.DayLimit(FlareCityPhotoRules.CatchUpPhotos) == FlareCityPhotoRules.DayMost
              && FlareCityPhotoRules.DayMostFilling > FlareCityPhotoRules.DayMost && FlareCityPhotoRules.DayMostFilling <= 40,
            "Flare feed photos: a game day takes " + FlareCityPhotoRules.DayMostFilling + " photos at most while the bank fills, " + FlareCityPhotoRules.DayMost + " after");
        double perMinute = MinuteMs / (FlareCityPhotoRules.PaceFilling * 1000.0) * FlareCityPhotoRules.SlowMs, perMinuteBefore = MinuteMs / (FlareCityPhotoRules.PaceLeast * 1000.0) * FlareCityPhotoRules.SlowMs;
        Check(perMinute <= 110.0 && perMinute <= perMinuteBefore * 1.7 && FlareCityPhotoRules.FrameMs * 2 == FlareCityPhotoRules.SlowMs,
            "Flare feed photos: the worst case of the main thread while the bank fills is " + perMinute.ToString("0") + " ms a minute (" + perMinuteBefore.ToString("0") + " before), a frame never over "
            + FlareCityPhotoRules.FrameMs + " ms, each photo's renders unchanged");
        Check(FixLineEverywhere("feed now shows photos on far more posts", "feed do Flare agora mostra fotos em muito mais posts", "现在有多得多的帖子带照片",
                                "Flare-Feed zeigt jetzt bei viel mehr Beiträgen Fotos", "le fil de Flare montre maintenant des photos sur beaucoup plus de publications",
                                "el feed de Flare ahora muestra fotos en muchas más publicaciones", "лента Flare теперь показывает фото у гораздо большего числа публикаций",
                                "Flare-feed toont nu op veel meer berichten foto's", "Flare akışı artık çok daha fazla gönderide fotoğraf gösteriyor"),
            "Flare feed photos: What's new has the line about photos on more posts, with its fix mark, in some row of every language");
        string camera = ReviewSource("Social/Flare/FlareCityCamera.cs");
        Check(camera.Contains("Renders") && !camera.Contains("PaceFilling") && !camera.Contains("CatchUp"), "Flare feed photos sources: the pacing is the director's alone; the camera's render path is as before");
    }
}
