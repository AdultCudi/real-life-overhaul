using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The Flare app's screens and what each lists; the rows are made when a screen is written, one walk over the posts at most.</summary>
    public sealed partial class FlareApp
    {
        private readonly List<FlareReaction> _reactions = new List<FlareReaction>(16);
        private readonly List<FlarePost> _synthetic = new List<FlarePost>(16);
        private readonly List<FlareComment> _comments = new List<FlareComment>(FlareCommentRules.WrittenMost);
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
            _director.CommentsAhead(null);   // RLO Texts: a few of the comments under the feed's posts asked; once a game minute at most
        }

        private static bool Followed(FlareFile file, FlarePost p)
        {
            return p.Author == FlareAuthor.Player || p.Author == FlareAuthor.Friend || p.Author == FlareAuthor.Network || FlareCast.Followed(file, FlareLiveRules.KeyOf(p));
        }

        private static bool Hidden(FlareFile file, FlarePost p)
        {
            return p.Author == FlareAuthor.Network && file.Net.IsHidden(p.AuthorId);
        }

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
            for (int i = 0; i < file.Posts.Count; i++)
            {
                FlarePost r = file.Posts[i];
                if (r.ReplyTo == p.Id && r.At <= now && !Hidden(file, r))
                {
                    while (p.Author != FlareAuthor.Player && next < _synthetic.Count && _synthetic[next].At <= r.At)
                    {
                        Row(CellPost).Post = _synthetic[next++];
                    }
                    Row(CellPost).Post = r;
                    FlarePost answer = r.Author == FlareAuthor.Player ? _director.AnswerShown(r, now) : null;   // RLO Texts: the author answering the player's reply, right under it
                    if (answer != null)
                    {
                        Row(CellPost).Post = answer;
                    }
                }
            }
            for (int i = next; i < _synthetic.Count; i++)
            {
                Row(CellPost).Post = _synthetic[i];
            }
            if (more > 0)
            {
                Note(more == 1 && _rows.Count == before ? FlareText.T("reallife_flare_comments_count_one")
                    : _rows.Count == before ? FlareText.Fill("reallife_flare_comments_count", "count", FlareText.N(more))
                    : more == 1 ? FlareText.T("reallife_flare_comments_more_one") : FlareText.Fill("reallife_flare_comments_more", "count", FlareText.N(more)));
            }
            else if (_rows.Count == before)
            {
                Note(FlareText.T("reallife_flare_no_replies"));
            }
            if (p.Author != FlareAuthor.Player)
            {
                _director.CommentsAhead(p);   // RLO Texts: the words of this post's comments still to come, asked first
            }
            FlareRow b = Row(CellButton);
            b.Key = "reply";
            b.Number = p.Id;
            b.Text = FlareText.T("reallife_flare_reply_button");
        }

        // The worded comments come in under another's post, as replies for display; how many of the count are not written (the "more" line).
        private int Comments(FlarePost p, long now)
        {
            int shown = FlareLiveRules.Shown(p.Replies, p, now), written = 0;
            _director.CommentsOf(p, _comments);
            int due = FlareCommentRules.Due(_comments, now);
            for (int i = 0; i < due; i++)
            {
                FlareComment c = _comments[i];
                string words = _director.CommentWords(p, c);
                if (words == null)
                {
                    continue;   // no words: not shown; it counts among the more
                }
                var comment = new FlarePost
                {
                    Id = FlareCommentRules.DisplayId(p.Id, c.Slot), Author = c.Kind == 'f' ? FlareAuthor.Friend : c.Kind == 'i' ? FlareAuthor.Influencer : FlareAuthor.Citizen,
                    Who = c.Who, Source = FlareSource.Reply, ReplyTo = p.Id, Text = words, Likes = c.Likes, Reach = 30, Slot = c.Slot,
                    Liked = _director.IsCommentLiked(p.Id, c.Slot, c.Kind, c.Who)
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
                        Text = _director.FriendWords(p.Id, index) ?? FlareText.Reply(FlareRules.ReplyOf(people[k], p), p, index), Seed = p.Seed + i, Slot = i,
                        Liked = _director.IsReplyLiked(p.Id, i)
                    };
                    FlareLiveRules.SetAt(reply, p.At + r.Delay);
                    _synthetic.Add(reply);
                }
            }
        }
    }
}
