using System;
using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using Buildings;
using Localizor;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Districts
{
    /// <summary>One district as the view shows it: its zone on the map, and every figure it has, read at a refresh.</summary>
    internal sealed class MapDistrict
    {
        public string Id;
        public string Name;
        public MeshRenderer Zone;
        public Color OwnColour;                 // the zone's colour as the game sets it (put back when the view goes)
        public float MinX, MaxX, MinZ, MaxZ;    // the zone's rectangle on the ground
        public Vector3 Centre;

        // From the game, on each refresh.
        public int Working, Middle, Upper;      // the class shares, per cent
        public float DemandWeight;              // the game's weight of its customers' demands
        public float Parking;                   // street parking, an hour
        public int Buildings;
        public long CommercialRent;             // cents a square metre a day: shops, offices, warehouses
        public long HomeRent;                   // the same for homes; 0 if none
        public long Value;                      // money a square metre
        public int Traffic;                     // tenths of the game's traffic index, averaged over the buildings that are not homes
        public int Businesses, Mine, Rivals;
        public readonly List<KeyValuePair<string, int>> Types = new List<KeyValuePair<string, int>>(3);   // the three kinds of business most open there

        // From the other features, on each refresh (DistrictBridges).
        public bool HasIncome, IncomeLive;
        public int Income;
        public int? Footfall, Bears;
        public int Crimes;
        public bool HasStanding;
        public int Opinion, Known;
        public bool HasSupply;
        public int Accounts, Supplied, Cut;
        public bool HasInternet;
        public int Providers, Down, Fibre, ReadyDay, Share;
        public bool HasCouncil;
        public int Sites, Electorate, WinnerPercent;
        public string Winner;

        // Each layer's figure and band, and whether there is a figure.
        public readonly long[] Figure = new long[DistrictRules.Layers];
        public readonly bool[] HasFigure = new bool[DistrictRules.Layers];
        public readonly int[] Band = new int[DistrictRules.Layers];

        public bool Contains(float x, float z)
        {
            return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        }
    }

    /// <summary>
    /// Everything the district view reads from the game or does to it. The districts are the game's own zones on the city
    /// map, tinted through a property block as the game tints them for its filters; a zone's rectangle comes from its
    /// mesh's bounds, which hold while it is switched off. The figures are one pass over the city's buildings.
    /// </summary>
    internal static class DistrictGame
    {
        private const string Global = "ba:neighborhood_global";
        private const string Empty = "ba:businesstype_empty";
        private const string Residential = "ba:buildingtype_residential";
        private const float SqftPerSqm = 10.76391f;   // the game's own figure (NeighborhoodHelper.AverageBuildingTypePrice)
        private static readonly int ZoneColour = Shader.PropertyToID("_UnlitColor");
        private static MaterialPropertyBlock _block;

        /// <summary>The districts that have a zone on the map, in the game's order. Empty if the map's zones are not there.</summary>
        public static List<MapDistrict> Read()
        {
            var found = new List<MapDistrict>(8);
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityMap == null || city.cityMap.cityMapNeighborhoodZones == null)
            {
                return found;
            }
            CityMapNeighborhoodZones.CityMapNeighborhoodZone[] zones = CityMapNeighborhoodZones.Zones;
            for (int i = 0; zones != null && i < zones.Length; i++)
            {
                CityMapNeighborhoodZones.CityMapNeighborhoodZone zone = zones[i];
                if (zone == null || zone.zoneRenderer == null || string.IsNullOrEmpty(zone.neighbourhood) || zone.neighbourhood == Global)
                {
                    continue;
                }
                var district = new MapDistrict { Id = zone.neighbourhood, Zone = zone.zoneRenderer, Name = Name(zone.neighbourhood) };
                try
                {
                    district.OwnColour = NeighborhoodHelper.GetData(zone.neighbourhood).cityMapZoneColor;
                }
                catch (Exception)
                {
                    district.OwnColour = new Color(1f, 0f, 0f, 0.47f);   // (the zone material's own colour)
                }
                Rectangle(district);
                found.Add(district);
            }
            return found;
        }

        // The zone's rectangle on the ground: the corners of its mesh's bounds through its transform.
        private static void Rectangle(MapDistrict district)
        {
            Transform t = district.Zone.transform;
            var filter = district.Zone.GetComponent<MeshFilter>();
            Bounds local = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            district.MinX = district.MinZ = float.MaxValue;
            district.MaxX = district.MaxZ = float.MinValue;
            for (int c = 0; c < 8; c++)
            {
                var corner = new Vector3((c & 1) == 0 ? local.min.x : local.max.x, (c & 2) == 0 ? local.min.y : local.max.y, (c & 4) == 0 ? local.min.z : local.max.z);
                Vector3 world = t.TransformPoint(corner);
                district.MinX = Mathf.Min(district.MinX, world.x);
                district.MaxX = Mathf.Max(district.MaxX, world.x);
                district.MinZ = Mathf.Min(district.MinZ, world.z);
                district.MaxZ = Mathf.Max(district.MaxZ, world.z);
            }
            district.Centre = new Vector3((district.MinX + district.MaxX) * 0.5f, t.position.y, (district.MinZ + district.MaxZ) * 0.5f);
        }

        /// <summary>A district's name as the game writes it (its id is its locale key).</summary>
        public static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? string.Empty : LocalizorManager.IsLocalizedKey(id) ? id.GetLocalization() : id;
        }

        /// <summary>The district whose zone holds a point; failing that, the nearest building's within so many metres. Null for none.</summary>
        public static MapDistrict At(List<MapDistrict> districts, Vector3 point, float nearest)
        {
            for (int i = 0; i < districts.Count; i++)
            {
                if (districts[i].Contains(point.x, point.z))
                {
                    return districts[i];
                }
            }
            if (nearest <= 0f)
            {
                return null;
            }
            string id = NearestDistrict(point, nearest);
            for (int i = 0; id != null && i < districts.Count; i++)
            {
                if (districts[i].Id == id)
                {
                    return districts[i];
                }
            }
            return null;
        }

        /// <summary>The neighbourhood of the nearest building within so many metres, or null. A pass over every building: never per frame.</summary>
        public static string NearestDistrict(Vector3 point, float within)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
            float best = within * within;
            string found = null;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                if (b == null || b.building == null)
                {
                    continue;
                }
                Vector3 at = b.transform.position;
                float sqr = (at.x - point.x) * (at.x - point.x) + (at.z - point.z) * (at.z - point.z);
                if (sqr < best && !string.IsNullOrEmpty(b.building.Neighbourhood) && b.building.Neighbourhood != Global)
                {
                    best = sqr;
                    found = b.building.Neighbourhood;
                }
            }
            return found;
        }

        /// <summary>Every district's own figures, from the game's neighbourhood data and one pass over the city's buildings.</summary>
        public static void Figures(List<MapDistrict> districts)
        {
            var index = new Dictionary<string, int>(districts.Count, StringComparer.Ordinal);
            var rent = new long[districts.Count, 4];   // commercial rent and area, homes' rent and area
            var value = new double[districts.Count, 2];
            var traffic = new long[districts.Count, 2];
            var types = new Dictionary<string, int>[districts.Count];
            for (int i = 0; i < districts.Count; i++)
            {
                MapDistrict d = districts[i];
                index[d.Id] = i;
                types[i] = new Dictionary<string, int>(16, StringComparer.Ordinal);
                d.Buildings = d.Businesses = d.Mine = d.Rivals = 0;
                d.Types.Clear();
                NeighborhoodData data = null;
                try
                {
                    data = NeighborhoodHelper.GetData(d.Id);
                }
                catch (Exception)
                {
                    // (a district the game's data does not know: its own figures are left at nothing)
                }
                d.Working = data != null ? data.workingClassPercentage : 0;
                d.Middle = data != null ? data.middleClassPercentage : 0;
                d.Upper = data != null ? data.upperClassClassPercentage : 0;
                d.DemandWeight = data != null ? data.customerDemandsWeight : 0f;
                d.Parking = data != null ? data.parkingPrice : 0f;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
            for (int b = 0; all != null && b < all.Length; b++)
            {
                CityBuildingController controller = all[b];
                Building building = controller != null ? controller.building : null;
                BuildingRegistration reg = controller != null ? controller.buildingRegistration : null;
                int i;
                if (building == null || reg == null || string.IsNullOrEmpty(building.Neighbourhood) || !index.TryGetValue(building.Neighbourhood, out i))
                {
                    continue;
                }
                MapDistrict d = districts[i];
                d.Buildings++;
                bool home = building.BuildingType == Residential;
                int sqm = 0;
                try
                {
                    BuildingSizeData size = BuildingSizeHelper.GetData(building);
                    sqm = size != null ? size.squareMeters : 0;
                    float perDay = reg.RentPerDay > 0f ? reg.RentPerDay : building.GetBuildingDailyMarketRent();
                    if (sqm > 0 && perDay > 0f)
                    {
                        rent[i, home ? 2 : 0] += (long)Math.Round(perDay * 100.0);
                        rent[i, home ? 3 : 1] += sqm;
                    }
                    float worth = building.GetMarketValue();
                    if (building.totalSqm > 0 && worth > 0f)
                    {
                        value[i, 0] += worth;
                        value[i, 1] += building.totalSqm;
                    }
                }
                catch (Exception)
                {
                    // (a building of a kind the game's size or type data does not know: left out of the averages)
                }
                if (!home)
                {
                    traffic[i, 0] += building.trafficIndex;
                    traffic[i, 1]++;
                }
                if (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == Empty || !reg.HasEstablishedBusiness)
                {
                    continue;
                }
                d.Businesses++;
                if (GoingPublic.Mine(reg))
                {
                    d.Mine++;
                }
                else if (!string.IsNullOrEmpty(reg.businessOwnerRivalId))
                {
                    d.Rivals++;
                }
                int count;
                types[i].TryGetValue(reg.businessTypeName, out count);
                types[i][reg.businessTypeName] = count + 1;
            }
            for (int i = 0; i < districts.Count; i++)
            {
                MapDistrict d = districts[i];
                d.CommercialRent = rent[i, 1] > 0 ? rent[i, 0] / rent[i, 1] : 0;
                d.HomeRent = rent[i, 3] > 0 ? rent[i, 2] / rent[i, 3] : 0;
                d.Value = value[i, 1] > 0 ? (long)Math.Round(value[i, 0] / value[i, 1]) : 0;
                d.Traffic = traffic[i, 1] > 0 ? (int)(traffic[i, 0] * 10 / traffic[i, 1]) : 0;
                foreach (KeyValuePair<string, int> pair in types[i])
                {
                    d.Types.Add(pair);
                }
                d.Types.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
                if (d.Types.Count > 3)
                {
                    d.Types.RemoveRange(3, d.Types.Count - 3);
                }
            }
        }

        /// <summary>A figure a square metre in the game's own unit of area (a square foot where the game is set to imperial).</summary>
        public static float PerArea(float perSqm)
        {
            return UnitHelper.useImperial ? perSqm / SqftPerSqm : perSqm;
        }

        public static string AreaUnit()
        {
            return UnitHelper.GetAreaUnit();
        }

        /// <summary>A zone shown in a colour of the view's.</summary>
        public static void Tint(MapDistrict district, Tint tint)
        {
            if (district.Zone == null)
            {
                return;
            }
            Paint(district.Zone, new Color32(tint.R, tint.G, tint.B, tint.A));
            if (!district.Zone.gameObject.activeSelf)
            {
                district.Zone.gameObject.SetActive(true);
            }
        }

        /// <summary>A zone back in the game's own colour and hidden; the game's own filters show it again if ticked.</summary>
        public static void Untint(MapDistrict district)
        {
            if (district.Zone != null)
            {
                Paint(district.Zone, district.OwnColour);
                district.Zone.gameObject.SetActive(false);
            }
        }

        private static void Paint(MeshRenderer zone, Color colour)
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }
            zone.GetPropertyBlock(_block);
            _block.SetColor(ZoneColour, colour);
            zone.SetPropertyBlock(_block);
        }

        /// <summary>The game looks at its own filters again: its neighbourhood zones and its lit buildings as its ticks say.</summary>
        public static void GameFilters()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null)
            {
                MapPlaces.Refilter(uis.mapFilters);
            }
        }

        /// <summary>True while the open map is the plain map: no subway pick, no full menu over it, no choice of the mod's up.</summary>
        public static bool MapIsPlain()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            return CityMap.IsOpen && city != null && city.cityMap != null && !city.cityMap.isSubwayMode && !UI.Smartphone.FullMenu.IsOpen && !MapPlaces.Quiet;
        }

        /// <summary>The point on the ground under the mouse on the open map; false if the camera cannot say.</summary>
        public static bool Pointed(out Vector3 point)
        {
            point = default;
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            float along;
            if (!ground.Raycast(ray, out along))
            {
                return false;
            }
            point = ray.GetPoint(along);
            return true;
        }

        /// <summary>Flies the open map to a place, as a filter's arrow button does.</summary>
        public static void FlyTo(Vector3 place)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (CityMap.IsOpen && city != null && city.cityMap != null && city.cityMap.cityMapCam != null)
            {
                city.cityMap.cityMapCam.MoveCameraToTarget(place);
            }
        }

        /// <summary>The district the player stands in (or the building they are in), or null.</summary>
        public static string PlayerDistrict()
        {
            string current = NeighborhoodHelper.CurrentNeighborhood;
            if (!string.IsNullOrEmpty(current) && current != Global)
            {
                return current;
            }
            return Helpers.PlayerHelper.PlayerController != null ? NearestDistrict(Helpers.PlayerHelper.GetCityPosition(), 80f) : null;
        }

        /// <summary>A kind of business as the game names it.</summary>
        public static string TypeName(string type)
        {
            return string.IsNullOrEmpty(type) ? string.Empty : type.GetLocalization();
        }
    }
}
