using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;

// The Flare gaps the logic audit found (w3a.md): ending a hire, ads, friend moments and the report, review and announce posts, the notifications' tabs,
// Mark all read and what needs an answer, what a post brought, a tier passed on the day's round, the city's donut by district, the week, rents and new
// shops, the fit and niche of the cast, the audience bars, the profile's words and picture, the trend card's Promote. Rules run game-free; every press
// goes through FlareKitPress against a host that records which call arrived and fails on one of another kind (w4-master 1.3 item 13).
internal static partial class Program
{
    private static void FlareAuditChecks()
    {
        FlareAuditCommands();
        FlareAuditNotices();
        FlareAuditTiers();
        FlareAuditHire();
        FlareAuditCity();
        FlareAuditProfile();
        FlareAuditPresses();
        FlareAuditWiring();
        FlareRoundChecks();
    }

    // A host that records which call came; every call has its own count, so a press that arrives as another kind shows.
    private sealed class FlareAuditHost : IFlareKitHost
    {
        public int Ended = -1, EndCalls, Seen, Bought, Saved, Opened = -1, OpenCalls;
        public string Place, Bio, Banner;
        public int Target = -1, Size = -1, Days = -1;
        public bool FailBuy;

        public bool OpenHire(int influencer)
        {
            OpenCalls++;
            Opened = influencer;
            return influencer < 100;   // an influencer the director does not know cannot be hired
        }

        public bool EndHire(int id)
        {
            EndCalls++;
            Ended = id;
            return true;
        }

        public bool BuyAd(string place, int target, int size, int days, out string why)
        {
            why = null;
            if (FailBuy)
            {
                why = "reallife_flare_ad_why_money";
                return false;
            }
            Bought++;
            Place = place;
            Target = target;
            Size = size;
            Days = days;
            return true;
        }

        public bool SaveProfile(string bio, string banner)
        {
            Saved++;
            Bio = bio;
            Banner = banner;
            return true;
        }

        public void MarkNoticesSeen() => Seen++;
    }

    // ---------------------------------------------------------------- the commands

    private static void FlareAuditCommands()
    {
        var all = new HashSet<int>(FlareFace.Commands);
        int[] mine = { FlareFace.MarkAllCommand, FlareFace.SortCommand, FlareFace.NicheCommand, FlareFace.PromoteCommand, FlareFace.EditCommand };
        bool inBlock = true, unique = all.Count == FlareFace.Commands.Length;
        foreach (int c in mine)
        {
            inBlock &= c >= 340 && c < 360;
        }
        bool clash = false;
        int[] others = { UiAppWindow.NavCommand, UiDecision.ChoiceCommand, UiDashboard.TabCommand, UiInfoPanel.TabCommand };
        foreach (int c in all)
        {
            foreach (int o in others)
            {
                clash |= c == o;
            }
        }
        Check(inBlock && unique && !clash, "flare audit: the new commands are a block of their own (340 to 344), none is the window's, a decision's, a dashboard's or an info panel's number");
        // The audit's presses that change the kit's own state are answered by it and by nothing else.
        var host = new FlareAuditHost();
        var kit = new FlareKitPress(host);
        bool others3 = !kit.Press(FlareFace.TabCommand, 0, 7) && !kit.Press(UiAppWindow.NavCommand, 1, 7) && !kit.Press(UiDecision.ChoiceCommand, 1, 7) && !kit.Press(UiDashboard.TabCommand, 1, 7)
                       && !kit.Press(UiInfoPanel.TabCommand, 1, 7) && !kit.Press(FlareFace.ButtonCommand, 1, 7);
        Check(others3 && host.Seen == 0 && host.EndCalls == 0 && host.Bought == 0 && host.Saved == 0,
            "flare audit: a press that is another kind of action (the window's nav, a decision's choice, a tab, a row's button) does nothing in the new part");
    }

    // ---------------------------------------------------------------- the notifications

