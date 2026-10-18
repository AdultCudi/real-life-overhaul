using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>One comment under a post that is not the player's: who writes it, when it comes in, its likes. Its words are RLO Texts'.</summary>
    public struct FlareComment
    {
        public int Slot;             // its place among the post's comments, 0 the first to come
        public long At;              // when it comes in, in game minutes (the moment the post's count reaches it)
        public char Kind;            // 'c' one of the city's people, 'f' a friend of the player's, 'i' an influencer
        public int Who;              // the city's cast number, the social life's number or the influencer's id
        public int Likes;            // its likes once all are in
    }

    /// <summary>
    /// The comments under a post that is not the player's: up to <see cref="WrittenMost"/> of its replies written out by
    /// people picked from its seed, coming in as its count reaches them. Nothing is stored, so a post gives the same comments
    /// on every machine of a session (a shared post's seed is the city's). Game-free. Cost: a few rolls a comment; no allocation.
    /// </summary>
    public static class FlareCommentRules
    {
        public const int WrittenMost = 16;          // comments written out at most: about what a phone's thread shows before "more"; a slot is four bits of a like's key
        public const int FriendShare = 150;         // thousandths: a slot a friend who cares about the topic may take (their own posts only)
        public const int InfluencerShare = 80;      // thousandths: a slot an influencer may take (their own posts only)
        public const int LikesBase = 3;             // a comment's likes at least spread over 0 to this
        public const int LikesMost = 60;            // ...plus at most this many, by how liked the post is
        private const int SaltSeed = 840, SaltKind = 843, SaltPick = 844, SaltCitizen = 845, SaltLikes = 846;

        /// <summary>Whether a post can have comments written out: not the player's, not a reply, not on its way, with replies.</summary>
        public static bool Has(FlarePost p)
        {
            return p != null && p.Author != FlareAuthor.Player && p.ReplyTo < 0 && !p.Pending && p.Replies > 0;
        }

        /// <summary>A post of the city's, shared in a session by its author: its comments come from the city's seed, by the city's people alone.</summary>
        public static bool City(FlarePost p)
        {
            switch (p.Author)
            {
                case FlareAuthor.Citizen:
                case FlareAuthor.Official:
                case FlareAuthor.Business:
                case FlareAuthor.Ledger:
                case FlareAuthor.Company:
                case FlareAuthor.Candidate:
                case FlareAuthor.Network:
                case FlareAuthor.Tycoon:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The post's number in the session: its own on the host, the host's on a guest's copy.</summary>
        public static int Net(FlarePost p) => p.NetId > 0 ? p.NetId : p.Id;

        /// <summary>The seed the comments are drawn from: for a city's post the city's seed and its session number, else the post's own.</summary>
        public static int Seed(FlarePost p, int citySeed)
        {
            return City(p) ? FlareRules.Roll(citySeed, Net(p), SaltSeed) * 1000 + FlareRules.Roll(citySeed, Net(p), SaltSeed + 1) : p.Seed;
        }

        public static int Written(int total)
        {
            return FlareRules.Clamp(total, 0, WrittenMost);
        }

        /// <summary>The count a written comment comes in at: spread over the replies, strictly rising, at most the total.</summary>
        public static int Threshold(int slot, int written, int total)
        {
            return 1 + (int)((long)slot * total / Math.Max(1, written));
        }

        /// <summary>The first game minute at which a post's shown count reaches <paramref name="count"/>, searched on its curve.</summary>
        public static long ArrivesAt(long postAt, int total, int count)
        {
            int lo = 1, hi = 1440;   // at 1440 every reply is in (FlareLiveRules.Arrived)
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (ShownAfter(total, mid) >= count)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }
            return postAt + lo;
        }

        private static int ShownAfter(int total, long since)
        {
            return (int)((long)Math.Max(0, total) * FlareLiveRules.ArrivedAfter(since) / 1000);
        }

        /// <summary>The post's written comments in order into <paramref name="into"/>; how many.</summary>
        public static int Plan(FlarePost p, int citySeed, int citizens, IReadOnlyList<FlarePerson> people, FlareTaste taste, IReadOnlyList<FlareInfluencer> influencers,
            List<FlareComment> into)
        {
            into.Clear();
            if (!Has(p) || citizens <= 0)
            {
                return 0;
            }
            bool city = City(p);
            int seed = Seed(p, citySeed), total = p.Replies, written = Written(total);
            int likes = LikesBase + Math.Min(LikesMost, p.Likes / 20);
            for (int slot = 0; slot < written; slot++)
            {
                var c = new FlareComment { Slot = slot, At = ArrivesAt(p.At, total, Threshold(slot, written, total)), Likes = FlareRules.Roll(seed, slot, SaltLikes) % likes };
                int roll = FlareRules.Roll(seed, slot, SaltKind);
                if (!city && roll < FriendShare && Friend(p, seed, slot, people, taste, into, out int friend))
                {
                    c.Kind = 'f';
                    c.Who = friend;
                }
                else if (!city && roll < FriendShare + InfluencerShare && Influencer(p, seed, slot, influencers, into, out int inf))
                {
                    c.Kind = 'i';
                    c.Who = inf;
                }
                else
                {
                    c.Kind = 'c';
                    c.Who = Citizen(p, seed, slot, citizens, into);
                }
                into.Add(c);
            }
            return into.Count;
        }

        /// <summary>The comments of a plan that have come in by now (they come in order).</summary>
        public static int Due(List<FlareComment> plan, long now)
        {
            int n = 0;
            while (n < plan.Count && plan[n].At <= now)
            {
                n++;
            }
            return n;
        }

        /// <summary>The "more" line's number: the count on the card less the comments written out.</summary>
        public static int More(int shownCount, int writtenShown) => Math.Max(0, shownCount - writtenShown);

        /// <summary>The key a comment's words are kept by: post, slot and writer ("fc:12:3:c17"); a guest's shared copy by its session number ("fn:").</summary>
        public static string Key(FlarePost p, FlareComment c)
        {
            return (p.Shared && p.NetId > 0 ? "fn:" + p.NetId : "fc:" + p.Id) + ":" + c.Slot + ":" + c.Kind + c.Who;
        }

        /// <summary>The number a comment is shown under, never a post's: below every other number, one for each post and slot.</summary>
        public static int DisplayId(int postId, int slot) => DisplayBase + postId * 16 + (slot & 15);

        private const int DisplayBase = -(1 << 30);   // the friends' display replies go from -1000 down: these stay clear of them

        /// <summary>True for a number a comment is shown under (<see cref="DisplayId"/>).</summary>
        public static bool IsDisplay(int id) => id < DisplayBase / 2;

        /// <summary>The comment kind ('f', 'i', 'c') of the author a comment is shown as.</summary>
        public static char KindOf(FlareAuthor author) => author == FlareAuthor.Friend ? 'f' : author == FlareAuthor.Influencer ? 'i' : 'c';

        public const int LikesKept = 256;   // a heart on every comment of thirty threads, and a bound for a damaged save

        private const int WhoMost = 1 << 26;   // the largest who a key can hold

        /// <summary>The stable key of a comment the player liked: post, slot and writer; -1 when out of range.</summary>
        public static long LikeKey(int postId, int slot, char kind, int who)
        {
            int code = kind == 'f' ? 1 : kind == 'i' ? 2 : kind == 'c' ? 0 : -1;
            if (postId < 0 || slot < 0 || slot > 15 || code < 0 || who < 0 || who >= WhoMost)
            {
                return -1;
            }
            return (long)postId << 32 | (long)slot << 28 | (long)code << 26 | (uint)who;
        }

        public static int PostOf(long key) => (int)(key >> 32);

        /// <summary>A key's parts, for the save line (C|post|slot|kind|who).</summary>
        public static void Parts(long key, out int postId, out int slot, out int kind, out int who)
        {
            postId = PostOf(key);
            slot = (int)(key >> 28) & 15;
            kind = (int)(key >> 26) & 3;
            who = (int)(key & (WhoMost - 1));
        }

        /// <summary>A key from the save line's parts (kind 0 the city's, 1 a friend, 2 an influencer); -1 when a part is out of range.</summary>
        public static long FromParts(int postId, int slot, int kind, int who)
        {
            return kind >= 0 && kind <= 2 ? LikeKey(postId, slot, kind == 1 ? 'f' : kind == 2 ? 'i' : 'c', who) : -1;
        }

        public static bool Liked(FlareFile f, int postId, int slot, char kind, int who)
        {
            long key = LikeKey(postId, slot, kind, who);
            return f != null && key >= 0 && f.CommentLikes.Contains(key);
        }

        /// <summary>The player likes a comment or takes the like back; the new state. Sends nothing; the oldest like goes past <see cref="LikesKept"/>.</summary>
        public static bool ToggleLike(FlareFile f, int postId, int slot, char kind, int who)
        {
            long key = LikeKey(postId, slot, kind, who);
            if (f == null || key < 0 || f.Post(postId) == null)
            {
                return false;
            }
            if (f.CommentLikes.Remove(key))
            {
                return false;
            }
            if (f.CommentLikes.Count >= LikesKept)
            {
                f.CommentLikes.RemoveAt(0);
            }
            f.CommentLikes.Add(key);
            return true;
        }

        /// <summary>Lets go of the likes of comments whose post is gone. How many went.</summary>
        public static int DropLikes(FlareFile f)
        {
            int n = 0;
            for (int i = f.CommentLikes.Count - 1; i >= 0; i--)
            {
                if (f.Post(PostOf(f.CommentLikes[i])) == null)
                {
                    f.CommentLikes.RemoveAt(i);
                    n++;
                }
            }
            return n;
        }

        /// <summary>A number for what was asked already, by post and slot (this file's posts).</summary>
        public static long Asked(int postId, int slot) => (long)postId << 4 | (uint)(slot & 15);

        public const int EarlierShare = 350;     // thousandths: a written comment after the first answers the one before it
        public const int AuthorAnswers = 600;    // thousandths: a city person answers the player's typed reply under their own post this often
        public const int AccountAnswers = 450;   // ...an account (an official one, a business, a brand, a rival) under its own post
        public const int AuthorLeast = 12;       // game minutes after the reply a person's answer comes at the earliest...
        public const int AuthorSpread = 80;      // ...and within this many more
        public const int AccountLeast = 30;      // game minutes: an account answers within half an hour to two hours, as it answers a mention
        public const int AccountSpread = 90;   // game minutes
        private const int SaltEarlier = 850, SaltAnswer = 851, SaltAnswerAt = 852;
        private const int AnswerBase = -(1 << 28);   // the numbers answers are shown under: below the friends' replies, above the comments' (IsDisplay stays false)

        /// <summary>Whether a written comment answers the one before it, from the post's comment seed.</summary>
        public static bool AnswersEarlier(FlarePost p, int citySeed, int slot)
        {
            return slot >= 1 && FlareRules.Roll(Seed(p, citySeed), slot, SaltEarlier) % 1000 < EarlierShare;
        }

        /// <summary>Whether, and when in game minutes, a post's author answers the player's typed reply under it, from the reply's seed.</summary>
        public static bool AuthorAnswer(FlarePost reply, FlarePost parent, out long at)
        {
            at = 0;
            if (reply == null || parent == null || reply.Author != FlareAuthor.Player || reply.Source != FlareSource.Reply || reply.ReplyTo != parent.Id
                || string.IsNullOrEmpty(reply.Text) || parent.ReplyTo >= 0 || parent.Pending || reply.Pending)
            {
                return false;
            }
            bool person = parent.Author == FlareAuthor.Citizen && FlareTextsMap.AnswerTopic(parent) != null;
            bool account = parent.Author == FlareAuthor.Official && parent.Source == FlareSource.Official
                           || (parent.Author == FlareAuthor.Business || parent.Author == FlareAuthor.Tycoon) && parent.Source == FlareSource.BusinessPost
                           || parent.Author == FlareAuthor.Brand && parent.Source == FlareSource.BrandNews;
            if (!person && !account)
            {
                return false;
            }
            if (FlareRules.Roll(reply.Seed, reply.Id, SaltAnswer) % 1000 >= (person ? AuthorAnswers : AccountAnswers))
            {
                return false;
            }
            at = reply.At + (person ? AuthorLeast : AccountLeast) + FlareRules.Roll(reply.Seed, reply.Id, SaltAnswerAt) % (person ? AuthorSpread : AccountSpread);
            return true;
        }

        /// <summary>The key an answer's words are kept by: the player's reply's number ("fa:31").</summary>
        public static string AnswerKey(FlarePost reply) => "fa:" + reply.Id;

        /// <summary>The number an answer is shown under, never a post's: one for each reply.</summary>
        public static int AnswerDisplayId(int replyId) => AnswerBase - replyId;

        // One of the city's people, not the author and not one already in the thread.
        private static int Citizen(FlarePost p, int seed, int slot, int citizens, List<FlareComment> taken)
        {
            int start = FlareRules.Roll(seed, slot, SaltCitizen) % citizens;
            for (int k = 0; k < citizens; k++)
            {
                int c = (start + k) % citizens;
                if ((p.Author == FlareAuthor.Citizen && p.Who == c) || Taken(taken, 'c', c))
                {
                    continue;
                }
                return c;
            }
            return start;
        }

        // A friend who follows the player and cares about the topic (a flaky one looks half the time), not the author, not taken.
        private static bool Friend(FlarePost p, int seed, int slot, IReadOnlyList<FlarePerson> people, FlareTaste taste, List<FlareComment> taken, out int index)
        {
            index = -1;
            if (people == null || taste == FlareTaste.None)
            {
                return false;
            }
            int roll = FlareRules.Roll(seed, slot, SaltPick), count = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int pick = pass == 1 && count > 0 ? roll % count : -1, seen = 0;
                for (int i = 0; i < people.Count; i++)
                {
                    FlarePerson f = people[i];
                    if (!f.Follows || (f.Taste & taste) == 0 || ((f.Taste & FlareTaste.Flaky) != 0 && roll < 500)
                        || (p.Author == FlareAuthor.Friend && p.Who == f.Index) || Taken(taken, 'f', f.Index))
                    {
                        continue;
                    }
                    if (seen == pick)
                    {
                        index = f.Index;
                        return true;
                    }
                    seen++;
                }
                count = seen;
            }
            return false;
        }

        // An influencer of the player's city, not the author, not taken.
        private static bool Influencer(FlarePost p, int seed, int slot, IReadOnlyList<FlareInfluencer> influencers, List<FlareComment> taken, out int id)
        {
            id = -1;
            if (influencers == null || influencers.Count == 0)
            {
                return false;
            }
            int start = FlareRules.Roll(seed, slot, SaltPick) % influencers.Count;
            for (int k = 0; k < influencers.Count; k++)
            {
                FlareInfluencer inf = influencers[(start + k) % influencers.Count];
                if (inf == null || (p.Author == FlareAuthor.Influencer && p.Who == inf.Id) || Taken(taken, 'i', inf.Id))
                {
                    continue;
                }
                id = inf.Id;
                return true;
            }
            return false;
        }

        private static bool Taken(List<FlareComment> taken, char kind, int who)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                if (taken[i].Kind == kind && taken[i].Who == who)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Which comments' words were asked: waiting, worded or asked so many times, up to <see cref="Most"/> a session. Game-free.</summary>
    public sealed class FlareCommentAsks
    {
        public const int Most = 4;   // asks of one comment a session at most; past this it waits for the next load

        private struct Entry
        {
            public bool Waiting, Urgent, Worded;
            public int Asks;
        }

        private readonly Dictionary<long, Entry> _all = new Dictionary<long, Entry>(256);
        private readonly List<long> _gone = new List<long>(32);

        public int Background { get; private set; }

        public int Count => _all.Count;

        public bool Worded(long id) => _all.TryGetValue(id, out Entry e) && e.Worded;

        public bool Waiting(long id) => _all.TryGetValue(id, out Entry e) && e.Waiting;

        /// <summary>True if it may be asked now: not worded, under <see cref="Most"/> asks, not waiting unless now for the open thread.</summary>
        public bool May(long id, bool urgent)
        {
            if (!_all.TryGetValue(id, out Entry e))
            {
                return true;
            }
            if (e.Worded || e.Asks >= Most)
            {
                return false;
            }
            return !e.Waiting || (urgent && !e.Urgent);
        }

        /// <summary>Its words were found kept (a load, or another look): never asked again.</summary>
        public void Kept(long id)
        {
            _all.TryGetValue(id, out Entry e);
            if (e.Waiting && !e.Urgent)
            {
                Background--;
            }
            e.Waiting = false;
            e.Worded = true;
            _all[id] = e;
        }

        public void Asked(long id, bool urgent)
        {
            _all.TryGetValue(id, out Entry e);
            if (e.Waiting && !e.Urgent)
            {
                Background--;
            }
            e.Waiting = true;
            e.Urgent = urgent;
            e.Asks++;
            if (!urgent)
            {
                Background++;
            }
            _all[id] = e;
        }

        /// <summary>Its request came back: words (worded, never asked again) or none (free to be asked again).</summary>
        public void Ended(long id, bool words)
        {
            if (!_all.TryGetValue(id, out Entry e))
            {
                return;
            }
            if (e.Waiting && !e.Urgent)
            {
                Background--;
            }
            e.Waiting = false;
            e.Urgent = false;
            e.Worded |= words;
            _all[id] = e;
        }

        /// <summary>Lets go of the comments of posts that are gone. How many went.</summary>
        public int Prune(Func<int, bool> postKept)
        {
            _gone.Clear();
            foreach (KeyValuePair<long, Entry> kv in _all)
            {
                if (!kv.Value.Waiting && !postKept((int)(kv.Key >> 4)))
                {
                    _gone.Add(kv.Key);
                }
            }
            for (int i = 0; i < _gone.Count; i++)
            {
                _all.Remove(_gone[i]);
            }
            return _gone.Count;
        }

        public void Clear()
        {
            _all.Clear();
            Background = 0;
        }
    }
}
