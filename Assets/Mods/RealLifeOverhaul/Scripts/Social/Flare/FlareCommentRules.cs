using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>One comment under a post that is not the player's: who writes it, when it comes in and how liked it is. Its words are the model's (RLO Texts), never a written line.</summary>
    public struct FlareComment
    {
        public int Slot;             // its place among the post's comments, 0 the first to come
        public long At;              // when it comes in, in game minutes (the moment the post's count reaches it)
        public char Kind;            // 'c' one of the city's people, 'f' a friend of the player's, 'i' an influencer
        public int Who;              // the city's cast number, the social life's number or the influencer's id
        public int Likes;            // its likes once they have all come in (FlareLiveRules.Shown by its own minute)
    }

    /// <summary>
    /// The comments under a post that is not the player's (the city's people's, the accounts', the businesses', the rivals',
    /// another player's, a friend's, an influencer's): the post's number of replies (FlarePost.Replies) is the count on its
    /// card; every one of them is written out, up to what a phone's thread shows at once (<see cref="WrittenMost"/>), by
    /// people chosen from the post's seed, each coming in at the moment the count reaches it, and only past that does the
    /// thread say how many more there are (never "2 more replies" with none of them shown). Nothing is stored: the same
    /// post always gives the same comments, on every machine of a Going Public session
    /// (a shared post's seed is the city's seed and its session number, which every machine has; only the city's people
    /// comment on a shared post, never anybody's own friends or influencers). The words are asked of RLO Texts and kept by
    /// <see cref="Key"/>: a comment whose words have not come yet is not shown and counts among the more until they come.
    /// Game-free (EconomyCheck, FlareCommentChecks.cs, FlareCommentAskChecks.cs).
    /// COST: when a thread is drawn or the comments are asked for, a few rolls and a short search of the curve for each
    /// written comment (at most <see cref="WrittenMost"/>, eleven steps each); nothing per frame, nothing allocated.
    /// </summary>
    public static class FlareCommentRules
    {
        public const int WrittenMost = 16;          // comments written out at most, however many replies a post has: a phone's thread shows about this many before "more" (and a slot is four bits of a like's key)
        public const int FriendShare = 150;         // thousandths: a slot a friend who cares about the topic may take (their own posts only)
        public const int InfluencerShare = 80;      // thousandths: a slot an influencer may take (their own posts only)
        public const int LikesBase = 3;             // a comment's likes at least spread over 0 to this
        public const int LikesMost = 60;            // ...plus at most this many, by how liked the post is
        private const int SaltSeed = 840, SaltKind = 843, SaltPick = 844, SaltCitizen = 845, SaltLikes = 846;

        /// <summary>Whether a post can have comments written out: not the player's, not itself a reply, not a guest's post on its way, with replies.</summary>
        public static bool Has(FlarePost p)
        {
            return p != null && p.Author != FlareAuthor.Player && p.ReplyTo < 0 && !p.Pending && p.Replies > 0;
        }

        /// <summary>A post of the city's (it is shared in a session, by its author): its comments come from the city's seed and are the city's people's alone.</summary>
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

        /// <summary>The post's number in the session: its own on the host (a host's post goes out under its own number), the host's on a guest's copy.</summary>
        public static int Net(FlarePost p) => p.NetId > 0 ? p.NetId : p.Id;

        /// <summary>The seed the comments are drawn from: the city's seed and the post's session number for a city's post (the same on every machine), the post's own seed otherwise.</summary>
        public static int Seed(FlarePost p, int citySeed)
        {
            return City(p) ? FlareRules.Roll(citySeed, Net(p), SaltSeed) * 1000 + FlareRules.Roll(citySeed, Net(p), SaltSeed + 1) : p.Seed;
        }

        /// <summary>How many of a post's replies are written out: every one, up to <see cref="WrittenMost"/>; the rest is the "more" line.</summary>
        public static int Written(int total)
        {
            return FlareRules.Clamp(total, 0, WrittenMost);
        }

        /// <summary>The count a written comment comes in at: spread over the replies, the first at the first, each later one further on (strictly rising, at most the total).</summary>
        public static int Threshold(int slot, int written, int total)
        {
            return 1 + (int)((long)slot * total / Math.Max(1, written));
        }

        /// <summary>The first minute (game minutes) at which a post's shown count reaches 'count': the count's own curve, searched.</summary>
        public static long ArrivesAt(long postAt, int total, int count)
        {
            int lo = 1, hi = 1440;   // (at 1440 every reply is in: FlareLiveRules.Arrived)
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

        // The shown count so many minutes after the post went up (FlareLiveRules.Shown on its curve).
        private static int ShownAfter(int total, long since)
        {
            return (int)((long)Math.Max(0, total) * FlareLiveRules.ArrivedAfter(since) / 1000);
        }

        /// <summary>
        /// The post's written comments, in the order they come in, into 'into' (emptied first). 'citizens' the city's cast;
        /// 'people' and 'influencers' only for a post that is not the city's (null otherwise); 'taste' what a friend must care
        /// about to comment (None: no friend does). Returns how many.
        /// </summary>
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

        /// <summary>The comments that have come in by now, of a plan (they come in order).</summary>
        public static int Due(List<FlareComment> plan, long now)
        {
            int n = 0;
            while (n < plan.Count && plan[n].At <= now)
            {
                n++;
            }
            return n;
        }

        /// <summary>The "more" line's number: the count on the card less the comments written out (never below none).</summary>
        public static int More(int shownCount, int writtenShown) => Math.Max(0, shownCount - writtenShown);

        /// <summary>
        /// The key a comment's words are kept by: the post's number, its slot and who writes it ("fc:12:3:c17"), so that words
        /// written for one person are never shown under another. A guest's copy of a shared post goes by the session's number
        /// ("fn:45:3:c17", what the host's "fc:" key is sent as: Texts/TextsShare.NetKey).
        /// </summary>
        public static string Key(FlarePost p, FlareComment c)
        {
            return (p.Shared && p.NetId > 0 ? "fn:" + p.NetId : "fc:" + p.Id) + ":" + c.Slot + ":" + c.Kind + c.Who;
        }

        /// <summary>The number a comment is shown under (made for display, never a post of the file's): below every other number, one for each post and slot.</summary>
        public static int DisplayId(int postId, int slot) => DisplayBase + postId * 16 + (slot & 15);

        private const int DisplayBase = -(1 << 30);   // (the friends' replies made for display go from -1000 down: these stay clear of them)

        /// <summary>True for a number a comment is shown under (<see cref="DisplayId"/>), never a post's or a friend's reply's (those stay above half of it).</summary>
        public static bool IsDisplay(int id) => id < DisplayBase / 2;

        /// <summary>The comment kind of the author a comment is shown as ('f' a friend, 'i' an influencer, 'c' one of the city's people), as <see cref="FlareComment.Kind"/>.</summary>
        public static char KindOf(FlareAuthor author) => author == FlareAuthor.Friend ? 'f' : author == FlareAuthor.Influencer ? 'i' : 'c';

        // ---------------------------------------------------------------- the player's likes of comments

        /// <summary>The most comments liked that are kept.</summary>
        public const int LikesKept = 256;   // keys: a heart on every written comment of thirty threads, and a bound for a damaged save

        private const int WhoMost = 1 << 26;   // the largest who a key can hold (the city's cast, a social life's number and an influencer's id are far below it)

        /// <summary>
        /// The key of a comment the player liked: the post's number in this file, the comment's slot and who writes it (as
        /// the words' <see cref="Key"/>: a like given to one person never shows under another). Stable, unlike the number the
        /// comment is shown under; -1 when it cannot be kept (no slot or no who in range).
        /// </summary>
        public static long LikeKey(int postId, int slot, char kind, int who)
        {
            int code = kind == 'f' ? 1 : kind == 'i' ? 2 : kind == 'c' ? 0 : -1;
            if (postId < 0 || slot < 0 || slot > 15 || code < 0 || who < 0 || who >= WhoMost)
            {
                return -1;
            }
            return (long)postId << 32 | (long)slot << 28 | (long)code << 26 | (uint)who;
        }

        /// <summary>The post a like's key belongs to.</summary>
        public static int PostOf(long key) => (int)(key >> 32);

        /// <summary>A key's parts, for the save line (C|post|slot|kind|who).</summary>
        public static void Parts(long key, out int postId, out int slot, out int kind, out int who)
        {
            postId = PostOf(key);
            slot = (int)(key >> 28) & 15;
            kind = (int)(key >> 26) & 3;
            who = (int)(key & (WhoMost - 1));
        }

        /// <summary>A key from the save line's parts (the kind as written: 0 the city's, 1 a friend, 2 an influencer); -1 when a part is out of range.</summary>
        public static long FromParts(int postId, int slot, int kind, int who)
        {
            return kind >= 0 && kind <= 2 ? LikeKey(postId, slot, kind == 1 ? 'f' : kind == 2 ? 'i' : 'c', who) : -1;
        }

        /// <summary>True if the player liked this comment.</summary>
        public static bool Liked(FlareFile f, int postId, int slot, char kind, int who)
        {
            long key = LikeKey(postId, slot, kind, who);
            return f != null && key >= 0 && f.CommentLikes.Contains(key);
        }

        /// <summary>
        /// The player likes a comment under a post that is not theirs, or takes the like back. The new state. It changes
        /// nothing else (no affection, standing or followers) and nothing is sent: in a session a like on a shared post's
        /// comment is this player's own. At <see cref="LikesKept"/> the oldest like goes.
        /// </summary>
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

        // ---------------------------------------------------------------- answers in a thread (data v3)

        public const int EarlierShare = 350;     // thousandths: a written comment after the first answers the one before it (data v3's comments; the training's share)
        public const int AuthorAnswers = 600;    // thousandths: a city person answers the player's typed reply under their own post this often
        public const int AccountAnswers = 450;   // ...an account (an official one, a business, a brand, a rival) under its own post
        public const int AuthorLeast = 12;       // game minutes after the reply a person's answer comes at the earliest...
        public const int AuthorSpread = 80;      // ...and within this many more
        public const int AccountLeast = 30;      // game minutes: an account answers within half an hour to two hours, as it answers a mention
        public const int AccountSpread = 90;
        private const int SaltEarlier = 850, SaltAnswer = 851, SaltAnswerAt = 852;
        private const int AnswerBase = -(1 << 28);   // the numbers answers are shown under: below the friends' replies, above the comments' (IsDisplay stays false)

        /// <summary>
        /// Whether a written comment answers the one before it under the same post (data v3 comments only: the caller asks
        /// this for a citizen.comment.* intent). From the post's comment seed, so the same on every machine.
        /// </summary>
        public static bool AnswersEarlier(FlarePost p, int citySeed, int slot)
        {
            return slot >= 1 && FlareRules.Roll(Seed(p, citySeed), slot, SaltEarlier) % 1000 < EarlierShare;
        }

        /// <summary>
        /// Whether the author of a post answers the player's typed reply under it, and when (game minutes): a city person
        /// under their own post on a topic the training's answers know (FlareTextsMap.AnswerTopic) <see cref="AuthorAnswers"/>
        /// times in a thousand, <see cref="AuthorLeast"/> to <see cref="AuthorLeast"/> + <see cref="AuthorSpread"/> minutes
        /// later; an official account, a city business, a brand or a rival under its own post <see cref="AccountAnswers"/>
        /// times in a thousand, half an hour to two hours later. Never a reply under a reply, a reply with nothing typed, or
        /// a post on its way to the host. From the reply's own seed and number: the same after a load. Data v3 only (its
        /// answers have no written line: an answer the model did not word in time is not shown).
        /// </summary>
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

        /// <summary>The number an answer is shown under (made for display, never a post of the file's), one for each reply.</summary>
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

    /// <summary>
    /// Which comments' words are asked, by the slot's number (<see cref="FlareCommentRules.Asked"/>): waiting (and whether
    /// the request is the open thread's, asked ahead of the rest), worded (kept: never asked again), or asked so many times.
    /// A request handed back without words (its time ran out, the checker refused them, the model restarted) frees the
    /// comment, and it is asked again on the next look, up to <see cref="Most"/> times a session: a comment is never left
    /// without words because one request missed (else two of three comments could stay unwritten).
    /// A comment waiting at the background's priority is asked again at the open thread's when its thread is opened.
    /// Game-free (EconomyCheck, FlareCommentAskChecks.cs). COST: a dictionary look a comment; nothing per frame.
    /// </summary>
    public sealed class FlareCommentAsks
    {
        public const int Most = 4;   // asks of one comment a session at most: a miss is asked again; past this it waits for the next load

        private struct Entry
        {
            public bool Waiting, Urgent, Worded;
            public int Asks;
        }

        private readonly Dictionary<long, Entry> _all = new Dictionary<long, Entry>(256);
        private readonly List<long> _gone = new List<long>(32);

        /// <summary>Requests waiting at the background's priority (the walk's share of the queue).</summary>
        public int Background { get; private set; }

        /// <summary>How many comments are known.</summary>
        public int Count => _all.Count;

        /// <summary>True once its words are kept: never asked again.</summary>
        public bool Worded(long id) => _all.TryGetValue(id, out Entry e) && e.Worded;

        /// <summary>True while a request for it waits.</summary>
        public bool Waiting(long id) => _all.TryGetValue(id, out Entry e) && e.Waiting;

        /// <summary>
        /// True if it may be asked now: not worded, asked fewer than <see cref="Most"/> times, and not waiting (or, for the
        /// open thread, waiting only at the background's priority: then it is asked again ahead).
        /// </summary>
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

        /// <summary>A request for it was taken by the hub.</summary>
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

        /// <summary>Lets go of the comments of posts that are gone (by the post's number: <see cref="FlareCommentRules.Asked"/>). How many went.</summary>
        public int Prune(System.Func<int, bool> postKept)
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

        /// <summary>Another file, or none: nothing asked.</summary>
        public void Clear()
        {
            _all.Clear();
            Background = 0;
        }
    }
}