    private static void FlareAuditNotices()
    {
        // Every kind is under All, and under at most one tab of its own; each tab has kinds, and no two tabs the same.
        var seen = new Dictionary<int, int>();
        bool each = true;
        foreach (FlareNoticeKind k in Enum.GetValues(typeof(FlareNoticeKind)))
        {
            int own = 0;
            for (int tab = 1; tab < FlareNoticeRules.Tabs; tab++)
            {
                own += FlareNoticeRules.InTab(tab, k) ? 1 : 0;
                if (FlareNoticeRules.InTab(tab, k))
                {
                    seen[tab] = seen.TryGetValue(tab, out int n) ? n + 1 : 1;
                }
            }
            each &= FlareNoticeRules.InTab(0, k) && own <= 1;
        }
        Check(each && seen.Count == FlareNoticeRules.Tabs - 1 && FlareNoticeRules.TabOf(FlareNoticeKind.Promo) == FlareNoticeTab.Sales && FlareNoticeRules.TabOf(FlareNoticeKind.Review) == FlareNoticeTab.Reviews
              && FlareNoticeRules.TabOf(FlareNoticeKind.Mention) == FlareNoticeTab.Mentions && FlareNoticeRules.TabOf(FlareNoticeKind.Offer) == FlareNoticeTab.Deals,
            "flare audit: the notifications' tabs split the kinds, every kind under All and under one tab of its own at most");

        FlareFile f = FlareRules.Open(11, 14, 100, 50);
        f.Followers = 620;
        long now = FlareLiveRules.Now(20, 22, 0);
        var post = new FlarePost { Id = f.NextId++, Day = 20, Hour = 9, Author = FlareAuthor.Player, Source = FlareSource.Life, Name = "Bloom Cafe", Place = "a~1", Reach = 400, Gained = 34, Topic = "broadwaynights" };
        f.Posts.Add(post);
        for (int i = 0; i < 3; i++)
        {
            f.Posts.Add(new FlarePost { Id = f.NextId++, Day = 20, Hour = 10 + i, Author = FlareAuthor.Citizen, Who = i, Source = FlareSource.Life, Topic = "broadwaynights", Reach = 50 });
        }
        f.Boosts.Add(new FlareBoost { Post = post.Id, Place = "a~1", FromMinute = (int)FlareLiveRules.Now(20, 9, 0), Hours = 6, GivenVisitors = 46, GivenCents = 118000 });
        f.TierDays[1] = 19;
        var ads = new List<FlareAd>
        {
            new FlareAd { Id = 1, Place = "z~9", Name = "Burger Barn", District = 3, Reach = 9000, Days = 2, From = 20, Mine = false },
            new FlareAd { Id = 2, Place = "a~1", Name = "Bloom Cafe", District = 3, Reach = 2000, Days = 1, From = 20, Mine = true },
            new FlareAd { Id = 3, Place = "q~2", Name = "Old News", District = 1, Reach = 100, Days = 1, From = 5, Mine = false },
            new FlareAd { Id = 4, Place = "r~4", Name = "Tomorrow", District = 1, Reach = 100, Days = 1, From = 21, Mine = false }
        };
        var into = new List<FlareNotice>();
        FlareLiveRules.AllNotices(f, now, "me", new List<string> { "a~1" }, null, new List<FlareReaction>(), into, 60, ads);
        int milestone = 0, gained = 0, trend = 0, rival = 0;
        bool sorted = true, none = true, right = true;
        for (int i = 0; i < into.Count; i++)
        {
            FlareNotice n = into[i];
            sorted &= i == 0 || into[i - 1].At >= n.At;
            none &= n.At <= now;
            milestone += n.Kind == FlareNoticeKind.Milestone ? 1 : 0;
            right &= n.Kind != FlareNoticeKind.Milestone || (n.Figure == 1 && n.At == FlareLiveRules.Now(19, FlareLiveRules.NoticeHour, 0));
            gained += n.Kind == FlareNoticeKind.Gained ? 1 : 0;
            right &= n.Kind != FlareNoticeKind.Gained || (n.Figure == 34 && n.Post == post.Id && n.Name == "Bloom Cafe");
            trend += n.Kind == FlareNoticeKind.TrendPart ? 1 : 0;
            right &= n.Kind != FlareNoticeKind.TrendPart || (n.Name == "broadwaynights" && n.Post == post.Id);
            rival += n.Kind == FlareNoticeKind.Rival ? 1 : 0;
            right &= n.Kind != FlareNoticeKind.Rival || (n.Name == "Burger Barn" && n.Figure == 9000 && n.Extra == 3);
        }
        Check(sorted && none && right && milestone == 1 && gained == 1 && trend == 1 && rival == 1,
            "flare audit: the notifications tell a tier passed, the followers a post brought, a tag trending with the player's post in it and a rival's promoted post (only today's and yesterday's, only another's)");
        bool brought = FlareNoticeRules.Brought(f, post.Id, now, out int visitors, out long cents);
        bool none2 = !FlareNoticeRules.Brought(f, 999, now, out _, out _);
        Check(brought && visitors == 46 && cents == 118000 && none2, "flare audit: a post's notice carries what its boost brought (46 visits, $1,180), and a post with no boost carries nothing");
        // The tab counts of that list: the milestone, the rival and the messages are under All only.
        int mentions = FlareNoticeRules.Count(into, (int)FlareNoticeTab.Mentions);
        Check(mentions >= 2 && FlareNoticeRules.Count(into, 0) == into.Count && FlareNoticeRules.Count(into, (int)FlareNoticeTab.Deals) == 0,
            "flare audit: a tab counts only its own kinds (" + mentions + " under Mentions of " + into.Count + ")");
    }

