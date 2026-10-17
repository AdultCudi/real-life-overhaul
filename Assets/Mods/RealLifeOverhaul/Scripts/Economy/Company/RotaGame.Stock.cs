using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings.Office.Headquarters;
using Buildings.Retail.Simulation;
using Entities;
using Helpers;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Reading a shop's goods into <see cref="RotaStock"/>: shelves, stock, sales, deliveries.</summary>
    internal static partial class RotaGame
    {
        private static readonly Dictionary<string, int> StockIndex = new Dictionary<string, int>(32, StringComparer.Ordinal);
        private static readonly bool[] StockEndless = new bool[RotaStock.ProductsMost];
        private static bool _stockSaid;

        // After ReadWorth: the same books or prices say what a customer takes.
        private static void ReadStock(RotaSheet sheet, BuildingRegistration reg)
        {
            RotaShop shop = sheet.Shop;
            RotaStock stock = shop.Stock;
            stock.Clear();
            sheet.Products.Clear();
            sheet.UrgentPercent = 0;
            StockIndex.Clear();
            Array.Clear(StockEndless, 0, StockEndless.Length);
            if (!shop.HasCustomers || reg.itemInstances == null)
            {
                return;
            }
            try
            {
                BusinessType type = BusinessTypeHelper.GetData(reg);
                if (type == null || !(type.simulator is RetailBusinessSimulator))
                {
                    return;   // only the game's shops sell off shelves
                }
                bool bags = type.HasTag(TagRef.Businesstag.customersneedpaperbags);
                ReadShelves(sheet, reg, bags);
                if (stock.Products == 0)
                {
                    return;
                }
                ReadOnSite(sheet, reg);
                ReadTaken(sheet, reg, type);
                ReadDeliveries(sheet, reg);
                ReadRounds(sheet, reg);
                Drop(sheet);
            }
            catch (Exception e)
            {
                // The plans are then valued without a stock limit.
                stock.Clear();
                sheet.Products.Clear();
                if (!_stockSaid)
                {
                    _stockSaid = true;
                    Logger?.Error(e);
                    Logger?.Warn("Rota: the goods of " + reg.BusinessName + " could not be read (the error above); its plans are valued without a stock limit. Said once a session.");
                }
            }
        }

        // The products shelves sell, and the paper bags at the tills where needed, with what the shelves hold.
        private static void ReadShelves(RotaSheet sheet, BuildingRegistration reg, bool bags)
        {
            RotaStock stock = sheet.Shop.Stock;
            foreach (ItemInstance item in reg.itemInstances.Values)
            {
                Item data = item != null ? item.ItemCached : null;
                if (data == null || item.cargoInstances == null || item.cargoInstances.Count != 1 || !data.IsStockCarrier() || ItemHelper.HasAnyMissingRequirements(item))
                {
                    continue;   // the game's day passes over an item that lacks a requirement (RetailBusinessSimulator.LoadAvailableFurniture)
                }
                bool shelf = (data.type & ItemType.ShowcaseShelf) != 0, till = (data.type & ItemType.PointOfSale) != 0;
                CargoInstance cargo = item.cargoInstances[0];
                if ((!shelf && !(till && bags)) || cargo == null || string.IsNullOrEmpty(cargo.itemName))
                {
                    continue;
                }
                int p = ProductOf(sheet, cargo.itemName);
                if (p < 0)
                {
                    continue;
                }
                stock.Shelf[p] += Math.Max(0, cargo.GetMaxStockCapacity(item));
                stock.OnSite[p] += Math.Max(0, cargo.amount);
                stock.Bag[p] |= till && bags;
                StockEndless[p] |= data.producerSettings == null || !data.producerSettings.ResourcesRequired;   // a sale takes nothing from such a shelf (ItemHelper.SubtractFromStock)
            }
        }

        private static int ProductOf(RotaSheet sheet, string name)
        {
            if (StockIndex.TryGetValue(name, out int p))
            {
                return p;
            }
            RotaStock stock = sheet.Shop.Stock;
            if (stock.Products >= RotaStock.ProductsMost)
            {
                return -1;   // past the products counted one by one: the rest stay in the margin
            }
            p = stock.Products++;
            StockIndex[name] = p;
            sheet.Products.Add(name);
            Item item = ItemsGetter.GetByName(name, true);
            stock.Box[p] = item != null ? Math.Max(1, item.boxSize) : 1;
            return p;
        }

        // What is there besides the shelves: the storage and any vehicle parked at the business, as the game counts it
        // for a refill (ReStockingHelper.CountAvailableStock), and the box places the storage has for these goods.
        private static void ReadOnSite(RotaSheet sheet, BuildingRegistration reg)
        {
            RotaStock stock = sheet.Shop.Stock;
            int places = 0, others = 0;
            foreach (ItemInstance item in reg.itemInstances.Values)
            {
                Item data = item != null ? item.ItemCached : null;
                if (data == null || item.cargoInstances == null)
                {
                    continue;
                }
                // A shelf's own stock is counted with the shelves.
                bool container = data.HasTag(TagRef.Itemtag.isstockcontainer) && !data.IsStockCarrier(), storage = data.HasTag(TagRef.Itemtag.isbusinessstorage);
                if (storage)
                {
                    places += Math.Max(0, data.cargoCapacity);
                }
                for (int k = 0; (container || storage) && k < item.cargoInstances.Count; k++)
                {
                    CargoInstance cargo = item.cargoInstances[k];
                    if (cargo == null || string.IsNullOrEmpty(cargo.itemName))
                    {
                        continue;
                    }
                    bool counted = StockIndex.TryGetValue(cargo.itemName, out int p);
                    if (container && counted && (cargo.nestedCargoInstances == null || cargo.nestedCargoInstances.Count == 0))
                    {
                        stock.OnSite[p] += Math.Max(0, cargo.amount);
                    }
                    if (storage && !counted)
                    {
                        others++;   // a box of goods not sold off a shelf takes a place
                    }
                }
            }
            stock.Boxes = Math.Max(0, places - others);
            List<VehicleInstance> vehicles = Save.VehicleInstances;
            for (int i = 0; vehicles != null && i < vehicles.Count; i++)
            {
                VehicleInstance v = vehicles[i];
                if (v == null || v.cargoInstances == null || v.id == Save.ActiveVehicleId || !v.IsAtAddress(reg.Address))
                {
                    continue;
                }
                for (int k = 0; k < v.cargoInstances.Count; k++)
                {
                    CargoInstance cargo = v.cargoInstances[k];
                    if (cargo != null && !string.IsNullOrEmpty(cargo.itemName) && StockIndex.TryGetValue(cargo.itemName, out int p)
                        && (cargo.nestedCargoInstances == null || cargo.nestedCargoInstances.Count == 0))
                    {
                        stock.OnSite[p] += Math.Max(0, cargo.amount);
                    }
                }
            }
        }

        // What a customer takes of each product and what a unit leaves, from the same source as a customer's worth;
        // then what a customer leaves is split into these products, other goods, and what is no goods at all.
        private static void ReadTaken(RotaSheet sheet, BuildingRegistration reg, BusinessType type)
        {
            RotaShop shop = sheet.Shop;
            RotaStock stock = shop.Stock;
            double goods = 0;
            if (sheet.FromBooks)
            {
                int today = Save.Day, customers = 0;
                var units = new long[RotaStock.ProductsMost];
                var left = new double[RotaStock.ProductsMost];
                List<OrderHistoryEntry> history = reg.orderHistory;
                for (int i = history != null ? history.Count - 1 : -1; i >= 0; i--)
                {
                    OrderHistoryEntry entry = history[i];
                    if (entry == null || entry.dayNumber >= today || entry.totalCustomers <= 0)
                    {
                        continue;
                    }
                    if (entry.dayNumber < today - RotaCatalogue.BooksDays)
                    {
                        break;
                    }
                    customers += entry.totalCustomers;
                    for (int k = 0; entry.itemSales != null && k < entry.itemSales.Count; k++)
                    {
                        OrderHistoryEntry.ItemReport sale = entry.itemSales[k];
                        if (sale == null || string.IsNullOrEmpty(sale.itemName))
                        {
                            continue;
                        }
                        double margin = sale.totalPrice - sale.totalWholesalePrice;
                        if (IsGoods(sale.itemName, type))
                        {
                            goods += margin;
                        }
                        if (StockIndex.TryGetValue(sale.itemName, out int p))
                        {
                            units[p] += Math.Max(0, sale.amountSold);
                            left[p] += margin;
                        }
                    }
                }
                for (int p = 0; customers > 0 && p < stock.Products; p++)
                {
                    stock.PerCustomer[p] = (int)Math.Min(int.MaxValue, units[p] * 1000L / customers);
                    stock.UnitMargin[p] = units[p] > 0 ? (int)Math.Round(left[p] * 100.0 / units[p]) : 0;
                    if (stock.Bag[p] && units[p] == 0)
                    {
                        stock.PerCustomer[p] = 1000;   // bags are not in the books: one for each customer who buys
                    }
                }
                goods = customers > 0 ? goods * 100.0 / customers : 0;
            }
            else
            {
                float content = Calculator.Satisfaction(reg);
                List<string> products = reg.cachedAvailableProducts;
                for (int i = 0; products != null && i < products.Count; i++)
                {
                    Item item = ItemsGetter.GetByName(products[i], true);
                    if (item == null || !IsGoods(products[i], type))
                    {
                        continue;
                    }
                    float taken = Taken(item, products[i], type, reg, content);
                    double margin = ItemHelper.GetPrice(products[i], reg) - item.GetWholesalePrice();
                    goods += taken * margin * 100.0;
                    if (StockIndex.TryGetValue(products[i], out int p))
                    {
                        stock.PerCustomer[p] = (int)Math.Round(Math.Max(0f, taken) * 1000f);
                        stock.UnitMargin[p] = (int)Math.Round(margin * 100.0);
                    }
                }
                for (int p = 0; p < stock.Products; p++)
                {
                    if (stock.Bag[p] && stock.PerCustomer[p] == 0)
                    {
                        stock.PerCustomer[p] = 1000;
                    }
                }
            }
            stock.Split(shop.Margin, (int)Math.Max(int.MinValue / 2, Math.Min(int.MaxValue / 2, Math.Round(goods))));
        }

        private static bool IsGoods(string name, BusinessType type)
        {
            Item item = ItemsGetter.GetByName(name, true);
            return item != null && (item.type & ItemType.ServiceProduct) == 0 && name != type.defaultEntranceFee && name != type.weekendOnlyEntranceFee
                   && !item.HasTag(TagRef.Itemtag.isticket);
        }

        // The delivery contracts that repeat: what each brings on a Monday, the wholesaler's weekly limit, and what the
        // wholesaler is short of this week (shown, not counted).
        private static void ReadDeliveries(RotaSheet sheet, BuildingRegistration reg)
        {
            RotaStock stock = sheet.Shop.Stock;
            List<DeliveryContract> contracts = Save.DeliveryContracts;
            bool limits = !DeliveryHelper.AreWholesaleAndImportLimitsDisabled();
            sheet.UrgentPercent = Mathf.Max(0, Mathf.RoundToInt((DeliveryHelper.GetWholesaleUrgentFeeMultiplier() - 1f) * 100f));
            for (int i = 0; contracts != null && i < contracts.Count; i++)
            {
                DeliveryContract c = contracts[i];
                if (c == null || !c.enabled || !c.repeatingOrder || c.businessAddress == null || c.businessAddress != reg.Address || c.items == null)
                {
                    continue;
                }
                BuildingRegistration seller = c.wholesaleAddress != null ? BuildingHelper.GetBuildingRegistration(c.wholesaleAddress) : null;
                for (int k = 0; k < c.items.Count; k++)
                {
                    DeliveryContractItem line = c.items[k];
                    if (line == null || string.IsNullOrEmpty(line.itemName) || line.amount <= 0 || !StockIndex.TryGetValue(line.itemName, out int p))
                    {
                        continue;
                    }
                    stock.Weekly[p] += line.amount;
                    Item item = line.ItemCached;
                    stock.Most[p] = limits && item != null ? Math.Max(stock.Most[p], item.maxWholesaleOrderAmount) : 0;
                    if (seller != null)
                    {
                        stock.Backorder[p] |= ProductMarketHelper.IsProductInMarketEvent(line.itemName, MarketEventType.ProductBackorder, seller.Neighborhood);
                        stock.Shortage[p] |= limits && ProductMarketHelper.IsProductInMarketEvent(line.itemName, MarketEventType.ProductShortage, seller.Neighborhood);
                    }
                }
            }
        }

        // The logistics managers' rounds to here: each target and the warehouse's stock, counted only where the game
        // would run the round (LogisticsManagerPlan.GetPlannedDeliveries): a manager at work, a destination served.
        private static void ReadRounds(RotaSheet sheet, BuildingRegistration reg)
        {
            RotaStock stock = sheet.Shop.Stock;
            List<LogisticsManagerPlan> plans = Save.logisticsManagerPlans;
            for (int i = 0; plans != null && i < plans.Count; i++)
            {
                LogisticsManagerPlan plan = plans[i];
                if (plan == null || plan.isFactory || plan.targetAddress == null || plan.targetAddress.IsUndefined() || plan.assignedEmployeeId == null
                    || plan.destinations == null)
                {
                    continue;
                }
                var warehouse = BuildingHelper.GetBuildingRegistration(plan.targetAddress) as Warehouse;
                EmployeeInstance manager = EmployeeHelper.GetEmployeeById(plan.assignedEmployeeId);
                if (warehouse == null || !GoingPublic.Mine(warehouse) || manager == null || !manager.IsEmployeeAvailable())
                {
                    continue;
                }
                int most = plan.MaxDestinations;
                for (int k = 0; k < plan.destinations.Count && k < most; k++)
                {
                    LogisticsManagerPlanDestination destination = plan.destinations[k];
                    if (destination == null || destination.deliveryTargetAddress == null || destination.deliveryTargetAddress != reg.Address || destination.stockTargets == null)
                    {
                        continue;
                    }
                    for (int t = 0; t < destination.stockTargets.Count; t++)
                    {
                        ItemAmountTarget target = destination.stockTargets[t];
                        if (target == null || target.targetAmount <= 0 || string.IsNullOrEmpty(target.itemName) || !StockIndex.TryGetValue(target.itemName, out int p))
                        {
                            continue;
                        }
                        stock.Target[p] = Math.Max(stock.Target[p], target.targetAmount);
                        stock.Warehouse[p] += BuildingHelper.CountTotalResourcesInStock(warehouse, target.itemName, false);
                    }
                }
            }
        }

        // A product whose shelf never runs down limits nothing: it goes back into the margin with the other goods.
        private static void Drop(RotaSheet sheet)
        {
            RotaStock stock = sheet.Shop.Stock;
            int to = 0;
            for (int p = 0; p < stock.Products; p++)
            {
                if (StockEndless[p])
                {
                    stock.Goods += (int)Math.Min(int.MaxValue / 2, (long)stock.PerCustomer[p] * stock.UnitMargin[p] / 1000);
                    continue;
                }
                if (to != p)
                {
                    stock.PerCustomer[to] = stock.PerCustomer[p];
                    stock.UnitMargin[to] = stock.UnitMargin[p];
                    stock.OnSite[to] = stock.OnSite[p];
                    stock.Shelf[to] = stock.Shelf[p];
                    stock.Box[to] = stock.Box[p];
                    stock.Weekly[to] = stock.Weekly[p];
                    stock.Target[to] = stock.Target[p];
                    stock.Most[to] = stock.Most[p];
                    stock.Warehouse[to] = stock.Warehouse[p];
                    stock.Bag[to] = stock.Bag[p];
                    stock.Backorder[to] = stock.Backorder[p];
                    stock.Shortage[to] = stock.Shortage[p];
                    sheet.Products[to] = sheet.Products[p];
                }
                to++;
            }
            sheet.Products.RemoveRange(to, sheet.Products.Count - to);
            stock.Products = to;
        }

        // The contracts and rounds a shop's goods figures are made from, for its stamp; the stock is read again daily.
        private static long StockStamp(long h, BuildingRegistration reg)
        {
            List<DeliveryContract> contracts = Save != null ? Save.DeliveryContracts : null;
            for (int i = 0; contracts != null && i < contracts.Count; i++)
            {
                DeliveryContract c = contracts[i];
                if (c == null || c.businessAddress == null || c.businessAddress != reg.Address || c.items == null)
                {
                    continue;
                }
                h = RotaLedger.Mix(h, (c.enabled ? 1 : 0) + (c.repeatingOrder ? 2 : 0));
                for (int k = 0; k < c.items.Count; k++)
                {
                    h = RotaLedger.Mix(h, c.items[k] != null ? c.items[k].itemName : null);
                    h = RotaLedger.Mix(h, c.items[k] != null ? c.items[k].amount : 0);
                }
            }
            List<LogisticsManagerPlan> plans = Save != null ? Save.logisticsManagerPlans : null;
            for (int i = 0; plans != null && i < plans.Count; i++)
            {
                LogisticsManagerPlan plan = plans[i];
                for (int k = 0; plan != null && plan.destinations != null && k < plan.destinations.Count; k++)
                {
                    LogisticsManagerPlanDestination d = plan.destinations[k];
                    if (d == null || d.deliveryTargetAddress == null || d.deliveryTargetAddress != reg.Address || d.stockTargets == null)
                    {
                        continue;
                    }
                    h = RotaLedger.Mix(h, plan.assignedEmployeeId);
                    h = RotaLedger.Mix(h, k);
                    for (int t = 0; t < d.stockTargets.Count; t++)
                    {
                        h = RotaLedger.Mix(h, d.stockTargets[t] != null ? d.stockTargets[t].itemName : null);
                        h = RotaLedger.Mix(h, d.stockTargets[t] != null ? d.stockTargets[t].targetAmount : 0);
                    }
                }
            }
            return h;
        }
    }
}
