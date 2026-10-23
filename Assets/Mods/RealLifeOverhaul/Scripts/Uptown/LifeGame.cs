using System;
using System.Collections.Generic;
using System.Reflection;
using AI.Citizens;
using BigAmbitions.Neighborhoods;
using Buildings;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's side of the game, the only place the district touches it (with .Buildings, .Map and .Tests). Here: the
    /// game's record of a district (NeighborhoodData), made with ScriptableObject.CreateInstance from its two neighbours'
    /// own figures and put wherever the game finds a district: its private lookup (NeighborhoodHelper.GetData throws on an
    /// id it lacks), its list and its array of districts, an empty list of its buildings (CompetitionHelper.RunDaily reads
    /// AllNeighbourhoodBuildings for every district with stats and throws on one it lacks), its people's class table and
    /// price index and the wholesalers' table (both made by the game once a run, so Uptown's rows go in only when the game
    /// has made them and lacks it: CitizenHelper.Init would throw on a key put there before it), and its share of offices
    /// and shops kept empty to let (BuildingTypeData's per-district targets; without one the game keeps none empty). The
    /// game makes its lookups and lists of districts again at every load; what lasts a run (the tables, BuildingTypeData's
    /// arrays) is given back as the game had it when the city goes. Nothing here runs a frame.
    /// </summary>
    internal static partial class LifeGame
    {
        private static NeighborhoodData _record;   // Uptown's own record at this load, destroyed when the city goes
        private static readonly Dictionary<BuildingTypeData, IdealAvailableBuildingsInNeighborhood[]> TargetsWas =
            new Dictionary<BuildingTypeData, IdealAvailableBuildingsInNeighborhood[]>(2);   // each type's targets as the game had them
        private static bool _sharesPut, _pricePut, _wholesalePut;   // Uptown's rows this load put in the game's run-long tables
        private static readonly List<int> Classes = new List<int>(3);
        private static readonly List<float> Shares = new List<float>(3);

        /// <summary>Uptown's figures at this load: its neighbours' own, half and half, or Build 3682's when theirs cannot be read.</summary>
        public static DistrictFigures Figures { get; private set; } = DistrictValues.Written;

        /// <summary>True when <see cref="Figures"/> come from the game's own neighbours at this load.</summary>
        public static bool FiguresLive { get; private set; }

        /// <summary>The neighbours whose figures differ from Build 3682's (for the census line), or null when none do.</summary>
        public static string FiguresChanged { get; private set; }

        /// <summary>Uptown's record as the game has it at this load, or null.</summary>
        public static NeighborhoodData Record => _record;

        /// <summary>True when the game's own lookup finds Uptown's record (what the census test asks first).</summary>
        public static bool GetDataFinds()
        {
            var lookup = GameContract.DistrictLookup.Field?.GetValue(null) as Dictionary<string, NeighborhoodData>;
            return lookup != null && lookup.TryGetValue(DistrictValues.Id, out NeighborhoodData data) && data != null && data == _record;
        }

        /// <summary>True when Uptown is in the game's list of districts.</summary>
        public static bool Listed()
        {
            List<string> list = NeighborhoodHelper.Neighborhoods;
            return list != null && list.Contains(DistrictValues.Id);
        }

        // ---------------------------------------------------------------- the district in the game's lookups

        /// <summary>
        /// Uptown as a district the game knows, at the district save's early step (a save read, its city not yet built) or at
        /// the city load: its record in the game's lookup, list and array of districts, an empty list of its buildings, its
        /// rows in the run-long tables when the game has made them, its targets of units kept to let. Its row on the city
        /// map waits for its zone (<see cref="Map"/>). True when it is there; false with why when the game's lookups cannot
        /// be reached. Done once a load; a second call finds it there and changes nothing.
        /// </summary>
        public static bool Know(out string why)
        {
            why = null;
            List<string> list = NeighborhoodHelper.Neighborhoods;
            var lookup = GameContract.DistrictLookup.Field?.GetValue(null) as Dictionary<string, NeighborhoodData>;
            MethodInfo setArray = GameContract.DistrictArray.Setter;
            if (list == null || lookup == null || setArray == null || NeighborhoodHelper.NeighborhoodsData == null)
            {
                why = "the game's lookups of districts could not be reached (" + (GameContract.UptownDistrict.Why ?? "not loaded yet") + ")";
                return false;
            }
            if (lookup.TryGetValue(DistrictValues.Id, out NeighborhoodData there) && there != null && there != _record)
            {
                why = "the game, or another mod, has a district of the id " + DistrictValues.Id + " already";
                return false;
            }
            DistrictFigures f = ReadFigures(lookup);
            if (_record == null)
            {
                _record = ScriptableObject.CreateInstance<NeighborhoodData>();   // destroyed when the city goes (Unload)
                Shape(_record, f, lookup);
            }
            lookup[DistrictValues.Id] = _record;
            if (!list.Contains(DistrictValues.Id))
            {
                list.Add(DistrictValues.Id);
            }
            NeighborhoodData[] was = NeighborhoodHelper.NeighborhoodsData;
            if (Array.IndexOf(was, _record) < 0)
            {
                var now = new NeighborhoodData[was.Length + 1];
                Array.Copy(was, now, was.Length);
                now[was.Length] = _record;
                setArray.Invoke(null, new object[] { now });
            }
            if (!BuildingHelper.AllNeighbourhoodBuildings.ContainsKey(DistrictValues.Id))
            {
                BuildingHelper.AllNeighbourhoodBuildings[DistrictValues.Id] = new List<Building>();   // the game's daily pass reads it for every district with stats
            }
            PeopleRows(f);
            WholesaleRow();
            Targets(f);
            return true;
        }

        // The figures from the game's own neighbours, half and half; Build 3682's when either cannot be read.
        private static DistrictFigures ReadFigures(Dictionary<string, NeighborhoodData> lookup)
        {
            bool midtown = Figure(lookup, DistrictValues.Midtown, out DistrictFigures a);
            bool kitchen = Figure(lookup, DistrictValues.HellsKitchen, out DistrictFigures b);
            FiguresLive = midtown && kitchen;
            FiguresChanged = null;
            if (FiguresLive)
            {
                bool midtownChanged = !DistrictValues.Same(a, DistrictValues.Midtown3682), kitchenChanged = !DistrictValues.Same(b, DistrictValues.HellsKitchen3682);
                string changed = (midtownChanged ? "Midtown" : string.Empty) + (kitchenChanged ? (midtownChanged ? " and " : string.Empty) + "Hell's Kitchen" : string.Empty);
                FiguresChanged = changed.Length > 0 ? changed : null;
            }
            DistrictFigures f = FiguresLive ? DistrictValues.Blend(a, b) : DistrictValues.Written;
            if (!DistrictValues.Sound(f))
            {
                FiguresChanged = (FiguresChanged ?? "the game's") + " figures make no district the game can take, so Build 3682's are used";
                f = DistrictValues.Written;
            }
            Figures = f;
            return f;
        }

        private static bool Figure(Dictionary<string, NeighborhoodData> lookup, string id, out DistrictFigures f)
        {
            f = default;
            if (!lookup.TryGetValue(id, out NeighborhoodData d) || d == null)
            {
                return false;
            }
            f = new DistrictFigures
            {
                Working = d.workingClassPercentage, Middle = d.middleClassPercentage, Upper = d.upperClassClassPercentage, RealEstate = d.realEstateMultiplier,
                Parking = d.parkingPrice, Demand = d.customerDemandsWeight, CarTraffic = d.vehicleTrafficDensityPercentage, InteriorScore = d.minimumInteriorScore,
                BasePrice = d.baseBuildingPricePerSqm, RentPerPrice = d.rentMultiplierPerSqmPrice, Marketing = d.marketingStrength, ShutDownBelow = d.maxDailyIncomeToShutDownBusiness,
                Offices = d.hasOfficeBusinesses, EmptyOffices = Target(DistrictValues.OfficeType, id), EmptyShops = Target(DistrictValues.RetailType, id)
            };
            return true;
        }

        // The game's own target of a type's units kept empty in a district, as CompetitionHelper.GetIdealPercentageOfEmptyBuildings reads it (the district's
        // own entry; this is only ever asked for the two neighbours, which have one).
        private static int Target(string type, string district)
        {
            BuildingTypeData data = BuildingTypeHelper.GetData(type);
            IdealAvailableBuildingsInNeighborhood[] all = data != null ? data.idealAvailableBuildingsInNeighborhood : null;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (all[i] != null && all[i].neighbourhood == district)
                {
                    return all[i].idealAvailableBuildingsPercentage;
                }
            }
            return 0;
        }

        // Uptown's record, its row and its look on the city map (its zone's colour, Midtown's icon), its name for the game's business names.
        private static void Shape(NeighborhoodData d, DistrictFigures f, Dictionary<string, NeighborhoodData> lookup)
        {
            d.name = "RealLife Uptown";
            d.hideFlags = HideFlags.DontUnloadUnusedAsset;   // the engine's sweep of unused assets would take a record only the mod holds
            d.neighbourhood = DistrictValues.Id;
            d.hasCityMapFilter = false;   // until its zone is on the city map: the game's district rows ask the zone for their focus (CityMapNeighborhoodZones.First throws without one)
            d.cityMapIcon = lookup.TryGetValue(DistrictValues.Midtown, out NeighborhoodData midtown) && midtown != null ? midtown.cityMapIcon : null;
            d.cityMapZoneColor = new Color(DistrictValues.ZoneRed, DistrictValues.ZoneGreen, DistrictValues.ZoneBlue, DistrictValues.ZoneAlpha);
            Apply(d, f);
            string name = DistrictValues.Id.GetLocalization();
            d.aliases = new[] { string.IsNullOrEmpty(name) || name == DistrictValues.Id ? "Uptown" : name };   // the game names businesses after their district
        }

        private static void Apply(NeighborhoodData d, DistrictFigures f)
        {
            d.hasOfficeBusinesses = f.Offices;
            d.workingClassPercentage = f.Working;
            d.middleClassPercentage = f.Middle;
            d.upperClassClassPercentage = f.Upper;
            d.realEstateMultiplier = f.RealEstate;
            d.parkingPrice = f.Parking;
            d.customerDemandsWeight = f.Demand;
            d.vehicleTrafficDensityPercentage = f.CarTraffic;
            d.minimumInteriorScore = f.InteriorScore;
            d.baseBuildingPricePerSqm = f.BasePrice;
            d.rentMultiplierPerSqmPrice = f.RentPerPrice;
            d.marketingStrength = f.Marketing;
            d.maxDailyIncomeToShutDownBusiness = f.ShutDownBelow;
        }

        // The people's class table and price index, as CitizenHelper.Init makes them for every district once a run: put in only once the game made them.
        private static void PeopleRows(DistrictFigures f)
        {
            var shares = GameContract.ClassShares.Field?.GetValue(null) as Dictionary<string, List<(SocialClass, float)>>;
            if (shares != null && shares.Count > 0 && !shares.ContainsKey(DistrictValues.Id))
            {
                DistrictValues.ClassOrder(f.Working, f.Middle, f.Upper, Classes, Shares);
                var row = new List<(SocialClass, float)>(3);
                for (int i = 0; i < Classes.Count; i++)
                {
                    row.Add((Classes[i] == 0 ? SocialClass.Working : Classes[i] == 1 ? SocialClass.Middle : SocialClass.Upper, Shares[i]));
                }
                shares[DistrictValues.Id] = row;
                _sharesPut = true;
            }
            Dictionary<string, float> prices = CitizenHelper.averagePriceIndicesInNeighborhoods;
            if (prices != null && prices.Count > 0 && !prices.ContainsKey(DistrictValues.Id))
            {
                prices[DistrictValues.Id] = DistrictValues.PriceIndex(f.Working, f.Middle, f.Upper);
                _pricePut = true;
            }
        }

        // The wholesalers' goods by district, made once a run on first asking: Uptown's row as the game makes one for a district with no wholesale store yet.
        private static void WholesaleRow()
        {
            var table = GameContract.WholesalersByDistrict.Field?.GetValue(null) as Dictionary<string, HashSet<string>>;
            if (table != null && !table.ContainsKey(DistrictValues.Id))
            {
                table[DistrictValues.Id] = new HashSet<string>();
                _wholesalePut = true;
            }
        }

        // The share of offices and shops the game keeps empty to let in Uptown: an entry for it in each type's targets (a homes' and a
        // warehouses' fall back to the game's own city-wide one). Each array as the game had it is kept to be given back.
        private static void Targets(DistrictFigures f)
        {
            Target(DistrictValues.OfficeType, f.EmptyOffices);
            Target(DistrictValues.RetailType, f.EmptyShops);
        }

        private static void Target(string type, int percent)
        {
            BuildingTypeData data = BuildingTypeHelper.GetData(type);
            if (data == null)
            {
                return;
            }
            IdealAvailableBuildingsInNeighborhood[] was = data.idealAvailableBuildingsInNeighborhood ?? new IdealAvailableBuildingsInNeighborhood[0];
            for (int i = 0; i < was.Length; i++)
            {
                if (was[i] != null && was[i].neighbourhood == DistrictValues.Id)
                {
                    return;   // there from earlier in this run
                }
            }
            if (!TargetsWas.ContainsKey(data))
            {
                TargetsWas[data] = data.idealAvailableBuildingsInNeighborhood;
            }
            var now = new IdealAvailableBuildingsInNeighborhood[was.Length + 1];
            Array.Copy(was, now, was.Length);
            now[was.Length] = new IdealAvailableBuildingsInNeighborhood { neighbourhood = DistrictValues.Id, idealAvailableBuildingsPercentage = percent };
            data.idealAvailableBuildingsInNeighborhood = now;
        }

        /// <summary>
        /// Uptown out of the game's lists of districts in play (it stood down): out of the list and the array, so the game
        /// makes no demand of it and starts nothing there; its record stays in the lookup, so anything still holding its id
        /// (a passer-by made for it) still finds it.
        /// </summary>
        public static void Unlist()
        {
            NeighborhoodHelper.Neighborhoods?.Remove(DistrictValues.Id);
            NeighborhoodData[] was = NeighborhoodHelper.NeighborhoodsData;
            MethodInfo setArray = GameContract.DistrictArray.Setter;
            int at = was != null && _record != null ? Array.IndexOf(was, _record) : -1;
            if (at >= 0 && setArray != null)
            {
                var now = new NeighborhoodData[was.Length - 1];
                for (int i = 0, k = 0; i < was.Length; i++)
                {
                    if (i != at)
                    {
                        now[k++] = was[i];
                    }
                }
                setArray.Invoke(null, new object[] { now });
            }
        }

        /// <summary>
        /// The city goes: what lasts a run is given back as the game had it (each type's targets, Uptown's rows in the run-long
        /// tables), the district out of the game's lookup, list and array of districts (which the game makes again when the
        /// next save is read), and the record destroyed.
        /// </summary>
        public static void Unload()
        {
            foreach (KeyValuePair<BuildingTypeData, IdealAvailableBuildingsInNeighborhood[]> was in TargetsWas)
            {
                if (was.Key != null)
                {
                    was.Key.idealAvailableBuildingsInNeighborhood = was.Value;
                }
            }
            TargetsWas.Clear();
            if (_sharesPut)
            {
                (GameContract.ClassShares.Field?.GetValue(null) as Dictionary<string, List<(SocialClass, float)>>)?.Remove(DistrictValues.Id);
                _sharesPut = false;
            }
            if (_pricePut)
            {
                CitizenHelper.averagePriceIndicesInNeighborhoods.Remove(DistrictValues.Id);
                _pricePut = false;
            }
            if (_wholesalePut)
            {
                (GameContract.WholesalersByDistrict.Field?.GetValue(null) as Dictionary<string, HashSet<string>>)?.Remove(DistrictValues.Id);
                _wholesalePut = false;
            }
            Unlist();   // nothing of the district left in the game's lists until the next load makes them again
            var lookup = GameContract.DistrictLookup.Field?.GetValue(null) as Dictionary<string, NeighborhoodData>;
            if (lookup != null && lookup.TryGetValue(DistrictValues.Id, out NeighborhoodData there) && there == _record)
            {
                lookup.Remove(DistrictValues.Id);
            }
            if (_record != null)
            {
                UnityEngine.Object.Destroy(_record);
                _record = null;
            }
            UnloadMap();
            UnloadBuildings();
        }
    }
}
