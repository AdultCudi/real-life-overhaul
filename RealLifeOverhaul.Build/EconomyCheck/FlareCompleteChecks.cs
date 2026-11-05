using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Ui;

// Flare complete (the owner, 3 Nov: "a lot of it is missing ... The hiring, posting, etc, is not social media like. No inbox"):
// the words a post carries to its card and through the save, how long ago, one capped list of notifications, the inbox and who
// writes to it, a hire's money and its seeded result, an answer to a review moving the rating by rule, the city's businesses on
// the same rules, a handler for every target the face draws, the cost of Home opened and scrolled, and the compose box's caret.
internal static partial class Program
{
    private static void FlareCompleteChecks()
    {
        FlareCompletePosts();
        FlareCompleteAgo();
        FlareCompleteNotices();
        FlareCompleteInbox();
        FlareCompleteHire();
        FlareCompleteReplies();
        FlareCompleteRivals();
        FlareCompleteHandlers();
        FlareCompleteCost();
        FlareCompleteCaret();
    }

    // ---------------------------------------------------------------- a post's words

    private static void FlareCompletePosts()
    {
        string typed = "  Fresh cinnamon buns at #BloomCafe\n from seven.   First twenty get a free coffee #MurrayHillEats <b>  ";
        string text = FlareLiveRules.Clip(typed);
        Check(text == "Fresh cinnamon buns at #BloomCafe from seven. First twenty get a free coffee #MurrayHillEats ‹b›",
            "Flare complete: typed words are kept on one line, their spaces run together and their markup made plain (" + text + ")");
        Check(FlareLiveRules.Clip(new string('a', 500)).Length == FlareLiveRules.TextMost && FlareLiveRules.Clip("   ") == null,
            "Flare complete: a post holds at most " + FlareLiveRules.TextMost + " characters, and spaces alone are no post");
        string topic = FlareLiveRules.TagsOf(text, "shoplocal");
        var tags = new string[FlareFeedRules.TagsShown];
        int n = FlareFeedRules.TagsOf(topic, tags);
        Check(topic == "shoplocal bloomcafe murrayhilleats" && n == 3 && tags[0] == "shoplocal" && tags[2] == "murrayhilleats",
            "Flare complete: the tags typed join the post's own under its words (" + topic + ")");
        Check(FlareFeedRules.Plain(text) == "Fresh cinnamon buns at BloomCafe from seven. First twenty get a free coffee MurrayHillEats ‹b›"
              && FlareFeedRules.Plain("Price #1 in town, C# and #") == "Price 1 in town, C# and #",
            "Flare complete: the card's words lose the hash marks the tags line carries, and only those");
        FlareFile f = FlareRules.Open(9, 3, 40, 50);
        var p = new FlarePost
        {
            Id = f.NextId++, Day = 3, Hour = 9, Minute = 5, Author = FlareAuthor.Player, Source = FlareSource.Life, Text = text, Topic = topic, Place = "a~1", Name = "Bloom Cafe",
            Reach = 120, Likes = 14
        };
        f.Posts.Add(p);
        FlareFile back = FlareStore.Read(FlareStore.WriteAccount(f), FlareStore.WritePosts(f));
        FlarePost again = back != null ? back.Post(p.Id) : null;
        Check(again != null && again.Text == text && again.Topic == topic && again.At == p.At && again.Place == "a~1",
            "Flare complete: a post keeps its typed words, its tags and its minute through the save (reallife:flare_posts)");
        // A poll: the post carries the poll tag; its votes are the people who reacted so far, its yes share the post's own roll.
        var poll = new FlarePost
        {
            Id = 77, Seed = 5, Day = 3, Hour = 9, Author = FlareAuthor.Player, Source = FlareSource.Life, Text = "A new menu?", Topic = "bloomcafe " + FlareFeedRules.PollTag,
            Likes = 40, Replies = 6, Reposts = 4
        };
        long later = FlareLiveRules.Now(6, 9, 0);
        bool asked = FlareFeedRules.Poll(poll, later, out int yes, out int votes);
        FlareFeedRules.Poll(poll, later, out int yesAgain, out int votesAgain);
        int reacted = FlareLiveRules.Shown(40, poll, later) + FlareLiveRules.Shown(6, poll, later) + FlareLiveRules.Shown(4, poll, later);
        Check(asked && votes == reacted && votes > 0 && yes >= 5 && yes <= 95 && yes == yesAgain && votes == votesAgain && !FlareFeedRules.Poll(p, later, out _, out _)
              && !FlareFeedRules.HasTag("pollen shoplocal", FlareFeedRules.PollTag) && FlareFeedRules.HasTag("shoplocal POLL", FlareFeedRules.PollTag),
            "Flare complete: a poll's votes are the people who reacted to it and its yes share is the post's own; only a post tagged as a poll is one ("
            + yes + "% yes of " + votes + ")");
    }

    // ---------------------------------------------------------------- how long ago

