using System;
using System.Collections.Generic;
using System.Text;
using AI.Citizens;
using BAModAPI;
using BigAmbitions.Items;
using BigAmbitions.Neighborhoods;
using Entities;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the city's economy asks of the rest of the mod. Each is optional and has a default, so Economy/City works
    /// alone; Scripts/Wiring/CityLinks.cs fills them in. The city's economy never calls another feature directly.
    /// </summary>
    public static class CityBridges
    {
        /// <summary>The season, the weather and the city's event of a game day. Default: spring, fair, nothing on.</summary>
        public static Func<int, CityDay> Day;

        /// <summary>
        /// Has a business's promotion figure written again as it should stand now (its own, anything the business
        /// feature has running there, and the city's economy on top), the customers still to come drawn again if
        /// asked. False where the game keeps no such figure. Default: nothing is written, so footfall is not changed.
        /// </summary>
        public static FootfallWriter Footfall;

        /// <summary>A rival opens near a business and undercuts it, as the business feature's own story. False if that feature will not start one there today. Default: never.</summary>
        public static Func<BuildingRegistration, bool> Copycat;

        public delegate bool FootfallWriter(BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written);

        public static void Reset()
        {
            Day = null;
            Footfall = null;
            Copycat = null;
        }
    }

    /// <summary>One business of the player's as the city's economy sees it today.</summary>
    public sealed class CityShop
    {
        public BuildingRegistration Reg;
        public CityDistrict District;
        public int Cycle, Area, Price;  // the three parts of its footfall, per cent of customers
        public int Wanted;              // the three together, as asked for
        public int Against;             // its prices against what the district bears, thousandths; 0 with no price to judge by
        public bool Over;
        public bool HasFigure;          // the game keeps a promotion figure for it, so its footfall can be changed
        public int Own, Written;        // that figure: the game's own and what stands now
        public bool Applied;            // something of the mod's is in the figure

        /// <summary>The change in customers the figure written gives, per cent, by the game's own formula.</summary>
        public int Achieved(float baseMultiplier)
        {
            float own = baseMultiplier + 0.75f * Own / 100f;
            return !HasFigure || own <= 0f ? 0 : Mathf.RoundToInt(((baseMultiplier + 0.75f * Written / 100f) / own - 1f) * 100f);
        }
    }

    /// <summary>
    /// Everything the city's economy reads from or does to the game, in one place.
    ///
    /// WHOLESALE PRICES. The game works a wholesale or import price out afresh each time it is asked for
    /// (ItemHelper.GetWholesalePrice: the item's own <c>wholesalePrice</c>, the market's import index and the game's
    /// price multiplier), at the wholesaler's shelf, on a delivery contract and at the importer alike. The item is an
    /// asset in memory (a ScriptableObject), not part of a save. So writing <c>wholesalePrice</c> moves every one of
    /// those prices at once and nothing of it reaches a save. The first value seen on an item is its own and is kept
    /// for as long as the game runs (never taken from an item this class has already written to), so
    /// <see cref="RestoreWholesale"/> is exact however often it and <see cref="ApplyWholesale"/> run. What a purchase
    /// cost is the player's own business afterwards, as any price paid is.
    ///
    /// Only goods with a wholesale price are touched: an item of the game's that is a retail product. Each is of one
    /// kind (<see cref="MacroCatalogue.KindOfType"/>), told from the business types that sell it.
    ///
    /// RENTS, WAGES AND TAKINGS are not changed anywhere: the game's own transactions of the day that has ended are
    /// added up (<see cref="DayFigures"/>) and the difference is one dated transaction of the mod's own each
    /// (<see cref="Book"/>), which EconoView and the Finance app list under its own name.
    ///
    /// Nothing here runs on a timer: once when the economy starts, once a game day, and when the app is opened.
    /// </summary>
    internal static class CityGame
    {
        public const string RentKey = "reallife_transaction_macro_rent";
        public const string WagesKey = "reallife_transaction_macro_wages";
        public const string TakingsKey = "reallife_transaction_macro_takings";
        private const string Global = "ba:neighborhood_global";
        private const string RentCategory = "ba:transactioncategory_rent";
        private const string SalaryCategory = "ba:transactioncategory_salaryexpenses";
        private const string RevenueType = "ba:transaction_revenue";

        private struct Good
        {
            public Item Item;
            public int Kind;        // the kind of goods; -1 for something sold that has no wholesale price (a service)
            public float Market;    // the game's default market price
        }

        public static IModLogger Logger;

        // Kept across city loads on purpose: an item written to in one city is the same object in the next.
        private static readonly Dictionary<Item, float> Own = new Dictionary<Item, float>(512);
        private static readonly Dictionary<string, Good> Goods = new Dictionary<string, Good>(512);
        private static readonly List<Item> Priced = new List<Item>(512);
        private static readonly List<int> PricedKind = new List<int>(512);
        private static readonly int[] Written = new int[MacroCatalogue.SupplyKinds];   // the factor last written for each kind, thousandths; 0 for the game's own
        private static readonly HashSet<string> Monopolies = new HashSet<string>();
        private static readonly int[] KindCount = new int[MacroCatalogue.SupplyKinds];

        /// <summary>True while any wholesale price in memory is this class's and not the game's own.</summary>
        public static bool WholesaleApplied { get; private set; }

        public static int GoodsCount => Priced.Count;

        // ---------------------------------------------------------------- districts

        /// <summary>Every neighbourhood of the game becomes a district, started from the game's own class shares. Returns how many.</summary>
        public static int Districts(CityState state)
        {
            NeighborhoodData[] all = NeighborhoodHelper.NeighborhoodsData;
            int known = 0;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                NeighborhoodData data = all[i];
                if (data == null || string.IsNullOrEmpty(data.neighbourhood) || data.neighbourhood == Global)
                {
                    continue;
                }
                CityModel.Know(state, data.neighbourhood, CityModel.StartIncome(data.workingClassPercentage, data.middleClassPercentage, data.upperClassClassPercentage));
                known++;
            }
            return known;
        }

        /// <summary>A district's name as the game writes it.</summary>
        public static string Name(string district)
        {
            return string.IsNullOrEmpty(district) ? string.Empty : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        // ---------------------------------------------------------------- goods

        /// <summary>
        /// One look through the game's business types and items: which kind each product is, and which have a
        /// wholesale price. Each one's own price is noted the first time it is ever seen. Returns how many have one.
        /// </summary>
        public static int FindGoods()
        {
            Goods.Clear();
            Priced.Clear();
            PricedKind.Clear();
            Array.Clear(KindCount, 0, KindCount.Length);
            var rank = new Dictionary<string, int>(512);   // the best (lowest) place in the catalogue's list of a type that sells the item
            foreach (string typeName in BusinessTypeHelper.BusinessTypeNames)
            {
                BusinessType type = BusinessTypeHelper.GetData(typeName);
                int place = MacroCatalogue.TypeRank(typeName);
                if (type == null || type.businessProducts == null || place < 0)
                {
                    continue;
                }
                for (int p = 0; p < type.businessProducts.Length; p++)
                {
                    string item = type.businessProducts[p] != null ? type.businessProducts[p].itemName : null;
                    int best;
                    if (!string.IsNullOrEmpty(item) && (!rank.TryGetValue(item, out best) || place < best))
                    {
                        rank[item] = place;
                    }
                }
            }
            IEnumerable<Item> items = ItemsGetter.AllItems;
            if (items == null)
            {
                return 0;
            }
            foreach (Item item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemName) || (item.type & (ItemType.RetailProduct | ItemType.ServiceProduct)) == 0)
                {
                    continue;
                }
                float own;
                if (!Own.TryGetValue(item, out own))
                {
                    own = item.wholesalePrice;
                    Own[item] = own;
                }
                bool priced = (item.type & ItemType.RetailProduct) != 0 && own > 0f;
                int place;
                int kind = !priced ? -1 : rank.TryGetValue(item.itemName, out place) ? MacroCatalogue.KindOfTypeIs[place] : MacroCatalogue.General;
                Goods[item.itemName] = new Good { Item = item, Kind = kind, Market = item.DefaultMarketPrice };
                if (priced)
                {
                    Priced.Add(item);
                    PricedKind.Add(kind);
                    KindCount[kind]++;
                }
            }
            return Priced.Count;
        }

        /// <summary>How many of the game's goods are of a kind.</summary>
        public static int CountOf(int kind)
        {
            return kind >= 0 && kind < KindCount.Length ? KindCount[kind] : 0;
        }

        /// <summary>
        /// Writes today's wholesale prices: each good's own price times its kind's factor and the price index. A kind
        /// whose factor has not changed since it was last written costs one comparison. Returns how many prices were written.
        /// </summary>
        public static int ApplyWholesale(CityState state)
        {
            int written = 0;
            bool any = false;
            for (int k = 0; k < Written.Length; k++)
            {
                int factor = CityModel.Wholesale(state, k);
                any |= factor != Written[k] && !(Written[k] == 0 && factor == 1000);   // (nothing written yet and nothing to write is no change)
            }
            if (!any)
            {
                return 0;
            }
            for (int i = 0; i < Priced.Count; i++)
            {
                Item item = Priced[i];
                int factor = CityModel.Wholesale(state, PricedKind[i]);
                if (item == null || factor == Written[PricedKind[i]])
                {
                    continue;   // (an item the game has unloaded has nothing to write to)
                }
                float want = factor == 1000 ? Own[item] : Own[item] * (factor / 1000f);
                if (item.wholesalePrice != want)
                {
                    item.wholesalePrice = want;
                    written++;
                }
            }
            bool applied = false;
            for (int k = 0; k < Written.Length; k++)
            {
                Written[k] = CityModel.Wholesale(state, k);
                applied |= Written[k] != 1000;
            }
            WholesaleApplied = applied;
            return written;
        }

        /// <summary>
        /// Puts every good's own wholesale price back, exactly: those of this city and any other item this class ever
        /// wrote to. Safe to call at any time and any number of times. Returns how many were put back.
        /// </summary>
        public static int RestoreWholesale()
        {
            int restored = 0;
            List<Item> dead = null;
            foreach (KeyValuePair<Item, float> pair in Own)
            {
                Item item = pair.Key;
                if (item == null)
                {
                    (dead ?? (dead = new List<Item>())).Add(item);
                    continue;
                }
                if (item.wholesalePrice != pair.Value)
                {
                    item.wholesalePrice = pair.Value;
                    restored++;
                }
            }
            for (int i = 0; dead != null && i < dead.Count; i++)
            {
                Own.Remove(dead[i]);   // an item the game has unloaded: when it is loaded again it has its own price
            }
            Array.Clear(Written, 0, Written.Length);
            WholesaleApplied = false;
            return restored;
        }

        /// <summary>Every good's wholesale price in memory is what the state says (or, with no state, the game's own). The detail names the first few that are not.</summary>
        public static bool CheckWholesale(CityState state, out string detail)
        {
            int wrong = 0;
            var text = new StringBuilder(120);
            for (int i = 0; i < Priced.Count; i++)
            {
                Item item = Priced[i];
                if (item == null)
                {
                    continue;
                }
                int factor = state != null ? CityModel.Wholesale(state, PricedKind[i]) : 1000;
                float want = factor == 1000 ? Own[item] : Own[item] * (factor / 1000f);
                if (item.wholesalePrice != want)
                {
                    if (wrong++ < 3)
                    {
                        text.Append(wrong > 1 ? "; " : string.Empty).Append(item.itemName).Append(" is ").Append(item.wholesalePrice.ToString("0.###")).Append(", expected ").Append(want.ToString("0.###"));
                    }
                }
            }
            detail = Priced.Count + " good(s) with a wholesale price, " + wrong + " not as expected" + (wrong > 0 ? " (" + text + ")" : string.Empty);
            return wrong == 0;
        }

        /// <summary>The city has gone: the lists are forgotten (each good's own price is kept for the next city).</summary>
        public static void Forget()
        {
            Goods.Clear();
            Priced.Clear();
            PricedKind.Clear();
            Monopolies.Clear();
        }

        // ---------------------------------------------------------------- the player's businesses

        /// <summary>
        /// The player's businesses as they stand today: each one's district, the three parts of its footfall, and its
        /// prices against what the district bears. One pass over the save's buildings and each business's price list,
        /// and one over the game's market entries for the monopolies. Also notes which kinds of goods the player's
        /// businesses stock (their price lists and their delivery contracts) in <see cref="CityState.Buys"/>.
        /// </summary>
        public static void ReadShops(CityState state, int level, List<CityShop> into, List<CityShop> pool)
        {
            for (int i = 0; i < into.Count; i++)
            {
                pool.Add(into[i]);
            }
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            if (all == null)
            {
                return;
            }
            ReadMonopolies(save);
            int buys = 0;
            int cycle = CityModel.CycleFootfall(state.Cycle, level);
            for (int i = 0; i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (!Books.IsBusiness(reg))
                {
                    continue;
                }
                CityShop shop;
                if (pool.Count > 0)
                {
                    shop = pool[pool.Count - 1];
                    pool.RemoveAt(pool.Count - 1);
                }
                else
                {
                    shop = new CityShop();
                }
                string district;
                try
                {
                    district = reg.Neighborhood;
                }
                catch (Exception)
                {
                    district = null;   // a business whose building cannot be read is in no district: it gets the cycle's part alone
                }
                shop.Reg = reg;
                shop.District = state.District(district);
                shop.Cycle = cycle;
                shop.Area = CityModel.DistrictFootfall(shop.District);
                int ratio = PriceRatio(reg, district, ref buys);
                shop.Against = ratio > 0 ? CityModel.Against(ratio, CityModel.Tolerance(state, shop.District, level)) : 0;
                shop.Over = shop.Against > 0 && CityModel.Over(shop.Against);
                shop.Price = CityModel.PriceFootfall(shop.Against);
                shop.Wanted = CityModel.Footfall(shop.Cycle, shop.Area, shop.Price);
                shop.HasFigure = false;
                shop.Own = shop.Written = 0;
                shop.Applied = false;
                into.Add(shop);
            }
            List<DeliveryContract> contracts = save.DeliveryContracts;
            for (int i = 0; contracts != null && i < contracts.Count; i++)
            {
                DeliveryContract c = contracts[i];
                for (int k = 0; c != null && c.enabled && c.items != null && k < c.items.Count; k++)
                {
                    Good good;
                    if (c.items[k] != null && c.items[k].itemName != null && Goods.TryGetValue(c.items[k].itemName, out good) && good.Kind >= 0)
                    {
                        buys |= 1 << good.Kind;
                    }
                }
            }
            state.Buys = buys;
        }

        // The game lets the one seller of a product in a neighbourhood charge more (ItemHelper.CalculateMaxAcceptablePrice): the same allowance is made here.
        private static void ReadMonopolies(GameInstance save)
        {
            Monopolies.Clear();
            List<ProductMarketEntry> entries = save.productMarketEntries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ProductMarketEntry entry = entries[i];
                for (int k = 0; entry != null && entry.demandValues != null && k < entry.demandValues.Count; k++)
                {
                    NeighborhoodDemand demand = entry.demandValues[k];
                    if (demand != null && demand.hasPlayerMonopoly && !string.IsNullOrEmpty(demand.neighborhood))
                    {
                        Monopolies.Add(entry.itemName + "@" + demand.neighborhood);
                    }
                }
            }
        }

        // A business's prices against the game's own price for its neighbourhood (the default market price times the
        // neighbourhood's price index, with the monopoly allowance), as a mean over its price list, in thousandths.
        // 0 if it has no price to judge by.
        private static int PriceRatio(BuildingRegistration reg, string district, ref int buys)
        {
            List<RetailPrice> prices = reg.retailPrices;
            float index;
            if (prices == null || string.IsNullOrEmpty(district) || !CitizenHelper.averagePriceIndicesInNeighborhoods.TryGetValue(district, out index) || index <= 0f)
            {
                return 0;
            }
            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < prices.Count; i++)
            {
                RetailPrice price = prices[i];
                Good good;
                if (price == null || price.itemName == null || !Goods.TryGetValue(price.itemName, out good))
                {
                    continue;
                }
                if (good.Kind >= 0)
                {
                    buys |= 1 << good.Kind;
                }
                if (price.price <= 0f || good.Market <= 0f)
                {
                    continue;
                }
                float bears = good.Market * (index + (Monopolies.Count > 0 && Monopolies.Contains(price.itemName + "@" + district) ? MacroCatalogue.MonopolyAllowance / 1000f : 0f));
                sum += price.price / bears;
                count++;
            }
            return count == 0 ? 0 : (int)Math.Round(sum / count * 1000.0);
        }

        // ---------------------------------------------------------------- the day's figures and the adjustments

        /// <summary>
        /// What the game's own run took in rent and wages and paid in takings for a day, from its transactions of that
        /// day (BusinessHelper.RunDaily and EmployeeHelper.PayDailyWages date them on the day that has ended). One pass
        /// over the game's transaction history. The mod's own adjustments have no category and another type, so they
        /// are never counted.
        /// </summary>
        public static void DayFigures(int day, out float rent, out float wages, out float takings)
        {
            rent = wages = takings = 0f;
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.Transactions == null)
            {
                return;
            }
            foreach (Transaction t in save.Transactions)
            {
                if (t == null || t.timestamp == null || t.timestamp.Day != day)
                {
                    continue;
                }
                if (t.transactionType == RevenueType)
                {
                    takings += Mathf.Max(0f, t.amount);
                }
                else if (t.amount < 0f && t.transactionCategories != null)
                {
                    if (t.transactionCategories.Contains(RentCategory))
                    {
                        rent -= t.amount;
                    }
                    else if (t.transactionCategories.Contains(SalaryCategory))
                    {
                        wages -= t.amount;
                    }
                }
            }
        }

        /// <summary>Cash in hand, whole and never under nothing.</summary>
        public static int Cash => SaveGameManager.Current != null ? Mathf.FloorToInt(Mathf.Max(0f, SaveGameManager.Current.Money)) : 0;

        /// <summary>
        /// One adjustment, as a transaction of the mod's own dated on the day it is for. More than nothing is paid to
        /// the player; less is taken, and only ever what the caller has found to be in hand (the game's own call refuses
        /// a charge the cash does not cover: false, and nothing is taken).
        /// </summary>
        public static bool Book(int amount, string key, int day, string percent)
        {
            if (amount == 0 || SaveGameManager.Current == null)
            {
                return amount == 0;
            }
            // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
            var info = new TransactionInfo(key, new Dictionary<string, string>(1) { { "percent", percent } });
            return GameManager.ChangeMoneySafe(amount, info, day, null, amount > 0);
        }

        // ---------------------------------------------------------------- property

        /// <summary>What the game says a building the player owns is worth: its market value (Building.GetMarketValue).</summary>
        public static float MarketValue(RealEstate estate)
        {
            try
            {
                Buildings.Building building = estate != null ? estate.Building : null;
                return building != null ? Mathf.Max(0f, building.GetMarketValue()) : 0f;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        public static string DistrictOf(RealEstate estate)
        {
            try
            {
                Buildings.Building building = estate != null ? estate.Building : null;
                return building != null ? building.Neighbourhood : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
