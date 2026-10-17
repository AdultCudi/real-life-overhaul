using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Entities;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The online shops' reach into the game, part: the building's real stock, taken out and put back with the game's own calls.</summary>
    internal static partial class OnlineGame
    {
        private const int DeliveriesKept = 60;      // as the game keeps a warehouse's delivery record (LogisticsManagerPlanDestination.Deliver)

        private static readonly List<ItemInstance> Holders = new List<ItemInstance>(64);
        private static readonly List<ItemInstance> Emptied = new List<ItemInstance>(8);
        private static readonly List<DeliveryItem> Taken = new List<DeliveryItem>(8);

        // Storage and pallets. A shelf or a till is never storage, whatever its tags: the game needs each to keep its
        // one load (ItemHelper.GetStockInstance), and only its own call may take from it.
        private static bool IsStorage(Item item)
        {
            return item != null && (item.type & (ItemType.PointOfSale | ItemType.ShowcaseShelf)) == 0
                   && (item.HasTag(TagRef.Itemtag.isstockcontainer) || item.HasTag(TagRef.Itemtag.iswarehousestorage));
        }

        private static bool IsShelf(Item item)
        {
            return item != null && (item.type & ItemType.ShowcaseShelf) != 0;
        }

        private static bool Loose(CargoInstance cargo)
        {
            return cargo != null && cargo.amount > 0 && !string.IsNullOrEmpty(cargo.itemName) && cargo.nestedCargoInstances.Count == 0 && !cargo.IsSealed;
        }

        /// <summary>The retail products a building has in stock, each once. One pass over what stands in the building.</summary>
        public static void Stocked(BuildingRegistration reg, List<string> into)
        {
            into.Clear();
            if (reg == null || reg.itemInstances == null)
            {
                return;
            }
            foreach (ItemInstance holder in reg.itemInstances.Values)
            {
                Item kind = holder != null ? holder.ItemCached : null;
                if (holder == null || holder.cargoInstances == null || (!IsStorage(kind) && !IsShelf(kind)))
                {
                    continue;
                }
                for (int c = 0; c < holder.cargoInstances.Count; c++)
                {
                    CargoInstance cargo = holder.cargoInstances[c];
                    if (!Loose(cargo) || into.Contains(cargo.itemName))
                    {
                        continue;
                    }
                    Item item = cargo.ItemCached;
                    if (item != null && (item.type & ItemType.RetailProduct) != 0)
                    {
                        into.Add(cargo.itemName);
                    }
                }
            }
        }

        /// <summary>Units of a product in the building's storage, pallets and showcase shelves. One pass over what stands in the building.</summary>
        public static int InStock(BuildingRegistration reg, string item)
        {
            int units = 0;
            if (reg == null || reg.itemInstances == null)
            {
                return 0;
            }
            foreach (ItemInstance holder in reg.itemInstances.Values)
            {
                Item kind = holder != null ? holder.ItemCached : null;
                if (holder == null || holder.cargoInstances == null || (!IsStorage(kind) && !IsShelf(kind)))
                {
                    continue;
                }
                for (int c = 0; c < holder.cargoInstances.Count; c++)
                {
                    CargoInstance cargo = holder.cargoInstances[c];
                    units += Loose(cargo) && cargo.itemName == item ? cargo.amount : 0;
                }
            }
            return units;
        }

        /// <summary>Counts the stock of every product of a site's catalogue into its <see cref="Site.Stock"/>.</summary>
        public static void CountStock(WebPlace place)
        {
            for (int l = 0; l < place.Shop.Lines.Count && l < place.Site.Stock.Length; l++)
            {
                place.Site.Stock[l] = InStock(place.Reg, place.Shop.Lines[l].Item);
            }
        }

        /// <summary>
        /// Takes units from storage and pallets, then from showcase shelves with the call a customer's purchase makes, and
        /// answers how many with their <paramref name="cost"/> in cents. One pass over what stands in the building.
        /// </summary>
        public static int Take(BuildingRegistration reg, string item, int wanted, out int cost)
        {
            cost = 0;
            if (reg == null || reg.itemInstances == null || wanted <= 0 || string.IsNullOrEmpty(item))
            {
                return 0;
            }
            Holders.Clear();
            foreach (ItemInstance holder in reg.itemInstances.Values)   // listed first: taking from one may change the building's list
            {
                Item kind = holder != null ? holder.ItemCached : null;
                if (holder != null && holder.cargoInstances != null && IsStorage(kind))
                {
                    Holders.Add(holder);
                }
            }
            int storage = Holders.Count;
            foreach (ItemInstance holder in reg.itemInstances.Values)
            {
                Item kind = holder != null ? holder.ItemCached : null;
                if (holder != null && holder.cargoInstances != null && !IsStorage(kind) && IsShelf(kind))
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
                    continue;   // a shelf has one load (ItemHelper.GetStockInstance)
                }
                CargoInstance stock = holder.cargoInstances[0];
                Item shelf = holder.ItemCached;
                if (!Loose(stock) || stock.itemName != item || shelf.producerSettings == null || !shelf.producerSettings.ResourcesRequired)
                {
                    continue;   // a shelf that uses up no goods would answer yes and give nothing
                }
                int take = Math.Min(wanted - got, stock.amount);
                float each = stock.pricePerUnit;
                if (holder.SubtractFromStock(take))   // an emptied shelf is refilled inside the call: what left it is what was asked for
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
            cost = Mathf.RoundToInt(paid * 100f);
            if (got > 0)
            {
                Taken.Add(new DeliveryItem(item, -got));
            }
            return got;
        }

        /// <summary>After an hour's picking: a warehouse's delivery record gets a line (BizMan's "runs out in" reads it) and the game hears that stock moved.</summary>
        public static void Picked(BuildingRegistration reg)
        {
            if (Taken.Count == 0)
            {
                return;
            }
            var warehouse = reg as Warehouse;
            if (warehouse != null && warehouse.deliveryTransactions != null)
            {
                var record = new DeliveryTransaction();
                record.SetItems(Taken);
                warehouse.deliveryTransactions.Add(record);
                if (warehouse.deliveryTransactions.Count > DeliveriesKept)
                {
                    warehouse.deliveryTransactions.RemoveRange(0, warehouse.deliveryTransactions.Count - DeliveriesKept);
                }
            }
            Taken.Clear();
            if (InstanceBehavior<GameManager>.Instance != null)
            {
                InstanceBehavior<GameManager>.Instance.shouldUpdateAfterDeliveries = true;
            }
        }

        /// <summary>Puts returned units into storage and pallets with the game's own delivery call: how many found room.</summary>
        public static int PutBack(BuildingRegistration reg, string item, int units, int unitCost)
        {
            if (reg == null || reg.itemInstances == null || units <= 0 || ItemsGetter.GetByName(item, true) == null)
            {
                return 0;
            }
            var cargo = new CargoInstance(item, units, unitCost / 100f);
            ItemHelper.DeliverCargoToBuilding(cargo, reg, IsStorageHolder);
            int back = units - Math.Max(0, cargo.amount);
            if (back > 0 && InstanceBehavior<GameManager>.Instance != null)
            {
                InstanceBehavior<GameManager>.Instance.shouldUpdateAfterDeliveries = true;
            }
            return back;
        }

        private static readonly Func<ItemInstance, bool> IsStorageHolder = holder => holder != null && IsStorage(holder.ItemCached);
    }
}
