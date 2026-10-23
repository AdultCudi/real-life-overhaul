using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using BigAmbitions.Tags;
using Buildings;
using Entities;
using Helpers;
using Localizor;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A business of the player's, as the company reads it: its place, address and name.</summary>
    public struct PlaceView
    {
        public string Place;      // "street#number"
        public string Street;
        public int Number;
        public string Name;
        public string District;
    }

    /// <summary>A building of the game's: the player's own (to rebuild) or a business's (a client's tender).</summary>
    public struct BuildingView
    {
        public string Street;
        public int Number;
        public string Name;       // its business's name or its address, as words
        public string District;
        public string Type;
        public string Size;
        public int Version;
        public int TotalSqm;
        public int Traffic;
    }

    /// <summary>A business of the player's that can be refitted: its address, name, floor area and kind.</summary>
    public struct BusinessView
    {
        public string Street;
        public int Number;
        public string Name;
        public string District;
        public int Sqm;
        public bool Office;       // an office's, else a shop's
        public bool Owned;        // the building is the player's own (an exterior refresh is a rebuild of it)
        public Vector3 Door;
    }

    /// <summary>An employee of the company's office: the game's id, name and main skill.</summary>
    public struct StaffView
    {
        public string Id;
        public string Name;
        public string Skill;
        public int Value;         // 0 to 100
    }

    /// <summary>
    /// The only part of the construction game that reads or changes the game: the clock and prices, money through the game's
    /// own transaction call, the player's offices of the company's type, warehouses and buildings, the office's staff, a
    /// building type's value multiplier, addresses and doors, and the game's notifications. Nothing here keeps state.
    /// </summary>
    internal static class ConstructionGame
    {
        private const string Warehouse = "ba:buildingtype_warehouse";

        internal static bool Loading => LoadScene.isLoading || !ModState.Loaded || SaveGameManager.Current == null;

        internal static int Day => TimeHelper.CurrentDay;

        internal static int Hour => TimeHelper.CurrentHour;

        internal static int DaysPerYear => GameCalendar.DaysPerYear;

        /// <summary>The city's price level in thousandths of the start's.</summary>
        internal static int Prices => Mathf.Max(1, RealLifeOverhaul.Prices.Level);

        internal static float Cash => SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;

        internal static bool Outdoors => PlayerHelper.PlayerController != null && !BuildingManager.IsInsideBuilding;

        internal static Vector3 Player => PlayerHelper.PlayerController != null ? PlayerHelper.GetPosition() : Vector3.zero;

        /// <summary>The way the player faces, flat on the ground.</summary>
        internal static Vector3 Ahead
        {
            get
            {
                Vector3 ahead = PlayerHelper.PlayerController != null ? PlayerHelper.PlayerController.transform.forward : Vector3.forward;
                ahead.y = 0f;
                return ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward;
            }
        }

        // ------------------------------------------------------------------ money

        /// <summary>Takes an amount if the cash is there, as the game's own purchases do; a business cost is marked deductible by its label's words.</summary>
        internal static bool Take(long amount, string key, string name, bool deductible)
        {
            return amount <= 0 || (SaveGameManager.Current != null && GameManager.ChangeMoneySafe(-(float)amount, Info(key, name, deductible), null, null, false, true));
        }

        /// <summary>Takes an amount whether or not it is there: wages, interest and hire run on as the game's own loan interest does.</summary>
        internal static void Force(long amount, string key, string name, bool deductible)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                GameManager.ChangeMoneySafe(-(float)amount, Info(key, name, deductible), null, null, true);
            }
        }

        internal static void Give(long amount, string key, string name)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                GameManager.ChangeMoneySafe(amount, Info(key, name, false), null, null, true);
            }
        }

        // A dictionary each: the game keeps a transaction's data for as long as it lists the transaction.
        private static TransactionInfo Info(string key, string name, bool deductible)
        {
            var info = new TransactionInfo(key, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } });
            if (deductible)
            {
                info.SetTaxDeductibleName((key + "_label").GetLocalization());   // as the game passes a business's name: words, read the same in a save without the mod
            }
            return info;
        }

        // ------------------------------------------------------------------ the player's places

        /// <summary>The player's businesses of the construction company's type, in the save's order.</summary>
        internal static void Offices(List<PlaceView> into)
        {
            into.Clear();
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (ConstructionType.Is(reg) && reg.RentedByPlayer && GoingPublic.Mine(reg) && reg.Address != null)
                {
                    into.Add(View(reg));
                }
            }
        }

        /// <summary>The player's warehouses, a yard's candidates: rented or owned, of the game's warehouse type.</summary>
        internal static void Warehouses(List<PlaceView> into)
        {
            into.Clear();
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || reg.Address == null || !(reg.RentedByPlayer || reg.BuildingOwnedByPlayer) || !GoingPublic.Mine(reg))
                {
                    continue;
                }
                Building b = reg.BuildingCached;
                if (b != null && b.BuildingType == Warehouse)
                {
                    into.Add(View(reg));
                }
            }
        }

        private static PlaceView View(BuildingRegistration reg)
        {
            Building b = reg.BuildingCached;
            return new PlaceView
            {
                Place = Place(reg.Address.streetName, reg.Address.streetNumber), Street = reg.Address.streetName, Number = reg.Address.streetNumber,
                Name = Words(reg), District = b != null ? b.Neighbourhood : string.Empty
            };
        }

        /// <summary>The buildings the player owns (the game's real estate), a rebuild's candidates: never a Hamptons house.</summary>
        internal static void Owned(List<BuildingView> into)
        {
            into.Clear();
            List<RealEstate> estate = SaveGameManager.Current != null ? SaveGameManager.Current.realEstate : null;
            for (int i = 0; estate != null && i < estate.Count; i++)
            {
                RealEstate r = estate[i];
                Building b = r != null && r.address != null ? BuildingHelper.GetBuilding(r.address) : null;
                if (b == null || b.IsHamptonsHouse() || b.SpecialService != null)
                {
                    continue;
                }
                into.Add(View(b));
            }
        }

        /// <summary>One of the city's buildings by address, as the company reads it; false for none.</summary>
        internal static bool Building(string street, int number, out BuildingView view)
        {
            view = default(BuildingView);
            Building b = string.IsNullOrEmpty(street) ? null : BuildingHelper.GetBuilding(new Address(street, number));
            if (b == null)
            {
                return false;
            }
            view = View(b);
            return true;
        }

        private static BuildingView View(Building b)
        {
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(b.Address);
            return new BuildingView
            {
                Street = b.StreetName, Number = b.StreetNumber, Name = reg != null ? Words(reg) : AddressText(b.StreetName, b.StreetNumber), District = b.Neighbourhood,
                Type = b.BuildingType, Size = b.BuildingSize, Version = b.BuildingVersion, TotalSqm = b.totalSqm, Traffic = b.trafficIndex
            };
        }

        /// <summary>True while the player owns a building (the game's real estate).</summary>
        internal static bool Owns(string street, int number)
        {
            List<RealEstate> estate = SaveGameManager.Current != null ? SaveGameManager.Current.realEstate : null;
            for (int i = 0; estate != null && i < estate.Count; i++)
            {
                Address a = estate[i] != null ? estate[i].address : null;
                if (a != null && a.streetNumber == number && a.streetName == street)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A building of the city's with a business of the game's own in it (a client for a tender): the open city's
        /// buildings walked from a start of the caller's, the first that fits: a door, an office or a shop, not the
        /// player's, not one the mod holds, never one a DLC keeps. False for none.
        /// </summary>
        internal static bool Client(int start, out BuildingView view)
        {
            view = default(BuildingView);
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            if (all == null || all.Length == 0)
            {
                return false;
            }
            for (int k = 0; k < all.Length; k++)
            {
                CityBuildingController c = all[(int)((uint)(start + k) % (uint)all.Length)];
                Building b = c != null ? c.building : null;
                if (b == null || !CivicCity.HasDoor(c) || b.SpecialService != null || b.IsHamptonsHouse() || SpecialBuildings.Holds(c) || GameMembers.Fenced(b)
                    || (b.BuildingType != "ba:buildingtype_retail" && b.BuildingType != "ba:buildingtype_office") || MadeLike(b.StreetName))
                {
                    continue;
                }
                BuildingRegistration reg = b.GetRegistration();
                if (reg == null || reg.RentedByPlayer || reg.BuildingOwnedByPlayer || reg.businessTypeName == "ba:businesstype_empty" || !GoingPublic.Mine(reg) && GoingPublic.Foreign(reg))
                {
                    continue;
                }
                view = View(b);
                return true;
            }
            return false;
        }

        // A street of the mod's own (a made building's): never a client.
        private static bool MadeLike(string street) => street != null && street.StartsWith("reallife", StringComparison.Ordinal);

        /// <summary>
        /// The player's businesses a refit can be done in: an established business the player runs in a shop or an office, rented or their own
        /// (theirs in a Going Public session), with a door on the street, never a Hamptons house; in the save's order.
        /// </summary>
        internal static void Businesses(List<BusinessView> into)
        {
            into.Clear();
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (BusinessOf(all[i], out BusinessView view))
                {
                    into.Add(view);
                }
            }
        }

        /// <summary>One business of the player's by its address, as <see cref="Businesses"/> reads it; false for none.</summary>
        internal static bool Business(string street, int number, out BusinessView view)
        {
            view = default(BusinessView);
            BuildingRegistration reg = string.IsNullOrEmpty(street) ? null : BuildingHelper.GetBuildingRegistration(new Address(street, number));
            return BusinessOf(reg, out view);
        }

        private static bool BusinessOf(BuildingRegistration reg, out BusinessView view)
        {
            view = default(BusinessView);
            if (reg == null || reg.Address == null || !reg.RentedByPlayer || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness
                || string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == "ba:businesstype_empty")
            {
                return false;
            }
            Building b = reg.BuildingCached;
            if (b == null || b.IsHamptonsHouse() || (b.BuildingType != "ba:buildingtype_retail" && b.BuildingType != "ba:buildingtype_office"))
            {
                return false;
            }
            CityBuildingController c = CivicCity.Find(b.StreetName, b.StreetNumber);
            if (!CivicCity.HasDoor(c))
            {
                return false;
            }
            view = new BusinessView
            {
                Street = b.StreetName, Number = b.StreetNumber, Name = Words(reg), District = b.Neighbourhood, Sqm = b.totalSqm, Office = b.BuildingType == "ba:buildingtype_office",
                Owned = reg.BuildingOwnedByPlayer, Door = c.entranceDoors[0].doorTransform.position
            };
            return true;
        }

        // ------------------------------------------------------------------ staff

        /// <summary>The employees of an office of the player's: the game's id, name and main skill.</summary>
        internal static void Staff(string street, int number, List<StaffView> into)
        {
            into.Clear();
            List<EmployeeInstance> all = SaveGameManager.Current != null ? SaveGameManager.Current.EmployeeInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                Address a = e != null ? e.assignedAddress : null;
                if (a == null || a.streetNumber != number || a.streetName != street)
                {
                    continue;
                }
                into.Add(View(e));
            }
        }

        /// <summary>One employee by the game's id; false for one no longer there.</summary>
        internal static bool Employee(string id, out StaffView view)
        {
            view = default(StaffView);
            EmployeeInstance e = string.IsNullOrEmpty(id) ? null : EmployeeHelper.GetEmployeeById(id);
            if (e == null)
            {
                return false;
            }
            view = View(e);
            return true;
        }

        private static StaffView View(EmployeeInstance e)
        {
            var view = new StaffView { Id = e.id, Name = e.characterData != null && !string.IsNullOrEmpty(e.characterData.name) ? e.characterData.name : "?" };
            if (e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null)
            {
                view.Skill = e.characterData.skills[0].name;
                view.Value = Mathf.Clamp(e.characterData.skills[0].GetRoundedValue(), 0, 100);
            }
            return view;
        }

        // ------------------------------------------------------------------ the game's figures

        /// <summary>A building type's value multiplier, and whether its value falls with traffic (the game's tag); 1 for a type the game lacks.</summary>
        internal static double TypeMultiplier(string type, out bool lowerForTraffic)
        {
            lowerForTraffic = false;
            BuildingTypeData data = string.IsNullOrEmpty(type) ? null : BuildingTypeHelper.GetData(type);
            if (data == null)
            {
                return 1.0;
            }
            lowerForTraffic = data.HasTag(TagRef.Buildingtypetag.lowerrentforhightraffic);
            return data.buildingPriceMultiplier > 0f ? data.buildingPriceMultiplier : 1.0;
        }

        /// <summary>A district's price a square metre as the game has it: its base price times its real estate multiplier; NaN for one it lacks.</summary>
        internal static double DistrictPrice(string district)
        {
            if (string.IsNullOrEmpty(district))
            {
                return double.NaN;
            }
            BigAmbitions.Neighborhoods.NeighborhoodData data = NeighborhoodHelper.GetData(district);
            return data != null ? data.baseBuildingPricePerSqm * (double)data.realEstateMultiplier : double.NaN;
        }

        /// <summary>
        /// The district's price a square metre (its base price times its real estate multiplier) of the city's building nearest
        /// a place: what a test's lot is priced at. NaN for none. A walk over the city's buildings, on a test's press only.
        /// </summary>
        internal static double PriceNear(Vector3 at)
        {
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            Building best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                if (c == null || c.building == null)
                {
                    continue;
                }
                Vector3 d = c.transform.position - at;
                d.y = 0f;
                if (d.sqrMagnitude < bestSqr)
                {
                    bestSqr = d.sqrMagnitude;
                    best = c.building;
                }
            }
            return best != null ? DistrictPrice(best.Neighbourhood) : double.NaN;
        }

        internal static string Place(string street, int number) => (street ?? string.Empty) + "#" + number.ToString(CultureInfo.InvariantCulture);

        internal static string AddressText(string street, int number) => string.IsNullOrEmpty(street) ? ConstructionText.T("reallife_bld_place_test_lot") : CivicCity.AddressText(street, number);

        /// <summary>A building's first door, for the ribbon on its opening day; false for none.</summary>
        internal static bool Door(string street, int number, out Vector3 door, out Vector3 outward)
        {
            door = Vector3.zero;
            outward = Vector3.forward;
            CityBuildingController c = CivicCity.Find(street, number);
            if (!CivicCity.HasDoor(c))
            {
                return false;
            }
            Transform t = c.entranceDoors[0].doorTransform;
            door = t.position;
            outward = t.forward;
            return true;
        }

        /// <summary>The words of a district's game id (also its text key).</summary>
        internal static string DistrictWords(string district) => string.IsNullOrEmpty(district) ? string.Empty : district.GetLocalization();

        /// <summary>A skill of the game's in the player's language (its name is its text key).</summary>
        internal static string SkillWords(string skill) => string.IsNullOrEmpty(skill) ? string.Empty : skill.GetLocalization();

        private static string Words(BuildingRegistration reg)
        {
            try
            {
                return reg.GetDisplayName();
            }
            catch (Exception)
            {
                return reg.Address != null ? AddressText(reg.Address.streetName, reg.Address.streetNumber) : "?";
            }
        }

        // ------------------------------------------------------------------ notices

        /// <summary>A notice of the game's: held for the morning while the player sleeps, else shown with a press that opens the app.</summary>
        internal static void Tell(NotificationType type, string key, Dictionary<string, string> data, Action open)
        {
            if (MorningNews.Holding)
            {
                MorningNews.Show(type, key, data, 11f);
                return;
            }
            Notifications.Show(type, key, data, 11f, null, open != null ? Guard.Wrap(open) : null, true, false);
        }
    }
}