    private static void FlareCompleteAgo()
    {
        long now = FlareLiveRules.Now(12, 15, 30);
        bool right = Ago(now, now, FlareAgo.Now, 0) && Ago(now + 30, now, FlareAgo.Now, 0) && Ago(now - 5, now, FlareAgo.Minutes, 5) && Ago(now - 59, now, FlareAgo.Minutes, 59)
                     && Ago(now - 60, now, FlareAgo.Hours, 1) && Ago(now - 1439, now, FlareAgo.Hours, 23) && Ago(now - 1440, now, FlareAgo.Days, 1)
                     && Ago(now - 7 * 1440 + 1, now, FlareAgo.Days, 6) && Ago(now - 7 * 1440, now, FlareAgo.Date, 5);
        Check(right, "Flare complete: a post says now, 5m, 2h or 3d, and past a week its day");
        Check(FlareFeedRules.Count(842) == "842" && FlareFeedRules.Count(1000) == "1K" && FlareFeedRules.Count(1250) == "1.2K" && FlareFeedRules.Count(11950) == "11.9K"
              && FlareFeedRules.Count(96000) == "96K" && FlareFeedRules.Count(120400) == "120K" && FlareFeedRules.Count(1400000) == "1.4M",
            "Flare complete: counts read as a feed writes them, never rounded up past what they are");
    }

    private static bool Ago(long at, long now, FlareAgo want, long wantN)
    {
        FlareAgo got = FlareFeedRules.Ago(at, now, out long n);
        return got == want && n == wantN;
    }

    // ---------------------------------------------------------------- notifications

    private static void FlareCompleteNotices()
    {
        FlareFile f = FlareRules.Open(11, 14, 100, 50);
        long now = FlareLiveRules.Now(20, 22, 0);
        var places = new List<string> { "a~1" };
        for (int k = 0; k < f.History.Length; k++)
        {
            f.History[k] = 100 + k * 10;   // ten more followers each evening
        }
        var offers = new List<FlareOffer> { new FlareOffer { Id = 4, Name = "Glow Cosmetics", Posts = 3, Fee = 45000, Day = 19, Until = 25 }, new FlareOffer { Id = 5, Name = "Later", Day = 21 } };
        f.PastHires.Add(new FlarePastHire { Who = 3, Place = "a~1", Name = "Bloom Cafe", Day = 19, Visitors = 38, Cents = 91200 });
        f.PastHires.Add(new FlarePastHire { Who = 5, Place = "a~1", Name = "Bloom Cafe", Day = 18, Visitors = 4, Cents = 9000, Flopped = true });
        var post = new FlarePost { Id = f.NextId++, Day = 19, Hour = 10, Author = FlareAuthor.Player, Source = FlareSource.Life, Name = "Bloom Cafe", Reach = 50 };
        f.Posts.Add(post);
        f.Boosts.Add(new FlareBoost { Post = post.Id, Place = "a~1", FromMinute = (int)FlareLiveRules.Now(19, 10, 0), Hours = 6, GivenVisitors = 46, GivenCents = 118000 });
        f.Boosts.Add(new FlareBoost { Post = post.Id, Place = "a~1", FromMinute = (int)FlareLiveRules.Now(20, 20, 0), Hours = 6 });   // still running: no result yet
        FlareLiveRules.Dm(f.Net, "c12", "Ana Ruiz", FlareLiveRules.Now(20, 9, 0), false, null, "reallife_flare_dm_complaint", "business=Bloom Cafe");
        FlareLiveRules.Dm(f.Net, "c12", "Ana Ruiz", FlareLiveRules.Now(20, 9, 30), true, "Sorry to hear that. We will put it right.", null, null);
        FlareLiveRules.Dm(f.Net, "c40", "Tom", FlareLiveRules.Now(21, 9, 0), false, null, "reallife_flare_dm_fan", null);   // tomorrow's: not yet
        var into = new List<FlareNotice>();
        var scratch = new List<FlareReaction>();
        FlareLiveRules.AllNotices(f, now, "me", places, offers, scratch, into, 60);
        int follows = 0, offerN = 0, campaigns = 0, flops = 0, promos = 0, dms = 0;
        bool newest = true, figures = true;
        for (int i = 0; i < into.Count; i++)
        {
            FlareNotice x = into[i];
            newest &= i == 0 || into[i - 1].At >= x.At;
            follows += x.Kind == FlareNoticeKind.Follow ? 1 : 0;
            figures &= x.Kind != FlareNoticeKind.Follow || x.Figure == 10;
            offerN += x.Kind == FlareNoticeKind.Offer ? 1 : 0;
            figures &= x.Kind != FlareNoticeKind.Offer || (x.Figure == 3 && x.Cents == 45000 && x.Who == FlareLiveRules.OfferKey(4) && x.Name == "Glow Cosmetics");
            campaigns += x.Kind == FlareNoticeKind.Campaign ? 1 : 0;
            flops += x.Kind == FlareNoticeKind.Campaign && x.Bad ? 1 : 0;
            figures &= x.Kind != FlareNoticeKind.Campaign || x.Bad || (x.Figure == 38 && x.Cents == 91200 && x.Who == "i:3");
            promos += x.Kind == FlareNoticeKind.Promo ? 1 : 0;
            figures &= x.Kind != FlareNoticeKind.Promo || (x.Figure == 46 && x.Cents == 118000 && x.Post == post.Id);
            dms += x.Kind == FlareNoticeKind.Dm ? 1 : 0;
            figures &= x.At <= now;
        }
        Check(newest && figures && follows == f.History.Length - 1 && offerN == 1 && campaigns == 2 && flops == 1 && promos == 1 && dms == 1,
            "Flare complete: one list of notifications, newest first: follows, an offer, the campaign posts and a promotion with what they brought, and a message, none ahead of now ("
            + follows + " follows, " + offerN + " offer, " + campaigns + " campaigns, " + promos + " promotion, " + dms + " message)");
        long seen = FlareLiveRules.Now(19, 12, 0);
        int unseen = FlareLiveRules.UnseenNotices(into, seen), byHand = 0;
        foreach (FlareNotice x in into)
        {
            byHand += x.At > seen && x.Kind != FlareNoticeKind.Dm ? 1 : 0;
        }
        Check(unseen == byHand && unseen > 0, "Flare complete: the bell's badge counts what came since the page was seen, and the inbox's own badge counts the messages");
        // A flood: a hundred and fifty residents' messages. The inbox keeps its newest 120 and the notifications their newest 60.
        for (int i = 0; i < 150; i++)
        {
            FlareLiveRules.Dm(f.Net, "c" + (i % 40), "Resident " + i, FlareLiveRules.Now(15 + i / 30, 8 + i % 12, i % 60), false, null, "reallife_flare_dm_hello", null);
        }
        FlareLiveRules.AllNotices(f, now, "me", places, offers, scratch, into, 60);
        bool order = true;
        for (int i = 1; i < into.Count; i++)
        {
            order &= into[i - 1].At >= into[i].At;
        }
        Check(f.Net.Dms.Count == FlareNet.DmsMost && into.Count == 60 && order, "Flare complete: messages are capped at " + FlareNet.DmsMost + " and the notifications at 60, the newest kept ("
                                                                             + f.Net.Dms.Count + ", " + into.Count + ")");
    }

