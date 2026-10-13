using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Markets;

// Checks for the rest of the city on Flare (Scripts/Social/Flare, FlareRules): people met online, the partner and the
// friends, the feed's mood and the issues, the market's buzz, a post about shares with its disclosure and the window of
// market abuse, a Celebrity in court, the newest pictures, and the save's new fields.
internal static partial class Program
{
    private static void FlareCityChecks()
    {
        FlareOnline();
        FlarePartner();
        FlareMoodAndIssues();
        FlareShares();
        FlarePicturesAndFame();
        FlareCitySave();
    }

    private static void FlareOnline()
    {
        int[] met = new int[5];
        for (int tier = 0; tier < 5; tier++)
        {
            FlareFile f = FlareRules.Open(17 + tier, 1, 0, 50);
            f.Followers = FlareRules.TierFrom[tier];
            for (int day = 2; day < 362; day++)
            {
                if (FlareRules.MeetsOnline(f, day, true))
                {
                    f.OnlineDay = day;
                    met[tier]++;
                }
            }
        }
        Check(met[0] == 0 && met[1] > 6 && met[1] < 45 && met[4] >= met[1] && met[4] <= 360 / FlareRules.OnlineGapDays,
            "Flare: nobody meets a new account online; a few a month a bigger one, never closer than five days (" + string.Join(", ", met) + " in a year)");
        FlareFile g = FlareRules.Open(5, 1, 0, 50);
        g.Followers = 100000;
        bool none = true;
        for (int day = 2; day < 200; day++)
        {
            none &= !FlareRules.MeetsOnline(g, day, false);
        }
        Check(none, "Flare: only a day after a post");
    }

    private static void FlarePartner()
    {
        Check(FlareRules.ViewOf(FlareTaste.Cheers) == FlareRules.PartnerView.Proud && FlareRules.ViewOf(FlareTaste.Cool) == FlareRules.PartnerView.Jealous
              && FlareRules.ViewOf(FlareTaste.Sport) == FlareRules.PartnerView.Cool, "Flare: the partner proud, jealous or cool by their nature");
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        Check(!FlareRules.NeverPosted(f, 30, true), "Flare: no others posted: the partner does not mind");
        FlareRules.Tagged(f, false, 25);
        Check(!FlareRules.NeverPosted(f, 30, false) && FlareRules.NeverPosted(f, 30, true) && !FlareRules.NeverPosted(f, 31, true), "Flare: three weeks without the partner while others are posted: they mind, once");
        FlareRules.Tagged(f, true, 40);
        FlareRules.Tagged(f, false, 55);
        Check(!FlareRules.NeverPosted(f, 56, true) && FlareRules.NeverPosted(f, 61, true), "Flare: posting the partner starts the three weeks again");
        Check(!FlareRules.Neglect(f, 10, 2, true) && FlareRules.Neglect(f, 10, 3, true) && !FlareRules.Neglect(f, 12, 5, true) && !FlareRules.Neglect(f, 20, 5, false) && FlareRules.Neglect(f, 20, 5, true),
            "Flare: three posts a day while a friend's texts wait: they mind, once a week");
        var people = new List<FlarePerson> { new FlarePerson { Index = 4, First = "Lee", Stage = 4, Partner = true } };
        FlareFile h = FlareRules.Open(5, 1, 0, 50);
        FlareRules.Post(h, FlareRules.Draft(h, FlareSource.Selfie, 3, 10, 4, null, "s.png", 0), FlareTone.Cheerful, 4, people, 3, 10, 600);
        Check(h.PartnerTagDay == 3 && h.OthersTagDay < 0, "Flare: a post tagging the partner is noted as theirs");
    }

