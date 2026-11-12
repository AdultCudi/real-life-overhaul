using System;
using RealLifeOverhaul.Ui;

// The world every Flare sample is drawn from: one player, their followers, posts, replies, ads, businesses, messages and days, and nothing else. Every head, tile, rail and
// count the samples show is worked out from it through FlareFigures, the functions the Live face (FlareScreen) calls too, so a figure on a page cannot say something the
// world and the other pages do not. A state is the world with the player established (Walter Mack of Bloom Cafe) or new (nobody follows, nothing posted, no review).
internal static partial class UiSamples
{
    internal struct FlareFunnelWorld
    {
        public int Seen, Interested, Followed, Came;   // Came below zero: it put that many off
        public int Dollars;                            // takings it brought (or cost), whole dollars
    }

    internal sealed class FlareMyPost
    {
        public string Id;
        public int AgeMinutes;
        public int Views, Likes, Replies, Reposts;     // Replies is every reply there is: the ones its thread lists and the ones the list says are more
        public int Listed;                             // how many of the replies its thread lists, one row each (the rest are "N more replies")
        public int FunnelAt = -1;                      // the funnel the city measured for it, an index into the world's funnels, or -1
        public bool Flop;
    }

    /// <summary>
    /// An influencer of the city, once: the hire table's row, the card under it, the hire sheet, the side's offer to follow and their own profile all read this record, so Jade Rivers
    /// has one audience and one set of odds wherever she is shown.
    /// </summary>
    internal sealed class FlareInfluencerWorld
    {
        public string Name, Handle, Key, District;
        public int Niche, Followers;
        public int Engagement;                         // hundredths of a per cent
        public int Fit, PriceDollars, Visitors;        // the fit in per cent, a post's price, the visits one post is likely to bring
        public int Flop, Viral;                        // the odds in tenths of a per cent that a post falls flat or goes viral (Live: FlareExpect)
        public int TakingsDollars;                     // what the first post is likely to take
        public bool Verified, Booked;
        public string[] AudienceDistricts = new string[0];   // where their followers in the city live, most first
        public int[] AudienceCounts = new int[0];
        public int AudienceTotal;
    }

    internal sealed class FlareShopWorld
    {
        public string Name;
        public int[] Bars;                             // reviews of five stars down to one
        public int Visitors, Dollars;                  // what Flare brought it over the last 7 days
        public int CityFollowers;
        public int TurningNow, TurningBefore;          // the stars lately and before, thousandths, when the reviews have turned (0 when they have not)
        public int Reviews => Bars[0] + Bars[1] + Bars[2] + Bars[3] + Bars[4];
        public int StarsMilli => Reviews == 0 ? 0 : (Bars[0] * 5 + Bars[1] * 4 + Bars[2] * 3 + Bars[3] * 2 + Bars[4]) * 1000 / Reviews;
        public int Halves => Reviews == 0 ? 0 : (StarsMilli + 250) / 500;   // as FlareScreen.Business rounds it
    }

    internal sealed class FlareWorld
    {
        public bool NewPlayer;
        public long Followers;
        public int[] History = new int[0];             // followers over the week before today, the oldest first
        public int Following, PostsTotal;
        public int VisitorsBefore, DollarsBefore;      // what Flare brought the businesses the week before
        public FlareShopWorld[] Shops = new FlareShopWorld[0];
        public FlareMyPost[] Mine = new FlareMyPost[0];
        public FlareFunnelWorld[] Funnels = new FlareFunnelWorld[0];   // posts' and ads' as the city measured them
        public int[] AdFunnel = new int[0];                            // which funnels are ads (the first is the first shop's)
        public int OfferFee, OfferPosts;
        public FlareInfluencerWorld[] Influencers = new FlareInfluencerWorld[0];
        public string[] Districts = new string[0];     // the districts the followers live in, most first
        public int[] FollowersByDistrict = new int[0];
        public int FollowersElsewhere;                 // followers in districts the donut does not name
        /// <summary>The followers the city placed in a district, which is what the city page's donut adds up to (the account's other followers live outside the city).</summary>
        public int FollowersInCity { get { int n = FollowersElsewhere; for (int i = 0; i < FollowersByDistrict.Length; i++) { n += FollowersByDistrict[i]; } return n; } }
        public string[] PostingDistricts = new string[0];   // where the city posted today, most first
        public int[] PostingCounts = new int[0];

        public int Gain => History.Length > 0 ? (int)Followers - History[0] : 0;

        /// <summary>The people the player's posts of the last week reached: what the posts say they were viewed by.</summary>
        public long WeekReach
        {
            get
            {
                long sum = 0;
                for (int i = 0; i < Mine.Length; i++)
                {
                    sum += Mine[i].AgeMinutes < 7 * 1440 ? Mine[i].Views : 0;
                }
                return sum;
            }
        }

