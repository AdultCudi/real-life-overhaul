using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.DayNightCycle;
using Buildings;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>One of the player's places as the utilities see it.</summary>
    public struct PlaceInfo
    {
        public BuildingRegistration Reg;
        public string Place;
        public string Name;      // the business's name, or the address of a home
        public PlaceKind Kind;
        public bool Owned;
        public int Sqm;
    }

    /// <summary>Everything the utilities read from or do to the game: places, opening days, the cash and transactions.</summary>
    public static class UtilitiesGame
    {
        private const string Residential = "ba:buildingtype_residential", HeadquartersType = "ba:businesstype_headquarters";
        private const string BillsDeductKey = "reallife_util_deduct", CompanyDeductKey = "reallife_broadband_deduct";

        public static GameInstance Save => SaveGameManager.Current;
        public static int Today => Save != null ? Save.Day : 0;
        public static long Cash => Save != null ? EndScaleRules.Whole(Save.Money) : 0;   // a long by hand: a float past $2,147,483,647 cast to an int is int.MinValue on Mono

        /// <summary>A place's address, written as business pressure, Company and Brands write it.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The player's places today; underground car parks go to <paramref name="unserved"/> instead.</summary>
        public static void Places(List<PlaceInfo> into, List<BuildingRegistration> unserved = null)
        {
            into.Clear();
            unserved?.Clear();
            GameInstance save = Save;
            if (save == null || save.BuildingRegistrations == null)
            {
                return;
            }
            foreach (BuildingRegistration reg in save.BuildingRegistrations)
            {
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasValidAddress)
                {
                    continue;
                }
                Building building = BuildingHelper.GetBuilding(reg.Address);
                if (building == null)
                {
                    continue;
                }
                if (!UtilitiesRules.Serves(reg.StreetName, building.BuildingSize))
                {
                    unserved?.Add(reg);
                    continue;
                }
                bool home = building.BuildingType == Residential;
                into.Add(new PlaceInfo
                {
                    Reg = reg,
                    Place = Place(reg),
                    Name = home || string.IsNullOrEmpty(reg.BusinessName) ? reg.Address.ToFormattedString() : reg.BusinessName,
                    Kind = home ? PlaceKind.Home : KindOf(reg.businessTypeName),
                    Owned = reg.BuildingOwnedByPlayer,
                    Sqm = Mathf.Max(0, building.totalSqm)
                });
            }
        }

        /// <summary>What a business uses most of, by the game's business types.</summary>
        public static PlaceKind KindOf(string type)
        {
            switch (type)
            {
                case "ba:businesstype_coffeeshop":
                case "ba:businesstype_fastfoodrestaurant":
                case "ba:businesstype_supermarket":
                case "ba:businesstype_fruitandvegetablestore":
                    return PlaceKind.FoodAndDrink;
                case "ba:businesstype_hairdresser":
                case "ba:businesstype_clinic":
                    return PlaceKind.Salon;
                case "ba:businesstype_gym":
                    return PlaceKind.Gym;
                case "ba:businesstype_nightclub":
                case "ba:businesstype_casino":
                case "ba:businesstype_cinema":
                case "ba:businesstype_theater":
                    return PlaceKind.Nightlife;
                case "ba:businesstype_warehouse":
                case "ba:businesstype_empty":
                case "ba:businesstype_movingservice":
                    return PlaceKind.Storage;
                case "ba:businesstype_factory":
                case "ba:businesstype_truckgarage":
                    return PlaceKind.Industry;
                case "ba:businesstype_headquarters":
                case "ba:businesstype_lawfirm":
                case "ba:businesstype_graphicdesigner":
                case "ba:businesstype_marketingagency":
                case "ba:businesstype_recruitmentagency":
                case "ba:businesstype_webdevelopmentagency":
                case "ba:businesstype_eventplanningagency":
                case "ba:businesstype_travelagency":
                case "ba:businesstype_interiorinstallationfirm":
                case "ba:businesstype_importexport":
                    return PlaceKind.Office;
                default:
                    // the tech sector's own types are offices
                    return type != null && type.StartsWith("reallife:businesstype_", System.StringComparison.Ordinal) ? PlaceKind.Office : PlaceKind.Retail;
            }
        }

        /// <summary>A business keeps opening hours on a day of its week (BuildingRegistration.scheduleDays).</summary>
        public static bool OpenOn(BuildingRegistration reg, int day)
        {
            if (reg == null || reg.scheduleDays == null)
            {
                return false;
            }
            DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(day);
            for (int i = 0; i < reg.scheduleDays.Count; i++)
            {
                ScheduleDay schedule = reg.scheduleDays[i];
                if (schedule != null && schedule.day == weekday)
                {
                    return schedule.isOpen && schedule.GetHoursOpen > 0;
                }
            }
            return false;
        }

        /// <summary>The people at a home: the player and whoever lives there with them; a business has none.</summary>
        public static int People(PlaceInfo place)
        {
            if (place.Kind != PlaceKind.Home)
            {
                return 0;
            }
            Func<BuildingRegistration, int> household = UtilitiesBridges.Household;
            return 1 + Mathf.Max(0, household != null ? household(place.Reg) : 0);
        }

        /// <summary>Money taken for a bill, fee or deposit, a business's deductible ones deducted by the game's tax; false if refused.</summary>
        public static bool Pay(int amount, string key, BuildingRegistration reg, bool force = false, bool deductible = false)
        {
            if (amount <= 0)
            {
                return true;
            }
            var info = new TransactionInfo(key, new Dictionary<string, string>(1) { { "place", reg != null ? reg.Address.ToFormattedString() : string.Empty } });
            if (deductible && reg != null && !IsHome(reg))
            {
                info.SetTaxDeductibleName(BillsDeductKey.GetLocalization());
            }
            return GameManager.ChangeMoneySafe(-amount, info, null, reg != null ? reg.Address : null, force: force, showNotification: true);
        }

        public static bool IsHome(BuildingRegistration reg)
        {
            Building building = reg != null ? BuildingHelper.GetBuilding(reg.Address) : null;
            return building != null && building.BuildingType == Residential;
        }

        /// <summary>The player's headquarters (the game has one at most), or null.</summary>
        public static BuildingRegistration Headquarters()
        {
            GameInstance save = Save;
            for (int i = 0; save != null && save.BuildingRegistrations != null && i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration reg = save.BuildingRegistrations[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.businessTypeName == HeadquartersType)
                {
                    return reg;
                }
            }
            return null;
        }

        /// <summary>The broadband company's money at its headquarters: running costs deductible and taken whatever the cash; false if refused.</summary>
        public static bool Book(long amount, string key, BuildingRegistration hq, string category, bool running = true)
        {
            if (amount == 0)
            {
                return true;
            }
            var data = new Dictionary<string, string>(1)
            {
                { "place", hq != null ? (string.IsNullOrEmpty(hq.BusinessName) ? hq.Address.ToFormattedString() : hq.BusinessName) : string.Empty }
            };
            TransactionInfo info = category != null ? new TransactionInfo(key, category, data) : new TransactionInfo(key, data);
            if (amount < 0 && running)
            {
                info.SetTaxDeductibleName(CompanyDeductKey.GetLocalization());
            }
            return GameManager.ChangeMoneySafe(amount, info, null, hq != null ? hq.Address : null, force: running && amount < 0, showNotification: false);
        }

        /// <summary>Money given back: a deposit, or the rest of one after a last bill.</summary>
        public static void Give(int amount, string key, BuildingRegistration reg)
        {
            if (amount <= 0)
            {
                return;
            }
            var info = new TransactionInfo(key, new Dictionary<string, string>(1) { { "place", reg != null ? reg.Address.ToFormattedString() : string.Empty } });
            GameManager.ChangeMoneySafe(amount, info, null, reg != null ? reg.Address : null, force: true, showNotification: true);
        }
    }
}
