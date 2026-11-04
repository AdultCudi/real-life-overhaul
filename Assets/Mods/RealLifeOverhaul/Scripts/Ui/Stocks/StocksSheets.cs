namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Which sheet of the Stocks desk is up, and the slots of its figures. Every old Stocks app action is one of these sheets
    /// or a button of its page; <see cref="Actions"/> lists them against the old app's, for the checks. Game-free.
    /// </summary>
    public static class StocksSheets
    {
        public const byte None = 0, BondBuy = 1, Ladder = 2, BondHeld = 3, Material = 4, ShareOption = 5, Contract = 6, Firm = 7, Rule = 8, Order = 9,
            Margin = 10, Publish = 11, Founder = 12, Own = 13, Alert = 14, Ticket = 15;

        // The slots of a bond's sheet.
        public const int Amount = 0, Reinvest = 1, Roll = 2;
        // A future's or an option's.
        public const int What = 0, Week = 1, Strike = 2, Qty = 3, Place = 4;
        // A firm's.
        public const int Capital = 0, Size = 1, Stop = 2, Loss = 3, Scope = 4, PickAt = 5, PickOn = 6, PayPct = 7, Every = 8, Change = 9;
        // A rule's.
        public const int RuleStock = 0, RuleKind = 1, Level = 2, Gain = 3, OrderKind = 4, Budget = 5, PerTrade = 6, RuleStop = 7, OpenOnly = 8, RuleReinvest = 9, Tell = 10, MaxOpen = 11;
        // The order the ticket sheet asks about: the company, buying (1) or selling (0), its kind (market, limit, stop loss, take profit), a typed price in cents (0: the quote's),
        // how long it lasts (0 the day, 1 until cancelled) and the shares.
        public const int TStock = 0, TBuy = 1, TKind = 2, TPrice = 3, TLife = 4, TQty = 5;
        // A price alert's: the level in cents, and above (0) or below (1).
        public const int AlertLevel = 0, AlertKindAt = 1;
        // The margin account's and the founder's.
        public const int Sum = 0, Side = 1, Float = 2, Walk = 3, Name = 4;

        /// <summary>
        /// What the old Stocks app lets a player do, by the kit's handler: the page, the old tab's action and the sheet or button
        /// that does it. The check wants every line to have a sheet or a button.
        /// </summary>
        public static readonly string[] Actions =
        {
            "bond buy:BondBuy", "bond ladder:Ladder", "bond sell early:BondHeld", "bond close deposit:BondHeld", "bond reinvest:BondHeld", "bond rollover:BondHeld",
            "future open:Material", "future sell:Material", "option call material:Material", "option put material:Material", "option on shares:ShareOption",
            "contract close:Contract", "contract sell back:Contract", "margin call pay:Page", "future delivery place:Material",
            "firm hire:Firm", "firm dismiss:Firm", "firm restart:Firm", "firm sell up:Firm", "firm settings:Firm", "firm shares:Firm", "firm payouts:Firm",
            "firm capital add:Firm", "firm capital take:Firm",
            "rule add:Rule", "rule edit:Rule", "rule pause:Rule", "rule remove:Rule",
            "order cancel:Order", "margin deposit:Margin", "margin withdraw:Margin", "margin open:Margin",
            "ticket confirm:Ticket", "alert set:Alert", "alert under a drawn line:Page", "alert remove:Page", "portfolio views:Page", "market orders box:Page",
            "search:Page", "watch:Page", "ticket limit:Page", "ticket stop loss:Page", "ticket take profit:Page", "ticket put back:Page", "ticket worked:Page",
            "listing engage:Own", "listing audit:Own", "listing withdraw:Own", "listing publish:Publish", "listing name:Publish",
            "founder pay:Founder", "founder dividend:Founder", "founder buy back:Founder", "founder issue:Founder", "founder private:Founder", "founder deal:Founder"
        };
    }
}