    // ---------------------------------------------------------------- the tiers on the day's round

    private static void FlareAuditTiers()
    {
        FlareFile f = FlareRules.Open(5, 1, 100, 50);
        f.Followers = 80;
        int[] told = new int[6];
        int stamped = -1;
        // A run of days: the followers grow by themselves and each tier is told once, on the day it is passed, with its day kept.
        for (int day = 2; day <= 90; day++)
        {
            f.Followers += day < 40 ? 15 : day < 70 ? 120 : 900;
            if (FlareNoticeRules.TierUp(f, day, out int tier))
            {
                told[tier]++;
                stamped = day;
            }
        }
        bool days = f.TierDays[1] > 0 && (f.TierDays[2] == -1 || f.TierDays[2] > f.TierDays[1]);
        Check(told[1] == 1 && told[2] <= 1 && f.TierDays[1] >= 2 && f.TierDays[1] <= 40 && f.TierSeen == (int)f.Tier && stamped > 0 && days,
            "flare audit: over 90 days of followers each tier passed is told once on its day and its day is kept (Local on day " + f.TierDays[1] + ")");
        var jump = FlareRules.Open(5, 1, 100, 50);
        jump.Followers = 30000;
        bool up = FlareNoticeRules.TierUp(jump, 10, out int top);
        Check(up && top == (int)FlareTier.Influencer && jump.TierDays[1] == 10 && jump.TierDays[2] == 10 && jump.TierDays[3] == 10 && !FlareNoticeRules.TierUp(jump, 11, out _),
            "flare audit: a jump over tiers keeps the day of each one passed, and asks nothing the next day");
        // The save keeps the days; an older line (without the column) loads with none.
        FlareFile back = FlareStore.Read(FlareStore.WriteAccount(jump), FlareStore.WritePosts(jump));
        bool kept = back != null && back.TierDays[1] == 10 && back.TierDays[3] == 10 && back.TierDays[4] == -1 && back.TierSeen == jump.TierSeen;
        string line = FlareStore.WriteAccount(jump);
        int u = line.IndexOf("\nU|", StringComparison.Ordinal);
        int end = u >= 0 ? line.IndexOf('\n', u + 1) : -1;
        string u0 = u >= 0 ? (end > 0 ? line.Substring(u, end - u) : line.Substring(u)) : null;
        string old = u0 != null ? line.Replace(u0, u0.Substring(0, u0.LastIndexOf('|'))) : line;
        FlareFile older = FlareStore.Read(old, FlareStore.WritePosts(jump));
        Check(kept && older != null && older.TierSeen == jump.TierSeen && older.TierDays[1] == -1 && older.Followers == jump.Followers,
            "flare audit: the days tiers were passed on go through the save, and an account saved before them loads with none");
    }

    // ---------------------------------------------------------------- hiring and a campaign running

