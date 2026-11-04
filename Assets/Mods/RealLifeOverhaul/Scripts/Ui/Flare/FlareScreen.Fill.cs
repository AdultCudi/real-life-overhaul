using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face, what each page shows: the nav with its badges, the side's week, trends and accounts to follow, and every
    /// page's head and rows, read from the account and the city when something changed. Every figure is the model's own; the
    /// lists are capped (FlareScreen's *Most) and only their rows in view are laid out.
    /// </summary>
    public sealed partial class FlareScreen
    {
        private static readonly string[] None = new string[0];
        private const int ClockStep = 10;   // game minutes: the times shown ("5m") and what came due are read again at most this often

        private long _filledAt = -1;
        private readonly List<string> _trendTags = new List<string>(8);
        private readonly List<int> _trendCounts = new List<int>(8);
        private readonly List<string> _whoKeys = new List<string>(WhoMost);
        private readonly List<string> _storyKeys = new List<string>(StoriesMost);
        private readonly List<int> _storyShops = new List<int>(StoriesMost);
        private readonly List<string> _storyPicks = new List<string>(StoriesMost);
        private readonly List<int> _storyAt = new List<int>(StoriesMost);
        private readonly List<string> _quick = new List<string>(4);
        private readonly List<FlareReviewNote> _notes = new List<FlareReviewNote>(ReviewsMost);
        private readonly HashSet<string> _seenKeys = new HashSet<string>();
        private readonly List<FlarePost> _synthetic = new List<FlarePost>(16);
        private readonly List<FlareComment> _comments = new List<FlareComment>(FlareCommentRules.WrittenMost);
        private readonly List<FlareReaction> _reactions = new List<FlareReaction>(16);
        private int _unseen;

        private bool Stale => _filledAt >= 0 && _flare != null && _flare.Now / ClockStep != _filledAt / ClockStep;

        private void Fill()
        {
            FlareFile file = _flare != null && _flare.Running ? _flare.File : null;
            long now = _flare != null ? _flare.Now : 0;
            _filledAt = now;
            ClearList(_m.List);
            _m.Detail.Clear();
            _m.Trends.Clear();
            _m.Who.Clear();
            _m.Head = _m.HeadLine = null;
            _m.Tabs = None;
            _m.Field = _m.FieldHint = null;
            _m.FieldArg = -1;
            _m.Quick = None;
            _m.DetailName = _m.DetailLine = _m.DetailLink = null;
            _m.Stories = None;
            _m.Business.Picker = None;
            _m.List.Foot = false;
            _m.List.EmptyIcon = UiIcon.Fire;
            _m.Detail.Foot = false;
            Shell(file, now);
            _m.Page = _page;
            _m.Sheet = file != null ? _sheet : FlareFace.NoSheet;
            if (file == null)
            {
                _m.Head = FlareText.T("reallife_flare_app");
                _m.List.EmptyTitle = FlareText.T("reallife_flare_off_title");
                _m.List.EmptyBody = FlareText.T("reallife_flare_kit_off_body");
                return;
            }
            CountReplies(file, now);
            if (FlareFace.HasSide(_page))
            {
                Side(file, now);
            }
            switch (_page)
            {
                case FlareFace.Home: Home(file, now); break;
                case FlareFace.Explore: Explore(file, now); break;
                case FlareFace.Notices: Notices(file, now); break;
                case FlareFace.Messages: Messages(file, now); break;
                case FlareFace.Profile: Profile(file, now); break;
                case FlareFace.Studio: Studio(file, now); break;
                case FlareFace.Business: Business(file, now); break;
                case FlareFace.Hire: Hire(file, now); break;
                case FlareFace.City: City(file, now); break;
                case FlareFace.Thread: Thread(file, now); break;
            }
            if (_sheet != FlareFace.NoSheet)
            {
                FillSheet(file, now);
            }
        }

        // ------------------------------------------------------------------------------------------------ the shell

        private static readonly UiIcon[] NavIcons =
        {
            UiIcon.Home, UiIcon.Search, UiIcon.Bell, UiIcon.Message, UiIcon.Person, UiIcon.ChartLineUp, UiIcon.Store, UiIcon.People, UiIcon.Building
        };

        private static readonly string[] NavKeys =
        {
            "reallife_flare_nav_home", "reallife_flare_nav_explore", "reallife_flare_nav_notices", "reallife_flare_nav_messages", "reallife_flare_nav_profile",
            "reallife_flare_nav_studio", "reallife_flare_nav_business", "reallife_flare_nav_hire", "reallife_flare_nav_city"
        };

        private readonly string[] _nav = new string[FlareFace.Pages];
        private readonly int[] _badges = new int[FlareFace.Pages];

        private void Shell(FlareFile file, long now)
        {
            for (int i = 0; i < _nav.Length; i++)
            {
                _nav[i] = FlareText.T(NavKeys[i]);
                _badges[i] = 0;
            }
            _m.Nav = _nav;
            _m.NavIcons = NavIcons;
            _m.Badges = _badges;
            _m.Post = FlareText.T("reallife_flare_post");
            _m.Move = FlareText.T("reallife_ui_prompt_move");
            _m.Select = FlareText.T("reallife_ui_prompt_select");
            _m.Back = FlareText.T("reallife_ui_prompt_back");
            _m.MyName = _flare != null ? _flare.MyName : string.Empty;
            _m.MyHandle = _flare != null ? _flare.MyHandle : string.Empty;
            _m.MyHue = UiColour.HueOf("me");
            if (file == null)
            {
                return;
            }
            List<FlareNotice> notices = _flare.KitNotices();
            _unseen = FlareLiveRules.UnseenNotices(notices, _page == FlareFace.Notices ? _seenBefore : _flare.NoticesSeenAt);
            _badges[FlareFace.Notices] = _page == FlareFace.Notices ? 0 : _unseen;
            _badges[FlareFace.Messages] = _flare.UnreadMessages;
        }

        // ------------------------------------------------------------------------------------------------ the side

        private void Side(FlareFile file, long now)
        {
            _m.SearchHint = FlareText.T("reallife_flare_kit_search");
            _m.WeekTitle = FlareText.T("reallife_flare_kit_week");
            _m.StudioLink = FlareText.T("reallife_flare_kit_studio_link");
            _m.TrendTitle = FlareText.T("reallife_flare_kit_trending");
            _m.WhoTitle = FlareText.T("reallife_flare_kit_who");
            _m.FollowWord = FlareText.T("reallife_flare_follow");
            _m.FollowingWord = FlareText.T("reallife_flare_follow_on");
            Week(file, now, out _m.WeekLabels, out _m.WeekFigures, out _m.WeekDeltas, out _m.WeekTones);
            _m.Line = FlareFeedRules.Line(file.History, file.Followers);
            FlareLiveRules.Trending(file.Posts, now, TrendsMost, _trendTags, _trendCounts);
            for (int i = 0; i < _trendTags.Count; i++)
            {
                ref FlareRow r = ref _m.Trends.Add();
                r.Kind = FlareRowKind.Tag;
                r.Name = "#" + _trendTags[i];
                r.Handle = TrendWhere(file, _trendTags[i], now);
                r.Value = PostsCount(_trendCounts[i]);
            }
            _whoKeys.Clear();
            WhoToFollow(file, _whoKeys, WhoMost);
            for (int i = 0; i < _whoKeys.Count; i++)
            {
                Account(_whoKeys[i], out string name, out string handle, out string about, out bool verified);
                ref FlareRow r = ref _m.Who.Add();
                r.Kind = FlareRowKind.Account;
                r.Name = name;
                r.Handle = AccountLine(_whoKeys[i], handle);
                r.Hue = UiColour.HueOf(_whoKeys[i]);
                r.Flags = (Following(_whoKeys[i]) ? FlareRowFlags.Following : FlareRowFlags.None) | (verified ? FlareRowFlags.Verified : FlareRowFlags.None);
            }
        }

        // Your week: followers won, the people reached against the week before, the rating of the player's businesses.
        private void Week(FlareFile file, long now, out string[] labels, out string[] figures, out string[] deltas, out int[] tones)
        {
            int today = (int)(now / 1440);
            int gain = FlareFeedRules.WeekGain(file.History, file.Followers);
            FlareFeedRules.Reach(file.Posts, today, out long week, out long before);
            int change = FlareFeedRules.Change(week, before);
            Rating(out int stars, out int reviews);
            labels = new[] { FlareText.T("reallife_flare_kit_fig_followers"), FlareText.T("reallife_flare_kit_fig_reach"), FlareText.T("reallife_flare_kit_fig_rating") };
            figures = new[]
            {
                FlareFeedRules.Count(file.Followers), FlareFeedRules.Count(week),
                reviews > 0 ? (stars / 1000f).ToString("0.0", CultureInfo.InvariantCulture) : FlareText.T("reallife_flare_kit_rating_none")
            };
            deltas = new[]
            {
                FlareText.Fill("reallife_flare_kit_week_followers", "change", FlareText.Signed(gain)),
                FlareText.Fill("reallife_flare_kit_week_reach", "change", FlareText.Signed(change)),
                reviews > 0 ? ReviewsCount(reviews) : FlareText.T("reallife_flare_kit_week_no_reviews")
            };
            tones = new[] { Math.Sign(gain), Math.Sign(change), 0 };
        }

        // The rating of the player's businesses together, thousandths of a star, and the reviews it stands on.
        private void Rating(out int stars, out int reviews)
        {
            long sum = 0;
            reviews = 0;
            List<FlareShop> shops = _flare.MyShops();
            for (int i = 0; i < shops.Count; i++)
            {
                FlareStarRow row = _flare.StarsFor(shops[i].Place);
                if (row != null && row.Total > 0)
                {
                    sum += (long)FlareReplyRules.Average(row) * row.Total;
                    reviews += row.Total;
                }
            }
            stars = reviews > 0 ? (int)(sum / reviews) : 0;
        }

        // Where a tag trends: the district most of its posts of the day are about, or the whole city.
        private static string TrendWhere(FlareFile file, string tag, long now)
        {
            string best = null;
            int bestCount = 0;
            for (int i = file.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || now - p.At > 1440 || p.District == null || p.Topic == null || !HasTag(p.Topic, tag))
                {
                    continue;
                }
                int n = 0;
                for (int k = file.Posts.Count - 1; k >= 0; k--)
                {
                    FlarePost q = file.Posts[k];
                    n += q.District == p.District && q.At <= now && now - q.At <= 1440 && q.Topic != null && HasTag(q.Topic, tag) ? 1 : 0;
                }
                if (n > bestCount)
                {
                    best = p.District;
                    bestCount = n;
                }
                if (bestCount >= 3)
                {
                    break;
                }
            }
            return best != null ? FlareText.Fill("reallife_flare_kit_trend_where", "district", FlareText.District(best)) : FlareText.T("reallife_flare_kit_trend_city");
        }

        private static string PostsCount(int n) => n == 1 ? FlareText.T("reallife_flare_kit_posts_one") : FlareText.Fill("reallife_flare_kit_posts_count", "count", FlareFeedRules.Count(n));

        private static string ReviewsCount(int n) => n == 1 ? FlareText.T("reallife_flare_kit_reviews_one") : FlareText.Fill("reallife_flare_kit_reviews_count", "count", FlareText.N(n));

        // Accounts to follow: a mix of the city, the busiest influencers, the businesses and the residents that posted lately (FlareFeedRules).
        private void WhoToFollow(FlareFile file, List<string> into, int most)
        {
            FlareFeedRules.WhoToFollow(file, _flare.Now, most, into);
        }

        // The line under an account's name: its handle and its followers, or what it is.
        private string AccountLine(string key, string handle)
        {
            FlareFile file = _flare.File;
            if (key.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(key.Substring(2), out int n) && file != null && file.Influencer(n) != null)
            {
                return FlareText.Fill("reallife_flare_kit_account_line", "handle", handle, "about",
                    FlareText.Fill("reallife_flare_kit_followers_n", "count", FlareFeedRules.Count(file.Influencer(n).Followers)));
            }
            return FlareText.Fill("reallife_flare_kit_account_line", "handle", handle, "about", KindOf(key));
        }

        // What an account is, in a word or two.
        private static string KindOf(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }
            switch (key[0])
            {
                case 'i': return FlareText.T("reallife_flare_kit_kind_influencer");
                case 'k':
                case 'b':
                case 'm':
                case 't': return FlareText.T("reallife_flare_kit_kind_business");
                case 'o':
                case 'l': return FlareText.T("reallife_flare_kit_kind_official");
                case 'p': return key.Length > 1 && key[1] == ':' ? FlareText.T("reallife_flare_kit_kind_player") : FlareText.T("reallife_flare_kit_kind_friend");
                case 'f': return FlareText.T("reallife_flare_kit_kind_friend");
                case 'd': return FlareText.T("reallife_flare_kit_kind_sponsor");
                default: return FlareText.T("reallife_flare_kit_kind_resident");
            }
        }

        // ------------------------------------------------------------------------------------------------ Home

        private void Home(FlareFile file, long now)
        {
            _m.Tabs = new[] { FlareText.T("reallife_flare_tab_foryou"), FlareText.T("reallife_flare_tab_following"), FlareText.T("reallife_flare_kit_tab_businesses") };
            _m.Tab = _tab;
            string first = _m.MyName;
            int space = first.IndexOf(' ');
            _m.ComposerHint = FlareText.Fill("reallife_flare_kit_composer", "name", space > 0 ? first.Substring(0, space) : first);
            _m.Chips = new[]
            {
                FlareText.T("reallife_flare_kit_chip_photo"), FlareText.T("reallife_flare_kit_chip_poll"), FlareText.T("reallife_flare_kit_chip_promote"),
                FlareText.T("reallife_flare_kit_chip_place")
            };
            _m.ChipIcons = ChipIcons;
            Stories(file, now);
            List<string> mine = _flare.MyPlaces();
            var tab = (FlareFeedTab)_tab;
            for (int i = file.Posts.Count - 1; i >= 0 && _m.List.Count < FeedMost; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || p.ReplyTo >= 0 || Hidden(file, p))
                {
                    continue;
                }
                bool followed = p.Author == FlareAuthor.Network || FlareCast.Followed(file, FlareLiveRules.KeyOf(p));
                if (!FlareFeedRules.InTab(tab, p.Author, p.Source, followed, p.Place != null && mine.Contains(p.Place)))
                {
                    continue;
                }
                PostRow(file, p, now);
            }
            _m.List.EmptyIcon = UiIcon.Fire;
            _m.List.EmptyTitle = FlareText.T(tab == FlareFeedTab.Following ? "reallife_flare_empty_following" : tab == FlareFeedTab.Businesses
                ? "reallife_flare_kit_empty_businesses" : "reallife_flare_empty_feed");
            _m.List.EmptyBody = FlareText.T("reallife_flare_kit_empty_feed_body");
            _flare.CommentsAhead(null);   // RLO Texts: a few of the comments under the feed's posts asked; once a game minute at most
        }

        private static readonly UiIcon[] ChipIcons = { UiIcon.Image, UiIcon.ChartPie, UiIcon.Megaphone, UiIcon.MapPin };

        // The stories: the player's businesses first, then the friends and the accounts followed that posted in the last day, then what the
        // city has been posting (a business, a resident, an influencer, an official in turn), newest first (FlareFeedRules.Stories).
        private void Stories(FlareFile file, long now)
        {
            _storyKeys.Clear();
            _storyShops.Clear();
            var names = new List<string>(StoriesMost);
            var hues = new List<float>(StoriesMost);
            var shops = new List<bool>(StoriesMost);
            List<FlareShop> mine = _flare.MyShops();
            for (int i = 0; i < mine.Count && names.Count < StoriesMost; i++)
            {
                names.Add(mine[i].Name ?? "?");
                hues.Add(UiColour.HueOf(mine[i].Place));
                shops.Add(true);
                _storyKeys.Add(null);
                _storyShops.Add(i);
            }
            FlareFeedRules.Stories(file, now, StoriesMost - names.Count, _storyPicks, _storyAt);
            for (int i = 0; i < _storyPicks.Count; i++)
            {
                Author(file.Posts[_storyAt[i]], out string name, out _, out _, out _);
                names.Add(name);
                hues.Add(UiColour.HueOf(_storyPicks[i]));
                shops.Add(false);
                _storyKeys.Add(_storyPicks[i]);
                _storyShops.Add(-1);
            }
            _m.Stories = names.ToArray();
            _m.StoryHues = hues.ToArray();
            _m.StoryShops = shops.ToArray();
        }

        private bool OpenStory(int i)
        {
            if (i < 0 || i >= _storyKeys.Count)
            {
                return false;
            }
            return _storyShops[i] >= 0 ? OpenBusiness(_storyShops[i]) : OpenProfile(_storyKeys[i]);
        }

        // ------------------------------------------------------------------------------------------------ Explore

        private void Explore(FlareFile file, long now)
        {
            _m.Search = _search;
            _m.SearchHint = FlareText.T("reallife_flare_kit_search_hint");
            if (_tag != null)
            {
                int count = 0;
                for (int i = file.Posts.Count - 1; i >= 0; i--)
                {
                    FlarePost p = file.Posts[i];
                    if (p.At > now || p.Topic == null || Hidden(file, p) || !HasTag(p.Topic, _tag))
                    {
                        continue;
                    }
                    count++;
                    if (_m.List.Count < FeedMost)
                    {
                        PostRow(file, p, now);
                    }
                }
                _m.Head = "#" + _tag;
                _m.HeadLine = PostsCount(count);
                _m.List.EmptyIcon = UiIcon.Search;
                _m.List.EmptyTitle = FlareText.T("reallife_flare_empty_tag");
                _m.List.EmptyBody = FlareText.T("reallife_flare_kit_empty_search_body");
                return;
            }
            if (_search.Trim().Length > 0)
            {
                Search(file, now, _search.Trim());
                return;
            }
            // Nothing asked: what trends, then accounts to follow.
            FlareLiveRules.Trending(file.Posts, now, 8, _trendTags, _trendCounts);
            if (_trendTags.Count > 0)
            {
                Section(FlareText.T("reallife_flare_city_trend_tags"));
            }
            for (int i = 0; i < _trendTags.Count; i++)
            {
                TagRow(file, _trendTags[i], _trendCounts[i], now);
            }
            var keys = new List<string>(ExploreWhoMost);
            WhoToFollow(file, keys, ExploreWhoMost);
            if (keys.Count > 0)
            {
                Section(FlareText.T("reallife_flare_kit_section_accounts"));
            }
            for (int i = 0; i < keys.Count; i++)
            {
                AccountRow(keys[i]);
            }
            _m.List.EmptyIcon = UiIcon.Search;
            _m.List.EmptyTitle = FlareText.T("reallife_flare_kit_empty_explore");
            _m.List.EmptyBody = FlareText.T("reallife_flare_kit_empty_search_body");
        }

        private void TagRow(FlareFile file, string tag, int count, long now)
        {
            ref FlareRow r = ref Add(_m.List, RowAct.Tag, null, tag);
            r.Kind = FlareRowKind.Tag;
            r.Name = "#" + tag;
            r.Handle = TrendWhere(file, tag, now);
            r.Value = PostsCount(count);
        }

        private void AccountRow(string key)
        {
            Account(key, out string name, out string handle, out _, out bool verified);
            bool following = Following(key);
            ref FlareRow r = ref Add(_m.List, RowAct.Profile, null, key);
            r.Kind = FlareRowKind.Account;
            r.Name = name;
            r.Handle = AccountLine(key, handle);
            r.Hue = UiColour.HueOf(key);
            r.Flags = (following ? FlareRowFlags.Following : FlareRowFlags.None) | (verified ? FlareRowFlags.Verified : FlareRowFlags.None);
            r.Button = Followable(key) ? FlareText.T(following ? "reallife_flare_follow_on" : "reallife_flare_follow") : null;
        }

        // A search: accounts by name or handle, tags, then posts by their words; each part capped.
        private void Search(FlareFile file, long now, string query)
        {
            var accounts = new List<string>(FlareFeedRules.SearchMost);
            _seenKeys.Clear();
            for (int i = 0; i < file.Influencers.Count && accounts.Count < FlareFeedRules.SearchMost / 2; i++)
            {
                FlareInfluencer inf = file.Influencers[i];
                string key = "i:" + inf.Id.ToString(CultureInfo.InvariantCulture);
                Account(key, out string name, out string handle, out _, out _);
                if (inf.Active && (FlareFeedRules.Matches(name, query) || FlareFeedRules.Matches(handle, query)) && _seenKeys.Add(key))
                {
                    accounts.Add(key);
                }
            }
            for (int i = file.Posts.Count - 1; i >= 0 && accounts.Count < FlareFeedRules.SearchMost / 2; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || Hidden(file, p))
                {
                    continue;
                }
                string key = FlareLiveRules.KeyOf(p);
                if (key == null || key == "me" || _seenKeys.Contains(key))
                {
                    continue;
                }
                Author(p, out string name, out string handle, out _, out _);
                if ((FlareFeedRules.Matches(name, query) || FlareFeedRules.Matches(handle, query)) && _seenKeys.Add(key))
                {
                    accounts.Add(key);
                }
            }
            if (accounts.Count > 0)
            {
                Section(FlareText.T("reallife_flare_tab_accounts"));
            }
            for (int i = 0; i < accounts.Count; i++)
            {
                AccountRow(accounts[i]);
            }
            FlareLiveRules.Trending(file.Posts, now, 24, _trendTags, _trendCounts);
            bool tagged = false;
            for (int i = 0, shown = 0; i < _trendTags.Count && shown < 6; i++)
            {
                if (!FlareFeedRules.Matches(_trendTags[i], query))
                {
                    continue;
                }
                if (!tagged)
                {
                    Section(FlareText.T("reallife_flare_kit_section_tags"));
                    tagged = true;
                }
                TagRow(file, _trendTags[i], _trendCounts[i], now);
                shown++;
            }
            bool posted = false;
            for (int i = file.Posts.Count - 1, shown = 0; i >= 0 && shown < FlareFeedRules.SearchMost; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || p.ReplyTo >= 0 || Hidden(file, p))
                {
                    continue;
                }
                Author(p, out string name, out _, out _, out _);
                if (!FlareFeedRules.Matches(p.Text ?? _flare.WordsFor(p, name), query) && !FlareFeedRules.Matches(p.Topic, query))
                {
                    continue;
                }
                if (!posted)
                {
                    Section(FlareText.T("reallife_flare_tab_posts"));
                    posted = true;
                }
                PostRow(file, p, now);
                shown++;
            }
            _m.List.EmptyIcon = UiIcon.Search;
            _m.List.EmptyTitle = FlareText.T("reallife_flare_kit_empty_search");
            _m.List.EmptyBody = FlareText.T("reallife_flare_kit_empty_search_body");
        }

        // ------------------------------------------------------------------------------------------------ Notifications

        private static readonly string[] NoticeTabKeys =
        {
            "reallife_flare_kit_notice_tab_0", "reallife_flare_kit_notice_tab_1", "reallife_flare_kit_notice_tab_2", "reallife_flare_kit_notice_tab_3", "reallife_flare_kit_notice_tab_4"
        };

        private void Notices(FlareFile file, long now)
        {
            _m.Head = FlareText.T("reallife_flare_nav_notices");
            _m.HeadLine = _unseen > 0 ? FlareText.Fill("reallife_flare_kit_new", "count", FlareText.N(_unseen)) : FlareText.T("reallife_flare_kit_all_seen");
            List<FlareNotice> notices = _flare.KitNotices();
            _m.Tabs = new string[NoticeTabKeys.Length];
            for (int i = 0; i < NoticeTabKeys.Length; i++)
            {
                int count = i == 0 ? 0 : FlareNoticeRules.Count(notices, i);   // each tab says how many it holds
                _m.Tabs[i] = count > 0 ? FlareText.T(NoticeTabKeys[i]) + " " + count.ToString(CultureInfo.InvariantCulture) : FlareText.T(NoticeTabKeys[i]);
            }
            _m.Tab = _kit.NoticeTab;
            _m.MarkAll = FlareText.T("reallife_flare_kit_mark_all");
            _m.MarkAllOff = _unseen == 0;
            NeedsAnswer(file, now);
            int shown = 0;
            for (int i = 0; i < notices.Count && shown < NoticesMost; i++)
            {
                if (FlareNoticeRules.InTab(_kit.NoticeTab, notices[i].Kind))
                {
                    NoticeRow(file, notices[i], now);
                    shown++;
                }
            }
            _m.List.EmptyIcon = UiIcon.Bell;
            _m.List.EmptyTitle = FlareText.T(_kit.NoticeTab == 0 ? "reallife_flare_empty_notices" : "reallife_flare_kit_empty_tab");
            _m.List.EmptyBody = FlareText.T(_kit.NoticeTab == 0 ? "reallife_flare_kit_empty_notices_body" : "reallife_flare_kit_empty_tab_body");
        }

        // What waits for the player's answer: the reviews of their businesses nobody has answered, and the offers of paid posts still open, at the page's top.
        private void NeedsAnswer(FlareFile file, long now)
        {
            int tab = _kit.NoticeTab, added = 0;
            bool reviews = tab == 0 || tab == (int)FlareNoticeTab.Reviews, deals = tab == 0 || tab == (int)FlareNoticeTab.Deals;
            List<FlareShop> shops = _flare.MyShops();
            for (int s = 0; reviews && s < shops.Count && added < NeedsMost; s++)
            {
                FlareShop shop = shops[s];
                _flare.ReviewNotesOf(shop.Place, _notes, ReviewsMost);
                for (int i = 0; i < _notes.Count && added < NeedsMost; i++)
                {
                    FlareReviewNote n = _notes[i];
                    if (!string.IsNullOrEmpty(n.Answer))
                    {
                        continue;
                    }
                    NeedsHead(ref added);
                    ref FlareRow r = ref Add(_m.List, RowAct.Note, null, null, 0, n);
                    r.Kind = FlareRowKind.Card;
                    r.Icon = UiIcon.Star;
                    r.Tint = UiRole.Gold;
                    r.Name = _flare.ReviewerName(n.Person);
                    r.Time = Ago(FlareLiveRules.Now(n.Day, FlareLiveRules.NoticeHour, 0), now);
                    r.Text = NoteWords(n, shop.Name);
                    r.Line = shop.Name;
                    r.Button = FlareText.T("reallife_flare_kit_reply");
                    added++;
                }
            }
            List<string> mine = _flare.MyPlaces();
            for (int i = file.Posts.Count - 1; reviews && i >= 0 && added < NeedsMost; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.Source != FlareSource.Review || p.Author == FlareAuthor.Player || p.Place == null || !mine.Contains(p.Place) || p.At > now || Hidden(file, p) || MyAnswer(file, p.Id) != null)
                {
                    continue;
                }
                NeedsHead(ref added);
                Author(p, out string name, out _, out string key, out _);
                ref FlareRow r = ref Add(_m.List, RowAct.Review, p, key);
                r.Kind = FlareRowKind.Card;
                r.Icon = UiIcon.Star;
                r.Tint = UiRole.Gold;
                r.Name = name;
                r.Time = Ago(p.At, now);
                r.Text = FlareFeedRules.Plain(p.Text ?? _flare.WordsFor(p, name));
                r.Line = p.Name;
                r.Button = FlareText.T("reallife_flare_kit_reply");
                added++;
            }
            FlareCityView offers = deals && _flare.CityOn ? _flare.CityOffersView() : null;
            int today = (int)(now / 1440);
            for (int i = 0; offers != null && i < offers.Rows.Count && added < NeedsMost; i++)
            {
                FlareOffer o = _flare.OfferOf(offers.Rows[i].Post);
                if (o == null || !o.Open(today))
                {
                    continue;
                }
                NeedsHead(ref added);
                ref FlareRow r = ref Add(_m.List, RowAct.Conversation, null, FlareLiveRules.OfferKey(o.Id));
                r.Kind = FlareRowKind.Card;
                r.Icon = UiIcon.Coin;
                r.Tint = UiRole.Gold;
                r.Name = o.Name ?? "?";
                r.Text = FlareText.Fill("reallife_flare_kit_offer_card", "money", FlareText.Money((int)(o.Fee / 100)), "posts", o.Posts.ToString(CultureInfo.InvariantCulture));
                r.Line = FlareText.Fill("reallife_flare_kit_offer_until", "days", Math.Max(0, o.Until - today).ToString(CultureInfo.InvariantCulture));
                r.Button = FlareText.T("reallife_flare_kit_view");
                added++;
            }
        }

        // The heading over the rows that wait for an answer, once.
        private void NeedsHead(ref int added)
        {
            if (added == 0)
            {
                Section(FlareText.T("reallife_flare_kit_needs_title"));
            }
        }

        private void NoticeRow(FlareFile file, FlareNotice n, long now)
        {
            FlarePost post = n.Post >= 0 ? file.Post(n.Post) : null;
            RowAct act = RowAct.None;
            string key = n.Who;
            switch (n.Kind)
            {
                case FlareNoticeKind.Dm:
                case FlareNoticeKind.Job:
                    act = RowAct.Conversation;
                    break;
                case FlareNoticeKind.Offer:
                    act = RowAct.Conversation;
                    break;
                case FlareNoticeKind.Follow:
                    act = RowAct.Profile;
                    key = "me";
                    break;
                case FlareNoticeKind.Campaign:
                    act = RowAct.Conversation;
                    break;
                case FlareNoticeKind.Milestone:
                    act = RowAct.Profile;
                    key = "me";
                    break;
                case FlareNoticeKind.Rival:
                    act = n.Name != null ? RowAct.Profile : RowAct.None;
                    key = n.Name != null ? "k:" + n.Name : null;
                    break;
                default:
                    act = post != null ? RowAct.Thread : RowAct.None;
                    break;
            }
            ref FlareRow r = ref Add(_m.List, act, post, key);
            r.Kind = FlareRowKind.Notice;
            r.Flags = n.At > _seenBefore ? FlareRowFlags.Unread : FlareRowFlags.None;
            r.Time = Ago(n.At, now);
            r.Text = NoticeWords(n);
            NoticeLook(n, out r.Icon, out r.Tint);
            if (post != null)
            {
                Author(post, out string name, out _, out _, out _);
                string words = post.Text ?? _flare.WordsFor(post, name);
                r.Line = FlareFeedRules.Plain(words);
            }
            else if (n.Kind == FlareNoticeKind.Dm || n.Kind == FlareNoticeKind.Job)
            {
                FlareDm m = DmOf(file, n.Dm);
                r.Line = m != null ? _flare.DmWords(m) : null;
            }
            // What the post a notice is about brought, under it: told for the player's own posts that carry a boost.
            if (post != null && post.Author == FlareAuthor.Player && n.Kind != FlareNoticeKind.Promo && FlareNoticeRules.Brought(file, post.Id, now, out int visitors, out long cents))
            {
                r.Value = FlareText.Fill("reallife_flare_kit_brought_short", "visitors", FlareText.N(visitors), "money", Signed(cents));
                r.Flags |= FlareRowFlags.Good;
            }
        }

        private static FlareDm DmOf(FlareFile file, int id)
        {
            for (int i = file.Net.Dms.Count - 1; id >= 0 && i >= 0; i--)
            {
                if (file.Net.Dms[i].Id == id)
                {
                    return file.Net.Dms[i];
                }
            }
            return null;
        }

        // A notification's line in words: who did what.
        private string NoticeWords(FlareNotice n)
        {
            string name = WhoName(n.Who);
            string money = FlareText.Money(n.Cents / 100);
            switch (n.Kind)
            {
                case FlareNoticeKind.Follow:
                    return n.Figure == 1 ? FlareText.T("reallife_flare_kit_notice_follow_one") : FlareText.Fill("reallife_flare_kit_notice_follow", "count", FlareText.N((int)n.Figure));
                case FlareNoticeKind.Offer:
                    return FlareText.Fill("reallife_flare_kit_notice_offer", "name", n.Name ?? name, "money", money, "posts", n.Figure.ToString(CultureInfo.InvariantCulture));
                case FlareNoticeKind.Job:
                    return FlareText.Fill("reallife_flare_kit_notice_job", "name", name);
                case FlareNoticeKind.Campaign:
                    return n.Bad ? FlareText.Fill("reallife_flare_kit_notice_campaign_flop", "name", name, "business", n.Name ?? "?")
                        : FlareText.Fill("reallife_flare_kit_notice_campaign", "name", name, "business", n.Name ?? "?", "visitors", FlareText.N((int)n.Figure), "money", money);
                case FlareNoticeKind.Promo:
                    return n.Bad && n.Figure <= 0 ? FlareText.Fill("reallife_flare_kit_notice_promo_flop", "business", n.Name ?? "?")
                        : FlareText.Fill("reallife_flare_kit_notice_promo", "business", n.Name ?? "?", "visitors", FlareText.N((int)n.Figure), "money", money);
                case FlareNoticeKind.Milestone:
                    return FlareText.Fill("reallife_flare_kit_notice_milestone", "tier", FlareText.Tier((FlareTier)Math.Max(0, Math.Min((int)n.Figure, (int)FlareTier.Celebrity))),
                        "count", FlareFeedRules.Count(FlareRules.TierFrom[Math.Max(0, Math.Min((int)n.Figure, FlareRules.TierFrom.Length - 1))]));
                case FlareNoticeKind.Gained:
                    return string.IsNullOrEmpty(n.Name) ? FlareText.Fill("reallife_flare_kit_notice_gained_plain", "count", FlareText.N((int)n.Figure))
                        : FlareText.Fill("reallife_flare_kit_notice_gained", "count", FlareText.N((int)n.Figure), "business", n.Name);
                case FlareNoticeKind.TrendPart:
                    return FlareText.Fill("reallife_flare_kit_notice_trend_part", "tag", n.Name ?? "?");
                case FlareNoticeKind.Rival:
                    return FlareText.Fill("reallife_flare_kit_notice_rival", "business", n.Name ?? "?", "district", _flare.DistrictLabel(n.Extra), "count", FlareText.N((int)n.Figure));
                default:
                    return FlareText.Notice(n.Kind, name);
            }
        }

        // A notification's who: a friend, the boss, one of the city's people, an account.
        private string WhoName(string who)
        {
            if (string.IsNullOrEmpty(who))
            {
                return "?";
            }
            if (who[0] == 'p' && who.Length > 1 && who[1] != ':' && int.TryParse(who.Substring(1), out int n))
            {
                return _flare.PersonName(n) ?? "?";
            }
            if (who == "b")
            {
                return FlareText.T("reallife_flare_your_boss");
            }
            Account(who, out string name, out _, out _, out _);
            return name;
        }

        private static void NoticeLook(FlareNotice n, out UiIcon icon, out UiRole tint)
        {
            switch (n.Kind)
            {
                case FlareNoticeKind.Like: icon = UiIcon.Heart; tint = UiRole.CatRetail; return;
                case FlareNoticeKind.Reply: icon = UiIcon.Message; tint = UiRole.AccentText; return;
                case FlareNoticeKind.Repost: icon = UiIcon.Reset; tint = UiRole.OkText; return;
                case FlareNoticeKind.Mention: icon = UiIcon.Person; tint = UiRole.AccentText; return;
                case FlareNoticeKind.Case: icon = UiIcon.Building; tint = UiRole.Text2; return;
                case FlareNoticeKind.Dm: icon = UiIcon.Envelope; tint = UiRole.AccentText; return;
                case FlareNoticeKind.Going: icon = UiIcon.Calendar; tint = UiRole.AccentText; return;
                case FlareNoticeKind.Review: icon = UiIcon.Star; tint = UiRole.Gold; return;
                case FlareNoticeKind.Follow: icon = UiIcon.People; tint = UiRole.AccentText; return;
                case FlareNoticeKind.Offer: icon = UiIcon.Coin; tint = UiRole.Gold; return;
                case FlareNoticeKind.Job: icon = UiIcon.Briefcase; tint = UiRole.AccentText; return;
                case FlareNoticeKind.Campaign: icon = UiIcon.Megaphone; tint = n.Bad ? UiRole.DangerText : UiRole.Chart2; return;
                case FlareNoticeKind.Milestone: icon = UiIcon.Trophy; tint = UiRole.Gold; return;
                case FlareNoticeKind.Gained: icon = UiIcon.People; tint = UiRole.OkText; return;
                case FlareNoticeKind.TrendPart: icon = UiIcon.Fire; tint = UiRole.Chart2; return;
                case FlareNoticeKind.Rival: icon = UiIcon.Megaphone; tint = UiRole.Text2; return;
                default: icon = UiIcon.ChartLineUp; tint = n.Bad ? UiRole.DangerText : UiRole.OkText; return;
            }
        }

        // ------------------------------------------------------------------------------------------------ Messages

        private void Messages(FlareFile file, long now)
        {
            _m.Head = FlareText.T("reallife_flare_nav_messages");
            int unread = _flare.UnreadMessages;
            _m.HeadLine = unread > 0 ? FlareText.Fill("reallife_flare_kit_unread", "count", FlareText.N(unread)) : FlareText.T("reallife_flare_kit_all_read");
            _m.Search = _inbox;
            _m.InboxHint = FlareText.T("reallife_flare_kit_inbox_hint");
            List<FlareThread> threads = _flare.KitThreads();
            string you = FlareText.T("reallife_flare_dm_you") + " ";
            for (int i = 0; i < threads.Count && _m.List.Count < ThreadsMost; i++)
            {
                FlareThread t = threads[i];
                string name = ThreadName(t.With, t.Name);
                string last = t.Last != null ? _flare.DmWords(t.Last) : string.Empty;
                if (_inbox.Trim().Length > 0 && !FlareFeedRules.Matches(name, _inbox) && !FlareFeedRules.Matches(last, _inbox))
                {
                    continue;
                }
                ref FlareRow r = ref Add(_m.List, RowAct.Conversation, null, t.With);
                r.Kind = FlareRowKind.Thread;
                r.Name = name;
                r.Hue = UiColour.HueOf(t.With);
                r.Time = Ago(t.At, now);
                r.Text = t.Last != null && t.Last.Mine ? you + last : last;
                r.Value = t.Unread > 0 ? t.Unread.ToString(CultureInfo.InvariantCulture) : null;
                r.Flags = (t.Unread > 0 && t.With != _with ? FlareRowFlags.Unread : FlareRowFlags.None) | (t.With == _with ? FlareRowFlags.Chosen : FlareRowFlags.None);
            }
            _m.List.EmptyIcon = UiIcon.Message;
            _m.List.EmptyTitle = FlareText.T(_inbox.Trim().Length > 0 ? "reallife_flare_kit_empty_search" : "reallife_flare_empty_messages");
            _m.List.EmptyBody = FlareText.T("reallife_flare_kit_empty_messages_body");
            _m.Detail.EmptyTitle = FlareText.T("reallife_flare_kit_pick_thread");
            _m.Detail.EmptyBody = FlareText.T("reallife_flare_kit_pick_thread_body");
            if (_with != null)
            {
                Conversation(file, now);
            }
        }

        private string ThreadName(string with, string given)
        {
            if (with != null && with.StartsWith("d:", StringComparison.Ordinal))
            {
                return _flare.AccountName(with);
            }
            if (!string.IsNullOrEmpty(given))
            {
                return given;
            }
            Account(with, out string name, out _, out _, out _);
            return name;
        }

        private void Conversation(FlareFile file, long now)
        {
            List<FlareDm> dms = _flare.KitThread(_with);
            bool unread = false;
            string name = null;
            for (int i = Math.Max(0, dms.Count - BubblesMost); i < dms.Count; i++)
            {
                FlareDm d = dms[i];
                unread |= !d.Mine && !d.Read;
                name = !d.Mine && !string.IsNullOrEmpty(d.Name) ? d.Name : name;
                ref FlareRow r = ref _m.Detail.Add();
                r.Kind = FlareRowKind.Bubble;
                r.Text = _flare.DmWords(d);
                r.Time = Ago(d.At, now);
                r.Flags = d.Mine ? FlareRowFlags.Mine : FlareRowFlags.None;
            }
            if (unread)
            {
                _flare.ReadDms(_with);   // seen as it shows: the badge goes
                _badges[FlareFace.Messages] = _flare.UnreadMessages;
            }
            _m.Detail.Foot = true;
            _m.DetailName = ThreadName(_with, name);
            _m.DetailHue = UiColour.HueOf(_with);
            _m.DetailLine = KindOf(_with);
            _m.DetailLink = _with.StartsWith("d:", StringComparison.Ordinal) ? null : FlareText.T("reallife_flare_kit_view_profile");
            _m.Send = FlareText.T("reallife_flare_send");
            QuickFor(file, _with);
            _m.Quick = _quick.Count > 0 ? QuickWords() : None;
            if (_with.StartsWith("d:", StringComparison.Ordinal))
            {
                FlareOffer o = _flare.OfferOf(FlareLiveRules.OfferOf(_with));
                _m.DetailLine = o == null ? KindOf(_with) : o.Open(_flare.CurrentDay) ? FlareText.Fill("reallife_flare_kit_offer_line", "money", FlareText.Money((int)(o.Fee / 100)),
                    "posts", o.Posts.ToString(CultureInfo.InvariantCulture)) : FlareText.T(o.Accepted ? "reallife_flare_city_offer_running" : "reallife_flare_city_offer_lapsed");
                return;   // an offer is answered by its two buttons, not in words
            }
            _m.Field = _message;
            _m.FieldHint = FlareText.T("reallife_flare_dm_placeholder");
            _m.FieldArg = FlareFace.ThreadField;
        }

        // ------------------------------------------------------------------------------------------------ Profile

        private void Profile(FlareFile file, long now)
        {
            string key = _profile ?? "me";
            bool me = key == "me" || key == _flare.MeKey;
            FlareProfileModel h = _m.Profile;
            Account(key, out h.Name, out h.Handle, out h.Bio, out h.Verified);
            h.Hue = UiColour.HueOf(me ? "me" : key);
            h.Edit = me ? FlareText.T("reallife_flare_kit_edit_profile") : null;
            if (me)
            {
                h.Handle = h.Handle + " \u00b7 " + FlareText.Fill("reallife_flare_kit_joined", "day", Math.Max(1, file.StartDay).ToString(CultureInfo.InvariantCulture));
            }
            h.Banner = me && !string.IsNullOrEmpty(file.Banner) ? file.Banner : null;
            int posts = 0;
            for (int i = file.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || Hidden(file, p) || KeyOf(p) != (me ? "me" : key))
                {
                    continue;
                }
                posts++;
                if (h.Banner == null)
                {
                    h.Banner = FlarePictures.KeyOf(p) ?? _flare.CityPhotoKey(p);   // the account's newest picture across its head
                }
                bool reply = p.ReplyTo >= 0, picture = FlarePictures.KeyOf(p) != null || _flare.CityPhotoKey(p) != null;
                if (_m.List.Count >= FeedMost || (_profileTab == 0 && reply) || (_profileTab == 1 && !reply) || (_profileTab == 2 && !picture))
                {
                    continue;
                }
                PostRow(file, p, now);
            }
            if (me)
            {
                h.Figures = new[] { FlareFeedRules.Count(file.Followers), FlareText.N(file.Follows.Count), FlareText.N(Math.Max(posts, file.TotalPosts)) };
                h.Labels = new[] { FlareText.T("reallife_flare_kit_followers"), FlareText.T("reallife_flare_kit_following"), FlareText.T("reallife_flare_kit_posts") };
                h.Follow = null;
            }
            else
            {
                FlareInfluencer inf = key.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(key.Substring(2), out int n) ? file.Influencer(n) : null;
                h.Figures = inf != null ? new[] { FlareFeedRules.Count(inf.Followers), FlareText.N(posts) } : new[] { FlareText.N(posts) };
                h.Labels = inf != null ? new[] { FlareText.T("reallife_flare_kit_followers"), FlareText.T("reallife_flare_kit_posts") } : new[] { FlareText.T("reallife_flare_kit_posts") };
                h.Following = Following(key);
                h.Follow = Followable(key) ? FlareText.T(h.Following ? "reallife_flare_follow_on" : "reallife_flare_follow") : null;
            }
            _m.Tabs = new[] { FlareText.T("reallife_flare_tab_posts"), FlareText.T("reallife_flare_tab_replies"), FlareText.T("reallife_flare_kit_tab_media") };
            _m.Tab = _profileTab;
            _m.List.EmptyIcon = _profileTab == 2 ? UiIcon.Image : UiIcon.Message;
            _m.List.EmptyTitle = FlareText.T(_profileTab == 2 ? "reallife_flare_empty_pictures" : _profileTab == 1 ? "reallife_flare_empty_replies" : "reallife_flare_empty_profile");
            _m.List.EmptyBody = me ? FlareText.T("reallife_flare_kit_empty_profile_body") : null;
        }

        private static string KeyOf(FlarePost p) => FlareLiveRules.KeyOf(p);

        private bool ProfileOfThread(string with)
        {
            return with != null && !with.StartsWith("d:", StringComparison.Ordinal) && OpenProfile(with);
        }

        // ------------------------------------------------------------------------------------------------ a post's thread

        private void Thread(FlareFile file, long now)
        {
            FlarePost p = file.Post(_post);
            _m.Head = FlareText.T("reallife_flare_kit_post_title");
            if (p == null)
            {
                _m.List.EmptyTitle = FlareText.T("reallife_flare_compose_gone");
                return;
            }
            FlarePost parent = p.ReplyTo >= 0 ? file.Post(p.ReplyTo) : null;
            if (parent != null)
            {
                PostRow(file, parent, now);
            }
            PostRow(file, p, now);
            Section(FlareText.T("reallife_flare_replies_title"));
            int before = _m.List.Count;
            _synthetic.Clear();
            int more = 0;
            if (p.Author == FlareAuthor.Player)
            {
                FriendReplies(p, now);
            }
            else
            {
                more = Comments(p, now);
            }
            int next = 0;   // the comments come in order of time: each goes in among the posted replies by its minute
            for (int i = 0; i < file.Posts.Count && _m.List.Count < FeedMost; i++)
            {
                FlarePost r = file.Posts[i];
                if (r.ReplyTo != p.Id || r.At > now || Hidden(file, r))
                {
                    continue;
                }
                while (next < _synthetic.Count && _synthetic[next].At <= r.At)
                {
                    PostRow(file, _synthetic[next++], now, true);
                }
                PostRow(file, r, now, r.Author != FlareAuthor.Player);
                FlarePost answer = r.Author == FlareAuthor.Player ? _flare.AnswerShown(r, now) : null;   // RLO Texts: the author answering the player's reply
                if (answer != null)
                {
                    PostRow(file, answer, now, true);
                }
            }
            for (int i = next; i < _synthetic.Count && _m.List.Count < FeedMost; i++)
            {
                PostRow(file, _synthetic[i], now, true);
            }
            int replies = _m.List.Count - before + more;
            _m.HeadLine = replies > 0 ? FlareText.Fill("reallife_flare_kit_replies_count", "count", FlareText.N(replies)) : FlareText.T("reallife_flare_no_replies");
            if (more > 0)
            {
                Section(more == 1 ? FlareText.T("reallife_flare_comments_more_one") : FlareText.Fill("reallife_flare_comments_more", "count", FlareText.N(more)));
            }
            if (p.Author != FlareAuthor.Player)
            {
                _flare.CommentsAhead(p);   // RLO Texts: the words of this post's comments still to come, asked first
            }
            _m.Field = _reply;
            _m.FieldHint = FlareText.T("reallife_flare_reply_placeholder");
            _m.FieldArg = FlareFace.ReplyField;
            _m.Send = FlareText.T("reallife_flare_reply_button");
        }

        // The worded comments under another's post, as replies for display; how many of the count are not written.
        private int Comments(FlarePost p, long now)
        {
            int shown = FlareLiveRules.Shown(p.Replies, p, now), written = 0;
            _flare.CommentsOf(p, _comments);
            int due = FlareCommentRules.Due(_comments, now);
            for (int i = 0; i < due; i++)
            {
                FlareComment c = _comments[i];
                string words = _flare.CommentWords(p, c);
                if (words == null)
                {
                    continue;
                }
                var comment = new FlarePost
                {
                    Id = FlareCommentRules.DisplayId(p.Id, c.Slot), Author = c.Kind == 'f' ? FlareAuthor.Friend : c.Kind == 'i' ? FlareAuthor.Influencer : FlareAuthor.Citizen,
                    Who = c.Who, Source = FlareSource.Reply, ReplyTo = p.Id, Text = words, Likes = c.Likes, Reach = 30, Slot = c.Slot,
                    Liked = _flare.IsCommentLiked(p.Id, c.Slot, c.Kind, c.Who)
                };
                FlareLiveRules.SetAt(comment, c.At);
                _synthetic.Add(comment);
                written++;
            }
            return FlareCommentRules.More(shown, written);
        }

        // The friends' replies to a post of the player's as they come in over the hours.
        private void FriendReplies(FlarePost p, long now)
        {
            FlareLiveRules.ReadReactors(p.Reactors, _reactions);
            IReadOnlyList<FlarePerson> people = _flare.People;
            for (int i = 0; i < _reactions.Count; i++)
            {
                FlareReaction r = _reactions[i];
                if (r.Kind != 'R' || p.At + r.Delay > now || r.Who.Length < 2 || !int.TryParse(r.Who.Substring(1), out int index))
                {
                    continue;
                }
                for (int k = 0; k < people.Count; k++)
                {
                    if (people[k].Index != index)
                    {
                        continue;
                    }
                    string words = _flare.FriendWords(p.Id, index);
                    if (words == null && _flare.FriendHeld(p.Id, index))
                    {
                        continue;   // RLO Texts: with a model present the reply comes only with its words
                    }
                    var reply = new FlarePost
                    {
                        Id = -1000 - p.Id * 32 - i, Author = FlareAuthor.Friend, Who = index, Source = FlareSource.Reply, ReplyTo = p.Id,
                        Text = words ?? FlareText.Reply(FlareRules.ReplyOf(people[k], p), p, index), Seed = p.Seed + i, Slot = i, Liked = _flare.IsReplyLiked(p.Id, i)
                    };
                    FlareLiveRules.SetAt(reply, p.At + r.Delay);
                    _synthetic.Add(reply);
                }
            }
        }
    }
}
