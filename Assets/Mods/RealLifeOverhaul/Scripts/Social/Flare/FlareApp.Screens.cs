using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// What each of Flare's screens lists, and what a click on a card does. The rows are made when a screen is written (a
    /// click, or the director's word that something changed): one walk over the posts (some two hundred) at most.
    /// </summary>
    public sealed partial class FlareApp
    {
        private readonly List<FlareReaction> _reactions = new List<FlareReaction>(16);
        private readonly List<FlarePost> _synthetic = new List<FlarePost>(16);
        private readonly List<FlarePerson> _persons = new List<FlarePerson>(8);
        private long _seenBefore;

        private void WriteScreen(FlareFile file)
        {
            long now = _director.Now;
            switch (_at.Screen)
            {
                case FlareScreen.Explore: WriteExplore(file, now); break;
                case FlareScreen.Tag: WriteTag(file, now); break;
                case FlareScreen.Notices: WriteNotices(file); break;
                case FlareScreen.Messages: WriteMessages(file, now); break;
                case FlareScreen.Conversation: WriteConversation(file, now); break;
                case FlareScreen.Profile: WriteProfile(file, now); break;
                case FlareScreen.Thread: WriteThread(file, now); break;
                case FlareScreen.Business: WriteBusiness(file); break;
                default: WriteHome(file, now); break;
            }
        }

        // ---------------------------------------------------------------- Home

        private void WriteHome(FlareFile file, long now)
        {
            Row(CellPrompt);
            if (file.Drafts.Count > 0)
            {
                FlareRow r = Row(CellButton);
                r.Key = "compose";
                r.Text = FlareText.Fill(file.Drafts.Count == 1 ? "reallife_flare_moments_one" : "reallife_flare_moments", "count", file.Drafts.Count.ToString());
            }
            bool following = _at.Tab == 1;
            int shown = 0;
            for (int i = file.Posts.Count - 1; i >= 0 && shown < FlareRules.FeedRows; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || p.ReplyTo >= 0 || Hidden(file, p))
                {
                    continue;
                }
                if (following && !Followed(file, p))
                {
                    continue;
                }
                Row(CellPost).Post = p;
                shown++;
            }
            if (shown == 0)
            {
                Note(FlareText.T(following ? "reallife_flare_empty_following" : "reallife_flare_empty_feed"));
            }
        }

        private static bool Followed(FlareFile file, FlarePost p)
        {
            return p.Author == FlareAuthor.Player || p.Author == FlareAuthor.Friend || p.Author == FlareAuthor.Network || FlareCast.Followed(file, FlareLiveRules.KeyOf(p));
        }

        private static bool Hidden(FlareFile file, FlarePost p)
        {
            return p.Author == FlareAuthor.Network && file.Net.IsHidden(p.AuthorId);
        }

        // ---------------------------------------------------------------- Explore, a tag

        private void WriteExplore(FlareFile file, long now)
        {
            if (_at.Tab == 0)
            {
                FlareLiveRules.Trending(file.Posts, now, 12, _tags, _tagCounts);
                for (int i = 0; i < _tags.Count; i++)
                {
                    FlareRow r = Row(CellTag);
                    r.Key = _tags[i];
                    r.Number = i * 100000 + Mathf.Min(99999, _tagCounts[i]);
                }
                if (_tags.Count == 0)
                {
                    Note(FlareText.T("reallife_flare_trend_none"));
                }
                return;
            }
            Section(FlareText.T("reallife_flare_accounts_official"));
            for (int i = 0; i <= (int)FlareOfficial.Bank; i++)
            {
                Account("o:" + i, FlareText.T("reallife_flare_about_official"));
            }
            if (GoingPublic.InSession)
            {
                int before = _rows.Count;
                Section(FlareText.T("reallife_flare_accounts_players"));
                Players(file);
                for (int i = 0; i < _players.Length; i++)
                {
                    if (_players[i] != null)
                    {
                        Account("p:" + _players[i], FlareText.T("reallife_flare_about_player"));
                    }
                }
                if (_rows.Count == before + 1)
                {
                    _rows.RemoveAt(_rows.Count - 1);
                }
            }
            Section(FlareText.T("reallife_flare_kind_influencers"));
            for (int i = 0; i < file.Influencers.Count; i++)
            {
                FlareInfluencer inf = file.Influencers[i];
                if (inf.Active)
                {
                    Account("i:" + inf.Id, FlareText.Fill("reallife_flare_profile_what", "niche", FlareText.Niche(inf.Niche), "district", FlareGame.DistrictName(inf.District),
                        "style", FlareText.Style(inf.Style)) + " " + FlareText.Fill("reallife_flare_about_followers", "followers", FlareText.N(inf.Followers)));
                }
            }
            IReadOnlyList<string> brands = _director.BrandAccounts, candidates = _director.CandidateAccounts;
            if (brands.Count > 0)
            {
                Section(FlareText.T("reallife_flare_kind_brands"));
                for (int i = 0; i < brands.Count; i++)
                {
                    Account("b:" + brands[i], FlareText.T("reallife_flare_account_brand_about"));
                }
            }
            if (candidates.Count > 0)
            {
                Section(FlareText.T("reallife_flare_kind_candidates"));
                for (int i = 0; i < candidates.Count; i++)
                {
                    Account("c:" + candidates[i], FlareText.T("reallife_flare_account_candidate_about"));
                }
            }
            Section(FlareText.T("reallife_flare_accounts_people"));
            int people = 0;
            for (int i = file.Posts.Count - 1; i >= 0 && people < 12; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.Author != FlareAuthor.Citizen || p.At > now || p.Who < 0 || p.Who >= _director.Citizens.Count)
                {
                    continue;
                }
                string key = "c" + p.Who;
                bool seen = false;
                for (int k = 0; k < _rows.Count && !seen; k++)
                {
                    seen = _rows[k].Kind == CellAccount && _rows[k].Key == key;
                }
                if (!seen)
                {
                    Account(key, FlareText.Fill("reallife_flare_about_citizen", "district", FlareText.District(_director.Citizens[p.Who].District)));
                    people++;
                }
            }
        }

        private void Account(string key, string about)
        {
            FlareRow r = Row(CellAccount);
            r.Key = key;
            r.Text = about;
        }

        private void WriteTag(FlareFile file, long now)
        {
            int shown = 0;
            for (int i = file.Posts.Count - 1; i >= 0 && shown < FlareRules.FeedRows; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At <= now && !Hidden(file, p) && HasTag(p.Topic, _at.Key))
                {
                    Row(CellPost).Post = p;
                    shown++;
                }
            }
            if (shown == 0)
            {
                Note(FlareText.T("reallife_flare_empty_tag"));
            }
        }

        private static bool HasTag(string topic, string tag)
        {
            if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(tag))
            {
                return false;
            }
            int at = 0;
            while ((at = topic.IndexOf(tag, at, StringComparison.Ordinal)) >= 0)
            {
                int end = at + tag.Length;
                if ((at == 0 || topic[at - 1] == ' ') && (end == topic.Length || topic[end] == ' '))
                {
                    return true;
                }
                at = end;
            }
            return false;
        }

        // ---------------------------------------------------------------- Notifications

        private void WriteNotices(FlareFile file)
        {
            _seenBefore = file.Net.NoticeSeen;
            List<FlareNotice> notices = _director.Notices();
            for (int i = 0; i < notices.Count; i++)
            {
                FlareNotice n = notices[i];
                if (n.Who != null && n.Who.StartsWith("p:", StringComparison.Ordinal) && file.Net.IsMuted(n.Who.Substring(2)))
                {
                    continue;   // a muted player's are not shown
                }
                FlareRow r = Row(CellNotice);
                r.Notice = n;
                r.Number = i;
                r.Post = n.Post >= 0 ? file.Post(n.Post) : null;
                if (n.Dm >= 0)
                {
                    for (int k = 0; k < file.Net.Dms.Count; k++)
                    {
                        if (file.Net.Dms[k].Id == n.Dm)
                        {
                            r.Dm = file.Net.Dms[k];
                        }
                    }
                }
            }
            if (notices.Count == 0)
            {
                Note(FlareText.T("reallife_flare_empty_notices"));
            }
            _director.NoticesSeen();
        }

        // ---------------------------------------------------------------- Messages

        private readonly List<string> _withs = new List<string>(16);

        private void WriteMessages(FlareFile file, long now)
        {
            _withs.Clear();
            for (int i = file.Net.Dms.Count - 1; i >= 0; i--)
            {
                FlareDm m = file.Net.Dms[i];
                if (m.At > now || _withs.Contains(m.With) || (m.With.StartsWith("p:", StringComparison.Ordinal) && file.Net.IsHidden(m.With.Substring(2))))
                {
                    continue;
                }
                _withs.Add(m.With);
                FlareRow r = Row(CellThread);
                r.Dm = Newest(file, m.With, now);
                int unread = 0;
                for (int k = 0; k < file.Net.Dms.Count; k++)
                {
                    FlareDm d = file.Net.Dms[k];
                    unread += d.With == m.With && !d.Read && !d.Mine && d.At <= now ? 1 : 0;
                }
                r.Number = unread;
            }
            if (_withs.Count == 0)
            {
                Note(FlareText.T("reallife_flare_empty_messages"));
            }
        }

        private static FlareDm Newest(FlareFile file, string with, long now)
        {
            FlareDm best = null;
            for (int i = 0; i < file.Net.Dms.Count; i++)
            {
                FlareDm d = file.Net.Dms[i];
                if (d.With == with && d.At <= now && (best == null || d.At >= best.At))
                {
                    best = d;
                }
            }
            return best;
        }

        private void WriteConversation(FlareFile file, long now)
        {
            for (int i = 0; i < file.Net.Dms.Count; i++)
            {
                FlareDm m = file.Net.Dms[i];
                if (m.With == _at.Key && m.At <= now)
                {
                    Row(CellBubble).Dm = m;
                }
            }
            FlareRow b = Row(CellButton);
            b.Key = "write";
            b.Text = FlareText.T("reallife_flare_write_message");
            _director.ReadDms(_at.Key);
            _top = false;
        }

        // ---------------------------------------------------------------- a profile

        private void WriteProfile(FlareFile file, long now)
        {
            string key = _at.Key ?? "me";
            FlareRow head = Row(CellProfile);
            head.Key = key;
            head.Text = AboutOf(file, key);
            int shown = 0;
            for (int i = file.Posts.Count - 1; i >= 0 && shown < FlareRules.FeedRows; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || FlareLiveRules.KeyOf(p) != key || Hidden(file, p))
                {
                    continue;
                }
                bool reply = p.ReplyTo >= 0;
                bool picture = FlarePictures.KeyOf(p) != null;
                if ((_at.Tab == 0 && reply) || (_at.Tab == 1 && !reply) || (_at.Tab == 2 && !picture))
                {
                    continue;
                }
                Row(CellPost).Post = p;
                shown++;
            }
            if (shown == 0)
            {
                Note(FlareText.T(_at.Tab == 2 ? "reallife_flare_empty_pictures" : _at.Tab == 1 ? "reallife_flare_empty_replies" : "reallife_flare_empty_profile"));
            }
        }

        private string AboutOf(FlareFile file, string key)
        {
            int n;
            if (key == "me")
            {
                return FlareText.Fill("reallife_flare_about_me", "tier", FlareText.Tier(file.Tier), "reputation", FlareText.Reputation(file.Reputation));
            }
            if (key.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(key.Substring(2), out n))
            {
                FlareInfluencer inf = file.Influencer(n);
                if (inf != null)
                {
                    string about = FlareText.Fill("reallife_flare_profile_what", "niche", FlareText.Niche(inf.Niche), "district", FlareGame.DistrictName(inf.District), "style", FlareText.Style(inf.Style));
                    if (_director.CurrentDay - inf.ScandalDay <= FlareCast.MonthDays)
                    {
                        about += " " + FlareText.T("reallife_flare_profile_scandal");
                    }
                    if (inf.Person >= 0 && _director.PersonName(inf.Person) != null)
                    {
                        about += " " + FlareText.Fill(inf.Star ? "reallife_flare_profile_star_person" : "reallife_flare_profile_friend", "name", _director.PersonName(inf.Person));
                    }
                    return about;
                }
            }
            if (key.StartsWith("o:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_about_official");
            }
            if (key.StartsWith("p:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_about_player");
            }
            if (key.StartsWith("b:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_account_brand_about");
            }
            if (key.StartsWith("c:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_account_candidate_about");
            }
            if (key.StartsWith("k:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_about_business");
            }
            if (key.StartsWith("t:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_about_tycoon");
            }
            if (key.Length > 1 && key[0] == 'c' && int.TryParse(key.Substring(1), out n) && n >= 0 && n < _director.Citizens.Count)
            {
                return FlareText.Fill("reallife_flare_about_citizen", "district", FlareText.District(_director.Citizens[n].District));
            }
            if (key.Length > 1 && key[0] == 'f' && int.TryParse(key.Substring(1), out n))
            {
                FlarePerson person;
                if (_director.Person(n, out person))
                {
                    return FlareText.Fill("reallife_flare_about_friend", "job", person.Job, "district", person.District);
                }
            }
            return string.Empty;
        }

        /// <summary>A profile's figures, in a line.</summary>
        private string StatsOf(string key)
        {
            FlareFile file = _director.File;
            if (file == null)
            {
                return string.Empty;
            }
            int n;
            if (key == "me")
            {
                return FlareText.Fill("reallife_flare_stats_me", "followers", FlareText.N(file.Followers), "friends", FlareRules.Count(file.Following).ToString(),
                    "posts", FlareText.N(file.TotalPosts), "likes", FlareText.N(file.TotalLikes), "following", file.Follows.Count.ToString());
            }
            if (key.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(key.Substring(2), out n) && file.Influencer(n) != null)
            {
                FlareInfluencer inf = file.Influencer(n);
                return FlareText.Fill("reallife_flare_profile_figures", "followers", FlareText.N(inf.Followers), "engaged", (inf.Engagement / 100f).ToString("0.0") + "%",
                    "reliable", inf.Reliability.ToString()) + "  ·  " + FlareText.Pair("reallife_flare_profile_price", FlareText.Money(Prices.At(FlareCast.Price(inf))));
            }
            int posts = 0;
            for (int i = 0; i < file.Posts.Count; i++)
            {
                posts += FlareLiveRules.KeyOf(file.Posts[i]) == key ? 1 : 0;
            }
            return FlareText.Fill("reallife_flare_stats_other", "posts", posts.ToString());
        }

        private readonly List<int> _profileActions = new List<int>(4);
        private const int ActFollow = 0, ActMessage = 1, ActMute = 2, ActHide = 3, ActHire = 4;

        // The buttons of a profile's head: follow, message, mute, hide, hire, as fit the account.
        private void ProfileActions(string key)
        {
            _profileActions.Clear();
            if (key == null || key == "me")
            {
                return;
            }
            if (Followable(key))
            {
                _profileActions.Add(ActFollow);
            }
            if (key.StartsWith("p:", StringComparison.Ordinal) || key.StartsWith("o:", StringComparison.Ordinal) || key.StartsWith("i:", StringComparison.Ordinal)
                || (key.Length > 1 && key[0] == 'c' && key[1] != ':'))
            {
                _profileActions.Add(ActMessage);
            }
            if (key.StartsWith("p:", StringComparison.Ordinal))
            {
                _profileActions.Add(ActMute);
                _profileActions.Add(ActHide);
            }
            if (key.StartsWith("i:", StringComparison.Ordinal) && FlareGame.Businesses().Count > 0)
            {
                _profileActions.Add(ActHire);
            }
        }

        private bool ProfileButtonOf(string key, int i, out string label, out bool filled)
        {
            ProfileActions(key);
            label = null;
            filled = false;
            if (i >= _profileActions.Count)
            {
                return false;
            }
            FlareFile file = _director.File;
            string player = key.Length > 2 ? key.Substring(2) : null;
            switch (_profileActions[i])
            {
                case ActFollow:
                    bool following = file != null && FlareCast.Followed(file, key);
                    label = FlareText.T(following ? "reallife_flare_follow_on" : "reallife_flare_follow");
                    filled = !following;
                    return true;
                case ActMessage:
                    label = FlareText.T("reallife_flare_message");
                    return true;
                case ActMute:
                    label = FlareText.T(file != null && file.Net.IsMuted(player) ? "reallife_flare_unmute" : "reallife_flare_mute");
                    return true;
                case ActHide:
                    label = FlareText.T(file != null && file.Net.IsHidden(player) ? "reallife_flare_unhide" : "reallife_flare_hide");
                    return true;
                default:
                    label = FlareText.T("reallife_flare_shop_hire");
                    return true;
            }
        }

        private void ProfileButton(FlareRow row, int i)
        {
            if (row == null)
            {
                return;
            }
            string key = row.Key;
            ProfileActions(key);
            FlareFile file = _director.File;
            if (file == null || i >= _profileActions.Count)
            {
                return;
            }
            string player = key.Length > 2 ? key.Substring(2) : null;
            switch (_profileActions[i])
            {
                case ActFollow: _director.Follow(key, !FlareCast.Followed(file, key)); break;
                case ActMessage: Open(FlareScreen.Conversation, 0, -1, key); break;
                case ActMute: _director.Mute(player, !file.Net.IsMuted(player)); break;
                case ActHide: _director.Hide(player, !file.Net.IsHidden(player)); break;
                default:
                    int id;
                    int.TryParse(key.Substring(2), out id);
                    List<BuildingRegistration> shops = FlareGame.Businesses();
                    if (shops.Count > 0)
                    {
                        OpenHire(FlareGame.Place(shops[0]), FlareGame.Name(shops[0]), id);
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- a post's thread

        private void WriteThread(FlareFile file, long now)
        {
            FlarePost p = file.Post(_at.Post);
            if (p == null)
            {
                Note(FlareText.T("reallife_flare_compose_gone"));
                return;
            }
            FlarePost parent = p.ReplyTo >= 0 ? file.Post(p.ReplyTo) : null;
            if (parent != null)
            {
                Row(CellPost).Post = parent;
            }
            FlareRow main = Row(CellPost);
            main.Post = p;
            main.Number = 1;
            Section(FlareText.T("reallife_flare_replies_title"));
            int before = _rows.Count;
            _synthetic.Clear();
            if (p.Author == FlareAuthor.Player)
            {
                FriendReplies(p, now);
            }
            for (int i = 0; i < file.Posts.Count; i++)
            {
                FlarePost r = file.Posts[i];
                if (r.ReplyTo == p.Id && r.At <= now && !Hidden(file, r))
                {
                    Row(CellPost).Post = r;
                }
            }
            for (int i = 0; i < _synthetic.Count; i++)
            {
                Row(CellPost).Post = _synthetic[i];
            }
            if (_rows.Count == before)
            {
                Note(FlareText.T("reallife_flare_no_replies"));
            }
            FlareRow b = Row(CellButton);
            b.Key = "reply";
            b.Number = p.Id;
            b.Text = FlareText.T("reallife_flare_reply_button");
        }

        // The friends' replies to a post of the player's, as they come in over the hours (their words are the post's own rules').
        private void FriendReplies(FlarePost p, long now)
        {
            FlareLiveRules.ReadReactors(p.Reactors, _reactions);
            IReadOnlyList<FlarePerson> people = _director.People;
            for (int i = 0; i < _reactions.Count; i++)
            {
                FlareReaction r = _reactions[i];
                int index;
                if (r.Kind != 'R' || p.At + r.Delay > now || r.Who.Length < 2 || !int.TryParse(r.Who.Substring(1), out index))
                {
                    continue;
                }
                for (int k = 0; k < people.Count; k++)
                {
                    if (people[k].Index != index)
                    {
                        continue;
                    }
                    var reply = new FlarePost
                    {
                        Id = -1000 - p.Id * 32 - i, Author = FlareAuthor.Friend, Who = index, Source = FlareSource.Reply, ReplyTo = p.Id,
                        Text = _director.FriendWords(p.Id, index) ?? FlareText.Reply(FlareRules.ReplyOf(people[k], p), p, index), Seed = p.Seed + i
                    };
                    FlareLiveRules.SetAt(reply, p.At + r.Delay);
                    _synthetic.Add(reply);
                }
            }
        }

        // ---------------------------------------------------------------- Business

        private void WriteBusiness(FlareFile file)
        {
            List<BuildingRegistration> shops = FlareGame.Businesses();
            int[] parts = new int[4];
            for (int i = 0; i < shops.Count; i++)
            {
                FlareRow r = Row(CellShop);
                r.Key = FlareGame.Place(shops[i]);
                r.Text = FlareGame.Name(shops[i]);
                int overall;
                r.Number = FlareGame.Satisfaction(shops[i], out overall, parts) ? overall : -1;
            }
            if (shops.Count == 0)
            {
                Note(FlareText.T("reallife_flare_business_none"));
            }
            int day = _director.CurrentDay;
            bool title = false;
            for (int i = file.Hires.Count - 1; i >= 0; i--)
            {
                FlareHire h = file.Hires[i];
                if (!title)
                {
                    Section(FlareText.T("reallife_flare_deals_title"));
                    title = true;
                }
                FlareRow r = Row(CellDeal);
                r.Key = FlareText.Fill("reallife_flare_row_hire", "business", h.Name, "who", _director.WhoLine(h));
                r.Text = DealLines(file, h, day);
                r.Number = h.Ended ? 0 : h.Id;
            }
            List<FlareCampaign> campaigns = _director.Campaigns();
            for (int i = 0; i < campaigns.Count; i++)
            {
                FlareCampaign c = campaigns[i];
                if (FlareRules.IsDeal(file, c.Place, c.Kind, c.From))
                {
                    continue;
                }
                if (!title)
                {
                    Section(FlareText.T("reallife_flare_deals_title"));
                    title = true;
                }
                FlareRow r = Row(CellDeal);
                r.Key = FlareText.Fill("reallife_flare_row_campaign", "business", c.Name, "campaign", c.Campaign);
                r.Text = FlareText.Busy(c, day);
            }
            Note(FlareText.Fill("reallife_flare_business_talk", "most", FlareRules.BuzzMost.ToString(), "days", FlareRules.BuzzDays.ToString()));
        }

        private static string DealLines(FlareFile file, FlareHire h, int day)
        {
            var b = new System.Text.StringBuilder(160);
            b.Append(FlareText.Pair("reallife_flare_hire_deal", FlareText.Deal(h.Kind - FlareCast.FirstDealKind)));
            b.Append('\n').Append(FlareText.Fill("reallife_flare_hire_days", "from", h.From.ToString(), "to", h.To.ToString(), "percent", h.Percent.ToString()));
            // Its posts: when the first comes, how many have come and when the last did, and where they are.
            string posts = FlareCast.HirePostsKey(h, day);
            if (posts != null)
            {
                b.Append('\n').Append(FlareText.Fill(posts, "day", (h.Posted > 0 ? h.LastPost : h.From).ToString(), "count", h.Posted.ToString()));
            }
            b.Append('\n').Append(FlareText.T("reallife_flare_hire_posts_where"));
            if (h.Ended)
            {
                b.Append('\n').Append(FlareText.T("reallife_flare_hire_ended"));
            }
            if (h.Late)
            {
                b.Append('\n').Append(FlareText.T(day >= h.From ? "reallife_flare_hire_late" : "reallife_flare_hire_unknown"));
            }
            if (h.OffMessage && day >= h.From)
            {
                b.Append('\n').Append(FlareText.T("reallife_flare_hire_off"));
            }
            if (h.Backfired && day >= h.From)
            {
                b.Append('\n').Append(FlareText.T("reallife_flare_hire_backfired"));
            }
            for (int i = 0; i < h.Who.Count; i++)
            {
                FlareInfluencer inf = file.Influencer(h.Who[i]);
                if (inf != null && inf.ScandalDay >= h.From && inf.ScandalDay <= h.To)
                {
                    b.Append('\n').Append(FlareText.T("reallife_flare_hire_scandal"));
                    break;
                }
            }
            return b.ToString();
        }

        private string ShopLines(FlareRow row)
        {
            FlareFile file = _director.File;
            if (file == null)
            {
                return string.Empty;
            }
            int day = _director.CurrentDay;
            int talk = file.BuzzAt(row.Key, day), own, last, reviews = 0, stars = 0;
            int posts = FlareCast.PostsAbout(file, row.Key, out own, out last);   // (the player's own and the paid posts of the deals there)
            for (int k = 0; k < file.Posts.Count; k++)
            {
                FlarePost p = file.Posts[k];
                if (p.Place == row.Key && p.Source == FlareSource.Review)
                {
                    reviews++;
                    stars += p.Arg;
                }
            }
            string text = (talk != 0 ? FlareText.Fill("reallife_flare_shop_talk", "percent", FlareText.Signed(talk)) : FlareText.T("reallife_flare_shop_quiet"))
                          + "\n" + FlareText.Fill("reallife_flare_shop_posts_line", "posts", posts.ToString(), "last", last >= 0 ? FlareText.Fill("reallife_flare_day", "day", last.ToString()) : FlareText.T("reallife_flare_never"));
            if (posts > own)
            {
                text += "\n" + FlareText.Pair("reallife_flare_shop_paid_posts", (posts - own).ToString());   // (the posts of the influencers hired for it, which the Feed shows among the newest)
            }
            if (reviews > 0)
            {
                text += "\n" + FlareText.Fill("reallife_flare_shop_reviews", "count", reviews.ToString(), "stars", (stars / (float)reviews).ToString("0.0"));
            }
            FlareCampaign campaign;
            if (_director.CampaignAt(row.Key, out campaign))
            {
                text += "\n" + FlareText.Busy(campaign, day);
            }
            int reach = FlareRules.SponsoredReach(file.Followers);
            text += "\n" + (reach > 0 ? FlareText.Fill("reallife_flare_shop_sponsored", "share", (reach / 10).ToString())
                : FlareText.Fill("reallife_flare_shop_sponsored_later", "followers", FlareText.N(FlareRules.SponsoredFrom)));
            return text;
        }

        // ---------------------------------------------------------------- what a card shows

        /// <summary>Who wrote a post: their name, their handle and their account's key.</summary>
        private void AuthorOf(FlarePost p, out string name, out string handle, out string key)
        {
            key = FlareLiveRules.KeyOf(p);
            FlareFile file = _director.File;
            switch (p.Author)
            {
                case FlareAuthor.Player:
                    name = _director.MyName;
                    handle = _director.MyHandle;
                    return;
                case FlareAuthor.Friend:
                    name = _director.PersonName(p.Who) ?? "?";
                    handle = FlareText.HandleOf(name);
                    return;
                case FlareAuthor.Citizen:
                    if (p.Who >= 0 && p.Who < _director.Citizens.Count)
                    {
                        name = _director.Citizens[p.Who].First + " " + _director.Citizens[p.Who].Last;
                        handle = _director.Citizens[p.Who].Handle;
                        return;
                    }
                    name = "?";
                    handle = string.Empty;
                    return;
                case FlareAuthor.Official:
                    name = FlareText.OfficialName(p.Who);
                    handle = FlareText.OfficialHandle(p.Who);
                    return;
                case FlareAuthor.Influencer:
                    FlareInfluencer inf = file != null ? file.Influencer(p.Who) : null;
                    string known = inf != null && inf.Person >= 0 ? _director.PersonName(inf.Person) : null;
                    name = inf == null ? "?" : known != null ? known : inf.Handle.TrimStart('@');
                    handle = inf != null ? inf.Handle : string.Empty;
                    return;
                case FlareAuthor.Network:
                    name = p.AuthorName ?? p.AuthorId ?? "?";
                    handle = FlareText.HandleOf(p.AuthorId);
                    return;
                case FlareAuthor.Ledger:
                    name = FlareText.T("reallife_flare_ledger_name");
                    handle = "@cityledger";
                    return;
                default:
                    name = p.Name ?? "?";
                    handle = FlareText.HandleOf(name);
                    return;
            }
        }

        private string WordsOf(FlarePost p, string author)
        {
            string model = _director.ModelWords(p);   // (RLO Texts: the model's words, when they were written in time)
            if (model != null)
            {
                return model;
            }
            string built = FlareText.Built(p, author);
            if (built != null)
            {
                return built;
            }
            if (p.Source == FlareSource.Reply)
            {
                return p.Text ?? string.Empty;
            }
            FlarePerson who = default(FlarePerson);
            bool friend = p.Author == FlareAuthor.Friend && _director.Person(p.Who, out who);
            FlareInfluencer inf = p.Author == FlareAuthor.Influencer && _director.File != null ? _director.File.Influencer(p.Who) : null;
            if (inf != null)
            {
                return FlareText.Post(p, null, null, FlareGame.DistrictName(inf.District), (int)inf.Niche);
            }
            return FlareText.Post(p, _director.PersonName(p.Tag), friend ? who.Job : string.Empty, friend ? who.District : string.Empty);
        }

        private string ContextOf(FlarePost p)
        {
            FlareFile file = _director.File;
            if (p.ReplyTo >= 0 && file != null && !(_at.Screen == FlareScreen.Thread))
            {
                FlarePost parent = file.Post(p.ReplyTo);
                if (parent != null)
                {
                    string name, handle, key;
                    AuthorOf(parent, out name, out handle, out key);
                    return FlareText.Fill("reallife_flare_replying_to", "handle", handle);
                }
            }
            if (p.Author == FlareAuthor.Player && p.Tag >= 0 && _director.PersonName(p.Tag) != null)
            {
                return FlareText.Fill("reallife_flare_with_friend", "friend", _director.PersonName(p.Tag));
            }
            if (p.Mention != null && p.Mention.Length > 2)
            {
                return FlareText.Fill("reallife_flare_calling", "account", _director.AccountName(p.Mention));
            }
            if (p.Author == FlareAuthor.Player && p.Viral)
            {
                return FlareText.T("reallife_flare_post_viral");
            }
            return null;
        }

        private static FlareGlyph GlyphOf(FlarePost p)
        {
            switch (p.Source)
            {
                case FlareSource.Report: return FlareGlyph.Flag;
                case FlareSource.Review: return FlareGlyph.StarFull;
                case FlareSource.Announce:
                case FlareSource.Opened:
                case FlareSource.RecordDay:
                case FlareSource.Interior: return FlareGlyph.Store;
                case FlareSource.Invite: return FlareGlyph.Calendar;
                case FlareSource.Photo:
                case FlareSource.Selfie: return FlareGlyph.Picture;
                case FlareSource.Outing: return FlareGlyph.Person;
                case FlareSource.Voted:
                case FlareSource.Endorsed:
                case FlareSource.Campaign: return FlareGlyph.Check;
                case FlareSource.Issue: return FlareGlyph.Flag;
                default: return FlareGlyph.HeartFull;
            }
        }

        private Texture2D PictureOf(FlarePost p)
        {
            FlareFile file = _director.File;
            if (file == null || Hidden(file, p))
            {
                return null;
            }
            return _pictures.Of(FlarePictures.KeyOf(p));
        }

        // A report's case as it stands, in a line: the answer due, the works and when they end, the patrols, the cleaning, an inspection.
        private string CaseLineOf(FlarePost p)
        {
            FlareFile file = _director.File;
            if (file == null || p.Source != FlareSource.Report)
            {
                return null;
            }
            FlareCase c = null;
            for (int i = 0; i < file.Net.Cases.Count && c == null; i++)
            {
                c = file.Net.Cases[i].Post == p.Id ? file.Net.Cases[i] : null;
            }
            if (c == null && p.Case >= 0)
            {
                c = file.Net.Case(p.Case);
            }
            if (c == null)
            {
                return null;
            }
            string line;
            switch (c.State)
            {
                case FlareCaseState.Open:
                    line = FlareText.Fill("reallife_flare_case_open", "time", FlareText.Clock((int)(c.ReplyAt % 1440)));
                    break;
                case FlareCaseState.Queued:
                    line = FlareText.T("reallife_flare_case_queued");
                    break;
                case FlareCaseState.Done:
                    line = FlareText.T(c.Order == FlareOrder.Answered ? "reallife_flare_case_answered" : "reallife_flare_case_done");
                    break;
                default:
                    switch (c.Order)
                    {
                        case FlareOrder.Works:
                            FlarePost answer = c.Answer >= 0 ? file.Post(c.Answer) : null;
                            line = FlareText.Fill("reallife_flare_case_works", "address", answer != null && answer.Name != null ? answer.Name : FlareText.District(c.District),
                                "days", answer != null && answer.When > 0 ? answer.When.ToString() : "?");
                            break;
                        case FlareOrder.Patrol:
                            line = FlareText.Fill("reallife_flare_case_patrol", "district", FlareText.District(c.District), "day", c.Until.ToString());
                            break;
                        case FlareOrder.Cleanup:
                            line = FlareText.Fill("reallife_flare_case_cleanup", "district", FlareText.District(c.District), "day", c.Until.ToString());
                            break;
                        default:
                            line = FlareText.Fill("reallife_flare_case_inspection", "day", c.Until.ToString());
                            break;
                    }
                    break;
            }
            if (c.Repeats > 0)
            {
                line += "  ·  " + FlareText.Fill("reallife_flare_case_repeats", "count", c.Repeats.ToString());
            }
            return line;
        }

        private int RepliesOf(FlarePost p, long now)
        {
            FlareFile file = _director.File;
            int n = FlareLiveRules.Shown(p.Replies, p, now);
            for (int i = 0; file != null && i < file.Posts.Count; i++)
            {
                n += file.Posts[i].ReplyTo == p.Id && file.Posts[i].At <= now ? 1 : 0;
            }
            return n;
        }

        // A notification's who: a friend, the partner, one of the staff, the boss, one of the city's people, an account.
        private string ReactorName(string who)
        {
            if (string.IsNullOrEmpty(who))
            {
                return "?";
            }
            int n;
            if (who[0] == 'p' && who.Length > 1 && who[1] != ':' && int.TryParse(who.Substring(1), out n))
            {
                return _director.PersonName(n) ?? "?";
            }
            if (who == "b")
            {
                return FlareText.T("reallife_flare_your_boss");
            }
            return _director.AccountName(who);
        }

        private string SnippetOf(FlareNotice n)
        {
            FlareFile file = _director.File;
            if (n.Dm >= 0 && file != null)
            {
                for (int i = 0; i < file.Net.Dms.Count; i++)
                {
                    if (file.Net.Dms[i].Id == n.Dm)
                    {
                        return Cut(_director.DmWords(file.Net.Dms[i]) ?? FlareText.DmWords(file.Net.Dms[i], file.Net.Dms[i].Name));
                    }
                }
            }
            FlarePost p = n.Post >= 0 && file != null ? file.Post(n.Post) : null;
            if (p == null)
            {
                return null;
            }
            string name, handle, key;
            AuthorOf(p, out name, out handle, out key);
            return Cut(p.Text ?? WordsOf(p, name));
        }

        private static string Cut(string text)
        {
            return text != null && text.Length > 160 ? text.Substring(0, 157) + "..." : text;
        }

        /// <summary>Whether an account can be followed (any but the player and their friends).</summary>
        private static bool Followable(string key)
        {
            return key != null && key != "me" && !(key.Length > 1 && key[0] == 'f' && char.IsDigit(key[1]));
        }

        // ---------------------------------------------------------------- clicks

        private void PostClicked(FlareRow row)
        {
            if (row?.Post != null && row.Post.Id >= 0 && !(row.Number == 1 && _at.Screen == FlareScreen.Thread))
            {
                Open(FlareScreen.Thread, 0, row.Post.Id);
            }
        }

        private void AuthorClicked(FlareRow row)
        {
            if (row?.Post == null)
            {
                return;
            }
            string key = FlareLiveRules.KeyOf(row.Post);
            if (key != null)
            {
                Open(FlareScreen.Profile, 0, -1, key);
            }
        }

        private void LikeClicked(FlareRow row)
        {
            if (row?.Post != null && row.Post.Id >= 0)
            {
                _director.Like(row.Post.Id);
            }
        }

        private void RepostClicked(FlareRow row)
        {
            if (row?.Post != null && row.Post.Id >= 0)
            {
                _director.Repost(row.Post.Id);
            }
        }

        private void ReplyClicked(FlareRow row)
        {
            if (row?.Post != null && row.Post.Id >= 0)
            {
                OpenCompose(-1, row.Post.Id);
            }
        }

        private void GoingClicked(FlareRow row)
        {
            if (row?.Post != null && row.Post.Id >= 0)
            {
                _director.Going(row.Post.Id);
            }
        }

        private void NoticeClicked(FlareRow row)
        {
            if (row == null)
            {
                return;
            }
            if (row.Notice.Kind == FlareNoticeKind.Dm && row.Dm != null)
            {
                Open(FlareScreen.Conversation, 0, -1, row.Dm.With);
                return;
            }
            FlarePost p = row.Post;
            if (p == null)
            {
                return;
            }
            bool reply = row.Notice.Kind == FlareNoticeKind.Reply || row.Notice.Kind == FlareNoticeKind.Case;
            Open(FlareScreen.Thread, 0, reply && p.ReplyTo >= 0 ? p.ReplyTo : p.Id);
        }

        private void AccountClicked(FlareRow row)
        {
            if (row?.Key != null)
            {
                Open(FlareScreen.Profile, 0, -1, row.Key);
            }
        }

        private void FollowAccount(FlareRow row)
        {
            FlareFile file = _director.File;
            if (file != null && row?.Key != null)
            {
                _director.Follow(row.Key, !FlareCast.Followed(file, row.Key));
            }
        }

        private void ConversationClicked(FlareRow row)
        {
            if (row?.Dm != null)
            {
                Open(FlareScreen.Conversation, 0, -1, row.Dm.With);
            }
        }

        private void AnnounceClicked(FlareRow row)
        {
            if (row?.Key != null)
            {
                OpenCompose(-1, -1);
                _compose.Kind = FlareSource.Announce;
                _compose.Place = row.Key;
                _compose.Name = row.Text;
                WriteSheet();
            }
        }

        private void PromoteClicked(FlareRow row)
        {
            if (row?.Key != null)
            {
                OpenHire(row.Key, row.Text, -1);
            }
        }

        private void EndDealClicked(FlareRow row)
        {
            if (row != null && row.Number > 0)
            {
                _director.EndHire(row.Number);
            }
        }

        private void RowButton(FlareRow row)
        {
            if (row == null)
            {
                return;
            }
            switch (row.Key)
            {
                case "reply":
                    OpenCompose(-1, row.Number);
                    break;
                case "write":
                    string with = _at.Key;
                    AskText("reallife_flare_dm_placeholder", "reallife_flare_send", null, FlareLiveRules.DmTextMost, text =>
                    {
                        if (_director.SendDm(with, text))
                        {
                            Refresh(false);
                        }
                    });
                    break;
                default:
                    OpenCompose(-1, -1);
                    break;
            }
        }

        /// <summary>The game's own text box (the one the game names a business with), for what the player types.</summary>
        private static void AskText(string placeholderKey, string buttonKey, string initial, int most, Action<string> done)
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            uis.textInputUi.Show(placeholderKey, buttonKey, text => UiClone.Safe(() => done(text)), null, initial ?? string.Empty, true, most);
        }
    }
}
