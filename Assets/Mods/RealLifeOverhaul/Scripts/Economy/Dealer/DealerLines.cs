using System;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// What a transaction at one of the player's own dealerships is to its books (Economy/GameBooks), game-free. A car sold
    /// is booked as the game books a vehicle sold (ba:transaction_itemsold) and a car bought as the game books a vehicle
    /// bought (ba:transaction_vehiclebought), each with the dealership's address, so EconoView names them as the game's own;
    /// what the game has no type for (an instalment of the dealership's finance, a warranty, an inspection, the auction's
    /// fee, preparing a car, a claim) is a type of the mod's. Every cost is booked with the tax's deductible name at the
    /// moment it is paid, as the game marks its own business costs, so the game's tax deducts it itself.
    /// </summary>
    public static class DealerLines
    {
        public const string SoldType = "ba:transaction_itemsold";
        public const string BoughtType = "ba:transaction_vehiclebought";
        public const string FinanceKey = "reallife_transaction_dealer_finance";
        public const string PremiumKey = "reallife_transaction_dealer_premium";
        public const string RecoveryKey = "reallife_transaction_dealer_recovery";
        public const string InspectionKey = "reallife_transaction_dealer_inspection";
        public const string FeeKey = "reallife_transaction_dealer_fee";
        public const string PrepareKey = "reallife_transaction_dealer_prepare";
        public const string ClaimKey = "reallife_transaction_dealer_claim";
        public const string WorkshopKey = "reallife_transaction_dealer_workshop";
        public const string EventKey = "reallife_transaction_dealer_event";
        public const string ExtrasCostKey = "reallife_transaction_dealer_extras_cost";   // the extras sold, bought in
        public static readonly string[] ExtraKeys =   // each extra sold with a car, by DealerCatalogue's kinds
        {
            "reallife_transaction_dealer_extra_protection", "reallife_transaction_dealer_extra_service", "reallife_transaction_dealer_extra_accessories", "reallife_transaction_dealer_extra_wheels"
        };
        public const string StockRow = "reallife_books_dealer_stock";       // the row among the goods for cars and what they cost to buy
        public const string RunningRow = "reallife_books_dealer_running";   // ...and for warranty claims and events

        /// <summary>
        /// What a transaction booked at a dealership of the player's is: takings (a car sold, an instalment, a premium, a car
        /// taken back and sold at the auction, an extra), the stock's cost (a car bought, an inspection, the auction's fee,
        /// preparing one, the workshop, the extras bought in) or the running cost (a claim, an event). None for anything
        /// else booked there (the game's wages, rent and the rest).
        /// </summary>
        public static DealerLine Of(string type)
        {
            switch (type)
            {
                case SoldType:
                case FinanceKey:
                case PremiumKey:
                case RecoveryKey:
                    return DealerLine.Takings;
                case BoughtType:
                case InspectionKey:
                case FeeKey:
                case PrepareKey:
                case WorkshopKey:
                case ExtrasCostKey:
                    return DealerLine.Stock;   // (money back on one of them would come off the row)
                case ClaimKey:
                case EventKey:
                    return DealerLine.Running;
                default:
                    return Array.IndexOf(ExtraKeys, type) >= 0 ? DealerLine.Takings : DealerLine.None;
            }
        }

        /// <summary>The row of the goods a cost goes in; null for takings and for nothing.</summary>
        public static string RowOf(DealerLine line)
        {
            return line == DealerLine.Stock ? StockRow : line == DealerLine.Running ? RunningRow : null;
        }
    }
}