    private static void FlareAuditHire()
    {
        FlareFile f = FlareRules.Open(8, 3, 40, 50);
        f.Influencers.Clear();
        f.Influencers.Add(new FlareInfluencer { Id = 1, Handle = "@big", Niche = FlareNiche.Fashion, District = "d1", Followers = 90000, Engagement = 300, Reliability = 70 });
        f.Influencers.Add(new FlareInfluencer { Id = 2, Handle = "@fit", Niche = FlareNiche.Food, District = "d2", Followers = 8000, Engagement = 600, Reliability = 70 });
        f.Influencers.Add(new FlareInfluencer { Id = 3, Handle = "@near", Niche = FlareNiche.Business, District = "d2", Followers = 20000, Engagement = 500, Reliability = 70 });
        var shop = new FlareShop { Place = "a~1", Name = "Bloom Cafe", Niche = FlareNiche.Food, District = "d2" };
        var byFollowers = new List<FlareInfluencer>(f.Influencers);
        FlareDeskRules.Sort(byFollowers, false, shop);
        var byFit = new List<FlareInfluencer>(f.Influencers);
        FlareDeskRules.Sort(byFit, true, shop);
        Check(byFollowers[0].Id == 1 && byFit[0].Id == 2 && byFit[1].Id == 3 && byFit[2].Id == 1 && FlareDeskRules.FitPercent(FlareDeskRules.FitOf(f.Influencers[1], shop)) == 100
              && FlareDeskRules.FitPercent(FlareDeskRules.FitOf(f.Influencers[0], shop)) < 60,
            "flare audit: the cast sorts by fit to the business (food in its district first) or by followers, and the fit reads 100 per cent for a perfect match");
        int food = 0, all = 0;
        foreach (FlareInfluencer inf in f.Influencers)
        {
            all += FlareDeskRules.NicheShown(inf, 0) ? 1 : 0;
            food += FlareDeskRules.NicheShown(inf, (int)FlareNiche.Food + 1) ? 1 : 0;
        }
        Check(all == 3 && food == 1 && FlareDeskRules.NicheCount == Enum.GetValues(typeof(FlareNiche)).Length, "flare audit: the niche chips filter the cast, none shows every one");
        // A campaign running: what its influencers' posts have been worth since it began.
        var h = new FlareHire { Id = 7, Place = "a~1", Name = "Bloom Cafe", From = 10, To = 16 };
        h.Who.Add(2);
        f.PastHires.Add(new FlarePastHire { Who = 2, Place = "a~1", Day = 10, Visitors = 12, Cents = 30000 });
        f.PastHires.Add(new FlarePastHire { Who = 2, Place = "a~1", Day = 11, Visitors = 8, Cents = 20000 });
        f.PastHires.Add(new FlarePastHire { Who = 3, Place = "a~1", Day = 11, Visitors = 99, Cents = 99999 });   // another influencer's
        f.PastHires.Add(new FlarePastHire { Who = 2, Place = "b~2", Day = 11, Visitors = 77, Cents = 77777 });   // another business's
        f.PastHires.Add(new FlarePastHire { Who = 2, Place = "a~1", Day = 9, Visitors = 55, Cents = 55555 });    // before it began
        FlareDeskRules.CampaignSoFar(f, h, 11, out int visits, out long cents);
        Check(visits == 20 && cents == 50000, "flare audit: a campaign running shows the visits and takings its own posts have brought so far (" + visits + ", $" + cents / 100 + ")");
    }

    // ---------------------------------------------------------------- in the city

