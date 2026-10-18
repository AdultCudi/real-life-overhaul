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
    /// The city's influencers by rule: the cast made from a seed, a month of growing, fading, quitting, stars, scandals and
    /// newcomers, their daily posts about real places, what hiring one costs and brings, and following an account.
    /// Game-free: the caller gives the day and what the city is like.
    ///
    /// Hiring one for a business in their niche and district:
    ///
    ///   followers  engagement  reliability   engaged   price a post   reach (thousandths of radio's customers a day)
    ///      10,000        400bp           80       400            250   400 / 800 x 1.56 fit x 0.9 reliable x 1.1 name = 772
    ///      60,000        250bp           50     1,500            800   capped at 1,500
    ///
    /// The price is $50 and 50 cents an engaged follower at the start's prices, so a post's two days cost about what radio
    /// does for each customer.
    /// </summary>
    public static class FlareCast
    {
        public const int FirstDealKind = 5;        // the company's campaign kind of FlareDeal.Post; the other deals follow in order
        public const int Start = 16;               // influencers on the first day, a little under the level
        public const int CitizensEach = 22;        // the city's people for each active influencer the city settles around
        public const int Level = FlareLiveRules.Citizens / CitizensEach;   // 18 for four hundred: newcomers and fades pull the cast towards it
        public const int ArrivalTries = 3;         // the city's own newcomers a month, at most
        public const int ArrivalBase = 60;         // per mille each, at the level...
        public const int ArrivalPull = 600;        // ...and so many more for a cast with none, in proportion
        public const int CrowdBp = 150;            // basis points of growth a month each active one loses for every one over the level
        public const int PostsLevel = 12;          // active influencers whose posts fill the feed; more share it, so the feed's volume stays
        public const int ActiveSoft = 60;          // a safety bound, never reached in play: no newcomer at this many active
        public const int KeptMost = 240;           // records kept, active or not: one who stopped and is named by nothing goes first
        public const int MonthDays = 30;
        public const int StarFrom = 100000;        // followers
        public const int FadeUnder = 1500, QuitUnder = 600;   // followers
        public const int EngagedPerRadio = 800;    // engaged followers whose posts bring what a day of radio does
        public const int ReachLeast = 150, ReachMost = 1500;   // thousandths of radio's customers a day
        public const int PostChance = 700;         // per mille a day an influencer posts
        public const int ScandalLoss = 20;         // per cent of the followers a scandal costs
        public const int ScandalReputation = 40;   // points
        public const int TalkVisit = 1;            // per cent of customers an unpaid visit brings for three days
        public const int TalkBackfire = -3;        // ...and a hire that backfires, or a hired influencer's scandal
        public const int FriendChance = 100, StaffChance = 60;   // per mille a month: a friend or one of the staff turns influencer
        public const int ViralDraw = 300;          // per mille more for a newcomer in the niche of a viral post of the player's, for a month

        // What the city's news had today, as bits: local news posts about it.
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
        /// The influencers' month: each grows or fades, some become stars, quit or are caught in a scandal, and newcomers come
        /// into a niche the city makes busy (<paramref name="heat"/>, thousandths by FlareNiche), from the player's friends or
        /// staff. Over <see cref="Level"/> the audience is shared, so the cast grows and shrinks round it. Once a month: a few
        /// walks over the cast and the posts.
        /// </summary>
        public static void Month(FlareFile f, int day, int[] heat, IReadOnlyList<FlarePerson> people, bool staff, IReadOnlyList<string> districts, List<FlareCastNews> into)
        {
            f.MonthDay = day;
            Retire(f);
            int crowd = CrowdBp * Math.Max(0, f.ActiveInfluencers - Level);   // basis points every active one loses this month
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
                growth -= crowd;
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

        // Newcomers: the city's own (more while the cast is small), the busiest niche, a friend, one of the staff.
        private static void Arrivals(FlareFile f, int day, int[] heat, IReadOnlyList<FlarePerson> people, bool staff, IReadOnlyList<string> districts, List<FlareCastNews> into)
        {
            int active = f.ActiveInfluencers;
            if (active >= ActiveSoft)
            {
                return;
            }
            int chance = ArrivalChance(active);
            for (int k = 0; k < ArrivalTries && active < ActiveSoft; k++)
            {
                int roll = day * ArrivalTries + k;
                if (FlareRules.Roll(f.CastSeed, roll, 33) >= chance)
                {
                    continue;
                }
                var niche = (FlareNiche)(FlareRules.Roll(f.CastSeed, roll, 34) % 6);   // any niche but business, as on the first day
                FlareInfluencer arrived = New(f, niche, District(districts, f.CastSeed, roll + 1000), day, 1000 + FlareRules.Roll(f.CastSeed, roll, 35) * 6, -1, false);
                into.Add(new FlareCastNews { Kind = FlareCastNews.What.Arrived, Id = arrived.Id, After = arrived.Followers });
                active++;
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
            if (best >= 0 && active < ActiveSoft && FlareRules.Roll(f.CastSeed, day, 31) < bestHeat / 2)
            {
                FlareInfluencer inf = New(f, (FlareNiche)best, District(districts, f.CastSeed, day), day, 800 + FlareRules.Roll(f.CastSeed, day, 32) * 4, -1, false);
                into.Add(new FlareCastNews { Kind = FlareCastNews.What.Arrived, Id = inf.Id, After = inf.Followers });
            }
            for (int i = 0; people != null && i < people.Count && f.ActiveInfluencers < ActiveSoft; i++)
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
            if (staff && f.ActiveInfluencers < ActiveSoft && FlareRules.Roll(f.CastSeed, day, 50) < StaffChance)
            {
                FlareInfluencer inf = New(f, FlareNiche.Business, District(districts, f.CastSeed, day + 7), day, 400 + FlareRules.Roll(f.CastSeed, day, 51), -1, true);
                into.Add(new FlareCastNews { Kind = FlareCastNews.What.Arrived, Id = inf.Id, After = inf.Followers });
            }
        }

        /// <summary>The chance a month of each of the city's own newcomers, per mille: more under the level, none well over it.</summary>
        public static int ArrivalChance(int active)
        {
            return FlareRules.Clamp(ArrivalBase + ArrivalPull * (Level - active) / Level, 0, ArrivalBase + ArrivalPull);
        }

        /// <summary>
        /// Those who stopped posting and are named by nothing (no post, deal, message or follow; a friend who took it up stays)
        /// go; past <see cref="KeptMost"/> the oldest who stopped go whatever names them. How many went.
        /// </summary>
        public static int Retire(FlareFile f)
        {
            int gone = 0;
            for (int i = f.Influencers.Count - 1; i >= 0; i--)
            {
                FlareInfluencer inf = f.Influencers[i];
                if (inf.Active || inf.Person >= 0 || Named(f, inf.Id))
                {
                    continue;
                }
                f.Influencers.RemoveAt(i);
                gone++;
            }
            for (int i = 0; i < f.Influencers.Count && f.Influencers.Count > KeptMost;)
            {
                if (f.Influencers[i].Active)
                {
                    i++;
                    continue;
                }
                f.Influencers.RemoveAt(i);
                gone++;
            }
            return gone;
        }

        // Whether anything kept names an influencer: a post of theirs, a deal, a message, a follow.
        private static bool Named(FlareFile f, int id)
        {
            for (int i = 0; i < f.Posts.Count; i++)
            {
                if (f.Posts[i].Author == FlareAuthor.Influencer && f.Posts[i].Who == id)
                {
                    return true;
                }
            }
            for (int i = 0; i < f.Hires.Count; i++)
            {
                if (f.Hires[i].Who.Contains(id))
                {
                    return true;
                }
            }
            string key = "i:" + id;
            if (f.Follows.Contains(key))
            {
                return true;
            }
            for (int i = 0; i < f.Net.Dms.Count; i++)
            {
                if (string.Equals(f.Net.Dms[i].With, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
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

        /// <summary>How well an influencer suits a business, thousandths: their niche (business suits all a little) and district.</summary>
        public static int Fit(FlareInfluencer inf, FlareNiche niche, string district)
        {
            int n = inf.Niche == niche ? 1300 : inf.Niche == FlareNiche.Business || inf.Niche == FlareNiche.LocalNews ? 1000 : 700;
            int d = district != null && inf.District == district ? 1200 : 900;
            return n * d / 1000;
        }

        /// <summary>What a post of theirs brings a business, thousandths of radio's customers a day.</summary>
        public static int Reach(FlareInfluencer inf, FlareNiche niche, string district)
        {
            double reach = inf.Engaged * 1000.0 / EngagedPerRadio * Fit(inf, niche, district) / 1000.0 * (500 + 5 * inf.Reliability) / 1000.0 * (500 + 10 * inf.Reputation) / 1000.0;
            return FlareRules.Clamp((int)Math.Round(reach), ReachLeast, ReachMost);
        }

        /// <summary>The two or three best for a campaign plan, best fit first: active and not in a scandal this month. 0 for under two.</summary>
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

        /// <summary>How a hire goes, rolled once when booked: an unreliable one posts a day late, a flaky one goes off message, an edgy one backfires.</summary>
        public static void Outcome(FlareHire hire, FlareInfluencer inf, int seed)
        {
            int late = (100 - inf.Reliability) * 6 * (inf.Style == FlareStyle.Flaky ? 2 : 1);
            hire.Late = FlareRules.Roll(seed, hire.Id, 61) < late;
            hire.OffMessage = inf.Style == FlareStyle.Flaky && FlareRules.Roll(seed, hire.Id, 62) < 250;
            hire.Backfired = inf.Style == FlareStyle.Edgy && FlareRules.Roll(seed, hire.Id, 63) < 200;
        }

        /// <summary>
        /// The influencers', brands' and candidates' posts of the day: a paid one, one for a rival or a candidate, a visit to a
        /// business of the player's (unpaid: added to <paramref name="visits"/> for its talk), local news, their niche, or an
        /// apology on the day of a scandal.
        /// </summary>
        public static void Day(FlareFile f, int day, IReadOnlyList<FlareShop> shops, IReadOnlyList<string> rivals, IReadOnlyList<string> candidates, IReadOnlyList<string> brands,
            List<FlarePost> visits)
        {
            if (f.NewsDay != day - 1 && f.NewsDay != day)
            {
                f.NewsToday = 0;   // yesterday's news is still today's to talk about
            }
            int chance = PostChanceOf(f.ActiveInfluencers);
            for (int i = 0; i < f.Influencers.Count; i++)
            {
                FlareInfluencer inf = f.Influencers[i];
                FlareHire hire = HireOf(f, inf.Id, day);
                if (!inf.Active && hire == null)
                {
                    continue;   // one who stopped still makes the posts of a deal already paid for
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
                        hire.Counted |= hire.Posted == 0 && day == hire.From + (hire.Late ? 1 : 0);   // an older save's deal whose first post is this one is counted whole
                        hire.Posted++;
                        hire.LastPost = day;
                    }
                    continue;
                }
                int roll = FlareRules.Roll(inf.Seed, day, 70);
                if (roll >= chance)
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
        /// The chance a day an influencer with nothing paid posts, per mille: shared beyond <see cref="PostsLevel"/> active,
        /// so the feed has about as many of their posts however many there are.
        /// </summary>
        public static int PostChanceOf(int active)
        {
            return active <= PostsLevel ? PostChance : PostChance * PostsLevel / active;
        }

        /// <summary>
        /// The posts about a business for its Business tab card: the player's own still kept, reviews apart
        /// (<paramref name="own"/>), and the paid posts of every deal there. <paramref name="last"/> is the newest's day, -1 with none.
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

        /// <summary>
        /// The text key of a deal's line about its posts. Null for a deal from an older save with no post counted yet: its count
        /// is not known.
        /// </summary>
        public static string HirePostsKey(FlareHire h, int day)
        {
            int stage = HireStage(h, day);
            if (stage == HireBefore)
            {
                return "reallife_flare_hire_posts_from";
            }
            if (h.Posted > 0)
            {
                return h.Counted ? (h.Posted == 1 ? "reallife_flare_hire_posts_one" : "reallife_flare_hire_posts")
                    : h.Posted == 1 ? "reallife_flare_hire_posts_since_one" : "reallife_flare_hire_posts_since";
            }
            return !h.Counted ? null : stage == HireWaiting ? "reallife_flare_hire_posts_none" : "reallife_flare_hire_posts_none_over";
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

        // A business of the player's an influencer would visit: in their district and niche (local news: any niche).
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

        /// <summary>Whether the day's one notification of posts from followed accounts may go: the option on, one posted. True takes it.</summary>
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
