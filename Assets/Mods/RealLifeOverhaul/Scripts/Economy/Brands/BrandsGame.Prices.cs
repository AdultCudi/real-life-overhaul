using System;
using System.Collections.Generic;
using AI.Citizens;
using BigAmbitions.Items;
using Buildings.Office.Headquarters;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The game part for prices: a shop's prices written as the game's own price cell writes them, and what kept
    /// prices read (the market price, the district's price index and demand, the pricing manager's ceiling, the
    /// wholesale price).
    /// </summary>
    internal static partial class BrandsGame
    {
        /// <summary>
        /// The flagship's prices at another shop of the brand, for each product both sell, written as the game's own
        /// price cell writes them. A product the shop does not sell is not added. Returns how many prices changed.
        /// </summary>
        public static int SharePrices(BuildingRegistration from, BuildingRegistration to)
        {
            int changed = 0;
            List<RetailPrice> source = from != null ? from.retailPrices : null;
            for (int i = 0; to != null && to.retailPrices != null && source != null && i < source.Count; i++)
            {
                RetailPrice p = source[i];
                if (p != null && !string.IsNullOrEmpty(p.itemName) && p.price > 0f && SetPrice(to, p.itemName, p.price))
                {
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>A shop's prices put at the middle of what a city chain asks, against the game's own market price for each product. Returns how many changed.</summary>
        public static int ChainPrices(BuildingRegistration reg, int chain)
        {
            int changed = 0;
            float factor = (BrandCatalogue.ChainPriceLeast[chain] + BrandCatalogue.ChainPriceMost[chain]) / 2000f;
            List<RetailPrice> prices = reg != null ? reg.retailPrices : null;
            for (int i = 0; prices != null && i < prices.Count; i++)
            {
                RetailPrice p = prices[i];
                float market = p != null && !string.IsNullOrEmpty(p.itemName) ? ItemHelper.GetDefaultMarketPrice(p.itemName) : 0f;
                if (market > 0f && SetPrice(reg, p.itemName, Mathf.Round(market * factor * 100f) / 100f))
                {
                    changed++;
                }
            }
            return changed;
        }

        // One price where the shop already has one for the product: the price list, the stored one, then the game's own
        // market update (InventoryProductCellView). True if it changed.
        private static bool SetPrice(BuildingRegistration reg, string item, float price)
        {
            RetailPrice mine = null;
            for (int i = 0; i < reg.retailPrices.Count; i++)
            {
                if (reg.retailPrices[i] != null && reg.retailPrices[i].itemName == item)
                {
                    mine = reg.retailPrices[i];
                    break;
                }
            }
            if (mine == null || Mathf.Abs(mine.price - price) < 0.005f)
            {
                return false;
            }
            mine.price = price;
            bool stored = false;
            for (int i = 0; reg.storedRetailPrices != null && i < reg.storedRetailPrices.Count; i++)
            {
                if (reg.storedRetailPrices[i] != null && reg.storedRetailPrices[i].itemName == item)
                {
                    reg.storedRetailPrices[i].price = price;
                    stored = true;
                }
            }
            if (!stored && reg.storedRetailPrices != null)
            {
                reg.storedRetailPrices.Add(new RetailPrice { itemName = item, price = price });
            }
            try
            {
                ProductMarketHelper.UpdateMarketDemand(item);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            SaveGameManager.MarkChange();
            return true;
        }

        /// <summary>The game's own market price for a product, what a city chain judges prices against; 0 for none.</summary>
        public static float Market(string item)
        {
            try
            {
                return string.IsNullOrEmpty(item) ? 0f : Mathf.Max(0f, ItemHelper.GetDefaultMarketPrice(item));
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>The game's price index for a neighbourhood, thousandths; 0 where it has none.</summary>
        public static int DistrictIndex(string district)
        {
            float index;
            return !string.IsNullOrEmpty(district) && CitizenHelper.averagePriceIndicesInNeighborhoods.TryGetValue(district, out index) && index > 0f ? Mathf.RoundToInt(index * 1000f) : 0;
        }

        /// <summary>
        /// The game's demand for a product in a district (0 to 100) and whether the player is its only seller there; -1 for a
        /// product the game keeps no demand for.
        /// </summary>
        public static int Demand(string item, string district, out bool monopoly)
        {
            monopoly = false;
            List<ProductMarketEntry> entries = Save != null ? Save.productMarketEntries : null;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ProductMarketEntry entry = entries[i];
                if (entry == null || entry.itemName != item)
                {
                    continue;
                }
                for (int k = 0; entry.demandValues != null && k < entry.demandValues.Count; k++)
                {
                    NeighborhoodDemand d = entry.demandValues[k];
                    if (d != null && d.neighborhood == district)
                    {
                        monopoly = d.hasPlayerMonopoly;
                        return Mathf.Clamp(d.demand, 0, 100);
                    }
                }
                return -1;
            }
            return -1;
        }

        /// <summary>
        /// The highest price the game's own pricing manager would set for a product in a district, thousandths of its market
        /// price; 0 where it would set none.
        /// </summary>
        public static int Ceiling(string item, string district, float market)
        {
            try
            {
                BigAmbitions.Items.Item data = ItemsGetter.GetByName(item);
                if (data == null || !data.isADemandedProduct || market <= 0f || string.IsNullOrEmpty(district))
                {
                    return 0;
                }
                int cents = PricingManagerHelper.GetHighestAcceptableCents(item, district);
                return cents > 0 ? Mathf.RoundToInt(cents / 100f / market * 1000f) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>What the product costs the player at the wholesaler today, thousandths of its market price; 0 for none (a service).</summary>
        public static int Cost(string item, float market)
        {
            try
            {
                BigAmbitions.Items.Item data = ItemsGetter.GetByName(item);
                float cost = data != null && market > 0f ? data.GetWholesalePrice() : 0f;
                return cost > 0f ? Mathf.RoundToInt(cost / market * 1000f) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>True while one of the game's own pricing managers watches the district: it sets the prices of every shop of the player's there once a day.</summary>
        public static bool ManagerWatches(string district)
        {
            try
            {
                return !string.IsNullOrEmpty(district) && Save != null && Save.pricingManagerPlans != null && PricingManagerHelper.GetPlanCoveringNeighborhood(district) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The price a shop asks for a product; false where it has none.</summary>
        public static bool PriceOf(BuildingRegistration reg, string item, out float price)
        {
            price = 0f;
            List<RetailPrice> prices = reg != null ? reg.retailPrices : null;
            for (int i = 0; prices != null && i < prices.Count; i++)
            {
                if (prices[i] != null && prices[i].itemName == item)
                {
                    price = prices[i].price;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// One kept price, written as the game's own price cell writes one; with <paramref name="add"/>, a product with no
        /// price is given one as the game's pricing manager adds one. Only at the player's own business, never another
        /// player's in a Going Public session. True if it changed.
        /// </summary>
        public static bool KeepPrice(BuildingRegistration reg, string item, float price, string district, bool add)
        {
            if (reg == null || reg.retailPrices == null || string.IsNullOrEmpty(item) || price <= 0f || !GoingPublic.Mine(reg))
            {
                return false;
            }
            RetailPrice mine = null;
            for (int i = 0; i < reg.retailPrices.Count; i++)
            {
                if (reg.retailPrices[i] != null && reg.retailPrices[i].itemName == item)
                {
                    mine = reg.retailPrices[i];
                    break;
                }
            }
            if (mine == null && !add)
            {
                return false;
            }
            if (mine != null && Mathf.Abs(mine.price - price) < KeptPrices.Cent)
            {
                return false;
            }
            bool added = mine == null;
            if (added)
            {
                reg.retailPrices.Add(new RetailPrice { itemName = item, price = price });
            }
            else
            {
                mine.price = price;
            }
            bool stored = false;
            for (int i = 0; reg.storedRetailPrices != null && i < reg.storedRetailPrices.Count; i++)
            {
                if (reg.storedRetailPrices[i] != null && reg.storedRetailPrices[i].itemName == item)
                {
                    reg.storedRetailPrices[i].price = price;
                    stored = true;
                }
            }
            if (!stored && reg.storedRetailPrices != null)
            {
                reg.storedRetailPrices.Add(new RetailPrice { itemName = item, price = price });
            }
            try
            {
                if (added)
                {
                    ProductMarketHelper.UpdateMarketDemand(item);
                }
                else if (!string.IsNullOrEmpty(district))
                {
                    ItemHelper.GetLowestMarketPrice(item, district, true);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            SaveGameManager.MarkChange();
            return true;
        }
    }
}