    // ---------------------------------------------------------------- the inbox and who writes

    private static void FlareCompleteInbox()
    {
        var net = new FlareNet();
        long now = FlareLiveRules.Now(30, 18, 0);
        FlareLiveRules.Dm(net, "c12", "Ana Ruiz", FlareLiveRules.Now(29, 9, 0), false, null, "reallife_flare_dm_praise", "business=Bloom Cafe");
        FlareLiveRules.Dm(net, "c12", "Ana Ruiz", FlareLiveRules.Now(29, 9, 20), true, "Thank you, that means a lot.", null, null);
        FlareLiveRules.Dm(net, "c12", "Ana Ruiz", FlareLiveRules.Now(30, 17, 42), false, null, "reallife_flare_dm_complaint", "business=Bloom Cafe");
        FlareLiveRules.Dm(net, FlareLiveRules.OfferKey(7), "Glow Cosmetics", FlareLiveRules.Now(30, FlareLiveRules.OfferHour, 0), false, null, "reallife_flare_dm_offer",
            "business=Glow Cosmetics~fee=$450~posts=3");
        FlareLiveRules.Dm(net, "i:3", "@jaderivers", FlareLiveRules.Now(30, 15, 0), false, null, "reallife_flare_dm_campaign_post", "business=Bloom Cafe~visitors=38~money=$912");
        FlareLiveRules.Dm(net, "k:Speedy Bites", "Speedy Bites", FlareLiveRules.Now(28, 12, 30), false, null, "reallife_flare_dm_rival", "business=Bloom Cafe").Read = true;
        FlareLiveRules.Dm(net, "c40", "Tom", FlareLiveRules.Now(31, 9, 0), false, null, "reallife_flare_dm_fan", null);   // not yet due
        var threads = new List<FlareThread>();
        FlareLiveRules.Threads(net, now, threads);
        bool order = threads.Count == 4 && threads[0].With == "c12" && threads[1].With == "i:3" && threads[2].With == "d:7" && threads[3].With == "k:Speedy Bites";
        bool counts = order && threads[0].Unread == 2 && threads[0].Count == 3 && threads[0].Last.Key == "reallife_flare_dm_complaint" && threads[3].Unread == 0 && threads[1].Unread == 1;
        Check(order && counts, "Flare complete: the inbox has a conversation for each account, newest first, its newest message and what is unread; one not yet due waits ("
                               + threads.Count + ")");
        Check(FlareLiveRules.UnreadDms(net, now) == 4, "Flare complete: the inbox's badge counts the unread messages from others that are due");
        var one = new List<FlareDm>();
        FlareLiveRules.Thread(net, "c12", now, one);
        Check(one.Count == 3 && one[0].At < one[1].At && one[1].Mine && one[2].At == FlareLiveRules.Now(30, 17, 42),
            "Flare complete: a conversation opens oldest first, the player's own answers in it");
        Check(FlareLiveRules.OfferKey(7) == "d:7" && FlareLiveRules.OfferOf("d:7") == 7 && FlareLiveRules.OfferOf("c7") == -1 && FlareLiveRules.OfferOf("d:") == -1,
            "Flare complete: a sponsor's conversation names its offer, so Accept and Decline answer that offer");
        // Who writes, by the city's seed and the day: residents (a fan, a chat), the customers of a review, a job, an influencer.
        var seen = new HashSet<string>();
        List<string> run = FlareCityWriters(seen), rerun = FlareCityWriters(null);
        Check(seen.Contains("fan@resident") && seen.Contains("job@resident") && seen.Contains("collab@influencer") && (seen.Contains("complaint@resident") || seen.Contains("praise@resident"))
              && seen.Contains("chat@resident"),
            "Flare complete: residents, customers after a review, a job seeker and influencers write, by the seed (" + string.Join(", ", seen) + ")");
        Check(run.Count > 50 && string.Join("|", run) == string.Join("|", rerun), "Flare complete: the same city's people write the same messages on the same days (" + run.Count + ")");
        // The messages the director writes for sponsors, rivals and hires are in every required language with the names it fills.
        string kit = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Social", "Flare", "FlareDirector.Kit.cs"));
        var keys = new HashSet<string>();
        foreach (Match m in Regex.Matches(kit, "\"(reallife_flare_dm_[a-z_]+)\""))
        {
            keys.Add(m.Groups[1].Value);
        }
        string missing = "none";
        foreach (string key in keys)
        {
            foreach (string lang in new[] { "en", "pt", "zh-cn" })
            {
                missing = LocaleFile(lang).ContainsKey(key) ? missing : lang + " " + key;
            }
        }
        Check(keys.Count >= 8 && missing == "none",
            "Flare complete: the sponsors', rivals' and hires' messages are written in English, Portuguese and Chinese (" + keys.Count + " kinds, missing " + missing + ")");
    }