        /// <summary>What the posts of the week before reached.</summary>
        public long ReachBefore
        {
            get
            {
                long sum = 0;
                for (int i = 0; i < Mine.Length; i++)
                {
                    sum += Mine[i].AgeMinutes >= 7 * 1440 && Mine[i].AgeMinutes < 14 * 1440 ? Mine[i].Views : 0;
                }
                return sum;
            }
        }

        /// <summary>The account's reach by day for the last 14 days, the oldest first, as the posts of each day made it.</summary>
        public float[] ReachDays()
        {
            var days = new float[14];
            for (int i = 0; i < Mine.Length; i++)
            {
                int age = Mine[i].AgeMinutes / 1440;
                if (age < 14)
                {
                    days[13 - age] += Mine[i].Views;
                }
            }
            return days;
        }

        public int Visitors { get { int n = 0; for (int i = 0; i < Shops.Length; i++) { n += Shops[i].Visitors; } return n; } }

        public int Dollars { get { int n = 0; for (int i = 0; i < Shops.Length; i++) { n += Shops[i].Dollars; } return n; } }

        /// <summary>The rating of the businesses together and the reviews it stands on, as FlareScreen.Rating weighs them.</summary>
        public void Rating(out int milli, out int reviews)
        {
            long sum = 0;
            reviews = 0;
            for (int i = 0; i < Shops.Length; i++)
            {
                sum += (long)Shops[i].StarsMilli * Shops[i].Reviews;
                reviews += Shops[i].Reviews;
            }
            milli = reviews > 0 ? (int)(sum / reviews) : 0;
        }

        /// <summary>The facts the side's week and the studio's tiles are worded from.</summary>
        public FlareWeekFacts Week()
        {
            Rating(out int milli, out int reviews);
            return new FlareWeekFacts { Followers = Followers, Gain = Gain, Reach = WeekReach, ReachBefore = ReachBefore, StarsMilli = milli, Reviews = reviews };
        }

        /// <summary>The followers' line: the week kept and today's count at its end (FlareFeedRules.Line).</summary>
        public float[] Line()
        {
            var line = new float[History.Length + 1];
            for (int i = 0; i < History.Length; i++)
            {
                line[i] = History[i];
            }
            line[History.Length] = Followers;
            return line;
        }

        /// <summary>What the posts and ads did in the city over the week, as the city page adds them up: who saw, the visits brought and the takings.</summary>
        public void CityWeek(out int seen, out int visits, out int dollars)
        {
            seen = 0;
            for (int i = 0; i < Funnels.Length; i++)
            {
                seen += Funnels[i].Seen;
            }
            visits = Visitors;
            dollars = Dollars;
        }

        public bool IsAd(int funnel) => Array.IndexOf(AdFunnel, funnel) >= 0;

