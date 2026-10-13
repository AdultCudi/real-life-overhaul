using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Flare;

// Checks for what a Flare post does elsewhere (Scripts/Social/Flare, Wiring/FlareLinks): the talk about a business and
// its cap, the gap between two hypes, a scandal's spread and an honest answer (with the brand's own rules), the word in
// an election, an arrest trending, the boss who follows, a sponsored post as the company's fifth campaign priced
// against radio, and the company's save keeping it beside what an older version reads.
internal static partial class Program
{
    private static void FlareInfluences()
    {
        FlareTalk();
        FlareScandals();
        FlareElection();
        FlareLawAndWork();
        FlareSponsored();
        FlareCompanySave();
    }

    private static void FlareTalk()
    {
        Check(FlareRules.BuzzOf(new FlarePost { Place = "A~1", Reach = 10 }) == 1 && FlareRules.BuzzOf(new FlarePost { Place = "A~1", Reach = 1000 }) == 2
              && FlareRules.BuzzOf(new FlarePost { Place = "A~1", Reach = 5000 }) == 3 && FlareRules.BuzzOf(new FlarePost { Place = "A~1", Reach = 10, Viral = true }) == 4,
            "Flare: talk about a business grows with the people who see the post");
        Check(FlareRules.BuzzOf(new FlarePost { Reach = 9000 }) == 0 && FlareRules.BuzzOf(new FlarePost { Place = "A~1", Reach = 9000, Bad = true }) == 0
              && FlareRules.BuzzOf(new FlarePost { Place = "A~1", Reach = 9000, Author = FlareAuthor.Friend }) == 0, "Flare: no talk without a business, from a bad post or from a friend's");
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        Check(FlareRules.AddBuzz(f, "A~1", 4, 10, 3) == 4 && FlareRules.AddBuzz(f, "A~1", 4, 10, 3) == FlareRules.BuzzMost && FlareRules.AddBuzz(f, "A~1", 4, 11, 3) == FlareRules.BuzzMost,
            "Flare: talk adds up to 6% and no more");
        Check(f.BuzzAt("B~2", 10) == 0 && FlareRules.AddBuzz(f, "B~2", -9, 10, 3) == -FlareRules.BuzzMost, "Flare: each business its own, and the cap holds the other way too");
        Check(f.BuzzAt("A~1", 12) == FlareRules.BuzzMost && FlareRules.BuzzDay(f, 13) && f.BuzzAt("A~1", 13) == 0, "Flare: talk lasts its three days, then fades");
        FlareRules.BuzzDay(f, 20);
        Check(f.Buzz.Count == 0 && f.BuzzAt("A~1", 20) == 0, "Flare: and is gone");
        Check(FlareRules.MayHype(f, 1) && !FlareRules.MayHype(new FlareFile { HypeDay = 10 }, 23) && FlareRules.MayHype(new FlareFile { HypeDay = 10 }, 24), "Flare: the game's hype once a fortnight at most");
    }

    private static void FlareScandals()
    {
        Check(FlareRules.ScandalScale(50) == 1000 && FlareRules.ScandalScale(100) == 1000 && FlareRules.ScandalScale(0) == FlareRules.ScandalWorst && FlareRules.ScandalScale(25) == 1125,
            "Flare: a scandal spreads by a quarter more for an account of no reputation, never less than the whole without an answer");
        Check(FlareRules.Answered(400, 1000) == 80 && FlareRules.Answered(500, 1250) == 180 && FlareRules.Answered(0, 1250) == 0, "Flare: an honest answer brings the loss down to four fifths of the plain one");
        var b = new Brand { Id = 1, City = 2000 };
        b.Districts.Add(new DistrictFile { Name = "ba:neighborhood_midtown", Rec = 5000 });
        int lost = BrandRules.Hit(b, "ba:neighborhood_midtown", Scandal.Inspection, FlareRules.ScandalScale(0));
        int back = BrandRules.Regain(b, "ba:neighborhood_midtown", FlareRules.Answered(lost, FlareRules.ScandalScale(0)));
        Check(lost == 500 && back == 180 && b.District("ba:neighborhood_midtown").Rec == 5000 - 320, "Flare and brands: a doubted account's scandal costs 5 points, an honest answer leaves 3.2 of them lost");
        Check(BrandRules.Hit(b, "ba:neighborhood_midtown", Scandal.Inspection, 99999) == 500 && BrandRules.Hit(b, "ba:neighborhood_midtown", Scandal.Inspection, 1) == 320
              && BrandRules.Hit(b, "ba:neighborhood_midtown", Scandal.Inspection) == 400, "Brands: the scale is held between four fifths and a quarter more");
        var top = new Brand { Id = 2, City = 9990 };
        top.Districts.Add(new DistrictFile { Name = "x", Rec = BrandCatalogue.RecMost - 10 });
        Check(BrandRules.Regain(top, "x", 500) == 10 && top.District("x").Rec == BrandCatalogue.RecMost && BrandRules.Regain(top, "nowhere", 500) == 0, "Brands: what comes back never passes the most");
    }

