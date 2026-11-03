using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings.Office.Headquarters;
using Controllers;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The game's pricing plans as the player set them on BizMan's pricing page: a plan's strategy applied right after the
    /// game's own daily reprice through the plan's own ApplySuggestedPrice (what its Apply suggested prices calls), its
    /// prices held where the daily reprice is off, and its prices from just before its last reprice kept so Reset to
    /// yesterday puts them back. The game's reprice is never patched: the plan's next reprice moving is how one is seen, in
    /// the hour after the game's. A plan of another player, or one that would price another player's shop (Going Public),
    /// is never touched. Every figure is the game's own: the suggestion, the rivals' lowest price, the wholesale price and
    /// the neighbourhood's ceiling. The mod's own managers price a place by the same rule (DecidePrices).
    /// Cost: a few comparisons and a copy of each plan's prices an hour; the strategy's sums once a day a plan.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private readonly Dictionary<string, PricePlanFile> _pricePlans = new Dictionary<string, PricePlanFile>(8);
        private readonly List<string> _pricePlanGone = new List<string>(4);
        private readonly Dictionary<string, Dictionary<string, int>> _rivalLows = new Dictionary<string, Dictionary<string, int>>(4);   // district: product: cents, this hour
        private static readonly Dictionary<string, int> NoLows = new Dictionary<string, int>();
        private int _rivalLowsDay = -1, _rivalLowsHour = -1;
        private bool _pricePlansUnread, _pricePlansBroken;

        /// <summary>Whether a plan's strategy is kept and applied: the company running, no fault, the save's line read.</summary>
        internal bool PricePlansOn => _running && !_broken && !_pricePlansBroken && !_pricePlansUnread;

        internal PriceStrategy PlanStrategy(string id) => id != null && _pricePlans.TryGetValue(id, out PricePlanFile f) ? f.Strategy : default(PriceStrategy);

        /// <summary>Whether a plan has prices from before its last reprice to go back to.</summary>
        internal bool PlanHasYesterday(string id) => id != null && _pricePlans.TryGetValue(id, out PricePlanFile f) && f.Yesterday.Count > 0;

        /// <summary>
        /// Cuts the player's own plans by <paramref name="percent"/> and holds them, so the daily reprice does not put the
        /// prices back. Another player's plan is never touched. Returns how many prices moved.
        /// </summary>
        internal int CutPlans(int percent)
        {
            GameInstance save = Save;
            if (!PricePlansOn || save == null || save.pricingManagerPlans == null || percent <= 0 || percent >= 50)
            {
                return 0;
            }
            int changed = 0;
            for (int i = 0; i < save.pricingManagerPlans.Count; i++)
            {
                PricingManagerPlan plan = save.pricingManagerPlans[i];
                if (!PlanMine(plan) || plan.appliedPrices == null)
                {
                    continue;
                }
                PricePlanFile file = PlanFile(plan.id);
                PriceStrategy strategy = file.Strategy;
                strategy.Hold = true;
                file.Strategy = strategy;
                for (int p = 0; p < plan.appliedPrices.Count; p++)
                {
                    RetailPrice price = plan.appliedPrices[p];
                    if (price == null || string.IsNullOrEmpty(price.itemName) || price.price <= 0f || plan.IsManuallyPriced(price.itemName))
                    {
                        continue;
                    }
                    plan.ApplySuggestedPrice(price.itemName, price.price * (100 - percent) / 100f);
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>The hold a price cut put on the player's plans, taken off. A plan that was already held stays held.</summary>
        internal void ReleaseCut(string alreadyHeld)
        {
            GameInstance save = Save;
            if (!PricePlansOn || save == null || save.pricingManagerPlans == null)
            {
                return;
            }
            for (int i = 0; i < save.pricingManagerPlans.Count; i++)
            {
                PricingManagerPlan plan = save.pricingManagerPlans[i];
                if (!PlanMine(plan) || string.IsNullOrEmpty(plan.id) || (alreadyHeld != null && alreadyHeld.IndexOf(plan.id, StringComparison.Ordinal) >= 0))
                {
                    continue;
                }
                PricePlanFile file = PlanFile(plan.id);
                PriceStrategy strategy = file.Strategy;
                strategy.Hold = false;
                file.Strategy = strategy;
            }
        }

        /// <summary>A plan's strategy set from its page: kept, and the plan's prices worked out by it at once (the hold aside).</summary>
        internal void SetPlanStrategy(PricingManagerPlan plan, PriceStrategy s)
        {
            if (!PricePlansOn || plan == null || string.IsNullOrEmpty(plan.id) || !PlanMine(plan))
            {
                return;
            }
            try
            {
                PricePlanFile f = PlanFile(plan.id);
                f.Strategy = s;
                f.Day = plan.nextUpdateDay;
                f.Hour = plan.nextUpdateHour;
                PlanSnapshot(plan, f.Today);
                int changed = s.Prices && !s.Hold && !string.IsNullOrEmpty(plan.assignedEmployeeId) ? ApplyStrategy(plan, f, f.Today) : 0;
                PlanSnapshot(plan, f.Today);
                Logger?.Info("Company: the pricing plan of " + plan.supervisedNeighborhood + " now prices " + PlanWords(s) + "; " + changed + " price(s) changed now.");
                PricePlansSave(Save);
                Changed();
            }
            catch (Exception e)
            {
                PricePlansFault("a strategy set", e);
            }
        }

        /// <summary>Reset to yesterday: the plan's prices put back to where they stood before its last reprice, each kept managed or set by hand as it is now.</summary>
        internal int ResetPlan(PricingManagerPlan plan)
        {
            if (!PricePlansOn || plan == null || string.IsNullOrEmpty(plan.id) || !_pricePlans.TryGetValue(plan.id, out PricePlanFile f) || f.Yesterday.Count == 0
                || !PlanMine(plan))
            {
                return 0;
            }
            try
            {
                int changed = 0;
                foreach (KeyValuePair<string, int> p in f.Yesterday)
                {
                    if (CentsIn(plan.appliedPrices, p.Key) == p.Value)
                    {
                        continue;
                    }
                    if (plan.IsManuallyPriced(p.Key))
                    {
                        plan.ApplyManualPrice(p.Key, p.Value / 100f);
                    }
                    else
                    {
                        plan.ApplySuggestedPrice(p.Key, p.Value / 100f);
                    }
                    changed++;
                }
                PlanSnapshot(plan, f.Today);
                Logger?.Info("Company: the pricing plan of " + plan.supervisedNeighborhood + " was put back to its prices from before its last reprice: " + changed
                             + " price(s) changed.");
                PricePlansSave(Save);
                Changed();
                return changed;
            }
            catch (Exception e)
            {
                PricePlansFault("a reset to yesterday", e);
                return 0;
            }
        }

        partial void PricePlansLoad(GameInstance save)
        {
            string text = null;
            _pricePlans.Clear();
            _pricePlansBroken = false;
            _rivalLows.Clear();
            _rivalLowsDay = _rivalLowsHour = -1;
            _pricePlansUnread = save != null && save.modData != null && save.modData.TryGetValue(PricePlanStore.Key, out text) && !PricePlanStore.Read(text, _pricePlans);
            if (_pricePlansUnread)
            {
                SaveLines.Unreadable(save.modData, PricePlanStore.Key, "its version is not one this version of the mod reads");
                Logger?.Warn("Company: the pricing plans' strategies in this save are not of a version this one reads; every plan reprices as the game's own"
                             + " this session, and the line is kept in the save as it is.");
            }
            else if (Verbose)
            {
                Logger?.Info("Company check (pricing plans): " + _pricePlans.Count + " plan(s) with a strategy or their prices read from the save (version "
                             + PricePlanStore.Version + "): PASS");
            }
        }

        partial void PricePlansSave(GameInstance save)
        {
            if (_pricePlansUnread || save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            // A plan the game no longer has is let go.
            _pricePlanGone.Clear();
            List<PricingManagerPlan> plans = save.pricingManagerPlans;
            foreach (string id in _pricePlans.Keys)
            {
                bool there = false;
                for (int i = 0; plans != null && i < plans.Count && !there; i++)
                {
                    there = plans[i] != null && plans[i].id == id;
                }
                if (!there)
                {
                    _pricePlanGone.Add(id);
                }
            }
            for (int i = 0; i < _pricePlanGone.Count; i++)
            {
                _pricePlans.Remove(_pricePlanGone[i]);
            }
            string line = PricePlanStore.Write(_pricePlans);
            if (line == "v=" + PricePlanStore.Version)
            {
                save.modData.Remove(PricePlanStore.Key);
            }
            else
            {
                save.modData[PricePlanStore.Key] = line;
            }
        }

        // After the game's hour: each plan's prices are looked at, so the hour a plan reprices its prices from just before
        // are known. A plan whose next reprice moved has just repriced: those become yesterday's; a held plan's are put back;
        // a plan with a strategy has it applied; then its prices are looked at again.
        partial void PricePlansHour(int day, int hour)
        {
            List<PricingManagerPlan> plans = PricePlansOn && Save != null ? Save.pricingManagerPlans : null;
            if (plans == null)
            {
                return;
            }
            try
            {
                for (int i = 0; i < plans.Count; i++)
                {
                    PricingManagerPlan plan = plans[i];
                    if (plan == null || string.IsNullOrEmpty(plan.id))
                    {
                        continue;
                    }
                    PricePlanFile f = PlanFile(plan.id);
                    bool seen = f.Day >= 0, repriced = seen && (f.Day != plan.nextUpdateDay || f.Hour != plan.nextUpdateHour);
                    f.Day = plan.nextUpdateDay;
                    f.Hour = plan.nextUpdateHour;
                    if (!repriced || string.IsNullOrEmpty(plan.assignedEmployeeId))
                    {
                        PlanSnapshot(plan, f.Today);
                        continue;
                    }
                    f.Yesterday.Clear();
                    foreach (KeyValuePair<string, int> p in f.Today)
                    {
                        f.Yesterday[p.Key] = p.Value;
                    }
                    int changed = 0;
                    if ((f.Strategy.Hold || f.Strategy.Prices) && PlanMine(plan))
                    {
                        changed = f.Strategy.Hold ? HoldPlan(plan, f.Yesterday) : ApplyStrategy(plan, f, f.Yesterday);
                    }
                    PlanSnapshot(plan, f.Today);
                    if (changed > 0)
                    {
                        Logger?.Info("Company: after its daily reprice the pricing plan of " + plan.supervisedNeighborhood
                                     + (f.Strategy.Hold ? " kept its prices" : " priced " + PlanWords(f.Strategy)) + ": " + changed + " price(s).");
                    }
                }
            }
            catch (Exception e)
            {
                PricePlansFault("the hour", e);
            }
        }

        // The strategy over the plan's managed products (those set by hand stay), each against its price before.
        private int ApplyStrategy(PricingManagerPlan plan, PricePlanFile f, Dictionary<string, int> before)
        {
            List<PriceSuggestion> list = plan.cachedSuggestions;
            int changed = 0;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                PriceSuggestion sg = list[i];
                if (sg == null || string.IsNullOrEmpty(sg.itemName) || plan.IsManuallyPriced(sg.itemName) || sg.suggestedMax <= 0f)
                {
                    continue;
                }
                int now = CentsIn(plan.appliedPrices, sg.itemName);
                int yesterday = before.TryGetValue(sg.itemName, out int was) ? was : now;
                int want = PriceStrategyRules.Price(f.Strategy, Mathf.RoundToInt(sg.suggestedMax * 100f), sg.rivalReferencePrice > 0f ? Mathf.RoundToInt(sg.rivalReferencePrice * 100f) : 0,
                    WholesaleCents(sg.itemName), PlanCeiling(sg.itemName, plan.supervisedNeighborhood), yesterday);
                if (want > 0 && want != now)
                {
                    plan.ApplySuggestedPrice(sg.itemName, want / 100f);
                    changed++;
                }
            }
            return changed;
        }

        // A held plan: each managed product back at its price before the reprice.
        private static int HoldPlan(PricingManagerPlan plan, Dictionary<string, int> before)
        {
            int changed = 0;
            foreach (KeyValuePair<string, int> p in before)
            {
                if (!plan.IsManuallyPriced(p.Key) && CentsIn(plan.appliedPrices, p.Key) != p.Value)
                {
                    plan.ApplySuggestedPrice(p.Key, p.Value / 100f);
                    changed++;
                }
            }
            return changed;
        }

        private static void PlanSnapshot(PricingManagerPlan plan, Dictionary<string, int> into)
        {
            into.Clear();
            List<RetailPrice> prices = plan.appliedPrices;
            for (int i = 0; prices != null && i < prices.Count && into.Count < PricePlanStore.MostProducts; i++)
            {
                if (prices[i] != null && !string.IsNullOrEmpty(prices[i].itemName))
                {
                    into[prices[i].itemName] = Mathf.RoundToInt(prices[i].price * 100f);
                }
            }
        }

        private PricePlanFile PlanFile(string id)
        {
            if (!_pricePlans.TryGetValue(id, out PricePlanFile f))
            {
                f = new PricePlanFile();
                _pricePlans[id] = f;
            }
            return f;
        }

        /// <summary>
        /// A plan of the player's own whose every shop is the player's own too: Going Public puts another player's shops on this
        /// machine as rented by the player, and the plan's own price call would price theirs as well. Never per frame.
        /// </summary>
        internal static bool PlanMine(PricingManagerPlan plan)
        {
            BuildingRegistration hq = plan != null && plan.headquartersAddress != null ? BuildingHelper.GetBuildingRegistration(plan.headquartersAddress) : null;
            if (hq == null || !GoingPublic.Mine(hq))
            {
                return false;
            }
            if (!GoingPublic.InSession || Save == null || Save.BuildingRegistrations == null)
            {
                return true;
            }
            List<BuildingRegistration> all = Save.BuildingRegistrations;
            for (int i = 0; i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r != null && GameRegistrations.District(r) == plan.supervisedNeighborhood && GoingPublic.Foreign(r) && PricingManagerHelper.IsManageableBusiness(r))
                {
                    return false;
                }
            }
            return true;
        }

        private static int CentsIn(List<RetailPrice> prices, string item)
        {
            for (int i = 0; prices != null && i < prices.Count; i++)
            {
                if (prices[i] != null && prices[i].itemName == item)
                {
                    return Mathf.RoundToInt(prices[i].price * 100f);
                }
            }
            return -1;
        }

        /// <summary>A product's wholesale price today, cents; 0 for none.</summary>
        internal static int WholesaleCents(string item)
        {
            try
            {
                Item data = ItemsGetter.GetByName(item);
                return data != null ? Mathf.Max(0, Mathf.RoundToInt(data.GetWholesalePrice() * 100f)) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>The most a neighbourhood's customers pay for a product, as the game's pricing manager judges it, cents; 0 for not known.</summary>
        internal static int PlanCeiling(string item, string district)
        {
            try
            {
                return string.IsNullOrEmpty(district) ? 0 : Mathf.Max(0, PricingManagerHelper.GetHighestAcceptableCents(item, district));
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// The rivals' lowest price of each product in a district, cents, as the game's pricing plan finds it
        /// (PricingManagerPlan.BuildLowestRivalPrices): a shop not the player's that shows its prices, with the product on its
        /// shelves. Worked out once an hour a district at most, each district kept for the hour, for a place's manager pricing
        /// against the rivals: managers in several districts deciding in the same hour each read their own.
        /// </summary>
        internal Dictionary<string, int> RivalLows(string district)
        {
            if (Save == null || string.IsNullOrEmpty(district))
            {
                return NoLows;
            }
            if (_rivalLowsDay != Save.Day || _rivalLowsHour != Save.Hour)
            {
                _rivalLows.Clear();
                _rivalLowsDay = Save.Day;
                _rivalLowsHour = Save.Hour;
            }
            if (_rivalLows.TryGetValue(district, out Dictionary<string, int> lows))
            {
                return lows;
            }
            lows = new Dictionary<string, int>(64);
            _rivalLows[district] = lows;
            List<BuildingRegistration> all = Save.BuildingRegistrations;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r == null || r.RentedByPlayer || r.retailPrices == null || GameRegistrations.District(r) != district)
                {
                    continue;
                }
                var kind = BusinessTypeHelper.GetData(r);
                List<string> items = kind != null && !kind.HasTag(TagRef.Businesstag.hidepriceindex) ? r.GetListOfItemsForSale() : null;
                for (int k = 0; items != null && k < items.Count; k++)
                {
                    if (PlayerItemPurchaser.GetShelfFillState(items[k], r) <= 0f)
                    {
                        continue;
                    }
                    int cents = Mathf.RoundToInt(ItemHelper.GetPrice(items[k], r) * 100f);
                    if (cents > 0 && (!lows.TryGetValue(items[k], out int low) || cents < low))
                    {
                        lows[items[k]] = cents;
                    }
                }
            }
            return lows;
        }

        private static string PlanWords(PriceStrategy s)
        {
            return (s.Stance == PriceStance.Manager ? "by its manager's suggestion" : s.Stance == PriceStance.Undercut ? "under the rivals" : s.Stance == PriceStance.Match ? "at the rivals"
                       : "over the rivals")
                   + (s.Margin > 0 ? ", at least " + s.Margin + "% margin" : string.Empty) + (s.Ninety ? ", ending in .90" : string.Empty) + (s.OverCost ? ", never under cost" : string.Empty)
                   + (s.Step > 0 ? ", moving at most " + s.Step + "% a day" : string.Empty) + (s.Hold ? ", its daily reprice off" : string.Empty);
        }

        private void PricePlansFault(string where, Exception e)
        {
            _pricePlansBroken = true;
            Logger?.Error(e);
            Logger?.Warn("Company: the pricing plans' strategies switched off for this session after an error in " + where
                         + ". Every plan reprices as the game's own; the rest of the company is not affected.");
        }
    }
}
