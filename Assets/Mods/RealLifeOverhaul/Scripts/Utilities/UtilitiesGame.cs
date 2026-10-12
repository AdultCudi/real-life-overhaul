using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.DayNightCycle;
using Buildings;
using Buildings.Schedule;
using Helpers;
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

    /// <summary>
    /// Everything the utilities read from or do to the game, in one place: the player's places (homes and businesses
    /// they rent or own: GameInstance.BuildingRegistrations, RentedByPlayer), their floor (Building.totalSqm), what kind
    /// of business each is (businessTypeName), whether a business opened on a day (its week of opening hours), the cash,
    /// and the transactions the bills are paid by.
    /// </summary>
    public static class UtilitiesGame
    {
        private const string Residential = "ba:buildingtype_residential";

        public static GameInstance Save => SaveGameManager.Current;
        public static int Today => Save != null ? Save.Day : 0;
        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;

        /// <summary>A place's address, written as business pressure writes a place (as Company and Brands write it).</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The player's places today: every home and business they rent or own.</summary>
        public static void Places(List<PlaceInfo> into)
        {
            into.Clear();
            GameInstance save = Save;
            if (save == null || save.BuildingRegistrations == null)
            {
                return;
            }
            foreach (BuildingRegistration reg in save.BuildingRegistrations)
            {
                if (reg == null || !reg.RentedByPlayer || !reg.HasValidAddress)
                {
                    continue;
                }
                Building building = BuildingHelper.GetBuilding(reg.Address);
                if (building == null)
                {
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
                    return PlaceKind.Retail;
            }
        }

        /// <summary>True if a business keeps any opening hours on a day (its week of them: BuildingRegistration.scheduleDays).</summary>
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

        /// <summary>The people at a place: one at a home (the player) and whoever lives there with them; a business has none.</summary>
        public static int People(PlaceInfo place)
        {
            if (place.Kind != PlaceKind.Home)
            {
                return 0;
            }
            Func<BuildingRegistration, int> household = UtilitiesBridges.Household;
            return 1 + Mathf.Max(0, household != null ? household(place.Reg) : 0);
        }

        /// <summary>Money taken for a bill, a fee or a deposit. False if the game refused it (not enough cash).</summary>
        public static bool Pay(int amount, string key, BuildingRegistration reg, bool force = false)
        {
            if (amount <= 0)
            {
                return true;
            }
            var info = new TransactionInfo(key, new Dictionary<string, string>(1) { { "place", reg != null ? reg.Address.ToFormattedString() : string.Empty } });
            return GameManager.ChangeMoneySafe(-amount, info, null, reg != null ? reg.Address : null, force: force, showNotification: true);
        }

        /// <summary>Money given back (a deposit, the rest of one after a last bill).</summary>
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
