using System;
using System.Collections.Generic;
using BigAmbitions.Factories;
using BigAmbitions.Factories.Recipes;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Helpers;
using Localizor;
using Streets;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The raw materials exchange's one meeting with the game: which items are raw materials (every ingredient of the game's
    /// factory recipes that no recipe makes) and their own prices, the player's warehouses and factories, a future's goods
    /// put on a place's pallets as the game's importers deliver and charged to it, and what the player's factories use.
    /// Asked once a session, when a page is written or when a future is delivered; never per frame.
    /// </summary>
    public static class MaterialGame
    {
        private const string WarehouseType = "ba:buildingtype_warehouse";
        private const string FactoryType = "ba:businesstype_factory";
        private const string TaxName = "tax_import";   // the game's own name for imports on the tax return

        private static readonly HashSet<string> Made = new HashSet<string>();
        private static readonly HashSet<string> Seen = new HashSet<string>();

        /// <summary>
        /// The game's raw materials with their own prices in micros a unit, the game's price multiplier in them, in no
        /// particular order. How many; none while the game has no recipes loaded.
        /// </summary>
        public static int Find(List<string> names, List<long> bases, Func<string, float> own)
        {
            names.Clear();
            bases.Clear();
            HashSet<Recipe> recipes = FactoriesHelper.AllRecipes;
            if (recipes == null || recipes.Count == 0)
            {
                return 0;
            }
            Made.Clear();
            Seen.Clear();
            foreach (Recipe r in recipes)
            {
                if (r != null && !string.IsNullOrEmpty(r.output.item))
                {
                    Made.Add(r.output.item);
                }
            }
            float multiplier = Multiplier();
            foreach (Recipe r in recipes)
            {
                if (r == null || r.ingredients == null)
                {
                    continue;
                }
                for (int i = 0; i < r.ingredients.Count; i++)
                {
                    string name = r.ingredients[i].item;
                    if (string.IsNullOrEmpty(name) || Made.Contains(name) || !Seen.Add(name))
                    {
                        continue;
                    }
                    Item item = ItemsGetter.GetByName(name);
                    float price = own != null ? own(name) : -1f;
                    if (price <= 0f && item != null)
                    {
                        price = item.wholesalePrice;   // the city's economy has not read it: nothing of the mod's is on it
                    }
                    if (item == null || price <= 0f || (item.type & ItemType.RetailProduct) == 0)
                    {
                        continue;
                    }
                    names.Add(name);
                    bases.Add(Math.Max(1L, (long)Math.Round(price * Math.Max(0.01f, multiplier) * DerivCatalogue.Micro)));
                }
            }
            return names.Count;
        }

        /// <summary>The save's price multiplier, which every material's own price carries (the exchange's building materials too).</summary>
        public static float Multiplier()
        {
            GameInstance save = SaveGameManager.Current;
            return save != null && save.gameVariables != null ? save.gameVariables.marketPriceMultiplier : 1f;
        }

        /// <summary>A material's name in the game's language.</summary>
        public static string Name(string itemName)
        {
            return string.IsNullOrEmpty(itemName) ? string.Empty : LocalizorManager.IsLocalizedKey(itemName) ? itemName.GetLocalization() : itemName;
        }

        /// <summary>The player's own warehouses and factories that can take goods: a key each (the address) and its name.</summary>
        public static void Places(List<string> keys, List<string> names)
        {
            keys.Clear();
            names.Clear();
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null)
            {
                return;
            }
            foreach (BuildingRegistration reg in save.BuildingRegistrations)
            {
                if (!Takes(reg))
                {
                    continue;
                }
                keys.Add(Key(reg));
                names.Add(string.IsNullOrEmpty(reg.BusinessName) ? reg.Address.ToFormattedString() : reg.BusinessName);
            }
        }

        // A place a future's goods may go to: the player's, rented, a warehouse or a factory, with pallets to put them on.
        private static bool Takes(BuildingRegistration reg)
        {
            if (reg == null || !reg.RentedByPlayer || reg.Address == null || !GoingPublic.Mine(reg))
            {
                return false;
            }
            return reg.GetBuildingType() == WarehouseType || reg.businessTypeName == FactoryType;
        }

        /// <summary>A place's key as a contract keeps it: its street and number.</summary>
        public static string Key(BuildingRegistration reg)
        {
            return reg == null || reg.Address == null ? string.Empty : DerivStore.Clean(reg.Address.streetName).Replace('#', ' ') + "#" + reg.Address.streetNumber;
        }

        private static BuildingRegistration Find(string key)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null || string.IsNullOrEmpty(key))
            {
                return null;
            }
            foreach (BuildingRegistration reg in save.BuildingRegistrations)
            {
                if (Takes(reg) && Key(reg) == key)
                {
                    return reg;
                }
            }
            return null;
        }

        /// <summary>
        /// A future's goods put on a place's pallets at a price in micros a unit, as many as fit and as the cash pays for,
        /// charged to the place as the game charges an import. How many went; the place's name, or empty when it is gone.
        /// </summary>
        public static long Deliver(string key, string itemName, long qty, long priceMicros, out long charged, out string place)
        {
            charged = 0;
            place = string.Empty;
            BuildingRegistration reg = Find(key);
            GameInstance save = SaveGameManager.Current;
            if (reg == null || save == null || qty <= 0 || ItemsGetter.GetByName(itemName) == null)
            {
                return 0;
            }
            place = string.IsNullOrEmpty(reg.BusinessName) ? reg.Address.ToFormattedString() : reg.BusinessName;
            double cash = Math.Max(0.0, Math.Floor(save.Money));
            long affordable = priceMicros > 0 ? (long)Math.Min(qty, Math.Floor(cash * DerivCatalogue.Micro / priceMicros)) : 0L;
            if (affordable <= 0)
            {
                return 0;
            }
            var cargo = new CargoInstance(itemName, (int)Math.Min(int.MaxValue, affordable), (float)(priceMicros / (double)DerivCatalogue.Micro));
            ItemHelper.DeliverCargoToBuilding(cargo, reg, x => x.ItemCached != null && x.ItemCached.HasTag(TagRef.Itemtag.iswarehousestorage));
            long delivered = affordable - Math.Max(0, cargo.amount);
            if (delivered <= 0)
            {
                return 0;
            }
            charged = DerivRules.Money(delivered, priceMicros, true);
            var data = new Dictionary<string, string>(2) { { "name", Name(itemName) }, { "businessName", place } };
            var info = new TransactionInfo(DerivMoney.Goods, data);
            info.SetTaxDeductibleName(TaxName);
            GameManager.ChangeMoneySafe(-charged, info, null, reg.Address, true);
            return delivered;
        }

        /// <summary>The raw materials the player's factories' machines are set to use now, by the game's names.</summary>
        public static void Used(HashSet<string> into)
        {
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null)
            {
                return;
            }
            foreach (BuildingRegistration reg in save.BuildingRegistrations)
            {
                if (reg == null || !reg.RentedByPlayer || reg.businessTypeName != FactoryType || reg.itemInstances == null || !GoingPublic.Mine(reg))
                {
                    continue;
                }
                foreach (ItemInstance instance in reg.itemInstances.Values)
                {
                    if (!(instance is FactoryWorkstationInstance machine) || string.IsNullOrEmpty(machine.workstationType) || !machine.IsWorkstationValid())
                    {
                        continue;
                    }
                    Recipe recipe = machine.SelectedRecipe;
                    for (int i = 0; recipe != null && recipe.ingredients != null && i < recipe.ingredients.Count; i++)
                    {
                        into.Add(recipe.ingredients[i].item);
                    }
                }
            }
        }
    }
}
