using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare by rule: the account's followers and reputation, what a post reaches and brings, who of the player's friends
    /// likes and answers it, viral moments and the daily round. Game-free: the caller gives the day and the friends.
    ///
    /// The followers a post brings, reputation 50, nothing repeated:
    ///
    ///   followers   post                                  quality  tone fit   gain   reach
    ///          50   a selfie with a friend, cheerful         1000      1100     56      16
    ///       1,000   a selfie alone, cheerful                  750      1100     86     248
    ///       5,000   a shop opened, proud                     1000      1150    219   1,725
    ///      25,000   a record day, proud                       850      1150    374   7,331
    ///     100,000   a shop opened, proud, reputation 80      1000      1150    966  34,500
    ///       5,000   a selfie alone, reputation 10             750      1100    126   1,238
    ///
    /// The gain is (35 + 2.2 x the square root of the followers) x quality x tone fit x reputation (750 + 5 a point)
    /// x repeats. Only the first three posts of a day move the account; three ordinary posts a day take a new account to
    /// Known (5,000) in about a month.
    /// </summary>
    public static partial class FlareRules
    {
        public static readonly int[] TierFrom = { 0, 500, 5000, 25000, 100000 };   // followers: Newcomer, Local, Known, Influencer, Celebrity

        public const int FollowFromStage = 1;      // acquaintances and closer follow the player
        public const int ReputationStart = 50;     // 0 to 100
        public const int StartExtra = 36;          // followers a new account has beyond the people the player has met
        public const int PostsCounted = 3;         // the player's posts a day that move the account; later ones are seen and liked only
        public const int DraftDays = 2;            // a draft made today can still be posted tomorrow
        public const int DraftsMost = 8;
        public const int PostsMost = 220;          // the player's, the friends' and the city's (the city's go first: FlareLiveRules.CityKeepDays)
        public const int OwnMost = 40;             // ...of them the player's: older ones are folded into the totals
        public const int FeedRows = 120;           // only the cards in view are built (FlareList)

        // What a post is worth by FlareSource, thousandths (Life: the player's plain post of the moment).
        public static readonly int[] SourceQuality =
        {
            0, 600, 750, 700, 1000, 1000, 850, 500, 650, 750, 900, 400, 800, 0, 0, 0, 0, 0, 0, 0, 0, 0, 700, 0, 0, 650, 800, 950,
            850, 650, 600, 700, 0, 0, 0, 0, 0, 0
        };
        public const int WithFriend = 250;         // a friend tagged in it
        public const int InsideShop = 100;         // a picture taken in one of the player's businesses

        // How a tone suits a source, thousandths: cheerful, proud, honest, cheeky.
        private static readonly int[,] ToneFits =
        {
            { 1000, 1000, 1000, 1000 },   // None
            { 1000,  900,  950, 1100 },   // Photo
            { 1100,  900,  950, 1050 },   // Selfie
            { 1150,  900, 1000, 1000 },   // Outing
            { 1000, 1150,  950,  850 },   // Promotion
            { 1100, 1150,  950,  900 },   // Opened
            { 1000, 1150,  900,  850 },   // RecordDay
            { 1000,  950, 1100,  800 },   // Voted
            {  950, 1000, 1100,  750 },   // Endorsed
            {  900,  700, 1150,  800 },   // GoodDeed: proud is bragging
            {  600,  500, 1200,  550 },   // Arrested: only honesty does not go badly
            { 1000, 1000, 1000, 1000 },   // Life
            {  600,  550, 1200,  500 },   // Scandal: the same
            { 1000, 1000, 1000, 1000 },   // Sponsored
            { 1000, 1000, 1000, 1000 },   // Niche ... CityNews: the city's own accounts, never the player's
            { 1000, 1000, 1000, 1000 },
            { 1000, 1000, 1000, 1000 },
            { 1000, 1000, 1000, 1000 },
            { 1000, 1000, 1000, 1000 },
            { 1000, 1000, 1000, 1000 },
            { 1000, 1000, 1000, 1000 },
            { 1000, 1000, 1000, 1000 },
            {  900, 1000, 1150,  700 },   // Shares: honest is best; cheeky about money is not liked
            { 1000, 1000, 1000, 1000 },   // Results
            { 1000, 1000, 1000, 1000 },   // Headline
            {  800,  900, 1150,  950 },   // Issue
            { 1050, 1000, 1100,  800 },   // Campaign: proud of one's own run is fine; cheek about it less so
            { 1150, 1150, 1000,  950 },   // Graduated: cheerful and proud both suit it
            { 1100, 1100,  950, 1000 },   // Interior: a place to be proud of; cheek about it is fine too
            {  800,  800, 1150,  850 },   // Report: plainly put is best; cheerful about a pothole reads oddly
            { 1000,  950, 1100,  900 },   // Review
            { 1100, 1150,  950,  950 },   // Announce
            { 1000, 1000, 1000, 1000 },   // Reply ... Answer: the city's own and replies, never a post of the player's that gains
            { 1000, 1000, 1000, 1000 },
            { 1000, 1000, 1000, 1000 },
            { 1000, 1000, 1000, 1000 },
            { 1000, 1000, 1000, 1000 },
            { 1000, 1000, 1000, 1000 }
        };
        public const int BadFit = 650;             // a fit under this goes badly
        public const int GoodFit = 1150;           // ...one at or over this is well liked: a point of reputation

        public const int RepeatDays = 10;          // the same kind of post again within this many days is worth less
        public const int RepeatLoss = 500;         // thousandths lost the next day, less each day until RepeatDays
        public const double GainBase = 35.0, GainRoot = 2.2;   // followers: GainBase + GainRoot x the square root of the followers
        public const int ReachShare = 300;         // thousandths of the followers who see an ordinary post
        public const int LikeShare = 90;           // thousandths of those who see it who like it
        public const int ReplyPer = 250;           // one reply from the city for so many who see it

        public const int ViralMost = 60;           // per mille: the best chance there is
        public const int ViralGapDays = 7;         // one viral moment a week at most
        public const int ViralTimes = 4;           // a viral post's gain is so many times an ordinary one's
        public const int ViralShare = 30;          // ...and thousandths of the followers more
        public const int ViralReach = 8;           // ...seen by so many times as many

        public const int BadShare = 20;            // thousandths of the followers a bad post loses
        public const int BadLeast = 5;             // followers
        public const int BadReputation = 4, BadReputationKnown = 6;   // points
        public const int HonestReputation = 2;     // points, for an honest post about an arrest
        public const int ViralReputation = 2;      // points

        public const int SilentFrom = 7;           // days without a post after which followers drift away
        public const int SilentShare = 10;         // thousandths of them a day
        public const int PullTenths = 1;           // the reputation moves a tenth of the way to the city's opinion each day

        public const int FriendPostChance = 300;   // per mille a day for a friend who follows
        public const int FlakyPostChance = 150;   // per mille a day

        public static FlareTier TierOf(int followers)
        {
            for (int t = TierFrom.Length - 1; t > 0; t--)
            {
                if (followers >= TierFrom[t])
                {
                    return (FlareTier)t;
                }
            }
            return FlareTier.Newcomer;
        }

        /// <summary>The followers the next tier starts at; -1 at the top.</summary>
        public static int NextTierAt(int followers)
        {
            int t = (int)TierOf(followers) + 1;
            return t < TierFrom.Length ? TierFrom[t] : -1;
        }

        /// <summary>A new account: the people the player has met, and a few dozen more.</summary>
        public static FlareFile Open(int seed, int day, int met, int publicReputation)
        {
            var f = new FlareFile { Seed = seed, StartDay = day, SettledDay = day, Followers = Math.Max(0, met) + StartExtra, Reputation = Clamp(publicReputation, 0, 100) };
            for (int i = 0; i < f.History.Length; i++)
            {
                f.History[i] = f.Followers;
            }
            return f;
        }

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>0 to 999, always the same for the same three numbers.</summary>
        public static int Roll(int seed, int a, int b)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)a * 2246822519u ^ (uint)b * 3266489917u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h % 1000u);
            }
        }

        public static int PostsToday(FlareFile f, int day)
        {
            return f.PostsDay == day ? f.PostsToday : 0;
        }

        /// <summary>Whether the player's next post today moves the account: the first three of a day do.</summary>
        public static bool Counts(FlareFile f, int day)
        {
            return PostsToday(f, day) < PostsCounted;
        }

        public static int Quality(FlareSource source, bool tagged, bool inShop)
        {
            int q = SourceQuality[Clamp((int)source, 0, SourceQuality.Length - 1)];
            return q + (tagged ? WithFriend : 0) + (inShop && (source == FlareSource.Photo || source == FlareSource.Selfie) ? InsideShop : 0);
        }

        public static int ToneFit(FlareSource source, FlareTone tone)
        {
            return ToneFits[Clamp((int)source, 0, ToneFits.GetLength(0) - 1), Clamp((int)tone, 0, 3)];
        }

        /// <summary>The tone that suits a source best: the one a draft is offered in first.</summary>
        public static FlareTone BestTone(FlareSource source)
        {
            int best = 0;
            for (int t = 1; t < 4; t++)
            {
                best = ToneFit(source, (FlareTone)t) > ToneFit(source, (FlareTone)best) ? t : best;
            }
            return (FlareTone)best;
        }

        /// <summary>What posting the same kind again leaves of a post, thousandths: half the next day, all of it again after ten days.</summary>
        public static int Repeat(int lastDay, int day)
        {
            if (lastDay < 0)
            {
                return 1000;
            }
            int since = Math.Max(0, day - lastDay);
            return since >= RepeatDays ? 1000 : 1000 - RepeatLoss * (RepeatDays - since) / RepeatDays;
        }

        public static int ReputationFactor(int reputation)
        {
            return 750 + 5 * Clamp(reputation, 0, 100);
        }

        public static int Gain(int followers, int quality, int fit, int reputation, int repeat)
        {
            double g = (GainBase + GainRoot * Math.Sqrt(Math.Max(0, followers))) * quality / 1000.0 * fit / 1000.0 * ReputationFactor(reputation) / 1000.0 * repeat / 1000.0;
            return (int)Math.Round(g);
        }

        public static int Reach(int followers, int quality, int fit)
        {
            return (int)Math.Round(Math.Max(0, followers) * (ReachShare / 1000.0) * quality / 1000.0 * fit / 1000.0);
        }

        /// <summary>The chance of a viral moment, per mille: more for a good post and a bigger account, never over the most.</summary>
        public static int ViralChance(int followers, int quality, int fit)
        {
            double chance = quality / 1000.0 * fit / 1000.0 * (3.0 + 5.0 * Math.Log10(1.0 + Math.Max(0, followers)));
            return Clamp((int)Math.Round(chance), 0, ViralMost);
        }

        public static bool MayGoViral(FlareFile f, int day)
        {
            return day - f.LastViralDay >= ViralGapDays;
        }

        public static int BadLoss(int followers)
        {
            return Math.Min(Math.Max(0, followers), (int)((long)Math.Max(0, followers) * BadShare / 1000) + BadLeast);
        }

        /// <summary>A friend's answer to a post: 0 nothing, 1 a like, 2 a like and a reply.</summary>
        public static int Reaction(in FlarePerson p, FlarePost post, int roll)
        {
            bool money = AboutMoney(post);
            if (p.Index == post.Tag || p.Partner)
            {
                return 2;
            }
            if ((p.Taste & FlareTaste.Flaky) != 0 && roll < 500)
            {
                return 0;
            }
            if ((p.Taste & FlareTaste.Cool) != 0 && money)
            {
                return roll < 400 ? 2 : 0;   // a reply, and a cool one (ReplyKind)
            }
            if ((p.Taste & FlareTaste.Cheers) != 0)
            {
                return roll < 600 ? 2 : 1;
            }
            if (Likes(p.Taste, post))
            {
                return roll < 500 ? 2 : 1;
            }
            int like = 300 + p.Closeness * 5, reply = p.Closeness * 3;
            if (post.Source == FlareSource.Arrested)
            {
                like /= 2;
            }
            return roll < reply ? 2 : roll < like ? 1 : 0;
        }

        private static bool Likes(FlareTaste taste, FlarePost post)
        {
            if ((taste & FlareTaste.Shops) != 0 && (post.Source == FlareSource.Opened || post.Source == FlareSource.RecordDay || post.Place != null))
            {
                return true;
            }
            if (post.Source != FlareSource.Outing)
            {
                return false;
            }
            int kind = post.Arg;   // the social life's PlanKind: 0 coffee, 1 meal, 2 drinks, 3 cinema, 4 walk, 5 drive, 6 workout, 7 the shop
            return ((taste & FlareTaste.Food) != 0 && (kind == 0 || kind == 1)) || ((taste & FlareTaste.Night) != 0 && (kind == 2 || kind == 3))
                   || ((taste & FlareTaste.Sport) != 0 && (kind == 4 || kind == 6)) || ((taste & FlareTaste.Shops) != 0 && kind == 7);
        }

        /// <summary>The kinds of reply there are words for (FlareText).</summary>
        public enum ReplyKind { Plain = 0, Cheer = 1, Proud = 2, Cool = 3, Kind = 4, Love = 5, Funny = 6 }

        public static ReplyKind ReplyOf(in FlarePerson p, FlarePost post)
        {
            bool money = AboutMoney(post);
            if (post.Source == FlareSource.Arrested)
            {
                return ReplyKind.Kind;
            }
            if ((p.Taste & FlareTaste.Cool) != 0 && money && !p.Partner)
            {
                return ReplyKind.Cool;
            }
            if (p.Partner)
            {
                return ReplyKind.Love;
            }
            if (post.Tone == FlareTone.Cheeky)
            {
                return ReplyKind.Funny;
            }
            return money ? ReplyKind.Proud : (p.Taste & FlareTaste.Cheers) != 0 ? ReplyKind.Cheer : ReplyKind.Plain;
        }

        private static bool AboutMoney(FlarePost post)
        {
            return post.Source == FlareSource.Promotion || post.Source == FlareSource.Opened || post.Source == FlareSource.RecordDay;
        }

        /// <summary>The player posts a draft (tag -1 for none) and spends it; with <paramref name="viral"/>, a test, it goes viral unless it went badly.</summary>
        public static FlarePost Post(FlareFile f, FlareDraft d, FlareTone tone, int tag, IReadOnlyList<FlarePerson> people, int day, int hour, int minuteOfDay,
            bool viral = false)
        {
            bool counts = viral || Counts(f, day);
            int id = f.NextId++;
            FlarePost last = f.LastOwn(d.Source);
            var p = new FlarePost
            {
                Id = id, Day = day, Hour = hour, Author = FlareAuthor.Player, Source = d.Source, Tone = tone, Tag = tag, Place = d.Place, Name = d.Name, Arg = d.Arg,
                Seed = Roll(f.Seed, id, 17) * 1000 + Roll(f.Seed, id, 29)
            };
            int quality = Quality(d.Source, tag >= 0, d.Place != null);
            int fit = ToneFit(d.Source, tone);
            int repeat = Repeat(last != null ? last.Day : -1, day);
            p.Quality = quality * repeat / 1000;
            p.Bad = fit < BadFit;
            int followers = f.Followers;
            int reach = Reach(followers, p.Quality, fit);
            int gain = Gain(followers, quality, fit, f.Reputation, repeat);
            int reputation = 0;
            if (!counts)
            {
                gain = 0;   // past the day's three: the account stays as it is
            }
            else if (p.Bad)
            {
                gain = -BadLoss(followers);
                reputation -= f.Tier >= FlareTier.Known ? BadReputationKnown : BadReputation;
            }
            else
            {
                reputation += fit >= GoodFit ? 1 : 0;
                reputation += d.Source == FlareSource.Arrested && tone == FlareTone.Honest ? HonestReputation : 0;
                if (viral || (MayGoViral(f, day) && Roll(f.Seed, id, 101) < ViralChance(followers, p.Quality, fit)))
                {
                    p.Viral = true;
                    f.LastViralDay = day;
                    gain = gain * ViralTimes + (int)((long)followers * ViralShare / 1000);
                    reach *= ViralReach;
                    reputation += ViralReputation;
                }
            }
            p.Reach = Math.Max(reach, 1);
            p.Likes = (int)((long)p.Reach * LikeShare / 1000);
            p.Replies = p.Reach / ReplyPer;
            for (int i = 0; people != null && i < people.Count; i++)
            {
                FlarePerson person = people[i];
                if (!person.Follows || person.Index < 0 || person.Index > 30)
                {
                    continue;
                }
                int answer = Reaction(person, p, Roll(p.Seed, person.Index, 7));
                if (answer >= 1)
                {
                    p.Likers |= 1 << person.Index;
                    p.Likes++;
                }
                if (answer >= 2)
                {
                    p.Repliers |= 1 << person.Index;
                    p.Replies++;
                }
            }
            p.Gained = gain;
            p.Disclosed = d.Source == FlareSource.Shares && Discloses(f);
            if (p.Viral && d.Source == FlareSource.Shares)
            {
                f.ViralShares.Add(new FlareViralShares { Stock = d.Arg, Name = d.Name, Day = day });
            }
            if (tag >= 0)
            {
                bool partner = false;
                for (int i = 0; people != null && i < people.Count; i++)
                {
                    partner |= people[i].Index == tag && people[i].Partner;
                }
                Tagged(f, partner, day);
            }
            f.Followers = Math.Max(0, followers + gain);
            f.Reputation = Clamp(f.Reputation + reputation, 0, 100);
            f.LastPostDay = day;
            f.LastPostMinute = day * 1440 + minuteOfDay;
            if (f.PostsDay != day)
            {
                f.PostsDay = day;
                f.PostsToday = 0;
            }
            f.PostsToday++;
            f.TotalPosts++;
            f.TotalLikes += p.Likes;
            f.Unseen += Count(p.Likers) + Count(p.Repliers);   // the badge: friends' likes and replies, never a notification
            f.Drafts.Remove(d);
            f.Posts.Add(p);
            Prune(f);
            return p;
        }

        /// <summary>Whether Flare may send a notification that asks for something: with the option on, one a day at most. True takes the day's one.</summary>
        public static bool MayNotify(FlareFile f, int day, bool allowed)
        {
            if (!allowed || f.NoticeDay == day)
            {
                return false;
            }
            f.NoticeDay = day;
            return true;
        }
    }
}
