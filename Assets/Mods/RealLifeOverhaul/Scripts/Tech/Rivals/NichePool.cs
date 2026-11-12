using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One of the player's founded firms selling into niches, as competition reads it on a day. Game-free.</summary>
    public struct RivalryYou
    {
        public string Place, Name;
        public int Niche;           // the firm's own niche
        public int Served;          // one bit per niche its product sells into: its own and the neighbours it opened
        public int Market;          // the market of its best live product
        public int Features;        // the features of that product
        public int Appeal;          // its appeal in the niche today (the sale's, thousandths)
        public int Fit;             // its fit, thousandths
        public long Invested;       // cents at the start's prices all the investors in its cap table put in
        public int Stage;           // the last round its cap table raised; -1 before the first
    }

    /// <summary>
    /// The buyers of a niche as one pool for everyone who sells in it: the player's products and the city's startups (and the listed companies that came in) pull in the same
    /// buyers, a share of them each by the pull of every seller. Worked out on the day, never saved. Game-free.
    /// </summary>
    public sealed class NichePool
    {
        public const int Markets = (int)SectorMarket.Count;

        public readonly long[] Rival = new long[NicheCatalogue.Count * Markets];      // the pull of the rivals that sell in a niche, by niche and market
        public readonly long[] Player = new long[NicheCatalogue.Count * Markets];     // ...and of the player's products there, at their niche pull
        public readonly long[] BoundRival = new long[Markets];                        // the pull of every rival bound to a niche, as the market holds it
        public readonly long[] BoundPlayer = new long[Markets];                       // ...and of every product of the player's bound to one, at its pull in the whole market

        public static int At(int niche, int market) => niche * Markets + market;

        public void Clear()
        {
            Array.Clear(Rival, 0, Rival.Length);
            Array.Clear(Player, 0, Player.Length);
            Array.Clear(BoundRival, 0, BoundRival.Length);
            Array.Clear(BoundPlayer, 0, BoundPlayer.Length);
        }

        public void AddRival(int niche, int market, long pull)
        {
            Rival[At(niche, market)] += Math.Max(0L, pull);
            BoundRival[market] += Math.Max(0L, pull);
        }

        /// <summary>A product of the player's bound to a niche: its pull among that niche's buyers, and its pull as the whole market holds it (taken out of the market's rest).</summary>
        public void AddPlayer(int niche, int market, long nichePull, long marketPull)
        {
            Player[At(niche, market)] += Math.Max(0L, nichePull);
            BoundPlayer[market] += Math.Max(0L, marketPull);
        }

        /// <summary>What the market's pull holds that no niche owns: the companies', the startups' with no niche, the player's products with none.</summary>
        public long Rest(int market, long marketAll) => Math.Max(0L, marketAll - BoundRival[market] - BoundPlayer[market]);
    }

    /// <summary>Everything one day of competition reads and changes, filled by the director from the game and kept apart from it so the rules can be run on their own. Game-free.</summary>
    public sealed class RivalryWorld
    {
        public SectorFile Sector;
        public VentureFile Venture;
        public RivalryFile Rec;
        public readonly NichePool Pool = new NichePool();
        public readonly List<RivalryYou> You = new List<RivalryYou>(4);
        public readonly long[] Soft = new long[NicheCatalogue.Count], Ai = new long[NicheCatalogue.Count];   // buyers in each niche today, for a software product and an AI one
        public int Selling, Sense, Relationships;        // the player's skills, 0 to 100
        // what a day of it did, for the director to tell and to book
        public readonly List<RivalryReaction> Told = new List<RivalryReaction>(8);
        public readonly List<BakeOff> Decided = new List<BakeOff>(2);
        public readonly List<BakeOff> Opened = new List<BakeOff>(2);
        public readonly List<VentureNote> Sales = new List<VentureNote>(2);

        public int Seed => Venture != null ? Venture.Seed : Sector != null ? Sector.Seed : 1;

        public long NicheBuyers(int niche, int market) => niche < 0 || niche >= NicheCatalogue.Count ? 0L : NicheRules.IsAi(market) ? Ai[niche] : Soft[niche];
    }
}
