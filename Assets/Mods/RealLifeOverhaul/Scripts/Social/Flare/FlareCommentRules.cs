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
    /// card; a handful of it is written out, by people chosen from the post's seed, each coming in at the moment the count
    /// reaches it, and the thread says how many more there are. Nothing is stored: the same post always gives the same
    /// comments, on every machine of a Going Public session (a shared post's seed is the city's seed and its session number,
    /// which every machine has; only the city's people comment on a shared post, never anybody's own friends or influencers).
    /// The words are asked of RLO Texts and kept by <see cref="Key"/>: a comment without words is not shown and counts among
    /// the more. Game-free (EconomyCheck, FlareCommentChecks.cs).
    /// COST: when a thread is drawn or the comments are asked for, a few rolls and a short search of the curve for each
    /// written comment (at most <see cref="WrittenMost"/>, eleven steps each); nothing per frame, nothing allocated.
    /// </summary>
    public static class FlareCommentRules
    {
        public const int WrittenLeast = 2;          // comments written out under a post with a few replies (fewer only if it has fewer)
        public const int WrittenMost = 8;           // ...and at most, however many replies it has: the rest is the "more" line
        public const int WideReach = 5000;          // people who saw a post from which one more comment is written out
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

        /// <summary>How many of a post's replies are written out: a handful, more for more replies and a wider reach, never more than it has.</summary>
        public static int Written(int total, int reach)
        {
            if (total <= 0)
            {
                return 0;
            }
            int n = WrittenLeast;
            for (int t = total; t > 1; t >>= 1)
            {
                n++;   // (one more for each doubling: 3 replies 3, 5 replies 4, 10 replies 5, 50 replies 7)
            }
            n += reach >= WideReach ? 1 : 0;
            return Math.Min(total, FlareRules.Clamp(n, WrittenLeast, WrittenMost));
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
            int seed = Seed(p, citySeed), total = p.Replies, written = Written(total, p.Reach);
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

        /// <summary>A number for what was asked already, by post and slot (this file's posts).</summary>
        public static long Asked(int postId, int slot) => (long)postId << 4 | (uint)(slot & 15);

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
}
