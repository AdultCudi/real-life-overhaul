using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>One lot of the new district as its lots plan gives it (Scripts/Uptown, Lots), read through the wiring. Game-free.</summary>
    public struct LotView
    {
        public string Id;          // stable on every machine and every load
        public string Street;      // the address a building there has
        public int Number;
        public float X, Y, Z;      // m: its middle on the ground
        public int Yaw;            // degrees: its front looks out along -Z turned by this, as the sites' plots take it
        public int Width, Depth;   // m: along the street and back from it
        public int Class;          // small, office, big, warehouse
        public bool First;         // in the first block
        public bool Open;          // its ground is made at this load
        public int Traffic;        // the traffic index its building gets, 0 to 100
        public int Block;          // the block of the district it is in (row * 100 + column): the player's "lot", which holds many buildings
        public int District;       // which of the band's two districts the block is in (0 Uptown, 1 Cedar Heights): each has its own demand, market, rents and growth
        public string DistrictKey; // that district's name, as a locale key
        public int Strip;          // its strip of spaces along one side of the block, and its first space along it
        public int Index;
        public int Spaces;         // the spaces it takes: a site of several spaces bought side by side takes them all
        public string Size;        // the game's size planned on it, or null for a free space the construction game sells
        public bool StreetLow;     // a cross street runs past its strip's low end (west or south): the block's corner there (Uptown's LayoutRules.StreetEnds)
        public bool StreetHigh;    // ... past its high end (east or north)
        public float EdgeLow;      // m on its strip's axis: the block's edge at the low end, the cross street's building line
        public float EdgeHigh;     // ... at the high end
    }

    /// <summary>
    /// The land: a lot's price from the game's own value of a building (Building.GetMarketPricePerSqm), its zoning and the
    /// council's odds of changing it, the land taxes, and a home's flats. Game-free.
    /// </summary>
    public static class LandRules
    {
        /// <summary>
        /// The game's market price a square metre of a building: the district's price (its base price times its real estate
        /// multiplier), the type's multiplier, and the traffic index, as Building.GetMarketPricePerSqm works it.
        /// </summary>
        public static double PricePerSqm(double districtPrice, double typeMultiplier, int traffic, bool lowerForTraffic)
        {
            double price = districtPrice * typeMultiplier;
            if (lowerForTraffic)
            {
                double cut = price * Math.Min(traffic - 30, 0) / 100.0;
                return price - cut;
            }
            return price * (1.3 - (100 - traffic) / 100.0);
        }

        /// <summary>A building's value: its floor area at that price, as Building.GetMarketValue works it.</summary>
        public static long Value(int totalSqm, double pricePerSqm)
        {
            return totalSqm <= 0 || pricePerSqm <= 0.0 ? 0L : (long)Math.Round(totalSqm * pricePerSqm);
        }

        /// <summary>The floor area of a lot's usual building: its area times its class's plot ratio.</summary>
        public static int UsualArea(int width, int depth, int lotClass)
        {
            int c = BuildCatalogue.Clamp(lotClass, 0, BuildCatalogue.ClassCount - 1);
            return (int)((long)Math.Max(0, width) * Math.Max(0, depth) * BuildCatalogue.PlotRatioPermille[c] / 1000L);
        }

        /// <summary>What a lot costs: a share of the value of its usual building at the price a square metre given, in thousands.</summary>
        public static long LotPrice(int width, int depth, int lotClass, double pricePerSqm)
        {
            long value = Value(UsualArea(width, depth, lotClass), pricePerSqm);
            long price = value * BuildCatalogue.LandSharePermille / 1000L;
            long round = BuildCatalogue.LandRound;
            return Math.Max(round, (price + round / 2) / round * round);
        }

        /// <summary>
        /// The frame's footprint a plot of a lot's size has, as the city's construction programme lays the plot out (Civic's
        /// PlotRules.Frame: a narrow plot keeps a metre each side and its yard behind, a wider one its margins and its yard
        /// beside the frame); the checks hold the two together.
        /// </summary>
        public static void Frame(int width, int depth, out float frameWidth, out float frameDepth)
        {
            bool narrow = width < BuildCatalogue.WideFrom;
            frameWidth = Math.Max(0f, narrow ? width - 2f * BuildCatalogue.NarrowSide : width - BuildCatalogue.FrameMargin - BuildCatalogue.FrameYard);
            frameDepth = Math.Max(0f, narrow ? depth - BuildCatalogue.FrameMargin - BuildCatalogue.NarrowYard : depth - 2f * BuildCatalogue.FrameMargin);
        }

        /// <summary>The storeys a plot's frame shows of a building's: a narrow plot's at most six, a wider one's at most ten.</summary>
        public static int FrameStoreys(int width, int storeys)
        {
            return BuildCatalogue.Clamp(storeys, 1, width < BuildCatalogue.WideFrom ? BuildCatalogue.NarrowFrameStoreys : BuildCatalogue.FrameStoreysMost);
        }

        /// <summary>The uses a lot of a class is zoned for to begin with (bits of <see cref="BuildUse"/>).</summary>
        public static int ZonesOf(int lotClass)
        {
            return BuildCatalogue.ClassZones[BuildCatalogue.Clamp(lotClass, 0, BuildCatalogue.ClassCount - 1)];
        }

        public static bool Zoned(int zones, BuildUse use)
        {
            return (zones & BuildCatalogue.Zone(use)) != 0;
        }

        /// <summary>
        /// The council's odds, in thousandths, of adding a use to a lot: the use and the class's own, the permits policy (-1
        /// to 1 steps) and the company's reputation.
        /// </summary>
        public static int ZoneOdds(BuildUse use, int lotClass, int permits, int reputation)
        {
            int u = BuildCatalogue.Clamp((int)use, 0, (int)BuildUse.Count - 1), c = BuildCatalogue.Clamp(lotClass, 0, BuildCatalogue.ClassCount - 1);
            int odds = BuildCatalogue.ZoneOdds[u][c] + permits * BuildCatalogue.PermitsPermille + (reputation - 50) * 2;
            return BuildCatalogue.Clamp(odds, 50, 950);
        }

        /// <summary>The council's meeting that hears an application made on a day: its weekday, at least a few days on.</summary>
        public static int CouncilDay(int day)
        {
            int d = day + BuildCatalogue.ZoneDaysLeast;
            while (Weekday(d) != BuildCatalogue.CouncilWeekday)
            {
                d++;
            }
            return d;
        }

        /// <summary>0 for a Monday to 6 for a Sunday (the game's day 1 is a Monday).</summary>
        public static int Weekday(int day)
        {
            return ((day - 1) % 7 + 7) % 7;
        }

        /// <summary>
        /// The land tax on a lot for some days: a share of its price a year, and more on land kept empty, the surcharge as the
        /// permits policy (-1 to 1) in force sets it.
        /// </summary>
        public static int LandTax(long price, int days, int daysPerYear, bool empty, int permits = 0)
        {
            if (price <= 0 || days <= 0 || daysPerYear <= 0)
            {
                return 0;
            }
            int permille = BuildCatalogue.LandTaxPermille + (empty ? VacantTax(permits) : 0);
            return (int)Math.Min(int.MaxValue, price * permille * days / (1000L * daysPerYear));
        }

        /// <summary>The surcharge a year on land kept empty, in thousandths of its price, under the permits policy in force.</summary>
        public static int VacantTax(int permits)
        {
            return BuildCatalogue.VacantTaxPermille * CivicCatalogue.VacantPermille(permits) / 1000;
        }

        /// <summary>The uses a lot may be built for: its own zoning (and changes granted for it) and what the council allowed for its whole class.</summary>
        public static int ZonesWith(int lotZones, int classAdded)
        {
            return (lotZones | classAdded) & ((1 << (int)BuildUse.Count) - 1);
        }

        /// <summary>True when land counts as empty: held long enough with no works under way.</summary>
        public static bool Empty(int emptySince, int day)
        {
            return emptySince > 0 && day - emptySince >= BuildCatalogue.VacantAfter;
        }

        /// <summary>How many flats a block of homes of a floor area holds.</summary>
        public static int Flats(int area)
        {
            return Math.Max(BuildCatalogue.FlatsLeast, area / BuildCatalogue.FlatArea);
        }

        /// <summary>A flat's price: its share of the building's value, at the asking price chosen, more or less by the work's quality.</summary>
        public static int FlatPrice(long value, int flats, int quality, int ask)
        {
            if (flats <= 0 || value <= 0)
            {
                return 0;
            }
            int a = BuildCatalogue.Clamp(ask, 0, BuildCatalogue.AskPermille.Length - 1);
            long share = value / flats;
            long price = share * (BuildCatalogue.AskPermille[a] + (quality - 60) * BuildCatalogue.QualityPricePermille) / 1000L;
            return (int)Math.Min(int.MaxValue, Math.Max(0L, price));
        }

        /// <summary>A flat sold before it is built: a share of its price, with a deposit now and the rest at completion.</summary>
        public static int OffPlanPrice(long value, int flats, int quality)
        {
            long full = FlatPrice(value, flats, quality, 1);
            return (int)(full * BuildCatalogue.OffPlanPermille / 1000L);
        }

        public static int Deposit(int price)
        {
            return (int)((long)price * BuildCatalogue.DepositPermille / 1000L);
        }

        /// <summary>The chance a day, in thousandths, that a buyer takes a flat at the asking price chosen, by the company's reputation.</summary>
        public static int BuyerChance(int ask, int reputation)
        {
            int a = BuildCatalogue.Clamp(ask, 0, BuildCatalogue.AskChancePermille.Length - 1);
            return BuildCatalogue.Clamp(BuildCatalogue.AskChancePermille[a] + (reputation - 50) * 3, 50, 900);
        }
    }
}
