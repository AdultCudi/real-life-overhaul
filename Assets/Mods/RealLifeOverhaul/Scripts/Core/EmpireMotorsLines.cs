using System;

namespace RealLifeOverhaul
{
    /// <summary>What a transaction of Empire Motors' is to the books of one of its dealerships.</summary>
    public enum DealerLine
    {
        None,      // not the dealership's, or not a kind this mod knows
        Takings,   // money the dealership took: a car sold, a finance payment, a warranty premium (a refund of one is negative)
        Stock,     // what its cars cost: bought, inspected, reconditioned, serviced (a refund is positive)
        Running    // what running it cost besides: supplies, campaigns, a specialism, warranty claims
    }

    /// <summary>
    /// The arithmetic of living with Empire Motors (Core/EmpireMotors) that needs nothing of the game's: which of its
    /// transactions are a dealership's takings and which its costs, by their type, and whether a vehicle's id is among
    /// the records it keeps in the save. Game-free: the economy's command-line check compiles this file too.
    ///
    /// Read in its decompiled code: a car sold to a customer is paid with the game's own type "ba:transaction_itemsold" and
    /// the dealership's address, its one transaction with an address; a sale with a trade-in is paid with the same type,
    /// no address and no data (every item the game itself sells carries data). Everything else it books under types of
    /// its own, "empiremotors:transaction_...", with no address and none of the game's categories, so neither the game's
    /// statement nor its tax sees any of it.
    /// </summary>
    public static class EmpireMotorsLines
    {
        public const string SoldType = "ba:transaction_itemsold";
        public const string Prefix = "empiremotors:transaction_";
        public const string StockRow = "reallife_books_empire_stock";       // the statement's row among the goods for the cars
        public const string RunningRow = "reallife_books_empire_running";   // and for the rest of what running it costs

        private static readonly string[] TakingsTypes = { "finance_payment", "warranty_premium", "vehicle_sale_refund", "tradein_sale_refund" };

        private static readonly string[] StockTypes =
        {
            "vehicle_wholesale_purchase", "vehicle_wholesale_refund", "vehicle_used_purchase", "vehicle_used_refund", "vehicle_inspection",
            "vehicle_inspection_refund", "inventory_inspection", "inventory_inspection_refund", "reconditioning", "reconditioning_refund",
            "reconditioning_all", "reconditioning_all_refund", "vehicle_service"
        };

        private static readonly string[] RunningTypes =
        {
            "dealership_supplies", "dealership_supplies_refund", "ad_campaign", "ad_campaign_refund", "campaign_daily", "campaign_daily_refund",
            "specialization", "specialization_refund", "warranty_claim"
        };

        /// <summary>
        /// What a transaction is to a dealership, by its type, whether it has an address and how many entries its data has.
        /// A sold item with an address is a dealership's takings only if that address is a dealership's, which the caller
        /// knows. A type of Empire Motors' this table does not have is nobody's (a later version's: it is not guessed).
        /// </summary>
        public static DealerLine Of(string type, bool hasAddress, int dataCount)
        {
            if (string.IsNullOrEmpty(type))
            {
                return DealerLine.None;
            }
            if (type == SoldType)
            {
                return hasAddress || dataCount == 0 ? DealerLine.Takings : DealerLine.None;
            }
            if (!type.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return DealerLine.None;
            }
            string kind = type.Substring(Prefix.Length);
            return Array.IndexOf(TakingsTypes, kind) >= 0 ? DealerLine.Takings
                : Array.IndexOf(StockTypes, kind) >= 0 ? DealerLine.Stock
                : Array.IndexOf(RunningTypes, kind) >= 0 ? DealerLine.Running : DealerLine.None;
        }

        /// <summary>The row among a statement's goods a cost goes under; null for takings and for nothing.</summary>
        public static string RowOf(DealerLine line)
        {
            return line == DealerLine.Stock ? StockRow : line == DealerLine.Running ? RunningRow : null;
        }

        /// <summary>One dealership's share of what was booked with no address: the same for each of them.</summary>
        public static float Share(float amount, int dealerships)
        {
            return dealerships > 0 ? amount / dealerships : 0f;
        }

        /// <summary>
        /// Whether a vehicle's id is among Empire Motors' records, in the text it saves them as (Odin's JSON). The id is
        /// looked for between quotes, as every string there is written, and with a slash written as "\/" too (the game's
        /// ids are base 64, which has slashes).
        /// </summary>
        public static bool Holds(string saved, string id)
        {
            if (string.IsNullOrEmpty(saved) || string.IsNullOrEmpty(id))
            {
                return false;
            }
            if (saved.IndexOf("\"" + id + "\"", StringComparison.Ordinal) >= 0)
            {
                return true;
            }
            return id.IndexOf('/') >= 0 && saved.IndexOf("\"" + id.Replace("/", "\\/") + "\"", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// What the day's costs of the dealerships come to once refunds are taken off them: what the game's tax may deduct.
        /// 'stock' and 'running' are the day's sums as booked (spent negative, refunded positive). Never below nothing.
        /// </summary>
        public static float Deductible(float stock, float running)
        {
            float spent = -(stock + running);
            return spent > 0f ? spent : 0f;
        }
    }
}
