using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Flare;

// Checks for the city's influencers on Flare (Scripts/Social/Flare/FlareCast): the first day's cast from a seed, the
// niches of the game's business types, the worked example of a hire's price and reach, the deals priced against radio,
// a campaign plan, how a hire goes, the month (growth, stars, quitting, scandals, newcomers and the cap), the day's
// posts, following and the one notification a day, the save's line, and a long game's time.
internal static partial class Program
{
    private static readonly string[] FlareDistricts = { "ba:neighborhood_midtown", "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen" };

    private static void FlareInfluencersChecks()
    {
        FlareCastMade();
        FlareNiches();
        FlareHirePrice();
        FlareDealsAgainstRadio();
        FlareHireOutcome();
        FlareCastMonth();
        FlareCastDay();
        FlareFollowing();
        FlareCastSave();
        FlareCastTiming();
    }

    private static FlareFile FlareCastFile(int seed)
    {
        FlareFile f = FlareRules.Open(seed, 1, 0, 50);
        f.CastSeed = seed * 31 + 7;
        FlareCast.Make(f, 1, FlareDistricts);
        return f;
    }

    private static void FlareCastMade()
    {
        FlareFile a = FlareCastFile(77), b = FlareCastFile(77), c = FlareCastFile(78);
        Check(a.Influencers.Count == FlareCast.Start && FlareStore.WriteCast(a) == FlareStore.WriteCast(b) && FlareStore.WriteCast(a) != FlareStore.WriteCast(c),
            "Flare: six influencers on the first day, the same for the same seed");
        bool sane = true;
        var niches = new HashSet<FlareNiche>();
        foreach (FlareInfluencer n in a.Influencers)
        {
            sane &= n.Followers >= 2000 && n.Followers <= 60000 && n.Engagement >= 150 && n.Engagement < 700 && n.Reliability >= 40 && n.Reliability <= 95 && n.Handle.StartsWith("@")
                    && Array.IndexOf(FlareDistricts, n.District) >= 0 && n.Active && !n.Star;
            niches.Add(n.Niche);
        }
        Check(sane && niches.Count == 6, "Flare: each has followers, engagement, reliability, a handle and a home district, one in each niche but business");
        FlareCast.Make(a, 5, FlareDistricts);
        Check(a.Influencers.Count == FlareCast.Start, "Flare: a cast is made once");
        int edgy = 0, flakyReliable = 0;
        FlareFile many = FlareRules.Open(1, 1, 0, 50);
        for (int i = 0; i < 2000; i++)
        {
            FlareInfluencer n = FlareCast.New(many, FlareNiche.Food, null, 1, 1000, -1, false);
            edgy += n.Style == FlareStyle.Edgy ? 1 : 0;
            flakyReliable += n.Style == FlareStyle.Flaky && n.Reliability > 60 ? 1 : 0;
        }
        Check(edgy > 200 && edgy < 400 && flakyReliable == 0, "Flare: about one in seven is edgy, and a flaky one is never very reliable (" + edgy + " of 2000)");
    }

    private static void FlareNiches()
    {
        Check(FlareCast.NicheOf("ba:businesstype_coffeeshop") == FlareNiche.Food && FlareCast.NicheOf("ba:businesstype_fastfoodrestaurant") == FlareNiche.Food
              && FlareCast.NicheOf("ba:businesstype_clothingstore") == FlareNiche.Fashion && FlareCast.NicheOf("ba:businesstype_gym") == FlareNiche.Fitness
              && FlareCast.NicheOf("ba:businesstype_nightclub") == FlareNiche.Nightlife && FlareCast.NicheOf("ba:businesstype_electronicsstore") == FlareNiche.Tech
              && FlareCast.NicheOf("ba:businesstype_lawfirm") == FlareNiche.Business && FlareCast.NicheOf(null) == FlareNiche.Business,
            "Flare: the game's business types by niche");
        Check(FlareCast.FirstDealKind == (int)CampaignKind.InfluencerPost && FlareCast.FirstDealKind + (int)FlareDeal.Series == (int)CampaignKind.InfluencerSeries
              && FlareCast.FirstDealKind + (int)FlareDeal.Ambassador == (int)CampaignKind.Ambassador && FlareCast.FirstDealKind + (int)FlareDeal.Plan == (int)CampaignKind.InfluencerPlan
              && FlareCast.FirstDealKind + (int)FlareDeal.Launch == (int)CampaignKind.Launch && FlareCast.FirstDealKind + (int)FlareDeal.Giveaway == (int)CampaignKind.Giveaway
              && (int)CampaignKind.Giveaway == CompanyCatalogue.CampaignKinds - 1, "Flare: each deal is the company's campaign of its kind, in order");
    }

