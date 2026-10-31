using System;
using System.Collections.Generic;
using BAModAPI;
using BA_Packages.Rivals.Scripts;
using BigAmbitions.Characters;
using BigAmbitions.Neighborhoods;
using Buildings;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The only place the population touches the game: the city's districts and buildings as they stand (each building's floor, size, special
    /// service, market rent and place), the registrations as business seeds (type, owner, room for customers, hours, the game's own judgment of its
    /// prices, facilities and satisfaction, the player's staff there, the promotion multiplier), the player's staff, a person's name by the game's
    /// own name maker, and a redraw of a player's business's customers after the day's figures. Every pick from the game's buildings goes through
    /// Core/GameDlc. COST: nothing per frame; a pass over the buildings at a load, a pass over the registrations once a game day.
    /// </summary>
    internal static partial class PopulationGame
    {
        public static IModLogger Logger;

        private const string Global = "ba:neighborhood_global";
        private const string Hamptons = "ba:neighborhood_thehamptons";

        private static GameInstance Save => SaveGameManager.Current;

        public static int Today => Save != null ? Save.Day : -1;

        public static int YearDays => GameCalendar.DaysPerYear;

        /// <summary>The city's districts (the game's neighbourhoods this city knows) and its buildings as they stand; false while the game has none.</summary>
        public static bool ReadCity(List<CityBuilding> buildings, List<PopDistrict> districts)
        {
            buildings.Clear();
            districts.Clear();
            NeighborhoodData[] all = NeighborhoodHelper.NeighborhoodsData;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                NeighborhoodData data = all[i];
                if (data == null || string.IsNullOrEmpty(data.neighbourhood) || data.neighbourhood == Global || !CityCheck.Here(data.neighbourhood))
                {
                    continue;
                }
                districts.Add(new PopDistrict
                {
                    Id = data.neighbourhood, Working = data.workingClassPercentage, Middle = data.middleClassPercentage, Upper = data.upperClassClassPercentage,
                    RealEstate = Mathf.RoundToInt(data.realEstateMultiplier * 1000f), DemandWeight = Mathf.RoundToInt(data.customerDemandsWeight * 1000f), Villas = data.neighbourhood == Hamptons
                });
            }
            List<Building> list = BuildingHelper.allBuildings;
            if (list == null || list.Count == 0 || districts.Count == 0)
            {
                return false;
            }
            var places = new Dictionary<string, Vector3>(list.Count, StringComparer.Ordinal);
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] standing = city != null ? GameDlc.Open(city) : null;
            for (int i = 0; standing != null && i < standing.Length; i++)
            {
                CityBuildingController c = standing[i];
                if (c != null && c.building != null && !string.IsNullOrEmpty(c.building.StreetName))
                {
                    places[PopulationState.KeyOf(c.building.StreetName, c.building.StreetNumber)] = c.transform.position;
                }
            }
            for (int i = 0; i < list.Count; i++)
            {
                Building b = list[i];
                if (b == null || string.IsNullOrEmpty(b.StreetName) || GameDlc.Gated(b))
                {
                    continue;
                }
                int d = IndexOf(districts, b.Neighbourhood);
                if (d < 0)
                {
                    continue;
                }
                string special = b.SpecialService != null ? b.SpecialService.businessTypeName : null;
                Use use = MeasureRules.UseOf(b.BuildingType, special);
                string key = PopulationState.KeyOf(b.StreetName, b.StreetNumber);
                places.TryGetValue(key, out Vector3 at);
                var cb = new CityBuilding
                {
                    Street = b.StreetName, Number = b.StreetNumber, District = d, Use = use, UnitSqm = UnitSqm(b), TotalSqm = b.totalSqm, Villa = use == Use.Homes && b.IsHamptonsHouse(),
                    Special = special, Traffic = b.trafficIndex, Rent = use == Use.Homes ? Rent(b) : 0, X = at.x, Z = at.z
                };
                buildings.Add(cb);
                if (use == Use.Shop || use == Use.Cinema || use == Use.Theatre)
                {
                    districts[d].Pull += Room(b) * districts[d].DemandWeight / 1000;
                }
            }
            return buildings.Count > 0;
        }

        private static int IndexOf(List<PopDistrict> districts, string id)
        {
            for (int i = 0; i < districts.Count; i++)
            {
                if (string.Equals(districts[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // The game's unit for the building's size (BuildingSizeData.squareMeters); 0 for a size it has no data of.
        private static int UnitSqm(Building b)
        {
            try
            {
                BuildingSizeData size = BuildingSizeHelper.GetData(b.BuildingSize);
                return size != null ? Mathf.Max(0, size.squareMeters) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // The game's market rent of one unit a day, cents.
        private static int Rent(Building b)
        {
            try
            {
                return (int)Math.Min(int.MaxValue / 4, (long)b.GetBuildingDailyMarketRent() * 100);
            }
            catch (Exception)
            {
                return 0;   // the population works it out from the district instead
            }
        }

        // The customers the building's size has room for as its type.
        private static int Room(Building b)
        {
            try
            {
                return Mathf.Max(0, BuildingSizeHelper.GetData(b.BuildingSize).GetCustomerCapacity(b.BuildingType, b.BuildingVersion));
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>The registrations as the population's business seeds, with the player's staff counted by their address.</summary>
        public static void ReadSeeds(List<BusinessSeed> into, Dictionary<string, int> staffAt)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            float baseMultiplier = Save != null && Save.gameVariables != null ? Save.gameVariables.baseCustomerPromotionMultiplier : 0.5f;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || reg.Address == null || string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == "ba:businesstype_empty" || GameDlc.Gated(reg))
                {
                    continue;
                }
                Building b = GameRegistrations.BuildingOf(reg);
                if (b == null)
                {
                    continue;
                }
                string key = PopulationState.KeyOf(reg.Address.streetName, reg.Address.streetNumber);
                Owner owner = GoingPublic.Mine(reg) ? Owner.Player : GoingPublic.Foreign(reg) ? Owner.Guest : !string.IsNullOrEmpty(reg.businessOwnerRivalId) ? Owner.Rival : Owner.City;
                int promotion = reg.RentedByPlayer && reg.promotion != null ? reg.promotion.total : b.trafficIndex;
                var content = reg.satisfaction;
                into.Add(new BusinessSeed
                {
                    Street = reg.Address.streetName, Number = reg.Address.streetNumber, Type = reg.businessTypeName, Owner = owner, Room = Mathf.Max(1, reg.customerCapacity > 0 ? reg.customerCapacity : Room(b)),
                    Hours = HoursOpen(reg), Price = content != null ? 1000 + (50 - Mathf.Clamp(content.pricing, 0, 100)) * 6 : 1000, Quality = content != null ? content.facility : 50,
                    Content = content != null ? content.overall : 50, Staff = owner == Owner.Player ? (staffAt != null && staffAt.TryGetValue(key, out int n) ? n : 0) : -1,
                    Promotion = CustomerRules.PromotionMultiplier(baseMultiplier, promotion)
                });
            }
        }

        // Hours open a day over the week, whole hours (the schedule's slots, no LINQ).
        private static int HoursOpen(BuildingRegistration reg)
        {
            List<ScheduleDay> week = reg.scheduleDays;
            if (week == null || week.Count == 0)
            {
                return 12;
            }
            int hours = 0;
            for (int d = 0; d < week.Count; d++)
            {
                ScheduleDay day = week[d];
                if (day == null || !day.isOpen || day.openingHourSlots == null)
                {
                    continue;
                }
                for (int s = 0; s < day.openingHourSlots.Count; s++)
                {
                    OpeningHourSlot slot = day.openingHourSlots[s];
                    hours += slot != null ? Mathf.Max(0, slot.endingHour - slot.startingHour) : 0;
                }
            }
            return Mathf.Clamp(Mathf.RoundToInt(hours / 7f), 0, 24);
        }

        /// <summary>The player's staff: each one's id, where they work, pay, hours, age and sex; and how many work at each address.</summary>
        public static void ReadStaff(List<StaffRef> into, Dictionary<string, int> staffAt)
        {
            into.Clear();
            staffAt.Clear();
            List<EmployeeInstance> all = Save != null ? Save.EmployeeInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (e == null || string.IsNullOrEmpty(e.id))
                {
                    continue;
                }
                string key = e.assignedAddress != null ? PopulationState.KeyOf(e.assignedAddress.streetName, e.assignedAddress.streetNumber) : null;
                var data = e.characterData;
                into.Add(new StaffRef
                {
                    Id = e.id, Key = key, Pay = Mathf.RoundToInt(e.hourlyWage * 100f), Hours = PopulationCatalogue.FullTimeHours,
                    Age = data != null ? TimeHelper.GetYearsByDays(data.ageInDays) : 30, Female = data != null && data.gender == Gender.Female
                });
                if (key != null)
                {
                    staffAt[key] = (staffAt.TryGetValue(key, out int n) ? n : 0) + 1;
                }
            }
        }

        /// <summary>A person's name by the game's own name maker, from a seed: the same on every machine.</summary>
        public static string NameOf(int seed, bool female)
        {
            try
            {
                return new System.Random(seed).GetFullName(female ? Gender.Female : Gender.Male);
            }
            catch (Exception)
            {
                return "?";
            }
        }

        /// <summary>A registration by its address key, or null.</summary>
        public static BuildingRegistration Registration(string key)
        {
            int cut = key != null ? key.LastIndexOf('|') : -1;
            if (cut <= 0 || !int.TryParse(key.Substring(cut + 1), out int number))
            {
                return null;
            }
            return BuildingHelper.GetBuildingRegistration(new Address(key.Substring(0, cut), number));
        }

        /// <summary>A registration's business name as the game shows it, or the key's address.</summary>
        public static string BusinessName(string key)
        {
            BuildingRegistration reg = Registration(key);
            return reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : key;
        }

        /// <summary>The game's promotion multiplier of a registration now, thousandths (the player's promotion, or the building's traffic for the city's).</summary>
        public static int PromotionNow(BuildingRegistration reg)
        {
            float baseMultiplier = Save != null && Save.gameVariables != null ? Save.gameVariables.baseCustomerPromotionMultiplier : 0.5f;
            Building b = GameRegistrations.BuildingOf(reg);
            int promotion = reg.RentedByPlayer && reg.promotion != null ? reg.promotion.total : b != null ? b.trafficIndex : 0;
            return CustomerRules.PromotionMultiplier(baseMultiplier, promotion);
        }

        /// <summary>The player's businesses' customers still to come today drawn again with the day's figures (Core/CustomerDraw spreads it over frames).</summary>
        public static int RedrawMine()
        {
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            int asked = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && !string.IsNullOrEmpty(reg.businessTypeName))
                {
                    CustomerDraw.Redraw(reg);
                    asked++;
                }
            }
            return asked;
        }

        /// <summary>The save's modData, made if missing; null with no save.</summary>
        public static Dictionary<string, string> Data()
        {
            GameInstance save = Save;
            if (save == null)
            {
                return null;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            return save.modData;
        }
    }
}
