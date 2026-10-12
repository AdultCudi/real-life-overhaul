using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Items;
using Buildings;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>
    /// The one place where the roommate touches the game and the mod's home life: which home, its furniture and its
    /// rent, money in and out, the fridges, the floor's dirt, the player's mood, hunger and sleep.
    ///
    ///   READ   SaveGameManager.Current.BuildingRegistrations (RentedByPlayer, GetBuildingType, StreetName, StreetNumber,
    ///          RentPerDay, BuildingOwnedByPlayer, itemInstances), Building.GetBuildingDailyMarketRent, BuildingManager
    ///          (IsInsideBuilding, buildingRegistration), TimeHelper (CurrentDay, CurrentHour), SaveGameManager.Current.Hunger
    ///   WRITE  GameManager.ChangeMoneySafe (a transaction of the mod's own), EnergyHelper.SetCurrentHunger; through the
    ///          mod's home life: HomeTidy.Change (the game's own floor dirt of the home), HomeState.PutBy and Leftovers
    ///          (portions put by), HomePlace.Take (one unit of the fridges' cargo), HomeState.SleepDebt; through
    ///          Core/ModState: a happiness modifier the mod already has
    ///
    /// Nothing here runs by itself. The furniture is counted when a page is opened or a day begins (one pass over the
    /// home's items); everything else is one call on an event.
    /// </summary>
    public static class RoommateGame
    {
        private const string Residential = "ba:buildingtype_residential";

        private static readonly List<PantryItem> Pantry = new List<PantryItem>(16);
        private static readonly CookPlan OneUnit = new CookPlan();

        public static IModLogger Logger;

        public static int Day => TimeHelper.CurrentDay;
        public static int Hour => TimeHelper.CurrentHour;
        public static int Stamp => TimeHelper.CurrentDay * 24 + TimeHelper.CurrentHour;
        public static int Weekday(int day) => Planner.PlannerTime.Weekday(day);

        private static bool HomeLifeOn => Settings.HomeLife && HomeState.Loaded;

        // ---------------------------------------------------------------- the home

        /// <summary>The player's home at an address, while they still have it; null otherwise.</summary>
        public static BuildingRegistration HomeAt(string street, int number)
        {
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && !string.IsNullOrEmpty(street) && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r != null && r.RentedByPlayer && r.StreetNumber == number && r.StreetName == street && IsHome(r))
                {
                    return r;
                }
            }
            return null;
        }

        /// <summary>A home of the player's to share: the one they stand in, else the first they have. Null with none.</summary>
        public static BuildingRegistration AnyHome()
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (BuildingManager.IsInsideBuilding && manager != null && manager.buildingRegistration != null && manager.buildingRegistration.RentedByPlayer
                && IsHome(manager.buildingRegistration))
            {
                return manager.buildingRegistration;
            }
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].RentedByPlayer && IsHome(all[i]))
                {
                    return all[i];
                }
            }
            return null;
        }

        private static bool IsHome(BuildingRegistration r)
        {
            try
            {
                return r.GetBuildingType() == Residential;
            }
            catch (Exception)
            {
                return false;   // a registration the game has no building for is nobody's home
            }
        }

        /// <summary>True while the player stands inside that home.</summary>
        public static bool Inside(string street, int number)
        {
            if (!BuildingManager.IsInsideBuilding || string.IsNullOrEmpty(street))
            {
                return false;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            BuildingRegistration r = manager != null ? manager.buildingRegistration : null;
            return r != null && r.RentedByPlayer && r.StreetNumber == number && r.StreetName == street;
        }

        /// <summary>The beds and sofas a home is furnished with: one pass over its items, by what the game calls them.</summary>
        public static void Furniture(BuildingRegistration home, out int beds, out int sofas)
        {
            beds = 0;
            sofas = 0;
            if (home == null || home.itemInstances == null)
            {
                return;
            }
            foreach (KeyValuePair<string, ItemInstance> pair in home.itemInstances)
            {
                string name = pair.Value != null ? pair.Value.itemName : null;
                if (RoommateRules.IsBed(name))
                {
                    beds++;
                }
                else if (RoommateRules.IsSofa(name))
                {
                    sofas++;
                }
            }
        }

        /// <summary>The room a home has for a roommate now.</summary>
        public static RoomKind Room(BuildingRegistration home, bool partnerLivesIn)
        {
            Furniture(home, out int beds, out int sofas);
            return RoommateRules.Room(beds, sofas, partnerLivesIn);
        }

        /// <summary>
        /// What a day of the home costs: the rent the player pays, or for a home they own (or one the game charges
        /// nothing for) what the game says it would let for, at today's prices as the city's cost of living charges
        /// it. <paramref name="owned"/>: the roommate is a lodger.
        /// </summary>
        public static float RentPerDay(BuildingRegistration home, out bool owned)
        {
            owned = false;
            if (home == null)
            {
                return 0f;
            }
            try
            {
                owned = home.BuildingOwnedByPlayer;
                float level = Prices.Level / 1000f;
                if (!owned && home.RentPerDay > 0f)
                {
                    return home.RentPerDay * level;
                }
                Building building = home.BuildingCached;
                return building != null ? building.GetBuildingDailyMarketRent() * level : 0f;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return 0f;
            }
        }

        // ---------------------------------------------------------------- money

        /// <summary>Money to the player, as a line of the game's own statement.</summary>
        public static void Receive(int amount, string transaction)
        {
            if (amount > 0)
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(transaction), null, null, true);
            }
        }

        /// <summary>Money from the player. False, with the game's own notice, if they have not got it.</summary>
        public static bool Pay(int amount, string transaction)
        {
            return amount <= 0 || GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transaction), null, null, false, true);
        }

        // ---------------------------------------------------------------- the flat

        /// <summary>The home is so many points tidier or messier, on the game's own floor where the home has one. False with home life's needs off.</summary>
        public static bool Tidy(float points)
        {
            if (!HomeLifeOn || !Settings.HomeNeeds || !HomeTidy.HasHome)
            {
                return false;
            }
            HomeTidy.Change(points);
            return true;
        }

        /// <summary>A cooked portion is put by in the home's fridge, as the player's own cooking puts leftovers by. False if there is no room or no home life.</summary>
        public static bool LeaveMeal(int pick)
        {
            if (!HomeLifeOn || !Settings.HomeRoutines)
            {
                return false;
            }
            Dish dish = null;
            for (int i = 0, n = HomeBalance.Dishes.Length; i < n; i++)
            {
                Dish d = HomeBalance.Dishes[(pick + i) % n];
                if (d != null && d.Cooker && d.Portions >= 2)
                {
                    dish = d;
                    break;
                }
            }
            return dish != null && HomeState.PutBy(1, dish.Number, RoommateRules.MealFill) > 0;
        }

        /// <summary>True while a portion is put by.</summary>
        public static bool HasMeal => HomeLifeOn && HomeState.Leftovers > 0;

        /// <summary>The player eats a portion that was put by, in company. False if there is none.</summary>
        public static bool EatMeal()
        {
            if (!HasMeal || SaveGameManager.Current == null)
            {
                return false;
            }
            HomeState.Leftovers--;
            int fill = HomeState.LeftoverFill > 0 ? HomeState.LeftoverFill : HomeBalance.LeftoverSaturation;
            EnergyHelper.SetCurrentHunger(Mathf.Min(100f, SaveGameManager.Current.Hunger + fill));
            HomeState.Ate(Meal.Home);
            return true;
        }

        /// <summary>
        /// They take one unit of food: one from the fridges of the home the player stands in (the fridges are only
        /// known there), else a portion that was put by; never below what <see cref="RoommateRules.RaidTake"/>
        /// leaves. Returns the game's name of what went (a locale key), or null if nothing did.
        /// </summary>
        public static string TakeFood(int raidsThisWeek, bool stopped, bool playerInside)
        {
            if (!HomeLifeOn)
            {
                return null;
            }
            if (playerInside)
            {
                HomePlace.ReadPantry(Pantry);
                int units = 0;
                PantryItem most = null;
                for (int i = 0; i < Pantry.Count; i++)
                {
                    units += Pantry[i].Amount;
                    if (most == null || Pantry[i].Amount > most.Amount)
                    {
                        most = Pantry[i];
                    }
                }
                if (most != null && RoommateRules.RaidTake(units, raidsThisWeek, stopped) > 0)
                {
                    OneUnit.Uses.Clear();
                    OneUnit.Uses.Add(new CookUse { Id = most.Id, Amount = 1, Saturation = most.Saturation });
                    if (HomePlace.Take(OneUnit))
                    {
                        return most.Id;
                    }
                }
            }
            if (HomeState.Leftovers > 0 && RoommateRules.RaidTake(HomeState.Leftovers + RoommateRules.RaidLeaves, raidsThisWeek, stopped) > 0)
            {
                HomeState.Leftovers--;
                return "reallife_roommate_food_leftovers";
            }
            return null;
        }

        // ---------------------------------------------------------------- the player

        /// <summary>One of the moods the mod already has, through the game's own happiness modifiers.</summary>
        public static void Mood(string modifier)
        {
            ModState.AddModifier(modifier);
        }

        /// <summary>A night's sleep is so many hours shorter, where home life counts sleep. False where it does not.</summary>
        public static bool BrokenSleep(float hours)
        {
            if (!HomeLifeOn || !Settings.HomeSleep)
            {
                return false;
            }
            HomeState.SleepDebt = HomeState.Clamp(HomeState.SleepDebt + hours, 0f, HomeBalance.DebtMost);
            return true;
        }
    }
}
