using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face, its rows: a post's author, words, tags, picture, line and counts read from the account; who an account
    /// is; and for every row what pressing it does, kept beside it so a press acts on the post, the account or the review it
    /// shows. Filled when a page is written, never per frame.
    /// </summary>
    public sealed partial class FlareScreen
    {
        // What a row's press does.
        private enum RowAct : byte
        {
            None = 0,
            Thread,         // a post: its thread
            Profile,        // an account: its profile
            Tag,            // a tag: its posts
            Conversation,   // a conversation, or the message a notice is about
            Review,         // a review post of one of the player's businesses: the reply sheet
            Note,           // a review a resident left (Flare in the city): the reply sheet
            Hire,           // an influencer: the hire sheet
            Trend,          // the city's trend: a post about it
            Going,          // an invitation: going or not
            Business,       // one of the player's businesses: its page
            Campaign        // a campaign running: its influencer's conversation, and the button that ends it
        }

        private struct RowRef
        {
            public RowAct Act;
            public FlarePost Post;           // the post the row shows, or null
            public string Key;               // its author's account, or the account, tag or conversation the row is
            public int Id;                   // an influencer's id, a shop's place in the picker
            public FlareReviewNote Note;     // a resident's review
        }

        private readonly List<RowRef> _refs = new List<RowRef>(64);
        private readonly Dictionary<int, int> _replyCount = new Dictionary<int, int>(64);
        private readonly string[] _tagScratch = new string[FlareFeedRules.TagsShown];

        // ------------------------------------------------------------------------------------------------ the list's rows

        private void ClearList(FlareList list)
        {
            list.Clear();
            if (list == _m.List)
            {
                _refs.Clear();
            }
        }

        private ref FlareRow Add(FlareList list, RowAct act, FlarePost post = null, string key = null, int id = 0, FlareReviewNote note = null)
        {
            if (list == _m.List)
            {
                _refs.Add(new RowRef { Act = act, Post = post, Key = key, Id = id, Note = note });
            }
            return ref list.Add();
        }

        private RowRef RefAt(int index) => index >= 0 && index < _refs.Count ? _refs[index] : default(RowRef);

        private void Section(string words)
        {
            ref FlareRow r = ref Add(_m.List, RowAct.None);
            r.Kind = FlareRowKind.Section;
            r.Name = words;
        }

        // The replies under each post, counted once for a page.
        private void CountReplies(FlareFile file, long now)
        {
            _replyCount.Clear();
            for (int i = 0; i < file.Posts.Count; i++)
            {
                FlarePost p = file.Posts[i];
                if (p.ReplyTo >= 0 && p.At <= now && !Hidden(file, p))
                {
                    _replyCount.TryGetValue(p.ReplyTo, out int n);
                    _replyCount[p.ReplyTo] = n + 1;
                }
            }
        }

        private static bool Hidden(FlareFile file, FlarePost p) => p.Author == FlareAuthor.Network && file.Net.IsHidden(p.AuthorId);

        /// <summary>A post's card: its author, when, its words with its tags under them, its picture, its line and its counts.</summary>
        private void PostRow(FlareFile file, FlarePost p, long now, bool comment = false)
        {
            Author(p, out string name, out string handle, out string key, out bool verified);
            ref FlareRow r = ref Add(_m.List, comment ? RowAct.None : RowAct.Thread, p, key);
            r.Kind = FlareRowKind.Post;
            r.Name = name;
            r.Handle = handle;
            r.Hue = UiColour.HueOf(key ?? name);
            r.Time = Ago(p.At, now);
            r.Flags = (verified ? FlareRowFlags.Verified : FlareRowFlags.None) | (p.Liked ? FlareRowFlags.Liked : FlareRowFlags.None)
                      | (p.Reposted ? FlareRowFlags.Reposted : FlareRowFlags.None) | (comment ? FlareRowFlags.Comment : FlareRowFlags.None)
                      | (p.Author == FlareAuthor.Player ? FlareRowFlags.Mine : FlareRowFlags.None);
            LookOfPost(p, ref r, key);
            string built = _flare.WordsFor(p, name);
            string words = p.Text ?? built;
            r.Text = FlareFeedRules.Plain(words);
            int tags = FlareFeedRules.TagsOf(p.Topic, _tagScratch);
            if (tags > 0)
            {
                r.Tags = new string[tags];
                for (int t = 0; t < tags; t++)
                {
                    r.Tags[t] = "#" + _tagScratch[t];
                }
            }
            r.Picture = Hidden(file, p) ? null : FlarePictures.KeyOf(p) ?? _flare.CityPhotoKey(p);
            r.Line = LineOf(file, p, built, out UiIcon icon);
            r.Icon = icon;
            int likes = FlareLiveRules.Shown(p.Likes, p, now) + p.PlayerLikes + (p.Liked ? 1 : 0);
            _replyCount.TryGetValue(p.Id, out int replies);
            replies += p.Id >= 0 ? FlareLiveRules.Shown(p.Replies, p, now) : 0;
            r.Likes = likes > 0 ? FlareFeedRules.Count(likes) : null;
            r.Comments = replies > 0 ? FlareFeedRules.Count(replies) : null;
            int reposts = FlareLiveRules.Shown(p.Reposts, p, now);
            r.Reposts = reposts > 0 ? FlareFeedRules.Count(reposts) : null;
            int views = FlareLiveRules.Shown(p.Reach, p, now);
            r.Views = views > 0 ? FlareFeedRules.Count(views) : null;
            ResultOf(file, p, now, ref r);
        }

        // The line over a post: promoted where, a paid partnership, replying to whom, calling on whom, the built part of a typed post.
        private string LineOf(FlareFile file, FlarePost p, string built, out UiIcon icon)
        {
            icon = UiIcon.Megaphone;
            if ((p.Author == FlareAuthor.Player && FlareFeedRules.Promoted(file.Boosts, p.Id)) || _flare.RivalPaid(p))
            {
                return string.IsNullOrEmpty(p.District) ? FlareText.T("reallife_flare_kit_promoted_city")
                    : FlareText.Fill("reallife_flare_kit_promoted", "district", FlareText.District(p.District));
            }
            if (p.Source == FlareSource.Hired || p.Source == FlareSource.Sponsored || p.Source == FlareSource.Rival)
            {
                icon = UiIcon.Briefcase;
                return FlareText.Fill("reallife_flare_kit_partnership", "business", p.Name ?? "?");
            }
            if (p.ReplyTo >= 0 && _page != FlareFace.Thread)
            {
                FlarePost parent = file.Post(p.ReplyTo);
                if (parent != null)
                {
                    Author(parent, out _, out string handle, out _, out _);
                    icon = UiIcon.Message;
                    return FlareText.Fill("reallife_flare_replying_to", "handle", handle);
                }
            }
            if (p.Mention != null && p.Mention.Length > 2)
            {
                icon = UiIcon.Person;
                return FlareText.Fill("reallife_flare_calling", "account", _flare.AccountName(p.Mention));
            }
            if (p.Author == FlareAuthor.Player && p.Viral)
            {
                icon = UiIcon.Fire;
                return FlareText.T("reallife_flare_post_viral");
            }
            if (p.Text != null && built != null && p.Source != FlareSource.Reply && p.Source != FlareSource.Life)
            {
                icon = p.Source == FlareSource.Review ? UiIcon.Star : p.Source == FlareSource.Report ? UiIcon.Flag : UiIcon.Store;
                return built;   // a typed post keeps what it is about: the review, the report, the announcement
            }
            return null;
        }

        // What a post of the player's brought (its boost's visitors and money), a poll's votes, in the line under its picture.
        private void ResultOf(FlareFile file, FlarePost p, long now, ref FlareRow r)
        {
            if (FlareFeedRules.Poll(p, now, out int yes, out int votes))
            {
                r.Value = FlareText.Fill("reallife_flare_kit_poll", "yes", yes.ToString(), "no", (100 - yes).ToString(), "votes", FlareText.N(votes));
                return;
            }
            if (p.Author != FlareAuthor.Player)
            {
                return;
            }
            FlareBoost b = BoostOf(file, p.Id);
            if (b == null)
            {
                return;
            }
            FlareUseRules.Credited(b, (int)Math.Min(int.MaxValue, now), out int given, out int givenCents);   // what the hours that ran brought
            if (b.Flopped && given <= 0)
            {
                r.Value = FlareText.T("reallife_flare_kit_result_flop");
                r.Flags |= FlareRowFlags.Bad;
                return;
            }
            if (given > 0)
            {
                r.Value = FlareText.Fill("reallife_flare_kit_brought", "visitors", FlareText.N(given), "money", FlareText.Money(givenCents / 100));
                r.Flags |= FlareRowFlags.Good;
            }
            else
            {
                r.Value = FlareText.Fill("reallife_flare_kit_expected", "visitors", FlareText.N(b.Visitors), "hours", b.Hours.ToString());
            }
        }

        private static FlareBoost BoostOf(FlareFile file, int post)
        {
            for (int i = file.Boosts.Count - 1; i >= 0; i--)
            {
                if (file.Boosts[i].Post == post)
                {
                    return file.Boosts[i];
                }
            }
            return null;
        }

        private static bool HasTag(string topic, string tag) => FlareFeedRules.HasTag(topic, tag);

        // ------------------------------------------------------------------------------------------------ the face an account wears

        // A person's face is painted from their name on the ring of their mood where the game has one (a friend's mood, the player's happiness, an influencer's record);
        // a business, a brand or an office keeps its initials. The name is set before this is asked.
        private void Look(string key, ref FlareRow r)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            FlareFile file = _flare.File;
            int n;
            if (key == "me" || key == _flare.MeKey)
            {
                r.Flags |= FlareRowFlags.Person;
                r.Ring = _m.MyRing;
            }
            else if (key.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(key.Substring(2), out n))
            {
                FlareInfluencer inf = file != null ? file.Influencer(n) : null;
                if (inf != null)
                {
                    r.Flags |= FlareRowFlags.Person;
                    r.Ring = UiTokens.Grade(inf.Reliability);
                }
            }
            else if (key.Length > 1 && key[0] == 'f' && int.TryParse(key.Substring(1), out n) && _flare.Person(n, out FlarePerson person))
            {
                r.Flags |= FlareRowFlags.Person;
                r.Ring = FlareFace.MoodRing(person.Mood);
                UiFaces.Traits(r.Name, person.Female, person.Age);
            }
            else if (key.Length > 1 && key[0] == 'c' && key[1] != ':' && int.TryParse(key.Substring(1), out n) && n >= 0 && n < _flare.Citizens.Count)
            {
                FlareCitizen c = _flare.Citizens[n];
                r.Flags |= FlareRowFlags.Person;
                UiFaces.Traits(r.Name, c.Female, c.Age);   // no mood is known of the city's people: no ring
            }
        }

        // The face a post's author wears: the account is told from who wrote it, since a friend's or the player's own post has no key.
        private void LookOfPost(FlarePost p, ref FlareRow r, string key)
        {
            switch (p.Author)
            {
                case FlareAuthor.Player:
                    Look("me", ref r);
                    break;
                case FlareAuthor.Friend:
                    Look("f" + p.Who.ToString(CultureInfo.InvariantCulture), ref r);
                    break;
                case FlareAuthor.Citizen:
                    Look("c" + p.Who.ToString(CultureInfo.InvariantCulture), ref r);
                    break;
                case FlareAuthor.Influencer:
                    Look("i:" + p.Who.ToString(CultureInfo.InvariantCulture), ref r);
                    break;
                default:
                    Look(key, ref r);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------ who wrote it

        /// <summary>A post's author as the card shows them: name, handle, account key and Flare's mark.</summary>
        private void Author(FlarePost p, out string name, out string handle, out string key, out bool verified)
        {
            key = FlareLiveRules.KeyOf(p);
            FlareFile file = _flare.File;
            verified = false;
            switch (p.Author)
            {
                case FlareAuthor.Player:
                    name = _flare.MyName;
                    handle = _flare.MyHandle;
                    verified = file != null && FlareUseRules.Verified(file.Followers, false);
                    return;
                case FlareAuthor.Friend:
                    name = _flare.PersonName(p.Who) ?? "?";
                    handle = FlareText.HandleOf(name);
                    return;
                case FlareAuthor.Citizen:
                    if (p.Who >= 0 && p.Who < _flare.Citizens.Count)
                    {
                        FlareCitizen c = _flare.Citizens[p.Who];
                        name = c.First + " " + c.Last;
                        handle = c.Handle;
                        return;
                    }
                    name = "?";
                    handle = string.Empty;
                    return;
                case FlareAuthor.Official:
                    name = FlareText.OfficialName(p.Who);
                    handle = FlareText.OfficialHandle(p.Who);
                    verified = true;
                    return;
                case FlareAuthor.Influencer:
                    FlareInfluencer inf = file != null ? file.Influencer(p.Who) : null;
                    string known = inf != null && inf.Person >= 0 ? _flare.PersonName(inf.Person) : null;
                    name = inf == null ? "?" : known ?? inf.Handle.TrimStart('@');
                    handle = inf != null ? inf.Handle : string.Empty;
                    verified = inf != null && FlareUseRules.Verified(inf.Followers, false);
                    return;
                case FlareAuthor.Network:
                    name = p.AuthorName ?? p.AuthorId ?? "?";
                    handle = FlareText.HandleOf(p.AuthorId);
                    return;
                case FlareAuthor.Ledger:
                    name = FlareText.T("reallife_flare_ledger_name");
                    handle = "@cityledger";
                    verified = true;
                    return;
                default:
                    name = p.Name ?? "?";
                    handle = FlareText.HandleOf(name);
                    verified = p.Author == FlareAuthor.Business || p.Author == FlareAuthor.Brand || p.Author == FlareAuthor.Company || p.Author == FlareAuthor.Tycoon;
                    return;
            }
        }

        /// <summary>An account's name, handle and what it is, by its key: the profile's head and a conversation's.</summary>
        private void Account(string key, out string name, out string handle, out string about, out bool verified)
        {
            FlareFile file = _flare.File;
            verified = false;
            about = null;
            if (key == "me" || key == _flare.MeKey)
            {
                name = _flare.MyName;
                handle = _flare.MyHandle;
                verified = file != null && FlareUseRules.Verified(file.Followers, false);
                about = file != null && !string.IsNullOrEmpty(file.Bio) ? file.Bio
                    : file != null ? FlareText.Fill("reallife_flare_about_me", "tier", FlareText.Tier(file.Tier), "reputation", FlareText.Reputation(file.Reputation)) : null;
                return;
            }
            name = _flare.AccountName(key);
            handle = FlareText.HandleOf(name);
            int n;
            if (key.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(key.Substring(2), out n) && file != null && file.Influencer(n) != null)
            {
                FlareInfluencer inf = file.Influencer(n);
                string known = inf.Person >= 0 ? _flare.PersonName(inf.Person) : null;
                name = known ?? inf.Handle.TrimStart('@');
                handle = inf.Handle;
                verified = FlareUseRules.Verified(inf.Followers, false);
                about = FlareText.Fill("reallife_flare_profile_what", "niche", FlareText.Niche(inf.Niche), "district", FlareGame.DistrictName(inf.District),
                    "style", FlareText.Style(inf.Style));
                return;
            }
            if (key.Length > 1 && key[0] == 'c' && key[1] != ':' && int.TryParse(key.Substring(1), out n) && n >= 0 && n < _flare.Citizens.Count)
            {
                FlareCitizen c = _flare.Citizens[n];
                name = c.First + " " + c.Last;
                handle = c.Handle;
                about = FlareText.Fill("reallife_flare_about_citizen", "district", FlareText.District(c.District));
                return;
            }
            if (key.Length > 1 && key[0] == 'f' && int.TryParse(key.Substring(1), out n) && _flare.Person(n, out FlarePerson person))
            {
                about = FlareText.Fill("reallife_flare_about_friend", "job", person.Job, "district", person.District);
                return;
            }
            switch (key[0])
            {
                case 'o':
                    if (int.TryParse(key.Substring(2), out n))
                    {
                        handle = FlareText.OfficialHandle(n);
                    }
                    verified = true;
                    about = FlareText.T("reallife_flare_about_official");
                    return;
                case 'p':
                    about = FlareText.T("reallife_flare_about_player");
                    return;
                case 'b':
                    verified = true;
                    about = FlareText.T("reallife_flare_account_brand_about");
                    return;
                case 'k':
                    verified = true;
                    about = FlareText.T("reallife_flare_about_business");
                    return;
                case 't':
                    verified = true;
                    about = FlareText.T("reallife_flare_about_tycoon");
                    return;
                case 'd':
                    verified = true;
                    about = FlareText.T("reallife_flare_kit_kind_sponsor");
                    return;
                case 'l':
                    handle = "@cityledger";
                    verified = true;
                    return;
            }
        }

        // Whether the player may follow an account: not themselves, nor a friend of their own life (they already follow each other).
        private static bool Followable(string key) => FlareFeedRules.Followable(key);

        private bool Following(string key) => _flare.File != null && FlareCast.Followed(_flare.File, key);

        // ------------------------------------------------------------------------------------------------ when

        /// <summary>How long ago, in the feed's words: now, 5m, 2h, 3d, or the day.</summary>
        public static string Ago(long at, long now)
        {
            FlareAgo ago = FlareFeedRules.Ago(at, now, out long n);
            string count = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            switch (ago)
            {
                case FlareAgo.Now: return FlareText.T("reallife_flare_ago_now");
                case FlareAgo.Minutes: return FlareText.Fill("reallife_flare_ago_min", "n", count);
                case FlareAgo.Hours: return FlareText.Fill("reallife_flare_ago_hour", "n", count);
                case FlareAgo.Days: return FlareText.Fill("reallife_flare_kit_ago_day", "n", count);
                default: return FlareText.Fill("reallife_flare_day", "day", count);
            }
        }

        // ------------------------------------------------------------------------------------------------ the rows' presses

        private bool OpenRow(int index)
        {
            RowRef r = RefAt(index);
            switch (r.Act)
            {
                case RowAct.Thread: return r.Post != null && r.Post.Id >= 0 && OpenThread(r.Post.Id);
                case RowAct.Profile: return OpenProfile(r.Key);
                case RowAct.Tag: return OpenTag(r.Key);
                case RowAct.Conversation:
                case RowAct.Campaign: return OpenConversation(r.Key);
                case RowAct.Business: return OpenBusiness(r.Id);
                case RowAct.Review:
                case RowAct.Note: return OpenReply(r);
                case RowAct.Hire:
                    _hireSel = r.Id;   // the row chooses the influencer the card under the table tells of; the button hires them
                    _dirty = true;
                    return true;
                default: return false;
            }
        }

        // A row's own button: Reply on a review, Hire on an influencer, a city card's action.
        private bool RowButton(int index)
        {
            RowRef r = RefAt(index);
            switch (r.Act)
            {
                case RowAct.Review:
                case RowAct.Note: return OpenReply(r);
                case RowAct.Hire: return OpenHire(r.Id);
                case RowAct.Trend: return PromoteTrend(r.Id, r.Key);
                case RowAct.Campaign: return _kit.End(r.Id);
                case RowAct.Going:
                    if (r.Post == null)
                    {
                        return false;
                    }
                    _flare.Going(r.Post.Id);
                    return true;
                case RowAct.Conversation: return OpenConversation(r.Key);
                case RowAct.Thread: return r.Post != null && r.Post.Id >= 0 && OpenThread(r.Post.Id);
                case RowAct.Profile: return OpenProfile(r.Key);
                default: return false;
            }
        }

        private bool Like(int index)
        {
            FlarePost p = RefAt(index).Post;
            if (p == null)
            {
                return false;
            }
            if (p.Id >= 0)
            {
                _flare.Like(p.Id);
            }
            else if (p.Slot >= 0 && FlareCommentRules.IsDisplay(p.Id))
            {
                _flare.LikeComment(p.ReplyTo, p.Slot, FlareCommentRules.KindOf(p.Author), p.Who);   // a comment kept by its post, slot and who
            }
            else if (p.Slot >= 0)
            {
                _flare.LikeReply(p.ReplyTo, p.Slot);   // a friend's reply, kept by its post and place
            }
            else
            {
                return false;
            }
            return true;
        }

        private bool Repost(int index)
        {
            FlarePost p = RefAt(index).Post;
            if (p == null || p.Id < 0 || p.Author == FlareAuthor.Player)
            {
                return false;
            }
            _flare.Repost(p.Id);
            return true;
        }

        // The comment count: the post's thread, its field ready for a reply.
        private bool Comment(int index)
        {
            FlarePost p = RefAt(index).Post;
            if (p == null)
            {
                return false;
            }
            int id = p.Id >= 0 ? p.Id : p.ReplyTo;
            if (_page == FlareFace.Thread && id == _post)
            {
                _field = FlareFace.ReplyField;
                return true;
            }
            return id >= 0 && OpenThread(id);
        }

        // Share: a new post that calls on the author, as the feed's share sheet does.
        private bool Share(int index)
        {
            RowRef r = RefAt(index);
            if (r.Post == null)
            {
                return false;
            }
            if (!OpenCompose(-1))
            {
                return false;
            }
            _mention = r.Key != null && r.Key != "me" && r.Key != _flare.MeKey ? r.Key : null;
            Author(r.Post, out string name, out _, out _, out _);
            _compose.Text = FlareLiveRules.Clip(FlareText.Fill("reallife_flare_kit_share_text", "name", name), FlareLiveRules.TextMost);
            return true;
        }

        private bool TagOfRow(int arg)
        {
            int index = arg / 4, t = arg % 4;
            if (index < 0 || index >= _m.List.Count)
            {
                return false;
            }
            string[] tags = _m.List.Rows[index].Tags;
            return tags != null && t < tags.Length && OpenTag(tags[t]);
        }

        private bool ToggleFollow(string key)
        {
            if (!Followable(key) || _flare.File == null)
            {
                return false;
            }
            _flare.Follow(key, !Following(key));
            return true;
        }

        private bool OpenBusiness(int shop)
        {
            Push();
            _shop = Math.Max(0, shop);
            Show(FlareFace.Business);
            return true;
        }
    }
}
