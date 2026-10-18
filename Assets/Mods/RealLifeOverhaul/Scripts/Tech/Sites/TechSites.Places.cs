using System.Collections.Generic;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the tech sector's buildings tell the rest of the mod: the companies' floors and towers, and the city map's places.</summary>
    public sealed partial class TechSites
    {
        /// <summary>The company whose floor of a tower the player is on now, or null (the careers' desk is on its company's floor).</summary>
        public string FloorCompany()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (!Settings.TechSiteHq || !BuildingManager.IsInsideBuilding || manager == null || manager.enteringBuilding || manager.exitingBuilding
                || Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking)
            {
                return null;
            }
            Headquarters.Hq hq = OnFloor(manager.buildingRegistration);
            return hq != null ? hq.Name : null;
        }

        /// <summary>True while a panel of the towers is up, and in the frame its button ran (World/NearbyInteraction).</summary>
        public static bool TakesInteractKey => Instance != null && Instance._floors != null && Instance._floors.TakesKey;

        /// <summary>A tech company's headquarters by its name: its tower's address (the careers' jobs there).</summary>
        public bool HeadquartersOf(string company, out string street, out int number)
        {
            street = null;
            number = 0;
            return Settings.TechSiteHq && _hq != null && _hq.Of(company, out street, out number);
        }

        /// <summary>A company's tower, floor (from 1) and address, for the Tech app's Competitors page. False while it has none.</summary>
        public bool FloorOf(string company, out string tower, out int floor, out string street, out int number)
        {
            Headquarters.Hq hq = Settings.TechSiteHq && _hq != null ? _hq.Named(company) : null;
            tower = hq != null && hq.Tower != null ? hq.Tower.Name : null;
            floor = hq != null ? hq.Floor : 0;
            street = tower != null ? hq.Street : null;
            number = tower != null ? hq.Number : 0;
            return tower != null && !string.IsNullOrEmpty(street);
        }

        /// <summary>Changes whenever the headquarters do.</summary>
        public long HeadquartersStamp => Settings.TechSiteHq && _hq != null && _hq.All.Count > 0 ? 1 + _hq.All.Count + _hq.Stamp * 16L : 0;

        /// <summary>The towers as places on the city map: one each, its name and address (its companies are on its card and in the search).</summary>
        public void HeadquartersPlaces(List<MapPlace> into)
        {
            for (int i = 0; _hq != null && i < _hq.Towers.Count; i++)
            {
                Headquarters.Tower tower = _hq.Towers[i];
                if (tower.Building == null)
                {
                    continue;
                }
                into.Add(new MapPlace(tower.Front, tower.Name + ", " + tower.Building.building.Address.ToFormattedString(), tower.Building) { CardTypeKey = "reallife_hq_tower_card_type" });
            }
        }

        /// <summary>Changes whenever the player's data centres, their names or the cloud's do.</summary>
        public long CloudStamp
        {
            get
            {
                InfraFile infra = TechDirector.Instance != null ? TechDirector.Instance.Infra : null;
                if (!Settings.TechSiteCloud || infra == null || infra.Centres.Count == 0)
                {
                    return 0;
                }
                unchecked
                {
                    long stamp = 1 + infra.Centres.Count + _fronts.Stamp * 64L + (infra.Cloud.Open ? TechRandom.Of(infra.Cloud.Name) : 0) * 7L + (infra.Cloud.Hyper ? 3 : 0);
                    for (int i = 0; i < infra.Centres.Count; i++)
                    {
                        stamp = stamp * 31 + TechRandom.Of(infra.Centres[i].Name);
                    }
                    return stamp;
                }
            }
        }

        /// <summary>The player's data centres as places on the map: the cloud's regions, a hyperscaler's all of them.</summary>
        public void CloudPlaces(List<MapPlace> into)
        {
            InfraFile infra = TechDirector.Instance != null ? TechDirector.Instance.Infra : null;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            for (int i = 0; infra != null && i < infra.Centres.Count; i++)
            {
                DataCentre d = infra.Centres[i];
                if (!_fronts.Front(d.Place, out Vector3 front, out CityBuildingController building))
                {
                    BuildingRegistration reg = TalentGame.Find(d.Place);
                    building = reg != null && city != null ? city.FindCityBuildingController(reg.Address) : null;
                    if (building == null || !JobSpots.Front(building, out front))
                    {
                        continue;
                    }
                }
                string region = HiringGame.District(d.District);
                string name = infra.Cloud.Open && !string.IsNullOrEmpty(infra.Cloud.Name)
                    ? TechText.Fill("reallife_sites_map_region", "cloud", infra.Cloud.Name, "region", region)
                    : TechText.Fill("reallife_sites_map_centre", "name", d.Name);
                into.Add(new MapPlace(front, name) { Lit = building });
            }
        }
    }
}
