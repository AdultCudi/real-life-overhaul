using System;
using System.Collections;
using System.Collections.Generic;
using BigAmbitions.Tags;
using Buildings;
using Helpers;
using IngameDebugConsole;
using Streets;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The game's own calls that move the player, pay him and start a business, each in a small method of its own.</summary>
    internal static class SelfTestWorld
    {
        public static bool Console(string command, out string why)
        {
            why = null;
            if (DebugLogManager.Instance == null)
            {
                why = "the game's debug console is not built in this scene (DebugLogManager)";
                return false;
            }
            DebugLogConsole.ExecuteCommand(command);
            return true;
        }

        public static void SetMoney(float amount)
        {
            GameManager.Command_SetMoney(amount);
        }

        public static bool Teleport(string address, string business, bool inside, out string why)
        {
            why = null;
            if (!GameManager.IsPlayerWalking)
            {
                why = "the player is not walking (in a car, a screen or a building): the game's teleport needs him on foot";
                return false;
            }
            if (address != null)
            {
                if (!ParseAddress(address, out int number, out string street))
                {
                    why = "'" + address + "' is not an address (a number and a street: 12 5th Avenue)";
                    return false;
                }
                Building building = BuildingHelper.GetBuilding(new Address(street, number));
                if (building == null)
                {
                    why = "no building stands at " + address;
                    return false;
                }
                if (inside)
                {
                    InstanceBehavior<BuildingManager>.Instance.EnterBuilding(building);
                }
                else
                {
                    GameManager.Command_TeleportPlayerToAddress(number, street);
                }
                return true;
            }
            GameManager.Command_TeleportPlayerToBusiness(business, 0);
            if (inside)
            {
                why = "inside works with an address only";
                return false;
            }
            return true;
        }

        /// <summary>A monologue put on the game's own speech card, through the game's own queue.</summary>
        public static bool Monologue(string key)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.monologueUI == null)
            {
                return false;
            }
            uis.monologueUI.EnqueueMonologue(key, null, null);
            return true;
        }

        /// <summary>A window of the game's own opened by its own call: help, blueprints or the phone's menu.</summary>
        public static bool GameOpen(string what, out string why)
        {
            why = null;
            switch (what)
            {
                case "help":
                    HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
                    if (help != null)
                    {
                        help.Toggle(true);
                        return true;
                    }
                    break;
                case "blueprints":
                    UIs uis = InstanceBehavior<UIs>.Instance;
                    if (uis != null && uis.miniMenuUI != null && uis.miniMenuUI.blueprintsPanel != null)
                    {
                        uis.miniMenuUI.blueprintsPanel.Show();
                        return true;
                    }
                    break;
                case "phone":
                    SelfTestGame.OpenPhone();
                    return true;
                default:
                    why = "'" + what + "' is not one of help, blueprints, phone";
                    return false;
            }
            why = "the game has no '" + what + "' built in this scene";
            return false;
        }

        public static bool ParseAddress(string text, out int number, out string street)
        {
            number = 0;
            street = null;
            int space = text.IndexOf(' ');
            if (space <= 0 || !int.TryParse(text.Substring(0, space), out number))
            {
                return false;
            }
            street = text.Substring(space + 1).Trim();
            return street.Length > 0;
        }

        public static bool FoundBusiness(string type, string name, string address, out string words)
        {
            words = null;
            BusinessType data = BusinessTypeHelper.GetData(type);
            if (data == null)
            {
                words = "no business type is named '" + type + "'";
                return false;
            }
            Building building = null;
            if (address != null)
            {
                if (!ParseAddress(address, out int number, out string street) || (building = BuildingHelper.GetBuilding(new Address(street, number))) == null)
                {
                    words = "no building stands at '" + address + "'";
                    return false;
                }
            }
            else
            {
                building = Cheapest(type);
                if (building == null)
                {
                    words = "no free building takes a '" + type + "' (the building types list the business types they allow)";
                    return false;
                }
            }
            BuildingRegistration reg = building.GetRegistration();
            if (reg == null || GameDlc.Gated(building))
            {
                words = "the building has no registration, or belongs to a DLC the mod leaves alone";
                return false;
            }
            if (data.courseRequired != DiplomaName.Undefined && !EducationHelper.HasCompletedDiploma(data.courseRequired))
            {
                EducationHelper.UnlockAllCourses();   // the start screen refuses without the course; a test save has them
            }
            BuildingHelper.RentBuilding(building, 0f, 0f);   // as the console's RentBuilding does: no deposit
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Selftest " + type.Substring(type.IndexOf('_') + 1) + " " + SaveGameManager.Current.BuildingRegistrations.Count;
            }
            reg.BusinessName = name;
            reg.businessTypeName = type;
            reg.TemporarilyClose(type != "ba:businesstype_warehouse");
            BusinessHelper.UpdateCustomerCapacity(reg);
            reg.creationDay = SaveGameManager.Current.Day;
            if (BusinessTypeHelper.GetData(reg).HasTag(TagRef.Businesstag.generatesrevenue))
            {
                CustomerDraw.Redraw(reg);   // the customers drawn as the mod draws them everywhere (Core/CustomerDraw), the game's own redraw behind it
                BusinessHelper.UpdatePromotion(reg);
                reg.UpdateSecurityLevel();
            }
            BusinessHelper.GenerateMissingTodoTasksForBusiness(reg);
            GameEvent.Invoke("ba:gameevent_newbusiness");
            InstanceBehavior<CityManager>.Instance.FindCityBuildingController(reg.Address)?.UpdatePoi();
            InstanceBehavior<UIs>.Instance.mapFilters.ApplyFilters();
            GlobalEvents.onBuildingRegistrationChange?.Invoke(reg.Address);
            words = "'" + name + "' started as a " + type + " at " + reg.Address.ToFormattedString();
            return true;
        }

        // The building the type may be started in that is free and costs least to rent.
        private static Building Cheapest(string type)
        {
            Building best = null;
            float bestRent = float.MaxValue;
            List<Building> all = BuildingHelper.allBuildings;
            for (int i = 0; i < all.Count; i++)
            {
                Building b = all[i];
                BuildingRegistration reg = b != null ? b.GetRegistration() : null;
                // a DLC's building is left out as everywhere in the mod
                if (reg == null || !reg.AvailableForRent || reg.RentedByPlayer || b.BuildingType == "ba:buildingtype_residential" || GameDlc.Gated(b))
                {
                    continue;
                }
                BuildingTypeData kind = BuildingTypeHelper.GetData(b);
                if (kind == null || kind.availableBusinessTypes == null || Array.IndexOf(kind.availableBusinessTypes, type) < 0)
                {
                    continue;
                }
                float rent = b.GetBuildingDailyMarketRent();
                if (rent < bestRent)
                {
                    bestRent = rent;
                    best = b;
                }
            }
            return best;
        }
    }
}
