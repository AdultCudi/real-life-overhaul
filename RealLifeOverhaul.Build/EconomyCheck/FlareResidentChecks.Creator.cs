using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;
using SectorNews = RealLifeOverhaul.Tech.SectorNews;

// Flare in the city, the player as a creator, the studio on Flare, the market and the council (the brief's fourth, sixth, seventh, eighth and ninth
// steps), game-free on the population's city: the city's businesses offer paid posts only from Local on, one open at a time, never the player's own,
// paid by the followers and the city's trust, a sponsorship's posts on their days, a creator income from Influencer on; a devlog's hype by the
// residents who play games it excited, a campaign's pledges by those who follow and play, never past the goal; a viral thread's kicks on the market by
// the investors it reached; the player's word in an election by the city's adults who follow them; the save keeps the offers.
internal static partial class Program
{
    private static void FlareResidentCreator()
    {
        FlareResidentRig rig = FlareResidentCity(42, 5000, (int)FlareTier.Known);
        PopulationState s = rig.S;
        FlareCityState c = rig.C;
        // offers: none below Local, some at the top, one open at a time, never a business of the player's
        var none = new FlareCityState { Seed = c.Seed };
        int below = 0;
        for (int day = 1; day < 300; day++)
        {
            below += FlareCityRules.Offer(none, s, day, 400, (int)FlareTier.Newcomer, 50, 1000) != null ? 1 : 0;
        }
        var top = new FlareCityState { Seed = c.Seed };
        int made = 0, open = 0, sponsors = 0;
        bool mine = false, one = true;
        for (int day = 1; day < 300; day++)
        {
            FlareOffer o = FlareCityRules.Offer(top, s, day, 200000, (int)FlareTier.Celebrity, 80, 1000);
            if (o == null)
            {
                continue;
            }
            made++;
            int b = s.BusinessOfKey(o.Key);
            mine |= b < 0 || s.Businesses.Owner[b] == (byte)Owner.Player || s.Businesses.Owner[b] == (byte)Owner.Guest;
            sponsors += o.Posts > 1 ? 1 : 0;
            open = 0;
            foreach (FlareOffer x in top.Offers)
            {
                open += x.Open(day) ? 1 : 0;
            }
            one &= open == 1;
        }
        Check(below == 0 && made > 20 && !mine && one && sponsors > 0 && sponsors < made,
            "flare city creator: the city's businesses offer paid posts from Local on, one open at a time, never a business of a player's, sometimes a sponsorship (" + made + " offers in 300 days, " + sponsors + " sponsorships)");
        Check(FlareCityRules.OfferFee(0, 50, 1000) == FlareCityRules.OfferFeeLeast && FlareCityRules.OfferFee(100000, 50, 1000) > FlareCityRules.OfferFee(10000, 50, 1000)
              && FlareCityRules.OfferFee(100000, 90, 1000) > FlareCityRules.OfferFee(100000, 10, 1000) && FlareCityRules.OfferFee(100000, 50, 1000) == 100000L * FlareCityRules.OfferFeeThousand / 1000
              && FlareCityRules.OfferFee(100000, 50, 2000) == 2 * FlareCityRules.OfferFee(100000, 50, 1000) && FlareCityRules.OfferFee(0, 50, 1500) == FlareCityRules.OfferFeeLeast * 3 / 2,
            "flare city creator: a post's fee follows the followers, the city's trust in the account and the game's prices, with a least (" + FlareCityRules.OfferFee(100000, 50, 1000) / 100
            + " for 100,000 followers)");
        var offer = new FlareOffer { Posts = FlareCityRules.SponsorPosts, Day = 10, Until = 12, Fee = 5000 };
        bool taken = FlareCityRules.Accept(offer, 11) && FlareCityRules.Due(offer, 11) && !FlareCityRules.Accept(offer, 11);
        FlareCityRules.Posted(offer, 11);
        bool next = !FlareCityRules.Due(offer, 12) && FlareCityRules.Due(offer, 11 + FlareCityRules.SponsorGap);
        for (int i = 1; i < FlareCityRules.SponsorPosts; i++)
        {
            FlareCityRules.Posted(offer, 11 + i * FlareCityRules.SponsorGap);
        }
        Check(taken && next && !offer.Running && offer.Next == -1 && offer.Done == FlareCityRules.SponsorPosts && !FlareCityRules.Accept(new FlareOffer { Until = 5 }, 6),
            "flare city creator: an offer is taken while open, its posts come every " + FlareCityRules.SponsorGap + " days until done, and a lapsed one cannot be taken");
        var month = new FlareCityState();
        bool creator = !FlareCityRules.CreatorDue(month, 100, (int)FlareTier.Known) && !FlareCityRules.CreatorDue(month, 100, (int)FlareTier.Influencer) && month.CreatorDay == 100
                       && !FlareCityRules.CreatorDue(month, 129, (int)FlareTier.Influencer) && FlareCityRules.CreatorDue(month, 130, (int)FlareTier.Influencer);
        Check(creator && FlareCityRules.CreatorIncome(500000, 1000) == 500000L * FlareCityRules.CreatorThousandViews / 1000 && FlareCityRules.CreatorIncome(-5, 1000) == 0
              && FlareCityRules.CreatorIncome(500000, 1200) == FlareCityRules.CreatorIncome(500000, 1000) * 12 / 10,
            "flare city creator: a creator income from Influencer on, every " + FlareCityRules.CreatorDays + " days, for the views of the player's posts");

        // the studio on Flare: residents who play games, a devlog's hype, a campaign's pledges
        int gamers = FlareCityRules.Gamers(c, s);
        int adults = FlareCityRules.AdultsOf(c);
        Check(gamers > adults / 10 && gamers < adults, "flare city studio: the city's adults who play games are the population's own (" + gamers + " of " + adults + ")");
        Check(FlareCityRules.DevlogHype(0, gamers) == 0 && FlareCityRules.DevlogHype(gamers * 10, gamers) == FlareCityRules.DevlogHypeMost
              && FlareCityRules.DevlogHype(gamers / 20, gamers) > FlareCityRules.DevlogHype(gamers / 200, gamers) && FlareCityRules.DevlogHype(1, gamers) >= 1,
            "flare city studio: a devlog's hype grows with the residents who play it excited, up to a trailer's (" + FlareCityRules.DevlogHype(gamers / 20, gamers) + " thousandths for a twentieth of them)");
        Check(FlareCityRules.DevlogHypeFollowers(0) == 0 && FlareCityRules.DevlogHypeFollowers(100000) > FlareCityRules.DevlogHypeFollowers(1000)
              && FlareCityRules.DevlogHypeFollowers(int.MaxValue) == FlareCityRules.DevlogHypeMost, "flare city studio: with no residents to count, a devlog's hype follows the player's followers");
        var games = new CityPost { Id = 77, Account = FlareCityState.PlayerAccount, Business = -1, Kind = FlareCityRules.GamesKind, Sign = 0, Quality = 1000, Fit = 1000, Seed = 5 };
        HeardReport heard = FlareCityRules.Hear(c, s, games, s.Day, rig.Pass.AccountBiz, rig.Pass.Scratch, null);
        Check(heard.Seen > 0 && heard.Interested > 0, "flare city studio: a post about games is heard by the residents (" + heard.Interested + " of " + heard.Seen + " interested)");
        long goal = 5000000, all = long.MaxValue / 4;
        int studio = c.Account("s:Studio~1");
        long city = FlareCityRules.Pledges(c, s, FlareCityState.PlayerAccount, studio, 0, all, 9, rig.Pass.Scratch);
        long pledged = FlareCityRules.Pledges(c, s, FlareCityState.PlayerAccount, studio, 20000, goal, 9, rig.Pass.Scratch);
        long small = FlareCityRules.Pledges(c, s, FlareCityState.PlayerAccount, studio, 0, 1000, 9, rig.Pass.Scratch);
        long twice = FlareCityRules.Pledges(c, s, FlareCityState.PlayerAccount, FlareCityState.PlayerAccount, 0, all, 9, rig.Pass.Scratch);
        long once = FlareCityRules.Pledges(c, s, FlareCityState.PlayerAccount, -1, 0, all, 9, rig.Pass.Scratch);
        long beyond = 20000L * FlareCityRules.BeyondPledges / 1000 * FlareCityRules.BeyondPledgeWhole * 100;
        int playing = 0, start;
        int follow = c.FollowersOf(FlareCityState.PlayerAccount, out start);
        for (int i = 0; i < follow; i++)
        {
            playing += FlareCityRules.PlaysGames(s, c.FollowerList[start + i]) ? 1 : 0;
        }
        Check(city > 0 && city <= (long)playing * FlareCityRules.PledgeWhole[2] * 100 && pledged == Math.Min(goal, city + beyond) && small == Math.Min(1000, city) && twice == once
              && FlareCityRules.Pledges(c, s, FlareCityState.PlayerAccount, studio, 20000, 0, 9, rig.Pass.Scratch) == 0,
            "flare city studio: a campaign is backed by the residents who follow and play, each once, and by followers beyond the city, never past its goal (" + city / 100 + " from "
            + playing + " followers who play, " + beyond / 100 + " from beyond)");
        Check(FlareCityRules.PledgesFollowers(100000, goal) == 100000L * FlareCityRules.BeyondPledges / 1000 * FlareCityRules.BeyondPledgeWhole * 100 && FlareCityRules.PledgesFollowers(100000, 500) == 500
              && FlareCityRules.PledgesFollowers(-3, goal) == 0, "flare city studio: with no residents to count, a campaign is backed by the player's followers alone, never past its goal");

        // the market and the council
        Check(FlareCityRules.MarketKicks(0) == 1 && FlareCityRules.MarketKicks(FlareCityCatalogue.InvestorsPerKick) == 2 && FlareCityRules.MarketKicks(int.MaxValue) == FlareCityCatalogue.MarketKicksMost,
            "flare city market: a viral thread's kicks on its trade by the city's investors it reached, " + FlareCityCatalogue.MarketKicksMost + " at the most");
        int b0 = FlareResidentShop(s, Spend.Electronics);
        var viral = new CityPost { Id = 78, Account = FlareCityState.PlayerAccount, Business = b0, Kind = s.Businesses.Kind[b0], Sign = 1, Quality = 1000, Fit = 1000, Seed = 6, Viral = true };
        HeardReport v = FlareCityRules.Hear(c, s, viral, s.Day, rig.Pass.AccountBiz, rig.Pass.Scratch, null);
        Check(v.Investors > 0 && v.Investors <= v.Interested, "flare city market: the investors a viral post reached are interested residents with savings (" + v.Investors + " of " + v.Interested + ")");
        Check(FlareCityRules.IssueMood(0, adults) == FlareCityCatalogue.IssueMoodLeast && FlareCityRules.IssueMood(adults, adults) == FlareCityCatalogue.IssueMoodMost
              && FlareCityRules.IssueMood(FlareCityCatalogue.IssueMoodAt, 1000) == FlareRules.MoodIssuePost && FlareCityRules.IssueMood(FlareCityCatalogue.IssueMoodAt * 2, 1000) == FlareRules.MoodIssuePost * 2
              && FlareCityRules.IssueMood(5, 0) == FlareCityCatalogue.IssueMoodMost,
            "flare city council: a post about an issue moves the feed's mood by the residents it reached, Flare's own move for a usual reach");
        Check(FlareCityRules.EndorseShare(0, adults, 50) == 0 && FlareCityRules.EndorseShare(adults, adults, 50) == 1000 && FlareCityRules.EndorseShare(500, 2000, 50) == 500
              && FlareCityRules.EndorseShare(adults, adults, 25) == 500 && FlareCityRules.EndorseShare(adults, adults, 90) == 1000 && FlareCityRules.EndorseShare(5, 0, 50) == 1000,
            "flare city council: the player's word in an election is worth the share of the city's adults who follow them, for a trusted account");

        // the save keeps the offers, the creator's day and a scandal's day
        c.Offers.Add(new FlareOffer { Id = 3, Key = "Main St|4", Name = "Five Cups", Posts = 4, Done = 1, Fee = 7200, Day = 9, Until = 11, Accepted = true, Next = 13 });
        c.NextOffer = 4;
        c.CreatorDay = 20;
        c.ScandalDay = 21;
        FlareCityState back = FlareCityStore.Decode(FlareCityStore.Encode(c), out _);
        FlareOffer o2 = back != null && back.Offers.Count > 0 ? back.Offers[back.Offers.Count - 1] : null;
        Check(o2 != null && o2.Id == 3 && o2.Key == "Main St|4" && o2.Name == "Five Cups" && o2.Posts == 4 && o2.Done == 1 && o2.Fee == 7200 && o2.Accepted && o2.Next == 13
              && back.NextOffer == 4 && back.CreatorDay == 20 && back.ScandalDay == 21, "flare city creator: the save keeps the offers, the creator's month and a scandal's day");
        FlareCityView offers = FlareCityViews.Offers(c, 10);
        Check(offers.Rows.Count > 0 && offers.Rows[0].Post == 3 && offers.Rows[0].Value == 72 && offers.Rows[0].Second == 4 && offers.Rows[0].Subtitle.EndsWith("offer_running", StringComparison.Ordinal),
            "flare city creator: the offers' screen shows each business, its fee a post, its posts and where it stands");
        string links = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Wiring", "Wiring.FlareCityLinks.cs"));
        string residents = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Social", "Flare", "FlareDirector.Residents.cs"));
        Check(links.Contains("StudioBridges.Talk = Talk;") && links.Contains("StudioBridges.Devlog = Devlog;") && links.Contains("StudioBridges.Crowdfund = Crowdfund;") && links.Contains("StudioBridges.Talk = null;")
              && residents.Contains("CreatorDay(residents,"), "flare city sources: the studio's three bridges are Flare's, and the creator's day follows the residents' day");

        // the other tech firms' news (Tech/Sector/SectorBridges.Talk)
        bool signs = FlareCityRules.SectorSign((int)SectorNews.Launch) == 1 && FlareCityRules.SectorSign((int)SectorNews.Benchmark) == 1
                     && FlareCityRules.SectorSign((int)SectorNews.Award) == 1 && FlareCityRules.SectorSign((int)SectorNews.Outage) == -1
                     && FlareCityRules.SectorSign((int)SectorNews.Breach) == -1 && FlareCityRules.SectorSign((int)SectorNews.Incident) == -1
                     && FlareCityRules.SectorSign((int)SectorNews.RivalShipped) == 0 && FlareCityRules.SectorSign((int)SectorNews.TenderWon) == 0;
        bool rivals = FlareCityRules.SectorRival((int)SectorNews.RivalShipped) && FlareCityRules.SectorRival((int)SectorNews.RivalCut)
                      && FlareCityRules.SectorRival((int)SectorNews.TenderWon) && !FlareCityRules.SectorRival((int)SectorNews.Launch)
                      && !FlareCityRules.SectorRival((int)SectorNews.Award) && !FlareCityRules.SectorRival((int)SectorNews.Outage);
        string sector = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Social", "Flare", "FlareDirector.Sector.cs"));
        string live = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Social", "Flare", "FlareText.Live.cs"));
        Check(signs && rivals && (int)SectorNews.Count == 9 && FlareCityRules.SectorNewsArg > FlareCityRules.CrowdfundArg && links.Contains("SectorBridges.Talk = SectorTalk;")
              && links.Contains("SectorBridges.Talk = null;") && sector.Contains("if (Following || string.IsNullOrEmpty(name))") && sector.Contains("Kind = (int)Spend.Digital")
              && live.Contains("if (p.Arg >= FlareCityRules.SectorNewsArg)") && residents.Contains("p.Arg >= FlareCityRules.StudioNewsArg)"),
            "flare city sources: the other tech firms' news is Flare's: a firm of the player's posts on its page, its launch drawing residents and its outage keeping them away, a rival's is the city's own post, on the host alone");
    }
}
