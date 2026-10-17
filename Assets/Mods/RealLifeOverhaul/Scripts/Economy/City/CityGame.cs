using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>Thousandths more (or less) of the price a district bears at one business for its interior (Scripts/Interiors). Default: nothing.</summary>
        public static Func<BuildingRegistration, int> Bears;

        public delegate bool FootfallWriter(BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written);

        public static void Reset()
        {
            Day = null;
            Footfall = null;
            Copycat = null;
            Bears = null;
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
    /// RENTS, WAGES, TAKINGS, SHOPS AND SERVICES are not changed anywhere: the game's own transactions of the day that
    /// has ended are added up (<see cref="DayFigures"/>) and the difference is dated transactions of the mod's own
    /// (<see cref="Book"/>), which EconoView and the Finance app list under their own name: one for each business,
    /// home and category the game booked the day's own under, shared out by those figures, so that each business's
    /// statement, the game's taxes and its bank see them (GameBooks.Refresh). Shops and services are
    /// the game's charges at prices of its own (<see cref="LivingTypes"/>), less what the mod paid back of them
    /// (EconomyBridges.GivenBack) and less what was bought at a wholesaler's, at today's prices already.
    ///
    /// Nothing here runs on a timer: once when the economy starts, once a game day, and when the app is opened.
    /// </summary>
    /// <summary>
    /// One of the city's lines for a day: the game's own figure, and where the game booked it, by address, the category
    /// the day's statement keeps it under and whether the game's tax deducts it, so that the line can be booked where
    /// its parts were and be deducted where they were. Each part is found by a dictionary (Core/LookupRules KeyedSums):
    /// the day's wages alone are one transaction an employee, thousands, which used to be compared with every part so far.
    /// </summary>
    internal sealed class DayLine
    {
        public float Total;
        private readonly KeyedSums<DayKey> _parts = new KeyedSums<DayKey>(DayKey.Comparer);

        /// <summary>The parts' figures, in the order each was first booked.</summary>
        public List<float> Figures => _parts.Sums;

        public Address AddressAt(int i) => _parts.Keys[i].Address;
        public string CategoryAt(int i) => _parts.Keys[i].Category;
        public bool DeductibleAt(int i) => _parts.Keys[i].Deductible;

        public void Add(Address address, string category, float figure, bool deductible = false)
        {
            Total += figure;
            _parts.Add(new DayKey { Address = address, Category = category, Deductible = deductible }, figure);
        }
    }

    /// <summary>
    /// A part of a day's line: the address (none for the player's own), the category and whether it is deducted. The same
    /// part as the game's own address test makes one (Address.Equals: the street and the number; no address is only no
    /// address), the same category text, the same deduction.
    /// </summary>
    internal struct DayKey
    {
        public Address Address;
        public string Category;
        public bool Deductible;

        public static readonly IEqualityComparer<DayKey> Comparer = new KeyComparer();

        private sealed class KeyComparer : IEqualityComparer<DayKey>
        {
            public bool Equals(DayKey a, DayKey b)
            {
                return a.Deductible == b.Deductible && a.Category == b.Category
                       && (ReferenceEquals(a.Address, b.Address) || (!ReferenceEquals(a.Address, null) && !ReferenceEquals(b.Address, null) && a.Address.Equals(b.Address)));
            }

            public int GetHashCode(DayKey k)
            {
                unchecked
                {
                    return ((ReferenceEquals(k.Address, null) ? 0 : k.Address.GetHashCode()) * 397 ^ (k.Category != null ? k.Category.GetHashCode() : 0)) * 2 + (k.Deductible ? 1 : 0);
                }
            }
        }
    }

    /// <summary>The game's own rent, wages, takings and shops and services of a day, each by where it was booked.</summary>
    internal sealed class DayBooks
    {
        public readonly DayLine Rent = new DayLine(), Wages = new DayLine(), Takings = new DayLine(), Living = new DayLine();
    }

    internal static class CityGame
    {
        public const string RentKey = "reallife_transaction_macro_rent";
        public const string WagesKey = "reallife_transaction_macro_wages";
        public const string TakingsKey = "reallife_transaction_macro_takings";
        public const string LivingKey = "reallife_transaction_macro_living";

        // The game's own charges at prices of its own that never move: tickets, rides, the hospital, the doctor, gyms and
        // entrance fees, shopping, fuel and stalls, fit-outs, training and hiring. (Not one under rent or salaries, adjusted
        // on their own lines; not goods bought at a wholesaler's or delivered, whose prices the city writes at today's; not
        // a building, vehicle or boat bought, nor loans, taxes, deposits or the casino.)
        private static readonly HashSet<string> LivingTypes = new HashSet<string>
        {
            "ba:transaction_parkingticket", "ba:transaction_subwayride", "ba:transaction_taxiride", "ba:transaction_hospitalbill",
            "ba:transaction_autotowservice", "ba:transaction_publicparking", "ba:transaction_doctorappointment", "ba:transaction_gymentrance",
            "ba:transaction_healthinsurance", "ba:transaction_entrancefee", "ba:transaction_electricscooterfee", "ba:transaction_pierride",
            "ba:transaction_casinoboatticket", "ba:transaction_purchaseattractionticket", "ba:transaction_purchasefromsellerstand",
            "ba:transaction_tuitionfee", "ba:transaction_itempurchase", "ba:transaction_interiordesigner", "ba:transaction_furniturerecolor",
            "ba:transaction_interiorinstallation", "ba:transaction_movingservice", "ba:transaction_employeetraining", "ba:transaction_hrtraining",
            "ba:transaction_recruitmentcampaign", "ba:transaction_replacementfee", "ba:transaction_licensingfee", "ba:transaction_marketing",
            "ba:transaction_boatyearlymaintenance", "ba:transaction_cleaningwage"
        };
        private const string PurchaseType = "ba:transaction_itempurchase";
        private const string WholesaleType = "ba:businesstype_wholesalestore";
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
        private static readonly List<float> PricedOwn = new List<float>(512);
        private static readonly HashSet<string> Monopolies = new HashSet<string>();   // as texts, only while a name holds '@' (ReadMonopolies)
        private static readonly HashSet<(string, string)> MonopolyPairs = new HashSet<(string, string)>();
        private static bool _monopolyTexts;
        private static readonly int[] KindCount = new int[MacroCatalogue.SupplyKinds];
        private static readonly int[] Factors = new int[MacroCatalogue.SupplyKinds];   // today's factor of each kind, while the prices are written

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
            PricedOwn.Clear();
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
                    PricedOwn.Add(own);
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
        /// Writes today's wholesale prices: each good's own price times its kind's factor and the price index. Every
        /// good is looked at (one comparison each) and only a price that is not what it should be is written, so a
        /// price the game has put back by itself is found the same day. Returns how many prices were written.
        /// </summary>
        public static int ApplyWholesale(CityState state)
        {
            int written = 0;
            bool applied = false;
            for (int k = 0; k < Factors.Length; k++)
            {
                Factors[k] = CityModel.Wholesale(state, k);
                applied |= Factors[k] != 1000;
            }
            for (int i = 0; i < Priced.Count; i++)
            {
                Item item = Priced[i];
                if (item == null)
                {
                    continue;   // (an item the game has unloaded has nothing to write to)
                }
                int factor = Factors[PricedKind[i]];
                float want = factor == 1000 ? PricedOwn[i] : PricedOwn[i] * (factor / 1000f);
                if (item.wholesalePrice != want)
                {
                    item.wholesalePrice = want;
                    written++;
                }
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
                float want = factor == 1000 ? PricedOwn[i] : PricedOwn[i] * (factor / 1000f);
                if (item.wholesalePrice != want)
                {
                    if (wrong++ < 3)
                    {
                        text.Append(wrong > 1 ? "; " : string.Empty).Append(item.itemName).Append(" is ").Append(item.wholesalePrice.ToString("0.###", CultureInfo.InvariantCulture)).Append(", expected ").Append(want.ToString("0.###", CultureInfo.InvariantCulture));
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
            PricedOwn.Clear();
            Monopolies.Clear();
            MonopolyPairs.Clear();
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
                shop.Against = ratio > 0 ? CityModel.Against(ratio, CityModel.WithBears(CityModel.Tolerance(state, shop.District, level), BearsAt(reg))) : 0;
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

        // What the business's interior adds to what its district bears (CityModel.WithBears keeps it inside its bounds).
        private static int BearsAt(BuildingRegistration reg)
        {
            try
            {
                return CityBridges.Bears != null ? CityBridges.Bears(reg) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // The game lets the one seller of a product in a neighbourhood charge more (ItemHelper.CalculateMaxAcceptablePrice): the
        // same allowance is made here. Each monopoly is kept as its product and neighbourhood, so a price line is looked up
        // with no text made for it (a business's ten price lines, every business, each time the figures are applied). The
        // game's names hold no '@'; were one to, its monopolies are kept as the texts they used to be, which match the same.
        private static void ReadMonopolies(GameInstance save)
        {
            Monopolies.Clear();
            MonopolyPairs.Clear();
            _monopolyTexts = false;
            List<ProductMarketEntry> entries = save.productMarketEntries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ProductMarketEntry entry = entries[i];
                for (int k = 0; entry != null && entry.demandValues != null && k < entry.demandValues.Count; k++)
                {
                    NeighborhoodDemand demand = entry.demandValues[k];
                    if (demand != null && demand.hasPlayerMonopoly && !string.IsNullOrEmpty(demand.neighborhood))
                    {
                        MonopolyPairs.Add((entry.itemName, demand.neighborhood));
                        _monopolyTexts |= (entry.itemName != null && entry.itemName.IndexOf('@') >= 0) || demand.neighborhood.IndexOf('@') >= 0;
                    }
                }
            }
            if (_monopolyTexts)
            {
                foreach ((string item, string district) pair in MonopolyPairs)
                {
                    Monopolies.Add(pair.item + "@" + pair.district);
                }
            }
        }

        private static bool Monopoly(string item, string district)
        {
            if (MonopolyPairs.Count == 0)
            {
                return false;
            }
            return _monopolyTexts ? Monopolies.Contains(item + "@" + district) : MonopolyPairs.Contains((item, district));
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
                float bears = good.Market * (index + (Monopoly(price.itemName, district) ? MacroCatalogue.MonopolyAllowance / 1000f : 0f));
                sum += price.price / bears;
                count++;
            }
            return count == 0 ? 0 : (int)Math.Round(sum / count * 1000.0);
        }

        // ---------------------------------------------------------------- the day's figures and the adjustments

        /// <summary>
        /// What the game's own run took in rent and wages and paid in takings for a day, from its transactions of that
        /// day (BusinessHelper.RunDaily and EmployeeHelper.PayDailyWages date them on the day that has ended): the day's
        /// transactions as GameBooks.Day keeps them (the morning's one walk of the history). The mod's own adjustments
        /// have no category and another type, so they are never counted.
        /// </summary>
        public static DayBooks DayFigures(int day)
        {
            var books = new DayBooks();
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.Transactions == null)
            {
                return books;
            }
            Func<string, bool> givenBack = EconomyBridges.GivenBack;
            List<Transaction> ofDay = GameBooks.Day(day);
            for (int i = 0; i < ofDay.Count; i++)
            {
                Transaction t = ofDay[i];
                // (The mod's own costs under the game's categories, a raise say, are at today's prices already: the game's alone are adjusted.)
                if (GameBooks.Ours(t))
                {
                    if (t.amount > 0f && givenBack != null && givenBack(t.transactionType))
                    {
                        books.Living.Add(null, Kept(t), -t.amount);   // a game charge the mod paid back: not the player's to pay at all
                    }
                    continue;
                }
                if (t.transactionType == RevenueType)
                {
                    books.Takings.Add(t.address, null, Mathf.Max(0f, t.amount));
                }
                else if (t.amount < 0f)
                {
                    if (t.transactionCategories != null && t.transactionCategories.Contains(RentCategory))
                    {
                        books.Rent.Add(t.address, RentCategory, -t.amount, t.isTaxDeductible);
                    }
                    else if (t.transactionCategories != null && t.transactionCategories.Contains(SalaryCategory))
                    {
                        books.Wages.Add(t.address, SalaryCategory, -t.amount, t.isTaxDeductible);
                    }
                    else if (LivingTypes.Contains(t.transactionType) && !AtWholesale(t))
                    {
                        // (A business's marketing and licences are in its statement, by its address; the day's own figures
                        // of parking, health insurance and the headhunter by their category alone; the rest is the player's.)
                        string category = Kept(t);
                        books.Living.Add(category == GameBooks.Marketing || category == GameBooks.Licensing ? t.address : null, category, -t.amount, t.isTaxDeductible);
                    }
                }
            }
            return books;
        }

        // The category of the game's a charge is kept under where the day's statement has a line for it.
        private static string Kept(Transaction t)
        {
            List<string> categories = t.transactionCategories;
            for (int i = 0; categories != null && i < categories.Count; i++)
            {
                string c = categories[i];
                if (c == GameBooks.Marketing || c == GameBooks.Licensing || c == GameBooks.Parking || c == GameBooks.HealthInsurance || c == GameBooks.Headhunter)
                {
                    return c;
                }
            }
            return null;
        }

        // Bought at a wholesaler's: at today's prices already, as the city writes the goods' wholesale prices (above).
        private static bool AtWholesale(Transaction t)
        {
            if (t.transactionType != PurchaseType || t.address == null)
            {
                return false;
            }
            BuildingRegistration seller = BuildingHelper.GetBuildingRegistration(t.address);
            return seller != null && seller.businessTypeName == WholesaleType;
        }

        /// <summary>Cash in hand, whole and never under nothing.</summary>
        public static int Cash => SaveGameManager.Current != null ? Mathf.FloorToInt(Mathf.Max(0f, SaveGameManager.Current.Money)) : 0;

        /// <summary>
        /// One adjustment, as a transaction of the mod's own dated on the day it is for. More than nothing is paid to
        /// the player; less is taken, and only ever what the caller has found to be in hand (the game's own call refuses
        /// a charge the cash does not cover: false, and nothing is taken).
        /// </summary>
        public static bool Book(int amount, string key, int day, string percent, Address address = null, string category = null, bool deductible = false)
        {
            if (amount == 0 || SaveGameManager.Current == null)
            {
                return amount == 0;
            }
            // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
            var data = new Dictionary<string, string>(2) { { "percent", percent } };
            var info = category != null ? new TransactionInfo(key, category, data) : new TransactionInfo(key, data);
            if (deductible && amount < 0)
            {
                info.SetTaxDeductibleName(key);   // the game's tax deducts it as it does the charges it adjusts, under its own name
            }
            return GameManager.ChangeMoneySafe(amount, info, day, address, amount > 0);
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