    private static void FlareHirePrice()
    {
        var one = new FlareInfluencer { Followers = 10000, Engagement = 400, Reliability = 80, Reputation = 60, Niche = FlareNiche.Food, District = FlareDistricts[0] };
        var big = new FlareInfluencer { Followers = 60000, Engagement = 250, Reliability = 50, Reputation = 60, Niche = FlareNiche.Food, District = FlareDistricts[0] };
        Check(one.Engaged == 400 && FlareCast.Price(one) == 250 && FlareCast.Reach(one, FlareNiche.Food, FlareDistricts[0]) == 772, "Flare: the worked example's first row (price "
              + FlareCast.Price(one) + ", reach " + FlareCast.Reach(one, FlareNiche.Food, FlareDistricts[0]) + ")");
        Check(big.Engaged == 1500 && FlareCast.Price(big) == 800 && FlareCast.Reach(big, FlareNiche.Food, FlareDistricts[0]) == FlareCast.ReachMost, "Flare: the second row, capped");
        Check(FlareCast.Fit(one, FlareNiche.Food, FlareDistricts[0]) == 1560 && FlareCast.Fit(one, FlareNiche.Tech, FlareDistricts[1]) == 630
              && FlareCast.Fit(new FlareInfluencer { Niche = FlareNiche.Business }, FlareNiche.Tech, null) == 900, "Flare: niche and district decide how well it lands");
        Check(FlareCast.Reach(new FlareInfluencer { Followers = 10 }, FlareNiche.Food, null) == FlareCast.ReachLeast, "Flare: never under the least");
        Check(FlareCast.Fee(one, FlareDeal.Post) == 250 && FlareCast.Fee(one, FlareDeal.Series) == 1250 && FlareCast.Fee(one, FlareDeal.Ambassador) == 1500, "Flare: a series is five posts, an ambassador eight at a quarter off");
        FlareFile f = FlareCastFile(5);
        var plan = new List<int>();
        int picked = FlareCast.PlanPick(f, FlareNiche.Food, FlareDistricts[0], 1, plan);
        Check(picked == 3 && plan.Count == 3 && plan[0] != plan[1] && FlareCast.Reach(f.Influencer(plan[0]), FlareNiche.Food, FlareDistricts[0]) >= FlareCast.Reach(f.Influencer(plan[1]), FlareNiche.Food, FlareDistricts[0]),
            "Flare: a plan takes the three who suit the business best, best first");
        foreach (FlareInfluencer n in f.Influencers)
        {
            n.Quit = n.Id != f.Influencers[0].Id;
        }
        Check(FlareCast.PlanPick(f, FlareNiche.Food, null, 1, plan) == 0, "Flare: a plan needs two at least");
    }

