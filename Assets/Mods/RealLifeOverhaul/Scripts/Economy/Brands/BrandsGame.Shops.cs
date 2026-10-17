using System;
using System.Collections.Generic;
using Buildings.BuildingTypes.Shared.Dirtiness;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>The game part for the player's businesses as read: for a shop under a brand or a chain, the game's own figures.</summary>
    internal static partial class BrandsGame
    {
        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);
        private static readonly List<BrandShop> Before = new List<BrandShop>(16);
        private static readonly Dictionary<BuildingRegistration, int> BeforeAt =
            new Dictionary<BuildingRegistration, int>(16, ReferenceComparer<BuildingRegistration>.Instance);   // a shop of before by its business: the first
        private static readonly List<EmployeeInstance> Staff = new List<EmployeeInstance>(16);
        private static readonly KeyedSums<Address> Skills = new KeyedSums<Address>();   // the staff's main skill by the business they work at, for one read
        private static readonly HashSet<Address> SkillFaults = new HashSet<Address>();
        private static bool _skillsRead, _skillsFault;
        /// <summary>
        /// The player's businesses today, with the game's figures for those under a brand or a chain. A shop no longer the
        /// player's leaves its brand or chain; <paramref name="left"/> counts them.
        /// </summary>
        public static void ReadShops(BrandsState state, List<BrandShop> into, List<BrandShop> pool, int day, out int left)
        {
            left = 0;
            Before.Clear();
            Before.AddRange(into);   // a shop still the player's keeps its object, and with it what was last asked for and written there
            into.Clear();
            BeforeAt.Clear();
            for (int k = 0; k < Before.Count; k++)
            {
                if (Before[k].Reg != null && !BeforeAt.ContainsKey(Before[k].Reg))
                {
                    BeforeAt.Add(Before[k].Reg, k);   // the first shop of each business, as a walk along them finds
                }
            }
            _skillsRead = false;   // the staff are read for this pass when the first shop with a file asks
            for (int i = 0; i < state.Members.Count; i++)
            {
                state.Members[i].Seen = false;
            }
            for (int i = 0; i < state.Taken.Count; i++)
            {
                state.Taken[i].Seen = false;
            }
            Books.Businesses(Regs);
            for (int i = 0; i < Regs.Count; i++)
            {
                BuildingRegistration reg = Regs[i];
                BrandShop shop = null;
                if (BeforeAt.TryGetValue(reg, out int at))
                {
                    shop = Before[at];
                    Before[at] = null;   // taken: not given to the pool below
                    BeforeAt.Remove(reg);
                }
                if (shop == null)
                {
                    if (pool.Count > 0)
                    {
                        shop = pool[pool.Count - 1];
                        pool.RemoveAt(pool.Count - 1);
                    }
                    else
                    {
                        shop = new BrandShop();
                    }
                    shop.Asked = shop.Last = 0;
                    shop.Applied = false;
                }
                shop.Reg = reg;
                shop.Place = Place(reg);
                shop.Name = string.IsNullOrEmpty(reg.BusinessName) ? "?" : reg.BusinessName;
                shop.Type = reg.businessTypeName ?? string.Empty;
                try
                {
                    shop.Address = reg.Address;
                    shop.District = reg.Neighborhood;
                }
                catch (Exception)
                {
                    shop.Address = null;   // a business whose building cannot be read is in no district
                    shop.District = null;
                }
                shop.File = state.Member(shop.Place);
                shop.Chain = state.TakenAt(shop.Place);
                shop.Customers = shop.Takings = 0;
                if (shop.File != null || shop.Chain != null)
                {
                    Figures(shop, day);
                }
                into.Add(shop);
            }
            Regs.Clear();
            for (int i = 0; i < Before.Count; i++)
            {
                if (Before[i] == null)
                {
                    continue;
                }
                Before[i].Reg = null;
                Before[i].File = null;
                Before[i].Chain = null;
                pool.Add(Before[i]);
            }
            Before.Clear();
            BeforeAt.Clear();
            Skills.Clear();
            _skillsRead = false;
            for (int i = state.Members.Count - 1; i >= 0; i--)
            {
                if (!state.Members[i].Seen)
                {
                    BrandRules.Leave(state, state.Members[i].Place);
                    left++;
                }
            }
            for (int i = state.Taken.Count - 1; i >= 0; i--)
            {
                if (!state.Taken[i].Seen)
                {
                    state.Taken.RemoveAt(i);
                    left++;
                }
            }
        }

        private static void Figures(BrandShop shop, int day)
        {
            BuildingRegistration reg = shop.Reg;
            int satisfaction = reg.satisfaction != null ? reg.satisfaction.overall : 50;
            int clean = Cleanliness(reg), stock = Stock(reg), skill = Skill(reg);
            int standards = BrandRules.WithInterior(BrandRules.ShopStandards(satisfaction, clean, stock, skill), InteriorOf(reg));
            Yesterday(reg, day, out shop.Customers, out shop.Takings);
            if (shop.File != null)
            {
                Member m = shop.File;
                m.Seen = true;
                m.District = shop.District;
                m.Satisfaction = satisfaction;
                m.Clean = clean;
                m.Stock = stock;
                m.Skill = skill;
                m.Standards = standards;
                m.Customers = shop.Customers;
                m.Takings = shop.Takings;
                Vector3 spot;
                m.HasSpot = Spot(shop.Address, out spot);
                if (m.HasSpot)
                {
                    m.X = Mathf.RoundToInt(spot.x);
                    m.Z = Mathf.RoundToInt(spot.z);
                }
                int loss = 0;
                try
                {
                    loss = BrandsBridges.PriceLoss != null ? BrandsBridges.PriceLoss(reg) : 0;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                m.PriceLoss = Math.Min(0, loss);
            }
            if (shop.Chain != null)
            {
                shop.Chain.Seen = true;
                shop.Chain.Standards = standards;
                shop.Chain.Prices = PriceRatio(reg);
            }
        }

        private static int InteriorOf(BuildingRegistration reg)
        {
            try
            {
                return BrandsBridges.Interior != null ? BrandsBridges.Interior(reg) : 0;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return 0;
            }
        }

        private static int Cleanliness(BuildingRegistration reg)
        {
            try
            {
                return Mathf.Clamp(Mathf.RoundToInt(reg.GetCleanliness()), 0, 100);
            }
            catch (Exception)
            {
                return 100;   // a business with no floor to dirty is clean
            }
        }

        // The share of the type's main goods the shop has to sell, per cent; 100 for a type with none (a service).
        private static int Stock(BuildingRegistration reg)
        {
            try
            {
                HashSet<string> main = BusinessTypeHelper.GetPrimaryProducts(reg.businessTypeName);
                List<string> shelves = reg.cachedAvailableProducts;
                if (main == null || main.Count == 0 || shelves == null)
                {
                    return 100;
                }
                int have = 0;
                foreach (string item in main)
                {
                    have += shelves.Contains(item) ? 1 : 0;
                }
                return have * 100 / main.Count;
            }
            catch (Exception)
            {
                return 100;
            }
        }

        // The mean of the staff's main skill (0 to 100); 50 for a shop the player runs alone. Only a business with no address
        // walks the employees itself, as the game's query then takes everyone.
        private static int Skill(BuildingRegistration reg)
        {
            Address address = reg.Address;
            if (!ReferenceEquals(address, null) && !(string.IsNullOrEmpty(address.streetName) && address.streetNumber == 0))
            {
                if (!_skillsRead)
                {
                    ReadSkills();
                }
                if (_skillsFault || SkillFaults.Contains(address))
                {
                    return 50;   // the game's own query, or the look at one of the shop's employees, failed
                }
                int at = Skills.At(address);
                return at >= 0 && Skills.Counts[at] > 0 ? Mathf.Clamp(Mathf.RoundToInt(Skills.Sums[at] / Skills.Counts[at]), 0, 100) : 50;
            }
            Staff.Clear();
            try
            {
                EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = address, excludeBeingReplaced = true }, Staff);
                float sum = 0f;
                int count = 0;
                for (int i = 0; i < Staff.Count; i++)
                {
                    EmployeeInstance e = Staff[i];
                    if (e != null && !e.IsCandidate && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null)
                    {
                        sum += e.characterData.skills[0].value;
                        count++;
                    }
                }
                return count > 0 ? Mathf.Clamp(Mathf.RoundToInt(sum / count), 0, 100) : 50;
            }
            catch (Exception)
            {
                return 50;
            }
            finally
            {
                Staff.Clear();
            }
        }

        // One pass over the game's employees for a read of the shops: each main skill added to its business's sum, in the
        // game's own order so each mean matches the game's query to the last bit.
        private static void ReadSkills()
        {
            _skillsRead = true;
            _skillsFault = false;
            Skills.Clear();
            SkillFaults.Clear();
            List<EmployeeInstance> all = EmployeeHelper.GetEmployeeInstances();
            if (all == null)
            {
                return;
            }
            try
            {
                for (int i = 0; i < all.Count; i++)
                {
                    EmployeeInstance e = all[i];
                    Address at = e.assignedAddress;   // an empty place in the game's list fails its query for every shop, and so does this
                    if (at == null || e.isBeingReplaced)
                    {
                        continue;   // Address == null is also true for the game's "no address"
                    }
                    try
                    {
                        if (!e.IsCandidate && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null)
                        {
                            Skills.Add(at, e.characterData.skills[0].value);
                        }
                    }
                    catch (Exception)
                    {
                        SkillFaults.Add(at);
                    }
                }
            }
            catch (Exception)
            {
                _skillsFault = true;
            }
        }

        // Yesterday's customers and takings by the game's own order history, from the newest entry back.
        private static void Yesterday(BuildingRegistration reg, int day, out int customers, out int takings)
        {
            customers = takings = 0;
            List<OrderHistoryEntry> history = reg.orderHistory;
            for (int i = history != null ? history.Count - 1 : -1; i >= 0; i--)
            {
                OrderHistoryEntry entry = history[i];
                if (entry == null || entry.dayNumber >= day)
                {
                    continue;
                }
                if (entry.dayNumber == day - 1)
                {
                    customers = Mathf.Max(0, entry.totalCustomers);
                    takings = Mathf.RoundToInt(Mathf.Max(0f, entry.totalRevenue));
                }
                break;
            }
        }

        /// <summary>A shop's prices against the game's own market price for the same goods, thousandths (1000: the same); 0 with no price to judge by.</summary>
        public static int PriceRatio(BuildingRegistration reg)
        {
            try
            {
                List<RetailPrice> prices = reg.retailPrices;
                double sum = 0d;
                int count = 0;
                for (int i = 0; prices != null && i < prices.Count; i++)
                {
                    RetailPrice p = prices[i];
                    float market = p != null && p.price > 0f && !string.IsNullOrEmpty(p.itemName) ? ItemHelper.GetDefaultMarketPrice(p.itemName) : 0f;
                    if (market > 0f)
                    {
                        sum += p.price / market;
                        count++;
                    }
                }
                return count > 0 ? Mathf.RoundToInt((float)(sum / count * 1000d)) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
