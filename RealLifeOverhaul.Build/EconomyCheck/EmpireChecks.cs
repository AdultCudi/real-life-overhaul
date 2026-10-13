using RealLifeOverhaul;

// Living with Empire Motors, another author's dealership mod (Scripts/Core/EmpireMotorsLines): which of its transactions are
// a dealership's takings and which its costs, the share of one booked with no address, a car of its stock found in its
// saved records, and what the game's tax may deduct of a day.
internal static partial class Program
{
    private static void Empire()
    {
        // ---------------------------------------------------------------- what a transaction is
        Check(EmpireMotorsLines.Of("ba:transaction_itemsold", true, 1) == DealerLine.Takings && EmpireMotorsLines.Of("ba:transaction_itemsold", false, 0) == DealerLine.Takings,
            "empire: a car sold at a dealership's address, and a sale with a trade-in (no address, no data), are takings");
        Check(EmpireMotorsLines.Of("ba:transaction_itemsold", false, 1) == DealerLine.None,
            "empire: an item the player sells in the game's own way (no address, with data) is nobody's dealership's");
        Check(EmpireMotorsLines.Of("empiremotors:transaction_finance_payment", false, 0) == DealerLine.Takings
              && EmpireMotorsLines.Of("empiremotors:transaction_warranty_premium", false, 0) == DealerLine.Takings
              && EmpireMotorsLines.Of("empiremotors:transaction_vehicle_sale_refund", false, 0) == DealerLine.Takings,
            "empire: finance payments, warranty premiums and a sale given back are takings");
        Check(EmpireMotorsLines.Of("empiremotors:transaction_vehicle_wholesale_purchase", false, 0) == DealerLine.Stock
              && EmpireMotorsLines.Of("empiremotors:transaction_vehicle_used_refund", false, 0) == DealerLine.Stock
              && EmpireMotorsLines.Of("empiremotors:transaction_reconditioning_all", false, 0) == DealerLine.Stock
              && EmpireMotorsLines.Of("empiremotors:transaction_vehicle_service", false, 0) == DealerLine.Stock,
            "empire: cars bought, given back, reconditioned and serviced are the stock's cost");
        Check(EmpireMotorsLines.Of("empiremotors:transaction_dealership_supplies", false, 0) == DealerLine.Running
              && EmpireMotorsLines.Of("empiremotors:transaction_campaign_daily_refund", false, 0) == DealerLine.Running
              && EmpireMotorsLines.Of("empiremotors:transaction_warranty_claim", false, 0) == DealerLine.Running,
            "empire: supplies, campaigns and warranty claims are the running cost");
        Check(EmpireMotorsLines.Of("empiremotors:transaction_something_new", false, 0) == DealerLine.None && EmpireMotorsLines.Of("reallife_transaction_car_insurance", false, 0) == DealerLine.None
              && EmpireMotorsLines.Of("ba:transaction_revenue", true, 0) == DealerLine.None && EmpireMotorsLines.Of(null, false, 0) == DealerLine.None
              && EmpireMotorsLines.Of("empiremotors:transaction_", false, 0) == DealerLine.None,
            "empire: a type it may add later, this mod's own, the game's takings and no type at all are not guessed");
        Check(EmpireMotorsLines.RowOf(DealerLine.Stock) == EmpireMotorsLines.StockRow && EmpireMotorsLines.RowOf(DealerLine.Running) == EmpireMotorsLines.RunningRow
              && EmpireMotorsLines.RowOf(DealerLine.Takings) == null && EmpireMotorsLines.RowOf(DealerLine.None) == null
              && EmpireMotorsLines.StockRow.StartsWith("reallife_") && EmpireMotorsLines.RunningRow.StartsWith("reallife_"),
            "empire: each cost has its row among the goods, a row of the mod's (GameBooks keeps those)");

        // ---------------------------------------------------------------- a share of what has no address
        Check(EmpireMotorsLines.Share(30000f, 1) == 30000f && EmpireMotorsLines.Share(30000f, 2) == 15000f && EmpireMotorsLines.Share(-900f, 3) == -300f && EmpireMotorsLines.Share(500f, 0) == 0f,
            "empire: what was booked with no address is shared evenly, and nothing goes to no dealership");

        // ---------------------------------------------------------------- a car of its stock in its records
        const string saved = "{\"vehicleRecords\":[{\"RecordId\":\"r1\",\"VehicleInstanceId\":\"Ab+cD9/xYz0123456789qw\"},{\"VehicleInstanceId\":\"Kk0\\/pq\"}],\"Note\":\"Zz9\"}";
        Check(EmpireMotorsLines.Holds(saved, "Ab+cD9/xYz0123456789qw") && EmpireMotorsLines.Holds(saved, "Kk0/pq"),
            "empire: a vehicle among its records is found, with its slash as it is or written \\/");
        Check(!EmpireMotorsLines.Holds(saved, "Ab+cD9") && !EmpireMotorsLines.Holds(saved, "r") && !EmpireMotorsLines.Holds(saved, "Zz") && !EmpireMotorsLines.Holds(saved, "xYz0123456789qw"),
            "empire: a part of an id, or of another string, is not a car of its stock");
        Check(!EmpireMotorsLines.Holds(null, "a") && !EmpireMotorsLines.Holds(saved, null) && !EmpireMotorsLines.Holds(saved, "") && !EmpireMotorsLines.Holds("", "Kk0/pq"),
            "empire: no records, or no id, is no stock");

        // ---------------------------------------------------------------- a day of a dealership
        // Two cars sold (one at the dealership, one with a trade-in), a finance payment; two cars bought, one given back,
        // an inspection, supplies and a campaign refunded in part.
        float takings = 0f, stock = 0f, running = 0f;
        var day = new[]
        {
            ("ba:transaction_itemsold", true, 1, 42000f), ("ba:transaction_itemsold", false, 0, 18000f), ("empiremotors:transaction_finance_payment", false, 0, 650f),
            ("empiremotors:transaction_vehicle_wholesale_purchase", false, 0, -36000f), ("empiremotors:transaction_vehicle_used_purchase", false, 0, -14000f),
            ("empiremotors:transaction_vehicle_used_refund", false, 0, 14000f), ("empiremotors:transaction_vehicle_inspection", false, 0, -250f),
            ("empiremotors:transaction_dealership_supplies", false, 0, -1200f), ("empiremotors:transaction_campaign_daily_refund", false, 0, 100f),
            ("ba:transaction_itemsold", false, 2, 75f)
        };
        foreach (var t in day)
        {
            DealerLine line = EmpireMotorsLines.Of(t.Item1, t.Item2, t.Item3);
            takings += line == DealerLine.Takings ? t.Item4 : 0f;
            stock += line == DealerLine.Stock ? t.Item4 : 0f;
            running += line == DealerLine.Running ? t.Item4 : 0f;
        }
        Check(takings == 60650f && stock == -36250f && running == -1100f,
            "empire: a day's takings are the cars and the payment, its stock the cars kept and inspected, its running cost the supplies less the refund (got "
            + takings + ", " + stock + ", " + running + ")");
        Check(EmpireMotorsLines.Deductible(stock, running) == 37350f && EmpireMotorsLines.Deductible(500f, -200f) == 0f && EmpireMotorsLines.Deductible(0f, 0f) == 0f,
            "empire: the tax deducts what was spent less what came back, and never less than nothing");
        Check(takings - EmpireMotorsLines.Deductible(stock, running) == 23300f,
            "empire: so the tax counts the dealership's margin, not its takings");
    }
}
