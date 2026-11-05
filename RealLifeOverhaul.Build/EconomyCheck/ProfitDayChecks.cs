using System;
using RealLifeOverhaul.Economy;

// Stock bought for a business and the day's result (Economy/Finance/DayRules.cs StockHeld, GameBooks.Result.cs Holds): the game's figure takes the wholesale
// cost of goods when they are sold, so the day they are bought must not take it a second time. A player's wholesaler purchase, an import delivery and a
// delivery contract are held; a shelf at a shop the player only shops at is the player's own spending and still counted.
internal static partial class Program
{
    // What the day's walk makes of a made-up purchase: (game type, amount, tax deductible, booked to a business with a statement).
    private static DayFigure StockDay(float game, (string type, float amount, bool deductible, bool statement)[] lines)
    {
        var tally = new ResultTally();
        for (int i = 0; i < lines.Length; i++)
        {
            bool held = DayRules.StockHeld(CashCategory.For(lines[i].type) == CashCategory.Stock, false, lines[i].deductible, lines[i].statement);
            tally.Add(lines[i].type, lines[i].amount, held);
        }
        return tally.Figure(12, game);
    }

    private static void ProfitDayChecks()
    {
        const string G = "ba:transaction_";
        DayFigure bought = StockDay(500f, new[]
        {
            (G + "itempurchase", -1200f, true, false),     // the player's basket at a wholesaler: booked to the wholesaler, tax deductible
            (G + "importdelivery", -800f, true, false),    // an importer's delivery: no address at all
            (G + "deliverycontract", -300f, true, true)    // a wholesaler's delivery to the business: booked to it
        });
        Check(bought.Counted == 0 && bought.Held == 3 && Math.Abs(bought.Mod - 500f) < 0.001f,
            "profit day: stock bought at a wholesaler, imported and delivered leaves the day's result at the game's 500 (held " + bought.Held + ", result " + bought.Mod + ")");

        DayFigure shopping = StockDay(500f, new[] { (G + "itempurchase", -40f, false, false) });
        Check(shopping.Counted == 1 && Math.Abs(shopping.Mod - 460f) < 0.001f, "profit day: groceries from a shop the player only shops at stay the player's own spending (" + shopping.Mod + ")");

        // the old rule held stock only when booked to a business with a statement: made to fail once
        DayFigure old = StockDay(500f, new[] { (G + "itempurchase", -1200f, false, false), (G + "importdelivery", -800f, false, false) });
        Check(Math.Abs(old.Mod - 500f) > 1f, "profit day: without the deductible mark the same purchases would cut the day by " + (500f - old.Mod) + " (made to fail once)");

        Check(!DayRules.StockHeld(true, true, true, true) && !DayRules.StockHeld(false, false, true, true),
            "profit day: the mod's own keys and anything that is not stock are never held by this rule");

        string books = DaySource(@"Economy\GameBooks.Result.cs");
        int rule = books.IndexOf("DayRules.StockHeld(", StringComparison.Ordinal);
        int noAddress = books.IndexOf("if (!here)", StringComparison.Ordinal);
        Check(rule > 0 && noAddress > rule && books.Contains("t.isTaxDeductible"),
            "profit day: the walk asks the stock rule before it gives up on a transaction with no address, and reads the game's tax deductible mark");
        Check(!books.Contains("return CashCategory.For(type) == CashCategory.Stock;"), "profit day: the old stock test that needed a business address is gone");
    }
}
