using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Items;
using Buildings;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>Where the roommate touches the game and home life: the home, its rent and furniture, money, the fridges, the player's mood and sleep.</summary>
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

        public static BuildingRegistration HomeAt(string street, int number)
        {
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && !string.IsNullOrEmpty(street) && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r != null && GoingPublic.Mine(r) && r.StreetNumber == number && r.StreetName == street && IsHome(r))
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
            if (BuildingManager.IsInsideBuilding && manager != null && manager.buildingRegistration != null && GoingPublic.Mine(manager.buildingRegistration)
                && IsHome(manager.buildingRegistration))
            {
                return manager.buildingRegistration;
            }
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && GoingPublic.Mine(all[i]) && IsHome(all[i]))
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

        public static bool Inside(string street, int number)
        {
            if (!BuildingManager.IsInsideBuilding || string.IsNullOrEmpty(street))
            {
                return false;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            BuildingRegistration r = manager != null ? manager.buildingRegistration : null;
            return r != null && GoingPublic.Mine(r) && r.StreetNumber == number && r.StreetName == street;
        }

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

        public static RoomKind Room(BuildingRegistration home, bool partnerLivesIn)
        {
            Furniture(home, out int beds, out int sofas);
            return RoommateRules.Room(beds, sofas, partnerLivesIn);
        }

        /// <summary>What a home's power and water come to a day (Utilities, through Wiring/UtilityLinks); null: none known.</summary>
        public static Func<BuildingRegistration, float> Bills;

        /// <summary>
        /// A day of the home at today's prices, with its bills: the rent the player pays, or for a home they own (or one let
        /// for nothing) what the game says it would let for. <paramref name="owned"/>: the roommate is a lodger.
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
                Func<BuildingRegistration, float> billed = Bills;
                float bills = billed != null ? Mathf.Max(0f, billed(home)) : 0f;   // the home's power and water a day (Utilities), shared with the rent
                if (!owned && home.RentPerDay > 0f)
                {
                    return home.RentPerDay * level + bills;
                }
                Building building = home.BuildingCached;
                return (building != null ? building.GetBuildingDailyMarketRent() * level : 0f) + bills;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return 0f;
            }
        }

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

        public static bool HasMeal => HomeLifeOn && HomeState.Leftovers > 0;

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
        /// They take one unit of food: from the fridges if the player is in the home (only then are they known), else a portion
        /// put by; never below what <see cref="RoommateRules.RaidTake"/> leaves. Returns what went (a locale key), or null.
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
