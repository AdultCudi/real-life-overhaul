using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in the city by rule, the game studio on Flare (the brief's sixth step; Tech/Studio/StudioBridges): a studio's news is its own account's
    /// post, heard by its followers, and residents who play games follow it; a devlog excites the residents who care for games as many as it reaches;
    /// a crowdfunding campaign is backed by the residents who follow the studio or the player and care for games, each by what their household can
    /// give, and by followers beyond the city. Without the residents (Flare in the city off, or a guest), the player's followers stand in for them.
    /// Game-free. COST: a post heard (a walk over the account's followers), on the studio's own beat.
    /// </summary>
    public static partial class FlareCityRules
    {
        public const int StudioNewsArg = 20;             // a business post's Arg from here on is a studio's news (Arg - 20 is Tech.StudioNews; 30 a devlog)
        public const int DevlogArg = 30;
        public const int CrowdfundArg = 31;
        public const int SectorNewsArg = 40;             // ...and from here a tech firm's news (Arg - 40 is Tech.SectorNews: Tech/Sector/SectorBridges)
        public const int GamesKind = -2;                 // a post about games: interested are the residents who play (the population's games), not a household's taste
        public const int DevlogHypeMost = 80;            // thousandths of the audience a devlog excites at the most: a trailer's (StudioCatalogue.HypeTrailer)
        public const int DevlogPerFan = 400;             // thousandths of hype for each interested resident, over the city's adults who care for games
        public const int FounderShareQuality = 700;      // thousandths: the player sharing their studio's post is heard as most of a post
        public static readonly int[] PledgeWhole = { 10, 25, 60 };   // whole money a resident backing a campaign gives, by class
        public const int PledgeChance = 120;             // per mille of the interested residents who saw a campaign who back it
        public const int BeyondPledges = 8;              // per mille of the player's followers beyond the city who back it...
        public const int BeyondPledgeWhole = 20;         // ...and what each gives, whole money

        /// <summary>A devlog's hype from the residents it excited: interested residents over the city's adults who care for games, at most a trailer's.</summary>
        public static int DevlogHype(int interested, int gamers)
        {
            if (interested <= 0)
            {
                return 0;
            }
            long hype = (long)interested * DevlogPerFan / Math.Max(1, gamers);
            return (int)Math.Min(DevlogHypeMost, Math.Max(1, hype));
        }

        /// <summary>A devlog's hype with no residents to count (Flare in the city off, a guest): by the player's followers, a few points for each tenfold.</summary>
        public static int DevlogHypeFollowers(int followers)
        {
            return followers <= 0 ? 0 : (int)Math.Min(DevlogHypeMost, Math.Round(12.0 * Math.Log10(1.0 + followers)));
        }

        /// <summary>The adults of the city who play games (the population's games): a devlog's and a campaign's own public.</summary>
        public static int Gamers(FlareCityState c, PopulationState s)
        {
            int n = 0;
            int to = c.AdultStart.Length > 0 ? c.AdultStart[c.AdultStart.Length - 1] : 0;
            for (int i = 0; i < to && i < c.AdultList.Length; i++)
            {
                n += PlaysGames(s, c.AdultList[i]) ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// A crowdfunding campaign's pledges, cents: each follower of the player's or the studio's account who cares for games, sees it (Flare's reach
        /// share) and backs it (<see cref="PledgeChance"/>) gives by their household's class; the player's followers beyond the city give too; never
        /// more than the goal. Each resident counts once.
        /// </summary>
        public static long Pledges(FlareCityState c, PopulationState s, int playerAccount, int studioAccount, int beyond, long goal, int seed, HearScratch scratch)
        {
            if (goal <= 0)
            {
                return 0;
            }
            scratch.Size(s.People.Count);
            int serial = scratch.Next();
            long cents = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int account = pass == 0 ? playerAccount : studioAccount;
                int start = 0;
                int n = account >= 0 ? c.FollowersOf(account, out start) : 0;
                for (int i = 0; i < n; i++)
                {
                    int p = c.FollowerList[start + i];
                    if (p < 0 || p >= s.People.Count || s.People.Id[p] == 0 || scratch.Stamp[p] == serial)
                    {
                        continue;
                    }
                    scratch.Stamp[p] = serial;
                    int id = s.People.Id[p];
                    int h = s.People.Household[p];
                    if (h < 0 || !s.Households.Alive(h) || !PlaysGames(s, p))
                    {
                        continue;
                    }
                    if (Mille(seed, id, SaltSee, 7) < FlareCityCatalogue.SeenShare && Mille(seed, id, SaltShare, 7) < PledgeChance)
                    {
                        cents += PledgeWhole[Math.Max(0, Math.Min((int)s.Households.Class[h], 2))] * 100L;
                    }
                }
            }
            cents += (long)Math.Max(0, beyond) * BeyondPledges / 1000 * BeyondPledgeWhole * 100;
            return Math.Min(goal, cents);
        }

        /// <summary>A tech firm's news as residents take it: a launch, a benchmark or an award good (+1), an outage, a breach or an incident bad (-1), a rival's neither.</summary>
        public static int SectorSign(int news)
        {
            switch (news)
            {
                case 0:   // Tech.SectorNews.Launch
                case 3:   // Benchmark
                case 7:   // Award
                    return 1;
                case 1:   // Outage
                case 2:   // Breach
                case 8:   // Incident
                    return -1;
                default:
                    return 0;
            }
        }

        /// <summary>A rival's news (a product shipped, a price cut, a tender won): the city's own post, never a firm of the player's.</summary>
        public static bool SectorRival(int news) => news == 4 || news == 5 || news == 6;

        /// <summary>A campaign's pledges with no residents to count: the player's followers alone, as those beyond the city give.</summary>
        public static long PledgesFollowers(int followers, long goal)
        {
            return Math.Min(Math.Max(0, goal), (long)Math.Max(0, followers) * BeyondPledges / 1000 * BeyondPledgeWhole * 100);
        }
    }
}