    private static void FlareMoodAndIssues()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        Check(FlareRules.Mood(f, 2) == -16 && FlareRules.Mood(f, 0) == -12 && FlareRules.ApprovalPull(-12) == -1, "Flare: bad news darkens the feed's mood, which fades by a fifth a day");
        for (int i = 0; i < 40; i++)
        {
            FlareRules.Mood(f, 5);
        }
        Check(f.MayorMood >= -100 && FlareRules.ApprovalPull(f.MayorMood) == -10 && FlareRules.ApprovalPull(100) == 10 && FlareRules.ApprovalPull(0) == 0,
            "Flare: the pull on the mayor's approval is a point a day at most either way");
        Check(FlareRules.IssueWeight(0) == 0 && FlareRules.IssueWeight(FlareRules.EndorseFull) == FlareRules.IssueMost && FlareRules.IssueWeight(int.MaxValue) == FlareRules.IssueMost
              && FlareRules.IssueWeight(500) < FlareRules.IssueWeight(5000), "Flare: an issue post adds more for a bigger account, never past 0.3 of a point");
        Check(MarketInputs.EventStorm == 7 && MarketInputs.EventBuzz == 8 && MarketInputs.EventTrade == 9 && MarketInputs.EventVolume == 10 && MarketInputs.EventCompany == 11 && MarketInputs.EventCount == 12,
            "Market: the buzz keeps 8, after the seven that keep their numbers, a shared trade 9 and 10, and the tech sector's news of a company 11");
        var up = new MarketEvent { Kind = MarketInputs.EventBuzz, A = 3, B = 1 };
        var down = new MarketEvent { Kind = MarketInputs.EventBuzz, A = 3, B = -9 };
        Check(MarketInputs.BuzzBp(up, MarketSector.Restaurants, MarketSector.Restaurants) == MarketInputs.BuzzKickBp && MarketInputs.BuzzBp(down, MarketSector.Restaurants, MarketSector.Restaurants) == -MarketInputs.BuzzKickBp
              && MarketInputs.BuzzBp(up, MarketSector.Banks, MarketSector.Restaurants) == 0 && MarketInputs.EventBp(up, MarketSector.Restaurants) == 0,
            "Market: a buzz moves the sector of its trade only, by its capped kick either way");
    }

    private static void FlareShares()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        f.Followers = 10000;
        FlarePost p = FlareRules.Post(f, FlareRules.Draft(f, FlareSource.Shares, 4, 9, -1, null, "Hudson Foods", 42), FlareTone.Honest, -1, null, 4, 9, 540, true);
        Check(p.Disclosed && p.Viral && f.ViralShares.Count == 1 && f.ViralShares[0].Stock == 42, "Flare: a Known account's post about shares it holds says so; a viral one is remembered");
        Check(FlareRules.Abuse(f, 41, 5) == null && FlareRules.Abuse(f, 42, 5) != null && f.ViralShares.Count == 0, "Flare: selling those shares within two days is market abuse, once");
        f.ViralShares.Add(new FlareViralShares { Stock = 42, Day = 4 });
        Check(FlareRules.Abuse(f, 42, 7) == null, "Flare: three days after, it is not");
        FlareRules.AbuseDay(f, 7);
        Check(f.ViralShares.Count == 0, "Flare: and is let go");
        FlareFile small = FlareRules.Open(5, 1, 0, 50);
        FlarePost q = FlareRules.Post(small, FlareRules.Draft(small, FlareSource.Shares, 4, 9, -1, null, "X", 1), FlareTone.Honest, -1, null, 4, 9, 540);
        Check(!q.Disclosed, "Flare: a small account need not disclose");
    }

    private static void FlarePicturesAndFame()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        for (int i = 0; i < 8; i++)
        {
            FlareRules.Post(f, FlareRules.Draft(f, i % 2 == 0 ? FlareSource.Photo : FlareSource.Selfie, i, 10, -1, null, "p" + i + ".png", 0), FlareTone.Cheeky, -1, null, i, 10, 600);
        }
        FlareRules.Post(f, FlareRules.Draft(f, FlareSource.GoodDeed, 9, 10, -1, null, null, 0), FlareTone.Honest, -1, null, 9, 10, 600);
        var newest = new List<int>();
        FlareRules.Pictures(f, newest);
        Check(newest.Count == FlareRules.Thumbs && f.Post(newest[0]).Name == "p7.png" && f.Post(newest[4]).Name == "p3.png", "Flare: the five newest pictures, newest first, and nothing else");
        Check(FlareRules.Fame(f) == 0 && FlareRules.Fame(new FlareFile { Followers = FlareRules.TierFrom[4] }) == FlareRules.FameFee && FlareRules.FameFee <= 250, "Flare: only a Celebrity pays a lawyer more, and a little");
    }

    private static void FlareCitySave()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        f.OnlineDay = 9;
        f.PartnerTagDay = 8;
        f.OthersTagDay = 7;
        f.NeverDay = 6;
        f.NeglectDay = 5;
        f.LedgerDay = 4;
        f.MayorMood = -33;
        f.IssueDay = 3;
        f.ViralShares.Add(new FlareViralShares { Stock = 12, Name = "Hudson | Foods", Day = 2 });
        f.Posts.Add(new FlarePost { Id = 99, Day = 1, Source = FlareSource.Shares, Disclosed = true, Name = "Hudson Foods" });
        f.Posts.Add(new FlarePost { Id = 98, Day = 1, Author = FlareAuthor.Ledger, Source = FlareSource.Headline, Name = "FIRE CREWS CALLED OUT." });
        string a = FlareStore.WriteAccount(f), p = FlareStore.WritePosts(f);
        FlareFile b = FlareStore.Read(a, p);
        Check(b != null && FlareStore.WriteAccount(b) == a && FlareStore.WritePosts(b) == p && b.OnlineDay == 9 && b.MayorMood == -33 && b.ViralShares.Count == 1 && b.ViralShares[0].Name == "Hudson / Foods"
              && b.Post(99).Disclosed && b.Post(98).Author == FlareAuthor.Ledger, "Flare: the city's part of the account survives the save");
    }
}
