using System.Collections.Generic;
using Entities;
using Extensions;
using Helpers;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.BizMan.PurchasingAgent;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the game's own goods rows of the Deliveries tab and the purchasing agent's plan say under each good: a shortage or a
    /// backorder it is in, and (in the game's panel that opens under the chosen row) the week's limit, the import index, what was
    /// ordered last week and what sold. The kit has the room to say it under every row. Read when a layout is made, from the
    /// game's own helpers as its cells call them.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private static readonly List<string> Noted = new List<string>(6);

        // A wholesale contract's good: its note, from the cell's own sums (BizManDeliveriesProductCellView.OpenBottomPart and SetData).
        private static string WholesaleNote(BizManDeliveriesProductModel g)
        {
            if (g == null || g.deliveryContractItem == null)
            {
                return null;
            }
            string item = g.deliveryContractItem.itemName;
            BuildingRegistration wholesaler = g.wholesalerAddress != null ? BuildingHelper.GetBuildingRegistration(g.wholesalerAddress) : null;
            BuildingRegistration business = g.businessAddress != null ? BuildingHelper.GetBuildingRegistration(g.businessAddress) : null;
            Noted.Clear();
            string warning = MarketWarning(item, wholesaler != null ? GameRegistrations.District(wholesaler) : null);
            if (warning != null)
            {
                Noted.Add(warning);
            }
            string limit = !DeliveryHelper.AreWholesaleAndImportLimitsDisabled() && g.deliveryContractItem.ItemCached != null
                ? (g.deliveryContractItem.ItemCached.maxWholesaleOrderAmount - g.deliveryContractItem.amountOrderedThisWeek).ToFormattedNumber() : "-";
            return Joined(item, limit, g.deliveryContractItem.amountOrderedLastWeek, business != null ? FinancialSummaryHelper.ProductsSoldLastWeekInRegistration(item, business) : 0);
        }

        // An import plan's good: the same, from the purchasing agent's cell (PurchasingAgentProductCellView.OpenBottomPart).
        private static string ImportNote(PurchasingAgentProductModel p)
        {
            if (p == null || p.productRef == null)
            {
                return null;
            }
            string item = p.productRef.itemName;
            Noted.Clear();
            string warning = MarketWarning(item, null);
            if (warning != null)
            {
                Noted.Add(warning);
            }
            string limit = "-";
            if (!DeliveryHelper.AreWholesaleAndImportLimitsDisabled() && p.productRef.ItemCached != null && DeliveryHelper.ShouldLimitImporterMaxAmount(item, p.importerAddress))
            {
                int left = p.productRef.ItemCached.maxOrderAmountPerImporter - ImportPartnership.GetItemAmountOrderedThisWeek(p.importerAddress, item);
                limit = (left > 0 ? left : 0).ToFormattedNumber();
            }
            return Joined(item, limit, p.productRef.amountOrderedLastWeek, FinancialSummaryHelper.ProductsSoldLastWeek(item));
        }

        // The shortage or backorder a good is in, in the game's own words for the kind of event; null when it is in neither.
        private static string MarketWarning(string item, string neighbourhood)
        {
            bool backorder = neighbourhood != null ? ProductMarketHelper.IsProductInMarketEvent(item, MarketEventType.ProductBackorder, neighbourhood)
                : ProductMarketHelper.IsProductInMarketEvent(item, MarketEventType.ProductBackorder);
            if (backorder)
            {
                return UiText.T("marketeventtype_productbackorder");
            }
            bool shortage = neighbourhood != null ? ProductMarketHelper.IsProductInMarketEvent(item, MarketEventType.ProductShortage, neighbourhood)
                : ProductMarketHelper.IsProductInMarketEvent(item, MarketEventType.ProductShortage);
            return shortage ? UiText.T("marketeventtype_productshortage") : null;
        }

        // The note's parts in the game's four labels ("Limit: 120", "Index: 1.2", "Previous: 80", "Sold: 40"), after any warning, as one line.
        private static string Joined(string item, string limit, int orderedLastWeek, int soldLastWeek)
        {
            ProductMarketEntry market = ProductMarketHelper.GetProductMarketEntry(item);
            Noted.Add(UiText.Fill("bizman_purchasingagents_products_weekly_limit", "weeklyLimit", limit));
            Noted.Add(UiText.Fill("bizman_purchasingagents_products_import_index", "importIndex", market != null ? market.importPriceIndex.ToString("F1") : "-"));
            Noted.Add(UiText.Fill("bizman_purchasingagents_products_ordered_last_week", "orderedLastWeek", orderedLastWeek.ToFormattedNumber()));
            Noted.Add(UiText.Fill("bizman_purchasingagents_products_sold_last_week", "soldLastWeek", soldLastWeek.ToFormattedNumber()));
            return string.Join(", ", Noted);
        }
    }
}
