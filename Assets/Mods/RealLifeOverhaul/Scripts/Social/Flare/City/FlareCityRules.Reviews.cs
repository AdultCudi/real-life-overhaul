using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in the city by rule, reviews and follows from residents who really shopped there (the brief's third step): a customer of a business is
    /// drawn from the day's pools as its money came (a class by its takings by class, a district of origin by the money its residents routed there,
    /// a household of that district and class by its day's budget for the kind), so the drawn household spent there that day in the pools' own
    /// proportions; an adult of it writes the review or follows. The stars follow the business's satisfaction, its interior and its prices against
    /// what the reviewer's class bears. Game-free. COST: a pass over one district and class's households a draw (a few hundred), a few thousand
    /// draws a day in the whole city, in the day's slices.
    /// </summary>
    public static partial class FlareCityRules
    {
        private const int SaltClass = 51, SaltOrigin = 52, SaltHousehold = 53, SaltMember = 54, SaltStars = 55, SaltBrowse = 56, SaltReviewFollow = 57, SaltFollowVisit = 58;

        /// <summary>
        /// A household that spent at a business on the pools' day, drawn as its money came; -1 when none of its takings came from residents at home.
        /// </summary>
        public static int Customer(FlareCityState c, PopulationState s, PoolRules.DemandDay dd, int b, int seed, int draw)
        {
            BusinessTable bt = s.Businesses;
            if (dd == null || dd.Day < 0 || b < 0 || b >= bt.Count || !Sells(bt, b))
            {
                return -1;
            }
            int n = dd.Districts, db = bt.District[b], k = bt.Kind[b];
            if (db >= n || c.HomeStart.Length < n * PopulationCatalogue.Classes + 1)
            {
                return -1;
            }
            long classes = 0;
            for (int cls = 0; cls < PopulationCatalogue.Classes; cls++)
            {
                classes += Math.Max(0, bt.ByClass[b * PopulationCatalogue.Classes + cls]);
            }
            if (classes <= 0)
            {
                return -1;
            }
            long x = (long)Millionth(seed, b, SaltClass, draw) * classes / 1000000;
            int pick = 0;
            for (long sum = 0; pick < PopulationCatalogue.Classes; pick++)
            {
                sum += Math.Max(0, bt.ByClass[b * PopulationCatalogue.Classes + pick]);
                if (x < sum)
                {
                    break;
                }
            }
            int c0 = Math.Min(pick, PopulationCatalogue.Classes - 1);
            // the district of origin, by the money its residents at home routed to the business's district for the kind
            long total = 0;
            for (int o = 0; o < n; o++)
            {
                total += Routed(dd, o, c0, k, db);
            }
            if (total <= 0)
            {
                return -1;
            }
            x = (long)Millionth(seed, b, SaltOrigin, draw) * total / 1000000;
            int origin = n - 1;
            for (long sum = 0, o = 0; o < n; o++)
            {
                sum += Routed(dd, (int)o, c0, k, db);
                if (x < sum)
                {
                    origin = (int)o;
                    break;
                }
            }
            // a household of that district and class, by its day's budget for the kind
            int cell = origin * PopulationCatalogue.Classes + c0;
            int from = c.HomeStart[cell], to = c.HomeStart[cell + 1];
            long budgets = 0;
            for (int i = from; i < to; i++)
            {
                int h = c.HomeList[i];
                budgets += h * Kinds + k < dd.Budget.Length ? Math.Max(0, dd.Budget[h * Kinds + k]) : 0;
            }
            if (budgets <= 0)
            {
                return -1;
            }
            x = (long)Millionth(seed, b, SaltHousehold, draw) * budgets / 1000000;
            long run = 0;
            for (int i = from; i < to; i++)
            {
                int h = c.HomeList[i];
                run += h * Kinds + k < dd.Budget.Length ? Math.Max(0, dd.Budget[h * Kinds + k]) : 0;
                if (x < run)
                {
                    return h;
                }
            }
            return c.HomeList[to - 1];
        }

        // The money a district's households of a class spent at home on a kind that the day's routing sent to the business's district.
        private static long Routed(PoolRules.DemandDay dd, int origin, int cls, int k, int db)
        {
            int n = dd.Districts;
            int r = dd.RouteIndex(PoolRules.Home, origin, k);
            long total = dd.RouteCum[r + n];
            long part = dd.RouteCum[r + db + 1] - dd.RouteCum[r + db];
            long money = dd.LineMoney[dd.LineIndex(PoolRules.Home, origin, cls, k)];
            return total <= 0 || part <= 0 || money <= 0 ? 0 : money / 100 * part / total;
        }

        /// <summary>An adult of a household by a roll (the one who writes, or follows), -1 for none.</summary>
        public static int AdultOf(FlareCityState c, PopulationState s, int h, int day, int roll)
        {
            int adults = 0;
            for (int m = s.Households.First[h]; m >= 0; m = s.People.Next[m])
            {
                adults += Age(s, m, day) >= FlareCityCatalogue.AdultAge ? 1 : 0;
            }
            if (adults == 0)
            {
                return -1;
            }
            int want = roll % adults;
            for (int m = s.Households.First[h]; m >= 0; m = s.People.Next[m])
            {
                if (Age(s, m, day) >= FlareCityCatalogue.AdultAge && want-- == 0)
                {
                    return m;
                }
            }
            return -1;
        }

        /// <summary>
        /// A review's stars, 1 to 5: the business's satisfaction (its customers'), a little for its interior, less where its prices are past what the
        /// reviewer's class bears (a star for each <see cref="FlareCityCatalogue.StarsPerPricePoint"/> thousandths of the price term lost), a star off
        /// or on now and then (FlareLiveRules.Stars).
        /// </summary>
        public static int Stars(BusinessTable bt, int b, int cls, int bears, int roll)
        {
            int score = bt.Content[b] + (bt.Quality[b] - 50) / 5;
            int price = PoolRules.PriceTerm(bt.Price[b], Math.Max(0, Math.Min(cls, 2)), bears);
            if (price < 1000)
            {
                score -= (1000 - price) * 20 / FlareCityCatalogue.StarsPerPricePoint;
            }
            return FlareLiveRules.Stars(Math.Max(0, Math.Min(100, score)), roll);
        }

        /// <summary>
        /// The day's reviews of a business by its customers: one for every <see cref="FlareCityCatalogue.VisitsPerReview"/> of its visits (the rest
        /// carried to the next day), each by a resident drawn from its customers, kept in its ring and its rating, and read by the people round the
        /// reviewer and a few residents of its district looking for its kind. How many were written.
        /// </summary>
        public static int Reviews(FlareCityState c, PopulationState s, PoolRules.DemandDay dd, int b, int day, int bears, int room, HearScratch scratch, int[] accountBiz)
        {
            BusinessTable bt = s.Businesses;
            if (b >= c.BusinessCount || b >= bt.Count || !Sells(bt, b) || room <= 0)
            {
                return 0;
            }
            c.ReviewDue[b] += Math.Max(0, bt.Visits[b]);
            int written = 0;
            while (c.ReviewDue[b] >= FlareCityCatalogue.VisitsPerReview && written < room)
            {
                c.ReviewDue[b] -= FlareCityCatalogue.VisitsPerReview;
                int draw = c.ReviewCount[b] + written;
                int h = Customer(c, s, dd, b, c.Seed ^ day, draw);
                int p = h >= 0 ? AdultOf(c, s, h, day, Mille(c.Seed, b, SaltMember, draw)) : -1;
                if (p < 0)
                {
                    continue;   // a visitor's or a commuter's visit: no resident wrote it
                }
                int stars = Stars(bt, b, s.Households.Class[h], bears, Mille(c.Seed, b, SaltStars, day * 31 + draw));
                int price = PoolRules.PriceTerm(bt.Price[b], Math.Max(0, Math.Min((int)s.Households.Class[h], 2)), bears);
                int priceScore = price >= 1000 ? 100 : price / 10;
                int why = (int)FlareUseRules.Reason(priceScore, bt.Quality[b], FlareUseRules.WaitScore(bt.Visits[b], bt.Room[b]), bt.Content[b]);
                AddReview(c, b, s.People.Id[p], stars, day);
                // The warning is the player's alone: every business of the city is reviewed here, and theirs are the only ones
                // whose fall they are told of (it once named any business "a business of yours", every day from then on).
                if (FlareUseRules.NoteReview(c.ReviewNotes, c.StarRows, bt.Key[b], s.People.Id[p], stars, day, why) && FlareUseRules.WarnsPlayer(bt.Owner[b]))
                {
                    c.WarnKey = bt.Key[b];
                }
                written++;
                Read(c, s, b, p, stars, day, draw, scratch, accountBiz);
            }
            c.ReviewCount[b] += written;
            return written;
        }

        /// <summary>A review into a business's ring and rating.</summary>
        public static void AddReview(FlareCityState c, int b, int person, int stars, int day)
        {
            int head = c.ReviewHead[b];
            c.Reviews[b * FlareCityCatalogue.ReviewRing + head] = new FlareCityReview { Person = person, Stars = (byte)Math.Max(1, Math.Min(5, stars)), Day = day };
            c.ReviewHead[b] = (byte)((head + 1) % FlareCityCatalogue.ReviewRing);
            c.RatingSum[b] += stars * 1000L;
            c.RatingCount[b] += 1000L;
        }

        /// <summary>The newest reviews of a business, newest first, into <paramref name="into"/> (up to its length); how many.</summary>
        public static int NewestReviews(FlareCityState c, int b, FlareCityReview[] into)
        {
            if (b < 0 || b >= c.BusinessCount)
            {
                return 0;
            }
            int n = 0;
            for (int k = 1; k <= FlareCityCatalogue.ReviewRing && n < into.Length; k++)
            {
                FlareCityReview r = c.Reviews[b * FlareCityCatalogue.ReviewRing + (c.ReviewHead[b] - k + FlareCityCatalogue.ReviewRing) % FlareCityCatalogue.ReviewRing];
                if (r.Person != 0)
                {
                    into[n++] = r;
                }
            }
            return n;
        }

        // A review read: by the reviewer's household and two at work, and by residents of its district who care for its kind and look it up.
        private static void Read(FlareCityState c, PopulationState s, int b, int reviewer, int stars, int day, int draw, HearScratch scratch, int[] accountBiz)
        {
            int sign = stars >= 4 ? 1 : stars <= 2 ? -1 : 0;
            if (sign == 0)
            {
                return;
            }
            var post = new CityPost
            {
                Id = draw, Account = -1, Business = b, Kind = s.Businesses.Kind[b], Sign = sign, Quality = 1000, Fit = 1000, District = s.Businesses.District[b],
                Seed = (int)Hash(c.Seed, b, day, draw)
            };
            scratch.Size(s.People.Count);
            int serial = scratch.Next();
            var r = new HeardReport();
            int seed = post.Seed;
            scratch.Stamp[reviewer] = serial;
            Ties(c, s, reviewer, s.People.Id[reviewer], seed, post, serial, scratch, ref r);
            int d = post.District;
            if (d >= 0 && d + 1 < c.AdultStart.Length)
            {
                int from = c.AdultStart[d], adults = c.AdultStart[d + 1] - from;
                for (int j = 0; j < FlareCityCatalogue.ReviewBrowsers && adults > 0; j++)
                {
                    int p = c.AdultList[from + Millionth(seed, j, SaltBrowse, 0) % adults];
                    int h = s.People.Household[p];
                    if (h >= 0 && s.Households.Alive(h) && Cares(s.Households.Tastes[h], post.Kind) != 0)
                    {
                        Reach(c, s, p, seed, post, serial, scratch, ref r, true);
                    }
                }
            }
        }

        /// <summary>
        /// The residents who start to follow a business from their visits today: one for every <see cref="FlareCityCatalogue.VisitsPerFollow"/>
        /// visits (the rest a roll), each drawn from its customers. How many followed.
        /// </summary>
        public static int VisitFollows(FlareCityState c, PopulationState s, PoolRules.DemandDay dd, int b, int day, int account)
        {
            if (account < 0 || b >= s.Businesses.Count)
            {
                return 0;
            }
            int visits = Math.Max(0, s.Businesses.Visits[b]);
            int follows = visits / FlareCityCatalogue.VisitsPerFollow;
            if (Millionth(c.Seed, b, SaltFollowVisit, day) < (long)(visits % FlareCityCatalogue.VisitsPerFollow) * 1000000 / FlareCityCatalogue.VisitsPerFollow)
            {
                follows++;
            }
            int done = 0;
            for (int i = 0; i < follows; i++)
            {
                int h = Customer(c, s, dd, b, c.Seed ^ (day * 7 + 3), 1000 + i);
                int p = h >= 0 ? AdultOf(c, s, h, day, Mille(c.Seed, b, SaltReviewFollow, day * 13 + i)) : -1;
                if (p >= 0 && p < c.Owner.Length && c.Owner[p] == s.People.Id[p] && Follow(c, p, account))
                {
                    done++;
                }
            }
            return done;
        }
    }
}