    private static void FlareDealsAgainstRadio()
    {
        int radio = CampaignRules.Effect((int)CampaignKind.Radio, 1000, CycleState.Steady, 1000);
        double radioPer = CompanyCatalogue.CampaignCost[(int)CampaignKind.Radio] / (double)(radio * CompanyCatalogue.CampaignDays[(int)CampaignKind.Radio]);
        var one = new FlareInfluencer { Followers = 20000, Engagement = 400, Reliability = 70, Reputation = 60, Niche = FlareNiche.Food, District = FlareDistricts[0] };
        int reach = FlareCast.Reach(one, FlareNiche.Food, FlareDistricts[0]);
        bool close = true;
        string seen = string.Empty;
        foreach (FlareDeal deal in new[] { FlareDeal.Post, FlareDeal.Series, FlareDeal.Ambassador, FlareDeal.Launch, FlareDeal.Giveaway })
        {
            int kind = FlareCast.FirstDealKind + (int)deal;
            int cost = FlareCast.Fee(one, deal);
            int percent = CampaignRules.Effect(kind, 1000, CycleState.Steady, 1000, reach);
            double per = cost / (double)(percent * CompanyCatalogue.CampaignDays[kind]);
            close &= per > radioPer * 0.4 && per < radioPer * 1.8;
            seen += " " + deal + " " + cost + " for " + percent + "% over " + CompanyCatalogue.CampaignDays[kind] + ";";
        }
        Check(close, "Flare: each deal costs near what radio does for each customer (radio " + radioPer.ToString("0.0") + " a point a day;" + seen + ")");
        Check(CampaignRules.Effect((int)CampaignKind.Giveaway, 1000, CycleState.Steady, 1000, FlareCast.ReachMost) == CompanyCatalogue.CampaignMost, "Flare: a deal never passes the campaigns' own cap");
    }

    private static void FlareHireOutcome()
    {
        int lateFlaky = 0, lateHonest = 0, off = 0, backfire = 0, backfireHonest = 0;
        for (int i = 0; i < 2000; i++)
        {
            var hire = new FlareHire { Id = i };
            FlareCast.Outcome(hire, new FlareInfluencer { Reliability = 50, Style = FlareStyle.Flaky }, 99);
            lateFlaky += hire.Late ? 1 : 0;
            off += hire.OffMessage ? 1 : 0;
            var h2 = new FlareHire { Id = i };
            FlareCast.Outcome(h2, new FlareInfluencer { Reliability = 90, Style = FlareStyle.Honest }, 99);
            lateHonest += h2.Late ? 1 : 0;
            backfireHonest += h2.Backfired || h2.OffMessage ? 1 : 0;
            var h3 = new FlareHire { Id = i };
            FlareCast.Outcome(h3, new FlareInfluencer { Reliability = 90, Style = FlareStyle.Edgy }, 99);
            backfire += h3.Backfired ? 1 : 0;
        }
        Check(lateFlaky > lateHonest * 5 && off > 300 && off < 700 && backfire > 250 && backfire < 550 && backfireHonest == 0,
            "Flare: a flaky one is late far more often and now and then off message, an edgy one backfires a fifth of the time, an honest one never (" + lateFlaky + ", " + lateHonest + ", " + off + ", " + backfire + ")");
    }