    private static void FlareAuditCity()
    {
        FlareResidentRig rig = FlareResidentCity(42, 5000, (int)FlareTier.Known);
        var counts = new int[rig.S.Districts.Count];
        int counted = FlareCityRules.FollowersByDistrict(rig.C, rig.S, FlareCityState.PlayerAccount, counts);
        int sum = 0;
        foreach (int n in counts)
        {
            sum += n;
        }
        var order = new int[FlareDeskRules.DistrictsShown];
        int shown = FlareDeskRules.TopDistricts(counts, order, out int rest);
        bool descending = true;
        int top = 0;
        for (int i = 0; i < shown; i++)
        {
            descending &= i == 0 || counts[order[i - 1]] >= counts[order[i]];
            top += counts[order[i]];
        }
        Check(counted > 0 && sum == counted && shown > 0 && shown <= FlareDeskRules.DistrictsShown && descending && top + rest == sum,
            "flare audit: the player's followers in the city are counted by the district they live in and the donut names the biggest (" + counted + " followers, " + shown + " districts, " + rest + " elsewhere)");
        int unknown = FlareCityRules.FollowersByDistrict(rig.C, rig.S, rig.C.AccountOf("nobody"), counts);
        Check(unknown == 0, "flare audit: an account the city has no followers of has no districts");
        // The best-rated shop of the same kind in the same district, never the player's own.
        int mineAt = -1;
        for (int b = 0; b < rig.S.Businesses.Count && mineAt < 0; b++)
        {
            mineAt = rig.S.Businesses.Owner[b] != (byte)RealLifeOverhaul.Population.Owner.Player ? b : -1;
        }
        int rivalAt = -1;
        for (int b = 0; b < rig.S.Businesses.Count && rivalAt < 0; b++)
        {
            if (b != mineAt && rig.S.Businesses.Kind[b] == rig.S.Businesses.Kind[mineAt] && rig.S.Businesses.District[b] == rig.S.Businesses.District[mineAt] && rig.S.Businesses.Key[b] != null
                && rig.S.Businesses.Owner[b] != (byte)RealLifeOverhaul.Population.Owner.Player)
            {
                rivalAt = b;
            }
        }
        if (mineAt >= 0 && rivalAt >= 0)
        {
            rig.C.RatingSum[rivalAt] = 4100;
            rig.C.RatingCount[rivalAt] = 1000;
            Check(FlareCityRules.Competitor(rig.C, rig.S, mineAt) == rivalAt && rig.C.Rating(rivalAt) == 41,
                "flare audit: the competitor line names the shop of the same kind in the same district that the residents rate (4.1)");
        }
        else
        {
            Check(FlareCityRules.Competitor(rig.C, rig.S, -1) == -1, "flare audit: no competitor for a business that is not there");
        }
        // The week's totals count the last seven days of funnels, today included.
        var funnels = new List<FlareFunnel>
        {
            new FlareFunnel { Kind = FunnelKind.Post, Day = 20, Seen = 100, Came = 5, Spent = 5000 },
            new FlareFunnel { Kind = FunnelKind.Ad, Day = 14, Seen = 300, Came = 9, Spent = 12000 },
            new FlareFunnel { Kind = FunnelKind.Post, Day = 13, Seen = 999, Came = 99, Spent = 99900 },
            new FlareFunnel { Kind = FunnelKind.Business, Day = 20, Seen = 999, Came = 99, Spent = 99900 }
        };
        FlareDeskRules.WeekTotals(funnels, 20, out int seen, out int came, out long spent);
        Check(seen == 400 && came == 14 && spent == 17000, "flare audit: the week's totals add the player's posts and ads of the last seven days, not older ones and not a business page's");
        // The city's own ads come out of the day pass (the same one the population's day runs), not out of a hand-built list: each is a business of the city's or
        // a rival's, kept by its key for the day, and the notifications and the shared-reach line read them.
        int dayAds = rig.C.CityAds.Count;
        bool real = dayAds > 0 && dayAds <= FlareCityCatalogue.CityAdsKept;
        foreach (FlareAd ad in rig.C.CityAds)
        {
            int b = rig.S.BusinessOfKey(ad.Place);
            real &= !ad.Mine && b >= 0 && rig.S.Businesses.Owner[b] != (byte)RealLifeOverhaul.Population.Owner.Player && ad.From == rig.S.Day && ad.Reach == FlareCityCatalogue.CityAdReach
                    && ad.District == rig.S.Businesses.District[b];
        }
        var told = new List<FlareNotice>();
        var dayFile = FlareRules.Open(2, rig.S.Day, 30, 50);
        FlareNoticeRules.AddDerived(dayFile, FlareLiveRules.Now(rig.S.Day, 12, 0), rig.C.CityAds, told, new List<string>(), new List<int>());
        int rivalNotices = 0;
        foreach (FlareNotice n in told)
        {
            rivalNotices += n.Kind == FlareNoticeKind.Rival && n.Figure == FlareCityCatalogue.CityAdReach ? 1 : 0;
        }
        FlareAd firstAd = dayAds > 0 ? rig.C.CityAds[0] : null;
        bool shares = firstAd != null && FlareDeskRules.SharedWith(rig.C.CityAds, firstAd.District, rig.S.Day, "not~theirs") != null
                      && FlareDeskRules.SharedWith(rig.C.CityAds, firstAd.District, rig.S.Day + 5, "not~theirs") == null;
        Check(real && rivalNotices == Math.Min(FlareNoticeRules.RivalMost, dayAds) && shares,
            "flare audit: the city's and the rivals' ads of the day pass reach the notifications as rival notices and the shared-reach line (" + dayAds + " ads kept, " + rivalNotices + " notices)");
        // The ad that shares the reach.
        var ads = new List<FlareAd>
        {
            new FlareAd { Place = "z~9", Name = "Burger Barn", District = 2, Days = 2, From = 20, Mine = false }, new FlareAd { Place = "a~1", Name = "Mine", District = 2, Days = 2, From = 20 },
            new FlareAd { Place = "y~8", Name = "Elsewhere", District = 5, Days = 3, From = 20, Mine = false }, new FlareAd { Place = "x~7", Name = "Over", District = 2, Days = 1, From = 10, Mine = false }
        };
        FlareAd shared = FlareDeskRules.SharedWith(ads, 2, 21, "a~1");
        Check(shared != null && shared.Name == "Burger Barn" && FlareDeskRules.SharedWith(ads, 2, 25, "a~1") == null && FlareDeskRules.SharedWith(ads, 4, 21, "a~1") == null,
            "flare audit: an ad running now in the same district shares its reach with another's; one over, or in another district, does not");
        // The creator income paid is saved with the city and an older line loads with none.
        rig.C.CreatorPaid = 123400;
        rig.C.CreatorTotal = 340000;
        FlareCityState back = FlareCityStore.Decode(FlareCityStore.Encode(rig.C), out _);
        Check(back != null && back.CreatorPaid == 123400 && back.CreatorTotal == 340000, "flare audit: the creator income last paid and all paid go through the city's save");
    }

