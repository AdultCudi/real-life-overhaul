namespace RealLifeOverhaul.Economy
{
    /// <summary>The test tools of the agency's book: an owner's call for pitches now, and the month's fees now. Each opens the Agency tab on what it made.</summary>
    public sealed partial class EstateDirector
    {
        /// <summary>Options, Tests: an owner asks agencies to pitch for a block of flats now, as the day would draw one.</summary>
        public void TestBlockOffer()
        {
            const string test = "reallife_estate_test_block";
            if (!TestReady(test, EstatePart.Property))
            {
                return;
            }
            EstateGame.ReadAgency(_state.Agency);
            AgencyFile agency = _state.Agency;
            if (!agency.Running)
            {
                TestRefusals.TellKey(Logger, test, "reallife_estate_test_no_agency");
                return;
            }
            AgencyBook book = agency.Book;
            if (book.Count(BlockStage.Offered) >= AgencyBookCatalogue.OffersMost || book.Count(BlockStage.Held) >= AgencyBookCatalogue.BlocksMost)
            {
                TestRefusals.TellKey(Logger, test, "reallife_estate_test_block_full");
                return;
            }
            int was = book.LastOfferDay;
            ManagedBlock made = null;
            for (int n = 0; n < 12 && made == null; n++)
            {
                book.LastOfferDay = -1000;
                made = AgencyBookRules.Draw(agency, Landlords(), _state.Seed, Save.Day + n * 101 + book.NextId, _climate, _state.Real);
            }
            book.LastOfferDay = made != null ? Save.Day : was;
            if (made == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_estate_test_no_fit");
                return;
            }
            made.Until = Save.Day + AgencyBookCatalogue.OfferOpenDays;
            TestDone("an owner's call for pitches: " + made.Units + " flats in " + made.District + " of " + made.Owner + ", asking " + made.FeeAsk / 10f + "% of the rent, " + made.Bids.Count
                     + " rival agency(ies) pitching, decided on day " + made.Until, TabAgency, "blk:" + made.Id);
        }

        /// <summary>Options, Tests: the market's month turns for the agency's blocks now: each held block pays its fee, once, and the month is counted as done.</summary>
        public void TestBlockMonth()
        {
            const string test = "reallife_estate_test_block_month";
            if (!TestReady(test, EstatePart.Property))
            {
                return;
            }
            EstateGame.ReadAgency(_state.Agency);
            AgencyFile agency = _state.Agency;
            if (!agency.Running || agency.Book.Count(BlockStage.Held) == 0)
            {
                TestRefusals.TellKey(Logger, test, agency.Running ? "reallife_estate_test_block_none" : "reallife_estate_test_no_agency");
                return;
            }
            _blockEvents.Clear();
            int before = agency.Book.LastMonthFees;
            AgencyBookRules.MonthEnd(agency, System.Math.Max(_state.Months, agency.Book.Month) + 1, _blockEvents);
            BlockEvents(Save.Day);
            TestDone("the agency's month: " + agency.Book.LastMonthFees + " in management fees paid (" + before + " the month before), " + agency.Book.LastMonthPenalty + " held back",
                TabAgency, null);
        }
    }
}
