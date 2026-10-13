using System;
using System.Collections.Generic;
using BAModAPI;
using Buildings;
using Helpers;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Everything education and careers read from or do to the game, in one place.
    ///
    /// READ: the day, hour and minute; the player's cash and energy; where the player is (inside the game's school, a
    /// hospital or clinic, a home of their own, a building at an address); whether the game's own activity panel or time
    /// skip is busy.
    ///
    /// DONE, each the game's own way: money is the mod's own dated transactions (GameManager.ChangeMoneySafe); a class or
    /// homework spends energy as the game's own studying does (EnergyHelper's spender, at the game's "low" rate, for as
    /// long as it runs); the mood is one of the mod's happiness modifiers. Nothing here runs by itself.
    /// </summary>
    internal static class CareersGame
    {
        public const string SchoolType = "ba:businesstype_school";
        public const string FeeKey = "reallife_transaction_study_fee";
        public const string EnergyKey = "RealLifeStudy";

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        public static int Today => Save != null ? Save.Day : 0;

        public static int MinuteOfDay => Save != null ? Save.Hour * 60 + Mathf.FloorToInt(Save.Minute) : 0;

        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;

        public static float Energy => Save != null ? Save.Energy : 0f;

        // ---------------------------------------------------------------- where the player is

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
                   && string.IsNullOrEmpty(Save != null ? Save.ActiveVehicleId : null) && !FuneralHelper.PlayerDead && !EnergyHelper.goingToHospital;
        }

        private static CityManager _schoolOf;
        private static string _schoolStreet;
        private static int _schoolNumber;

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

        // ---------------------------------------------------------------- money, energy and mood

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
