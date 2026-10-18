using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare's living network, the feed: reactions over the hours, Trending and tags, typed text, messages, notifications and what is kept.</summary>
    public static partial class FlareLiveRules
    {
        // The reactions over the hours.
        public const int CurveMinutes = 150;           // likes come in on a curve: two thirds in the first two and a half hours
        public const int CitizensReactMost = 6;        // the city's people with names who react to one post
        public const int PileOnFrom = 150;             // a post that went badly and was seen by this many: a pile-on
        public const int PileOnLeast = 4, PileOnMost = 10;   // replies

        // The city's people's messages.
        public const int DmGapDays = 2;
        public const int DmChance = 450;               // per mille a day after the gap

        /// <summary>Who with a name reacts to a post of the player's, and when, as one short line kept on the post ("L:f3:12;R:c7:95").</summary>
        public static string Reactors(FlarePost p, IReadOnlyList<FlarePerson> people, IReadOnlyList<string> staff, bool boss, StringBuilder b)
        {
            b.Length = 0;
            for (int i = 0; people != null && i < people.Count; i++)
            {
                FlarePerson person = people[i];
                if (person.Index < 0 || person.Index > 30)
                {
                    continue;
                }
                int bit = 1 << person.Index;
                bool liked = (p.Likers & bit) != 0, replied = (p.Repliers & bit) != 0;
                if (!liked && !replied)
                {
                    continue;
                }
                string who = (person.Partner ? "p" : "f") + person.Index;
                int delay = person.Partner ? 3 + FlareRules.Roll(p.Seed, person.Index, 801) % 30 : 5 + FlareRules.Roll(p.Seed, person.Index, 802) % 240;
                Add(b, replied ? 'R' : 'L', who, delay);
            }
            for (int i = 0; staff != null && i < staff.Count && i < 8; i++)
            {
                if (FlareRules.Roll(p.Seed, i, 803) < 350 && !p.Bad)
                {
                    Add(b, 'L', "s:" + Plain(staff[i]), 20 + FlareRules.Roll(p.Seed, i, 804) % 600);
                }
            }
            if (boss)
            {
                Add(b, 'L', "b", 60 + FlareRules.Roll(p.Seed, 1, 805) % 240);
            }
            int citizens = FlareRules.Clamp(1 + p.Reach / 300, 1, CitizensReactMost);
            for (int i = 0; i < citizens; i++)
            {
                int c = PickCitizen(p.Seed, i, 806);
                char kind = p.Reach > 1000 && FlareRules.Roll(p.Seed, i, 807) < 250 ? 'S' : 'L';
                Add(b, kind, "c" + c, 2 + FlareRules.Roll(p.Seed, i, 808) % 420);
            }
            return b.ToString();
        }

        private static void Add(StringBuilder b, char kind, string who, int delay)
        {
            if (b.Length > 0)
            {
                b.Append(';');
            }
            b.Append(kind).Append(':').Append(who).Append(':').Append(delay);
        }

        // A name as the reactions' line can carry it: none of its separators in it.
        private static string Plain(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "?";
            }
            var b = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length && b.Length < 32; i++)
            {
                char c = name[i];
                b.Append(c == ';' || c == ':' || c == '|' || c == '\n' || c == '\r' || c == '~' ? ' ' : c);
            }
            return b.ToString();
        }

        /// <summary>The reactions of a post's line, into <paramref name="into"/>; none for a line that does not read.</summary>
        public static void ReadReactors(string line, List<FlareReaction> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            int start = 0;
            while (start < line.Length)
            {
                int end = line.IndexOf(';', start);
                if (end < 0)
                {
                    end = line.Length;
                }
                int a = line.IndexOf(':', start);
                int z = line.LastIndexOf(':', end - 1);
                if (a > start && z > a && z < end)
                {
                    int delay;
                    if (int.TryParse(line.Substring(z + 1, end - z - 1), out delay))
                    {
                        into.Add(new FlareReaction { Kind = line[start], Who = line.Substring(a + 1, z - a - 1), Delay = Math.Max(0, delay) });
                    }
                }
                start = end + 1;
            }
        }

        /// <summary>How much of a post's figures have come in by now, thousandths: on a curve, all of it a day after.</summary>
        public static int Arrived(FlarePost p, long now) => ArrivedAfter(now - p.At);

        /// <summary>The same, so many game minutes after it went up.</summary>
        public static int ArrivedAfter(long since)
        {
            if (since <= 0)
            {
                return 0;
            }
            if (since >= 1440)
            {
                return 1000;
            }
            return (int)Math.Round(1000.0 * (1.0 - Math.Exp(-since / (double)CurveMinutes)) / (1.0 - Math.Exp(-1440.0 / CurveMinutes)));
        }

        public static int Shown(int total, FlarePost p, long now)
        {
            return (int)((long)Math.Max(0, total) * Arrived(p, now) / 1000);
        }

        /// <summary>The city's people's replies to a post of the player's, a post each at its minute; a pile-on for a bad one seen widely.</summary>
        public static int CityReplies(FlareFile f, FlarePost p, List<FlarePost> made)
        {
            int count = p.Bad && p.Reach >= PileOnFrom ? FlareRules.Clamp(PileOnLeast + p.Reach / 2000, PileOnLeast, PileOnMost)
                : FlareRules.Clamp(p.Reach / 500 + (p.Viral ? 3 : 0), 0, 5);
            for (int i = 0; i < count; i++)
            {
                int id = f.NextId++;
                long at = p.At + 4 + FlareRules.Roll(p.Seed, i, 820) % (p.Bad ? 180 : 360);
                var r = new FlarePost
                {
                    Id = id, Author = FlareAuthor.Citizen, Who = PickCitizen(p.Seed, i, 821), Source = FlareSource.Reply, ReplyTo = p.Id,
                    Arg = (int)ReplyKindOf(p, FlareRules.Roll(p.Seed, i, 822)), Seed = FlareRules.Roll(p.Seed, id, 17) * 1000 + FlareRules.Roll(p.Seed, id, 29),
                    Likes = FlareRules.Roll(p.Seed, i, 823) % (p.Bad ? 40 : 12), Reach = 30
                };
                SetAt(r, at);
                f.Posts.Add(r);
                made?.Add(r);
            }
            return count;
        }

        /// <summary>How one of the city's people answers a post: angry at a bad one, kind about an honest arrest, doubting a boast.</summary>
        public static FlareReplyKind ReplyKindOf(FlarePost p, int roll)
        {
            if (p.Bad)
            {
                return roll < 800 ? FlareReplyKind.Angry : FlareReplyKind.Doubt;
            }
            if (p.Source == FlareSource.Arrested || p.Source == FlareSource.Scandal)
            {
                return p.Tone == FlareTone.Honest ? FlareReplyKind.Kind : FlareReplyKind.Doubt;
            }
            if (p.Source == FlareSource.Report)
            {
                return roll < 600 ? FlareReplyKind.Agree : FlareReplyKind.Question;
            }
            if (p.Tone == FlareTone.Cheeky)
            {
                return roll < 700 ? FlareReplyKind.Funny : FlareReplyKind.Love;
            }
            bool money = p.Source == FlareSource.Promotion || p.Source == FlareSource.Opened || p.Source == FlareSource.RecordDay || p.Source == FlareSource.Shares;
            if (money && p.Tone == FlareTone.Proud && roll < 300)
            {
                return FlareReplyKind.Doubt;
            }
            return p.Viral || roll < 350 ? FlareReplyKind.Love : roll < 700 ? FlareReplyKind.Agree : FlareReplyKind.Question;
        }

        /// <summary>A post that drew a pile-on: it went badly and was seen widely.</summary>
        public static bool PileOn(FlarePost p)
        {
            return p != null && p.Bad && p.Reach >= PileOnFrom && p.Author == FlareAuthor.Player;
        }

        public static void SetAt(FlarePost p, long at)
        {
            at = Math.Max(0, at);
            p.Minute = (int)(at % 60);
            p.Hour = (int)(at / 60 % 24);
            p.Day = (int)(at / 1440);
        }

        /// <summary>The day's Trending: the tags of the last day's posts, weighed by reach, the most first, at most <paramref name="most"/>.</summary>
        public static void Trending(List<FlarePost> posts, long now, int most, List<string> tags, List<int> counts)
        {
            tags.Clear();
            counts.Clear();
            for (int i = 0; i < posts.Count; i++)
            {
                FlarePost p = posts[i];
                if (string.IsNullOrEmpty(p.Topic) || p.At > now || now - p.At > 1440)
                {
                    continue;
                }
                int weight = 1 + Math.Min(40, p.Reach / 120);
                int start = 0;
                string t = p.Topic;
                while (start < t.Length)
                {
                    int end = t.IndexOf(' ', start);
                    if (end < 0)
                    {
                        end = t.Length;
                    }
                    if (end > start)
                    {
                        string tag = t.Substring(start, end - start);
                        int at = tags.IndexOf(tag);
                        if (at < 0)
                        {
                            tags.Add(tag);
                            counts.Add(weight);
                        }
                        else
                        {
                            counts[at] += weight;
                        }
                    }
                    start = end + 1;
                }
            }
            for (int i = 1; i < tags.Count; i++)
            {
                for (int k = i; k > 0 && (counts[k] > counts[k - 1] || (counts[k] == counts[k - 1] && string.CompareOrdinal(tags[k], tags[k - 1]) < 0)); k--)
                {
                    int c = counts[k];
                    counts[k] = counts[k - 1];
                    counts[k - 1] = c;
                    string s = tags[k];
                    tags[k] = tags[k - 1];
                    tags[k - 1] = s;
                }
            }
            if (tags.Count > most)
            {
                tags.RemoveRange(most, tags.Count - most);
                counts.RemoveRange(most, counts.Count - most);
            }
        }

        /// <summary>The tags typed (#word) and built, lower case, joined by a space, at most five, each once.</summary>
        public static string TagsOf(string text, string built)
        {
            var b = new StringBuilder(64);
            int n = 0;
            AddTags(built, b, ref n, false);
            AddTags(text, b, ref n, true);
            return b.Length > 0 ? b.ToString() : null;
        }

        private static void AddTags(string text, StringBuilder b, ref int n, bool hashed)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            for (int i = 0; i < text.Length && n < TagsMost; i++)
            {
                if (hashed && text[i] != '#')
                {
                    continue;
                }
                int start = hashed ? i + 1 : i;
                int end = start;
                while (end < text.Length && end - start < TagLength && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                {
                    end++;
                }
                if (end > start)
                {
                    string tag = text.Substring(start, end - start).ToLowerInvariant();
                    if (!HasTag(b, tag))
                    {
                        b.Append(b.Length > 0 ? " " : string.Empty).Append(tag);
                        n++;
                    }
                }
                i = end;
            }
        }

        private static bool HasTag(StringBuilder b, string tag)
        {
            string all = " " + b + " ";
            return all.IndexOf(" " + tag + " ", StringComparison.Ordinal) >= 0;
        }

        /// <summary>Typed text as Flare keeps it: one line of printable characters, spaces run together, at most <paramref name="most"/>; null for nothing.</summary>
        public static string Clip(string text, int most = TextMost)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            var b = new StringBuilder(Math.Min(text.Length, most));
            bool space = false;
            for (int i = 0; i < text.Length && b.Length < most; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    space = b.Length > 0;
                    continue;
                }
                if (c == '<' || c == '>')
                {
                    c = c == '<' ? '‹' : '›';   // the text's own markup is never markup: TextMeshPro would read <b> as bold
                }
                if (space && b.Length < most - 1)
                {
                    b.Append(' ');
                }
                space = false;
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < text.Length && b.Length < most - 1)
                    {
                        b.Append(c).Append(text[i + 1]);
                    }
                    i++;   // (its second half goes with it, or neither does)
                    continue;
                }
                b.Append(c);
            }
            return b.Length > 0 ? b.ToString() : null;
        }

        /// <summary>Whether one of the city's people writes to the player today, who and about what; once every two days at most.</summary>
        public static bool CityDm(FlareFile f, int day, bool hiring, out string with, out string what)
        {
            with = null;
            what = null;
            if (day - f.Net.DmDay < DmGapDays || FlareRules.Roll(f.Seed, day, 840) >= DmChance)
            {
                return false;
            }
            int roll = FlareRules.Roll(f.Seed, day, 841);
            int who = PickCitizen(f.Seed, day, 845);
            if (day - f.LastViralDay <= 2)
            {
                with = "c" + who;
                what = "fan";
            }
            else if (hiring && roll < 500)
            {
                with = "c" + who;
                what = "job";
            }
            else if (f.Tier >= FlareTier.Local && f.ActiveInfluencers > 0 && roll < 750)
            {
                int k = roll % f.Influencers.Count;
                for (int i = 0; i < f.Influencers.Count; i++)
                {
                    FlareInfluencer inf = f.Influencers[(k + i) % f.Influencers.Count];
                    if (inf.Active)
                    {
                        with = "i:" + inf.Id;
                        break;
                    }
                }
                what = "collab";
            }
            else
            {
                with = "c" + who;
                what = f.LastPostDay >= day - 3 ? "chat" : null;
            }
            if (with == null || what == null)
            {
                return false;
            }
            f.Net.DmDay = day;
            return true;
        }

        /// <summary>A message added, the oldest going past the most.</summary>
        public static FlareDm Dm(FlareNet net, string with, string name, long at, bool mine, string text, string key, string data)
        {
            var dm = new FlareDm { Id = net.NextDm++, With = with, Name = name, At = at, Mine = mine, Text = text, Key = key, Data = data, Read = mine };
            net.Dms.Add(dm);
            while (net.Dms.Count > FlareNet.DmsMost)
            {
                net.Dms.RemoveAt(0);
            }
            return dm;
        }

        /// <summary>The Notifications screen, newest first, at most <paramref name="most"/>, worked out afresh from the posts and messages.</summary>
        public static void Notices(FlareFile f, long now, string me, List<string> myPlaces, List<FlareReaction> scratch, List<FlareNotice> into, int most)
        {
            into.Clear();
            for (int i = 0; i < f.Posts.Count; i++)
            {
                FlarePost p = f.Posts[i];
                if (p.At > now)
                {
                    continue;
                }
                if (p.Author == FlareAuthor.Player)
                {
                    ReadReactors(p.Reactors, scratch);
                    for (int k = 0; k < scratch.Count; k++)
                    {
                        long at = p.At + scratch[k].Delay;
                        if (at <= now)
                        {
                            FlareNoticeKind kind = scratch[k].Kind == 'R' ? FlareNoticeKind.Reply : scratch[k].Kind == 'S' ? FlareNoticeKind.Repost : FlareNoticeKind.Like;
                            into.Add(new FlareNotice { Kind = kind, At = at, Post = p.Id, Who = scratch[k].Who, Dm = -1 });
                        }
                    }
                    continue;
                }
                FlarePost parent = p.ReplyTo >= 0 ? f.Post(p.ReplyTo) : null;
                if (parent != null && parent.Author == FlareAuthor.Player && (p.Source == FlareSource.Reply || p.Source == FlareSource.Answer))
                {
                    FlareNoticeKind kind = p.Source == FlareSource.Answer && parent.Source == FlareSource.Report ? FlareNoticeKind.Case : FlareNoticeKind.Reply;
                    into.Add(new FlareNotice { Kind = kind, At = p.At, Post = p.Id, Who = KeyOf(p), Dm = -1 });
                    continue;
                }
                if (p.Source == FlareSource.Review && p.Place != null && myPlaces != null && myPlaces.Contains(p.Place))
                {
                    into.Add(new FlareNotice { Kind = FlareNoticeKind.Review, At = p.At, Post = p.Id, Who = KeyOf(p), Dm = -1 });
                    continue;
                }
                if (me != null && p.Mention == me)
                {
                    into.Add(new FlareNotice { Kind = FlareNoticeKind.Mention, At = p.At, Post = p.Id, Who = KeyOf(p), Dm = -1 });
                }
            }
            for (int i = 0; i < f.Net.Dms.Count; i++)
            {
                FlareDm dm = f.Net.Dms[i];
                if (!dm.Mine && dm.At <= now)
                {
                    into.Add(new FlareNotice { Kind = FlareNoticeKind.Dm, At = dm.At, Post = -1, Who = dm.With, Dm = dm.Id });
                }
            }
            for (int i = 1; i < into.Count; i++)
            {
                for (int k = i; k > 0 && into[k].At > into[k - 1].At; k--)
                {
                    FlareNotice t = into[k];
                    into[k] = into[k - 1];
                    into[k - 1] = t;
                }
            }
            if (into.Count > most)
            {
                into.RemoveRange(most, into.Count - most);
            }
        }

        /// <summary>Notifications newer than the ones read: the app's badge.</summary>
        public static int Unread(List<FlareNotice> notices, FlareNet net)
        {
            int n = 0;
            for (int i = 0; i < notices.Count; i++)
            {
                n += notices[i].At > net.NoticeSeen ? 1 : 0;
            }
            return n;
        }

        /// <summary>The account a post is from, as a key: "me", "f3", "c12", "o:0", "i:3", "p:id", "k:name", "t:name" and FlareCast's.</summary>
        public static string KeyOf(FlarePost p)
        {
            switch (p.Author)
            {
                case FlareAuthor.Player: return "me";
                case FlareAuthor.Friend: return "f" + p.Who;
                case FlareAuthor.Citizen: return "c" + p.Who;
                case FlareAuthor.Official: return "o:" + p.Who;
                case FlareAuthor.Business: return "k:" + p.Name;
                case FlareAuthor.Network: return "p:" + p.AuthorId;
                case FlareAuthor.Tycoon: return "t:" + p.Name;
                default: return FlareCast.KeyOf(p);
            }
        }

        /// <summary>The city's posts past a few days go (the session players' a day later), with replies whose post is gone.</summary>
        public static void PruneCity(FlareFile f)
        {
            if (f.Posts.Count == 0)
            {
                return;
            }
            int today = f.Posts[f.Posts.Count - 1].Day;
            for (int i = 0; i < f.Posts.Count; i++)
            {
                today = Math.Max(today, f.Posts[i].Day);
            }
            bool removed = false;
            for (int i = f.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = f.Posts[i];
                int keep = p.Author == FlareAuthor.Network ? NetworkKeepDays : CityKeepDays;
                bool city = p.Author == FlareAuthor.Citizen || p.Author == FlareAuthor.Official || p.Author == FlareAuthor.Business || p.Author == FlareAuthor.Network
                            || p.Author == FlareAuthor.Company || p.Author == FlareAuthor.Ledger || p.Author == FlareAuthor.Tycoon;
                if (city && today - p.Day >= keep && p.ReplyTo < 0)
                {
                    f.Posts.RemoveAt(i);
                    removed = true;
                }
            }
            if (!removed)
            {
                return;
            }
            for (int i = f.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = f.Posts[i];
                if (p.ReplyTo >= 0 && f.Post(p.ReplyTo) == null)
                {
                    f.Posts.RemoveAt(i);
                }
            }
        }
    }
}