    private static void FlareCastMonth()
    {
        FlareFile f = FlareCastFile(9);
        var news = new List<FlareCastNews>();
        var heat = new int[(int)FlareNiche.Business + 1];
        FlareCast.Month(f, 31, heat, null, false, FlareDistricts, news);
        Check(f.MonthDay == 31 && news.Count >= FlareCast.Start, "Flare: the month is played for every influencer");
        var star = new FlareInfluencer { Id = 900, Followers = FlareCast.StarFrom - 1, Engagement = 700, Reputation = 100, Style = FlareStyle.Wholesome, Seed = 3 };
        var fading = new FlareInfluencer { Id = 901, Followers = FlareCast.QuitUnder - 1, Engagement = 150, Reputation = 0, Style = FlareStyle.Honest, Seed = 4 };
        f.Influencers.Add(star);
        f.Influencers.Add(fading);
        news.Clear();
        bool starred = false;
        for (int m = 2; m < 6 && !starred; m++)
        {
            news.Clear();
            FlareCast.Month(f, 31 * m, heat, null, false, FlareDistricts, news);
            starred = star.Star;
        }
        Check(starred && fading.Quit, "Flare: a well-liked one becomes a star, one with too few followers stops");
        var scandal = new FlareInfluencer { Followers = 10000, Reputation = 60 };
        FlareCast.Scandal(scandal, 40);
        Check(scandal.ScandalDay == 40 && scandal.Followers == 8000 && scandal.Reputation == 20, "Flare: a scandal costs a fifth of the followers and most of the name");
        int edgy = 0, wholesome = 0;
        for (int d = 0; d < 2000; d++)
        {
            var e = new FlareInfluencer { Followers = 50000, Engagement = 400, Reputation = 60, Style = FlareStyle.Edgy, Seed = d };
            var w = new FlareInfluencer { Followers = 50000, Engagement = 400, Reputation = 60, Style = FlareStyle.Wholesome, Seed = d };
            var g = FlareRules.Open(1, 1, 0, 50);
            g.Influencers.Add(e);
            g.Influencers.Add(w);
            for (int k = 0; k < 8; k++)
            {
                g.Influencers.Add(new FlareInfluencer { Followers = 50000, Reputation = 60, Seed = 1000 + k });   // full: no newcomers
            }
            FlareCast.Month(g, 60, heat, null, false, FlareDistricts, news);
            edgy += e.ScandalDay == 60 ? 1 : 0;
            wholesome += w.ScandalDay == 60 ? 1 : 0;
        }
        Check(edgy > wholesome * 5 && edgy > 150 && edgy < 350, "Flare: an edgy one has scandals far more often than a wholesome one (" + edgy + ", " + wholesome + " of 2000 months)");
        // Newcomers: a busy niche draws one, the cap holds.
        FlareFile g2 = FlareCastFile(11);
        heat[(int)FlareNiche.Fitness] = 1000;
        int arrived = 0;
        for (int m = 1; m < 40; m++)
        {
            news.Clear();
            FlareCast.Month(g2, 30 * m, heat, null, false, FlareDistricts, news);
            foreach (FlareCastNews n in news)
            {
                arrived += n.Kind == FlareCastNews.What.Arrived && g2.Influencer(n.Id).Niche == FlareNiche.Fitness ? 1 : 0;
            }
            Check(g2.ActiveInfluencers <= FlareCast.Most, "Flare: never more than ten active (month " + m + ")");
        }
        Check(arrived > 0, "Flare: a busy niche draws newcomers (" + arrived + " into fitness)");
        // A friend who likes sport, close enough, may take it up; once.
        FlareFile g3 = FlareCastFile(12);
        var friends = new List<FlarePerson> { new FlarePerson { Index = 2, First = "Sam", Stage = 3, Taste = FlareTaste.Sport, Closeness = 60 } };
        Array.Clear(heat, 0, heat.Length);
        for (int m = 1; m < 120 && !FlareCast.IsOne(g3, 2); m++)
        {
            FlareCast.Month(g3, 30 * m, heat, friends, false, FlareDistricts, news);
        }
        int asFriend = 0;
        foreach (FlareInfluencer n in g3.Influencers)
        {
            asFriend += n.Person == 2 ? 1 : 0;
        }
        Check(asFriend == 1 && g3.Influencers.Find(n => n.Person == 2).Niche == FlareNiche.Fitness, "Flare: a sporty friend can turn into a fitness influencer, once");
        FlareFile g4 = FlareCastFile(13);
        for (int m = 1; m < 120 && !g4.Influencers.Exists(n => n.Staff); m++)
        {
            FlareCast.Month(g4, 30 * m, heat, null, true, FlareDistricts, news);
        }
        Check(g4.Influencers.Exists(n => n.Staff && n.Niche == FlareNiche.Business), "Flare: one of the player's staff can take it up");
    }

