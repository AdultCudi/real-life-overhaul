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
    /// The band's side of the game, the only place its districts touch it (with .Buildings, .Map and .Tests). Here: the game's
    /// record of each of the band's two districts (NeighborhoodData; Uptown and Cedar Heights, DistrictSplit), made with
    /// ScriptableObject.CreateInstance from the district's own figures (DistrictSplit.Figures) and put wherever the game finds a
    /// district: its private lookup (NeighborhoodHelper.GetData throws on an id it lacks), its list and its array of districts,
    /// an empty list of its buildings (CompetitionHelper.RunDaily reads AllNeighbourhoodBuildings for every district with stats
    /// and throws on one it lacks), its people's class table and price index and the wholesalers' table (both made by the game
    /// once a run, so a district's rows go in only when the game has made them and lacks it: CitizenHelper.Init would throw on
    /// a key put there before it), and its share of offices and shops kept empty to let (BuildingTypeData's per-district
    /// targets; without one the game keeps none empty). The game makes its lookups and lists of districts again at every load;
    /// what lasts a run (the tables, BuildingTypeData's arrays) is given back as the game had it when the city goes. Nothing
    /// here runs a frame.
    /// </summary>
    internal static partial class LifeGame
    {
        private static readonly NeighborhoodData[] Records = new NeighborhoodData[DistrictSplit.Count];   // each district's own record at this load, destroyed when the city goes
        private static readonly Dictionary<BuildingTypeData, IdealAvailableBuildingsInNeighborhood[]> TargetsWas =
            new Dictionary<BuildingTypeData, IdealAvailableBuildingsInNeighborhood[]>(2);   // each type's targets as the game had them
        private static readonly bool[] SharesPut = new bool[DistrictSplit.Count];     // each district's rows this load put in the game's run-long tables
        private static readonly bool[] PricePut = new bool[DistrictSplit.Count];
        private static readonly bool[] WholesalePut = new bool[DistrictSplit.Count];
        private static readonly List<int> Classes = new List<int>(3);
        private static readonly List<float> Shares = new List<float>(3);

        /// <summary>A district's figures at this load: the ones its design gives it (DistrictSplit.Figures).</summary>
        public static DistrictFigures Figures(int district)
        {
            return DistrictSplit.Figures(district);
        }

        /// <summary>A district's record as the game has it at this load, or null.</summary>
        public static NeighborhoodData Record(int district)
        {
            return district >= 0 && district < Records.Length ? Records[district] : null;
        }

        /// <summary>True when any of the band's districts has its record at this load.</summary>
        public static bool AnyRecord
        {
            get
            {
                for (int i = 0; i < Records.Length; i++)
                {
                    if (Records[i] != null)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>True when the game's own lookup finds the record of every district of the band (what the census test asks first).</summary>
        public static bool GetDataFinds()
        {
            var lookup = GameContract.DistrictLookup.Field?.GetValue(null) as Dictionary<string, NeighborhoodData>;
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                if (lookup == null || !lookup.TryGetValue(DistrictSplit.Ids[d], out NeighborhoodData data) || data == null || data != Records[d])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True when every district of the band is in the game's list of districts.</summary>
        public static bool Listed()
        {
            List<string> list = NeighborhoodHelper.Neighborhoods;
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                if (list == null || !list.Contains(DistrictSplit.Ids[d]))
                {
                    return false;
                }
            }
            return true;
        }

        // ---------------------------------------------------------------- the districts in the game's lookups

        /// <summary>
        /// The band's districts as districts the game knows, at the district save's early step (a save read, its city not yet
        /// built) or at the city load: each record in the game's lookup, list and array of districts, an empty list of its
        /// buildings, its rows in the run-long tables when the game has made them, its targets of units kept to let. Their rows
        /// on the city map wait for their zones (<see cref="Map"/>). True when they are there; false with why when the game's
        /// lookups cannot be reached. Done once a load; a second call finds them there and changes nothing.
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
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                if (lookup.TryGetValue(DistrictSplit.Ids[d], out NeighborhoodData there) && there != null && there != Records[d])
                {
                    why = "the game, or another mod, has a district of the id " + DistrictSplit.Ids[d] + " already";
                    return false;
                }
            }
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                string id = DistrictSplit.Ids[d];
                DistrictFigures f = DistrictSplit.Figures(d);
                if (Records[d] == null)
                {
                    NeighborhoodData record = ScriptableObject.CreateInstance<NeighborhoodData>();   // destroyed when the city goes (Unload)
                    Shape(d, record, f, lookup);
                    Records[d] = record;
                }
                lookup[id] = Records[d];
                if (!list.Contains(id))
                {
                    list.Add(id);
                }
                NeighborhoodData[] was = NeighborhoodHelper.NeighborhoodsData;
                if (Array.IndexOf(was, Records[d]) < 0)
                {
                    var now = new NeighborhoodData[was.Length + 1];
                    Array.Copy(was, now, was.Length);
                    now[was.Length] = Records[d];
                    setArray.Invoke(null, new object[] { now });
                }
                if (!BuildingHelper.AllNeighbourhoodBuildings.ContainsKey(id))
                {
                    BuildingHelper.AllNeighbourhoodBuildings[id] = new List<Building>();   // the game's daily pass reads it for every district with stats
                }
                PeopleRows(d, f);
                WholesaleRow(d);
                Targets(d, f);
            }
            return true;
        }

        // A district's record, its row and its look on the city map (its zone's colour, Midtown's icon), its name for the game's business names.
        private static void Shape(int district, NeighborhoodData d, DistrictFigures f, Dictionary<string, NeighborhoodData> lookup)
        {
            string id = DistrictSplit.Ids[district];
            d.name = district == DistrictSplit.CedarHeights ? "RealLife Cedar Heights" : "RealLife Uptown";
            d.hideFlags = HideFlags.DontUnloadUnusedAsset;   // the engine's sweep of unused assets would take a record only the mod holds
            d.neighbourhood = id;
            d.hasCityMapFilter = false;   // until its zone is on the city map: the game's district rows ask the zone for their focus (CityMapNeighborhoodZones.First throws without one)
            d.cityMapIcon = Icon(district);   // a disc of its own colour: Midtown's icon would show Midtown's colour on its toggle
            float[] colour = DistrictValues.ColourOf(district);
            d.cityMapZoneColor = new Color(colour[0], colour[1], colour[2], DistrictValues.ZoneAlpha);
            Apply(d, f);
            string name = id.GetLocalization();
            string plain = district == DistrictSplit.CedarHeights ? "Cedar Heights" : "Uptown";
            d.aliases = new[] { string.IsNullOrEmpty(name) || name == id ? plain : name };   // the game names businesses after their district
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
        private static void PeopleRows(int district, DistrictFigures f)
        {
            string id = DistrictSplit.Ids[district];
            var shares = GameContract.ClassShares.Field?.GetValue(null) as Dictionary<string, List<(SocialClass, float)>>;
            if (shares != null && shares.Count > 0 && !shares.ContainsKey(id))
            {
                DistrictValues.ClassOrder(f.Working, f.Middle, f.Upper, Classes, Shares);
                var row = new List<(SocialClass, float)>(3);
                for (int i = 0; i < Classes.Count; i++)
                {
                    row.Add((Classes[i] == 0 ? SocialClass.Working : Classes[i] == 1 ? SocialClass.Middle : SocialClass.Upper, Shares[i]));
                }
                shares[id] = row;
                SharesPut[district] = true;
            }
            Dictionary<string, float> prices = CitizenHelper.averagePriceIndicesInNeighborhoods;
            if (prices != null && prices.Count > 0 && !prices.ContainsKey(id))
            {
                prices[id] = DistrictValues.PriceIndex(f.Working, f.Middle, f.Upper);
                PricePut[district] = true;
            }
        }

        // The wholesalers' goods by district, made once a run on first asking: a district's row as the game makes one for a district with no wholesale store yet.
        private static void WholesaleRow(int district)
        {
            string id = DistrictSplit.Ids[district];
            var table = GameContract.WholesalersByDistrict.Field?.GetValue(null) as Dictionary<string, HashSet<string>>;
            if (table != null && !table.ContainsKey(id))
            {
                table[id] = new HashSet<string>();
                WholesalePut[district] = true;
            }
        }

        // The share of offices, shops and homes the game keeps empty to let in a district: an entry for it in each type's targets, the
        // owner's third (MixRules.ToLetShare) whatever the figures say; a warehouses' falls back to the game's own city-wide one. The
        // generator, the game's daily swaps of homes and its new businesses all read them. Each array as the game had it is kept to be given back.
        private static void Targets(int district, DistrictFigures f)
        {
            Target(district, DistrictValues.OfficeType, f.EmptyOffices);
            Target(district, DistrictValues.RetailType, f.EmptyShops);
            Target(district, MixRules.HomeType, MixRules.ToLetShare);
        }

        private static void Target(int district, string type, int percent)
        {
            string id = DistrictSplit.Ids[district];
            BuildingTypeData data = BuildingTypeHelper.GetData(type);
            if (data == null)
            {
                return;
            }
            IdealAvailableBuildingsInNeighborhood[] was = data.idealAvailableBuildingsInNeighborhood ?? new IdealAvailableBuildingsInNeighborhood[0];
            for (int i = 0; i < was.Length; i++)
            {
                if (was[i] != null && was[i].neighbourhood == id)
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
            now[was.Length] = new IdealAvailableBuildingsInNeighborhood { neighbourhood = id, idealAvailableBuildingsPercentage = percent };
            data.idealAvailableBuildingsInNeighborhood = now;
        }

        /// <summary>
        /// The band out of the game's lists of districts in play (it stood down): out of the list and the array, so the game
        /// makes no demand of it and starts nothing there; the records stay in the lookup, so anything still holding an id
        /// (a passer-by made for it) still finds it.
        /// </summary>
        public static void Unlist()
        {
            MethodInfo setArray = GameContract.DistrictArray.Setter;
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                NeighborhoodHelper.Neighborhoods?.Remove(DistrictSplit.Ids[d]);
                NeighborhoodData[] was = NeighborhoodHelper.NeighborhoodsData;
                int at = was != null && Records[d] != null ? Array.IndexOf(was, Records[d]) : -1;
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
        }

        /// <summary>
        /// The city goes: what lasts a run is given back as the game had it (each type's targets, the districts' rows in the run-long
        /// tables), the districts out of the game's lookup, list and array of districts (which the game makes again when the
        /// next save is read), and the records destroyed.
        /// </summary>
        public static void Unload()
        {
            DropFillers();
            foreach (KeyValuePair<BuildingTypeData, IdealAvailableBuildingsInNeighborhood[]> was in TargetsWas)
            {
                if (was.Key != null)
                {
                    was.Key.idealAvailableBuildingsInNeighborhood = was.Value;
                }
            }
            TargetsWas.Clear();
            var lookup = GameContract.DistrictLookup.Field?.GetValue(null) as Dictionary<string, NeighborhoodData>;
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                string id = DistrictSplit.Ids[d];
                if (SharesPut[d])
                {
                    (GameContract.ClassShares.Field?.GetValue(null) as Dictionary<string, List<(SocialClass, float)>>)?.Remove(id);
                    SharesPut[d] = false;
                }
                if (PricePut[d])
                {
                    CitizenHelper.averagePriceIndicesInNeighborhoods.Remove(id);
                    PricePut[d] = false;
                }
                if (WholesalePut[d])
                {
                    (GameContract.WholesalersByDistrict.Field?.GetValue(null) as Dictionary<string, HashSet<string>>)?.Remove(id);
                    WholesalePut[d] = false;
                }
            }
            Unlist();   // nothing of the districts left in the game's lists until the next load makes them again
            for (int d = 0; d < DistrictSplit.Count; d++)
            {
                if (lookup != null && lookup.TryGetValue(DistrictSplit.Ids[d], out NeighborhoodData there) && there == Records[d])
                {
                    lookup.Remove(DistrictSplit.Ids[d]);
                }
                NeighborhoodData record = Records[d];
                if (record != null)
                {
                    UnityEngine.Object.Destroy(record);
                    Records[d] = null;
                }
            }
            UnloadMap();
            UnloadBuildings();
        }
    }
}
