using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>One of the player's businesses as an influencer sees it: where it is, what it is, its name.</summary>
    public struct FlareShop
    {
        public string Place;
        public string Name;
        public FlareNiche Niche;
        public string District;
        public int OpenedDay;        // when Flare first saw it (a launch is for a business in its first fortnight)
    }

    /// <summary>Something that happened to the cast this month or today, for the log, the notification and the paper.</summary>
    public struct FlareCastNews
    {
        public enum What { Arrived, Grew, Star, Quit, Scandal }

        public What Kind;
        public int Id;
        public int Before, After;    // followers
    }

    /// <summary>A deal the player can make with influencers, each the company's campaign of its kind (CampaignKind 5 to 10).</summary>
    public enum FlareDeal
    {
        Post = 0,        // one post
        Series = 1,      // a week of posts
        Ambassador = 2,  // four weeks, two posts a week
        Plan = 3,        // two or three influencers for a week
        Launch = 4,      // a business in its first fortnight
        Giveaway = 5     // a giveaway: it costs stock, and brings a crowd
    }

    /// <summary>
    /// The city's influencers by rule: the cast made from a seed on the first day (and in a save that first gets Flare),
    /// a month of growing, fading, quitting, stars and scandals, newcomers drawn in by what the city does (a busy niche,
    /// the night-life policy, a viral moment, the player's friends and staff), their daily posts about real places and
    /// events, what hiring one costs and brings, how a hire goes (late, off message, a backfire), and following an
    /// account. Game-free: whoever calls says what day it is and what the city is like; EconomyCheck runs these rules.
    ///
    /// A WORKED EXAMPLE (hiring one for a business in their niche and district):
    ///
    ///   followers  engagement  reliability   engaged   price a post   reach (thousandths of radio's customers a day)
    ///      10,000        400bp           80       400            250   400 / 800 x 1.56 fit x 0.9 reliable x 1.1 name = 772
    ///      60,000        250bp           50     1,500            800   capped at 1,500
    ///
    /// The price is $50 and 50 cents for each engaged follower, at the start's prices, so that a post's two days cost
    /// about what radio does for each customer. A week's series is five posts, an ambassador eight over four weeks at a
    /// quarter off (its customers are half radio's a day, for longer).
    /// </summary>
    public static class FlareCast
    {
        public const int FirstDealKind = 5;        // the company's campaign kind of FlareDeal.Post; the deals follow in their order (CampaignKind 5 to 10)
        public const int Start = 6;                // influencers on the first day
        public const int Most = 10;                // active at once, at most
        public const int MonthDays = 30;
        public const int StarFrom = 100000;        // followers
        public const int FadeUnder = 1500, QuitUnder = 600;
        public const int EngagedPerRadio = 800;    // engaged followers whose posts bring what a day of radio does
        public const int ReachLeast = 150, ReachMost = 1500;
        public const int PostChance = 700;         // per mille a day an influencer posts
        public const int ScandalLoss = 20;         // per cent of the followers a scandal costs
        public const int ScandalReputation = 40;
        public const int TalkVisit = 1;            // per cent of customers an unpaid visit brings for three days
        public const int TalkBackfire = -3;        // ...and a hire that backfires, or a hired influencer's scandal
        public const int FriendChance = 100, StaffChance = 60;   // per mille a month: a friend or one of the staff turns influencer
        public const int ViralDraw = 300;          // per mille more for a newcomer in the niche of a viral post of the player's, for a month

        // ---- what the city's news had today, as bits (local news posts about it)
        public const int NewsAccident = 1, NewsCrime = 2, NewsFire = 4, NewsArrest = 8, NewsBreakIn = 16;

        private static readonly int[] ScandalChance = { 20, 40, 120, 10 };   // per mille a month, by FlareStyle
        private static readonly int[] DealPosts = { 1, 5, 8, 2, 3, 2 };      // posts each influencer makes, by FlareDeal
        private static readonly int[] DealShare = { 1000, 1000, 750, 1000, 1000, 1000 };   // thousandths of the price: an ambassador gets a quarter off

        private static readonly string[][] HandleHeads =
        {
            new[] { "bites", "plates", "eats", "crumbs" },
            new[] { "threads", "style", "looks", "fits" },
            new[] { "reps", "fit", "strides", "sweat" },
            new[] { "afterdark", "nights", "latenight", "neon" },
            new[] { "bytes", "gadgets", "circuits", "pixels" },
            new[] { "dispatch", "corner", "beat", "notes" },
            new[] { "hustle", "ledger", "deals", "grind" }
        };
        private static readonly string[] HandleFronts = { "city", "uptown", "metro", "daily", "the", "real", "block", "street" };

        // ---------------------------------------------------------------- what a business is, as a niche

        /// <summary>The niche of a business by the game's own type name: food, fashion, fitness, night life, tech, or business.</summary>
        public static FlareNiche NicheOf(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return FlareNiche.Business;
            }
            if (Has(type, "coffeeshop", "fastfood", "fruitandvegetable", "supermarket"))
            {
                return FlareNiche.Food;
            }
            if (Has(type, "clothing", "jewelry", "hairdresser", "giftshop", "florist"))
            {
                return FlareNiche.Fashion;
            }
            if (Has(type, "gym"))
            {
                return FlareNiche.Fitness;
            }
            if (Has(type, "nightclub", "casino", "liquor", "cinema", "theater"))
            {
                return FlareNiche.Nightlife;
            }
            return Has(type, "electronics", "appliance", "webdevelopment", "graphicdesign") ? FlareNiche.Tech : FlareNiche.Business;
        }

        private static bool Has(string type, params string[] parts)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0 && type.IndexOf(parts[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- the cast

        /// <summary>The first day's cast, one in each niche but business, if there is none yet. The same seed gives the same cast.</summary>
        public static void Make(FlareFile f, int day, IReadOnlyList<string> districts)
        {
            if (f.Influencers.Count > 0)
            {
                return;
            }
            for (int i = 0; i < Start; i++)
            {
                int roll = FlareRules.Roll(f.CastSeed, i, 1);
                int followers = 2000 + (int)(58000.0 * roll / 1000.0 * roll / 1000.0);
                New(f, (FlareNiche)(i % 6), District(districts, f.CastSeed, i), day, followers, -1, false);
            }
        }

        private static string District(IReadOnlyList<string> districts, int seed, int i)
        {
            return districts != null && districts.Count > 0 ? districts[FlareRules.Roll(seed, i, 2) % districts.Count] : null;
        }

        /// <summary>A new influencer, from the cast's seed and their number.</summary>
        public static FlareInfluencer New(FlareFile f, FlareNiche niche, string district, int day, int followers, int person, bool staff)
        {
            int id = f.NextId++;
            int seed = FlareRules.Roll(f.CastSeed, id, 3) * 1000 + FlareRules.Roll(f.CastSeed, id, 4);
            int style = FlareRules.Roll(seed, 5, 6);
            string[] heads = HandleHeads[FlareRules.Clamp((int)niche, 0, HandleHeads.Length - 1)];
            var inf = new FlareInfluencer
            {
                Id = id, Niche = niche, District = district, Followers = Math.Max(100, followers), StartDay = day, Person = person, Seed = seed, Staff = staff,
                Engagement = 150 + FlareRules.Roll(seed, 7, 8) * 55 / 100,
                Reliability = 40 + FlareRules.Roll(seed, 9, 10) * 55 / 1000,
                Style = style < 350 ? FlareStyle.Honest : style < 650 ? FlareStyle.Wholesome : style < 850 ? FlareStyle.Flaky : FlareStyle.Edgy,
                Handle = "@" + HandleFronts[FlareRules.Roll(seed, 11, 12) % HandleFronts.Length] + heads[FlareRules.Roll(seed, 13, 14) % heads.Length] + (id % 100).ToString("00")
            };
            if (inf.Style == FlareStyle.Flaky)
            {
                inf.Reliability = Math.Min(inf.Reliability, 60);
            }
            f.Influencers.Add(inf);
            return inf;
        }

        /// <summary>
        /// The influencers' month: each grows or fades by their engagement, manner and name, some become stars, some quit,
        /// some are caught in a scandal; and newcomers come into a niche the city makes busy ('heat', thousandths by
        /// FlareNiche), the niche of a viral post of the player's, from among the player's friends, or from their staff.
        /// Played once a month whatever the time skipped. What happened goes into the list given.
        /// </summary>
        public static void Month(FlareFile f, int day, int[] heat, IReadOnlyList<FlarePerson> people, bool staff, IReadOnlyList<string> districts, List<FlareCastNews> into)
        {
            f.MonthDay = day;
            for (int i = 0; i < f.Influencers.Count; i++)
            {
                FlareInfluencer inf = f.Influencers[i];
                if (!inf.Active)
                {
                    continue;
                }
                int before = inf.Followers;
                int luck = FlareRules.Roll(inf.Seed, day, 21);
                int growth = (inf.Engagement - 350) / 2 + (inf.Reputation - 50) * 4 + (luck - 430);   // basis points a month
                growth += inf.Style == FlareStyle.Edgy ? (FlareRules.Roll(inf.Seed, day, 22) - 500) * 2 : inf.Style == FlareStyle.Wholesome ? 50 : 0;
                inf.Followers = Math.Max(0, (int)(inf.Followers + (long)inf.Followers * growth / 10000));
                inf.Reputation = FlareRules.Clamp(inf.Reputation + Math.Sign(60 - inf.Reputation) * 5, 0, 100);
                if (FlareRules.Roll(inf.Seed, day, 23) < ScandalChance[FlareRules.Clamp((int)inf.Style, 0, ScandalChance.Length - 1)])
                {
                    Scandal(inf, day);
                    into.Add(new FlareCastNews { Kind = FlareCastNews.What.Scandal, Id = inf.Id, Before = before, After = inf.Followers });
                }
                if (!inf.Star && inf.Followers >= StarFrom)
                {
                    inf.Star = true;
                    into.Add(new FlareCastNews { Kind = FlareCastNews.What.Star, Id = inf.Id, Before = before, After = inf.Followers });
                }
                else if (inf.Followers < QuitUnder || (inf.Followers < FadeUnder && FlareRules.Roll(inf.Seed, day, 24) < 300))
                {
                    inf.Quit = true;
                    into.Add(new FlareCastNews { Kind = FlareCastNews.What.Quit, Id = inf.Id, Before = before, After = inf.Followers });
                }
                else
                {
                    into.Add(new FlareCastNews { Kind = FlareCastNews.What.Grew, Id = inf.Id, Before = before, After = inf.Followers });
                }
            }
            Arrivals(f, day, heat, people, staff, districts, into);
        }

        /// <summary>An influencer's scandal: their reputation crashes and a fifth of the followers go.</summary>
        public static void Scandal(FlareInfluencer inf, int day)
        {
            inf.ScandalDay = day;
            inf.Reputation = FlareRules.Clamp(inf.Reputation - ScandalReputation, 0, 100);
            inf.Followers -= inf.Followers * ScandalLoss / 100;
        }

        // Newcomers, while there is room: the busiest niche, a friend, one of the staff.
        private static void Arrivals(FlareFile f, int day, int[] heat, IReadOnlyList<FlarePerson> people, bool staff, IReadOnlyList<string> districts, List<FlareCastNews> into)
        {
            if (f.ActiveInfluencers >= Most)
            {
                return;
            }
            int best = -1, bestHeat = 0;
            for (int n = 0; heat != null && n < heat.Length && n <= (int)FlareNiche.Business; n++)
            {
                int h = heat[n] + (n == f.ViralNiche && day - f.ViralNicheDay <= MonthDays ? ViralDraw : 0);
                if (h > bestHeat)
                {
                    best = n;
                    bestHeat = h;
                }
            }
            if (best >= 0 && FlareRules.Roll(f.CastSeed, day, 31) < bestHeat / 2)
            {
                FlareInfluencer inf = New(f, (FlareNiche)best, District(districts, f.CastSeed, day), day, 800 + FlareRules.Roll(f.CastSeed, day, 32) * 4, -1, false);
                into.Add(new FlareCastNews { Kind = FlareCastNews.What.Arrived, Id = inf.Id, After = inf.Followers });
            }
            for (int i = 0; people != null && i < people.Count && f.ActiveInfluencers < Most; i++)
            {
                FlarePerson p = people[i];
                FlareNiche niche;
                if (p.Stage < 2 || IsOne(f, p.Index) || !NicheOf(p.Taste, out niche) || FlareRules.Roll(f.CastSeed, day, 40 + p.Index) >= FriendChance)
                {
                    continue;
                }
                FlareInfluencer inf = New(f, niche, District(districts, f.CastSeed, day + p.Index), day, 500 + p.Closeness * 10, p.Index, false);
                into.Add(new FlareCastNews { Kind = FlareCastNews.What.Arrived, Id = inf.Id, After = inf.Followers });
                break;   // one friend a month at most
            }
            if (staff && f.ActiveInfluencers < Most && FlareRules.Roll(f.CastSeed, day, 50) < StaffChance)
            {
                FlareInfluencer inf = New(f, FlareNiche.Business, District(districts, f.CastSeed, day + 7), day, 400 + FlareRules.Roll(f.CastSeed, day, 51), -1, true);
                into.Add(new FlareCastNews { Kind = FlareCastNews.What.Arrived, Id = inf.Id, After = inf.Followers });
            }
        }

        private static bool NicheOf(FlareTaste taste, out FlareNiche niche)
        {
            niche = (taste & FlareTaste.Sport) != 0 ? FlareNiche.Fitness : (taste & FlareTaste.Food) != 0 ? FlareNiche.Food : (taste & FlareTaste.Night) != 0 ? FlareNiche.Nightlife
                : (taste & FlareTaste.Shops) != 0 ? FlareNiche.Fashion : FlareNiche.Business;
            return niche != FlareNiche.Business;
        }

        /// <summary>True if a person of the social life is one of the influencers already.</summary>
        public static bool IsOne(FlareFile f, int person)
        {
            for (int i = 0; i < f.Influencers.Count; i++)
            {
                if (f.Influencers[i].Person == person && person >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- hiring

        /// <summary>What a post of theirs costs, at the start's prices: $50 and 50 cents an engaged follower, in tens.</summary>
        public static int Price(FlareInfluencer inf)
        {
            return (50 + inf.Engaged / 2) / 10 * 10;
        }

        /// <summary>What a deal with them costs, at the start's prices.</summary>
        public static int Fee(FlareInfluencer inf, FlareDeal deal)
        {
            int d = FlareRules.Clamp((int)deal, 0, DealPosts.Length - 1);
            return (int)((long)Price(inf) * DealPosts[d] * DealShare[d] / 1000);
        }

        /// <summary>How well an influencer suits a business, thousandths: their niche (business suits everything a little) and their part of town.</summary>
        public static int Fit(FlareInfluencer inf, FlareNiche niche, string district)
        {
            int n = inf.Niche == niche ? 1300 : inf.Niche == FlareNiche.Business || inf.Niche == FlareNiche.LocalNews ? 1000 : 700;
            int d = district != null && inf.District == district ? 1200 : 900;
            return n * d / 1000;
        }

        /// <summary>What a post of theirs brings a business, thousandths of radio's customers a day: engaged followers, fit, reliability and name.</summary>
        public static int Reach(FlareInfluencer inf, FlareNiche niche, string district)
        {
            double reach = inf.Engaged * 1000.0 / EngagedPerRadio * Fit(inf, niche, district) / 1000.0 * (500 + 5 * inf.Reliability) / 1000.0 * (500 + 10 * inf.Reputation) / 1000.0;
            return FlareRules.Clamp((int)Math.Round(reach), ReachLeast, ReachMost);
        }

        /// <summary>The two or three best for a business, for a campaign plan: active, not in a scandal this month, best fit first.</summary>
        public static int PlanPick(FlareFile f, FlareNiche niche, string district, int day, List<int> into)
        {
            into.Clear();
            for (int k = 0; k < 3; k++)
            {
                FlareInfluencer best = null;
                for (int i = 0; i < f.Influencers.Count; i++)
                {
                    FlareInfluencer inf = f.Influencers[i];
                    if (inf.Active && day - inf.ScandalDay > MonthDays && !into.Contains(inf.Id) && (best == null || Reach(inf, niche, district) > Reach(best, niche, district)))
                    {
                        best = inf;
                    }
                }
                if (best == null)
                {
                    break;
                }
                into.Add(best.Id);
            }
            return into.Count >= 2 ? into.Count : 0;
        }

        /// <summary>How a hire goes, rolled once when it is booked: a flaky or unreliable one posts a day late, a flaky one may go off message, an edgy one may backfire.</summary>
        public static void Outcome(FlareHire hire, FlareInfluencer inf, int seed)
        {
            int late = (100 - inf.Reliability) * 6 * (inf.Style == FlareStyle.Flaky ? 2 : 1);
            hire.Late = FlareRules.Roll(seed, hire.Id, 61) < late;
            hire.OffMessage = inf.Style == FlareStyle.Flaky && FlareRules.Roll(seed, hire.Id, 62) < 250;
            hire.Backfired = inf.Style == FlareStyle.Edgy && FlareRules.Roll(seed, hire.Id, 63) < 200;
        }

        // ---------------------------------------------------------------- the day's posts

        /// <summary>
        /// The influencers', brands' and candidates' posts of the day: a hired one about the business it was paid for; one
        /// paid by a rival or a chain; one for a candidate in an election; a visit to a business of the player's in their
        /// niche and district (unpaid: returned in 'visits' for the talk it brings); local news about what happened; their
        /// niche otherwise; an apology on the day of a scandal. The brands and the candidates post their own.
        /// </summary>
        public static void Day(FlareFile f, int day, IReadOnlyList<FlareShop> shops, IReadOnlyList<string> rivals, IReadOnlyList<string> candidates, IReadOnlyList<string> brands,
            List<FlarePost> visits)
        {
            if (f.NewsDay != day - 1 && f.NewsDay != day)
            {
                f.NewsToday = 0;   // (yesterday's news is today's to talk about)
            }
            for (int i = 0; i < f.Influencers.Count; i++)
            {
                FlareInfluencer inf = f.Influencers[i];
                FlareHire hire = HireOf(f, inf.Id, day);
                if (!inf.Active && hire == null)
                {
                    continue;   // (one who has stopped posting still makes the posts of a deal already paid for)
                }
                if (inf.ScandalDay == day)
                {
                    Add(f, inf, day, FlareSource.Apology, null, null, 0);
                    continue;
                }
                if (hire != null)
                {
                    if (!(hire.Late && day == hire.From))
                    {
                        Add(f, inf, day, FlareSource.Hired, hire.Place, hire.Name, hire.OffMessage ? 1 : 0);
                        hire.Posted++;
                        hire.LastPost = day;
                    }
                    continue;
                }
                int roll = FlareRules.Roll(inf.Seed, day, 70);
                if (roll >= PostChance)
                {
                    continue;
                }
                int what = FlareRules.Roll(inf.Seed, day, 71);
                if (rivals != null && rivals.Count > 0 && what < 120)
                {
                    Add(f, inf, day, FlareSource.Rival, null, rivals[FlareRules.Roll(inf.Seed, day, 72) % rivals.Count], 0);
                    continue;
                }
                if (candidates != null && candidates.Count > 0 && what < 320)
                {
                    Add(f, inf, day, FlareSource.Election, null, candidates[inf.Seed % candidates.Count], 0);   // each backs the same candidate all through
                    continue;
                }
                int visit = Visited(inf, shops, day);
                if (visit >= 0 && what < 470)
                {
                    FlarePost p = Add(f, inf, day, FlareSource.Visit, shops[visit].Place, shops[visit].Name, 0);
                    visits?.Add(p);
                    continue;
                }
                if (inf.Niche == FlareNiche.LocalNews && f.NewsToday != 0)
                {
                    Add(f, inf, day, FlareSource.CityNews, null, null, f.NewsToday);
                    continue;
                }
                Add(f, inf, day, FlareSource.Niche, null, null, FlareRules.Roll(inf.Seed, day, 73) % 2);
            }
            for (int i = 0; brands != null && i < brands.Count; i++)
            {
                if (FlareRules.Roll(f.CastSeed, day, 80 + i) < 250)
                {
                    Account(f, FlareAuthor.Brand, brands[i], day, FlareSource.BrandNews);
                }
            }
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                if (FlareRules.Roll(f.CastSeed, day, 90 + i) < 400)
                {
                    Account(f, FlareAuthor.Candidate, candidates[i], day, FlareSource.Election);
                }
            }
            FlareRules.Prune(f);
        }

        /// <summary>
        /// The posts about a business, for its card on the Business tab: the player's own still on the account, reviews apart (<paramref name="own"/>)
        /// and the paid posts of every deal there that Flare still has on record. <paramref name="last"/> is the day of the
        /// newest, -1 with none. One walk over the posts and the deals.
        /// </summary>
        public static int PostsAbout(FlareFile f, string place, out int own, out int last)
        {
            own = 0;
            last = -1;
            int paid = 0;
            for (int i = 0; i < f.Posts.Count; i++)
            {
                FlarePost p = f.Posts[i];
                if (p.Author == FlareAuthor.Player && p.Place == place && p.Source != FlareSource.Review)
                {
                    own++;
                    last = Math.Max(last, p.Day);
                }
            }
            for (int i = 0; i < f.Hires.Count; i++)
            {
                FlareHire h = f.Hires[i];
                if (h.Place == place && h.Posted > 0)
                {
                    paid += h.Posted;
                    last = Math.Max(last, h.LastPost);
                }
            }
            return own + paid;
        }

        public const int HireBefore = 0, HireWaiting = 1, HirePosting = 2, HireOver = 3;

        /// <summary>Where a deal stands for its page: before its first day, on but with no post yet (a late first one), posting, or over.</summary>
        public static int HireStage(FlareHire h, int day)
        {
            return h.Ended || day > h.To ? HireOver : day < h.From ? HireBefore : h.Posted > 0 ? HirePosting : HireWaiting;
        }

        private static FlareHire HireOf(FlareFile f, int id, int day)
        {
            for (int i = 0; i < f.Hires.Count; i++)
            {
                if (f.Hires[i].On(day) && f.Hires[i].Who.Contains(id))
                {
                    return f.Hires[i];
                }
            }
            return null;
        }

        // A business of the player's an influencer would visit: in their district and their niche (or business, or local news, anywhere in their district).
        private static int Visited(FlareInfluencer inf, IReadOnlyList<FlareShop> shops, int day)
        {
            for (int i = 0; shops != null && i < shops.Count; i++)
            {
                int k = (i + FlareRules.Roll(inf.Seed, day, 74)) % shops.Count;
                if (shops[k].District == inf.District && (shops[k].Niche == inf.Niche || inf.Niche == FlareNiche.LocalNews))
                {
                    return k;
                }
            }
            return -1;
        }

        private static FlarePost Add(FlareFile f, FlareInfluencer inf, int day, FlareSource source, string place, string name, int arg)
        {
            int id = f.NextId++;
            int reach = Math.Max(1, inf.Followers * 300 / 1000);
            var p = new FlarePost
            {
                Id = id, Day = day, Hour = 8 + FlareRules.Roll(inf.Seed, day, 75) % 14, Author = FlareAuthor.Influencer, Who = inf.Id, Source = source, Place = place, Name = name, Arg = arg,
                Seed = FlareRules.Roll(f.CastSeed, id, 17) * 1000 + FlareRules.Roll(f.CastSeed, id, 29), Reach = reach, Likes = inf.Engaged, Replies = inf.Engaged / 20
            };
            f.Posts.Add(p);
            return p;
        }

        private static void Account(FlareFile f, FlareAuthor author, string name, int day, FlareSource source)
        {
            int id = f.NextId++;
            f.Posts.Add(new FlarePost
            {
                Id = id, Day = day, Hour = 10 + FlareRules.Roll(f.CastSeed, id, 76) % 10, Author = author, Source = source, Name = name,
                Seed = FlareRules.Roll(f.CastSeed, id, 17) * 1000 + FlareRules.Roll(f.CastSeed, id, 29), Reach = 2000 + FlareRules.Roll(f.CastSeed, id, 77) * 5,
                Likes = 60 + FlareRules.Roll(f.CastSeed, id, 78) / 4, Replies = FlareRules.Roll(f.CastSeed, id, 79) / 50
            });
        }

        // ---------------------------------------------------------------- following

        /// <summary>The account a post is from, as the player follows it ("i:3", "b:name", "c:name"); null for the player's own and the friends'.</summary>
        public static string KeyOf(FlarePost p)
        {
            switch (p.Author)
            {
                case FlareAuthor.Influencer: return "i:" + p.Who;
                case FlareAuthor.Brand: return "b:" + p.Name;
                case FlareAuthor.Candidate: return "c:" + p.Name;
                case FlareAuthor.Company: return "m:" + p.Name;
                case FlareAuthor.Ledger: return "l:ledger";
                default: return null;
            }
        }

        public static bool Followed(FlareFile f, string key)
        {
            return key != null && f.Follows.Contains(key);
        }

        /// <summary>The player follows an account, or stops. True if anything changed.</summary>
        public static bool Follow(FlareFile f, string key, bool on)
        {
            if (string.IsNullOrEmpty(key) || Followed(f, key) == on)
            {
                return false;
            }
            if (on)
            {
                f.Follows.Add(key);
            }
            else
            {
                f.Follows.Remove(key);
            }
            return true;
        }

        /// <summary>The posts of a day from accounts the player follows.</summary>
        public static int FollowedPosts(FlareFile f, int day)
        {
            int n = 0;
            for (int i = 0; i < f.Posts.Count; i++)
            {
                n += f.Posts[i].Day == day && Followed(f, KeyOf(f.Posts[i])) ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// Whether the one notification of a day for the accounts the player follows may be sent: only with the option on,
        /// only if one of them posted, and once a day at most, however many posted. True takes the day's one.
        /// </summary>
        public static bool FollowNotice(FlareFile f, int day, int posts, bool allowed)
        {
            if (!allowed || posts <= 0 || f.FollowNoticeDay == day)
            {
                return false;
            }
            f.FollowNoticeDay = day;
            return true;
        }
    }
}