    // ---------------------------------------------------------------- the profile

    private static void FlareAuditProfile()
    {
        string bio = FlareDeskRules.CleanBio("  Coffee first,\n then everything else.   " + new string('x', 200));
        Check(bio != null && bio.Length == FlareDeskRules.BioMost && bio.IndexOf('\n') < 0 && FlareDeskRules.CleanBio("   ") == null,
            "flare audit: the bio is one line of at most " + FlareDeskRules.BioMost + " characters, and spaces alone are none");
        Check(FlareDeskRules.CleanBanner("f:C:/photos/a.jpg") == "f:C:/photos/a.jpg" && FlareDeskRules.CleanBanner("http://x") == null && FlareDeskRules.CleanBanner(null) == null
              && FlareDeskRules.CleanBanner("f:" + new string('a', 400)) == null,
            "flare audit: the banner is a picture of the player's, else the newest picture is used");
        FlareFile f = FlareRules.Open(3, 2, 30, 50);
        f.Bio = FlareDeskRules.CleanBio("Owner of Bloom Cafe.");
        f.Banner = FlareDeskRules.CleanBanner("f:C:/photos/a.jpg");
        FlareFile back = FlareStore.Read(FlareStore.WriteAccount(f), FlareStore.WritePosts(f));
        Check(back != null && back.Bio == "Owner of Bloom Cafe." && back.Banner == "f:C:/photos/a.jpg", "flare audit: the bio and the banner written go through the save and come back");
    }

    // ---------------------------------------------------------------- the presses

