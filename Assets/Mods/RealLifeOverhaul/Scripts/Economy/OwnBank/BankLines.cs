namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A day of the bank as the lines of the headquarters' statement it is booked in, in cents (OwnBankGame.BookDay): the takings split into the loans' and fund's,
    /// the cards' and the desk's, and the costs into the savers', the losses', the cards', the funding's and the desk's. Each part of a day is in exactly one line,
    /// so the Company app's books, the game's tax and the stock market's view of a listed bank (BankDay.Income, BankDay.Profit) all count it once.
    /// Game-free: EconomyCheck adds the lines up against the day.
    /// </summary>
    public struct BankLines
    {
        public long Takings, Cards, Desk;                              // in
        public long Savers, Losses, CardCosts, Funding, DeskCosts;      // out
        public long Wages, Systems, Fine;                              // out, as before

        public long In => Takings + Cards + Desk;
        public long Out => Savers + Losses + CardCosts + Funding + DeskCosts + Wages + Systems + Fine;

        public static BankLines Of(BankDay d)
        {
            long cards = d.CardInterest + d.CardInterchange + d.CardFees, desk = d.Brokerage + d.Derivs;
            return new BankLines
            {
                Takings = d.Income - cards - desk, Cards = cards, Desk = desk,
                Savers = d.Savers, Losses = d.Lost + d.Court + d.FundLost + d.Fees + d.SaleLoss, CardCosts = d.CardLost + d.CardFraud + d.CardCosts, Funding = d.Funding,
                DeskCosts = d.DeskCosts, Wages = d.Staff, Systems = d.Systems, Fine = d.Fine
            };
        }
    }
}