        public static FlareWorld Established()
        {
            var w = new FlareWorld
            {
                Followers = 2418,
                History = new[] { 2254, 2270, 2268, 2301, 2336, 2352, 2389 },
                Following = 86,
                PostsTotal = 63,
                VisitorsBefore = 140,
                DollarsBefore = 4832,
                OfferFee = 450,
                OfferPosts = 3
            };
            w.Shops = new[]
            {
                new FlareShopWorld { Name = FlareShop, Bars = new[] { 74, 31, 12, 7, 4 }, Visitors = 184, Dollars = 4310, CityFollowers = 840 },
                // 41 reviews that average 3.3: the 14 before at 4.1 and the 27 lately at 2.9 make it
                new FlareShopWorld { Name = FlareShop2, Bars = new[] { 12, 9, 6, 8, 6 }, Visitors = 17, Dollars = 402, CityFollowers = 260, TurningNow = 2900, TurningBefore = 4100 }
            };
            // The funnels the city measured: the thanks post, then the ads of the two shops. An ad that put people off came with a negative number.
            w.Funnels = new[]
            {
                new FlareFunnelWorld { Seen = 9400, Interested = 812, Followed = 34, Came = 46, Dollars = 1180 },
                new FlareFunnelWorld { Seen = 1200, Interested = 140, Followed = 6, Came = 27, Dollars = 640 },
                new FlareFunnelWorld { Seen = 700, Interested = 20, Followed = 0, Came = -12, Dollars = -140 }
            };
            w.AdFunnel = new[] { 1, 2 };
            w.Mine = new[]
            {
                new FlareMyPost { Id = "poll", AgeMinutes = 360, Views = 1840, Likes = 57, Replies = 11, Reposts = 3 },
                // the thread lists two of its fourteen replies, so the feed card and the thread card say 14 and the thread says "12 more"
                new FlareMyPost { Id = "thanks", AgeMinutes = 1440, Views = 9600, Likes = 212, Replies = 14, Listed = 2, Reposts = 9, FunnelAt = 0 },
                new FlareMyPost { Id = "late", AgeMinutes = 4 * 1440, Views = 480, Likes = 34, Replies = 2, Reposts = 0, Flop = true },
                // the week before this one: what it reached is the reach the week is set against
                new FlareMyPost { Id = "a", AgeMinutes = 8 * 1440, Views = 6200, Likes = 140, Replies = 6, Reposts = 4 },
                new FlareMyPost { Id = "b", AgeMinutes = 10 * 1440, Views = 2100, Likes = 61, Replies = 3, Reposts = 1 },
                new FlareMyPost { Id = "c", AgeMinutes = 12 * 1440, Views = 1460, Likes = 38, Replies = 2, Reposts = 0 }
            };
            w.Districts = new[] { "Murray Hill", "Midtown", "Garment District", "Chelsea" };
            // four in five of the 2,418 followers live in the city (FlareCityRules.PlayerCityTarget for a Local account): 1,934, the donut's whole
            w.FollowersByDistrict = new[] { 790, 560, 300, 130 };
            w.FollowersElsewhere = 154;
            w.Influencers = new[]
            {
                new FlareInfluencerWorld
                {
                    Name = "Jade Rivers", Handle = "@jaderivers", Key = "i:3", District = "Murray Hill", Niche = 0, Followers = 96000, Engagement = 410, Fit = 100, PriceDollars = 2400, Visitors = 38,
                    Flop = 120, Viral = 90, TakingsDollars = 2150, Verified = true,
                    AudienceDistricts = new[] { "Murray Hill", "Midtown", "Garment District", "Chelsea", "Chinatown" }, AudienceCounts = new[] { 340, 310, 140, 90, 60 }, AudienceTotal = 1000
                },
                new FlareInfluencerWorld { Name = "Marco Bell", Handle = "@marcobell", Key = "i:5", District = "Midtown", Niche = 3, Followers = 18200, Engagement = 630, Fit = 74, PriceDollars = 620, Visitors = 14,
                    Flop = 160, Viral = 50, TakingsDollars = 520, Booked = true },
                new FlareInfluencerWorld { Name = "Priya Nair", Handle = "@priyanair", Key = "i:8", District = "Garment District", Niche = 1, Followers = 9400, Engagement = 780, Fit = 58, PriceDollars = 340, Visitors = 6,
                    Flop = 200, Viral = 40, TakingsDollars = 180 },
                new FlareInfluencerWorld { Name = "Sam Okafor", Handle = "@samokafor", Key = "i:11", District = "Murray Hill", Niche = 2, Followers = 3100, Engagement = 920, Fit = 58, PriceDollars = 150, Visitors = 5,
                    Flop = 230, Viral = 30, TakingsDollars = 90 },
                new FlareInfluencerWorld { Name = "Tess Whitaker", Handle = "@tesswhitaker", Key = "i:14", District = "Midtown", Niche = 0, Followers = 27500, Engagement = 490, Fit = 52, PriceDollars = 540, Visitors = 9,
                    Flop = 190, Viral = 60, TakingsDollars = 300 },
                new FlareInfluencerWorld { Name = "Omar Haddad", Handle = "@omarhaddad", Key = "i:17", District = "Murray Hill", Niche = 2, Followers = 19800, Engagement = 520, Fit = 47, PriceDollars = 410, Visitors = 7,
                    Flop = 210, Viral = 40, TakingsDollars = 220 }
            };
            w.PostingDistricts = new[] { "Midtown", "Garment District", "Murray Hill" };
            w.PostingCounts = new[] { 3940, 2620, 2610 };
            return w;
        }

        /// <summary>A first day: no followers, nothing posted, no review, and so nothing to count anywhere. Its city has the same news as anyone's.</summary>
        public static FlareWorld New()
        {
            return new FlareWorld
            {
                NewPlayer = true,
                Followers = 0,
                History = new[] { 0, 0, 0, 0, 0, 0, 0 },
                Following = 0,
                PostsTotal = 0,
                Influencers = Known.Influencers,   // the city's influencers are the city's, whoever plays
                OfferFee = 450,
                OfferPosts = 3,
                PostingDistricts = new[] { "Midtown", "Garment District", "Murray Hill" },
                PostingCounts = new[] { 3940, 2620, 2610 }
            };
        }

        /// <summary>An influencer of the city by their account key.</summary>
        public FlareInfluencerWorld Influencer(string key)
        {
            for (int i = 0; i < Influencers.Length; i++)
            {
                if (Influencers[i].Key == key)
                {
                    return Influencers[i];
                }
            }
            return null;
        }

        public static readonly FlareWorld Known = Established();

        /// <summary>The world a state is drawn from.</summary>
        public static FlareWorld Of(string state) => state == "city_new" || state == "city_empty" ? New() : Known;
    }
}
