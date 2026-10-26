using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// A district's figures as the game's own data has them (NeighborhoodData, and BuildingTypeData's share of units kept
    /// empty to let), the ones Uptown takes from its two neighbours. Game-free.
    /// </summary>
    public struct DistrictFigures
    {
        public int Working;           // % of its people of the working class (CitizenHelper: who walks and buys there)
        public int Middle;            // % middle class
        public int Upper;             // % upper class
        public float RealEstate;      // realEstateMultiplier: a building's price per square metre, and so its rent (Building.GetMarketPricePerSqm)
        public float Parking;         // parkingPrice: dollars an hour for a car parked there (ParkingSimulator)
        public float Demand;          // customerDemandsWeight: how many customers come into a business (CustomerEntriesHelper)
        public int CarTraffic;        // vehicleTrafficDensityPercentage: the cars on its streets (TimeOfDayController)
        public int InteriorScore;     // minimumInteriorScore: the interior score its customers ask for
        public float BasePrice;       // baseBuildingPricePerSqm: dollars a square metre before the multipliers
        public float RentPerPrice;    // rentMultiplierPerSqmPrice: a day's rent of a square metre for a dollar of its price
        public float Marketing;       // marketingStrength: what a business's marketing is worth there
        public float ShutDownBelow;   // maxDailyIncomeToShutDownBusiness: dollars a day under which the game closes a business
        public bool Offices;          // hasOfficeBusinesses: offices' goods are in demand there
        public int EmptyOffices;      // % of its offices the game keeps empty to let (BuildingTypeData, office)
        public int EmptyShops;        // % of its shops (BuildingTypeData, retail)
    }

    /// <summary>
    /// Uptown's values as a district of the game, game-free (EconomyCheck's UptownLifeChecks): north of Manhattan, where
    /// Midtown and Hell's Kitchen end at the wall, so it is made of the two of them half and half, read from the game's
    /// own data at every load (<see cref="Blend"/>), with the figures of Build 3682 written down here for when the game's
    /// cannot be read and for the load's check that compares them. Also the class table and price index the game makes
    /// once a run for every district (CitizenHelper.Init), where a district's quad on the city map stands, its colour,
    /// and the price of its land. Every answer comes from the arguments alone: the same on every machine.
    /// COST: nothing per frame; a few sums at a load.
    /// </summary>
    public static class DistrictValues
    {
        public const string Id = "reallife_neighborhood_uptown";         // the district's id, never one of the game's, and its name's locale key
        public const string Midtown = "ba:neighborhood_midtown";
        public const string HellsKitchen = "ba:neighborhood_hellskitchen";
        public const string Global = "ba:neighborhood_global";
        public const string OfficeType = "ba:buildingtype_office";
        public const string RetailType = "ba:buildingtype_retail";

        public const float ZoneAlpha = 100f / 255f;  // the game's own zones' alpha on the city map: 100 of 255
        public const float ZoneRed = 0.30f;          // a green-blue no zone of the game's has: the widest gap between their hues (110 to 195 degrees)
        public const float ZoneGreen = 0.85f;
        public const float ZoneBlue = 0.57f;

        // The game's own price index of a class (CitizenHelper.Init): what each class pays at most, relative to the market.
        private const float WorkingIndex = 1.1999999f;
        private const float MiddleIndex = 1.4f;
        private const float UpperIndex = 1.6999999f;

        /// <summary>Midtown as Build 3682 has it (the game's NeighborhoodData and BuildingTypeData).</summary>
        public static readonly DistrictFigures Midtown3682 = new DistrictFigures
        {
            Working = 0, Middle = 6, Upper = 94, RealEstate = 2.2f, Parking = 15f, Demand = 0.9f, CarTraffic = 100, InteriorScore = 50,
            BasePrice = 20554f, RentPerPrice = 0.00056f, Marketing = 0.5f, ShutDownBelow = 850f, Offices = true, EmptyOffices = 15, EmptyShops = 6
        };

        /// <summary>Hell's Kitchen as Build 3682 has it.</summary>
        public static readonly DistrictFigures HellsKitchen3682 = new DistrictFigures
        {
            Working = 6, Middle = 82, Upper = 12, RealEstate = 1.3f, Parking = 7.5f, Demand = 0.7f, CarTraffic = 90, InteriorScore = 0,
            BasePrice = 20554f, RentPerPrice = 0.00056f, Marketing = 0.7f, ShutDownBelow = 700f, Offices = true, EmptyOffices = 20, EmptyShops = 10
        };

        /// <summary>
        /// Uptown from its two neighbours, half and half: each figure their mean, a share of people or of empty units to the
        /// nearest whole one (halves up), the working and upper classes so and the middle class the rest of 100, a price to
        /// six places (an equal figure kept exactly as it is).
        /// </summary>
        public static DistrictFigures Blend(DistrictFigures a, DistrictFigures b)
        {
            int working = Half(a.Working, b.Working);
            int upper = Half(a.Upper, b.Upper);
            int middle = Math.Max(0, 100 - working - upper);
            return new DistrictFigures
            {
                Working = working,
                Middle = middle,
                Upper = 100 - working - middle,
                RealEstate = Half(a.RealEstate, b.RealEstate),
                Parking = Half(a.Parking, b.Parking),
                Demand = Half(a.Demand, b.Demand),
                CarTraffic = Half(a.CarTraffic, b.CarTraffic),
                InteriorScore = Half(a.InteriorScore, b.InteriorScore),
                BasePrice = Half(a.BasePrice, b.BasePrice),
                RentPerPrice = Half(a.RentPerPrice, b.RentPerPrice),
                Marketing = Half(a.Marketing, b.Marketing),
                ShutDownBelow = Half(a.ShutDownBelow, b.ShutDownBelow),
                Offices = a.Offices || b.Offices,
                EmptyOffices = Half(a.EmptyOffices, b.EmptyOffices),
                EmptyShops = Half(a.EmptyShops, b.EmptyShops)
            };
        }

        /// <summary>Uptown as Build 3682's own figures make it: 3, 44 and 53 % of the classes, prices 1.75 times the base, and the rest.</summary>
        public static DistrictFigures Written => Blend(Midtown3682, HellsKitchen3682);

        private static int Half(int a, int b)
        {
            return (int)Math.Round((a + b) / 2.0, MidpointRounding.AwayFromZero);
        }

        private static float Half(float a, float b)
        {
            if (a == b)
            {
                return a;
            }
            return (float)Math.Round(((double)a + b) / 2.0, 6, MidpointRounding.AwayFromZero);
        }

        /// <summary>True when every figure is one the game can take: the classes add up to 100, prices and weights above 0.</summary>
        public static bool Sound(DistrictFigures f)
        {
            return f.Working >= 0 && f.Middle >= 0 && f.Upper >= 0 && f.Working + f.Middle + f.Upper == 100 && f.RealEstate > 0f && f.Parking >= 0f && f.Demand > 0f
                   && f.CarTraffic >= 0 && f.CarTraffic <= 100 && f.InteriorScore >= 0 && f.InteriorScore <= 100 && f.BasePrice > 0f && f.RentPerPrice > 0f
                   && f.Marketing > 0f && f.ShutDownBelow > 0f && f.EmptyOffices >= 0 && f.EmptyOffices <= 100 && f.EmptyShops >= 0 && f.EmptyShops <= 100;
        }

        /// <summary>True when the game's own figures for a neighbour are those of Build 3682 (the load's check says which changed).</summary>
        public static bool Same(DistrictFigures a, DistrictFigures b)
        {
            return a.Working == b.Working && a.Middle == b.Middle && a.Upper == b.Upper && a.RealEstate == b.RealEstate && a.Parking == b.Parking && a.Demand == b.Demand
                   && a.CarTraffic == b.CarTraffic && a.InteriorScore == b.InteriorScore && a.BasePrice == b.BasePrice && a.RentPerPrice == b.RentPerPrice
                   && a.Marketing == b.Marketing && a.ShutDownBelow == b.ShutDownBelow && a.Offices == b.Offices && a.EmptyOffices == b.EmptyOffices && a.EmptyShops == b.EmptyShops;
        }

        /// <summary>The price of a square metre of Uptown's land, as the game prices a building there before its type and traffic.</summary>
        public static float LandPerSqm(DistrictFigures f)
        {
            return f.BasePrice * f.RealEstate;
        }

        /// <summary>
        /// The game's own price index of a district's people (CitizenHelper.Init, its arithmetic in single precision): the most
        /// they pay relative to the market, from the shares of the classes.
        /// </summary>
        public static float PriceIndex(int working, int middle, int upper)
        {
            int all = working + middle + upper;
            if (all <= 0)
            {
                return MiddleIndex;
            }
            return (WorkingIndex * working + MiddleIndex * middle + UpperIndex * upper) / all;
        }

        /// <summary>
        /// The classes in the order the game keeps them for a district (CitizenHelper.Init: by share, largest first, those
        /// alike in their order working, middle, upper), each as 0 working, 1 middle, 2 upper with its share; into the
        /// caller's lists (cleared).
        /// </summary>
        public static void ClassOrder(int working, int middle, int upper, List<int> classes, List<float> shares)
        {
            classes.Clear();
            shares.Clear();
            int[] share = { working, middle, upper };
            for (int k = 0; k < 3; k++)
            {
                int at = classes.Count;
                while (at > 0 && share[classes[at - 1]] < share[k])
                {
                    at--;   // a stable sort, as OrderByDescending is: an equal share stays behind the earlier class
                }
                classes.Insert(at, k);
            }
            for (int i = 0; i < classes.Count; i++)
            {
                shares.Add(share[classes[i]]);
            }
        }

        /// <summary>
        /// Where a quad copied from one of the game's district zones stands, and its scale, to cover a rectangle of the world
        /// (corners in any order): the game's zones lie flat and unturned, scaled on X and Z, so the template's mesh extent in
        /// its own space (its mesh's bounds) is all it takes. False when the template's extent is empty.
        /// </summary>
        public static bool ZoneOver(float x1, float z1, float x2, float z2, float meshMinX, float meshMaxX, float meshMinZ, float meshMaxZ,
                                    out float x, out float z, out float scaleX, out float scaleZ)
        {
            x = z = scaleX = scaleZ = 0f;
            float spanX = meshMaxX - meshMinX, spanZ = meshMaxZ - meshMinZ;
            if (spanX <= 0f || spanZ <= 0f)
            {
                return false;
            }
            float minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2), minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);
            scaleX = (maxX - minX) / spanX;
            scaleZ = (maxZ - minZ) / spanZ;
            x = minX - meshMinX * scaleX;
            z = minZ - meshMinZ * scaleZ;
            return scaleX > 0f && scaleZ > 0f;
        }

        /// <summary>
        /// The south edge a district's quad over a rectangle (corners in any order) takes so it never lies over a zone of the
        /// game's south of it: a zone (its world extent, corners in order) across the rectangle's span west to east whose
        /// north edge reaches into the rectangle from the south moves the edge up to that north edge; else the rectangle's
        /// own south edge. Manhattan's zones reach past its old north wall, onto the first strip of Uptown's ground.
        /// </summary>
        public static float SouthClear(float x1, float z1, float x2, float z2, float zoneMinX, float zoneMaxX, float zoneMinZ, float zoneMaxZ)
        {
            float minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2), minZ = Math.Min(z1, z2), maxZ = Math.Max(z1, z2);
            bool across = zoneMaxX > minX && zoneMinX < maxX;
            bool reaches = zoneMinZ < minZ && zoneMaxZ > minZ && zoneMaxZ < maxZ;
            return across && reaches ? zoneMaxZ : minZ;
        }

        /// <summary>The figures as one line for the log, numbers in the invariant culture.</summary>
        public static string Describe(DistrictFigures f)
        {
            var c = CultureInfo.InvariantCulture;
            return f.Working + "/" + f.Middle + "/" + f.Upper + " % working, middle and upper class, real estate x" + f.RealEstate.ToString("0.###", c) + ", parking $"
                   + f.Parking.ToString("0.##", c) + " an hour, customers x" + f.Demand.ToString("0.###", c) + ", cars " + f.CarTraffic + " %, interior score "
                   + f.InteriorScore + ", marketing x" + f.Marketing.ToString("0.###", c) + ", closed under $" + f.ShutDownBelow.ToString("0", c) + " a day, "
                   + (f.Offices ? "offices' goods sold" : "no offices' goods") + ", " + f.EmptyOffices + " % of offices and " + f.EmptyShops + " % of shops kept to let";
        }
    }
}
