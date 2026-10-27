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
    /// <summary>What the city's economy asks of other features, filled in by Wiring/CityLinks; each may be left null.</summary>
    public static class CityBridges
    {
        /// <summary>The season, the weather and the city's event of a game day; null means spring, fair, nothing on.</summary>
        public static Func<int, CityDay> Day;

        /// <summary>Writes a business's promotion figure as it should stand now, redrawing its customers if asked; false where the game keeps none.</summary>
        public static FootfallWriter Footfall;

        /// <summary>Starts the business feature's "a rival undercuts you" at a business; false if it will not today.</summary>
        public static Func<BuildingRegistration, bool> Copycat;

        /// <summary>Thousandths more (or less) of the price a district bears at one business, for its interior.</summary>
        public static Func<BuildingRegistration, int> Bears;

        /// <summary>
        /// A raw material's price against its own, set by the stock market's raw materials exchange in place of its kind's
        /// factor and the price index (Markets/Derivatives); NaN for an item the exchange does not price now.
        /// </summary>
        public static Func<string, double> MaterialFactor;

        public delegate bool FootfallWriter(BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written);

        public static void Reset()
        {
            Day = null;
            Footfall = null;
            Copycat = null;
            Bears = null;
            MaterialFactor = null;
        }
    }

    /// <summary>One business of the player's as the city's economy sees it today.</summary>
    public sealed class CityShop
    {
        public BuildingRegistration Reg;
        public CityDistrict District;
        public int Cycle, Area, Price;  // the three parts of its footfall, per cent of customers
        public int Wanted;              // the three together
        public int Against;             // its prices against what the district bears, thousandths; 0 with no price to judge by
        public bool Over;
        public bool HasFigure;          // the game keeps a promotion figure for it, so its footfall can be changed
        public int Own, Written;        // that figure: the game's own and what stands now
        public bool Applied;            // something of the mod's is in the figure

        /// <summary>The change in customers the figure written gives, per cent, by the game's formula (CustomerEntriesCalculator).</summary>
        public int Achieved(float baseMultiplier)
        {
            float own = baseMultiplier + 0.75f * Own / 100f;
            return !HasFigure || own <= 0f ? 0 : Mathf.RoundToInt(((baseMultiplier + 0.75f * Written / 100f) / own - 1f) * 100f);
        }
    }

    /// <summary>One of the city's lines for a day: the game's figure, split by address, statement category and deduction as it was booked.</summary>
    internal sealed class DayLine
    {
        public float Total;
        private readonly KeyedSums<DayKey> _parts = new KeyedSums<DayKey>(DayKey.Comparer);

        /// <summary>The parts' figures, in the order each was first booked.</summary>
        public List<float> Figures => _parts.Sums;

        public Address AddressAt(int i) => _parts.Keys[i].Address;
        public string CategoryAt(int i) => _parts.Keys[i].Category;

        /// <summary>The name the game deducted the part's charges under; empty for deductible without a name, null for not deductible.</summary>
        public string DeductedAt(int i) => _parts.Keys[i].Deducted;

        public void Add(Address address, string category, float figure, string deducted = null)
        {
            Total += figure;
            _parts.Add(new DayKey { Address = address, Category = category, Deducted = deducted }, figure);
        }
    }

    /// <summary>A part of a day's line. Addresses match as the game's Address.Equals does, except that null matches only null.</summary>
    internal struct DayKey
    {
        public Address Address;
        public string Category;
        public string Deducted;   // the game's deduction name; null: not deductible

        public static readonly IEqualityComparer<DayKey> Comparer = new KeyComparer();

        private sealed class KeyComparer : IEqualityComparer<DayKey>
        {
            public bool Equals(DayKey a, DayKey b)
            {
                return a.Deducted == b.Deducted && a.Category == b.Category
                       && (ReferenceEquals(a.Address, b.Address) || (!ReferenceEquals(a.Address, null) && !ReferenceEquals(b.Address, null) && a.Address.Equals(b.Address)));
            }

            public int GetHashCode(DayKey k)
            {
                unchecked
                {
                    int address = ReferenceEquals(k.Address, null) ? 0 : k.Address.GetHashCode();
                    return (address * 397 ^ (k.Category != null ? k.Category.GetHashCode() : 0)) * 2 + (k.Deducted != null ? 1 : 0);
                }
            }
        }
    }

    /// <summary>The game's own rent, wages, takings and shops and services of a day, each by where it was booked.</summary>
    internal sealed class DayBooks
    {
        public readonly DayLine Rent = new DayLine(), Wages = new DayLine(), Takings = new DayLine(), Living = new DayLine();
    }

    /// <summary>
    /// Everything the city's economy reads from or does to the game: wholesale prices written on the items in memory
    /// (never saved; each item's own price kept, so a restore is exact), and transactions of the mod's own adjusting the
    /// day's rent, wages, takings and the game's shops and services. Runs on events, nothing per frame.
    /// </summary>
    internal static partial class CityGame
    {
        public const string RentKey = "reallife_transaction_macro_rent";
        public const string WagesKey = "reallife_transaction_macro_wages";
        public const string TakingsKey = "reallife_transaction_macro_takings";
        public const string LivingKey = "reallife_transaction_macro_living";

        // The game's charges at fixed prices of its own, adjusted as the cost of living. Rent and wages have lines of their
        // own; wholesale goods are at today's prices already.
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

        // Each item's own wholesale price, kept across city loads: an item written to in one city is the same object in the next.
        private static readonly Dictionary<Item, float> Own = new Dictionary<Item, float>(512);
        private static readonly Dictionary<string, Good> Goods = new Dictionary<string, Good>(512);
        private static readonly List<Item> Priced = new List<Item>(512);
        private static readonly List<int> PricedKind = new List<int>(512);
        private static readonly List<float> PricedOwn = new List<float>(512);
        private static readonly HashSet<string> Monopolies = new HashSet<string>();   // as texts, only while a name holds '@'
        private static readonly HashSet<(string, string)> MonopolyPairs = new HashSet<(string, string)>();
        private static bool _monopolyTexts;
        private static readonly int[] KindCount = new int[MacroCatalogue.SupplyKinds];
        private static readonly int[] Factors = new int[MacroCatalogue.SupplyKinds];   // today's factor of each kind, while the prices are written

        /// <summary>Every neighbourhood of the game becomes a district, started from the game's own class shares. Returns how many.</summary>
        public static int Districts(CityState state)
        {
            NeighborhoodData[] all = NeighborhoodHelper.NeighborhoodsData;
            int known = 0;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                NeighborhoodData data = all[i];
                if (data == null || string.IsNullOrEmpty(data.neighbourhood) || data.neighbourhood == Global || !CityCheck.Here(data.neighbourhood))
                {
                    continue;
                }
                CityModel.Know(state, data.neighbourhood,
                    CityModel.StartIncome(data.workingClassPercentage, data.middleClassPercentage, data.upperClassClassPercentage));
                known++;
            }
            return known;
        }

        /// <summary>A district's name as the game writes it.</summary>
        public static string Name(string district)
        {
            return string.IsNullOrEmpty(district) ? string.Empty : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        /// <summary>Finds each product's kind and the goods with a wholesale price, noting each one's own price once. Returns how many.</summary>
        public static int FindGoods()
        {
            Goods.Clear();
            Priced.Clear();
            PricedKind.Clear();
            PricedOwn.Clear();
            Array.Clear(KindCount, 0, KindCount.Length);
            var rank = new Dictionary<string, int>(512);   // the lowest place in the catalogue's list of a type that sells the item
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

        public static int CountOf(int kind)
        {
            return kind >= 0 && kind < KindCount.Length ? KindCount[kind] : 0;
        }

        /// <summary>Writes today's wholesale prices where they differ, so one the game has put back is found the same day. Returns how many.</summary>
        public static int ApplyWholesale(CityState state)
        {
            int written = 0;
            for (int k = 0; k < Factors.Length; k++)
            {
                Factors[k] = CityModel.Wholesale(state, k);
            }
            Func<string, double> exchange = CityBridges.MaterialFactor;
            for (int i = 0; i < Priced.Count; i++)
            {
                Item item = Priced[i];
                if (item == null)
                {
                    continue;   // unloaded by the game
                }
                float want = Want(i, Factors[PricedKind[i]], exchange);
                if (item.wholesalePrice != want)
                {
                    item.wholesalePrice = want;
                    written++;
                }
            }
            return written;
        }

        // A good's price today: a raw material at the exchange's price when it prices it, any other at its kind's factor.
        private static float Want(int i, int factor, Func<string, double> exchange)
        {
            double material = exchange != null && Priced[i] != null ? exchange(Priced[i].itemName) : double.NaN;
            if (material > 0.0 && !double.IsInfinity(material))
            {
                return (float)(PricedOwn[i] * material);
            }
            return factor == 1000 ? PricedOwn[i] : PricedOwn[i] * (factor / 1000f);
        }

        /// <summary>An item's own wholesale price as first seen, before any of the mod's; -1 for an item the city has not read.</summary>
        public static float OwnPrice(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || !Goods.TryGetValue(itemName, out Good good) || good.Item == null)
            {
                return -1f;
            }
            return Own.TryGetValue(good.Item, out float own) ? own : good.Item.wholesalePrice;
        }

        /// <summary>Puts the own wholesale price back on every item ever seen, of any city. Safe at any time. Returns how many.</summary>
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
                Own.Remove(dead[i]);   // unloaded by the game: loaded again, it has its own price
            }
            return restored;
        }

        /// <summary>True if every good's wholesale price is what the state says (with no state, the game's own); the detail names the first few that are not.</summary>
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
                float want = state != null ? Want(i, CityModel.Wholesale(state, PricedKind[i]), CityBridges.MaterialFactor) : PricedOwn[i];
                if (item.wholesalePrice != want)
                {
                    if (wrong++ < 3)
                    {
                        text.Append(wrong > 1 ? "; " : string.Empty).Append(item.itemName).Append(" is ").Append(item.wholesalePrice.ToString("0.###", CultureInfo.InvariantCulture))
                            .Append(", expected ").Append(want.ToString("0.###", CultureInfo.InvariantCulture));
                    }
                }
            }
            detail = Priced.Count + " good(s) with a wholesale price, " + wrong + " not as expected" + (wrong > 0 ? " (" + text + ")" : string.Empty);
            return wrong == 0;
        }

        /// <summary>The city has gone: the lists are forgotten; each good's own price is kept for the next city.</summary>
        public static void Forget()
        {
            Goods.Clear();
            Priced.Clear();
            PricedKind.Clear();
            PricedOwn.Clear();
            Monopolies.Clear();
            MonopolyPairs.Clear();
        }

        /// <summary>The player's businesses as they stand today, noting in <see cref="CityState.Buys"/> the kinds of goods they stock.</summary>
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
                string district = GameRegistrations.District(reg);
                shop.Reg = reg;
                shop.District = state.District(district);
                shop.Cycle = cycle;
                shop.Area = CityModel.DistrictFootfall(shop.District);
                int ratio = PriceRatio(reg, district, ref buys);
                shop.Against = ratio > 0
                    ? CityModel.Against(ratio, CityModel.WithBears(CityModel.Tolerance(state, shop.District, level), BearsAt(reg)))
                    : 0;
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

        // The one seller of a product in a neighbourhood may charge more (ItemHelper.CalculateMaxAcceptablePrice). Kept as
        // pairs, so no text is made per price line; should a name hold '@', matched as "item@district" texts, as a text key is.
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

        // A business's prices against the game's own for its neighbourhood (default market price times the neighbourhood's
        // price index, with the monopoly allowance), the mean over its price list in thousandths; 0 with nothing to judge by.
        // Its goods are noted in 'buys' whether or not its prices can be judged.
        private static int PriceRatio(BuildingRegistration reg, string district, ref int buys)
        {
            List<RetailPrice> prices = reg.retailPrices;
            if (prices == null)
            {
                return 0;
            }
            float index = 0f;
            bool judged = !string.IsNullOrEmpty(district) && CitizenHelper.averagePriceIndicesInNeighborhoods.TryGetValue(district, out index) && index > 0f;
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
                if (!judged || price.price <= 0f || good.Market <= 0f)
                {
                    continue;
                }
                sum += CityModel.PriceRatioTerm(price.price, good.Market, index, Monopoly(price.itemName, district));
                count++;
            }
            return CityModel.MeanPriceRatio(sum, count);
        }

        /// <summary>The game's own rent, wages, takings and shops and services of a day (its daily run dates them on the day that has ended).</summary>
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
                // The mod's own costs are at today's prices already.
                if (GameBooks.Ours(t))
                {
                    if (t.amount > 0f && givenBack != null && givenBack(t.transactionType))
                    {
                        books.Living.Add(null, Kept(t), -t.amount);   // a game charge the mod paid back, deducted under nothing
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
                        books.Rent.Add(t.address, RentCategory, -t.amount, Deducted(t));
                    }
                    else if (t.transactionCategories != null && t.transactionCategories.Contains(SalaryCategory))
                    {
                        books.Wages.Add(t.address, SalaryCategory, -t.amount, Deducted(t));
                    }
                    else if (LivingTypes.Contains(t.transactionType) && !AtWholesale(t))
                    {
                        // A business's marketing and licences go in its statement by address; parking, health insurance and
                        // the headhunter by their category alone; the rest is the player's.
                        string category = Kept(t);
                        Address at = category == GameBooks.Marketing || category == GameBooks.Licensing ? t.address : null;
                        books.Living.Add(at, category, -t.amount, Deducted(t));
                    }
                }
            }
            return books;
        }

        // The name the game deducts a charge under (TaxHelper.TrackTransaction); empty if it is marked deductible without one, null if not deductible.
        private static string Deducted(Transaction t)
        {
            if (!t.isTaxDeductible)
            {
                return null;
            }
            string name;
            return t.transactionData != null && t.transactionData.TryGetValue("taxDeductibleName", out name) && name != null ? name : string.Empty;
        }

        // The category a charge is kept under where the day's statement has a line for it; null for none.
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

        // Bought at a wholesaler's, so at today's prices already.
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
        public static int Cash => SaveGameManager.Current != null ? WholeMoney.Floor(SaveGameManager.Current.Money) : 0;

        /// <summary>
        /// One adjustment, dated on the day it is for: positive is paid, negative taken. A charge is deducted as the charges
        /// it adjusts were (<paramref name="deducted"/> not null); a refund lowers their deduction. False, nothing taken, for
        /// a charge the cash does not cover.
        /// </summary>
        public static bool Book(int amount, string key, int day, string percent, Address address = null, string category = null, string deducted = null)
        {
            if (amount == 0 || SaveGameManager.Current == null)
            {
                return amount == 0;
            }
            // A dictionary each: the game keeps a transaction's data for as long as it lists the transaction.
            var data = new Dictionary<string, string>(2) { { "percent", percent } };
            var info = category != null ? new TransactionInfo(key, category, data) : new TransactionInfo(key, data);
            if (deducted != null && amount < 0)
            {
                info.SetTaxDeductibleName(key);
            }
            bool booked = GameManager.ChangeMoneySafe(amount, info, day, address, amount > 0);
            if (booked && amount > 0 && !string.IsNullOrEmpty(deducted))
            {
                LowerDeduction(deducted, amount);   // the game counts charges alone (TaxHelper.TrackTransaction)
            }
            return booked;
        }

        // The tax period's deduction kept under a name, lowered by a refund of the charges it was made for.
        private static void LowerDeduction(string name, float amount)
        {
            List<TaxDeductibleExpense> kept = SaveGameManager.Current.currentTaxPeriodDeductibleExpenses;
            float left = amount;
            for (int i = 0; kept != null && i < kept.Count && left > 0f; i++)
            {
                TaxDeductibleExpense e = kept[i];
                if (e != null && e.key == name)
                {
                    e.amount = CityAdjust.Lowered(e.amount, ref left);
                }
            }
        }

        /// <summary>What the game says a building the player owns is worth (Building.GetMarketValue); 0 if it cannot be read.</summary>
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
