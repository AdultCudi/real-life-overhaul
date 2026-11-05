using System;
using RealLifeOverhaul.Ui;

// Flare on the kit, every page and sheet as the game fills them (FlareScreen.Fill, FlareScreen.Desk and FlareScreen.Sheet): the same keys,
// the same rows, a city of the mockup's (flare_mock): Walter Mack of Bloom Cafe in Murray Hill, Speedy Bites, Pioneer Game studio,
// Thrifty Threads, Jade Rivers. The photos are the mockup's (UiPreviewRunner.Flare's "sample:" pictures).
internal static partial class UiSamples
{
    public static readonly string[] FlareStates =
    {
        "home", "home_following", "home_businesses", "explore", "explore_search", "explore_tag", "notices", "notices_empty", "messages", "messages_offer",
        "messages_none", "profile", "profile_other", "thread", "thread_mine", "business", "business_turning", "business_none", "studio", "hire", "city", "city_empty",
        "compose", "compose_promote", "hire_sheet", "reply_sheet", "photo_picker", "notices_sales", "city_new", "compose_report", "compose_moment", "ad_sheet", "edit_sheet"
    };

    public static void Flare(UiScene s, UiFrame frame, bool dark, IUiKeys keys, UiWords t, string state)
    {
        FlareFace.Lay(s, frame, dark, keys, t, FlareSample(t, state));
    }

    // One world for the badges and the pages they open: what is new in the notifications and unread in the messages. The nav's badge and the page's own head say the same figure:
    // FlareNewNotices counts the unread lines of the Notifications sample itself and FlareUnreadMessages the unread messages over the inbox's conversations, so neither can drift
    // from the page it opens; a check holds them together.
    internal static int FlareNewNotices
    {
        get
        {
            if (_flareNew < 0)
            {
                UiWords before = _flareWords;
                FlareWorld kept = _world;
                _flareWords = (key, english) => english;
                _world = FlareWorld.Known;
                var scratch = new FlareFaceModel();
                FlareNotices(scratch, _flareWords, true);
                _flareNew = 0;
                for (int i = 0; i < scratch.List.Count; i++)
                {
                    _flareNew += scratch.List.Rows[i].Kind == FlareRowKind.Notice && (scratch.List.Rows[i].Flags & FlareRowFlags.Unread) != 0 ? 1 : 0;
                }
                _flareWords = before;
                _world = kept;
            }
            return _flareNew;
        }
    }

    private static int _flareNew = -1;

    // The inbox's conversations in order, with what is unread in each: Ana Ruiz, Glow Cosmetics, Jade Rivers, Speedy Bites, Alex.
    private static readonly int[] FlareThreadUnread = { 1, 1, 1, 0, 0 };

    internal static int FlareUnreadMessages
    {
        get
        {
            int n = 0;
            for (int i = 0; i < FlareThreadUnread.Length; i++)
            {
                n += FlareThreadUnread[i];
            }
            return n;
        }
    }

    // What the world is made of: the figures a page quotes of Jade Rivers, and how many posts a trending tag has (a trend's district holds more posts than all its tags).
    private const int FlareJadeFollowers = 96000, FlareJadePosts = 412;
    private static readonly string[] FlareTagNames = { "BroadwayNights", "BurgerWeek", "ThriftyFinds", "MidtownEats", "CoffeeCrawl", "GarmentSale", "HarbourLights" };
    private static readonly string[] FlareTagWhere = { "Midtown", "Garment District", "Murray Hill", null, "Midtown", "Garment District", null };
    private static readonly int[] FlareTagPosts = { 3104, 1877, 905, 642, 512, 430, 388 };

    private const string FlareMe = "Walter Mack", FlareMyHandle = "@waltermack", FlareShop = "Bloom Cafe", FlareShop2 = "Bloom Bakery";

    private static readonly string[] FlareNavKeys =
    {
        "reallife_flare_nav_home", "reallife_flare_nav_explore", "reallife_flare_nav_notices", "reallife_flare_nav_messages", "reallife_flare_nav_profile",
        "reallife_flare_nav_studio", "reallife_flare_nav_business", "reallife_flare_nav_hire", "reallife_flare_nav_city"
    };

    private static readonly string[] FlareNavEnglish = { "Home", "Explore", "Notifications", "Messages", "Profile", "Studio", "Business", "Hire", "In the city" };

    private static UiWords _flareWords;

    // A short age as the screen writes it (FlareScreen.Ago): from the language's own templates, never letters of the sample's.
    private static string FlareAge(int n, char unit)
    {
        string count = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return unit == 'm' ? _flareWords("reallife_flare_ago_min", "{n}m").Replace("{n}", count) : unit == 'h' ? _flareWords("reallife_flare_ago_hour", "{n}h").Replace("{n}", count)
            : _flareWords("reallife_flare_kit_ago_day", "{n}d").Replace("{n}", count);
    }