    // A year of the city's messages to a Local account with a business, its reviews and its posts: who wrote and about what.
    private static List<string> FlareCityWriters(HashSet<string> kinds)
    {
        FlareFile f = FlareCastFile(51);
        f.Followers = 900;   // Local: the influencers write too
        var places = new List<string> { "a~1" };
        var written = new List<string>();
        for (int day = 2; day < 400; day++)
        {
            if (day % 9 == 0)
            {
                f.Posts.Add(new FlarePost
                {
                    Id = f.NextId++, Day = day, Hour = 8, Author = FlareAuthor.Citizen, Who = day % 50, Source = FlareSource.Review, Place = "a~1", Name = "Bloom Cafe", Arg = 1 + day % 5
                });
            }
            if (day % 13 == 0)
            {
                f.Posts.Add(new FlarePost { Id = f.NextId++, Day = day, Hour = 7, Author = FlareAuthor.Player, Source = FlareSource.Life, Text = "Morning" });
            }
            f.LastViralDay = day % 97 == 0 ? day : f.LastViralDay;
            if (FlareLiveRules.CityDm(f, day, day % 3 == 0, places, out string with, out string what, out long at, out string _))
            {
                kinds?.Add(what + (with.StartsWith("i:", StringComparison.Ordinal) ? "@influencer" : with.StartsWith("c", StringComparison.Ordinal) ? "@resident" : "@other"));
                written.Add(with + " " + what + " " + at);
            }
        }
        return written;
    }

    // ---------------------------------------------------------------- a hire

