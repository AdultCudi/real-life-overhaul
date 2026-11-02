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
        private const int DeliveriesKept = 60;   // as the game keeps a warehouse's record of goods moved (LogisticsManagerPlanDestination.Deliver)

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

        /// <summary>
        /// The products a place holds that spoil, each once: its name, whether any of it sits in a fridge or a freezer, and how
        /// long it keeps there. Shelves, tills, storage and pallets alike: every holder with goods in it.
        /// </summary>
        public static void Spoilable(BuildingRegistration reg, List<string> items, List<int> lives)
        {
            items.Clear();
            lives.Clear();
            if (reg == null || reg.itemInstances == null)
            {
                return;
            }
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
                    int life = SpoilRules.Life(cargo.itemName, item != null && item.requiresWeighing, cold);
                    if (life <= 0)
                    {
                        continue;
                    }
                    int at = items.IndexOf(cargo.itemName);
                    if (at < 0)
                    {
                        items.Add(cargo.itemName);
                        lives.Add(life);
                    }
                    else if (life > lives[at])
                    {
                        lives[at] = life;   // some of it is kept cold: the place keeps it as long as its coldest holder
                    }
                }
            }
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
        /// what they had cost, cents; a warehouse's record of goods out says so as the game's own movements do.
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
            var warehouse = reg as Warehouse;
            if (got > 0 && warehouse != null && warehouse.deliveryTransactions != null)
            {
                var record = new DeliveryTransaction();
                record.SetItems(new List<DeliveryItem>(1) { new DeliveryItem(item, -got) });
                warehouse.deliveryTransactions.Add(record);
                if (warehouse.deliveryTransactions.Count > DeliveriesKept)
                {
                    warehouse.deliveryTransactions.RemoveRange(0, warehouse.deliveryTransactions.Count - DeliveriesKept);
                }
            }
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