    private static void FlareAuditPresses()
    {
        var host = new FlareAuditHost();
        var kit = new FlareKitPress(host);
        // Mark all read: the host's one call.
        bool marked = kit.Press(FlareFace.MarkAllCommand, 0, 7);
        Check(marked && host.Seen == 1 && host.EndCalls == 0 && host.Bought == 0 && host.Saved == 0, "flare audit: Mark all read marks the notifications seen and does nothing else");
        // Tabs.
        bool tabs = true;
        for (int tab = 0; tab < FlareKitPress.NoticeTabs; tab++)
        {
            tabs &= kit.SetNoticeTab(tab) && kit.NoticeTab == tab;
        }
        Check(tabs && !kit.SetNoticeTab(FlareKitPress.NoticeTabs) && !kit.SetNoticeTab(-1) && kit.NoticeTab == FlareKitPress.NoticeTabs - 1, "flare audit: each notifications tab is chosen, and none past the last");
        // Hire order and niche.
        bool hire = kit.Press(FlareFace.SortCommand, 1, 7) && kit.Sort == 1 && kit.Press(FlareFace.NicheCommand, 4, 7) && kit.Niche == 4 && !kit.Press(FlareFace.NicheCommand, 9, 7) && kit.Niche == 4
                    && !kit.Press(FlareFace.SortCommand, 2, 7) && kit.Sort == 1;
        Check(hire, "flare audit: the hire page's order and niche are set by their presses, and a press past what was drawn does nothing");
        // The hire table's row chooses, its button (and the card's) hires: each press arrives as its own kind with the influencer it was drawn for (w4-master 1.3 item 13).
        host.Opened = -1;
        bool chosen = kit.ChooseHire(14) && kit.HireSel == 14 && host.OpenCalls == 0;
        bool opened = kit.HireOpen(14) && host.Opened == 14 && host.OpenCalls == 1 && kit.HireSel == 14;
        bool otherRow = kit.ChooseHire(3) && kit.HireSel == 3 && host.OpenCalls == 1 && kit.HireOpen(3) && host.Opened == 3 && host.OpenCalls == 2;
        Check(chosen && opened && otherRow && !kit.ChooseHire(-1) && !kit.HireOpen(-1) && !kit.HireOpen(500) && host.OpenCalls == 3 && host.Ended == -1 && host.Bought == 0 && host.Saved == 0,
            "flare round: a press on an influencer's row chooses them and does not open the sheet; the Hire button opens the sheet for that influencer and chooses no other");
        string rows = PlayFile("Ui", "Flare", "FlareScreen.Rows.cs");
        Check(rows.Contains("return _kit.ChooseHire(r.Id);") && rows.Contains("case RowAct.Hire: return _kit.HireOpen(r.Id);")
              && PlayFile("Ui", "Flare", "FlareFace.Hire.cs").Contains("ButtonCommand, d.Row,") && PlayFile("Ui", "Flare", "FlareFace.Rows.cs").Contains("targets ? ButtonCommand : -1, index,"),
            "flare round: the screen routes the row's RowCommand to the choice and its ButtonCommand (the row's and the card's, with the chosen row's number) to the hire sheet");
        // End a campaign: asks, then ends that one.
        bool first = kit.End(7) && kit.EndArmed == 7 && host.EndCalls == 0;
        bool other = kit.End(9) && kit.EndArmed == 9 && host.EndCalls == 0;
        bool ends = kit.End(9) && kit.EndArmed == -1 && host.EndCalls == 1 && host.Ended == 9;
        Check(first && other && ends && !kit.End(-1) && host.Saved == 0 && host.Bought == 0, "flare audit: a campaign is ended by a second press on that campaign, and only that one");
        // An ad: the sheet's chips land in the call, the price is the director's.
        kit.AdTargets = 5;
        kit.AdSizes = 3;
        kit.AdDays = 3;
        kit.BeginAd("a~1", 0);
        bool chips = kit.AdChip(0, 3) && kit.AdChip(1, 2) && kit.AdChip(2, 1) && !kit.AdChip(0, 5) && !kit.AdChip(1, 3) && !kit.AdChip(2, 3) && !kit.AdChip(3, 0);
        host.FailBuy = true;
        bool refused = !kit.AdConfirm() && kit.AdWhy == "reallife_flare_ad_why_money" && host.Bought == 0;
        host.FailBuy = false;
        bool bought = kit.AdConfirm() && host.Bought == 1 && host.Place == "a~1" && host.Target == 3 && host.Size == 2 && host.Days == 1 && kit.AdWhy == null;
        Check(chips && refused && bought && host.EndCalls == 1 && host.Saved == 0, "flare audit: the ad sheet runs the ad it shows (target, size, days), keeps the sheet open with the reason when it cannot, and calls nothing else");
        // The profile.
        kit.BeginEdit("Coffee first.", null);
        kit.Bio = "Coffee first, then the rest.";
        kit.Banner = "f:C:/photos/b.jpg";
        bool saved = kit.EditConfirm() && host.Saved == 1 && host.Bio == "Coffee first, then the rest." && host.Banner == "f:C:/photos/b.jpg" && host.Bought == 1;
        Check(saved, "flare audit: Save on the profile sheet writes the words and the picture chosen");
    }

    // ---------------------------------------------------------------- the face reaches them

