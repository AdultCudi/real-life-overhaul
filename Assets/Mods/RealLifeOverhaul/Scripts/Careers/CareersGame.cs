using System;
using System.Collections.Generic;
using BAModAPI;
using Buildings;
using Helpers;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Everything education and careers read from or do to the game: the clock, cash, energy, where the player is, money, energy spent and mood.</summary>
    internal static class CareersGame
    {
        public const string SchoolType = "ba:businesstype_school";
        public const string FeeKey = "reallife_transaction_study_fee";
        public const string EnergyKey = "RealLifeStudy";

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        public static int Today => Save != null ? Save.Day : 0;

        public static int MinuteOfDay => Save != null ? Save.Hour * 60 + Mathf.FloorToInt(Save.Minute) : 0;

        /// <summary>The game's clock in whole minutes since day 0, as its activity panel reads it (TimeHelper.NowInMinutes): what a sitting's minutes are counted by.</summary>
        public static int Clock => Save != null ? (int)TimeHelper.NowInMinutes() : 0;

        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;

        public static float Energy => Save != null ? Save.Energy : 0f;

        /// <summary>The player's age in whole years, as the game counts it.</summary>
        public static int AgeYears()
        {
            try
            {
                CharacterData data = PlayerHelper.CharacterData ?? (Save != null && Save.charactersData != null && Save.charactersData.Count > 0 ? Save.charactersData[0] : null);
                return data != null ? TimeHelper.GetYearsByDays(data.ageInDays) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static Building Inside()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return null;
            }
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null ? manager.building : null;
        }

        /// <summary>True inside the game's own school (Manhattan Business School).</summary>
        public static bool InSchool()
        {
            try
            {
                Building b = Inside();
                return b != null && b.SpecialService != null && b.SpecialService.businessTypeName == SchoolType;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True inside a building of the game's own of a kind (a recruitment office).</summary>
        public static bool InsideType(string type)
        {
            try
            {
                if (!BuildingManager.IsInsideBuilding)
                {
                    return false;
                }
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                Building b = manager != null ? manager.building : null;
                string service = b != null && b.SpecialService != null ? b.SpecialService.businessTypeName : null;
                string business = manager != null && manager.buildingRegistration != null ? manager.buildingRegistration.businessTypeName : null;
                return service == type || business == type;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool InHospital()
        {
            try
            {
                return HomePlace.InHospital();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool AtHome()
        {
            try
            {
                return HomeTidy.AtHome();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True inside the building at an address.</summary>
        public static bool InsideAt(string street, int number)
        {
            try
            {
                Building b = Inside();
                return b != null && !string.IsNullOrEmpty(street) && b.Address.streetName == street && b.Address.streetNumber == number;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True if nothing of the game's has the clock or the activity panel: an activity of the mod's may be offered.</summary>
        public static bool Free()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && !PlayerActivity.PlayerActivityUI.IsPanelOpen && (uis.timeMachine == null || !uis.timeMachine.isRunning)
                   && string.IsNullOrEmpty(Save != null ? Save.ActiveVehicleId : null) && !GameMembers.PlayerDead && !EnergyHelper.goingToHospital;
        }

        private static CityManager _schoolOf;
        private static string _schoolStreet;
        private static int _schoolNumber;

        /// <summary>The university's permanent building's address as the game writes it ("26 2nd Avenue"), for the Guide's degrees page.</summary>
        public static string HomeAddress()
        {
            try
            {
                return CampusRules.HomeAddress(StreetName(CampusRules.HomeStreet));
            }
            catch (Exception)
            {
                return CampusRules.HomeAddress(null);   // (outside the game: the Guide's page made offline)
            }
        }

        // The game's name for a street, or null where it does not know the street (its streets not loaded).
        private static string StreetName(string street)
        {
            string name = Streets.AddressHelper.GetStreetNameLocalized(street);
            return string.IsNullOrEmpty(name) || name == street ? null : name;
        }

        /// <summary>The address of the game's school, for the Planner and the map; looked up once a city (one walk over its building list).</summary>
        public static bool SchoolAddress(out string street, out int number)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (!ReferenceEquals(city, _schoolOf))
            {
                _schoolOf = city;
                _schoolStreet = null;
                CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    Building b = all[i] != null ? all[i].building : null;
                    if (b != null && b.SpecialService != null && b.SpecialService.businessTypeName == SchoolType)
                    {
                        _schoolStreet = b.Address.streetName;
                        _schoolNumber = b.Address.streetNumber;
                        break;
                    }
                }
            }
            street = _schoolStreet;
            number = _schoolNumber;
            return !string.IsNullOrEmpty(street);
        }

        /// <summary>A charge of whole money with a line of the bank history naming what for. False if there was not the money.</summary>
        public static bool Charge(int whole, string key, string name)
        {
            if (whole <= 0)
            {
                return true;
            }
            if (Cash < whole)
            {
                return false;
            }
            var info = new TransactionInfo(key, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } });
            return GameManager.ChangeMoneySafe(-whole, info, null, null, force: false);
        }

        /// <summary>Money paid in, whole, with its line of the bank history.</summary>
        public static void Pay(float whole, string key, string category, string name)
        {
            if (whole <= 0f)
            {
                return;
            }
            var info = category != null
                ? new TransactionInfo(key, category, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } })
                : new TransactionInfo(key, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } });
            GameManager.ChangeMoneySafe(whole, info, null, null, force: true);
        }

        /// <summary>A charge that is taken whatever the cash (a loan's repayment, a fine): the game lets money go below zero.</summary>
        public static void Take(float whole, string key, string name)
        {
            if (whole <= 0f)
            {
                return;
            }
            GameManager.ChangeMoneySafe(-whole, new TransactionInfo(key, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } }), null, null, force: true);
        }

        public static void Tiring(bool on)
        {
            try
            {
                if (on)
                {
                    EnergyHelper.AddEnergySpender(EnergyKey, EnergyConsumption.Low);
                }
                else
                {
                    EnergyHelper.RemoveEnergySpender(EnergyKey);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        public static void Mood(string modifier)
        {
            try
            {
                ModState.AddModifier(modifier);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
