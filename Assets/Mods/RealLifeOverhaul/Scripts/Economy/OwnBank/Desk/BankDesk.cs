namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's trading desk (Economy/OwnBank/Desk), game-free so the offline checks and the Guide read one table. Two businesses on the stock
    /// market: brokerage for the bank's clients, which earns a commission on their volume as the market's activity moves it (and on options and
    /// futures when the exchange has them), and the bank's own book, a limited share of its capital held in the market's companies by a mandate,
    /// marked to the market's real prices each day and cut when its loss from its peak passes the owner's risk limit. Money in cents at the
    /// start's prices unless said; shares and factors in thousandths.
    /// </summary>
    public static class BankDesk
    {
        // The mandates of the own book. Saved as a number: never renumber.
        public const int None = 0, Index = 1, Income = 2, Growth = 3, Mandates = 4;
        public static readonly string[] MandateKeys = { "reallife_ob_desk_none", "reallife_ob_desk_index", "reallife_ob_desk_income", "reallife_ob_desk_growth" };
        public static readonly int[] Companies = { 0, 10, 8, 8 };               // held, an even part each
        public static readonly int[] Distress = { 0, 100, 150, 350 };            // the most distress a company held may show, thousandths
        public const int ShareMost = 250, ShareStep = 50;                        // the book's share of the capital, thousandths: a quarter of it at most
        public const int ShareDefault = 100;
        public const int LimitLeast = 50, LimitMost = 250, LimitStep = 25, LimitDefault = 100;   // the loss from its peak that cuts it, thousandths of its value
        public const int CutKeep = 500;                                          // a cut keeps this share of the book, thousandths
        public const int CutMonths = 1;                                          // game months after a cut with no buying
        public const int Drift = 200;                                            // thousandths off its part before a holding is traded back, as the fund's

        // The market's activity as the desk reads it: thousandths of an ordinary day's (the market's own variance), within bounds.
        public const int ActivityLeast = 500, ActivityMost = 2500;

        // Brokerage for the clients: what they hold with the bank for trading, how often they trade it, and the commission.
        public static readonly int[] Custody = { 350, 80 };                      // of a kind of saver's deposits that is held for trading, thousandths: people trade more
        public static readonly int[] Turnover = { 3000, 1500 };                  // a year's trading in what is held, thousandths of it: a retail client turns it over about three times
        public const int CommissionBp = 25;                                      // the bank's commission on clients' trades, hundredths of a per cent
        public static readonly int[] DerivShare = { 60, 150 };                   // of the custody that goes into options and futures when the exchange has them, thousandths
        public const int DerivCommissionBp = 60;                                 // and the commission on their notional: a future or an option costs more to deal
        public const int MarginActivity = 1500;                                  // a market livelier than this makes clients' margin calls fail now and then...
        public const int MarginLossBp = 15;                                      // ...losing the bank this share of the derivatives' notional, hundredths of a per cent, at twice that activity
        public const long DayCents = 15000L;                                     // the desk's systems and analysts a day: $150
        public const int CustodyCost = 3;                                        // thousandths of the custody a year: settlement, research and the exchange's fees

        // What the regulator counts the book at.
        public const int Weight = OwnBankCatalogue.FundWeight;

        public static int MandateOf(int mandate) => mandate < None ? None : mandate >= Mandates ? Mandates - 1 : mandate;
    }
}