    private static void FlareAuditWiring()
    {
        string screen = PlayFile("Ui", "Flare", "FlareScreen.cs");
        string audit = PlayFile("Ui", "Flare", "FlareScreen.Audit.cs");
        string rows = PlayFile("Ui", "Flare", "FlareScreen.Rows.cs");
        string fill = PlayFile("Ui", "Flare", "FlareScreen.Fill.cs") + PlayFile("Ui", "Flare", "FlareScreen.Desk.cs");
        string sheet = PlayFile("Ui", "Flare", "FlareScreen.Sheet.cs") + PlayFile("Ui", "Flare", "FlareScreen.Compose.cs");
        string director = PlayFile("Social", "Flare", "FlareDirector.cs") + PlayFile("Social", "Flare", "FlareDirector.Use.cs") + PlayFile("Social", "Flare", "FlareDirector.Pages.cs");
        Check(screen.Contains("case FlareFace.MarkAllCommand:") && screen.Contains("return KitPress(command, arg);") && screen.Contains("case FlareFace.PromoteCommand: return OpenAd(arg);")
              && screen.Contains("case FlareFace.EditCommand: return OpenEdit();") && screen.Contains("return _kit.SetNoticeTab(tab);"),
            "flare audit: the face's new commands are answered by the screen: Mark all, order, niche, Promote, Edit profile and the notifications' tabs");
        Check(rows.Contains("case RowAct.Campaign: return _kit.End(r.Id);") && rows.Contains("case RowAct.Trend: return PromoteTrend(r.Id);") && rows.IndexOf("OpenCompose(-1, r.Key)", StringComparison.Ordinal) < 0
              && audit.Contains("OpenCompose(2)") && audit.Contains("_place = shop;"),
            "flare audit: End on a running campaign and Promote on a trend card act on that campaign and that business, not a blank page");
        Check(fill.Contains("FlareNoticeRules.InTab(_kit.NoticeTab") && fill.Contains("NeedsAnswer(file, now)") && fill.Contains("FlareNoticeRules.Brought(") && fill.Contains("_flare.CompetitorOf(")
              && fill.Contains("_flare.FollowersInDistricts(") && fill.Contains("FlareDeskRules.CampaignSoFar(") && fill.Contains("FlareDeskRules.Sort(") && fill.Contains("_flare.CityMoves()")
              && fill.Contains("_flare.CreatorPaid("),
            "flare audit: the pages read the rules: tabs, what needs an answer, what a post brought, the competitor, the donut, the campaign so far, the cast's order, the city's moves and the creator income");
        Check(sheet.Contains("_flare.StandingIn(") && sheet.Contains("_compose.Kind = FlareSource.Report") && sheet.Contains("_compose.Kind = FlareSource.Review") && sheet.Contains("_compose.Kind = FlareSource.Announce")
              && sheet.Contains("_compose.Draft = d.Id") && sheet.Contains("_compose.ToneChosen = _tone >= 0") && sheet.Contains("_compose.Mention = _mention") && sheet.Contains("_flare.AudienceOf("),
            "flare audit: the compose sheet writes a moment, a report, a review and an announcement with its tone and mention, and the hire sheet its audience");
        Check(director.Contains("FlareNoticeRules.TierUp(") && director.Contains("TierDay(day);") && director.IndexOf("_file.TierSeen = tier;", StringComparison.Ordinal) < 0
              && PlayFile("Social", "Flare", "FlareDirector.Creator.cs").Contains("_city.CreatorTotal += cents;")
              && PlayFile("Social", "Flare", "FlareDirector.Kit.cs").Contains("CityAdsNamed()") && director.Contains("ToLetQueue()") && PlayFile("Wiring", "Wiring.FlareCityLinks.cs").Contains("_toLetScan = new Districts.ToLetScan()"),
            "flare audit: the tier is checked on the day's round (not only in a post), the creator income paid is kept, and the notifications read the city's ads");
        // The new texts: every key the new code asks for is in the three locales.
        string en = PlayFile("..", "Locales", "en.json");
        string missing = "none";
        foreach (string key in new[] { "reallife_flare_kit_mark_all", "reallife_flare_kit_needs_title", "reallife_flare_kit_notice_milestone", "reallife_flare_kit_donut_title", "reallife_flare_kit_edit_profile",
                     "reallife_flare_kit_ad_pay", "reallife_flare_kit_sort_fit", "reallife_flare_kit_end_sure" })
        {
            missing = en.Contains("\"" + key + "\"") && PlayFile("..", "Locales", "pt.json").Contains("\"" + key + "\"") && PlayFile("..", "Locales", "zh-cn.json").Contains("\"" + key + "\"") ? missing : key;
        }
        Check(missing == "none", "flare audit: the new texts are in English, Portuguese and Chinese (" + missing + ")");
    }
}
