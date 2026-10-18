using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare by rule, the upkeep: liked replies, the posts kept, drafts and the daily round.</summary>
    public static partial class FlareRules
    {
        public const int ReplyLikesMost = 128;   // more than any save has, and a bound for a damaged line

        /// <summary>A friend's reply's stable key: its post's number and its place among the post's reactions.</summary>
        public static int ReplyKey(int parentId, int slot)
        {
            return parentId * 32 + slot;
        }

        public static bool ReplyLiked(FlareFile f, int parentId, int slot)
        {
            return f != null && slot >= 0 && slot < 32 && f.ReplyLikes.Contains(ReplyKey(parentId, slot));
        }

        /// <summary>The player likes a friend's reply, or takes the like back; the new state. Moves nothing else and sends nothing.</summary>
        public static bool ToggleReplyLike(FlareFile f, int parentId, int slot)
        {
            if (f == null || slot < 0 || slot >= 32 || f.Post(parentId) == null)
            {
                return false;
            }
            int key = ReplyKey(parentId, slot);
            if (f.ReplyLikes.Remove(key))
            {
                return false;
            }
            if (f.ReplyLikes.Count >= ReplyLikesMost)
            {
                f.ReplyLikes.RemoveAt(0);
            }
            f.ReplyLikes.Add(key);
            return true;
        }

        /// <summary>Lets go of the likes of replies whose post is gone. How many went.</summary>
        public static int DropReplyLikes(FlareFile f)
        {
            int n = 0;
            for (int i = f.ReplyLikes.Count - 1; i >= 0; i--)
            {
                if (f.Post(f.ReplyLikes[i] / 32) == null)
                {
                    f.ReplyLikes.RemoveAt(i);
                    n++;
                }
            }
            return n;
        }

        /// <summary>Keeps the posts within their bounds, the city's old ones first; a post on its way to a session's host is kept.</summary>
        public static void Prune(FlareFile f)
        {
            FlareLiveRules.PruneCity(f);
            int own = f.OwnPosts;
            while (own > OwnMost)
            {
                int at = Oldest(f.Posts, true);
                if (at < 0 || !OwnAfter(f.Posts, at))
                {
                    break;   // the only own post not on its way is the newest
                }
                f.Posts.RemoveAt(at);
                own--;
            }
            while (f.Posts.Count > PostsMost)
            {
                int at = Oldest(f.Posts, false);
                at = at >= 0 ? at : Oldest(f.Posts, true);
                if (at < 0)
                {
                    break;   // only posts on their way are left
                }
                f.Posts.RemoveAt(at);
            }
            DropLikesOfGone(f);
        }

        /// <summary>Lets go of the likes of display-only replies and comments whose post is gone. How many went.</summary>
        public static int DropLikesOfGone(FlareFile f)
        {
            return DropReplyLikes(f) + FlareCommentRules.DropLikes(f);
        }

        private static bool OwnAfter(List<FlarePost> posts, int at)
        {
            for (int i = at + 1; i < posts.Count; i++)
            {
                if (posts[i].Author == FlareAuthor.Player)
                {
                    return true;
                }
            }
            return false;
        }

        private static int Oldest(List<FlarePost> posts, bool own)
        {
            for (int i = 0; i < posts.Count; i++)
            {
                if ((posts[i].Author == FlareAuthor.Player) == own && !posts[i].Pending)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>A draft of something that happened, once a source and thing; the oldest of eight goes. Null if it was there.</summary>
        public static FlareDraft Draft(FlareFile f, FlareSource source, int day, int hour, int person, string place, string name, int arg)
        {
            for (int i = 0; i < f.Drafts.Count; i++)
            {
                FlareDraft d = f.Drafts[i];
                if (d.Source == source && d.Person == person && d.Place == place && d.Name == name && d.Arg == arg && d.Day == day)
                {
                    return null;
                }
            }
            while (f.Drafts.Count >= DraftsMost)
            {
                f.Drafts.RemoveAt(0);
            }
            var draft = new FlareDraft { Id = f.NextId++, Source = source, Day = day, Hour = hour, Person = person, Place = place, Name = name, Arg = arg };
            f.Drafts.Add(draft);
            return draft;
        }

        public static bool Lapsed(FlareDraft d, int day)
        {
            return day - d.Day >= DraftDays;
        }

        /// <summary>The ballot (1) and the endorsement (2) of the election on now, against what was drafted already: what is new to draft.</summary>
        public static int CivicNew(FlareFile f, int mask)
        {
            int fresh = mask & ~f.CivicSeen;
            f.CivicSeen = mask;   // a bit that clears (a new election) can be drafted again
            return fresh;
        }

        /// <summary>The employer and rank now (employer * 100 + rank, -1 out of work), against the last seen: true if it is a promotion.</summary>
        public static bool Promoted(FlareFile f, int key)
        {
            int before = f.CareerKey;
            f.CareerKey = key;
            return before >= 0 && key >= 0 && key / 100 == before / 100 && key % 100 > before % 100;
        }

        /// <summary>A business's day: opened (first seen after Flare's first day), or a record a fifth over its best after three days.</summary>
        public static FlareSource ShopDay(FlareFile f, string place, int takings, int day)
        {
            FlareShopSeen s = f.Shop(place);
            if (s == null)
            {
                f.Shops.Add(new FlareShopSeen { Place = place, FirstDay = day, Best = Math.Max(0, takings), Days = takings > 0 ? 1 : 0 });
                return day > f.StartDay ? FlareSource.Opened : FlareSource.None;
            }
            if (takings <= 0)
            {
                return FlareSource.None;
            }
            s.Days++;
            bool record = s.Days > 3 && takings > s.Best + s.Best / 5;
            s.Best = Math.Max(s.Best, takings);
            return record ? FlareSource.RecordDay : FlareSource.None;
        }

        /// <summary>The daily round, once a day: reputation, silence, the friends following and posting, drafts, the chart.</summary>
        public static FlareDayReport Day(FlareFile f, int day, int publicReputation, IReadOnlyList<FlarePerson> people)
        {
            var r = new FlareDayReport { Day = day, FollowersBefore = f.Followers, ReputationBefore = f.Reputation };
            f.SettledDay = day;
            int gap = Clamp(publicReputation, 0, 100) - f.Reputation;
            int step = gap * PullTenths / 10;
            f.Reputation = Clamp(f.Reputation + (step != 0 ? step : Math.Sign(gap)), 0, 100);
            r.Silent = f.LastPostDay < 0 ? day - f.StartDay : day - f.LastPostDay;
            if (r.Silent >= SilentFrom && f.Followers > StartExtra)
            {
                r.Lost = Math.Max(1, (int)((long)f.Followers * SilentShare / 1000));
                f.Followers -= r.Lost;
            }
            int following = 0;
            for (int i = 0; people != null && i < people.Count; i++)
            {
                FlarePerson p = people[i];
                if (p.Follows && p.Index >= 0 && p.Index <= 30)
                {
                    following |= 1 << p.Index;
                    int chance = (p.Taste & FlareTaste.Flaky) != 0 ? FlakyPostChance : FriendPostChance;
                    if (Roll(f.Seed, day, 300 + p.Index) < chance)
                    {
                        FriendPost(f, p, day);
                        r.FriendPosts++;
                    }
                }
            }
            f.Following = following;
            r.Following = Count(following);
            for (int i = f.Drafts.Count - 1; i >= 0; i--)
            {
                if (Lapsed(f.Drafts[i], day))
                {
                    f.Drafts.RemoveAt(i);
                    r.DraftsGone++;
                }
            }
            BuzzDay(f, day);
            Array.Copy(f.History, 1, f.History, 0, f.History.Length - 1);
            f.History[f.History.Length - 1] = f.Followers;
            Prune(f);
            r.FollowersAfter = f.Followers;
            r.ReputationAfter = f.Reputation;
            return r;
        }

        /// <summary>The topics a friend's own post can be about (FlareText words them).</summary>
        public enum LifeTopic { Work = 0, District = 1, Sport = 2, Food = 3, Night = 4, Shops = 5, Plain = 6 }

        public const int LifeVariants = 2;

        // A friend posts something of their own: a topic by what they like, a variant by the day.
        private static void FriendPost(FlareFile f, in FlarePerson p, int day)
        {
            int roll = Roll(f.Seed, day, 400 + p.Index);
            LifeTopic topic = roll < 250 ? LifeTopic.Work : roll < 450 ? LifeTopic.District : LifeTopic.Plain;
            if (roll >= 450 && roll < 800)
            {
                topic = (p.Taste & FlareTaste.Sport) != 0 ? LifeTopic.Sport : (p.Taste & FlareTaste.Food) != 0 ? LifeTopic.Food
                    : (p.Taste & FlareTaste.Night) != 0 ? LifeTopic.Night : (p.Taste & FlareTaste.Shops) != 0 ? LifeTopic.Shops : LifeTopic.Plain;
            }
            int id = f.NextId++;
            int reach = 40 + Roll(f.Seed, id, 5) / 4;
            f.Posts.Add(new FlarePost
            {
                Id = id, Day = day, Hour = 9 + roll % 12, Author = FlareAuthor.Friend, Who = p.Index, Source = FlareSource.Life, Arg = (int)topic * 10 + roll % LifeVariants,
                Seed = Roll(f.Seed, id, 17) * 1000 + Roll(f.Seed, id, 29), Reach = reach, Likes = reach / 6, Replies = reach / 40
            });
        }
    }
}
