using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The buffers a post's hearing works in, made once and kept (no allocation per post).</summary>
    public sealed class HearScratch
    {
        public int[] Stamp = new int[0];             // by person slot: the last post that reached them (each resident counts once a post)
        public int Serial;
        public int[] Candidates = new int[256];      // residents who saw it, were interested and do not follow its author
        public int CandidateCount;

        public void Size(int people)
        {
            if (Stamp.Length < people)
            {
                Array.Resize(ref Stamp, Math.Max(people, Stamp.Length * 3 / 2 + 64));
            }
        }

        /// <summary>A fresh post: every resident may be reached again.</summary>
        public int Next()
        {
            CandidateCount = 0;
            Serial++;
            if (Serial == int.MaxValue)
            {
                Array.Clear(Stamp, 0, Stamp.Length);
                Serial = 1;
            }
            return Serial;
        }

        public void AddCandidate(int slot)
        {
            if (CandidateCount == Candidates.Length)
            {
                Array.Resize(ref Candidates, Candidates.Length * 2);
            }
            Candidates[CandidateCount++] = slot;
        }
    }

    /// <summary>
    /// Flare in the city by rule, a post heard: its author's followers see it (as many as FlareRules.Reach says see a post, resident by resident),
    /// the interested share it with the people round them (their household, two at work, a neighbour), others come across it by its tags, and those
    /// who are interested and hear of a business remember it (<see cref="Impress"/>); some of those who do not follow its author start to. Each
    /// resident counts once a post. Game-free. COST: a pass over the author's followers and a few hundred more, on a post (the player's at once, the
    /// city's in the day's slices).
    /// </summary>
    public static partial class FlareCityRules
    {
        private const int SaltSee = 31, SaltInterest = 32, SaltShare = 33, SaltTie = 34, SaltDiscover = 35, SaltFollowPost = 36, SaltCoworker = 37, SaltNeighbour = 38;

        /// <summary>Per mille of the author's followers who see a post: FlareRules.Reach's share by its quality and fit, three in four when it went viral.</summary>
        public static int SeenOf(in CityPost post)
        {
            if (post.Viral)
            {
                return FlareCityCatalogue.SeenViral;
            }
            long seen = (long)FlareCityCatalogue.SeenShare * Math.Max(0, post.Quality) / 1000 * Math.Max(0, post.Fit) / 1000;
            return (int)Math.Min(FlareCityCatalogue.SeenViral, seen);
        }

        /// <summary>The residents of the district who come across a post by its tags and Explore, at most <see cref="FlareCityCatalogue.DiscoverMost"/>.</summary>
        public static int DiscoveredOf(in CityPost post, int adults)
        {
            int share = post.Viral ? FlareCityCatalogue.DiscoverViral
                : (int)((long)FlareCityCatalogue.DiscoverShare * Math.Max(0, post.Quality) / 1000 * Math.Max(0, post.Fit) / 1000);
            return (int)Math.Min(FlareCityCatalogue.DiscoverMost, (long)Math.Max(0, adults) * share / 1000);
        }

        /// <summary>
        /// A post heard by the city's residents. The followers index and the people indexes must be the day's. <paramref name="funnel"/>, when
        /// given, is the player's measure of it; <paramref name="accountBiz"/> tells the businesses' accounts (for a resident making room to follow).
        /// </summary>
        public static HeardReport Hear(FlareCityState c, PopulationState s, in CityPost post, int day, int[] accountBiz, HearScratch scratch, FlareFunnel funnel)
        {
            var r = new HeardReport();
            if (c.Indexed < 0 || s == null)
            {
                return r;
            }
            scratch.Size(s.People.Count);
            int serial = scratch.Next();
            int seed = (int)Hash(c.Seed, post.Seed, post.Id, day);
            int seen = SeenOf(post);
            int share = post.Viral ? FlareCityCatalogue.ShareViral : FlareCityCatalogue.ShareChance;
            int start = 0;
            int n = post.Account >= 0 ? c.FollowersOf(post.Account, out start) : 0;
            for (int i = 0; i < n; i++)
            {
                int p = c.FollowerList[start + i];
                if (p < 0 || p >= s.People.Count || s.People.Id[p] == 0 || scratch.Stamp[p] == serial)
                {
                    continue;
                }
                int id = s.People.Id[p];
                if (p >= c.Owner.Length || c.Owner[p] != id || Mille(seed, id, SaltSee, 0) >= seen)
                {
                    continue;   // somebody new in the slot since the index (their follows start at the next pass), or did not see it
                }
                scratch.Stamp[p] = serial;
                r.Seen++;
                if (!Interested(c, s, p, id, seed, post))
                {
                    continue;
                }
                r.Interested++;
                r.Investors += Investor(s, p) ? 1 : 0;
                r.Impressions += Impress(c, s, p, post);
                if (Mille(seed, id, SaltShare, 0) < share)
                {
                    r.Shared++;
                    Ties(c, s, p, id, seed, post, serial, scratch, ref r);
                }
            }
            int district = post.Business >= 0 && post.Business < s.Businesses.Count ? s.Businesses.District[post.Business] : post.District;
            if (district >= 0 && district < c.Adults.Length && district + 1 < c.AdultStart.Length)
            {
                int from = c.AdultStart[district], adults = c.AdultStart[district + 1] - from;
                int find = DiscoveredOf(post, adults);
                for (int j = 0; j < find && adults > 0; j++)
                {
                    int p = c.AdultList[from + Millionth(seed, j, SaltDiscover, 0) % adults];
                    Reach(c, s, p, seed, post, serial, scratch, ref r, true);
                }
            }
            r.Followed = Gain(c, s, post, seed, accountBiz, scratch);
            if (funnel != null)
            {
                funnel.Seen += r.Seen;
                funnel.Interested += r.Interested;
                funnel.Followed += r.Followed;
                funnel.Live = (int)Math.Min(int.MaxValue, funnel.Live + r.Impressions);
            }
            return r;
        }

        private static bool Interested(FlareCityState c, PopulationState s, int p, int id, int seed, in CityPost post)
        {
            if (post.Kind == GamesKind)
            {
                return Mille(seed, id, SaltInterest, 0) < (PlaysGames(s, p) ? FlareCityCatalogue.InterestMatch : FlareCityCatalogue.InterestOther);
            }
            int h = s.People.Household[p];
            uint tastes = h >= 0 && s.Households.Alive(h) ? s.Households.Tastes[h] : 0u;
            int more = Trending(c, s, post) ? FlareCityCatalogue.TrendInterest : 0;   // what is trending interests more of those who hear of it
            return Mille(seed, id, SaltInterest, 0) < InterestOf(tastes, post.Kind, FlareCityCatalogue.InterestMatch, FlareCityCatalogue.InterestOther) + more;
        }

        /// <summary>Whether a resident plays games: a genre they like (the population's games, GameRules).</summary>
        public static bool PlaysGames(PopulationState s, int p)
        {
            return p >= 0 && p < s.People.Games.Length && (s.People.Games[p] & ((1 << GameRules.Genres) - 1)) != 0;
        }

        /// <summary>Whether a resident is one of the city's retail investors: their household has savings to put in the market.</summary>
        public static bool Investor(PopulationState s, int p)
        {
            int h = s.People.Household[p];
            return h >= 0 && s.Households.Alive(h) && s.Households.Savings[h] >= FlareCityCatalogue.InvestorSavings;
        }

        /// <summary>The stock market's kicks a viral post about a business gives its trade's sector: one, and one more for each so many investors it reached.</summary>
        public static int MarketKicks(int investors)
        {
            return 1 + Math.Min(FlareCityCatalogue.MarketKicksMost - 1, Math.Max(0, investors) / FlareCityCatalogue.InvestorsPerKick);
        }

        // The people round a resident who shared it: their household, two at work and a neighbour, each seeing it now and then.
        private static void Ties(FlareCityState c, PopulationState s, int p, int id, int seed, in CityPost post, int serial, HearScratch scratch, ref HeardReport r)
        {
            int h = s.People.Household[p];
            for (int m = h >= 0 && s.Households.Alive(h) ? s.Households.First[h] : -1; m >= 0; m = s.People.Next[m])
            {
                if (m != p)
                {
                    Tie(c, s, m, seed, post, serial, scratch, ref r);
                }
            }
            int e = s.People.Job[p];
            if (e >= 0 && e + 1 < c.WorkStart.Length)
            {
                int from = c.WorkStart[e], k = c.WorkStart[e + 1] - from;
                for (int t = 0; t < FlareCityCatalogue.Coworkers && k > 1; t++)
                {
                    Tie(c, s, c.WorkList[from + Millionth(seed, id, SaltCoworker, t) % k], seed, post, serial, scratch, ref r);
                }
            }
            int d = DistrictOf(s, p);
            if (d >= 0 && d + 1 < c.AdultStart.Length)
            {
                int from = c.AdultStart[d], k = c.AdultStart[d + 1] - from;
                for (int t = 0; t < FlareCityCatalogue.Neighbours && k > 1; t++)
                {
                    Tie(c, s, c.AdultList[from + Millionth(seed, id, SaltNeighbour, t) % k], seed, post, serial, scratch, ref r);
                }
            }
        }

        private static void Tie(FlareCityState c, PopulationState s, int p, int seed, in CityPost post, int serial, HearScratch scratch, ref HeardReport r)
        {
            if (p < 0 || p >= s.People.Count || s.People.Id[p] == 0 || scratch.Stamp[p] == serial || Mille(seed, s.People.Id[p], SaltTie, 0) >= FlareCityCatalogue.TieSees)
            {
                return;
            }
            Reach(c, s, p, seed, post, serial, scratch, ref r, false);
        }

        // A resident reached by a share or by coming across it: seen, maybe interested, and a candidate to follow its author.
        private static void Reach(FlareCityState c, PopulationState s, int p, int seed, in CityPost post, int serial, HearScratch scratch, ref HeardReport r, bool discovered)
        {
            if (p < 0 || p >= s.People.Count || s.People.Id[p] == 0 || scratch.Stamp[p] == serial || p >= c.Owner.Length || c.Owner[p] != s.People.Id[p])
            {
                return;   // a child, somebody Flare has not met yet, or reached already
            }
            scratch.Stamp[p] = serial;
            int id = s.People.Id[p];
            r.Seen++;
            if (discovered)
            {
                r.Discovered++;
            }
            if (!Interested(c, s, p, id, seed, post))
            {
                return;
            }
            r.Interested++;
            r.Investors += Investor(s, p) ? 1 : 0;
            r.Impressions += Impress(c, s, p, post);
            if (post.Account >= 0 && !Follows(c, p, post.Account))
            {
                scratch.AddCandidate(p);
            }
        }

        /// <summary>
        /// A resident heard of a business: one more of its district who heard good (or bad) things, counted as the share of the district who had
        /// not heard yet (the district's count never passes its adults). Thousandths of a resident added.
        /// </summary>
        public static int Impress(FlareCityState c, PopulationState s, int p, in CityPost post)
        {
            int b = post.Business;
            if (b < 0 || b >= c.BusinessCount || post.Sign == 0)
            {
                return 0;
            }
            int d = DistrictOf(s, p);
            if (d < 0 || d >= c.Districts || d >= c.Adults.Length)
            {
                return 0;
            }
            return Impress(c, b, d, post.Sign, 1000);
        }

        /// <summary>So many thousandths of a resident of a district heard of a business, good or bad, as the share who had not yet: what was added.</summary>
        public static int Impress(FlareCityState c, int b, int d, int sign, int thousandths)
        {
            int adults = d < c.Adults.Length ? c.Adults[d] : 0;
            if (adults <= 0 || thousandths <= 0)
            {
                return 0;
            }
            int[] into = sign > 0 ? c.Heard : c.Sour;
            int at = b * c.Districts + d;
            long full = (long)adults * 1000;
            long now = into[at];
            long add = thousandths * (full - now) / full;
            if (add <= 0)
            {
                return 0;
            }
            into[at] = (int)Math.Min(full, now + add);
            if (sign < 0)
            {
                c.Answered[b] = false;   // fresh bad talk: unanswered again
            }
            return (int)add;
        }

        // Residents who saw a post and were interested and did not follow its author: the player's city share of what the account gained, in the
        // order they were reached; for a business or a voice, some of them.
        private static int Gain(FlareCityState c, PopulationState s, in CityPost post, int seed, int[] accountBiz, HearScratch scratch)
        {
            if (post.Account < 0)
            {
                return 0;
            }
            int followed = 0;
            bool player = post.Account == FlareCityState.PlayerAccount;
            for (int i = 0; i < scratch.CandidateCount; i++)
            {
                int p = scratch.Candidates[i];
                if (player)
                {
                    if (followed >= post.Gain)
                    {
                        break;
                    }
                }
                else if (Mille(seed, s.People.Id[p], SaltFollowPost, 0) >= FlareCityCatalogue.BusinessFollowFromPost)
                {
                    continue;
                }
                if (FollowMakingRoom(c, p, post.Account, accountBiz, Mille(seed, s.People.Id[p], SaltFollowPost, 1)))
                {
                    followed++;
                }
            }
            if (player)
            {
                c.CityFollowers += followed;
            }
            return followed;
        }

        /// <summary>The player's share of a post's gain that is the city's, by the account's tier (the rest is beyond the city).</summary>
        public static int CityGain(int gain, int tier)
        {
            if (gain <= 0)
            {
                return 0;
            }
            return (int)((long)gain * FlareCityCatalogue.CityShare[Math.Max(0, Math.Min(tier, FlareCityCatalogue.CityShare.Length - 1))] / 1000);
        }
    }
}
