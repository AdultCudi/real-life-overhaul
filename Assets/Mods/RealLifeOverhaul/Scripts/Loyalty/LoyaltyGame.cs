using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Entities;
using Helpers;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Loyalty
{
    /// <summary>A shop's brand as its card is shared by: the key the brand's shops have in common (null for a shop of no brand) and the brand's name.</summary>
    public delegate string LoyaltyChain(string place, out string name);

    /// <summary>
    /// What the loyalty cards ask of the rest of the mod, each optional so the feature works alone; Wiring/Wiring.LoyaltyLinks
    /// fills them in.
    /// </summary>
    public static class LoyaltyBridges
    {
        /// <summary>Business pressure's writer of a business's footfall; unset, nothing is written and a scheme adds no customers.</summary>
        public static Economy.CityBridges.FootfallWriter Footfall;

        /// <summary>The brand a shop's card is shared with. Default: every shop has a card of its own.</summary>
        public static LoyaltyChain Chain;

        /// <summary>
        /// The rival tycoons' outlets of a kind in a district that stand on no business of the game's (those are read with the
        /// city's shops), added to the list as of a day. Default: none.
        /// </summary>
        public static Action<string, string, int, List<LoyaltyRival>> Outlets;

        public static void Reset()
        {
            Footfall = null;
            Chain = null;
            Outlets = null;
        }
    }

    /// <summary>
    /// Everything the loyalty cards read from the game or do to it: the player's businesses, yesterday as the game booked it
    /// (its order history and its customers' satisfaction), whether an hour could serve anybody, the other owners' shops of a
    /// kind in a district, a product more in a member's basket as the game adds one, and money through the mod's one booking
    /// call. No timer of its own.
    /// </summary>
    internal static class LoyaltyGame
    {
        public const string RewardsKey = "reallife_transaction_loyalty_rewards";
        public const string CostsKey = "reallife_transaction_loyalty_costs";
        public const string SetupKey = "reallife_transaction_loyalty_setup";
        public const string ChangeKey = "reallife_transaction_loyalty_change";
        private const string DeductKey = "reallife_loyalty_deduct";
        private const string Casino = "ba:businesstype_casino";
        private const int BooksDays = 7;          // days of the game's order history a shop's margin and average day are read from
        private const int QualityUnknown = 50;    // how well a shop with no crew on the game's books is taken to serve, 0 to 100
        public const float BasketLead = 2f;       // game minutes: a customer due within them of the hour's turn is left as drawn

        public static IModLogger Logger;

        private static readonly List<string> Products = new List<string>(32);
        private static readonly ConditionalWeakTable<CustomerEntry, object> Seen = new ConditionalWeakTable<CustomerEntry, object>();
        private static readonly object Mark = new object();

        private static GameInstance Save => SaveGameManager.Current;

        public static bool Ready => Save != null && Save.BuildingRegistrations != null;

        public static int Day => Save != null ? Save.Day : 0;

        public static int Hour => Save != null ? Save.Hour : 0;

        public static float Minute => TimeHelper.CurrentMinute;

        public static int Level => Prices.Level;

        /// <summary>True while the player's cash is under nothing.</summary>
        public static bool Broke => Save != null && Save.Money < 0f;

        /// <summary>A business of the player's that takes money: the ones a scheme can run at.</summary>
        public static bool IsShop(BuildingRegistration reg) => BusinessGame.IsBusiness(reg);

        public static string Place(BuildingRegistration reg) => BusinessGame.Place(reg);

        public static string Name(BuildingRegistration reg) => BusinessGame.Name(reg);

        /// <summary>The district of a business as the game writes its name.</summary>
        public static string DistrictName(BuildingRegistration reg)
        {
            string id = GameRegistrations.District(reg);
            return string.IsNullOrEmpty(id) ? string.Empty : LocalizorManager.IsLocalizedKey(id) ? id.GetLocalization() : id;
        }

        /// <summary>The player's businesses that take money, by their place. One walk of the save's registrations.</summary>
        public static void Shops(Dictionary<string, BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Ready ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (IsShop(all[i]))
                {
                    into[Place(all[i])] = all[i];
                }
            }
        }

        private static OrderHistoryEntry Books(BuildingRegistration reg, int day)
        {
            List<OrderHistoryEntry> history = reg != null ? reg.orderHistory : null;
            for (int i = history != null ? history.Count - 1 : -1; i >= 0; i--)
            {
                if (history[i] != null && history[i].dayNumber == day)
                {
                    return history[i];
                }
            }
            return null;
        }

        /// <summary>A day of a business as the game booked it at the day's turn, with its customers' satisfaction as it stands.</summary>
        public static LoyaltyInput Yesterday(BuildingRegistration reg, int day)
        {
            var d = new LoyaltyInput { Day = day, Weekday = LoyaltyRules.Weekday(day), Satisfaction = 50, Pricing = 100, Clean = 100 };
            OrderHistoryEntry books = Books(reg, day);
            if (books != null)
            {
                d.Served = Math.Max(0, books.totalCustomers);
                d.Takings = (long)Math.Round(Math.Max(0f, books.totalRevenue) * 100.0);
                double goods = 0.0;
                for (int i = 0; books.itemSales != null && i < books.itemSales.Count; i++)
                {
                    OrderHistoryEntry.ItemReport item = books.itemSales[i];
                    if (item != null)
                    {
                        d.Items += Math.Max(0, item.amountSold);
                        goods += Math.Max(0f, item.totalWholesalePrice);
                    }
                }
                d.Goods = (long)Math.Round(goods * 100.0);
            }
            Satisfaction s = reg.satisfaction;
            if (s != null)
            {
                d.Satisfaction = LoyaltyRules.Clamp(s.overall, 0, 100);
                d.Pricing = LoyaltyRules.Clamp(s.pricing, 0, 100);
            }
            d.Clean = LoyaltyRules.Clamp(BusinessGame.Cleanliness(reg), 0, 100);
            return d;
        }

        /// <summary>
        /// The customers a business served a day over its last days with books, in hundredths, and what it kept of its takings
        /// after its goods' cost, per mille (<see cref="LoyaltyRules.MarginUnknown"/> with no takings yet).
        /// </summary>
        public static int Average(BuildingRegistration reg, out int marginMille)
        {
            marginMille = LoyaltyRules.MarginUnknown;
            List<OrderHistoryEntry> history = reg != null ? reg.orderHistory : null;
            int days = 0;
            long served = 0;
            double takings = 0.0, goods = 0.0;
            for (int i = history != null ? history.Count - 1 : -1; i >= 0 && days < BooksDays; i--)
            {
                OrderHistoryEntry books = history[i];
                if (books == null)
                {
                    continue;
                }
                days++;
                served += Math.Max(0, books.totalCustomers);
                takings += Math.Max(0f, books.totalRevenue);
                for (int k = 0; books.itemSales != null && k < books.itemSales.Count; k++)
                {
                    goods += books.itemSales[k] != null ? Math.Max(0f, books.itemSales[k].totalWholesalePrice) : 0f;
                }
            }
            if (takings > 0.0)
            {
                marginMille = LoyaltyRules.Clamp((int)Math.Round((takings - goods) * 1000.0 / takings), 0, 1000);
            }
            return days > 0 ? (int)(served * 100 / days) : 0;
        }

        /// <summary>
        /// The hour under way at a business: whether its hours say it is open, and if so whether it can serve nobody (closed by
        /// its switch, nothing to sell, nobody at a station that needs somebody).
        /// </summary>
        public static void HourOf(BuildingRegistration reg, int hour, out bool open, out bool bad)
        {
            open = bad = false;
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            if (today == null || !today.isOpen || today.openingHourSlots == null)
            {
                return;
            }
            for (int i = 0; i < today.openingHourSlots.Count && !open; i++)
            {
                OpeningHourSlot slot = today.openingHourSlots[i];
                open = slot != null && hour >= slot.startingHour && hour < slot.endingHour;
            }
            bad = open && (reg.temporarilyClosed || reg.cachedAvailableProducts == null || reg.cachedAvailableProducts.Count == 0 || BusinessHelper.IsMissingEmployeeAlertNeeded(reg));
        }

        /// <summary>
        /// The shops of another owner of a business's kind in its district (the city's and the game's rivals', never another
        /// player's), as the rules see them, added to the list. The game keeps no satisfaction for a shop that is not the
        /// player's, so how well one serves is the skill of its crew as the game holds it. One walk of the save's
        /// registrations, the district read only for those of the kind.
        /// </summary>
        public static void Rivals(BuildingRegistration reg, int today, List<LoyaltyRival> into)
        {
            string district = GameRegistrations.District(reg), type = reg.businessTypeName;
            List<BuildingRegistration> all = Ready ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && district != null && i < all.Count; i++)
            {
                BuildingRegistration other = all[i];
                if (other == null || ReferenceEquals(other, reg) || other.RentedByPlayer || !other.HasEstablishedBusiness || !other.HasValidAddress
                    || !string.Equals(other.businessTypeName, type, StringComparison.Ordinal) || GoingPublic.Foreign(other)
                    || !string.Equals(GameRegistrations.District(other), district, StringComparison.Ordinal))
                {
                    continue;
                }
                into.Add(new LoyaltyRival
                {
                    Seed = LoyaltyRules.Hash(other.StreetName) ^ (other.StreetNumber * 7919),
                    Quality = Crew(other),
                    Days = other.creationDay >= 0 ? Math.Max(0, today - other.creationDay) : LoyaltyRules.RivalSetupDays + LoyaltyRules.RivalSettleDays,
                    Tycoon = !string.IsNullOrEmpty(other.businessOwnerRivalId)
                });
            }
        }

        // The average main skill of a shop's crew as the game holds them (30 to 95 each; somebody the player poached is not
        // counted, and the game's replacement is a weaker hand), 0 to 100.
        private static int Crew(BuildingRegistration other)
        {
            float sum = 0f;
            int hands = 0;
            for (int i = 0; other.aiEmployees != null && i < other.aiEmployees.Count; i++)
            {
                if (other.aiEmployees[i] != null && !other.aiEmployees[i].isPoached)
                {
                    sum += other.aiEmployees[i].primarySkillValue;
                    hands++;
                }
            }
            return hands > 0 ? LoyaltyRules.Clamp((int)Math.Round(sum / hands), 0, 100) : QualityUnknown;
        }

        /// <summary>
        /// One more product for some of a retail shop's customers still to come this hour and the next, each customer looked at
        /// once: so many in a thousand by a roll from the place, the day and the hour, never the engine's own dice. Returns how
        /// many products were added.
        /// </summary>
        public static int Basket(BuildingRegistration reg, int share, int day, int hour, float soonest)
        {
            if (share <= 0 || reg == null || reg.temporarilyClosed || !Retail(reg))
            {
                return 0;
            }
            List<CustomerEntry> entries = CustomerDraw.Entries(reg);
            BusinessType type = BusinessTypeHelper.GetData(reg);
            if (entries == null || entries.Count == 0 || type == null || !Sellable(reg, type))
            {
                return 0;
            }
            int seed = LoyaltyRules.Hash(Place(reg)), added = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                CustomerEntry e = entries[i];
                if (e == null || e.completed || e.spawnTime == null || e.spawnTime.Day != day || e.order == null || e.order.entries == null || e.order.entries.Count == 0
                    || !(e.spawnTime.Hour == hour + 1 || (e.spawnTime.Hour == hour && e.spawnTime.Minute > soonest)) || Seen.TryGetValue(e, out object _))
                {
                    continue;
                }
                Seen.Add(e, Mark);
                if (LoyaltyRules.Roll(seed, day * 24 + hour, i) >= share)
                {
                    continue;
                }
                int start = LoyaltyRules.Roll(seed, day * 24 + hour, i + 100000) % Products.Count;
                for (int tried = 0; tried < Products.Count; tried++)
                {
                    string product = Products[(start + tried) % Products.Count];
                    if (Service(product) && Has(e, product))
                    {
                        continue;   // a service is had once a customer, as the game draws them
                    }
                    e.order.entries.Add(new OrderEntry { itemName = product });
                    added++;
                    break;
                }
            }
            Products.Clear();
            return added;
        }

        // A shop whose customers the game draws as retail ones, buying products off its shelves.
        private static bool Retail(BuildingRegistration reg)
        {
            try
            {
                return reg.businessTypeName != Casino && CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg).GetType() == typeof(CustomerEntriesCalculatorRetail);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // What the shop has on its shelves now, less entrance fees and tickets, into Products. False if it has nothing.
        private static bool Sellable(BuildingRegistration reg, BusinessType type)
        {
            Products.Clear();
            List<string> stocked = reg.cachedAvailableProducts;
            for (int i = 0; stocked != null && i < stocked.Count; i++)
            {
                string name = stocked[i];
                if (string.IsNullOrEmpty(name) || name == type.defaultEntranceFee || name == type.weekendOnlyEntranceFee)
                {
                    continue;
                }
                BigAmbitions.Items.Item item = ItemsGetter.GetByName(name, true);
                if (item != null && !item.HasTag(TagRef.Itemtag.isticket))
                {
                    Products.Add(name);
                }
            }
            return Products.Count > 0;
        }

        private static bool Service(string product)
        {
            BigAmbitions.Items.Item item = ItemsGetter.GetByName(product, true);
            return item != null && (item.type & ItemType.ServiceProduct) != 0;
        }

        private static bool Has(CustomerEntry e, string product)
        {
            for (int i = 0; i < e.order.entries.Count; i++)
            {
                if (e.order.entries[i] != null && e.order.entries[i].itemName == product)
                {
                    return true;
                }
            }
            return false;
        }

        // A marketing cost of the business, deducted by the game's tax as its own marketing is: the name is written as words, so
        // the tax notice reads the same in a save the mod has left.
        private static TransactionInfo Info(string key, BuildingRegistration reg)
        {
            var info = new TransactionInfo(key, Economy.GameBooks.Marketing, new Dictionary<string, string>(2) { { "name", Name(reg) } });
            info.SetTaxDeductibleName(DeductKey.GetLocalization());
            return info;
        }

        /// <summary>
        /// Books a cost of a scheme at its business, dated <paramref name="day"/> (yesterday's rewards go into yesterday's
        /// statement) or today. Without <paramref name="force"/> the game refuses what the cash does not cover: false, nothing taken.
        /// </summary>
        public static bool Charge(BuildingRegistration reg, long amount, string key, int? day, bool force)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (Save == null || reg == null)
            {
                return false;
            }
            bool paid = Economy.BizBooks.Book(-(float)amount, Info(key, reg), day, reg.Address, force);
            Logger?.Info("Loyalty: " + (paid ? "booked " : "could not book ") + amount + " (" + key + ") at " + Name(reg) + ".");
            return paid;
        }

        /// <summary>A day's statement brought up to date with what the schemes booked for it since the game made it; as often as asked, nothing counts twice.</summary>
        public static void Refresh(int day)
        {
            try
            {
                Economy.GameBooks.Refresh(day);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        public static void Tell(NotificationType type, string key, Dictionary<string, string> data)
        {
            Notifications.Show(type, key, data, 10f);
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
