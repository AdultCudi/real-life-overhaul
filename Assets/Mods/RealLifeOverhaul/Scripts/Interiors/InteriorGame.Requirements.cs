using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Entities;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>The game's own rules for a business's type, read for the Interior tab.</summary>
    internal static partial class InteriorGame
    {
        /// <summary>The game's rules for the business's type, wants and needs, each met or not, with the items that meet it or would.</summary>
        public static void Rules(BuildingRegistration reg, List<GameRule> into)
        {
            into.Clear();
            BusinessType type = BusinessTypeHelper.GetData(reg);
            if (type == null)
            {
                return;
            }
            float weight = 0f;
            try
            {
                weight = NeighborhoodHelper.GetData(reg.Neighborhood).customerDemandsWeight;
            }
            catch (Exception)
            {
                weight = 0f;
            }
            for (int i = 0; type.customerDemandSets != null && i < type.customerDemandSets.Count; i++)
            {
                CustomerDemandSet set = type.customerDemandSets[i];
                if (set == null || string.IsNullOrEmpty(set.type))
                {
                    continue;
                }
                var rule = new GameRule { Demand = true, Kind = set.type, Chance = Mathf.RoundToInt(Mathf.Clamp01(weight * set.weight) * 100f) };
                rule.Met = reg.cachedFulfilledCustomerDemands != null && reg.cachedFulfilledCustomerDemands.Contains(set.type);
                CustomerDemand demand = null;
                try
                {
                    demand = CustomerDemandHelper.GetByType(set.type);
                }
                catch (Exception)
                {
                    demand = null;
                }
                rule.Class = demand != null ? demand.GetType().Name : string.Empty;
                Func<BigAmbitions.Items.Item, bool> meets = Meets(rule.Class);
                if (meets != null)
                {
                    Named(reg, meets, rule);
                }
                if (demand is InteriorDesignCustomerDemand)
                {
                    rule.Have = InteriorScore(reg);
                    rule.Need = Minimum(reg.Neighborhood);
                }
                into.Add(rule);
            }
            for (int i = 0; type.businessRequirements != null && i < type.businessRequirements.Count; i++)
            {
                BusinessRequirement need = type.businessRequirements[i];
                if (need == null)
                {
                    continue;
                }
                var rule = new GameRule { Demand = false, Class = need.GetType().Name };
                try
                {
                    rule.Kind = need.GetLocalizeKey();
                    rule.Met = BusinessHelper.IsRequirementMet(reg, need);
                    string[] items = need.GetItems();
                    ItemType of = need.GetItemType();
                    if (need is SpecificItemsInBuildingBySqm bySqm)
                    {
                        rule.Need = bySqm.GetRequiredItemCount(reg);
                    }
                    else if (need is ItemsOfTypeInBuildingBySqm typeBySqm)
                    {
                        rule.Need = typeBySqm.GetRequiredItemCount(reg);
                    }
                    if (items != null && items.Length > 0)
                    {
                        var wanted = new HashSet<string>(items, StringComparer.Ordinal);
                        Named(reg, item => wanted.Contains(item.itemName), rule);
                    }
                    else if (of != 0)
                    {
                        Named(reg, item => (item.type & of) != 0, rule);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                into.Add(rule);
            }
        }

        // The game's own test of each kind of want, as a test of one item: what each CustomerDemand looks for.
        private static Func<BigAmbitions.Items.Item, bool> Meets(string demandClass)
        {
            switch (demandClass)
            {
                case "MusicCustomerDemand": return item => (item.type & ItemType.RadioSource) != 0;
                case "SeatingCustomerDemand": return item => (item.type & ItemType.Seat) != 0;
                case "SinkCustomerDemand": return item => (item.type & ItemType.Sink) != 0;
                case "ToiletCustomerDemand": return item => (item.type & ItemType.Toilet) != 0;
                case "ToiletPrivacyCustomerDemand": return item => item.HasTag(TagRef.Itemtag.isprivacytoilet);
                case "WorkoutVarietyCustomerDemand": return item => (item.type & ItemType.WorkoutMachine) != 0;
                default: return null;
            }
        }

        // The placed items that meet a rule (how many, and the names of a few), or a few of the game's own that would.
        private static void Named(BuildingRegistration reg, Func<BigAmbitions.Items.Item, bool> meets, GameRule rule)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            if (reg.itemInstances != null)
            {
                foreach (ItemInstance instance in reg.itemInstances.Values)
                {
                    BigAmbitions.Items.Item item = instance != null ? instance.ItemCached : null;
                    if (item == null || !meets(item))
                    {
                        continue;
                    }
                    count++;
                    if (seen.Add(item.itemName) && rule.Placed.Count < GameRule.NamesMost)
                    {
                        rule.Placed.Add(ItemName(item.itemName));
                    }
                }
            }
            if (rule.Have < 0)
            {
                rule.Have = count;
            }
            if (count > 0 || ItemsGetter.AllItems == null)
            {
                return;
            }
            var names = new List<string>(8);
            foreach (BigAmbitions.Items.Item item in ItemsGetter.AllItems)
            {
                if (item != null && item.isFurniture && (item.type & (ItemType.RetailProduct | ItemType.ServiceProduct | ItemType.ForSale)) == 0 && !item.HasTag(TagRef.Itemtag.dev)
                    && meets(item) && seen.Add(item.itemName))
                {
                    names.Add(ItemName(item.itemName));
                }
            }
            names.Sort(StringComparer.CurrentCulture);
            for (int i = 0; i < names.Count && rule.Examples.Count < GameRule.NamesMost; i++)
            {
                rule.Examples.Add(names[i]);
            }
        }

        /// <summary>An item's name as the game shows it.</summary>
        public static string ItemName(string item)
        {
            return string.IsNullOrEmpty(item) ? "?" : LocalizorManager.IsLocalizedKey(item) ? item.GetLocalization() : item;
        }
    }
}
