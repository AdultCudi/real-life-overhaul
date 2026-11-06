using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>How a car park is let. Saved as a number.</summary>
    public enum CarParkMode
    {
        Public = 0,   // pay parking by the hour to anyone
        Lease = 1     // bays leased by the month to businesses and rivals nearby
    }

    /// <summary>Who owns a car park. Saved as a number.</summary>
    public enum CarParkOwner
    {
        Player = 0,
        Builder = 1   // one of the city's builders' development arms (the rival's index)
    }

    /// <summary>Where a car park stands. Saved as a number.</summary>
    public enum CarParkStage
    {
        Plans = 0,      // a developer's plans are being drawn (the city's lots only)
        Planning = 1,   // at City Hall
        Building = 2,   // the works on the programme's site
        Open = 3,
        Closed = 4      // taken away: its land is free again
    }

    /// <summary>
    /// Car parks (docs/features/construction.md, "Parking lots"): the owner, 27 Oct 2026, "Give the ability to build parking lots and
    /// let it for use. In build mode they must be able to build parking lots. It must finish fast and it can be let out for rivals or
    /// the public." Everything a car park is that the game does not decide, game-free: the plan of its bays, its works (a day of
    /// plans, a day at City Hall, one to three days of building), its cost by its area, where it may stand, the price band and how
    /// full it is by the hour, what it takes, the leases offered and paid, and when the city's builders build one. Every draw is from
    /// the save's seed and the car park's own number, so each is the same on every machine. COST: a few comparisons a call.
    /// </summary>
    public static partial class CarParkRules
    {
        // ------------------------------------------------------------------ the plan's figures

        public const float BayWidth = 2.5f;          // m a bay is wide, side by side along its row
        public const float BayDepth = 5f;            // m a bay is deep: a car and a little
        public const float AisleWidth = 6f;          // m an aisle between rows: two ways
        public const float ConnectorWidth = 6.5f;    // m the way in from the street to the aisles: two lanes
        public const float ModuleDepth = 16f;        // m: a row, an aisle and a row
        public const float Edge = 0.6f;              // m kept round the site for its kerb
        public const float KerbWidth = 0.3f;         // m the kerb round the site is wide
        public const float LineWidth = 0.1f;         // m a bay's divider is wide
        public const float DashLength = 1.5f;        // m a middle dash is long, and the gap after it
        public const float DashWidth = 0.12f;        // m a middle dash is wide
        public const float LampEvery = 16f;          // m along an aisle between its lamps
        public const float BlueSide = 1.2f;          // m the blue square of a bay for the disabled is
        public const int AccessibleEvery = 20;       // bays: one more for the disabled for each
        public const float PavementDepth = 3.65f;    // m the pavement the mouth crosses is deep, the streets' and the avenues' alike within a few cm

        // ------------------------------------------------------------------ where, how big

        public const int ColumnsLeast = 2, RowsLeast = 2;   // the squares a car park is laid on at the least: 14 by 14 m
        public const int BaysLeast = 6;                     // a site that holds fewer is not a car park
        public const int HomesBaysMost = 12;                // on land zoned for homes alone a car park is the residents', no bigger

        // ------------------------------------------------------------------ the works

        public const int AreaPerHour = 36;       // m2 of surface a working hour lays: with the least and most below, one to three working days
        public const int HoursLeast = 10, HoursMost = 30;
        public const int HoursPerDay = 10;       // working hours in a working day
        public const int PlansDays = 1;          // the practice draws a car park's plans in a day
        public const int PermitDays = 1;         // City Hall decides in a working day
        public const int PlansFeePermille = 10;  // of the estimate; a car park's plans are a drawing of lines
        public const int PlansFeeLeast = 400;    // money
        public static readonly int[] FirmQuality = { 70, 78, 86 };   // the plans' quality by practice, 0 to 100
        public const int WorksKind = 7;          // the programme's kind of site (Civic's SiteKind.Parking): the works tables' row

        /// <summary>Working hours a car park of this many square metres takes to build: 10 to 30, a day to three.</summary>
        public static int Hours(int area)
        {
            return BuildCatalogue.Clamp((Math.Max(0, area) + AreaPerHour - 1) / AreaPerHour, HoursLeast, HoursMost);
        }

        /// <summary>Working days of those hours.</summary>
        public static int WorkingDays(int hours)
        {
            return Math.Max(1, (hours + HoursPerDay - 1) / HoursPerDay);
        }

        /// <summary>The practice's terms for a car park's plans: a day, a fee of a hundredth of the estimate, the practice's own quality.</summary>
        public static PlansQuote Quote(int firm, long estimate)
        {
            int f = BuildCatalogue.Clamp(firm, 0, FirmQuality.Length - 1);
            return new PlansQuote { Fee = (int)Math.Min(int.MaxValue, Math.Max(PlansFeeLeast, Math.Max(0L, estimate) * PlansFeePermille / 1000L)), Days = PlansDays, Quality = FirmQuality[f] };
        }

        /// <summary>City Hall's answer: a car park is granted whenever the land is zoned for one, with no objections to weigh and no odds to draw.</summary>
        public static bool Permit(bool zoned, bool allowed)
        {
            return zoned && allowed;
        }

        // ------------------------------------------------------------------ zoning

        /// <summary>
        /// Whether a car park may stand on land zoned for these uses (bits of BuildUse) and why not (a text key): on land zoned for shops,
        /// offices or warehouses always; on land zoned for homes alone as the residents' own, up to <see cref="HomesBaysMost"/> bays.
        /// </summary>
        public static bool Allowed(int zones, int bays, out string why)
        {
            why = null;
            int commercial = BuildCatalogue.Zone(BuildUse.Shops) | BuildCatalogue.Zone(BuildUse.Offices) | BuildCatalogue.Zone(BuildUse.Warehouses);
            if ((zones & commercial) != 0)
            {
                return true;
            }
            if ((zones & BuildCatalogue.Zone(BuildUse.Homes)) != 0)
            {
                if (bays <= HomesBaysMost)
                {
                    return true;
                }
                why = "reallife_park_answer_homes_big";
                return false;
            }
            why = "reallife_park_answer_not_zoned";
            return false;
        }

        /// <summary>The use a car park is filed under in a plan: the commercial use its land is zoned for first, else homes.</summary>
        public static BuildUse UseOf(int zones)
        {
            if ((zones & BuildCatalogue.Zone(BuildUse.Shops)) != 0)
            {
                return BuildUse.Shops;
            }
            if ((zones & BuildCatalogue.Zone(BuildUse.Offices)) != 0)
            {
                return BuildUse.Offices;
            }
            if ((zones & BuildCatalogue.Zone(BuildUse.Warehouses)) != 0)
            {
                return BuildUse.Warehouses;
            }
            return BuildUse.Homes;
        }

        // ------------------------------------------------------------------ the price

        public const int PriceStep = 50;                         // cents: the price moves by this
        public const int PriceLeastPermille = 500, PriceMostPermille = 2500;   // of the district's street rate

        /// <summary>The district's street rate an hour in cents: what the game charges a car left in its streets, at least a dollar.</summary>
        public static int Fair(double districtDollars)
        {
            int cents = (int)Math.Round(Math.Max(0.0, Math.Min(10000.0, districtDollars)) * 100.0);
            return Math.Max(100, (cents + PriceStep / 2) / PriceStep * PriceStep);
        }

        public static int PriceLeast(int fair) => Math.Max(PriceStep, (int)((long)fair * PriceLeastPermille / 1000L) / PriceStep * PriceStep);

        public static int PriceMost(int fair) => Math.Max(PriceLeast(fair), (int)((long)fair * PriceMostPermille / 1000L) / PriceStep * PriceStep);

        /// <summary>A price put inside the band and on a step.</summary>
        public static int ClampPrice(int price, int fair)
        {
            int least = PriceLeast(fair), most = PriceMost(fair);
            int on = (Math.Max(0, price) + PriceStep / 2) / PriceStep * PriceStep;
            return BuildCatalogue.Clamp(on, least, most);
        }

        /// <summary>The price one step up (+1) or down (-1) from this one, kept in the band.</summary>
        public static int Stepped(int price, int fair, int direction)
        {
            return ClampPrice(price + (direction < 0 ? -PriceStep : direction > 0 ? PriceStep : 0), fair);
        }

        // ------------------------------------------------------------------ how full it is

        /// <summary>The hours of a weekday as thousandths of the day's busiest: people to work and to shop, a second peak at supper.</summary>
        public static readonly int[] WeekdayHours =
        {
            30, 20, 15, 15, 20, 40, 100, 300, 650, 900, 950, 950, 1000, 950, 900, 850, 850, 900, 850, 700, 600, 500, 400, 250
        };

        /// <summary>The hours of a weekend day: late to start, busy in the afternoon and at night.</summary>
        public static readonly int[] WeekendHours =
        {
            60, 40, 30, 25, 25, 30, 50, 100, 200, 350, 550, 750, 850, 900, 900, 900, 900, 900, 950, 1000, 1000, 900, 700, 450
        };

        public const int CarsPerTraffic = 40;       // hundredths of a car at the busiest hour for each point of the lot's traffic index (0 to 100)
        public const int CarsPerBusiness = 50;      // hundredths of a car for each business within reach
        public const int NearbyMost = 30;           // businesses counted
        public const int RivalWeight = 700;         // thousandths: a public bay of another car park within reach takes this much of a bay's share
        public const int PriceBase = 1600, PriceSlope = 600;   // the price's pull: 1600 less 600 times the price over the street rate; best near 1.33 times
        public const int PriceFactorLeast = 50, PriceFactorMost = 1500;   // thousandths

        /// <summary>The thousandths of the lot's custom a price keeps: 1000 at the street rate, 1300 at half of it, 400 at twice.</summary>
        public static int PriceFactor(int priceCents, int fairCents)
        {
            int ratio = (int)((long)Math.Max(0, priceCents) * 1000L / Math.Max(1, fairCents));
            return BuildCatalogue.Clamp(PriceBase - PriceSlope * ratio / 1000, PriceFactorLeast, PriceFactorMost);
        }

        /// <summary>Cars at the busiest hour that look for a bay round a lot: its traffic and the businesses nearby.</summary>
        public static int Catchment(int traffic, int nearby)
        {
            return (BuildCatalogue.Clamp(traffic, 0, 100) * CarsPerTraffic + BuildCatalogue.Clamp(nearby, 0, NearbyMost) * CarsPerBusiness + 50) / 100;
        }

        /// <summary>
        /// Bays taken at an hour: the catchment's cars at that hour of that kind of day, the district's share of people who drive, the
        /// price's pull and the market's level, shared with the other public bays within reach, and a car more or less by a draw
        /// from the car park's own seed when the hour is busy; never more than the bays.
        /// </summary>
        public static int Cars(int bays, int hour, bool weekend, int catchment, int rivalBays, int priceCents, int fairCents, int carShare, int level, int seed, int lot, int day)
        {
            int[] curve = weekend ? WeekendHours : WeekdayHours;
            long pm = (long)Math.Max(0, catchment) * 1000L;
            pm = pm * curve[BuildCatalogue.Clamp(hour, 0, 23)] / 1000L;
            pm = pm * BuildCatalogue.Clamp(carShare, 0, 1000) / 1000L;
            pm = pm * PriceFactor(priceCents, fairCents) / 1000L;
            pm = pm * BuildCatalogue.Clamp(level, 500, 1500) / 1000L;
            long mine = pm * bays * 1000L / (Math.Max(1, bays) * 1000L + Math.Max(0, rivalBays) * RivalWeight);
            int jitter = 0;
            if (mine >= 1500L)
            {
                BuildRandom random = BuildRandom.Of(seed, lot * 4099 + day, hour + 7);
                jitter = random.Range(-1, 2);
            }
            return BuildCatalogue.Clamp((int)((mine + 500L) / 1000L) + jitter, 0, Math.Max(0, bays));
        }

        public const int LeasedWeekday = 900, LeasedWeekend = 350;   // thousandths of the leased bays that hold a car at the busiest hour of a weekday and of a weekend day

        /// <summary>Cars standing in the bays that are leased out at an hour: the lessees' staff and customers, by the same hours' curve, fewer at the weekend.</summary>
        public static int LeaseCars(int leasedBays, int hour, bool weekend)
        {
            if (leasedBays <= 0)
            {
                return 0;
            }
            int[] curve = weekend ? WeekendHours : WeekdayHours;
            long pm = (long)curve[BuildCatalogue.Clamp(hour, 0, 23)] * (weekend ? LeasedWeekend : LeasedWeekday) / 1000L;
            return BuildCatalogue.Clamp((int)((leasedBays * pm + 500L) / 1000L), 0, leasedBays);
        }

        /// <summary>What an hour of those cars takes, in cents.</summary>
        public static long Takings(int cars, int priceCents)
        {
            return (long)Math.Max(0, cars) * Math.Max(0, priceCents);
        }

        /// <summary>The share of the game's cars left to park in the lane's bays, 0 to 100: the lane's chance of a free spot is its rest.</summary>
        public static int Occupied(int cars, int bays)
        {
            return bays <= 0 ? 0 : BuildCatalogue.Clamp((int)((long)Math.Max(0, cars) * 100L / bays), 0, 100);
        }

        // ------------------------------------------------------------------ running costs

        public const int UpkeepPublic = 22;     // money a bay a day at the start's prices: the attendant, the lights, the cleaning, the insurance
        public const int UpkeepLeased = 6;      // money a bay a day while it is leased out or kept for the month's lessees: the lights and the cleaning
        public const int UpkeepOwnLot = 6;      // money a bay a day of a lot kept by its owner and not let

        /// <summary>A day's upkeep of a car park, in money at today's prices (<paramref name="prices"/> in thousandths of the start's).</summary>
        public static long Upkeep(int bays, CarParkMode mode, int prices)
        {
            return (long)Math.Max(0, bays) * (mode == CarParkMode.Public ? UpkeepPublic : UpkeepLeased) * Math.Max(1, prices) / 1000L;
        }

        // ------------------------------------------------------------------ leases

        public const int LeaseDays = 30;                    // a lease runs in terms of a month
        public const int LeaseShare = 170;                  // thousandths of a bay's takings at the street rate round the clock for the month
        public const int LeaseJitterLeast = 900, LeaseJitterMost = 1100;   // thousandths: each offer's price about the usual
        public const int LeaseBaysLeast = 2, LeaseBaysMost = 10;
        public const int OfferDays = 7;                     // an offer stands a week
        public const int OffersMost = 3;                    // offers waiting at a car park at once
        public const int LeasesMost = 12;                   // leases at a car park at once
        public const int ReachMetres = 160;                 // m: the businesses that look to a car park for bays
        public const int RivalReachMetres = 250;            // m: the other public bays that share its custom
        public const int ChanceLeast = 120, ChanceMost = 650;   // thousandths a week a business within reach asks, by its weight and the lot's traffic

        /// <summary>A bay's usual lease for a month, in cents: the share of what a bay would take round the clock at the street rate.</summary>
        public static long LeaseMonthly(int fairCents)
        {
            long cents = (long)Math.Max(0, fairCents) * 24L * LeaseDays * LeaseShare / 1000L;
            return cents / 100L * 100L;
        }

        /// <summary>One offer's price for a bay a month in cents: the usual, 90% to 110% of it by the draw.</summary>
        public static long LeaseOffer(int fairCents, int seed, int lot, int week, int who)
        {
            BuildRandom random = BuildRandom.Of(seed, lot * 131 + week, who * 7 + 11);
            return LeaseMonthly(fairCents) * random.Range(LeaseJitterLeast, LeaseJitterMost + 1) / 1000L / 100L * 100L;
        }

        /// <summary>The thousandths of a week a business near a car park asks it for bays: by its weight (1 to 10) and the lot's traffic.</summary>
        public static int AskChance(int weight, int traffic)
        {
            return BuildCatalogue.Clamp(100 + BuildCatalogue.Clamp(weight, 1, 10) * 40 + BuildCatalogue.Clamp(traffic, 0, 100) * 4, ChanceLeast, ChanceMost);
        }

        /// <summary>The bays a business asks for, by its weight, and no more than the car park has to give.</summary>
        public static int OfferBays(int weight, int free, int seed, int lot, int week, int who)
        {
            BuildRandom random = BuildRandom.Of(seed, lot * 37 + week, who * 13 + 5);
            int asked = BuildCatalogue.Clamp(weight / 2 + 1 + random.Range(0, 3), LeaseBaysLeast, LeaseBaysMost);
            return Math.Min(asked, Math.Max(0, free));
        }

        /// <summary>The unused part of a month paid back when a lease ends before its term: the days left of the month's price.</summary>
        public static long Refund(long monthlyCents, int bays, int daysLeft)
        {
            return monthlyCents * Math.Max(0, bays) * BuildCatalogue.Clamp(daysLeft, 0, LeaseDays) / LeaseDays;
        }

        // ------------------------------------------------------------------ the player's own cars, and plans

        /// <summary>What a car left in a lot of its owner's saved on the game's street rate for the hours it stood there, in money.</summary>
        public static long OwnFee(int hours, double districtDollars)
        {
            return (long)Math.Round(Math.Max(0, hours) * Math.Max(0.0, districtDollars));
        }

        public const int NearMetres = 150;      // m: a car park this close to a new building counts as the parking its plans ask for

        // ------------------------------------------------------------------ the city's builders

        public const int NpcFirstDay = 12;                  // the game day from which the city's builders may begin one
        public const int NpcGapDays = 10;                   // days between two of theirs beginning in a district
        public const int NpcMostPerDistrict = 3;            // car parks of the city's in a district at most
        public const int NpcUnderWayMost = 1;               // under way in a district at once
        public const int NpcPressureLeast = 50;             // the district's demand for shops and offices, 0 to 100, below which nobody builds one
        public const int NpcBaysPerPoint = 3;               // the public bays a district's pressure asks for, for each point of it
        public const int NpcRoom = 24;                      // bays: the supply must fall this far short of the wish before another is begun
        public const int NpcBusyPermille = 550;             // the public lots' average fill, thousandths, from which another is wanted
        public const int NpcLeasingIn = 3;                  // one of the city's lots in this many lets leases instead of the hour

        /// <summary>Whether the city's builders want a car park in a district: its pressure, the public bays it has and how full they are.</summary>
        public static bool NpcWants(int pressure, int publicBays, int lots, int fillPermille, int underWay)
        {
            if (pressure < NpcPressureLeast || underWay >= NpcUnderWayMost || lots >= NpcMostPerDistrict)
            {
                return false;
            }
            return publicBays + NpcRoom <= pressure * NpcBaysPerPoint && (lots == 0 || fillPermille >= NpcBusyPermille);
        }

        /// <summary>Whether one of the city's lots lets leases and not the hour, from the save's seed and the lot's number.</summary>
        public static bool NpcLeases(int seed, int lot)
        {
            return BuildRandom.Of(seed, lot, 211).Range(0, NpcLeasingIn) == 0;
        }

        /// <summary>The columns a builder takes of a free run for a car park: two to four, by the draw, of the run.</summary>
        public static int NpcColumns(int seed, int day, int run)
        {
            int most = Math.Min(4, run);
            return most < ColumnsLeast ? 0 : BuildRandom.Of(seed, day, 223).Range(ColumnsLeast, most + 1);
        }

        /// <summary>The fill of a day as a share of the day's cars for a quick figure on the panel: what the hours add up to against the bays all day.</summary>
        public static int DayFill(int[] carsByHour, int bays)
        {
            if (carsByHour == null || bays <= 0)
            {
                return 0;
            }
            long total = 0;
            int hours = Math.Min(24, carsByHour.Length);
            for (int h = 0; h < hours; h++)
            {
                total += Math.Max(0, carsByHour[h]);
            }
            return (int)(total * 1000L / ((long)bays * Math.Max(1, hours)));
        }
    }
}