    private static void FlareElection()
    {
        Check(FlareRules.EndorseShare(0, 80) == 0 && FlareRules.EndorseShare(FlareRules.EndorseFull, 80) == 1000 && FlareRules.EndorseShare(10000000, 80) == 1000, "Flare: an account's word in an election, up to the whole at 100,000 followers");
        Check(FlareRules.EndorseShare(FlareRules.EndorseFull, 25) == 500 && FlareRules.EndorseShare(FlareRules.EndorseFull, 0) == 0, "Flare: and only as far as the city trusts it");
        bool rising = true;
        for (int f = 0, last = -1; f <= 200000; f += 1000)
        {
            int share = FlareRules.EndorseShare(f, 60);
            rising &= share >= last;
            last = share;
        }
        Check(rising, "Flare: more followers never weaken it");
    }

    private static void FlareLawAndWork()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        f.Followers = 400;
        Check(FlareRules.Arrest(f, 3) == 0 && f.Followers == 400 && f.Reputation == 50, "Flare: an arrest does not trend for a small account");
        f.Followers = 10000;
        Check(FlareRules.Arrest(f, 4) == 300 && f.Followers == 9700 && f.Reputation == 50 - FlareRules.ArrestReputationKnown && FlareRules.Arrest(f, 4) == 0,
            "Flare: from Local on an arrest trends, 3% of the followers, once a day");
        int follows = 0;
        bool same = true, rankHelps = true;
        for (int seed = 0; seed < 2000; seed++)
        {
            bool low = FlareRules.BossFollows(seed, 300, FlareTier.Newcomer);
            follows += low ? 1 : 0;
            same &= low == FlareRules.BossFollows(seed, 300, FlareTier.Newcomer);
            rankHelps &= !low || FlareRules.BossFollows(seed, 302, FlareTier.Known);
        }
        Check(follows > 300 && follows < 500 && same && rankHelps, "Flare: a fifth of bosses follow a new account, the same ones each time, and more for rank and tier (" + follows + " of 2000)");
        Check(!FlareRules.BossFollows(5, -1, FlareTier.Celebrity), "Flare: no job, no boss");
    }

    private static void FlareSponsored()
    {
        int kind = (int)CampaignKind.Sponsored;
        int kinds = CompanyCatalogue.CampaignKinds;
        Check(kind == 4 && (int)CampaignKind.Giveaway == 10 && kinds == 11 && CompanyCatalogue.CampaignOwnKinds == 4 && CompanyCatalogue.CampaignCost.Length == kinds && CompanyCatalogue.CampaignDays.Length == kinds
              && CompanyCatalogue.CampaignFootfall.Length == kinds && CompanyCatalogue.CampaignLocal.Length == kinds && !CompanyCatalogue.CampaignLocal[kind],
            "Company: a sponsored post is the fifth campaign, after the four that keep their numbers, with an entry in every table");
        Check(FlareRules.SponsoredReach(499) == 0 && FlareRules.SponsoredReach(500) == FlareRules.SponsoredLeast && FlareRules.SponsoredReach(5000) == 625
              && FlareRules.SponsoredReach(100000000) == FlareRules.SponsoredMost, "Flare: a sponsored post from Local on, its reach by followers on a log scale");
        // Radio's price per customer per day against a sponsored post's, at every tier and a middling reputation: close to it.
        int radio = CompanyCatalogue.Clamp(CampaignRules.Effect((int)CampaignKind.Radio, 1000, CycleState.Steady, 1000), 1, 99);
        double radioPer = CompanyCatalogue.CampaignCost[(int)CampaignKind.Radio] / (double)(radio * CompanyCatalogue.CampaignDays[(int)CampaignKind.Radio]);
        bool close = true;
        string seen = string.Empty;
        foreach (int followers in new[] { 500, 5000, 25000, 100000 })
        {
            int reach = FlareRules.SponsoredReach(followers);
            int cost = (int)((long)CompanyCatalogue.CampaignCost[kind] * FlareRules.SponsoredCost(reach, 50) / 1000);
            int percent = CampaignRules.Effect(kind, 1000, CycleState.Steady, 1000, reach);
            double per = cost / (double)(percent * CompanyCatalogue.CampaignDays[kind]);
            close &= per > radioPer * 0.6 && per < radioPer * 1.4;
            seen += " " + followers + ": " + cost + " for " + percent + "%;";
        }
        Check(close, "Flare: a sponsored post costs about what radio does for each customer, at every tier (radio " + radioPer.ToString("0") + " a point a day;" + seen + ")");
        int liked = FlareRules.SponsoredCost(1000, 90), doubted = FlareRules.SponsoredCost(1000, 10);
        Check(liked < 1000 && doubted > 1000, "Flare: a little cheaper for a trusted account, dearer for a doubted one");
        Check(CampaignRules.Effect(kind, 1000, CycleState.Steady, 1000, 99999) == CompanyCatalogue.CampaignMost && CampaignRules.Effect(kind, 1000, CycleState.Steady, 1000, 0) == 1
              && CampaignRules.Effect((int)CampaignKind.Radio, 1000, CycleState.Steady, 1000) == CampaignRules.Effect((int)CampaignKind.Radio, 1000, CycleState.Steady, 1000, 1000),
            "Company: a reach from outside stays inside the campaigns' own bounds, and the company's own campaigns are as they were");
    }

    private static void FlareCompanySave()
    {
        CompanyState s = FullCompany();
        s.Shops[0].PushEnded[(int)CampaignKind.Sponsored] = 90;
        ShopFile second = s.Shops[1];
        CampaignRules.Book(second, (int)CampaignKind.Sponsored, 300, 5, 80);
        string line = CompanyStore.Write(s);
        int version;
        CompanyState back = CompanyStore.Read(line, out version);
        Check(back != null && back.Shops[0].PushEnded[(int)CampaignKind.Sponsored] == 90 && back.Shops[1].Push == (int)CampaignKind.Sponsored && back.Shops[1].PushPercent == 5
              && CompanyStore.Write(back) == line, "Company: a sponsored post and when the last one ended survive the save");
        string tail = string.Empty;
        for (int k = CompanyCatalogue.CampaignOwnKinds; k < s.Shops[0].PushEnded.Length; k++)
        {
            tail += "|" + s.Shops[0].PushEnded[k];
        }
        string older = line.Replace(tail + ";", ";");   // the first shop as a version before the new kinds wrote it
        CompanyState old = older != line ? CompanyStore.Read(older, out version) : null;
        Check(old != null && old.Shops[0].PushEnded[(int)CampaignKind.Sponsored] == 0 && old.Shops[0].PushEnded[(int)CampaignKind.Radio] == s.Shops[0].PushEnded[(int)CampaignKind.Radio]
              && old.Shops[0].Permits[0].Until == s.Shops[0].Permits[0].Until && old.Shops[0].Cover[0].Insurer == s.Shops[0].Cover[0].Insurer,
            "Company: a line from before the sponsored post reads as before, with none ended");
        // An older version reads only the fields it knows: the new one is after every one of them.
        string[] fields = line.Substring(line.IndexOf("^s:", StringComparison.Ordinal) + 3).Split(';')[0].Split('|');
        Check(fields.Length == 1 + 20 + 3 * 7 + 4 * 3 + CompanyCatalogue.CampaignKinds - CompanyCatalogue.CampaignOwnKinds && fields[1 + 20 + 3 * 7 + 4 * 3] == "90",
            "Company: the new fields go after everything an older version reads");
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        FlareRules.AddBuzz(f, "Main%20St~1", 3, 4, 3);
        f.HypeDay = 9;
        f.ComplaintDay = 8;
        f.NoticedDay = 7;
        f.StoryDay = 6;
        f.ArrestDay = 5;
        FlareFile r = FlareStore.Read(FlareStore.WriteAccount(f), FlareStore.WritePosts(f));
        Check(r != null && r.Buzz.Count == 1 && r.BuzzAt("Main%20St~1", 5) == 3 && r.HypeDay == 9 && r.ComplaintDay == 8 && r.NoticedDay == 7 && r.StoryDay == 6 && r.ArrestDay == 5,
            "Flare: the talk and the days of what a post did survive the save");
        Check(FlareStore.Read("FL1|1|2|3|50|50|-1|-1|-1|0|-100|0|0|1|0|0|0|-1|50,50,50,50,50,50,50", null) != null, "Flare: a line from before these fields reads, with none of them");
    }
}