    private static void FlareCastDay()
    {
        FlareFile f = FlareCastFile(21);
        FlareInfluencer food = f.Influencers.Find(n => n.Niche == FlareNiche.Food);
        var shops = new List<FlareShop> { new FlareShop { Place = "A~1", Name = "Copper Kettle", Niche = FlareNiche.Food, District = food.District } };
        var hire = new FlareHire { Id = 500, Kind = 6, Place = "A~1", Name = "Copper Kettle", From = 10, To = 16, Late = true };
        hire.Who.Add(food.Id);
        f.Hires.Add(hire);
        var visits = new List<FlarePost>();
        FlareCast.Day(f, 10, shops, null, null, null, visits);
        Check(!f.Posts.Exists(p => p.Day == 10 && p.Who == food.Id), "Flare: a late one's first post does not come");
        FlareCast.Day(f, 11, shops, null, null, null, visits);
        Check(f.Posts.Exists(p => p.Day == 11 && p.Who == food.Id && p.Source == FlareSource.Hired && p.Place == "A~1"), "Flare: then the hired posts come, about the business");
        hire.Ended = true;
        int visited = 0;
        for (int d = 20; d < 120; d++)
        {
            visits.Clear();
            FlareCast.Day(f, d, shops, new[] { "Rival Corp" }, new[] { "A Candidate" }, new[] { "Copper Kettle Brand" }, visits);
            visited += visits.Count;
        }
        Check(visited > 0 && f.Posts.Exists(p => p.Source == FlareSource.Rival && p.Name == "Rival Corp") && f.Posts.Exists(p => p.Source == FlareSource.Election && p.Author == FlareAuthor.Candidate)
              && f.Posts.Exists(p => p.Source == FlareSource.BrandNews && p.Author == FlareAuthor.Brand), "Flare: influencers visit, rivals pay them, candidates and brands post");
        Check(f.Posts.Count <= FlareRules.PostsMost, "Flare: the Feed stays within its bound");
        food.ScandalDay = 130;
        FlareCast.Day(f, 130, shops, null, null, null, visits);
        Check(f.Posts.Exists(p => p.Day == 130 && p.Who == food.Id && p.Source == FlareSource.Apology), "Flare: on the day of a scandal, an apology");
        f.NewsDay = 140;
        f.NewsToday = FlareCast.NewsAccident;
        FlareInfluencer news = f.Influencers.Find(n => n.Niche == FlareNiche.LocalNews);
        bool spoke = false;
        for (int d = 140; d < 141; d++)
        {
            FlareCast.Day(f, d, null, null, null, null, visits);
            spoke = f.Posts.Exists(p => p.Day == d && p.Who == news.Id && p.Source == FlareSource.CityNews && (p.Arg & FlareCast.NewsAccident) != 0)
                    || !f.Posts.Exists(p => p.Day == d && p.Who == news.Id);
        }
        Check(spoke, "Flare: local news posts about what happened in the city");
    }

    private static void FlareFollowing()
    {
        FlareFile f = FlareCastFile(31);
        Check(f.Follows.Count == 0, "Flare: a new game follows nobody");
        var inf = new FlarePost { Author = FlareAuthor.Influencer, Who = 4 };
        var brand = new FlarePost { Author = FlareAuthor.Brand, Name = "Copper Kettle" };
        Check(FlareCast.KeyOf(inf) == "i:4" && FlareCast.KeyOf(brand) == "b:Copper Kettle" && FlareCast.KeyOf(new FlarePost { Author = FlareAuthor.Candidate, Name = "X" }) == "c:X"
              && FlareCast.KeyOf(new FlarePost()) == null && FlareCast.KeyOf(new FlarePost { Author = FlareAuthor.Friend }) == null, "Flare: an account to follow for each influencer, brand and candidate; never the player or a friend");
        Check(FlareCast.Follow(f, "i:4", true) && !FlareCast.Follow(f, "i:4", true) && FlareCast.Followed(f, "i:4") && FlareCast.Follow(f, "i:4", false) && !FlareCast.Followed(f, "i:4"),
            "Flare: follow and unfollow");
        f.Posts.Add(new FlarePost { Day = 5, Author = FlareAuthor.Influencer, Who = 4 });
        f.Posts.Add(new FlarePost { Day = 5, Author = FlareAuthor.Influencer, Who = 5 });
        Check(FlareCast.FollowedPosts(f, 5) == 0 && !FlareCast.FollowNotice(f, 5, 0, true), "Flare: an account nobody follows never notifies");
        FlareCast.Follow(f, "i:4", true);
        FlareCast.Follow(f, "i:5", true);
        Check(FlareCast.FollowedPosts(f, 5) == 2 && FlareCast.FollowNotice(f, 5, 2, true) && !FlareCast.FollowNotice(f, 5, 2, true) && FlareCast.FollowNotice(f, 6, 1, true),
            "Flare: the accounts followed send one notification a day for all their posts");
        Check(!FlareCast.FollowNotice(f, 7, 3, false) && f.FollowNoticeDay == 6, "Flare: none with the option off");
    }

