using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    internal static partial class LogisticsGame
    {
        private static readonly List<ItemInstance> Holders = new List<ItemInstance>(64);
        private static readonly List<ItemInstance> Emptied = new List<ItemInstance>(8);

        /// <summary>The player's own places that hold goods: their businesses, warehouses and factories (another player's never).</summary>
        public static void Owned(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r != null && r.RentedByPlayer && r.itemInstances != null && r.itemInstances.Count > 0 && GoingPublic.Mine(r))
                {
                    into.Add(r);
                }
            }
        }

        private const string Residential = "ba:buildingtype_residential";

        private static readonly List<int> Pieces = new List<int>(8);      // per product of the look in hand: how many of it the place holds
        private static readonly List<Item> Kinds = new List<Item>(8);     // and the game's item, for the room a fridge gives it
        private static readonly List<Item> ColdFurniture = new List<Item>(16);
        private static HashSet<string> _shelfCold;   // products some fridge or freezer of the game shows on its own shelf (read from the game's items once)

        /// <summary>
        /// The products a place holds that spoil, each once: its name and how long it keeps there at the usual pace (the caller scales it).
        /// Shelves, tills, storage and pallets alike: every holder with goods in it.
        /// </summary>
        public static void Spoilable(BuildingRegistration reg, List<string> items, List<int> lives) => Spoilable(reg, items, lives, null, null);

        /// <summary>
        /// The same, with the cold furniture's share. A product that a cold shelf of the game shows (<see cref="ShownCold"/>) is cold only
        /// if some of it sits inside a fridge or a freezer, and then the place keeps all of it as long as its coldest holder. Any other
        /// product keeps its ordinary life, and where the player runs a business (not a home) each fridge or freezer standing there keeps
        /// cold up to its own room of that product (<see cref="RoomFor"/>): <paramref name="chilled"/> is those pieces and
        /// <paramref name="coldLives"/> the days they keep (0 when none are). Read from the game's own data and the furniture, so every
        /// machine of one game build reads the same; nothing is saved.
        /// </summary>
        public static void Spoilable(BuildingRegistration reg, List<string> items, List<int> lives, List<int> chilled, List<int> coldLives)
        {
            items.Clear();
            lives.Clear();
            chilled?.Clear();
            coldLives?.Clear();
            Pieces.Clear();
            Kinds.Clear();
            if (reg == null || reg.itemInstances == null)
            {
                return;
            }
            bool covered = false;
            foreach (ItemInstance holder in reg.itemInstances.Values)
            {
                Item kind = holder != null ? holder.ItemCached : null;
                if (kind == null || holder.cargoInstances == null || holder.cargoInstances.Count == 0)
                {
                    continue;
                }
                bool cold = SpoilRules.IsCold(kind.itemName);
                for (int c = 0; c < holder.cargoInstances.Count; c++)
                {
                    CargoInstance cargo = holder.cargoInstances[c];
                    if (cargo == null || cargo.amount <= 0 || string.IsNullOrEmpty(cargo.itemName))
                    {
                        continue;
                    }
                    Item item = cargo.ItemCached;
                    bool inside = cold && ShownCold(cargo.itemName);
                    int life = SpoilRules.UsualLife(cargo.itemName, item != null && item.requiresWeighing, inside);
                    if (life <= 0)
                    {
                        continue;
                    }
                    int at = items.IndexOf(cargo.itemName);
                    if (at < 0)
                    {
                        items.Add(cargo.itemName);
                        lives.Add(life);
                        Pieces.Add(cargo.amount);
                        Kinds.Add(item);
                    }
                    else
                    {
                        Pieces[at] += cargo.amount;
                        if (life > lives[at])
                        {
                            lives[at] = life;   // some of it is kept cold: the place keeps it as long as its coldest holder
                        }
                    }
                    covered |= !ShownCold(cargo.itemName);
                }
            }
            if (chilled == null || coldLives == null)
            {
                return;
            }
            bool shop = covered && IsShop(reg);
            if (shop)
            {
                ColdFurniture.Clear();
                foreach (ItemInstance holder in reg.itemInstances.Values)
                {
                    Item kind = holder != null ? holder.ItemCached : null;
                    if (kind != null && SpoilRules.IsCold(kind.itemName))
                    {
                        ColdFurniture.Add(kind);
                    }
                }
            }
            for (int i = 0; i < items.Count; i++)
            {
                int pieces = 0;
                if (shop && ColdFurniture.Count > 0 && !ShownCold(items[i]))
                {
                    int room = 0;
                    for (int f = 0; f < ColdFurniture.Count; f++)
                    {
                        room = (int)Math.Min(int.MaxValue, (long)room + RoomFor(ColdFurniture[f], Kinds[i]));
                    }
                    pieces = SpoilRules.Chilled(Pieces[i], room);
                }
                chilled.Add(pieces);
                coldLives.Add(pieces > 0 ? SpoilRules.UsualLife(items[i], Kinds[i] != null && Kinds[i].requiresWeighing, true) : 0);
            }
            ColdFurniture.Clear();
        }

        /// <summary>
        /// Whether the game lets a product stand in a cold shelf of its own: some fridge or freezer showcase lists it (Item.itemsThatCanShowcase,
        /// which Producer.CanAddToInventory reads). Those are drinks, fresh food, frozen food and ice cream; burgers, hot dogs, pizza, kebabs,
        /// chips, buns, raw meat, dough, dairy, bakery and produce are on no cold shelf's list. Read from the items once, at run time; if the
        /// game's items are not there yet it says yes for everything, which is the rule as it was (cold only inside a fridge).
        /// </summary>
        public static bool ShownCold(string item)
        {
            if (_shelfCold == null)
            {
                IEnumerable<Item> all = ItemsGetter.AllItems;
                if (all == null)
                {
                    return true;
                }
                var shown = new HashSet<string>(StringComparer.Ordinal);
                foreach (Item it in all)
                {
                    if (it != null && SpoilRules.IsCold(it.itemName) && (it.type & ItemType.ShowcaseShelf) != 0 && it.itemsThatCanShowcase != null)
                    {
                        for (int i = 0; i < it.itemsThatCanShowcase.Length; i++)
                        {
                            shown.Add(it.itemsThatCanShowcase[i]);
                        }
                    }
                }
                _shelfCold = shown;
            }
            return _shelfCold.Contains(item);
        }

        /// <summary>
        /// The pieces of a product one fridge or freezer keeps cold, by the game's own figures: a showcase shelf gives the room of its one
        /// load (CargoInstance.GetMaxStockCapacity: the product's box size times the furniture's capacity multiplier, rounded up); a
        /// fridge with slots (the staff fridges, FridgeController) gives its slots times that, and takes only food that fills
        /// (FridgeController.CanStore) and what its whitelist allows (ItemInstance.TryToAddToCargo). Furniture the game gives no room
        /// (a decorative fridge) gives 0.
        /// </summary>
        public static int RoomFor(Item furniture, Item product)
        {
            if (furniture == null || product == null)
            {
                return 0;
            }
            string[] only = furniture.itemsWhitelist;
            if (only != null && only.Length > 0 && Array.IndexOf(only, product.itemName) < 0)
            {
                return 0;   // ItemInstance.TryToAddToCargo: a whitelist keeps out what it does not name
            }
            return SpoilRules.RoomOf((furniture.type & ItemType.ShowcaseShelf) != 0, furniture.cargoCapacity, furniture.cargoCapacityMultiplier, product.boxSize, product.saturation, true);
        }

        // A place the player runs as a business: a shop, a warehouse or a factory, never a home (a home's fridge is not shop equipment).
        private static bool IsShop(BuildingRegistration reg)
        {
            string type = GameRegistrations.BuildingType(reg);
            return type != null && type != Residential;
        }

        /// <summary>Whether the game weighs a product at the till: fresh produce sold by weight, which spoils whatever its name.</summary>
        public static bool Weighed(string item)
        {
            Item it = !string.IsNullOrEmpty(item) ? ItemsGetter.GetByName(item, true) : null;
            return it != null && it.requiresWeighing;
        }

        // Storage and pallets. A shelf or a till is never storage, whatever its tags: the game needs each to keep its one load.
        private static bool IsStorage(Item item) =>
            item != null && (item.type & (ItemType.PointOfSale | ItemType.ShowcaseShelf)) == 0
            && (item.HasTag(TagRef.Itemtag.isstockcontainer) || item.HasTag(TagRef.Itemtag.iswarehousestorage));

        private static bool IsShelf(Item item) => item != null && (item.type & ItemType.ShowcaseShelf) != 0;

        private static bool Loose(CargoInstance cargo) =>
            cargo != null && cargo.amount > 0 && !string.IsNullOrEmpty(cargo.itemName) && cargo.nestedCargoInstances.Count == 0 && !cargo.IsSealed;

        /// <summary>
        /// Throws out spoiled pieces of a product by the game's own calls: from storage first (ItemInstance.ReduceFromCargo), then
        /// from the shelves (SubtractFromStock, which refills an emptied shelf as a sale does). Returns the pieces thrown out and
        /// what they had cost, cents. A warehouse's record of goods out is not written: that record is what its page and its route's page
        /// count as goods sent, and thrown-out stock is not sent to anybody.
        /// </summary>
        public static int Discard(BuildingRegistration reg, string item, int wanted, out int cents)
        {
            cents = 0;
            if (reg == null || reg.itemInstances == null || wanted <= 0 || string.IsNullOrEmpty(item))
            {
                return 0;
            }
            Holders.Clear();
            foreach (ItemInstance holder in reg.itemInstances.Values)   // listed first: taking from one may change the building's list
            {
                if (holder != null && holder.cargoInstances != null && IsStorage(holder.ItemCached))
                {
                    Holders.Add(holder);
                }
            }
            int storage = Holders.Count;
            foreach (ItemInstance holder in reg.itemInstances.Values)
            {
                if (holder != null && holder.cargoInstances != null && !IsStorage(holder.ItemCached) && IsShelf(holder.ItemCached))
                {
                    Holders.Add(holder);
                }
            }
            int got = 0;
            float paid = 0f;
            Emptied.Clear();
            for (int h = 0; h < Holders.Count && got < wanted; h++)
            {
                ItemInstance holder = Holders[h];
                if (h < storage)
                {
                    int before = got;
                    for (int c = holder.cargoInstances.Count - 1; c >= 0 && got < wanted; c--)
                    {
                        CargoInstance cargo = holder.cargoInstances[c];
                        if (!Loose(cargo) || cargo.itemName != item)
                        {
                            continue;
                        }
                        int n = Math.Min(wanted - got, cargo.amount);
                        paid += n * cargo.pricePerUnit;
                        holder.ReduceFromCargo(cargo, n);
                        got += n;
                    }
                    if (got > before && holder.cargoInstances.Count == 0 && holder.ItemCached.HasTag(TagRef.Itemtag.discardcontainerwhenempty))
                    {
                        Emptied.Add(holder);
                    }
                    continue;
                }
                if (holder.cargoInstances.Count != 1)
                {
                    continue;   // a shelf has one load
                }
                CargoInstance stock = holder.cargoInstances[0];
                Item shelf = holder.ItemCached;
                if (!Loose(stock) || stock.itemName != item || shelf.producerSettings == null || !shelf.producerSettings.ResourcesRequired)
                {
                    continue;
                }
                int take = Math.Min(wanted - got, stock.amount);
                float each = stock.pricePerUnit;
                if (holder.SubtractFromStock(take))
                {
                    paid += take * each;
                    got += take;
                }
            }
            for (int i = 0; i < Emptied.Count; i++)
            {
                reg.RemoveItemInstanceFromBuilding(Emptied[i]);
            }
            Emptied.Clear();
            Holders.Clear();
            cents = Mathf.RoundToInt(paid * 100f);
            if (got > 0 && InstanceBehavior<GameManager>.Instance != null)
            {
                InstanceBehavior<GameManager>.Instance.shouldUpdateAfterDeliveries = true;   // the game's pages and shelves read it again
            }
            return got;
        }

        public static string NameOf(BuildingRegistration reg) => reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : Key(reg != null ? reg.Address : null);

        // ------------------------------------------------------------------------------------------------- the rivals' books

        /// <summary>The city's rival shops whose day the game values (CompetitionHelper.UpdateDailyValuation), and its suppliers' places.</summary>
        public static void Rivals(List<BuildingRegistration> rivals, List<string> supplies)
        {
            rivals.Clear();
            supplies.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r == null)
                {
                    continue;
                }
                if (r.businessTypeName == "ba:businesstype_wholesalestore" || r.businessTypeName == "ba:businesstype_importexport")
                {
                    supplies.Add(Key(r.Address));
                }
                else if (!string.IsNullOrEmpty(r.businessOwnerRivalId) && !r.RentedByPlayer && r.dailyIncomes != null)
                {
                    rivals.Add(r);
                }
            }
        }

        /// <summary>Whether the game is still valuing the city's shops for the day (its queues), so their day is not read half made.</summary>
        public static bool Valuing => CompetitionHelper.AreRecalculationsPending();

        /// <summary>A rival shop's last day as the game valued it, cents; 0 when it has none.</summary>
        public static int LastDay(BuildingRegistration r) =>
            r != null && r.dailyIncomes != null && r.dailyIncomes.Count > 0 ? Mathf.RoundToInt(Mathf.Clamp(r.dailyIncomes[r.dailyIncomes.Count - 1], -1e7f, 1e7f) * 100f) : 0;

        /// <summary>Takes a day's supply costs off a rival shop's last valued day, which its rival's income, worth and closing are read from.</summary>
        public static void Charge(BuildingRegistration r, int cents)
        {
            if (r != null && r.dailyIncomes != null && r.dailyIncomes.Count > 0 && cents > 0)
            {
                r.dailyIncomes[r.dailyIncomes.Count - 1] -= cents / 100f;
            }
        }
    }
}