    public static FlareFaceModel FlareSample(UiWords t, string state)
    {
        _flareWords = t;
        _world = FlareWorld.Of(state);
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
            case "thread_mine": FlareThreadMine(m, t); break;
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
            case "notices_sales": FlareNotices(m, t, true); m.Tab = 1; FlareKeepOnly(m.List, 1); break;
            case "city_new": FlareCity(m, t, false); FlareCityNew(m, t); break;
            case "compose_report": FlareHome(m, t, 0); FlareComposeKind(m, t, 2); break;
            case "compose_moment": FlareHome(m, t, 0); FlareComposeKind(m, t, 1); break;
            case "ad_sheet": FlareBusiness(m, t, 1); FlareAdSheet(m, t); break;
            case "edit_sheet": FlareProfile(m, t, true); FlareEditSheet(m, t); break;
        }
        m.Who.Count = Math.Min(m.Who.Count, FlareWhoMost);   // the side keeps eight accounts whatever the page
        // A new account has nothing unread; on the page that is open the notifications are read (its own head says how many were new).
        m.Badges[FlareFace.Notices] = m.Page == FlareFace.Notices || _world.NewPlayer ? 0 : FlareNewNotices;
        return m;
    }

    private static FlareWorld _world;

    /// <summary>The world a state of Flare is drawn from, for the checks that read its figures against the page's.</summary>
    internal static FlareWorld FlareWorldOf(string state) => FlareWorld.Of(state);

    // ------------------------------------------------------------------------------------------------ the shell and the side

    private static void FlareShell(FlareFaceModel m, UiWords t)
    {
        // The badges of the pages: what is new in the notifications and unread in the messages (FlareMessages takes off the conversation it opens, which is read by then).
        m.Badges = new int[FlareFace.Pages];
        m.Badges[FlareFace.Messages] = _world.NewPlayer ? 0 : FlareUnreadMessages;
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
        m.MyRing = UiRole.Grade4;   // the player's happiness, as the HUD has it
        m.SearchHint = t("reallife_flare_kit_search", "Search Flare");
        m.WeekTitle = t("reallife_flare_kit_week", "Your week");
        m.StudioLink = t("reallife_flare_kit_studio_link", "Open Creator studio");
        m.TrendTitle = t("reallife_flare_kit_trending", "Trending in the city");
        m.WhoTitle = t("reallife_flare_kit_who", "Who to follow");
        m.FollowWord = t("reallife_flare_follow", "Follow");
        m.FollowingWord = t("reallife_flare_follow_on", "Following");
        // The week the side shows is the world's, worded by the functions the Live face calls; a new account has none (and no line of followers).
        FlareFigures.Week(_world.Week(), t, false, out m.WeekLabels, out m.WeekFigures, out m.WeekDeltas, out m.WeekTones);
        m.Line = FlareFigures.NewAccount(_world.Week()) ? null : _world.Line();
        for (int i = 0; i < FlareTagNames.Length && i < 4; i++)
        {
            FlareTrend(m.Trends, t, FlareTagNames[i], FlareTagWhere[i], FlareTagPosts[i]);
        }
        // The accounts to follow, as the city offers them: the more of them there are, the more the page can take away (FlareScreen.Side keeps the first eight that remain).
        FlareWho(m.Who, t, "Thrifty Threads", "@thriftythreads", "k:Thrifty Threads", null, false);
        FlareWho(m.Who, t, "Jade Rivers", "@jaderivers", "i:3", FlareFigures.Count(FlareJadeFollowers), true);
        FlareWho(m.Who, t, "Marco Bell", "@marcobell", "i:5", "18.2K", false);
        FlareWho(m.Who, t, "Midtown Diner", "@midtowndiner", "k:Midtown Diner", null, false);
        FlareWho(m.Who, t, "Pioneer Game studio", "@pioneergames", "k:Pioneer Game studio", null, false);
        FlareWho(m.Who, t, "Burger Barn", "@burgerbarn", "k:Burger Barn", null, false);
        FlareWho(m.Who, t, "Speedy Bites", "@speedybites", "k:Speedy Bites", null, true);
        FlareWho(m.Who, t, "Priya Nair", "@priyanair", "i:8", "9.4K", false);
        FlareWho(m.Who, t, "Sam Okafor", "@samokafor", "i:11", "3.1K", false);
        FlareWho(m.Who, t, "Tess Whitaker", "@tesswhitaker", "i:14", "27.5K", false);
        FlareWho(m.Who, t, "Harbourside Cafe", "@harboursidecafe", "k:Harbourside Cafe", null, false);
        FlareWho(m.Who, t, "Omar Haddad", "@omarhaddad", "i:17", "19.8K", false);
    }

    // An account the city offers to follow: its handle and either its followers (a person's) or what it is (a business).
    private static void FlareWho(FlareList list, UiWords t, string name, string handle, string key, string followers, bool verified)
    {
        ref FlareRow r = ref FlareRowOf(list, FlareRowKind.Account, name, key);
        r.Handle = FlareAccountLine(t, handle, followers != null ? t("reallife_flare_kit_followers_n", "{count} followers").Replace("{count}", followers) : t("reallife_flare_kit_kind_business", "Business"));
        r.Flags |= verified ? FlareRowFlags.Verified : FlareRowFlags.None;
    }

    // What the page itself shows is not said again in the side column (FlareScreen.Side leaves it out and takes the next): a tag or an account is told once on a screen, and the
    // side keeps the first eight accounts that remain, as Live does.
    private static void FlareSideWithout(FlareFaceModel m, string tag, params string[] accounts)
    {
        FlareDrop(m.Trends, tag != null ? "#" + tag : null);
        for (int i = 0; i < accounts.Length; i++)
        {
            FlareDrop(m.Who, accounts[i]);
        }
        m.Who.Count = Math.Min(m.Who.Count, FlareWhoMost);
    }

    private const int FlareWhoMost = 8;   // FlareScreen.WhoMost

    private static void FlareDrop(FlareList list, string name)
    {
        int to = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (name == null || list.Rows[i].Name != name)
            {
                list.Rows[to++] = list.Rows[i];
            }
        }
        list.Count = to;
    }

    private static void FlareTrend(FlareList list, UiWords t, string tag, string district, int posts)
    {
        ref FlareRow r = ref list.Add();
        r.Kind = FlareRowKind.Tag;
        r.Name = "#" + tag;
        r.Handle = district != null ? t("reallife_flare_kit_trend_where", "Trending in {district}").Replace("{district}", district) : t("reallife_flare_kit_trend_city", "Across the city");
        r.Value = FlareFigures.PostsCount(posts, t);
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
        FlareLook(ref r, key);
        return ref r;
    }

    // What the rows of the screen make of an account's key (FlareScreen.Look): a person's face on the ring of what the game knows of their mood, a business its initials.
    private static void FlareLook(ref FlareRow r, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }
        int mood = key == "f1" ? 2 : key == "f2" ? 0 : -1;
        int reliability = key == "i:3" ? 92 : key == "i:5" ? 66 : key == "i:8" ? 74 : key == "i:11" ? 41 : key == "i:14" ? 83 : key == "i:17" ? 58 : key == "i:21" ? 71 : 70;
        if (key == "me")
        {
            r.Flags |= FlareRowFlags.Person;
            r.Ring = UiRole.Grade4;
        }
        else if (key.StartsWith("i:", System.StringComparison.Ordinal))
        {
            r.Flags |= FlareRowFlags.Person;
            r.Ring = UiTokens.Grade(reliability);
        }
        else if (key[0] == 'f' && key.Length > 1)
        {
            r.Flags |= FlareRowFlags.Person;
            r.Ring = FlareFace.MoodRing(mood);
        }
        else if (key[0] == 'c' && key.Length > 1 && key[1] != ':')
        {
            r.Flags |= FlareRowFlags.Person;
        }
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
        // Alex and Mom are people: a painted face on the ring of their mood (Alex a great day, Mom an ordinary one), as FlareScreen.Stories fills them.
        m.StoryPeople = new bool[m.Stories.Length];
        m.StoryRings = new UiRole[m.Stories.Length];
        m.StoryPeople[5] = m.StoryPeople[6] = true;
        m.StoryRings[5] = FlareFace.MoodRing(2);
        m.StoryRings[6] = FlareFace.MoodRing(0);
        // What the page names is not offered again in the side: its stories, and the authors of its feed.
        if (tab == 2)
        {
            FlareSideWithout(m, null, "Pioneer Game studio", "Speedy Bites", "Thrifty Threads", "Midtown Diner");
            FlareBusinessPosts(m, t);
            return;
        }
        FlareSideWithout(m, null, "Pioneer Game studio", "Speedy Bites", "Thrifty Threads", "Midtown Diner", "Jade Rivers");
        ref FlareRow p = ref FlarePost(m.List, "Speedy Bites", "@speedybites", "k:Speedy Bites", FlareAge(2, 'h'),
            t("reallife_flare_biz_post_0", "Two for one at {name} today only.").Replace("{name}", "Speedy Bites"), FlareFigures.Count(FlareSpeedy[0]), FlareFigures.Count(FlareSpeedy[1]),
            FlareFigures.Count(FlareSpeedy[2]), FlareFigures.Count(FlareSpeedy[3]));
        p.Flags |= FlareRowFlags.Verified | FlareRowFlags.Liked;
        p.Line = t("reallife_flare_kit_promoted", "Promoted in {district}").Replace("{district}", "Midtown");
        p.Icon = UiIcon.Megaphone;
        p.Tags = new[] { "#BroadwayNights", "#MidtownEats" };
        p.Picture = "sample:p1";
        ref FlareRow q = ref FlarePost(m.List, "Pioneer Game studio", "@pioneergames", "k:Pioneer Game studio", FlareAge(5, 'h'),
            t("reallife_flare_announce_post_3", "{name} is hiring. Send a message if you want to work with us.").Replace("{name}", "Pioneer Game studio"), "642", "37", "12", "8.4K");
        q.Picture = "sample:p2";
        ref FlareRow mine = ref FlareMine(m.List, t, FlareMy("thanks"));
        mine.Flags |= FlareRowFlags.Good;
        mine.Line = t("reallife_flare_kit_promoted", "Promoted in {district}").Replace("{district}", "Murray Hill");
        mine.Icon = UiIcon.Megaphone;
        mine.Value = FlareBrought(t, _world.Funnels[0]);
        ref FlareRow inf = ref FlarePost(m.List, "Jade Rivers", "@jaderivers", "i:3", FlareAge(1, 'd'),
            t("reallife_flare_inf_food_1", "Best plate I have had in {district} this month.").Replace("{district}", "Murray Hill"), FlareFigures.Count(FlareJadePost[0]),
            FlareFigures.Count(FlareJadePost[1]), FlareFigures.Count(FlareJadePost[2]), FlareFigures.Count(FlareJadePost[3]));
        inf.Flags |= FlareRowFlags.Verified;
        inf.Line = t("reallife_flare_kit_partnership", "Paid partnership with {business}").Replace("{business}", FlareShop);
        inf.Icon = UiIcon.Briefcase;
        inf.Tags = new[] { "#MurrayHillEats" };
    }

    // Likes, replies, reposts and views of the posts the pages share: Speedy Bites' two for one, and Jade Rivers' plate (an influencer's views are well under their followers).
    private static readonly int[] FlareSpeedy = { 1230, 84, 31, 10800 }, FlareJadePost = { 4800, 230, 120, 41300 };

    // Brought {visitors} visitors, {money}: what the city measured of a post of the player's (the funnel of the world).
    private static string FlareBrought(UiWords t, FlareFunnelWorld f) =>
        t("reallife_flare_kit_brought", "Brought {visitors} visitors, {money}").Replace("{visitors}", FlareFigures.N(f.Came)).Replace("{money}", FlareMoney(f.Dollars));

    // The short age of minutes ago as the screen writes it (FlareScreen.Ago): the minute, the hour, the day, and past a week the day's number.
    private static string FlareAgeOf(int minutes)
    {
        if (minutes < 60)
        {
            return FlareAge(Math.Max(1, minutes), 'm');
        }
        if (minutes < 1440)
        {
            return FlareAge(minutes / 60, 'h');
        }
        return minutes < 7 * 1440 ? FlareAge(minutes / 1440, 'd')
            : _flareWords("reallife_flare_day", "Day {day}").Replace("{day}", (FlareToday - minutes / 1440).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private const int FlareToday = 412;   // the day the status bar of the previews shows

    private static string FlareMoney(long dollars) => "$" + FlareFigures.N(dollars);

    private static FlareMyPost FlareMy(string id) => Array.Find(_world.Mine, p => p.Id == id);

    // A post of the player's with the counts the world holds for it: what the post and its city results say agree because they are the same record.
    private static ref FlareRow FlareMine(FlareList list, UiWords t, FlareMyPost p)
    {
        int seen = p.FunnelAt >= 0 ? _world.Funnels[p.FunnelAt].Seen : 0;
        ref FlareRow r = ref FlarePost(list, FlareMe, FlareMyHandle, "me", FlareAgeOf(p.AgeMinutes), FlareMyWords(t, p.Id), p.Likes > 0 ? FlareFigures.Count(p.Likes) : null,
            p.Replies > 0 ? FlareFigures.Count(p.Replies) : null, p.Reposts > 0 ? FlareFigures.Count(p.Reposts) : null, FlareFigures.Views(p.Views, seen));
        r.Flags |= FlareRowFlags.Mine;
        return ref r;
    }

    private static string FlareMyWords(UiWords t, string id)
    {
        switch (id)
        {
            case "thanks": return t("reallife_flare_biz_post_2", "Thank you to everyone who came to {name} this week.").Replace("{name}", FlareShop);
            case "late": return t("reallife_flare_biz_post_3", "{name} is open late tonight.").Replace("{name}", FlareShop2);
            case "poll": return "Should we open on Sundays?";
            case "a": return t("reallife_flare_biz_post_0", "Two for one at {name} today only.").Replace("{name}", FlareShop);
            case "b": return t("reallife_flare_biz_post_1", "New in at {name}. Come and see what is on the shelves.").Replace("{name}", FlareShop2);
            default: return t("reallife_flare_biz_post_3", "{name} is open late tonight.").Replace("{name}", FlareShop);
        }
    }

    private static void FlareBusinessPosts(FlareFaceModel m, UiWords t)
    {
        ref FlareRow a = ref FlarePost(m.List, "Thrifty Threads", "@thriftythreads", "k:Thrifty Threads", FlareAge(1, 'h'),
            t("reallife_flare_biz_post_1", "New in at {name}. Come and see what is on the shelves.").Replace("{name}", "Thrifty Threads"), "318", "22", "14", "4.2K");
        a.Picture = "sample:p3";
        a.Tags = new[] { "#ThriftyFinds" };
        ref FlareRow b = ref FlarePost(m.List, "Midtown Diner", "@midtowndiner", "k:Midtown Diner", FlareAge(3, 'h'),
            t("reallife_flare_biz_post_3", "{name} is open late tonight.").Replace("{name}", "Midtown Diner"), "96", "4", "2", "1.4K");
        b.Line = t("reallife_flare_kit_promoted_city", "Promoted");
        b.Icon = UiIcon.Megaphone;
        FlareMyPost poll = FlareMy("poll");
        ref FlareRow c = ref FlarePost(m.List, FlareShop, "@bloomcafe", "b:Bloom Cafe", FlareAgeOf(poll.AgeMinutes), FlareMyWords(t, "poll"), FlareFigures.Count(poll.Likes),
            FlareFigures.Count(poll.Replies), FlareFigures.Count(poll.Reposts), FlareFigures.Views(poll.Views, 0));
        c.Flags |= FlareRowFlags.Mine;
        // a poll's votes are the people who reacted to it: its likes, replies and reposts (FlareFeedRules.Poll)
        c.Value = t("reallife_flare_kit_poll", "Yes {yes}%, no {no}%, {votes} votes").Replace("{yes}", "64").Replace("{no}", "36")
            .Replace("{votes}", FlareFigures.N(poll.Likes + poll.Replies + poll.Reposts));
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
            FlareSideWithout(m, tag, "Speedy Bites");   // the head says the tag and its count; the posts name their authors
            m.Head = "#" + tag;
            m.HeadLine = FlareFigures.PostsCount(FlareTagPosts[Array.IndexOf(FlareTagNames, tag)], t);
            m.List.EmptyTitle = t("reallife_flare_empty_tag", "No posts with this tag in the last few days.");
            ref FlareRow p = ref FlarePost(m.List, "Speedy Bites", "@speedybites", "k:Speedy Bites", FlareAge(2, 'h'),
                t("reallife_flare_biz_post_0", "Two for one at {name} today only.").Replace("{name}", "Speedy Bites"), FlareFigures.Count(FlareSpeedy[0]), FlareFigures.Count(FlareSpeedy[1]),
                FlareFigures.Count(FlareSpeedy[2]), FlareFigures.Count(FlareSpeedy[3]));
            p.Flags |= FlareRowFlags.Verified;
            p.Tags = new[] { "#BroadwayNights", "#MidtownEats" };
            p.Picture = "sample:p1";
            ref FlareRow q = ref FlarePost(m.List, "Ana Ruiz", "@anaruiz", "c12", FlareAge(4, 'h'),
                t("reallife_flare_inf_food_2", "Coffee crawl through {district}. My feet hurt and I regret nothing.").Replace("{district}", "Midtown"), "41", "3", "1", "610");
            q.Tags = new[] { "#BroadwayNights" };
            return;
        }
        if (search != null)
        {
            // A search for "burger": the account whose name has it, the tag, and the post, each once; the side leaves out the tag and the account.
            m.List.EmptyTitle = t("reallife_flare_kit_empty_search", "Nothing matches");
            FlareSideWithout(m, "BurgerWeek", "Burger Barn");
            FlareSection(m.List, t("reallife_flare_tab_accounts", "Accounts"));
            FlareAccount(m.List, t, "Burger Barn", "@burgerbarn", "k:Burger Barn", t("reallife_flare_kit_kind_business", "Business"), false, false);
            FlareSection(m.List, t("reallife_flare_kit_section_tags", "Tags"));
            FlareTrend(m.List, t, "BurgerWeek", "Garment District", FlareTagPosts[1]);
            FlareSection(m.List, t("reallife_flare_tab_posts", "Posts"));
            ref FlareRow p = ref FlarePost(m.List, "Burger Barn", "@burgerbarn", "k:Burger Barn", FlareAge(7, 'h'),
                t("reallife_flare_biz_post_0", "Two for one at {name} today only.").Replace("{name}", "Burger Barn"), "203", "19", "6", "2.7K");
            p.Tags = new[] { "#BurgerWeek" };
            return;
        }
        m.List.EmptyTitle = t("reallife_flare_kit_empty_explore", "Nothing trending yet");
        // Nothing asked: what trends, all of it in the page, so the side keeps its room for the week and who to follow; then where the city posted, which no tag or account says.
        FlareSection(m.List, t("reallife_flare_city_trend_tags", "Trending tags"));
        for (int i = 0; i < FlareTagNames.Length; i++)
        {
            FlareTrend(m.List, t, FlareTagNames[i], FlareTagWhere[i], FlareTagPosts[i]);
        }
        m.Trends.Clear();
        FlareSideWithout(m, null);
        FlareSection(m.List, t("reallife_flare_kit_section_busy", "Where the city posted today"));
        for (int i = 0; i < _world.PostingDistricts.Length; i++)
        {
            FlareCard(m.List, UiIcon.MapPin, UiRole.AccentText, _world.PostingDistricts[i], null, FlareFigures.PostsCount(_world.PostingCounts[i], t), null, null);
        }
    }

    private static void FlareAccount(FlareList list, UiWords t, string name, string handle, string key, string about, bool verified, bool following)
    {
        ref FlareRow r = ref FlareRowOf(list, FlareRowKind.Account, name, key);
        r.Handle = FlareAccountLine(t, handle, about);
        r.Flags |= (verified ? FlareRowFlags.Verified : FlareRowFlags.None) | (following ? FlareRowFlags.Following : FlareRowFlags.None);
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
            m.HeadLine = FlareFigures.NewHead(0, t);
            return;
        }
        FlareFunnelWorld thanks = _world.Funnels[0];
        string[] reviewed = FlareReviewWords(t, FlareShop, 2, 3);
        m.Tabs = new[]
        {
            t("reallife_flare_kit_notice_tab_0", "All"), t("reallife_flare_kit_notice_tab_1", "Sales"), t("reallife_flare_kit_notice_tab_2", "Reviews"), t("reallife_flare_kit_notice_tab_3", "Mentions"),
            t("reallife_flare_kit_notice_tab_4", "Deals")
        };
        m.Tab = 0;
        m.MarkAll = t("reallife_flare_kit_mark_all", "Mark all read");
        FlareSection(m.List, t("reallife_flare_kit_needs_title", "Needs your answer"));
        FlareCard(m.List, UiIcon.Star, UiRole.Gold, "Ana Ruiz", FlareAge(18, 'm'), reviewed[0] + " " + reviewed[1], FlareShop, t("reallife_flare_kit_reply", "Reply"));
        FlareCard(m.List, UiIcon.Coin, UiRole.Gold, "Glow Cosmetics", null,
            t("reallife_flare_kit_offer_card", "Pays {money} a post for {posts} posts").Replace("{money}", FlareMoney(_world.OfferFee)).Replace("{posts}", FlareFigures.N(_world.OfferPosts)),
            t("reallife_flare_kit_offer_until", "Open for {days} more days").Replace("{days}", "4"), t("reallife_flare_kit_view", "View"));
        FlareSection(m.List, t("reallife_flare_nav_notices", "Notifications"));
        FlareNotice(m.List, UiIcon.Megaphone, UiRole.Text2, true, FlareAge(55, 'm'), t("reallife_flare_kit_notice_rival", "{business} is running a promoted post in {district}, {count} people reached")
            .Replace("{business}", "Burger Barn").Replace("{district}", "Midtown").Replace("{count}", "9,000"), null);
        // Jade Rivers' first post of the campaign running in the studio (31 visits so far): what this one brought is part of that
        FlareNotice(m.List, UiIcon.Megaphone, UiRole.Chart2, true, FlareAge(40, 'm'),
            t("reallife_flare_kit_notice_campaign", "{name} posted for {business}: {visitors} visitors, {money}").Replace("{name}", "Jade Rivers").Replace("{business}", FlareShop)
                .Replace("{visitors}", "22").Replace("{money}", "$540"), null);
        FlareNotice(m.List, UiIcon.Heart, UiRole.CatRetail, true, FlareAge(1, 'h'), t("reallife_flare_notice_like", "{name} liked your post").Replace("{name}", "Alex"), FlareMyWords(t, "thanks"));
        m.List.Rows[m.List.Count - 1].Value = t("reallife_flare_kit_brought_short", "{visitors} visits, {money}").Replace("{visitors}", FlareFigures.N(thanks.Came))
            .Replace("{money}", FlareFigures.SignedMoney(thanks.Dollars, FlareMoney));
        m.List.Rows[m.List.Count - 1].Flags |= FlareRowFlags.Good;
        FlareNotice(m.List, UiIcon.People, UiRole.OkText, true, FlareAge(1, 'h'), t("reallife_flare_kit_notice_gained", "{count} followers came from your {business} post")
            .Replace("{count}", FlareFigures.N(thanks.Followed)).Replace("{business}", FlareShop), null);
        FlareNotice(m.List, UiIcon.Fire, UiRole.Chart2, false, FlareAge(2, 'h'), t("reallife_flare_kit_notice_trend_part", "#{tag} is trending and your post is part of it").Replace("{tag}", "BroadwayNights"), null);
        FlareNotice(m.List, UiIcon.People, UiRole.AccentText, true, FlareAge(2, 'h'), t("reallife_flare_kit_notice_follow", "{count} new followers").Replace("{count}", "23"), null);
        FlareNotice(m.List, UiIcon.Message, UiRole.AccentText, false, FlareAge(3, 'h'), t("reallife_flare_notice_reply", "{name} replied to your post").Replace("{name}", "Tom Hart"),
            t("reallife_flare_cit_reply_kind_1", "Hope you are doing all right."));
        FlareNotice(m.List, UiIcon.ChartLineUp, UiRole.OkText, false, FlareAge(1, 'd'),
            t("reallife_flare_kit_notice_promo", "Your post about {business} brought {visitors} visitors, {money}").Replace("{business}", FlareShop).Replace("{visitors}", FlareFigures.N(thanks.Came))
                .Replace("{money}", FlareMoney(thanks.Dollars)), null);
        FlareNotice(m.List, UiIcon.ChartLineUp, UiRole.DangerText, false, FlareAge(4, 'd'),
            t("reallife_flare_kit_notice_promo_flop", "Your post about {business} fell flat").Replace("{business}", FlareShop2), null);
        string[] who = { "Marco Bell", "Priya Nair", "Sam Okafor", "Tess Whitaker", "Jade Rivers", "Omar Haddad", "Lila Chen", "Kenji Mori" };
        string[] visits = { "14", "6", "5", "9", "27", "7", "11", "8" };
        string[] money = { "$318", "$140", "$96", "$205", "$640", "$122", "$260", "$188" };
        for (int i = 0; i < who.Length; i++)
        {
            FlareNotice(m.List, UiIcon.Megaphone, UiRole.Chart2, false, FlareAge(5 + i, 'd'),
                t("reallife_flare_kit_notice_campaign", "{name} posted for {business}: {visitors} visitors, {money}").Replace("{name}", who[i]).Replace("{business}", i % 2 == 0 ? FlareShop2 : FlareShop)
                    .Replace("{visitors}", visits[i]).Replace("{money}", money[i]), null);
        }
        // the head counts the unread lines of the page itself (the cards above them wait for an answer and are not counted)
        int unread = 0;
        for (int i = 0; i < m.List.Count; i++)
        {
            unread += m.List.Rows[i].Kind == FlareRowKind.Notice && (m.List.Rows[i].Flags & FlareRowFlags.Unread) != 0 ? 1 : 0;
        }
        m.HeadLine = FlareFigures.NewHead(unread, t);
    }

    // A review's words by its stars and what it was about, the way the business page and the answer card say it: the stars' words, then the reason.
    private static string[] FlareReviewWords(UiWords t, string shop, int stars, int why) => new[]
    {
        t("reallife_flare_review_post_" + stars.ToString(System.Globalization.CultureInfo.InvariantCulture), FlareStarsEnglish[stars - 1]).Replace("{name}", shop),
        stars >= 3 ? t("reallife_flare_kit_why_" + why.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_good", FlareWhyGood[why])
            : t("reallife_flare_kit_why_" + why.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_poor", FlareWhyPoor[why])
    };

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
        int unread = FlareUnreadMessages - (open == null ? 0 : 1);   // the conversation open is read: the badge and the head count the others
        m.Badges[FlareFace.Messages] = unread;
        m.HeadLine = FlareFigures.UnreadHead(unread, t);
        m.Search = string.Empty;
        m.InboxHint = t("reallife_flare_kit_inbox_hint", "Search messages");
        m.List.EmptyIcon = UiIcon.Message;
        m.List.EmptyTitle = t("reallife_flare_empty_messages", "No messages yet.");
        m.List.EmptyBody = t("reallife_flare_kit_empty_messages_body", "Residents, businesses and influencers write to you here.");
        m.Detail.EmptyTitle = t("reallife_flare_kit_pick_thread", "Pick a conversation");
        m.Detail.EmptyBody = t("reallife_flare_kit_pick_thread_body", "Your messages open here.");
        string complaint = t("reallife_flare_dm_complaint", "I left a poor review of {business}. The visit really let me down. Will anything change?").Replace("{business}", FlareShop);
        string offer = t("reallife_flare_dm_offer", "Hello from {business}. We would pay {fee} a post for {posts} posts about us. Interested?")
            .Replace("{business}", "Glow Cosmetics").Replace("{fee}", FlareMoney(_world.OfferFee)).Replace("{posts}", FlareFigures.N(_world.OfferPosts));
        string you = t("reallife_flare_dm_you", "You:") + " ";
        FlareThreadRow(m.List, "Ana Ruiz", "c12", FlareAge(18, 'm'), complaint, FlareThreadUnread[0], open == "resident");
        FlareThreadRow(m.List, "Glow Cosmetics", "d:7", FlareAge(1, 'h'), offer, FlareThreadUnread[1], open == "offer");
        FlareThreadRow(m.List, "Jade Rivers", "i:3", FlareAge(3, 'h'),
            t("reallife_flare_dm_campaign_post", "My post for {business} is up. It brought {visitors} people in, about {money} spent.").Replace("{business}", FlareShop)
                .Replace("{visitors}", "22").Replace("{money}", "$540"), FlareThreadUnread[2], false);
        FlareThreadRow(m.List, "Speedy Bites", "k:Speedy Bites", FlareAge(1, 'd'), you + t("reallife_flare_kit_rival_calm",
            "We wish every business on the street well. Come in and judge us for yourself, the door is open."), FlareThreadUnread[3], false);
        FlareThreadRow(m.List, "Alex", "f1", FlareAge(2, 'd'), t("reallife_flare_dm_fan", "Your post was everywhere today. I had to say hello."), FlareThreadUnread[4], false);
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
            m.DetailLine = t("reallife_flare_kit_kind_sponsor", "Sponsor");   // who writes; the offer itself is the bubble and the button
            FlareBubble(m.Detail, offer, FlareAge(1, 'h'), false);
            m.Quick = new[]
            {
                t("reallife_flare_kit_accept", "Accept {money}").Replace("{money}", FlareMoney((long)_world.OfferFee * _world.OfferPosts)), t("reallife_flare_kit_decline", "Decline")
            };
            return;   // an offer is answered by its two buttons
        }
        m.DetailName = "Ana Ruiz";
        m.DetailHue = UiColour.HueOf("c12");
        m.DetailPerson = true;
        m.DetailLine = t("reallife_flare_kit_kind_resident", "Resident");
        m.DetailLink = t("reallife_flare_kit_view_profile", "Profile");
        FlareBubble(m.Detail, t("reallife_flare_dm_praise", "Just wanted to say {business} was lovely. I will be back soon.").Replace("{business}", FlareShop), FlareAge(6, 'd'), false);
        FlareBubble(m.Detail, t("reallife_flare_kit_quick_chat_1", "Good to hear from you."), FlareAge(6, 'd'), true);
        FlareBubble(m.Detail, complaint, FlareAge(18, 'm'), false);
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
        r.Flags |= (unread > 0 && !chosen ? FlareRowFlags.Unread : FlareRowFlags.None) | (chosen ? FlareRowFlags.Chosen : FlareRowFlags.None);
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
        if (me)
        {
            FlareSideWithout(m, null);
            // the profile's own head says the followers: the week beside it leaves them out, with their line
            FlareFigures.Week(_world.Week(), t, true, out m.WeekLabels, out m.WeekFigures, out m.WeekDeltas, out m.WeekTones);
            m.Line = null;
        }
        else
        {
            FlareSideWithout(m, null, "Jade Rivers");
        }
        m.Tabs = new[] { t("reallife_flare_tab_posts", "Posts"), t("reallife_flare_tab_replies", "Replies"), t("reallife_flare_kit_tab_media", "Media") };
        m.List.EmptyIcon = UiIcon.Message;
        m.List.EmptyTitle = t("reallife_flare_empty_profile", "No posts yet.");
        if (me)
        {
            h.Name = FlareMe;
            h.Handle = FlareMyHandle + " \u00b7 " + t("reallife_flare_kit_joined", "Joined day {day}").Replace("{day}", "12");
            h.Edit = t("reallife_flare_kit_edit_profile", "Edit profile");
            h.Bio = "Owner of Bloom Cafe and Bloom Bakery in Murray Hill. Coffee first, then everything else.";
            h.Hue = UiColour.HueOf("me");
            h.Person = true;
            h.Ring = UiRole.Grade4;
            h.Banner = "sample:p3";
            h.Figures = new[] { FlareFigures.Count(_world.Followers), FlareFigures.N(_world.Following), FlareFigures.N(_world.PostsTotal) };
            h.Labels = new[] { t("reallife_flare_kit_followers", "Followers"), t("reallife_flare_kit_following", "Following"), t("reallife_flare_kit_posts", "Posts") };
            m.List.EmptyBody = t("reallife_flare_kit_empty_profile_body", "Your posts show here with what they brought.");
            // every post of the world, newest first, each with the counts of the record it is
            for (int i = 0; i < _world.Mine.Length; i++)
            {
                FlareMyPost post = _world.Mine[i];
                ref FlareRow r = ref FlareMine(m.List, t, post);
                if (post.Id == "thanks")
                {
                    r.Flags |= FlareRowFlags.Good;
                    r.Line = t("reallife_flare_kit_promoted", "Promoted in {district}").Replace("{district}", "Murray Hill");
                    r.Icon = UiIcon.Megaphone;
                    r.Value = FlareBrought(t, _world.Funnels[post.FunnelAt]);
                    r.Picture = "sample:p3";
                }
                else if (post.Flop)
                {
                    r.Flags |= FlareRowFlags.Bad;
                    r.Value = t("reallife_flare_kit_result_flop", "Fell flat");
                }
            }
            return;
        }
        h.Name = "Jade Rivers";
        h.Handle = "@jaderivers";
        h.Bio = t("reallife_flare_inf_food_2", "Coffee crawl through {district}. My feet hurt and I regret nothing.").Replace("{district}", "Murray Hill");
        h.Hue = UiColour.HueOf("i:3");
        h.Person = true;
        h.Ring = UiTokens.Grade(92);
        h.Verified = true;
        h.Banner = "sample:p1";
        h.Figures = new[] { FlareFigures.Count(FlareJadeFollowers), FlareFigures.N(FlareJadePosts) };
        h.Labels = new[] { t("reallife_flare_kit_followers", "Followers"), t("reallife_flare_kit_posts", "Posts") };
        h.Follow = t("reallife_flare_follow", "Follow");
        ref FlareRow p = ref FlarePost(m.List, "Jade Rivers", "@jaderivers", "i:3", FlareAge(1, 'd'),
            t("reallife_flare_inf_food_1", "Best plate I have had in {district} this month.").Replace("{district}", "Murray Hill"), FlareFigures.Count(FlareJadePost[0]),
            FlareFigures.Count(FlareJadePost[1]), FlareFigures.Count(FlareJadePost[2]), FlareFigures.Count(FlareJadePost[3]));
        p.Flags |= FlareRowFlags.Verified;
        p.Line = t("reallife_flare_kit_partnership", "Paid partnership with {business}").Replace("{business}", FlareShop);
        p.Icon = UiIcon.Briefcase;
        p.Picture = "sample:p2";
    }

    // Two replies are listed under Speedy Bites' post; the rest of its 84 are the "more" line, so the card's count is the list's.
    private const int FlareListedReplies = 2;

    private static void FlareThread(FlareFaceModel m, UiWords t)
    {
        m.Page = FlareFace.Thread;
        m.Head = t("reallife_flare_kit_post_title", "Post");
        FlareSideWithout(m, null, "Speedy Bites");   // the post's author
        int more = FlareSpeedy[1] - FlareListedReplies;
        ref FlareRow p = ref FlarePost(m.List, "Speedy Bites", "@speedybites", "k:Speedy Bites", FlareAge(2, 'h'),
            t("reallife_flare_biz_post_0", "Two for one at {name} today only.").Replace("{name}", "Speedy Bites"), FlareFigures.Count(FlareSpeedy[0]),
            FlareFigures.Count(FlareFigures.Replies(FlareListedReplies, more)), FlareFigures.Count(FlareSpeedy[2]), FlareFigures.Count(FlareSpeedy[3]));
        p.Flags |= FlareRowFlags.Verified | FlareRowFlags.Liked;
        p.Tags = new[] { "#BroadwayNights", "#MidtownEats" };
        p.Picture = "sample:p1";
        FlareSection(m.List, t("reallife_flare_replies_title", "Replies"));
        ref FlareRow a = ref FlarePost(m.List, "Ana Ruiz", "@anaruiz", "c12", FlareAge(1, 'h'), t("reallife_flare_cit_reply_agree_0", "Same here. Somebody has to say it."), "12", null, null, null);
        a.Flags |= FlareRowFlags.Comment;
        ref FlareRow b = ref FlarePost(m.List, "Tom Hart", "@tomhart", "c40", FlareAge(50, 'm'), t("reallife_flare_cit_reply_kind_1", "Hope you are doing all right."), "3", null, null, null);
        b.Flags |= FlareRowFlags.Comment | FlareRowFlags.Liked;
        FlareSection(m.List, t("reallife_flare_comments_more", "And {count} more replies.").Replace("{count}", FlareFigures.N(more)));
        m.Field = string.Empty;
        m.FieldHint = t("reallife_flare_reply_placeholder", "Post your reply");
        m.FieldArg = FlareFace.ReplyField;
        m.Send = t("reallife_flare_reply_button", "Reply");
    }

    // The player's own post: what it did in the city under it (FlareScreen.FunnelRow), then the replies, all of them (a post of the player's lists every reply it has).
    private static void FlareThreadMine(FlareFaceModel m, UiWords t)
    {
        m.Page = FlareFace.Thread;
        m.Head = t("reallife_flare_kit_post_title", "Post");
        FlareSideWithout(m, null);
        FlareMyPost post = FlareMy("thanks");
        FlareFunnelWorld did = _world.Funnels[post.FunnelAt];
        ref FlareRow p = ref FlareMine(m.List, t, post);
        p.Flags |= FlareRowFlags.Good;
        p.Picture = "sample:p3";
        FlareSection(m.List, t("reallife_flare_kit_funnel_title", "What it did"));
        FlareResult(m.List, UiIcon.ChartLineUp, UiRole.OkText, FlareFigures.FunnelSaid(did.Seen, did.Came, false, t), FlareFigures.FunnelMore(did.Interested, did.Followed, t),
            FlareFigures.SignedMoney(did.Dollars, FlareMoney), 1);
        FlareSection(m.List, t("reallife_flare_replies_title", "Replies"));
        ref FlareRow a = ref FlarePost(m.List, "Ana Ruiz", "@anaruiz", "c12", FlareAge(20, 'h'), t("reallife_flare_cit_reply_agree_0", "Same here. Somebody has to say it."), "12", null, null, null);
        a.Flags |= FlareRowFlags.Comment;
        ref FlareRow b = ref FlarePost(m.List, "Tom Hart", "@tomhart", "c40", FlareAge(9, 'h'), t("reallife_flare_cit_reply_kind_1", "Hope you are doing all right."), "3", null, null, null);
        b.Flags |= FlareRowFlags.Comment | FlareRowFlags.Liked;
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
        FlareShopWorld shop = _world.Shops[turning ? 1 : 0];
        b.Picker = new[] { _world.Shops[0].Name, _world.Shops[1].Name };
        b.Chosen = turning ? 1 : 0;
        m.HeadLine = t("reallife_flare_kit_business_line", "{kind} in {district}").Replace("{kind}", t("reallife_flare_niche_0", "Food")).Replace("{district}", "Murray Hill")
            + " \u00b7 12 Lexington Avenue \u00b7 " + t("reallife_flare_kit_open_hours", "open about {hours} hours a day").Replace("{hours}", "12") + " \u00b7 "
            + t("reallife_flare_kit_opened", "opened day {day}").Replace("{day}", "9");
        b.Promote = t("reallife_flare_kit_promote_ad", "Run an ad");
        b.ReachTitle = t("reallife_flare_kit_reach_title", "Your reach each day");
        b.ReachEmpty = t("reallife_flare_kit_reach_empty", "Not enough days yet.");
        // the account's reach a day from the posts of the world, only the days from the first one that reached anyone (FlareScreen.ReachLine)
        float[] days = _world.ReachDays();
        int first = 0;
        while (first < days.Length && days[first] <= 0f)
        {
            first++;
        }
        b.Reach = new float[days.Length - first];
        Array.Copy(days, first, b.Reach, 0, b.Reach.Length);
        b.Rival = t("reallife_flare_kit_rival_line", "{business} is rated {stars} stars in the same district").Replace("{business}", "Speedy Bites").Replace("{stars}", "4.1");
        b.RatingTitle = t("reallife_flare_kit_rating", "Rating");
        b.Rating = FlareFigures.Stars(shop.StarsMilli);
        b.Halves = shop.Halves;
        b.RatingLine = FlareFigures.ReviewsCount(shop.Reviews, t);
        b.Bars = shop.Bars;
        b.VisitsTitle = t("reallife_flare_kit_visits_title", "From Flare, 7 days");
        b.Figures = new[]
        {
            FlareFigures.N(shop.Visitors), FlareMoney(shop.Dollars), FlareFigures.Count(shop.CityFollowers),
            turning ? t("reallife_flare_kit_campaign_none", "None running") : t("reallife_flare_kit_campaign_percent", "+{percent}% customers").Replace("{percent}", "8")
        };
        b.FigureLabels = new[]
        {
            t("reallife_flare_kit_fig_visitors", "Visitors"), t("reallife_flare_kit_fig_spent", "Spent there"), t("reallife_flare_kit_fig_city_followers", "Followers in the city"),
            t("reallife_flare_kit_fig_campaign", "Campaign")
        };
        b.Warning = shop.TurningNow > 0
            ? t("reallife_flare_kit_turning", "Reviews are turning: {now} stars lately, against {before} before. Answer the poor ones.").Replace("{now}", FlareFigures.Stars(shop.TurningNow))
                .Replace("{before}", FlareFigures.Stars(shop.TurningBefore))
            : null;
        b.ReviewsTitle = t("reallife_flare_kit_reviews_title", "Reviews");
        m.List.EmptyIcon = UiIcon.Star;
        m.List.EmptyTitle = t("reallife_flare_kit_no_reviews", "No reviews yet");
        m.List.EmptyBody = t("reallife_flare_kit_no_reviews_body", "Reviews come in as residents visit.");
        string name = shop.Name;
        FlareReview(m.List, t, "Ana Ruiz", 2, FlareAge(18, 'm'), name, 3, null);
        FlareReview(m.List, t, "Tom Hart", 5, FlareAge(2, 'h'), name, 2, null);
        FlareReview(m.List, t, "Lena Fox", 1, FlareAge(1, 'd'), name, 1, t("reallife_flare_reply_sorry", "We are sorry your visit was not right. Thank you for telling us, we are putting it right."));
        FlareReview(m.List, t, "Ravi Shah", 4, FlareAge(2, 'd'), name, 4, null);
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
        r.Flags |= FlareRowFlags.Person;
        r.Ring = FlareFace.StarRing(stars);   // how they left
        r.Stars = stars;
        r.Time = time;
        string n = stars.ToString(System.Globalization.CultureInfo.InvariantCulture), w = why.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string about = stars >= 3 ? t("reallife_flare_kit_why_" + w + "_good", FlareWhyGood[why]) : t("reallife_flare_kit_why_" + w + "_poor", FlareWhyPoor[why]);
        r.Text = t("reallife_flare_review_post_" + n, FlareStarsEnglish[stars - 1]).Replace("{name}", shop) + " " + about;
        if (answer != null)
        {
            r.Flags |= FlareRowFlags.Answered;
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
        // No line under the head: the first tile is the followers and the sponsorship card is the tier, as Live has it.
        FlareFigures.StudioTiles(_world.Week(), _world.Visitors, _world.VisitorsBefore, _world.Dollars, _world.DollarsBefore, t, FlareMoney, st);
        st.CreatorTitle = t("reallife_flare_kit_creator_title", "Creator income");
        st.Creator = "$1,240";
        st.CreatorLine = t("reallife_flare_kit_creator_paid", "Paid last time. {total} paid in all. Next in {days} days.").Replace("{total}", "$3,720").Replace("{days}", "12");
        st.LineTitle = t("reallife_flare_kit_line_title", "Followers this week");
        st.Line = _world.Line();
        st.TierTitle = t("reallife_flare_kit_tier_title", "Sponsorship");
        st.Tier = t("reallife_flare_tier_1", "Local");
        st.TierShare = (_world.Followers - 500f) / 4500f;
        st.TierLine = t("reallife_flare_kit_tier_next", "{count} more followers to {tier}").Replace("{count}", FlareFigures.Count(5000 - _world.Followers)).Replace("{tier}", t("reallife_flare_tier_2", "Known"));
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
        FlareFunnelWorld viral = _world.Funnels[FlareMy("thanks").FunnelAt];
        FlareSection(m.List, t("reallife_flare_kit_results_posts", "Your posts"));
        FlareResult(m.List, UiIcon.Megaphone, UiRole.Chart2, t("reallife_flare_kit_result_viral", "Your post about {business} went viral: {visitors} visitors").Replace("{business}", FlareShop)
            .Replace("{visitors}", FlareFigures.N(viral.Came)), t("reallife_flare_kit_result_done", "Done, {time}").Replace("{time}", FlareAge(1, 'd')), FlareFigures.SignedMoney(viral.Dollars, FlareMoney), 1);
        FlareSection(m.List, t("reallife_flare_kit_results_ads", "Your ads"));
        for (int i = 0; i < _world.AdFunnel.Length; i++)
        {
            FlareFunnelWorld ad = _world.Funnels[_world.AdFunnel[i]];
            bool off = ad.Came < 0 || ad.Dollars < 0;
            FlareResult(m.List, UiIcon.Megaphone, off ? UiRole.DangerText : UiRole.OkText, t("reallife_flare_kit_result_ad", "Your ad for {business}").Replace("{business}", _world.Shops[i].Name),
                FlareFigures.FunnelSaid(ad.Seen, ad.Came, off, t), FlareFigures.SignedMoney(ad.Dollars, FlareMoney), off ? -1 : 1);
        }
        FlareSection(m.List, t("reallife_flare_kit_results_campaigns", "Campaigns"));
        FlareResult(m.List, UiIcon.Megaphone, UiRole.Chart2, t("reallife_flare_kit_campaign_running", "{handle} for {business}").Replace("{handle}", "@jaderivers").Replace("{business}", FlareShop),
            t("reallife_flare_kit_campaign_day", "Day {day} of {days}").Replace("{day}", "3").Replace("{days}", "7") + " \u00b7 " + t("reallife_flare_kit_campaign_so_far", "{visitors} visits, {money} so far").Replace("{visitors}", "31").Replace("{money}", "+$760"),
            t("reallife_flare_kit_campaign_percent", "+{percent}% customers").Replace("{percent}", "8"), 0);
        m.List.Rows[m.List.Count - 1].Button = t("reallife_flare_kit_end_campaign", "End");
        FlareResult(m.List, UiIcon.Megaphone, UiRole.OkText, t("reallife_flare_kit_campaign_brought", "{handle} brought {visitors} visitors to {business}").Replace("{handle}", "@marcobell")
            .Replace("{visitors}", "64").Replace("{business}", FlareShop2), t("reallife_flare_kit_result_done", "Done, {time}").Replace("{time}", FlareAge(9, 'd')), "+$1,540", 1);
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
        m.HeadLine = null;   // set once the table is laid, from its rows
        m.Business.Picker = new[] { t("reallife_flare_kit_all_districts", "All districts"), "Midtown", "Murray Hill", "Garment District" };
        m.Business.Chosen = 0;
        m.Business.Picker2 = new[] { t("reallife_flare_kit_all_niches", "Every niche"), t("reallife_flare_niche_0", "Food"), t("reallife_flare_niche_1", "Fashion"), t("reallife_flare_niche_2", "Fitness"), t("reallife_flare_niche_3", "Night life") };
        m.Business.Chosen2 = 0;
        m.Business.Sort = new[] { t("reallife_flare_kit_sort_followers", "Most followers"), t("reallife_flare_kit_sort_fit", "Best fit") };
        m.Business.SortChosen = 1;
        m.List.EmptyIcon = UiIcon.People;
        m.List.EmptyTitle = t("reallife_flare_kit_no_influencers", "No influencers here");
        m.List.EmptyBody = t("reallife_flare_kit_no_influencers_body", "Try every district, or come back in a few days.");
        FlareInfluencer(m.List, t, "Jade Rivers", "@jaderivers", "i:3", 0, "Murray Hill", "96K", 410, 100, "$2,400", "38", true, false).Flags |= FlareRowFlags.Chosen;
        FlareInfluencer(m.List, t, "Marco Bell", "@marcobell", "i:5", 3, "Midtown", "18.2K", 630, 74, "$620", "14", false, true);
        FlareInfluencer(m.List, t, "Priya Nair", "@priyanair", "i:8", 1, "Garment District", "9.4K", 780, 58, "$340", "6", false, false);
        FlareInfluencer(m.List, t, "Sam Okafor", "@samokafor", "i:11", 2, "Murray Hill", "3.1K", 920, 58, "$150", "5", false, false);
        FlareInfluencer(m.List, t, "Tess Whitaker", "@tesswhitaker", "i:14", 0, "Midtown", "27.5K", 490, 52, "$540", "9", false, false);
        FlareInfluencer(m.List, t, "Omar Haddad", "@omarhaddad", "i:17", 2, "Murray Hill", "19.8K", 520, 47, "$410", "7", false, false);
        m.HeadLine = t("reallife_flare_kit_hire_line", "{count} influencers for {business}").Replace("{count}", FlareFigures.N(m.List.Count)).Replace("{business}", FlareShop);
        FlareHireCard(m, t);
    }

    // The card of the influencer chosen in the table, as FlareScreen.HireCard fills it.
    private static void FlareHireCard(FlareFaceModel m, UiWords t)
    {
        FlareHireModel d = m.Hire;
        d.Shown = true;
        d.Name = "Jade Rivers";
        d.Hue = UiColour.HueOf("i:3");
        d.Ring = UiTokens.Grade(92);
        d.Verified = true;
        d.Row = 0;
        d.ForecastLabels = new[] { t("reallife_flare_kit_fig_takings", "Likely takings") };
        d.ForecastFigures = new[] { "+$2,150" };
        d.Hire = t("reallife_flare_kit_hire_pay", "Hire for {money}").Replace("{money}", "$2,400");
        d.Note = t("reallife_flare_kit_hire_card_fits", "Their followers live near your business. {flop} in 100 posts like this fall flat, {viral} in 100 go viral.").Replace("{flop}", "12")
            .Replace("{viral}", "9");
        d.AudienceTitle = t("reallife_flare_kit_audience_title", "Audience by district");
        d.AudienceLabels = new[] { "Murray Hill", "Midtown", "Garment District", "Chelsea" };
        d.AudienceFigures = new[] { "34%", "31%", "14%", "9%" };
        d.AudienceShares = new[] { 1f, 0.91f, 0.41f, 0.26f };
        d.LastTitle = t("reallife_flare_kit_hire_last", "Last campaigns");
        // the ages as FlareScreen.Ago writes them: a short one from the keyed template, one past a week as its day
        d.LastLines = new[] { "Burger Barn, " + FlareAge(2, 'd'), "Harbourside Cafe, " + FlareAge(5, 'd'), "Moonlight Bar, " + t("reallife_flare_day", "Day {day}").Replace("{day}", "12") };
        d.LastFigures = new[] { t("reallife_flare_kit_visitors_n", "{count} visitors").Replace("{count}", "131"), t("reallife_flare_kit_visitors_n", "{count} visitors").Replace("{count}", "74"),
            t("reallife_flare_kit_result_flop", "Fell flat") };
        d.LastTones = new[] { 1, 1, -1 };
    }

    private static readonly string[] FlareNiches = { "Food", "Fashion", "Fitness", "Night life" };

    private static ref FlareRow FlareInfluencer(FlareList list, UiWords t, string name, string handle, string key, int niche, string district, string followers, int engagement, int fit, string price,
        string visitors, bool verified, bool booked)
    {
        ref FlareRow r = ref FlareRowOf(list, FlareRowKind.Influencer, name, key);
        r.Handle = t("reallife_flare_kit_influencer_line", "{handle}, {niche}, {district}").Replace("{handle}", handle)
            .Replace("{niche}", t("reallife_flare_niche_" + niche.ToString(System.Globalization.CultureInfo.InvariantCulture), FlareNiches[niche])).Replace("{district}", district);
        r.Flags |= (verified ? FlareRowFlags.Verified : FlareRowFlags.None) | (booked ? FlareRowFlags.Off : FlareRowFlags.None);
        r.Tags = new[]
        {
            t("reallife_flare_kit_fig_followers", "Followers"), followers, t("reallife_flare_kit_fig_engagement", "Engagement"),
            (engagement / 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
            t("reallife_flare_kit_fig_fit", "Fit"), fit.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%", t("reallife_flare_kit_fig_price", "A post"), price,
            t("reallife_flare_kit_fig_expected", "Likely visits"), t("reallife_flare_kit_visitors_n", "{count} visitors").Replace("{count}", visitors)
        };
        r.Shares = new[] { -1f, Math.Min(1f, engagement / 1200f), fit / 100f, -1f, -1f };
        r.Button = booked ? t("reallife_flare_kit_booked", "Booked") : t("reallife_flare_kit_hire", "Hire");
        return ref r;
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
        FlareCityModel band = m.City;
        band.Shown = true;
        band.DonutTitle = t("reallife_flare_kit_donut_title", "Followers by district");
        band.DonutEmpty = t("reallife_flare_kit_donut_empty", "Nobody in the city follows you yet. Your followers show here once they do.");
        band.WeekTitle = t("reallife_flare_kit_city_week", "This week in the city");
        band.WeekLabels = new string[0];
        band.WeekFigures = new string[0];
        if (_world.NewPlayer)
        {
            // Nobody follows and nothing was measured: the page says so once, in the donut's words, with no week of zeros beside it (FlareScreen.CityBand).
            band.WeekTitle = null;
            return;
        }
        band.WeekLabels = new[] { t("reallife_flare_kit_fig_seen", "Saw your posts"), t("reallife_flare_kit_fig_visits", "Visits brought"), t("reallife_flare_kit_fig_takings", "Takings") };
        band.Names = _world.Districts;
        band.Values = new string[_world.Districts.Length];
        band.Shares = new float[_world.Districts.Length];
        for (int i = 0; i < _world.Districts.Length; i++)
        {
            band.Values[i] = FlareFigures.N(_world.FollowersByDistrict[i]);
            band.Shares[i] = _world.FollowersByDistrict[i];
        }
        band.Rest = _world.FollowersElsewhere;
        band.DonutRest = t("reallife_flare_kit_donut_rest", "{count} more in other districts").Replace("{count}", FlareFigures.N(_world.FollowersElsewhere));
        _world.CityWeek(out int seen, out int visits, out int dollars);
        band.WeekFigures = new[] { FlareFigures.Count(seen), FlareFigures.N(visits), FlareFigures.SignedMoney(dollars, FlareMoney) };
        FlareSideWithout(m, null, "Speedy Bites", "Thrifty Threads");   // the rivals' cards name them
        FlareSection(m.List, t("reallife_flare_kit_city_demand", "Trending demand"));
        FlareCard(m.List, UiIcon.ChartLineUp, UiRole.OkText,
            t("reallife_flare_city_trend_district", "{what} in {district}").Replace("{what}", t("reallife_flare_city_trend_kind_3", "Eating out")).Replace("{district}", "Murray Hill"), null,
            t("reallife_flare_kit_trend_card", "Now {stage}: {moved} residents moved by it, {days} days left.").Replace("{stage}", t("reallife_flare_city_trend_rising", "rising"))
                .Replace("{moved}", "1,380").Replace("{days}", "5"), "Murray Hill", t("reallife_flare_kit_promote_there", "Promote"));
        FlareSection(m.List, t("reallife_flare_kit_city_rivals", "Rival moves"));
        FlareCard(m.List, UiIcon.Warning, UiRole.DangerText, "Speedy Bites", FlareAge(3, 'h'), t("reallife_flare_kit_rival_attack", "Ran your business down in {district}").Replace("{district}", "Murray Hill"),
            t("reallife_flare_dm_rival", "We posted about {business} today. The street deserves better, and we said so.").Replace("{business}", FlareShop), t("reallife_flare_kit_answer", "Answer"));
        FlareCard(m.List, UiIcon.Megaphone, UiRole.Chart2, "Thrifty Threads", FlareAge(5, 'h'), t("reallife_flare_kit_rival_promo", "Paid to promote in {district}").Replace("{district}", "Midtown"),
            t("reallife_flare_biz_post_1", "New in at {name}. Come and see what is on the shelves.").Replace("{name}", "Thrifty Threads"), t("reallife_flare_kit_view", "View"));
        FlareSection(m.List, t("reallife_flare_kit_city_moves", "Shops and rents"));
        FlareCard(m.List, UiIcon.Store, UiRole.DangerText, t("reallife_flare_kit_move_outlet", "{by} opens a shop in {district}").Replace("{by}", "Orion Holdings").Replace("{district}", "Murray Hill"),
            FlareAge(2, 'd'), t("reallife_flare_kit_move_outlet_line", "It sells what your {business} sells.").Replace("{business}", FlareShop), null, null);
        FlareCard(m.List, UiIcon.Key, UiRole.AccentText, t("reallife_flare_kit_move_to_let", "To let in {district}").Replace("{district}", "Midtown"), null,
            t("reallife_flare_kit_move_to_let_line", "{count} places are empty and for rent.").Replace("{count}", "7"), null, null);
        FlareSection(m.List, t("reallife_flare_kit_city_events", "Events"));
        ref FlareRow e = ref FlareCard(m.List, UiIcon.Calendar, UiRole.AccentText, "Open mic at the Lantern", FlareAge(1, 'h'), "Music and a cheap round from eight. Bring a friend.", "Alex",
            t("reallife_flare_kit_going", "Going"));
        e.Flags |= FlareRowFlags.Chosen;
        FlareSection(m.List, t("reallife_flare_kit_city_work", "Offers and work"));
        FlareCard(m.List, UiIcon.Coin, UiRole.Gold, "Glow Cosmetics", null,
            t("reallife_flare_kit_offer_card", "Pays {money} a post for {posts} posts").Replace("{money}", FlareMoney(_world.OfferFee)).Replace("{posts}", FlareFigures.N(_world.OfferPosts)),
            t("reallife_flare_kit_offer_until", "Open for {days} more days").Replace("{days}", "4"), t("reallife_flare_kit_view", "View"));
    }

    // A new account: nobody follows it yet, but the city's own news is there for it, as FlareScreen.City fills it: what the residents want, the rivals' new shops and the premises to let.
    private static void FlareCityNew(FlareFaceModel m, UiWords t)
    {
        FlareSideWithout(m, null, "Thrifty Threads");   // its card names it
        FlareSection(m.List, t("reallife_flare_kit_city_demand", "Trending demand"));
        FlareCard(m.List, UiIcon.ChartLineUp, UiRole.OkText,
            t("reallife_flare_city_trend_district", "{what} in {district}").Replace("{what}", t("reallife_flare_city_trend_kind_3", "Eating out")).Replace("{district}", "Murray Hill"), null,
            t("reallife_flare_kit_trend_card", "Now {stage}: {moved} residents moved by it, {days} days left.").Replace("{stage}", t("reallife_flare_city_trend_rising", "rising"))
                .Replace("{moved}", "1,380").Replace("{days}", "5"), "Murray Hill", t("reallife_flare_kit_promote_there", "Promote"));
        FlareSection(m.List, t("reallife_flare_kit_city_rivals", "Rival moves"));
        FlareCard(m.List, UiIcon.Megaphone, UiRole.Chart2, "Thrifty Threads", FlareAge(5, 'h'), t("reallife_flare_kit_rival_promo", "Paid to promote in {district}").Replace("{district}", "Midtown"),
            t("reallife_flare_biz_post_1", "New in at {name}. Come and see what is on the shelves.").Replace("{name}", "Thrifty Threads"), t("reallife_flare_kit_view", "View"));
        FlareSection(m.List, t("reallife_flare_kit_city_moves", "Shops and rents"));
        FlareCard(m.List, UiIcon.Store, UiRole.DangerText, t("reallife_flare_kit_move_outlet", "{by} opens a shop in {district}").Replace("{by}", "Orion Holdings").Replace("{district}", "Murray Hill"),
            FlareAge(2, 'd'), t("reallife_flare_kit_move_outlet_line", "It sells what your {business} sells.").Replace("{business}", FlareShop), null, null);
        string[] districts = { "Midtown", "Murray Hill", "Garment District" };
        string[] counts = { "7", "4", "3" };
        for (int i = 0; i < districts.Length; i++)
        {
            FlareCard(m.List, UiIcon.Key, UiRole.AccentText, t("reallife_flare_kit_move_to_let", "To let in {district}").Replace("{district}", districts[i]), null,
                t("reallife_flare_kit_move_to_let_line", "{count} places are empty and for rent.").Replace("{count}", counts[i]), null, null);
        }
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
        k.BarsTitle = t("reallife_flare_kit_audience_title", "Audience by district");
        k.BarLabels = new[] { "Murray Hill", "Midtown", "Chelsea" };
        k.BarShares = new[] { 1f, 0.55f, 0.2f };
        k.BarFigures = new[] { "52%", "29%", "11%" };
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
        // the rating of the business now, and with this two star review counting for half as an apology makes it (the world's reviews: Bars hold the stars of each)
        int[] bars = _world.Shops[0].Bars;
        float stars = bars[0] * 5f + bars[1] * 4f + bars[2] * 3f + bars[3] * 2f + bars[4], total = _world.Shops[0].Reviews;
        k.Figures = new[]
        {
            (stars / total).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), ((stars - 1f) / (total - 0.5f)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
        };
        k.Note = t("reallife_flare_reply_sorry", "We are sorry your visit was not right. Thank you for telling us, we are putting it right.");
        k.Confirm = t("reallife_flare_kit_reply", "Reply");
    }

    // The notifications under a tab: the rows of that tab's kinds only (the sample marks them by their glyph).
    private static void FlareKeepOnly(FlareList list, int tab)
    {
        int keep = 0;
        for (int i = 0; i < list.Count; i++)
        {
            UiIcon icon = list.Rows[i].Icon;
            bool sales = list.Rows[i].Kind == FlareRowKind.Notice && (icon == UiIcon.Megaphone && list.Rows[i].Tint == UiRole.Chart2 || icon == UiIcon.ChartLineUp);
            if (list.Rows[i].Kind == FlareRowKind.Notice && sales)
            {
                list.Rows[keep++] = list.Rows[i];
            }
        }
        list.Count = keep;
        list.Measured = -1f;
    }

    // The compose sheet for a kind of post that is not a moment of now: what it is about, what it needs, the tone and a mention.
    private static void FlareComposeKind(FlareFaceModel m, UiWords t, int kind)
    {
        FlareSheet(m, t, FlareFace.ComposeSheet);
        FlareSheetModel k = m.Sheet0;
        k.Title = t("reallife_flare_kit_compose_title", "Create a post");
        k.Line = t("reallife_flare_kit_compose_as", "Posting as {name}, {handle}").Replace("{name}", FlareMe).Replace("{handle}", FlareMyHandle);
        k.Field = true;
        k.FieldText = kind == 2 ? t("reallife_flare_report_post_0", "Broken road in {district}. Somebody is going to get hurt.").Replace("{district}", "Murray Hill") : string.Empty;
        k.FieldHint = t("reallife_flare_kit_composer", "What's happening, {name}?").Replace("{name}", "Walter");
        k.FieldCount = t("reallife_flare_kit_count", "{count} of {most}").Replace("{count}", k.FieldText.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{most}", "200");
        var groups = new System.Collections.Generic.List<string> { t("reallife_flare_kit_group_about", "About") };
        var chips = new System.Collections.Generic.List<string[]>
        {
            new[] { t("reallife_flare_kind_now", "Now"), t("reallife_flare_kind_moment", "A moment"), t("reallife_flare_kind_report", "Report"), t("reallife_flare_kind_review", "Review"),
                t("reallife_flare_kind_announce", "Announce") }
        };
        var chosen = new System.Collections.Generic.List<int> { kind };
        if (kind == 2)
        {
            groups.Add(t("reallife_flare_kit_group_report", "Problem"));
            chips.Add(new[]
            {
                t("reallife_flare_report_0", "Broken road"), t("reallife_flare_report_1", "Street light out"), t("reallife_flare_report_2", "Crime"), t("reallife_flare_report_3", "Litter"),
                t("reallife_flare_report_4", "Unsafe place"), t("reallife_flare_report_5", "A business"), t("reallife_flare_report_6", "The buses"), t("reallife_flare_report_7", "Power or water")
            });
            chosen.Add(0);
        }
        else
        {
            groups.Add(t("reallife_flare_kit_group_moment", "Moment"));
            // the moments the phone's life drafts, in the words the screen gives them (FlareText.Draft)
            chips.Add(new[]
            {
                t("reallife_flare_draft_selfie_with", "A selfie with {friend}").Replace("{friend}", "Alex"),
                t("reallife_flare_draft_outing", "Out for {kind} with {friend}").Replace("{kind}", t("reallife_flare_outing_4", "a walk")).Replace("{friend}", "Mom"),
                t("reallife_flare_draft_photo_with", "A picture with {friend}").Replace("{friend}", "Mom")
            });
            chosen.Add(0);
            groups.Add(t("reallife_flare_kit_group_friend", "Friend"));
            chips.Add(new[] { t("reallife_flare_kit_no_tag", "Do not tag"), t("reallife_flare_tag_yes", "Tag {friend}").Replace("{friend}", "Alex") });
            chosen.Add(1);
        }
        groups.Add(t("reallife_flare_kit_group_tone", "Tone"));
        chips.Add(new[] { t("reallife_flare_tone_0", "Cheerful"), t("reallife_flare_tone_1", "Proud"), t("reallife_flare_tone_2", "Honest"), t("reallife_flare_tone_3", "Cheeky") });
        chosen.Add(kind == 2 ? 2 : 0);
        groups.Add(t("reallife_flare_kit_group_mention", "Mention"));
        chips.Add(new[] { t("reallife_flare_kit_no_mention", "Nobody"), "@cityoffice", "@police", "@transit" });
        chosen.Add(kind == 2 ? 1 : 0);
        k.Groups = groups.ToArray();
        k.Chips = chips.ToArray();
        k.Chosen = chosen.ToArray();
        k.Confirm = t("reallife_flare_post", "Post");
    }

    // The ad sheet of a business: who sees it, how many a day, for how long, with the price.
    private static void FlareAdSheet(FlareFaceModel m, UiWords t)
    {
        FlareSheet(m, t, FlareFace.AdSheet);
        FlareSheetModel k = m.Sheet0;
        k.Title = t("reallife_flare_ad_title", "Run an ad for {business}").Replace("{business}", FlareShop);
        k.Line = t("reallife_flare_kit_ad_line", "Who sees it, how many a day, and for how long.");
        k.Groups = new[] { t("reallife_flare_ad_label_target", "Who sees it"), t("reallife_flare_ad_label_size", "People a day"), t("reallife_flare_ad_label_days", "Days") };
        k.Chips = new[]
        {
            new[]
            {
                t("reallife_flare_city_target_0", "Its district, who care for it"), t("reallife_flare_city_target_1", "The whole city, who care for it"), t("reallife_flare_city_target_2", "Its district, everybody"),
                t("reallife_flare_city_target_3", "The young"), t("reallife_flare_city_target_4", "The well off")
            },
            new[] { "500", "2,000", "5,000" },
            new[] { t("reallife_flare_kit_day_one", "1 day"), t("reallife_flare_kit_days_n", "{count} days").Replace("{count}", "3"), t("reallife_flare_kit_days_n", "{count} days").Replace("{count}", "7") }
        };
        k.Chosen = new[] { 0, 1, 0 };
        k.FigureLabels = new[] { t("reallife_flare_kit_fig_reach_day", "People a day"), t("reallife_flare_kit_fig_holds", "In the target"), t("reallife_flare_kit_fig_cost", "Cost") };
        k.Figures = new[] { "2K", "6.4K", "$180" };
        k.Note = t("reallife_flare_kit_reach_shared", "The reach is shared with the ad of {business} in this district.").Replace("{business}", "Speedy Bites");
        k.Confirm = t("reallife_flare_kit_ad_pay", "Run it for {money}").Replace("{money}", "$180");
    }

    // The profile sheet: the words about the player and the picture across the head of the profile.
    private static void FlareEditSheet(FlareFaceModel m, UiWords t)
    {
        FlareSheet(m, t, FlareFace.EditSheet);
        FlareSheetModel k = m.Sheet0;
        k.Title = t("reallife_flare_kit_edit_profile", "Edit profile");
        k.Line = null;
        k.Field = true;
        k.FieldArg = FlareFace.BioField;
        k.FieldText = "Owner of Bloom Cafe and Bloom Bakery. Coffee first.";
        k.FieldHint = t("reallife_flare_kit_bio_hint", "Write a line about you");
        k.FieldCount = t("reallife_flare_kit_count", "{count} of {most}").Replace("{count}", k.FieldText.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{most}", "90");
        k.Picture = "sample:p3";
        k.Groups = new[] { t("reallife_flare_kit_group_banner", "Picture") };
        k.Chips = new[]
        {
            new[]
            {
                t("reallife_flare_kit_banner_newest", "Newest picture"), t("reallife_flare_kit_photo_n", "City photo {n}").Replace("{n}", "1"), t("reallife_flare_kit_photo_n", "City photo {n}").Replace("{n}", "2"),
                t("reallife_flare_kit_photo_gallery", "Gallery")
            }
        };
        k.Chosen = new[] { 1 };
        k.Confirm = t("reallife_flare_kit_save", "Save");
    }
}
