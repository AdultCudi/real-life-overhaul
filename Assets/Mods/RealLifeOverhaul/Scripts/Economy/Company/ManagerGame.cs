using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using Buildings.Office.Headquarters;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One of the player's employees as the Managers tab lists them, to be appointed.</summary>
    public struct ManagerHand
    {
        public string Id, Name, Place, PlaceName;
        public int Skill;
        public int Wage;        // the game's hourly wage at today's prices, hundredths
    }

    /// <summary>
    /// Everything the managers read from the game or do to it. What they do is what the player does in the game's own
    /// screens: a delivery contract's amounts (BizMan), a shop's prices (the game's price cell, through the brands'
    /// writer), a candidate from the place's own job board hired (MyEmployees). Never another player's: every place is
    /// one Going Public calls the player's own (RotaGame.ReadPlaces). Runs on the managers' hours, never per frame.
    /// </summary>
    internal static class ManagerGame
    {
        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The employee by the game's id; null if the game no longer has them.</summary>
        public static EmployeeInstance Employee(string id)
        {
            return CompanyGame.Employee(id);
        }

        /// <summary>True for an employee assigned to the place with this street and number, as the game counts staff there.</summary>
        public static bool At(EmployeeInstance e, BuildingRegistration reg)
        {
            return e != null && reg != null && e.assignedAddress != null && e.assignedAddress.streetNumber == reg.StreetNumber
                   && string.Equals(e.assignedAddress.streetName, reg.StreetName, StringComparison.Ordinal);
        }

        /// <summary>The game's value of somebody's main skill, 0 to 100.</summary>
        public static int SkillOf(EmployeeInstance e)
        {
            return e != null && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null
                ? CompanyCatalogue.Clamp(Mathf.RoundToInt(e.characterData.skills[0].value), 0, 100)
                : 0;
        }

        public static string NameOf(EmployeeInstance e)
        {
            return e != null && e.characterData != null && !string.IsNullOrEmpty(e.characterData.name) ? e.characterData.name : "?";
        }

        /// <summary>True while the game has somebody off sick today (EmployeeHelper.RunDaily picks it each morning).</summary>
        public static bool Absent(EmployeeInstance e)
        {
            return e != null && e.isAbsent;
        }

        private static bool Hireable(EmployeeInstance e, EmployeeInstance self)
        {
            return e != null && !ReferenceEquals(e, self) && !string.IsNullOrEmpty(e.id) && !e.IsCandidate && !e.isBeingReplaced && e.satisfaction > 0f
                   && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null;
        }

        /// <summary>Who works at a place and could be made its manager.</summary>
        public static void StaffAt(RotaPlace place, int index, List<ManagerHand> into)
        {
            into.Clear();
            List<EmployeeInstance> all = Save != null ? Save.EmployeeInstances : null;
            EmployeeInstance self = Self();
            for (int i = 0; all != null && place != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (Hireable(e, self) && At(e, place.Reg))
                {
                    into.Add(Hand(e, place, index));
                }
            }
        }

        /// <summary>
        /// Who works at one of the player's headquarters and is on none of the game's own plans there (HR, headhunting,
        /// logistics, pricing, purchasing): they could be made a regional manager.
        /// </summary>
        public static void Offices(IReadOnlyList<RotaPlace> places, int index, List<ManagerHand> into)
        {
            into.Clear();
            List<EmployeeInstance> all = Save != null ? Save.EmployeeInstances : null;
            EmployeeInstance self = Self();
            for (int p = 0; p < places.Count; p++)
            {
                if (places[p].Type != RotaType.Headquarters)
                {
                    continue;
                }
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    EmployeeInstance e = all[i];
                    if (Hireable(e, self) && At(e, places[p].Reg) && !OnAPlan(e.id))
                    {
                        into.Add(Hand(e, places[p], index));
                    }
                }
            }
        }

        private static ManagerHand Hand(EmployeeInstance e, RotaPlace place, int index)
        {
            return new ManagerHand
            {
                Id = e.id, Name = NameOf(e), Place = place.Place, PlaceName = place.Name, Skill = SkillOf(e),
                Wage = CompanyCatalogue.Share(Mathf.RoundToInt(e.hourlyWage * 100f), index)
            };
        }

        /// <summary>True for somebody one of the game's own plans at a headquarters has: they have work of their own there.</summary>
        public static bool OnAPlan(string id)
        {
            GameInstance save = Save;
            if (save == null || string.IsNullOrEmpty(id))
            {
                return false;
            }
            for (int i = 0; save.hrManagerPlans != null && i < save.hrManagerPlans.Count; i++)
            {
                if (save.hrManagerPlans[i] != null && save.hrManagerPlans[i].assignedEmployeeId == id)
                {
                    return true;
                }
            }
            for (int i = 0; save.headhunterPlans != null && i < save.headhunterPlans.Count; i++)
            {
                if (save.headhunterPlans[i] != null && save.headhunterPlans[i].assignedEmployeeId == id)
                {
                    return true;
                }
            }
            for (int i = 0; save.logisticsManagerPlans != null && i < save.logisticsManagerPlans.Count; i++)
            {
                if (save.logisticsManagerPlans[i] != null && save.logisticsManagerPlans[i].assignedEmployeeId == id)
                {
                    return true;
                }
            }
            for (int i = 0; save.pricingManagerPlans != null && i < save.pricingManagerPlans.Count; i++)
            {
                if (save.pricingManagerPlans[i] != null && save.pricingManagerPlans[i].assignedEmployeeId == id)
                {
                    return true;
                }
            }
            for (int i = 0; save.importPartnerships != null && i < save.importPartnerships.Count; i++)
            {
                if (save.importPartnerships[i] != null && save.importPartnerships[i].employeeInstanceId == id)
                {
                    return true;
                }
            }
            return false;
        }

        private static EmployeeInstance Self()
        {
            try
            {
                return PlayerHelper.GetPlayerEmployeeInstance();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A place's district, as the game names it; null if it cannot be read.</summary>
        public static string District(BuildingRegistration reg)
        {
            try
            {
                return reg != null ? reg.Neighborhood : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // --- Stock: repeating delivery contracts ---

        /// <summary>The repeating delivery contracts that bring goods to a place, in the order of their wholesalers' places.</summary>
        public static void Contracts(BuildingRegistration reg, List<DeliveryContract> into)
        {
            into.Clear();
            List<DeliveryContract> all = Save != null ? Save.DeliveryContracts : null;
            for (int i = 0; all != null && reg != null && i < all.Count; i++)
            {
                DeliveryContract c = all[i];
                if (c != null && c.enabled && c.repeatingOrder && !c.isUrgentOrder && c.items != null && c.businessAddress != null && c.wholesaleAddress != null
                    && c.businessAddress == reg.Address)
                {
                    into.Add(c);
                }
            }
            into.Sort(ByWholesaler);
        }

        private static readonly Comparison<DeliveryContract> ByWholesaler = (a, b) => string.CompareOrdinal(Wholesaler(a), Wholesaler(b));

        /// <summary>A contract's wholesaler as a place: with the place it delivers to, the game's one contract between them.</summary>
        public static string Wholesaler(DeliveryContract c)
        {
            Address a = c != null ? c.wholesaleAddress : null;
            return a == null ? string.Empty : Uri.EscapeDataString(a.streetName ?? string.Empty) + "~" + a.streetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The contract from a wholesaler to a place; null if there is none now.</summary>
        public static DeliveryContract Contract(BuildingRegistration reg, string wholesaler)
        {
            List<DeliveryContract> all = Save != null ? Save.DeliveryContracts : null;
            for (int i = 0; all != null && reg != null && i < all.Count; i++)
            {
                DeliveryContract c = all[i];
                if (c != null && c.items != null && c.businessAddress != null && c.businessAddress == reg.Address && Wholesaler(c) == wholesaler)
                {
                    return c;
                }
            }
            return null;
        }

        /// <summary>False while the game's lock period holds the contract's next delivery, as BizMan's own screen refuses a change.</summary>
        public static bool CanChange(DeliveryContract c)
        {
            return c != null && DeliveryHelper.CanModifyContract(c.nextDeliveryDay);
        }

        /// <summary>The most of a product one delivery brings; 0 with no limit (the game's own switch, or none set).</summary>
        public static int Most(DeliveryContractItem line)
        {
            if (DeliveryHelper.AreWholesaleAndImportLimitsDisabled())
            {
                return 0;
            }
            Item item = line != null ? line.ItemCached : null;
            return item != null ? Math.Max(0, item.maxWholesaleOrderAmount) : 0;
        }

        /// <summary>A contract line's amount set, as the game's own contract screen sets it.</summary>
        public static void SetAmount(DeliveryContractItem line, int amount)
        {
            line.amount = Math.Max(0, amount);
            SaveGameManager.MarkChange();
        }

        public static DeliveryContractItem Line(DeliveryContract c, string item)
        {
            for (int i = 0; c != null && c.items != null && i < c.items.Count; i++)
            {
                if (c.items[i] != null && c.items[i].itemName == item)
                {
                    return c.items[i];
                }
            }
            return null;
        }

        /// <summary>The products a logistics manager of the game's tops up at a place: those are the game's own to keep.</summary>
        public static void Rounds(BuildingRegistration reg, HashSet<string> into)
        {
            into.Clear();
            List<LogisticsManagerPlan> plans = Save != null ? Save.logisticsManagerPlans : null;
            for (int i = 0; plans != null && reg != null && i < plans.Count; i++)
            {
                List<LogisticsManagerPlanDestination> to = plans[i] != null ? plans[i].destinations : null;
                for (int k = 0; to != null && k < to.Count; k++)
                {
                    if (to[k] == null || to[k].deliveryTargetAddress == null || !(to[k].deliveryTargetAddress == reg.Address) || to[k].stockTargets == null)
                    {
                        continue;
                    }
                    for (int t = 0; t < to[k].stockTargets.Count; t++)
                    {
                        if (to[k].stockTargets[t] != null && !string.IsNullOrEmpty(to[k].stockTargets[t].itemName))
                        {
                            into.Add(to[k].stockTargets[t].itemName);
                        }
                    }
                }
            }
        }

        /// <summary>Units of a product at a place: on its shelves, in its storage, on its pallets, as the game counts its cargo.</summary>
        public static int OnSite(BuildingRegistration reg, string item)
        {
            int units = 0;
            if (reg == null || reg.itemInstances == null)
            {
                return 0;
            }
            foreach (ItemInstance instance in reg.itemInstances.Values)
            {
                List<CargoInstance> cargo = instance != null ? instance.cargoInstances : null;
                for (int k = 0; cargo != null && k < cargo.Count; k++)
                {
                    if (cargo[k] != null && cargo[k].itemName == item)
                    {
                        units += Math.Max(0, cargo[k].amount);
                    }
                }
            }
            return units;
        }

        // --- Prices ---

        /// <summary>True while one of the game's own pricing managers sets the prices in a district: they are theirs.</summary>
        public static bool PricingManagerCovers(string district)
        {
            try
            {
                return !string.IsNullOrEmpty(district) && Save != null && Save.pricingManagerPlans != null && PricingManagerHelper.GetPlanCoveringNeighborhood(district) != null;
            }
            catch (Exception)
            {
                return true;   // cannot tell: left alone
            }
        }

        /// <summary>The market price of a product in a district, cents; 0 with none.</summary>
        public static int MarketCents(string item, string district)
        {
            try
            {
                return Mathf.Max(0, PricingManagerHelper.FloorToCents(ItemHelper.GetMarketReferencePrice(item, district)));
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// The price the game's own pricing manager would set, cents (PricingManagerHelper.ComputeSuggestion, its highest),
        /// at the manager's skill; a mistake is the game's own jitter at its fullest, which lands lower. 0 with none.
        /// </summary>
        public static int Suggested(string item, string district, int skill, bool mistake)
        {
            try
            {
                (float min, float max) s = PricingManagerHelper.ComputeSuggestion(item, district, Mathf.Clamp01(skill / 100f), mistake ? 1f : 0f);
                return Mathf.Max(0, Mathf.RoundToInt(s.max * 100f));
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>A product's price at a place, cents; -1 where it has none.</summary>
        public static int PriceCents(BuildingRegistration reg, string item)
        {
            List<RetailPrice> prices = reg != null ? reg.retailPrices : null;
            for (int i = 0; prices != null && i < prices.Count; i++)
            {
                if (prices[i] != null && prices[i].itemName == item)
                {
                    return Cents(prices[i].price);
                }
            }
            return -1;
        }

        public static int Cents(float price)
        {
            return Mathf.Max(0, Mathf.RoundToInt(price * 100f));
        }

        // --- Hiring ---

        /// <summary>The game's candidates its job board has brought to a place, waiting in MyEmployees.</summary>
        public static void Candidates(BuildingRegistration reg, List<EmployeeInstance> into)
        {
            into.Clear();
            List<EmployeeInstance> all = Save != null ? Save.CandidateEmployeeInstances : null;
            for (int i = 0; all != null && reg != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (e != null && e.candidateInfo != null && e.hourlyWage > 0f && At(e, reg) && e.characterData != null && e.characterData.skills != null
                    && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null)
                {
                    into.Add(e);
                }
            }
        }

        /// <summary>The going rate an hour for somebody's main skill at a place, at today's prices, hundredths (as the Staff tab counts it).</summary>
        public static int Going(EmployeeInstance e, int index, CycleState cycle, int income)
        {
            return StaffRules.Going(Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(e.characterData.skills[0]) * 100f), StaffRules.Market(index, cycle, income));
        }

        /// <summary>The game's hourly wage of somebody at today's prices, hundredths.</summary>
        public static int WageNow(EmployeeInstance e, int index)
        {
            return CompanyCatalogue.Share(Mathf.RoundToInt(e.hourlyWage * 100f), index);
        }

        /// <summary>A candidate hired the game's own way (EmployeeHelper.HireCandidate), at the place their job board found them for.</summary>
        public static bool Hire(EmployeeInstance e, BuildingRegistration reg)
        {
            if (e == null || reg == null || !e.IsCandidate || !At(e, reg) || !GoingPublic.Mine(reg))
            {
                return false;
            }
            EmployeeHelper.HireCandidate(e);
            return !e.IsCandidate;
        }
    }
}
