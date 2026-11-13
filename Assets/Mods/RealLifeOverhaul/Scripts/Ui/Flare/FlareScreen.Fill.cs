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
        private readonly HashSet<string> _bodyTags = new HashSet<string>();       // the tags the page itself shows, so the side does not list them again
        private readonly HashSet<string> _bodyAccounts = new HashSet<string>();   // the accounts the page itself shows, likewise
        private readonly List<FlareNotice> _notified = new List<FlareNotice>(FlareDirector.KitNoticesMost);
        private readonly List<FlareFunnel> _adFunnels = new List<FlareFunnel>(AdsMost);
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
            _bodyTags.Clear();
            _bodyAccounts.Clear();
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
            if (FlareFace.HasSide(_page))
            {
                Side(file, now);   // after the page: what the page already shows is not said again beside it
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
            _m.MyRing = UiTokens.Grade(FlareGame.PlayerHappiness());   // the player's own mood, as the HUD has it
            FaceSources.Player(_m.MyName);
            if (file == null)
            {
                return;
            }
            List<FlareNotice> notices = Notified(file, now);
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
            bool own = _page == FlareFace.Profile && (_profile == "me" || _profile == _flare.MeKey);
            FlareWeekFacts facts = WeekFacts(file, now);
            FlareFigures.Week(facts, Words, own, out _m.WeekLabels, out _m.WeekFigures, out _m.WeekDeltas, out _m.WeekTones);
            // The followers' line and figure are the profile's own head on one's own profile, and an account with nothing to count has no week at all: the page says its empty state once.
            _m.Line = own || FlareFigures.NewAccount(facts) ? null : FlareFeedRules.Line(file.History, file.Followers);
            // What trends and who to follow, less what the page itself shows: a tag or an account is told once on a screen.
            FlareLiveRules.Trending(file.Posts, now, TrendsMost + ExploreTrendsMost, _trendTags, _trendCounts);
            int kept = 0;
            for (int i = 0; i < _trendTags.Count && kept < TrendsMost; i++)
            {
                if (_bodyTags.Contains(_trendTags[i]))
                {
                    continue;
                }
                _trendTags[kept] = _trendTags[i];
                _trendCounts[kept++] = _trendCounts[i];
            }
            _trendTags.RemoveRange(kept, _trendTags.Count - kept);
            _trendCounts.RemoveRange(kept, _trendCounts.Count - kept);
            for (int i = 0; i < _trendTags.Count; i++)
            {
                ref FlareRow r = ref _m.Trends.Add();
                r.Kind = FlareRowKind.Tag;
                r.Name = "#" + _trendTags[i];
                r.Handle = TrendWhere(file, _trendTags[i], now);
                r.Value = PostsCount(_trendCounts[i]);
            }
            WhoToFollow(file, _whoKeys, WhoMost + _bodyAccounts.Count);   // as many as the page can take away and still leave the rail its eight
            kept = 0;
            for (int i = 0; i < _whoKeys.Count && kept < WhoMost; i++)
            {
                if (!_bodyAccounts.Contains(_whoKeys[i]))
                {
                    _whoKeys[kept++] = _whoKeys[i];
                }
            }
            _whoKeys.RemoveRange(kept, _whoKeys.Count - kept);
            for (int i = 0; i < _whoKeys.Count; i++)
            {
                Account(_whoKeys[i], out string name, out string handle, out string about, out bool verified);
                ref FlareRow r = ref _m.Who.Add();
                r.Kind = FlareRowKind.Account;
                r.Name = name;
                r.Handle = AccountLine(_whoKeys[i], handle);
                r.Hue = UiColour.HueOf(_whoKeys[i]);
                r.Flags = (Following(_whoKeys[i]) ? FlareRowFlags.Following : FlareRowFlags.None) | (verified ? FlareRowFlags.Verified : FlareRowFlags.None);
                Look(_whoKeys[i], ref r);
            }
        }

        // The account's last week as every page that shows it reads it (the side's week and the studio's tiles are FlareFigures' own words over these facts).
        private FlareWeekFacts WeekFacts(FlareFile file, long now)
        {
            _seenOfPost = _seenOfPost ?? SeenOf;
            FlareFeedRules.Reach(file.Posts, (int)(now / 1440), out long week, out long before, _seenOfPost);
            Rating(out int stars, out int reviews);
            return new FlareWeekFacts
            {
                Followers = file.Followers, Gain = FlareFeedRules.WeekGain(file.History, file.Followers), Reach = week, ReachBefore = before, StarsMilli = stars, Reviews = reviews
            };
        }

        private Func<int, int> _seenOfPost;

        // The residents the city counted seeing a post of the player's, none when it measured nothing.
        private int SeenOf(int post)
        {
            FlareFunnel f = _flare.FunnelOfPost(post);
            return f != null ? f.Seen : 0;
        }

        // The game's words for FlareFigures, which writes the same figures in the previews with the previews' own.
        private static readonly UiWords Words = (key, english) => FlareText.T(key);
        private static readonly Func<long, string> Money = dollars => FlareText.Money(dollars);

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

        private static string PostsCount(int n) => FlareFigures.PostsCount(n, Words);

        private static string ReviewsCount(int n) => FlareFigures.ReviewsCount(n, Words);

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
            string bodyKey = FlareFeedRules.EmptyBodyKey(tab, file.Follows.Count);
            _m.List.EmptyTitle = FlareText.T(FlareFeedRules.EmptyTitleKey(tab, file.Follows.Count));
            _m.List.EmptyBody = bodyKey != null ? FlareText.T(bodyKey) : null;
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
            var people = new List<bool>(StoriesMost);
            var rings = new List<UiRole>(StoriesMost);
            List<FlareShop> mine = _flare.MyShops();
            for (int i = 0; i < mine.Count && names.Count < StoriesMost; i++)
            {
                names.Add(mine[i].Name ?? "?");
                hues.Add(UiColour.HueOf(mine[i].Place));
                shops.Add(true);
                people.Add(false);
                rings.Add(UiRole.None);
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
                FlareRow who = default(FlareRow);
                who.Name = name;
                Look(_storyPicks[i], ref who);   // a friend's, an influencer's: a face on the ring of their mood
                people.Add((who.Flags & FlareRowFlags.Person) != 0);
                rings.Add(who.Ring);
                _storyKeys.Add(_storyPicks[i]);
                _storyShops.Add(-1);
                _bodyAccounts.Add(_storyPicks[i]);   // a story names the account: the side does not offer it again
            }
            _m.Stories = names.ToArray();
            _m.StoryHues = hues.ToArray();
            _m.StoryShops = shops.ToArray();
            _m.StoryPeople = people.ToArray();
            _m.StoryRings = rings.ToArray();
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
                _bodyTags.Add(_tag);
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
            // Nothing asked: what trends. The accounts to follow are the side's, so no account is listed twice on the page.
            FlareLiveRules.Trending(file.Posts, now, ExploreTrendsMost, _trendTags, _trendCounts);
            if (_trendTags.Count > 0)
            {
                Section(FlareText.T("reallife_flare_city_trend_tags"));
            }
            for (int i = 0; i < _trendTags.Count; i++)
            {
                TagRow(file, _trendTags[i], _trendCounts[i], now);
            }
            BusyDistricts(file, now);
            _m.List.EmptyIcon = UiIcon.Search;
            _m.List.EmptyTitle = FlareText.T("reallife_flare_kit_empty_explore");
            _m.List.EmptyBody = FlareText.T("reallife_flare_kit_empty_search_body");
        }

        private readonly List<string> _busyNames = new List<string>(16);
        private readonly List<int> _busyCounts = new List<int>(16);
        private const int BusyMost = 3;   // districts listed: with the tags they fill the page's height, and the side holds the accounts

        // Where the city posted over the last day: the districts with the most posts, from the posts themselves. It is what the page can say that its tags and the side do not,
        // and it gives Explore a body as tall as a page when a day's tags are few.
        private void BusyDistricts(FlareFile file, long now)
        {
            _busyNames.Clear();
            _busyCounts.Clear();
            for (int i = file.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || now - p.At > 1440 || p.ReplyTo >= 0 || string.IsNullOrEmpty(p.District) || Hidden(file, p))
                {
                    continue;
                }
                int at = _busyNames.IndexOf(p.District);
                if (at < 0)
                {
                    _busyNames.Add(p.District);
                    _busyCounts.Add(1);
                }
                else
                {
                    _busyCounts[at]++;
                }
            }
            for (int shown = 0; shown < BusyMost && _busyNames.Count > 0; shown++)
            {
                int best = 0;
                for (int i = 1; i < _busyNames.Count; i++)
                {
                    best = _busyCounts[i] > _busyCounts[best] || (_busyCounts[i] == _busyCounts[best] && string.CompareOrdinal(_busyNames[i], _busyNames[best]) < 0) ? i : best;
                }
                if (shown == 0)
                {
                    Section(FlareText.T("reallife_flare_kit_section_busy"));
                }
                ref FlareRow r = ref Add(_m.List, RowAct.None);
                r.Kind = FlareRowKind.Card;
                r.Icon = UiIcon.MapPin;
                r.Tint = UiRole.AccentText;
                r.Name = FlareText.District(_busyNames[best]);
                r.Text = PostsCount(_busyCounts[best]);
                _busyNames.RemoveAt(best);
                _busyCounts.RemoveAt(best);
            }
        }

        private void TagRow(FlareFile file, string tag, int count, long now)
        {
            _bodyTags.Add(tag);
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
            _bodyAccounts.Add(key);
            ref FlareRow r = ref Add(_m.List, RowAct.Profile, null, key);
            r.Kind = FlareRowKind.Account;
            r.Name = name;
            r.Handle = AccountLine(key, handle);
            r.Hue = UiColour.HueOf(key);
            r.Flags = (following ? FlareRowFlags.Following : FlareRowFlags.None) | (verified ? FlareRowFlags.Verified : FlareRowFlags.None);
            Look(key, ref r);
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
            _m.HeadLine = FlareFigures.NewHead(_unseen, Words);
            List<FlareNotice> notices = Notified(file, now);
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

        // The notifications as the badge and the page count them: what the page already holds as a card that waits for the player's answer (an offer still open, a review of
        // their business not yet answered) is that card, and no line of the list says it a second time.
        private List<FlareNotice> Notified(FlareFile file, long now)
        {
            List<FlareNotice> all = _flare.KitNotices();
            _notified.Clear();
            int today = (int)(now / 1440);
            List<string> mine = _flare.MyPlaces();
            for (int i = 0; i < all.Count; i++)
            {
                FlareNotice n = all[i];
                if (n.Kind == FlareNoticeKind.Offer)
                {
                    FlareOffer o = _flare.OfferOf(FlareLiveRules.OfferOf(n.Who));
                    if (o != null && o.Open(today))
                    {
                        continue;
                    }
                }
                else if (n.Kind == FlareNoticeKind.Review && n.Post >= 0 && AwaitsAnswer(file, file.Post(n.Post), mine, now))
                {
                    continue;
                }
                _notified.Add(n);
            }
            return _notified;
        }

        // A review of one of the player's businesses that they have not answered.
        private bool AwaitsAnswer(FlareFile file, FlarePost p, List<string> mine, long now)
        {
            return p != null && p.Source == FlareSource.Review && p.Author != FlareAuthor.Player && p.Place != null && mine.Contains(p.Place) && p.At <= now && !Hidden(file, p)
                && MyAnswer(file, p.Id) == null;
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
                if (!AwaitsAnswer(file, p, mine, now))
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
                r.Line = m != null ? _flare.DmLine(m) : null;
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
            _m.HeadLine = FlareFigures.UnreadHead(unread, Words);
            _m.Search = _inbox;
            _m.InboxHint = FlareText.T("reallife_flare_kit_inbox_hint");
            List<FlareThread> threads = _flare.KitThreads();
            string you = FlareText.T("reallife_flare_dm_you") + " ";
            for (int i = 0; i < threads.Count && _m.List.Count < ThreadsMost; i++)
            {
                FlareThread t = threads[i];
                string name = ThreadName(t.With, t.Name);
                string last = t.Last != null ? _flare.DmLine(t.Last) : string.Empty;
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
                Look(t.With, ref r);
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
                r.Text = _flare.DmLine(d);
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
            FlareRow who = default(FlareRow);
            who.Name = _m.DetailName;
            Look(_with, ref who);
            _m.DetailPerson = (who.Flags & FlareRowFlags.Person) != 0;
            _m.DetailRing = who.Ring;
            _m.DetailLine = KindOf(_with);
            _m.DetailLink = _with.StartsWith("d:", StringComparison.Ordinal) ? null : FlareText.T("reallife_flare_kit_view_profile");
            _m.Send = FlareText.T("reallife_flare_send");
            QuickFor(file, _with);
            _m.Quick = _quick.Count > 0 ? QuickWords() : None;
            if (_with.StartsWith("d:", StringComparison.Ordinal))
            {
                FlareOffer o = _flare.OfferOf(FlareLiveRules.OfferOf(_with));
                // An open offer is in the bubble and on its buttons, so the head says who writes; once it is answered or has lapsed the head says what became of it.
                _m.DetailLine = o == null || o.Open(_flare.CurrentDay) ? KindOf(_with) : FlareText.T(o.Accepted ? "reallife_flare_city_offer_running" : "reallife_flare_city_offer_lapsed");
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
            _bodyAccounts.Add(key);
            FlareProfileModel h = _m.Profile;
            Account(key, out h.Name, out h.Handle, out h.Bio, out h.Verified);
            h.Hue = UiColour.HueOf(me ? "me" : key);
            FlareRow face = default(FlareRow);
            face.Name = h.Name;
            Look(key, ref face);
            h.Person = (face.Flags & FlareRowFlags.Person) != 0;
            h.Ring = face.Ring;
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
                ProfilePills(h, file, key);
            }
            _m.Tabs = new[] { FlareText.T("reallife_flare_tab_posts"), FlareText.T("reallife_flare_tab_replies"), FlareText.T("reallife_flare_kit_tab_media") };
            _m.Tab = _profileTab;
            _m.List.EmptyIcon = _profileTab == 2 ? UiIcon.Image : UiIcon.Message;
            _m.List.EmptyTitle = FlareText.T(_profileTab == 2 ? "reallife_flare_empty_pictures" : _profileTab == 1 ? "reallife_flare_empty_replies" : "reallife_flare_empty_profile");
            _m.List.EmptyBody = me ? FlareText.T("reallife_flare_kit_empty_profile_body") : null;
        }

        private static string KeyOf(FlarePost p) => FlareLiveRules.KeyOf(p);

        // The old profile's further buttons: a message to a player, an official, an influencer or a resident; and for a player of the session, muting and hiding them.
        private void ProfilePills(FlareProfileModel h, FlareFile file, string key)
        {
            var words = new List<string>(3);
            var acts = new List<int>(3);
            if (FlareScreenRules.CanMessage(key))
            {
                words.Add(FlareText.T("reallife_flare_message"));
                acts.Add(FlareFace.ActMessage);
            }
            if (FlareScreenRules.IsPlayer(key))
            {
                string player = key.Substring(2);
                words.Add(FlareText.T(file.Net.IsMuted(player) ? "reallife_flare_unmute" : "reallife_flare_mute"));
                acts.Add(FlareFace.ActMute);
                words.Add(FlareText.T(file.Net.IsHidden(player) ? "reallife_flare_unhide" : "reallife_flare_hide"));
                acts.Add(FlareFace.ActHide);
            }
            h.Pills = words.ToArray();
            h.PillActs = acts.ToArray();
        }

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
            int postRow = _m.List.Count;
            PostRow(file, p, now);
            Author(p, out _, out _, out string author, out _);
            _bodyAccounts.Add(author ?? string.Empty);
            FlareFunnel did = p.Author == FlareAuthor.Player ? _flare.FunnelOfPost(p.Id) : null;
            if (did != null && did.Seen > 0)
            {
                Section(FlareText.T("reallife_flare_kit_funnel_title"));
                FunnelRow(did, null);
            }
            Section(FlareText.T("reallife_flare_replies_title"));
            int before = _m.List.Count;
            _synthetic.Clear();
            if (p.Author == FlareAuthor.Player)
            {
                FriendReplies(p, now);
            }
            else
            {
                Comments(p, now);
            }
            int answers = 0;
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
                    answers++;   // the author's answer is a line under the reply it answers, not a reply of its own
                }
            }
            for (int i = next; i < _synthetic.Count && _m.List.Count < FeedMost; i++)
            {
                PostRow(file, _synthetic[i], now, true);
            }
            // The card counts the post's replies as its feed card does (RepliesOf), whoever wrote the post, and the rows the list did not draw are the "more" line, so the post, its
            // list and the feed agree; the head says it only when there is none.
            int listed = _m.List.Count - before - answers;
            int more = FlareFigures.ReplyMore(RepliesOf(p, now), listed);
            int replies = FlareFigures.Replies(listed, more);
            _m.List.Rows[postRow].Comments = replies > 0 ? FlareFeedRules.Count(replies) : null;
            _m.HeadLine = replies > 0 ? null : FlareText.T("reallife_flare_no_replies");
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

        // What a post or an ad of the player's did in the city, read from the director's record of it (the one the retired Flare in the city page listed): who saw it, how many
        // came and what they spent, or how many it put off and what that cost. A post says it under itself, an ad in the studio's list under the business it was for.
        private void FunnelRow(FlareFunnel f, string business)
        {
            bool off = f.Came < 0 || f.Spent < 0;
            ref FlareRow r = ref Add(_m.List, RowAct.None);
            r.Kind = FlareRowKind.Result;
            r.Icon = business != null ? UiIcon.Megaphone : UiIcon.ChartLineUp;
            r.Tint = off ? UiRole.DangerText : UiRole.OkText;
            string said = FlareFigures.FunnelSaid(f.Seen, f.Came, off, Words);
            r.Text = business != null ? FlareText.Fill("reallife_flare_kit_result_ad", "business", business) : said;
            r.Line = business != null ? said : FlareFigures.FunnelMore(f.Interested, f.Followed, Words);
            r.Value = Signed(f.Spent);
            r.Flags |= off ? FlareRowFlags.Bad : f.Spent > 0 ? FlareRowFlags.Good : FlareRowFlags.None;
        }

        // The worded comments under another's post, as replies for display; the replies not written are the "more" line the thread counts from the post's total.
        private void Comments(FlarePost p, long now)
        {
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
            }
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
                        Text = words ?? _flare.FriendLine(p, index, people[k]), Seed = p.Seed + i, Slot = i, Liked = _flare.IsReplyLiked(p.Id, i)
                    };
                    FlareLiveRules.SetAt(reply, p.At + r.Delay);
                    _synthetic.Add(reply);
                }
            }
        }
    }
}