    private static void FlareCastSave()
    {
        FlareFile f = FlareCastFile(41);
        f.Influencers[0].Person = 3;
        f.Influencers[1].Star = true;
        f.Influencers[2].Quit = true;
        var hire = new FlareHire { Id = 70, Kind = 8, Place = "Main%20St~1", Name = "Copper | Kettle", From = 5, To = 11, Percent = 9, Backfired = true };
        hire.Who.Add(f.Influencers[0].Id);
        hire.Who.Add(f.Influencers[3].Id);
        f.Hires.Add(hire);
        FlareCast.Follow(f, "i:" + f.Influencers[1].Id, true);
        FlareCast.Follow(f, "b:Copper Kettle", true);
        f.MonthDay = 30;
        f.ViralNiche = 2;
        f.ViralNicheDay = 28;
        string line = FlareStore.WriteCast(f);
        FlareFile back = FlareRules.Open(41, 1, 0, 50);
        FlareStore.ReadCast(back, line);
        Check(FlareStore.WriteCast(back) == line && back.Influencers.Count == f.Influencers.Count && back.Hires.Count == 1 && back.Hires[0].Who.Count == 2 && back.Hires[0].Backfired
              && back.Follows.Count == 2 && back.Influencers[0].Person == 3 && back.Influencers[1].Star && back.Influencers[2].Quit && back.ViralNiche == 2 && back.MonthDay == 30,
            "Flare: the cast, the deals and the follows survive the save");
        Check(back.Hires[0].Name == "Copper / Kettle", "Flare: a name with the separator kept readable");
        FlareFile later = FlareRules.Open(41, 1, 0, 50);
        FlareStore.ReadCast(later, line + "\nI|99|@x|9|d|1|1|1|0|1|1|0|0|-1|0|0|0\nI|98|@y|0|d|1|1|1|9|1|1|0|0|-1|0|0|0\nZ|what");
        Check(later.Influencers.Count == f.Influencers.Count, "Flare: an influencer of a niche or manner of a later version is left out");
        FlareFile other = FlareRules.Open(41, 1, 0, 50);
        FlareStore.ReadCast(other, "FC9|1|2|3|4|5|6|7");
        FlareStore.ReadCast(other, null);
        Check(other.Influencers.Count == 0 && other.CastSeed == 0, "Flare: another version's line leaves a new cast to be made");
        var data = new Dictionary<string, string>();
        FlareStore.Save(data, f);
        FlareFile loaded = FlareStore.Load(data);
        Check(data.ContainsKey(FlareStore.CastKey) && FlareStore.CastKey.StartsWith("reallife:flare_") && loaded != null && loaded.Influencers.Count == f.Influencers.Count && loaded.Follows.Count == 2,
            "Flare: the cast is saved under reallife:flare_cast and loads with the account");
        data.Remove(FlareStore.CastKey);
        FlareFile old = FlareStore.Load(data);
        Check(old != null && old.Influencers.Count == 0 && old.Follows.Count == 0, "Flare: a save from before the influencers loads with none (a cast is made on start)");
    }

    private static void FlareCastTiming()
    {
        FlareFile f = FlareCastFile(51);
        var heat = new int[(int)FlareNiche.Business + 1];
        heat[0] = 800;
        var news = new List<FlareCastNews>();
        var shops = new List<FlareShop> { new FlareShop { Place = "A~1", Name = "Shop", Niche = FlareNiche.Food, District = FlareDistricts[0] } };
        var visits = new List<FlarePost>();
        var clock = Stopwatch.StartNew();
        for (int day = 2; day < 3000; day++)
        {
            if (day % FlareCast.MonthDays == 0)
            {
                news.Clear();
                FlareCast.Month(f, day, heat, null, true, FlareDistricts, news);
            }
            visits.Clear();
            FlareCast.Day(f, day, shops, new[] { "R" }, null, new[] { "B" }, visits);
        }
        FlareStore.ReadCast(FlareRules.Open(1, 1, 0, 50), FlareStore.WriteCast(f));
        clock.Stop();
        Check(clock.ElapsedMilliseconds < 2000 && f.ActiveInfluencers <= FlareCast.Most, "Flare: three thousand days of the city's influencers in under 2 s (" + clock.ElapsedMilliseconds + " ms)");
    }
}
