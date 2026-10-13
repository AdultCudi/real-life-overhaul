using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare by rule: the account's followers and reputation, what a post reaches and brings, who of the player's
    /// friends likes and answers it, a viral moment, the limits on posting, and the daily round. Game-free: whoever
    /// calls says what day it is and who the friends are; RealLifeOverhaul.Build/EconomyCheck runs these rules.
    ///
    /// A WORKED EXAMPLE (the followers a post brings, reputation 50, nothing repeated):
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
    /// x repeats, so it grows ever more slowly. Three ordinary posts a day (a selfie, an outing, a picture, each worth
    /// a little over half the next day for being the same kind again) take a new account to Known (5,000) in about a
    /// month of game days; Celebrity takes viral moments and businesses. A post in
    /// the wrong tone for what it is about (cheerful about an arrest) goes badly: 2% of the followers and 5 more are
    /// lost and the reputation falls. A viral moment (a chance of a few in a thousand to 6%, once a week at most)
    /// brings four times the gain and 3% of the followers more.
    /// </summary>
    public static class FlareRules
    {
        public static readonly int[] TierFrom = { 0, 500, 5000, 25000, 100000 };   // followers: Newcomer, Local, Known, Influencer, Celebrity

        public const int FollowFromStage = 1;      // acquaintances and closer follow the player
        public const int ReputationStart = 50;     // 0 to 100
        public const int StartExtra = 36;          // followers a new account has beyond the people the player has met
        public const int PostGapMinutes = 120;     // game minutes between two posts
        public const int PostsPerDay = 3;
        public const int DraftDays = 2;            // a draft made today can still be posted tomorrow
        public const int DraftsMost = 8;
        public const int PostsMost = 60;           // posts kept, the player's and the friends'
        public const int OwnMost = 30;             // ...of them the player's: older ones are folded into the totals
        public const int FeedRows = 20;            // the Feed shows this many

        // ---- what a post is worth, thousandths, by FlareSource (Life: a friend's own, worth nothing to the player)
        public static readonly int[] SourceQuality = { 0, 600, 750, 700, 1000, 1000, 850, 500, 650, 750, 900, 0, 800, 0, 0, 0, 0, 0, 0, 0, 0, 0, 700, 0, 0, 650, 800, 950, 850 };
        public const int WithFriend = 250;         // a friend tagged in it
        public const int InsideShop = 100;         // a picture taken in one of the player's businesses

        // ---- how a tone suits a source, thousandths: cheerful, proud, honest, cheeky
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
            { 1100, 1100,  950, 1000 }    // Interior: a place to be proud of; cheek about it is fine too
        };
        public const int BadFit = 650;             // a fit under this goes badly
        public const int GoodFit = 1150;           // ...one at or over this is well liked: a point of reputation

        public const int RepeatDays = 10;          // the same kind of post again within this many days is worth less
        public const int RepeatLoss = 500;         // thousandths lost the next day, less each day until RepeatDays
        public const double GainBase = 35.0, GainRoot = 2.2;
        public const int ReachShare = 300;         // thousandths of the followers who see an ordinary post
        public const int LikeShare = 90;           // thousandths of those who see it who like it
        public const int ReplyPer = 250;           // one reply from the city for so many who see it

        public const int ViralMost = 60;           // per mille: the best chance there is
        public const int ViralGapDays = 7;         // one viral moment a week at most
        public const int ViralTimes = 4;           // a viral post's gain is so many times an ordinary one's
        public const int ViralShare = 30;          // ...and thousandths of the followers more
        public const int ViralReach = 8;           // ...seen by so many times as many

        public const int BadShare = 20;            // thousandths of the followers a bad post loses
        public const int BadLeast = 5;
        public const int BadReputation = 4, BadReputationKnown = 6;
        public const int HonestReputation = 2;     // an honest post about an arrest
        public const int ViralReputation = 2;

        public const int SilentFrom = 7;           // days without a post after which followers drift away
        public const int SilentShare = 10;         // thousandths of them a day
        public const int PullTenths = 1;           // the reputation moves a tenth of the way to the city's opinion each day

        public const int FriendPostChance = 300;   // per mille a day for a friend who follows
        public const int FlakyPostChance = 150;

        // ---- what a post does elsewhere (Wiring/FlareLinks carries each to its feature)
        public const int BuzzMost = 6;             // per cent of a business's customers from Flare's talk, all of it together, either way
        public const int BuzzDays = 3;             // a post about a business is talked about this long
        public const int HypeGapDays = 14;         // a viral post raises the game's own hype for a product once a fortnight at most
        public const int ScandalWorst = 1250;      // thousandths of a scandal's loss to a brand, for an account of no reputation...
        public const int ScandalAnswered = 800;    // ...and what is left of it after an honest answer
        public const int ArrestShare = 30;         // thousandths of the followers an arrest costs, from Local on
        public const int ArrestReputation = 3, ArrestReputationKnown = 6;
        public const int EndorseFull = 100000;     // followers at which the account's word in an election is worth the most
        public const int BossBase = 200;           // per mille: the boss follows the player, and more for each rank and tier
        public const int BossStep = 150;
        public const int SponsoredFrom = 500;      // followers: a sponsored post is offered from Local on
        public const int SponsoredLeast = 250, SponsoredMost = 1500;   // thousandths of radio's customers a day, by followers

        // ---- the rest of the city: people met online, the partner, friends, the market, the council and the courts
        public static readonly int[] OnlinePerMonth = { 0, 2, 3, 4, 5 };   // people the player has still to meet who answer a post and become contacts, by tier
        public const int OnlineGapDays = 5;
        public const int NeverPostedDays = 21;     // the partner not posted this long, while others are: they mind
        public const int OthersLately = 7;
        public const int NeglectPosts = 3;         // posts in a day while a friend's texts wait
        public const int NeglectGapDays = 7;
        public const int MoodBadNews = 8;          // the feed's mood about the city's running: each kind of bad news today
        public const int MoodIssuePost = 10;       // ...a post of the player's about an issue
        public const int MoodKeep = 800;           // thousandths of yesterday's mood kept
        public const int IssueMost = 300;          // thousandths of a point of an issue's weight in the council a post can add, at the most
        public const int AbuseDays = 2;            // selling shares this soon after a viral post about the company is market abuse
        public const int FameFee = 150;            // thousandths more a lawyer asks of a Celebrity
        public const int Thumbs = 5;               // the newest pictures shown in their posts
        public const int ThumbWidth = 256;         // pixels

        // ---------------------------------------------------------------- the account

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

        /// <summary>A number from 0 to 999 that is always the same for the same three numbers.</summary>
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

        // ---------------------------------------------------------------- posting

        /// <summary>Why a post cannot be made now.</summary>
        public enum Wait { None, TooSoon, DayFull }

        /// <summary>Whether the player may post now: two game hours apart, three a day.</summary>
        public static Wait CanPost(FlareFile f, int day, int minuteOfDay)
        {
            if (f.PostsDay == day && f.PostsToday >= PostsPerDay)
            {
                return Wait.DayFull;
            }
            int now = day * 1440 + minuteOfDay;
            return f.LastPostMinute >= 0 && now - f.LastPostMinute < PostGapMinutes ? Wait.TooSoon : Wait.None;
        }

        /// <summary>The minute of today the next post may be made from (0: now or earlier); -1 when it is not today.</summary>
        public static int NextPostMinute(FlareFile f, int day)
        {
            if (f.PostsDay == day && f.PostsToday >= PostsPerDay)
            {
                return -1;
            }
            int next = f.LastPostMinute < 0 ? 0 : f.LastPostMinute + PostGapMinutes - day * 1440;
            return next >= 1440 ? -1 : Math.Max(0, next);
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

        /// <summary>The followers an ordinary post brings.</summary>
        public static int Gain(int followers, int quality, int fit, int reputation, int repeat)
        {
            double g = (GainBase + GainRoot * Math.Sqrt(Math.Max(0, followers))) * quality / 1000.0 * fit / 1000.0 * ReputationFactor(reputation) / 1000.0 * repeat / 1000.0;
            return (int)Math.Round(g);
        }

        /// <summary>The people who see an ordinary post.</summary>
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

        /// <summary>What a bad post costs.</summary>
        public static int BadLoss(int followers)
        {
            return Math.Min(Math.Max(0, followers), (int)((long)Math.Max(0, followers) * BadShare / 1000) + BadLeast);
        }

        /// <summary>
        /// A friend's answer to a post: 0 nothing, 1 a like, 2 a like and a reply. Tagged friends and partners always
        /// look; a flaky friend often does not; an envious one is cool about money; each likes what they like.
        /// </summary>
        public static int Reaction(in FlarePerson p, FlarePost post, int roll)
        {
            bool money = post.Source == FlareSource.Promotion || post.Source == FlareSource.Opened || post.Source == FlareSource.RecordDay;
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

        // What a friend's taste likes in particular: the shop, a workout, a meal, a night out.
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
            bool money = post.Source == FlareSource.Promotion || post.Source == FlareSource.Opened || post.Source == FlareSource.RecordDay;
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

        /// <summary>
        /// The player posts a draft in a tone, with a friend tagged or not (-1). Every figure of the account follows;
        /// the draft is spent. The friends are the social life's people, as they stand now. Returns the post. With
        /// 'viral' (a test) a post that does not go badly goes viral whatever the roll and the gap.
        /// </summary>
        public static FlarePost Post(FlareFile f, FlareDraft d, FlareTone tone, int tag, IReadOnlyList<FlarePerson> people, int day, int hour, int minuteOfDay, bool viral = false)
        {
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
            if (p.Bad)
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

        /// <summary>
        /// Whether Flare may send a notification that asks for something (a viral moment; in later stages a deal on offer
        /// or ending): only with the option on, and one a day at most. True takes the day's one. Everything else Flare
        /// has to say waits in the app: the badge counts the friends' likes and replies and the drafts.
        /// </summary>
        public static bool MayNotify(FlareFile f, int day, bool allowed)
        {
            if (!allowed || f.NoticeDay == day)
            {
                return false;
            }
            f.NoticeDay = day;
            return true;
        }

        // ---------------------------------------------------------------- what a post does elsewhere

        /// <summary>The talk a post about a business brings, per cent of its customers for three days: more as more people see it.</summary>
        public static int BuzzOf(FlarePost p)
        {
            if (p.Place == null || p.Bad || p.Author != FlareAuthor.Player)
            {
                return 0;
            }
            return p.Viral ? 4 : p.Reach >= 5000 ? 3 : p.Reach >= 1000 ? 2 : 1;
        }

        /// <summary>Talk about a business from today: kept beside what is there, the whole never past the cap either way. Returns the business's figure now.</summary>
        public static int AddBuzz(FlareFile f, string place, int percent, int day, int days)
        {
            if (string.IsNullOrEmpty(place) || percent == 0)
            {
                return f.BuzzAt(place, day);
            }
            int now = f.BuzzAt(place, day);
            int room = percent > 0 ? Math.Max(0, BuzzMost - now) : Math.Min(0, -BuzzMost - now);
            int add = percent > 0 ? Math.Min(percent, room) : Math.Max(percent, room);
            if (add != 0)
            {
                f.Buzz.Add(new FlareBuzz { Place = place, Percent = add, Until = day + Math.Max(1, days) });
            }
            return f.BuzzAt(place, day);
        }

        /// <summary>Talk that has run its days is gone. True if any went.</summary>
        public static bool BuzzDay(FlareFile f, int day)
        {
            bool gone = false;
            for (int i = f.Buzz.Count - 1; i >= 0; i--)
            {
                if (day >= f.Buzz[i].Until)
                {
                    f.Buzz.RemoveAt(i);
                    gone = true;
                }
            }
            return gone;
        }

        public static bool MayHype(FlareFile f, int day)
        {
            return day - f.HypeDay >= HypeGapDays;
        }

        /// <summary>What a scandal at a brand's shop costs, thousandths of its loss: more for an account the city doubts, never under the whole.</summary>
        public static int ScandalScale(int reputation)
        {
            return reputation >= 50 ? 1000 : Clamp(1000 + (50 - reputation) * (ScandalWorst - 1000) / 50, 1000, ScandalWorst);
        }

        /// <summary>What an honest answer to a scandal gives back of what it cost: down to four fifths of the plain loss.</summary>
        public static int Answered(int lost, int scale)
        {
            return Math.Max(0, lost - (int)((long)lost * ScandalAnswered / Math.Max(1000, scale)));
        }

        /// <summary>How much of the most an account's word adds to an endorsement, thousandths: by followers on a log scale, and only for a reputation the city trusts.</summary>
        public static int EndorseShare(int followers, int reputation)
        {
            double share = Math.Log10(1.0 + Math.Max(0, followers)) / Math.Log10(1.0 + EndorseFull);
            return Clamp((int)(Math.Min(1.0, share) * 1000.0 * Clamp(reputation, 0, 50) / 50.0), 0, 1000);
        }

        /// <summary>An arrest trends: from Local on, 3% of the followers and some reputation, once a day. Returns the followers lost.</summary>
        public static int Arrest(FlareFile f, int day)
        {
            if (f.ArrestDay == day || f.Followers < TierFrom[(int)FlareTier.Local])
            {
                return 0;
            }
            f.ArrestDay = day;
            int lost = (int)((long)f.Followers * ArrestShare / 1000);
            f.Reputation = Clamp(f.Reputation - (f.Tier >= FlareTier.Known ? ArrestReputationKnown : ArrestReputation), 0, 100);
            f.Followers -= lost;
            return lost;
        }

        /// <summary>Whether the boss follows the player: a fifth of bosses, and more for each rank and each tier of the account. Always the same for the same job.</summary>
        public static bool BossFollows(int seed, int career, FlareTier tier)
        {
            return career >= 0 && Roll(seed, career / 100, 811) < BossBase + BossStep * (career % 100 + (int)tier);
        }

        /// <summary>What a sponsored post brings, thousandths of radio's customers a day: by followers on a log scale, from Local on; 0 below.</summary>
        public static int SponsoredReach(int followers)
        {
            if (followers < SponsoredFrom)
            {
                return 0;
            }
            return Clamp((int)Math.Round(SponsoredLeast + 375.0 * Math.Log10(followers / (double)SponsoredFrom)), SponsoredLeast, SponsoredMost);
        }

        /// <summary>What a sponsored post costs, thousandths of its base price: its reach, a little less for a trusted account and more for a doubted one.</summary>
        public static int SponsoredCost(int reach, int reputation)
        {
            return (int)((long)reach * (1100 - 2 * Clamp(reputation, 0, 100)) / 1000);
        }

        // ---------------------------------------------------------------- the rest of the city

        /// <summary>Whether, on a day after a post, somebody the player has still to meet answers it and becomes a contact: a few a month, more for a bigger account.</summary>
        public static bool MeetsOnline(FlareFile f, int day, bool postedYesterday)
        {
            int perMonth = OnlinePerMonth[Clamp((int)f.Tier, 0, OnlinePerMonth.Length - 1)];
            return postedYesterday && perMonth > 0 && day - f.OnlineDay >= OnlineGapDays && Roll(f.Seed, day, 901) < perMonth * 1000 / 30;
        }

        /// <summary>How a partner takes a post of the player with somebody they could fall for.</summary>
        public enum PartnerView { Proud = 0, Cool = 1, Jealous = 2 }

        public static PartnerView ViewOf(FlareTaste partner)
        {
            return (partner & FlareTaste.Cheers) != 0 ? PartnerView.Proud : (partner & FlareTaste.Cool) != 0 ? PartnerView.Jealous : PartnerView.Cool;
        }

        /// <summary>A tag noted: the partner's, or somebody else's (for "you never post us").</summary>
        public static void Tagged(FlareFile f, bool partner, int day)
        {
            if (partner)
            {
                f.PartnerTagDay = day;
            }
            else
            {
                f.OthersTagDay = day;
            }
        }

        /// <summary>The partner has not been in a post for three weeks while others have been lately: they mind it, once in three weeks.</summary>
        public static bool NeverPosted(FlareFile f, int day, bool partner)
        {
            if (!partner || day - Math.Max(f.PartnerTagDay, f.StartDay) < NeverPostedDays || f.OthersTagDay < 0 || day - f.OthersTagDay > OthersLately || day - f.NeverDay < NeverPostedDays)
            {
                return false;
            }
            f.NeverDay = day;
            return true;
        }

        /// <summary>A friend whose texts wait minds the player posting all day: three posts yesterday, once a week.</summary>
        public static bool Neglect(FlareFile f, int day, int postsYesterday, bool unanswered)
        {
            if (!unanswered || postsYesterday < NeglectPosts || day - f.NeglectDay < NeglectGapDays)
            {
                return false;
            }
            f.NeglectDay = day;
            return true;
        }

        /// <summary>The feed's mood about the city's running for a day: four fifths of yesterday's, less for each kind of bad news.</summary>
        public static int Mood(FlareFile f, int badNews)
        {
            f.MayorMood = Clamp(f.MayorMood * MoodKeep / 1000 - MoodBadNews * Math.Max(0, badNews), -100, 100);
            return f.MayorMood;
        }

        /// <summary>What the mood pulls on the mayor's approval a day, in tenths of a point: a point at most either way.</summary>
        public static int ApprovalPull(int mood)
        {
            return Clamp(mood / 10, -10, 10);
        }

        /// <summary>What a post about an issue adds to its weight in the council, thousandths of a point: by followers on a log scale, at most 0.3.</summary>
        public static int IssueWeight(int followers)
        {
            return (int)(IssueMost * Math.Min(1.0, Math.Log10(1.0 + Math.Max(0, followers)) / Math.Log10(1.0 + EndorseFull)));
        }

        /// <summary>A post about a company the player holds carries a disclosure from Known on.</summary>
        public static bool Discloses(FlareFile f)
        {
            return f.Tier >= FlareTier.Known;
        }

        /// <summary>Selling a company's shares on a day: market abuse if the player's post about it went viral within two days. The viral post it was, or null (spent if found).</summary>
        public static FlareViralShares Abuse(FlareFile f, int stock, int day)
        {
            for (int i = 0; i < f.ViralShares.Count; i++)
            {
                FlareViralShares v = f.ViralShares[i];
                if (v.Stock == stock && day - v.Day <= AbuseDays && day >= v.Day)
                {
                    f.ViralShares.RemoveAt(i);
                    return v;
                }
            }
            return null;
        }

        /// <summary>Viral posts about shares older than the window are let go.</summary>
        public static void AbuseDay(FlareFile f, int day)
        {
            for (int i = f.ViralShares.Count - 1; i >= 0; i--)
            {
                if (day - f.ViralShares[i].Day > AbuseDays)
                {
                    f.ViralShares.RemoveAt(i);
                }
            }
        }

        /// <summary>What a lawyer asks more of a Celebrity, thousandths.</summary>
        public static int Fame(FlareFile f)
        {
            return f.Tier >= FlareTier.Celebrity ? FameFee : 0;
        }

        /// <summary>The newest pictures of the player's posts, the ones shown with their picture: their post numbers into the list given, newest first.</summary>
        public static void Pictures(FlareFile f, List<int> into)
        {
            into.Clear();
            for (int i = f.Posts.Count - 1; i >= 0 && into.Count < Thumbs; i--)
            {
                FlarePost p = f.Posts[i];
                if (p.Author == FlareAuthor.Player && (p.Source == FlareSource.Photo || p.Source == FlareSource.Selfie) && !string.IsNullOrEmpty(p.Name))
                {
                    into.Add(p.Id);
                }
            }
        }

        public static int Count(int bits)
        {
            int n = 0;
            for (; bits != 0; bits &= bits - 1)
            {
                n++;
            }
            return n;
        }

        /// <summary>Keeps the posts within their bounds: the oldest friend's post goes first, and the player's own beyond thirty.</summary>
        public static void Prune(FlareFile f)
        {
            int own = f.OwnPosts;
            while (own > OwnMost)
            {
                int at = Oldest(f.Posts, true);
                f.Posts.RemoveAt(at);
                own--;
            }
            while (f.Posts.Count > PostsMost)
            {
                int at = Oldest(f.Posts, false);
                f.Posts.RemoveAt(at < 0 ? 0 : at);
            }
        }

        private static int Oldest(List<FlarePost> posts, bool own)
        {
            for (int i = 0; i < posts.Count; i++)
            {
                if ((posts[i].Author == FlareAuthor.Player) == own)
                {
                    return i;
                }
            }
            return -1;
        }

        // ---------------------------------------------------------------- drafts

        /// <summary>
        /// Something happened that can be posted about. One draft a source and a thing (the same promotion is not drafted
        /// twice); the oldest goes when there are eight. Returns the draft, or null if it was there already.
        /// </summary>
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

        /// <summary>A draft made today can still be posted tomorrow; after that it is gone.</summary>
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

        /// <summary>
        /// A business's day: the first sight of it (opened, once Flare had been running a day), or its best day of
        /// takings yet (after three days with figures, and a fifth better than the old best). What to draft.
        /// </summary>
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

        // ---------------------------------------------------------------- the day

        /// <summary>
        /// The daily round, once a day whatever the time skipped: the reputation moves a tenth of the way to the city's
        /// opinion, followers drift away after a week of silence, friends follow or stop, a few of them post, drafts
        /// lapse and the chart moves on.
        /// </summary>
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

        /// <summary>The topics a friend's own post can be about (FlareText words them): their work, their part of town, and what they like.</summary>
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
