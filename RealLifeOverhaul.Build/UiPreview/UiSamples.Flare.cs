using RealLifeOverhaul.Ui;

// Flare on the kit, every page and sheet as the game fills them (FlareScreen.Fill, FlareScreen.Desk and FlareScreen.Sheet): the same keys,
// the same rows, a city of the mockup's (flare_mock): Walter Mack of Bloom Cafe in Murray Hill, Speedy Bites, Pioneer Game studio,
// Thrifty Threads, Jade Rivers. The photos are the mockup's (UiPreviewRunner.Flare's "sample:" pictures).
internal static partial class UiSamples
{
    public static readonly string[] FlareStates =
    {
        "home", "home_following", "home_businesses", "explore", "explore_search", "explore_tag", "notices", "notices_empty", "messages", "messages_offer",
        "messages_none", "profile", "profile_other", "thread", "business", "business_turning", "business_none", "studio", "hire", "city", "city_empty",
        "compose", "compose_promote", "hire_sheet", "reply_sheet", "photo_picker"
    };

    public static void Flare(UiScene s, UiFrame frame, bool dark, IUiKeys keys, UiWords t, string state)
    {
        FlareFace.Lay(s, frame, dark, keys, t, FlareSample(t, state));
    }

    private const string FlareMe = "Walter Mack", FlareMyHandle = "@waltermack", FlareShop = "Bloom Cafe", FlareShop2 = "Bloom Bakery";

    private static readonly string[] FlareNavKeys =
    {
        "reallife_flare_nav_home", "reallife_flare_nav_explore", "reallife_flare_nav_notices", "reallife_flare_nav_messages", "reallife_flare_nav_profile",
        "reallife_flare_nav_studio", "reallife_flare_nav_business", "reallife_flare_nav_hire", "reallife_flare_nav_city"
    };

    private static readonly string[] FlareNavEnglish = { "Home", "Explore", "Notifications", "Messages", "Profile", "Studio", "Business", "Hire", "In the city" };

    public static FlareFaceModel FlareSample(UiWords t, string state)
    {
        var m = new FlareFaceModel { Name = "flare_" + state };
        FlareShell(m, t);
        switch (state)
        {
            case "home": FlareHome(m, t, 0); break;
            case "home_following": FlareHome(m, t, 1); break;
            case "home_businesses": FlareHome(m, t, 2); break;
            case "explore": FlareExplore(m, t, null, null); break;
            case "explore_search": FlareExplore(m, t, "burger", null); break;
            case "explore_tag": FlareExplore(m, t, null, "BroadwayNights"); break;
            case "notices": FlareNotices(m, t, true); break;
            case "notices_empty": FlareNotices(m, t, false); break;
            case "messages": FlareMessages(m, t, "resident"); break;
            case "messages_offer": FlareMessages(m, t, "offer"); break;
            case "messages_none": FlareMessages(m, t, null); break;
            case "profile": FlareProfile(m, t, true); break;
            case "profile_other": FlareProfile(m, t, false); break;
            case "thread": FlareThread(m, t); break;
            case "business": FlareBusiness(m, t, 1); break;
            case "business_turning": FlareBusiness(m, t, 2); break;
            case "business_none": FlareBusiness(m, t, 0); break;
            case "studio": FlareStudio(m, t); break;
            case "hire": FlareHire(m, t); break;
            case "city": FlareCity(m, t, true); break;
            case "city_empty": FlareCity(m, t, false); break;
            case "compose": FlareHome(m, t, 0); FlareCompose(m, t, false); break;
            case "compose_promote": FlareHome(m, t, 0); FlareCompose(m, t, true); break;
            case "hire_sheet": FlareHire(m, t); FlareHireSheet(m, t); break;
            case "reply_sheet": FlareBusiness(m, t, 1); FlareReplySheet(m, t); break;
            case "photo_picker": FlareHome(m, t, 0); FlareCompose(m, t, false); FlarePhotoPicker(m, t); break;
        }
        // The badges of the pages not open: what is new in the notifications and the unread messages.
        m.Badges = new int[FlareFace.Pages];
        m.Badges[FlareFace.Notices] = m.Page == FlareFace.Notices ? 0 : 12;
        m.Badges[FlareFace.Messages] = m.Page == FlareFace.Messages ? 2 : 3;
        return m;
    }

    // ------------------------------------------------------------------------------------------------ the shell and the side

    private static void FlareShell(FlareFaceModel m, UiWords t)
    {
        m.Nav = new string[FlareNavKeys.Length];
        for (int i = 0; i < m.Nav.Length; i++)
        {
            m.Nav[i] = t(FlareNavKeys[i], FlareNavEnglish[i]);
        }
        m.NavIcons = new[] { UiIcon.Home, UiIcon.Search, UiIcon.Bell, UiIcon.Message, UiIcon.Person, UiIcon.ChartLineUp, UiIcon.Store, UiIcon.People, UiIcon.Building };
        m.Post = t("reallife_flare_post", "Post");
        m.Move = t("reallife_ui_prompt_move", "Move");
        m.Select = t("reallife_ui_prompt_select", "Select");
        m.Back = t("reallife_ui_prompt_back", "Back");
        m.MyName = FlareMe;
        m.MyHandle = FlareMyHandle;
        m.MyHue = UiColour.HueOf("me");
        m.SearchHint = t("reallife_flare_kit_search", "Search Flare");
        m.WeekTitle = t("reallife_flare_kit_week", "Your week");
        m.StudioLink = t("reallife_flare_kit_studio_link", "Open Creator studio");
        m.TrendTitle = t("reallife_flare_kit_trending", "Trending in the city");
        m.WhoTitle = t("reallife_flare_kit_who", "Who to follow");
        m.FollowWord = t("reallife_flare_follow", "Follow");
        m.FollowingWord = t("reallife_flare_follow_on", "Following");
        m.WeekLabels = new[] { t("reallife_flare_kit_fig_followers", "Followers"), t("reallife_flare_kit_fig_reach", "Reach"), t("reallife_flare_kit_fig_rating", "Rating") };
        m.WeekFigures = new[] { "2,418", "11.9K", "4.3" };
        m.WeekDeltas = new[]
        {
            t("reallife_flare_kit_week_followers", "{change} this week").Replace("{change}", "+164"), t("reallife_flare_kit_week_reach", "{change}% on last week").Replace("{change}", "+22"),
            t("reallife_flare_kit_reviews_count", "{count} reviews").Replace("{count}", "128")
        };
        m.WeekTones = new[] { 1, 1, 0 };
        m.Line = new[] { 2010f, 2032f, 2025f, 2090f, 2146f, 2131f, 2254f, 2418f };
        FlareTrend(m.Trends, t, "BroadwayNights", "Midtown", 3104);
        FlareTrend(m.Trends, t, "BurgerWeek", "Garment District", 1877);
        FlareTrend(m.Trends, t, "ThriftyFinds", "Murray Hill", 905);
        FlareTrend(m.Trends, t, "MidtownEats", null, 642);
        ref FlareRow a = ref FlareRowOf(m.Who, FlareRowKind.Account, "Thrifty Threads", "k:Thrifty Threads");
        a.Handle = FlareAccountLine(t, "@thriftythreads", t("reallife_flare_kit_kind_business", "Business"));
        ref FlareRow b = ref FlareRowOf(m.Who, FlareRowKind.Account, "Jade Rivers", "i:3");
        b.Handle = FlareAccountLine(t, "@jaderivers", t("reallife_flare_kit_followers_n", "{count} followers").Replace("{count}", "96K"));
        b.Flags = FlareRowFlags.Verified;
    }

