using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using RealLifeOverhaul.Economy;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>
    /// The one file of suppliers and contracts that touches the game, and the world the engine works in (<see cref="ISupplierWorld"/>): the clock, the player's cash
    /// through the mod's one booking call, the game's wholesale prices and market events, the player's own places with what they sell, goods put on shelves and into
    /// storage by the game's own delivery call, the city's wholesale stores and importers a depot stands by, the city's rival shops as the game values them, and the
    /// notices. Only places Going Public calls the player's own are ever read or delivered to. Nothing per frame.
    /// </summary>
    public sealed class SupplierGame : ISupplierWorld
    {
        private const string WholesaleType = "ba:businesstype_wholesalestore", ImportType = "ba:businesstype_importexport";

        public static IModLogger Logger;

        private static readonly Dictionary<string, Vector3> Spots = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private static readonly List<string> Names = new List<string>(32);
        private static readonly Dictionary<string, int> Sold = new Dictionary<string, int>(32, StringComparer.Ordinal);

        private readonly SupplierState _state;

        public SupplierGame(SupplierState state)
        {
            _state = state;
        }

        private static GameInstance Save => SaveGameManager.Current;

        public static bool Ready => Save != null && Save.BuildingRegistrations != null;

        public static int Today => Save != null ? Save.Day : 0;

        public static int HourNow => Save != null ? Mathf.Clamp(Save.Hour, 0, 23) : 0;

        // ---- the clock, the cash, the customer's standing

        public int Day => Today;

        public int Hour => HourNow;

        public int Month => Today / Math.Max(1, MacroCatalogue.MonthDays(GameCalendar.DaysPerYear));

        public long Cash => CashNow;

        /// <summary>The player's cash in cents, never under nothing.</summary>
        public static long CashNow => Save != null ? (long)Math.Floor(Mathf.Clamp(Save.Money, 0f, 1e12f) * 100.0) : 0L;

        public int Level => Prices.Level;

        public int Credit
        {
            get
            {
                Func<int> credit = SupplierBridges.Credit;
                return credit != null ? credit() : 0;
            }
        }

        // ---- the market (the city's economy; calm, with no shortage, while it is off)

        public int Cycle => (int)CityEconomy.State;

        public int Factor(int kind) => CityEconomy.SupplyFactor(kind);

        public int ShortageOn(int kind, int day)
        {
            int ahead = day - Today;
            return CityEconomy.SupplyShockOf(kind, out int startsIn, out int days, out int size) == SupplyShock.Shortage && ahead >= startsIn && ahead < startsIn + days ? size : 0;
        }

        public void ShortAhead(int kind, out int from, out int days, out int size)
        {
            if (CityEconomy.SupplyShockOf(kind, out from, out days, out size) != SupplyShock.Shortage)
            {
                from = days = size = 0;
            }
        }

        public CityDay Conditions(int day)
        {
            Func<int, CityDay> conditions = SupplierBridges.Day;
            return conditions != null ? conditions(day) : new CityDay { Season = 1 };
        }

        // ---- the goods

        /// <summary>The kind of goods a product is, or -1 for what no supplier of the mod's trades (a service, a raw material of the exchange's).</summary>
        public static int KindOf(string item)
        {
            Func<string, int> kind = SupplierBridges.KindOf;
            return kind != null && !string.IsNullOrEmpty(item) ? kind(item) : -1;
        }

        private static long Mills(float price) => price > 0f && price < 1e9f ? (long)Math.Round(price * 1000.0) : 0L;

        public long Base(string item) => BaseOf(item);

        public long Market(string item) => MarketOf(item);

        public static long BaseOf(string item)
        {
            Item it = ItemsGetter.GetByName(item, true);
            return it != null ? Mills(it.GetWholesalePrice()) : 0L;
        }

        /// <summary>The market's price of the day, as the game's importers charge it: the wholesale price and the whole market's event on it (a shortage, a backorder).</summary>
        public static long MarketOf(string item)
        {
            Item it = ItemsGetter.GetByName(item, true);
            return it != null ? Mills(it.GetWholesalePrice() * ProductMarketHelper.GetProductMarketEventMultiplier(item)) : 0L;
        }

        public long Export(string item)
        {
            return ItemsGetter.GetByName(item, true) != null ? Mills(ProductMarketHelper.GetProductExportPrice(item)) : 0L;
        }

        public bool Backorder(string item) => ProductMarketHelper.IsProductInMarketEvent(item, MarketEventType.ProductBackorder);

        public int Most(string item)
        {
            Item it = ItemsGetter.GetByName(item, true);
            return it == null || DeliveryHelper.AreWholesaleAndImportLimitsDisabled() ? 0 : Math.Max(0, it.maxWholesaleOrderAmount);
        }

        public int Box(string item)
        {
            Item it = ItemsGetter.GetByName(item, true);
            return it != null ? Math.Max(1, it.boxSize) : 1;
        }

        public static string ItemName(string item)
        {
            return string.IsNullOrEmpty(item) ? string.Empty : LocalizationHelper.GetItemLabel(item).ToString();
        }

        // ---- the places

        /// <summary>A place's key: its street (escaped) and number, as the company's files name it.</summary>
        public static string Key(Address a) => a == null ? string.Empty
            : Uri.EscapeDataString(a.streetName ?? string.Empty) + "~" + a.streetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static BuildingRegistration Reg(string key)
        {
            int at = key != null ? key.LastIndexOf('~') : -1;
            if (at <= 0 || !Ready || !int.TryParse(key.Substring(at + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int number))
            {
                return null;
            }
            return BuildingHelper.GetBuildingRegistration(new Address(Uri.UnescapeDataString(key.Substring(0, at)), number));
        }

        public static string NameOf(BuildingRegistration reg)
        {
            return reg == null ? string.Empty : string.IsNullOrEmpty(reg.BusinessName) ? reg.Address.ToFormattedString() : reg.BusinessName;
        }

        private static bool IsWarehouse(BuildingRegistration reg) => reg is Warehouse;

        // The player's own, by Going Public's word too, and in business: a warehouse, or a business of a type the game knows.
        private static bool Own(BuildingRegistration reg)
        {
            return reg != null && reg.RentedByPlayer && reg.Address != null && reg.itemInstances != null && GoingPublic.Mine(reg)
                && (IsWarehouse(reg) || (!string.IsNullOrEmpty(reg.businessTypeName) && BusinessTypeHelper.GetData(reg) != null));
        }

        public bool PlaceOpen(string place) => Own(Reg(place));

        /// <summary>The player's places that sell or hold something a supplier could bring, by name.</summary>
        public static void Places(List<SupplyPlaceFact> into, List<ContractLine> scratch)
        {
            into.Clear();
            List<BuildingRegistration> all = Ready ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (!Own(reg))
                {
                    continue;
                }
                WantsOf(reg, scratch);
                if (scratch.Count > 0)
                {
                    into.Add(new SupplyPlaceFact { Key = Key(reg.Address), Name = NameOf(reg), Buyer = BuyerAt(reg) });
                }
            }
            scratch.Clear();
            into.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name) != 0 ? string.CompareOrdinal(a.Name, b.Name) : string.CompareOrdinal(a.Key, b.Key));
        }

        public void Wants(string place, List<ContractLine> into)
        {
            WantsOf(Reg(place), into);
        }

        /// <summary>
        /// What a place sells and how much of it in a week. A shop: the goods of its business type that it stocks or sold, each with the units its tills sold in the
        /// last seven days (the game's order history), a box where it has sold none yet. A warehouse: the goods the purchasing agents' plans bring it, at their amounts.
        /// In the order of the products' names, so the same on every machine.
        /// </summary>
        public static void WantsOf(BuildingRegistration reg, List<ContractLine> into)
        {
            into.Clear();
            if (!Own(reg))
            {
                return;
            }
            Sold.Clear();
            Names.Clear();
            if (IsWarehouse(reg))
            {
                List<ImportPartnership> plans = Save.importPartnerships;
                for (int i = 0; plans != null && i < plans.Count; i++)
                {
                    List<ImportProduct> products = plans[i] != null ? plans[i].products : null;
                    for (int k = 0; products != null && k < products.Count; k++)
                    {
                        ImportProduct p = products[k];
                        if (p != null && p.amount > 0 && p.assignedWarehouse == reg.Address && !string.IsNullOrEmpty(p.itemName))
                        {
                            Sold[p.itemName] = Sold.TryGetValue(p.itemName, out int n) ? Math.Max(n, p.amount) : p.amount;
                        }
                    }
                }
            }
            else
            {
                BusinessType type = BusinessTypeHelper.GetData(reg);
                int from = Today - SupplierCatalogue.WeekDays;
                List<OrderHistoryEntry> history = reg.orderHistory;
                for (int i = 0; history != null && i < history.Count; i++)
                {
                    OrderHistoryEntry day = history[i];
                    if (day == null || day.dayNumber <= from || day.itemSales == null)
                    {
                        continue;
                    }
                    for (int k = 0; k < day.itemSales.Count; k++)
                    {
                        OrderHistoryEntry.ItemReport sale = day.itemSales[k];
                        if (sale != null && sale.amountSold > 0 && !string.IsNullOrEmpty(sale.itemName))
                        {
                            Sold[sale.itemName] = (Sold.TryGetValue(sale.itemName, out int n) ? n : 0) + sale.amountSold;
                        }
                    }
                }
                for (int i = 0; type != null && type.businessProducts != null && i < type.businessProducts.Length; i++)
                {
                    string item = type.businessProducts[i] != null ? type.businessProducts[i].itemName : null;
                    if (!string.IsNullOrEmpty(item) && !Sold.ContainsKey(item) && BuildingHelper.CountTotalResourcesInStock(reg, item) > 0)
                    {
                        Sold[item] = 1;   // stocked and none sold yet: a box, the least a supplier brings
                    }
                }
            }
            foreach (KeyValuePair<string, int> pair in Sold)
            {
                Names.Add(pair.Key);
            }
            Names.Sort(StringComparer.Ordinal);
            for (int i = 0; i < Names.Count; i++)
            {
                int kind = KindOf(Names[i]);
                if (kind >= 0 && BaseOf(Names[i]) > 0)
                {
                    into.Add(new ContractLine { Item = Names[i], Kind = kind, Units = Sold[Names[i]] });
                }
            }
            Sold.Clear();
            Names.Clear();
        }

        private static readonly Func<ItemInstance, bool> IsPallet = x => x != null && x.ItemCached != null && x.ItemCached.HasTag(TagRef.Itemtag.iswarehousestorage);
        private static readonly Func<ItemInstance, bool> IsStorage = x => x != null && x.ItemCached != null && x.ItemCached.HasTag(TagRef.Itemtag.isbusinessstorage);

        /// <summary>
        /// Goods put at a place by the game's own delivery call, as its wholesale contract brings them: a shop's shelves that show the product first, then its storage;
        /// a warehouse's pallets. Returns how many found room; the rest never enters the game.
        /// </summary>
        public int Deliver(string place, string item, int units, long mills)
        {
            BuildingRegistration reg = Reg(place);
            if (!Own(reg) || units <= 0 || ItemsGetter.GetByName(item, true) == null)
            {
                return 0;
            }
            var cargo = new CargoInstance(item, units, mills / 1000f);
            if (IsWarehouse(reg))
            {
                ItemHelper.DeliverCargoToBuilding(cargo, reg, IsPallet);
            }
            else
            {
                ItemHelper.DeliverCargoToBuilding(cargo, reg, x => Shows(x, item));
                if (cargo.amount > 0)
                {
                    ItemHelper.DeliverCargoToBuilding(cargo, reg, IsStorage);
                }
            }
            int took = units - Math.Max(0, cargo.amount);
            if (took > 0)
            {
                GameEvent.Invoke("ba:gameevent_itemcargochanged");
                if (!IsWarehouse(reg))
                {
                    BusinessHelper.FillUpEmptyShowcaseShelvesAndPointsOfSales(reg.Address);
                }
                if (InstanceBehavior<GameManager>.Instance != null)
                {
                    InstanceBehavior<GameManager>.Instance.shouldUpdateAfterDeliveries = true;
                }
            }
            return took;
        }

        // A shelf or a point of sale that shows the product now (BusinessHelper.DeliverWholesaleContract's own filter).
        private static bool Shows(ItemInstance x, string item)
        {
            if (x == null || x.ItemCached == null || (x.ItemCached.type & (ItemType.PointOfSale | ItemType.ShowcaseShelf)) == 0)
            {
                return false;
            }
            CargoInstance stock = x.GetStockInstance();
            return stock != null && stock.itemName == item;
        }

        // ---- the money: only through the mod's one booking call, at the place it is for

        public bool Pay(long cents, string key, string place, int supplier)
        {
            if (cents <= 0)
            {
                return true;
            }
            if (cents > CashNow || cents > int.MaxValue / 2)
            {
                return false;
            }
            return BizBooks.Book(-(cents / 100f), Info(key, place, supplier, true), null, AddressOf(place), force: false, showNotification: false);
        }

        public void Receive(long cents, string key, string place, int supplier)
        {
            if (cents > 0)
            {
                BizBooks.Book(cents / 100f, Info(key, place, supplier, false), null, AddressOf(place), force: true);
            }
        }

        private static Address AddressOf(string place)
        {
            BuildingRegistration reg = Reg(place);
            return reg != null && reg.RentedByPlayer ? reg.Address : null;
        }

        // A purchase from a supplier is a cost of stock, deducted from the game's tax under the supplier's name as the game deducts a wholesale store's by its own.
        private static TransactionInfo Info(string key, string place, int supplier, bool deducted)
        {
            string name = SupplierText.Firm(supplier);
            var info = new TransactionInfo(key, new Dictionary<string, string>(2) { { "supplier", name }, { "businessName", NameOf(Reg(place)) } });
            if (deducted && name.Length > 0)
            {
                info.SetTaxDeductibleName(name);
            }
            return info;
        }

        // ---- who decides

        public bool Buyer(string place) => BuyerAt(Reg(place));

        /// <summary>
        /// True where somebody who orders stock decides at a place today: a manager of the mod's with stock orders on (Economy/Company), or for a warehouse a purchasing
        /// agent of the game's whose plan brings goods there and who is at work.
        /// </summary>
        public static bool BuyerAt(BuildingRegistration reg)
        {
            if (!Own(reg))
            {
                return false;
            }
            Func<BuildingRegistration, bool> manager = SupplierBridges.Manager;
            if (manager != null && manager(reg))
            {
                return true;
            }
            List<ImportPartnership> plans = IsWarehouse(reg) ? Save.importPartnerships : null;
            for (int i = 0; plans != null && i < plans.Count; i++)
            {
                ImportPartnership plan = plans[i];
                if (plan == null || string.IsNullOrEmpty(plan.employeeInstanceId) || plan.products == null)
                {
                    continue;
                }
                EmployeeInstance agent = plan.PurchasingAgentInstance;
                if (agent == null || agent.isBeingReplaced)
                {
                    continue;
                }
                for (int k = 0; k < plan.products.Count; k++)
                {
                    if (plan.products[k] != null && plan.products[k].assignedWarehouse == reg.Address)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // ---- the depots: each supplier's stands by one of the city's own wholesale stores or importers

        /// <summary>The city's wholesale stores and importers, by key, in the keys' order.</summary>
        public static void Supplies(List<string> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Ready ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r != null && r.Address != null && (r.businessTypeName == WholesaleType || r.businessTypeName == ImportType))
                {
                    into.Add(Key(r.Address));
                }
            }
            into.Sort(StringComparer.Ordinal);
        }

        /// <summary>The district a depot stands in, in the game's words; empty when the city shows none.</summary>
        public static string District(string depot)
        {
            BuildingRegistration reg = Reg(depot);
            string district = reg != null ? reg.Neighborhood : null;
            return string.IsNullOrEmpty(district) ? string.Empty : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        private static bool Spot(string key, out Vector3 at)
        {
            if (Spots.TryGetValue(key, out at))
            {
                return true;
            }
            BuildingRegistration reg = Reg(key);
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController c = city != null && reg != null && reg.Address != null && !reg.Address.IsUndefined() ? city.FindCityBuildingController(reg.Address) : null;
            if (c == null)
            {
                return false;
            }
            at = c.transform.position;
            Spots[key] = at;
            return true;
        }

        public int FarDays(int supplier, string place)
        {
            FirmState f = _state.Firm(supplier);
            if (f == null || string.IsNullOrEmpty(f.Depot) || !Spot(f.Depot, out Vector3 from) || !Spot(place, out Vector3 to))
            {
                return 0;
            }
            float x = from.x - to.x, z = from.z - to.z;
            return x * x + z * z > (float)SupplierCatalogue.FarMetres * SupplierCatalogue.FarMetres ? 1 : 0;
        }

        /// <summary>Forgets where the buildings stand: another city may be loaded next.</summary>
        public static void Forget() => Spots.Clear();

        // ---- the rivals

        public void OwnerGain(int tycoon, long cents)
        {
            Action<int, long> move = SupplierBridges.RivalMove;
            if (move != null && cents >= 100 && !GoingPublic.InSession)
            {
                move(tycoon, cents / 100);
            }
        }

        public void RivalMoved(int tycoon, long cents)
        {
            Action<int, long> move = SupplierBridges.RivalMove;
            if (move != null && cents / 100 != 0)
            {
                move(tycoon, -(cents / 100));
            }
        }

        /// <summary>The city's rival shops that sell goods, as the game holds them.</summary>
        public static void RivalShops(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Ready ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r != null && r.Address != null && !string.IsNullOrEmpty(r.businessOwnerRivalId) && !r.RentedByPlayer && r.dailyIncomes != null && r.dailyIncomes.Count > 0
                    && KindOfShop(r.businessTypeName) >= 0)
                {
                    into.Add(r);
                }
            }
        }

        /// <summary>The kind of goods a business type sells, by the city's own list; -1 for one that sells no goods a supplier brings.</summary>
        public static int KindOfShop(string businessTypeName)
        {
            if (string.IsNullOrEmpty(businessTypeName) || businessTypeName == WholesaleType || businessTypeName == ImportType)
            {
                return -1;
            }
            int rank = MacroCatalogue.TypeRank(businessTypeName);
            if (rank >= 0)
            {
                return MacroCatalogue.KindOfTypeIs[rank];
            }
            BusinessType type = BusinessTypeHelper.GetData(businessTypeName);
            for (int i = 0; type != null && type.businessProducts != null && i < type.businessProducts.Length; i++)
            {
                if (type.businessProducts[i] != null && KindOf(type.businessProducts[i].itemName) >= 0)
                {
                    return MacroCatalogue.General;
                }
            }
            return -1;
        }

        /// <summary>Whether the game is still valuing the city's shops for the day, so their day is not read half made.</summary>
        public static bool Valuing => CompetitionHelper.AreRecalculationsPending();

        /// <summary>A rival shop's last day as the game valued it, cents.</summary>
        public static int LastDay(BuildingRegistration r)
        {
            return r != null && r.dailyIncomes != null && r.dailyIncomes.Count > 0 ? Mathf.RoundToInt(Mathf.Clamp(r.dailyIncomes[r.dailyIncomes.Count - 1], -1e7f, 1e7f) * 100f) : 0;
        }

        /// <summary>The supply side's cents come off (or, a saving, go on) a rival shop's last valued day, which its rival's income, worth and closing are read from.</summary>
        public static void Charge(BuildingRegistration r, int cents)
        {
            if (r != null && r.dailyIncomes != null && r.dailyIncomes.Count > 0 && cents != 0)
            {
                r.dailyIncomes[r.dailyIncomes.Count - 1] -= cents / 100f;
            }
        }

        // ---- words

        public static string Money(long cents) => (cents / 100f).ToCurrencyFormat();

        public static string Price(long mills) => (mills / 1000f).ToCurrencyFormat();

        public static void Tell(NotificationType type, string key, Dictionary<string, string> data, Action click = null)
        {
            Notifications.Show(type, key, data, 10f, null, click != null ? Guard.Wrap(click) : null, true, false);
        }

        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var d = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                d[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return d;
        }
    }
}