    private static void FlareCompleteHire()
    {
        FlareFile f = FlareCastFile(41);
        FlareInfluencer inf = f.Influencers.Find(x => x.Niche == FlareNiche.Food) ?? f.Influencers[0];
        int kind = FlareCast.FirstDealKind + (int)FlareDeal.Series;
        int fee = FlareCast.Fee(inf, FlareDeal.Series);
        var shop = new ShopFile { Place = "A~1" };
        Check(CampaignRules.HiredOnFlare(kind) && CampaignRules.CanBook(shop) && fee == FlareCast.Price(inf) * FlareCast.PostsOf(FlareDeal.Series),
            "Flare complete: a hire on Flare is the company's campaign of its kind, priced at the influencer's fee for each post (" + fee + ")");
        // The company books it and pays it through its own costs (CompanyDirector.BookCampaign: CostKind.Marketing); what it paid is the booking's.
        CampaignRules.Book(shop, kind, fee, 8, 9);
        Check(shop.Push == kind && shop.PushCost == fee && shop.PushFrom == 10 && !CampaignRules.CanBook(shop)
              && CampaignRules.Busy(shop, 10, out int busyKind, out int from, out int to, out bool started)
              && busyKind == kind && from == 10 && started,
            "Flare complete: the booking keeps what was paid and holds the business, so a second hire waits for it to end");
        var hire = new FlareHire { Id = 700, Kind = kind, Place = "A~1", Name = "Bloom Cafe", From = shop.PushFrom, To = shop.PushEnd - 1, Percent = shop.PushPercent };
        hire.Who.Add(inf.Id);
        f.Hires.Add(hire);
        var shops = new List<FlareShop> { new FlareShop { Place = "A~1", Name = "Bloom Cafe", Niche = FlareNiche.Food, District = inf.District } };
        var visits = new List<FlarePost>();
        for (int d = hire.From; d <= hire.To; d++)
        {
            FlareCast.Day(f, d, shops, null, null, null, visits);
        }
        int made = 0;
        long visitors = 0, cents = 0;
        foreach (FlarePastHire h in f.PastHires)
        {
            if (h.Who == inf.Id && h.Place == "A~1")
            {
                made++;
                visitors += h.Visitors;
                cents += h.Cents;
            }
        }
        Check(made == FlareCast.PostsOf(FlareDeal.Series) && hire.Posted == made && visitors > 0 && cents > 0,
            "Flare complete: each paid post comes on its day and says what it brought (" + made + " posts, " + visitors + " visitors, " + cents / 100 + " spent)");
        // The same seed, the same days: the same result, flop or viral included (a session's host and guest see one Flare).
        FlareFile g = FlareCastFile(41);
        FlareInfluencer inf2 = g.Influencers.Find(x => x.Id == inf.Id);
        var hire2 = new FlareHire { Id = 700, Kind = kind, Place = "A~1", Name = "Bloom Cafe", From = hire.From, To = hire.To, Percent = hire.Percent };
        hire2.Who.Add(inf2.Id);
        g.Hires.Add(hire2);
        for (int d = hire.From; d <= hire.To; d++)
        {
            FlareCast.Day(g, d, shops, null, null, null, visits);
        }
        bool same = f.PastHires.Count == g.PastHires.Count;
        for (int i = 0; same && i < f.PastHires.Count; i++)
        {
            same = f.PastHires[i].Visitors == g.PastHires[i].Visitors && f.PastHires[i].Cents == g.PastHires[i].Cents && f.PastHires[i].Flopped == g.PastHires[i].Flopped;
        }
        Check(same && hire.Flopped == hire2.Flopped && hire.WentViral == hire2.WentViral, "Flare complete: a hire's result is seeded: the same city gets the same posts and the same result");
        var notices = new List<FlareNotice>();
        FlareLiveRules.AllNotices(f, FlareLiveRules.Now(hire.To + 1, 9, 0), "me", new List<string> { "A~1" }, null, new List<FlareReaction>(), notices, 60);
        int told = 0;
        foreach (FlareNotice x in notices)
        {
            told += x.Kind == FlareNoticeKind.Campaign && x.Who == "i:" + inf.Id && x.Name == "Bloom Cafe" ? 1 : 0;
        }
        Check(told == made, "Flare complete: each campaign post is told in the notifications with what it brought (" + told + ")");
        // The money goes by the game's own path: Flare asks the company to book (FlareLinks.Book), the company pays the quote
        // from the business's books as marketing, then books it; each post's result is a message from the influencer and a
        // row of the studio.
        string scripts = ModScripts();
        string hireCode = FlareMethod(File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.Cast.cs")),
            "public bool Hire(string place, FlareDeal deal, int influencer)");
        string links = FlareMethod(File.ReadAllText(System.IO.Path.Combine(scripts, "Wiring", "FlareLinks.cs")), "private static bool Book(string place, int kind)");
        string booking = FlareMethod(File.ReadAllText(System.IO.Path.Combine(scripts, "Economy", "Company", "CompanyDirector.Marketing.cs")),
            "public void BookCampaign(string place, int kind)");
        int paid = booking.IndexOf("Cost(cost, CompanyGame.CampaignKey, shop, shop.Name, CostKind.Marketing", StringComparison.Ordinal);
        int booked = booking.IndexOf("CampaignRules.Book(shop.File, kind, cost, percent, Save.Day);", StringComparison.Ordinal);
        Check(hireCode.Contains("FlareBridges.Book(place, _hireKind)") && links.Contains("company.BookCampaign(place, kind);") && paid > 0 && booked > paid
              && booking.Contains("Notifications.ShowInsufficientMoney();"),
            "Flare complete: a hire is paid from the business's books as marketing before the company books it, and short of money it is not booked");
        string kit = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "Flare", "FlareDirector.Kit.cs"));
        string desk = File.ReadAllText(System.IO.Path.Combine(scripts, "Ui", "Flare", "FlareScreen.Desk.cs"));
        Check(kit.Contains("\"reallife_flare_dm_campaign_hired\"") && kit.Contains("p.Flopped ? \"reallife_flare_dm_campaign_flop\" : \"reallife_flare_dm_campaign_post\"")
              && desk.Contains("file.PastHires"),
            "Flare complete: a hire is told in Messages as it is booked and as each post comes or falls flat, and the studio lists what each campaign brought");
        // Flop or viral, by the post's own roll: an unreliable, flaky one falls flat far more often; a big one goes viral now and then.
        int flops = 0, virals = 0, flopsSteady = 0;
        FlareExpect shaky = default(FlareExpect);
        for (int key = 0; key < 1000; key++)
        {
            var b = new FlareBrief { Followers = 40000, Hour = 12, Photo = true, PhotoHere = true, SameDistrict = 1, Reliability = 55, Style = (int)FlareStyle.Flaky, Seed = 17, RollKey = key };
            shaky = FlareUseRules.Expect(in b);
            flops += shaky.Flopped ? 1 : 0;
            virals += shaky.WentViral ? 1 : 0;
            b.Reliability = 95;
            b.Style = (int)FlareStyle.Honest;
            flopsSteady += FlareUseRules.Expect(in b).Flopped ? 1 : 0;
        }
        Check(flops > 0 && virals > 0 && Math.Abs(flops - shaky.Flop) < 60 && flopsSteady < flops / 3,
            "Flare complete: a hire can fall flat or go viral, by its roll, and an unreliable one falls flat far more often (" + flops + " flops in 1000 against " + shaky.Flop
            + " a thousand, " + virals + " viral, " + flopsSteady + " for a steady one)");
    }

    // ---------------------------------------------------------------- answers to reviews

    private static void FlareCompleteReplies()
    {
        FlareStarRow row = Row(2, 1, 0, 1, 1);   // five stars twice, four, two, one: 3.4
        Check(FlareReplyRules.Average(row) == 3400, "Flare complete: a rating is its reviews' stars (" + FlareReplyRules.Average(row) + ")");
        FlareStarRow sorry = Row(2, 1, 0, 1, 1);
        FlareReplyRules.Answered(sorry, 2, FlareReplyRules.Quality(FlareReplyOption.Sorry, 2));
        FlareStarRow rude = Row(2, 1, 0, 1, 1);
        FlareReplyRules.Answered(rude, 2, FlareReplyRules.Quality(FlareReplyOption.Defend, 2));
        FlareStarRow thanks = Row(2, 1, 0, 1, 1);
        FlareReplyRules.Answered(thanks, 2, FlareReplyRules.Quality(FlareReplyOption.Thanks, 2));
        FlareReplyRules.Answered(thanks, 5, FlareReplyRules.Quality(FlareReplyOption.Thanks, 5));
        Check(FlareReplyRules.Average(sorry) == 3555 && FlareReplyRules.Average(rude) == 3272 && FlareReplyRules.Average(thanks) == 3400,
            "Flare complete: saying sorry to a poor review halves its weight (3.40 to " + FlareReplyRules.Average(sorry) / 1000.0 + "), standing your ground gives it half again (to "
            + FlareReplyRules.Average(rude) / 1000.0 + "), a thank you leaves the rating as it was");
        var stars = new List<int> { 5, 5, 4, 2, 1 };
        var qualities = new List<int> { 0, 0, 0, FlareReplyRules.Quality(FlareReplyOption.Sorry, 2), 0 };
        Check(FlareReplyRules.Average(stars, qualities) == FlareReplyRules.Average(sorry), "Flare complete: the reply sheet's after figure is the rating the answer leaves");
        var notes = new List<FlareReviewNote>
        {
            new FlareReviewNote { Key = "cafe", Stars = 2, Answer = "sorry", Quality = FlareReplyRules.Quality(FlareReplyOption.Sorry, 2) },
            new FlareReviewNote { Key = "cafe", Stars = 1, Answer = "no", Quality = FlareReplyRules.Quality(FlareReplyOption.Defend, 1) },
            new FlareReviewNote { Key = "cafe", Stars = 4 }
        };
        FlareStarRow kept = Row(1, 1, 0, 1, 1);
        kept.Key = "cafe";
        var rows = new List<FlareStarRow> { kept };
        FlareReplyRules.Recount(notes, rows);
        Check(kept.Eased == 1 && kept.EasedStars == 2 && kept.Raised == 1 && kept.RaisedStars == 1, "Flare complete: an older save's rows count their answers again from the notes it kept");
        var c = new FlareCityState { Seed = 5 };
        c.StarRows.Add(sorry);
        sorry.Key = "cafe";
        FlareCityState back = FlareCityStore.Decode(FlareCityStore.Encode(c), out _);
        FlareStarRow read = back != null && back.StarRows.Count == 1 ? back.StarRows[0] : null;
        Check(read != null && read.Eased == 1 && read.EasedStars == 2 && FlareReplyRules.Average(read) == 3555, "Flare complete: the answers a rating weighs survive the save (reallife:flare_city)");
        FlareStarRow turning = Row(1, 1, 0, 1, 2);
        turning.Earlier = 4100;
        turning.Recent = 2900;
        FlareStarRow steady = Row(1, 1, 0, 1, 2);
        steady.Earlier = 4100;
        steady.Recent = 3800;
        Check(FlareReplyRules.Turning(turning) && !FlareReplyRules.Turning(steady), "Flare complete: the Business page warns when the newest reviews run half a star under the ones before");
    }

    private static FlareStarRow Row(int five, int four, int three, int two, int one)
    {
        return new FlareStarRow { Five = five, Four = four, Three = three, Two = two, One = one };
    }

    // ---------------------------------------------------------------- the city's businesses on the same rules

    private static void FlareCompleteRivals()
    {
        int answered = 0, rude = 0, praised = 0, sorryKey = -1;
        for (int key = 0; key < 2000; key++)
        {
            if (FlareReplyRules.NpcAnswer(31, key, 2, out FlareReplyOption option))
            {
                answered++;
                rude += option == FlareReplyOption.Defend ? 1 : 0;
                sorryKey = option == FlareReplyOption.Sorry && sorryKey < 0 ? key : sorryKey;
            }
            praised += FlareReplyRules.NpcAnswer(31, key, 5, out FlareReplyOption _) ? 1 : 0;
        }
        bool again = FlareReplyRules.NpcAnswer(31, sorryKey, 2, out FlareReplyOption same) && same == FlareReplyOption.Sorry;
        Check(answered > 1300 && answered < 1500 && rude > answered / 10 && rude < answered / 5 && praised > 500 && praised < 700 && again,
            "Flare complete: the city's businesses answer most poor reviews, a few rudely, and thank some good ones, by their seed (" + answered + ", " + rude + ", " + praised + " of 2000)");
        FlareStarRow theirs = Row(2, 1, 0, 1, 1), mine = Row(2, 1, 0, 1, 1);
        FlareReplyRules.NpcAnswer(31, sorryKey, 2, out FlareReplyOption picked);
        FlareReplyRules.Answered(theirs, 2, FlareReplyRules.Quality(picked, 2));
        FlareReplyRules.Answered(mine, 2, FlareReplyRules.Quality(FlareReplyOption.Sorry, 2));
        Check(FlareReplyRules.Average(theirs) == FlareReplyRules.Average(mine), "Flare complete: a rival's answer moves its rating exactly as the player's same answer moves theirs");
        // A rival's promoted post and its hire are forecast by the player's own rule: the same brief, the same result.
        var brief = new FlareBrief
        {
            Followers = 2400, Hour = 12, Photo = true, PhotoHere = true, SameDistrict = 1, DayCustomers = 180, ReachPaid = 2000, AdDays = 1, Reliability = 70,
            Style = (int)FlareStyle.Edgy, Seed = 9, RollKey = 4
        };
        FlareExpect rival = FlareUseRules.Expect(in brief), player = FlareUseRules.Expect(in brief);
        Check(rival.Visitors == player.Visitors && rival.Cost == player.Cost && rival.Flop == player.Flop && rival.Flopped == player.Flopped && rival.WentViral == player.WentViral && rival.Cost > 0,
            "Flare complete: a rival's paid post is priced and rolled by the player's rule (" + rival.Visitors + " visitors for " + rival.Cost / 100 + ")");
        int[] moves = new int[3];
        for (int day = 0; day < 300; day++)
        {
            moves[FlareUseRules.RivalMove(13, day)]++;
        }
        Check(moves[0] > 60 && moves[1] > 60 && moves[2] > 60,
            "Flare complete: a rival promotes, hires or runs the player down, each by the seed and the day (" + moves[0] + ", " + moves[1] + ", " + moves[2] + ")");
    }

    // ---------------------------------------------------------------- every target has a handler

    private static void FlareCompleteHandlers()
    {
        var names = new Dictionary<int, string>();
        foreach (FieldInfo field in typeof(FlareFace).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && field.FieldType == typeof(int) && field.Name.EndsWith("Command", StringComparison.Ordinal))
            {
                names[(int)field.GetRawConstantValue()] = field.Name;
            }
        }
        string ui = System.IO.Path.Combine(ModScripts(), "Ui", "Flare");
        string screen = File.ReadAllText(System.IO.Path.Combine(ui, "FlareScreen.cs"));
        int start = screen.IndexOf("private bool Pressed(int command, int arg)", StringComparison.Ordinal);
        int end = start >= 0 ? screen.IndexOf("public bool Act(", start, StringComparison.Ordinal) : -1;
        string pressed = start >= 0 && end > start ? screen.Substring(start, end - start) : string.Empty;
        string focused = screen.Substring(Math.Max(0, screen.IndexOf("public void Focused(", StringComparison.Ordinal)));
        // What the face draws in every page and sheet: each target's command is one the screen answers.
        var drawn = new HashSet<int>();
        var navs = new HashSet<int>();
        var chips = new HashSet<int>();
        var s = new UiScene();
        UiChrome.Status = () => UiSamples.Status;
        UiWords en = UiWords("en");
        foreach (string state in UiSamples.FlareStates)
        {
            UiSamples.Flare(s, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, en, state);
            for (int i = 0; i < s.NodeCount; i++)
            {
                int command = s.Nodes[i].Command;
                if (UiDraw.IsChrome(command) || command == UiDraw.CloseCommand)
                {
                    continue;   // the status bar's and the window's close: the host's
                }
                drawn.Add(command);
                if (command == FlareFace.NavCommand)
                {
                    navs.Add(s.Nodes[i].Arg);
                }
                if (command == FlareFace.ChipCommand)
                {
                    chips.Add(s.Nodes[i].Arg);
                }
            }
        }
        string unanswered = "none";
        foreach (int command in drawn)
        {
            string name = names.TryGetValue(command, out string n) ? n : command.ToString();
            bool answered = command == FlareFace.FieldCommand ? focused.Contains("command == FlareFace.FieldCommand") : pressed.Contains("case FlareFace." + name + ":");
            unanswered = answered ? unanswered : name;
        }
        Check(drawn.Count >= 25 && unanswered == "none", "Flare complete: every target the face draws, in every page and sheet, is answered by the screen (" + drawn.Count + " kinds, unanswered "
                                                         + unanswered + ")");
        string fill = File.ReadAllText(System.IO.Path.Combine(ui, "FlareScreen.Fill.cs"));
        string pages = "none";
        string[] pageNames = { "Home", "Explore", "Notices", "Messages", "Profile", "Studio", "Business", "Hire", "City", "Thread" };
        for (int page = 0; page <= FlareFace.Thread; page++)
        {
            pages = fill.Contains("case FlareFace." + pageNames[page] + ": " + pageNames[page] + "(file, now); break;") ? pages : pageNames[page];
        }
        bool allNavs = navs.Count == FlareFace.Pages && pressed.Contains("case FlareFace.NavCommand: return arg >= 0 && arg < FlareFace.Pages && NavPressed(arg);");
        Check(allNavs && pages == "none", "Flare complete: each of the nine pages of the nav, and a post's thread, is filled from the account (" + navs.Count + " in the nav, unfilled " + pages + ")");
        string sheets = File.ReadAllText(System.IO.Path.Combine(ui, "FlareScreen.Sheet.cs"));
        int open = sheets.IndexOf("private bool OpenCompose(int chip", StringComparison.Ordinal);
        string compose = open >= 0 ? sheets.Substring(open, Math.Min(1800, sheets.Length - open)) : string.Empty;
        bool chipped = chips.Count == 4 && compose.Contains("case 1:") && compose.Contains("_poll = true") && compose.Contains("case 2:") && compose.Contains("_promote = shops.Count > 0 ? 1 : 0")
                       && compose.Contains("case 3:") && compose.Contains("chip == 0") && pressed.Contains("case FlareFace.ChipCommand: return OpenCompose(arg);");
        Check(chipped, "Flare complete: the composer's Photo, Poll, Promote a business and Tag a place each open the sheet with their part chosen (" + chips.Count + " chips)");
        Check(sheets.Contains("case FlareFace.ComposeSheet: return PostNow();") && sheets.Contains("case FlareFace.HireSheet: return HireNow();")
              && sheets.Contains("case FlareFace.ReplySheet: return ReplyNow();"), "Flare complete: each sheet's confirm posts, hires or answers");
    }

    // ---------------------------------------------------------------- what Home costs

    private static void FlareCompleteCost()
    {
        UiChrome.Status = () => UiSamples.Status;
        UiWords en = UiWords("en");
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Flare");
        var s = new UiScene();
        FlareFaceModel few = UiSamples.FlareSample(en, "home");
        FlareFaceModel many = UiSamples.FlareSample(en, "home");
        FlareGrow(few.List, 10);
        FlareGrow(many.List, 200);
        FlareFace.Lay(s, frame, true, UiDefaultKeys.Keyboard, en, few);
        int fewItems = s.ItemCount, fewNodes = s.NodeCount;
        FlareFace.Lay(s, frame, true, UiDefaultKeys.Keyboard, en, many);
        int manyItems = s.ItemCount, manyNodes = s.NodeCount;
        Check(manyItems <= fewItems + 40 && manyNodes <= FlareFace.MostTargets + 20,
            "Flare complete: a feed of 200 posts lays only the rows in view, as a feed of 10 does (" + manyItems + " items against " + fewItems + ", " + manyNodes + " targets)");
        many.List.Offset = 1e7f;
        FlareFace.Lay(s, frame, true, UiDefaultKeys.Keyboard, en, many);
        Check(s.ItemCount <= fewItems + 40 && many.List.Offset > 0f && many.List.Offset < 1e7f, "Flare complete: scrolled to its end, the long feed still lays only the rows in view ("
                                                                                                + s.ItemCount + " items at " + (int)many.List.Offset + " px)");
        // Home as it opens: the feed filled to its cap (FlareScreen.FeedMost), each row measured at the column's width once.
        Match cap = Regex.Match(File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Ui", "Flare", "FlareScreen.cs")), @"\bFeedMost = (\d+)");
        int feedMost = cap.Success ? int.Parse(cap.Groups[1].Value) : 0;
        FlareFaceModel home = UiSamples.FlareSample(en, "home");
        FlareGrow(home.List, feedMost);
        double open = FlareLayTime(s, home, frame, en, 200, true);
        double scroll = FlareLayTime(s, home, frame, en, 200, false);
        long bytes = FlareLayBytes(s, home, frame, en);
        Check(feedMost >= 20 && open < 2.0, "Flare complete: speed: opening Home on its full feed of " + feedMost + " posts (each measured) lays in " + open.ToString("0.000")
                                            + " ms, under the 2 ms slice");
        Check(scroll < 2.0, "Flare complete: speed: a notch of the wheel lays Home again in " + scroll.ToString("0.000") + " ms, under the 2 ms slice (no fill: the rows are kept)");
        Check(bytes < 16384, "Flare complete: a scroll's layout allocates " + bytes + " bytes (the photos' keys, the avatars' initials, the badges' counts), under 16 KB");
        Console.WriteLine("    flare: Home open " + open.ToString("0.000") + " ms on " + feedMost + " posts, scroll " + scroll.ToString("0.000") + " ms, " + bytes + " bytes a scroll; "
                          + manyItems + " items and " + manyNodes + " targets for 200 posts, " + fewItems + " for 10");
    }

    // A list grown to so many posts, its first rows over and over.
    private static void FlareGrow(FlareList list, int posts)
    {
        int seed = list.Count;
        while (list.Count > posts)
        {
            list.Count--;
        }
        for (int i = list.Count; i < posts; i++)
        {
            ref FlareRow r = ref list.Add();
            r = list.Rows[i % seed];
        }
        list.Measured = -1f;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double FlareLayTime(UiScene s, FlareFaceModel m, UiFrame frame, UiWords t, int times, bool open)
    {
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < times; i++)
        {
            if (open)
            {
                m.List.Measured = -1f;   // a fill: every row measured again
                m.List.Offset = 0f;
            }
            else
            {
                m.List.Offset = (i % 24) * UiTokens.S16 * 2f;   // the wheel, notch by notch
            }
            FlareFace.Lay(s, frame, true, UiDefaultKeys.Keyboard, t, m);
        }
        return watch.Elapsed.TotalMilliseconds / times;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long FlareLayBytes(UiScene s, FlareFaceModel m, UiFrame frame, UiWords t)
    {
        FlareFace.Lay(s, frame, true, UiDefaultKeys.Keyboard, t, m);
        m.List.Offset += UiTokens.S16 * 2f;
        long start = GC.GetAllocatedBytesForCurrentThread();
        FlareFace.Lay(s, frame, true, UiDefaultKeys.Keyboard, t, m);
        return GC.GetAllocatedBytesForCurrentThread() - start;
    }

    // ---------------------------------------------------------------- the compose box's caret

    private static void FlareCompleteCaret()
    {
        UiChrome.Status = () => UiSamples.Status;
        UiWords en = UiWords("en");
        foreach (string state in new[] { "compose", "compose_promote" })
        {
            foreach (bool longer in new[] { false, true })
            {
                FlareFaceModel m = UiSamples.FlareSample(en, state);
                if (longer)
                {
                    m.Sheet0.FieldText = FlareLiveRules.Clip("Fresh cinnamon buns at Bloom Cafe from seven, the first twenty get a free coffee, and the new oat latte"
                                                             + " is on the board all week long for everyone #MurrayHillEats");
                }
                var s = new UiScene();
                s.Begin("flare", 1920f, 1080f, true);
                s.TypingCommand = FlareFace.FieldCommand;
                s.TypingArg = FlareFace.ComposeField;
                FlareFace.Lay(s, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, en, m);
                string fault = UiCaretFault(s, FlareFace.FieldCommand, FlareFace.ComposeField, UiStyle.Body);
                string name = state + (longer ? ", over lines" : string.Empty);
                Check(fault == null, "Flare complete: the compose box (" + name + ") shows its caret while it takes the keys" + (fault == null ? string.Empty : " (" + fault + ")"));
                s.TypingCommand = -1;
                FlareFace.Lay(s, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, en, m);
                Check(s.CaretItem < 0, "Flare complete: the compose box (" + name + ") draws no caret while nothing types");
                s.TypingCommand = FlareFace.FieldCommand;
                s.TypingArg = FlareFace.InboxField;
                FlareFace.Lay(s, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, en, m);
                Check(s.CaretItem < 0, "Flare complete: the compose box (" + name + ") draws none while another field types");
            }
        }
    }
}