    private static void FlareTrend(FlareList list, UiWords t, string tag, string district, int posts)
    {
        ref FlareRow r = ref list.Add();
        r.Kind = FlareRowKind.Tag;
        r.Name = "#" + tag;
        r.Handle = district != null ? t("reallife_flare_kit_trend_where", "Trending in {district}").Replace("{district}", district) : t("reallife_flare_kit_trend_city", "Across the city");
        r.Value = t("reallife_flare_kit_posts_count", "{count} posts").Replace("{count}", posts.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string FlareAccountLine(UiWords t, string handle, string about) =>
        t("reallife_flare_kit_account_line", "{handle}, {about}").Replace("{handle}", handle).Replace("{about}", about);

    // A row of a kind with its name and the hue of its account.
    private static ref FlareRow FlareRowOf(FlareList list, FlareRowKind kind, string name, string key)
    {
        ref FlareRow r = ref list.Add();
        r.Kind = kind;
        r.Name = name;
        r.Hue = UiColour.HueOf(key ?? name ?? string.Empty);
        return ref r;
    }

    private static void FlareSection(FlareList list, string words)
    {
        ref FlareRow r = ref list.Add();
        r.Kind = FlareRowKind.Section;
        r.Name = words;
    }

    // A post's card as PostRow fills it.
    private static ref FlareRow FlarePost(FlareList list, string name, string handle, string key, string time, string text, string likes, string comments, string reposts, string views)
    {
        ref FlareRow r = ref FlareRowOf(list, FlareRowKind.Post, name, key);
        r.Handle = handle;
        r.Time = time;
        r.Text = text;
        r.Likes = likes;
        r.Comments = comments;
        r.Reposts = reposts;
        r.Views = views;
        return ref r;
    }

    // ------------------------------------------------------------------------------------------------ Home

    private static void FlareHome(FlareFaceModel m, UiWords t, int tab)
    {
        m.Page = FlareFace.Home;
        m.Tabs = new[] { t("reallife_flare_tab_foryou", "For you"), t("reallife_flare_tab_following", "Following"), t("reallife_flare_kit_tab_businesses", "Businesses") };
        m.Tab = tab;
        m.ComposerHint = t("reallife_flare_kit_composer", "What's happening, {name}?").Replace("{name}", "Walter");
        m.Chips = new[]
        {
            t("reallife_flare_kit_chip_photo", "Photo"), t("reallife_flare_kit_chip_poll", "Poll"), t("reallife_flare_kit_chip_promote", "Promote a business"),
            t("reallife_flare_kit_chip_place", "Tag a place")
        };
        m.ChipIcons = new[] { UiIcon.Image, UiIcon.ChartPie, UiIcon.Megaphone, UiIcon.MapPin };
        m.List.EmptyIcon = UiIcon.Fire;
        m.List.EmptyTitle = t(tab == 1 ? "reallife_flare_empty_following" : tab == 2 ? "reallife_flare_kit_empty_businesses" : "reallife_flare_empty_feed",
            tab == 1 ? "Nothing yet from the accounts you follow. Follow some from Explore." : tab == 2 ? "No business posts yet" : "Nothing posted yet.");
        m.List.EmptyBody = t("reallife_flare_kit_empty_feed_body", "Follow a few accounts or post something, and your feed fills up.");
        if (tab == 1)
        {
            return;   // following nobody yet: no stories, the empty feed
        }
        m.Stories = new[] { "Pioneer Game studio", "Speedy Bites", FlareShop, "Thrifty Threads", "Midtown Diner", "Alex", "Mom" };
        m.StoryHues = new float[m.Stories.Length];
        m.StoryShops = new bool[m.Stories.Length];
        for (int i = 0; i < m.Stories.Length; i++)
        {
            m.StoryHues[i] = UiColour.HueOf(m.Stories[i]);
        }
        m.StoryShops[2] = true;
        if (tab == 2)
        {
            FlareBusinessPosts(m, t);
            return;
        }
        ref FlareRow p = ref FlarePost(m.List, "Speedy Bites", "@speedybites", "k:Speedy Bites", "2h",
            t("reallife_flare_biz_post_0", "Two for one at {name} today only.").Replace("{name}", "Speedy Bites"), "1.2K", "84", "31", "11.9K");
        p.Flags = FlareRowFlags.Verified | FlareRowFlags.Liked;
        p.Line = t("reallife_flare_kit_promoted", "Promoted in {district}").Replace("{district}", "Midtown");
        p.Icon = UiIcon.Megaphone;
        p.Tags = new[] { "#BroadwayNights", "#MidtownEats" };
        p.Picture = "sample:p1";
        ref FlareRow q = ref FlarePost(m.List, "Pioneer Game studio", "@pioneergames", "k:Pioneer Game studio", "5h",
            t("reallife_flare_announce_post_3", "{name} is hiring. Send a message if you want to work with us.").Replace("{name}", "Pioneer Game studio"), "642", "37", "12", "8.4K");
        q.Picture = "sample:p2";
        ref FlareRow mine = ref FlarePost(m.List, FlareMe, FlareMyHandle, "me", "1d",
            t("reallife_flare_biz_post_2", "Thank you to everyone who came to {name} this week.").Replace("{name}", FlareShop), "212", "18", "9", "3.1K");
        mine.Flags = FlareRowFlags.Mine | FlareRowFlags.Good;
        mine.Line = t("reallife_flare_kit_promoted", "Promoted in {district}").Replace("{district}", "Murray Hill");
        mine.Icon = UiIcon.Megaphone;
        mine.Value = t("reallife_flare_kit_brought", "Brought {visitors} visitors, {money}").Replace("{visitors}", "46").Replace("{money}", "$1,180");
        ref FlareRow inf = ref FlarePost(m.List, "Jade Rivers", "@jaderivers", "i:3", "1d",
            t("reallife_flare_inf_food_1", "Best plate I have had in {district} this month.").Replace("{district}", "Murray Hill"), "4.8K", "230", "96", "96K");
        inf.Flags = FlareRowFlags.Verified;
        inf.Line = t("reallife_flare_kit_partnership", "Paid partnership with {business}").Replace("{business}", FlareShop);
        inf.Icon = UiIcon.Briefcase;
        inf.Tags = new[] { "#MurrayHillEats" };
    }

    private static void FlareBusinessPosts(FlareFaceModel m, UiWords t)
    {
        ref FlareRow a = ref FlarePost(m.List, "Thrifty Threads", "@thriftythreads", "k:Thrifty Threads", "1h",
            t("reallife_flare_biz_post_1", "New in at {name}. Come and see what is on the shelves.").Replace("{name}", "Thrifty Threads"), "318", "22", "14", "4.2K");
        a.Picture = "sample:p3";
        a.Tags = new[] { "#ThriftyFinds" };
        ref FlareRow b = ref FlarePost(m.List, "Midtown Diner", "@midtowndiner", "k:Midtown Diner", "3h",
            t("reallife_flare_biz_post_3", "{name} is open late tonight.").Replace("{name}", "Midtown Diner"), "96", "4", "2", "1.4K");
        b.Line = t("reallife_flare_kit_promoted_city", "Promoted");
        b.Icon = UiIcon.Megaphone;
        ref FlareRow c = ref FlarePost(m.List, FlareShop, "@bloomcafe", "b:Bloom Cafe", "6h", "Should we open on Sundays?", "57", "11", "3", "940");
        c.Flags = FlareRowFlags.Mine;
        c.Value = t("reallife_flare_kit_poll", "Yes {yes}%, no {no}%, {votes} votes").Replace("{yes}", "64").Replace("{no}", "36").Replace("{votes}", "212");
    }

    // ------------------------------------------------------------------------------------------------ Explore

    private static void FlareExplore(FlareFaceModel m, UiWords t, string search, string tag)
    {
        m.Page = FlareFace.Explore;
        m.Search = search ?? string.Empty;
        m.SearchHint = t("reallife_flare_kit_search_hint", "Search accounts and tags");
        m.List.EmptyIcon = UiIcon.Search;
        m.List.EmptyBody = t("reallife_flare_kit_empty_search_body", "Try a name, a handle or a tag.");
        if (tag != null)
        {
            m.Head = "#" + tag;
            m.HeadLine = t("reallife_flare_kit_posts_count", "{count} posts").Replace("{count}", "3,104");
            m.List.EmptyTitle = t("reallife_flare_empty_tag", "No posts with this tag in the last few days.");
            ref FlareRow p = ref FlarePost(m.List, "Speedy Bites", "@speedybites", "k:Speedy Bites", "2h",
                t("reallife_flare_biz_post_0", "Two for one at {name} today only.").Replace("{name}", "Speedy Bites"), "1.2K", "84", "31", "11.9K");
            p.Flags = FlareRowFlags.Verified;
            p.Tags = new[] { "#BroadwayNights", "#MidtownEats" };
            p.Picture = "sample:p1";
            ref FlareRow q = ref FlarePost(m.List, "Ana Ruiz", "@anaruiz", "c12", "4h",
                t("reallife_flare_inf_food_2", "Coffee crawl through {district}. My feet hurt and I regret nothing.").Replace("{district}", "Midtown"), "41", "3", "1", "610");
            q.Tags = new[] { "#BroadwayNights" };
            return;
        }
        if (search != null)
        {
            m.List.EmptyTitle = t("reallife_flare_kit_empty_search", "Nothing matches");
            FlareSection(m.List, t("reallife_flare_tab_accounts", "Accounts"));
            FlareAccount(m.List, t, "Speedy Bites", "@speedybites", "k:Speedy Bites", t("reallife_flare_kit_kind_business", "Business"), true, true);
            FlareSection(m.List, t("reallife_flare_kit_section_tags", "Tags"));
            ref FlareRow tagRow = ref m.List.Add();
            tagRow.Kind = FlareRowKind.Tag;
            tagRow.Name = "#BurgerWeek";
            tagRow.Handle = t("reallife_flare_kit_trend_where", "Trending in {district}").Replace("{district}", "Garment District");
            tagRow.Value = t("reallife_flare_kit_posts_count", "{count} posts").Replace("{count}", "1,877");
            FlareSection(m.List, t("reallife_flare_tab_posts", "Posts"));
            ref FlareRow p = ref FlarePost(m.List, "Burger Barn", "@burgerbarn", "k:Burger Barn", "7h",
                t("reallife_flare_biz_post_0", "Two for one at {name} today only.").Replace("{name}", "Burger Barn"), "203", "19", "6", "2.7K");
            p.Tags = new[] { "#BurgerWeek" };
            return;
        }
        m.List.EmptyTitle = t("reallife_flare_kit_empty_explore", "Nothing trending yet");
        FlareSection(m.List, t("reallife_flare_city_trend_tags", "Trending tags"));
        for (int i = 0; i < m.Trends.Count; i++)
        {
            ref FlareRow r = ref m.List.Add();
            r = m.Trends.Rows[i];
            r.Height = 0f;
        }
        FlareSection(m.List, t("reallife_flare_kit_section_accounts", "Accounts to follow"));
        FlareAccount(m.List, t, "Jade Rivers", "@jaderivers", "i:3", t("reallife_flare_kit_followers_n", "{count} followers").Replace("{count}", "96K"), true, false);
        FlareAccount(m.List, t, "Thrifty Threads", "@thriftythreads", "k:Thrifty Threads", t("reallife_flare_kit_kind_business", "Business"), false, false);
        FlareAccount(m.List, t, "Marco Bell", "@marcobell", "i:5", t("reallife_flare_kit_followers_n", "{count} followers").Replace("{count}", "18.2K"), false, true);
    }

    private static void FlareAccount(FlareList list, UiWords t, string name, string handle, string key, string about, bool verified, bool following)
    {
        ref FlareRow r = ref FlareRowOf(list, FlareRowKind.Account, name, key);
        r.Handle = FlareAccountLine(t, handle, about);
        r.Flags = (verified ? FlareRowFlags.Verified : FlareRowFlags.None) | (following ? FlareRowFlags.Following : FlareRowFlags.None);
        r.Button = following ? t("reallife_flare_follow_on", "Following") : t("reallife_flare_follow", "Follow");
    }

    // ------------------------------------------------------------------------------------------------ Notifications

    private static void FlareNotices(FlareFaceModel m, UiWords t, bool some)
    {
        m.Page = FlareFace.Notices;
        m.Head = t("reallife_flare_nav_notices", "Notifications");
        m.List.EmptyIcon = UiIcon.Bell;
        m.List.EmptyTitle = t("reallife_flare_empty_notices", "Nothing new yet. Post something and the city will answer.");
        m.List.EmptyBody = t("reallife_flare_kit_empty_notices_body", "Likes, follows, replies, reviews and offers show here.");
        if (!some)
        {
            m.HeadLine = t("reallife_flare_kit_all_seen", "All caught up");
            return;
        }
        m.HeadLine = t("reallife_flare_kit_new", "{count} new").Replace("{count}", "4");
        string thanks = t("reallife_flare_biz_post_2", "Thank you to everyone who came to {name} this week.").Replace("{name}", FlareShop);
        FlareNotice(m.List, UiIcon.Coin, UiRole.Gold, true, "12m",
            t("reallife_flare_kit_notice_offer", "{name} offers {money} a post for {posts} posts").Replace("{name}", "Glow Cosmetics").Replace("{money}", "$450").Replace("{posts}", "3"), null);
        FlareNotice(m.List, UiIcon.Megaphone, UiRole.Chart2, true, "40m",
            t("reallife_flare_kit_notice_campaign", "{name} posted for {business}: {visitors} visitors, {money}").Replace("{name}", "Jade Rivers").Replace("{business}", FlareShop)
                .Replace("{visitors}", "38").Replace("{money}", "$912"), null);
        FlareNotice(m.List, UiIcon.Heart, UiRole.CatRetail, true, "1h", t("reallife_flare_notice_like", "{name} liked your post").Replace("{name}", "Alex"), thanks);
        FlareNotice(m.List, UiIcon.People, UiRole.AccentText, true, "2h", t("reallife_flare_kit_notice_follow", "{count} new followers").Replace("{count}", "23"), null);
        FlareNotice(m.List, UiIcon.Message, UiRole.AccentText, false, "3h", t("reallife_flare_notice_reply", "{name} replied to your post").Replace("{name}", "Mom"),
            t("reallife_flare_cit_reply_kind_1", "Hope you are doing all right."));
        FlareNotice(m.List, UiIcon.Star, UiRole.Gold, false, "5h", t("reallife_flare_notice_review", "{name} reviewed your business").Replace("{name}", "Ana Ruiz"), null);
        FlareNotice(m.List, UiIcon.ChartLineUp, UiRole.OkText, false, "1d",
            t("reallife_flare_kit_notice_promo", "Your post about {business} brought {visitors} visitors, {money}").Replace("{business}", FlareShop).Replace("{visitors}", "46")
                .Replace("{money}", "$1,180"), null);
        FlareNotice(m.List, UiIcon.ChartLineUp, UiRole.DangerText, false, "2d",
            t("reallife_flare_kit_notice_promo_flop", "Your post about {business} fell flat").Replace("{business}", FlareShop2), null);
    }

    private static void FlareNotice(FlareList list, UiIcon icon, UiRole tint, bool unread, string time, string text, string line)
    {
        ref FlareRow r = ref list.Add();
        r.Kind = FlareRowKind.Notice;
        r.Icon = icon;
        r.Tint = tint;
        r.Flags = unread ? FlareRowFlags.Unread : FlareRowFlags.None;
        r.Time = time;
        r.Text = text;
        r.Line = line;
    }

    // ------------------------------------------------------------------------------------------------ Messages

    private static void FlareMessages(FlareFaceModel m, UiWords t, string open)
    {
        m.Page = FlareFace.Messages;
        m.Head = t("reallife_flare_nav_messages", "Messages");
        m.HeadLine = t("reallife_flare_kit_unread", "{count} unread").Replace("{count}", "2");
        m.Search = string.Empty;
        m.InboxHint = t("reallife_flare_kit_inbox_hint", "Search messages");
        m.List.EmptyIcon = UiIcon.Message;
        m.List.EmptyTitle = t("reallife_flare_empty_messages", "No messages yet.");
        m.List.EmptyBody = t("reallife_flare_kit_empty_messages_body", "Residents, businesses and influencers write to you here.");
        m.Detail.EmptyTitle = t("reallife_flare_kit_pick_thread", "Pick a conversation");
        m.Detail.EmptyBody = t("reallife_flare_kit_pick_thread_body", "Your messages open here.");
        string complaint = t("reallife_flare_dm_complaint", "I left a poor review of {business}. The visit really let me down. Will anything change?").Replace("{business}", FlareShop);
        string offer = t("reallife_flare_dm_offer", "Hello from {business}. We would pay {fee} a post for {posts} posts about us. Interested?")
            .Replace("{business}", "Glow Cosmetics").Replace("{fee}", "$450").Replace("{posts}", "3");
        string you = t("reallife_flare_dm_you", "You:") + " ";
        FlareThreadRow(m.List, "Ana Ruiz", "c12", "18m", complaint, 1, open == "resident");
        FlareThreadRow(m.List, "Glow Cosmetics", "d:7", "1h", offer, 1, open == "offer");
        FlareThreadRow(m.List, "Jade Rivers", "i:3", "3h",
            t("reallife_flare_dm_campaign_post", "My post for {business} is up. It brought {visitors} people in, about {money} spent.").Replace("{business}", FlareShop)
                .Replace("{visitors}", "38").Replace("{money}", "$912"), 0, false);
        FlareThreadRow(m.List, "Speedy Bites", "k:Speedy Bites", "1d", you + t("reallife_flare_kit_rival_calm",
            "We wish every business on the street well. Come in and judge us for yourself, the door is open."), 0, false);
        FlareThreadRow(m.List, "Alex", "f1", "2d", t("reallife_flare_dm_fan", "Your post was everywhere today. I had to say hello."), 0, false);
        if (open == null)
        {
            return;
        }
        m.Send = t("reallife_flare_send", "Send");
        m.Detail.Foot = true;
        if (open == "offer")
        {
            m.DetailName = "Glow Cosmetics";
            m.DetailHue = UiColour.HueOf("d:7");
            m.DetailLine = t("reallife_flare_kit_offer_line", "Offers {money} a post for {posts} posts").Replace("{money}", "$450").Replace("{posts}", "3");
            FlareBubble(m.Detail, offer, "1h", false);
            m.Quick = new[] { t("reallife_flare_kit_accept", "Accept {money}").Replace("{money}", "$1,350"), t("reallife_flare_kit_decline", "Decline") };
            return;   // an offer is answered by its two buttons
        }
        m.DetailName = "Ana Ruiz";
        m.DetailHue = UiColour.HueOf("c12");
        m.DetailLine = t("reallife_flare_kit_kind_resident", "Resident");
        m.DetailLink = t("reallife_flare_kit_view_profile", "Profile");
        FlareBubble(m.Detail, t("reallife_flare_dm_praise", "Just wanted to say {business} was lovely. I will be back soon.").Replace("{business}", FlareShop), "6d", false);
        FlareBubble(m.Detail, t("reallife_flare_kit_quick_res_0", "Thank you, that means a lot."), "6d", true);
        FlareBubble(m.Detail, complaint, "18m", false);
        m.Quick = new[]
        {
            t("reallife_flare_kit_quick_res_0", "Thank you, that means a lot."), t("reallife_flare_kit_quick_res_1", "Sorry to hear that. We will put it right."),
            t("reallife_flare_kit_quick_res_2", "Come and see us again soon.")
        };
        m.Field = string.Empty;
        m.FieldHint = t("reallife_flare_dm_placeholder", "Write a message");
        m.FieldArg = FlareFace.ThreadField;
    }

    private static void FlareThreadRow(FlareList list, string name, string key, string time, string last, int unread, bool chosen)
    {
        ref FlareRow r = ref FlareRowOf(list, FlareRowKind.Thread, name, key);
        r.Time = time;
        r.Text = last;
        r.Value = unread > 0 ? unread.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        r.Flags = (unread > 0 && !chosen ? FlareRowFlags.Unread : FlareRowFlags.None) | (chosen ? FlareRowFlags.Chosen : FlareRowFlags.None);
    }

    private static void FlareBubble(FlareList list, string text, string time, bool mine)
    {
        ref FlareRow r = ref list.Add();
        r.Kind = FlareRowKind.Bubble;
        r.Text = text;
        r.Time = time;
        r.Flags = mine ? FlareRowFlags.Mine : FlareRowFlags.None;
    }

    // ------------------------------------------------------------------------------------------------ Profile and a post's thread

    private static void FlareProfile(FlareFaceModel m, UiWords t, bool me)
    {
        m.Page = FlareFace.Profile;
        FlareProfileModel h = m.Profile;
        m.Tabs = new[] { t("reallife_flare_tab_posts", "Posts"), t("reallife_flare_tab_replies", "Replies"), t("reallife_flare_kit_tab_media", "Media") };
        m.List.EmptyIcon = UiIcon.Message;
        m.List.EmptyTitle = t("reallife_flare_empty_profile", "No posts yet.");
        if (me)
        {
            h.Name = FlareMe;
            h.Handle = FlareMyHandle;
            h.Bio = "Owner of Bloom Cafe and Bloom Bakery in Murray Hill. Coffee first, then everything else.";
            h.Hue = UiColour.HueOf("me");
            h.Banner = "sample:p3";
            h.Figures = new[] { "2,418", "86", "57" };
            h.Labels = new[] { t("reallife_flare_kit_followers", "Followers"), t("reallife_flare_kit_following", "Following"), t("reallife_flare_kit_posts", "Posts") };
            m.List.EmptyBody = t("reallife_flare_kit_empty_profile_body", "Your posts show here with what they brought.");
            ref FlareRow a = ref FlarePost(m.List, FlareMe, FlareMyHandle, "me", "1d",
                t("reallife_flare_biz_post_2", "Thank you to everyone who came to {name} this week.").Replace("{name}", FlareShop), "212", "18", "9", "3.1K");
            a.Flags = FlareRowFlags.Mine | FlareRowFlags.Good;
            a.Line = t("reallife_flare_kit_promoted", "Promoted in {district}").Replace("{district}", "Murray Hill");
            a.Icon = UiIcon.Megaphone;
            a.Value = t("reallife_flare_kit_brought", "Brought {visitors} visitors, {money}").Replace("{visitors}", "46").Replace("{money}", "$1,180");
            a.Picture = "sample:p3";
            ref FlareRow b = ref FlarePost(m.List, FlareMe, FlareMyHandle, "me", "4d",
                t("reallife_flare_biz_post_3", "{name} is open late tonight.").Replace("{name}", FlareShop2), "34", "2", null, "480");
            b.Flags = FlareRowFlags.Mine | FlareRowFlags.Bad;
            b.Value = t("reallife_flare_kit_result_flop", "Fell flat");
            return;
        }
        h.Name = "Jade Rivers";
        h.Handle = "@jaderivers";
        h.Bio = t("reallife_flare_inf_food_2", "Coffee crawl through {district}. My feet hurt and I regret nothing.").Replace("{district}", "Murray Hill");
        h.Hue = UiColour.HueOf("i:3");
        h.Verified = true;
        h.Banner = "sample:p1";
        h.Figures = new[] { "96K", "412" };
        h.Labels = new[] { t("reallife_flare_kit_followers", "Followers"), t("reallife_flare_kit_posts", "Posts") };
        h.Follow = t("reallife_flare_follow", "Follow");
        ref FlareRow p = ref FlarePost(m.List, "Jade Rivers", "@jaderivers", "i:3", "1d",
            t("reallife_flare_inf_food_1", "Best plate I have had in {district} this month.").Replace("{district}", "Murray Hill"), "4.8K", "230", "96", "96K");
        p.Flags = FlareRowFlags.Verified;
        p.Line = t("reallife_flare_kit_partnership", "Paid partnership with {business}").Replace("{business}", FlareShop);
        p.Icon = UiIcon.Briefcase;
        p.Picture = "sample:p2";
    }

    private static void FlareThread(FlareFaceModel m, UiWords t)
    {
        m.Page = FlareFace.Thread;
        m.Head = t("reallife_flare_kit_post_title", "Post");
        m.HeadLine = t("reallife_flare_kit_replies_count", "{count} replies").Replace("{count}", "84");
        ref FlareRow p = ref FlarePost(m.List, "Speedy Bites", "@speedybites", "k:Speedy Bites", "2h",
            t("reallife_flare_biz_post_0", "Two for one at {name} today only.").Replace("{name}", "Speedy Bites"), "1.2K", "84", "31", "11.9K");
        p.Flags = FlareRowFlags.Verified | FlareRowFlags.Liked;
        p.Tags = new[] { "#BroadwayNights", "#MidtownEats" };
        p.Picture = "sample:p1";
        FlareSection(m.List, t("reallife_flare_replies_title", "Replies"));
        ref FlareRow a = ref FlarePost(m.List, "Ana Ruiz", "@anaruiz", "c12", "1h", t("reallife_flare_cit_reply_agree_0", "Same here. Somebody has to say it."), "12", null, null, null);
        a.Flags = FlareRowFlags.Comment;
        ref FlareRow b = ref FlarePost(m.List, "Tom Hart", "@tomhart", "c40", "50m", t("reallife_flare_cit_reply_kind_1", "Hope you are doing all right."), "3", null, null, null);
        b.Flags = FlareRowFlags.Comment | FlareRowFlags.Liked;
        FlareSection(m.List, t("reallife_flare_comments_more", "And {count} more replies.").Replace("{count}", "82"));
        m.Field = string.Empty;
        m.FieldHint = t("reallife_flare_reply_placeholder", "Post your reply");
        m.FieldArg = FlareFace.ReplyField;
        m.Send = t("reallife_flare_reply_button", "Reply");
    }

    // ------------------------------------------------------------------------------------------------ Business

    private static void FlareBusiness(FlareFaceModel m, UiWords t, int shops)
    {
        m.Page = FlareFace.Business;
        FlareBusinessModel b = m.Business;
        m.Head = t("reallife_flare_kit_business_title", "Your businesses");
        m.List.EmptyIcon = UiIcon.Store;
        if (shops == 0)
        {
            m.List.EmptyTitle = t("reallife_flare_kit_no_business", "No business yet");
            m.List.EmptyBody = t("reallife_flare_kit_no_business_body", "Open a business and its stars, reviews and visits from Flare show here.");
            return;
        }
        bool turning = shops == 2;
        b.Picker = new[] { FlareShop, FlareShop2 };
        b.Chosen = turning ? 1 : 0;
        m.HeadLine = t("reallife_flare_kit_business_line", "{kind} in {district}").Replace("{kind}", t("reallife_flare_niche_0", "Food")).Replace("{district}", "Murray Hill");
        b.RatingTitle = t("reallife_flare_kit_rating", "Rating");
        b.Rating = turning ? "3.6" : "4.3";
        b.Halves = turning ? 7 : 9;
        b.RatingLine = t("reallife_flare_kit_reviews_count", "{count} reviews").Replace("{count}", turning ? "41" : "128");
        b.Bars = turning ? new[] { 12, 9, 6, 8, 6 } : new[] { 74, 31, 12, 7, 4 };
        b.VisitsTitle = t("reallife_flare_kit_visits_title", "From Flare, 7 days");
        b.Figures = new[]
        {
            turning ? "17" : "184", turning ? "$402" : "$4,310", turning ? "640" : "1.9K",
            turning ? t("reallife_flare_kit_campaign_none", "None running") : t("reallife_flare_kit_campaign_percent", "+{percent}% customers").Replace("{percent}", "8")
        };
        b.FigureLabels = new[]
        {
            t("reallife_flare_kit_fig_visitors", "Visitors"), t("reallife_flare_kit_fig_spent", "Spent there"), t("reallife_flare_kit_fig_city_followers", "Followers in the city"),
            t("reallife_flare_kit_fig_campaign", "Campaign")
        };
        b.Warning = turning
            ? t("reallife_flare_kit_turning", "Reviews are turning: {now} stars lately, against {before} before. Answer the poor ones.").Replace("{now}", "2.9").Replace("{before}", "4.1")
            : null;
        b.ReviewsTitle = t("reallife_flare_kit_reviews_title", "Reviews");
        m.List.EmptyIcon = UiIcon.Star;
        m.List.EmptyTitle = t("reallife_flare_kit_no_reviews", "No reviews yet");
        m.List.EmptyBody = t("reallife_flare_kit_no_reviews_body", "Reviews come in as residents visit.");
        string shop = turning ? FlareShop2 : FlareShop;
        FlareReview(m.List, t, "Ana Ruiz", 2, "18m", shop, 3, null);
        FlareReview(m.List, t, "Tom Hart", 5, "2h", shop, 2, null);
        FlareReview(m.List, t, "Lena Fox", 1, "1d", shop, 1, t("reallife_flare_reply_sorry", "We are sorry your visit was not right. Thank you for telling us, we are putting it right."));
        FlareReview(m.List, t, "Ravi Shah", 4, "2d", shop, 4, null);
    }

    private static readonly string[] FlareStarsEnglish =
    {
        "One star for {name}. Never again.", "Two stars for {name}. Not great.", "Three stars for {name}. It was fine.", "Four stars for {name}. Would go back.", "Five stars for {name}."
    };

    private static readonly string[] FlareWhyGood = { "A good visit all round.", "Fair prices for what you get.", "Lovely quality.", "Quick service.", "Clean and tidy inside." };
    private static readonly string[] FlareWhyPoor = { "A mixed visit at best.", "Too dear for what it is.", "The quality let me down.", "The wait was far too long.", "It was not clean." };

    private static void FlareReview(FlareList list, UiWords t, string name, int stars, string time, string shop, int why, string answer)
    {
        ref FlareRow r = ref FlareRowOf(list, FlareRowKind.Review, name, name);
        r.Stars = stars;
        r.Time = time;
        string n = stars.ToString(System.Globalization.CultureInfo.InvariantCulture), w = why.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string about = stars >= 3 ? t("reallife_flare_kit_why_" + w + "_good", FlareWhyGood[why]) : t("reallife_flare_kit_why_" + w + "_poor", FlareWhyPoor[why]);
        r.Text = t("reallife_flare_review_post_" + n, FlareStarsEnglish[stars - 1]).Replace("{name}", shop) + " " + about;
        if (answer != null)
        {
            r.Flags = FlareRowFlags.Answered;
            r.Line = t("reallife_flare_kit_your_reply", "Your reply: {words}").Replace("{words}", answer);
        }
        else
        {
            r.Button = t("reallife_flare_kit_reply", "Reply");
        }
    }

    // ------------------------------------------------------------------------------------------------ the creator studio

    private static void FlareStudio(FlareFaceModel m, UiWords t)
    {
        m.Page = FlareFace.Studio;
        FlareStudioModel st = m.Studio;
        m.Head = t("reallife_flare_kit_studio_title", "Creator studio");
        m.HeadLine = t("reallife_flare_kit_studio_line", "{tier}, {followers} followers").Replace("{tier}", t("reallife_flare_tier_1", "Local")).Replace("{followers}", "2,418");
        st.Labels = new[]
        {
            t("reallife_flare_kit_fig_followers", "Followers"), t("reallife_flare_kit_fig_reach", "Reach"), t("reallife_flare_kit_fig_visitors", "Visitors"),
            t("reallife_flare_kit_fig_money", "Money from Flare")
        };
        st.Figures = new[] { "2,418", "11.9K", "184", "$4,310" };
        st.Deltas = new[]
        {
            t("reallife_flare_kit_week_followers", "{change} this week").Replace("{change}", "+164"), t("reallife_flare_kit_week_reach", "{change}% on last week").Replace("{change}", "+22"),
            t("reallife_flare_kit_week_before", "{change} on last week").Replace("{change}", "+61"), t("reallife_flare_kit_week_before", "{change} on last week").Replace("{change}", "-$120")
        };
        st.Tones = new[] { 1, 1, 1, -1 };
        st.LineTitle = t("reallife_flare_kit_line_title", "Followers this week");
        st.Line = new[] { 2010f, 2032f, 2025f, 2090f, 2146f, 2131f, 2254f, 2418f };
        st.TierTitle = t("reallife_flare_kit_tier_title", "Sponsorship");
        st.Tier = t("reallife_flare_tier_1", "Local");
        st.TierShare = (2418f - 500f) / 4500f;
        st.TierLine = t("reallife_flare_kit_tier_next", "{count} more followers to {tier}").Replace("{count}", "2,582").Replace("{tier}", t("reallife_flare_tier_2", "Known"));
        st.Perks = new[]
        {
            "-" + t("reallife_flare_kit_perk_sponsors", "Sponsor deals from {tier}").Replace("{tier}", t("reallife_flare_tier_2", "Known")),
            "+" + t("reallife_flare_kit_perk_cap", "Posts add up to {percent}% customers").Replace("{percent}", "6"),
            "-" + t("reallife_flare_kit_perk_jobs", "Job offers pay {percent}% more").Replace("{percent}", "10"),
            "-" + t("reallife_flare_kit_perk_vip", "{percent}% off promotions and hires").Replace("{percent}", "5"),
            "-" + t("reallife_flare_kit_perk_raise", "Pay reviews add {percent}%").Replace("{percent}", "2"),
            "-" + t("reallife_flare_kit_perk_investors", "Investors come asking")
        };
        m.List.EmptyIcon = UiIcon.ChartLineUp;
        m.List.EmptyTitle = t("reallife_flare_kit_no_results", "No results yet");
        m.List.EmptyBody = t("reallife_flare_kit_no_results_body", "Post about your business or hire an influencer to see what it brings.");
        FlareSection(m.List, t("reallife_flare_kit_results_posts", "Your posts"));
        FlareResult(m.List, UiIcon.Megaphone, UiRole.AccentText, t("reallife_flare_kit_result_post", "Your post about {business} brought {visitors} visitors").Replace("{business}", FlareShop)
            .Replace("{visitors}", "12"), t("reallife_flare_kit_result_on", "Running, {hours}h left, {visitors} expected").Replace("{hours}", "4").Replace("{visitors}", "31"), "+$290", 1);
        FlareResult(m.List, UiIcon.Megaphone, UiRole.Chart2, t("reallife_flare_kit_result_viral", "Your post about {business} went viral: {visitors} visitors").Replace("{business}", FlareShop)
            .Replace("{visitors}", "46"), t("reallife_flare_kit_result_done", "Done, {time}").Replace("{time}", "1d"), "+$1,180", 1);
        FlareResult(m.List, UiIcon.ChartLineUp, UiRole.AccentText, t("reallife_flare_kit_result_flopped", "Your post about {business} fell flat").Replace("{business}", FlareShop2),
            t("reallife_flare_kit_result_done", "Done, {time}").Replace("{time}", "4d"), null, -1);
        FlareSection(m.List, t("reallife_flare_kit_results_campaigns", "Campaigns"));
        FlareResult(m.List, UiIcon.Megaphone, UiRole.Chart2, t("reallife_flare_kit_campaign_running", "{handle} for {business}").Replace("{handle}", "@jaderivers").Replace("{business}", FlareShop),
            t("reallife_flare_kit_campaign_day", "Day {day} of {days}").Replace("{day}", "3").Replace("{days}", "7"),
            t("reallife_flare_kit_campaign_percent", "+{percent}% customers").Replace("{percent}", "8"), 0);
        FlareResult(m.List, UiIcon.Megaphone, UiRole.OkText, t("reallife_flare_kit_campaign_brought", "{handle} brought {visitors} visitors to {business}").Replace("{handle}", "@marcobell")
            .Replace("{visitors}", "64").Replace("{business}", FlareShop2), t("reallife_flare_kit_result_done", "Done, {time}").Replace("{time}", "9d"), "+$1,540", 1);
    }

    private static void FlareResult(FlareList list, UiIcon icon, UiRole tint, string text, string line, string value, int tone)
    {
        ref FlareRow r = ref list.Add();
        r.Kind = FlareRowKind.Result;
        r.Icon = icon;
        r.Tint = tint;
        r.Text = text;
        r.Line = line;
        r.Value = value;
        r.Flags = tone > 0 ? FlareRowFlags.Good : tone < 0 ? FlareRowFlags.Bad : FlareRowFlags.None;
    }

    // ------------------------------------------------------------------------------------------------ Hire

    private static void FlareHire(FlareFaceModel m, UiWords t)
    {
        m.Page = FlareFace.Hire;
        m.Head = t("reallife_flare_kit_hire_title", "Hire an influencer");
        m.HeadLine = t("reallife_flare_kit_hire_line", "{count} influencers for {business}").Replace("{count}", "4").Replace("{business}", FlareShop);
        m.Business.Picker = new[] { t("reallife_flare_kit_all_districts", "All districts"), "Midtown", "Murray Hill", "Garment District" };
        m.Business.Chosen = 0;
        m.List.EmptyIcon = UiIcon.People;
        m.List.EmptyTitle = t("reallife_flare_kit_no_influencers", "No influencers here");
        m.List.EmptyBody = t("reallife_flare_kit_no_influencers_body", "Try every district, or come back in a few days.");
        FlareInfluencer(m.List, t, "Jade Rivers", "@jaderivers", "i:3", 0, "Murray Hill", "96K", "4.1%", "$2,400", "38", true, false);
        FlareInfluencer(m.List, t, "Marco Bell", "@marcobell", "i:5", 3, "Midtown", "18.2K", "6.3%", "$620", "14", false, true);
        FlareInfluencer(m.List, t, "Priya Nair", "@priyanair", "i:8", 1, "Garment District", "9.4K", "7.8%", "$340", "6", false, false);
        FlareInfluencer(m.List, t, "Sam Okafor", "@samokafor", "i:11", 2, "Murray Hill", "3.1K", "9.2%", "$150", "5", false, false);
    }

    private static readonly string[] FlareNiches = { "Food", "Fashion", "Fitness", "Night life" };

    private static void FlareInfluencer(FlareList list, UiWords t, string name, string handle, string key, int niche, string district, string followers, string engagement, string price,
        string visitors, bool verified, bool booked)
    {
        ref FlareRow r = ref FlareRowOf(list, FlareRowKind.Influencer, name, key);
        r.Handle = t("reallife_flare_kit_influencer_line", "{handle}, {niche}, {district}").Replace("{handle}", handle)
            .Replace("{niche}", t("reallife_flare_niche_" + niche.ToString(System.Globalization.CultureInfo.InvariantCulture), FlareNiches[niche])).Replace("{district}", district);
        r.Flags = (verified ? FlareRowFlags.Verified : FlareRowFlags.None) | (booked ? FlareRowFlags.Off : FlareRowFlags.None);
        r.Tags = new[]
        {
            t("reallife_flare_kit_fig_followers", "Followers"), followers, t("reallife_flare_kit_fig_engagement", "Engagement"), engagement,
            t("reallife_flare_kit_fig_price", "A post"), price, t("reallife_flare_kit_fig_expected", "Likely visits"),
            t("reallife_flare_kit_visitors_n", "{count} visitors").Replace("{count}", visitors)
        };
        r.Button = booked ? t("reallife_flare_kit_booked", "Booked") : t("reallife_flare_kit_hire", "Hire");
    }

    // ------------------------------------------------------------------------------------------------ In the city

    private static void FlareCity(FlareFaceModel m, UiWords t, bool some)
    {
        m.Page = FlareFace.City;
        m.Head = t("reallife_flare_nav_city", "In the city");
        m.HeadLine = t("reallife_flare_kit_city_line", "What the city wants, and what your rivals do");
        m.List.EmptyIcon = UiIcon.Building;
        m.List.EmptyTitle = t("reallife_flare_kit_city_empty", "Quiet in the city");
        m.List.EmptyBody = t("reallife_flare_kit_city_empty_body", "Trends, rival moves, events and offers show here as they happen.");
        if (!some)
        {
            return;
        }
        FlareSection(m.List, t("reallife_flare_kit_city_demand", "Trending demand"));
        FlareCard(m.List, UiIcon.ChartLineUp, UiRole.OkText,
            t("reallife_flare_city_trend_district", "{what} in {district}").Replace("{what}", t("reallife_flare_city_trend_kind_3", "Eating out")).Replace("{district}", "Murray Hill"), null,
            t("reallife_flare_kit_trend_card", "Now {stage}: {moved} residents moved by it, {days} days left.").Replace("{stage}", t("reallife_flare_city_trend_rising", "rising"))
                .Replace("{moved}", "412").Replace("{days}", "5"), "Murray Hill", t("reallife_flare_kit_promote_there", "Promote"));
        FlareSection(m.List, t("reallife_flare_kit_city_rivals", "Rival moves"));
        FlareCard(m.List, UiIcon.Warning, UiRole.DangerText, "Speedy Bites", "3h", t("reallife_flare_kit_rival_attack", "Ran your business down in {district}").Replace("{district}", "Murray Hill"),
            t("reallife_flare_dm_rival", "We posted about {business} today. The street deserves better, and we said so.").Replace("{business}", FlareShop), t("reallife_flare_kit_answer", "Answer"));
        FlareCard(m.List, UiIcon.Megaphone, UiRole.Chart2, "Thrifty Threads", "5h", t("reallife_flare_kit_rival_promo", "Paid to promote in {district}").Replace("{district}", "Midtown"),
            t("reallife_flare_biz_post_1", "New in at {name}. Come and see what is on the shelves.").Replace("{name}", "Thrifty Threads"), t("reallife_flare_kit_view", "View"));
        FlareSection(m.List, t("reallife_flare_kit_city_events", "Events"));
        ref FlareRow e = ref FlareCard(m.List, UiIcon.Calendar, UiRole.AccentText, "Open mic at the Lantern", "1h", "Music and a cheap round from eight. Bring a friend.", "Alex",
            t("reallife_flare_kit_going", "Going"));
        e.Flags |= FlareRowFlags.Chosen;
        FlareSection(m.List, t("reallife_flare_kit_city_work", "Offers and work"));
        FlareCard(m.List, UiIcon.Coin, UiRole.Gold, "Glow Cosmetics", null,
            t("reallife_flare_kit_offer_card", "Pays {money} a post for {posts} posts").Replace("{money}", "$450").Replace("{posts}", "3"),
            t("reallife_flare_kit_offer_until", "Open for {days} more days").Replace("{days}", "4"), t("reallife_flare_kit_view", "View"));
    }

    private static ref FlareRow FlareCard(FlareList list, UiIcon icon, UiRole tint, string name, string time, string text, string line, string button)
    {
        ref FlareRow r = ref list.Add();
        r.Kind = FlareRowKind.Card;
        r.Icon = icon;
        r.Tint = tint;
        r.Name = name;
        r.Time = time;
        r.Text = text;
        r.Line = line;
        r.Button = button;
        return ref r;
    }

    // ------------------------------------------------------------------------------------------------ the sheets

    private static void FlareSheet(FlareFaceModel m, UiWords t, int sheet)
    {
        m.Sheet = sheet;
        m.Sheet0.Cancel = t("reallife_flare_kit_cancel", "Cancel");
    }

    private static void FlareCompose(FlareFaceModel m, UiWords t, bool promote)
    {
        FlareSheet(m, t, FlareFace.ComposeSheet);
        FlareSheetModel k = m.Sheet0;
        k.Title = t("reallife_flare_kit_compose_title", "Create a post");
        k.Line = t("reallife_flare_kit_compose_as", "Posting as {name}, {handle}").Replace("{name}", FlareMe).Replace("{handle}", FlareMyHandle);
        k.Field = true;
        k.FieldText = promote ? "Fresh cinnamon buns at Bloom Cafe from seven. First twenty get a free coffee." : string.Empty;
        k.FieldHint = t("reallife_flare_kit_composer", "What's happening, {name}?").Replace("{name}", "Walter");
        k.FieldCount = t("reallife_flare_kit_count", "{count} of {most}").Replace("{count}", k.FieldText.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{most}", "200");
        k.Picture = promote ? "sample:p3" : null;
        string[] photos =
        {
            t("reallife_flare_kit_no_photo", "No photo"), t("reallife_flare_kit_photo_mine", "Your photo {n}").Replace("{n}", "1"),
            t("reallife_flare_kit_photo_n", "City photo {n}").Replace("{n}", "2"), t("reallife_flare_kit_photo_n", "City photo {n}").Replace("{n}", "3")
        };
        string[] places = { t("reallife_flare_kit_no_place", "None"), FlareShop, FlareShop2 };
        string[] poll = { t("reallife_flare_kit_no_poll", "No poll"), t("reallife_flare_kit_poll_yes_no", "Yes or no") };
        if (!promote)
        {
            k.Groups = new[] { t("reallife_flare_kit_chip_photo", "Photo"), t("reallife_flare_kit_chip_place", "Tag a place"), t("reallife_flare_kit_chip_poll", "Poll") };
            k.Chips = new[] { photos, places, poll };
            k.Chosen = new[] { 0, 0, 0 };
            k.Confirm = t("reallife_flare_post", "Post");
            k.ConfirmOff = true;
            return;
        }
        string[] steps =
        {
            t("reallife_flare_kit_free", "Free"), t("reallife_flare_kit_reach_step", "{count} a day").Replace("{count}", "2K"),
            t("reallife_flare_kit_reach_step", "{count} a day").Replace("{count}", "5K"), t("reallife_flare_kit_reach_step", "{count} a day").Replace("{count}", "10K")
        };
        string[] days = { t("reallife_flare_kit_day_one", "1 day"), t("reallife_flare_kit_days_n", "{count} days").Replace("{count}", "3"), t("reallife_flare_kit_days_n", "{count} days").Replace("{count}", "7") };
        k.Groups = new[]
        {
            t("reallife_flare_kit_chip_photo", "Photo"), t("reallife_flare_kit_chip_place", "Tag a place"), t("reallife_flare_kit_chip_promote", "Promote a business"),
            t("reallife_flare_kit_for_how_long", "For how long"), t("reallife_flare_kit_chip_poll", "Poll")
        };
        k.Chips = new[] { photos, places, steps, days, poll };
        k.Chosen = new[] { 3, 1, 2, 1, 0 };
        k.FigureLabels = new[]
        {
            t("reallife_flare_kit_fig_reach", "Reach"), t("reallife_flare_kit_fig_likely", "Likely visitors"), t("reallife_flare_kit_fig_takings", "Likely takings"),
            t("reallife_flare_kit_fig_cost", "Cost")
        };
        k.Figures = new[] { "15K", "52", "$1,240", "$360" };
        k.Note = t("reallife_flare_kit_forecast", "Runs for {hours} hours. {flop} in 100 posts like this fall flat, {viral} in 100 go viral.").Replace("{hours}", "72").Replace("{flop}", "18")
            .Replace("{viral}", "3");
        k.Confirm = t("reallife_flare_kit_post_pay", "Post and pay {money}").Replace("{money}", "$360");
    }

    // The picker over the compose sheet: the whole Gallery of 500 photos as the Gallery's own code fills it, the albums with their counts, a search.
    private static void FlarePhotoPicker(FlareFaceModel m, UiWords t)
    {
        m.Sheet = FlareFace.PhotosSheet;
        FlarePickerModel k = m.Picker;
        k.Title = t("reallife_flare_kit_picker_title", "Choose a photo");
        k.SearchHint = t("reallife_flare_kit_picker_search", "Search your photos");
        k.Cancel = t("reallife_flare_kit_cancel", "Cancel");
        UiAlbumModel album = Album(t, 500, "grid", out int[] counts);
        k.Photos.Photos = album.Photos;
        k.Photos.Count = album.Count;
        k.Photos.EmptyTitle = album.EmptyTitle;
        k.Photos.EmptyBody = album.EmptyBody;
        k.Albums = new string[6];
        for (int i = 0; i < 6; i++)
        {
            k.Albums[i] = t(RealLifeOverhaul.Photos.GalleryFind.KitKeys[i], RealLifeOverhaul.Photos.GalleryFind.KitKeys[i]) + " " + counts[i];
        }
        k.Album = 0;
    }

    private static void FlareHireSheet(FlareFaceModel m, UiWords t)
    {
        FlareSheet(m, t, FlareFace.HireSheet);
        FlareSheetModel k = m.Sheet0;
        k.Title = t("reallife_flare_kit_hire_sheet", "Hire {handle}").Replace("{handle}", "@jaderivers");
        k.Line = t("reallife_flare_kit_hire_sheet_line", "{niche}, {followers} followers, {engagement}% engagement").Replace("{niche}", t("reallife_flare_niche_0", "Food"))
            .Replace("{followers}", "96K").Replace("{engagement}", "4.1");
        k.Groups = new[] { t("reallife_flare_kit_group_business", "Business"), t("reallife_flare_kit_group_deal", "Deal") };
        k.Chips = new[]
        {
            new[] { FlareShop, FlareShop2 },
            new[]
            {
                t("reallife_flare_deal_0", "One post"), t("reallife_flare_deal_1", "A week's series"), t("reallife_flare_deal_2", "Ambassador, four weeks"),
                t("reallife_flare_deal_4", "A launch"), t("reallife_flare_deal_5", "A giveaway")
            }
        };
        k.Chosen = new[] { 0, 1 };
        k.Words = t("reallife_flare_deal_about_1", "Five posts over a week.");
        k.FigureLabels = new[]
        {
            t("reallife_flare_kit_fig_cost", "Cost"), t("reallife_flare_kit_fig_customers", "Customers"), t("reallife_flare_kit_fig_likely", "Likely visitors"),
            t("reallife_flare_kit_fig_audience", "Audience")
        };
        k.Figures = new[] { "$9,600", "+12%", "190", "Murray Hill" };
        k.Note = t("reallife_flare_kit_hire_fits", "{posts} posts. Their followers live near your business. {flop} in 100 fall flat, {viral} in 100 go viral.").Replace("{posts}", "5")
            .Replace("{flop}", "14").Replace("{viral}", "4");
        k.Confirm = t("reallife_flare_kit_hire_pay", "Hire for {money}").Replace("{money}", "$9,600");
    }

    private static void FlareReplySheet(FlareFaceModel m, UiWords t)
    {
        FlareSheet(m, t, FlareFace.ReplySheet);
        FlareSheetModel k = m.Sheet0;
        k.Title = t("reallife_flare_kit_reply_title", "Answer {name}").Replace("{name}", "Ana Ruiz");
        k.Line = t("reallife_flare_kit_reply_line", "A review of {business}").Replace("{business}", FlareShop);
        k.Stars = 2;
        k.Words = t("reallife_flare_review_post_2", "Two stars for {name}. Not great.").Replace("{name}", FlareShop) + " " + t("reallife_flare_kit_why_3_poor", "The wait was far too long.");
        k.Options = new[] { t("reallife_flare_kit_option_0", "Thank them"), t("reallife_flare_kit_option_1", "Say sorry"), t("reallife_flare_kit_option_2", "Stand your ground") };
        k.OptionLines = new[]
        {
            t("reallife_flare_kit_option_stands", "Polite: the review is left as it was"), t("reallife_flare_kit_option_eases", "Softens it: this review counts for half"),
            t("reallife_flare_kit_option_worsens", "Draws eyes: it counts for more, and talk sours")
        };
        k.Option = 1;
        k.FigureLabels = new[] { t("reallife_flare_kit_fig_rating_now", "Rating now"), t("reallife_flare_kit_fig_rating_after", "After") };
        k.Figures = new[] { "4.27", "4.30" };
        k.Note = t("reallife_flare_reply_sorry", "We are sorry your visit was not right. Thank you for telling us, we are putting it right.");
        k.Confirm = t("reallife_flare_kit_reply", "Reply");
    }
}
