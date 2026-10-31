using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>One building as the population measures it: what the game says of it, in the game's own figures, nothing of the game's types.</summary>
    public struct CityBuilding
    {
        public string Street;      // the game's street id ("ba:street_fifthavenue", or the mod's own)
        public int Number;
        public int District;       // its district's place in the city's table
        public Use Use;
        public int UnitSqm;        // m2 of the unit the game lets in a building of its size (BuildingSizeData.squareMeters); 0 where it gives none
        public int TotalSqm;       // m2 of its whole floor (Building.totalSqm)
        public bool Villa;         // a Hamptons villa (size T): one household, however large
        public string Special;     // a special service's business type, or null
        public string Business;    // the business type of the registration in it now, or null for none
        public int Traffic;        // the game's traffic index, 0 to 100
        public int Rent;           // cents a day: the game's market rent of one of its units (Building.GetBuildingDailyMarketRent); 0 to work it out from the district
        public float X, Z;         // m: where it stands
    }

    /// <summary>One district of the city as the population knows it: its id and what the game's data says of its people.</summary>
    public sealed class PopDistrict
    {
        public string Id;
        public int Working, Middle, Upper;   // per cent of its people by class (NeighborhoodData)
        public int RealEstate = 1000;        // thousandths: the game's real estate multiplier, which its rents follow
        public int DemandWeight = 500;       // thousandths: the game's customer demands weight
        public int Pull;                     // its shops' room for customers times that weight (the census's volume)
        public float X, Z;                   // m: its middle
        public bool Villas;                  // the Hamptons: villas, and nearly everyone has a car
    }

    /// <summary>A district's measure: its homes, people and jobs as the buildings give them.</summary>
    public sealed class DistrictMeasure
    {
        public int Buildings, HomeBuildings, Flats, Villas;
        public int FlatsAboveShops;               // of its flats, those above its shops
        public long FlatSqm;                      // m2 of all its flats but its houses, net
        public int Jobs, OfficeJobs, ShopJobs, WarehouseJobs, VenueJobs, SpecialJobs, BuildingJobs;
        public int OfficeSqm, ShopSqm, WarehouseSqm;
        public int Households, People, Workers;   // filled in once a city is generated

        public void Add(DistrictMeasure o)
        {
            Buildings += o.Buildings;
            HomeBuildings += o.HomeBuildings;
            Flats += o.Flats;
            Villas += o.Villas;
            FlatsAboveShops += o.FlatsAboveShops;
            FlatSqm += o.FlatSqm;
            Jobs += o.Jobs;
            OfficeJobs += o.OfficeJobs;
            ShopJobs += o.ShopJobs;
            WarehouseJobs += o.WarehouseJobs;
            VenueJobs += o.VenueJobs;
            SpecialJobs += o.SpecialJobs;
            BuildingJobs += o.BuildingJobs;
            OfficeSqm += o.OfficeSqm;
            ShopSqm += o.ShopSqm;
            WarehouseSqm += o.WarehouseSqm;
            Households += o.Households;
            People += o.People;
            Workers += o.Workers;
        }
    }

    /// <summary>
    /// The city measured from its buildings (POPULATION.md section 2; the brief's first step; the owner's bigger city of 1 Nov 2026): flats of a real
    /// size in each residential building (the game's own unit is its first flat, and the rest of its floor times <see cref="PopulationCatalogue.HomeNetShare"/>
    /// is flats of its district's size; a Hamptons house is one) and in the floors above a shop (a share of them by district: most of New York's streets
    /// of shops have homes above); and jobs in each building (offices a desk for every <see cref="PopulationCatalogue.DeskSqm"/> m2 of their net floor,
    /// a shop by its sales floor and kind, warehouses by floor, venues by kind, the special services by their kind's density, a residential building's
    /// own staff by the game's units). Game-free (EconomyCheck's PopulationChecks measures Build 3682's city with it); at a load the same rule reads
    /// each building's floor from the game, so Uptown's and Cedar Heights' are measured then, their flats by their classes.
    /// COST: nothing per frame; a few integer operations a building.
    /// </summary>
    public static class MeasureRules
    {
        /// <summary>
        /// The flats of a building in its district: a residential building's first is the game's own unit and the rest of its net floor is flats of the
        /// district's size (a house, a Hamptons one, is the one); a shop's are the district's share of its floor above the shop; none in any other.
        /// </summary>
        public static int Flats(in CityBuilding b, PopDistrict d)
        {
            switch (b.Use)
            {
                case Use.Homes:
                    if (House(b, d))
                    {
                        return 1;
                    }
                    long net = (long)Math.Max(0, b.TotalSqm) * PopulationCatalogue.HomeNetShare / 1000;
                    return 1 + (int)Math.Max(0L, (net - GameUnitSqm(b)) / FlatNet(d));
                case Use.Shop:
                    long above = (long)Math.Max(0, b.TotalSqm - Math.Max(0, b.UnitSqm)) * HomesAboveShops(d) / 1000 * PopulationCatalogue.HomeNetShare / 1000;
                    return (int)(above / FlatNet(d));
                default:
                    return 0;
            }
        }

        /// <summary>The m2 of the building's flats (its first flat, the game's own unit, has <see cref="GameUnitSqm"/>): a house's whole, else its district's.</summary>
        public static int FlatSqm(in CityBuilding b, PopDistrict d)
        {
            return b.Use == Use.Homes && House(b, d) ? GameUnitSqm(b) : FlatNet(d);
        }

        /// <summary>A house: a Hamptons villa, or any home in a district of houses; one household, in the game's own unit.</summary>
        public static bool House(in CityBuilding b, PopDistrict d)
        {
            return b.Use == Use.Homes && (b.Villa || (d != null && d.Villas));
        }

        /// <summary>The m2 of the game's own unit in a residential building (BuildingSizeData.squareMeters; a villa's whole floor).</summary>
        public static int GameUnitSqm(in CityBuilding b)
        {
            return b.Villa ? Math.Max(b.UnitSqm, b.TotalSqm) : b.UnitSqm > 0 ? b.UnitSqm : PopulationCatalogue.FlatSqmDefault;
        }

        /// <summary>The game's own units a residential building's floor holds (four fifths of it net), its building staff's measure.</summary>
        public static int GameUnits(in CityBuilding b)
        {
            return b.Use != Use.Homes ? 0 : b.Villa ? 1 : Math.Max(1, (int)((long)Math.Max(0, b.TotalSqm) * 800 / 1000 / GameUnitSqm(b)));
        }

        /// <summary>The net m2 of a flat in a district: the catalogue's for the game's districts, else its classes' mix (Uptown, Cedar Heights).</summary>
        public static int FlatNet(PopDistrict d)
        {
            int i = HomesIndex(d);
            int sqm = i >= 0 ? PopulationCatalogue.DistrictHomes[i].FlatSqm : 0;
            if (sqm <= 0 && d != null && d.Working + d.Middle + d.Upper > 0)
            {
                int[] c = PopulationCatalogue.ClassFlatSqm;
                sqm = (d.Working * c[0] + d.Middle * c[1] + d.Upper * c[2]) / (d.Working + d.Middle + d.Upper);
            }
            return Math.Max(PopulationCatalogue.FlatSqmLeast, sqm > 0 ? sqm : PopulationCatalogue.ClassFlatSqm[1]);
        }

        /// <summary>The thousandths of a shop building's floor above its shop that is homes in a district.</summary>
        public static int HomesAboveShops(PopDistrict d)
        {
            int i = HomesIndex(d);
            return i >= 0 ? PopulationCatalogue.DistrictHomes[i].HomesAboveShops : PopulationCatalogue.HomesAboveShopsDefault;
        }

        private static int HomesIndex(PopDistrict d)
        {
            for (int i = 0; d != null && i < PopulationCatalogue.DistrictHomes.Length; i++)
            {
                if (string.Equals(PopulationCatalogue.DistrictHomes[i].District, d.Id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The jobs in a building, by its use and, for a shop, the kind of business in it now.</summary>
        public static int Jobs(in CityBuilding b)
        {
            int sqm = Math.Max(0, b.TotalSqm);
            switch (b.Use)
            {
                case Use.Office:
                    return (int)((long)sqm * PopulationCatalogue.OfficeNetShare / 1000 / PopulationCatalogue.DeskSqm);
                case Use.Shop:
                    return ShopJobs(b.UnitSqm > 0 ? b.UnitSqm : sqm, b.Business);
                case Use.Warehouse:
                    return Math.Max(PopulationCatalogue.ShopJobsLeast, sqm / PopulationCatalogue.WarehouseSqmPerJob);
                case Use.Cinema:
                    return PopulationCatalogue.CinemaJobs;
                case Use.Theatre:
                    return PopulationCatalogue.TheatreJobs;
                case Use.Special:
                    return Math.Max(PopulationCatalogue.SpecialJobsLeast, sqm / SpecialSqmPerJob(b.Special));
                case Use.Homes:
                    return GameUnits(b) / PopulationCatalogue.UnitsPerStaffJob;
                default:
                    return 0;
            }
        }

        /// <summary>The jobs of a shop's sales floor of so many m2 for a business type (the average shop's where the type is not known).</summary>
        public static int ShopJobs(int shopSqm, string businessType)
        {
            int i = PopulationCatalogue.TypeIndex(businessType);
            int per = i >= 0 && PopulationCatalogue.Types[i].SqmPerJob > 0 ? PopulationCatalogue.Types[i].SqmPerJob : PopulationCatalogue.ShopSqmPerJob;
            return Math.Max(PopulationCatalogue.ShopJobsLeast, Math.Max(0, shopSqm) / per);
        }

        /// <summary>The m2 a job at a special service of a business type.</summary>
        public static int SpecialSqmPerJob(string type)
        {
            int i = SpecialIndex(type);
            return i >= 0 ? PopulationCatalogue.SpecialDensity[i].SqmPerJob : PopulationCatalogue.SpecialSqmPerJobDefault;
        }

        /// <summary>The field of most of a building's jobs.</summary>
        public static Field JobField(in CityBuilding b)
        {
            switch (b.Use)
            {
                case Use.Office:
                    return OfficeField(b.Business);
                case Use.Shop:
                case Use.Cinema:
                case Use.Theatre:
                    return Field.Service;
                case Use.Warehouse:
                    return Field.Trades;
                case Use.Special:
                    int i = SpecialIndex(b.Special);
                    return i >= 0 ? PopulationCatalogue.SpecialDensity[i].Field : Field.Service;
                case Use.Homes:
                    return Field.Service;
                default:
                    return Field.None;
            }
        }

        /// <summary>The field of an office business type: the professions' offices are professional, the agencies creative, an empty floor office work.</summary>
        public static Field OfficeField(string type)
        {
            switch (type)
            {
                case "ba:businesstype_lawfirm":
                    return Field.Professional;
                case "ba:businesstype_webdevelopmentagency":
                    return Field.Tech;
                case "ba:businesstype_graphicdesigner":
                case "ba:businesstype_eventplanningagency":
                    return Field.Creative;
                case "ba:businesstype_headquarters":
                    return Field.Management;
                default:
                    return Field.Office;
            }
        }

        /// <summary>Who employs a special service's people: the public sector for the council's own places, else the service itself.</summary>
        public static EmployerKind SpecialKind(string type)
        {
            int i = SpecialIndex(type);
            return i >= 0 ? PopulationCatalogue.SpecialDensity[i].Kind : EmployerKind.Special;
        }

        private static int SpecialIndex(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return -1;
            }
            for (int i = 0; i < PopulationCatalogue.SpecialDensity.Length; i++)
            {
                if (string.Equals(PopulationCatalogue.SpecialDensity[i].Type, type, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The use of one of the game's building types; a special building is a special service whatever its size.</summary>
        public static Use UseOf(string buildingType, string special)
        {
            if (!string.IsNullOrEmpty(special))
            {
                return Use.Special;
            }
            switch (buildingType)
            {
                case "ba:buildingtype_residential": return Use.Homes;
                case "ba:buildingtype_retail": return Use.Shop;
                case "ba:buildingtype_office": return Use.Office;
                case "ba:buildingtype_warehouse": return Use.Warehouse;
                case "ba:buildingtype_cinema": return Use.Cinema;
                case "ba:buildingtype_theater": return Use.Theatre;
                case "ba:buildingtype_special": return Use.Special;
                default: return Use.None;
            }
        }

        /// <summary>Each district's measure, and the city's in <paramref name="city"/>; one row a district of the table, in its order.</summary>
        public static DistrictMeasure[] Measure(IList<CityBuilding> buildings, IList<PopDistrict> districts, DistrictMeasure city)
        {
            var rows = new DistrictMeasure[districts != null ? districts.Count : 0];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = new DistrictMeasure();
            }
            for (int i = 0; buildings != null && i < buildings.Count; i++)
            {
                CityBuilding b = buildings[i];
                if (b.District < 0 || b.District >= rows.Length)
                {
                    continue;
                }
                DistrictMeasure r = rows[b.District];
                PopDistrict d = districts[b.District];
                r.Buildings++;
                int jobs = Jobs(b);
                r.Jobs += jobs;
                int flats = Flats(b, d);
                r.Flats += flats;
                r.FlatSqm += House(b, d) ? 0 : b.Use == Use.Homes && flats > 0 ? GameUnitSqm(b) + (long)(flats - 1) * FlatSqm(b, d) : (long)flats * FlatSqm(b, d);
                switch (b.Use)
                {
                    case Use.Homes:
                        r.HomeBuildings++;
                        r.Villas += House(b, d) ? 1 : 0;
                        r.BuildingJobs += jobs;
                        break;
                    case Use.Office:
                        r.OfficeJobs += jobs;
                        r.OfficeSqm += Math.Max(0, b.TotalSqm);
                        break;
                    case Use.Shop:
                        r.ShopJobs += jobs;
                        r.ShopSqm += Math.Max(0, b.UnitSqm > 0 ? b.UnitSqm : b.TotalSqm);
                        r.FlatsAboveShops += flats;
                        break;
                    case Use.Warehouse:
                        r.WarehouseJobs += jobs;
                        r.WarehouseSqm += Math.Max(0, b.TotalSqm);
                        break;
                    case Use.Cinema:
                    case Use.Theatre:
                        r.VenueJobs += jobs;
                        break;
                    case Use.Special:
                        r.SpecialJobs += jobs;
                        break;
                }
            }
            if (city != null)
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    city.Add(rows[i]);
                }
            }
            return rows;
        }
    }
}
