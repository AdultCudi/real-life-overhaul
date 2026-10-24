using System;
using System.Collections.Generic;
using AI.Citizens;
using BigAmbitions.Items;
using Entities;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What the online shops ask of the rest of the mod, filled in by Wiring/OnlineLinks. Each is optional.</summary>
    public static class OnlineBridges
    {
        /// <summary>The customers, in per cent, that one of the company's campaigns adds at a business today. Default: none.</summary>
        public static Func<BuildingRegistration, int> Campaign;

        /// <summary>The season of a game day: 0 winter, 1 spring, 2 summer, 3 autumn. Default: spring.</summary>
        public static Func<int, int> Season;

        /// <summary>Metres of road between two places, measured a slice a call: -1 while still looking, 0 with no way. Default: 0, the guess stays.</summary>
        public static Func<Vector3, Vector3, int> Road;

        /// <summary>Told of every deductible cost in cents as it is paid. Default: nobody (the game's own tax still deducts it).</summary>
        public static Action<int> Cost;

        public static void Reset()
        {
            Campaign = null;
            Season = null;
            Road = null;
            Cost = null;
        }
    }

    /// <summary>One online shop with what was read from the game for it. Not saved.</summary>
    public sealed class WebPlace
    {
        public WebShop Shop;
        public BuildingRegistration Reg;
        public Address Address;
        public string Business = string.Empty;  // the business's own name
        public bool Warehouse;
        public readonly Site Site = new Site();
        public Vector3 At;                      // where the building stands
        public bool HasAt;
        public int Pickers;                     // staff picking in the last hour worked through
        public int Measuring;                   // the next district whose road is to be measured
    }

    /// <summary>
    /// Everything the online shops read from or do to the game (stock in OnlineGame.Stock, staff and vans in
    /// OnlineGame.Staff). Runs on the game's hour, once a day and when the app opens; never per frame.
    /// </summary>
    internal static partial class OnlineGame
    {
        /// <summary>Owner deduction labels whose mixed costs have no single portable recurrence rule.</summary>
        internal static string[] CompanionUnclassifiedTaxLabels() => new[] { DeductKey.GetLocalization() };

        public const string TakingsKey = "reallife_transaction_online_takings";
        public const string RefundsKey = "reallife_transaction_online_refunds";
        public const string CourierKey = "reallife_transaction_online_courier";
        public const string VansKey = "reallife_transaction_online_vans";
        public const string HostingKey = "reallife_transaction_online_hosting";
        public const string SetUpKey = "reallife_transaction_online_setup";
        public const string AdsKey = "reallife_transaction_online_ads";

        private const string DeductKey = "reallife_online_deduct";
        private const string WarehouseType = "ba:businesstype_warehouse";
        private const string AgencyType = "ba:businesstype_webdevelopmentagency";
        private const string Global = "ba:neighborhood_global";
        private const int NoDemand = 50;            // what the game answers for a product it keeps no demand for (ProductMarketHelper.GetNeighborhoodDemand)
        private const int FarMetres = 2500;         // to a district with no building to measure to

        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);
        private static readonly List<int> Counts = new List<int>(16);
        private static readonly List<WebPlace> Before = new List<WebPlace>(OnlineCatalogue.ShopsMost);

        private static GameInstance Save => SaveGameManager.Current;

        public static int DaysPerYear
        {
            get
            {
                int days = GameCalendar.DaysPerYear;
                return days >= 8 ? days : 60;
            }
        }

        /// <summary>A business by its address, written as the company writes a place.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool IsWarehouse(BuildingRegistration reg)
        {
            return reg is Warehouse && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.businessTypeName == WarehouseType;
        }

        /// <summary>The businesses that take money and the warehouses the player runs. One pass over the save's buildings.</summary>
        public static void Candidates(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && (IsWarehouse(reg) || Books.IsBusiness(reg)))
                {
                    into.Add(reg);
                }
            }
        }

        public static BuildingRegistration Find(string place)
        {
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && string.Equals(Place(reg), place, StringComparison.Ordinal) && (IsWarehouse(reg) || Books.IsBusiness(reg)))
                {
                    return reg;
                }
            }
            return null;
        }

        public static string NameOf(BuildingRegistration reg)
        {
            return reg == null ? "?" : !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : reg.GetDisplayName();
        }

        public static string TypeOf(BuildingRegistration reg)
        {
            string type = reg != null ? reg.businessTypeName : null;
            return string.IsNullOrEmpty(type) ? string.Empty : LocalizorManager.IsLocalizedKey(type) ? type.GetLocalization() : type;
        }

        /// <summary>True if the player runs a web development agency, one of the game's own office kinds.</summary>
        public static bool HasAgency()
        {
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].businessTypeName == AgencyType && Books.IsBusiness(all[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Ties each site to its building again; one whose business is no longer the player's is marked unseen. Measured roads stay while the building does.</summary>
        public static void ReadPlaces(OnlineState state, List<WebPlace> into, List<WebPlace> pool)
        {
            Before.Clear();
            Before.AddRange(into);
            into.Clear();
            for (int i = 0; i < state.Shops.Count; i++)
            {
                WebShop shop = state.Shops[i];
                BuildingRegistration reg = Find(shop.Place);
                shop.Seen = reg != null;
                WebPlace place = null;
                for (int b = 0; b < Before.Count && place == null; b++)
                {
                    if (Before[b] != null && ReferenceEquals(Before[b].Shop, shop))
                    {
                        place = Before[b];
                        Before[b] = null;
                    }
                }
                bool known = place != null;
                if (!known && pool.Count > 0)
                {
                    place = pool[pool.Count - 1];
                    pool.RemoveAt(pool.Count - 1);
                }
                place = place ?? new WebPlace();
                Vector3 stood = place.At;
                bool had = known && place.HasAt;
                place.Shop = shop;
                place.Reg = reg;
                place.Warehouse = reg != null && IsWarehouse(reg);
                place.Business = NameOf(reg);
                place.HasAt = false;
                try
                {
                    place.Address = reg != null ? reg.Address : null;
                    CityBuildingController building = place.Address != null && InstanceBehavior<CityManager>.Instance != null
                        ? InstanceBehavior<CityManager>.Instance.FindCityBuildingController(place.Address)
                        : null;
                    if (building != null)
                    {
                        place.At = building.transform.position;
                        place.HasAt = true;
                    }
                }
                catch (Exception)
                {
                    place.Address = null;   // a building that cannot be read stands nowhere: its distances stay the guess
                }
                if (!known || had != place.HasAt || (had && (stood - place.At).sqrMagnitude > 1f))
                {
                    place.Pickers = 0;
                    place.Measuring = 0;
                    for (int d = 0; d < place.Site.Measured.Length; d++)
                    {
                        place.Site.Measured[d] = false;
                    }
                }
                into.Add(place);
            }
            for (int b = 0; b < Before.Count; b++)
            {
                if (Before[b] != null)
                {
                    Before[b].Reg = null;
                    Before[b].Shop = null;
                    pool.Add(Before[b]);
                }
            }
            Before.Clear();
        }

        /// <summary>Adds the city's districts not yet known (orders keep the index) and finds each one's middle. One pass over the city's buildings.</summary>
        public static void Districts(OnlineState state, List<Vector3> centres, List<bool> has)
        {
            List<string> names = NeighborhoodHelper.Neighborhoods;
            for (int i = 0; names != null && i < names.Count; i++)
            {
                if (!string.IsNullOrEmpty(names[i]) && names[i] != Global && state.District(names[i]) < 0 && state.Districts.Count < 64)
                {
                    state.Districts.Add(names[i]);
                }
            }
            centres.Clear();
            has.Clear();
            Counts.Clear();
            for (int i = 0; i < state.Districts.Count; i++)
            {
                centres.Add(Vector3.zero);
                has.Add(false);
                Counts.Add(0);
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            for (int i = 0; buildings != null && i < buildings.Length; i++)
            {
                CityBuildingController b = buildings[i];
                int d = b != null && b.building != null ? state.District(b.building.Neighbourhood) : -1;
                if (d >= 0)
                {
                    centres[d] += b.transform.position;
                    Counts[d]++;
                }
            }
            for (int i = 0; i < centres.Count; i++)
            {
                if (Counts[i] > 0)
                {
                    centres[i] /= Counts[i];
                    has[i] = true;
                }
            }
        }

        /// <summary>A straight line's guess at the road to each district not yet measured along the roads.</summary>
        public static void GuessRoads(WebPlace place, List<Vector3> centres, List<bool> has)
        {
            Site site = place.Site;
            for (int d = 0; d < centres.Count && d < site.Metres.Length; d++)
            {
                if (site.Measured[d])
                {
                    continue;
                }
                if (!place.HasAt || !has[d])
                {
                    site.Metres[d] = FarMetres;
                    continue;
                }
                Vector3 to = centres[d] - place.At;
                to.y = 0f;
                site.Metres[d] = Mathf.RoundToInt(to.magnitude * OnlineCatalogue.RoadFactor / 1000f);
            }
        }

        /// <summary>Reads each product's online and going price and the game's demand for it by district. One pass over the market a product.</summary>
        public static void ReadSite(WebPlace place, OnlineState state)
        {
            WebShop shop = place.Shop;
            Site site = place.Site;
            int districts = state.Districts.Count, lines = Math.Min(shop.Lines.Count, OnlineCatalogue.LinesMost);
            site.Size(lines, districts);
            int rank = place.Reg != null && !place.Warehouse ? MacroCatalogue.TypeRank(place.Reg.businessTypeName) : -1;
            site.Kind = rank >= 0 ? MacroCatalogue.KindOfTypeIs[rank] : MacroCatalogue.General;
            float index = PriceIndex(state);
            float level = Prices.Level / 1000f;   // the game's prices are the start's; the going price is raised to today's as the shop's is
            List<ProductMarketEntry> market = Save != null ? Save.productMarketEntries : null;
            for (int l = 0; l < lines; l++)
            {
                WebLine line = shop.Lines[l];
                Item item = ItemsGetter.GetByName(line.Item, true);
                int own = item != null && place.Reg != null ? OnlineRules.ShopCents(ItemHelper.GetPrice(line.Item, place.Reg), Prices.Level) : 0;
                site.Ask[l] = line.Price > 0 ? line.Price : own;
                site.Going[l] = item != null ? Mathf.Max(1, Mathf.RoundToInt(item.DefaultMarketPrice * index * level * 100f)) : 0;
                ProductMarketEntry entry = null;
                for (int m = 0; market != null && m < market.Count && entry == null; m++)
                {
                    entry = market[m] != null && market[m].itemName == line.Item ? market[m] : null;
                }
                for (int d = 0; d < districts; d++)
                {
                    site.Demand[l * districts + d] = entry != null && entry.demandValues != null ? 0 : NoDemand;
                }
                for (int v = 0; entry != null && entry.demandValues != null && v < entry.demandValues.Count; v++)
                {
                    NeighborhoodDemand demand = entry.demandValues[v];
                    int d = demand != null ? state.District(demand.neighborhood) : -1;
                    if (d >= 0)
                    {
                        site.Demand[l * districts + d] = Mathf.Clamp(demand.demand, 0, 100);
                    }
                }
            }
        }

        // What the city's customers pay over the game's market price, averaged over the districts (the game's own indices, 1.2 to 1.7).
        private static float PriceIndex(OnlineState state)
        {
            float sum = 0f;
            int count = 0;
            for (int d = 0; d < state.Districts.Count; d++)
            {
                float index;
                if (CitizenHelper.averagePriceIndicesInNeighborhoods.TryGetValue(state.Districts[d], out index) && index > 0f)
                {
                    sum += index;
                    count++;
                }
            }
            return count > 0 ? sum / count : 1.4f;
        }

        /// <summary>The business's own price of a product in cents at today's prices, as the site charges it: its retail price, or the market price where it set none.</summary>
        public static int ShopPrice(WebPlace place, string item)
        {
            return place.Reg != null && ItemsGetter.GetByName(item, true) != null ? OnlineRules.ShopCents(ItemHelper.GetPrice(item, place.Reg), Prices.Level) : 0;
        }

        public static string ItemName(string item)
        {
            return string.IsNullOrEmpty(item) ? "?" : LocalizorManager.IsLocalizedKey(item) ? item.GetLocalization() : item;
        }

        /// <summary>For a test: the business that took most over its counter in the days the game keeps; a warehouse if there is nothing else.</summary>
        public static BuildingRegistration Busiest(OnlineState state)
        {
            Candidates(Regs);
            BuildingRegistration best = null;
            float most = -1f;
            for (int i = 0; i < Regs.Count; i++)
            {
                BuildingRegistration reg = Regs[i];
                if (state.Shop(Place(reg)) != null)
                {
                    continue;
                }
                float took = 0f;
                for (int d = 0; reg.orderHistory != null && d < reg.orderHistory.Count; d++)
                {
                    took += reg.orderHistory[d] != null ? reg.orderHistory[d].totalRevenue : 0f;
                }
                if (took > most)
                {
                    most = took;
                    best = reg;
                }
            }
            Regs.Clear();
            return best;
        }

        /// <summary>Pays cents in at a business as a dated transaction of the mod's own.</summary>
        public static void Give(int cents, string key, WebPlace place, int? day)
        {
            if (cents > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(cents / 100f, Info(key, place), day, place != null ? place.Address : null, true);
            }
        }

        /// <summary>Takes cents at a business, deductible if asked. Unless <paramref name="force"/>, false and nothing taken when the cash falls short.</summary>
        public static bool Take(int cents, string key, WebPlace place, int? day, bool deductible, bool force)
        {
            if (cents <= 0)
            {
                return true;
            }
            if (Save == null)
            {
                return false;
            }
            TransactionInfo info = Info(key, place);
            if (deductible)
            {
                info.SetTaxDeductibleName(DeductKey.GetLocalization());
            }
            bool taken = GameManager.ChangeMoneySafe(-cents / 100f, info, day, place != null ? place.Address : null, force);
            if (taken && deductible && OnlineBridges.Cost != null)
            {
                OnlineBridges.Cost(cents);
            }
            return taken;
        }

        // A dictionary each: the game keeps a transaction's data for as long as it lists the transaction.
        private static TransactionInfo Info(string key, WebPlace place)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", place != null && place.Shop != null ? place.Shop.Name : string.Empty } });
        }

        /// <summary>
        /// Enters a site's unbooked sales, less refunds, and their cost in the game's statement of the business for a day
        /// (with <paramref name="orEarlier"/>, the latest it has up to that day: a site closing), so its tax and takings
        /// pages count them once. False if the game has no such statement: the sums wait.
        /// </summary>
        public static bool Book(WebPlace place, int day, out int sales, out int goods, bool orEarlier = false)
        {
            sales = goods = 0;
            WebShop shop = place.Shop;
            if (!OnlineRules.Unbooked(shop, out long takings, out long cost))
            {
                return true;   // refunds alone wait for takings to come off
            }
            List<FinancialSummary> summaries = Save != null ? Save.financialSummaries : null;
            FinancialSummary summary = null;
            FinancialSummary.BusinessIncomeStatement statement = null;
            for (int i = summaries != null ? summaries.Count - 1 : -1; i >= 0 && statement == null; i--)
            {
                FinancialSummary s = summaries[i];
                if (s == null || s.dayNumber > day || (!orEarlier && s.dayNumber != day))
                {
                    continue;
                }
                statement = StatementOf(s, place);
                summary = s;
                if (!orEarlier)
                {
                    break;   // the day's own statement or none
                }
            }
            if (statement == null)
            {
                return false;
            }
            long refunds = Math.Min(takings, Math.Max(0, shop.BookRefunds)), net = takings - refunds;
            if (statement.Sales == null)
            {
                statement.Sales = new List<FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry>();
            }
            if (statement.Resources == null)
            {
                statement.Resources = new List<FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry>();
            }
            float before = statement.TotalProfit;
            for (int l = 0; l < shop.Lines.Count; l++)
            {
                WebLine line = shop.Lines[l];
                if (line.BookTakings > 0 && takings > 0)
                {
                    Entry(statement.Sales, line.Item).Amount += (float)((double)line.BookTakings * net / takings / 100.0);
                }
                if (line.BookCost != 0)
                {
                    Entry(statement.Resources, line.Item).Amount += line.BookCost / 100f;   // under nothing: goods back on the shelves
                    line.BookCost = 0;
                }
                line.BookTakings = 0;
            }
            statement.TotalSales += net / 100f;
            statement.TotalResources += cost / 100f;
            statement.TotalProfit = statement.TotalSales - statement.TotalResources - statement.TotalOngoing;   // the game's own sum (FinancialSummaryHelper.CreateBusinessIncomeStatement)
            summary.totalBusinessProfit += statement.TotalProfit - before;
            FinancialSummaryHelper.UpdateIncomeStatementTotalProfit(summary);
            shop.BookRefunds -= (int)refunds;
            sales = (int)net;
            goods = (int)cost;
            return true;
        }

        // The site's business's statement in a day's summary, by its address, or by its place once the business is gone; null if none.
        private static FinancialSummary.BusinessIncomeStatement StatementOf(FinancialSummary summary, WebPlace place)
        {
            for (int b = 0; summary.businessIncomeStatements != null && b < summary.businessIncomeStatements.Count; b++)
            {
                FinancialSummary.BusinessIncomeStatement s = summary.businessIncomeStatements[b];
                if (s != null && s.Address != null && (place.Address != null ? s.Address == place.Address : PlaceOf(s.Address) == place.Shop.Place))
                {
                    return s;
                }
            }
            return null;
        }

        // An address written as Place writes a business's.
        private static string PlaceOf(Address address)
        {
            return Uri.EscapeDataString(address.streetName ?? string.Empty) + "~" + address.streetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry Entry(List<FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry> list, string item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].ItemName == item)
                {
                    return list[i];
                }
            }
            var entry = new FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry { ItemName = item, Amount = 0f };
            list.Add(entry);
            return entry;
        }
    }
}
