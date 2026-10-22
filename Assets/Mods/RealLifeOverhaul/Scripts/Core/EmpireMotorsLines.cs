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
    /// The game-free side of Empire Motors (Core/EmpireMotors): which of its transactions are takings and which costs,
    /// and whether a vehicle's id is in its save text. Checked by EconomyCheck. From its decompiled code: a car sold is
    /// "ba:transaction_itemsold" with the dealership's address, a trade-in sale the same type with no address and no
    /// data; the rest are types of its own with no address, which neither the game's statement nor its tax sees.
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
        /// What a transaction is to a dealership. The caller checks that a sale's address is a dealership's. A type of
        /// a later version, not in the table, is not guessed.
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

        /// <summary>One dealership's equal share of what was booked with no address.</summary>
        public static float Share(float amount, int dealerships)
        {
            return dealerships > 0 ? amount / dealerships : 0f;
        }

        /// <summary>
        /// True if a vehicle's id is in Empire Motors' save text (Odin's JSON): between quotes, a slash also as "\/"
        /// (the game's ids are base 64).
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
        /// The day's costs net of refunds, which the game's tax may deduct, from the sums as booked (spent negative);
        /// never below 0.
        /// </summary>
        public static float Deductible(float stock, float running)
        {
            float spent = -(stock + running);
            return spent > 0f ? spent : 0f;
        }
    }
}
