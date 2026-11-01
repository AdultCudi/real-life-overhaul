using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in the city by rule, ads (the brief's third step: "Ads target residents by district, age, income and interests, priced by reach, and the
    /// result is measured in visits and takings"): an ad is shown to so many residents a day who match its target, picked in a rolled order; those who
    /// care for what it sells are mostly interested, and remember the business as a post's readers do; the day after, the attention it gave is
    /// measured in visits and takings like a post's (FlareCityRules.Apportion). The city's own businesses and the rivals run ads by the same rules.
    /// Game-free. COST: a walk over the targeted district's adults an ad a day (a few thousand), on a purchase and in the day's slices.
    /// </summary>
    public static partial class FlareCityRules
    {
        private const int SaltAdStart = 71, SaltAdStep = 72, SaltAdInterest = 73, SaltAdFollow = 74, SaltCityAd = 75, SaltCityAdPick = 76;

        /// <summary>Whether a resident (an adult Flare knows) is in an ad's target: its district, age band, class and interest.</summary>
        public static bool Targets(PopulationState s, FlareAd ad, int p, int day)
        {
            int h = s.People.Household[p];
            if (h < 0 || !s.Households.Alive(h))
            {
                return false;
            }
            if (ad.District >= 0 && s.Households.District[h] != ad.District)
            {
                return false;
            }
            if (ad.Class >= 0 && s.Households.Class[h] != ad.Class)
            {
                return false;
            }
            int band = Math.Max(0, Math.Min(ad.Age, FlareCityCatalogue.AgeFrom.Length - 1));
            int age = Age(s, p, day);
            if (age < FlareCityCatalogue.AgeFrom[band] || age > FlareCityCatalogue.AgeTo[band])
            {
                return false;
            }
            return ad.Interest < 0 || Cares(s.Households.Tastes[h], ad.Interest) != 0;
        }

        /// <summary>The residents an ad's target holds today (the adults index must be the day's).</summary>
        public static int TargetSize(FlareCityState c, PopulationState s, FlareAd ad, int day)
        {
            int from, to;
            Region(c, ad.District, out from, out to);
            int n = 0;
            for (int i = from; i < to; i++)
            {
                n += Targets(s, ad, c.AdultList[i], day) ? 1 : 0;
            }
            return n;
        }

        // The adults an ad walks: one district's, or the whole city's.
        private static void Region(FlareCityState c, int district, out int from, out int to)
        {
            int districts = c.AdultStart.Length - 1;
            if (district >= 0 && district < districts)
            {
                from = c.AdultStart[district];
                to = c.AdultStart[district + 1];
            }
            else
            {
                from = 0;
                to = districts >= 0 ? c.AdultStart[districts] : 0;
            }
        }

        /// <summary>What an ad costs, cents: its residents a day times its days at <see cref="FlareCityCatalogue.AdCpm"/> a thousand, at the game's prices (thousandths).</summary>
        public static long AdCost(int reach, int days, int priceLevel)
        {
            long views = (long)Math.Max(0, reach) * Math.Max(1, days);
            return views * FlareCityCatalogue.AdCpm * 100 / 1000 * Math.Max(1, priceLevel) / 1000;
        }

        /// <summary>
        /// A day of an ad: so many residents of its target shown it (no more than the target holds), interested by what they care for, remembering its
        /// business; some start to follow the business. <paramref name="funnel"/> is the player's measure of it, or null.
        /// </summary>
        public static HeardReport Deliver(FlareCityState c, PopulationState s, FlareAd ad, int b, int kind, int account, int day, int[] accountBiz, HearScratch scratch,
            FlareFunnel funnel)
        {
            var r = new HeardReport();
            int from, to;
            Region(c, ad.District, out from, out to);
            int n = to - from;
            if (n <= 0 || ad.Reach <= 0 || c.Indexed < 0)
            {
                return r;
            }
            scratch.Size(s.People.Count);
            int serial = scratch.Next();
            int seed = (int)Hash(c.Seed, ad.Id, day, b);
            int step = Coprime(1 + Millionth(seed, n, SaltAdStep, 0) % n, n);
            int first = Millionth(seed, n, SaltAdStart, 0) % n;
            var post = new CityPost { Id = ad.Id, Account = account, Business = b, Kind = kind, Sign = 1, Quality = 1000, Fit = 1000, Seed = seed };
            for (int k = 0; k < n && r.Seen < ad.Reach; k++)
            {
                int p = c.AdultList[from + (int)(((long)first + (long)k * step) % n)];
                int id = s.People.Id[p];
                if (id == 0 || p >= c.Owner.Length || c.Owner[p] != id || !Targets(s, ad, p, day))
                {
                    continue;
                }
                scratch.Stamp[p] = serial;
                r.Seen++;
                int h = s.People.Household[p];
                uint tastes = s.Households.Tastes[h];
                int chance = ad.Interest >= 0 ? FlareCityCatalogue.AdInterest : InterestOf(tastes, kind, FlareCityCatalogue.AdInterest, FlareCityCatalogue.AdInterestOther);
                if (Mille(seed, id, SaltAdInterest, 0) >= chance)
                {
                    continue;
                }
                r.Interested++;
                r.Impressions += Impress(c, s, p, post);
                if (account >= 0 && !Follows(c, p, account) && Mille(seed, id, SaltAdFollow, 0) < FlareCityCatalogue.BusinessFollowFromPost
                    && FollowMakingRoom(c, p, account, accountBiz, Mille(seed, id, SaltAdFollow, 1)))
                {
                    r.Followed++;
                }
            }
            if (funnel != null)
            {
                funnel.Seen += r.Seen;
                funnel.Interested += r.Interested;
                funnel.Followed += r.Followed;
                funnel.Live = (int)Math.Min(int.MaxValue, funnel.Live + r.Impressions);
            }
            return r;
        }

        /// <summary>Whether an ad runs on a day after its first (its first is shown when it is bought).</summary>
        public static bool RunsAfterFirst(FlareAd ad, int day) => day > ad.From && day < ad.From + Math.Max(1, ad.Days);

        /// <summary>
        /// The city's own and the rivals' businesses that run an ad today (parity: the same rules as the player's), by their room for customers, never
        /// the player's: their slots into <paramref name="into"/>; how many.
        /// </summary>
        public static int CityAdvertisers(PopulationState s, int seed, int day, int busy, int[] into)
        {
            BusinessTable bt = s.Businesses;
            long total = 0;
            for (int b = 0; b < bt.Count; b++)
            {
                total += Sells(bt, b) && bt.Owner[b] != (byte)Population.Owner.Player && bt.Owner[b] != (byte)Population.Owner.Guest ? Math.Max(1, bt.Room[b]) : 0;
            }
            int want = Math.Min(into.Length, FlareCityCatalogue.CityAdsDay * (Math.Max(0, Math.Min(busy, 2)) + 1) / 2);
            int n = 0;
            for (int t = 0; t < want * 4 && n < want && total > 0; t++)
            {
                long x = (long)Millionth(seed, day, SaltCityAdPick, t) * total / 1000000;
                long sum = 0;
                for (int b = 0; b < bt.Count; b++)
                {
                    if (!Sells(bt, b) || bt.Owner[b] == (byte)Population.Owner.Player || bt.Owner[b] == (byte)Population.Owner.Guest)
                    {
                        continue;
                    }
                    sum += Math.Max(1, bt.Room[b]);
                    if (x < sum)
                    {
                        bool dup = false;
                        for (int i = 0; i < n; i++)
                        {
                            dup |= into[i] == b;
                        }
                        if (!dup)
                        {
                            into[n++] = b;
                        }
                        break;
                    }
                }
            }
            return n;
        }

        /// <summary>A city business's ad of the day: its own district's residents who care for its kind, <see cref="FlareCityCatalogue.CityAdReach"/> of them.</summary>
        public static FlareAd CityAd(PopulationState s, int b, int day, int seed)
        {
            return new FlareAd
            {
                Id = (int)(Hash(seed, b, day, SaltCityAd) & 0x3FFFFFFF), District = s.Businesses.District[b], Interest = s.Businesses.Kind[b],
                Reach = FlareCityCatalogue.CityAdReach, Days = 1, From = day, Mine = false
            };
        }
    }
}
