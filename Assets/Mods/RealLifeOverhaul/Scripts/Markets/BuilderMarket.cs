using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// What happened to one of the city's builders in the construction game, or of its own on the market. Saved as a
    /// number on the tape and in the news: never renumber, add at the end.
    /// </summary>
    public enum BuilderNewsKind
    {
        None = 0,
        TenderWon = 1,     // it won a tender (the others that bid lost it); a weight by the size of the works
        Finished = 2,      // a site of its handed over on time
        Late = 3,          // a site of its handed over late
        Developed = 4,     // a building of its finished on the new district's lots: the district grew
        Bribery = 5,       // a bribe of its came out (drawn by the market's own engine)
        Strike = 6,        // its crews went on strike (drawn by the market's own engine)
        Rigged = 7,        // a tender it won was found rigged
        Award = 8,         // a building of its was named the building of the year
        Count
    }

    /// <summary>One piece of builder news waiting for the market: who (a rival builder's number, -1 the player), what and how big.</summary>
    public struct BuilderItem
    {
        public int Rival;
        public BuilderNewsKind Kind;
        public int Weight;   // 1 to WeightMost: a city's works, a business's building or refit, a public building, a landmark
    }

    /// <summary>
    /// The city's builders on the stock market, game-free. The construction game's eight rival builders (Scripts/Construction,
    /// Civic's contractors in their order) are listed where a market would list them: the four largest of their own and
    /// Ironbridge Contracting as the contracting arm of Ironbridge Civil Works, which was listed already; the three
    /// family firms stay private. Their prices move on what they do in the construction game (tenders won and lost, sites
    /// handed over on time or late, the district's growth, a rigged tender, a building of the year), told through
    /// <see cref="Add"/> and played by every machine from the host's tape, and on their own news drawn by the engine at
    /// the close (a bribe coming out, a strike). Nothing here is per frame: a queue of a few items read once a second.
    /// </summary>
    public static class BuilderMarket
    {
        public const int Rivals = 8;                     // the construction game's rival builders (BuildCatalogue.RivalCount)
        public const int PublicWeight = 3;               // a public building's tender
        public const int WeightMost = 4;                 // a landmark's: the largest works the city puts out
        public const int QueueMost = 32;                 // news waiting for the market; the oldest goes past it
        public const int Player = -1;                    // the player's own company won the tender

        /// <summary>The listed company of each rival builder, by its number; 0 for one that stays private.</summary>
        public static readonly int[] RivalStock =
        {
            147,   // Dunmore and Pike Civil
            149,   // Hartwell Groundworks
            0,     // Calloway Brothers Scaffolding: a family firm
            146,   // Ostrander Building Co.
            148,   // Tri-Street Utilities
            0,     // Mercer Shopfitters: a family firm
            32,    // Ironbridge Contracting: Ironbridge Civil Works' contracting arm
            0      // Fenwick and Daughters: a family firm
        };

        /// <summary>The catalogue rows that are listed for the builders alone: they join a market only while the option is on.</summary>
        public static readonly int[] OwnRows = { 146, 147, 148, 149 };

        // A tender's winner gains most of a win and every other listed builder loses its share, so the builders together
        // gain nothing on average from the tenders they are expected to win between them.
        public const int WinBp = 160;                    // the mood's move of a win at weight one, before the size's scale
        public const int SiteOnTimeBp = 40;              // a site handed over on time, a weight
        public const int SiteLateBp = -120;              // a site handed over late, a weight
        public const int DevelopedBp = 50;               // the builder of a district's new building
        public const int DistrictBp = 6;                 // every other listed builder, as the district grows
        public const int BriberyBp = -350, BriberyMarginPermille = -60;   // the mood, and its usual margin lost for good
        public const int StrikeBp = -150;
        public const int RiggedBp = -450, RiggedMarginPermille = -100;
        public const int AwardBp = 220, AwardMarginPermille = 20;
        public const int MoodMostBp = 600;               // one piece of news moves a builder at most this much
        public static readonly int[] SizeScalePct = { 60, 100, 150 };   // by MarketSize: a small builder moves more

        // A listed builder's own news, drawn at the close from the day's secret like a crisis (MarketEngine.Builders).
        public const double BriberyChanceYear = 0.05;   // each listed builder, a year of the market
        public const double StrikeChanceYear = 0.10;

        private static readonly BuilderItem[] Queue = new BuilderItem[QueueMost];
        private static int _first, _count;

        /// <summary>The listed company of a rival builder; 0 for a private one or a number out of range.</summary>
        public static int StockOf(int rival)
        {
            return rival >= 0 && rival < RivalStock.Length ? RivalStock[rival] : 0;
        }

        /// <summary>The rival builder a listed company is, or -1 for a company that is none of them.</summary>
        public static int RivalOf(int stockId)
        {
            for (int r = 0; stockId > 0 && r < RivalStock.Length; r++)
            {
                if (RivalStock[r] == stockId)
                {
                    return r;
                }
            }
            return -1;
        }

        /// <summary>True for a catalogue row listed only for the builders (the option decides whether it joins a market).</summary>
        public static bool OwnRow(int stockId)
        {
            return Array.IndexOf(OwnRows, stockId) >= 0;
        }

        /// <summary>
        /// The construction game tells the market of a builder's news (Wiring, from its events); it reaches the market's tape
        /// within the second. A rival of -1 is the player's company winning a tender, which every listed builder lost.
        /// </summary>
        public static void Add(int rival, BuilderNewsKind kind, int weight)
        {
            if (kind <= BuilderNewsKind.None || kind >= BuilderNewsKind.Count || rival < Player || rival >= Rivals || (rival == Player && kind != BuilderNewsKind.TenderWon))
            {
                return;
            }
            if (_count == QueueMost)
            {
                _first = (_first + 1) % QueueMost;   // the oldest goes: a market that cannot hear for so long has missed it
                _count--;
            }
            Queue[(_first + _count) % QueueMost] = new BuilderItem { Rival = rival, Kind = kind, Weight = Math.Max(1, Math.Min(WeightMost, weight)) };
            _count++;
        }

        /// <summary>The oldest piece waiting, taken off the queue; false with none.</summary>
        public static bool Take(out BuilderItem item)
        {
            if (_count == 0)
            {
                item = default(BuilderItem);
                return false;
            }
            item = Queue[_first];
            _first = (_first + 1) % QueueMost;
            _count--;
            return true;
        }

        public static int Waiting => _count;

        public static void Clear()
        {
            _first = 0;
            _count = 0;
        }

        /// <summary>A piece as the tape's event keeps it: B is the kind and the weight.</summary>
        public static int Packed(BuilderNewsKind kind, int weight)
        {
            return (int)kind * 16 + Math.Max(1, Math.Min(WeightMost, weight));
        }

        public static BuilderNewsKind KindOf(int packed)
        {
            int k = packed / 16;
            return k > 0 && k < (int)BuilderNewsKind.Count ? (BuilderNewsKind)k : BuilderNewsKind.None;
        }

        public static int WeightOf(int packed)
        {
            return Math.Max(1, Math.Min(WeightMost, packed % 16));
        }

        /// <summary>
        /// The mood's move in bp a piece of news gives the builder it names (<paramref name="named"/>) or every other listed
        /// builder, scaled by the company's size and held to <see cref="MoodMostBp"/>.
        /// </summary>
        public static int MoodBp(BuilderNewsKind kind, int weight, bool named, MarketSize size)
        {
            int w = Math.Max(1, Math.Min(WeightMost, weight));
            int bp;
            switch (kind)
            {
                case BuilderNewsKind.TenderWon:
                    bp = named ? WinBp * w * (Rivals - 1) / Rivals : -WinBp * w / Rivals;
                    break;
                case BuilderNewsKind.Finished:
                    bp = named ? SiteOnTimeBp * w : 0;
                    break;
                case BuilderNewsKind.Late:
                    bp = named ? SiteLateBp * w : 0;
                    break;
                case BuilderNewsKind.Developed:
                    bp = named ? DevelopedBp : DistrictBp;
                    break;
                case BuilderNewsKind.Bribery:
                    bp = named ? BriberyBp : 0;
                    break;
                case BuilderNewsKind.Strike:
                    bp = named ? StrikeBp : 0;
                    break;
                case BuilderNewsKind.Rigged:
                    bp = named ? RiggedBp : 0;
                    break;
                case BuilderNewsKind.Award:
                    bp = named ? AwardBp : 0;
                    break;
                default:
                    bp = 0;
                    break;
            }
            int s = Math.Max(0, Math.Min(SizeScalePct.Length - 1, (int)size));
            bp = bp * SizeScalePct[s] / 100;
            return bp > MoodMostBp ? MoodMostBp : bp < -MoodMostBp ? -MoodMostBp : bp;
        }

        /// <summary>Thousandths of its usual margin a piece of news moves for good: a scandal costs it, an award adds; else none.</summary>
        public static int MarginPermille(BuilderNewsKind kind)
        {
            switch (kind)
            {
                case BuilderNewsKind.Bribery: return BriberyMarginPermille;
                case BuilderNewsKind.Rigged: return RiggedMarginPermille;
                case BuilderNewsKind.Award: return AwardMarginPermille;
                default: return 0;
            }
        }

        /// <summary>True when a piece of news moves the listed builders it does not name too (a tender's losers, the district's growth).</summary>
        public static bool MovesOthers(BuilderNewsKind kind)
        {
            return kind == BuilderNewsKind.TenderWon || kind == BuilderNewsKind.Developed;
        }

        /// <summary>A listed builder's own news at a close from a draw of 0 to 1: a bribe coming out, a strike, or none.</summary>
        public static BuilderNewsKind Drawn(double draw, double tradingDaysYear)
        {
            double year = Math.Max(1.0, tradingDaysYear);
            if (draw < BriberyChanceYear / year)
            {
                return BuilderNewsKind.Bribery;
            }
            return draw < (BriberyChanceYear + StrikeChanceYear) / year ? BuilderNewsKind.Strike : BuilderNewsKind.None;
        }
    }
}
