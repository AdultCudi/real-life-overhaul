using System;
using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using Buildings;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One electoral district: one of the game's neighbourhoods.</summary>
    public sealed class District
    {
        public string Id;                          // the game's neighbourhood id, which is also its name's locale key
        public int Electorate;
        public CityBuildingController Polling;     // the building that is its polling place; null if none could be designated
        public string PollingAddress;
        public Vector3 Door;                       // that building's door
    }

    /// <summary>
    /// What civic life reads from the city itself: the districts (the game's neighbourhoods),
    /// how many voters each has, the building designated as each one's polling place, the
    /// player's own buildings, and the building nearest a point. Built once per city load (one
    /// walk over the game's building list); nothing here runs on a tick.
    ///
    /// The game keeps no population: its citizens are made at random as customers and
    /// pedestrians are needed. So an electorate is derived from what the neighbourhood really
    /// has: one resident for every 30 square metres of its residential buildings, and one for
    /// every 150 of the rest (people who live over the shop). The petition threshold and the
    /// counts on result night come from that.
    /// </summary>
    public sealed class CivicCity
    {
        private const string Residential = "ba:buildingtype_residential";
        private const string GlobalDistrict = "ba:neighborhood_global";
        private static readonly string[] PollingKinds = { "ba:businesstype_school", "ba:businesstype_bank", "ba:businesstype_recruitmentagency", "ba:businesstype_clinic" };

        public readonly List<District> Districts = new List<District>(8);
        public int Electorate { get; private set; }
        public bool Ready { get; private set; }

        /// <summary>Looks the city over. False if the game's lists are not there yet: ask again later.</summary>
        public bool Build()
        {
            Districts.Clear();
            Electorate = 0;
            Ready = false;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<string> ids = NeighborhoodHelper.Neighborhoods;
            if (city == null || city.cityBuildingControllers == null || ids == null || ids.Count == 0 || BuildingHelper.AllNeighbourhoodBuildings == null)
            {
                return false;
            }
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrEmpty(id) || id == GlobalDistrict || !BuildingHelper.AllNeighbourhoodBuildings.TryGetValue(id, out List<Building> buildings) || buildings == null || buildings.Count == 0)
                {
                    continue;
                }
                float residents = 0f;
                for (int b = 0; b < buildings.Count; b++)
                {
                    Building building = buildings[b];
                    if (building != null)
                    {
                        residents += building.BuildingType == Residential ? building.totalSqm / 30f : building.totalSqm / 150f;
                    }
                }
                var district = new District { Id = id, Electorate = Mathf.Max(400, Mathf.RoundToInt(residents * 0.72f)) };   // 72 % of residents are of voting age
                Designate(district, buildings, city);
                Districts.Add(district);
                Electorate += district.Electorate;
            }
            Ready = Districts.Count > 0;
            return Ready;
        }

        // The polling place: a school if the district has one, then a bank, an agency, a clinic; else its first retail building by address.
        // Always the same building in every game, and always one with a door on the street.
        private static void Designate(District district, List<Building> buildings, CityManager city)
        {
            Building chosen = null;
            int rank = int.MaxValue;
            for (int b = 0; b < buildings.Count; b++)
            {
                Building building = buildings[b];
                if (building == null || GameMembers.Fenced(building))
                {
                    continue;
                }
                int own = PollingKinds.Length + 1;
                if (building.SpecialService != null)
                {
                    int kind = Array.IndexOf(PollingKinds, building.SpecialService.businessTypeName);
                    own = kind >= 0 ? kind : PollingKinds.Length;
                }
                if (own > rank || (own == rank && chosen != null && Later(building, chosen)))
                {
                    continue;
                }
                CityBuildingController controller = city.FindCityBuildingController(building.Address);
                if (!HasDoor(controller))
                {
                    continue;
                }
                chosen = building;
                rank = own;
                district.Polling = controller;
                district.Door = controller.entranceDoors[0].doorTransform.position;
            }
            if (chosen != null)
            {
                district.PollingAddress = chosen.Address.ToFormattedString();
            }
        }

        private static bool Later(Building a, Building b)
        {
            int byStreet = string.CompareOrdinal(a.StreetName, b.StreetName);
            return byStreet > 0 || (byStreet == 0 && a.StreetNumber > b.StreetNumber);
        }

        public static bool HasDoor(CityBuildingController controller)
        {
            return controller != null && controller.building != null && controller.entranceDoors != null && controller.entranceDoors.Length > 0
                   && controller.entranceDoors[0] != null && controller.entranceDoors[0].doorTransform != null;
        }

        public static string Name(District district)
        {
            return district.Id.GetLocalization();
        }

        /// <summary>The district whose polling place is nearest a point; null if none has one.</summary>
        public District NearestPolling(Vector3 point, out float distance)
        {
            District best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < Districts.Count; i++)
            {
                if (Districts[i].Polling == null)
                {
                    continue;
                }
                float sqr = Flat(Districts[i].Door - point);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = Districts[i];
                }
            }
            distance = best != null ? Mathf.Sqrt(bestSqr) : 0f;
            return best;
        }

        /// <summary>
        /// The building with a street door nearest a point, between two distances, that a test
        /// accepts. One walk over the city's buildings: for when a site is being placed, never a tick.
        /// </summary>
        public static CityBuildingController NearestBuilding(Vector3 point, float minDistance, float maxDistance, Predicate<CityBuildingController> only = null)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
            if (all == null)
            {
                return null;
            }
            CityBuildingController best = null;
            float bestSqr = maxDistance * maxDistance;
            float minSqr = minDistance * minDistance;
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController controller = all[i];
                if (!HasDoor(controller) || GameMembers.Fenced(controller.building))
                {
                    continue;
                }
                Vector3 offset = controller.entranceDoors[0].doorTransform.position - point;
                float sqr = Flat(offset);
                if (sqr < minSqr || sqr >= bestSqr || Mathf.Abs(offset.y) > 6f || (only != null && !only(controller)))
                {
                    continue;
                }
                bestSqr = sqr;
                best = controller;
            }
            return best;
        }

        /// <summary>The scene object of the building at an address; null if there is none (or the city is not there).</summary>
        public static CityBuildingController Find(string street, int number)
        {
            if (string.IsNullOrEmpty(street))
            {
                return null;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            return city != null ? city.FindCityBuildingController(new Address(street, number)) : null;
        }

        public static string AddressText(string street, int number)
        {
            try
            {
                return string.IsNullOrEmpty(street) ? string.Empty : new Address(street, number).ToFormattedString();
            }
            catch (Exception)
            {
                return number + " " + street;
            }
        }

        /// <summary>How many businesses the player runs, as the game last counted them at midnight.</summary>
        public static int PlayerBusinesses()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null)
            {
                return 0;
            }
            int count = 0;
            for (int i = 0; i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration registration = save.BuildingRegistrations[i];
                if (registration != null && GoingPublic.Mine(registration) && registration.HasEstablishedBusiness)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>The doors of everything the player rents or owns (home and businesses), for "is a site next door". Fills the list; a walk over the save's buildings.</summary>
        public static void PlayerDoors(List<Vector3> doors)
        {
            doors.Clear();
            GameInstance save = SaveGameManager.Current;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (save == null || save.BuildingRegistrations == null || city == null)
            {
                return;
            }
            for (int i = 0; i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration registration = save.BuildingRegistrations[i];
                if (registration == null || !GoingPublic.Mine(registration))
                {
                    continue;
                }
                CityBuildingController controller = city.FindCityBuildingController(registration.Address);
                if (HasDoor(controller))
                {
                    doors.Add(controller.entranceDoors[0].doorTransform.position);
                }
            }
        }

        public static float Flat(Vector3 offset)
        {
            return offset.x * offset.x + offset.z * offset.z;
        }
    }
}
